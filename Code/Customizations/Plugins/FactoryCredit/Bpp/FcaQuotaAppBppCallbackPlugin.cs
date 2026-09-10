using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using SanyD365.Plugins.FactoryCredit.Bpp.Services;
using System;

namespace SanyD365.Plugins.FactoryCredit.Bpp
{
    /// <summary>
    /// 厂端授信额度调整申请 - BPP回调Plugin
    /// 触发时机：mcs_fca_quotaapp Update PostOperation, mcs_bppstatuscode 字段变更
    /// 业务规则：根据 BPP 回调状态更新审批状态，审批通过后更新额度表并写入台账
    /// </summary>
    public class FcaQuotaAppBppCallbackPlugin : IPlugin
    {
        // mcs_fca_quotaapp.mcs_bppstatus 选项集值
        private const int STATUS_APPLY = 1;      // 申请
        private const int STATUS_APPROVED = 3;   // 通过
        private const int STATUS_REJECTED = 4;   // 驳回

        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = factory.CreateOrganizationService(context.UserId);
            ITracingService tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            // 系统上下文：审批通过后额度表(mcs_fca_quota)/台账(mcs_fca_records)读写走系统(admin)身份（禅道 #1855），
            // 不依赖回调触发人对基础数据的权限；申请单本身状态回写仍走用户上下文
            IOrganizationService systemService = factory.CreateOrganizationService(null);

            tracer.Trace("=== FcaQuotaAppBppCallbackPlugin 开始执行 ===");
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
            if (target.LogicalName != "mcs_fca_quotaapp")
            {
                tracer.Trace($"非额度调整申请实体，跳过: {target.LogicalName}");
                return;
            }

            // 只处理 BPP 审批状态码字段变更
            if (!target.Contains("mcs_bppstatuscode"))
            {
                tracer.Trace("mcs_bppstatuscode 未变更，跳过");
                return;
            }

            string bppStatus = GetBppStatusValue(target, tracer);
            tracer.Trace($"BPP 回调状态: {bppStatus}");

            if (string.IsNullOrWhiteSpace(bppStatus))
            {
                tracer.Trace("BPP 状态为空，跳过处理");
                return;
            }

            // 中间状态不处理业务状态流转
            if (IsIntermediateStatus(bppStatus))
            {
                tracer.Trace($"BPP 中间状态: {bppStatus}，跳过业务状态流转");
                return;
            }

