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
    /// 厂端授信额度调整申请 BPP 处理类
    /// </summary>
    [Injection(InterfaceType = typeof(BPPHandlerServiceForFcaQuotaApp), Scope = InjectionScope.Singleton)]
    public class BPPHandlerServiceForFcaQuotaApp : IBPPHandlerService
    {
        private readonly ID365SystemConfigurationRepositoryCacheProxy _d365ConfigRepository;
        private readonly ICrmServiceGenerateService _crmServiceGenerateService;
        private readonly IBPPEndpointRepositoryCacheProxy _bPPEndpointRepositoryCacheProxy;

        public BPPHandlerServiceForFcaQuotaApp(
            ID365SystemConfigurationRepositoryCacheProxy d365ConfigRepository,
            ICrmServiceGenerateService crmServiceGenerateService,
            IBPPEndpointRepositoryCacheProxy bPPEndpointRepositoryCacheProxy)
        {
            _d365ConfigRepository = d365ConfigRepository;
            _crmServiceGenerateService = crmServiceGenerateService;
            _bPPEndpointRepositoryCacheProxy = bPPEndpointRepositoryCacheProxy;
        }

        /// <summary>
        /// 封装 BPP 流程发起参数
        /// </summary>
        public async Task<BPPFormData> GetBppFormData(Guid EntityId, string EntityName)
        {
            try
            {
                LoggerMainHelper.LogInformation($"BPPHandlerServiceForFcaQuotaApp.GetBppFormData 开始", $"EntityId={EntityId}, EntityName={EntityName}");

                var config = await _d365ConfigRepository.QueryByName(SystemConfigurationNames.BPPWorkFlowTemplateCode);
                LoggerMainHelper.LogInformation($"BPPHandlerServiceForFcaQuotaApp.GetBppFormData 查询配置", $"ConfigName={SystemConfigurationNames.BPPWorkFlowTemplateCode}, ConfigContent={(config?.Content ?? "null")}");
                if (config == null || string.IsNullOrWhiteSpace(config.Content))
                {
                    var errMsg = $"找不到配置Name为{SystemConfigurationNames.BPPWorkFlowTemplateCode}的系统配置!";
                    await WriteErrorToQuotaAppAsync(EntityId, $"[GetBppFormData] {errMsg}");
                    throw new UtilityException((int)MainErrorCodes.DefaultErrorCode, errMsg);
                }

                var tags = JsonSerializerHelper.Deserialize<BPP_WorkFlowTemplateCodeEntity>(config.Content);
                if (tags == null || string.IsNullOrWhiteSpace(tags.FactoryCreditQuotaApp))
                {
                    var errMsg = $"配置 {SystemConfigurationNames.BPPWorkFlowTemplateCode} 中 FactoryCreditQuotaApp 为空或反序列化失败。Content: {config.Content}";
                    await WriteErrorToQuotaAppAsync(EntityId, $"[GetBppFormData] {errMsg}");
                    throw new UtilityException((int)MainErrorCodes.DefaultErrorCode, errMsg);
                }
                LoggerMainHelper.LogInformation($"BPPHandlerServiceForFcaQuotaApp.GetBppFormData 解析TemplateCode", $"FactoryCreditQuotaApp={tags.FactoryCreditQuotaApp}");

                var crmService = await _crmServiceGenerateService.Generate();
                if (crmService == null)
                {
                    var errMsg = "生成 CRM Service 失败，返回 null";
                    await WriteErrorToQuotaAppAsync(EntityId, $"[GetBppFormData] {errMsg}");
                    throw new UtilityException((int)MainErrorCodes.DefaultErrorCode, errMsg);
                }
                LoggerMainHelper.LogInformation($"BPPHandlerServiceForFcaQuotaApp.GetBppFormData 获取CrmService成功", "");

                // 查询 mcs_fca_quotaapp
                var recordFetch = $@"<fetch version=""1.0"" output-format=""xml-platform"" mapping=""logical"" distinct=""false"">
              <entity name=""mcs_fca_quotaapp"">
                <attribute name=""mcs_grantid""/>
                <attribute name=""mcs_accountid""/>
                <attribute name=""mcs_custname""/>
                <attribute name=""mcs_sellergrant""/>
                <attribute name=""mcs_sellerbalance""/>
                <attribute name=""mcs_tobegrant""/>
                <attribute name=""mcs_tobebalance""/>
                <attribute name=""mcs_remark""/>
                <attribute name=""mcs_applicant""/>
                <attribute name=""mcs_applydate""/>
                <filter type=""and"">
                  <condition attribute=""mcs_fca_quotaappid"" operator=""eq"" value=""{EntityId}""/>
                </filter>
              </entity>
            </fetch>";

                var record = await CrmQueryHelper.Query(crmService, "mcs_fca_quotaapp", recordFetch);
                LoggerMainHelper.LogInformation($"BPPHandlerServiceForFcaQuotaApp.GetBppFormData 查询quota_app", $"Record={(record == null ? "null" : "found")}");
                if (record == null)
                {
                    var errMsg = $"mcs_fca_quotaapp中对应实体不存在，ID：{EntityId}";
                    await WriteErrorToQuotaAppAsync(EntityId, $"[GetBppFormData] {errMsg}");
                    throw new UtilityException((int)MainErrorCodes.DefaultErrorCode, errMsg);
                }

                // 查询客户主数据获取客户编码（mcs_sapnumber）
                string accountCode = string.Empty;
                var accountRef = record.GetLookupEntityReference("mcs_accountid");
                if (accountRef != null)
                {
                    var accountFetch = $@"<fetch version=""1.0"" output-format=""xml-platform"" mapping=""logical"" distinct=""false"">
                  <entity name=""account"">
                    <attribute name=""mcs_customermasterdata""/>
                    <filter type=""and"">
                      <condition attribute=""accountid"" operator=""eq"" value=""{accountRef.Id}""/>
                    </filter>
                  </entity>
                </fetch>";
                    var account = await CrmQueryHelper.Query(crmService, "account", accountFetch);
                    var customerMasterDataRef = account?.GetLookupEntityReference("mcs_customermasterdata");
                    if (customerMasterDataRef != null)
                    {
                        var cmdFetch = $@"<fetch version=""1.0"" output-format=""xml-platform"" mapping=""logical"" distinct=""false"">
                      <entity name=""mcs_customermasterdata"">
                        <attribute name=""mcs_sapnumber""/>
                        <filter type=""and"">
                          <condition attribute=""mcs_customermasterdataid"" operator=""eq"" value=""{customerMasterDataRef.Id}""/>
                        </filter>
                      </entity>
                    </fetch>";
                        var cmd = await CrmQueryHelper.Query(crmService, "mcs_customermasterdata", cmdFetch);
                        accountCode = cmd?.GetStringValue("mcs_sapnumber") ?? string.Empty;
                    }
                    LoggerMainHelper.LogInformation($"BPPHandlerServiceForFcaQuotaApp.GetBppFormData 查询客户编码", $"AccountCode={accountCode}");
                }

                // D365 记录链接
                string recordUrl = string.Empty;
                var baseUrlConfig = await _d365ConfigRepository.QueryByName("D365BaseUrl");
                if (baseUrlConfig != null && !string.IsNullOrWhiteSpace(baseUrlConfig.Content))
                {
                    var baseUrl = baseUrlConfig.Content.TrimEnd('/');
                    recordUrl = $"{baseUrl}/main.aspx?forceUCI=1&pagetype=entityrecord&etn=mcs_fca_quotaapp&id={EntityId}";
                }

                var grantId = record.GetStringValue("mcs_grantid") ?? EntityId.ToString();
                LoggerMainHelper.LogInformation($"BPPHandlerServiceForFcaQuotaApp.GetBppFormData 组装表单数据", $"GrantId={grantId}, AccountCode={accountCode}");

                // 表单变量（与 BPP 模板字段 Code 对应）
                // mcs_approver 传空字符串，审批人员由 BPP 模板配置
                var formVars = new Dictionary<string, object?>
                {
                    ["mcs_grantid"] = record.GetStringValue("mcs_grantid"),
                    ["mcs_fca_quotaapp_url"] = recordUrl,
                    ["mcs_accountcode"] = accountCode,
                    ["mcs_custname"] = record.GetStringValue("mcs_custname"),
                    ["mcs_sellergrant"] = record.GetDecimalValue("mcs_sellergrant"),
                    ["mcs_sellerbalance"] = record.GetDecimalValue("mcs_sellerbalance"),
                    ["mcs_tobegrant"] = record.GetDecimalValue("mcs_tobegrant"),
                    ["mcs_tobebalance"] = record.GetDecimalValue("mcs_tobebalance"),
                    ["mcs_remark"] = record.GetStringValue("mcs_remark"),
                    ["mcs_applicant"] = record.GetStringValue("mcs_applicant"),
                    ["mcs_approver"] = string.Empty,
                    ["mcs_applydate"] = record.GetStringValue("mcs_applydate")
                };

                // 发起前清空错误信息
                await ClearErrorMessageAsync(EntityId);

                var result = new BPPFormData
                {
                    TemplateCode = tags.FactoryCreditQuotaApp,
                    TitleName = $"Factory Credit Quota Adjustment Approval: {grantId}",
                    TitleNameCn = $"厂端授信额度调整申请审批: {grantId}",
                    FormVarChaInfo = JsonSerializerHelper.Serializer(formVars),
                    VariableInfo = new Dictionary<string, object>()
                };
                LoggerMainHelper.LogInformation($"BPPHandlerServiceForFcaQuotaApp.GetBppFormData 完成", $"TemplateCode={result.TemplateCode}, TitleName={result.TitleName}");
                return result;
            }
            catch (UtilityException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var errMsg = $"[GetBppFormData] 未捕获异常: {ex.GetType().Name}: {ex.Message}\nStackTrace: {ex.StackTrace}";
                await WriteErrorToQuotaAppAsync(EntityId, errMsg);
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
            // FcaQuotaAppBppIntegrationPlugin 在调用 mcs_bppstartapi 失败时会抛出异常，D365 事务自动回滚。
            // 错误信息已在 GetBppFormData 中回写。
        }

        /// <summary>
        /// 流程发起前处理：驳回后重新提交时，废弃旧 BPP 流程实例
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

                // 只有已结束（非 Pending/Submitted=20）的旧流程才清理
                if (oldStatus == 20) return;

                LoggerMainHelper.LogInformation($"BPPHandlerServiceForFcaQuotaApp.PreStart 清理旧流程",
                    $"EntityId={EntityId}, OldWorkflowId={oldApply.GetStringValue("mcs_workflowid")}, OldStatus={oldStatus}");

                // 1. 禁用旧 mcs_bppapply 记录
                var disableApply = new CrmExecuteEntity("mcs_bppapply", oldApply.Id);
                disableApply.Attributes.Add("statecode", 1);
                await crmService.Update(disableApply);

                // 2. 清空业务实体上的旧 BPP 标识
                var clearRecord = new CrmExecuteEntity(EntityName, EntityId);
                clearRecord.Attributes.Add("mcs_bppid", string.Empty);
                clearRecord.Attributes.Add("mcs_bppstatuscode", string.Empty);
                clearRecord.Attributes.Add("mcs_fca_quotaapp_url", string.Empty);
                await crmService.Update(clearRecord);
            }
            catch (Exception ex)
            {
                LoggerMainHelper.LogError($"BPPHandlerServiceForFcaQuotaApp.PreStart 异常", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// 流程发起成功
        /// </summary>
        public async Task UpdateEntityStatusForStart(Guid EntityId, string EntityName, string flowId)
        {
            try
            {
                var crmService = await _crmServiceGenerateService.Generate();
                var updateEntity = new CrmExecuteEntity(EntityName, EntityId);
                updateEntity.Attributes.Add("mcs_bppstatuscode", "Submitted");
                updateEntity.Attributes.Add("mcs_bpperrormsg", string.Empty);

                // 拼接 BPP 审批链接并回写表单字段，供用户复制到 BPP 审批界面
                var config = await _d365ConfigRepository.QueryByName("Bpp_ApprovalFlowBaseUrl");
                if (config != null && !string.IsNullOrWhiteSpace(config.Content))
                {
                    updateEntity.Attributes.Add("mcs_fca_quotaapp_url", $"{config.Content}{flowId}");
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
                await WriteErrorToQuotaAppAsync(EntityId, errMsg);
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
                    $"BPPHandlerServiceForFcaQuotaApp CallBack request is null or it's EntityID is null");
            }

            var entityId = Guid.Parse(request.EntityID);

            try
            {
                var crmService = await _crmServiceGenerateService.Generate();
                var updateEntity = new CrmExecuteEntity(request.EntityName, entityId);

                // BPP Status: 11=驳回, 30=完成
                // mcs_fca_quotaapp.mcs_bppstatuscode 是 String 类型
                string bppStatusString = request.Status switch
                {
                    30 => "Approved",
                    11 => "Rejected",
                    _ => request.Status.ToString()
                };
                updateEntity.Attributes.Add("mcs_bppstatuscode", bppStatusString);

                if (!string.IsNullOrWhiteSpace(request.Reason))
                {
                    updateEntity.Attributes.Add("mcs_bpprejectreason", request.Reason);
                }

                if (request.Status == 30 || request.Status == 11)
                {
                    updateEntity.Attributes.Add("mcs_approvedate", DateTime.Now);
                }

                // 每次回调都取当前审批人回写 mcs_nextapprover
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
                await WriteErrorToQuotaAppAsync(entityId, errMsg);
                throw;
            }
        }

        /// <summary>
        /// 获取 BPP 当前审批人并按 100 字符截断
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

                return userId.Length > 100 ? userId.Substring(0, 100) : userId;
            }
            catch (Exception ex)
            {
                LoggerMainHelper.LogInformation($"BPPHandlerServiceForFcaQuotaApp.GetCurrentApprover 失败", ex.Message);
                return string.Empty;
            }
        }

        /// <summary>
        /// 将错误信息写回 mcs_fca_quotaapp.mcs_bpperrormsg
        /// </summary>
        private async Task WriteErrorToQuotaAppAsync(Guid entityId, string errorMessage)
        {
            try
            {
                var crmService = await _crmServiceGenerateService.Generate();
                if (crmService == null) return;

                var updateEntity = new CrmExecuteEntity("mcs_fca_quotaapp", entityId);
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
                // 回写错误时不能再抛异常
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

                var updateEntity = new CrmExecuteEntity("mcs_fca_quotaapp", entityId);
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


