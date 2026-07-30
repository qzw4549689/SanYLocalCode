using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace SanyD365.Plugins.FinancingManagement.Bpp
{
    /// <summary>
    /// 融资管理 - BPP提交Plugin
    /// 触发时机：mcs_fsm_data Update PostOperation, mcs_bppstatus 字段变更
    /// 业务规则：
    /// 1. 当审批状态从非"审批中"变为 2（审批中）时，校验业务条件后调用 mcs_bppstartapi 发起 BPP 审批
    /// 2. 立项审批：mcs_fsm_status=2（融资立项）且 mcs_can_initiated=1
    /// 3. 融资方案审批：mcs_fsm_status=3（融资解决方案）且 mcs_can_project=1
    /// </summary>
    public class FsmDataBppIntegrationPlugin : IPlugin
    {
        // mcs_fsm_data.mcs_bppstatus 选项集值：2-审批中
        private const int STATUS_IN_REVIEW = 2;

        // mcs_fsm_data.mcs_fsm_status 选项集值
        private const int FSM_STATUS_INITIATION = 2;   // 融资立项
        private const int FSM_STATUS_SOLUTION = 3;     // 融资解决方案

        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = factory.CreateOrganizationService(context.UserId);
            ITracingService tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracer.Trace("=== FsmDataBppIntegrationPlugin 开始执行 ===");
            tracer.Trace($"Message: {context.MessageName}, Stage: {context.Stage}, Depth: {context.Depth}");

            // 防递归
            if (context.Depth > 2)
            {
                tracer.Trace("递归深度超过2，跳过处理");
                return;
            }

            if (context.MessageName != "Update" || context.Stage != 40)
            {
                tracer.Trace("非 Update PostOperation 事件，跳过");
                return;
            }

            if (!context.InputParameters.Contains("Target") || !(context.InputParameters["Target"] is Entity target))
            {
                tracer.Trace("未找到 Target 参数");
                return;
            }

            // 严格校验实体名
            if (target.LogicalName != "mcs_fsm_data")
            {
                tracer.Trace($"非融资管理实体，跳过: {target.LogicalName}");
                return;
            }

            // 只处理审批状态字段变更
            if (!target.Contains("mcs_bppstatus"))
            {
                tracer.Trace("审批状态字段未变更，跳过");
                return;
            }

            int newStatus = GetOptionSetValue(target, "mcs_bppstatus");
            tracer.Trace($"新审批状态: {newStatus}");

            if (newStatus != STATUS_IN_REVIEW)
            {
                tracer.Trace($"当前状态不是审批中({newStatus})，跳过 BPP 处理");
                return;
            }

            // 获取旧状态
            int oldStatus = 0;
            if (context.PreEntityImages.Contains("PreImage"))
            {
                Entity preImage = context.PreEntityImages["PreImage"];
                oldStatus = GetOptionSetValue(preImage, "mcs_bppstatus");
                tracer.Trace($"旧审批状态: {oldStatus}");
            }

            if (oldStatus == STATUS_IN_REVIEW)
            {
                tracer.Trace("原状态已是审批中，不重复处理");
                return;
            }

            try
            {
                // 查询业务字段与现有 BPP 信息
                Entity fsmData = service.Retrieve("mcs_fsm_data", target.Id,
                    new ColumnSet("mcs_fsm_status", "mcs_can_initiated", "mcs_can_project",
                                  "mcs_approve_type", "mcs_bppid", "mcs_bppstatuscode"));

                int fsmStatus = GetOptionSetValue(fsmData, "mcs_fsm_status");
                bool canInitiated = fsmData.GetAttributeValue<bool?>("mcs_can_initiated") ?? false;
                bool canProject = fsmData.GetAttributeValue<bool?>("mcs_can_project") ?? false;
                int approveType = GetOptionSetValue(fsmData, "mcs_approve_type");
                string existingWorkflowId = fsmData.GetAttributeValue<string>("mcs_bppid");
                string existingBppStatus = fsmData.GetAttributeValue<string>("mcs_bppstatuscode");

                tracer.Trace($"融资状态: {fsmStatus}, 审批类型: {approveType}, 可提交立项: {canInitiated}, 可提交方案: {canProject}");
                tracer.Trace($"现有 workflowId: {existingWorkflowId}, bppStatusCode: {existingBppStatus}");

                // 业务条件校验：立项审批（状态=2 且可提交立项）或方案审批（状态=3 且可提交方案）
                bool isInitiationApproval = fsmStatus == FSM_STATUS_INITIATION && approveType == 1;
                bool isProjectApproval = fsmStatus == FSM_STATUS_SOLUTION && approveType == 2;

                if (!isInitiationApproval && !isProjectApproval)
                {
                    tracer.Trace($"融资状态({fsmStatus})与审批类型({approveType})不匹配，跳过 BPP 提交");
                    return;
                }

                if (isInitiationApproval && !canInitiated)
                {
                    tracer.Trace("当前不允许提交立项审批(mcs_can_initiated=0)，跳过");
                    return;
                }

                if (isProjectApproval && !canProject)
                {
                    tracer.Trace("当前不允许提交融资方案审批(mcs_can_project=0)，跳过");
                    return;
                }

                // 防重复提交
                if (!string.IsNullOrEmpty(existingWorkflowId) && IsBppInProgress(existingBppStatus))
                {
                    tracer.Trace("流程仍在审批中，跳过重复提交");
                    return;
                }

                // 调用 mcs_bppstartapi
                OrganizationRequest request = new OrganizationRequest("mcs_bppstartapi");
                request["EntityId"] = target.Id.ToString();
                request["EntityName"] = "mcs_fsm_data";
                request["UserId"] = context.InitiatingUserId.ToString();

                tracer.Trace($"调用 mcs_bppstartapi: EntityId={target.Id}, UserId={context.InitiatingUserId}");

                OrganizationResponse response = service.Execute(request);
                string result = response["Result"]?.ToString() ?? "";
                tracer.Trace($"mcs_bppstartapi 返回: {result}");

                bool success = !string.IsNullOrEmpty(result) &&
                               (result.IndexOf("true", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                result.IndexOf("success", StringComparison.OrdinalIgnoreCase) >= 0);

                if (success)
                {
                    // 更新 BPP 审批状态码为 Submitted
                    Entity updateRecord = new Entity("mcs_fsm_data") { Id = target.Id };
                    updateRecord["mcs_bppstatuscode"] = "Submitted";
                    updateRecord["mcs_bpperrormsg"] = null;
                    service.Update(updateRecord);
                    tracer.Trace("BPP 提交成功，状态码更新为 Submitted");
                }
                else
                {
                    tracer.Trace($"BPP 发起失败: {result}");
                    throw new InvalidPluginExecutionException($"BPP 审批发起失败: {result}");
                }
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                tracer.Trace($"FsmDataBppIntegrationPlugin 异常: {ex.Message}");
                throw new InvalidPluginExecutionException($"BPP 集成处理失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 判断 BPP 状态是否为"进行中"
        /// </summary>
        private bool IsBppInProgress(string bppStatus)
        {
            if (string.IsNullOrWhiteSpace(bppStatus))
                return false;

            string status = bppStatus.Trim().ToLowerInvariant();
            return status == "submitted" ||
                   status == "inreview" ||
                   status == "pending" ||
                   status == "10" ||
                   status == "20";
        }

        private int GetOptionSetValue(Entity entity, string fieldName)
        {
            if (!entity.Contains(fieldName))
            {
                return 0;
            }

            OptionSetValue value = entity.GetAttributeValue<OptionSetValue>(fieldName);
            return value?.Value ?? 0;
        }
    }
}