            try
            {
                Entity updateRecord = new Entity("mcs_fca_quotaapp") { Id = target.Id };

                switch (bppStatus.ToLowerInvariant())
                {
                    case "approved":
                    case "30":
                        tracer.Trace("BPP 审批通过，更新审批状态并回写额度表");
                        updateRecord["mcs_bppstatus"] = new OptionSetValue(STATUS_APPROVED);
                        updateRecord["mcs_approvedate"] = DateTime.UtcNow;
                        service.Update(updateRecord);

                        // 更新额度表并写入台账
                        ProcessApprovalResult(service, systemService, tracer, target.Id);
                        break;

                    case "rejected":
                    case "11":
                        tracer.Trace("BPP 审批驳回");
                        updateRecord["mcs_bppstatus"] = new OptionSetValue(STATUS_REJECTED);
                        service.Update(updateRecord);
                        break;

                    case "withdrawn":
                    case "withdraw":
                        tracer.Trace("BPP 审批撤回，状态回到申请");
                        updateRecord["mcs_bppstatus"] = new OptionSetValue(STATUS_APPLY);
                        updateRecord["mcs_bppid"] = null;
                        updateRecord["mcs_nextapprover"] = null;
                        service.Update(updateRecord);
                        break;

                    case "abandoned":
                    case "abandon":
                        tracer.Trace("BPP 审批废弃，状态回到申请");
                        updateRecord["mcs_bppstatus"] = new OptionSetValue(STATUS_APPLY);
                        updateRecord["mcs_bppid"] = null;
                        updateRecord["mcs_nextapprover"] = null;
                        service.Update(updateRecord);
                        break;

                    default:
                        tracer.Trace($"未知的 BPP 回调状态: {bppStatus}，暂不处理");
                        break;
                }
            }
            catch (Exception ex)
            {
                tracer.Trace($"FcaQuotaAppBppCallbackPlugin 异常: {ex.Message}");
                tracer.Trace($"异常堆栈: {ex.StackTrace}");
                throw new InvalidPluginExecutionException($"BPP 回调处理失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 审批通过后处理：更新额度表 + 写入台账
        /// </summary>
        private void ProcessApprovalResult(IOrganizationService service, IOrganizationService systemService, ITracingService tracer, Guid quotaAppId)
        {
            Entity quotaApp = service.Retrieve("mcs_fca_quotaapp", quotaAppId,
                new ColumnSet("mcs_accountid", "mcs_custname", "mcs_sellergrant", "mcs_sellerbalance",
                              "mcs_tobegrant", "mcs_tobebalance", "mcs_doid", "ownerid"));

            EntityReference accountRef = quotaApp.GetAttributeValue<EntityReference>("mcs_accountid");
            string custName = quotaApp.GetAttributeValue<string>("mcs_custname");
            Money currentGrant = quotaApp.GetAttributeValue<Money>("mcs_sellergrant");
            Money currentBalance = quotaApp.GetAttributeValue<Money>("mcs_sellerbalance");
            Money tobeGrant = quotaApp.GetAttributeValue<Money>("mcs_tobegrant");
            Money tobeBalance = quotaApp.GetAttributeValue<Money>("mcs_tobebalance");
            EntityReference doidRef = quotaApp.GetAttributeValue<EntityReference>("mcs_doid");
            string doid = GetDoidString(service, doidRef);

            if (accountRef == null)
            {
                throw new InvalidPluginExecutionException("额度调整申请缺少客户编码，无法处理审批结果。");
            }

            if (tobeGrant == null)
            {
                throw new InvalidPluginExecutionException("额度调整申请缺少调整后额度，无法处理审批结果。");
            }

            // #1643/#1856 台账/额度记录负责人跟随申请人，避免本人级/部门级权限用户看不到系统身份名下记录
            // 口径（用户 2026-08-17 明确）：台账 owner=申请人（申请单 owner）；额度 owner=申请人，客户负责人仅作兜底
            EntityReference appOwner = quotaApp.GetAttributeValue<EntityReference>("ownerid");
            EntityReference customerOwner = GetCustomerOwner(service, tracer, accountRef);
            EntityReference quotaOwner = appOwner ?? customerOwner;
            EntityReference ledgerOwner = appOwner ?? customerOwner;
            tracer.Trace($"负责人归属: 申请人={(appOwner != null ? appOwner.Id.ToString() : "空")}, 客户负责人={(customerOwner != null ? customerOwner.Id.ToString() : "空")}");

            // 1. 更新额度表（#1855 系统身份读写 mcs_fca_quota）
            // #2025 proc 存在（doid 文本非空）时才传 procRef 写 Lookup，避免指向已删除 proc 的失效引用
            QuotaActivationService quotaService = new QuotaActivationService(systemService, tracer);
            quotaService.ActivateQuota(accountRef, custName, tobeGrant, tobeBalance, doid, quotaOwner,
                string.IsNullOrWhiteSpace(doid) ? null : doidRef);

            // 2. 写入台账（#1855 系统身份写 mcs_fca_records）
            QuotaRecordService recordService = new QuotaRecordService(systemService, tracer);
            recordService.AddQuotaRecord(accountRef, custName, currentGrant, currentBalance, tobeGrant, tobeBalance, ledgerOwner);
        }

        /// <summary>
        /// 读取客户主数据负责人（#1643）；读取失败返回 null，由调用方兜底，不阻断审批回写主流程
        /// </summary>
        private EntityReference GetCustomerOwner(IOrganizationService service, ITracingService tracer, EntityReference accountRef)
        {
            try
            {
                Entity customer = service.Retrieve("mcs_customermasterdata", accountRef.Id, new ColumnSet("ownerid"));
                return customer.GetAttributeValue<EntityReference>("ownerid");
            }
            catch (Exception ex)
            {
                tracer.Trace($"读取客户主数据负责人失败（按申请人兜底）: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 从额度调整申请的模型计算序列号 Lookup 中读取字符串序列号
        /// </summary>
        private string GetDoidString(IOrganizationService service, EntityReference doidRef)
        {
            if (doidRef == null)
            {
                return string.Empty;
            }

            try
            {
                var proc = service.Retrieve("mcs_fca_proc", doidRef.Id, new ColumnSet("mcs_doid"));
                return proc.GetAttributeValue<string>("mcs_doid") ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// 获取 BPP 状态值，兼容字符串和数字类型
        /// </summary>
        private string GetBppStatusValue(Entity target, ITracingService tracer)
        {
            if (target["mcs_bppstatuscode"] is string strValue)
            {
                return strValue.Trim();
            }

            if (target["mcs_bppstatuscode"] is OptionSetValue optionValue)
            {
                return optionValue.Value.ToString();
            }

            tracer.Trace($"mcs_bppstatuscode 类型未识别: {target["mcs_bppstatuscode"].GetType().FullName}");
            return target["mcs_bppstatuscode"]?.ToString() ?? "";
        }

        /// <summary>
        /// 判断是否为中间状态
        /// </summary>
        private bool IsIntermediateStatus(string bppStatus)
        {
            string status = bppStatus.ToLowerInvariant();
            return status == "submitted" ||
                   status == "pending" ||
                   status == "10" ||
                   status == "20";
        }
    }
}
