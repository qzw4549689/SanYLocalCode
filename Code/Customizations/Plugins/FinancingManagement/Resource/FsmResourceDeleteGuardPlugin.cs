using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace SanyD365.Plugins.FinancingManagement.Resource
{
    /// <summary>
    /// 融资资源管理 - 删除守卫Plugin（禅道 #1433 关联 PRD 删除规则）
    /// 触发时机：mcs_fsm_resource Delete PreOperation
    /// 业务规则（PRD）：仅「是否启用过=否」且「创建人=当前登录人」的记录允许删除；
    /// System Administrator 放行（内置角色，便于运维清理测试数据）；
    /// 「融资资源管理员」业务角色不写死，由各环境安全角色的删除权限配置控制。
    /// </summary>
    public class FsmResourceDeleteGuardPlugin : IPlugin
    {
        // 内置管理员角色名（平台固定，非业务角色）
        private const string SYSTEM_ADMIN_ROLE = "System Administrator";

        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = factory.CreateOrganizationService(context.UserId);
            ITracingService tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracer.Trace("=== FsmResourceDeleteGuardPlugin 开始执行 ===");
            tracer.Trace($"Message: {context.MessageName}, Stage: {context.Stage}, UserId: {context.UserId}, InitiatingUserId: {context.InitiatingUserId}");

            if (context.MessageName != "Delete" || context.Stage != 20)
            {
                tracer.Trace("非 Delete PreOperation 事件，跳过");
                return;
            }

            if (!context.InputParameters.Contains("Target") || !(context.InputParameters["Target"] is EntityReference targetRef))
            {
                tracer.Trace("未找到 Target 参数");
                return;
            }

            // 严格校验实体名
            if (targetRef.LogicalName != "mcs_fsm_resource")
            {
                tracer.Trace($"非融资资源实体，跳过: {targetRef.LogicalName}");
                return;
            }

            // 优先取 PreImage（注册时配置 mcs_fsm_rl_status + createdby），缺失时回退实时查询
            Entity record;
            if (context.PreEntityImages.Contains("PreImage"))
            {
                record = context.PreEntityImages["PreImage"];
            }
            else
            {
                tracer.Trace("PreImage 缺失，回退实时查询");
                record = service.Retrieve("mcs_fsm_resource", targetRef.Id,
                    new ColumnSet("mcs_fsm_rl_status", "createdby"));
            }

            // 规则1：是否启用过=是 的记录不允许删除
            bool everEnabled = record.GetAttributeValue<bool?>("mcs_fsm_rl_status") ?? false;
            tracer.Trace($"是否启用过: {everEnabled}");
            if (everEnabled)
            {
                tracer.Trace("记录已启用过，拦截删除");
                throw new InvalidPluginExecutionException("该融资资源已启用过，不允许删除。");
            }

            // 规则2：仅创建人可删除；System Administrator 放行
            // 注意：本环境 Delete 管道的 context.UserId 恒为 SYSTEM（平台行为），
            // 真实操作人必须取 InitiatingUserId（实测 Trace 证实，见禅道 #1433 记录）
            Guid callerId = context.InitiatingUserId;
            EntityReference createdBy = record.GetAttributeValue<EntityReference>("createdby");
            if (createdBy != null && createdBy.Id != callerId)
            {
                IOrganizationService systemService = factory.CreateOrganizationService(null);
                if (!UserHasAnyRole(systemService, callerId, SYSTEM_ADMIN_ROLE))
                {
                    tracer.Trace($"操作人 {callerId} 非创建人 {createdBy.Id} 且非管理员，拦截删除");
                    throw new InvalidPluginExecutionException("仅创建人可删除该融资资源记录。");
                }
                tracer.Trace($"操作人 {callerId} 非创建人，但为 System Administrator，放行");
            }

            tracer.Trace($"删除校验通过: {targetRef.Id}");
        }

        /// <summary>
        /// 判断用户是否拥有任一指定角色（系统身份查询，避免普通用户无 role 读权限误判；
        /// 仅查 systemuserroles 直接分配，不含团队继承角色）
        /// </summary>
        private bool UserHasAnyRole(IOrganizationService systemService, Guid userId, params string[] roleNames)
        {
            var query = new QueryExpression("role")
            {
                ColumnSet = new ColumnSet("roleid"),
                TopCount = 1,
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("name", ConditionOperator.In, roleNames)
                    }
                },
                LinkEntities =
                {
                    new LinkEntity("role", "systemuserroles", "roleid", "roleid", JoinOperator.Inner)
                    {
                        LinkCriteria = new FilterExpression
                        {
                            Conditions =
                            {
                                new ConditionExpression("systemuserid", ConditionOperator.Equal, userId)
                            }
                        }
                    }
                }
            };

            return systemService.RetrieveMultiple(query).Entities.Count > 0;
        }
    }
}
