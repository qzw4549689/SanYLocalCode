using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;

namespace D365ToolCommon.Metadata
{
    /// <summary>
    /// 实体字段通用服务：检查、创建、删除。
    /// </summary>
    public class MetadataFieldService
    {
        private readonly ServiceClient _service;

        public MetadataFieldService(ServiceClient service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        /// <summary>
        /// 检查字段是否存在。
        /// </summary>
        public bool FieldExists(string entityName, string fieldLogicalName)
        {
            try
            {
                var request = new RetrieveAttributeRequest
                {
                    EntityLogicalName = entityName,
                    LogicalName = fieldLogicalName.ToLower()
                };
                _service.Execute(request);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 批量检查字段是否存在，返回存在/不存在的字段列表。
        /// </summary>
        public (List<string> exists, List<string> missing) CheckFieldsExist(string entityName, params string[] fieldLogicalNames)
        {
            var request = new RetrieveEntityRequest
            {
                EntityFilters = EntityFilters.Attributes,
                LogicalName = entityName
            };

            var response = (RetrieveEntityResponse)_service.Execute(request);
            var attributes = response.EntityMetadata.Attributes;

            var exists = new List<string>();
            var missing = new List<string>();

            foreach (var field in fieldLogicalNames)
            {
                var attr = attributes.FirstOrDefault(a => a.LogicalName == field.ToLower());
                if (attr != null)
                    exists.Add(field);
                else
                    missing.Add(field);
            }

            return (exists, missing);
        }

        /// <summary>
        /// 创建字符串字段（如果不存在）。
        /// </summary>
        public bool CreateStringFieldIfNotExists(string entityName, string schemaName, string displayName, string description, int maxLength = 100, bool required = false)
        {
            var logicalName = schemaName.ToLower();
            if (FieldExists(entityName, logicalName))
            {
                Console.WriteLine($"  ⬜ 字段已存在: {schemaName}");
                return false;
            }

            var request = new CreateAttributeRequest
            {
                EntityName = entityName,
                Attribute = new StringAttributeMetadata
                {
                    SchemaName = schemaName,
                    LogicalName = logicalName,
                    DisplayName = LabelHelper.Create(displayName),
                    RequiredLevel = new AttributeRequiredLevelManagedProperty(required ? AttributeRequiredLevel.ApplicationRequired : AttributeRequiredLevel.None),
                    Description = LabelHelper.Create(description),
                    MaxLength = maxLength
                }
            };

            _service.Execute(request);
            Console.WriteLine($"  ✅ 字段已创建: {schemaName} ({displayName})");
            return true;
        }

        /// <summary>
        /// 创建整数字段（如果不存在）。
        /// </summary>
        public bool CreateIntegerFieldIfNotExists(string entityName, string schemaName, string displayName, string description, int minValue = 0, int maxValue = int.MaxValue)
        {
            var logicalName = schemaName.ToLower();
            if (FieldExists(entityName, logicalName))
            {
                Console.WriteLine($"  ⬜ 字段已存在: {schemaName}");
                return false;
            }

            var request = new CreateAttributeRequest
            {
                EntityName = entityName,
                Attribute = new IntegerAttributeMetadata
                {
                    SchemaName = schemaName,
                    LogicalName = logicalName,
                    DisplayName = LabelHelper.Create(displayName),
                    RequiredLevel = new AttributeRequiredLevelManagedProperty(AttributeRequiredLevel.None),
                    Description = LabelHelper.Create(description),
                    Format = IntegerFormat.None,
                    MinValue = minValue,
                    MaxValue = maxValue
                }
            };

            _service.Execute(request);
            Console.WriteLine($"  ✅ 字段已创建: {schemaName} ({displayName})");
            return true;
        }

        /// <summary>
        /// 创建布尔字段（如果不存在）。
        /// </summary>
        public bool CreateBooleanFieldIfNotExists(string entityName, string schemaName, string displayName, string description, bool defaultValue = false)
        {
            var logicalName = schemaName.ToLower();
            if (FieldExists(entityName, logicalName))
            {
                Console.WriteLine($"  ⬜ 字段已存在: {schemaName}");
                return false;
            }

            var request = new CreateAttributeRequest
            {
                EntityName = entityName,
                Attribute = new BooleanAttributeMetadata
                {
                    SchemaName = schemaName,
                    LogicalName = logicalName,
                    DisplayName = LabelHelper.Create(displayName),
                    RequiredLevel = new AttributeRequiredLevelManagedProperty(AttributeRequiredLevel.None),
                    Description = LabelHelper.Create(description),
                    OptionSet = new BooleanOptionSetMetadata(
                        new OptionMetadata(LabelHelper.Create("是"), 1),
                        new OptionMetadata(LabelHelper.Create("否"), 0))
                }
            };

            _service.Execute(request);
            Console.WriteLine($"  ✅ 字段已创建: {schemaName} ({displayName})");
            return true;
        }

        /// <summary>
        /// 删除字段。
        /// </summary>
        public void DeleteField(string entityName, string fieldLogicalName)
        {
            var request = new DeleteAttributeRequest
            {
                EntityLogicalName = entityName,
                LogicalName = fieldLogicalName.ToLower()
            };
            _service.Execute(request);
        }
    }
}
