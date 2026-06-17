using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System.Text.Json;

namespace D365MetadataTool;

/// <summary>
/// 同步 Coface 基础数据（NACE行业映射、汇率配置表）
/// </summary>
public class CofaceDataSyncHelper
{
    private readonly ServiceClient _service;

    public CofaceDataSyncHelper(ServiceClient service)
    {
        _service = service;
    }

    public void ExportToFile(string entityName, string filePath)
    {
        Console.WriteLine($"\n=== 导出 {entityName} 数据 ===");

        var query = new QueryExpression(entityName)
        {
            ColumnSet = new ColumnSet(true)
        };

        var result = _service.RetrieveMultiple(query);
        Console.WriteLine($"读取到 {result.Entities.Count} 条记录");

        var records = new List<Dictionary<string, object?>>();
        foreach (var entity in result.Entities)
        {
            var record = new Dictionary<string, object?>();
            foreach (var attr in entity.Attributes)
            {
                record[attr.Key] = SerializeValue(attr.Value);
            }
            records.Add(record);
        }

        var json = JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filePath, json);
        Console.WriteLine($"✅ 已导出到: {filePath}");
    }

    public void ImportFromFile(string entityName, string filePath)
    {
        Console.WriteLine($"\n=== 导入 {entityName} 数据到 UAT ===");

        if (!File.Exists(filePath))
        {
            Console.WriteLine($"❌ 文件不存在: {filePath}");
            return;
        }

        var json = File.ReadAllText(filePath);
        var records = JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(json);
        if (records == null || records.Count == 0)
        {
            Console.WriteLine("❌ 没有数据需要导入");
            return;
        }

        // 先查询 UAT 中是否已有数据
        var existingQuery = new QueryExpression(entityName)
        {
            ColumnSet = new ColumnSet(entityName + "id")
        };
        var existing = _service.RetrieveMultiple(existingQuery);
        Console.WriteLine($"UAT 中已有 {existing.Entities.Count} 条记录");

        if (existing.Entities.Count > 0)
        {
            Console.WriteLine("⚠️ UAT 中已有数据，跳过导入（避免重复）");
            return;
        }

        int success = 0;
        int failed = 0;
        foreach (var record in records)
        {
            var entity = new Entity(entityName);
            foreach (var kvp in record)
            {
                if (ShouldSkipAttribute(kvp.Key)) continue;
                var value = DeserializeValue(kvp.Value);
                if (value != null)
                {
                    entity[kvp.Key] = value;
                }
            }

            try
            {
                _service.Create(entity);
                success++;
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine($"❌ 创建记录失败: {ex.Message}");
            }
        }

        Console.WriteLine($"✅ 导入完成: 成功 {success} 条, 失败 {failed} 条");
    }

    private object? SerializeValue(object? value)
    {
        if (value == null) return null;
        return value switch
        {
            EntityReference er => new { type = "EntityReference", logicalName = er.LogicalName, id = er.Id },
            OptionSetValue osv => new { type = "OptionSetValue", value = osv.Value },
            Money m => new { type = "Money", value = m.Value },
            DateTime dt => dt.ToString("O"),
            bool b => b,
            int i => i,
            decimal d => d,
            double dbl => dbl,
            string s => s,
            Guid g => g.ToString(),
            AliasedValue av => SerializeValue(av.Value),
            _ => value.ToString()
        };
    }

    private object? DeserializeValue(object? value)
    {
        if (value == null) return null;
        if (value is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Null) return null;
            if (element.ValueKind == JsonValueKind.Object)
            {
                if (element.TryGetProperty("type", out var typeProp))
                {
                    var typeName = typeProp.GetString();
                    switch (typeName)
                    {
                        case "EntityReference":
                            var logicalName = element.GetProperty("logicalName").GetString() ?? "";
                            var id = Guid.Parse(element.GetProperty("id").GetString() ?? Guid.Empty.ToString());
                            return new EntityReference(logicalName, id);
                        case "OptionSetValue":
                            return new OptionSetValue(element.GetProperty("value").GetInt32());
                        case "Money":
                            return new Money(element.GetProperty("value").GetDecimal());
                    }
                }
            }
            if (element.ValueKind == JsonValueKind.String)
            {
                var str = element.GetString();
                if (DateTime.TryParse(str, out var dt)) return dt;
                if (Guid.TryParse(str, out var g)) return g;
                return str;
            }
            if (element.ValueKind == JsonValueKind.Number)
            {
                if (element.TryGetInt32(out var i)) return i;
                if (element.TryGetDecimal(out var d)) return d;
                return element.GetDouble();
            }
            if (element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False)
            {
                return element.GetBoolean();
            }
        }
        return value;
    }

    private bool ShouldSkipAttribute(string name)
    {
        // 跳过系统字段和主键
        var skipList = new[]
        {
            "createdby", "createdbyname", "createdbyyominame",
            "createdon", "createdonbehalfby", "createdonbehalfbyname", "createdonbehalfbyyominame",
            "modifiedby", "modifiedbyname", "modifiedbyyominame",
            "modifiedon", "modifiedonbehalfby", "modifiedonbehalfbyname", "modifiedonbehalfbyyominame",
            "ownerid", "owneridname", "owneridtype", "owneridyominame",
            "owningbusinessunit", "owningbusinessunitname", "owningteam", "owninguser",
            "statecode", "statecodename", "statuscode", "statuscodename",
            "timezoneruleversionnumber", "utcconversiontimezonecode", "versionnumber",
            "importsequencenumber", "overriddencreatedon"
        };
        return skipList.Contains(name.ToLower());
    }
}
