using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace DeployTool
{
    public class ViewCreditScoreTrace
    {
        public static void View(ServiceClient service)
        {
            Console.WriteLine(">>> 查看 CreditScorePlugin Trace 日志...");

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
                TopCount = 10
            };

            var results = service.RetrieveMultiple(query);
            Console.WriteLine($"  找到 {results.Entities.Count} 条记录");

            foreach (var log in results.Entities)
            {
                var createdOn = log.GetAttributeValue<DateTime>("createdon");
                var message = log.GetAttributeValue<string>("messagename");
                var exception = log.GetAttributeValue<string>("exceptiondetails");
                var traceLog = log.GetAttributeValue<string>("messageblock");

                Console.WriteLine($"\n  [{createdOn}] {message}");
                
                if (!string.IsNullOrEmpty(exception))
                {
                    Console.WriteLine($"  ❌ 异常: {exception.Substring(0, Math.Min(500, exception.Length))}");
                }
                
                if (!string.IsNullOrEmpty(traceLog))
                {
                    var lines = traceLog.Split('\n');
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (trimmed.Contains("CreditScore") || trimmed.Contains("步骤") || trimmed.Contains("异常") || trimmed.Contains("类型") || trimmed.Contains("GetAttribute"))
                        {
                            Console.WriteLine($"    {trimmed}");
                        }
                    }
                }
            }
        }
    }
}
