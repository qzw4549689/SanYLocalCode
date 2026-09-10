using Microsoft.Xrm.Sdk;
using SanyD365.Plugins.CofaceIntegration;
using System;

namespace SanyD365.Plugins.CofaceIntegration.Plugin
{
    /// <summary>
    /// 禅道 #2092：绑定 Coface ID 后系统身份回写客户主数据 mcs_cofaceid
    /// 触发：Update of mcs_credit_record，PostOperation(40)，Sync，Filter=mcs_cofaceid
    /// 背景：原前端绑定弹窗直接 PATCH 客户主数据，受操作者 mcs_customermasterdata 写权限限制 403；
    ///       改为服务端系统身份回写，操作者只需评估记录写权限即可完成绑定。
    /// 规则：现值为空才写，有值不覆盖并记 Trace（防换绑覆盖既有绑定）。
    /// </summary>
    public class CofaceBindWritebackPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = factory.CreateOrganizationService(context.UserId);
            ITracingService tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            // 系统身份服务（回写客户主数据用，#2092：操作者无主数据写权限）
            IOrganizationService systemService = factory.CreateOrganizationService(null);

            // 严格校验：只处理 Update PostOperation
            if (context.MessageName != "Update" || context.Stage != 40)
            {
                return;
            }

            // 严格校验：只处理 mcs_credit_record 实体
            if (!context.InputParameters.Contains("Target") || !(context.InputParameters["Target"] is Entity))
            {
                return;
            }

            Entity target = (Entity)context.InputParameters["Target"];
            if (target.LogicalName != "mcs_credit_record")
            {
                return;
            }

            // 只管绑定写入：Filter 已保证字段变更，此处取新值；清空（null/空串）不处理
            if (!target.Contains("mcs_cofaceid"))
            {
                return;
            }
            string cofaceId = target["mcs_cofaceid"] as string;
            if (string.IsNullOrWhiteSpace(cofaceId))
            {
                tracer.Trace("CofaceBindWritebackPlugin：mcs_cofaceid 新值为空（清空场景），跳过");
                return;
            }

            try
            {
                // 评估记录 → 客户（account）
                Entity record = systemService.Retrieve("mcs_credit_record", target.Id,
                    new Microsoft.Xrm.Sdk.Query.ColumnSet("mcs_scoreid", "mcs_accountid"));
                string recordCode = record.GetAttributeValue<string>("mcs_scoreid") ?? target.Id.ToString();
                EntityReference accountRef = record.GetAttributeValue<EntityReference>("mcs_accountid");
                if (accountRef == null)
                {
                    tracer.Trace($"CofaceBindWritebackPlugin：评估记录 {recordCode} 无关联客户，跳过主数据回写");
                    return;
                }

                // 客户 → 客户主数据
                Entity account = systemService.Retrieve("account", accountRef.Id,
                    new Microsoft.Xrm.Sdk.Query.ColumnSet("mcs_customermasterdata"));
                EntityReference masterRef = account.GetAttributeValue<EntityReference>("mcs_customermasterdata");
                if (masterRef == null)
                {
                    tracer.Trace($"CofaceBindWritebackPlugin：评估记录 {recordCode} 的客户未关联客户主数据，跳过回写");
                    return;
                }

                // 空才写，有值不覆盖
                Entity master = systemService.Retrieve("mcs_customermasterdata", masterRef.Id,
                    new Microsoft.Xrm.Sdk.Query.ColumnSet("mcs_cofaceid"));
                string existingCofaceId = master.GetAttributeValue<string>("mcs_cofaceid");
                if (!string.IsNullOrWhiteSpace(existingCofaceId))
                {
                    tracer.Trace($"CofaceBindWritebackPlugin：客户主数据已有科法斯客户代码 {existingCofaceId}，不覆盖（评估记录 {recordCode} 本次绑定值 {cofaceId}）");
                    return;
                }

                Entity update = new Entity("mcs_customermasterdata", masterRef.Id);
                update["mcs_cofaceid"] = cofaceId;
                systemService.Update(update);
                tracer.Trace($"CofaceBindWritebackPlugin：已回写客户主数据科法斯客户代码={cofaceId}（评估记录 {recordCode}）");
            }
            catch (Exception ex)
            {
                // 回写失败不阻断绑定主流程（评估记录已写入），记 Trace 便于排查
                tracer.Trace($"CofaceBindWritebackPlugin：回写客户主数据失败：{ex}");
            }
        }
    }
}
