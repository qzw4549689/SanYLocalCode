using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace DeployTool
{
    public class DiagnoseCreditScore
    {
        public static void Diagnose(ServiceClient service)
        {
            Console.WriteLine(">>> 诊断信用分计算问题...");

            // 1. 检查 mcs_credit_record 最新记录的状态
            var recordQuery = new QueryExpression("mcs_credit_record")
            {
                ColumnSet = new ColumnSet("mcs_status", "mcs_scoreid", "mcs_accountid"),
                Orders = { new OrderExpression("modifiedon", OrderType.Descending) },
                TopCount = 1
            };

            var records = service.RetrieveMultiple(recordQuery);
            if (records.Entities.Count == 0)
            {
                Console.WriteLine("  未找到评估记录");
                return;
            }

            var record = records.Entities[0];
            var recordId = record.Id;
            var status = record.GetAttributeValue<OptionSetValue>("mcs_status")?.Value;
            var accountRef = record.GetAttributeValue<EntityReference>("mcs_accountid");
            
            Console.WriteLine($"  记录ID: {recordId}");
            Console.WriteLine($"  状态: {status}");
            Console.WriteLine($"  客户: {accountRef?.Id}");

            // 2. 检查客户属性
            if (accountRef != null)
            {
                var account = service.Retrieve("account", accountRef.Id,
                    new ColumnSet("mcs_accountcategory", "mcs_accountlevel", "mcs_accounttype"));
                
                Console.WriteLine($"  客户类别: {account.GetAttributeValue<OptionSetValue>("mcs_accountcategory")?.Value}");
                Console.WriteLine($"  客户级别: {account.GetAttributeValue<OptionSetValue>("mcs_accountlevel")?.Value}");
                Console.WriteLine($"  客户类型: {account.GetAttributeValue<OptionSetValue>("mcs_accounttype")?.Value}");
            }

            // 3. 检查标签记录
            var tagQuery = new QueryExpression("mcs_customer_tag")
            {
                ColumnSet = new ColumnSet("mcs_itemid", "mcs_datatype", "mcs_itemintvalue2", "mcs_itemtxtvalue2", "mcs_itemvalue2"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_credit_record", ConditionOperator.Equal, recordId)
                    }
                }
            };

            var tags = service.RetrieveMultiple(tagQuery);
            Console.WriteLine($"  标签记录数: {tags.Entities.Count}");

            foreach (var tag in tags.Entities)
            {
                var itemId = tag["mcs_itemid"];
                var dataType = tag.GetAttributeValue<OptionSetValue>("mcs_datatype")?.Value;
                
                Console.WriteLine($"    标签ID: {tag.Id}");
                Console.WriteLine($"    mcs_itemid类型: {itemId?.GetType()?.Name ?? "null"}");
                
                if (itemId is EntityReference er)
                {
                    Console.WriteLine($"    mcs_itemid: {er.Id} ({er.LogicalName})");
                }
                else if (itemId != null)
                {
                    Console.WriteLine($"    mcs_itemid值: {itemId}");
                }
                
                Console.WriteLine($"    数据类型: {dataType}");
                Console.WriteLine($"    定量值: {tag.GetAttributeValue<decimal>("mcs_itemintvalue2")}");
                Console.WriteLine($"    定性值: {tag.GetAttributeValue<string>("mcs_itemtxtvalue2")}");
                Console.WriteLine();
            }

            // 4. 检查评分卡配置
            var scoringQuery = new QueryExpression("mcs_credit_scoringcard")
            {
                ColumnSet = new ColumnSet("mcs_itemid", "mcs_datatype", "mcs_weight"),
                TopCount = 3
            };

            var scoringCards = service.RetrieveMultiple(scoringQuery);
            Console.WriteLine($"  评分卡配置数: {scoringCards.Entities.Count}");

            foreach (var sc in scoringCards.Entities)
            {
                var itemId = sc["mcs_itemid"];
                Console.WriteLine($"    配置ID: {sc.Id}");
                Console.WriteLine($"    mcs_itemid类型: {itemId?.GetType()?.Name ?? "null"}");
                
                if (itemId is EntityReference er)
                {
                    Console.WriteLine($"    mcs_itemid: {er.Id} ({er.LogicalName})");
                }
                else if (itemId != null)
                {
                    Console.WriteLine($"    mcs_itemid值: {itemId}");
                }
                Console.WriteLine();
            }
        }
    }
}
