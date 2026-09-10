using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace SanyD365.Plugins.FinancingManagement.Notify
{
    /// <summary>
    /// 融资管理 - 阶段流转时记录共享 + 小铃铛通知 Plugin（禅道 #1727）
    /// 触发时机：mcs_fsm_data Update PostOperation，mcs_fsm_status 字段变更
    /// 业务规则：
    ///   #1727 融资状态变为 2（融资立项，融资经理推进）时：
    ///         记录共享给「融资方案接口人」（mcs_fsm_manager，Read+Write），
    ///         并发送小铃铛通知其复核融资六要素并推进后续工作。
    ///   【#1759 变更】原 #1712「状态→4 共享给贷后管理人」逻辑已后移至 BPF 实例「完成」时点，
    ///   由 FsmDataBpfCompleteShareNotifyPlugin（mcs_fmprocess Update）承担，本 Plugin 不再处理状态 4。
    ///   【#1764 变更】共享主记录给融资方案接口人时，级联共享当时已存在的关联业务记录
    ///   （线索/报价单/合同/客户主数据/客户 account，均只读 Read）；融资落实及其附件在立项时点尚未产生，
    ///   不在此处共享，由 BPF 完成时点（FsmDataBpfCompleteShareNotifyPlugin）统一共享给贷后管理人。
    /// 注意：本环境 SendAppNotification 不支持 Data 操作按钮、正文链接不可点击（2026-08-06 实测），
    /// 故正文直接放融资管理编号，用户凭编号自行查找记录。
    /// 共享/通知失败仅记 Trace，绝不影响阶段流转主流程。
    /// </summary>
    public class FsmDataHandoverShareNotifyPlugin : IPlugin
    {
        // mcs_fsm_data.mcs_fsm_status 选项集值
        private const int FSM_STATUS_INITIATION = 2;     // 融资立项

        // SendAppNotification 参数枚举（同 #1654 实测口径）
        private const int ICON_SUCCESS = 100000001;  // IconType=Success（✅ 绿色）
        private const int TOAST_TIMED = 200000000;   // ToastType=Timed（弹出后自动消失）
        private const int EXPIRY_SECONDS = 604800;   // 通知保留 7 天

        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = factory.CreateOrganizationService(context.UserId);
            ITracingService tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            // 系统身份服务：级联共享线索/报价单/合同等他人负责的业务记录时，
            // 触发的融资经理对那些记录可能没有共享权限，必须用系统身份执行 GrantAccess
            IOrganizationService systemService = factory.CreateOrganizationService(null);

            tracer.Trace("=== FsmDataHandoverShareNotifyPlugin 开始执行 ===");
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

            int newStatus = GetOptionSetValue(target, "mcs_fsm_status");
            int oldStatus = context.PreEntityImages.Contains("PreImage")
                ? GetOptionSetValue(context.PreEntityImages["PreImage"], "mcs_fsm_status")
                : -1;
            tracer.Trace($"融资状态旧值={oldStatus}，新值={newStatus}");

            Entity preImage = context.PreEntityImages.Contains("PreImage") ? context.PreEntityImages["PreImage"] : null;
            string fsmNo = preImage != null ? preImage.GetAttributeValue<string>("mcs_fsm_no") : null;

            // 取值口径：Target 优先、PreImage 兜底——用户可能在表单上改完人字段未保存就直接点 BPF 推进阶段，
            // 此时平台保存把人字段与状态放在同一事务，PreImage 里是旧值（空），必须从 Target 取新值。
            // 注意：Target 含该键但值为 null = 用户明确清空，应按空处理（跳过），不可回退 PreImage 旧值。
            EntityReference contactRef = target.Contains("mcs_fsm_manager")
                ? target.GetAttributeValue<EntityReference>("mcs_fsm_manager")
                : (preImage != null ? preImage.GetAttributeValue<EntityReference>("mcs_fsm_manager") : null);

            // #1727：进入融资立项（新值=2 且旧值≠2）→ 共享+通知融资方案接口人
            if (newStatus == FSM_STATUS_INITIATION && oldStatus != FSM_STATUS_INITIATION)
            {
                tracer.Trace($"[#1727] 进入融资立项阶段，融资方案接口人: {(contactRef != null ? $"{contactRef.Name} ({contactRef.Id})" : "空")}");
                if (contactRef == null)
                {
                    tracer.Trace("[#1727] ⚠️ 融资方案接口人为空，跳过共享与通知");
                }
                else
                {
                    ShareRecord(service, tracer, target.ToEntityReference(), contactRef, "融资方案接口人");
                    // #1764：级联共享关联业务记录（只读），失败仅记 Trace 不阻断主流程
                    ShareFsmRelatedRecords(systemService, tracer, target.Id, contactRef, "融资方案接口人", includeImplementation: false);
                    SendNotification(service, tracer, contactRef,
                        "融资立项待复核",
                        $"您好，融资经理已完成单据【{fsmNo}】的融资需求、融资六要素信息填报并提交。现流转至您环节，请您复核、确认本条记录的融资六要素内容是否准确并推进后续工作。");
                }
            }

            tracer.Trace("=== FsmDataHandoverShareNotifyPlugin 执行结束 ===");
        }

        /// <summary>
        /// 共享记录给指定用户（Read+Write），失败仅记 Trace 不阻断主流程。
        /// internal 供同模块 FsmDataBpfCompleteShareNotifyPlugin（#1759）复用。
        /// </summary>
        internal static void ShareRecord(IOrganizationService service, ITracingService tracer,
            EntityReference recordRef, EntityReference principal, string principalRole)
        {
            ShareRecord(service, tracer, recordRef, principal, principalRole,
                AccessRights.ReadAccess | AccessRights.WriteAccess);
        }

        /// <summary>
        /// 共享记录给指定用户（可指定权限掩码），失败仅记 Trace 不阻断主流程。
        /// </summary>
        internal static void ShareRecord(IOrganizationService service, ITracingService tracer,
            EntityReference recordRef, EntityReference principal, string principalRole, AccessRights accessMask)
        {
            try
            {
                var grantRequest = new GrantAccessRequest
                {
                    Target = recordRef,
                    PrincipalAccess = new PrincipalAccess
                    {
                        Principal = new EntityReference("systemuser", principal.Id),
                        AccessMask = accessMask
                    }
                };
                service.Execute(grantRequest);
                tracer.Trace($"✅ 记录({recordRef.LogicalName} {recordRef.Id})已共享给{principalRole}: {principal.Name}，权限={accessMask}");
            }
            catch (Exception ex)
            {
                tracer.Trace($"⚠️ 共享记录({recordRef.LogicalName} {recordRef.Id})给{principalRole}失败（已忽略，不影响主流程）: {ex.Message}");
            }
        }

        /// <summary>
        /// 【#1764】级联共享融资管理记录的关联记录给指定用户。
        /// 始终共享（只读 Read）：线索 mcs_leadmain_id / 报价单 mcs_quoter_id / 合同 mcs_contract_id /
        ///   客户主数据 mcs_customer_name（Lookup→mcs_customermasterdata）/ 客户 account（按 account.mcs_customermasterdata 反查）。
        /// includeImplementation=true 时（BPF 完成共享贷后管理人场景）追加共享：
        ///   全部融资落实 mcs_fsm_detail_data（Read+Write）、落实上的订单 mcs_order_id（只读）、
        ///   落实附件 mcs_customer_file（按 mcs_fsm_detail_dataid 反查，Read+Write）。
        /// 全部操作幂等（重复 GrantAccess 无副作用），单条失败仅记 Trace 继续后续记录。
        /// </summary>
        internal static void ShareFsmRelatedRecords(IOrganizationService systemService, ITracingService tracer,
            Guid fsmDataId, EntityReference principal, string principalRole, bool includeImplementation)
        {
            try
            {
                Entity fsm = systemService.Retrieve("mcs_fsm_data", fsmDataId,
                    new ColumnSet("mcs_leadmain_id", "mcs_quoter_id", "mcs_contract_id", "mcs_customer_name"));

                // 1. 线索 / 报价单 / 合同 / 客户主数据：直接取主记录 Lookup（只读）
                ShareLookupRecord(systemService, tracer, fsm, "mcs_leadmain_id", principal, principalRole, AccessRights.ReadAccess, "线索");
                ShareLookupRecord(systemService, tracer, fsm, "mcs_quoter_id", principal, principalRole, AccessRights.ReadAccess, "报价单");
                ShareLookupRecord(systemService, tracer, fsm, "mcs_contract_id", principal, principalRole, AccessRights.ReadAccess, "合同");
                EntityReference customerMasterRef = fsm.GetAttributeValue<EntityReference>("mcs_customer_name");
                if (customerMasterRef != null)
                {
                    ShareRecord(systemService, tracer, customerMasterRef, principal, principalRole, AccessRights.ReadAccess);

                    // 2. 客户 account：按 account.mcs_customermasterdata 反查（只读）
                    try
                    {
                        var accountQuery = new QueryExpression("account")
                        {
                            ColumnSet = new ColumnSet("accountid"),
                            TopCount = 1,
                            Criteria = new FilterExpression
                            {
                                Conditions =
                                {
                                    new ConditionExpression("mcs_customermasterdata", ConditionOperator.Equal, customerMasterRef.Id)
                                }
                            }
                        };
                        var accounts = systemService.RetrieveMultiple(accountQuery).Entities;
                        if (accounts.Count > 0)
                        {
                            ShareRecord(systemService, tracer, accounts[0].ToEntityReference(), principal, principalRole, AccessRights.ReadAccess);
                        }
                        else
                        {
                            tracer.Trace($"[#1764] 未找到客户主数据 {customerMasterRef.Id} 对应的 account 记录，跳过客户共享");
                        }
                    }
                    catch (Exception ex)
                    {
                        tracer.Trace($"⚠️ [#1764] 反查/共享客户 account 失败（已忽略）: {ex.Message}");
                    }
                }
                else
                {
                    tracer.Trace("[#1764] 主记录客户名称为空，跳过客户主数据/客户共享");
                }

                // 3. 融资落实 + 订单 + 落实附件（仅 BPF 完成共享贷后管理人场景）
                if (includeImplementation)
                {
                    var detailQuery = new QueryExpression("mcs_fsm_detail_data")
                    {
                        ColumnSet = new ColumnSet("mcs_order_id"),
                        Criteria = new FilterExpression
                        {
                            Conditions =
                            {
                                new ConditionExpression("mcs_fsm_data_id", ConditionOperator.Equal, fsmDataId)
                            }
                        }
                    };
                    var details = systemService.RetrieveMultiple(detailQuery).Entities;
                    tracer.Trace($"[#1764] 融资落实记录共 {details.Count} 条");
                    foreach (var detail in details)
                    {
                        // 落实记录（Read+Write，与主记录一致）
                        ShareRecord(systemService, tracer, detail.ToEntityReference(), principal, principalRole);

                        // 订单（salesorder，只读）
                        ShareLookupRecord(systemService, tracer, detail, "mcs_order_id", principal, principalRole, AccessRights.ReadAccess, "订单");

                        // 落实附件（mcs_customer_file，Read+Write）
                        try
                        {
                            var fileQuery = new QueryExpression("mcs_customer_file")
                            {
                                ColumnSet = new ColumnSet(false),
                                Criteria = new FilterExpression
                                {
                                    Conditions =
                                    {
                                        new ConditionExpression("mcs_fsm_detail_dataid", ConditionOperator.Equal, detail.Id)
                                    }
                                }
                            };
                            var files = systemService.RetrieveMultiple(fileQuery).Entities;
                            foreach (var file in files)
                            {
                                ShareRecord(systemService, tracer, file.ToEntityReference(), principal, principalRole);
                            }
                            if (files.Count > 0)
                            {
                                tracer.Trace($"[#1764] 落实 {detail.Id} 的附件已共享 {files.Count} 条");
                            }
                        }
                        catch (Exception ex)
                        {
                            tracer.Trace($"⚠️ [#1764] 查询/共享落实 {detail.Id} 的附件失败（已忽略）: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                tracer.Trace($"⚠️ [#1764] 级联共享关联记录失败（已忽略，不影响主流程）: {ex.Message}");
            }
        }

        /// <summary>
        /// 取实体上的 Lookup 字段并共享其引用记录（字段为空则跳过并记 Trace）。
        /// </summary>
        private static void ShareLookupRecord(IOrganizationService systemService, ITracingService tracer,
            Entity entity, string lookupField, EntityReference principal, string principalRole,
            AccessRights accessMask, string recordLabel)
        {
            EntityReference refValue = entity.GetAttributeValue<EntityReference>(lookupField);
            if (refValue == null)
            {
                tracer.Trace($"[#1764] {recordLabel}字段 {lookupField} 为空，跳过共享");
                return;
            }
            ShareRecord(systemService, tracer, refValue, principal, principalRole, accessMask);
        }

        /// <summary>
        /// 发送小铃铛（In-App Notification），失败仅记 Trace 不阻断主流程。
        /// internal 供同模块 FsmDataBpfCompleteShareNotifyPlugin（#1759）复用。
        /// </summary>
        internal static void SendNotification(IOrganizationService service, ITracingService tracer,
            EntityReference recipient, string title, string body)
        {
            try
            {
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
                tracer.Trace($"✅ 小铃铛通知已发送: 标题「{title}」，NotificationId={(response.Results.Contains("NotificationId") ? response.Results["NotificationId"] : "未知")}");
            }
            catch (Exception ex)
            {
                tracer.Trace($"⚠️ SendAppNotification 发送失败（已忽略，不影响主流程）: {ex.Message}");
            }
        }

        /// <summary>
        /// 安全读取 OptionSetValue 字段，缺失返回 -1。
        /// internal 供同模块 FsmDataBpfCompleteShareNotifyPlugin（#1759）复用。
        /// </summary>
        internal static int GetOptionSetValue(Entity entity, string fieldName)
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
