using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace DeployTool
{
    public class RetriggerPlugin
    {
        public static void Retrigger(ServiceClient service)
        {
            Console.WriteLine(">>> 重新触发 CofaceDataSyncPlugin...");

            // 查询所有状态=11且countryCode=PL的记录
            var query = new QueryExpression("mcs_credit_record")
            {
                ColumnSet = new ColumnSet("mcs_cofaceid", "mcs_countrycode"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_status", ConditionOperator.Equal, 11),
                        new ConditionExpression("mcs_countrycode", ConditionOperator.Equal, "PL"),
                        new ConditionExpression("mcs_cofaceid", ConditionOperator.Equal, "icon#5415240")
                    }
                }
            };

            var results = service.RetrieveMultiple(query);
            Console.WriteLine($"  找到 {results.Entities.Count} 条需要重新触发的记录");

            foreach (var record in results.Entities)
            {
                // 通过更新状态字段重新触发 Plugin
                var updateRecord = new Entity("mcs_credit_record")
                {
                    Id = record.Id
                };
                updateRecord["mcs_status"] = new OptionSetValue(11);
                service.Update(updateRecord);
                Console.WriteLine($"  ✅ 已重新触发记录: {record.Id}");
            }
        }
    }
}
