using Microsoft.Xrm.Sdk;
using System;

namespace SanyD365.Plugins.FinancingManagement.Notify
{
    /// <summary>
    /// 融资管理 - 进入融资落实阶段小铃铛通知Plugin（禅道 #1654，来源用例-547）
    /// 触发时机：mcs_fsm_data Update PostOperation，mcs_fsm_status 字段变更
    /// 业务规则：融资状态从非 4 变为 4（融资落实，即融资方案审批通过）时，
    /// 通过 SendAppNotification 给该记录的融资经理（mcs_fsm_manager，为空兜底创建人）
    /// 发送小铃铛（In-App Notification）消息提醒。
    /// 注意：本环境 SendAppNotification 不支持 Data 操作按钮、正文链接不可点击（2026-08-06 实测），
    /// 故正文直接放融资管理编号，用户凭编号自行查找记录。
    /// 通知发送失败仅记 Trace，绝不影响 BPP 回调/状态流转主流程。
    /// </summary>
    public class FsmDataStage4NotifyPlugin : IPlugin
    {
        // mcs_fsm_data.mcs_fsm_status 选项集值
        private const int FSM_STATUS_IMPLEMENTATION = 4; // 融资落实

        // SendAppNotification 参数枚举
        private const int ICON_SUCCESS = 100000001;  // IconType=Success（✅ 绿色）
        private const int TOAST_TIMED = 200000000;   // ToastType=Timed（弹出后自动消失）
        private const int EXPIRY_SECONDS = 604800;   // 通知保留 7 天

        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = factory.CreateOrganizationService(context.UserId);
            ITracingService tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracer.Trace("=== FsmDataStage4NotifyPlugin 开始执行 ===");
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

            // 只处理融资状态字段变更
            if (!target.Contains("mcs_fsm_status"))
            {
                tracer.Trace("mcs_fsm_status 未变更，跳过");
                return;
            }

            // 新值必须为 4（融资落实）
            int newStatus = GetOptionSetValue(target, "mcs_fsm_status");
            if (newStatus != FSM_STATUS_IMPLEMENTATION)
            {
                tracer.Trace($"融资状态新值={newStatus}，非融资落实(4)，跳过");
                return;
            }

            // PreImage 对比：旧值已是 4 则跳过（防重复触发，如状态4记录后续普通更新）
            if (context.PreEntityImages.Contains("PreImage"))
            {
                int oldStatus = GetOptionSetValue(context.PreEntityImages["PreImage"], "mcs_fsm_status");
                tracer.Trace($"融资状态旧值={oldStatus}，新值={newStatus}");
                if (oldStatus == FSM_STATUS_IMPLEMENTATION)
                {
                    tracer.Trace("旧值已是融资落实(4)，非本次进入，跳过");
                    return;
                }
            }
            else
            {
                tracer.Trace("⚠️ 未注册 PreImage，无法对比旧值，继续按新值=4 处理");
            }

            // 取通知所需信息（优先从 PreImage 取，避免二次查询）
            Entity preImage = context.PreEntityImages.Contains("PreImage") ? context.PreEntityImages["PreImage"] : null;
            string fsmNo = preImage != null ? preImage.GetAttributeValue<string>("mcs_fsm_no") : null;
            EntityReference managerRef = preImage != null ? preImage.GetAttributeValue<EntityReference>("mcs_fsm_manager") : null;
            EntityReference creatorRef = preImage != null ? preImage.GetAttributeValue<EntityReference>("createdby") : null;

            // 接收人：融资经理（mcs_fsm_manager），为空兜底创建人（禅道 #1654 口径）
            EntityReference recipient = managerRef ?? creatorRef;
            if (recipient == null)
            {
                tracer.Trace("⚠️ 融资经理与创建人均为空，无法确定接收人，跳过通知");
                return;
            }
            tracer.Trace($"通知接收人: {(managerRef != null ? "融资经理" : "创建人(兜底)")} = {recipient.Name} ({recipient.Id})");

            // 发送小铃铛通知（独立 try-catch，失败不影响主流程）
            try
            {
                string title = "融资落实提醒";
                string body = $"您负责的融资记录 {(string.IsNullOrWhiteSpace(fsmNo) ? "" : fsmNo + " ")}已进入【融资落实】阶段，请及时跟进处理。";

                var request = new OrganizationRequest("SendAppNotification")
                {
                    ["Recipient"] = new EntityReference("systemuser", recipient.Id),
                    ["Title"] = title,
                    ["Body"] = body,
                    ["IconType"] = new OptionSetValue(ICON_SUCCESS),
                    ["ToastType"] = new OptionSetValue(TOAST_TIMED),
                    ["Expiry"] = EXPIRY_SECONDS
                };

                var response = service.Execute(request);
                tracer.Trace($"✅ 小铃铛通知已发送: NotificationId={(response.Results.Contains("NotificationId") ? response.Results["NotificationId"] : "未知")}");
            }
            catch (Exception ex)
            {
                // 通知失败只记日志，不抛异常（不阻断 BPP 回调/状态流转）
                tracer.Trace($"⚠️ SendAppNotification 发送失败（已忽略，不影响主流程）: {ex.Message}");
            }

            tracer.Trace("=== FsmDataStage4NotifyPlugin 执行结束 ===");
        }

        /// <summary>
        /// 安全读取 OptionSetValue 字段，缺失返回 -1
        /// </summary>
        private static int GetOptionSetValue(Entity entity, string fieldName)
        {
            if (entity != null && entity.Contains(fieldName))
            {
                var value = entity.GetAttributeValue<OptionSetValue>(fieldName);
                if (value != null)
                {
                    return value.Value;
                }
            }
            return -1;
        }
    }
}
