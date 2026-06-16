using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace DeployTool
{
    public class FixCountryCode
    {
        public static void Fix(ServiceClient service)
        {
            Console.WriteLine(">>> 修复记录 countryCode...");

            // 查询所有状态=11且countryCode=CN的记录
            var query = new QueryExpression("mcs_credit_record")
            {
                ColumnSet = new ColumnSet("mcs_cofaceid", "mcs_countrycode"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_status", ConditionOperator.Equal, 11),
                        new ConditionExpression("mcs_countrycode", ConditionOperator.Equal, "CN"),
                        new ConditionExpression("mcs_cofaceid", ConditionOperator.Equal, "icon#5415240")
                    }
                }
            };

            var results = service.RetrieveMultiple(query);
            Console.WriteLine($"  找到 {results.Entities.Count} 条需要修复的记录");

            foreach (var record in results.Entities)
            {
                var updateRecord = new Entity("mcs_credit_record")
                {
                    Id = record.Id
                };
                updateRecord["mcs_countrycode"] = "PL";
                service.Update(updateRecord);
                Console.WriteLine($"  ✅ 已修复记录: {record.Id}");
            }
        }
    }
}
