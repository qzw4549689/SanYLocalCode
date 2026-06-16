using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Threading;

namespace DeployTool
{
    public class TestBppIntegrationFlow
    {
        public static void Run(ServiceClient service, Guid recordId)
        {
            Console.WriteLine(">>> 测试BPP审批流程...");
            
            // 步骤1: 状态12→13（触发CreditScorePlugin）
            Console.WriteLine("  步骤1: 设置状态=13（信用分计算）...");
            try
            {
                var update13 = new Entity("mcs_credit_record") { Id = recordId };
                update13["mcs_status"] = new OptionSetValue(13);
                service.Update(update13);
                Console.WriteLine("  ✅ 状态已设为13");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ 失败: {ex.Message}");
                return;
            }
            
            // 等待Plugin执行完成
            Console.WriteLine("  等待2秒让CreditScorePlugin执行...");
            Thread.Sleep(2000);
            
            // 检查13的状态结果
            try
            {
                var record13 = service.Retrieve("mcs_credit_record", recordId, 
                    new ColumnSet("mcs_status", "mcs_creditscore", "mcs_checkdate"));
                var status13 = record13.GetAttributeValue<OptionSetValue>("mcs_status")?.Value;
                var score = record13.GetAttributeValue<decimal?>("mcs_creditscore");
                var checkDate = record13.GetAttributeValue<DateTime?>("mcs_checkdate");
                Console.WriteLine($"  状态13结果: status={status13}, score={score}, checkDate={checkDate}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠️ 查询失败: {ex.Message}");
            }
            
            // 步骤2: 状态13→14（触发BppIntegrationPlugin）
            Console.WriteLine("  步骤2: 设置状态=14（审核申请）...");
            try
            {
                var update14 = new Entity("mcs_credit_record") { Id = recordId };
                update14["mcs_status"] = new OptionSetValue(14);
                service.Update(update14);
                Console.WriteLine("  ✅ 状态已设为14");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ 失败: {ex.Message}");
                return;
            }
            
            // 等待Plugin执行完成
            Console.WriteLine("  等待3秒让BppIntegrationPlugin执行...");
            Thread.Sleep(3000);
            
            // 检查14的状态结果
            try
            {
                var record14 = service.Retrieve("mcs_credit_record", recordId, 
                    new ColumnSet("mcs_status", "mcs_bppid", "mcs_bppstatus", "mcs_bpperrormsg", "mcs_bppappriver", "mcs_creditscore"));
                var status14 = record14.GetAttributeValue<OptionSetValue>("mcs_status")?.Value;
                var bppId = record14.GetAttributeValue<string>("mcs_bppid");
                var bppStatus = record14.GetAttributeValue<string>("mcs_bppstatus");
                var bppError = record14.GetAttributeValue<string>("mcs_bpperrormsg");
                var bppAppriver = record14.GetAttributeValue<object>("mcs_bppappriver")?.ToString();
                var score = record14.GetAttributeValue<decimal?>("mcs_creditscore");
                
                Console.WriteLine($"  状态14结果:");
                Console.WriteLine($"    status={status14}");
                Console.WriteLine($"    creditscore={score}");
                Console.WriteLine($"    bppId={bppId}");
                Console.WriteLine($"    bppStatus={bppStatus}");
                Console.WriteLine($"    bppError={bppError}");
                Console.WriteLine($"    bppAppriver={bppAppriver}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠️ 查询失败: {ex.Message}");
            }
            
            // 步骤3: 查看Plugin Trace日志
            Console.WriteLine("  步骤3: 查看BppIntegrationPlugin Trace日志...");
            try
            {
                var traceQuery = new Microsoft.Xrm.Sdk.Query.QueryExpression("plugintracelog")
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
                    TopCount = 5
                };
                var traces = service.RetrieveMultiple(traceQuery);
                foreach (var trace in traces.Entities)
                {
                    var msg = trace.GetAttributeValue<string>("messageblock") ?? "";
                    var exc = trace.GetAttributeValue<string>("exceptiondetails") ?? "";
                    Console.WriteLine($"    [{trace.GetAttributeValue<DateTime>("createdon")}]");
                    if (!string.IsNullOrEmpty(msg))
                    {
                        var shortMsg = msg.Length > 300 ? msg.Substring(0, 300) + "..." : msg;
                        Console.WriteLine($"      Msg: {shortMsg.Replace("\n", " ")}");
                    }
                    if (!string.IsNullOrEmpty(exc))
                    {
                        var shortExc = exc.Length > 300 ? exc.Substring(0, 300) + "..." : exc;
                        Console.WriteLine($"      Exc: {shortExc.Replace("\n", " ")}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    ❌ 查询失败: {ex.Message}");
            }
            
            Console.WriteLine("  测试完成。");
        }
    }
}
