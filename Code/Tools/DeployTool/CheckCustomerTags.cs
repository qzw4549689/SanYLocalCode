using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace DeployTool
{
    public class CheckCustomerTags
    {
        public static void Check(ServiceClient service)
        {
            Console.WriteLine(">>> 检查客户标签数据...");

            var creditRecordId = new Guid("b7e04e27-0974-4fec-8994-7c409c83b620");

            try
            {
                // 查询标签记录
                var query = new QueryExpression("mcs_customer_tag")
                {
                    ColumnSet = new ColumnSet("mcs_credit_item", "mcs_datatype", "mcs_itemintvalue2", "mcs_itemtxtvalue2", "mcs_itemvalue2"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("mcs_credit_record", ConditionOperator.Equal, creditRecordId),
                            new ConditionExpression("mcs_active", ConditionOperator.Equal, true)
                        }
                    }
                };

                var link = new LinkEntity("mcs_customer_tag", "mcs_credit_items", "mcs_credit_item", "mcs_credit_itemsid", JoinOperator.LeftOuter)
                {
                    Columns = new ColumnSet("mcs_credit_itemsno"),
                    EntityAlias = "item"
                };
                query.LinkEntities.Add(link);

                var records = service.RetrieveMultiple(query);
                Console.WriteLine($"  找到 {records.Entities.Count} 条标签记录");

                foreach (var record in records.Entities)
                {
                    Console.WriteLine($"\n  --- 标签记录 ID: {record.Id} ---");
                    
                    // 检查所有属性
                    foreach (var attr in record.Attributes)
                    {
                        Console.WriteLine($"    {attr.Key}: {attr.Value?.GetType()?.Name} = {attr.Value}");
                    }

                    // 尝试获取 itemCode
                    string itemCode = "";
                    if (record.Contains("item.mcs_credit_itemsno"))
                    {
                        var aliased = record.GetAttributeValue<AliasedValue>("item.mcs_credit_itemsno");
                        Console.WriteLine($"    [LinkEntity] AliasedValue.Value = {aliased?.Value} (类型: {aliased?.Value?.GetType()?.Name})");
                        if (aliased?.Value != null)
                            itemCode = aliased.Value.ToString();
                    }

                    if (string.IsNullOrEmpty(itemCode) && record.Contains("mcs_credit_item"))
                    {
                        var er = record.GetAttributeValue<EntityReference>("mcs_credit_item");
                        Console.WriteLine($"    [EntityReference] ID={er?.Id}, Name={er?.Name}");
                        if (er != null)
                        {
                            var item = service.Retrieve("mcs_credit_items", er.Id, new ColumnSet("mcs_credit_itemsno"));
                            if (item != null && item.Contains("mcs_credit_itemsno"))
                            {
                                itemCode = item.GetAttributeValue<string>("mcs_credit_itemsno") ?? "";
                                Console.WriteLine($"    [Retrieve] mcs_credit_itemsno = {itemCode}");
                            }
                        }
                    }

                    Console.WriteLine($"    => 最终 itemCode: '{itemCode}'");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ 查询失败: {ex.Message}");
            }
        }
    }
}
