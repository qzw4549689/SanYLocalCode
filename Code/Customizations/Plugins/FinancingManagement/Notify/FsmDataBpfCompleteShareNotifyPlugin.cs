using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace SanyD365.Plugins.FinancingManagement.Notify
{
    /// <summary>
    /// 融资管理 - BPF「融资管理」实例完成时记录共享 + 小铃铛通知 Plugin（禅道 #1759，原 #1712 逻辑后移）
    /// 触发时机：mcs_fmprocess（BPF「融资管理」实例实体）Update，statecode/statuscode 字段变更
    ///   - PreOperation（Stage 20）：禅道 #1766 完成前必填校验——点「完成」时贷后管理人必填（点保存不校验）
    ///   - PostOperation（Stage 40）：禅道 #1759 共享+通知贷后管理人
    /// 业务规则：
    ///   #1766（PreOp）：BPF 实例即将完成（statuscode→2）且关联融资记录 mcs_fsm_status=4 时，
    ///         贷后管理人（mcs_fsm_postloan_manager）为空则抛 InvalidPluginExecutionException 阻断完成。
    ///         （前端 OnPreProcessStatusChange 在本环境无 preventDefault 无法阻断，2026-08-11 实锤，故服务端兜底）
    ///   #1712（#1759 后移，PostOp）：融资经理在融资落实阶段点击 BPF 阶段条原生「完成」按钮
    ///         （BPF 实例 statuscode 变为 2=完成）时：
    ///         关联融资记录共享给「贷后管理人」（mcs_fsm_postloan_manager，Read+Write），
    ///         并发送小铃铛通知其查看记录并推进贷后工作。
    ///   安全口径：仅当关联融资记录 mcs_fsm_status=4（融资落实）才执行共享，
    ///         防止融资落实前通过 API 等途径提前完成 BPF 导致贷后管理人提前看到记录。
    ///   【#1764 变更】共享主记录给贷后管理人时，级联共享关联记录：
    ///   融资落实 mcs_fsm_detail_data（Read+Write）、落实附件 mcs_customer_file（Read+Write）、
    ///   订单 salesorder（只读）、线索/报价单/合同/客户主数据/客户 account（只读）。
    ///   落实记录及其附件在 BPF 完成时点均已产生，一次扫全共享即可，无需增量补共享（2026-08-11 用户确认口径）。
    ///   防重口径：PreImage 对比 旧值≠2 且 新值=2 才触发；
    ///         BPF 重新激活后再次完成会再次触发（共享幂等无副作用，通知会重发，业务可接受）。
    /// 背景（禅道 #1759）：原 #1712 在 mcs_fsm_status 变为 4 时（BPP 方案审批通过回调）即共享，
    ///   导致阶段刚进入融资落实、融资经理尚未点击「完成」时贷后接口人即可看到记录；
    ///   经用户 2026-08-11 确认，共享+通知统一后移到 BPF 实例「完成」时点。
    /// 注意：本环境 SendAppNotification 不支持 Data 操作按钮、正文链接不可点击（2026-08-06 实测），
    /// 故正文直接放融资管理编号，用户凭编号自行查找记录。
    /// 共享/通知失败仅记 Trace，绝不影响 BPF 完成主流程。
    /// </summary>
    public class FsmDataBpfCompleteShareNotifyPlugin : IPlugin
    {
        // BPF「融资管理」实例实体及其关联融资记录 Lookup（2026-08-11 DEV1 实查：
        // workflow 18c5e92c「融资管理」→ BPF 实体 mcs_fmprocess，关联字段 bpf_mcs_fsm_dataid）
        private const string BPF_ENTITY = "mcs_fmprocess";
        private const string FSM_LOOKUP = "bpf_mcs_fsm_dataid";

        // mcs_fmprocess.statuscode 选项集值（2026-08-11 DEV1 元数据实查）：1=可用 / 2=完成 / 3=已中止
        private const int BPF_STATUS_FINISHED = 2;

        // mcs_fsm_data.mcs_fsm_status 选项集值：4=融资落实
        private const int FSM_STATUS_IMPLEMENTATION = 4;

        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = factory.CreateOrganizationService(context.UserId);
            ITracingService tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            // 系统身份服务：级联共享落实/附件/线索/合同等他人负责的记录时必须用系统身份执行 GrantAccess
            IOrganizationService systemService = factory.CreateOrganizationService(null);

            tracer.Trace("=== FsmDataBpfCompleteShareNotifyPlugin 开始执行 ===");
            tracer.Trace($"Message: {context.MessageName}, Stage: {context.Stage}, Depth: {context.Depth}");

            // 防递归
            if (context.Depth > 3)
            {
                tracer.Trace("递归深度超过3，跳过处理");
                return;
            }

            if (context.MessageName != "Update")
            {
                tracer.Trace("非 Update 事件，跳过");
                return;
            }

            if (!context.InputParameters.Contains("Target") || !(context.InputParameters["Target"] is Entity target))
            {
                tracer.Trace("未找到 Target 参数");
                return;
            }

            // 严格校验实体名（仅 BPF「融资管理」实例实体）
            if (target.LogicalName != BPF_ENTITY)
            {
                tracer.Trace($"非 BPF 融资管理实例实体，跳过: {target.LogicalName}");
                return;
            }

            // PreOperation：#1766 完成前必填校验（阻断型）
            if (context.Stage == 20)
            {
                ValidatePostloanRequiredOnComplete(context, service, tracer, target);
                tracer.Trace("=== FsmDataBpfCompleteShareNotifyPlugin 执行结束（PreOp 校验） ===");
                return;
            }

            if (context.Stage != 40)
            {
                tracer.Trace($"非 PostOperation 事件（Stage={context.Stage}），跳过");
                return;
            }

            // 只处理 statuscode 变更（注册时 FilteringAttributes=statecode,statuscode，
            // 阶段推进 moveNext 只改 activestageid/traversedpath，不会触发本 Plugin）
            if (!target.Contains("statuscode"))
            {
                tracer.Trace("statuscode 未变更，跳过");
                return;
            }

            int newStatus = FsmDataHandoverShareNotifyPlugin.GetOptionSetValue(target, "statuscode");
            Entity preImage = context.PreEntityImages.Contains("PreImage") ? context.PreEntityImages["PreImage"] : null;
            int oldStatus = preImage != null ? FsmDataHandoverShareNotifyPlugin.GetOptionSetValue(preImage, "statuscode") : -1;
            tracer.Trace($"BPF 实例状态旧值={oldStatus}，新值={newStatus}");

            // 仅「完成」跃迁（旧≠2 且 新=2）；重新激活（2→1）、中止（→3）、同值重复均不触发
            if (newStatus != BPF_STATUS_FINISHED || oldStatus == BPF_STATUS_FINISHED)
            {
                tracer.Trace("非「完成」跃迁，跳过");
                return;
            }

            // 取关联融资记录：Target 优先、PreImage 兜底（Lookup 正常不会变更，口径与 #1727 一致）
            EntityReference fsmRef = target.Contains(FSM_LOOKUP)
                ? target.GetAttributeValue<EntityReference>(FSM_LOOKUP)
                : (preImage != null ? preImage.GetAttributeValue<EntityReference>(FSM_LOOKUP) : null);
            if (fsmRef == null)
            {
                tracer.Trace("⚠️ BPF 实例未关联融资记录，跳过");
                return;
            }
            tracer.Trace($"关联融资记录: {fsmRef.Name} ({fsmRef.Id})");

            // 回查融资记录：融资状态 + 贷后管理人 + 编号
            Entity fsm;
            try
            {
                fsm = service.Retrieve("mcs_fsm_data", fsmRef.Id,
                    new ColumnSet("mcs_fsm_no", "mcs_fsm_status", "mcs_fsm_postloan_manager"));
            }
            catch (Exception ex)
            {
                tracer.Trace($"⚠️ 回查融资记录失败（已忽略，不影响 BPF 完成主流程）: {ex.Message}");
                return;
            }

            // 安全口径：仅融资落实阶段（mcs_fsm_status=4）才共享，
            // 防止融资落实前通过 API 等途径提前完成 BPF 导致贷后管理人提前看到记录
            int fsmStatus = FsmDataHandoverShareNotifyPlugin.GetOptionSetValue(fsm, "mcs_fsm_status");
            if (fsmStatus != FSM_STATUS_IMPLEMENTATION)
            {
                tracer.Trace($"⚠️ 关联融资记录当前状态={fsmStatus}（非 4 融资落实），安全起见跳过共享与通知");
                return;
            }

            string fsmNo = fsm.GetAttributeValue<string>("mcs_fsm_no");
            EntityReference postloanRef = fsm.GetAttributeValue<EntityReference>("mcs_fsm_postloan_manager");
            tracer.Trace($"[#1712/#1759] BPF 已完成，贷后管理人: {(postloanRef != null ? $"{postloanRef.Name} ({postloanRef.Id})" : "空")}");
            if (postloanRef == null)
            {
                tracer.Trace("[#1712/#1759] ⚠️ 贷后管理人为空，跳过共享与通知");
                return;
            }

            FsmDataHandoverShareNotifyPlugin.ShareRecord(service, tracer,
                new EntityReference("mcs_fsm_data", fsmRef.Id), postloanRef, "贷后管理人");
            // #1764：级联共享落实/附件/订单/线索/报价单/合同/客户主数据/客户，失败仅记 Trace 不阻断主流程
            FsmDataHandoverShareNotifyPlugin.ShareFsmRelatedRecords(systemService, tracer,
                fsmRef.Id, postloanRef, "贷后管理人", includeImplementation: true);
            FsmDataHandoverShareNotifyPlugin.SendNotification(service, tracer, postloanRef,
                "融资落实贷后跟进",
                $"您好，融资经理已完成单据【{fsmNo}】的融资落实的填报。现流转至您环节，您可以查看该条记录并推进后续工作。");

            tracer.Trace("=== FsmDataBpfCompleteShareNotifyPlugin 执行结束 ===");
        }

        /// <summary>
        /// 【禅道 #1766】PreOperation：BPF 点「完成」时贷后管理人必填校验（点击保存不校验）。
        /// 仅在「完成」跃迁（Target statuscode=2）且关联融资记录处于融资落实（mcs_fsm_status=4）时强制；
        /// 贷后管理人为空则抛异常阻断完成（同步 PreOp 抛出即回滚，UCI 弹出 Business Process Error 对话框）。
        /// </summary>
        private static void ValidatePostloanRequiredOnComplete(
            IPluginExecutionContext context, IOrganizationService service, ITracingService tracer, Entity target)
        {
            // 仅处理「完成」（statuscode 新值=2）；重新激活/中止/阶段推进不校验
            if (!target.Contains("statuscode")
                || FsmDataHandoverShareNotifyPlugin.GetOptionSetValue(target, "statuscode") != BPF_STATUS_FINISHED)
            {
                tracer.Trace("PreOp：非「完成」跃迁，跳过必填校验");
                return;
            }

            Entity preImage = context.PreEntityImages.Contains("PreImage") ? context.PreEntityImages["PreImage"] : null;
            EntityReference fsmRef = target.Contains(FSM_LOOKUP)
                ? target.GetAttributeValue<EntityReference>(FSM_LOOKUP)
                : (preImage != null ? preImage.GetAttributeValue<EntityReference>(FSM_LOOKUP) : null);
            if (fsmRef == null)
            {
                tracer.Trace("PreOp：⚠️ BPF 实例未关联融资记录，跳过必填校验");
                return;
            }

            Entity fsm = service.Retrieve("mcs_fsm_data", fsmRef.Id,
                new ColumnSet("mcs_fsm_status", "mcs_fsm_postloan_manager"));
            int fsmStatus = FsmDataHandoverShareNotifyPlugin.GetOptionSetValue(fsm, "mcs_fsm_status");
            if (fsmStatus != FSM_STATUS_IMPLEMENTATION)
            {
                tracer.Trace($"PreOp：关联融资记录状态={fsmStatus}（非 4 融资落实），不强制必填");
                return;
            }

            if (fsm.GetAttributeValue<EntityReference>("mcs_fsm_postloan_manager") == null)
            {
                tracer.Trace("PreOp：❌ 贷后管理人为空，阻断 BPF 完成");
                throw new InvalidPluginExecutionException("请先填写「融资落实」页签中的贷后管理人并保存，再点击完成。");
            }
            tracer.Trace("PreOp：贷后管理人已填写，必填校验通过");
        }
    }
}
