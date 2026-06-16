using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;

namespace D365MetadataTool
{
    /// <summary>
    /// D365 实体管理器
    /// 支持：创建实体、创建字段、更新字段、删除字段、添加到解决方案、查询实体
    /// </summary>
    public class EntityManager
    {
        private readonly ServiceClient _service;

        public EntityManager(ServiceClient service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        #region 实体操作

        /// <summary>
        /// 创建实体
        /// </summary>
        public Guid CreateEntity(string schemaName, string displayName, string primaryAttributeName, string primaryAttributeDisplayName, int primaryAttributeLength = 100)
        {
            Console.WriteLine($"创建实体: {schemaName}");

            var request = new CreateEntityRequest
            {
                Entity = new EntityMetadata
                {
                    SchemaName = schemaName,
                    DisplayName = LabelHelper.Create(displayName),
                    DisplayCollectionName = LabelHelper.Create(displayName),
                    Description = LabelHelper.Create(displayName),
                    OwnershipType = OwnershipTypes.UserOwned,
                    IsActivity = false,
                },
                PrimaryAttribute = new StringAttributeMetadata
                {
                    SchemaName = primaryAttributeName,
                    LogicalName = primaryAttributeName.ToLower(),
                    RequiredLevel = new AttributeRequiredLevelManagedProperty(AttributeRequiredLevel.None),
                    MaxLength = primaryAttributeLength,
                    FormatName = StringFormatName.Text,
                    DisplayName = LabelHelper.Create(primaryAttributeDisplayName),
                    Description = LabelHelper.Create(primaryAttributeDisplayName)
                }
            };

            var response = (CreateEntityResponse)_service.Execute(request);
            Console.WriteLine($"  ✓ 实体创建成功! ID: {response.EntityId}");
            return response.EntityId;
        }

        /// <summary>
        /// 更新实体显示名称（英文 1033 + 简体中文 2052）
        /// </summary>
        public void UpdateEntityDisplayName(string entityLogicalName, string displayName)
        {
            Console.WriteLine($"更新实体显示名称: {entityLogicalName} -> {displayName}");

            // 必须先 Retrieve 获取 MetadataId，否则 UpdateEntityRequest 不会生效
            var retrieveRequest = new RetrieveEntityRequest
            {
                EntityFilters = EntityFilters.Entity,
                LogicalName = entityLogicalName
            };
            var retrieveResponse = (RetrieveEntityResponse)_service.Execute(retrieveRequest);
            var metadataId = retrieveResponse.EntityMetadata.MetadataId;

            var entity = new EntityMetadata
            {
                MetadataId = metadataId,
                LogicalName = entityLogicalName,
                DisplayName = LabelHelper.Create(displayName),
                DisplayCollectionName = LabelHelper.Create(displayName),
                Description = LabelHelper.Create(displayName)
            };

            var request = new UpdateEntityRequest
            {
                Entity = entity,
                HasNotes = false,
                HasActivities = false
            };

            _service.Execute(request);
            Console.WriteLine($"  ✓ 实体显示名称更新成功");
        }

        /// <summary>
        /// 更新字段显示名称（多语言）
        /// </summary>
        public void UpdateAttributeDisplayName(string entityLogicalName, string attributeLogicalName, string displayName)
        {
            Console.WriteLine($"更新字段显示名称: {entityLogicalName}.{attributeLogicalName} -> {displayName}");

            var attribute = new AttributeMetadata
            {
                LogicalName = attributeLogicalName,
                DisplayName = LabelHelper.Create(displayName),
                Description = LabelHelper.Create(displayName)
            };

            var request = new UpdateAttributeRequest
            {
                EntityName = entityLogicalName,
                Attribute = attribute
            };

            _service.Execute(request);
            Console.WriteLine($"  ✓ 字段显示名称更新成功");
        }

        /// <summary>
        /// 删除实体（危险操作！）
        /// </summary>
        public void DeleteEntity(string entityName)
        {
            Console.WriteLine($"删除实体: {entityName}");
            
            var request = new DeleteEntityRequest
            {
                LogicalName = entityName
            };
            
            _service.Execute(request);
            Console.WriteLine($"  ✓ 实体已删除");
        }

        /// <summary>
        /// 检查实体是否存在
        /// </summary>
        public bool EntityExists(string entityName)
        {
            try
            {
                var request = new RetrieveEntityRequest
                {
                    EntityFilters = EntityFilters.Entity,
                    LogicalName = entityName
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
        /// 获取实体ID
        /// </summary>
        public Guid? GetEntityId(string entityName)
        {
            try
            {
                var request = new RetrieveEntityRequest
                {
                    EntityFilters = EntityFilters.Entity,
                    LogicalName = entityName
                };
                var response = (RetrieveEntityResponse)_service.Execute(request);
                return response.EntityMetadata.MetadataId;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 打印实体和主字段的显示名称（用于诊断标签问题）
        /// </summary>
        public void PrintEntityDisplayName(string entityName)
        {
            try
            {
                var request = new RetrieveEntityRequest
                {
                    EntityFilters = EntityFilters.Entity,
                    LogicalName = entityName
                };
                var response = (RetrieveEntityResponse)_service.Execute(request);
                var metadata = response.EntityMetadata;

                Console.WriteLine($"实体: {metadata.LogicalName}");
                Console.WriteLine($"  SchemaName: {metadata.SchemaName}");
                Console.WriteLine($"  DisplayName LocalizedLabels:");
                if (metadata.DisplayName?.LocalizedLabels != null)
                {
                    foreach (var label in metadata.DisplayName.LocalizedLabels)
                    {
                        Console.WriteLine($"    LCID={label.LanguageCode}, Label={label.Label}");
                    }
                }
                else
                {
                    Console.WriteLine("    (空)");
                }

                Console.WriteLine($"  DisplayCollectionName LocalizedLabels:");
                if (metadata.DisplayCollectionName?.LocalizedLabels != null)
                {
                    foreach (var label in metadata.DisplayCollectionName.LocalizedLabels)
                    {
                        Console.WriteLine($"    LCID={label.LanguageCode}, Label={label.Label}");
                    }
                }
                else
                {
                    Console.WriteLine("    (空)");
                }

                if (metadata.PrimaryNameAttribute != null)
                {
                    Console.WriteLine($"  PrimaryNameAttribute: {metadata.PrimaryNameAttribute}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"查询实体显示名称失败: {ex.Message}");
            }
        }

        #endregion

        #region 字段操作

        /// <summary>
        /// 创建字符串字段
        /// </summary>
        public void CreateStringField(string entityName, string schemaName, string displayName, string description, int maxLength = 100, bool required = false)
        {
            var request = new CreateAttributeRequest
            {
                EntityName = entityName,
                Attribute = new StringAttributeMetadata
                {
                    SchemaName = schemaName,
                    LogicalName = schemaName.ToLower(),
                    DisplayName = LabelHelper.Create(displayName),
                    RequiredLevel = new AttributeRequiredLevelManagedProperty(required ? AttributeRequiredLevel.ApplicationRequired : AttributeRequiredLevel.None),
                    Description = LabelHelper.Create(description),
                    MaxLength = maxLength
                }
            };

            _service.Execute(request);
            Console.WriteLine($"  ✓ {schemaName} ({displayName}) - 字符串({maxLength})");
        }

        /// <summary>
        /// 创建多行文本字段
        /// </summary>
        public void CreateMemoField(string entityName, string schemaName, string displayName, string description, int maxLength = 4000)
        {
            var request = new CreateAttributeRequest
            {
                EntityName = entityName,
                Attribute = new MemoAttributeMetadata
                {
                    SchemaName = schemaName,
                    LogicalName = schemaName.ToLower(),
                    DisplayName = LabelHelper.Create(displayName),
                    RequiredLevel = new AttributeRequiredLevelManagedProperty(AttributeRequiredLevel.None),
                    Description = LabelHelper.Create(description),
                    Format = StringFormat.TextArea,
                    MaxLength = maxLength
                }
            };

            _service.Execute(request);
            Console.WriteLine($"  ✓ {schemaName} ({displayName}) - 多行文本({maxLength})");
        }

        /// <summary>
        /// 创建整数字段
        /// </summary>
        public void CreateIntegerField(string entityName, string schemaName, string displayName, string description, int minValue = 0, int maxValue = 100)
        {
            var request = new CreateAttributeRequest
            {
                EntityName = entityName,
                Attribute = new IntegerAttributeMetadata
                {
                    SchemaName = schemaName,
                    LogicalName = schemaName.ToLower(),
                    DisplayName = LabelHelper.Create(displayName),
                    RequiredLevel = new AttributeRequiredLevelManagedProperty(AttributeRequiredLevel.None),
                    Description = LabelHelper.Create(description),
                    Format = IntegerFormat.None,
                    MaxValue = maxValue,
                    MinValue = minValue
                }
            };

            _service.Execute(request);
            Console.WriteLine($"  ✓ {schemaName} ({displayName}) - 整数");
        }

        /// <summary>
        /// 创建小数字段
        /// </summary>
        public void CreateDecimalField(string entityName, string schemaName, string displayName, string description, decimal minValue = 0, decimal maxValue = 999999.99m, int precision = 2)
        {
            var request = new CreateAttributeRequest
            {
                EntityName = entityName,
                Attribute = new DecimalAttributeMetadata
                {
                    SchemaName = schemaName,
                    LogicalName = schemaName.ToLower(),
                    DisplayName = LabelHelper.Create(displayName),
                    RequiredLevel = new AttributeRequiredLevelManagedProperty(AttributeRequiredLevel.None),
                    Description = LabelHelper.Create(description),
                    MaxValue = maxValue,
                    MinValue = minValue,
                    Precision = precision
                }
            };

            _service.Execute(request);
            Console.WriteLine($"  ✓ {schemaName} ({displayName}) - 小数({precision}位)");
        }

        /// <summary>
        /// 创建货币字段
        /// </summary>
        public void CreateMoneyField(string entityName, string schemaName, string displayName, string description, decimal minValue = 0, decimal maxValue = 1000000, int precision = 2)
        {
            var request = new CreateAttributeRequest
            {
                EntityName = entityName,
                Attribute = new MoneyAttributeMetadata
                {
                    SchemaName = schemaName,
                    LogicalName = schemaName.ToLower(),
                    DisplayName = LabelHelper.Create(displayName),
                    RequiredLevel = new AttributeRequiredLevelManagedProperty(AttributeRequiredLevel.None),
                    Description = LabelHelper.Create(description),
                    MaxValue = (double?)maxValue,
                    MinValue = (double?)minValue,
                    Precision = precision,
                    PrecisionSource = 1
                }
            };

            _service.Execute(request);
            Console.WriteLine($"  ✓ {schemaName} ({displayName}) - 货币");
        }

        /// <summary>
        /// 创建日期时间字段
        /// </summary>
        public void CreateDateTimeField(string entityName, string schemaName, string displayName, string description, bool dateOnly = true)
        {
            var request = new CreateAttributeRequest
            {
                EntityName = entityName,
                Attribute = new DateTimeAttributeMetadata
                {
                    SchemaName = schemaName,
                    LogicalName = schemaName.ToLower(),
                    DisplayName = LabelHelper.Create(displayName),
                    RequiredLevel = new AttributeRequiredLevelManagedProperty(AttributeRequiredLevel.None),
                    Description = LabelHelper.Create(description),
                    Format = dateOnly ? DateTimeFormat.DateOnly : DateTimeFormat.DateAndTime,
                    ImeMode = ImeMode.Disabled
                }
            };

            _service.Execute(request);
            Console.WriteLine($"  ✓ {schemaName} ({displayName}) - 日期{(dateOnly ? "" : "时间")}");
        }

        /// <summary>
        /// 创建选项集字段
        /// </summary>
        public void CreatePicklistField(string entityName, string schemaName, string displayName, string description, Dictionary<string, int> options)
        {
            var request = new CreateAttributeRequest
            {
                EntityName = entityName,
                Attribute = new PicklistAttributeMetadata
                {
                    SchemaName = schemaName,
                    LogicalName = schemaName.ToLower(),
                    DisplayName = LabelHelper.Create(displayName),
                    RequiredLevel = new AttributeRequiredLevelManagedProperty(AttributeRequiredLevel.None),
                    Description = LabelHelper.Create(description),
                    OptionSet = new OptionSetMetadata
                    {
                        IsGlobal = false,
                        OptionSetType = OptionSetType.Picklist
                    }
                }
            };
            
            foreach (var opt in options)
            {
                ((PicklistAttributeMetadata)request.Attribute).OptionSet.Options.Add(
                    new OptionMetadata(LabelHelper.Create(opt.Key), opt.Value));
            }

            _service.Execute(request);
            Console.WriteLine($"  ✓ {schemaName} ({displayName}) - 选项集({options.Count}项)");
        }

        /// <summary>
        /// 创建布尔字段
        /// </summary>
        public void CreateBooleanField(string entityName, string schemaName, string displayName, string description, string trueLabel = "是", string falseLabel = "否")
        {
            var request = new CreateAttributeRequest
            {
                EntityName = entityName,
                Attribute = new BooleanAttributeMetadata
                {
                    SchemaName = schemaName,
                    LogicalName = schemaName.ToLower(),
                    DisplayName = LabelHelper.Create(displayName),
                    RequiredLevel = new AttributeRequiredLevelManagedProperty(AttributeRequiredLevel.None),
                    Description = LabelHelper.Create(description),
                    OptionSet = new BooleanOptionSetMetadata(
                        new OptionMetadata(LabelHelper.Create(trueLabel), 1),
                        new OptionMetadata(LabelHelper.Create(falseLabel), 0)
                    )
                }
            };

            _service.Execute(request);
            Console.WriteLine($"  ✓ {schemaName} ({displayName}) - 布尔");
        }

        /// <summary>
        /// 创建 Lookup 字段（关联到其他实体）
        /// </summary>
        public void CreateLookupField(string entityName, string schemaName, string displayName, string description, string targetEntityName, string targetEntityDisplayName)
        {
            Console.WriteLine($"创建Lookup字段: {schemaName} -> {targetEntityName}");
            
            // 获取目标实体的主键名
            string targetPrimaryKey = $"{targetEntityName}id";
            
            // 创建一对多关系（Lookup字段）- ReferencingAttribute 由系统自动生成
            var request = new CreateOneToManyRequest
            {
                Lookup = new LookupAttributeMetadata
                {
                    SchemaName = schemaName,
                    LogicalName = schemaName.ToLower(),
                    DisplayName = LabelHelper.Create(displayName),
                    RequiredLevel = new AttributeRequiredLevelManagedProperty(AttributeRequiredLevel.None),
                    Description = LabelHelper.Create(description)
                },
                OneToManyRelationship = new OneToManyRelationshipMetadata
                {
                    SchemaName = $"{targetEntityName}_{entityName}_{schemaName}",
                    ReferencedEntity = targetEntityName,
                    ReferencingEntity = entityName,
                    ReferencedAttribute = targetPrimaryKey,
                    AssociatedMenuConfiguration = new AssociatedMenuConfiguration
                    {
                        Behavior = AssociatedMenuBehavior.UseCollectionName,
                        Group = AssociatedMenuGroup.Details,
                        Label = LabelHelper.Create(targetEntityDisplayName),
                        Order = 10000
                    },
                    CascadeConfiguration = new CascadeConfiguration
                    {
                        Assign = CascadeType.NoCascade,
                        Delete = CascadeType.RemoveLink,
                        Merge = CascadeType.NoCascade,
                        Reparent = CascadeType.NoCascade,
                        Share = CascadeType.NoCascade,
                        Unshare = CascadeType.NoCascade
                    }
                }
            };

            _service.Execute(request);
            Console.WriteLine($"  ✓ {schemaName} ({displayName}) - Lookup -> {targetEntityName}");
        }

        /// <summary>
        /// 更新选项集字段的选项值（插入新选项）
        /// </summary>
        public void InsertOptionValue(string entityName, string fieldName, string label, int value)
        {
            Console.WriteLine($"插入选项: {entityName}.{fieldName} -> {label} ({value})");
            
            var request = new InsertOptionValueRequest
            {
                EntityLogicalName = entityName,
                AttributeLogicalName = fieldName,
                Label = LabelHelper.Create(label),
                Value = value
            };
            
            _service.Execute(request);
            Console.WriteLine($"  ✓ 选项已插入");
        }

        /// <summary>
        /// 批量更新选项集字段选项值
        /// </summary>
        public void UpdatePicklistOptions(string entityName, string fieldName, Dictionary<string, int> options)
        {
            Console.WriteLine($"更新选项集: {entityName}.{fieldName}");
            
            foreach (var opt in options)
            {
                try
                {
                    InsertOptionValue(entityName, fieldName, opt.Key, opt.Value);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ⚠ {opt.Key}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 删除字段
        /// </summary>
        public void DeleteField(string entityName, string fieldName)
        {
            Console.WriteLine($"删除字段: {entityName}.{fieldName}");
            
            var request = new DeleteAttributeRequest
            {
                EntityLogicalName = entityName,
                LogicalName = fieldName
            };
            
            _service.Execute(request);
            Console.WriteLine($"  ✓ 字段已删除");
        }

        /// <summary>
        /// 获取实体的所有字段
        /// </summary>
        public List<AttributeMetadata> GetFields(string entityName)
        {
            var request = new RetrieveEntityRequest
            {
                EntityFilters = EntityFilters.Attributes,
                LogicalName = entityName
            };
            var response = (RetrieveEntityResponse)_service.Execute(request);
            return response.EntityMetadata.Attributes.ToList();
        }

        #endregion

        #region 解决方案操作

        /// <summary>
        /// 添加实体到解决方案
        /// </summary>
        public void AddEntityToSolution(string entityName, string solutionUniqueName)
        {
            Console.WriteLine($"添加实体 {entityName} 到解决方案 {solutionUniqueName}...");

            var entityId = GetEntityId(entityName);
            if (!entityId.HasValue)
            {
                throw new Exception($"实体 {entityName} 不存在");
            }

            var request = new AddSolutionComponentRequest
            {
                ComponentType = 1,  // Entity
                ComponentId = entityId.Value,
                SolutionUniqueName = solutionUniqueName,
                AddRequiredComponents = true
            };

            _service.Execute(request);
            Console.WriteLine($"  ✓ 添加成功!");
        }

        /// <summary>
        /// 从解决方案中移除实体（不会删除实体本身）
        /// </summary>
        public void RemoveEntityFromSolution(string entityName, string solutionUniqueName)
        {
            Console.WriteLine($"从解决方案移除实体 {entityName}...");

            // 查询解决方案ID
            var solutionQuery = new QueryExpression("solution")
            {
                ColumnSet = new ColumnSet("solutionid"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("uniquename", ConditionOperator.Equal, solutionUniqueName) }
                }
            };
            var solutions = _service.RetrieveMultiple(solutionQuery);
            if (solutions.Entities.Count == 0)
            {
                throw new Exception($"解决方案 {solutionUniqueName} 不存在");
            }

            Guid solutionId = solutions.Entities[0].Id;
            var entityId = GetEntityId(entityName);

            if (!entityId.HasValue)
            {
                throw new Exception($"实体 {entityName} 不存在");
            }

            // 查询解决方案组件
            var componentQuery = new QueryExpression("solutioncomponent")
            {
                ColumnSet = new ColumnSet("solutioncomponentid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("solutionid", ConditionOperator.Equal, solutionId),
                        new ConditionExpression("objectid", ConditionOperator.Equal, entityId.Value),
                        new ConditionExpression("componenttype", ConditionOperator.Equal, 1)
                    }
                }
            };

            var components = _service.RetrieveMultiple(componentQuery);
            foreach (var comp in components.Entities)
            {
                _service.Delete("solutioncomponent", comp.Id);
            }

            Console.WriteLine($"  ✓ 已从解决方案移除");
        }

        /// <summary>
        /// 获取解决方案中的所有实体
        /// </summary>
        public List<(string LogicalName, Guid EntityId)> GetSolutionEntities(string solutionUniqueName)
        {
            var result = new List<(string, Guid)>();

            // 查询解决方案ID
            var solutionQuery = new QueryExpression("solution")
            {
                ColumnSet = new ColumnSet("solutionid"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("uniquename", ConditionOperator.Equal, solutionUniqueName) }
                }
            };
            var solutions = _service.RetrieveMultiple(solutionQuery);
            if (solutions.Entities.Count == 0) return result;

            Guid solutionId = solutions.Entities[0].Id;

            // 查询实体组件
            var componentQuery = new QueryExpression("solutioncomponent")
            {
                ColumnSet = new ColumnSet("objectid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("solutionid", ConditionOperator.Equal, solutionId),
                        new ConditionExpression("componenttype", ConditionOperator.Equal, 1)
                    }
                }
            };

            var components = _service.RetrieveMultiple(componentQuery);
            foreach (var comp in components.Entities)
            {
                Guid objectId = (Guid)comp["objectid"];
                try
                {
                    var entityRequest = new RetrieveEntityRequest
                    {
                        EntityFilters = EntityFilters.Entity,
                        MetadataId = objectId
                    };
                    var entityResponse = (RetrieveEntityResponse)_service.Execute(entityRequest);
                    result.Add((entityResponse.EntityMetadata.LogicalName, objectId));
                }
                catch { }
            }

            return result;
        }

        #endregion

        #region 发布操作

        /// <summary>
        /// 发布所有自定义项
        /// </summary>
        public void PublishAll()
        {
            Console.WriteLine("发布所有自定义项...");
            var request = new PublishAllXmlRequest();
            _service.Execute(request);
            Console.WriteLine("  ✓ 发布完成");
        }

        /// <summary>
        /// 发布指定实体
        /// </summary>
        public void PublishEntity(string entityName)
        {
            Console.WriteLine($"发布实体: {entityName}");
            var request = new PublishXmlRequest
            {
                ParameterXml = $"<importexportxml><entities><entity>{entityName}</entity></entities></importexportxml>"
            };
            _service.Execute(request);
            Console.WriteLine("  ✓ 发布完成");
        }

        #endregion

        #region 导出解决方案

        /// <summary>
        /// 导出解决方案为 ZIP 文件
        /// </summary>
        public void ExportSolution(string solutionName, string exportPath)
        {
            Console.WriteLine($"导出解决方案: {solutionName}");
            
            var request = new ExportSolutionRequest
            {
                SolutionName = solutionName,
                Managed = false,
                ExportAutoNumberingSettings = false,
                ExportCalendarSettings = false,
                ExportCustomizationSettings = false,
                ExportEmailTrackingSettings = false,
                ExportGeneralSettings = false,
                ExportIsvConfig = false,
                ExportMarketingSettings = false,
                ExportOutlookSynchronizationSettings = false,
                ExportRelationshipRoles = false
            };
            
            var response = (ExportSolutionResponse)_service.Execute(request);
            
            File.WriteAllBytes(exportPath, response.ExportSolutionFile);
            Console.WriteLine($"  ✓ 导出完成: {exportPath}");
            Console.WriteLine($"  文件大小: {response.ExportSolutionFile.Length / 1024} KB");
        }

        /// <summary>
        /// 导出实体 Main 窗体的 FormXml
        /// </summary>
        public void ExportFormXml(string entityName, string outputPath)
        {
            var query = new QueryExpression("systemform")
            {
                ColumnSet = new ColumnSet("formxml", "name"),
                Criteria = new FilterExpression()
                {
                    Conditions = 
                    {
                        new ConditionExpression("objecttypecode", ConditionOperator.Equal, entityName),
                        new ConditionExpression("type", ConditionOperator.Equal, 2)
                    }
                }
            };
            
            var forms = _service.RetrieveMultiple(query);
            if (forms.Entities.Count == 0)
            {
                Console.WriteLine("未找到 Main 窗体");
                return;
            }
            
            var formXml = forms.Entities[0].GetAttributeValue<string>("formxml");
            File.WriteAllText(outputPath, formXml);
            Console.WriteLine($"FormXml 已导出到: {outputPath}");
        }

        /// <summary>
        /// 检查实体所有窗体的字段
        /// </summary>
        public void CheckFormFields(string entityName)
        {
            Console.WriteLine($"检查 {entityName} 的所有窗体...\n");
            
            var query = new QueryExpression("systemform")
            {
                ColumnSet = new ColumnSet("formid", "name", "type", "formxml"),
                Criteria = new FilterExpression()
                {
                    Conditions = 
                    {
                        new ConditionExpression("objecttypecode", ConditionOperator.Equal, entityName)
                    }
                }
            };
            
            var forms = _service.RetrieveMultiple(query);
            Console.WriteLine($"找到 {forms.Entities.Count} 个窗体\n");
            
            foreach (var form in forms.Entities)
            {
                var formId = form.GetAttributeValue<Guid>("formid");
                var name = form.GetAttributeValue<string>("name");
                var type = form.GetAttributeValue<OptionSetValue>("type")?.Value;
                var formXml = form.GetAttributeValue<string>("formxml");
                
                string typeName = type switch
                {
                    2 => "Main",
                    6 => "Mobile",
                    7 => "Dashboard",
                    11 => "Quick Create",
                    _ => $"Unknown({type})"
                };
                
                Console.WriteLine($"=== {name} (type={typeName}, id={formId}) ===");
                
                if (formXml != null)
                {
                    var matches = System.Text.RegularExpressions.Regex.Matches(formXml, "datafieldname=\"([^\"]+)\"");
                    Console.WriteLine($"字段数量: {matches.Count}");
                    foreach (System.Text.RegularExpressions.Match m in matches)
                    {
                        Console.WriteLine($"  - {m.Groups[1].Value}");
                    }
                }
                Console.WriteLine();
            }
        }

        /// <summary>
        /// 清理窗体 footer 中不存在的字段
        /// </summary>
        public void CleanFormFooter(string entityName)
        {
            Console.WriteLine($"清理 {entityName} 窗体 footer...");
            
            var query = new QueryExpression("systemform")
            {
                ColumnSet = new ColumnSet("formxml", "name"),
                Criteria = new FilterExpression()
                {
                    Conditions = 
                    {
                        new ConditionExpression("objecttypecode", ConditionOperator.Equal, entityName),
                        new ConditionExpression("type", ConditionOperator.Equal, 2)
                    }
                }
            };
            
            var forms = _service.RetrieveMultiple(query);
            if (forms.Entities.Count == 0) return;
            
            foreach (var form in forms.Entities)
            {
                string formXml = form.GetAttributeValue<string>("formxml");
                
                // 找到 footer 开始位置
                int footerPos = formXml.IndexOf("<footer");
                if (footerPos < 0) continue;
                
                // 提取 footer 内容
                string footerSection = formXml.Substring(footerPos);
                
                // 找到 footer 里的 <rows> 和 </rows>
                int footerRowsStart = footerSection.IndexOf("<rows>");
                int footerRowsEnd = footerSection.IndexOf("</rows>");
                if (footerRowsStart < 0 || footerRowsEnd < 0) continue;
                
                string footerRows = footerSection.Substring(footerRowsStart + "<rows>".Length, 
                    footerRowsEnd - footerRowsStart - "<rows>".Length);
                
                // 检查 footer rows 里是否有 datafieldname 的字段（这些是我们错误添加的）
                // 保留没有 datafieldname 的空白 cell，移除有 datafieldname 的 row
                var rowMatches = System.Text.RegularExpressions.Regex.Matches(footerRows, "<row>.*?</row>", System.Text.RegularExpressions.RegexOptions.Singleline);
                string cleanFooterRows = "";
                int removedCount = 0;
                
                foreach (System.Text.RegularExpressions.Match m in rowMatches)
                {
                    string row = m.Value;
                    // 如果这个 row 包含 datafieldname，说明是我们错误添加的字段，移除
                    if (row.Contains("datafieldname="))
                    {
                        removedCount++;
                        Console.WriteLine($"  移除 footer 中的错误字段");
                    }
                    else
                    {
                        cleanFooterRows += row;
                    }
                }
                
                if (removedCount > 0)
                {
                    // 重建 footer
                    string newFooterSection = footerSection.Substring(0, footerRowsStart + "<rows>".Length)
                        + cleanFooterRows
                        + footerSection.Substring(footerRowsEnd);
                    
                    string newFormXml = formXml.Substring(0, footerPos) + newFooterSection;
                    
                    var updateForm = new Entity("systemform", form.Id);
                    updateForm["formxml"] = newFormXml;
                    _service.Update(updateForm);
                    
                    Console.WriteLine($"  ✓ 清理完成，移除了 {removedCount} 个错误字段");
                }
                else
                {
                    Console.WriteLine("  无需清理");
                }
            }
        }

        /// <summary>
        /// 更新实体主窗体，添加字段（支持两列布局）
        /// </summary>
        public void UpdateMainForm(string entityName, Dictionary<string, string> fields)
        {
            Console.WriteLine($"更新 {entityName} 主窗体...");
            
            var query = new QueryExpression("systemform")
            {
                ColumnSet = new ColumnSet("formxml", "name", "type"),
                Criteria = new FilterExpression()
                {
                    Conditions = 
                    {
                        new ConditionExpression("objecttypecode", ConditionOperator.Equal, entityName),
                        new ConditionExpression("type", ConditionOperator.Equal, 2)
                    }
                }
            };
            
            var forms = _service.RetrieveMultiple(query);
            if (forms.Entities.Count == 0)
            {
                Console.WriteLine("  ✗ 未找到 type=2 的主窗体");
                return;
            }
            
            foreach (var form in forms.Entities)
            {
                string formXml = form.GetAttributeValue<string>("formxml");
                string formName = form.GetAttributeValue<string>("name");
                Console.WriteLine($"  处理窗体: {formName}");
                
                // 检查字段是否已存在
                int addedCount = 0;
                var newFields = new Dictionary<string, string>();
                foreach (var field in fields)
                {
                    if (formXml.Contains($"datafieldname=\"{field.Key}\""))
                    {
                        Console.WriteLine($"    ⊘ {field.Key} 已存在，跳过");
                        continue;
                    }
                    newFields[field.Key] = field.Value;
                    addedCount++;
                }
                
                if (addedCount == 0)
                {
                    Console.WriteLine("  没有新字段需要添加");
                    continue;
                }
                
                // 构建新字段 XML（两列布局）
                string newRowsXml = "";
                var fieldList = new List<KeyValuePair<string, string>>(newFields);
                for (int i = 0; i < fieldList.Count; i += 2)
                {
                    string cellId1 = Guid.NewGuid().ToString("B");
                    string cellId2 = Guid.NewGuid().ToString("B");
                    
                    newRowsXml += "<row>";
                    
                    // 左列
                    newRowsXml += $"<cell id=\"{cellId1}\" colspan=\"1\"><labels><label description=\"{fieldList[i].Value}\" languagecode=\"2052\" /></labels><control id=\"{fieldList[i].Key}\" classid=\"{{4273EDBD-AC1D-40d3-9FB2-095C621B552D}}\" datafieldname=\"{fieldList[i].Key}\" /></cell>";
                    
                    // 右列（如果有）
                    if (i + 1 < fieldList.Count)
                    {
                        newRowsXml += $"<cell id=\"{cellId2}\" colspan=\"1\"><labels><label description=\"{fieldList[i+1].Value}\" languagecode=\"2052\" /></labels><control id=\"{fieldList[i+1].Key}\" classid=\"{{4273EDBD-AC1D-40d3-9FB2-095C621B552D}}\" datafieldname=\"{fieldList[i+1].Key}\" /></cell>";
                    }
                    
                    newRowsXml += "</row>";
                    Console.WriteLine($"    + {fieldList[i].Key} + {((i+1 < fieldList.Count) ? fieldList[i+1].Key : "")}");
                }
                
                // 在第一个字段的 </row> 后插入新字段
                int firstControlPos = formXml.IndexOf("<control id=");
                int rowEndPos = formXml.IndexOf("</row>", firstControlPos);
                if (rowEndPos > 0)
                {
                    rowEndPos += "</row>".Length;
                    string newFormXml = formXml.Insert(rowEndPos, newRowsXml);
                    
                    var updateForm = new Entity("systemform", form.Id);
                    updateForm["formxml"] = newFormXml;
                    _service.Update(updateForm);
                    
                    Console.WriteLine($"  ✓ 窗体已更新，添加了 {addedCount} 个字段（两列布局）");
                }
            }
            
            // 发布实体
            Console.WriteLine("  发布实体...");
            var publishRequest = new PublishXmlRequest
            {
                ParameterXml = $"<importexportxml><entities><entity>{entityName}</entity></entities></importexportxml>"
            };
            _service.Execute(publishRequest);
            Console.WriteLine("  ✓ 发布完成");
        }

        /// <summary>
        /// 获取实体关联的Lookup字段的目标实体名称
        /// </summary>
        private string GetLookupTargetEntity(string sourceEntity, string fieldName)
        {
            // 根据字段名推断目标实体（基于项目命名约定）
            var lookupMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["mcs_credit_record"] = "mcs_credit_record",
                ["mcs_accountid"] = "account",
                ["mcs_credit_item"] = "mcs_credit_items",
                ["mcs_credititem"] = "mcs_credit_items",
                ["mcs_listvalue"] = "mcs_credititem_value",
            };
            
            if (lookupMap.TryGetValue(fieldName, out string target))
                return target;
            
            return null;
        }
        
        /// <summary>
        /// 获取实体的默认Lookup视图ID (querytype=64)
        /// </summary>
        private string GetLookupViewId(string entityName)
        {
            var query = new QueryExpression("savedquery")
            {
                ColumnSet = new ColumnSet("savedqueryid", "name"),
                Criteria = new FilterExpression()
                {
                    Conditions = 
                    {
                        new ConditionExpression("returnedtypecode", ConditionOperator.Equal, entityName),
                        new ConditionExpression("querytype", ConditionOperator.Equal, 64)
                    }
                }
            };
            
            var views = _service.RetrieveMultiple(query);
            if (views.Entities.Count > 0)
            {
                return views.Entities[0].GetAttributeValue<Guid>("savedqueryid").ToString("B");
            }
            return null;
        }

        /// <summary>
        /// 生成Lookup控件的parameters XML
        /// </summary>
        private string BuildLookupParameters(string fieldName, string viewId, Dictionary<string, (string dependentField, string dependentEntity, string filterRelationship)> lookupFilterMap)
        {
            string parameters = "<parameters>";
            
            // 添加DefaultViewId和AvailableViewIds
            if (!string.IsNullOrEmpty(viewId))
            {
                parameters += $"<DefaultViewId>{viewId}</DefaultViewId><AvailableViewIds>{viewId}</AvailableViewIds>";
            }
            
            // 添加筛选参数
            if (lookupFilterMap != null && lookupFilterMap.TryGetValue(fieldName, out var filterConfig))
            {
                if (!string.IsNullOrEmpty(filterConfig.dependentField))
                    parameters += $"<DependentAttributeName>{filterConfig.dependentField}</DependentAttributeName>";
                if (!string.IsNullOrEmpty(filterConfig.dependentEntity))
                    parameters += $"<DependentAttributeType>{filterConfig.dependentEntity}</DependentAttributeType>";
                if (!string.IsNullOrEmpty(filterConfig.filterRelationship))
                    parameters += $"<FilterRelationshipName>{filterConfig.filterRelationship}</FilterRelationshipName>";
            }
            
            parameters += "</parameters>";
            return parameters;
        }

        /// <summary>
        /// 重新排列窗体字段（两列布局 + 分组）
        /// </summary>
        public void RearrangeForm(string entityName, Dictionary<string, List<(string fieldName, string displayName)>> fieldGroups, HashSet<string> lookupFields = null, Dictionary<string, (string dependentField, string dependentEntity, string filterRelationship)> lookupFilterMap = null)
        {
            Console.WriteLine($"重新排列 {entityName} 窗体...");
            
            var query = new QueryExpression("systemform")
            {
                ColumnSet = new ColumnSet("formxml", "name"),
                Criteria = new FilterExpression()
                {
                    Conditions = 
                    {
                        new ConditionExpression("objecttypecode", ConditionOperator.Equal, entityName),
                        new ConditionExpression("type", ConditionOperator.Equal, 2)
                    }
                }
            };
            
            var forms = _service.RetrieveMultiple(query);
            if (forms.Entities.Count == 0)
            {
                Console.WriteLine("  ✗ 未找到主窗体");
                return;
            }
            
            foreach (var form in forms.Entities)
            {
                string formXml = form.GetAttributeValue<string>("formxml");
                
                // 构建新的sections
                string newSectionsXml = "";
                foreach (var group in fieldGroups)
                {
                    string sectionId = Guid.NewGuid().ToString("B");
                    string sectionName = group.Key;
                    var fields = group.Value;
                    
                    newSectionsXml += $"<section showlabel=\"true\" showbar=\"true\" IsUserDefined=\"1\" id=\"{sectionId}\" layout=\"varwidth\" celllabelalignment=\"Left\" celllabelposition=\"Left\" columns=\"11\" labelwidth=\"115\">";
                    newSectionsXml += $"<labels><label description=\"{sectionName}\" languagecode=\"2052\" /></labels>";
                    newSectionsXml += "<rows>";
                    
                    // 两列排列
                    for (int i = 0; i < fields.Count; i += 2)
                    {
                        string cellId1 = Guid.NewGuid().ToString("B");
                        string cellId2 = Guid.NewGuid().ToString("B");
                        
                        newSectionsXml += "<row>";
                        bool isLookup1 = lookupFields != null && lookupFields.Contains(fields[i].fieldName);
                        string classid1 = isLookup1 
                            ? "{270BD3DB-D9AF-4782-9025-509E298DEC0A}" 
                            : "{4273EDBD-AC1D-40d3-9FB2-095C621B552D}";
                        
                        if (isLookup1)
                        {
                            string targetEntity = GetLookupTargetEntity(entityName, fields[i].fieldName);
                            string viewId = targetEntity != null ? GetLookupViewId(targetEntity) : null;
                            string parameters = BuildLookupParameters(fields[i].fieldName, viewId, lookupFilterMap);
                            if (parameters != "<parameters></parameters>")
                            {
                                newSectionsXml += $"<cell id=\"{cellId1}\" colspan=\"1\"><labels><label description=\"{fields[i].displayName}\" languagecode=\"2052\" /></labels><control id=\"{fields[i].fieldName}\" classid=\"{classid1}\" datafieldname=\"{fields[i].fieldName}\">{parameters}</control></cell>";
                            }
                            else
                            {
                                newSectionsXml += $"<cell id=\"{cellId1}\" colspan=\"1\"><labels><label description=\"{fields[i].displayName}\" languagecode=\"2052\" /></labels><control id=\"{fields[i].fieldName}\" classid=\"{classid1}\" datafieldname=\"{fields[i].fieldName}\" /></cell>";
                            }
                        }
                        else
                        {
                            newSectionsXml += $"<cell id=\"{cellId1}\" colspan=\"1\"><labels><label description=\"{fields[i].displayName}\" languagecode=\"2052\" /></labels><control id=\"{fields[i].fieldName}\" classid=\"{classid1}\" datafieldname=\"{fields[i].fieldName}\" /></cell>";
                        }
                        
                        if (i + 1 < fields.Count)
                        {
                            bool isLookup2 = lookupFields != null && lookupFields.Contains(fields[i+1].fieldName);
                            string classid2 = isLookup2 
                                ? "{270BD3DB-D9AF-4782-9025-509E298DEC0A}" 
                                : "{4273EDBD-AC1D-40d3-9FB2-095C621B552D}";
                            
                            if (isLookup2)
                            {
                                string targetEntity2 = GetLookupTargetEntity(entityName, fields[i+1].fieldName);
                                string viewId2 = targetEntity2 != null ? GetLookupViewId(targetEntity2) : null;
                                string parameters2 = BuildLookupParameters(fields[i+1].fieldName, viewId2, lookupFilterMap);
                                if (parameters2 != "<parameters></parameters>")
                                {
                                    newSectionsXml += $"<cell id=\"{cellId2}\" colspan=\"1\"><labels><label description=\"{fields[i+1].displayName}\" languagecode=\"2052\" /></labels><control id=\"{fields[i+1].fieldName}\" classid=\"{classid2}\" datafieldname=\"{fields[i+1].fieldName}\">{parameters2}</control></cell>";
                                }
                                else
                                {
                                    newSectionsXml += $"<cell id=\"{cellId2}\" colspan=\"1\"><labels><label description=\"{fields[i+1].displayName}\" languagecode=\"2052\" /></labels><control id=\"{fields[i+1].fieldName}\" classid=\"{classid2}\" datafieldname=\"{fields[i+1].fieldName}\" /></cell>";
                                }
                            }
                            else
                            {
                                newSectionsXml += $"<cell id=\"{cellId2}\" colspan=\"1\"><labels><label description=\"{fields[i+1].displayName}\" languagecode=\"2052\" /></labels><control id=\"{fields[i+1].fieldName}\" classid=\"{classid2}\" datafieldname=\"{fields[i+1].fieldName}\" /></cell>";
                            }
                        }
                        
                        newSectionsXml += "</row>";
                    }
                    
                    newSectionsXml += "</rows></section>";
                }
                
                // 替换原formXml中的sections
                int sectionsStart = formXml.IndexOf("<sections>");
                int sectionsEnd = formXml.IndexOf("</sections>");
                if (sectionsStart > 0 && sectionsEnd > 0)
                {
                    string newFormXml = formXml.Substring(0, sectionsStart + "<sections>".Length)
                        + newSectionsXml
                        + formXml.Substring(sectionsEnd);
                    
                    var updateForm = new Entity("systemform", form.Id);
                    updateForm["formxml"] = newFormXml;
                    _service.Update(updateForm);
                    
                    Console.WriteLine($"  ✓ 窗体已重新排列，{fieldGroups.Count} 个分组");
                }
            }
            
            // 发布实体
            Console.WriteLine("  发布实体...");
            var publishRequest = new PublishXmlRequest
            {
                ParameterXml = $"<importexportxml><entities><entity>{entityName}</entity></entities></importexportxml>"
            };
            _service.Execute(publishRequest);
            Console.WriteLine("  ✓ 发布完成");
        }

        #endregion

        #region 视图管理

        /// <summary>
        /// 检查实体的所有视图
        /// </summary>
        public void CheckViews(string entityName)
        {
            Console.WriteLine($"检查 {entityName} 的所有视图...\n");
            
            var query = new QueryExpression("savedquery")
            {
                ColumnSet = new ColumnSet("savedqueryid", "name", "querytype", "isdefault", "layoutxml"),
                Criteria = new FilterExpression()
                {
                    Conditions = 
                    {
                        new ConditionExpression("returnedtypecode", ConditionOperator.Equal, entityName)
                    }
                }
            };
            
            var views = _service.RetrieveMultiple(query);
            Console.WriteLine($"找到 {views.Entities.Count} 个视图\n");
            
            foreach (var view in views.Entities)
            {
                var id = view.GetAttributeValue<Guid>("savedqueryid");
                var name = view.GetAttributeValue<string>("name");
                var qtype = view.GetAttributeValue<int>("querytype");
                var isdefault = view.GetAttributeValue<bool>("isdefault");
                var layoutXml = view.GetAttributeValue<string>("layoutxml");
                
                string typeName = qtype switch
                {
                    0 => "Public",
                    1 => "Private",
                    2 => "Offline",
                    4 => "Lookup",
                    _ => $"Unknown({qtype})"
                };
                
                Console.WriteLine($"=== {name} ===");
                Console.WriteLine($"  ID: {id}");
                Console.WriteLine($"  Type: {typeName} {(isdefault ? "[DEFAULT]" : "")}");
                
                if (layoutXml != null)
                {
                    var matches = System.Text.RegularExpressions.Regex.Matches(layoutXml, "name=\"([^\"]+)\"");
                    Console.WriteLine($"  列数: {matches.Count}");
                    foreach (System.Text.RegularExpressions.Match m in matches)
                    {
                        Console.WriteLine($"    - {m.Groups[1].Value}");
                    }
                }
                Console.WriteLine();
            }
        }

        /// <summary>
        /// 更新实体默认Public视图，添加字段列
        /// </summary>
        public void UpdateDefaultView(string entityName, Dictionary<string, string> fields)
        {
            Console.WriteLine($"更新 {entityName} 默认视图...");
            
            // 查询默认Public视图 (querytype=0, isdefault=true)
            var query = new QueryExpression("savedquery")
            {
                ColumnSet = new ColumnSet("savedqueryid", "name", "layoutxml", "fetchxml"),
                Criteria = new FilterExpression()
                {
                    Conditions = 
                    {
                        new ConditionExpression("returnedtypecode", ConditionOperator.Equal, entityName),
                        new ConditionExpression("querytype", ConditionOperator.Equal, 0),
                        new ConditionExpression("isdefault", ConditionOperator.Equal, true)
                    }
                }
            };
            
            var views = _service.RetrieveMultiple(query);
            if (views.Entities.Count == 0)
            {
                Console.WriteLine("  ✗ 未找到默认Public视图");
                return;
            }
            
            foreach (var view in views.Entities)
            {
                string layoutXml = view.GetAttributeValue<string>("layoutxml");
                string fetchXml = view.GetAttributeValue<string>("fetchxml");
                string viewName = view.GetAttributeValue<string>("name");
                Console.WriteLine($"  视图: {viewName}");
                
                // 构建新列 XML (layoutxml)
                string newCellsXml = "";
                int layoutAddedCount = 0;
                
                // 构建fetchxml中的attribute节点
                string newFetchAttrsXml = "";
                int fetchAddedCount = 0;
                
                foreach (var field in fields)
                {
                    string fieldName = field.Key;
                    string displayName = field.Value;
                    
                    // 检查layoutxml是否已存在
                    if (layoutXml.Contains($"name=\"{fieldName}\""))
                    {
                        Console.WriteLine($"    ⊘ layoutxml {fieldName} 已存在，跳过");
                    }
                    else
                    {
                        // 构建 cell XML - 视图列格式
                        newCellsXml += $"<cell name=\"{fieldName}\" width=\"150\" />";
                        Console.WriteLine($"    + layoutxml {fieldName} ({displayName})");
                        layoutAddedCount++;
                    }
                    
                    // 检查fetchxml是否已存在
                    if (fetchXml.Contains($"name=\"{fieldName}\""))
                    {
                        Console.WriteLine($"    ⊘ fetchxml {fieldName} 已存在，跳过");
                    }
                    else
                    {
                        // 构建 attribute XML - fetch查询字段
                        newFetchAttrsXml += $"<attribute name=\"{fieldName}\" />";
                        Console.WriteLine($"    + fetchxml {fieldName} ({displayName})");
                        fetchAddedCount++;
                    }
                }
                
                var updateView = new Entity("savedquery", view.Id);
                bool needUpdate = false;
                
                // 更新layoutxml
                if (layoutAddedCount > 0)
                {
                    int insertPos = layoutXml.LastIndexOf("</row>");
                    if (insertPos > 0)
                    {
                        string newLayoutXml = layoutXml.Insert(insertPos, newCellsXml);
                        updateView["layoutxml"] = newLayoutXml;
                        needUpdate = true;
                        Console.WriteLine($"  layoutxml: 添加 {layoutAddedCount} 列");
                    }
                }
                
                // 更新fetchxml
                if (fetchAddedCount > 0)
                {
                    // 在 </entity> 前插入attribute节点（在order之后，filter之前）
                    int insertPos = fetchXml.LastIndexOf("<order");
                    if (insertPos > 0)
                    {
                        // 找到order节点的结束位置
                        int orderEndPos = fetchXml.IndexOf(">", insertPos);
                        if (orderEndPos > 0)
                        {
                            // 如果是自闭合标签，在>后插入；否则在</order>后插入
                            if (fetchXml[orderEndPos - 1] == '/')
                            {
                                string newFetchXml = fetchXml.Insert(orderEndPos + 1, newFetchAttrsXml);
                                updateView["fetchxml"] = newFetchXml;
                                needUpdate = true;
                                Console.WriteLine($"  fetchxml: 添加 {fetchAddedCount} 个attribute");
                            }
                            else
                            {
                                int closeOrderPos = fetchXml.IndexOf("</order>", orderEndPos);
                                if (closeOrderPos > 0)
                                {
                                    string newFetchXml = fetchXml.Insert(closeOrderPos + 8, newFetchAttrsXml);
                                    updateView["fetchxml"] = newFetchXml;
                                    needUpdate = true;
                                    Console.WriteLine($"  fetchxml: 添加 {fetchAddedCount} 个attribute");
                                }
                            }
                        }
                    }
                    else
                    {
                        // 没有order节点，在<entity>后插入
                        int entityEndPos = fetchXml.IndexOf(">", fetchXml.IndexOf("<entity"));
                        if (entityEndPos > 0)
                        {
                            string newFetchXml = fetchXml.Insert(entityEndPos + 1, newFetchAttrsXml);
                            updateView["fetchxml"] = newFetchXml;
                            needUpdate = true;
                            Console.WriteLine($"  fetchxml: 添加 {fetchAddedCount} 个attribute");
                        }
                    }
                }
                
                if (needUpdate)
                {
                    _service.Update(updateView);
                    Console.WriteLine($"  ✓ 视图已更新");
                }
                else
                {
                    Console.WriteLine("  没有需要更新的内容");
                }
            }
            
            // 发布实体
            Console.WriteLine("  发布实体...");
            var publishRequest = new PublishXmlRequest
            {
                ParameterXml = $"<importexportxml><entities><entity>{entityName}</entity></entities></importexportxml>"
            };
            _service.Execute(publishRequest);
            Console.WriteLine("  ✓ 发布完成");
        }

        /// <summary>
        /// 从所有视图中移除指定字段
        /// </summary>
        public void RemoveFieldFromViews(string entityName, string fieldName)
        {
            Console.WriteLine($"从视图中移除字段: {entityName}.{fieldName}");
            
            // 查询该实体的所有视图
            var query = new QueryExpression("savedquery")
            {
                ColumnSet = new ColumnSet("savedqueryid", "name", "layoutxml", "fetchxml"),
                Criteria = new FilterExpression()
                {
                    Conditions = 
                    {
                        new ConditionExpression("returnedtypecode", ConditionOperator.Equal, entityName)
                    }
                }
            };
            
            var views = _service.RetrieveMultiple(query);
            int updatedCount = 0;
            
            foreach (var view in views.Entities)
            {
                string layoutXml = view.GetAttributeValue<string>("layoutxml") ?? "";
                string fetchXml = view.GetAttributeValue<string>("fetchxml") ?? "";
                string viewName = view.GetAttributeValue<string>("name") ?? "";
                
                bool needUpdate = false;
                
                // 从layoutxml中移除字段列
                if (layoutXml.Contains($"name=\"{fieldName}\""))
                {
                    // 移除 <cell name="fieldName" ... /> 节点
                    var cellPattern = $"<cell[^>]*name=\"{fieldName}\"[^/]*/>";
                    layoutXml = System.Text.RegularExpressions.Regex.Replace(layoutXml, cellPattern, "");
                    needUpdate = true;
                    Console.WriteLine($"  从视图 '{viewName}' 的layoutxml中移除 {fieldName}");
                }
                
                // 从fetchxml中移除字段attribute
                if (fetchXml.Contains($"name=\"{fieldName}\""))
                {
                    // 移除 <attribute name="fieldName" /> 节点
                    var attrPattern = $"<attribute[^>]*name=\"{fieldName}\"[^/]*/>";
                    fetchXml = System.Text.RegularExpressions.Regex.Replace(fetchXml, attrPattern, "");
                    needUpdate = true;
                    Console.WriteLine($"  从视图 '{viewName}' 的fetchxml中移除 {fieldName}");
                }
                
                if (needUpdate)
                {
                    var updateView = new Entity("savedquery", view.Id);
                    updateView["layoutxml"] = layoutXml;
                    updateView["fetchxml"] = fetchXml;
                    _service.Update(updateView);
                    updatedCount++;
                }
            }
            
            Console.WriteLine($"  ✓ 已更新 {updatedCount} 个视图");
            
            // 发布实体
            Console.WriteLine("  发布实体...");
            var publishRequest = new PublishXmlRequest
            {
                ParameterXml = $"<importexportxml><entities><entity>{entityName}</entity></entities></importexportxml>"
            };
            _service.Execute(publishRequest);
            Console.WriteLine("  ✓ 发布完成");
        }

        #endregion

        #region WebResource管理

        /// <summary>
        /// 创建或更新WebResource（JS文件）
        /// </summary>
        public Guid DeployWebResource(string name, string displayName, string filePath, string solutionName)
        {
            return DeployWebResource(name, displayName, filePath, solutionName, 3); // 3 = JScript
        }

        /// <summary>
        /// 创建或更新WebResource（支持指定类型：1=HTML, 3=JScript）
        /// </summary>
        public Guid DeployWebResource(string name, string displayName, string filePath, string solutionName, int resourceType)
        {
            string typeName = resourceType == 1 ? "HTML" : (resourceType == 3 ? "JScript" : "Unknown");
            Console.WriteLine($"部署WebResource [{typeName}]: {name}...");
            
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"  ✗ 文件不存在: {filePath}");
                return Guid.Empty;
            }
            
            var content = File.ReadAllBytes(filePath);
            var base64Content = Convert.ToBase64String(content);
            
            // 检查是否已存在
            var query = new QueryExpression("webresource")
            {
                ColumnSet = new ColumnSet("webresourceid", "name"),
                Criteria = new FilterExpression()
                {
                    Conditions = 
                    {
                        new ConditionExpression("name", ConditionOperator.Equal, name)
                    }
                }
            };
            
            var existing = _service.RetrieveMultiple(query);
            Guid webResourceId;
            
            if (existing.Entities.Count > 0)
            {
                // 更新
                webResourceId = existing.Entities[0].Id;
                var updateResource = new Entity("webresource", webResourceId);
                updateResource["content"] = base64Content;
                _service.Update(updateResource);
                Console.WriteLine($"  ✓ WebResource已更新 (ID: {webResourceId})");
            }
            else
            {
                // 创建
                var newResource = new Entity("webresource");
                newResource["name"] = name;
                newResource["displayname"] = displayName;
                newResource["webresourcetype"] = new OptionSetValue(resourceType);
                newResource["content"] = base64Content;
                webResourceId = _service.Create(newResource);
                Console.WriteLine($"  ✓ WebResource已创建 (ID: {webResourceId})");
            }
            
            // 添加到解决方案
            if (!string.IsNullOrEmpty(solutionName) && webResourceId != Guid.Empty)
            {
                try
                {
                    AddWebResourceToSolution(webResourceId, solutionName);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ⚠ 添加到解决方案失败: {ex.Message}");
                }
            }
            
            return webResourceId;
        }

        /// <summary>
        /// 将WebResource添加到解决方案
        /// </summary>
        private void AddWebResourceToSolution(Guid webResourceId, string solutionName)
        {
            // 获取解决方案ID
            var solutionQuery = new QueryExpression("solution")
            {
                ColumnSet = new ColumnSet("solutionid"),
                Criteria = new FilterExpression()
                {
                    Conditions = 
                    {
                        new ConditionExpression("uniquename", ConditionOperator.Equal, solutionName)
                    }
                }
            };
            
            var solutions = _service.RetrieveMultiple(solutionQuery);
            if (solutions.Entities.Count == 0)
            {
                Console.WriteLine($"  ⚠ 解决方案 {solutionName} 未找到");
                return;
            }
            
            var solutionId = solutions.Entities[0].GetAttributeValue<Guid>("solutionid");
            
            // 使用AddSolutionComponentRequest
            var addRequest = new AddSolutionComponentRequest
            {
                ComponentType = 61, // WebResource
                ComponentId = webResourceId,
                SolutionUniqueName = solutionName
            };
            
            _service.Execute(addRequest);
            Console.WriteLine($"  ✓ 已添加到解决方案 {solutionName}");
        }

        /// <summary>
        /// 将JS WebResource绑定到表单事件
        /// </summary>
        public void BindJsToForm(string entityName, string webResourceName, string formName)
        {
            Console.WriteLine($"绑定JS到表单: {entityName} / {formName}...");
            
            // 1. 获取WebResource ID
            var wrQuery = new QueryExpression("webresource")
            {
                ColumnSet = new ColumnSet("webresourceid"),
                Criteria = new FilterExpression()
                {
                    Conditions = 
                    {
                        new ConditionExpression("name", ConditionOperator.Equal, webResourceName)
                    }
                }
            };
            
            var wrResult = _service.RetrieveMultiple(wrQuery);
            if (wrResult.Entities.Count == 0)
            {
                Console.WriteLine($"  ✗ WebResource {webResourceName} 未找到");
                return;
            }
            
            var webResourceId = wrResult.Entities[0].Id;
            
            // 2. 获取表单
            var formQuery = new QueryExpression("systemform")
            {
                ColumnSet = new ColumnSet("formid", "name", "formxml"),
                Criteria = new FilterExpression()
                {
                    Conditions = 
                    {
                        new ConditionExpression("objecttypecode", ConditionOperator.Equal, entityName),
                        new ConditionExpression("type", ConditionOperator.Equal, 2),
                        new ConditionExpression("name", ConditionOperator.Equal, formName)
                    }
                }
            };
            
            var forms = _service.RetrieveMultiple(formQuery);
            if (forms.Entities.Count == 0)
            {
                Console.WriteLine($"  ✗ 表单 {formName} 未找到");
                return;
            }
            
            var form = forms.Entities[0];
            var formXml = form.GetAttributeValue<string>("formxml");
            
            // 根据实体名确定JS函数前缀
            string jsPrefix = entityName switch
            {
                "mcs_credit_items" => "CreditItemsForm",
                "mcs_credit_scoringcard" => "ScoringCardForm",
                "mcs_credit_record" => "CreditRecordForm",
                "mcs_customer_tag" => "CustomerTagForm",
                "mcs_credititem_value" => "CreditItemValueForm",
                "account" => "AccountForm",
                _ => "ScoringCardForm"
            };
            
            // 3. 检查是否已绑定
            if (formXml.Contains($"library name=\"{webResourceName}\""))
            {
                // 已绑定，检查函数名是否正确
                if (!formXml.Contains($"{jsPrefix}.onLoad"))
                {
                    // 函数名不匹配，更新events中的函数名
                    formXml = formXml.Replace("ScoringCardForm.onLoad", $"{jsPrefix}.onLoad");
                    formXml = formXml.Replace("ScoringCardForm.onSave", $"{jsPrefix}.onSave");
                    
                    // 更新表单
                    var updateForm2 = new Entity("systemform", form.Id);
                    updateForm2["formxml"] = formXml;
                    _service.Update(updateForm2);
                    
                    // 发布
                    var publishRequest2 = new PublishXmlRequest();
                    publishRequest2.ParameterXml = $"<importexportxml><entities><entity>{entityName}</entity></entities><nodes/><securityroles/><settings/><workflows/></importexportxml>";
                    _service.Execute(publishRequest2);
                    
                    Console.WriteLine($"  ✓ JS函数名已更新为 {jsPrefix}");
                    return;
                }
                Console.WriteLine($"  ⊘ JS已绑定，跳过");
                return;
            }
            
            // 4. 在formXml中添加JS引用和事件
            // 添加formLibraries
            string libraryXml = $"<Library name=\"{webResourceName}\" libraryUniqueId=\"{{{Guid.NewGuid()}}}\" />";
            
            int formLibrariesEnd = formXml.IndexOf("</formLibraries>");
            if (formLibrariesEnd > 0)
            {
                formXml = formXml.Insert(formLibrariesEnd, libraryXml);
            }
            else
            {
                // 如果没有formLibraries节点，在</form>前添加
                int formEnd = formXml.LastIndexOf("</form>");
                if (formEnd > 0)
                {
                    formXml = formXml.Insert(formEnd, $"<formLibraries>{libraryXml}</formLibraries>");
                }
            }
            
            // 添加事件处理程序
            string eventXml = $@"
    <event name='onload' application='false' active='true'>
      <Handlers>
        <Handler handlerUniqueId='{{{Guid.NewGuid()}}}' libraryName='{webResourceName}' functionName='{jsPrefix}.onLoad' enabled='true' parameters='' passExecutionContext='true' />
      </Handlers>
    </event>
    <event name='onsave' application='false' active='true'>
      <Handlers>
        <Handler handlerUniqueId='{{{Guid.NewGuid()}}}' libraryName='{webResourceName}' functionName='{jsPrefix}.onSave' enabled='true' parameters='' passExecutionContext='true' />
      </Handlers>
    </event>";
            
            int eventsEnd = formXml.IndexOf("</events>");
            if (eventsEnd > 0)
            {
                formXml = formXml.Insert(eventsEnd, eventXml);
            }
            else
            {
                // 如果没有events节点，在</form>前添加
                int formEnd = formXml.LastIndexOf("</form>");
                if (formEnd > 0)
                {
                    formXml = formXml.Insert(formEnd, $"<events>{eventXml}</events>");
                }
            }
            
            // 5. 更新表单
            var updateForm = new Entity("systemform", form.Id);
            updateForm["formxml"] = formXml;
            _service.Update(updateForm);
            
            // 6. 发布表单更改
            var publishRequest = new PublishXmlRequest();
            publishRequest.ParameterXml = $"<importexportxml><entities><entity>{entityName}</entity></entities><nodes/><securityroles/><settings/><workflows/></importexportxml>";
            _service.Execute(publishRequest);
            Console.WriteLine($"  ✓ 表单已发布");
            
            Console.WriteLine($"  ✓ JS已绑定到表单");
        }

        #endregion

        #region Plugin注册

        /// <summary>
        /// 注册Plugin Assembly和Step
        /// </summary>
        public void RegisterPlugin(string dllPath, string className, string entityName, string messageName, int stage, int mode)
        {
            Console.WriteLine($"注册Plugin: {className}...");
            
            if (!File.Exists(dllPath))
            {
                Console.WriteLine($"  ✗ DLL不存在: {dllPath}");
                return;
            }
            
            // 读取DLL
            byte[] dllBytes = File.ReadAllBytes(dllPath);
            string base64Dll = Convert.ToBase64String(dllBytes);
            string assemblyName = System.IO.Path.GetFileNameWithoutExtension(dllPath);
            
            // 1. 注册/更新Assembly
            var assemblyQuery = new QueryExpression("pluginassembly")
            {
                ColumnSet = new ColumnSet("pluginassemblyid"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("name", ConditionOperator.Equal, assemblyName) }
                }
            };
            
            var assemblyResult = _service.RetrieveMultiple(assemblyQuery);
            Guid assemblyId;
            
            if (assemblyResult.Entities.Count > 0)
            {
                assemblyId = assemblyResult.Entities[0].Id;
                var updateAssembly = new Entity("pluginassembly", assemblyId);
                updateAssembly["content"] = base64Dll;
                _service.Update(updateAssembly);
                Console.WriteLine($"  ✓ Plugin Assembly已更新 (ID: {assemblyId})");
            }
            else
            {
                var newAssembly = new Entity("pluginassembly");
                newAssembly["name"] = assemblyName;
                newAssembly["content"] = base64Dll;
                newAssembly["sourcetype"] = new OptionSetValue(0); // Database
                newAssembly["isolationmode"] = new OptionSetValue(2); // Sandbox - 不需要强命名
                newAssembly["culture"] = "neutral";
                newAssembly["version"] = "1.0.0.0";
                newAssembly["publickeytoken"] = "null"; // Sandbox模式下可为null
                assemblyId = _service.Create(newAssembly);
                Console.WriteLine($"  ✓ Plugin Assembly已创建 (ID: {assemblyId})");
            }
            
            // 2. 注册Plugin Type
            var typeQuery = new QueryExpression("plugintype")
            {
                ColumnSet = new ColumnSet("plugintypeid"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("typename", ConditionOperator.Equal, className) }
                }
            };
            
            var typeResult = _service.RetrieveMultiple(typeQuery);
            Guid pluginTypeId;
            
            if (typeResult.Entities.Count > 0)
            {
                pluginTypeId = typeResult.Entities[0].Id;
                Console.WriteLine($"  ✓ Plugin Type已存在 (ID: {pluginTypeId})");
            }
            else
            {
                var newType = new Entity("plugintype");
                newType["pluginassemblyid"] = new EntityReference("pluginassembly", assemblyId);
                newType["typename"] = className;
                newType["friendlyname"] = className.Split('.').Last();
                newType["name"] = className.Split('.').Last();
                pluginTypeId = _service.Create(newType);
                Console.WriteLine($"  ✓ Plugin Type已创建 (ID: {pluginTypeId})");
            }
            
            // 3. 获取Message和Filter
            var msgQuery = new QueryExpression("sdkmessage")
            {
                ColumnSet = new ColumnSet("sdkmessageid"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("name", ConditionOperator.Equal, messageName) }
                }
            };
            var msgResult = _service.RetrieveMultiple(msgQuery);
            if (msgResult.Entities.Count == 0)
            {
                Console.WriteLine($"  ✗ 消息 {messageName} 未找到");
                return;
            }
            Guid msgId = msgResult.Entities[0].GetAttributeValue<Guid>("sdkmessageid");
            
            var filterQuery = new QueryExpression("sdkmessagefilter")
            {
                ColumnSet = new ColumnSet("sdkmessagefilterid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("sdkmessageid", ConditionOperator.Equal, msgId),
                        new ConditionExpression("primaryobjecttypecode", ConditionOperator.Equal, entityName)
                    }
                }
            };
            var filterResult = _service.RetrieveMultiple(filterQuery);
            if (filterResult.Entities.Count == 0)
            {
                Console.WriteLine($"  ✗ 消息过滤器未找到");
                return;
            }
            Guid filterId = filterResult.Entities[0].GetAttributeValue<Guid>("sdkmessagefilterid");
            
            // 4. 注册Step
            var stepQuery = new QueryExpression("sdkmessageprocessingstep")
            {
                ColumnSet = new ColumnSet("sdkmessageprocessingstepid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("plugintypeid", ConditionOperator.Equal, pluginTypeId),
                        new ConditionExpression("sdkmessagefilterid", ConditionOperator.Equal, filterId),
                        new ConditionExpression("stage", ConditionOperator.Equal, stage)
                    }
                }
            };
            
