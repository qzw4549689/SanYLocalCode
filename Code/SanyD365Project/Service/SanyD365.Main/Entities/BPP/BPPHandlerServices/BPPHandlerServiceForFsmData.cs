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
    /// 融资管理 BPP 处理类（立项审批 / 融资方案审批）
    /// 根据 mcs_fsm_data.mcs_approve_type 区分审批类型：1 立项审批 / 2 融资方案审批
    /// </summary>
    [Injection(InterfaceType = typeof(BPPHandlerServiceForFsmData), Scope = InjectionScope.Singleton)]
    public class BPPHandlerServiceForFsmData : IBPPHandlerService
    {
        private readonly ID365SystemConfigurationRepositoryCacheProxy _d365ConfigRepository;
        private readonly ICrmServiceGenerateService _crmServiceGenerateService;
        private readonly IBPPEndpointRepositoryCacheProxy _bPPEndpointRepositoryCacheProxy;

        // mcs_fsm_data.mcs_approve_type 选项集值
        private const int APPROVE_TYPE_INITIATION = 1;  // 立项审批
        private const int APPROVE_TYPE_PROJECT = 2;     // 融资方案审批

        public BPPHandlerServiceForFsmData(
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
                LoggerMainHelper.LogInformation($"BPPHandlerServiceForFsmData.GetBppFormData 开始", $"EntityId={EntityId}, EntityName={EntityName}");

                var config = await _d365ConfigRepository.QueryByName(SystemConfigurationNames.BPPWorkFlowTemplateCode);
                LoggerMainHelper.LogInformation($"BPPHandlerServiceForFsmData.GetBppFormData 查询配置", $"ConfigName={SystemConfigurationNames.BPPWorkFlowTemplateCode}, ConfigContent={(config?.Content ?? "null")}");
                if (config == null || string.IsNullOrWhiteSpace(config.Content))
                {
                    var errMsg = $"找不到配置Name为{SystemConfigurationNames.BPPWorkFlowTemplateCode}的系统配置!";
                    await WriteErrorToFsmDataAsync(EntityId, $"[GetBppFormData] {errMsg}");
                    throw new UtilityException((int)MainErrorCodes.DefaultErrorCode, errMsg);
                }

                var tags = JsonSerializerHelper.Deserialize<BPP_WorkFlowTemplateCodeEntity>(config.Content);
                if (tags == null)
                {
                    var errMsg = $"配置 {SystemConfigurationNames.BPPWorkFlowTemplateCode} 反序列化失败。Content: {config.Content}";
                    await WriteErrorToFsmDataAsync(EntityId, $"[GetBppFormData] {errMsg}");
                    throw new UtilityException((int)MainErrorCodes.DefaultErrorCode, errMsg);
                }

                var crmService = await _crmServiceGenerateService.Generate();
                if (crmService == null)
                {
                    var errMsg = "生成 CRM Service 失败，返回 null";
                    await WriteErrorToFsmDataAsync(EntityId, $"[GetBppFormData] {errMsg}");
                    throw new UtilityException((int)MainErrorCodes.DefaultErrorCode, errMsg);
                }
                LoggerMainHelper.LogInformation($"BPPHandlerServiceForFsmData.GetBppFormData 获取CrmService成功", "");

                // 查询 mcs_fsm_data
                var recordFetch = $@"<fetch version=""1.0"" output-format=""xml-platform"" mapping=""logical"" distinct=""false"">
              <entity name=""mcs_fsm_data"">
                <attribute name=""mcs_fsm_no""/>
                <attribute name=""mcs_approve_type""/>
                <attribute name=""mcs_customer_name""/>
                <attribute name=""mcs_customer_id""/>
                <attribute name=""mcs_fsm_manager""/>
                <attribute name=""mcs_fsm_initiation_remark""/>
                <attribute name=""mcs_fsm_project_remark""/>
                <attribute name=""createdon""/>
                <filter type=""and"">
                  <condition attribute=""mcs_fsm_dataid"" operator=""eq"" value=""{EntityId}""/>
                </filter>
              </entity>
            </fetch>";

                var record = await CrmQueryHelper.Query(crmService, "mcs_fsm_data", recordFetch);
                LoggerMainHelper.LogInformation($"BPPHandlerServiceForFsmData.GetBppFormData 查询fsm_data", $"Record={(record == null ? "null" : "found")}");
                if (record == null)
                {
                    var errMsg = $"mcs_fsm_data中对应实体不存在，ID：{EntityId}";
                    await WriteErrorToFsmDataAsync(EntityId, $"[GetBppFormData] {errMsg}");
                    throw new UtilityException((int)MainErrorCodes.DefaultErrorCode, errMsg);
                }

                // 按审批类型选择 BPP 模板与标题
                int approveType = record.GetOptionSetValue("mcs_approve_type");
                string templateCode;
                string titleName;
                string titleNameCn;
                var fsmNo = record.GetStringValue("mcs_fsm_no") ?? EntityId.ToString();

                if (approveType == APPROVE_TYPE_INITIATION)
                {
                    templateCode = tags.FsmDataInitiation;
                    titleName = $"Financing Initiation Approval: {fsmNo}";
                    titleNameCn = $"融资立项审批: {fsmNo}";
                }
                else if (approveType == APPROVE_TYPE_PROJECT)
                {
                    templateCode = tags.FsmDataProject;
                    titleName = $"Financing Solution Approval: {fsmNo}";
                    titleNameCn = $"融资方案审批: {fsmNo}";
                }
                else
                {
                    var errMsg = $"mcs_fsm_data 审批类型(mcs_approve_type)为空或非法({approveType})，无法发起BPP审批。ID：{EntityId}";
                    await WriteErrorToFsmDataAsync(EntityId, $"[GetBppFormData] {errMsg}");
                    throw new UtilityException((int)MainErrorCodes.DefaultErrorCode, errMsg);
                }

                if (string.IsNullOrWhiteSpace(templateCode))
                {
                    var errMsg = $"配置 {SystemConfigurationNames.BPPWorkFlowTemplateCode} 中 {(approveType == APPROVE_TYPE_INITIATION ? "FsmDataInitiation" : "FsmDataProject")} 为空，请联系管理员配置BPP模板Code。Content: {config.Content}";
                    await WriteErrorToFsmDataAsync(EntityId, $"[GetBppFormData] {errMsg}");
                    throw new UtilityException((int)MainErrorCodes.DefaultErrorCode, errMsg);
                }
                LoggerMainHelper.LogInformation($"BPPHandlerServiceForFsmData.GetBppFormData 解析TemplateCode", $"ApproveType={approveType}, TemplateCode={templateCode}");

                // 客户名称（Lookup → mcs_customermasterdata，取显示名）
                string customerName = record.GetLookupEntityReference("mcs_customer_name")?.Name ?? string.Empty;
                // 申请人/融资经理（Lookup → systemuser，取显示名）
                string managerName = record.GetLookupEntityReference("mcs_fsm_manager")?.Name ?? string.Empty;

                // D365 记录链接
                string recordUrl = string.Empty;
                var baseUrlConfig = await _d365ConfigRepository.QueryByName("D365BaseUrl");
                if (baseUrlConfig != null && !string.IsNullOrWhiteSpace(baseUrlConfig.Content))
                {
                    var baseUrl = baseUrlConfig.Content.TrimEnd('/');
                    recordUrl = $"{baseUrl}/main.aspx?forceUCI=1&pagetype=entityrecord&etn=mcs_fsm_data&id={EntityId}";
                }

                LoggerMainHelper.LogInformation($"BPPHandlerServiceForFsmData.GetBppFormData 组装表单数据", $"FsmNo={fsmNo}, CustomerName={customerName}, CustomerId={record.GetStringValue("mcs_customer_id")}, Manager={managerName}");

                // Bug #1561：提交审批备注（评审意见），按审批类型分流：
                // 立项审批取 mcs_fsm_initiation_remark，融资方案审批取 mcs_fsm_project_remark
                var submitRemark = approveType == APPROVE_TYPE_INITIATION
                    ? record.GetStringValue("mcs_fsm_initiation_remark")
                    : record.GetStringValue("mcs_fsm_project_remark");

                // 表单变量（与 BPP 模板字段 Code 对应）
                // mcs_approver 传空字符串，审批人员由 BPP 模板配置
                var formVars = new Dictionary<string, object?>
                {
                    ["mcs_fsm_managment_no"] = fsmNo,
                    ["mcs_fsm_data_url"] = recordUrl,
                    ["mcs_customer_name"] = customerName,
                    ["mcs_customer_id"] = record.GetStringValue("mcs_customer_id"),
                    ["mcs_fsm_manager"] = managerName,
                    ["mcs_approver"] = string.Empty,
                    ["mcs_applydate"] = record.GetTimeStringOrDefault("createdon", "yyyy-MM-dd", string.Empty)
                };

                // 发起前清空错误信息
                await ClearErrorMessageAsync(EntityId);

                var result = new BPPFormData
                {
                    TemplateCode = templateCode,
                    TitleName = titleName,
                    TitleNameCn = titleNameCn,
                    FormVarChaInfo = JsonSerializerHelper.Serializer(formVars),
                    VariableInfo = new Dictionary<string, object>(),
                    // Bug #1561：评审意见放平台级「发起/重提时审批意见」字段（审批记录-起草人节点下展示，
                    // 参照 FundClaim 先例），不走表单变量（融资两个模板无备注字段 Code，BPP 模板侧零改动）
                    ApproveOpn = submitRemark,
                    RetryApproveOpn = submitRemark
                };
                LoggerMainHelper.LogInformation($"BPPHandlerServiceForFsmData.GetBppFormData 完成", $"TemplateCode={result.TemplateCode}, TitleName={result.TitleName}");
                return result;
            }
            catch (UtilityException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var errMsg = $"[GetBppFormData] 未捕获异常: {ex.GetType().Name}: {ex.Message}\nStackTrace: {ex.StackTrace}";
                await WriteErrorToFsmDataAsync(EntityId, errMsg);
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
            // FsmDataBppIntegrationPlugin 在调用 mcs_bppstartapi 失败时会抛出异常，D365 事务自动回滚。
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

                LoggerMainHelper.LogInformation($"BPPHandlerServiceForFsmData.PreStart 清理旧流程",
                    $"EntityId={EntityId}, OldWorkflowId={oldApply.GetStringValue("mcs_workflowid")}, OldStatus={oldStatus}");

                // 1. 禁用旧 mcs_bppapply 记录
                var disableApply = new CrmExecuteEntity("mcs_bppapply", oldApply.Id);
                disableApply.Attributes.Add("statecode", 1);
                await crmService.Update(disableApply);

                // 2. 清空业务实体上的旧 BPP 标识
                var clearRecord = new CrmExecuteEntity(EntityName, EntityId);
                clearRecord.Attributes.Add("mcs_bppid", string.Empty);
                clearRecord.Attributes.Add("mcs_bppstatuscode", string.Empty);
                clearRecord.Attributes.Add("mcs_fsm_data_url", string.Empty);
                await crmService.Update(clearRecord);
            }
            catch (Exception ex)
            {
                LoggerMainHelper.LogError($"BPPHandlerServiceForFsmData.PreStart 异常", ex.Message);
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

                // 拼接 BPP 审批链接并回写表单字段，供用户点击跳转 BPP 审批界面
                var config = await _d365ConfigRepository.QueryByName("Bpp_ApprovalFlowBaseUrl");
                if (config != null && !string.IsNullOrWhiteSpace(config.Content))
                {
                    updateEntity.Attributes.Add("mcs_fsm_data_url", $"{config.Content}{flowId}");
                }

                // 取当前审批人并回写 mcs_nextapprover（与 BPP 框架通用回写字段一致）
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
                await WriteErrorToFsmDataAsync(EntityId, errMsg);
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
                    $"BPPHandlerServiceForFsmData CallBack request is null or it's EntityID is null");
            }

            var entityId = Guid.Parse(request.EntityID);

            try
            {
                var crmService = await _crmServiceGenerateService.Generate();
                var updateEntity = new CrmExecuteEntity(request.EntityName, entityId);

                // BPP Status: 11=驳回, 30=完成
                // mcs_fsm_data.mcs_bppstatuscode 是 String 类型
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

                // 每次回调都取当前审批人回写 mcs_nextapprover（与 BPP 框架通用回写字段一致）
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
                await WriteErrorToFsmDataAsync(entityId, errMsg);
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
                LoggerMainHelper.LogInformation($"BPPHandlerServiceForFsmData.GetCurrentApprover 失败", ex.Message);
                return string.Empty;
            }
        }

        /// <summary>
        /// 将错误信息写回 mcs_fsm_data.mcs_bpperrormsg
        /// </summary>
        private async Task WriteErrorToFsmDataAsync(Guid entityId, string errorMessage)
        {
            try
            {
                var crmService = await _crmServiceGenerateService.Generate();
                if (crmService == null) return;

                var updateEntity = new CrmExecuteEntity("mcs_fsm_data", entityId);
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

                var updateEntity = new CrmExecuteEntity("mcs_fsm_data", entityId);
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
