using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace DeployTool
{
    public class FindAccountFields
    {
        public static void Find(ServiceClient service)
        {
            Console.WriteLine(">>> 查找 account 实体的国家相关字段...");

            // 按名称查询客户
            var query = new QueryExpression("account")
            {
                ColumnSet = new ColumnSet("accountid", "name", "accountnumber"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("name", ConditionOperator.Like, "%LTC客户-1%")
                    }
                }
            };

            var results = service.RetrieveMultiple(query);
            if (results.Entities.Count == 0)
            {
                Console.WriteLine("  未找到客户记录");
                return;
            }

            var record = results.Entities[0];
            Console.WriteLine($"  客户名称: {record.GetAttributeValue<string>("name")}");
            Console.WriteLine($"  客户编号: {record.GetAttributeValue<string>("accountnumber")}");
            Console.WriteLine($"  客户 ID: {record.Id}");

            // 获取所有字段
            var allColumns = new ColumnSet(true);
            var fullRecord = service.Retrieve("account", record.Id, allColumns);
            
            Console.WriteLine("\n  所有字段值:");
            foreach (var attr in fullRecord.Attributes.OrderBy(a => a.Key))
            {
                string valueStr = "(null)";
                if (attr.Value != null)
                {
                    if (attr.Value is OptionSetValue osv)
                        valueStr = osv.Value.ToString();
                    else if (attr.Value is EntityReference er)
                        valueStr = $"{er.LogicalName}({er.Id})";
                    else if (attr.Value is Money money)
                        valueStr = money.Value.ToString();
                    else
                        valueStr = attr.Value.ToString();
                }
                
                // 只显示可能与国家相关的字段
                string key = attr.Key.ToLower();
                if (key.Contains("country") || key.Contains("region") || key.Contains("nation") || 
                    key.Contains("state") || key.Contains("province") || key.Contains("address") ||
                    key.Contains("mcs_country") || key.Contains("registered"))
                {
                    Console.WriteLine($"    {attr.Key}: {valueStr}");
                }
            }
        }
    }
}
