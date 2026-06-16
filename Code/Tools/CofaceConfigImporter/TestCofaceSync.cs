using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

class TestCofaceSync
{
    /// <summary>
    /// 列出适合测试 Coface 数据集成的 mcs_credit_record 候选记录
    /// </summary>
    public static void ListCandidates(ServiceClient service)
    {
        Console.WriteLine(">>> 查询可用于测试 Coface 数据集成的评估记录...");

        var query = new QueryExpression("mcs_credit_record")
        {
            ColumnSet = new ColumnSet(
                "mcs_credit_recordid",
                "mcs_scoreid",
                "mcs_status",
                "mcs_countrycode",
                "mcs_cofaceid",
                "mcs_custnameen",
                "mcs_accountid",
                "createdon"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("mcs_cofaceid", ConditionOperator.NotNull),
                    new ConditionExpression("mcs_countrycode", ConditionOperator.NotNull),
                    new ConditionExpression("mcs_status", ConditionOperator.NotEqual, 11)
                }
            },
            Orders = { new OrderExpression("createdon", OrderType.Descending) },
            TopCount = 20
        };

        var results = service.RetrieveMultiple(query);
        Console.WriteLine($"找到 {results.Entities.Count} 条候选记录（状态≠11，且已有 cofaceId 和 countryCode）：\n");

        if (results.Entities.Count == 0)
        {
            Console.WriteLine("⚠️ 没有符合条件的记录。需要：");
            Console.WriteLine("  1. 在 D365 中创建或找到一条 mcs_credit_record");
            Console.WriteLine("  2. 确保该记录已关联客户（mcs_accountid）");
            Console.WriteLine("  3. 确保 mcs_cofaceid 已填写");
            Console.WriteLine("  4. 确保 mcs_countrycode 已填写，且在国家配置表中有财务指标配置");
            return;
        }

        Console.WriteLine($"{"ID",-38} {"ScoreID",-18} {"Status",-8} {"Country",-8} {"CofaceID",-20} {"Customer(EN)",-30}");
        Console.WriteLine(new string('-', 130));
        foreach (var r in results.Entities)
        {
            var id = r.Id;
            var scoreId = r.GetAttributeValue<string>("mcs_scoreid") ?? "";
            var status = r.GetAttributeValue<OptionSetValue>("mcs_status")?.Value.ToString() ?? "";
            var country = r.GetAttributeValue<string>("mcs_countrycode") ?? "";
            var cofaceId = r.GetAttributeValue<string>("mcs_cofaceid") ?? "";
            var custName = r.GetAttributeValue<string>("mcs_custnameen") ?? "";
            Console.WriteLine($"{id,-38} {scoreId,-18} {status,-8} {country,-8} {cofaceId,-20} {custName}");
        }

