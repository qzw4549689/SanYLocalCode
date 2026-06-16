using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace DeployTool
{
    public class ViewBppPluginTrace
    {
        public static void View(ServiceClient service)
        {
            Console.WriteLine(">>> 查看 BppIntegrationPlugin Trace 日志...");
            try
            {
                var query = new QueryExpression("plugintracelog")
                {
                    ColumnSet = new ColumnSet("createdon", "messageblock", "exceptiondetails"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("messageblock", ConditionOperator.Like, "%BppIntegrationPlugin%")
                        }
                    },
                    Orders = { new OrderExpression("createdon", OrderType.Descending) },
                    TopCount = 10
                };
                var traces = service.RetrieveMultiple(query);
                Console.WriteLine($"  找到 {traces.Entities.Count} 条记录");
                Console.WriteLine();
                foreach (var trace in traces.Entities)
                {
                    var time = trace.GetAttributeValue<DateTime>("createdon");
                    var msg = trace.GetAttributeValue<string>("messageblock") ?? "";
                    var exc = trace.GetAttributeValue<string>("exceptiondetails") ?? "";
                    Console.WriteLine($"  [{time:yyyy/MM/dd HH:mm:ss}]");
                    
                    // 打印完整消息
                    if (!string.IsNullOrEmpty(msg))
                    {
                        Console.WriteLine("  --- Message ---");
                        foreach (var line in msg.Split('\n'))
                        {
                            if (!string.IsNullOrWhiteSpace(line))
                                Console.WriteLine($"    {line.Trim()}");
                        }
                    }
                    
                    // 打印完整异常
                    if (!string.IsNullOrEmpty(exc))
                    {
                        Console.WriteLine("  --- Exception ---");
                        foreach (var line in exc.Split('\n'))
                        {
                            if (!string.IsNullOrWhiteSpace(line))
                                Console.WriteLine($"    {line.Trim()}");
                        }
                    }
                    Console.WriteLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ 查询失败: {ex.Message}");
            }
        }
    }
}