            var stepResult = _service.RetrieveMultiple(stepQuery);
            
            if (stepResult.Entities.Count > 0)
            {
                Console.WriteLine($"  ⊘ Step已存在，跳过");
            }
            else
            {
                var newStep = new Entity("sdkmessageprocessingstep");
                newStep["plugintypeid"] = new EntityReference("plugintype", pluginTypeId);
                newStep["sdkmessageid"] = new EntityReference("sdkmessage", msgId);
                newStep["sdkmessagefilterid"] = new EntityReference("sdkmessagefilter", filterId);
                newStep["name"] = $"{className.Split('.').Last()}: {messageName} of {entityName}";
                newStep["stage"] = new OptionSetValue(stage);
                newStep["mode"] = new OptionSetValue(mode);
                newStep["rank"] = 1;
                newStep["supporteddeployment"] = new OptionSetValue(0);
                
                var stepId = _service.Create(newStep);
                Console.WriteLine($"  ✓ Plugin Step已注册 (ID: {stepId})");
            }
            
            Console.WriteLine("  ✓ Plugin注册完成");
        }

        /// <summary>
        /// 注册Plugin（支持Update消息和筛选属性）
        /// </summary>
        public void RegisterPluginWithFilter(string dllPath, string className, string entityName, string messageName, int stage, int mode, string filteringAttributes = null)
        {
            Console.WriteLine($"注册Plugin: {className}...");
            Console.WriteLine($"  消息: {messageName}, 实体: {entityName}, 阶段: {stage}, 筛选属性: {filteringAttributes ?? "无"}");
            
            if (!File.Exists(dllPath))
            {
                Console.WriteLine($"  ✗ DLL不存在: {dllPath}");
                return;
            }
            
            // 读取DLL
            byte[] dllBytes = File.ReadAllBytes(dllPath);
            string base64Dll = Convert.ToBase64String(dllBytes);
            string assemblyName = System.IO.Path.GetFileNameWithoutExtension(dllPath);
            
            // 1. 注册/更新Assembly
            var assemblyQuery = new QueryExpression("pluginassembly")
            {
                ColumnSet = new ColumnSet("pluginassemblyid"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("name", ConditionOperator.Equal, assemblyName) }
                }
            };
            