        Console.WriteLine("\n用法示例：");
        Console.WriteLine("  dotnet run test-coface trigger <记录ID>");
    }

    /// <summary>
    /// 触发指定记录的 Coface 数据集成，并等待查询结果
    /// </summary>
    public static void Trigger(ServiceClient service, Guid recordId)
    {
        Console.WriteLine($">>> 准备触发记录 {recordId} 的 Coface 数据集成...");

        // 1. 读取记录前置条件
        var record = service.Retrieve("mcs_credit_record", recordId,
            new ColumnSet("mcs_scoreid", "mcs_status", "mcs_countrycode", "mcs_cofaceid", "mcs_custnameen", "mcs_accountid"));

        if (record == null)
        {
            Console.WriteLine($"❌ 记录 {recordId} 不存在");
            return;
        }

        var scoreId = record.GetAttributeValue<string>("mcs_scoreid") ?? "";
        var countryCode = record.GetAttributeValue<string>("mcs_countrycode") ?? "";
        var cofaceId = record.GetAttributeValue<string>("mcs_cofaceid") ?? "";
        var currentStatus = record.GetAttributeValue<OptionSetValue>("mcs_status")?.Value ?? 0;

        Console.WriteLine($"  ScoreID: {scoreId}");
        Console.WriteLine($"  Country: {countryCode}");
        Console.WriteLine($"  CofaceID: {cofaceId}");
        Console.WriteLine($"  当前状态: {currentStatus}");

        if (string.IsNullOrEmpty(cofaceId))
        {
            Console.WriteLine("❌ mcs_cofaceid 为空，无法触发");
            return;
        }

        if (string.IsNullOrEmpty(countryCode))
        {
            Console.WriteLine("❌ mcs_countrycode 为空，无法触发");
            return;
        }

        // 2. 检查该国家是否有财务指标配置
        var configQuery = new QueryExpression("mcs_coface_financial_indicator")
        {
            ColumnSet = new ColumnSet("mcs_countrycode"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("mcs_countrycode", ConditionOperator.Equal, countryCode),
                    new ConditionExpression("mcs_isactive", ConditionOperator.Equal, true)
                }
            },
            TopCount = 1
        };
        var configExists = service.RetrieveMultiple(configQuery).Entities.Count > 0;
        Console.WriteLine($"  财务指标配置存在: {(configExists ? "是" : "否")}");

        // 3. 触发 Plugin：将状态更新为 11
        Console.WriteLine("\n>>> 更新状态为 11，触发 CofaceDataSyncPlugin...");
        var update = new Entity("mcs_credit_record") { Id = recordId };
        update["mcs_status"] = new OptionSetValue(11);
        service.Update(update);
        Console.WriteLine("✅ 已触发，等待 15 秒让 Plugin 异步完成...");
        Thread.Sleep(15000);

        // 4. 读取记录的最新 API 状态
        Console.WriteLine("\n>>> 查询评估记录的最新状态...");
        var refreshed = service.Retrieve("mcs_credit_record", recordId,
            new ColumnSet("mcs_api_status", "mcs_api_name", "mcs_api_msg", "mcs_urbastatus", "mcs_rptstatus"));

        var apiStatus = refreshed.GetAttributeValue<string>("mcs_api_status") ?? "(空)";
        var apiName = refreshed.GetAttributeValue<string>("mcs_api_name") ?? "(空)";
        var apiMsg = refreshed.GetAttributeValue<string>("mcs_api_msg") ?? "(空)";
        var urbaStatus = refreshed.GetAttributeValue<string>("mcs_urbastatus") ?? "(空)";
        var rptStatus = refreshed.GetAttributeValue<string>("mcs_rptstatus") ?? "(空)";

        Console.WriteLine($"  API Status: {apiStatus}");
        Console.WriteLine($"  API Name: {apiName}");
        Console.WriteLine($"  API Msg: {apiMsg}");
        Console.WriteLine($"  URBA Status: {urbaStatus}");
        Console.WriteLine($"  Report Status: {rptStatus}");

        // 5. 查询生成的标签
        Console.WriteLine("\n>>> 查询生成的客户信用标签...");
        var tagQuery = new QueryExpression("mcs_customer_tag")
        {
            ColumnSet = new ColumnSet("mcs_credit_record", "mcs_credit_item", "mcs_itemvalue1", "mcs_itemtxtvalue1", "mcs_scorevalue", "createdon"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("mcs_credit_record", ConditionOperator.Equal, recordId)
                }
            }
        };
        var tags = service.RetrieveMultiple(tagQuery);
        Console.WriteLine($"  标签数量: {tags.Entities.Count}");

        if (tags.Entities.Count > 0)
        {
            Console.WriteLine($"\n  {"创建时间",-22} {"评分项目",-38} {"数值",-20} {"文本值",-20}");
            Console.WriteLine(new string('-', 100));
            foreach (var tag in tags.Entities)
            {
                var createdOn = tag.GetAttributeValue<DateTime>("createdon").ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
                var itemRef = tag.GetAttributeValue<EntityReference>("mcs_credit_item");
                var itemName = itemRef?.Name ?? itemRef?.Id.ToString() ?? "";
                var value1Obj = tag.GetAttributeValue<object>("mcs_itemvalue1");
                var value1Str = value1Obj?.ToString() ?? "";
                var txtValue1 = tag.GetAttributeValue<string>("mcs_itemtxtvalue1") ?? "";
                var scoreValueObj = tag.GetAttributeValue<object>("mcs_scorevalue");
                var scoreValueStr = scoreValueObj?.ToString() ?? "";

                Console.WriteLine($"  {createdOn,-22} {itemName,-38} {value1Str,-20} {txtValue1,-20}");
            }
        }

        // 6. 查询 Plugin Trace
        Console.WriteLine("\n>>> 查询最近 3 条 CofaceDataSyncPlugin 执行日志...");
        var traceQuery = new QueryExpression("plugintracelog")
        {
            ColumnSet = new ColumnSet("createdon", "typename", "messageblock", "exceptiondetails"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("typename", ConditionOperator.Like, "%CofaceDataSyncPlugin%")
                }
            },
            Orders = { new OrderExpression("createdon", OrderType.Descending) },
            TopCount = 3
        };
        var traces = service.RetrieveMultiple(traceQuery);
        foreach (var trace in traces.Entities)
        {
            var createdOn = trace.GetAttributeValue<DateTime>("createdon").ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            var typeName = trace.GetAttributeValue<string>("typename") ?? "";
            var msg = trace.GetAttributeValue<string>("messageblock") ?? "";
            var ex = trace.GetAttributeValue<string>("exceptiondetails") ?? "";

            Console.WriteLine($"\n  [{createdOn}] {typeName}");
            if (!string.IsNullOrEmpty(ex))
            {
                Console.WriteLine($"  ❌ 异常: {ex.Substring(0, Math.Min(ex.Length, 500))}");
            }
            if (!string.IsNullOrEmpty(msg))
            {
                Console.WriteLine($"  日志: {msg.Substring(0, Math.Min(msg.Length, 500))}");
            }
        }
    }
}
