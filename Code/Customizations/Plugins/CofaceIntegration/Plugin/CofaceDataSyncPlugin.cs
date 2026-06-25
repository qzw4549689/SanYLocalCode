using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using SanyD365.Plugins.CofaceIntegration;
using SanyD365.Plugins.CofaceIntegration.Api;
using SanyD365.Plugins.CofaceIntegration.Parser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;

namespace SanyD365.Plugins.CofaceIntegration.Plugin
{
    /// <summary>
    /// Coface数据集成Plugin
    /// 触发时机：信用评估记录Update后，状态变为11(内外部数据集成)时
    /// 功能：
    /// 1. 查询URBA360监控订单
    /// 2. 获取URBA360内容并解析9个指标
    /// 3. 查询Full Report订单
    /// 4. 获取Full Report内容并解析3个指标
    /// 5. 写入客户信用标签表
    /// 6. 记录数据集成结果（不修改状态，状态流转由BPF流程控制）
    /// </summary>
    public class CofaceDataSyncPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = factory.CreateOrganizationService(context.UserId);
            ITracingService tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracer.Trace("===== CofaceDataSyncPlugin 开始执行 =====");

            // 严格校验：只处理Update后事件
            if (context.MessageName != "Update" || context.Stage != 40)
            {
                tracer.Trace("非Update后事件，跳过");
                return;
            }

            // 获取Target实体
            if (!context.InputParameters.Contains("Target") || !(context.InputParameters["Target"] is Entity))
            {
                tracer.Trace("未找到Target实体");
                return;
            }

            Entity target = (Entity)context.InputParameters["Target"];

            if (target.LogicalName != "mcs_credit_record")
            {
                tracer.Trace($"实体不匹配: {target.LogicalName}");
                return;
            }

            // 检查状态是否变为11(内外部数据集成)
            if (!target.Contains("mcs_status"))
            {
                tracer.Trace("状态未变更，跳过");
                return;
            }

            int status = target.GetAttributeValue<OptionSetValue>("mcs_status")?.Value ?? 0;
            if (status != 11)
            {
                tracer.Trace($"状态不是11(内外部数据集成)，当前状态={status}，跳过");
                return;
            }

            tracer.Trace("状态=11，开始Coface数据集成");

