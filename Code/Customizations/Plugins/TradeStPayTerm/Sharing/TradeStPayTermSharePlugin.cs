using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace SanyD365.Plugins.TradeStPayTerm
{
    /// <summary>
    /// 成交条件样板库 - 提交审批共享 Plugin（禅道 #1151）
    /// 触发时机：mcs_trade_stpayterm Update PostOperation（FilteringAttributes = mcs_status）
    /// 业务规则：生效状态从非"待审批"变为 1（待审批）时，
    /// 按记录的事业部（mcs_businessunit）查询 mcs_bu.mcs_buteamid（BU的团队），
    /// 将记录共享（Read + Write）给该团队，使对应事业部审批人（LTC Regional Overseas Risk Director）
    /// 能够看到并审批本事业部的数据。
    /// 说明：
    /// 1. owner 保持创建人不变，制定人可见性不受影响；
    /// 2. GrantAccessRequest 幂等，重复提交（拒绝后重新申请）不会报错；
    /// 3. 共享不回收：生效/拒绝后审批人仍可持续查看本事业部数据。
    /// </summary>
    public class TradeStPayTermSharePlugin : IPlugin
    {
        // 生效状态选项集值：1 = 待审批
        private const int STATUS_PENDING = 1;

        public void Execute(IServiceProvider serviceProvider)
        {
            // 2026-08-14 Bug #1834：取消审批功能，提交审批时按事业部共享给审批人的逻辑已停用
            // 保留完整代码注释，便于后续恢复审批流程时启用
            //
            // IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            // IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            // IOrganizationService service = factory.CreateOrganizationService(context.UserId);
            // ITracingService tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            //
            // // 系统身份服务：查询事业部团队/执行共享（普通用户可能无读 mcs_bu / 共享记录的权限）
            // // 注意：本行必须紧跟上方 4 行初始化代码之后，sync-plugin-to-remote.py 依赖该顺序做远程转换
            // IOrganizationService systemService = factory.CreateOrganizationService(null);
            //
            // tracer.Trace("TradeStPayTermSharePlugin 开始执行");
            //
            // if (context.MessageName != "Update" || context.Stage != 40)
            // {
            //     tracer.Trace("非 Update PostOperation 事件，跳过");
            //     return;
            // }
            //
            // if (!context.InputParameters.Contains("Target") || !(context.InputParameters["Target"] is Entity))
            // {
            //     tracer.Trace("未找到 Target 实体");
            //     return;
            // }
            //
            // Entity target = (Entity)context.InputParameters["Target"];
            //
            // if (target.LogicalName != "mcs_trade_stpayterm")
            // {
            //     tracer.Trace($"实体不匹配: {target.LogicalName}");
            //     return;
            // }
            //
            // // 只有状态字段变更时才需要处理
            // if (!target.Contains("mcs_status"))
            // {
            //     tracer.Trace("状态字段未变更，跳过");
            //     return;
            // }
            //
            // int newStatus = target.GetAttributeValue<OptionSetValue>("mcs_status")?.Value ?? -1;
            // tracer.Trace($"新状态: {newStatus}");
            //
            // if (newStatus != STATUS_PENDING)
            // {
            //     tracer.Trace("新状态不是待审批，跳过");
            //     return;
            // }
            //
            // // 获取旧状态，仅处理"非待审批 -> 待审批"的提交动作
            // int oldStatus = -1;
            // EntityReference buRef = null;
            // if (context.PreEntityImages.Contains("PreImage"))
            // {
            //     Entity preImage = context.PreEntityImages["PreImage"];
            //     oldStatus = preImage.GetAttributeValue<OptionSetValue>("mcs_status")?.Value ?? -1;
            //     buRef = preImage.GetAttributeValue<EntityReference>("mcs_businessunit");
            //     tracer.Trace($"旧状态: {oldStatus}");
            // }
            //
            // if (oldStatus == STATUS_PENDING)
            // {
            //     tracer.Trace("旧状态已是待审批，跳过");
            //     return;
            // }
            //
            // try
            // {
            //     // 事业部优先取 Target（本批次可能同时变更），其次 PreImage，最后实时查询
            //     if (target.Contains("mcs_businessunit"))
            //     {
            //         buRef = target.GetAttributeValue<EntityReference>("mcs_businessunit");
            //     }
            //     if (buRef == null)
            //     {
            //         Entity record = systemService.Retrieve("mcs_trade_stpayterm", target.Id, new ColumnSet("mcs_businessunit"));
            //         buRef = record.GetAttributeValue<EntityReference>("mcs_businessunit");
            //     }
            //
            //     if (buRef == null)
            //     {
            //         throw new InvalidPluginExecutionException("提交失败：请先选择【事业部】再提交审批。");
            //     }
            //
            //     tracer.Trace($"事业部: {buRef.Name}({buRef.Id})");
            //
            //     // 查询事业部的 BU 团队（主数据已有 mcs_bu.mcs_buteamid，零新增配置）
            //     Entity bu = systemService.Retrieve("mcs_bu", buRef.Id, new ColumnSet("mcs_buteamid"));
            //     EntityReference teamRef = bu.GetAttributeValue<EntityReference>("mcs_buteamid");
            //
            //     if (teamRef == null)
            //     {
            //         throw new InvalidPluginExecutionException(
            //             $"提交失败：事业部【{buRef.Name}】未维护【BU的团队】，审批人将无法看到该数据，请联系管理员配置后再提交。");
            //     }
            //
            //     tracer.Trace($"BU团队: {teamRef.Name}({teamRef.Id})");
            //
            //     // 共享记录给 BU 团队（Read + Write：审批人需要查看并更新状态；GrantAccess 幂等）
            //     var grantRequest = new GrantAccessRequest
            //     {
            //         Target = new EntityReference("mcs_trade_stpayterm", target.Id),
            //         PrincipalAccess = new PrincipalAccess
            //         {
            //             Principal = teamRef,
            //             AccessMask = AccessRights.ReadAccess | AccessRights.WriteAccess
            //         }
            //     };
            //     systemService.Execute(grantRequest);
            //
            //     tracer.Trace($"已将记录 {target.Id} 共享（Read+Write）给团队 {teamRef.Name}({teamRef.Id})");
            // }
            // catch (InvalidPluginExecutionException)
            // {
            //     throw;
            // }
            // catch (Exception ex)
            // {
            //     tracer.Trace($"共享失败: {ex}");
            //     throw new InvalidPluginExecutionException($"提交失败：按事业部共享数据时出错（{ex.Message}）");
            // }
        }
    }
}