            var assemblyResult = _service.RetrieveMultiple(assemblyQuery);
            Guid assemblyId;
            
            if (assemblyResult.Entities.Count > 0)
            {
                assemblyId = assemblyResult.Entities[0].Id;
                var updateAssembly = new Entity("pluginassembly", assemblyId);
                updateAssembly["content"] = base64Dll;
                _service.Update(updateAssembly);
                Console.WriteLine($"  ✓ Plugin Assembly已更新 (ID: {assemblyId})");
            }
            else
            {
                var newAssembly = new Entity("pluginassembly");
                newAssembly["name"] = assemblyName;
                newAssembly["content"] = base64Dll;
                newAssembly["sourcetype"] = new OptionSetValue(0); // Database
                newAssembly["isolationmode"] = new OptionSetValue(2); // Sandbox
                newAssembly["culture"] = "neutral";
                newAssembly["version"] = "1.0.0.0";
                newAssembly["publickeytoken"] = "null";
                assemblyId = _service.Create(newAssembly);
                Console.WriteLine($"  ✓ Plugin Assembly已创建 (ID: {assemblyId})");
            }
            
            // 2. 注册Plugin Type
            var typeQuery = new QueryExpression("plugintype")
            {
                ColumnSet = new ColumnSet("plugintypeid"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("typename", ConditionOperator.Equal, className) }
                }
            };
            
            var typeResult = _service.RetrieveMultiple(typeQuery);
            Guid pluginTypeId;
            
            if (typeResult.Entities.Count > 0)
            {
                pluginTypeId = typeResult.Entities[0].Id;
                Console.WriteLine($"  ✓ Plugin Type已存在 (ID: {pluginTypeId})");
            }
            else
            {
                var newType = new Entity("plugintype");
                newType["pluginassemblyid"] = new EntityReference("pluginassembly", assemblyId);
                newType["typename"] = className;
                newType["friendlyname"] = className.Split('.').Last();
                newType["name"] = className.Split('.').Last();
                pluginTypeId = _service.Create(newType);
                Console.WriteLine($"  ✓ Plugin Type已创建 (ID: {pluginTypeId})");
            }
            
            // 3. 获取Message和Filter
            var msgQuery = new QueryExpression("sdkmessage")
            {
                ColumnSet = new ColumnSet("sdkmessageid"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("name", ConditionOperator.Equal, messageName) }
                }
            };
            var msgResult = _service.RetrieveMultiple(msgQuery);
            if (msgResult.Entities.Count == 0)
            {
                Console.WriteLine($"  ✗ 消息 {messageName} 未找到");
                return;
            }
            Guid msgId = msgResult.Entities[0].GetAttributeValue<Guid>("sdkmessageid");
            
            var filterQuery = new QueryExpression("sdkmessagefilter")
            {
                ColumnSet = new ColumnSet("sdkmessagefilterid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("sdkmessageid", ConditionOperator.Equal, msgId),
                        new ConditionExpression("primaryobjecttypecode", ConditionOperator.Equal, entityName)
                    }
                }
            };
            var filterResult = _service.RetrieveMultiple(filterQuery);
            if (filterResult.Entities.Count == 0)
            {
                Console.WriteLine($"  ✗ 消息过滤器未找到");
                return;
            }
            Guid filterId = filterResult.Entities[0].GetAttributeValue<Guid>("sdkmessagefilterid");
            
            // 4. 注册Step
            var stepQuery = new QueryExpression("sdkmessageprocessingstep")
            {
                ColumnSet = new ColumnSet("sdkmessageprocessingstepid", "filteringattributes"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("plugintypeid", ConditionOperator.Equal, pluginTypeId),
                        new ConditionExpression("sdkmessagefilterid", ConditionOperator.Equal, filterId),
                        new ConditionExpression("stage", ConditionOperator.Equal, stage)
                    }
                }
            };
            
            var stepResult = _service.RetrieveMultiple(stepQuery);
            
            if (stepResult.Entities.Count > 0)
            {
                // 更新现有Step（添加筛选属性）
                var existingStep = stepResult.Entities[0];
                Guid stepId = existingStep.Id;
                
                if (!string.IsNullOrEmpty(filteringAttributes))
                {
                    var updateStep = new Entity("sdkmessageprocessingstep", stepId);
                    updateStep["filteringattributes"] = filteringAttributes;
                    _service.Update(updateStep);
                    Console.WriteLine($"  ✓ Plugin Step已更新筛选属性 (ID: {stepId})");
                }
                else
                {
                    Console.WriteLine($"  ⊘ Step已存在，跳过");
                }
            }
            else
            {
                var newStep = new Entity("sdkmessageprocessingstep");
                newStep["plugintypeid"] = new EntityReference("plugintype", pluginTypeId);
                newStep["sdkmessageid"] = new EntityReference("sdkmessage", msgId);
                newStep["sdkmessagefilterid"] = new EntityReference("sdkmessagefilter", filterId);
                newStep["name"] = $"{className.Split('.').Last()}: {messageName} of {entityName}";
                newStep["stage"] = new OptionSetValue(stage);
                newStep["mode"] = new OptionSetValue(mode);
                newStep["rank"] = 1;
                newStep["supporteddeployment"] = new OptionSetValue(0);
                
                if (!string.IsNullOrEmpty(filteringAttributes))
                {
                    newStep["filteringattributes"] = filteringAttributes;
                }
                
                var stepId = _service.Create(newStep);
                Console.WriteLine($"  ✓ Plugin Step已注册 (ID: {stepId})");
            }
            
            Console.WriteLine("  ✓ Plugin注册完成");
        }

        /// <summary>
        /// 创建评分项目测试数据（12个Coface指标）
        /// </summary>
        public void CreateCreditItemRecords()
        {
            Console.WriteLine("创建评分项目记录...");

            // D365选项集值格式: 100000000 + index
            // dtype: 100000000=定量, 100000001=定性
            // group: 100000000=客户实力, 100000001=客户财务, 100000002=宏观市场, 100000003=历史交易
            // source: 100000000=内部, 100000001=外部
            var items = new (string code, string name, string desc, int dtype, int group, int source, bool validate, bool thirdParty)[]
            {
                // 客户实力 (7项)
                ("ExternalRating", "外部评级", "外部资信机构给出的评级（1-10分）", 100000000, 100000000, 100000001, false, true),
                ("RegisteredCapital", "注册资本", "注册资本,金额单位元,货币USD", 100000000, 100000000, 100000001, true, true),
                ("RegistrationDate", "从业年限", "从业年限的年数", 100000000, 100000000, 100000001, false, true),
                ("LatePaymentIndex", "迟付指数", "迟付指数比率,采用小数两位", 100000000, 100000000, 100000001, false, true),
                ("LegalEvents", "诉讼债权金额", "诉讼债权标的金额,金额单位元,货币USD", 100000000, 100000000, 100000001, true, true),
                ("ProjectAmt", "在手项目", "在手项目合同额合计,金额单位元,货币USD", 100000000, 100000000, 100000000, true, false),
                ("ProductNum", "自有设备", "自有设备数", 100000000, 100000000, 100000000, true, false),
                
                // 客户财务 (6项)
                ("NetAssets", "净资产", "净资产金额单位元,货币USD", 100000000, 100000001, 100000001, true, true),
                ("DebtRatio", "资产负债率", "资产负债率,采用小数两位", 100000000, 100000001, 100000001, true, true),
                ("CurrentRatio", "流动比率", "流动比率,采用小数两位", 100000000, 100000001, 100000001, true, true),
                ("NetProfit", "净利润率", "净利润率,采用小数两位", 100000000, 100000001, 100000001, true, true),
                ("DebtAmount", "还款来源", "近半年个人银行账户借方月平均值,单位元,货币USD", 100000000, 100000001, 100000000, true, false),
                ("TotalAssets", "资产证明", "个人名下资产合计金额,单位元,货币USD", 100000000, 100000001, 100000000, true, false),
                
                // 宏观市场 (3项)
                ("CountryRisk", "国别风险", "国别风险（低、中、高）", 100000001, 100000002, 100000001, false, true),
                ("SectorRisk", "行业风险", "行业风险（低、中、高）", 100000001, 100000002, 100000001, false, true),
                ("Sectors", "行业属性", "行业属性", 100000001, 100000002, 100000001, false, true),
                
                // 历史交易 (6项)
                ("OverdueModel", "逾期未回收率模型分", "逾期未回收率模型分（0-100）", 100000000, 100000003, 100000000, true, false),
                ("BigAccount", "客户评级", "客户评级S/A/S或A级控股参股公司", 100000001, 100000003, 100000000, false, false),
                ("SalesAmount", "历史采购金额", "累计采购金额,单位元,货币USD", 100000000, 100000003, 100000000, false, false),
                ("ARAmount", "历史逾期金额", "最大逾期付款金额（过去两年）USD/元", 100000000, 100000003, 100000000, false, false),
                ("ARAge", "历史逾期账龄", "最大逾期账龄天数（过去两年）USD/元", 100000000, 100000003, 100000000, false, false),
                ("DealerRating", "经销商分级", "经销商分级（钻石、铂金、白银等）", 100000001, 100000003, 100000000, false, false),
            };

            int created = 0;
            int updated = 0;
            int existing = 0;

            foreach (var item in items)
            {
                // 检查是否已存在
                var query = new QueryExpression("mcs_credit_items")
                {
                    ColumnSet = new ColumnSet("mcs_credit_itemsid", "mcs_itemname", "mcs_itemdesc", "mcs_datatype", "mcs_group", "mcs_source", "mcs_validate", "mcs__3p"),
                    Criteria = new FilterExpression
                    {
                        Conditions = { new ConditionExpression("mcs_credit_itemsno", ConditionOperator.Equal, item.code) }
                    },
                    TopCount = 1
                };

                var result = _service.RetrieveMultiple(query);
                if (result.Entities.Count > 0)
                {
                    var existingEntity = result.Entities[0];
                    bool needUpdate = false;
                    
                    // 检查字段是否为空，需要更新
                    if (!existingEntity.Contains("mcs_itemname") || string.IsNullOrEmpty(existingEntity.GetAttributeValue<string>("mcs_itemname")))
                        needUpdate = true;
                    if (!existingEntity.Contains("mcs_itemdesc") || string.IsNullOrEmpty(existingEntity.GetAttributeValue<string>("mcs_itemdesc")))
                        needUpdate = true;
                    if (!existingEntity.Contains("mcs_datatype"))
                        needUpdate = true;
                    if (!existingEntity.Contains("mcs_group"))
                        needUpdate = true;
                    if (!existingEntity.Contains("mcs_source"))
                        needUpdate = true;
                    if (!existingEntity.Contains("mcs_validate"))
                        needUpdate = true;
                    if (!existingEntity.Contains("mcs__3p"))
                        needUpdate = true;

                    if (needUpdate)
                    {
                        // 更新现有记录，补全缺失字段
                        var updateEntity = new Entity("mcs_credit_items");
                        updateEntity.Id = existingEntity.Id;
                        updateEntity["mcs_itemname"] = item.name;
                        updateEntity["mcs_itemdesc"] = item.desc;
                        updateEntity["mcs_datatype"] = new OptionSetValue(item.dtype);
                        updateEntity["mcs_group"] = new OptionSetValue(item.group);
                        updateEntity["mcs_source"] = new OptionSetValue(item.source);
                        updateEntity["mcs_validate"] = item.validate;
                        updateEntity["mcs__3p"] = item.thirdParty;
                        
                        _service.Update(updateEntity);
                        Console.WriteLine($"  ↻ 更新补全: {item.code}");
                        updated++;
                    }
                    else
                    {
                        Console.WriteLine($"  ⊘ 已存在(完整): {item.code}");
                        existing++;
                    }
                    continue;
                }

                // 创建新记录
                var entity = new Entity("mcs_credit_items");
                entity["mcs_credit_itemsno"] = item.code;
                entity["mcs_itemname"] = item.name;
                entity["mcs_itemdesc"] = item.desc;
                entity["mcs_datatype"] = new OptionSetValue(item.dtype);
                entity["mcs_group"] = new OptionSetValue(item.group);
                entity["mcs_source"] = new OptionSetValue(item.source);
                entity["mcs_validate"] = item.validate;
                entity["mcs__3p"] = item.thirdParty;

                var id = _service.Create(entity);
                Console.WriteLine($"  ✓ 创建成功: {item.code}, ID={id}");
                created++;
            }

            Console.WriteLine($"\n完成: 创建{created}条, 更新{updated}条, 已存在{existing}条, 总计{created + updated + existing}条");
        }

        /// <summary>
        /// 创建定性评分项目枚举值测试数据
        /// </summary>
        public void CreateQualitativeEnumRecords()
        {
            Console.WriteLine("创建定性评分项目枚举值记录...");

            // (评分项目编码, 选择项编码, 选择项名称, 枚举赋分)
            var enums = new (string itemCode, string listValue, string listName, int score)[]
            {
                // CountryRisk 国别风险
                ("CountryRisk", "1", "低风险", 5),
                ("CountryRisk", "2", "中风险", 3),
                ("CountryRisk", "3", "高风险", -1),
                ("CountryRisk", "O", "缺失", 2),
                
                // SectorRisk 行业风险
                ("SectorRisk", "1", "低风险", 5),
                ("SectorRisk", "2", "中风险", 3),
                ("SectorRisk", "3", "高风险", -1),
                ("SectorRisk", "O", "缺失", 2),
                
                // Sectors 行业属性
                ("Sectors", "Mining", "矿业", 5),
                ("Sectors", "Port", "港务", 4),
                ("Sectors", "Construction", "建工", 4),
                ("Sectors", "Lifting", "吊装", 4),
                ("Sectors", "Container", "集装箱运力", 3),
                ("Sectors", "Rental", "租赁", 3),
                ("Sectors", "Concrete", "商混", 3),
                ("Sectors", "Forestry", "林业", 3),
                ("Sectors", "Agriculture", "农业", 3),
                ("Sectors", "Manufacturing", "制造业", 4),
                ("Sectors", "Transportation", "交通运输", 3),
                ("Sectors", "Other", "其他", 2),
                ("Sectors", "O", "缺失", 3),
                
                // BigAccount 客户评级
                ("BigAccount", "S", "S级", 10),
                ("BigAccount", "A", "A级", 8),
                ("BigAccount", "S_JV", "S级控股/参股公司", 6),
                ("BigAccount", "A_JV", "A级控股/参股公司", 3),
                
                // DealerRating 经销商分级
                ("DealerRating", "Diamond", "钻石", 10),
                ("DealerRating", "Platinum", "铂金", 8),
                ("DealerRating", "Silver", "白银", 6),
                ("DealerRating", "Certified", "认证", 4),
                ("DealerRating", "Intention", "意向", 2),
            };

            // 预加载评分项目编码→GUID映射
            var itemGuidMap = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            var itemQuery = new QueryExpression("mcs_credit_items")
            {
                ColumnSet = new ColumnSet("mcs_credit_itemsid", "mcs_credit_itemsno")
            };
            var itemResults = _service.RetrieveMultiple(itemQuery);
            foreach (var item in itemResults.Entities)
            {
                var itemNo = item.GetAttributeValue<string>("mcs_credit_itemsno");
                var itemId = item.GetAttributeValue<Guid>("mcs_credit_itemsid");
                if (!string.IsNullOrEmpty(itemNo))
                {
                    itemGuidMap[itemNo] = itemId;
                }
            }
            Console.WriteLine($"  预加载 {itemGuidMap.Count} 个评分项目GUID");

            int created = 0;
            int existing = 0;
            int skipped = 0;

            foreach (var en in enums)
            {
                // 查找评分项目GUID
                if (!itemGuidMap.TryGetValue(en.itemCode, out Guid itemGuid))
                {
                    Console.WriteLine($"  ✗ 跳过: 未找到评分项目 {en.itemCode}");
                    skipped++;
                    continue;
                }

                // 检查是否已存在 (组合唯一键: mcs_credititemno + mcs_listvalue)
                // 注意: mcs_credititemno现在是Lookup，需要用GUID查询
                var query = new QueryExpression("mcs_credititem_value")
                {
                    ColumnSet = new ColumnSet("mcs_credititem_valueid"),
                    Criteria = new FilterExpression
                    {
                        Conditions = 
                        { 
                            new ConditionExpression("mcs_credititemno", ConditionOperator.Equal, itemGuid),
                            new ConditionExpression("mcs_listvalue", ConditionOperator.Equal, en.listValue)
                        }
                    },
                    TopCount = 1
                };

                var result = _service.RetrieveMultiple(query);
                if (result.Entities.Count > 0)
                {
                    Console.WriteLine($"  ⊘ 已存在: {en.itemCode}/{en.listValue}");
                    existing++;
                    continue;
                }

                // 创建新记录 - 使用EntityReference设置Lookup字段
                var entity = new Entity("mcs_credititem_value");
                entity["mcs_credititemno"] = new EntityReference("mcs_credit_items", itemGuid);
                entity["mcs_listvalue"] = en.listValue;
                entity["mcs_listname"] = en.listName;
                // 注: 当前实体无赋分字段，仅创建基础枚举值
                // 赋分信息在评分卡配置表中维护

                var id = _service.Create(entity);
                Console.WriteLine($"  ✓ 创建成功: {en.itemCode}/{en.listValue}={en.listName}, ID={id}");
                created++;
            }

            Console.WriteLine($"\n完成: 创建{created}条, 已存在{existing}条, 跳过{skipped}条, 总计{created + existing + skipped}条");
        }

        /// <summary>
        /// 清理定性评分项目枚举值表中的所有记录
        /// </summary>
        public void CleanupQualitativeEnumRecords()
        {
            Console.WriteLine("清理定性评分项目枚举值记录...");

            var query = new QueryExpression("mcs_credititem_value")
            {
                ColumnSet = new ColumnSet("mcs_credititem_valueid"),
                TopCount = 100
            };

            int deleted = 0;
            int batch = 0;

            while (true)
            {
                var results = _service.RetrieveMultiple(query);
                if (results.Entities.Count == 0) break;

                foreach (var entity in results.Entities)
                {
                    _service.Delete("mcs_credititem_value", entity.Id);
                    deleted++;
                }

                batch++;
                Console.WriteLine($"  第{batch}批: 已删除 {deleted} 条");
            }

            Console.WriteLine($"\n完成: 共删除 {deleted} 条记录");
        }

        /// <summary>
        /// 检查评分项目记录，输出重复名称和空编码的记录
        /// </summary>
        public void CheckCreditItemRecords()
        {
            Console.WriteLine("查询评分项目记录...");

            var query = new QueryExpression("mcs_credit_items")
            {
                ColumnSet = new ColumnSet("mcs_credit_itemsid", "mcs_credit_itemsno", "mcs_itemname", "mcs_itemdesc", "mcs_group", "mcs_datatype", "mcs_source", "mcs_validate", "mcs__3p", "createdon"),
                Orders = { new OrderExpression("mcs_itemname", OrderType.Ascending) }
            };

            var result = _service.RetrieveMultiple(query);
            Console.WriteLine($"系统中共有 {result.Entities.Count} 条评分项目记录\n");
            Console.WriteLine("评分项目编码         评分项目名称      分类          类型      来源      补录    3P      创建日期              ");
            Console.WriteLine(new string('-', 110));

            foreach (var e in result.Entities)
            {
                var code = e.Contains("mcs_credit_itemsno") ? e.GetAttributeValue<string>("mcs_credit_itemsno") ?? "(空)" : "(空)";
                var name = e.Contains("mcs_itemname") ? e.GetAttributeValue<string>("mcs_itemname") ?? "(空)" : "(空)";
                var group = e.Contains("mcs_group") ? ((OptionSetValue)e["mcs_group"]).Value.ToString() : "(空)";
                var dtype = e.Contains("mcs_datatype") ? ((OptionSetValue)e["mcs_datatype"]).Value.ToString() : "(空)";
                var source = e.Contains("mcs_source") ? ((OptionSetValue)e["mcs_source"]).Value.ToString() : "(空)";
                var validate = e.Contains("mcs_validate") ? e.GetAttributeValue<bool>("mcs_validate").ToString() : "(空)";
                var tp = e.Contains("mcs__3p") ? e.GetAttributeValue<bool>("mcs__3p").ToString() : "(空)";
                var created = e.GetAttributeValue<DateTime>("createdon").ToLocalTime().ToString("yyyy-MM-dd HH:mm");
                
                Console.WriteLine($"{code:<20} {name:<15} {group:<12} {dtype:<8} {source:<8} {validate:<6} {tp:<6} {created:<22}");
            }

            // 检查重复名称
            Console.WriteLine("\n=== 重复名称检查 ===");
            var nameGroups = result.Entities
                .Where(e => e.Contains("mcs_itemname") && !string.IsNullOrEmpty(e.GetAttributeValue<string>("mcs_itemname")))
                .GroupBy(e => e.GetAttributeValue<string>("mcs_itemname"))
                .Where(g => g.Count() > 1);
            
            if (nameGroups.Any())
            {
                foreach (var g in nameGroups)
                {
                    Console.WriteLine($"名称 '{g.Key}' 有 {g.Count()} 条记录:");
                    foreach (var e in g)
                    {
                        var code = e.Contains("mcs_credit_itemsno") ? e.GetAttributeValue<string>("mcs_credit_itemsno") ?? "(空)" : "(空)";
                        var id = e.Id.ToString().Substring(0, 8);
                        Console.WriteLine($"  - ID={id}..., 编码={code}");
                    }
                }
            }
            else
            {
                Console.WriteLine("未发现重复名称");
            }

            // 检查空编码
            Console.WriteLine("\n=== 空编码检查 ===");
            var emptyCode = result.Entities.Where(e => !e.Contains("mcs_credit_itemsno") || string.IsNullOrEmpty(e.GetAttributeValue<string>("mcs_credit_itemsno")));
            if (emptyCode.Any())
            {
                foreach (var e in emptyCode)
                {
                    var name = e.Contains("mcs_itemname") ? e.GetAttributeValue<string>("mcs_itemname") ?? "(空)" : "(空)";
                    var id = e.Id.ToString().Substring(0, 8);
                    Console.WriteLine($"ID={id}..., 名称={name}, 编码=空");
                }
            }
            else
            {
                Console.WriteLine("未发现空编码记录");
            }
        }

        /// <summary>
        /// 清理评分项目重复和空编码记录
        /// </summary>
        public void CleanupCreditItemRecords()
        {
            Console.WriteLine("清理评分项目记录...");

            // 1. 删除重复的旧数据（保留PRD标准编码）
            var duplicateCodes = new[] { "EstablishedYear", "NetProfitMargin", "NaceCodes" };
            int deletedDuplicates = 0;
            
            foreach (var code in duplicateCodes)
            {
                var query = new QueryExpression("mcs_credit_items")
                {
                    ColumnSet = new ColumnSet("mcs_credit_itemsid", "mcs_itemname"),
                    Criteria = new FilterExpression
                    {
                        Conditions = { new ConditionExpression("mcs_credit_itemsno", ConditionOperator.Equal, code) }
                    }
                };

                var result = _service.RetrieveMultiple(query);
                foreach (var e in result.Entities)
                {
                    _service.Delete("mcs_credit_items", e.Id);
                    var name = e.GetAttributeValue<string>("mcs_itemname") ?? "(空)";
                    Console.WriteLine($"  ✗ 删除重复记录: {code} ({name}), ID={e.Id}");
                    deletedDuplicates++;
                }
            }

            // 2. 删除空编码的旧数据
            var emptyNames = new[] { "交易记录", "信用分", "市场风险", "综合评分", "财务状况" };
            int deletedEmpty = 0;

            foreach (var name in emptyNames)
            {
                var query = new QueryExpression("mcs_credit_items")
                {
                    ColumnSet = new ColumnSet("mcs_credit_itemsid"),
                    Criteria = new FilterExpression
                    {
                        Conditions = 
                        { 
                            new ConditionExpression("mcs_itemname", ConditionOperator.Equal, name),
                            new ConditionExpression("mcs_credit_itemsno", ConditionOperator.Null)
                        }
                    }
                };

                var result = _service.RetrieveMultiple(query);
                foreach (var e in result.Entities)
                {
                    _service.Delete("mcs_credit_items", e.Id);
                    Console.WriteLine($"  ✗ 删除空编码记录: {name}, ID={e.Id}");
                    deletedEmpty++;
                }
            }

            Console.WriteLine($"\n完成: 删除重复{deletedDuplicates}条, 删除空编码{deletedEmpty}条, 总计删除{deletedDuplicates + deletedEmpty}条");
        }

        /// <summary>
        /// 删除指定编码的评分项目记录
        /// </summary>
        public void DeleteCreditItemByCode(string code)
        {
            Console.WriteLine($"删除编码为 {code} 的评分项目记录...");

            var query = new QueryExpression("mcs_credit_items")
            {
                ColumnSet = new ColumnSet("mcs_credit_itemsid", "mcs_itemname"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("mcs_credit_itemsno", ConditionOperator.Equal, code) }
                }
            };

            var result = _service.RetrieveMultiple(query);
            foreach (var e in result.Entities)
            {
                _service.Delete("mcs_credit_items", e.Id);
                var name = e.GetAttributeValue<string>("mcs_itemname") ?? "(空)";
                Console.WriteLine($"  ✗ 删除: {code} ({name}), ID={e.Id}");
            }

            Console.WriteLine("完成");
        }

        /// <summary>
        /// 更新实体的Lookup视图，确保fetchxml包含必要的attribute
        /// </summary>
        public void UpdateLookupView(string entityName, string[] fields)
        {
            Console.WriteLine($"更新 {entityName} 的Lookup视图...");
            
            // 查询Lookup视图 (querytype=64)
            var query = new QueryExpression("savedquery")
            {
                ColumnSet = new ColumnSet("savedqueryid", "name", "layoutxml", "fetchxml"),
                Criteria = new FilterExpression()
                {
                    Conditions = 
                    {
                        new ConditionExpression("returnedtypecode", ConditionOperator.Equal, entityName),
                        new ConditionExpression("querytype", ConditionOperator.Equal, 64)
                    }
                }
            };
            
            var views = _service.RetrieveMultiple(query);
            if (views.Entities.Count == 0)
            {
                Console.WriteLine("  ✗ 未找到Lookup视图");
                return;
            }
            
            foreach (var view in views.Entities)
            {
                string fetchXml = view.GetAttributeValue<string>("fetchxml");
                string viewName = view.GetAttributeValue<string>("name");
                Console.WriteLine($"  视图: {viewName}");
                
                string newFetchAttrsXml = "";
                int fetchAddedCount = 0;
                
                foreach (var fieldName in fields)
                {
                    if (fetchXml.Contains($"name=\"{fieldName}\""))
                    {
                        Console.WriteLine($"    ⊘ fetchxml {fieldName} 已存在，跳过");
                    }
                    else
                    {
                        newFetchAttrsXml += $"<attribute name=\"{fieldName}\" />";
                        Console.WriteLine($"    + fetchxml {fieldName}");
                        fetchAddedCount++;
                    }
                }
                
                if (fetchAddedCount > 0)
                {
                    var updateView = new Entity("savedquery", view.Id);
                    
                    // 在 <entity> 标签后插入attribute
                    int entityEndPos = fetchXml.IndexOf(">", fetchXml.IndexOf("<entity"));
                    if (entityEndPos > 0)
                    {
                        string newFetchXml = fetchXml.Insert(entityEndPos + 1, newFetchAttrsXml);
                        updateView["fetchxml"] = newFetchXml;
                        _service.Update(updateView);
                        Console.WriteLine($"  ✓ Lookup视图已更新，添加 {fetchAddedCount} 个attribute");
                    }
                }
                else
                {
                    Console.WriteLine("  没有需要更新的内容");
                }
            }
            
            // 发布实体
            Console.WriteLine("  发布实体...");
            var publishRequest = new PublishXmlRequest
            {
                ParameterXml = $"<importexportxml><entities><entity>{entityName}</entity></entities></importexportxml>"
            };
            _service.Execute(publishRequest);
            Console.WriteLine("  ✓ 发布完成");
        }

        /// <summary>
        /// 导出指定实体的Lookup视图fetchxml（用于学习筛选结构）
        /// </summary>
        public void ExportLookupViewFetchXml(string entityName, string outputPath)
        {
            Console.WriteLine($"导出 {entityName} 的Lookup视图fetchxml...");
            
            var query = new QueryExpression("savedquery")
            {
                ColumnSet = new ColumnSet("savedqueryid", "name", "fetchxml", "layoutxml"),
                Criteria = new FilterExpression()
                {
                    Conditions = 
                    {
                        new ConditionExpression("returnedtypecode", ConditionOperator.Equal, entityName),
                        new ConditionExpression("querytype", ConditionOperator.Equal, 64)
                    }
                }
            };
            
            var views = _service.RetrieveMultiple(query);
            if (views.Entities.Count == 0)
            {
                Console.WriteLine("  ✗ 未找到Lookup视图");
                return;
            }
            
            foreach (var view in views.Entities)
            {
                string fetchXml = view.GetAttributeValue<string>("fetchxml");
                string layoutXml = view.GetAttributeValue<string>("layoutxml");
                string viewName = view.GetAttributeValue<string>("name");
                
                string content = $"=== {viewName} ===\n\n";
                content += $"fetchxml:\n{fetchXml}\n\n";
                content += $"layoutxml:\n{layoutXml}\n";
                
                System.IO.File.WriteAllText(outputPath, content);
                Console.WriteLine($"  ✓ 已导出到: {outputPath}");
            }
        }

        #endregion

        #region Assembly更新

        /// <summary>
        /// 仅更新Plugin Assembly DLL，不注册Step
        /// </summary>
        public void UpdateAssemblyOnly(string dllPath)
        {
            if (!File.Exists(dllPath))
            {
                Console.WriteLine($"✗ DLL不存在: {dllPath}");
                return;
            }

            byte[] dllBytes = File.ReadAllBytes(dllPath);
            string base64Dll = Convert.ToBase64String(dllBytes);
            string assemblyName = Path.GetFileNameWithoutExtension(dllPath);

            Console.WriteLine($"更新Assembly: {assemblyName}");
            Console.WriteLine($"DLL大小: {dllBytes.Length / 1024} KB");

            var assemblyQuery = new QueryExpression("pluginassembly")
            {
                ColumnSet = new ColumnSet("pluginassemblyid"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("name", ConditionOperator.Equal, assemblyName) }
                }
            };

            var assemblyResult = _service.RetrieveMultiple(assemblyQuery);

            if (assemblyResult.Entities.Count > 0)
            {
                Guid assemblyId = assemblyResult.Entities[0].Id;
                var updateAssembly = new Entity("pluginassembly", assemblyId);
                updateAssembly["content"] = base64Dll;
                _service.Update(updateAssembly);
                Console.WriteLine($"✓ Assembly已更新 (ID: {assemblyId})");
            }
            else
            {
                var newAssembly = new Entity("pluginassembly");
                newAssembly["name"] = assemblyName;
                newAssembly["content"] = base64Dll;
                newAssembly["sourcetype"] = new OptionSetValue(0);
                newAssembly["isolationmode"] = new OptionSetValue(2);
                newAssembly["culture"] = "neutral";
                newAssembly["version"] = "1.0.0.0";
                newAssembly["publickeytoken"] = "null";
                Guid assemblyId = _service.Create(newAssembly);
                Console.WriteLine($"✓ Assembly已创建 (ID: {assemblyId})");
            }
        }

        #endregion
    }
}