            try
            {
                // 获取完整记录信息
                Entity creditRecord = service.Retrieve("mcs_credit_record", target.Id,
                    new ColumnSet("mcs_scoreid", "mcs_accountid", "mcs_custnameen", "mcs_countrycode", "mcs_cofaceid"));

                string scoreId = creditRecord.GetAttributeValue<string>("mcs_scoreid");
                string cofaceId = creditRecord.GetAttributeValue<string>("mcs_cofaceid");
                string countryCode = creditRecord.GetAttributeValue<string>("mcs_countrycode");
                string custNameEn = creditRecord.GetAttributeValue<string>("mcs_custnameen");

                tracer.Trace($"评估记录: scoreId={scoreId}, cofaceId={cofaceId}, country={countryCode}");

                // 校验必要参数
                if (string.IsNullOrEmpty(cofaceId))
                {
                    throw new InvalidPluginExecutionException("科法斯客户代码(cofaceId)为空，无法获取数据");
                }

                if (string.IsNullOrEmpty(countryCode))
                {
                    throw new InvalidPluginExecutionException("国家编码为空，无法获取数据");
                }

                // 初始化API服务
                var apiService = new CofaceApiService(service, tracer);
                var urbaParser = new Urba360Parser(tracer, service, countryCode);
                var reportParser = new FullReportParser(tracer, service);

                // ========== 步骤1: 获取URBA360数据 ==========
                tracer.Trace("步骤1: 获取URBA360数据");
                string urbaOrderId = null;
                string urbaJson = null;
                UrbaOrderStatus urbaStatus;
                var urbaData = GetUrba360Data(apiService, urbaParser, cofaceId, countryCode, tracer, out urbaOrderId, out urbaJson, out urbaStatus);

                // ========== 步骤2: 获取Full Report数据 ==========
                tracer.Trace("步骤2: 获取Full Report数据");
                string reportOrderId = null;
                string publicationId = null;
                string reportJson = null;
                ReportOrderStatus reportStatus;
                var reportData = GetFullReportData(service, apiService, reportParser, cofaceId, countryCode, tracer, out reportOrderId, out publicationId, out reportJson, out reportStatus);

                // 检查订单状态，如果有订单未就绪，记录警告信息
                string statusWarning = "";
                if (urbaStatus == UrbaOrderStatus.NotFound)
                    statusWarning += "URBA360:未下单;";
                else if (urbaStatus == UrbaOrderStatus.NotReady)
                    statusWarning += "URBA360:订单未就绪;";

                if (reportStatus == ReportOrderStatus.NotFound)
                    statusWarning += "Report:未下单;";
                else if (reportStatus == ReportOrderStatus.NotReady)
                    statusWarning += "Report:订单未就绪;";

                // ========== 步骤3: 合并数据并写入标签表 ==========
                tracer.Trace("步骤3: 写入客户信用标签表");
                var allData = new Dictionary<string, object>();
                foreach (var kvp in urbaData) allData[kvp.Key] = kvp.Value;
                foreach (var kvp in reportData) allData[kvp.Key] = kvp.Value;

                // 先查询评分卡配置（读操作，不会触发事务问题）
                var scoringCardItems = GetScoringCardItems(service, tracer, creditRecord);
                tracer.Trace($"评分卡配置项数={scoringCardItems.Count}");

                WriteToCustomerTags(service, tracer, target.Id, scoreId, creditRecord, allData, scoringCardItems);

                // 反写外部评级到客户主数据
                UpdateCustomerMasterDataExternalRate(service, tracer, creditRecord, allData);

                // ========== 步骤3.5: 下载并保存 Coface Report PDF 附件 ==========
                tracer.Trace("步骤3.5: 保存 Coface Report 附件");
                string attachmentMsg = "";
                try
                {
                    if (reportStatus == ReportOrderStatus.Ready && !string.IsNullOrEmpty(publicationId))
                    {
                        SaveCofaceReportAttachment(service, tracer, apiService, creditRecord, publicationId, scoreId);
                        attachmentMsg = "Report附件已保存";
                    }
                    else
                    {
                        attachmentMsg = $"Report附件未保存(状态:{reportStatus})";
                    }
                }
                catch (Exception attachEx)
                {
                    attachmentMsg = $"Report附件保存失败:{attachEx.Message}";
                    tracer.Trace(attachmentMsg);
                    // 不中断主流程，错误信息记录到评估记录 API 消息中
                }

                // ========== 步骤4: 一次性更新评估记录（合并所有字段更新） ==========
                tracer.Trace("步骤4: 记录数据集成结果");
                var updateRecord = new Entity("mcs_credit_record")
                {
                    Id = target.Id
                };
                // 订单信息
                if (!string.IsNullOrEmpty(urbaOrderId))
                {
                    updateRecord["mcs_urba360id"] = urbaOrderId;
                    updateRecord["mcs_urbastatus"] = "ready";
                }
                if (!string.IsNullOrEmpty(reportOrderId))
                {
                    updateRecord["mcs_rptorderid"] = reportOrderId;
                }
                if (!string.IsNullOrEmpty(publicationId))
                {
                    updateRecord["mcs_publicationid"] = publicationId;
                    updateRecord["mcs_rptstatus"] = "ready";
                }
                // JSON数据（截断到4000字符）
                if (!string.IsNullOrEmpty(urbaJson))
                {
                    updateRecord["mcs_urbajson"] = urbaJson.Length > 4000 ? urbaJson.Substring(0, 4000) : urbaJson;
                }
                if (!string.IsNullOrEmpty(reportJson))
                {
                    updateRecord["mcs_reportjson"] = reportJson.Length > 4000 ? reportJson.Substring(0, 4000) : reportJson;
                }
                // API状态
                updateRecord["mcs_abidate"] = DateTime.Now;
                updateRecord["mcs_api_status"] = "SUCCESS";
                updateRecord["mcs_api_name"] = "CofaceDataSync";
                
                // 构建API消息：包含订单状态警告和附件状态
                string apiMsg = $"URBA360+Full Report数据集成完成，共{allData.Count}个指标，标签{scoringCardItems.Count}项";
                if (!string.IsNullOrEmpty(statusWarning))
                {
                    apiMsg += $" [警告:{statusWarning}]";
                }
                if (!string.IsNullOrEmpty(attachmentMsg))
                {
                    apiMsg += $" [{attachmentMsg}]";
                }
                updateRecord["mcs_api_msg"] = apiMsg;

                service.Update(updateRecord);

                tracer.Trace("===== CofaceDataSyncPlugin 执行完成 =====");
            }
            catch (Exception ex)
            {
                tracer.Trace($"Coface数据集成失败: {ex.Message}");

                // 记录错误信息到评估记录
                try
                {
                    var errorRecord = new Entity("mcs_credit_record")
                    {
                        Id = target.Id
                    };
                    errorRecord["mcs_api_status"] = "ERROR";
                    errorRecord["mcs_api_name"] = "CofaceDataSync";
                    errorRecord["mcs_api_msg"] = ex.Message.Length > 2000 ? ex.Message.Substring(0, 2000) : ex.Message;
                    service.Update(errorRecord);
                }
                catch
                {
                    // 记录错误失败不影响主异常抛出
                }

                throw new InvalidPluginExecutionException($"Coface数据集成失败: {ex.Message}");
            }
        }

        #region URBA360数据获取

        /// <summary>
        /// URBA360订单状态
        /// </summary>
        private enum UrbaOrderStatus
        {
            NotFound,       // 未找到订单
            NotReady,       // 订单存在但状态未就绪
            Ready           // 订单就绪可取数
        }

        /// <summary>
        /// 获取URBA360数据
        /// </summary>
        private Dictionary<string, object> GetUrba360Data(
            CofaceApiService apiService,
            Urba360Parser parser,
            string cofaceId,
            string countryCode,
            ITracingService tracer,
            out string orderId,
            out string rawJson,
            out UrbaOrderStatus orderStatus)
        {
            var result = new Dictionary<string, object>();
            orderId = null;
            rawJson = null;
            orderStatus = UrbaOrderStatus.NotFound;

            try
            {
                // 1. 查询URBA360监控订单
                tracer.Trace("查询URBA360监控订单...");
                using (var ordersDoc = apiService.GetUrbaMonitoringOrders(cofaceId, countryCode))
                {
                    // 保存订单查询的原始JSON
                    try
                    {
                        rawJson = ordersDoc.RootElement.GetRawText();
                        tracer.Trace($"URBA订单查询JSON获取成功，长度: {rawJson.Length}");
                    }
                    catch (Exception jsonEx)
                    {
                        tracer.Trace($"获取URBA订单查询JSON失败: {jsonEx.Message}");
                    }
                    
                    var orderInfo = ExtractUrbaOrderInfo(ordersDoc, tracer);
                    orderId = orderInfo.OrderId;
                    orderStatus = orderInfo.Status;

                    if (orderStatus == UrbaOrderStatus.NotFound)
                    {
                        tracer.Trace("未找到URBA360监控订单，该客户可能尚未下单");
                        FillUrbaMissingValues(result);
                        return result;
                    }

                    if (orderStatus == UrbaOrderStatus.NotReady)
                    {
                        tracer.Trace($"URBA360订单存在但状态未就绪: {orderInfo.StatusDetail}");
                        FillUrbaMissingValues(result);
                        return result;
                    }

                    tracer.Trace($"找到URBA360订单(状态就绪): {orderId}");

                    // 2. 获取URBA360内容
                    tracer.Trace("获取URBA360内容...");
                    using (var contentDoc = apiService.GetUrbaContent(orderId))
                    {
                        // 保存原始JSON（通过out参数返回，由调用方统一更新）
                        try
                        {
                            rawJson = contentDoc.RootElement.GetRawText();
                            tracer.Trace($"URBA JSON获取成功，长度: {rawJson.Length}");
                        }
                        catch (Exception jsonEx)
                        {
                            tracer.Trace($"获取URBA JSON失败(不影响数据解析): {jsonEx.Message}");
                        }

                        // 3. 解析数据
                        result = parser.Parse(contentDoc);
                    }
                }
            }
            catch (Exception ex)
            {
                tracer.Trace($"获取URBA360数据异常: {ex.Message}");
                FillUrbaMissingValues(result);
            }

            return result;
        }

        /// <summary>
        /// 填充URBA360缺失值
        /// </summary>
        private void FillUrbaMissingValues(Dictionary<string, object> result)
        {
            result["ExternalRating"] = "O";
            result["LatePaymentIndex"] = null;
            result["CountryRisk"] = "O";
            result["SectorRisk"] = "O";
            result["NaceCodes"] = "O";
            result["NetAssets"] = null;
            result["DebtRatio"] = null;
            result["CurrentRatio"] = null;
            result["NetProfitMargin"] = null;
        }

        /// <summary>
        /// URBA360订单信息
        /// </summary>
        private class UrbaOrderInfo
        {
            public string OrderId { get; set; }
            public UrbaOrderStatus Status { get; set; }
            public string StatusDetail { get; set; }
        }

        /// <summary>
        /// 从订单列表中提取URBA360订单信息（包含状态判断）
        /// </summary>
        private UrbaOrderInfo ExtractUrbaOrderInfo(JsonDocument ordersDoc, ITracingService tracer)
        {
            var result = new UrbaOrderInfo { Status = UrbaOrderStatus.NotFound };

            try
            {
                var root = ordersDoc.RootElement;
                List<JsonElement> orders = new List<JsonElement>();

                // 收集所有订单
                if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (var order in root.EnumerateArray())
                        orders.Add(order);
                }
                else if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("orders", out var ordersArray))
                    {
                        foreach (var order in ordersArray.EnumerateArray())
                            orders.Add(order);
                    }
                    else if (root.TryGetProperty("id", out _))
                    {
                        orders.Add(root);
                    }
                }

                if (orders.Count == 0)
                {
                    tracer.Trace("订单列表为空，该客户尚未下单");
                    return result;
                }

                // 检查每个订单的状态
                foreach (var order in orders)
                {
                    string orderId = null;
                    string statusStr = null;

                    if (order.TryGetProperty("id", out var idProp))
                        orderId = idProp.GetString();

                    // URBA360 订单状态字段是 "urbaStatus"，不是 "status"
                    if (order.TryGetProperty("urbaStatus", out var status))
                        statusStr = status.GetString();
                    else if (order.TryGetProperty("status", out var status2))
                        statusStr = status2.GetString();

                    tracer.Trace($"检查订单: id={orderId}, status={statusStr}");

                    // 状态为ready或partially_ready → 订单就绪
                    if (statusStr == "ready" || statusStr == "partially_ready")
                    {
                        result.OrderId = orderId;
                        result.Status = UrbaOrderStatus.Ready;
                        result.StatusDetail = statusStr;
                        return result;
                    }
                    // 状态为空或不存在，但有id → 兼容处理，视为就绪（Coface某些环境不返回status）
                    else if (string.IsNullOrEmpty(statusStr) && !string.IsNullOrEmpty(orderId))
                    {
                        tracer.Trace($"订单status为空，按兼容逻辑视为就绪");
                        result.OrderId = orderId;
                        result.Status = UrbaOrderStatus.Ready;
                        result.StatusDetail = "empty_status";
                        return result;
                    }
                    // 状态存在但不为ready → 未就绪
                    else if (!string.IsNullOrEmpty(orderId))
                    {
                        result.OrderId = orderId;
                        result.Status = UrbaOrderStatus.NotReady;
                        result.StatusDetail = statusStr ?? "unknown";
                    }
                }

                // 有订单但都不是就绪状态
                if (result.Status == UrbaOrderStatus.NotReady)
                {
                    tracer.Trace($"找到{orders.Count}个订单，但状态均未就绪");
                }
            }
            catch (Exception ex)
            {
                tracer.Trace($"提取URBA订单信息异常: {ex.Message}");
            }

            return result;
        }

        #endregion

        #region Full Report数据获取

        /// <summary>
        /// Full Report订单状态
        /// </summary>
        private enum ReportOrderStatus
        {
            NotFound,       // 未找到订单
            NotReady,       // 订单存在但状态未就绪
            Ready           // 订单就绪可取数
        }

        /// <summary>
        /// Full Report订单信息
        /// </summary>
        private class ReportOrderInfo
        {
            public string OrderId { get; set; }
            public string PublicationId { get; set; }
            public ReportOrderStatus Status { get; set; }
            public string StatusDetail { get; set; }
        }

        /// <summary>
        /// 获取Full Report数据
        /// </summary>
        private Dictionary<string, object> GetFullReportData(
            IOrganizationService service,
            CofaceApiService apiService,
            FullReportParser parser,
            string cofaceId,
            string countryCode,
            ITracingService tracer,
            out string reportOrderId,
            out string publicationId,
            out string rawJson,
            out ReportOrderStatus orderStatus)
        {
            var result = new Dictionary<string, object>();
            reportOrderId = null;
            publicationId = null;
            rawJson = null;
            orderStatus = ReportOrderStatus.NotFound;

            try
            {
                // 根据国家选择对应的 Report 产品
                var countryConfig = CofaceCountryConfigHelper.GetConfig(service);
                var reportProduct = countryConfig.GetReportProduct(countryCode);
                tracer.Trace($"国家{countryCode}对应的Report产品: slug={reportProduct.Slug}, productCode={reportProduct.ProductCode ?? "(null)"}");

                // 1. 查询Report订单
                tracer.Trace("查询Full Report订单...");
                using (var ordersDoc = apiService.GetReportOrders(cofaceId, countryCode, reportProduct.Slug, reportProduct.ProductCode))
                {
                    // 保存订单查询的原始JSON
                    try
                    {
                        rawJson = ordersDoc.RootElement.GetRawText();
                        tracer.Trace($"Report订单查询JSON获取成功，长度: {rawJson.Length}");
                    }
                    catch (Exception jsonEx)
                    {
                        tracer.Trace($"获取Report订单查询JSON失败: {jsonEx.Message}");
                    }
                    
                    var orderInfo = ExtractReportOrderInfo(ordersDoc, tracer, reportProduct.Slug, reportProduct.ProductCode);
                    reportOrderId = orderInfo.OrderId;
                    publicationId = orderInfo.PublicationId;
                    orderStatus = orderInfo.Status;

                    if (orderStatus == ReportOrderStatus.NotFound)
                    {
                        tracer.Trace("未找到Full Report订单，该客户可能尚未下单");
                        FillReportMissingValues(result);
                        return result;
                    }

                    if (orderStatus == ReportOrderStatus.NotReady)
                    {
                        tracer.Trace($"Full Report订单存在但状态未就绪: {orderInfo.StatusDetail}");
                        FillReportMissingValues(result);
                        return result;
                    }

                    tracer.Trace($"找到Full Report订单(状态就绪): {publicationId}");

                    // 2. 获取Report内容
                    tracer.Trace("获取Full Report内容...");
                    using (var contentDoc = apiService.GetReportContent(publicationId))
                    {
                        // 保存原始JSON（通过out参数返回，由调用方统一更新）
                        try
                        {
                            rawJson = contentDoc.RootElement.GetRawText();
                            tracer.Trace($"Report JSON获取成功，长度: {rawJson.Length}");
                        }
                        catch (Exception jsonEx)
                        {
                            tracer.Trace($"获取Report JSON失败(不影响数据解析): {jsonEx.Message}");
                        }

                        // 3. 解析数据
                        result = parser.Parse(contentDoc);
                    }
                }
            }
            catch (Exception ex)
            {
                tracer.Trace($"获取Full Report数据异常: {ex.Message}");
                FillReportMissingValues(result);
            }

            return result;
        }

        /// <summary>
        /// 填充Full Report缺失值
        /// </summary>
        private void FillReportMissingValues(Dictionary<string, object> result)
        {
            result["RegisteredCapital"] = null;
            result["EstablishedYear"] = null;
            result["LitigationCount"] = null;
        }

        /// <summary>
        /// 下载并保存 Coface Full Report PDF 到 mcs_customer_file
        /// 文件类型固定为 2（客户资信报告），同时关联 account 和 mcs_credit_record
        /// 通过平台通用上传 API 写入 Blob，不再直接保存 Base64 到 mcs_filebyte，避免 Memo 字段 4000 字符限制
        /// </summary>
        private void SaveCofaceReportAttachment(
            IOrganizationService service,
            ITracingService tracer,
            CofaceApiService apiService,
            Entity creditRecord,
            string publicationId,
            string scoreId)
        {
            tracer.Trace($"SaveCofaceReportAttachment: publicationId={publicationId}, scoreId={scoreId}");

            // 1. 准备附件记录基础信息
            var accountRef = creditRecord.GetAttributeValue<EntityReference>("mcs_accountid");
            if (accountRef == null)
            {
                throw new InvalidPluginExecutionException("评估记录未关联客户，无法保存 Coface Report 附件");
            }

            string fileName = $"Coface_Report_{scoreId}_{DateTime.Now:yyyyMMdd}.pdf";

            // 2. 防重复：同一评估记录 + 同一文件名 已存在则不重复创建
            var existingQuery = new QueryExpression("mcs_customer_file")
            {
                ColumnSet = new ColumnSet("mcs_customer_fileid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_credit_recordid", ConditionOperator.Equal, creditRecord.Id),
                        new ConditionExpression("mcs_filename", ConditionOperator.Equal, fileName)
                    }
                },
                TopCount = 1
            };

            var existing = service.RetrieveMultiple(existingQuery);
            if (existing.Entities.Count > 0)
            {
                tracer.Trace("Coface Report 附件已存在，跳过创建");
                return;
            }

            // 3. 下载 PDF
            byte[] pdfBytes = apiService.GetReportPdf(publicationId);
            if (pdfBytes == null || pdfBytes.Length == 0)
            {
                throw new InvalidPluginExecutionException("Coface PDF 下载结果为空");
            }

            tracer.Trace($"Coface PDF 下载完成: {pdfBytes.Length} bytes");

            // 4. 通过平台通用上传 API 上传到 Blob
            string uploadId = UploadFileToMcsPlatform(service, tracer, creditRecord.Id, fileName, pdfBytes);

            // 5. 创建 mcs_customer_file 记录（不保存 Base64 到 mcs_filebyte，避免 Memo 字段长度限制）
            var attachment = new Entity("mcs_customer_file");
            attachment["mcs_accountid"] = accountRef;
            attachment["mcs_credit_recordid"] = new EntityReference("mcs_credit_record", creditRecord.Id);
            attachment["mcs_filename"] = fileName;
            attachment["mcs_filetype"] = new OptionSetValue(2); // 2 = 客户资信报告
            attachment["mcs_api_fileid"] = uploadId;
            attachment["mcs_api_status"] = "SUCCESS";
            attachment["mcs_api_msg"] = $"Coface Full Report PDF uploaded via platform API, publicationId={publicationId}, originalSize={pdfBytes.Length} bytes, uploadId={uploadId}";

            var fileId = service.Create(attachment);
            tracer.Trace($"Coface Report 附件创建成功: ID={fileId}, filename={fileName}, uploadId={uploadId}");
        }

        /// <summary>
        /// 通过平台通用上传 Custom API 上传文件到 Blob
        /// 流程：mcs_InitUploadFile -> HTTP PUT -> mcs_CommitUploadFile
        /// </summary>
        private string UploadFileToMcsPlatform(
            IOrganizationService service,
            ITracingService tracer,
            Guid creditRecordId,
            string fileName,
            byte[] fileBytes)
        {
            // 4.1 InitUploadFile
            var initRequest = new OrganizationRequest("mcs_InitUploadFile");
            initRequest["EntityName"] = "mcs_credit_record";
            initRequest["EntityID"] = creditRecordId.ToString();
            initRequest["FileName"] = fileName;
            initRequest["Type"] = "002"; // 客户资信报告

            var initResponse = service.Execute(initRequest);
            string initResult = initResponse.Results.ContainsKey("Result")
                ? initResponse.Results["Result"]?.ToString()
                : null;

            if (string.IsNullOrEmpty(initResult))
            {
                throw new InvalidPluginExecutionException("mcs_InitUploadFile 未返回 Result");
            }

            string uploadId = ParseUploadIdFromInitResult(initResult);
            string uploadUrl = ExtractFileUrlFromInitResult(initResult);

            if (string.IsNullOrEmpty(uploadId))
            {
                throw new InvalidPluginExecutionException($"未能从 mcs_InitUploadFile 结果解析上传 ID: {initResult}");
            }
            if (string.IsNullOrEmpty(uploadUrl))
            {
                throw new InvalidPluginExecutionException($"未能从 mcs_InitUploadFile 结果解析上传 URL: {initResult}");
            }

            tracer.Trace($"mcs_InitUploadFile 成功: uploadId={uploadId}, uploadUrl={uploadUrl}");

            // 4.2 HTTP PUT 上传文件内容到 Blob
            UploadBytesToBlobUrl(tracer, uploadUrl, fileBytes);

            // 4.3 CommitUploadFile
            var commitRequest = new OrganizationRequest("mcs_CommitUploadFile");
            commitRequest["EntityName"] = "mcs_credit_record";
            commitRequest["EntityID"] = creditRecordId.ToString();
            commitRequest["ID"] = uploadId;

            service.Execute(commitRequest);
            tracer.Trace($"mcs_CommitUploadFile 成功: uploadId={uploadId}");

            return uploadId;
        }

        /// <summary>
        /// 通过 HTTP PUT 上传字节数组到 Blob URL
        /// </summary>
        private void UploadBytesToBlobUrl(ITracingService tracer, string uploadUrl, byte[] fileBytes)
        {
            // URL 通常已包含 comp=appendblock；若未包含再追加
            string appendUrl = uploadUrl.IndexOf("comp=appendblock", StringComparison.OrdinalIgnoreCase) >= 0
                ? uploadUrl
                : (uploadUrl.Contains("?") ? $"{uploadUrl}&comp=appendblock" : $"{uploadUrl}?comp=appendblock");

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(appendUrl);
            request.Method = "PUT";
            request.ContentType = "application/pdf";
            request.Headers.Add("x-ms-blob-type", "AppendBlob");
            request.ContentLength = fileBytes.Length;
            request.Timeout = 120000; // Blob 上传 120 秒超时

            using (Stream requestStream = request.GetRequestStream())
            {
                requestStream.Write(fileBytes, 0, fileBytes.Length);
            }

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                tracer.Trace($"Blob 上传响应: Status={response.StatusCode}, Length={response.ContentLength}");
                if (response.StatusCode != HttpStatusCode.Created && response.StatusCode != HttpStatusCode.OK)
                {
                    throw new InvalidPluginExecutionException($"Blob 上传失败: Status={response.StatusCode}");
                }
            }
        }

        /// <summary>
        /// 从 mcs_InitUploadFile 返回结果中解析上传 ID
        /// 兼容纯 GUID 或 JSON 格式
        /// </summary>
        private string ParseUploadIdFromInitResult(string result)
        {
            if (string.IsNullOrEmpty(result))
            {
                return null;
            }

            // 直接是 GUID
            if (Guid.TryParse(result, out _))
            {
                return result;
            }

            // 尝试 JSON 解析
            try
            {
                using (var doc = JsonDocument.Parse(result))
                {
                    var root = doc.RootElement;
                    foreach (var propName in new[] { "id", "ID", "Id", "fileId", "UploadId", "uploadId" })
                    {
                        if (root.TryGetProperty(propName, out var prop) && prop.ValueKind == JsonValueKind.String)
                        {
                            string value = prop.GetString();
                            if (!string.IsNullOrEmpty(value))
                            {
                                return value;
                            }
                        }
                    }
                }
            }
            catch
            {
                // 不是 JSON，按无法解析处理
            }

            return null;
        }

        /// <summary>
        /// 从 mcs_InitUploadFile 返回结果中解析 Blob 上传 URL
        /// </summary>
        private string ExtractFileUrlFromInitResult(string result)
        {
            if (string.IsNullOrEmpty(result))
            {
                return null;
            }

            try
            {
                using (var doc = JsonDocument.Parse(result))
                {
                    if (doc.RootElement.TryGetProperty("FileUrl", out var prop) && prop.ValueKind == JsonValueKind.String)
                    {
                        return prop.GetString();
                    }
                }
            }
            catch
            {
                // 不是 JSON
            }

            return null;
        }

        /// <summary>
        /// 从订单列表中提取Full Report订单信息（包含状态判断）
        /// 只匹配指定 reportSlug 和 customReportId 的 publication
        /// </summary>
        private ReportOrderInfo ExtractReportOrderInfo(JsonDocument ordersDoc, ITracingService tracer, string expectedSlug, string expectedProductCode)
        {
            var result = new ReportOrderInfo { Status = ReportOrderStatus.NotFound };

            try
            {
                var root = ordersDoc.RootElement;
                List<JsonElement> orders = new List<JsonElement>();

                // 收集所有订单
                if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (var order in root.EnumerateArray())
                        orders.Add(order);
                }
                else if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("orders", out var ordersArray))
                    {
                        foreach (var order in ordersArray.EnumerateArray())
                            orders.Add(order);
                    }
                    else if (root.TryGetProperty("id", out _))
                    {
                        orders.Add(root);
                    }
                }

                if (orders.Count == 0)
                {
                    tracer.Trace("订单列表为空，该客户尚未下单");
                    return result;
                }

                string normalizedExpectedSlug = expectedSlug?.ToUpperInvariant();
                string normalizedExpectedProductCode = expectedProductCode?.ToUpperInvariant();

                // 检查每个订单的状态
                foreach (var order in orders)
                {
                    string statusStr = null;
                    string orderId = null;
                    string matchedPubId = null;

                    if (order.TryGetProperty("status", out var status))
                        statusStr = status.GetString();

                    // 获取订单ID
                    if (order.TryGetProperty("id", out var idProp))
                    {
                        orderId = idProp.GetString();
                    }

                    // 从publications数组中匹配符合 expectedSlug + expectedProductCode 的 Publication
                    if (order.TryGetProperty("publications", out var publications))
                    {
                        foreach (var pub in publications.EnumerateArray())
                        {
                            string pubSlug = null;
                            if (pub.TryGetProperty("reportSlug", out var slugProp))
                                pubSlug = slugProp.GetString();

                            string pubProductCode = null;
                            if (pub.TryGetProperty("customReportId", out var codeProp))
                                pubProductCode = codeProp.ToString();

                            // 匹配规则：slug 必须一致；如果期望 productCode 不为空，则 productCode 也必须一致
                            bool slugMatch = !string.IsNullOrEmpty(pubSlug) &&
                                             pubSlug.ToUpperInvariant() == normalizedExpectedSlug;
                            bool codeMatch = string.IsNullOrEmpty(normalizedExpectedProductCode) ||
                                             (!string.IsNullOrEmpty(pubProductCode) &&
                                              pubProductCode.ToUpperInvariant() == normalizedExpectedProductCode);

                            if (slugMatch && codeMatch)
                            {
                                if (pub.TryGetProperty("id", out var pubIdProp))
                                {
                                    matchedPubId = pubIdProp.GetString();
                                    tracer.Trace($"匹配到Publication: slug={pubSlug}, customReportId={pubProductCode}, pubId={matchedPubId}");
                                    break;
                                }
                            }
                        }
                    }

                    // 兼容旧格式：如果订单本身没有 publications，但 reportSlug / customReportId 在 order 层级
                    if (string.IsNullOrEmpty(matchedPubId))
                    {
                        string orderSlug = null;
                        string orderProductCode = null;
                        if (order.TryGetProperty("reportSlug", out var orderSlugProp))
                            orderSlug = orderSlugProp.GetString();
                        if (order.TryGetProperty("customReportId", out var orderCodeProp))
                            orderProductCode = orderCodeProp.ToString();

                        bool slugMatch = !string.IsNullOrEmpty(orderSlug) &&
                                         orderSlug.ToUpperInvariant() == normalizedExpectedSlug;
                        bool codeMatch = string.IsNullOrEmpty(normalizedExpectedProductCode) ||
                                         (!string.IsNullOrEmpty(orderProductCode) &&
                                          orderProductCode.ToUpperInvariant() == normalizedExpectedProductCode);

                        if (slugMatch && codeMatch && !string.IsNullOrEmpty(orderId))
                        {
                            matchedPubId = orderId;
                            tracer.Trace($"订单层级匹配到产品: slug={orderSlug}, customReportId={orderProductCode}, pubId={matchedPubId}");
                        }
                    }

                    // 如果都没匹配到，跳过该订单
                    if (string.IsNullOrEmpty(matchedPubId))
                    {
                        tracer.Trace($"订单 {orderId} 未匹配到指定产品，跳过");
                        continue;
                    }

                    tracer.Trace($"检查订单: orderId={orderId}, pubId={matchedPubId}, status={statusStr}");

                    // 状态为ready或delivered → 订单就绪
                    if (statusStr == "ready" || statusStr == "delivered")
                    {
                        result.OrderId = orderId;
                        result.PublicationId = matchedPubId;
                        result.Status = ReportOrderStatus.Ready;
                        result.StatusDetail = statusStr;
                        return result;
                    }
                    // 状态为空或不存在，但有orderId → 兼容处理，视为就绪
                    else if (string.IsNullOrEmpty(statusStr) && !string.IsNullOrEmpty(orderId))
                    {
                        tracer.Trace($"订单status为空，按兼容逻辑视为就绪");
                        result.OrderId = orderId;
                        result.PublicationId = matchedPubId;
                        result.Status = ReportOrderStatus.Ready;
                        result.StatusDetail = "empty_status";
                        return result;
                    }
                    // 状态存在但不为ready → 未就绪
                    else if (!string.IsNullOrEmpty(orderId))
                    {
                        result.OrderId = orderId;
                        result.PublicationId = matchedPubId;
                        result.Status = ReportOrderStatus.NotReady;
                        result.StatusDetail = statusStr ?? "unknown";
                    }
                }

                // 有订单但都不是就绪状态，或没有匹配产品的订单
                if (result.Status == ReportOrderStatus.NotReady)
                {
                    tracer.Trace($"找到{orders.Count}个订单，但状态均未就绪");
                }
                else
                {
                    tracer.Trace($"找到{orders.Count}个订单，但没有匹配指定产品的就绪订单");
                }
            }
            catch (Exception ex)
            {
                tracer.Trace($"提取Report订单信息异常: {ex.Message}");
            }

            return result;
        }

        #endregion

        #region 写入客户信用标签表

        /// <summary>
        /// Coface字段名到D365评分项目编码的映射
        /// Key=Coface Parser输出的字段名, Value=D365评分项目编码(mcs_credit_itemsno)
        /// </summary>
        private static readonly Dictionary<string, string> CofaceToD365Mapping = new Dictionary<string, string>
        {
            ["ExternalRating"] = "ExternalRating",
            ["LatePaymentIndex"] = "LatePaymentIndex",
            ["CountryRisk"] = "CountryRisk",
            ["SectorRisk"] = "SectorRisk",
            ["NaceCodes"] = "Sectors",
            ["NetAssets"] = "NetAssets",
            ["DebtRatio"] = "DebtRatio",
            ["CurrentRatio"] = "CurrentRatio",
            ["NetProfitMargin"] = "NetProfit",
            ["RegisteredCapital"] = "RegisteredCapital",
            ["EstablishedYear"] = "RegistrationDate",
            ["LitigationCount"] = "LegalEvents"
        };

        /// <summary>
        /// 将指标数据写入客户信用标签表
        /// 根据评分卡配置动态创建标签，只创建评分卡中配置的指标
        /// </summary>
        private void WriteToCustomerTags(
            IOrganizationService service,
            ITracingService tracer,
            Guid creditRecordId,
            string scoreId,
            Entity creditRecord,
            Dictionary<string, object> data,
            List<ScoringCardItem> scoringCardItems)
        {
            if (scoringCardItems == null || scoringCardItems.Count == 0)
            {
                tracer.Trace("评分卡配置为空，跳过标签创建");
                return;
            }

            // 遍历评分卡配置项，创建/更新标签
            int createdCount = 0;
            foreach (var item in scoringCardItems)
            {
                string itemCode = item.ItemCode;      // D365评分项目编码
                int dataType = item.DataType;          // 数据类型
                string itemName = item.ItemName;       // 评分项目名称

                try
                {
                    object value;
                    EntityReference creditItemValueRef = null;

                    if (itemCode == "BigAccount")
                    {
                        // 客户评级：从客户主数据自动取值，不依赖 Coface
                        var bigAccountValue = GetBigAccountValue(service, tracer, creditRecord);
                        value = bigAccountValue.ListValue;
                        creditItemValueRef = bigAccountValue.EnumRef;
                    }
                    else
                    {
                        // 查找Coface数据中是否有该指标的值
                        value = GetCofaceValue(itemCode, data);
                    }

                    // 查找或创建标签记录
                    Guid? tagId = FindOrCreateTag(service, tracer, creditRecordId, scoreId, creditRecord, itemCode, dataType);

                    if (tagId.HasValue)
                    {
                        // 更新指标值
                        UpdateTagValue(service, tracer, tagId.Value, dataType, value, itemCode, creditItemValueRef);
                        createdCount++;
                    }
                }
                catch (Exception ex)
                {
                    tracer.Trace($"写入指标 {itemCode}({itemName}) 失败: {ex.Message}");
                }
            }

            tracer.Trace($"标签写入完成: 成功{createdCount}/{scoringCardItems.Count}项");
        }

        /// <summary>
        /// 查询评分卡配置项列表
        /// 在Plugin中执行查询是安全的（读操作不会触发事务问题）
        /// </summary>
        private List<ScoringCardItem> GetScoringCardItems(IOrganizationService service, ITracingService tracer, Entity creditRecord)
        {
            var result = new List<ScoringCardItem>();

            try
            {
                // 获取关联的客户ID
                Guid? accountId = null;
                if (creditRecord.Contains("mcs_accountid") && creditRecord["mcs_accountid"] is EntityReference accountRef)
                {
                    accountId = accountRef.Id;
                }

                if (!accountId.HasValue)
                {
                    tracer.Trace("评估记录未关联客户，无法匹配评分卡");
                    return result;
                }

                // 查询Account找到关联的客户主数据，客户属性统一从 mcs_customermasterdata 读取
                var account = service.Retrieve("account", accountId.Value, new ColumnSet("mcs_customermasterdata"));

                int accountCategory = 0;
                int accountLevel = 0;
                int accountType = 0;

                if (account.Contains("mcs_customermasterdata") && account["mcs_customermasterdata"] is EntityReference cmRef)
                {
                    var customerMasterData = service.Retrieve("mcs_customermasterdata", cmRef.Id,
                        new ColumnSet("mcs_accountcategory", "mcs_accountlevel", "mcs_accounttype"));
                    accountCategory = customerMasterData.GetAttributeValue<OptionSetValue>("mcs_accountcategory")?.Value ?? 0;
                    accountLevel = customerMasterData.GetAttributeValue<OptionSetValue>("mcs_accountlevel")?.Value ?? 0;
                    accountType = customerMasterData.GetAttributeValue<OptionSetValue>("mcs_accounttype")?.Value ?? 0;
                    tracer.Trace($"从客户主数据读取属性: category={accountCategory}, level={accountLevel}, type={accountType}");
                }
                else
                {
                    tracer.Trace("account 未关联 mcs_customermasterdata，尝试从 account 本身读取属性（兼容）");
                    var accountFallback = service.Retrieve("account", accountId.Value,
                        new ColumnSet("mcs_accountcategory", "mcs_accountlevel", "mcs_accounttype"));
                    accountCategory = accountFallback.GetAttributeValue<OptionSetValue>("mcs_accountcategory")?.Value ?? 0;
                    accountLevel = accountFallback.GetAttributeValue<OptionSetValue>("mcs_accountlevel")?.Value ?? 0;
                    accountType = accountFallback.GetAttributeValue<OptionSetValue>("mcs_accounttype")?.Value ?? 0;
                }

                // 查询是否有销售订单（判断新老客户）
                var orderQuery = new QueryExpression("salesorder")
                {
                    ColumnSet = new ColumnSet("salesorderid"),
                    Criteria = new FilterExpression
                    {
                        Conditions = { new ConditionExpression("customerid", ConditionOperator.Equal, accountId.Value) }
                    },
                    TopCount = 1
                };
                var orders = service.RetrieveMultiple(orderQuery);
                bool isOldCustomer = orders.Entities.Count > 0;

                tracer.Trace($"客户属性: category={accountCategory}, level={accountLevel}, type={accountType}, 老客户={isOldCustomer}");

                // 匹配评分卡类型
                int categoryId = MatchScoringCardType(isOldCustomer, accountCategory, accountLevel, accountType);
                tracer.Trace($"匹配评分卡类型: {categoryId}");

                if (categoryId == 0)
                {
                    tracer.Trace("无法匹配评分卡类型");
                    return result;
                }

                // 查询评分卡配置项
                var query = new QueryExpression("mcs_credit_scoringcard")
                {
                    ColumnSet = new ColumnSet("mcs_itemid", "mcs_itemname", "mcs_datatype", "mcs_credititem"),
                    Criteria = new FilterExpression
                    {
                        Conditions = { new ConditionExpression("mcs_categoryid", ConditionOperator.Equal, categoryId) }
                    }
                };

                var records = service.RetrieveMultiple(query);
                tracer.Trace($"评分卡配置查询返回: {records.Entities.Count}条");

                foreach (var record in records.Entities)
                {
                    string itemName = record.GetAttributeValue<string>("mcs_itemname") ?? "";
                    int dataType = record.GetAttributeValue<OptionSetValue>("mcs_datatype")?.Value ?? 1;
                    
                    // 获取评分项目编码
                    string itemCode = "";
                    // 优先从 mcs_itemid 字符串字段获取（安全访问，避免 KeyNotFoundException）
                    itemCode = record.GetAttributeValue<string>("mcs_itemid") ?? "";
                    if (string.IsNullOrEmpty(itemCode))
                    {
                        // fallback: 从 mcs_credititem Lookup 获取
                        var itemRef = record.GetAttributeValue<EntityReference>("mcs_credititem");
                        if (itemRef != null)
                        {
                            var item = service.Retrieve("mcs_credit_items", itemRef.Id, new ColumnSet("mcs_credit_itemsno", "mcs_group"));
                            itemCode = item?.GetAttributeValue<string>("mcs_credit_itemsno") ?? "";
                        }
                    }

                    if (string.IsNullOrEmpty(itemCode))
                    {
                        tracer.Trace($"评分卡配置项'{itemName}'缺少项目编码，跳过");
                        continue;
                    }

                    // 数据类型转换：评分卡配置表用100000000/100000001，标签表用1/2
                    int tagDataType = (dataType == 100000000) ? 1 : 2;

                    result.Add(new ScoringCardItem
                    {
                        ItemCode = itemCode,
                        ItemName = itemName,
                        DataType = tagDataType
                    });
                }
            }
            catch (Exception ex)
            {
                tracer.Trace($"查询评分卡配置异常: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 匹配评分卡类型
        /// </summary>
        private int MatchScoringCardType(bool isOldCustomer, int accountCategory, int accountLevel, int accountType)
        {
            // 个人客户优先: mcs_accounttype = 1 (Individual Account)
            if (accountType == 1) return 5;

            // 经销商判断: mcs_accountcategory = 10 (Official Dealer) 或 90 (Prospective Dealer)
            bool isDealer = (accountCategory == 10 || accountCategory == 90);
            if (isDealer)
            {
                return isOldCustomer ? 6 : 7;
            }

            // 直销客户判断: mcs_accountlevel = 4(Diamond=S级) 或 3(Gold=A级) 为大客户
            bool isBigAccount = (accountLevel == 4 || accountLevel == 3);
            if (isOldCustomer)
            {
                return isBigAccount ? 1 : 3;  // SA级老客户 : BC级老客户
            }
            else
            {
                return isBigAccount ? 2 : 4;  // SA级新客户 : BC级新客户
            }
        }

        /// <summary>
        /// 根据D365评分项目编码，从Coface数据中获取对应值
        /// </summary>
        private object GetCofaceValue(string d365ItemCode, Dictionary<string, object> cofaceData)
        {
            // 查找D365编码对应的Coface字段名
            string cofaceKey = null;
            foreach (var kvp in CofaceToD365Mapping)
            {
                if (kvp.Value == d365ItemCode)
                {
                    cofaceKey = kvp.Key;
                    break;
                }
            }

            if (cofaceKey != null && cofaceData.ContainsKey(cofaceKey))
            {
                return cofaceData[cofaceKey];
            }

            // Coface数据中没有该指标，返回null（缺失值）
            return null;
        }

        /// <summary>
        /// 查找或创建客户信用标签记录
        /// </summary>
        private Guid? FindOrCreateTag(
            IOrganizationService service,
            ITracingService tracer,
            Guid creditRecordId,
            string scoreId,
            Entity creditRecord,
            string indicatorCode,
            int dataType)
        {
            // 查询评分项目完整信息（用于带出名称、分类、说明）
            var itemQuery = new QueryExpression("mcs_credit_items")
            {
                ColumnSet = new ColumnSet("mcs_credit_itemsid", "mcs_itemname", "mcs_group", "mcs_itemdesc"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_credit_itemsno", ConditionOperator.Equal, indicatorCode)
                    }
                },
                TopCount = 1
            };

            var itemResult = service.RetrieveMultiple(itemQuery);
            if (itemResult.Entities.Count == 0)
            {
                tracer.Trace($"评分项目 {indicatorCode} 不存在");
                return null;
            }

            var itemRecord = itemResult.Entities[0];
            Guid itemId = itemRecord.Id;
            string itemName = itemRecord.GetAttributeValue<string>("mcs_itemname") ?? "";
            int rawGroup = itemRecord.GetAttributeValue<OptionSetValue>("mcs_group")?.Value ?? 1;
            string itemDesc = itemRecord.GetAttributeValue<string>("mcs_itemdesc") ?? "";
            
            // mcs_group值转换：评分项目表用100000000/100000001/100000002等，标签表用1/2/3/4/5
            int itemGroup = ConvertGroupValue(rawGroup);
            tracer.Trace($"评分项目{indicatorCode}: rawGroup={rawGroup}, convertedGroup={itemGroup}");

            // 查询是否已存在标签记录（通过mcs_itemcode文本字段匹配）
            var tagQuery = new QueryExpression("mcs_customer_tag")
            {
                ColumnSet = new ColumnSet("mcs_customer_tagid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_credit_record", ConditionOperator.Equal, creditRecordId),
                        new ConditionExpression("mcs_itemcode", ConditionOperator.Equal, indicatorCode)
                    }
                },
                TopCount = 1
            };

            var tagResult = service.RetrieveMultiple(tagQuery);
            if (tagResult.Entities.Count > 0)
            {
                return tagResult.Entities[0].Id;
            }

            // 创建新标签记录
            var newTag = new Entity("mcs_customer_tag");
            newTag["mcs_credit_record"] = new EntityReference("mcs_credit_record", creditRecordId);
            newTag["mcs_scoreid"] = scoreId;
            // mcs_accountid是Lookup字段，需要获取EntityReference
            if (creditRecord.Contains("mcs_accountid") && creditRecord["mcs_accountid"] is EntityReference accountRef)
            {
                newTag["mcs_accountid"] = accountRef;
            }
            else if (creditRecord.Contains("mcs_accountid"))
            {
                // 尝试直接获取字符串值
                newTag["mcs_accountid"] = creditRecord.GetAttributeValue<string>("mcs_accountid");
            }
            // 设置评分项目关联字段
            newTag["mcs_credit_item"] = new EntityReference("mcs_credit_items", itemId);
            // 设置指标编码（文本型，存储评分项目编码如ExternalRating）
            newTag["mcs_itemcode"] = indicatorCode;
            // 显式设置评分项目名称、分类、说明（确保表单正确显示）
            newTag["mcs_itemname"] = itemName;
            newTag["mcs_group"] = new OptionSetValue(itemGroup);
            newTag["mcs_itemdesc"] = itemDesc;
            newTag["mcs_datatype"] = new OptionSetValue(dataType);
            newTag["mcs_isscore"] = false;
            newTag["mcs_active"] = true;

            Guid newTagId = service.Create(newTag);
            tracer.Trace($"创建标签记录: {indicatorCode}({itemName}), ID={newTagId}");
            return newTagId;
        }

        /// <summary>
        /// 更新标签指标值
        /// 根据数据类型写入对应字段：
        /// - 定量(dataType=1): 写入mcs_itemintvalue1(数值)和mcs_itemvalue1(格式化字符串)，不写入mcs_itemtxtvalue1
        /// - 定性(dataType=2): 写入mcs_itemtxtvalue1(中文显示文本)和mcs_itemvalue1(原始值用于计算)，不写入mcs_itemintvalue1
        /// </summary>
        private void UpdateTagValue(IOrganizationService service, ITracingService tracer, Guid tagId, int dataType, object value, string itemCode, EntityReference creditItemValueRef = null)
        {
            var updateTag = new Entity("mcs_customer_tag") { Id = tagId };

            if (dataType == 1) // 定量
            {
                if (value == null)
                {
                    // 空值：清除数值字段，文本字段显示"N/A"
                    updateTag["mcs_itemintvalue1"] = null;
                    updateTag["mcs_itemvalue1"] = "N/A";
                    tracer.Trace("更新定量值: N/A (数据缺失)");
                }
                else
                {
                    decimal decimalValue = Convert.ToDecimal(value);
                    // 校验字段范围 (0 到 99999999999.99)
                    if (decimalValue < 0)
                    {
                        tracer.Trace($"定量值 {decimalValue} 小于0，按缺失值处理");
                        updateTag["mcs_itemintvalue1"] = null;
                        updateTag["mcs_itemvalue1"] = "N/A";
                    }
                    else if (decimalValue > 99999999999m)
                    {
                        tracer.Trace($"定量值 {decimalValue} 超出最大范围，截断为99999999999");
                        updateTag["mcs_itemintvalue1"] = 99999999999m;
                        updateTag["mcs_itemvalue1"] = "99999999999.00";
                    }
                    else
                    {
                        updateTag["mcs_itemintvalue1"] = decimalValue;
                        updateTag["mcs_itemvalue1"] = decimalValue.ToString("F2");
                        tracer.Trace($"更新定量值: {decimalValue:F2}");
                    }
                }
            }
            else // 定性
            {
                string stringValue = value?.ToString() ?? "O";
                // value1保持原始值（用于算分匹配）
                updateTag["mcs_itemvalue1"] = stringValue;
                // txtvalue1显示中文映射（用于界面展示）
                string displayValue = MapValueToDisplayName(itemCode, stringValue);
                updateTag["mcs_itemtxtvalue1"] = displayValue;
                // 确保定量字段为空（避免之前定量数据残留）
                updateTag["mcs_itemintvalue1"] = null;
                // 设置定性枚举 Lookup（如客户评级）
                if (creditItemValueRef != null)
                {
                    updateTag["mcs_credititem_value"] = creditItemValueRef;
                }
                tracer.Trace($"更新定性值: 原始={stringValue}, 显示={displayValue}");
            }

            service.Update(updateTag);
        }

        /// <summary>
        /// 定性指标值映射：原始值 → 中文显示文本
        /// 根据枚举值表配置映射，后台保留原始值参与算分
        /// </summary>
        private string MapValueToDisplayName(string itemCode, string value)
        {
            if (string.IsNullOrEmpty(value))
                return "缺失";

            switch (itemCode)
            {
                case "SectorRisk": // 行业风险: 1=低风险, 2=中风险, 3=高风险
                    switch (value)
                    {
                        case "1": return "低风险";
                        case "2": return "中风险";
                        case "3": return "高风险";
                        case "O": return "缺失";
                        default: return value;
                    }

                case "CountryRisk": // 国别风险: Coface返回A1/A2/A3/A4/B/C/D/E
                    switch (value?.ToUpper())
                    {
                        case "A1":
                        case "A2": return "低风险";
                        case "A3":
                        case "A4": return "中风险";
                        case "B":
                        case "C":
                        case "D":
                        case "E": return "高风险";
                        case "O": return "缺失";
                        default: return value;
                    }

                case "ExternalRating": // 外部评级: Coface返回0-10
                    switch (value)
                    {
                        case "0": return "违约/资不抵债";
                        case "1": return "极端风险";
                        case "2": return "极高风险";
                        case "3": return "高风险";
                        case "4": return "显著风险";
                        case "5": return "中等风险";
                        case "6": return "可接受风险";
                        case "7": return "一般风险";
                        case "8": return "低风险";
                        case "9": return "极低风险";
                        case "10": return "极佳风险";
                        case "O": return "缺失";
                        default: return value;
                    }

                case "BigAccount": // 客户评级
                    switch (value?.ToUpper())
                    {
                        case "S": return "S级";
                        case "A": return "A级";
                        case "S_JV": return "S级控股/参股公司";
                        case "A_JV": return "A级控股/参股公司";
                        case "O": return "缺失";
                        default: return value;
                    }

                default:
                    return value;
            }
        }

        /// <summary>
        /// 获取客户评级(BigAccount)的定性值
        /// 根据 mcs_customermasterdata.mcs_accountlevel 和 account.new_is_joint_venture 自动判断
        /// </summary>
        private (string ListValue, string DisplayName, EntityReference EnumRef) GetBigAccountValue(
            IOrganizationService service,
            ITracingService tracer,
            Entity creditRecord)
        {
            string listValue = "O";
            string displayName = "缺失";
            EntityReference enumRef = null;

            try
            {
                var accountRef = creditRecord.GetAttributeValue<EntityReference>("mcs_accountid");
                if (accountRef == null)
                {
                    tracer.Trace("BigAccount: 评估记录未关联客户");
                    return (listValue, displayName, enumRef);
                }

                var account = service.Retrieve("account", accountRef.Id,
                    new ColumnSet("mcs_customermasterdata", "new_is_joint_venture"));
                bool isJointVenture = account.GetAttributeValue<bool>("new_is_joint_venture");

                int accountLevel = 0;
                if (account.Contains("mcs_customermasterdata") && account["mcs_customermasterdata"] is EntityReference cmRef)
                {
                    var customerMasterData = service.Retrieve("mcs_customermasterdata", cmRef.Id,
                        new ColumnSet("mcs_accountlevel"));
                    accountLevel = customerMasterData.GetAttributeValue<OptionSetValue>("mcs_accountlevel")?.Value ?? 0;
                }

                tracer.Trace($"BigAccount: level={accountLevel}, isJointVenture={isJointVenture}");

                switch (accountLevel)
                {
                    case 4:
                        listValue = isJointVenture ? "S_JV" : "S";
                        displayName = isJointVenture ? "S级控股/参股公司" : "S级";
                        break;
                    case 3:
                        listValue = isJointVenture ? "A_JV" : "A";
                        displayName = isJointVenture ? "A级控股/参股公司" : "A级";
                        break;
                    default:
                        listValue = "O";
                        displayName = "缺失";
                        break;
                }

                var enumId = ResolveCreditItemValueByListValue(service, tracer, "BigAccount", listValue);
                if (enumId.HasValue)
                {
                    enumRef = new EntityReference("mcs_credititem_value", enumId.Value);
                }
            }
            catch (Exception ex)
            {
                tracer.Trace($"BigAccount 取值异常: {ex.Message}");
            }

            return (listValue, displayName, enumRef);
        }

        /// <summary>
        /// 根据评分项目编码和 listValue 反查 mcs_credititem_value 记录
        /// </summary>
        private Guid? ResolveCreditItemValueByListValue(
            IOrganizationService service,
            ITracingService tracer,
            string itemCode,
            string listValue)
        {
            try
            {
                var itemQuery = new QueryExpression("mcs_credit_items")
                {
                    ColumnSet = new ColumnSet("mcs_credit_itemsid"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("mcs_credit_itemsno", ConditionOperator.Equal, itemCode)
                        }
                    },
                    TopCount = 1
                };

                var itemResult = service.RetrieveMultiple(itemQuery);
                if (itemResult.Entities.Count == 0)
                {
                    tracer.Trace($"ResolveCreditItemValueByListValue: 评分项目 {itemCode} 不存在");
                    return null;
                }

                var itemId = itemResult.Entities[0].Id;

                var enumQuery = new QueryExpression("mcs_credititem_value")
                {
                    ColumnSet = new ColumnSet("mcs_credititem_valueid"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("mcs_credititemno", ConditionOperator.Equal, itemId),
                            new ConditionExpression("mcs_listvalue", ConditionOperator.Equal, listValue),
                            new ConditionExpression("statecode", ConditionOperator.Equal, 0)
                        }
                    },
                    TopCount = 1
                };

                var enumResult = service.RetrieveMultiple(enumQuery);
                if (enumResult.Entities.Count > 0)
                {
                    return enumResult.Entities[0].Id;
                }
            }
            catch (Exception ex)
            {
                tracer.Trace($"ResolveCreditItemValueByListValue 异常: {ex.Message}");
            }

            return null;
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 将Coface外部评级反写到客户主数据
        /// </summary>
        private void UpdateCustomerMasterDataExternalRate(IOrganizationService service, ITracingService tracer, Entity creditRecord, Dictionary<string, object> data)
        {
            try
            {
                if (!creditRecord.Contains("mcs_accountid") || !(creditRecord["mcs_accountid"] is EntityReference accountRef))
                {
                    tracer.Trace("评估记录未关联客户，跳过外部评级反写");
                    return;
                }

                if (!data.ContainsKey("ExternalRating") || data["ExternalRating"] == null)
                {
                    tracer.Trace("Coface 数据中没有 ExternalRating，跳过反写");
                    return;
                }

                var account = service.Retrieve("account", accountRef.Id, new ColumnSet("mcs_customermasterdata"));
                if (!account.Contains("mcs_customermasterdata") || !(account["mcs_customermasterdata"] is EntityReference cmRef))
                {
                    tracer.Trace("account 未关联 mcs_customermasterdata，跳过外部评级反写");
                    return;
                }

                var updateMaster = new Entity("mcs_customermasterdata", cmRef.Id);
                updateMaster["mcs_externalrate"] = data["ExternalRating"].ToString();
                service.Update(updateMaster);
                tracer.Trace($"外部评级已反写到客户主数据: {data["ExternalRating"]}");
            }
            catch (Exception ex)
            {
                tracer.Trace($"外部评级反写失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 转换mcs_group值：评分项目表的选项集值 → 标签表的有效选项集值
        /// 评分项目表: 100000000=客户实力, 100000001=客户财务, 100000002=宏观市场, 100000003=历史交易, 100000004=综合指标
        /// 标签表: 1=客户实力, 2=客户财务, 3=宏观市场, 4=历史交易, 5=综合指标
        /// </summary>
        private int ConvertGroupValue(int rawValue)
        {
            switch (rawValue)
            {
                case 100000000: return 1; // 客户实力
                case 100000001: return 2; // 客户财务
                case 100000002: return 3; // 宏观市场
                case 100000003: return 4; // 历史交易
                case 100000004: return 5; // 综合指标
                default: return rawValue <= 5 ? rawValue : 1; // 如果已经是1-5，直接返回；否则默认1
            }
        }

        #endregion

        #region 内部类

        /// <summary>
        /// 评分卡配置项
        /// </summary>
        private class ScoringCardItem
        {
            public string ItemCode { get; set; }
            public string ItemName { get; set; }
            public int DataType { get; set; }
        }

        #endregion
    }
}
