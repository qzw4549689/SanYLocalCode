using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace SanyD365.Plugins.BppIntegration.Plugin
{
    /// <summary>
    /// BPP审批流程集成Plugin
    /// 触发条件: mcs_credit_record Update/PostOperation, mcs_status=14
    /// </summary>
    public class BppIntegrationPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var service = serviceFactory.CreateOrganizationService(context.UserId);
            var tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            try
            {
                tracer.Trace("=== BppIntegrationPlugin Execute ===");
                tracer.Trace($"Message: {context.MessageName}, Stage: {context.Stage}, Depth: {context.Depth}");

                // 防递归
                if (context.Depth > 2)
                {
                    tracer.Trace("递归深度超过2，跳过处理");
                    return;
                }

                // 获取Target
                if (!context.InputParameters.Contains("Target") || !(context.InputParameters["Target"] is Entity target))
                {
                    tracer.Trace("未找到Target参数");
                    return;
                }

                // 严格校验实体名
                if (!target.LogicalName.Equals("mcs_credit_record", StringComparison.OrdinalIgnoreCase))
                {
                    tracer.Trace($"非信用评估记录，跳过: {target.LogicalName}");
                    return;
                }

                // 只处理状态字段变更
                if (!target.Contains("mcs_status"))
                {
                    tracer.Trace("状态字段未变更，跳过");
                    return;
                }

                var currentStatus = target.GetAttributeValue<OptionSetValue>("mcs_status")?.Value ?? 0;
                tracer.Trace($"当前状态: {currentStatus}");

                // 只有状态=14时才触发BPP
                if (currentStatus != 14)
                {
                    tracer.Trace($"当前状态不是14({currentStatus})，跳过BPP处理");
                    return;
                }

                // 查询现有BPP信息，防重复提交
                var creditRecord = service.Retrieve("mcs_credit_record", target.Id,
                    new ColumnSet("mcs_workflowid", "mcs_bppstatus"));
                string existingWorkflowId = creditRecord.GetAttributeValue<string>("mcs_workflowid");
                string existingBppStatus = creditRecord.GetAttributeValue<string>("mcs_bppstatus");

                tracer.Trace($"现有 workflowId: {existingWorkflowId}, bppStatus: {existingBppStatus}");

                if (!string.IsNullOrEmpty(existingWorkflowId) &&
                    !string.IsNullOrEmpty(existingBppStatus) &&
                    existingBppStatus != "SubmitFailed")
                {
                    tracer.Trace("流程已在审批中或已完成，跳过重复提交");
                    return;
                }

                // 调用 mcs_bppstartapi
                var request = new OrganizationRequest("mcs_bppstartapi");
                request["EntityId"] = target.Id.ToString();
                request["EntityName"] = "mcs_credit_record";
                request["UserId"] = context.InitiatingUserId.ToString();

                tracer.Trace($"调用 mcs_bppstartapi: EntityId={target.Id}, UserId={context.InitiatingUserId}");

                var response = service.Execute(request);
                string result = response["Result"]?.ToString() ?? "";
                tracer.Trace($"mcs_bppstartapi 返回: {result}");

                // 解析返回结果
                bool success = !string.IsNullOrEmpty(result) &&
                               (result.Contains("true") || result.Contains("success") || result.Contains("Success"));

                if (success)
                {
                    // V1消息队列异步执行，同步调用只返回成功入队
                    // 更新状态为Submitted，表示已提交到BPP处理队列
                    var updateRecord = new Entity("mcs_credit_record") { Id = target.Id };
                    updateRecord["mcs_bppstatus"] = "Submitted";
                    updateRecord["mcs_bpperrormsg"] = null;
                    service.Update(updateRecord);
                    tracer.Trace("BPP提交成功，状态更新为Submitted");
                }
                else
                {
                    tracer.Trace($"BPP发起失败: {result}");
                    throw new InvalidPluginExecutionException($"BPP审批发起失败: {result}");
                }
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                tracer.Trace($"BppIntegrationPlugin异常: {ex.Message}");
                throw new InvalidPluginExecutionException($"BPP集成处理失败: {ex.Message}");
            }
        }
    }
}
