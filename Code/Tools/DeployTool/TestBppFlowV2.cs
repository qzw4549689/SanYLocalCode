using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Threading;

namespace DeployTool
{
    /// <summary>
    /// 测试BPP流程V2：验证异步BPP流程
    /// </summary>
    public class TestBppFlowV2
    {
        public static void Test(ServiceClient service)
        {
            Console.WriteLine(">>> 测试BPP流程V2...");

            Guid recordId = new Guid("b7e04e27-0974-4fec-8994-7c409c83b620");

            try
            {
                // 1. 检查当前记录状态
                var record = service.Retrieve("mcs_credit_record", recordId,
                    new ColumnSet("mcs_status", "mcs_workflowid", "mcs_bppstatus", "mcs_bpperrormsg", "mcs_nextapprover"));
                int status = record.GetAttributeValue<OptionSetValue>("mcs_status")?.Value ?? 0;
                string workflowId = record.GetAttributeValue<string>("mcs_workflowid");
                string bppStatus = record.GetAttributeValue<string>("mcs_bppstatus");
                string errorMsg = record.GetAttributeValue<string>("mcs_bpperrormsg");
                string nextApprover = record.GetAttributeValue<string>("mcs_nextapprover");

                Console.WriteLine($"  当前状态: {status}");
                Console.WriteLine($"  workflowId: {workflowId}");
                Console.WriteLine($"  bppStatus: {bppStatus}");
                Console.WriteLine($"  nextApprover: {nextApprover}");
                Console.WriteLine($"  错误信息: {errorMsg}");

                // 2. 重置状态到13，清除之前的BPP数据
                Console.WriteLine("  重置记录状态到13...");
                var resetRecord = new Entity("mcs_credit_record") { Id = recordId };
                resetRecord["mcs_status"] = new OptionSetValue(13);
                resetRecord["mcs_workflowid"] = null;
                resetRecord["mcs_bppstatus"] = null;
                resetRecord["mcs_bpperrormsg"] = null;
                resetRecord["mcs_nextapprover"] = null;
                service.Update(resetRecord);
                Console.WriteLine("  ✅ 状态已重置到13");

                // 3. 更新状态到14触发BppIntegrationPlugin
                Console.WriteLine("  更新状态到14，触发BPP Plugin...");
                var triggerRecord = new Entity("mcs_credit_record") { Id = recordId };
                triggerRecord["mcs_status"] = new OptionSetValue(14);
                service.Update(triggerRecord);
                Console.WriteLine("  ✅ 状态已更新到14");

                // 4. 等待异步处理完成，多次查询
                Console.WriteLine("  等待异步BPP处理...");
                for (int i = 0; i < 6; i++)
                {
                    Thread.Sleep(5000); // 每5秒查一次，共30秒
                    var checkRecord = service.Retrieve("mcs_credit_record", recordId,
                        new ColumnSet("mcs_status", "mcs_workflowid", "mcs_bppstatus", "mcs_bpperrormsg", "mcs_nextapprover"));
                    int checkStatus = checkRecord.GetAttributeValue<OptionSetValue>("mcs_status")?.Value ?? 0;
                    string checkWorkflowId = checkRecord.GetAttributeValue<string>("mcs_workflowid");
                    string checkBppStatus = checkRecord.GetAttributeValue<string>("mcs_bppstatus");
                    string checkError = checkRecord.GetAttributeValue<string>("mcs_bpperrormsg");
                    
                    Console.WriteLine($"  [{i+1}/6] 状态={checkStatus}, workflowId={checkWorkflowId}, bppStatus={checkBppStatus}");
                    
                    // 如果已经拿到workflowId，说明异步处理完成
                    if (!string.IsNullOrEmpty(checkWorkflowId))
                    {
                        Console.WriteLine($"  ✅ BPP流程发起成功！workflowId={checkWorkflowId}");
                        Console.WriteLine($"    审批地址: https://sanybpp-portal-uat.sany.com.cn/approval-form?instanceId={checkWorkflowId}&orgId=3");
                        break;
                    }
                    
                    // 如果状态回滚到13且有错误，说明失败了
                    if (checkStatus == 13 && !string.IsNullOrEmpty(checkError))
                    {
                        Console.WriteLine($"  ❌ BPP发起失败: {checkError}");
                        break;
                    }
                }

                // 5. 查看最新的Plugin Trace
                Console.WriteLine("  查看Plugin Trace日志...");
                var traceQuery = new QueryExpression("plugintracelog")
                {
                    ColumnSet = new ColumnSet("messagename", "primaryentity", "createdon", "messageblock", "exceptiondetails"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("primaryentity", ConditionOperator.Equal, "mcs_credit_record"),
                            new ConditionExpression("createdon", ConditionOperator.GreaterThan, DateTime.Now.AddMinutes(-10))
                        }
                    },
                    Orders = { new OrderExpression("createdon", OrderType.Descending) },
                    TopCount = 5
                };
                var traces = service.RetrieveMultiple(traceQuery);
                foreach (var t in traces.Entities)
                {
                    string msg = t.GetAttributeValue<string>("messagename");
                    string traceMsg = t.GetAttributeValue<string>("messageblock");
                    string exc = t.GetAttributeValue<string>("exceptiondetails");
                    DateTime created = t.GetAttributeValue<DateTime>("createdon");
                    
                    Console.WriteLine($"  [{created:HH:mm:ss}] {msg}");
                    if (!string.IsNullOrEmpty(traceMsg))
                    {
                        // 只显示trace的最后200字符
                        string shortTrace = traceMsg.Length > 200 ? "..." + traceMsg.Substring(traceMsg.Length - 200) : traceMsg;
                        Console.WriteLine($"    Trace: {shortTrace.Replace("\n", " | ")}");
                    }
                    if (!string.IsNullOrEmpty(exc))
                    {
                        Console.WriteLine($"    异常: {exc.Substring(0, Math.Min(300, exc.Length))}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ 测试失败: {ex.Message}");
            }
        }
    }
}
