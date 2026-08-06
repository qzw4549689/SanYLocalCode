using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace SanyD365.Plugins.FinancingManagement.Resource
{
    /// <summary>
    /// 融资资源管理 - 机构代码重复校验Plugin（禅道 #1512）
    /// 触发时机：mcs_fsm_resource Create/Update PreOperation
    /// 业务规则（PRD《LTC营销风控_外部融资额度管理》新增融资资源-保存：新增重复性校验（机构代码））：
    /// 机构代码（mcs_fsm_institution_code）全局唯一，含停用记录（口径经用户确认：
    /// 已停用机构想再用直接「启用」即可，不应再建同代码新记录）。
    /// </summary>
    public class FsmResourceDuplicationCheckPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = factory.CreateOrganizationService(context.UserId);
            ITracingService tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracer.Trace("=== FsmResourceDuplicationCheckPlugin 开始执行 ===");
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

            if (target.LogicalName != "mcs_fsm_resource")
            {
                tracer.Trace($"非融资资源实体，跳过: {target.LogicalName}");
                return;
            }

            // Update Step 按 mcs_fsm_institution_code 过滤触发，Target 必含新值；Create 同理
            string institutionCode = target.GetAttributeValue<string>("mcs_fsm_institution_code");
            if (string.IsNullOrWhiteSpace(institutionCode))
            {
                tracer.Trace("机构代码为空，跳过重复校验");
                return;
            }
            institutionCode = institutionCode.Trim();

            // 系统身份查询：避免操作人读不到他人创建的融资资源导致校验被绕过
            IOrganizationService systemService = factory.CreateOrganizationService(null);

            var query = new QueryExpression("mcs_fsm_resource")
            {
                ColumnSet = new ColumnSet("mcs_fsm_resource_no"),
                TopCount = 1,
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_fsm_institution_code", ConditionOperator.Equal, institutionCode)
                    }
                }
            };

            if (context.MessageName == "Update")
            {
                query.Criteria.Conditions.Add(new ConditionExpression("mcs_fsm_resourceid", ConditionOperator.NotEqual, target.Id));
            }

            var duplicates = systemService.RetrieveMultiple(query).Entities;
            if (duplicates.Count > 0)
            {
                string existingNo = duplicates[0].GetAttributeValue<string>("mcs_fsm_resource_no");
                tracer.Trace($"发现重复机构代码: 已有记录编号={(string.IsNullOrEmpty(existingNo) ? duplicates[0].Id.ToString() : existingNo)}，拦截保存");
                throw new InvalidPluginExecutionException(
                    $"已存在相同机构代码（{institutionCode}）的融资资源（{existingNo}），不允许重复保存。如需重新使用该机构，请对原记录执行「启用」。");
            }

            tracer.Trace("机构代码重复校验通过");
        }
    }
}
