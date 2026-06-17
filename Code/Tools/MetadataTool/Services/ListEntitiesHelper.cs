using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace D365MetadataTool;

/// <summary>
/// 列出环境中指定前缀的实体
/// </summary>
public class ListEntitiesHelper
{
    private readonly ServiceClient _service;

    public ListEntitiesHelper(ServiceClient service)
    {
        _service = service;
    }

    public void ListByPrefix(string prefix)
    {
        Console.WriteLine($"\n=== 列出以 '{prefix}' 开头的实体 ===");
        Console.WriteLine($"环境: {_service.ConnectedOrgUriActual}\n");

        var query = new QueryExpression("entity")
        {
            ColumnSet = new ColumnSet("name", "objecttypecode", "logicalname"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("name", ConditionOperator.BeginsWith, prefix) }
            },
            Orders = { new OrderExpression("name", OrderType.Ascending) }
        };

        var result = _service.RetrieveMultiple(query);
        Console.WriteLine($"找到 {result.Entities.Count} 个实体:\n");
        foreach (var entity in result.Entities)
        {
            var name = entity.GetAttributeValue<string>("name");
            var objectTypeCode = entity.GetAttributeValue<int?>("objecttypecode");
            Console.WriteLine($"  {name} (OTC: {objectTypeCode})");
        }
        Console.WriteLine("\n=== 完成 ===");
    }
}
