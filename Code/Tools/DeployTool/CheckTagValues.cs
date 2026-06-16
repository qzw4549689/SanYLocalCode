using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace DeployTool
{
    public class CheckTagValues
    {
        public static void Check(ServiceClient service)
        {
            Console.WriteLine(">>> 检查标签实际数值...");

            var creditRecordId = new Guid("b7e04e27-0974-4fec-8994-7c409c83b620");

            try
            {
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

                foreach (var record in records.Entities)
                {
                    var aliased = record.GetAttributeValue<AliasedValue>("item.mcs_credit_itemsno");
                    string itemCode = aliased?.Value?.ToString() ?? "";
                    int dataType = record.GetAttributeValue<OptionSetValue>("mcs_datatype")?.Value ?? 1;

                    Console.Write($"\n  {itemCode} (类型={dataType}): ");

                    bool isQuantitative = (dataType == 1 || dataType == 100000000);
                    if (isQuantitative)
                    {
                        if (record.Contains("mcs_itemintvalue2"))
                            Console.Write($"intvalue2={record.GetAttributeValue<decimal>("mcs_itemintvalue2")}");
                        else if (record.Contains("mcs_itemvalue2"))
                            Console.Write($"value2={record.GetAttributeValue<string>("mcs_itemvalue2")}");
                        else
                            Console.Write("无数值");
                    }
                    else
                    {
                        if (record.Contains("mcs_itemtxtvalue2"))
                            Console.Write($"txtvalue2={record.GetAttributeValue<string>("mcs_itemtxtvalue2")}");
                        else if (record.Contains("mcs_itemvalue2"))
                            Console.Write($"value2={record.GetAttributeValue<string>("mcs_itemvalue2")}");
                        else
                            Console.Write("无数值");
                    }
                }
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ 查询失败: {ex.Message}");
            }
        }
    }
}
