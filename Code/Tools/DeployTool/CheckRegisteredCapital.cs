using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace DeployTool
{
    public class CheckRegisteredCapital
    {
        public static void Check(ServiceClient service)
        {
            Console.WriteLine(">>> 检查注册资本相关字段...");

            // 1. 检查 mcs_credit_record 中是否有注册资本字段
            Console.WriteLine("\n  1. mcs_credit_record 实体:");
            var recordQuery = new QueryExpression("mcs_credit_record")
            {
                ColumnSet = new ColumnSet(true),
                TopCount = 1
            };
            var recordResults = service.RetrieveMultiple(recordQuery);
            if (recordResults.Entities.Count > 0)
            {
                var record = recordResults.Entities[0];
                var capitalFields = record.Attributes.Keys
                    .Where(k => k.ToLower().Contains("capital") || k.ToLower().Contains("register"))
                    .ToList();
                
                if (capitalFields.Count == 0)
                {
                    Console.WriteLine("     没有注册资本相关字段");
                }
                else
                {
                    foreach (var f in capitalFields)
                    {
                        Console.WriteLine($"     {f}: {record[f]}");
                    }
                }
            }

            // 2. 检查 mcs_customer_tag 中是否有注册资本标签
            Console.WriteLine("\n  2. mcs_customer_tag 实体 - 查找 RegisteredCapital 标签:");
            var tagQuery = new QueryExpression("mcs_customer_tag")
            {
                ColumnSet = new ColumnSet("mcs_itemcode", "mcs_itemname", "mcs_itemvalue1", "mcs_itemintvalue1", "mcs_datatype"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_itemcode", ConditionOperator.Equal, "RegisteredCapital")
                    }
                },
                TopCount = 3
            };

            var tagResults = service.RetrieveMultiple(tagQuery);
            Console.WriteLine($"     找到 {tagResults.Entities.Count} 条 RegisteredCapital 标签记录");
            
            foreach (var tag in tagResults.Entities)
            {
                Console.WriteLine($"     标签: {tag.GetAttributeValue<string>("mcs_itemname")}");
                Console.WriteLine($"     编码: {tag.GetAttributeValue<string>("mcs_itemcode")}");
                Console.WriteLine($"     数据类型: {tag.GetAttributeValue<OptionSetValue>("mcs_datatype")?.Value}");
                Console.WriteLine($"     文本值: {tag.GetAttributeValue<string>("mcs_itemvalue1")}");
                Console.WriteLine($"     数值: {tag.GetAttributeValue<decimal>("mcs_itemintvalue1")}");
                Console.WriteLine();
            }

            // 3. 检查评分项目中是否有注册资本
            Console.WriteLine("\n  3. mcs_credit_items 评分项目:");
            var itemQuery = new QueryExpression("mcs_credit_items")
            {
                ColumnSet = new ColumnSet("mcs_credit_itemsno", "mcs_itemname", "mcs_group", "mcs_datatype"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_credit_itemsno", ConditionOperator.Equal, "RegisteredCapital")
                    }
                }
            };

            var itemResults = service.RetrieveMultiple(itemQuery);
            if (itemResults.Entities.Count > 0)
            {
                var item = itemResults.Entities[0];
                Console.WriteLine($"     项目编码: {item.GetAttributeValue<string>("mcs_credit_itemsno")}");
                Console.WriteLine($"     项目名称: {item.GetAttributeValue<string>("mcs_itemname")}");
                Console.WriteLine($"     数据类型: {item.GetAttributeValue<OptionSetValue>("mcs_datatype")?.Value}");
                Console.WriteLine($"     分组: {item.GetAttributeValue<OptionSetValue>("mcs_group")?.Value}");
            }
            else
            {
                Console.WriteLine("     未找到 RegisteredCapital 评分项目");
            }
        }
    }
}
