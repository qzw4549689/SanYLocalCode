using System.Text.Json;
using System.Text.Json.Serialization;

namespace D365MetadataTool
{
    /// <summary>
    /// 实体定义（用于JSON序列化）
    /// </summary>
    public class EntityDefinition
    {
        [JsonPropertyName("entityName")]
        public string EntityName { get; set; } = "";

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = "";

        [JsonPropertyName("primaryAttribute")]
        public string PrimaryAttribute { get; set; } = "";

        [JsonPropertyName("primaryAttributeDisplayName")]
        public string PrimaryAttributeDisplayName { get; set; } = "";

        [JsonPropertyName("primaryAttributeLength")]
        public int PrimaryAttributeLength { get; set; } = 100;

        [JsonPropertyName("solutionName")]
        public string SolutionName { get; set; } = "";

        [JsonPropertyName("autoPublish")]
        public bool AutoPublish { get; set; } = false;

        [JsonPropertyName("fields")]
        public List<FieldDefinition> Fields { get; set; } = new();

        /// <summary>
        /// 从JSON文件加载定义
        /// </summary>
        public static EntityDefinition LoadFromJson(string filePath)
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<EntityDefinition>(json) 
                ?? throw new Exception("无法解析JSON文件");
        }

        /// <summary>
        /// 保存为JSON文件
        /// </summary>
        public void SaveToJson(string filePath)
        {
            var options = new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(filePath, json);
        }
    }

    /// <summary>
    /// 字段定义
    /// </summary>
    public class FieldDefinition
    {
        [JsonPropertyName("schemaName")]
        public string SchemaName { get; set; } = "";

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = "";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("type")]
        public string Type { get; set; } = "string";

        [JsonPropertyName("required")]
        public bool Required { get; set; } = false;

        [JsonPropertyName("maxLength")]
        public int? MaxLength { get; set; }

        [JsonPropertyName("minValue")]
        public int? MinValue { get; set; }

        [JsonPropertyName("maxValue")]
        public int? MaxValue { get; set; }

        [JsonPropertyName("minDecimalValue")]
        public decimal? MinDecimalValue { get; set; }

        [JsonPropertyName("maxDecimalValue")]
        public decimal? MaxDecimalValue { get; set; }

        [JsonPropertyName("precision")]
        public int? Precision { get; set; }

        [JsonPropertyName("dateOnly")]
        public bool? DateOnly { get; set; }

        [JsonPropertyName("trueLabel")]
        public string TrueLabel { get; set; } = "是";

        [JsonPropertyName("falseLabel")]
        public string FalseLabel { get; set; } = "否";

        [JsonPropertyName("options")]
        public Dictionary<string, int>? Options { get; set; }

        [JsonPropertyName("targetEntity")]
        public string TargetEntity { get; set; } = "";

        [JsonPropertyName("targetEntityDisplayName")]
        public string TargetEntityDisplayName { get; set; } = "";
    }
}
