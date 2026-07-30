using MSLibrary;
using MSLibrary.D365Integration.Entities;
using MSLibrary.DI;
using MSLibrary.Serializer;
using MSLibrary.Xrm;
using SanyD365.Main.DTO;
using SanyD365.Main.DTO.BPP;
using SanyD365.Main.Entities;
using SanyD365.Main.Entities.Endpoints;
using SanyD365.Main.Entities.HelperEx;
using SanyD365.Main.Logger;

namespace SanyD365.Main.Entities.BPP.BPPHandlerServices
{
    /// <summary>
    /// 客户信用评估BPP处理类
    /// </summary>
    [Injection(InterfaceType = typeof(BPPHandlerServiceForCreditRecord), Scope = InjectionScope.Singleton)]
    public class BPPHandlerServiceForCreditRecord : IBPPHandlerService
    {
        private readonly ID365SystemConfigurationRepositoryCacheProxy _d365ConfigRepository;
        private readonly ICrmServiceGenerateService _crmServiceGenerateService;
        private readonly IBPPEndpointRepositoryCacheProxy _bPPEndpointRepositoryCacheProxy;

        public BPPHandlerServiceForCreditRecord(
            ID365SystemConfigurationRepositoryCacheProxy d365ConfigRepository,
            ICrmServiceGenerateService crmServiceGenerateService,
            IBPPEndpointRepositoryCacheProxy bPPEndpointRepositoryCacheProxy)
        {
            _d365ConfigRepository = d365ConfigRepository;
            _crmServiceGenerateService = crmServiceGenerateService;
            _bPPEndpointRepositoryCacheProxy = bPPEndpointRepositoryCacheProxy;
        }

