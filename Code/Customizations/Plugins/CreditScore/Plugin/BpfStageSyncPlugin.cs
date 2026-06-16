using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace SanyD365.Plugins.CreditScore.Plugin
{
    /// <summary>
    /// BPF阶段同步Plugin
    /// 触发时机：信用评估记录Update后，mcs_status字段变更时
    /// 功能：同步更新BPF实例的activestageid，使BPF阶段与状态值保持一致
    /// </summary>
    public class BpfStageSyncPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = factory.CreateOrganizationService(context.UserId);
            ITracingService tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracer.Trace("===== BpfStageSyncPlugin 开始执行 =====");

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

            // 检查状态是否变更
            if (!target.Contains("mcs_status"))
            {
                tracer.Trace("状态未变更，跳过");
                return;
            }

            int status = target.GetAttributeValue<OptionSetValue>("mcs_status")?.Value ?? 0;
            tracer.Trace($"状态变更为: {status}");

            try
            {
                // 同步BPF实例
                BpfSyncHelper.SyncBpfStage(service, tracer, target.Id, status);
                tracer.Trace("===== BpfStageSyncPlugin 执行完成 =====");
            }
            catch (Exception ex)
            {
                tracer.Trace($"BPF阶段同步失败: {ex.Message}");
                // BPF同步失败不应阻塞主流程
            }
        }
    }
}
