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
            ColumnSet = new ColumnSet("name", "objecttypecode", "logicalname", "entityid"),
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
            var entityId = entity.GetAttributeValue<Guid>("entityid");
            Console.WriteLine($"  {name} (OTC: {objectTypeCode}, EntityId: {entityId})");
        }
        Console.WriteLine("\n=== 完成 ===");
    }

    public void CountRecords(string[] entityNames)
    {
        Console.WriteLine($"\n=== 实体记录数统计 ===");
        Console.WriteLine($"环境: {_service.ConnectedOrgUriActual}\n");
        Console.WriteLine(string.Format("{0,-40} {1,10}", "实体逻辑名", "记录数"));
        Console.WriteLine(new string('-', 52));

        foreach (var entityName in entityNames)
        {
            try
            {
                var idAttribute = entityName + "id";
                var fetchXml = $@"<fetch aggregate='true'>
  <entity name='{entityName}'>
    <attribute name='{idAttribute}' aggregate='count' alias='count'/>
  </entity>
</fetch>";
                var result = _service.RetrieveMultiple(new FetchExpression(fetchXml));
                if (result.Entities.Count > 0)
                {
                    var countValue = result.Entities[0].GetAttributeValue<AliasedValue>("count");
                    var count = countValue?.Value is int i ? i : Convert.ToInt32(countValue?.Value);
                    Console.WriteLine($"{entityName,-40} {count,10}");
                }
                else
                {
                    Console.WriteLine($"{entityName,-40} {0,10}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("{0,-40} {1,10}", entityName, "错误: " + ex.Message));
            }
        }

        Console.WriteLine("\n=== 完成 ===");
    }
}
