using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace D365MetadataTool;

/// <summary>
/// 诊断 UAT/DEV 上 credit record 数据集成问题
/// </summary>
public class CreditRecordDiagnosticHelper
{
    private readonly ServiceClient _service;

    public CreditRecordDiagnosticHelper(ServiceClient service)
    {
        _service = service;
    }

    public void DiagnoseByScoreId(string scoreId)
    {
        Console.WriteLine($"\n=== 诊断信用评估记录: {scoreId} ===");
        Console.WriteLine($"环境: {_service.ConnectedOrgUriActual}\n");

        // 1. 查询 credit record
        var query = new QueryExpression("mcs_credit_record")
        {
            ColumnSet = new ColumnSet(
                "mcs_credit_recordid", "mcs_scoreid", "mcs_status", "mcs_accountid",
                "mcs_custname", "mcs_custnameen", "mcs_countrycode", "mcs_cofaceid",
                "mcs_creditscore", "mcs_overduerate",
                "mcs_api_status", "mcs_api_name", "mcs_api_msg",
                "mcs_urbajson", "mcs_reportjson", "mcs_rptstatus", "mcs_rptorderid"
            ),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("mcs_scoreid", ConditionOperator.Equal, scoreId) }
            }
        };

        var result = _service.RetrieveMultiple(query);
        if (result.Entities.Count == 0)
        {
            Console.WriteLine($"❌ 未找到记录: {scoreId}");
            return;
        }

        var record = result.Entities[0];
        var recordId = record.GetAttributeValue<Guid>("mcs_credit_recordid");
        Console.WriteLine("信用评估记录基本信息:");
        PrintField(record, "mcs_scoreid");
        PrintField(record, "mcs_status");
        PrintField(record, "mcs_accountid");
        PrintField(record, "mcs_custname");
        PrintField(record, "mcs_custnameen");
        PrintField(record, "mcs_countrycode");
        PrintField(record, "mcs_cofaceid");
        PrintField(record, "mcs_creditscore");
        PrintField(record, "mcs_overduerate");
        PrintField(record, "mcs_api_status");
        PrintField(record, "mcs_api_name");
        PrintField(record, "mcs_api_msg");
        PrintField(record, "mcs_rptstatus");
        PrintField(record, "mcs_rptorderid");

        // 2. 查询关联的客户主数据
        var accountRef = record.GetAttributeValue<EntityReference>("mcs_accountid");
        if (accountRef != null)
        {
            Console.WriteLine("\n关联客户信息:");
            var account = _service.Retrieve("account", accountRef.Id,
                new ColumnSet("name", "mcs_customermasterdata"));
            PrintField(account, "name");

            var masterRef = account.GetAttributeValue<EntityReference>("mcs_customermasterdata");
            if (masterRef != null)
            {
                var master = _service.Retrieve("mcs_customermasterdata", masterRef.Id,
                    new ColumnSet("mcs_accountnumber", "mcs_englishname", "mcs_countrycode", "mcs_cofaceid"));
                PrintField(master, "mcs_accountnumber");
                PrintField(master, "mcs_englishname");
                PrintField(master, "mcs_countrycode");
                PrintField(master, "mcs_cofaceid");
            }
            else
            {
                Console.WriteLine("⚠️ 客户未关联客户主数据");
            }
        }

        // 3. 查询关联的 Credit Tags
        Console.WriteLine("\n关联 Credit Tags (mcs_customer_tag):");
        try
        {
            var tagQuery = new QueryExpression("mcs_customer_tag")
            {
                ColumnSet = new ColumnSet(
                    "mcs_group", "mcs_itemname", "mcs_itemcode", "mcs_datatype",
                    "mcs_itemtxtvalue1", "mcs_itemtxtvalue2",
                    "mcs_itemintvalue1", "mcs_itemintvalue2"
                ),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("mcs_credit_record", ConditionOperator.Equal, recordId) }
                }
            };
            var tags = _service.RetrieveMultiple(tagQuery);
            Console.WriteLine($"找到 {tags.Entities.Count} 条 Credit Tags");
            foreach (var tag in tags.Entities)
            {
                Console.WriteLine($"\n  Tag: {tag.GetAttributeValue<string>("mcs_itemname")} ({tag.GetAttributeValue<string>("mcs_itemcode")})");
                PrintField(tag, "mcs_group", "  分类: ");
                PrintField(tag, "mcs_datatype", "  数据类型: ");
                PrintField(tag, "mcs_itemtxtvalue1", "  集成定性: ");
                PrintField(tag, "mcs_itemintvalue1", "  集成定量: ");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 查询 Credit Tags 失败: {ex.Message}");
            Console.WriteLine("   可能原因: 尚未部署 mcs_customer_tag 实体");
        }

        // 4. 查询 Coface API 配置 (ms_systemconfiguration)
        Console.WriteLine("\nCoface API 配置 (ms_systemconfiguration):");
        try
        {
            var configQuery = new QueryExpression("ms_systemconfiguration")
            {
                ColumnSet = new ColumnSet("ms_name", "ms_content"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("ms_name", ConditionOperator.Equal, "CofaceApiConfig") }
                }
            };
            var configs = _service.RetrieveMultiple(configQuery);
            Console.WriteLine($"找到 {configs.Entities.Count} 条 Coface API 配置");
            foreach (var config in configs.Entities)
            {
                var value = config.GetAttributeValue<string>("ms_content");
                Console.WriteLine($"  {config.GetAttributeValue<string>("ms_name")}: {value?.Substring(0, Math.Min(value?.Length ?? 0, 200))}...");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 查询 Coface 配置失败: {ex.Message}");
        }

        // 5. 查询评分项目配置
        Console.WriteLine("\n评分项目配置 (mcs_credit_items):");
        var itemQuery = new QueryExpression("mcs_credit_items")
        {
            ColumnSet = new ColumnSet("mcs_itemname", "mcs_credit_itemsno", "mcs_datatype", "mcs_group")
        };
        var items = _service.RetrieveMultiple(itemQuery);
        Console.WriteLine($"找到 {items.Entities.Count} 条评分项目");

        // 6. 查询 Plugin Trace Log
        Console.WriteLine("\nCofaceDataSyncPlugin 最近 Trace 日志:");
        try
        {
            var traceQuery = new QueryExpression("plugintracelog")
            {
                ColumnSet = new ColumnSet("createdon", "messageblock", "operationtype", "plugintracelogid", "typename"),
                Orders = { new OrderExpression("createdon", OrderType.Descending) },
                PageInfo = { Count = 20, PageNumber = 1 }
            };
            var allTraces = _service.RetrieveMultiple(traceQuery).Entities
                .Where(t => t.GetAttributeValue<string>("typename")?.Contains("CofaceDataSyncPlugin") == true)
                .Take(5)
                .ToList();
            Console.WriteLine($"找到 {allTraces.Count} 条 CofaceDataSyncPlugin Trace 日志");
            foreach (var trace in allTraces)
            {
                var createdOn = trace.GetAttributeValue<DateTime>("createdon");
                var message = trace.GetAttributeValue<string>("messageblock");
                var typeName = trace.GetAttributeValue<string>("typename");
                Console.WriteLine($"\n  [{createdOn:yyyy-MM-dd HH:mm:ss}] {typeName}");
                Console.WriteLine($"  {message?.Substring(0, Math.Min(message?.Length ?? 0, 500))}...");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 查询 Trace 日志失败: {ex.Message}");
        }

        Console.WriteLine("\n=== 诊断完成 ===");
    }

    private void PrintField(Entity entity, string fieldName, string prefix = "")
    {
        if (entity.Contains(fieldName) && entity[fieldName] != null)
        {
            Console.WriteLine($"{prefix}{fieldName}: {entity[fieldName]}");
        }
        else
        {
            Console.WriteLine($"{prefix}{fieldName}: (空)");
        }
    }
}
