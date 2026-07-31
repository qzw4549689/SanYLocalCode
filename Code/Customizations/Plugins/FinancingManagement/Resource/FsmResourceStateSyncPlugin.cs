using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace SanyD365.Plugins.FinancingManagement.Resource
{
    /// <summary>
    /// 融资资源管理 - 状态同步Plugin（禅道 #1433）
    /// 触发时机：mcs_fsm_resource Update PostOperation，statecode 字段变更（列表【激活/停用】系统按钮）
    /// 业务规则：
    /// 1. 记录被激活（statecode 变为 0 Active）时，将「是否启用过」mcs_fsm_rl_status 幂等回写为 true
    /// 2. 单向标记：停用不清除；已启用过的记录不允许删除（由 FsmResourceDeleteGuardPlugin 拦截）
    /// </summary>
    public class FsmResourceStateSyncPlugin : IPlugin
    {
        // statecode 值：0 = Active
        private const int STATE_ACTIVE = 0;

        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = factory.CreateOrganizationService(context.UserId);
            ITracingService tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracer.Trace("=== FsmResourceStateSyncPlugin 开始执行 ===");
            tracer.Trace($"Message: {context.MessageName}, Stage: {context.Stage}, Depth: {context.Depth}, UserId: {context.UserId}, InitiatingUserId: {context.InitiatingUserId}");

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
            if (target.LogicalName != "mcs_fsm_resource")
            {
                tracer.Trace($"非融资资源实体，跳过: {target.LogicalName}");
                return;
            }

            // 只处理系统状态字段变更（激活/停用按钮触发 SetState，平台转为 Update statecode）
            if (!target.Contains("statecode"))
            {
                tracer.Trace("statecode 未变更，跳过");
                return;
            }

            OptionSetValue newState = target.GetAttributeValue<OptionSetValue>("statecode");
            tracer.Trace($"新 statecode: {newState?.Value}");

            // 只处理激活方向；停用不做任何处理（单向标记不清除）
            if (newState == null || newState.Value != STATE_ACTIVE)
            {
                tracer.Trace("非激活方向，跳过");
                return;
            }

            // 幂等：实时读取当前值，已是「启用过」则不再回写（避免依赖 PostImage，注册更简单）
            try
            {
                Entity current = service.Retrieve("mcs_fsm_resource", target.Id, new ColumnSet("mcs_fsm_rl_status"));
                bool alreadyEnabled = current.GetAttributeValue<bool?>("mcs_fsm_rl_status") ?? false;
                if (alreadyEnabled)
                {
                    tracer.Trace("是否启用过已是 是，无需回写");
                    return;
                }

                Entity updateRecord = new Entity("mcs_fsm_resource") { Id = target.Id };
                updateRecord["mcs_fsm_rl_status"] = true;
                service.Update(updateRecord);
                tracer.Trace($"已将记录 {target.Id} 的「是否启用过」回写为 是");
            }
            catch (Exception ex)
            {
                tracer.Trace($"FsmResourceStateSyncPlugin 异常: {ex.Message}");
                throw new InvalidPluginExecutionException($"融资资源状态同步失败: {ex.Message}");
            }
        }
    }
}
