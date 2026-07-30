using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace D365MetadataTool;

/// <summary>
/// 查询 D365 环境中的 Custom API（含请求参数与响应属性）
/// </summary>
public class QueryCustomApis
{
    private readonly ServiceClient _service;

    public QueryCustomApis(ServiceClient service)
    {
        _service = service;
    }

    /// <summary>
    /// 列出 unique name 包含指定关键字的 Custom API
    /// </summary>
    public void ListCustomApis(string? keyword = null)
    {
        Console.WriteLine($"\n=== 查询 Custom API ===");
        Console.WriteLine($"环境: {_service.ConnectedOrgUriActual}");
        Console.WriteLine(string.IsNullOrEmpty(keyword) ? "关键字: (全部)" : $"关键字: {keyword}");
        Console.WriteLine();

        var query = new QueryExpression("customapi")
        {
            ColumnSet = new ColumnSet(
                "customapiid",
                "name",
                "uniquename",
                "displayname",
                "description",
                "boundentitylogicalname",
                "isfunction",
                "statecode",
                "ismanaged"
            ),
            Orders = { new OrderExpression("uniquename", OrderType.Ascending) }
        };

        if (!string.IsNullOrEmpty(keyword))
        {
            query.Criteria.Conditions.Add(
                new ConditionExpression("uniquename", ConditionOperator.Like, $"%{keyword}%"));
        }

        var result = _service.RetrieveMultiple(query);
        if (result.Entities.Count == 0)
        {
            Console.WriteLine("❌ 未找到 Custom API");
            return;
        }

        Console.WriteLine($"找到 {result.Entities.Count} 个 Custom API(s):\n");

        foreach (var api in result.Entities)
        {
            var apiId = api.GetAttributeValue<Guid>("customapiid");
            var uniqueName = api.GetAttributeValue<string>("uniquename");
            var name = api.GetAttributeValue<string>("name");
            var displayName = api.GetAttributeValue<string>("displayname");
            var boundEntity = api.GetAttributeValue<string>("boundentitylogicalname");
            var isFunction = api.GetAttributeValue<bool>("isfunction");
            var description = (api.GetAttributeValue<string>("description") ?? "").Replace("\n", " ").Replace("\r", "");
            if (description.Length > 120) description = description.Substring(0, 120) + "...";

            Console.WriteLine($"🔹 {uniqueName}");
            Console.WriteLine($"   Name:        {name}");
            Console.WriteLine($"   DisplayName: {displayName}");
            Console.WriteLine($"   BoundEntity: {boundEntity ?? "(无)"}");
            Console.WriteLine($"   IsFunction:  {isFunction}");
            if (!string.IsNullOrEmpty(description))
            {
                Console.WriteLine($"   Description: {description}");
            }

            QueryRequestParameters(apiId);
            QueryResponseProperties(apiId);

            Console.WriteLine();
        }
    }

    private void QueryRequestParameters(Guid apiId)
    {
        var query = new QueryExpression("customapirequestparameter")
        {
            ColumnSet = new ColumnSet(
                "uniquename",
                "name",
                "displayname",
                "description",
                "type",
                "logicalentityname",
                "isoptional"
            ),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("customapiid", ConditionOperator.Equal, apiId) }
            },
            Orders = { new OrderExpression("uniquename", OrderType.Ascending) }
        };

        var result = _service.RetrieveMultiple(query);
        if (result.Entities.Count == 0)
        {
            Console.WriteLine("   Request Parameters: (无)");
            return;
        }

        Console.WriteLine($"   Request Parameters ({result.Entities.Count}):");
        foreach (var p in result.Entities)
        {
            var uniqueName = p.GetAttributeValue<string>("uniquename");
            var typeName = GetFormattedTypeName(p);
            var entityName = p.GetAttributeValue<string>("logicalentityname");
            var optional = p.GetAttributeValue<bool>("isoptional") ? "可选" : "必填";
            Console.WriteLine($"     - {uniqueName}: {typeName}{(string.IsNullOrEmpty(entityName) ? "" : $" ({entityName})")} [{optional}]");
        }
    }

    private void QueryResponseProperties(Guid apiId)
    {
        var query = new QueryExpression("customapiresponseproperty")
        {
            ColumnSet = new ColumnSet(
                "uniquename",
                "name",
                "displayname",
                "description",
                "type",
                "logicalentityname"
            ),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("customapiid", ConditionOperator.Equal, apiId) }
            },
            Orders = { new OrderExpression("uniquename", OrderType.Ascending) }
        };

        var result = _service.RetrieveMultiple(query);
        if (result.Entities.Count == 0)
        {
            Console.WriteLine("   Response Properties: (无)");
            return;
        }

        Console.WriteLine($"   Response Properties ({result.Entities.Count}):");
        foreach (var p in result.Entities)
        {
            var uniqueName = p.GetAttributeValue<string>("uniquename");
            var typeName = GetFormattedTypeName(p);
            var entityName = p.GetAttributeValue<string>("logicalentityname");
            Console.WriteLine($"     - {uniqueName}: {typeName}{(string.IsNullOrEmpty(entityName) ? "" : $" ({entityName})")}");
        }
    }

    private static string GetParameterTypeName(int? type)
    {
        return type switch
        {
            0 => "String",
            1 => "Boolean",
            2 => "DateTime",
            3 => "Decimal",
            4 => "Entity",
            5 => "EntityCollection",
            6 => "EntityReference",
            7 => "Float",
            8 => "Integer",
            9 => "Money",
            10 => "Picklist",
            11 => "StringArray",
            12 => "Guid",
            _ => $"Type_{type}"
        };
    }

    private static string GetFormattedTypeName(Entity entity)
    {
        if (entity.FormattedValues.ContainsKey("type"))
        {
            return entity.FormattedValues["type"];
        }
        return GetParameterTypeName(entity.GetAttributeValue<OptionSetValue>("type")?.Value);
    }
}
