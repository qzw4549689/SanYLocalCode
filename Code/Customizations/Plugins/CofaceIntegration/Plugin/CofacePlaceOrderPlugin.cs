using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using SanyD365.Plugins.CofaceIntegration;
using SanyD365.Plugins.CofaceIntegration.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace SanyD365.Plugins.CofaceIntegration.Plugin
{
    /// <summary>
    /// Coface 系统内下单 Custom API Plugin
    /// 触发: mcs_CofacePlaceOrder
    /// 输入: CreditRecordId(信用评估记录ID)
    /// 输出: ResultJson({ status, message, orderStatus, orderStatusName })
    ///
    /// 点击推进式状态机（详见 Documents/Planning/Coface系统内下单/Coface系统内下单实施方案.md 第 5/6 章）：
    ///   0 未下单 / 5 下单失败 → 首次下单流程（调查单 / 复用已有订单 / 下 URBA 监控单）
    ///   1 调查单已提交        → 查识别结果，identified 回写 Coface ID 后继续
    ///   2 URBA已下单待就绪    → 查状态，Ready 后下 Report 单
    ///   3 Report已下单待就绪  → 查状态，Ready 后置已就绪
    ///   4 已就绪              → 提示可进入下一阶段
    ///
    /// 防重复扣费红线：每个分支必须先查已有订单再下单；任何异常路径不得静默成功。
    /// </summary>
    public class CofacePlaceOrderPlugin : IPlugin
    {
        // Coface 下单状态（mcs_credit_record.mcs_cofaceorderstatus 选项集）
        private const int ORDER_NONE = 0;          // 未下单
        private const int ORDER_INVESTIGATING = 1; // 调查单已提交
        private const int ORDER_URBA_PENDING = 2;  // URBA已下单待就绪
        private const int ORDER_REPORT_PENDING = 3;// Report已下单待就绪
        private const int ORDER_READY = 4;         // 已就绪
        private const int ORDER_FAILED = 5;        // 下单失败

        private static readonly Dictionary<int, string> OrderStatusNames = new Dictionary<int, string>
        {
            { ORDER_NONE, "未下单" },
            { ORDER_INVESTIGATING, "调查单已提交" },
            { ORDER_URBA_PENDING, "URBA已下单待就绪" },
            { ORDER_REPORT_PENDING, "Report已下单待就绪" },
            { ORDER_READY, "已就绪" },
            { ORDER_FAILED, "下单失败" }
        };

        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = factory.CreateOrganizationService(context.UserId);
            ITracingService tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracer.Trace("===== CofacePlaceOrderPlugin 开始执行 =====");

            // 系统身份：Token 缓存回写与订单状态字段回写（不依赖业务员对配置实体的权限）
            IOrganizationService systemService = factory.CreateOrganizationService(null);

            try
            {
                string creditRecordIdStr = context.InputParameters.Contains("CreditRecordId")
                    ? context.InputParameters["CreditRecordId"]?.ToString() : null;

                if (string.IsNullOrWhiteSpace(creditRecordIdStr) || !Guid.TryParse(creditRecordIdStr, out Guid creditRecordId))
                {
                    SetResult(context, 0, "信用评估记录ID无效", null, null);
                    return;
                }

                var result = ExecutePlaceOrder(service, systemService, tracer, creditRecordId);
                context.OutputParameters["ResultJson"] = JsonSerializer.Serialize(result);

                tracer.Trace("===== CofacePlaceOrderPlugin 执行完成 =====");
            }
            catch (Exception ex)
            {
                tracer.Trace($"Coface下单失败: {ex.Message}");
                SetResult(context, 0, $"Coface下单失败: {ex.Message}", null, null);
            }
        }

        /// <summary>
        /// 下单状态机主流程
        /// </summary>
        private PlaceOrderResult ExecutePlaceOrder(IOrganizationService service, IOrganizationService systemService, ITracingService tracer, Guid creditRecordId)
        {
            // ========== 0. 前置校验 ==========
            Entity creditRecord = service.Retrieve("mcs_credit_record", creditRecordId,
                new ColumnSet("mcs_scoreid", "mcs_status", "mcs_cofaceid", "mcs_countrycode", "mcs_custnameen",
                    "mcs_accountid", "mcs_cofaceorderstatus", "mcs_cofaceordermsg"));

            int recordStatus = creditRecord.GetAttributeValue<OptionSetValue>("mcs_status")?.Value ?? 0;
            if (recordStatus != 10)
            {
                return Fail($"【Coface 下单】仅在「关联客户代码」阶段可用，当前状态={recordStatus}");
            }

            string cofaceId = creditRecord.GetAttributeValue<string>("mcs_cofaceid");
            string countryCode = creditRecord.GetAttributeValue<string>("mcs_countrycode");
            string scoreId = creditRecord.GetAttributeValue<string>("mcs_scoreid");

            if (string.IsNullOrEmpty(cofaceId))
            {
                return Fail("未关联科法斯客户代码（Coface ID），请先执行【搜索 Coface 企业】绑定");
            }
            if (string.IsNullOrEmpty(countryCode))
            {
                return Fail("国家编码为空，无法下单");
            }

            int orderStatus = creditRecord.GetAttributeValue<OptionSetValue>("mcs_cofaceorderstatus")?.Value ?? ORDER_NONE;
            string orderMsg = creditRecord.GetAttributeValue<string>("mcs_cofaceordermsg") ?? "";
            tracer.Trace($"评估记录: scoreId={scoreId}, cofaceId={cofaceId}, country={countryCode}, orderStatus={orderStatus}");

            var countryConfig = CofaceCountryConfigHelper.GetConfig(service);
            var apiService = new CofaceApiService(service, tracer, systemService);

            // ========== 1. 按当前下单状态分支推进 ==========
            switch (orderStatus)
            {
                case ORDER_NONE:
                case ORDER_FAILED:
                    return FirstPlaceFlow(apiService, service, tracer, creditRecord, countryConfig, cofaceId, countryCode, scoreId);

                case ORDER_INVESTIGATING:
                    return CheckIdentificationFlow(apiService, service, tracer, creditRecord, countryConfig, countryCode, scoreId, orderMsg);

                case ORDER_URBA_PENDING:
                    return CheckUrbaAndPlaceReportFlow(apiService, service, tracer, creditRecord, countryConfig, cofaceId, countryCode, scoreId);

                case ORDER_REPORT_PENDING:
                    return CheckReportReadyFlow(apiService, service, tracer, creditRecord, countryConfig, cofaceId, countryCode);

                case ORDER_READY:
                    return new PlaceOrderResult
                    {
                        Status = 1,
                        Message = "Coface 订单已就绪，可进入「内外部数据集成」阶段",
                        OrderStatus = ORDER_READY,
                        OrderStatusName = OrderStatusNames[ORDER_READY]
                    };

                default:
                    return Fail($"未知的下单状态: {orderStatus}");
            }
        }

        /// <summary>
        /// 首次下单流程（未下单 / 下单失败后重试）
        /// 复用优先级：已有 URBA 订单 → 即时报告 → 新下 URBA 监控单
        /// </summary>
        private PlaceOrderResult FirstPlaceFlow(CofaceApiService apiService, IOrganizationService service, ITracingService tracer,
            Entity creditRecord, CofaceCountryConfig countryConfig, string cofaceId, string countryCode, string scoreId)
        {
            // 1.1 Coface ID 非 icon# 格式 → 公司未识别，先下调查单（免费）
            if (!cofaceId.StartsWith("icon#", StringComparison.OrdinalIgnoreCase))
            {
                return PlaceIdentification(apiService, service, tracer, creditRecord, countryCode);
            }

            // 1.2 查已有 URBA 监控单（含在途）→ 复用
            using (var urbaOrdersDoc = apiService.GetUrbaMonitoringOrders(cofaceId, countryCode))
            {
                var urbaInfo = CofaceOrderInfoHelper.ExtractUrbaOrderInfo(urbaOrdersDoc, tracer);
                if (urbaInfo.Status != CofaceOrderInfoHelper.UrbaOrderStatus.NotFound)
                {
                    tracer.Trace($"复用已有 URBA 监控单: orderId={urbaInfo.OrderId}, status={urbaInfo.StatusDetail}");
                    return Advance(service, tracer, creditRecord.Id, ORDER_URBA_PENDING,
                        $"已有 URBA 监控单（orderId={urbaInfo.OrderId}，状态={urbaInfo.StatusDetail}），待就绪",
                        $"urbaOrderId={urbaInfo.OrderId}");
                }
            }

            // 1.3 查即时报告（免费复用）
            // TODO(沙盒确认)：即时报告可能仍需 POST /publications/orders 带 processingTimeInstruction=即时交付
            using (var instantDoc = apiService.GetInstantReport(countryCode, cofaceId))
            {
                string instantPubId = ExtractInstantReportPublicationId(instantDoc, tracer);
                if (!string.IsNullOrEmpty(instantPubId))
                {
                    tracer.Trace($"复用即时报告: publicationId={instantPubId}");
                    return Advance(service, tracer, creditRecord.Id, ORDER_REPORT_PENDING,
                        $"已获取即时报告（publicationId={instantPubId}），待确认就绪",
                        $"reportPublicationId={instantPubId}");
                }
            }

            // 1.4 下 URBA360 监控单
            string legitimateInterest = countryConfig.GetLegitimateInterest(countryCode);
            try
            {
                using (var orderDoc = apiService.PlaceUrbaMonitoringOrder(cofaceId, countryCode, legitimateInterest, scoreId))
                {
                    string urbaOrderId = ExtractOrderId(orderDoc, tracer);
                    tracer.Trace($"URBA 监控单下单成功: orderId={urbaOrderId}");
                    return Advance(service, tracer, creditRecord.Id, ORDER_URBA_PENDING,
                        $"URBA 监控单已提交（orderId={urbaOrderId ?? "见订单列表"}），待就绪",
                        $"urbaOrderId={urbaOrderId}");
                }
            }
            catch (Exception ex)
            {
                return PlaceFailed(service, tracer, creditRecord.Id, $"URBA 监控单下单失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 下公司识别调查单（公司未识别，免费）
        /// </summary>
        private PlaceOrderResult PlaceIdentification(CofaceApiService apiService, IOrganizationService service, ITracingService tracer,
            Entity creditRecord, string countryCode)
        {
            string custNameEn = creditRecord.GetAttributeValue<string>("mcs_custnameen");
            if (string.IsNullOrEmpty(custNameEn))
            {
                return Fail("客户英文名称为空，无法下调查单，请先维护客户主数据");
            }

            // 调查单必填 name + address(postalCode/city/countryCode)，地址从客户 Account 取
            // TODO(C1/取数确认)：地址字段来源（account.address1_*）需业务确认
            string city = null;
            string postalCode = null;
            var accountRef = creditRecord.GetAttributeValue<EntityReference>("mcs_accountid");
            if (accountRef != null)
            {
                var account = service.Retrieve("account", accountRef.Id, new ColumnSet("address1_city", "address1_postalcode"));
                city = account.GetAttributeValue<string>("address1_city");
                postalCode = account.GetAttributeValue<string>("address1_postalcode");
            }

            if (string.IsNullOrEmpty(city) || string.IsNullOrEmpty(postalCode))
            {
                return Fail("客户地址信息不完整（城市/邮编），无法下调查单，请先补充客户地址");
            }

            try
            {
                using (var doc = apiService.PlaceIdentificationOrder(custNameEn, countryCode, postalCode, city))
                {
                    string identificationId = ExtractOrderId(doc, tracer, "companyIdentificationId");
                    tracer.Trace($"调查单已提交: companyIdentificationId={identificationId}");
                    return Advance(service, tracer, creditRecord.Id, ORDER_INVESTIGATING,
                        $"公司识别调查单已提交（免费），等待识别结果",
                        $"identificationId={identificationId}");
                }
            }
            catch (Exception ex)
            {
                return PlaceFailed(service, tracer, creditRecord.Id, $"调查单提交失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 调查单已提交 → 查识别结果
        /// </summary>
        private PlaceOrderResult CheckIdentificationFlow(CofaceApiService apiService, IOrganizationService service, ITracingService tracer,
            Entity creditRecord, CofaceCountryConfig countryConfig, string countryCode, string scoreId, string orderMsg)
        {
            string identificationId = GetMsgToken(orderMsg, "identificationId");
            tracer.Trace($"查询识别结果: identificationId={identificationId}");

            using (var doc = apiService.GetIdentificationOrders())
            {
                string idStatus = null;
                string iconId = null;
                FindIdentificationResult(doc, identificationId, tracer, out idStatus, out iconId);

                if (string.IsNullOrEmpty(idStatus))
                {
                    return new PlaceOrderResult
                    {
                        Status = 1,
                        Message = "调查单仍在识别中，请稍后再试",
                        OrderStatus = ORDER_INVESTIGATING,
                        OrderStatusName = OrderStatusNames[ORDER_INVESTIGATING]
                    };
                }

                if (idStatus.Equals("identified", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrEmpty(iconId))
                    {
                        return PlaceFailed(service, tracer, creditRecord.Id, "识别成功但未返回 icon 号，请联系 Coface 核查");
                    }

                    // 回写 Coface ID，然后继续首次下单流程（查已有订单 / 下 URBA 单）
                    string newCofaceId = iconId.StartsWith("icon#", StringComparison.OrdinalIgnoreCase) ? iconId : $"icon#{iconId}";
                    var update = new Entity("mcs_credit_record", creditRecord.Id);
                    update["mcs_cofaceid"] = newCofaceId;
                    service.Update(update);
                    tracer.Trace($"识别成功，回写 Coface ID: {newCofaceId}");

                    return FirstPlaceFlow(apiService, service, tracer, creditRecord, countryConfig, newCofaceId, countryCode, scoreId);
                }

                if (idStatus.Equals("negative", StringComparison.OrdinalIgnoreCase))
                {
                    return PlaceFailed(service, tracer, creditRecord.Id, "Coface 未能识别该公司（negative），请走线下人工下单流程");
                }

                // initiated 等其他状态：仍在调查
                // TODO(C4 待确认)：initiated 状态下能否用 companyIdentificationId 直接下 Report 单
                return new PlaceOrderResult
                {
                    Status = 1,
                    Message = $"调查单仍在识别中（{idStatus}），请稍后再试",
                    OrderStatus = ORDER_INVESTIGATING,
                    OrderStatusName = OrderStatusNames[ORDER_INVESTIGATING]
                };
            }
        }

        /// <summary>
        /// URBA 已下单待就绪 → 查状态，Ready 后下 Report 单
        /// </summary>
        private PlaceOrderResult CheckUrbaAndPlaceReportFlow(CofaceApiService apiService, IOrganizationService service, ITracingService tracer,
            Entity creditRecord, CofaceCountryConfig countryConfig, string cofaceId, string countryCode, string scoreId)
        {
            // 查 URBA 状态
            CofaceOrderInfoHelper.UrbaOrderInfo urbaInfo;
            using (var urbaOrdersDoc = apiService.GetUrbaMonitoringOrders(cofaceId, countryCode))
            {
                urbaInfo = CofaceOrderInfoHelper.ExtractUrbaOrderInfo(urbaOrdersDoc, tracer);
            }

            if (urbaInfo.Status != CofaceOrderInfoHelper.UrbaOrderStatus.Ready)
            {
                return new PlaceOrderResult
                {
                    Status = 1,
                    Message = $"URBA 监控数据准备中（{urbaInfo.StatusDetail ?? "未就绪"}），请稍后再试",
                    OrderStatus = ORDER_URBA_PENDING,
                    OrderStatusName = OrderStatusNames[ORDER_URBA_PENDING]
                };
            }

            tracer.Trace($"URBA 已就绪: orderId={urbaInfo.OrderId}，开始 Report 下单流程");

            // 防重：先查已有 Report 订单（复用数据集成插件同款产品匹配逻辑）
            var reportProduct = countryConfig.GetReportProduct(countryCode);
            using (var reportOrdersDoc = apiService.GetReportOrders(cofaceId, countryCode, reportProduct.Slug, reportProduct.ProductCode))
            {
                var reportInfo = CofaceOrderInfoHelper.ExtractReportOrderInfo(reportOrdersDoc, tracer, reportProduct.Slug, reportProduct.ProductCode);
                if (reportInfo.Status != CofaceOrderInfoHelper.ReportOrderStatus.NotFound)
                {
                    tracer.Trace($"复用已有 Report 订单: orderId={reportInfo.OrderId}, publicationId={reportInfo.PublicationId}, status={reportInfo.StatusDetail}");
                    return Advance(service, tracer, creditRecord.Id, ORDER_REPORT_PENDING,
                        $"已有 Report 订单（publicationId={reportInfo.PublicationId}，状态={reportInfo.StatusDetail}），待就绪",
                        $"reportOrderId={reportInfo.OrderId};reportPublicationId={reportInfo.PublicationId}");
                }
            }

            // 下 Report 单（JSON）；双格式 39 国间隔 5 秒再下 PDF 单
            string legitimateInterest = countryConfig.GetLegitimateInterest(countryCode);
            try
            {
                string reportOrderId;
                string reportPubId;
                using (var orderDoc = apiService.PlaceReportOrder(cofaceId, countryCode, reportProduct.Slug, reportProduct.ProductCode, "json", legitimateInterest, scoreId))
                {
                    reportOrderId = ExtractOrderId(orderDoc, tracer);
                    reportPubId = ExtractPublicationId(orderDoc, tracer);
                    tracer.Trace($"Report 单（JSON）下单成功: orderId={reportOrderId}, publicationId={reportPubId}");
                }

                string msgExtra = null;
                if (countryConfig.IsDualFormatCountry(countryCode))
                {
                    // Coface 下单限流：单用户 10-12 次/分钟，两次下单间隔 5 秒
                    tracer.Trace("双格式国家，间隔 5 秒后下 PDF 单");
                    Thread.Sleep(5000);

                    using (var pdfOrderDoc = apiService.PlaceReportOrder(cofaceId, countryCode, reportProduct.Slug, reportProduct.ProductCode, "pdf", legitimateInterest, scoreId))
                    {
                        string pdfOrderId = ExtractOrderId(pdfOrderDoc, tracer);
                        string pdfPubId = ExtractPublicationId(pdfOrderDoc, tracer);
                        tracer.Trace($"Report 单（PDF）下单成功: orderId={pdfOrderId}, publicationId={pdfPubId}");
                        msgExtra = $"pdfOrderId={pdfOrderId};pdfPublicationId={pdfPubId}";
                    }
                }

                return Advance(service, tracer, creditRecord.Id, ORDER_REPORT_PENDING,
                    $"Report 单已提交（{(countryConfig.IsDualFormatCountry(countryCode) ? "JSON+PDF 两单" : "JSON 单")}，publicationId={reportPubId ?? "见订单列表"}），待就绪（约 6-7 个工作日）",
                    $"reportOrderId={reportOrderId};reportPublicationId={reportPubId}" + (msgExtra != null ? ";" + msgExtra : ""));
            }
            catch (Exception ex)
            {
                return PlaceFailed(service, tracer, creditRecord.Id, $"Report 单下单失败: {ex.Message}");
            }
        }

        /// <summary>
        /// Report 已下单待就绪 → 查状态，Ready 后置已就绪
        /// </summary>
        private PlaceOrderResult CheckReportReadyFlow(CofaceApiService apiService, IOrganizationService service, ITracingService tracer,
            Entity creditRecord, CofaceCountryConfig countryConfig, string cofaceId, string countryCode)
        {
            var reportProduct = countryConfig.GetReportProduct(countryCode);
            using (var reportOrdersDoc = apiService.GetReportOrders(cofaceId, countryCode, reportProduct.Slug, reportProduct.ProductCode))
            {
                var reportInfo = CofaceOrderInfoHelper.ExtractReportOrderInfo(reportOrdersDoc, tracer, reportProduct.Slug, reportProduct.ProductCode);

                if (reportInfo.Status == CofaceOrderInfoHelper.ReportOrderStatus.Ready)
                {
                    // TODO(C3 待确认)：双格式国家是否需 JSON+PDF 两单均 Ready 才放行，沙盒联调后按需收紧
                    return Advance(service, tracer, creditRecord.Id, ORDER_READY,
                        $"Report 已就绪（publicationId={reportInfo.PublicationId}），可进入「内外部数据集成」阶段",
                        $"reportOrderId={reportInfo.OrderId};reportPublicationId={reportInfo.PublicationId}");
                }

                if (reportInfo.Status == CofaceOrderInfoHelper.ReportOrderStatus.NotReady)
                {
                    return new PlaceOrderResult
                    {
                        Status = 1,
                        Message = $"Report 准备中（{reportInfo.StatusDetail}），报告约需 6-7 个工作日，请稍后再试",
                        OrderStatus = ORDER_REPORT_PENDING,
                        OrderStatusName = OrderStatusNames[ORDER_REPORT_PENDING]
                    };
                }

                return new PlaceOrderResult
                {
                    Status = 1,
                    Message = "未查询到 Report 订单状态，请稍后再试或联系管理员核查",
                    OrderStatus = ORDER_REPORT_PENDING,
                    OrderStatusName = OrderStatusNames[ORDER_REPORT_PENDING]
                };
            }
        }

        #region 结果与状态回写

        /// <summary>
        /// 推进状态机：回写下单三字段并返回成功结果
        /// </summary>
        private PlaceOrderResult Advance(IOrganizationService service, ITracingService tracer, Guid creditRecordId, int newStatus, string message, string msgToken)
        {
            WriteOrderFields(service, tracer, creditRecordId, newStatus, msgToken);
            return new PlaceOrderResult
            {
                Status = 1,
                Message = message,
                OrderStatus = newStatus,
                OrderStatusName = OrderStatusNames[newStatus]
            };
        }

        /// <summary>
        /// 下单失败：状态=下单失败 + 原因写下单信息（不静默成功）
        /// </summary>
        private PlaceOrderResult PlaceFailed(IOrganizationService service, ITracingService tracer, Guid creditRecordId, string reason)
        {
            tracer.Trace($"下单失败: {reason}");
            WriteOrderFields(service, tracer, creditRecordId, ORDER_FAILED, reason);
            return new PlaceOrderResult
            {
                Status = 0,
                Message = reason,
                OrderStatus = ORDER_FAILED,
                OrderStatusName = OrderStatusNames[ORDER_FAILED]
            };
        }

        /// <summary>
        /// 前置校验失败（不回写状态字段）
        /// </summary>
        private PlaceOrderResult Fail(string message)
        {
            return new PlaceOrderResult { Status = 0, Message = message };
        }

        /// <summary>
        /// 回写下单状态/信息/时间三字段（不含 mcs_status，不触发状态流转校验插件）
        /// </summary>
        private void WriteOrderFields(IOrganizationService service, ITracingService tracer, Guid creditRecordId, int newStatus, string msg)
        {
            try
            {
                var update = new Entity("mcs_credit_record", creditRecordId);
                update["mcs_cofaceorderstatus"] = new OptionSetValue(newStatus);
                update["mcs_cofaceordermsg"] = msg != null && msg.Length > 500 ? msg.Substring(0, 500) : msg;
                update["mcs_cofaceorderdate"] = DateTime.Now;
                service.Update(update);
            }
            catch (Exception ex)
            {
                // 回写失败仅记录（新字段可能尚未部署到环境）
                tracer.Trace($"回写下单状态字段失败（不阻断）: {ex.Message}");
            }
        }

        private void SetResult(IPluginExecutionContext context, int status, string message, int? orderStatus, string orderStatusName)
        {
            context.OutputParameters["ResultJson"] = JsonSerializer.Serialize(new PlaceOrderResult
            {
                Status = status,
                Message = message,
                OrderStatus = orderStatus,
                OrderStatusName = orderStatusName
            });
        }

        private class PlaceOrderResult
        {
            public int Status { get; set; }
            public string Message { get; set; }
            public int? OrderStatus { get; set; }
            public string OrderStatusName { get; set; }
        }

        #endregion

        #region 响应解析辅助

        /// <summary>
        /// 从下单响应中提取订单ID（兼容 id / orderId / 自定义字段名）
        /// </summary>
        private string ExtractOrderId(JsonDocument doc, ITracingService tracer, string preferredProperty = null)
        {
            try
            {
                var root = doc.RootElement;
                var candidates = new List<string>();
                if (!string.IsNullOrEmpty(preferredProperty)) candidates.Add(preferredProperty);
                candidates.AddRange(new[] { "id", "orderId", "companyIdentificationId" });

                foreach (var name in candidates)
                {
                    if (root.ValueKind == JsonValueKind.Object &&
                        root.TryGetProperty(name, out var prop) &&
                        prop.ValueKind == JsonValueKind.String)
                    {
                        return prop.GetString();
                    }
                }
                tracer.Trace($"下单响应未找到订单ID字段，原始响应: {root.GetRawText()}");
            }
            catch (Exception ex)
            {
                tracer.Trace($"提取订单ID异常: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// 从 Report 下单响应中提取 publicationId（publications[0].id）
        /// </summary>
        private string ExtractPublicationId(JsonDocument doc, ITracingService tracer)
        {
            try
            {
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object &&
                    root.TryGetProperty("publications", out var pubs) &&
                    pubs.ValueKind == JsonValueKind.Array)
                {
                    foreach (var pub in pubs.EnumerateArray())
                    {
                        if (pub.TryGetProperty("id", out var idProp))
                        {
                            return idProp.GetString();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                tracer.Trace($"提取publicationId异常: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// 从即时报告响应中提取可复用的 publicationId（无即时报告返回 null）
        /// </summary>
        private string ExtractInstantReportPublicationId(JsonDocument doc, ITracingService tracer)
        {
            try
            {
                var root = doc.RootElement;
                List<JsonElement> reports = new List<JsonElement>();
                if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in root.EnumerateArray()) reports.Add(item);
                }
                else if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("reports", out var arr) && arr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in arr.EnumerateArray()) reports.Add(item);
                    }
                    else if (root.TryGetProperty("id", out _))
                    {
                        reports.Add(root);
                    }
                }

                foreach (var report in reports)
                {
                    if (report.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String)
                    {
                        return idProp.GetString();
                    }
                }
                tracer.Trace("无可用即时报告");
            }
            catch (Exception ex)
            {
                tracer.Trace($"提取即时报告异常: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// 在调查单列表中定位本公司的识别结果
        /// </summary>
        private void FindIdentificationResult(JsonDocument doc, string identificationId, ITracingService tracer, out string idStatus, out string iconId)
        {
            idStatus = null;
            iconId = null;
            try
            {
                var root = doc.RootElement;
                List<JsonElement> orders = new List<JsonElement>();
                if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in root.EnumerateArray()) orders.Add(item);
                }
                else if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("identifications", out var arr) && arr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in arr.EnumerateArray()) orders.Add(item);
                    }
                    else if (root.TryGetProperty("orders", out var arr2) && arr2.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in arr2.EnumerateArray()) orders.Add(item);
                    }
                    else if (root.TryGetProperty("id", out _) || root.TryGetProperty("companyIdentificationId", out _))
                    {
                        orders.Add(root);
                    }
                }

                foreach (var order in orders)
                {
                    // 定位本单：优先按 identificationId 匹配；无记录时取第一条
                    string currentId = null;
                    if (order.TryGetProperty("companyIdentificationId", out var cid)) currentId = cid.GetString();
                    else if (order.TryGetProperty("id", out var oid)) currentId = oid.GetString();

                    if (!string.IsNullOrEmpty(identificationId) &&
                        !string.Equals(currentId, identificationId, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (order.TryGetProperty("status", out var statusProp)) idStatus = statusProp.GetString();
                    else if (order.TryGetProperty("identificationStatus", out var statusProp2)) idStatus = statusProp2.GetString();

                    // 识别成功时提取 icon 号（externalIds 中 repositorySlug=icon）
                    if (order.TryGetProperty("externalIds", out var extIds) && extIds.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var extId in extIds.EnumerateArray())
                        {
                            string slug = extId.TryGetProperty("repositorySlug", out var slugProp) ? slugProp.GetString() : null;
                            string id = extId.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                            if (string.Equals(slug, "icon", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(id))
                            {
                                iconId = id;
                                break;
                            }
                        }
                    }
                    if (string.IsNullOrEmpty(iconId) && order.TryGetProperty("iconId", out var iconProp))
                    {
                        iconId = iconProp.GetString();
                    }

                    tracer.Trace($"识别结果: id={currentId}, status={idStatus}, icon={iconId}");
                    return;
                }

                tracer.Trace("未找到调查单记录，视为仍在识别中");
            }
            catch (Exception ex)
            {
                tracer.Trace($"解析识别结果异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 从下单信息字段中读取 key=value 标记（格式：key=value;key2=value2）
        /// </summary>
        private string GetMsgToken(string msg, string key)
        {
            if (string.IsNullOrEmpty(msg)) return null;
            foreach (var part in msg.Split(';'))
            {
                var kv = part.Split(new[] { '=' }, 2);
                if (kv.Length == 2 && kv[0].Trim() == key && !string.IsNullOrWhiteSpace(kv[1].Trim()) && kv[1].Trim() != "")
                {
                    return kv[1].Trim();
                }
            }
            return null;
        }

        #endregion
    }
}
