using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace DeployTool
{
    public class CheckPluginTrace
    {
        public static void Check(ServiceClient service)
        {
            Console.WriteLine(">>> 检查 Plugin Trace 日志...");

            var query = new QueryExpression("plugintracelog")
            {
                ColumnSet = new ColumnSet("createdon", "messagename", "primaryentity", "exceptiondetails", "messageblock"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("primaryentity", ConditionOperator.Equal, "mcs_credit_record")
                    }
                },
                Orders = { new OrderExpression("createdon", OrderType.Descending) },
                TopCount = 5
            };

            var results = service.RetrieveMultiple(query);
            Console.WriteLine($"  找到 {results.Entities.Count} 条 Plugin Trace 记录");

            foreach (var log in results.Entities)
            {
                var createdOn = log.GetAttributeValue<DateTime>("createdon");
                var message = log.GetAttributeValue<string>("messagename");
                var exception = log.GetAttributeValue<string>("exceptiondetails");
                var traceLog = log.GetAttributeValue<string>("messageblock");

                Console.WriteLine($"\n  [{createdOn}] {message}");
                if (!string.IsNullOrEmpty(exception))
                {
                    Console.WriteLine($"  ❌ 异常: {exception.Substring(0, Math.Min(200, exception.Length))}");
                }
                if (!string.IsNullOrEmpty(traceLog))
                {
                    var lines = traceLog.Split('\n');
                    foreach (var line in lines)
                    {
                        if (line.Contains("Coface") || line.Contains("URBA") || line.Contains("Report") || line.Contains("ERROR") || line.Contains("失败") || line.Contains("订单") || line.Contains("order") || line.Contains("cofaceId") || line.Contains("country") || line.Contains("externalId") || line.Contains("HTTP") || line.Contains("JSON") || line.Contains("状态"))
                        {
                            Console.WriteLine($"    {line.Trim()}");
                        }
                    }
                }
            }
        }
    }
}
