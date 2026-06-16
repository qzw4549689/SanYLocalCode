using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace DeployTool
{
    public class UpdateAccountCountry
    {
        public static void Update(ServiceClient service)
        {
            Console.WriteLine(">>> 更新客户 mcs_country 字段...");

            // 1. 查询 Poland 对应的国家记录
            var countryQuery = new QueryExpression("mcs_country")
            {
                ColumnSet = new ColumnSet("mcs_countryid", "mcs_name"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_name", ConditionOperator.Equal, "Poland")
                    }
                }
            };

            var countryResults = service.RetrieveMultiple(countryQuery);
            if (countryResults.Entities.Count == 0)
            {
                Console.WriteLine("  未找到 Poland 国家记录");
                return;
            }

            var polandCountry = countryResults.Entities[0];
            Guid polandId = polandCountry.Id;
            Console.WriteLine($"  找到 Poland 国家记录: {polandId}");

            // 2. 查询客户 LTC客户-1
            var accountQuery = new QueryExpression("account")
            {
                ColumnSet = new ColumnSet("accountid", "name", "mcs_country"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("name", ConditionOperator.Like, "%LTC客户-1%")
                    }
                }
            };

            var accountResults = service.RetrieveMultiple(accountQuery);
            Console.WriteLine($"  找到 {accountResults.Entities.Count} 条客户记录");

            foreach (var record in accountResults.Entities)
            {
                Console.WriteLine($"  客户 ID: {record.Id}");
                Console.WriteLine($"  客户名称: {record.GetAttributeValue<string>("name")}");
                
                var currentCountry = record.GetAttributeValue<EntityReference>("mcs_country");
                Console.WriteLine($"  当前 mcs_country: {currentCountry?.Id}");
                
                var updateRecord = new Entity("account")
                {
                    Id = record.Id
                };
                updateRecord["mcs_country"] = new EntityReference("mcs_country", polandId);
                service.Update(updateRecord);
                Console.WriteLine($"  ✅ 已更新 mcs_country 为 Poland");
            }
        }
    }
}