        /// <summary>
        /// 封装BPP流程发起参数
        /// </summary>
        public async Task<BPPFormData> GetBppFormData(Guid EntityId, string EntityName)
        {
            try
            {
                LoggerMainHelper.LogInformation($"BPPHandlerServiceForCreditRecord.GetBppFormData 开始", $"EntityId={EntityId}, EntityName={EntityName}");

                var config = await _d365ConfigRepository.QueryByName(SystemConfigurationNames.BPPWorkFlowTemplateCode);
                LoggerMainHelper.LogInformation($"BPPHandlerServiceForCreditRecord.GetBppFormData 查询配置", $"ConfigName={SystemConfigurationNames.BPPWorkFlowTemplateCode}, ConfigContent={(config?.Content ?? "null")}");
                if (config == null || string.IsNullOrWhiteSpace(config.Content))
                {
                    var errMsg = $"找不到配置Name为{SystemConfigurationNames.BPPWorkFlowTemplateCode}的系统配置!";
                    await WriteErrorToCreditRecordAsync(EntityId, $"[GetBppFormData] {errMsg}");
                    throw new UtilityException((int)MainErrorCodes.DefaultErrorCode, errMsg);
                }

                var tags = JsonSerializerHelper.Deserialize<BPP_WorkFlowTemplateCodeEntity>(config.Content);
                if (tags == null || string.IsNullOrWhiteSpace(tags.CustomerCreditEvaluation))
                {
                    var errMsg = $"配置 {SystemConfigurationNames.BPPWorkFlowTemplateCode} 中 CustomerCreditEvaluation 为空或反序列化失败。Content: {config.Content}";
                    await WriteErrorToCreditRecordAsync(EntityId, $"[GetBppFormData] {errMsg}");
                    throw new UtilityException((int)MainErrorCodes.DefaultErrorCode, errMsg);
                }
                LoggerMainHelper.LogInformation($"BPPHandlerServiceForCreditRecord.GetBppFormData 解析TemplateCode", $"CustomerCreditEvaluation={tags.CustomerCreditEvaluation}");

                var crmService = await _crmServiceGenerateService.Generate();
                if (crmService == null)
                {
                    var errMsg = "生成 CRM Service 失败，返回 null";
                    await WriteErrorToCreditRecordAsync(EntityId, $"[GetBppFormData] {errMsg}");
                    throw new UtilityException((int)MainErrorCodes.DefaultErrorCode, errMsg);
                }
                LoggerMainHelper.LogInformation($"BPPHandlerServiceForCreditRecord.GetBppFormData 获取CrmService成功", "");

                // 查询 mcs_credit_record
                var recordFetch = $@"<fetch version=""1.0"" output-format=""xml-platform"" mapping=""logical"" distinct=""false"">
              <entity name=""mcs_credit_record"">
                <attribute name=""mcs_scoreid""/>
                <attribute name=""mcs_applicant""/>
                <attribute name=""mcs_custname""/>
                <attribute name=""mcs_accountid""/>
                <attribute name=""mcs_countrycode""/>
                <attribute name=""mcs_creditscore""/>
                <attribute name=""mcs_cofaceid""/>
                <filter type=""and"">
                  <condition attribute=""mcs_credit_recordid"" operator=""eq"" value=""{EntityId}""/>
                </filter>
              </entity>
            </fetch>";

                var record = await CrmQueryHelper.Query(crmService, "mcs_credit_record", recordFetch);
                LoggerMainHelper.LogInformation($"BPPHandlerServiceForCreditRecord.GetBppFormData 查询credit_record", $"Record={(record == null ? "null" : "found")}");
                if (record == null)
                {
                    var errMsg = $"mcs_credit_record中对应实体不存在，ID：{EntityId}";
                    await WriteErrorToCreditRecordAsync(EntityId, $"[GetBppFormData] {errMsg}");
                    throw new UtilityException((int)MainErrorCodes.DefaultErrorCode, errMsg);
                }

                // 查询 Account 的 accountnumber（客户编码）和信用等级
                string accountCode = string.Empty;
                string creditGrade = string.Empty;
                var accountRef = record.GetLookupEntityReference("mcs_accountid");
                if (accountRef != null)
                {
                    var accountFetch = $@"<fetch version=""1.0"" output-format=""xml-platform"" mapping=""logical"" distinct=""false"">
                  <entity name=""account"">
                    <attribute name=""accountnumber""/>
                    <attribute name=""mcs_creditgrade""/>
                    <filter type=""and"">
                      <condition attribute=""accountid"" operator=""eq"" value=""{accountRef.Id}""/>
                    </filter>
                  </entity>
                </fetch>";
                    var account = await CrmQueryHelper.Query(crmService, "account", accountFetch);
                    accountCode = account?.GetStringValue("accountnumber") ?? string.Empty;
                    creditGrade = account?.GetStringValue("mcs_creditgrade") ?? string.Empty;
                    LoggerMainHelper.LogInformation($"BPPHandlerServiceForCreditRecord.GetBppFormData 查询account", $"AccountCode={accountCode}, CreditGrade={creditGrade}");
                }

                // D365 记录链接
                string recordUrl = string.Empty;
                var baseUrlConfig = await _d365ConfigRepository.QueryByName("D365BaseUrl");
                if (baseUrlConfig != null && !string.IsNullOrWhiteSpace(baseUrlConfig.Content))
                {
                    var baseUrl = baseUrlConfig.Content.TrimEnd('/');
                    recordUrl = $"{baseUrl}/main.aspx?forceUCI=1&pagetype=entityrecord&etn=mcs_credit_record&id={EntityId}";
                }

                var scoreId = record.GetStringValue("mcs_scoreid") ?? EntityId.ToString();
                LoggerMainHelper.LogInformation($"BPPHandlerServiceForCreditRecord.GetBppFormData 组装表单数据", $"ScoreId={scoreId}, AccountCode={accountCode}");

                // 表单变量（与BPP模板字段Code对应）
                var formVars = new Dictionary<string, object?>
                {
                    ["mcs_scoreid"] = record.GetStringValue("mcs_scoreid"),
                    ["mcs_applicant"] = record.GetStringValue("mcs_applicant"),
                    ["mcs_custname"] = record.GetStringValue("mcs_custname"),
                    ["mcs_accountcode"] = accountCode,
                    ["mcs_countrycode"] = record.GetStringValue("mcs_countrycode"),
                    ["mcs_creditscore"] = record.GetDecimalValue("mcs_creditscore"),
                    ["mcs_creditgrade"] = creditGrade,
                    ["mcs_cofaceid"] = record.GetStringValue("mcs_cofaceid"),
                    ["mcs_approver"] = "gw_qiuzw",
                    ["mcs_credit_record_url"] = recordUrl
                };

                // 发起前清空错误信息
                await ClearErrorMessageAsync(EntityId);

                var result = new BPPFormData
                {
                    TemplateCode = tags.CustomerCreditEvaluation,
                    TitleName = $"Customer Credit Evaluation Approval: {scoreId}",
                    TitleNameCn = $"客户信用评估审批: {scoreId}",
                    FormVarChaInfo = JsonSerializerHelper.Serializer(formVars),
                    VariableInfo = new Dictionary<string, object>()
                };
                LoggerMainHelper.LogInformation($"BPPHandlerServiceForCreditRecord.GetBppFormData 完成", $"TemplateCode={result.TemplateCode}, TitleName={result.TitleName}");
                return result;
            }
            catch (UtilityException)
            {
                // 已经回写过错误，直接抛出
                throw;
            }
            catch (Exception ex)
            {
                var errMsg = $"[GetBppFormData] 未捕获异常: {ex.GetType().Name}: {ex.Message}\nStackTrace: {ex.StackTrace}";
                await WriteErrorToCreditRecordAsync(EntityId, errMsg);
                throw new UtilityException((int)MainErrorCodes.DefaultErrorCode, errMsg);
            }
        }

