using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace SanyD365.Plugins.FinancingManagement.Detail
{
    /// <summary>
    /// 融资落实 - 订单号唯一性校验Plugin（禅道 #1511）
    /// 触发时机：mcs_fsm_detail_data Create/Update PreOperation
    /// 业务规则（PRD《LTC营销风控_外部融资额度管理》融资方案落实-新增-保存：唯一性校验（订单号））：
    /// 同一融资管理记录（mcs_fsm_data_id）下，订单编号（mcs_order_id）唯一；
    /// 不同融资管理记录之间同一订单号不拦截（口径经用户确认，与禅道用例 553 前置条件一致）。
    /// </summary>
    public class FsmDetailDataDuplicationCheckPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = factory.CreateOrganizationService(context.UserId);
            ITracingService tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracer.Trace("=== FsmDetailDataDuplicationCheckPlugin 开始执行 ===");
            tracer.Trace($"Message: {context.MessageName}, Stage: {context.Stage}");

            if ((context.MessageName != "Create" && context.MessageName != "Update") || context.Stage != 20)
            {
                tracer.Trace("非 Create/Update PreOperation 事件，跳过");
                return;
            }

            if (!context.InputParameters.Contains("Target") || !(context.InputParameters["Target"] is Entity target))
            {
                tracer.Trace("未找到 Target 参数");
                return;
            }

            if (target.LogicalName != "mcs_fsm_detail_data")
            {
                tracer.Trace($"非融资落实实体，跳过: {target.LogicalName}");
                return;
            }

            // Update 时目标字段可能只改其一，用 PreImage（mcs_fsm_data_id + mcs_order_id）补齐另一字段
            Entity preImage = context.PreEntityImages.Contains("PreImage") ? context.PreEntityImages["PreImage"] : null;

            EntityReference fsmDataRef = target.GetAttributeValue<EntityReference>("mcs_fsm_data_id")
                ?? preImage?.GetAttributeValue<EntityReference>("mcs_fsm_data_id");
            EntityReference orderRef = target.GetAttributeValue<EntityReference>("mcs_order_id")
                ?? preImage?.GetAttributeValue<EntityReference>("mcs_order_id");

            if (fsmDataRef == null || orderRef == null)
            {
                tracer.Trace($"融资管理记录或订单编号为空（fsmData={(fsmDataRef != null)}, order={(orderRef != null)}），跳过唯一性校验");
                return;
            }

            // 系统身份查询：避免操作人读不到他人创建的落实记录导致校验被绕过
            IOrganizationService systemService = factory.CreateOrganizationService(null);

            var query = new QueryExpression("mcs_fsm_detail_data")
            {
                ColumnSet = new ColumnSet("mcs_fsm_detail_no", "mcs_order_id"),
                TopCount = 1,
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_fsm_data_id", ConditionOperator.Equal, fsmDataRef.Id),
                        new ConditionExpression("mcs_order_id", ConditionOperator.Equal, orderRef.Id)
                    }
                }
            };

            if (context.MessageName == "Update")
            {
                query.Criteria.Conditions.Add(new ConditionExpression("mcs_fsm_detail_dataid", ConditionOperator.NotEqual, target.Id));
            }

            var duplicates = systemService.RetrieveMultiple(query).Entities;
            if (duplicates.Count > 0)
            {
                string existingNo = duplicates[0].GetAttributeValue<string>("mcs_fsm_detail_no");
                tracer.Trace($"发现重复落实记录: 编号={(string.IsNullOrEmpty(existingNo) ? duplicates[0].Id.ToString() : existingNo)}，拦截保存");
                // Create 时 Target 中的 Lookup 不带 Name，显式读取订单名称，提示更友好
                string orderName = orderRef.Name;
                if (string.IsNullOrEmpty(orderName))
                {
                    orderName = systemService.Retrieve("salesorder", orderRef.Id, new ColumnSet("name"))
                        .GetAttributeValue<string>("name") ?? orderRef.Id.ToString();
                }
                throw new InvalidPluginExecutionException(
                    $"同一融资管理记录下已存在相同订单编号（{orderName}）的融资落实记录，不允许重复保存。");
            }

            tracer.Trace("唯一性校验通过");
        }
    }
}
