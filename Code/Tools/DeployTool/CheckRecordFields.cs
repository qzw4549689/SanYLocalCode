using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace DeployTool
{
    public class CheckRecordFields
    {
        public static void Check(ServiceClient service)
        {
            Console.WriteLine(">>> 检查最新记录的 Coface 字段...");

            var query = new QueryExpression("mcs_credit_record")
            {
                ColumnSet = new ColumnSet(
                    "mcs_status", "mcs_cofaceid", "mcs_countrycode", "mcs_scoreid",
                    "mcs_urba360id", "mcs_urbastatus", 
                    "mcs_rptorderid", "mcs_rptstatus", "mcs_publicationid",
                    "mcs_urbajson", "mcs_reportjson", "mcs_api_status", "mcs_api_msg"),
                Criteria = new FilterExpression(),
                Orders = { new OrderExpression("modifiedon", OrderType.Descending) },
                TopCount = 3
            };

            var results = service.RetrieveMultiple(query);
            Console.WriteLine($"  找到 {results.Entities.Count} 条记录\n");

            foreach (var record in results.Entities)
            {
                Console.WriteLine($"  记录 ID: {record.Id}");
                Console.WriteLine($"    状态: {record.GetAttributeValue<OptionSetValue>("mcs_status")?.Value}");
                Console.WriteLine($"    cofaceId: {record.GetAttributeValue<string>("mcs_cofaceid") ?? "(空)"}");
                Console.WriteLine($"    countryCode: {record.GetAttributeValue<string>("mcs_countrycode") ?? "(空)"}");
                Console.WriteLine($"    URBA订单ID: {record.GetAttributeValue<string>("mcs_urba360id") ?? "(空)"}");
                Console.WriteLine($"    URBA状态: {record.GetAttributeValue<string>("mcs_urbastatus") ?? "(空)"}");
                Console.WriteLine($"    Report订单ID: {record.GetAttributeValue<string>("mcs_rptorderid") ?? "(空)"}");
                Console.WriteLine($"    Report状态: {record.GetAttributeValue<string>("mcs_rptstatus") ?? "(空)"}");
                Console.WriteLine($"    Publication ID: {record.GetAttributeValue<string>("mcs_publicationid") ?? "(空)"}");
                Console.WriteLine($"    API状态: {record.GetAttributeValue<string>("mcs_api_status") ?? "(空)"}");
                Console.WriteLine($"    API消息: {record.GetAttributeValue<string>("mcs_api_msg") ?? "(空)"}");
                var urbaJson = record.GetAttributeValue<string>("mcs_urbajson");
                Console.WriteLine($"    URBA JSON: {(string.IsNullOrEmpty(urbaJson) ? "(空)" : $"有值({urbaJson.Length}字符)")}");
                var reportJson = record.GetAttributeValue<string>("mcs_reportjson");
                Console.WriteLine($"    Report JSON: {(string.IsNullOrEmpty(reportJson) ? "(空)" : $"有值({reportJson.Length}字符)")}");
                Console.WriteLine();
            }
        }
    }
}