        /// <summary>
        /// 信息附件
        /// </summary>
        public async Task<List<FileDataDTO>> GetFileData(Guid EntityId, string EntityName)
        {
            return new List<FileDataDTO>();
        }

        /// <summary>
        /// 流程发起失败
        /// </summary>
        public async Task RollbackEntityStatusForFail(Guid EntityId, string EntityName)
        {
            // 暂不处理。BppIntegrationPlugin在调用mcs_bppstartapi失败时会抛出异常，D365事务会自动回滚到状态13。
            // 错误信息已在GetBppFormData中回写。
        }

        /// <summary>
        /// 流程发起前处理：驳回后重新提交时，废弃旧 BPP 流程实例，强制创建新实例
        /// 参考 LC 限额逻辑：重新提交时生成新的审批链接
        /// </summary>
        public async Task PreStart(Guid EntityId, string EntityName)
        {
            try
            {
                var crmService = await _crmServiceGenerateService.Generate();
                if (crmService == null) return;

                // 查询该记录已有的 mcs_bppapply（非 Approved 状态）
                var result = await crmService.RetrieveMultiple("mcs_bppapply",
                    $"$select=mcs_bppapplyid,mcs_workflowid,mcs_bppstatus&" +
                    $"$filter=statecode eq 0 and mcs_entityid eq '{EntityId}' and mcs_entityname eq '{EntityName}' and mcs_bppstatus ne 30");

                if (result.Results.Count == 0) return;

                var oldApply = result.Results[0];
                var oldStatus = oldApply.GetOptionSetValue("mcs_bppstatus");

                // 只有已结束（非 Pending/Submitted=20）的旧流程才清理，避免误清理进行中的流程
                if (oldStatus == 20) return;

                LoggerMainHelper.LogInformation($"BPPHandlerServiceForCreditRecord.PreStart 清理旧流程",
                    $"EntityId={EntityId}, OldWorkflowId={oldApply.GetStringValue("mcs_workflowid")}, OldStatus={oldStatus}");

                // 1. 禁用旧 mcs_bppapply 记录
                var disableApply = new CrmExecuteEntity("mcs_bppapply", oldApply.Id);
                disableApply.Attributes.Add("statecode", 1); // 1 = Inactive
                await crmService.Update(disableApply);

                // 2. 清空业务实体上的旧 BPP 标识，让 BPPService.Start 走 Init 创建新实例
                var clearRecord = new CrmExecuteEntity(EntityName, EntityId);
                clearRecord.Attributes.Add("mcs_workflowid", string.Empty);
                clearRecord.Attributes.Add("mcs_nextapprover", string.Empty);
                clearRecord.Attributes.Add("mcs_bpplink", string.Empty);
                await crmService.Update(clearRecord);
            }
            catch (Exception ex)
            {
                LoggerMainHelper.LogError($"BPPHandlerServiceForCreditRecord.PreStart 异常", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// 流程发起成功
        /// 参考 LC 限额：拼接并更新 BPP 审批链接
        /// </summary>
        public async Task UpdateEntityStatusForStart(Guid EntityId, string EntityName, string flowId)
        {
            try
            {
                var crmService = await _crmServiceGenerateService.Generate();
                var updateEntity = new CrmExecuteEntity(EntityName, EntityId);
                updateEntity.Attributes.Add("mcs_bppstatus", "Submitted");
                updateEntity.Attributes.Add("mcs_bpperrormsg", string.Empty);

                // 拼接 BPP 审批链接（与 LC 限额保持一致，从配置读取基础 URL）
                var config = await _d365ConfigRepository.QueryByName("Bpp_ApprovalFlowBaseUrl");
                if (config != null && !string.IsNullOrWhiteSpace(config.Content))
                {
                    updateEntity.Attributes.Add("mcs_bpplink", $"{config.Content}{flowId}");
                }

                // 取当前审批人并回写 mcs_nextapprover
                var currentApprover = await GetCurrentApprover(flowId);
                if (!string.IsNullOrWhiteSpace(currentApprover))
                {
                    updateEntity.Attributes.Add("mcs_nextapprover", currentApprover);
                }

                await crmService.Update(updateEntity);
            }
            catch (Exception ex)
            {
                var errMsg = $"[UpdateEntityStatusForStart] 更新状态失败: {ex.GetType().Name}: {ex.Message}";
                await WriteErrorToCreditRecordAsync(EntityId, errMsg);
                throw;
            }
        }

        /// <summary>
        /// 回调统一处理
        /// </summary>
        public async Task CallBack(BPPCallBackDTO request)
        {
            if (request == null || request.EntityID == null)
            {
                throw new UtilityException((int)MainErrorCodes.BppCalBackError,
                    $"BPPHandlerServiceForCreditRecord CallBack request is null or it's EntityID is null");
            }

            var entityId = Guid.Parse(request.EntityID);

            try
            {
                var crmService = await _crmServiceGenerateService.Generate();
                var updateEntity = new CrmExecuteEntity(request.EntityName, entityId);

                // BPP Status: 11=驳回, 30=完成
                // mcs_credit_record.mcs_bppstatus 是 String 类型，需要转换为字符串
                string bppStatusString = request.Status switch
                {
                    30 => "Approved",
                    11 => "Rejected",
                    _ => request.Status.ToString()
                };
                updateEntity.Attributes.Add("mcs_bppstatus", bppStatusString);

                if (!string.IsNullOrWhiteSpace(request.Reason))
                {
                    updateEntity.Attributes.Add("mcs_bpprejectreason", request.Reason);
                }

                if (request.Status == 30 || request.Status == 11)
                {
                    updateEntity.Attributes.Add("mcs_approvedate", DateTime.Now);
                }

                // 每次回调都取当前审批人回写 mcs_nextapprover；审批结束后 GetNextApprover 返回空，保留已有值
                if (request.FlowId.HasValue)
                {
                    var currentApprover = await GetCurrentApprover(request.FlowId.Value.ToString());
                    if (!string.IsNullOrWhiteSpace(currentApprover))
                    {
                        updateEntity.Attributes.Add("mcs_nextapprover", currentApprover);
                    }
                }

                await crmService.Update(updateEntity);
            }
            catch (Exception ex)
            {
                var errMsg = $"[CallBack] 处理回调失败: Status={request.Status}, Reason={request.Reason}, Exception={ex.GetType().Name}: {ex.Message}";
                await WriteErrorToCreditRecordAsync(entityId, errMsg);
                throw;
            }
        }

        /// <summary>
        /// 获取BPP当前审批人并按45字符截断
        /// </summary>
        private async Task<string> GetCurrentApprover(string flowId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(flowId))
                {
                    return string.Empty;
                }

                var endpoint = await _bPPEndpointRepositoryCacheProxy.QueryByName(BPPEndpointNames.Default);
                var userId = await endpoint.GetNextApprover("niutd", flowId);
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return string.Empty;
                }

                // mcs_nextapprover 字段长度为 100
                return userId.Length > 100 ? userId.Substring(0, 100) : userId;
            }
            catch (Exception ex)
            {
                LoggerMainHelper.LogInformation($"BPPHandlerServiceForCreditRecord.GetCurrentApprover 失败", ex.Message);
                return string.Empty;
            }
        }

