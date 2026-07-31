using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace SanyD365.Plugins.FinancingManagement.Bpp
{
    /// <summary>
    /// 融资管理 - BPP回调Plugin
    /// 触发时机：mcs_fsm_data Update PostOperation, mcs_bppstatuscode 字段变更
    /// 业务规则：根据 BPP 回调状态与审批类型（mcs_approve_type）更新融资状态
    /// 1. 立项审批通过（类型=1，融资状态=2）：融资状态→3（融资解决方案），mcs_can_initiated=0
    /// 2. 融资方案审批通过（类型=2，融资状态=3）：融资状态→4（融资落实），mcs_is_valid=true，mcs_can_project=0
    /// 3. 驳回：融资状态不变，mcs_can_initiated=1（类型=1）或 mcs_can_project=1（类型=2）
    /// 4. 撤回/废弃：审批状态回到申请，清空 mcs_bppid、mcs_nextapprover
    /// </summary>
    public class FsmDataBppCallbackPlugin : IPlugin
    {
        // mcs_fsm_data.mcs_bppstatus 选项集值
        private const int STATUS_APPLY = 1;      // 申请
        private const int STATUS_APPROVED = 3;   // 通过
        private const int STATUS_REJECTED = 4;   // 驳回

        // mcs_fsm_data.mcs_fsm_status 选项集值
        private const int FSM_STATUS_INITIATION = 2;   // 融资立项
        private const int FSM_STATUS_SOLUTION = 3;     // 融资解决方案
        private const int FSM_STATUS_IMPLEMENTATION = 4; // 融资落实

        // mcs_fsm_data.mcs_approve_type 选项集值
        private const int APPROVE_TYPE_INITIATION = 1; // 立项审批
        private const int APPROVE_TYPE_PROJECT = 2;    // 融资方案审批

        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = factory.CreateOrganizationService(context.UserId);
            ITracingService tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracer.Trace("=== FsmDataBppCallbackPlugin 开始执行 ===");
            tracer.Trace($"Message: {context.MessageName}, Stage: {context.Stage}, Depth: {context.Depth}");

            // 防递归
            if (context.Depth > 3)
            {
                tracer.Trace("递归深度超过3，跳过处理");
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

            // 只处理 BPP 审批状态码字段变更
            if (!target.Contains("mcs_bppstatuscode"))
            {
                tracer.Trace("mcs_bppstatuscode 未变更，跳过");
                return;
            }

            string bppStatus = GetBppStatusValue(target, tracer);
            tracer.Trace($"BPP 回调状态: {bppStatus}");

            if (string.IsNullOrWhiteSpace(bppStatus))
            {
                tracer.Trace("BPP 状态为空，跳过处理");
                return;
            }

            // 中间状态不处理业务状态流转
            if (IsIntermediateStatus(bppStatus))
            {
                tracer.Trace($"BPP 中间状态: {bppStatus}，跳过业务状态流转");
                return;
            }

            try
            {
                Entity updateRecord = new Entity("mcs_fsm_data") { Id = target.Id };

                switch (bppStatus.ToLowerInvariant())
                {
                    case "approved":
                    case "30":
                        tracer.Trace("BPP 审批通过，按审批类型处理融资状态流转");
                        ProcessApprovalResult(service, tracer, target.Id, updateRecord);
                        break;

                    case "rejected":
                    case "11":
                        tracer.Trace("BPP 审批驳回，恢复可提交标记");
                        ProcessRejectionResult(service, tracer, target.Id, updateRecord);
                        break;

                    case "withdrawn":
                    case "withdraw":
                        tracer.Trace("BPP 审批撤回，状态回到申请");
                        updateRecord["mcs_bppstatus"] = new OptionSetValue(STATUS_APPLY);
                        updateRecord["mcs_bppid"] = null;
                        updateRecord["mcs_nextapprover"] = null;
                        service.Update(updateRecord);
                        break;

                    case "abandoned":
                    case "abandon":
                        tracer.Trace("BPP 审批废弃，状态回到申请");
                        updateRecord["mcs_bppstatus"] = new OptionSetValue(STATUS_APPLY);
                        updateRecord["mcs_bppid"] = null;
                        updateRecord["mcs_nextapprover"] = null;
                        service.Update(updateRecord);
                        break;

                    default:
                        tracer.Trace($"未知的 BPP 回调状态: {bppStatus}，暂不处理");
                        break;
                }
            }
            catch (Exception ex)
            {
                tracer.Trace($"FsmDataBppCallbackPlugin 异常: {ex.Message}");
                tracer.Trace($"异常堆栈: {ex.StackTrace}");
                throw new InvalidPluginExecutionException($"BPP 回调处理失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 审批通过处理：按审批类型推进融资状态
        /// </summary>
        private void ProcessApprovalResult(IOrganizationService service, ITracingService tracer, Guid fsmDataId, Entity updateRecord)
        {
            Entity fsmData = service.Retrieve("mcs_fsm_data", fsmDataId,
                new ColumnSet("mcs_fsm_status", "mcs_approve_type"));

            int fsmStatus = GetOptionSetValue(fsmData, "mcs_fsm_status");
            int approveType = GetOptionSetValue(fsmData, "mcs_approve_type");

            tracer.Trace($"当前融资状态: {fsmStatus}, 审批类型: {approveType}");

            updateRecord["mcs_bppstatus"] = new OptionSetValue(STATUS_APPROVED);
            updateRecord["mcs_approvedate"] = DateTime.UtcNow;

            if (approveType == APPROVE_TYPE_INITIATION && fsmStatus == FSM_STATUS_INITIATION)
            {
                // 立项审批通过：融资状态 2 → 3（融资解决方案）
                updateRecord["mcs_fsm_status"] = new OptionSetValue(FSM_STATUS_SOLUTION);
                updateRecord["mcs_can_initiated"] = false;
                tracer.Trace("立项审批通过：融资状态 2→3（融资解决方案），mcs_can_initiated=0");
            }
            else if (approveType == APPROVE_TYPE_PROJECT && fsmStatus == FSM_STATUS_SOLUTION)
            {
                // 融资方案审批通过：融资状态 3 → 4（融资落实），记录生效
                updateRecord["mcs_fsm_status"] = new OptionSetValue(FSM_STATUS_IMPLEMENTATION);
                updateRecord["mcs_is_valid"] = true;
                updateRecord["mcs_can_project"] = false;
                tracer.Trace("融资方案审批通过：融资状态 3→4（融资落实），mcs_is_valid=true，mcs_can_project=0");
            }
            else
            {
                tracer.Trace($"审批类型({approveType})与融资状态({fsmStatus})组合未匹配，仅更新审批状态，不流转融资状态");
            }

            service.Update(updateRecord);
        }

        /// <summary>
        /// 审批驳回处理：融资状态不变，恢复对应可提交标记
        /// </summary>
        private void ProcessRejectionResult(IOrganizationService service, ITracingService tracer, Guid fsmDataId, Entity updateRecord)
        {
            Entity fsmData = service.Retrieve("mcs_fsm_data", fsmDataId,
                new ColumnSet("mcs_fsm_status", "mcs_approve_type"));

            int fsmStatus = GetOptionSetValue(fsmData, "mcs_fsm_status");
            int approveType = GetOptionSetValue(fsmData, "mcs_approve_type");

            tracer.Trace($"当前融资状态: {fsmStatus}, 审批类型: {approveType}");

            updateRecord["mcs_bppstatus"] = new OptionSetValue(STATUS_REJECTED);

            if (approveType == APPROVE_TYPE_INITIATION)
            {
                updateRecord["mcs_can_initiated"] = true;
                tracer.Trace("立项审批驳回：mcs_can_initiated=1，融资状态不变");
            }
            else if (approveType == APPROVE_TYPE_PROJECT)
            {
                updateRecord["mcs_can_project"] = true;
                tracer.Trace("融资方案审批驳回：mcs_can_project=1，融资状态不变");
            }
            else
            {
                tracer.Trace($"审批类型({approveType})未识别，仅更新审批状态");
            }

            service.Update(updateRecord);
        }

        /// <summary>
        /// 获取 BPP 状态值，兼容字符串和数字类型
        /// </summary>
        private string GetBppStatusValue(Entity target, ITracingService tracer)
        {
            if (target["mcs_bppstatuscode"] is string strValue)
            {
                return strValue.Trim();
            }

            if (target["mcs_bppstatuscode"] is OptionSetValue optionValue)
            {
                return optionValue.Value.ToString();
            }

            tracer.Trace($"mcs_bppstatuscode 类型未识别: {target["mcs_bppstatuscode"]?.GetType().FullName}");
            return target["mcs_bppstatuscode"]?.ToString() ?? "";
        }

        /// <summary>
        /// 判断是否为中间状态
        /// </summary>
        private bool IsIntermediateStatus(string bppStatus)
        {
            string status = bppStatus.ToLowerInvariant();
            return status == "submitted" ||
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
