using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using System;

namespace DeployTool
{
    public class TestBppWithFeishu
    {
        public static void Test(ServiceClient service, Guid recordId)
        {
            Console.WriteLine(">>> 测试BPP审批（使用飞书账号gw_qiuzw）...");
            
            // 先把状态设为14
            Console.WriteLine("  设置状态=14...");
            try
            {
                var setStatus = new Entity("mcs_credit_record") { Id = recordId };
                setStatus["mcs_status"] = new OptionSetValue(14);
                service.Update(setStatus);
                Console.WriteLine("  ✅ 状态已设为14");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ 设状态失败: {ex.Message}");
                return;
            }
            
            // 等待Plugin执行
            Console.WriteLine("  等待3秒让BppIntegrationPlugin执行...");
            System.Threading.Thread.Sleep(3000);
            
            // 查询结果
            Console.WriteLine("  查询执行结果...");
            try
            {
                var record = service.Retrieve("mcs_credit_record", recordId,
                    new Microsoft.Xrm.Sdk.Query.ColumnSet("mcs_status", "mcs_bppid", "mcs_bppstatus", "mcs_bpperrormsg", "mcs_workflowid", "mcs_nextapprover"));
                
                var status = record.GetAttributeValue<OptionSetValue>("mcs_status")?.Value;
                var bppId = record.GetAttributeValue<string>("mcs_bppid");
                var bppStatus = record.GetAttributeValue<string>("mcs_bppstatus");
                var bppError = record.GetAttributeValue<string>("mcs_bpperrormsg");
                var workflowId = record.GetAttributeValue<string>("mcs_workflowid");
                var nextApprover = record.GetAttributeValue<string>("mcs_nextapprover");
                
                Console.WriteLine($"  状态: {status} (14=审核申请)");
                Console.WriteLine($"  BPP ID: {bppId}");
                Console.WriteLine($"  BPP Status: {bppStatus}");
                Console.WriteLine($"  BPP Error: {bppError}");
                Console.WriteLine($"  Workflow ID: {workflowId}");
                Console.WriteLine($"  Next Approver: {nextApprover}");
                
                if (status == 14 && !string.IsNullOrEmpty(bppId))
                {
                    Console.WriteLine("  ✅ BPP审批发起成功！");
                }
                else if (!string.IsNullOrEmpty(bppError))
                {
                    Console.WriteLine($"  ❌ BPP审批失败: {bppError}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ 查询失败: {ex.Message}");
            }
            
            // 查看Plugin Trace
            Console.WriteLine("  查看最新BppIntegrationPlugin Trace...");
            try
            {
                var query = new Microsoft.Xrm.Sdk.Query.QueryExpression("plugintracelog")
                {
                    ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("createdon", "messageblock", "exceptiondetails"),
                    Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression
                    {
                        Conditions =
                        {
                            new Microsoft.Xrm.Sdk.Query.ConditionExpression("messageblock", Microsoft.Xrm.Sdk.Query.ConditionOperator.Like, "%BppIntegrationPlugin%")
                        }
                    },
                    Orders = { new Microsoft.Xrm.Sdk.Query.OrderExpression("createdon", Microsoft.Xrm.Sdk.Query.OrderType.Descending) },
                    TopCount = 3
                };
                var traces = service.RetrieveMultiple(query);
                foreach (var trace in traces.Entities)
                {
                    var msg = trace.GetAttributeValue<string>("messageblock") ?? "";
                    var exc = trace.GetAttributeValue<string>("exceptiondetails") ?? "";
                    if (!string.IsNullOrEmpty(msg))
                    {
                        var shortMsg = msg.Length > 200 ? msg.Substring(0, 200) + "..." : msg;
                        Console.WriteLine($"    [{trace.GetAttributeValue<DateTime>("createdon"):HH:mm:ss}] {shortMsg.Replace("\n", " ")}");
                    }
                    if (!string.IsNullOrEmpty(exc))
                    {
                        var shortExc = exc.Length > 200 ? exc.Substring(0, 200) + "..." : exc;
                        Console.WriteLine($"    Exc: {shortExc.Replace("\n", " ")}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    查询Trace失败: {ex.Message}");
            }
        }
    }
}
