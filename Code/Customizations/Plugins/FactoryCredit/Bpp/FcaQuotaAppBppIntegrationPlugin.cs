using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace SanyD365.Plugins.FactoryCredit.Bpp
{
    /// <summary>
    /// 厂端授信额度调整申请 - BPP提交Plugin
    /// 触发时机：mcs_fca_quotaapp Update PostOperation, mcs_bppstatus 字段变更
    /// 业务规则：当审批状态从非"审批中"变为 2（审批中）时，调用 mcs_bppstartapi 发起 BPP 审批
    /// </summary>
    public class FcaQuotaAppBppIntegrationPlugin : IPlugin
    {
        // mcs_fca_quotaapp.mcs_bppstatus 选项集值：2-审批中
        private const int STATUS_IN_REVIEW = 2;

        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = factory.CreateOrganizationService(context.UserId);
            ITracingService tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracer.Trace("=== FcaQuotaAppBppIntegrationPlugin 开始执行 ===");
            tracer.Trace($"Message: {context.MessageName}, Stage: {context.Stage}, Depth: {context.Depth}");

            // 防递归
            if (context.Depth > 2)
            {
                tracer.Trace("递归深度超过2，跳过处理");
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

            // 只处理审批状态字段变更
            if (!target.Contains("mcs_bppstatus"))
            {
                tracer.Trace("审批状态字段未变更，跳过");
                return;
            }

            int newStatus = GetOptionSetValue(target, "mcs_bppstatus");
            tracer.Trace($"新审批状态: {newStatus}");

            if (newStatus != STATUS_IN_REVIEW)
            {
                tracer.Trace($"当前状态不是审批中({newStatus})，跳过 BPP 处理");
                return;
            }

            // 获取旧状态
            int oldStatus = 0;
            if (context.PreEntityImages.Contains("PreImage"))
            {
                Entity preImage = context.PreEntityImages["PreImage"];
                oldStatus = GetOptionSetValue(preImage, "mcs_bppstatus");
                tracer.Trace($"旧审批状态: {oldStatus}");
            }

            if (oldStatus == STATUS_IN_REVIEW)
            {
                tracer.Trace("原状态已是审批中，不重复处理");
                return;
            }

            try
            {
                // 查询现有 BPP 信息，防重复提交
                Entity quotaApp = service.Retrieve("mcs_fca_quotaapp", target.Id,
                    new ColumnSet("mcs_bppid", "mcs_bppstatuscode"));

                string existingWorkflowId = quotaApp.GetAttributeValue<string>("mcs_bppid");
                string existingBppStatus = quotaApp.GetAttributeValue<string>("mcs_bppstatuscode");

                tracer.Trace($"现有 workflowId: {existingWorkflowId}, bppStatusCode: {existingBppStatus}");

                if (!string.IsNullOrEmpty(existingWorkflowId) && IsBppInProgress(existingBppStatus))
                {
                    tracer.Trace("流程仍在审批中，跳过重复提交");
                    return;
                }

                // 调用 mcs_bppstartapi
                OrganizationRequest request = new OrganizationRequest("mcs_bppstartapi");
                request["EntityId"] = target.Id.ToString();
                request["EntityName"] = "mcs_fca_quotaapp";
                request["UserId"] = context.InitiatingUserId.ToString();

                tracer.Trace($"调用 mcs_bppstartapi: EntityId={target.Id}, UserId={context.InitiatingUserId}");

                OrganizationResponse response = service.Execute(request);
                string result = response["Result"]?.ToString() ?? "";
                tracer.Trace($"mcs_bppstartapi 返回: {result}");

                bool success = !string.IsNullOrEmpty(result) &&
                               (result.IndexOf("true", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                result.IndexOf("success", StringComparison.OrdinalIgnoreCase) >= 0);

                if (success)
                {
                    // 更新 BPP 审批状态码为 Submitted
                    Entity updateRecord = new Entity("mcs_fca_quotaapp") { Id = target.Id };
                    updateRecord["mcs_bppstatuscode"] = "Submitted";
                    updateRecord["mcs_bpperrormsg"] = null;
                    service.Update(updateRecord);
                    tracer.Trace("BPP 提交成功，状态码更新为 Submitted");
                }
                else
                {
                    tracer.Trace($"BPP 发起失败: {result}");
                    throw new InvalidPluginExecutionException($"BPP 审批发起失败: {result}");
                }
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                tracer.Trace($"FcaQuotaAppBppIntegrationPlugin 异常: {ex.Message}");
                throw new InvalidPluginExecutionException($"BPP 集成处理失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 判断 BPP 状态是否为"进行中"
        /// </summary>
        private bool IsBppInProgress(string bppStatus)
        {
            if (string.IsNullOrWhiteSpace(bppStatus))
                return false;

            string status = bppStatus.Trim().ToLowerInvariant();
            return status == "submitted" ||
                   status == "inreview" ||
                   status == "pending" ||
                   status == "10" ||
                   status == "20";
        }

        private int GetOptionSetValue(Entity entity, string fieldName)
        {
            if (!entity.Contains(fieldName))
            {
                return 0;
            }

            OptionSetValue value = entity.GetAttributeValue<OptionSetValue>(fieldName);
            return value?.Value ?? 0;
        }
    }
}
