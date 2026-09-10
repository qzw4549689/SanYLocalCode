using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace SanyD365.Plugins.FinancingManagement.Detail
{
    /// <summary>
    /// 融资落实 - 单据完成守卫Plugin（禅道 #1816，来源用例-577）
    /// 触发时机：mcs_fsm_detail_data Create/Update/Delete/SetState PreOperation
    /// 业务规则：融资管理主单 BPF「融资管理」（mcs_fmprocess，关联字段 bpf_mcs_fsm_dataid）点「完成」
    /// （statuscode=2）后，其下落实记录禁止新增/修改/删除/停用（界面信息不可以编辑）。
    /// 前端另有 mcs_fsm_detail_data.js 表单只读 + Uploader 附件只读，本插件为服务端兜底
    /// （覆盖 API/集成/ editable grid 等绕过前端的路径）。
    /// </summary>
    public class FsmDetailDataCompletedGuardPlugin : IPlugin
    {
        private const string BPF_ENTITY = "mcs_fmprocess";
        private const string BPF_FSM_LOOKUP = "bpf_mcs_fsm_dataid";
        private const int BPF_STATUS_FINISHED = 2;

        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = factory.CreateOrganizationService(context.UserId);
            ITracingService tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracer.Trace("=== FsmDetailDataCompletedGuardPlugin 开始执行 ===");
            tracer.Trace($"Message: {context.MessageName}, Stage: {context.Stage}");

            if (context.Stage != 20)
            {
                tracer.Trace("非 PreOperation 事件，跳过");
                return;
            }
            if (context.MessageName != "Create" && context.MessageName != "Update"
                && context.MessageName != "Delete" && context.MessageName != "SetState")
            {
                tracer.Trace("非 Create/Update/Delete/SetState 事件，跳过");
                return;
            }

            // 系统身份服务：避免操作人读不到 BPF 实例/主单导致守卫被绕过
            IOrganizationService systemService = factory.CreateOrganizationService(null);

            // 取主单（融资管理）Lookup：Create 从 Target 取，Update/Delete 从 PreImage 取，SetState 回读（不支持 PreImage）
            EntityReference fsmDataRef = null;
            if (context.MessageName == "Create")
            {
                if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity createTarget
                    && createTarget.LogicalName == "mcs_fsm_detail_data")
                {
                    fsmDataRef = createTarget.GetAttributeValue<EntityReference>("mcs_fsm_data_id");
                }
            }
            else if (context.MessageName == "SetState")
            {
                // SetState 消息不支持 PreEntityImage（Target 非 Entity），直接回读主单 Lookup
                if (context.InputParameters.Contains("EntityMoniker") && context.InputParameters["EntityMoniker"] is EntityReference moniker)
                {
                    fsmDataRef = systemService.Retrieve("mcs_fsm_detail_data", moniker.Id, new ColumnSet("mcs_fsm_data_id"))
                        .GetAttributeValue<EntityReference>("mcs_fsm_data_id");
                }
            }
            else
            {
                Entity preImage = context.PreEntityImages.Contains("PreImage") ? context.PreEntityImages["PreImage"] : null;
                fsmDataRef = preImage?.GetAttributeValue<EntityReference>("mcs_fsm_data_id");
            }

            if (fsmDataRef == null)
            {
                tracer.Trace("未取到融资管理记录 Lookup（mcs_fsm_data_id 为空），跳过守卫");
                return;
            }

            // 查主单 BPF 实例是否已完成（statuscode=2）
            var query = new QueryExpression(BPF_ENTITY)
            {
                ColumnSet = new ColumnSet(false),
                TopCount = 1,
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression(BPF_FSM_LOOKUP, ConditionOperator.Equal, fsmDataRef.Id),
                        new ConditionExpression("statuscode", ConditionOperator.Equal, BPF_STATUS_FINISHED)
                    }
                }
            };

            bool finished = systemService.RetrieveMultiple(query).Entities.Count > 0;
            tracer.Trace($"主单 {fsmDataRef.Id} BPF 完成状态: {finished}");
            if (!finished) return;

            string action = context.MessageName == "Create" ? "新增"
                : context.MessageName == "Delete" ? "删除"
                : "修改";
            tracer.Trace($"主单已完成，拦截{action}操作");
            throw new InvalidPluginExecutionException(
                $"融资管理单据已完成，融资落实记录已锁定，禁止{action}。");
        }
    }
}