        /// <summary>
        /// 将错误信息写回 mcs_credit_record.mcs_bpperrormsg
        /// </summary>
        private async Task WriteErrorToCreditRecordAsync(Guid entityId, string errorMessage)
        {
            try
            {
                var crmService = await _crmServiceGenerateService.Generate();
                if (crmService == null) return;

                var updateEntity = new CrmExecuteEntity("mcs_credit_record", entityId);
                var msg = errorMessage ?? string.Empty;
                if (msg.Length > 1000)
                {
                    msg = msg.Substring(0, 1000);
                }
                updateEntity.Attributes.Add("mcs_bpperrormsg", msg);
                await crmService.Update(updateEntity);
            }
            catch
            {
                // 回写错误时不能再抛异常，避免影响主流程
            }
        }

        /// <summary>
        /// 清空错误信息
        /// </summary>
        private async Task ClearErrorMessageAsync(Guid entityId)
        {
            try
            {
                var crmService = await _crmServiceGenerateService.Generate();
                if (crmService == null) return;

                var updateEntity = new CrmExecuteEntity("mcs_credit_record", entityId);
                updateEntity.Attributes.Add("mcs_bpperrormsg", string.Empty);
                await crmService.Update(updateEntity);
            }
            catch
            {
                // 非关键操作，忽略异常
            }
        }
    }
}




