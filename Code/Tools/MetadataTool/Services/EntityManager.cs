using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using D365ToolCommon.Connection;
using D365ToolCommon.Metadata;
using D365ToolCommon.Publishing;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;

namespace D365MetadataTool;

public class EntityManager
{
	private readonly ServiceClient _service;

	public EntityManager(ServiceClient service)
	{
		_service = service ?? throw new ArgumentNullException("service");
	}

	private static Label BuildLabel(string displayName, string displayNameZh = "", string displayNameEn = "")
	{
		if (!string.IsNullOrWhiteSpace(displayNameZh) && !string.IsNullOrWhiteSpace(displayNameEn))
		{
			return LabelHelper.Create(displayNameZh, displayNameEn);
		}
		return LabelHelper.Create(displayName);
	}

	public Guid CreateEntity(string schemaName, string displayName, string primaryAttributeName, string primaryAttributeDisplayName, int primaryAttributeLength = 100, string displayNameZh = "", string displayNameEn = "", string primaryAttributeDisplayNameZh = "", string primaryAttributeDisplayNameEn = "")
	{
		Console.WriteLine("创建实体: " + schemaName);
		Label label = BuildLabel(displayName, displayNameZh, displayNameEn);
		Label label2 = BuildLabel(primaryAttributeDisplayName, primaryAttributeDisplayNameZh, primaryAttributeDisplayNameEn);
		CreateEntityRequest request = new CreateEntityRequest
		{
			Entity = new EntityMetadata
			{
				SchemaName = schemaName,
				DisplayName = label,
				DisplayCollectionName = label,
				Description = label,
				OwnershipType = OwnershipTypes.UserOwned,
				IsActivity = false
			},
			PrimaryAttribute = new StringAttributeMetadata
			{
				SchemaName = primaryAttributeName,
				LogicalName = primaryAttributeName.ToLower(),
				RequiredLevel = new AttributeRequiredLevelManagedProperty(AttributeRequiredLevel.None),
				MaxLength = primaryAttributeLength,
				FormatName = StringFormatName.Text,
				DisplayName = label2,
				Description = label2
			}
		};
		CreateEntityResponse createEntityResponse = (CreateEntityResponse)_service.Execute(request);
		Console.WriteLine($"  ✓ 实体创建成功! ID: {createEntityResponse.EntityId}");
		return createEntityResponse.EntityId;
	}

	/// <summary>
	/// 更新实体显示名称（单语言，兼容旧调用）
	/// </summary>
	public void UpdateEntityDisplayName(string entityLogicalName, string displayName)
	{
		UpdateEntityDisplayName(entityLogicalName, displayName, displayName);
	}

	/// <summary>
	/// 更新实体显示名称（英文 1033 + 简体中文 2052）
	/// </summary>
	public void UpdateEntityDisplayName(string entityLogicalName, string zhCN, string enUS)
	{
		Console.WriteLine($"更新实体显示名称: {entityLogicalName} -> zhCN={zhCN}, enUS={enUS}");

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
			DisplayName = LabelHelper.Create(zhCN, enUS),
			DisplayCollectionName = LabelHelper.Create(zhCN + "列表", enUS + " List"),
			Description = LabelHelper.Create(zhCN, enUS)
		};

		var request = new UpdateEntityRequest
		{
			Entity = entity,
			HasNotes = false,
			HasActivities = false
		};

		_service.Execute(request);
		Console.WriteLine("  ✓ 实体显示名称更新成功");
	}

	/// <summary>
	/// 更新字段显示名称（单语言，兼容旧调用）
	/// </summary>
	public void UpdateAttributeDisplayName(string entityLogicalName, string attributeLogicalName, string displayName)
	{
		UpdateAttributeDisplayName(entityLogicalName, attributeLogicalName, displayName, displayName);
	}

	/// <summary>
	/// 更新字段显示名称（英文 1033 + 简体中文 2052）
	/// 2026-07-29 修复：UpdateAttributeRequest 对 Money 等部分字段类型不生效（静默失败，2052 未写入），
	/// 统一改走 D365ToolCommon MetadataFieldService 的 Web API PUT 路径（与 set-field-label 一致，经用户批准）。
	/// </summary>
	public void UpdateAttributeDisplayName(string entityLogicalName, string attributeLogicalName, string zhCN, string enUS)
	{
		Console.WriteLine($"更新字段显示名称: {entityLogicalName}.{attributeLogicalName} -> zhCN={zhCN}, enUS={enUS}");

		var fieldService = new MetadataFieldService(_service);
		fieldService.SetFieldDisplayNameAsync(entityLogicalName, attributeLogicalName, zhCN, enUS).GetAwaiter().GetResult();
		Console.WriteLine("  ✓ 字段显示名称更新成功");
	}

	public void UpdateIntegerFieldRange(string entityLogicalName, string attributeLogicalName, int minValue, int maxValue)
	{
		Console.WriteLine($"更新整数字段范围: {entityLogicalName}.{attributeLogicalName} -> [{minValue}, {maxValue}]");
		IntegerAttributeMetadata attribute = new IntegerAttributeMetadata
		{
			LogicalName = attributeLogicalName,
			MinValue = minValue,
			MaxValue = maxValue
		};
		UpdateAttributeRequest request = new UpdateAttributeRequest
		{
			EntityName = entityLogicalName,
			Attribute = attribute
		};
		_service.Execute(request);
		Console.WriteLine("  ✓ 整数字段范围更新成功");
	}

	/// <summary>
	/// 更新字段必填性（SDK 方式无效时 fallback 到 Web API PATCH）
	/// </summary>
	public async Task UpdateAttributeRequiredLevel(string entityLogicalName, string attributeLogicalName, bool required)
	{
		Console.WriteLine($"更新字段必填性: {entityLogicalName}.{attributeLogicalName} -> required={required}");
		var fieldService = new MetadataFieldService(_service);
		await fieldService.UpdateRequiredLevelAsync(entityLogicalName, attributeLogicalName, required);
	}

	public async Task UpdateAttributeDefaultValue(string entityLogicalName, string attributeLogicalName, int defaultValue)
	{
		Console.WriteLine($"设置字段默认值: {entityLogicalName}.{attributeLogicalName} -> defaultValue={defaultValue}");
		var fieldService = new MetadataFieldService(_service);
		if (GetAttributeType(entityLogicalName, attributeLogicalName) == AttributeTypeCode.Boolean)
		{
			await fieldService.SetBooleanDefaultValueAsync(entityLogicalName, attributeLogicalName, defaultValue != 0);
		}
		else
		{
			await fieldService.SetPicklistDefaultValueAsync(entityLogicalName, attributeLogicalName, defaultValue);
		}
	}

	public async Task UpdateAttributeDefaultValue(string entityLogicalName, string attributeLogicalName, decimal defaultValue)
	{
		Console.WriteLine($"设置字段默认值: {entityLogicalName}.{attributeLogicalName} -> defaultValue={defaultValue}");
		var fieldService = new MetadataFieldService(_service);
		await fieldService.SetDecimalDefaultValueAsync(entityLogicalName, attributeLogicalName, defaultValue);
	}

	public AttributeTypeCode? GetAttributeType(string entityLogicalName, string attributeLogicalName)
	{
		var request = new RetrieveAttributeRequest
		{
			EntityLogicalName = entityLogicalName,
			LogicalName = attributeLogicalName.ToLower(),
			RetrieveAsIfPublished = true
		};
		var response = (RetrieveAttributeResponse)_service.Execute(request);
		return response.AttributeMetadata.AttributeType;
	}

	public void UpdateAttributeDescription(string entityLogicalName, string attributeLogicalName, string description)
	{
		Console.WriteLine($"更新字段描述: {entityLogicalName}.{attributeLogicalName}");
		var fieldService = new MetadataFieldService(_service);
		fieldService.UpdateDescription(entityLogicalName, attributeLogicalName, description);
	}

	public void DeleteEntity(string entityName)
	{
		Console.WriteLine("删除实体: " + entityName);
		DeleteEntityRequest request = new DeleteEntityRequest
		{
			LogicalName = entityName
		};
		_service.Execute(request);
		Console.WriteLine("  ✓ 实体已删除");
	}

	public bool EntityExists(string entityName)
	{
		try
		{
			RetrieveEntityRequest request = new RetrieveEntityRequest
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

	public Guid? GetEntityId(string entityName)
	{
		try
		{
			RetrieveEntityRequest request = new RetrieveEntityRequest
			{
				EntityFilters = EntityFilters.Entity,
				LogicalName = entityName
			};
			RetrieveEntityResponse retrieveEntityResponse = (RetrieveEntityResponse)_service.Execute(request);
			return retrieveEntityResponse.EntityMetadata.MetadataId;
		}
		catch
		{
			return null;
		}
	}

	public void PrintEntityDisplayName(string entityName)
	{
		try
		{
			RetrieveEntityRequest request = new RetrieveEntityRequest
			{
				EntityFilters = (EntityFilters.Entity | EntityFilters.Attributes),
				LogicalName = entityName,
				RetrieveAsIfPublished = true
			};
			RetrieveEntityResponse retrieveEntityResponse = (RetrieveEntityResponse)_service.Execute(request);
			EntityMetadata entityMetadata = retrieveEntityResponse.EntityMetadata;
			Console.WriteLine("实体: " + entityMetadata.LogicalName);
			Console.WriteLine("  SchemaName: " + entityMetadata.SchemaName);
			Console.WriteLine("  DisplayName LocalizedLabels:");
			PrintLocalizedLabels(entityMetadata.DisplayName);
			Console.WriteLine("  DisplayCollectionName LocalizedLabels:");
			PrintLocalizedLabels(entityMetadata.DisplayCollectionName);
			Console.WriteLine("  自定义字段标签:");
			IOrderedEnumerable<AttributeMetadata> orderedEnumerable = from a in entityMetadata.Attributes
				where a.LogicalName.StartsWith("mcs_")
				orderby a.LogicalName
				select a;
			foreach (AttributeMetadata item in orderedEnumerable)
			{
				Console.WriteLine("    " + item.LogicalName + ":");
				PrintLocalizedLabels(item.DisplayName, "      ");
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine("查询实体显示名称失败: " + ex.Message);
		}
	}

	private void PrintLocalizedLabels(Label? label, string indent = "    ")
	{
		if (label?.LocalizedLabels != null && label.LocalizedLabels.Count > 0)
		{
			foreach (LocalizedLabel localizedLabel in label.LocalizedLabels)
			{
				Console.WriteLine($"{indent}LCID={localizedLabel.LanguageCode}, Label={localizedLabel.Label}");
			}
			return;
		}
		Console.WriteLine(indent + "(空)");
	}

	public void CreateStringField(string entityName, string schemaName, string displayName, string description, int maxLength = 100, bool required = false, string displayNameZh = "", string displayNameEn = "")
	{
		var fieldService = new MetadataFieldService(_service);
		fieldService.CreateStringFieldIfNotExists(entityName, schemaName, displayName, description, maxLength, required, displayNameZh, displayNameEn);
		Console.WriteLine($"  ✓ {schemaName} ({displayName}) - 字符串({maxLength})");
	}

	public void CreateMemoField(string entityName, string schemaName, string displayName, string description, int maxLength = 4000, string displayNameZh = "", string displayNameEn = "")
	{
		CreateAttributeRequest request = new CreateAttributeRequest
		{
			EntityName = entityName,
			Attribute = new MemoAttributeMetadata
			{
				SchemaName = schemaName,
				LogicalName = schemaName.ToLower(),
				DisplayName = BuildLabel(displayName, displayNameZh, displayNameEn),
				RequiredLevel = new AttributeRequiredLevelManagedProperty(AttributeRequiredLevel.None),
				Description = LabelHelper.Create(description),
				Format = StringFormat.TextArea,
				MaxLength = maxLength
			}
		};
		_service.Execute(request);
		Console.WriteLine($"  ✓ {schemaName} ({displayName}) - 多行文本({maxLength})");
	}

	public void CreateIntegerField(string entityName, string schemaName, string displayName, string description, int minValue = 0, int maxValue = 100, string displayNameZh = "", string displayNameEn = "")
	{
		CreateAttributeRequest request = new CreateAttributeRequest
		{
			EntityName = entityName,
			Attribute = new IntegerAttributeMetadata
			{
				SchemaName = schemaName,
				LogicalName = schemaName.ToLower(),
				DisplayName = BuildLabel(displayName, displayNameZh, displayNameEn),
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

	public void CreateDecimalField(string entityName, string schemaName, string displayName, string description, decimal minValue = 0m, decimal maxValue = 999999.99m, int precision = 2, string displayNameZh = "", string displayNameEn = "")
	{
		CreateAttributeRequest request = new CreateAttributeRequest
		{
			EntityName = entityName,
			Attribute = new DecimalAttributeMetadata
			{
				SchemaName = schemaName,
				LogicalName = schemaName.ToLower(),
				DisplayName = BuildLabel(displayName, displayNameZh, displayNameEn),
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

	public void CreateMoneyField(string entityName, string schemaName, string displayName, string description, decimal minValue = 0m, decimal maxValue = 1000000m, int precision = 2, string displayNameZh = "", string displayNameEn = "")
	{
		CreateAttributeRequest request = new CreateAttributeRequest
		{
			EntityName = entityName,
			Attribute = new MoneyAttributeMetadata
			{
				SchemaName = schemaName,
				LogicalName = schemaName.ToLower(),
				DisplayName = BuildLabel(displayName, displayNameZh, displayNameEn),
				RequiredLevel = new AttributeRequiredLevelManagedProperty(AttributeRequiredLevel.None),
				Description = LabelHelper.Create(description),
				MaxValue = (double)maxValue,
				MinValue = (double)minValue,
				Precision = precision,
				PrecisionSource = 1
			}
		};
		_service.Execute(request);
		Console.WriteLine($"  ✓ {schemaName} ({displayName}) - 货币");
	}

	public void CreateDateTimeField(string entityName, string schemaName, string displayName, string description, bool dateOnly = true, string displayNameZh = "", string displayNameEn = "")
	{
		CreateAttributeRequest request = new CreateAttributeRequest
		{
			EntityName = entityName,
			Attribute = new DateTimeAttributeMetadata
			{
				SchemaName = schemaName,
				LogicalName = schemaName.ToLower(),
				DisplayName = BuildLabel(displayName, displayNameZh, displayNameEn),
				RequiredLevel = new AttributeRequiredLevelManagedProperty(AttributeRequiredLevel.None),
				Description = LabelHelper.Create(description),
				Format = ((!dateOnly) ? DateTimeFormat.DateAndTime : DateTimeFormat.DateOnly),
				ImeMode = ImeMode.Disabled
			}
		};
		_service.Execute(request);
		Console.WriteLine($"  ✓ {schemaName} ({displayName}) - 日期{(dateOnly ? "" : "时间")}");
	}

	public void CreatePicklistField(string entityName, string schemaName, string displayName, string description, Dictionary<string, int> options, string displayNameZh = "", string displayNameEn = "")
	{
		CreateAttributeRequest createAttributeRequest = new CreateAttributeRequest
		{
			EntityName = entityName,
			Attribute = new PicklistAttributeMetadata
			{
				SchemaName = schemaName,
				LogicalName = schemaName.ToLower(),
				DisplayName = BuildLabel(displayName, displayNameZh, displayNameEn),
				RequiredLevel = new AttributeRequiredLevelManagedProperty(AttributeRequiredLevel.None),
				Description = LabelHelper.Create(description),
				OptionSet = new OptionSetMetadata
				{
					IsGlobal = false,
					OptionSetType = OptionSetType.Picklist
				}
			}
		};
		foreach (KeyValuePair<string, int> option in options)
		{
			((PicklistAttributeMetadata)createAttributeRequest.Attribute).OptionSet.Options.Add(new OptionMetadata(LabelHelper.Create(option.Key), option.Value));
		}
		_service.Execute(createAttributeRequest);
		Console.WriteLine($"  ✓ {schemaName} ({displayName}) - 选项集({options.Count}项)");
	}

	public void CreateMultiSelectPicklistField(string entityName, string schemaName, string displayName, string description, Dictionary<string, int> options, string displayNameZh = "", string displayNameEn = "")
	{
		CreateAttributeRequest createAttributeRequest = new CreateAttributeRequest
		{
			EntityName = entityName,
			Attribute = new MultiSelectPicklistAttributeMetadata
			{
				SchemaName = schemaName,
				LogicalName = schemaName.ToLower(),
				DisplayName = BuildLabel(displayName, displayNameZh, displayNameEn),
				RequiredLevel = new AttributeRequiredLevelManagedProperty(AttributeRequiredLevel.None),
				Description = LabelHelper.Create(description),
				OptionSet = new OptionSetMetadata
				{
					IsGlobal = false,
					OptionSetType = OptionSetType.Picklist
				}
			}
		};
		foreach (KeyValuePair<string, int> option in options)
		{
			((MultiSelectPicklistAttributeMetadata)createAttributeRequest.Attribute).OptionSet.Options.Add(new OptionMetadata(LabelHelper.Create(option.Key), option.Value));
		}
		_service.Execute(createAttributeRequest);
		Console.WriteLine($"  ✓ {schemaName} ({displayName}) - 多选选项集({options.Count}项)");
	}

	public void CreateBooleanField(string entityName, string schemaName, string displayName, string description, string trueLabel = "是", string falseLabel = "否", string displayNameZh = "", string displayNameEn = "")
	{
		CreateAttributeRequest request = new CreateAttributeRequest
		{
			EntityName = entityName,
			Attribute = new BooleanAttributeMetadata
			{
				SchemaName = schemaName,
				LogicalName = schemaName.ToLower(),
				DisplayName = BuildLabel(displayName, displayNameZh, displayNameEn),
				RequiredLevel = new AttributeRequiredLevelManagedProperty(AttributeRequiredLevel.None),
				Description = LabelHelper.Create(description),
				OptionSet = new BooleanOptionSetMetadata(new OptionMetadata(LabelHelper.Create(trueLabel), 1), new OptionMetadata(LabelHelper.Create(falseLabel), 0))
			}
		};
		_service.Execute(request);
		Console.WriteLine($"  ✓ {schemaName} ({displayName}) - 布尔");
	}

	public void CreateLookupField(string entityName, string schemaName, string displayName, string description, string targetEntityName, string targetEntityDisplayName, string displayNameZh = "", string displayNameEn = "")
	{
		Console.WriteLine("创建Lookup字段: " + schemaName + " -> " + targetEntityName);
		string referencedAttribute = targetEntityName + "id";
		CreateOneToManyRequest request = new CreateOneToManyRequest
		{
			Lookup = new LookupAttributeMetadata
			{
				SchemaName = schemaName,
				LogicalName = schemaName.ToLower(),
				DisplayName = BuildLabel(displayName, displayNameZh, displayNameEn),
				RequiredLevel = new AttributeRequiredLevelManagedProperty(AttributeRequiredLevel.None),
				Description = LabelHelper.Create(description)
			},
			OneToManyRelationship = new OneToManyRelationshipMetadata
			{
				SchemaName = $"mcs_{entityName}_{targetEntityName}_{schemaName}",
				ReferencedEntity = targetEntityName,
				ReferencingEntity = entityName,
				ReferencedAttribute = referencedAttribute,
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

	public void InsertOptionValue(string entityName, string fieldName, string label, int value)
	{
		Console.WriteLine($"插入选项: {entityName}.{fieldName} -> {label} ({value})");
		InsertOptionValueRequest request = new InsertOptionValueRequest
		{
			EntityLogicalName = entityName,
			AttributeLogicalName = fieldName,
			Label = LabelHelper.Create(label),
			Value = value
		};
		_service.Execute(request);
		Console.WriteLine("  ✓ 选项已插入");
	}

	public void UpdatePicklistOptions(string entityName, string fieldName, Dictionary<string, int> options)
	{
		Console.WriteLine("更新选项集: " + entityName + "." + fieldName);
		foreach (KeyValuePair<string, int> option in options)
		{
			try
			{
				InsertOptionValue(entityName, fieldName, option.Key, option.Value);
			}
			catch (Exception ex)
			{
				Console.WriteLine("  ⚠ " + option.Key + ": " + ex.Message);
			}
		}
	}

	public void UpdateOptionLabels(string entityName, string fieldName, Dictionary<int, string> valueToLabel)
	{
		Console.WriteLine("更新选项集标签: " + entityName + "." + fieldName);
		foreach (KeyValuePair<int, string> item in valueToLabel)
		{
			try
			{
				Console.WriteLine($"  更新选项 {item.Key} -> {item.Value}");
				UpdateOptionValueRequest request = new UpdateOptionValueRequest
				{
					EntityLogicalName = entityName,
					AttributeLogicalName = fieldName,
					Value = item.Key,
					Label = LabelHelper.Create(item.Value)
				};
				_service.Execute(request);
				Console.WriteLine("    ✓ 已更新");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"    ⚠ {item.Key}: {ex.Message}");
			}
		}
	}

	public void DeleteField(string entityName, string fieldName)
	{
		Console.WriteLine("删除字段: " + entityName + "." + fieldName);
		DeleteAttributeRequest request = new DeleteAttributeRequest
		{
			EntityLogicalName = entityName,
			LogicalName = fieldName
		};
		_service.Execute(request);
		Console.WriteLine("  ✓ 字段已删除");
	}

	public List<AttributeMetadata> GetFields(string entityName)
	{
		RetrieveEntityRequest request = new RetrieveEntityRequest
		{
			EntityFilters = EntityFilters.Attributes,
			LogicalName = entityName,
			RetrieveAsIfPublished = true
		};
		RetrieveEntityResponse retrieveEntityResponse = (RetrieveEntityResponse)_service.Execute(request);
		return retrieveEntityResponse.EntityMetadata.Attributes.ToList();
	}

	public void AddEntityToSolution(string entityName, string solutionUniqueName)
	{
		Console.WriteLine($"添加实体 {entityName} 到解决方案 {solutionUniqueName}...");
		Guid? entityId = GetEntityId(entityName);
		if (!entityId.HasValue)
		{
			throw new Exception("实体 " + entityName + " 不存在");
		}
		AddSolutionComponentRequest request = new AddSolutionComponentRequest
		{
			ComponentType = 1,
			ComponentId = entityId.Value,
			SolutionUniqueName = solutionUniqueName,
			// 只添加实体本身，不自动带入关联/依赖实体（避免污染 Solution，2026-07-20 修正）
			AddRequiredComponents = false
		};
		_service.Execute(request);
		Console.WriteLine("  ✓ 添加成功!");
	}

	public void RemoveEntityFromSolution(string entityName, string solutionUniqueName)
	{
		Console.WriteLine("从解决方案移除实体 " + entityName + "...");
		QueryExpression queryExpression = new QueryExpression("solution");
		queryExpression.ColumnSet = new ColumnSet("solutionid");
		queryExpression.Criteria = new FilterExpression
		{
			Conditions = 
			{
				new ConditionExpression("uniquename", ConditionOperator.Equal, solutionUniqueName)
			}
		};
		QueryExpression query = queryExpression;
		EntityCollection entityCollection = _service.RetrieveMultiple(query);
		if (entityCollection.Entities.Count == 0)
		{
			throw new Exception("解决方案 " + solutionUniqueName + " 不存在");
		}
		Guid id = entityCollection.Entities[0].Id;
		Guid? entityId = GetEntityId(entityName);
		if (!entityId.HasValue)
		{
			throw new Exception("实体 " + entityName + " 不存在");
		}
		queryExpression = new QueryExpression("solutioncomponent");
		queryExpression.ColumnSet = new ColumnSet("solutioncomponentid");
		queryExpression.Criteria = new FilterExpression
		{
			Conditions = 
			{
				new ConditionExpression("solutionid", ConditionOperator.Equal, id),
				new ConditionExpression("objectid", ConditionOperator.Equal, entityId.Value),
				new ConditionExpression("componenttype", ConditionOperator.Equal, 1)
			}
		};
		QueryExpression query2 = queryExpression;
		EntityCollection entityCollection2 = _service.RetrieveMultiple(query2);
		foreach (Entity entity in entityCollection2.Entities)
		{
			_service.Delete("solutioncomponent", entity.Id);
		}
		Console.WriteLine("  ✓ 已从解决方案移除");
	}

	public List<(string LogicalName, Guid EntityId)> GetSolutionEntities(string solutionUniqueName)
	{
		List<(string, Guid)> list = new List<(string, Guid)>();
		QueryExpression queryExpression = new QueryExpression("solution");
		queryExpression.ColumnSet = new ColumnSet("solutionid");
		queryExpression.Criteria = new FilterExpression
		{
			Conditions = 
			{
				new ConditionExpression("uniquename", ConditionOperator.Equal, solutionUniqueName)
			}
		};
		QueryExpression query = queryExpression;
		EntityCollection entityCollection = _service.RetrieveMultiple(query);
		if (entityCollection.Entities.Count == 0)
		{
			return list;
		}
		Guid id = entityCollection.Entities[0].Id;
		queryExpression = new QueryExpression("solutioncomponent");
		queryExpression.ColumnSet = new ColumnSet("objectid");
		queryExpression.Criteria = new FilterExpression
		{
			Conditions = 
			{
				new ConditionExpression("solutionid", ConditionOperator.Equal, id),
				new ConditionExpression("componenttype", ConditionOperator.Equal, 1)
			}
		};
		QueryExpression query2 = queryExpression;
		EntityCollection entityCollection2 = _service.RetrieveMultiple(query2);
		foreach (Entity entity in entityCollection2.Entities)
		{
			Guid guid = (Guid)entity["objectid"];
			try
			{
				RetrieveEntityRequest request = new RetrieveEntityRequest
				{
					EntityFilters = EntityFilters.Entity,
					MetadataId = guid
				};
				RetrieveEntityResponse retrieveEntityResponse = (RetrieveEntityResponse)_service.Execute(request);
				list.Add((retrieveEntityResponse.EntityMetadata.LogicalName, guid));
			}
			catch
			{
			}
		}
		return list;
	}

	public void ListSolutions()
	{
		Console.WriteLine("环境中解决方案列表:\n");
		QueryExpression queryExpression = new QueryExpression("solution");
		queryExpression.ColumnSet = new ColumnSet("uniquename", "friendlyname", "publisherid", "version");
		queryExpression.Criteria = new FilterExpression
		{
			Conditions = 
			{
				new ConditionExpression("isvisible", ConditionOperator.Equal, true)
			}
		};
		queryExpression.Orders.Add(new OrderExpression("friendlyname", OrderType.Ascending));
		QueryExpression query = queryExpression;
		EntityCollection entityCollection = _service.RetrieveMultiple(query);
		Console.WriteLine($"{"Unique Name",-50} {"Display Name",-40} {"Version",-15}");
		Console.WriteLine(new string('-', 105));
		foreach (Entity entity in entityCollection.Entities)
		{
			string value = entity.GetAttributeValue<string>("uniquename") ?? "";
			string value2 = entity.GetAttributeValue<string>("friendlyname") ?? "";
			string value3 = entity.GetAttributeValue<string>("version") ?? "";
			Console.WriteLine($"{value,-50} {value2,-40} {value3,-15}");
		}
		Console.WriteLine($"\n总计: {entityCollection.Entities.Count} 个解决方案");
	}

	public void PublishAll()
	{
		Console.WriteLine("发布所有自定义项...");
		new PublishingService(_service).PublishAll();
		Console.WriteLine("  ✓ 发布完成");
	}

	public void PublishEntity(string entityName)
	{
		Console.WriteLine("发布实体: " + entityName);
		new PublishingService(_service).PublishEntities(entityName);
		Console.WriteLine("  ✓ 发布完成");
	}

	/// <summary>
	/// 导入 Solution ZIP（2026-07-31，Ribbon 部署链路：导出→合并 customizations.xml→导入）
	/// 非托管叠加导入，导入后不自动发布（由调用方按需发布指定实体）
	/// </summary>
	public void ImportSolution(string zipPath)
	{
		Console.WriteLine($">>> 导入 Solution: {zipPath}");
		var zipBytes = File.ReadAllBytes(zipPath);
		Console.WriteLine($"  包大小: {zipBytes.Length / 1024} KB");
		var req = new ImportSolutionRequest
		{
			CustomizationFile = zipBytes,
			PublishWorkflows = false,
			OverwriteUnmanagedCustomizations = true,
			ImportJobId = Guid.NewGuid()
		};
		_service.Execute(req);
		Console.WriteLine("  ✅ 导入完成");
	}

	/// <summary>
	/// 幂等部署实体 RibbonDiffXml（2026-07-31，融资管理提交审批按钮显隐）
	/// 链路：导出实体所在 Solution → 解包合并 customizations.xml 实体 RibbonDiffXml 节点
	/// （按 Id 前缀清理旧节点后合入片段 CustomActions/CommandDefinitions/RuleDefinitions/LocLabels）
	/// → 重新打包 → 非托管导入 → 发布实体。
	/// 背景：元数据实体 entity 不支持 ribbondiffxml 直接读写，实体 Ribbon 只能随 Solution 导入生效。
	/// 片段文件参考 Code/Customizations/Ribbon/mcs_fsm_data.ribbon.xml。
	/// </summary>
	/// <param name="entityName">实体逻辑名</param>
	/// <param name="ribbonXmlPath">RibbonDiffXml 片段文件路径</param>
	/// <param name="solutionName">实体所在 Solution 唯一名（导出/导入载体）</param>
	/// <param name="workDir">解包工作目录</param>
	/// <param name="idPrefix">幂等清理前缀（默认 mcs.{实体名}.）</param>
	public void DeployRibbonDiff(string entityName, string ribbonXmlPath, string solutionName, string workDir, string idPrefix = null)
	{
		idPrefix ??= "mcs." + entityName + ".";
		Console.WriteLine($">>> 部署 RibbonDiffXml: {entityName}（载体 Solution={solutionName}，幂等前缀 {idPrefix}）");

		// 1. 导出 Solution 并解包
		Directory.CreateDirectory(workDir);
		var zipPath = Path.Combine(workDir, solutionName + ".zip");
		ExportSolution(solutionName, zipPath);
		var extractDir = Path.Combine(workDir, "unpacked");
		if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
		System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, extractDir);
		var custPath = Path.Combine(extractDir, "customizations.xml");
		if (!File.Exists(custPath)) throw new FileNotFoundException("包内缺少 customizations.xml", custPath);

		// 2. 定位实体节点的 RibbonDiffXml
		var doc = XDocument.Load(custPath, LoadOptions.PreserveWhitespace);
		var snippetRoot = XDocument.Load(ribbonXmlPath).Root ?? throw new InvalidOperationException("片段文件缺少 RibbonDiffXml 根节点");
		var entityEl = doc.Root?.Element("Entities")?.Elements("Entity")
			.FirstOrDefault(e => string.Equals(e.Element("Name")?.Value?.Trim(), entityName, StringComparison.OrdinalIgnoreCase))
			?? throw new InvalidOperationException($"customizations.xml 中未找到实体节点: {entityName}");
		var ribbonEl = entityEl.Element("RibbonDiffXml");
		if (ribbonEl == null)
		{
			ribbonEl = new XElement("RibbonDiffXml");
			entityEl.Add(ribbonEl);
			Console.WriteLine("  实体节点无 RibbonDiffXml，已新建");
		}

		bool HasPrefix(XElement el) => ((string)el.Attribute("Id"))?.StartsWith(idPrefix, StringComparison.OrdinalIgnoreCase) == true;

		// 3. 合入平铺节（CustomActions / CommandDefinitions / LocLabels）：先删同前缀旧节点再追加
		foreach (var sectionName in new[] { "CustomActions", "CommandDefinitions", "LocLabels" })
		{
			var snippetSection = snippetRoot.Element(sectionName);
			if (snippetSection == null || !snippetSection.Elements().Any()) continue;
			var section = ribbonEl.Element(sectionName);
			if (section == null) { section = new XElement(sectionName); ribbonEl.Add(section); }
			section.Elements().Where(HasPrefix).Remove();
			foreach (var child in snippetSection.Elements()) section.Add(new XElement(child));
			Console.WriteLine($"  ✓ {sectionName}: 合入 {snippetSection.Elements().Count()} 个节点");
		}

		// 4. 合入 RuleDefinitions（二级容器：TabDisplayRules/DisplayRules/EnableRules）
		var snippetRules = snippetRoot.Element("RuleDefinitions");
		if (snippetRules != null)
		{
			var rules = ribbonEl.Element("RuleDefinitions");
			if (rules == null) { rules = new XElement("RuleDefinitions"); ribbonEl.Add(rules); }
			foreach (var container in snippetRules.Elements())
			{
				if (!container.Elements().Any()) continue;
				var currentContainer = rules.Element(container.Name);
				if (currentContainer == null) { currentContainer = new XElement(container.Name); rules.Add(currentContainer); }
				currentContainer.Elements().Where(HasPrefix).Remove();
				foreach (var child in container.Elements()) currentContainer.Add(new XElement(child));
				Console.WriteLine($"  ✓ RuleDefinitions/{container.Name.LocalName}: 合入 {container.Elements().Count()} 个节点");
			}
		}

		// 5. Templates：没有时补标准模板引用
		if (ribbonEl.Element("Templates") == null && snippetRoot.Element("Templates") != null)
			ribbonEl.Add(new XElement(snippetRoot.Element("Templates")));

		doc.Save(custPath);
		Console.WriteLine("  ✅ customizations.xml 合并完成");

		// 6. 重新打包 → 导入 → 发布
		var newZip = Path.Combine(workDir, solutionName + "_ribbon.zip");
		if (File.Exists(newZip)) File.Delete(newZip);
		System.IO.Compression.ZipFile.CreateFromDirectory(extractDir, newZip);
		Console.WriteLine($"  ✅ 已重新打包: {newZip}");

		ImportSolution(newZip);
		PublishEntity(entityName);
	}

	public void PublishEntities(params string[] entityNames)
	{
		if (entityNames.Length != 0)
		{
			Console.WriteLine("发布实体: " + string.Join(", ", entityNames));
			new PublishingService(_service).PublishEntities(entityNames);
			Console.WriteLine("  ✓ 发布完成");
		}
	}

	public Guid CloneDefaultView(string entityName, string newViewName, string filterConditionXml, string solutionName)
	{
		Console.WriteLine($"克隆默认视图: {entityName} -> {newViewName}");
		QueryExpression queryExpression = new QueryExpression("savedquery");
		queryExpression.ColumnSet = new ColumnSet("savedqueryid", "name", "layoutxml", "fetchxml");
		queryExpression.Criteria = new FilterExpression
		{
			Conditions =
			{
				new ConditionExpression("returnedtypecode", ConditionOperator.Equal, entityName),
				new ConditionExpression("querytype", ConditionOperator.Equal, 0),
				new ConditionExpression("isdefault", ConditionOperator.Equal, true)
			}
		};
		EntityCollection entityCollection = _service.RetrieveMultiple(queryExpression);
		if (entityCollection.Entities.Count == 0)
		{
			throw new Exception($"未找到 {entityName} 的默认 Public 视图");
		}

		Entity sourceView = entityCollection.Entities[0];
		string fetchXml = sourceView.GetAttributeValue<string>("fetchxml");
		string layoutXml = sourceView.GetAttributeValue<string>("layoutxml");
		string sourceName = sourceView.GetAttributeValue<string>("name");
		Console.WriteLine($"  源视图: {sourceName}");

		// 在 fetchxml 中插入过滤条件
		if (fetchXml.Contains("</filter>"))
		{
			int num = fetchXml.IndexOf("</filter>");
			fetchXml = fetchXml.Insert(num, filterConditionXml);
		}
		else
		{
			int num2 = fetchXml.LastIndexOf("</entity>");
			if (num2 > 0)
			{
				fetchXml = fetchXml.Insert(num2, $"<filter type=\"and\">{filterConditionXml}</filter>");
			}
		}

		Entity entity = new Entity("savedquery")
		{
			["name"] = newViewName,
			["description"] = newViewName,
			["querytype"] = 0,
			["isdefault"] = false,
			["returnedtypecode"] = entityName,
			["fetchxml"] = fetchXml,
			["layoutxml"] = layoutXml,
			["isuserdefined"] = true
		};
		Guid id = _service.Create(entity);
		Console.WriteLine($"  ✓ 视图已创建: {id}");

		if (!string.IsNullOrEmpty(solutionName))
		{
			AddSolutionComponentRequest request = new AddSolutionComponentRequest
			{
				ComponentType = 26,
				ComponentId = id,
				SolutionUniqueName = solutionName
			};
			_service.Execute(request);
			Console.WriteLine($"  ✓ 已添加到解决方案 {solutionName}");
		}

		PublishEntity(entityName);
		return id;
	}

	public void ExportSolution(string solutionName, string exportPath)
	{
		Console.WriteLine("导出解决方案: " + solutionName);
		ExportSolutionRequest request = new ExportSolutionRequest
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
		ExportSolutionResponse exportSolutionResponse = (ExportSolutionResponse)_service.Execute(request);
		File.WriteAllBytes(exportPath, exportSolutionResponse.ExportSolutionFile);
		Console.WriteLine("  ✓ 导出完成: " + exportPath);
		Console.WriteLine($"  文件大小: {exportSolutionResponse.ExportSolutionFile.Length / 1024} KB");
	}

	public void ExportFormXml(string entityName, string outputPath)
	{
		QueryExpression queryExpression = new QueryExpression("systemform");
		queryExpression.ColumnSet = new ColumnSet("formxml", "name");
		queryExpression.Criteria = new FilterExpression
		{
			Conditions = 
			{
				new ConditionExpression("objecttypecode", ConditionOperator.Equal, entityName),
				new ConditionExpression("type", ConditionOperator.Equal, 2)
			}
		};
		QueryExpression query = queryExpression;
		EntityCollection entityCollection = _service.RetrieveMultiple(query);
		if (entityCollection.Entities.Count == 0)
		{
			Console.WriteLine("未找到 Main 窗体");
			return;
		}
		string attributeValue = entityCollection.Entities[0].GetAttributeValue<string>("formxml");
		File.WriteAllText(outputPath, attributeValue);
		Console.WriteLine("FormXml 已导出到: " + outputPath);
	}

	public void CheckFormFields(string entityName)
	{
		Console.WriteLine("检查 " + entityName + " 的所有窗体...\n");
		QueryExpression queryExpression = new QueryExpression("systemform");
		queryExpression.ColumnSet = new ColumnSet("formid", "name", "type", "formxml");
		queryExpression.Criteria = new FilterExpression
		{
			Conditions = 
			{
				new ConditionExpression("objecttypecode", ConditionOperator.Equal, entityName)
			}
		};
		QueryExpression query = queryExpression;
		EntityCollection entityCollection = _service.RetrieveMultiple(query);
		Console.WriteLine($"找到 {entityCollection.Entities.Count} 个窗体\n");
		foreach (Entity entity in entityCollection.Entities)
		{
			Guid attributeValue = entity.GetAttributeValue<Guid>("formid");
			string attributeValue2 = entity.GetAttributeValue<string>("name");
			int? num = entity.GetAttributeValue<OptionSetValue>("type")?.Value;
			string attributeValue3 = entity.GetAttributeValue<string>("formxml");
			if (1 == 0)
			{
			}
			string text = num switch
			{
				2 => "Main", 
				6 => "Mobile", 
				7 => "Dashboard", 
				11 => "Quick Create", 
				_ => $"Unknown({num})", 
			};
			if (1 == 0)
			{
			}
			string value = text;
			Console.WriteLine($"=== {attributeValue2} (type={value}, id={attributeValue}) ===");
			if (attributeValue3 != null)
			{
				MatchCollection matchCollection = Regex.Matches(attributeValue3, "datafieldname=\"([^\"]+)\"");
				Console.WriteLine($"字段数量: {matchCollection.Count}");
				foreach (Match item in matchCollection)
				{
					Console.WriteLine("  - " + item.Groups[1].Value);
				}
			}
			Console.WriteLine();
		}
	}

	public void CleanFormFooter(string entityName)
	{
		Console.WriteLine("清理 " + entityName + " 窗体 footer...");
		QueryExpression queryExpression = new QueryExpression("systemform");
		queryExpression.ColumnSet = new ColumnSet("formxml", "name");
		queryExpression.Criteria = new FilterExpression
		{
			Conditions = 
			{
				new ConditionExpression("objecttypecode", ConditionOperator.Equal, entityName),
				new ConditionExpression("type", ConditionOperator.Equal, 2)
			}
		};
		QueryExpression query = queryExpression;
		EntityCollection entityCollection = _service.RetrieveMultiple(query);
		if (entityCollection.Entities.Count == 0)
		{
			return;
		}
		foreach (Entity entity2 in entityCollection.Entities)
		{
			string attributeValue = entity2.GetAttributeValue<string>("formxml");
			int num = attributeValue.IndexOf("<footer");
			if (num < 0)
			{
				continue;
			}
			string text = attributeValue.Substring(num);
			int num2 = text.IndexOf("<rows>");
			int num3 = text.IndexOf("</rows>");
			if (num2 < 0 || num3 < 0)
			{
				continue;
			}
			string input = text.Substring(num2 + "<rows>".Length, num3 - num2 - "<rows>".Length);
			MatchCollection matchCollection = Regex.Matches(input, "<row>.*?</row>", RegexOptions.Singleline);
			string text2 = "";
			int num4 = 0;
			foreach (Match item in matchCollection)
			{
				string value = item.Value;
				if (value.Contains("datafieldname="))
				{
					num4++;
					Console.WriteLine("  移除 footer 中的错误字段");
				}
				else
				{
					text2 += value;
				}
			}
			if (num4 > 0)
			{
				string text3 = text.Substring(0, num2 + "<rows>".Length) + text2 + text.Substring(num3);
				string value2 = attributeValue.Substring(0, num) + text3;
				Entity entity = new Entity("systemform", entity2.Id);
				entity["formxml"] = value2;
				_service.Update(entity);
				Console.WriteLine($"  ✓ 清理完成，移除了 {num4} 个错误字段");
			}
			else
			{
				Console.WriteLine("  无需清理");
			}
		}
	}

	private string GetControlClassId(AttributeTypeCode attributeType)
	{
		if (1 == 0)
		{
		}
		string result;
		switch (attributeType)
		{
		case AttributeTypeCode.Lookup:
			result = "{270BD3DB-D9AF-4782-9025-509E298DEC0A}";
			break;
		case AttributeTypeCode.Picklist:
			result = "{3EF39988-22BB-4f0b-BBBE-64B5A3748AEE}";
			break;
		case AttributeTypeCode.Boolean:
			result = "{67FAC785-CD58-4f9f-ABB3-4B7DDC6ED5ED}";
			break;
		case AttributeTypeCode.DateTime:
			result = "{5B773807-9FB2-42db-97C3-7A91EFFB8E5D}";
			break;
		case AttributeTypeCode.Decimal:
		case AttributeTypeCode.Double:
		case AttributeTypeCode.Integer:
		case AttributeTypeCode.Money:
			result = "{C6D124CA-7EDA-4a60-AEA9-3D5F7E24E023}";
			break;
		case AttributeTypeCode.Memo:
			result = "{4273EDBD-AC1D-40d3-9FB2-095C621B552D}";
			break;
		default:
			result = "{4273EDBD-AC1D-40d3-9FB2-095C621B552D}";
			break;
		}
		if (1 == 0)
		{
		}
		return result;
	}

	public void FixFormLookupControls(string entityName)
	{
		Console.WriteLine("修复 " + entityName + " 主窗体 Lookup 控件...");
		List<AttributeMetadata> fields = GetFields(entityName);
		HashSet<string> lookupFields = (from a in fields
			where a.AttributeType == AttributeTypeCode.Lookup
			select a.LogicalName).ToHashSet<string>(StringComparer.OrdinalIgnoreCase);
		if (lookupFields.Count == 0)
		{
			Console.WriteLine("  该实体没有 Lookup 字段");
			return;
		}
		Console.WriteLine($"  发现 {lookupFields.Count} 个 Lookup 字段");
		QueryExpression queryExpression = new QueryExpression("systemform");
		queryExpression.ColumnSet = new ColumnSet("formxml", "name", "type");
		queryExpression.Criteria = new FilterExpression
		{
			Conditions = 
			{
				new ConditionExpression("objecttypecode", ConditionOperator.Equal, entityName),
				new ConditionExpression("type", ConditionOperator.Equal, 2)
			}
		};
		QueryExpression query = queryExpression;
		EntityCollection entityCollection = _service.RetrieveMultiple(query);
		if (entityCollection.Entities.Count == 0)
		{
			Console.WriteLine("  ✗ 未找到 type=2 的主窗体");
			return;
		}
		string lookupClassId = GetControlClassId(AttributeTypeCode.Lookup);
		string text = Regex.Escape("{4273EDBD-AC1D-40d3-9FB2-095C621B552D}");
		Regex regex = new Regex("<control\\s+id=\"([^\"]+)\"\\s+classid=\"" + text + "\"\\s+datafieldname=\"([^\"]+)\"\\s*/?>", RegexOptions.IgnoreCase);
		foreach (Entity entity2 in entityCollection.Entities)
		{
			string attributeValue = entity2.GetAttributeValue<string>("formxml");
			string attributeValue2 = entity2.GetAttributeValue<string>("name");
			string text2 = regex.Replace(attributeValue, delegate(Match match)
			{
				string value = match.Groups[1].Value;
				string value2 = match.Groups[2].Value;
				if (lookupFields.Contains(value2))
				{
					Console.WriteLine("    ✓ 修复 " + value2 + ": 文本框 → Lookup");
					return $"<control id=\"{value}\" classid=\"{lookupClassId}\" datafieldname=\"{value2}\" />";
				}
				return match.Value;
			});
			if ((object)text2 != attributeValue && text2 != attributeValue)
			{
				Entity entity = new Entity("systemform", entity2.Id);
				entity["formxml"] = text2;
				_service.Update(entity);
				Console.WriteLine("  ✓ 窗体 " + attributeValue2 + " 已更新");
			}
			else
			{
				Console.WriteLine("  ⊘ 窗体 " + attributeValue2 + " 无需修复");
			}
		}
		PublishEntity(entityName);
	}

	public void UpdateMainForm(string entityName, Dictionary<string, string> fields)
	{
		Console.WriteLine("更新 " + entityName + " 主窗体...");
		Dictionary<string, AttributeTypeCode?> dictionary = GetFields(entityName).ToDictionary<AttributeMetadata, string, AttributeTypeCode?>((AttributeMetadata a) => a.LogicalName, (AttributeMetadata a) => a.AttributeType, StringComparer.OrdinalIgnoreCase);
		QueryExpression queryExpression = new QueryExpression("systemform");
		queryExpression.ColumnSet = new ColumnSet("formxml", "name", "type");
		queryExpression.Criteria = new FilterExpression
		{
			Conditions = 
			{
				new ConditionExpression("objecttypecode", ConditionOperator.Equal, entityName),
				new ConditionExpression("type", ConditionOperator.Equal, 2)
			}
		};
		QueryExpression query = queryExpression;
		EntityCollection entityCollection = _service.RetrieveMultiple(query);
		if (entityCollection.Entities.Count == 0)
		{
			Console.WriteLine("  ✗ 未找到 type=2 的主窗体");
			return;
		}
		foreach (Entity entity2 in entityCollection.Entities)
		{
			string attributeValue = entity2.GetAttributeValue<string>("formxml");
			string attributeValue2 = entity2.GetAttributeValue<string>("name");
			Console.WriteLine("  处理窗体: " + attributeValue2);
			int num = 0;
			Dictionary<string, string> dictionary2 = new Dictionary<string, string>();
			foreach (KeyValuePair<string, string> field in fields)
			{
				if (attributeValue.Contains("datafieldname=\"" + field.Key + "\""))
				{
					Console.WriteLine("    ⊘ " + field.Key + " 已存在，跳过");
					continue;
				}
				dictionary2[field.Key] = field.Value;
				num++;
			}
			if (num == 0)
			{
				Console.WriteLine("  没有新字段需要添加");
				continue;
			}
			string text = "";
			List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>(dictionary2);
			for (int num2 = 0; num2 < list.Count; num2 += 2)
			{
				string value = Guid.NewGuid().ToString("B");
				string value2 = Guid.NewGuid().ToString("B");
				text += "<row>";
				string key = list[num2].Key;
				AttributeTypeCode? value4;
				string value3 = (dictionary.TryGetValue(key, out value4) ? GetControlClassId(value4 ?? AttributeTypeCode.String) : GetControlClassId(AttributeTypeCode.String));
				text += $"<cell id=\"{value}\" locklevel=\"0\" colspan=\"1\" rowspan=\"1\"><labels><label description=\"{list[num2].Value}\" languagecode=\"1033\" /><label description=\"{list[num2].Value}\" languagecode=\"2052\" /></labels><control id=\"{key}\" classid=\"{value3}\" datafieldname=\"{key}\" /></cell>";
				if (num2 + 1 < list.Count)
				{
					string key2 = list[num2 + 1].Key;
					AttributeTypeCode? value6;
					string value5 = (dictionary.TryGetValue(key2, out value6) ? GetControlClassId(value6 ?? AttributeTypeCode.String) : GetControlClassId(AttributeTypeCode.String));
					text += $"<cell id=\"{value2}\" locklevel=\"0\" colspan=\"1\" rowspan=\"1\"><labels><label description=\"{list[num2 + 1].Value}\" languagecode=\"1033\" /><label description=\"{list[num2 + 1].Value}\" languagecode=\"2052\" /></labels><control id=\"{key2}\" classid=\"{value5}\" datafieldname=\"{key2}\" /></cell>";
				}
				text += "</row>";
				Console.WriteLine("    + " + list[num2].Key + " + " + ((num2 + 1 < list.Count) ? list[num2 + 1].Key : ""));
			}
			int startIndex = attributeValue.IndexOf("<control id=");
			int num3 = attributeValue.IndexOf("</row>", startIndex);
			if (num3 > 0)
			{
				num3 += "</row>".Length;
				string value7 = attributeValue.Insert(num3, text);
				Entity entity = new Entity("systemform", entity2.Id);
				entity["formxml"] = value7;
				_service.Update(entity);
				Console.WriteLine($"  ✓ 窗体已更新，添加了 {num} 个字段（两列布局）");
			}
		}
		PublishEntity(entityName);
	}

	/// <summary>
	/// 更新主窗体指定字段单元格的显示标签（1033/2052），并发布实体。
	/// 用于修复 Form 级标签与字段显示名不一致（Form 级标签优先于字段显示名）。
	/// 2026-07-29 新增（禅道 #1408 表单标签修复，经用户批准扩展公共方法）。
	/// </summary>
	public void UpdateFormFieldLabel(string entityName, string fieldName, string zhLabel, string enLabel = null)
	{
		Console.WriteLine($"更新 {entityName} 主窗体字段标签: {fieldName} -> 2052={zhLabel}" + (enLabel != null ? $", 1033={enLabel}" : ""));
		QueryExpression query = new QueryExpression("systemform");
		query.ColumnSet = new ColumnSet("formxml", "name", "type");
		query.Criteria = new FilterExpression
		{
			Conditions =
			{
				new ConditionExpression("objecttypecode", ConditionOperator.Equal, entityName),
				new ConditionExpression("type", ConditionOperator.Equal, 2)
			}
		};
		EntityCollection forms = _service.RetrieveMultiple(query);
		if (forms.Entities.Count == 0)
		{
			Console.WriteLine("  ✗ 未找到 type=2 的主窗体");
			return;
		}
		foreach (Entity form in forms.Entities)
		{
			string formXml = form.GetAttributeValue<string>("formxml");
			string formName = form.GetAttributeValue<string>("name");
			string marker = "datafieldname=\"" + fieldName + "\"";
			int fieldIndex = formXml.IndexOf(marker, StringComparison.Ordinal);
			if (fieldIndex < 0)
			{
				Console.WriteLine($"  ⊘ 窗体 {formName} 中未找到字段 {fieldName}，跳过");
				continue;
			}
			int cellStart = formXml.LastIndexOf("<cell ", fieldIndex, StringComparison.Ordinal);
			int cellEnd = formXml.IndexOf("</cell>", fieldIndex, StringComparison.Ordinal);
			if (cellStart < 0 || cellEnd < 0)
			{
				Console.WriteLine($"  ✗ 窗体 {formName} 中未定位到字段所在单元格，跳过");
				continue;
			}
			cellEnd += "</cell>".Length;
			string cell = formXml.Substring(cellStart, cellEnd - cellStart);
			string newCell = cell;
			if (enLabel != null)
			{
				newCell = SetCellLabelDescription(newCell, 1033, enLabel);
			}
			newCell = SetCellLabelDescription(newCell, 2052, zhLabel);
			if (newCell == cell)
			{
				Console.WriteLine($"  ⊘ 窗体 {formName} 标签无需变更");
				continue;
			}
			Entity update = new Entity("systemform", form.Id);
			update["formxml"] = formXml.Substring(0, cellStart) + newCell + formXml.Substring(cellEnd);
			_service.Update(update);
			Console.WriteLine($"  ✓ 窗体 {formName} 字段 {fieldName} 标签已更新");
		}
		PublishEntity(entityName);
	}

	/// <summary>
	/// 替换单元格内指定语言 label 的 description；不存在则插入到 labels 节点内。
	/// </summary>
	private static string SetCellLabelDescription(string cell, int languageCode, string description)
	{
		string escaped = description.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
		string replacement = $"<label description=\"{escaped}\" languagecode=\"{languageCode}\" />";
		var rx = new System.Text.RegularExpressions.Regex("<label description=\"[^\"]*\" languagecode=\"" + languageCode + "\"\\s*/>");
		if (rx.IsMatch(cell))
		{
			return rx.Replace(cell, replacement, 1);
		}
		int idx = cell.IndexOf("<labels>", StringComparison.Ordinal);
		if (idx < 0)
		{
			return cell;
		}
		idx += "<labels>".Length;
		return cell.Substring(0, idx) + replacement + cell.Substring(idx);
	}

	public void RemoveFieldsFromForm(string entityName, params string[] fieldNames)
	{
		Console.WriteLine("从 " + entityName + " 主窗体移除字段: " + string.Join(", ", fieldNames));
		QueryExpression queryExpression = new QueryExpression("systemform");
		queryExpression.ColumnSet = new ColumnSet("formxml", "name", "type");
		queryExpression.Criteria = new FilterExpression
		{
			Conditions = 
			{
				new ConditionExpression("objecttypecode", ConditionOperator.Equal, entityName),
				new ConditionExpression("type", ConditionOperator.Equal, 2)
			}
		};
		QueryExpression query = queryExpression;
		EntityCollection entityCollection = _service.RetrieveMultiple(query);
		if (entityCollection.Entities.Count == 0)
		{
			Console.WriteLine("  ✗ 未找到 type=2 的主窗体");
			return;
		}
		foreach (Entity entity2 in entityCollection.Entities)
		{
			string text = entity2.GetAttributeValue<string>("formxml");
			string attributeValue = entity2.GetAttributeValue<string>("name");
			bool flag = false;
			foreach (string text2 in fieldNames)
			{
				if (!text.Contains("datafieldname=\"" + text2 + "\""))
				{
					Console.WriteLine($"  ⊘ {text2} 不在窗体 {attributeValue} 中，跳过");
					continue;
				}
				string pattern = $"<cell[^>]*>.*?<control[^>]*datafieldname=\"{text2}\"[^>]*/>.*?</cell>|<cell[^>]*<control[^>]*datafieldname=\"{text2}\"[^>]*/>[^>]*/>";
				string text3 = text;
				text = Regex.Replace(text, pattern, "", RegexOptions.Singleline);
				if (text != text3)
				{
					flag = true;
					Console.WriteLine("  ✓ 从窗体 " + attributeValue + " 移除 " + text2);
				}
			}
			if (flag)
			{
				Entity entity = new Entity("systemform", entity2.Id);
				entity["formxml"] = text;
				_service.Update(entity);
			}
		}
		PublishEntity(entityName);
	}

	private string GetLookupTargetEntity(string sourceEntity, string fieldName)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			["mcs_credit_record"] = "mcs_credit_record",
			["mcs_accountid"] = "account",
			["mcs_credit_item"] = "mcs_credit_items",
			["mcs_credititem"] = "mcs_credit_items",
			["mcs_listvalue"] = "mcs_credititem_value",
			["mcs_businessunit"] = "mcs_bu",
			["mcs_subsidiary"] = "mcs_region",
			["mcs_nation"] = "mcs_country",
			["mcs_trade_pttype"] = "mcs_trade_pttype"
		};
		if (dictionary.TryGetValue(fieldName, out var value))
		{
			return value;
		}
		return null;
	}

	private string GetLookupViewId(string entityName)
	{
		QueryExpression queryExpression = new QueryExpression("savedquery");
		queryExpression.ColumnSet = new ColumnSet("savedqueryid", "name");
		queryExpression.Criteria = new FilterExpression
		{
			Conditions = 
			{
				new ConditionExpression("returnedtypecode", ConditionOperator.Equal, entityName),
				new ConditionExpression("querytype", ConditionOperator.Equal, 64)
			}
		};
		QueryExpression query = queryExpression;
		EntityCollection entityCollection = _service.RetrieveMultiple(query);
		if (entityCollection.Entities.Count > 0)
		{
			return entityCollection.Entities[0].GetAttributeValue<Guid>("savedqueryid").ToString("B");
		}
		return null;
	}

	private string BuildLookupParameters(string fieldName, string viewId, Dictionary<string, (string dependentField, string dependentEntity, string filterRelationship)> lookupFilterMap)
	{
		string text = "<parameters>";
		if (!string.IsNullOrEmpty(viewId))
		{
			text += $"<DefaultViewId>{viewId}</DefaultViewId><AvailableViewIds>{viewId}</AvailableViewIds>";
		}
		if (lookupFilterMap != null && lookupFilterMap.TryGetValue(fieldName, out (string, string, string) value))
		{
			if (!string.IsNullOrEmpty(value.Item1))
			{
				text = text + "<DependentAttributeName>" + value.Item1 + "</DependentAttributeName>";
			}
			if (!string.IsNullOrEmpty(value.Item2))
			{
				text = text + "<DependentAttributeType>" + value.Item2 + "</DependentAttributeType>";
			}
			if (!string.IsNullOrEmpty(value.Item3))
			{
				text = text + "<FilterRelationshipName>" + value.Item3 + "</FilterRelationshipName>";
			}
		}
		return text + "</parameters>";
	}

	public void RearrangeForm(string entityName, Dictionary<string, List<(string fieldName, string displayName)>> fieldGroups, HashSet<string> lookupFields = null, Dictionary<string, (string dependentField, string dependentEntity, string filterRelationship)> lookupFilterMap = null, HashSet<string> picklistFields = null)
	{
		Console.WriteLine("重新排列 " + entityName + " 窗体...");
		QueryExpression queryExpression = new QueryExpression("systemform");
		queryExpression.ColumnSet = new ColumnSet("formxml", "name");
		queryExpression.Criteria = new FilterExpression
		{
			Conditions = 
			{
				new ConditionExpression("objecttypecode", ConditionOperator.Equal, entityName),
				new ConditionExpression("type", ConditionOperator.Equal, 2)
			}
		};
		QueryExpression query = queryExpression;
		EntityCollection entityCollection = _service.RetrieveMultiple(query);
		if (entityCollection.Entities.Count == 0)
		{
			Console.WriteLine("  ✗ 未找到主窗体");
			return;
		}
		foreach (Entity entity2 in entityCollection.Entities)
		{
			string attributeValue = entity2.GetAttributeValue<string>("formxml");
			string text = "";
			foreach (KeyValuePair<string, List<(string, string)>> fieldGroup in fieldGroups)
			{
				string text2 = Guid.NewGuid().ToString("B");
				string key = fieldGroup.Key;
				List<(string, string)> value = fieldGroup.Value;
				text = text + "<section showlabel=\"true\" showbar=\"true\" IsUserDefined=\"1\" id=\"" + text2 + "\" layout=\"varwidth\" celllabelalignment=\"Left\" celllabelposition=\"Left\" columns=\"11\" labelwidth=\"115\">";
				text = text + "<labels><label description=\"" + key + "\" languagecode=\"2052\" /></labels>";
				text += "<rows>";
				for (int i = 0; i < value.Count; i += 2)
				{
					string value2 = Guid.NewGuid().ToString("B");
					string value3 = Guid.NewGuid().ToString("B");
					text += "<row>";
					bool flag = lookupFields?.Contains(value[i].Item1) ?? false;
					bool flag2 = picklistFields?.Contains(value[i].Item1) ?? false;
					string value4 = (flag ? "{270BD3DB-D9AF-4782-9025-509E298DEC0A}" : (flag2 ? "{3EF39988-22BB-4f0b-BBBE-64B5A3748AEE}" : "{4273EDBD-AC1D-40d3-9FB2-095C621B552D}"));
					if (flag)
					{
						string lookupTargetEntity = GetLookupTargetEntity(entityName, value[i].Item1);
						string viewId = ((lookupTargetEntity != null) ? GetLookupViewId(lookupTargetEntity) : null);
						string text3 = BuildLookupParameters(value[i].Item1, viewId, lookupFilterMap);
						text = ((!(text3 != "<parameters></parameters>")) ? (text + $"<cell id=\"{value2}\" colspan=\"1\"><labels><label description=\"{value[i].Item2}\" languagecode=\"2052\" /></labels><control id=\"{value[i].Item1}\" classid=\"{value4}\" datafieldname=\"{value[i].Item1}\" /></cell>") : (text + $"<cell id=\"{value2}\" colspan=\"1\"><labels><label description=\"{value[i].Item2}\" languagecode=\"2052\" /></labels><control id=\"{value[i].Item1}\" classid=\"{value4}\" datafieldname=\"{value[i].Item1}\">{text3}</control></cell>"));
					}
					else
					{
						text += $"<cell id=\"{value2}\" colspan=\"1\"><labels><label description=\"{value[i].Item2}\" languagecode=\"2052\" /></labels><control id=\"{value[i].Item1}\" classid=\"{value4}\" datafieldname=\"{value[i].Item1}\" /></cell>";
					}
					if (i + 1 < value.Count)
					{
						bool flag3 = lookupFields?.Contains(value[i + 1].Item1) ?? false;
						bool flag4 = picklistFields?.Contains(value[i + 1].Item1) ?? false;
						string value5 = (flag3 ? "{270BD3DB-D9AF-4782-9025-509E298DEC0A}" : (flag4 ? "{3EF39988-22BB-4f0b-BBBE-64B5A3748AEE}" : "{4273EDBD-AC1D-40d3-9FB2-095C621B552D}"));
						if (flag3)
						{
							string lookupTargetEntity2 = GetLookupTargetEntity(entityName, value[i + 1].Item1);
							string viewId2 = ((lookupTargetEntity2 != null) ? GetLookupViewId(lookupTargetEntity2) : null);
							string text4 = BuildLookupParameters(value[i + 1].Item1, viewId2, lookupFilterMap);
							text = ((!(text4 != "<parameters></parameters>")) ? (text + $"<cell id=\"{value3}\" colspan=\"1\"><labels><label description=\"{value[i + 1].Item2}\" languagecode=\"2052\" /></labels><control id=\"{value[i + 1].Item1}\" classid=\"{value5}\" datafieldname=\"{value[i + 1].Item1}\" /></cell>") : (text + $"<cell id=\"{value3}\" colspan=\"1\"><labels><label description=\"{value[i + 1].Item2}\" languagecode=\"2052\" /></labels><control id=\"{value[i + 1].Item1}\" classid=\"{value5}\" datafieldname=\"{value[i + 1].Item1}\">{text4}</control></cell>"));
						}
						else
						{
							text += $"<cell id=\"{value3}\" colspan=\"1\"><labels><label description=\"{value[i + 1].Item2}\" languagecode=\"2052\" /></labels><control id=\"{value[i + 1].Item1}\" classid=\"{value5}\" datafieldname=\"{value[i + 1].Item1}\" /></cell>";
						}
					}
					text += "</row>";
				}
				text += "</rows></section>";
			}
			int num = attributeValue.IndexOf("<tabs>");
			int num2 = attributeValue.IndexOf("</tabs>");
			if (num > 0 && num2 > 0)
			{
				string text2 = Guid.NewGuid().ToString("B");
				string text3 = $"<tabs><tab verticallayout=\"true\" id=\"{text2}\" name=\"general\" showlabel=\"true\"><labels><label description=\"常规\" languagecode=\"2052\" /></labels><columns><column width=\"100%\"><sections>{text}</sections></column></columns></tab></tabs>";
				string value6 = attributeValue.Substring(0, num) + text3 + attributeValue.Substring(num2 + "</tabs>".Length);
				Entity entity = new Entity("systemform", entity2.Id);
				entity["formxml"] = value6;
				_service.Update(entity);
				Console.WriteLine($"  ✓ 窗体已重新排列，{fieldGroups.Count} 个分组（单 Tab）");
			}
			else
			{
				int num3 = attributeValue.IndexOf("<sections>");
				int num4 = attributeValue.IndexOf("</sections>");
				if (num3 > 0 && num4 > 0)
				{
					string value7 = attributeValue.Substring(0, num3 + "<sections>".Length) + text + attributeValue.Substring(num4);
					Entity entity3 = new Entity("systemform", entity2.Id);
					entity3["formxml"] = value7;
					_service.Update(entity3);
					Console.WriteLine($"  ✓ 窗体已重新排列，{fieldGroups.Count} 个分组");
				}
			}
		}
		PublishEntity(entityName);
	}

	public void ReplaceFormTabs(string entityName, string newTabsXml)
	{
		Console.WriteLine("重建 " + entityName + " 窗体 Tabs...");
		QueryExpression queryExpression = new QueryExpression("systemform");
		queryExpression.ColumnSet = new ColumnSet("formxml", "name");
		queryExpression.Criteria = new FilterExpression
		{
			Conditions =
			{
				new ConditionExpression("objecttypecode", ConditionOperator.Equal, entityName),
				new ConditionExpression("type", ConditionOperator.Equal, 2)
			}
		};
		QueryExpression query = queryExpression;
		EntityCollection entityCollection = _service.RetrieveMultiple(query);
		if (entityCollection.Entities.Count == 0)
		{
			Console.WriteLine("  ✗ 未找到主窗体");
			return;
		}
		foreach (Entity entity2 in entityCollection.Entities)
		{
			string attributeValue = entity2.GetAttributeValue<string>("formxml");
			string attributeValue2 = entity2.GetAttributeValue<string>("name");
			int num = attributeValue.IndexOf("<tabs>");
			int num2 = attributeValue.IndexOf("</tabs>");
			if (num < 0 || num2 < 0)
			{
				Console.WriteLine("  ✗ 未找到 <tabs> 节点");
				continue;
			}
			string text = attributeValue.Substring(0, num) + newTabsXml + attributeValue.Substring(num2 + "</tabs>".Length);
			Entity entity = new Entity("systemform", entity2.Id);
			entity["formxml"] = text;
			_service.Update(entity);
			Console.WriteLine("  ✓ 窗体 " + attributeValue2 + " Tabs 已重建");
		}
		PublishEntity(entityName);
	}

	public void CheckViews(string entityName)
	{
		Console.WriteLine("检查 " + entityName + " 的所有视图...\n");
		QueryExpression queryExpression = new QueryExpression("savedquery");
		queryExpression.ColumnSet = new ColumnSet("savedqueryid", "name", "querytype", "isdefault", "layoutxml");
		queryExpression.Criteria = new FilterExpression
		{
			Conditions = 
			{
				new ConditionExpression("returnedtypecode", ConditionOperator.Equal, entityName)
			}
		};
		QueryExpression query = queryExpression;
		EntityCollection entityCollection = _service.RetrieveMultiple(query);
		Console.WriteLine($"找到 {entityCollection.Entities.Count} 个视图\n");
		foreach (Entity entity in entityCollection.Entities)
		{
			Guid attributeValue = entity.GetAttributeValue<Guid>("savedqueryid");
			string attributeValue2 = entity.GetAttributeValue<string>("name");
			int attributeValue3 = entity.GetAttributeValue<int>("querytype");
			bool attributeValue4 = entity.GetAttributeValue<bool>("isdefault");
			string attributeValue5 = entity.GetAttributeValue<string>("layoutxml");
			if (1 == 0)
			{
			}
			string text = attributeValue3 switch
			{
				0 => "Public", 
				1 => "Private", 
				2 => "Offline", 
				4 => "Lookup", 
				_ => $"Unknown({attributeValue3})", 
			};
			if (1 == 0)
			{
			}
			string text2 = text;
			Console.WriteLine("=== " + attributeValue2 + " ===");
			Console.WriteLine($"  ID: {attributeValue}");
			Console.WriteLine("  Type: " + text2 + " " + (attributeValue4 ? "[DEFAULT]" : ""));
			if (attributeValue5 != null)
			{
				MatchCollection matchCollection = Regex.Matches(attributeValue5, "name=\"([^\"]+)\"");
				Console.WriteLine($"  列数: {matchCollection.Count}");
				foreach (Match item in matchCollection)
				{
					Console.WriteLine("    - " + item.Groups[1].Value);
				}
			}
			Console.WriteLine();
		}
	}

	public void UpdateDefaultView(string entityName, Dictionary<string, string> fields)
	{
		Console.WriteLine("更新 " + entityName + " 默认视图...");
		QueryExpression queryExpression = new QueryExpression("savedquery");
		queryExpression.ColumnSet = new ColumnSet("savedqueryid", "name", "layoutxml", "fetchxml");
		queryExpression.Criteria = new FilterExpression
		{
			Conditions = 
			{
				new ConditionExpression("returnedtypecode", ConditionOperator.Equal, entityName),
				new ConditionExpression("querytype", ConditionOperator.Equal, 0),
				new ConditionExpression("isdefault", ConditionOperator.Equal, true)
			}
		};
		QueryExpression query = queryExpression;
		EntityCollection entityCollection = _service.RetrieveMultiple(query);
		if (entityCollection.Entities.Count == 0)
		{
			Console.WriteLine("  ✗ 未找到默认Public视图");
			return;
		}
		foreach (Entity entity2 in entityCollection.Entities)
		{
			string attributeValue = entity2.GetAttributeValue<string>("layoutxml");
			string attributeValue2 = entity2.GetAttributeValue<string>("fetchxml");
			string attributeValue3 = entity2.GetAttributeValue<string>("name");
			Console.WriteLine("  视图: " + attributeValue3);
			string text = "";
			int num = 0;
			string text2 = "";
			int num2 = 0;
			foreach (KeyValuePair<string, string> field in fields)
			{
				string key = field.Key;
				string value = field.Value;
				if (attributeValue.Contains("name=\"" + key + "\""))
				{
					Console.WriteLine("    ⊘ layoutxml " + key + " 已存在，跳过");
				}
				else
				{
					text = text + "<cell name=\"" + key + "\" width=\"150\" />";
					Console.WriteLine($"    + layoutxml {key} ({value})");
					num++;
				}
				if (attributeValue2.Contains("name=\"" + key + "\""))
				{
					Console.WriteLine("    ⊘ fetchxml " + key + " 已存在，跳过");
					continue;
				}
				text2 = text2 + "<attribute name=\"" + key + "\" />";
				Console.WriteLine($"    + fetchxml {key} ({value})");
				num2++;
			}
			Entity entity = new Entity("savedquery", entity2.Id);
			bool flag = false;
			if (num > 0)
			{
				int num3 = attributeValue.LastIndexOf("</row>");
				if (num3 > 0)
				{
					string value2 = attributeValue.Insert(num3, text);
					entity["layoutxml"] = value2;
					flag = true;
					Console.WriteLine($"  layoutxml: 添加 {num} 列");
				}
			}
			if (num2 > 0)
			{
				int num4 = attributeValue2.LastIndexOf("<order");
				if (num4 > 0)
				{
					int num5 = attributeValue2.IndexOf(">", num4);
					if (num5 > 0)
					{
						if (attributeValue2[num5 - 1] == '/')
						{
							string value3 = attributeValue2.Insert(num5 + 1, text2);
							entity["fetchxml"] = value3;
							flag = true;
							Console.WriteLine($"  fetchxml: 添加 {num2} 个attribute");
						}
						else
						{
							int num6 = attributeValue2.IndexOf("</order>", num5);
							if (num6 > 0)
							{
								string value4 = attributeValue2.Insert(num6 + 8, text2);
								entity["fetchxml"] = value4;
								flag = true;
								Console.WriteLine($"  fetchxml: 添加 {num2} 个attribute");
							}
						}
					}
				}
				else
				{
					int num7 = attributeValue2.IndexOf(">", attributeValue2.IndexOf("<entity"));
					if (num7 > 0)
					{
						string value5 = attributeValue2.Insert(num7 + 1, text2);
						entity["fetchxml"] = value5;
						flag = true;
						Console.WriteLine($"  fetchxml: 添加 {num2} 个attribute");
					}
				}
			}
			if (flag)
			{
				_service.Update(entity);
				Console.WriteLine("  ✓ 视图已更新");
			}
			else
			{
				Console.WriteLine("  没有需要更新的内容");
			}
		}
		PublishEntity(entityName);
	}

	public void RemoveFieldFromViews(string entityName, string fieldName)
	{
		Console.WriteLine("从视图中移除字段: " + entityName + "." + fieldName);
		QueryExpression queryExpression = new QueryExpression("savedquery");
		queryExpression.ColumnSet = new ColumnSet("savedqueryid", "name", "layoutxml", "fetchxml");
		queryExpression.Criteria = new FilterExpression
		{
			Conditions = 
			{
				new ConditionExpression("returnedtypecode", ConditionOperator.Equal, entityName)
			}
		};
		QueryExpression query = queryExpression;
		EntityCollection entityCollection = _service.RetrieveMultiple(query);
		int num = 0;
		foreach (Entity entity2 in entityCollection.Entities)
		{
			string text = entity2.GetAttributeValue<string>("layoutxml") ?? "";
			string text2 = entity2.GetAttributeValue<string>("fetchxml") ?? "";
			string text3 = entity2.GetAttributeValue<string>("name") ?? "";
			bool flag = false;
			if (text.Contains("name=\"" + fieldName + "\""))
			{
				string pattern = "<cell[^>]*name=\"" + fieldName + "\"[^/]*/>";
				text = Regex.Replace(text, pattern, "");
				flag = true;
				Console.WriteLine("  从视图 '" + text3 + "' 的layoutxml中移除 " + fieldName);
			}
			if (text2.Contains("name=\"" + fieldName + "\""))
			{
				string pattern2 = "<attribute[^>]*name=\"" + fieldName + "\"[^/]*/>";
				text2 = Regex.Replace(text2, pattern2, "");
				flag = true;
				Console.WriteLine("  从视图 '" + text3 + "' 的fetchxml中移除 " + fieldName);
			}
			if (flag)
			{
				Entity entity = new Entity("savedquery", entity2.Id);
				entity["layoutxml"] = text;
				entity["fetchxml"] = text2;
				_service.Update(entity);
				num++;
			}
		}
		Console.WriteLine($"  ✓ 已更新 {num} 个视图");
		PublishEntity(entityName);
	}

	public Guid DeployWebResource(string name, string displayName, string filePath, string solutionName)
	{
		return DeployWebResource(name, displayName, filePath, solutionName, 3);
	}

	public Guid DeployWebResource(string name, string displayName, string filePath, string solutionName, int resourceType)
	{
		string value = resourceType switch
		{
			3 => "JScript", 
			1 => "HTML", 
			_ => "Unknown", 
		};
		Console.WriteLine($"部署WebResource [{value}]: {name}...");
		if (!File.Exists(filePath))
		{
			Console.WriteLine("  ✗ 文件不存在: " + filePath);
			return Guid.Empty;
		}
		byte[] inArray = File.ReadAllBytes(filePath);
		string value2 = Convert.ToBase64String(inArray);
		QueryExpression queryExpression = new QueryExpression("webresource");
		queryExpression.ColumnSet = new ColumnSet("webresourceid", "name");
		queryExpression.Criteria = new FilterExpression
		{
			Conditions = 
			{
				new ConditionExpression("name", ConditionOperator.Equal, name)
			}
		};
		QueryExpression query = queryExpression;
		EntityCollection entityCollection = _service.RetrieveMultiple(query);
		Guid guid;
		if (entityCollection.Entities.Count > 0)
		{
			guid = entityCollection.Entities[0].Id;
			Entity entity = new Entity("webresource", guid);
			entity["content"] = value2;
			_service.Update(entity);
			Console.WriteLine($"  ✓ WebResource已更新 (ID: {guid})");
		}
		else
		{
			Entity entity2 = new Entity("webresource");
			entity2["name"] = name;
			entity2["displayname"] = displayName;
			entity2["webresourcetype"] = new OptionSetValue(resourceType);
			entity2["content"] = value2;
			guid = _service.Create(entity2);
			Console.WriteLine($"  ✓ WebResource已创建 (ID: {guid})");
		}
		if (!string.IsNullOrEmpty(solutionName) && guid != Guid.Empty)
		{
			try
			{
				AddWebResourceToSolution(guid, solutionName);
			}
			catch (Exception ex)
			{
				Console.WriteLine("  ⚠ 添加到解决方案失败: " + ex.Message);
			}
		}
		return guid;
	}

	private void AddWebResourceToSolution(Guid webResourceId, string solutionName)
	{
		QueryExpression queryExpression = new QueryExpression("solution");
		queryExpression.ColumnSet = new ColumnSet("solutionid");
		queryExpression.Criteria = new FilterExpression
		{
			Conditions = 
			{
				new ConditionExpression("uniquename", ConditionOperator.Equal, solutionName)
			}
		};
		QueryExpression query = queryExpression;
		EntityCollection entityCollection = _service.RetrieveMultiple(query);
		if (entityCollection.Entities.Count == 0)
		{
			Console.WriteLine("  ⚠ 解决方案 " + solutionName + " 未找到");
			return;
		}
		Guid attributeValue = entityCollection.Entities[0].GetAttributeValue<Guid>("solutionid");
		AddSolutionComponentRequest request = new AddSolutionComponentRequest
		{
			ComponentType = 61,
			ComponentId = webResourceId,
			SolutionUniqueName = solutionName
		};
		_service.Execute(request);
		Console.WriteLine("  ✓ 已添加到解决方案 " + solutionName);
	}

	public void BindJsToForm(string entityName, string webResourceName, string formName)
	{
		Console.WriteLine($"绑定JS到表单: {entityName} / {formName}...");
		QueryExpression queryExpression = new QueryExpression("webresource");
		queryExpression.ColumnSet = new ColumnSet("webresourceid");
		queryExpression.Criteria = new FilterExpression
		{
			Conditions = 
			{
				new ConditionExpression("name", ConditionOperator.Equal, webResourceName)
			}
		};
		QueryExpression query = queryExpression;
		EntityCollection entityCollection = _service.RetrieveMultiple(query);
		if (entityCollection.Entities.Count == 0)
		{
			Console.WriteLine("  ✗ WebResource " + webResourceName + " 未找到");
			return;
		}
		Guid id = entityCollection.Entities[0].Id;
		queryExpression = new QueryExpression("systemform");
		queryExpression.ColumnSet = new ColumnSet("formid", "name", "formxml");
		queryExpression.Criteria = new FilterExpression
		{
			Conditions = 
			{
				new ConditionExpression("objecttypecode", ConditionOperator.Equal, entityName),
				new ConditionExpression("type", ConditionOperator.Equal, 2),
				new ConditionExpression("name", ConditionOperator.Equal, formName)
			}
		};
		QueryExpression query2 = queryExpression;
		EntityCollection entityCollection2 = _service.RetrieveMultiple(query2);
		if (entityCollection2.Entities.Count == 0)
		{
			Console.WriteLine("  ✗ 表单 " + formName + " 未找到");
			return;
		}
		Entity entity = entityCollection2.Entities[0];
		string text = entity.GetAttributeValue<string>("formxml");
		if (1 == 0)
		{
		}
		string text2 = entityName switch
		{
			"mcs_credit_items" => "CreditItemsForm", 
			"mcs_credit_scoringcard" => "ScoringCardForm", 
			"mcs_credit_record" => "CreditRecordForm", 
			"mcs_customer_tag" => "CustomerTagForm", 
			"mcs_credititem_value" => "CreditItemValueForm", 
			"account" => "AccountForm", 
			"mcs_trade_stpayterm" => "TradeStPayTermForm",
			"mcs_fca_mdlversion" => "FactoryCreditModelVersionForm",
			"mcs_fca_mdlconfig" => "FcaMdlConfigForm",
			"mcs_fca_proc" => "FcaProcForm",
			"mcs_fca_quotaapp" => "FcaQuotaAppForm",
			"mcs_fca_records" => "FcaRecordsForm",
			_ => "ScoringCardForm", 
		};
		if (1 == 0)
		{
		}
		string text3 = text2;
		if (text.IndexOf("library name=\"" + webResourceName + "\"", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			bool flag = text.Contains("functionName=\"" + text3 + ".onLoad\"");
			bool flag2 = text.Contains("functionName=\"" + text3 + ".onSave\"");
			bool flag3 = Regex.IsMatch(text, "functionName=\"(?!" + text3 + "\\.on(Load|Save))[^\"]+\\.on(Load|Save)\"");
			bool flag4 = Regex.Matches(text, "<Library[^>]*name=\"" + Regex.Escape(webResourceName) + "\"[^>]*/>").Count > 1;
			if (!flag || !flag2 || flag3 || flag4)
			{
				text = EnsureSingleLibrary(text, webResourceName);
				text = NormalizeFormEvents(text, webResourceName, text3);
				Entity entity2 = new Entity("systemform", entity.Id);
				entity2["formxml"] = text;
				_service.Update(entity2);
				PublishEntity(entityName);
				Console.WriteLine("  ✓ JS事件处理函数已修正为 " + text3);
			}
			else
			{
				Console.WriteLine("  ⊘ JS已绑定，跳过");
			}
			return;
		}
		string text4 = $"<Library name=\"{webResourceName}\" libraryUniqueId=\"{{{Guid.NewGuid()}}}\" />";
		int num = text.IndexOf("</formLibraries>");
		if (num > 0)
		{
			text = text.Insert(num, text4);
		}
		else
		{
			int num2 = text.LastIndexOf("</form>");
			if (num2 > 0)
			{
				text = text.Insert(num2, "<formLibraries>" + text4 + "</formLibraries>");
			}
		}
		string text5 = $"\n    <event name='onload' application='false' active='true'>\n      <Handlers>\n        <Handler handlerUniqueId='{{{Guid.NewGuid()}}}' libraryName='{webResourceName}' functionName='{text3}.onLoad' enabled='true' parameters='' passExecutionContext='true' />\n      </Handlers>\n    </event>\n    <event name='onsave' application='false' active='true'>\n      <Handlers>\n        <Handler handlerUniqueId='{{{Guid.NewGuid()}}}' libraryName='{webResourceName}' functionName='{text3}.onSave' enabled='true' parameters='' passExecutionContext='true' />\n      </Handlers>\n    </event>";
		int num3 = text.IndexOf("</events>");
		if (num3 > 0)
		{
			text = text.Insert(num3, text5);
		}
		else
		{
			int num4 = text.LastIndexOf("</form>");
			if (num4 > 0)
			{
				text = text.Insert(num4, "<events>" + text5 + "</events>");
			}
		}
		Entity entity3 = new Entity("systemform", entity.Id);
		entity3["formxml"] = text;
		_service.Update(entity3);
		PublishEntity(entityName);
		Console.WriteLine("  ✓ 表单已发布");
		Console.WriteLine("  ✓ JS已绑定到表单");
	}

	private string InsertEventHandler(string formXml, string eventName, string handlerXml)
	{
		string pattern = "<event name=\"" + eventName + "\"[^>]*>.*?<Handlers>(.*?)</Handlers>.*?</event>";
		Match match = Regex.Match(formXml, pattern, RegexOptions.Singleline);
		if (match.Success)
		{
			string value = match.Groups[1].Value;
			string text = value + handlerXml;
			return formXml.Substring(0, match.Groups[1].Index) + text + formXml.Substring(match.Groups[1].Index + match.Groups[1].Length);
		}
		string text2 = $"\n    <event name='{eventName}' application='false' active='true'>\n      <Handlers>\n        {handlerXml}\n      </Handlers>\n    </event>";
		int num = formXml.IndexOf("</events>");
		if (num > 0)
		{
			return formXml.Insert(num, text2);
		}
		int num2 = formXml.LastIndexOf("</form>");
		if (num2 > 0)
		{
			return formXml.Insert(num2, "<events>" + text2 + "</events>");
		}
		return formXml;
	}

	private string EnsureSingleLibrary(string formXml, string webResourceName)
	{
		Regex regex = new Regex($"<Library[^>]*name=\"{Regex.Escape(webResourceName)}\"[^>]*/>");
		MatchCollection matchCollection = regex.Matches(formXml);
		if (matchCollection.Count <= 1)
		{
			return formXml;
		}
		string text = formXml;
		for (int num = matchCollection.Count - 1; num >= 1; num--)
		{
			text = text.Remove(matchCollection[num].Index, matchCollection[num].Length);
		}
		return text;
	}

	private string NormalizeFormEvents(string formXml, string webResourceName, string namespaceName)
	{
		Match match = Regex.Match(formXml, "<events>(.*?)</events>", RegexOptions.Singleline);
		if (!match.Success)
		{
			return formXml;
		}
		string text = match.Groups[1].Value;
		Regex regex = new Regex("<Handler[^>]*libraryName=\"([^\"]+)\"[^>]*functionName=\"([^\"]+)\"[^>]*/>");
		MatchCollection matchCollection = regex.Matches(text);
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		string text2 = "";
		string text3 = "";
		foreach (Match item in matchCollection)
		{
			string text4 = item.Groups[1].Value;
			string text5 = item.Groups[2].Value;
			if (text4 != webResourceName)
			{
				if (text5.EndsWith(".onLoad"))
				{
					list.Add(item.Value);
				}
				else if (text5.EndsWith(".onSave"))
				{
					list2.Add(item.Value);
				}
			}
			else if (text5 == namespaceName + ".onLoad" && text2 == "")
			{
				text2 = item.Value;
			}
			else if (text5 == namespaceName + ".onSave" && text3 == "")
			{
				text3 = item.Value;
			}
		}
		if (text2 == "")
		{
			text2 = $"<Handler handlerUniqueId=\"{{{Guid.NewGuid()}}}\" libraryName=\"{webResourceName}\" functionName=\"{namespaceName}.onLoad\" enabled=\"true\" parameters=\"\" passExecutionContext=\"true\" />";
		}
		if (text3 == "")
		{
			text3 = $"<Handler handlerUniqueId=\"{{{Guid.NewGuid()}}}\" libraryName=\"{webResourceName}\" functionName=\"{namespaceName}.onSave\" enabled=\"true\" parameters=\"\" passExecutionContext=\"true\" />";
		}
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("<events>");
		stringBuilder.AppendLine("    <event name=\"onload\" application=\"false\" active=\"true\">");
		stringBuilder.AppendLine("      <Handlers>");
		foreach (string item2 in list)
		{
			stringBuilder.AppendLine("        " + item2);
		}
		stringBuilder.AppendLine("        " + text2);
		stringBuilder.AppendLine("      </Handlers>");
		stringBuilder.AppendLine("    </event>");
		stringBuilder.AppendLine("    <event name=\"onsave\" application=\"false\" active=\"true\">");
		stringBuilder.AppendLine("      <Handlers>");
		foreach (string item3 in list2)
		{
			stringBuilder.AppendLine("        " + item3);
		}
		stringBuilder.AppendLine("        " + text3);
		stringBuilder.AppendLine("      </Handlers>");
		stringBuilder.AppendLine("    </event>");
		stringBuilder.Append("  </events>");
		return formXml.Substring(0, match.Index) + stringBuilder.ToString() + formXml.Substring(match.Index + match.Length);
	}

	public void RegisterPlugin(string dllPath, string className, string entityName, string messageName, int stage, int mode)
	{
		Console.WriteLine("注册Plugin: " + className + "...");
		if (!File.Exists(dllPath))
		{
			Console.WriteLine("  ✗ DLL不存在: " + dllPath);
			return;
		}
		byte[] inArray = File.ReadAllBytes(dllPath);
		string value = Convert.ToBase64String(inArray);
		string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(dllPath);
		QueryExpression queryExpression = new QueryExpression("pluginassembly");
		queryExpression.ColumnSet = new ColumnSet("pluginassemblyid");
		queryExpression.Criteria = new FilterExpression
		{
			Conditions = 
			{
				new ConditionExpression("name", ConditionOperator.Equal, fileNameWithoutExtension)
			}
		};
		QueryExpression query = queryExpression;
		EntityCollection entityCollection = _service.RetrieveMultiple(query);
		Guid guid;
		if (entityCollection.Entities.Count > 0)
		{
			guid = entityCollection.Entities[0].Id;
			Entity entity = new Entity("pluginassembly", guid);
			entity["content"] = value;
			_service.Update(entity);
			Console.WriteLine($"  ✓ Plugin Assembly已更新 (ID: {guid})");
		}
		else
		{
			Entity entity2 = new Entity("pluginassembly");
			entity2["name"] = fileNameWithoutExtension;
			entity2["content"] = value;
			entity2["sourcetype"] = new OptionSetValue(0);
			entity2["isolationmode"] = new OptionSetValue(2);
			entity2["culture"] = "neutral";
			entity2["version"] = "1.0.0.0";
			entity2["publickeytoken"] = "null";
			guid = _service.Create(entity2);
			Console.WriteLine($"  ✓ Plugin Assembly已创建 (ID: {guid})");
		}
		queryExpression = new QueryExpression("plugintype");
		queryExpression.ColumnSet = new ColumnSet("plugintypeid");
		queryExpression.Criteria = new FilterExpression
		{
			Conditions = 
			{
				new ConditionExpression("typename", ConditionOperator.Equal, className)
			}
		};
		QueryExpression query2 = queryExpression;
		EntityCollection entityCollection2 = _service.RetrieveMultiple(query2);
		Guid guid2;
		if (entityCollection2.Entities.Count > 0)
		{
			guid2 = entityCollection2.Entities[0].Id;
			Console.WriteLine($"  ✓ Plugin Type已存在 (ID: {guid2})");
		}
		else
		{
			Entity entity3 = new Entity("plugintype");
			entity3["pluginassemblyid"] = new EntityReference("pluginassembly", guid);
			entity3["typename"] = className;
			entity3["friendlyname"] = className.Split('.').Last();
			entity3["name"] = className.Split('.').Last();
			guid2 = _service.Create(entity3);
			Console.WriteLine($"  ✓ Plugin Type已创建 (ID: {guid2})");
		}
		queryExpression = new QueryExpression("sdkmessage");
		queryExpression.ColumnSet = new ColumnSet("sdkmessageid");
		queryExpression.Criteria = new FilterExpression
		{
			Conditions = 
			{
				new ConditionExpression("name", ConditionOperator.Equal, messageName)
			}
		};
		QueryExpression query3 = queryExpression;
		EntityCollection entityCollection3 = _service.RetrieveMultiple(query3);
		if (entityCollection3.Entities.Count == 0)
		{
			Console.WriteLine("  ✗ 消息 " + messageName + " 未找到");
			return;
		}
		Guid attributeValue = entityCollection3.Entities[0].GetAttributeValue<Guid>("sdkmessageid");
		queryExpression = new QueryExpression("sdkmessagefilter");
		queryExpression.ColumnSet = new ColumnSet("sdkmessagefilterid");
		queryExpression.Criteria = new FilterExpression
		{
			Conditions = 
			{
				new ConditionExpression("sdkmessageid", ConditionOperator.Equal, attributeValue),
				new ConditionExpression("primaryobjecttypecode", ConditionOperator.Equal, entityName)
			}
		};
		QueryExpression query4 = queryExpression;
		EntityCollection entityCollection4 = _service.RetrieveMultiple(query4);
		if (entityCollection4.Entities.Count == 0)
		{
			Console.WriteLine("  ✗ 消息过滤器未找到");
			return;
		}
		Guid attributeValue2 = entityCollection4.Entities[0].GetAttributeValue<Guid>("sdkmessagefilterid");
		queryExpression = new QueryExpression("sdkmessageprocessingstep");
		queryExpression.ColumnSet = new ColumnSet("sdkmessageprocessingstepid");
		queryExpression.Criteria = new FilterExpression
		{
			Conditions = 
			{
				new ConditionExpression("plugintypeid", ConditionOperator.Equal, guid2),
				new ConditionExpression("sdkmessagefilterid", ConditionOperator.Equal, attributeValue2),
				new ConditionExpression("stage", ConditionOperator.Equal, stage)
			}
		};
		QueryExpression query5 = queryExpression;
		EntityCollection entityCollection5 = _service.RetrieveMultiple(query5);
		if (entityCollection5.Entities.Count > 0)
		{
			Console.WriteLine("  ⊘ Step已存在，跳过");
		}
		else
		{
			Entity entity4 = new Entity("sdkmessageprocessingstep");
			entity4["plugintypeid"] = new EntityReference("plugintype", guid2);
			entity4["sdkmessageid"] = new EntityReference("sdkmessage", attributeValue);
			entity4["sdkmessagefilterid"] = new EntityReference("sdkmessagefilter", attributeValue2);
			entity4["name"] = $"{className.Split('.').Last()}: {messageName} of {entityName}";
			entity4["stage"] = new OptionSetValue(stage);
			entity4["mode"] = new OptionSetValue(mode);
			entity4["rank"] = 1;
			entity4["supporteddeployment"] = new OptionSetValue(0);
			Guid value2 = _service.Create(entity4);
			Console.WriteLine($"  ✓ Plugin Step已注册 (ID: {value2})");
		}
		Console.WriteLine("  ✓ Plugin注册完成");
	}

	public Guid RegisterPluginAssemblyOnly(string dllPath, string className)
	{
		Console.WriteLine(">>> 注册 Plugin Assembly: " + Path.GetFileName(dllPath));
		if (!File.Exists(dllPath))
		{
			Console.WriteLine("  ✗ DLL不存在: " + dllPath);
			return Guid.Empty;
		}
		byte[] inArray = File.ReadAllBytes(dllPath);
		string value = Convert.ToBase64String(inArray);
		string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(dllPath);
		QueryExpression queryExpression = new QueryExpression("pluginassembly");
		queryExpression.ColumnSet = new ColumnSet("pluginassemblyid");
		queryExpression.Criteria = new FilterExpression
		{
			Conditions = 
			{
				new ConditionExpression("name", ConditionOperator.Equal, fileNameWithoutExtension)
			}
		};
		QueryExpression query = queryExpression;
		EntityCollection entityCollection = _service.RetrieveMultiple(query);
		Guid guid;
		if (entityCollection.Entities.Count > 0)
		{
			guid = entityCollection.Entities[0].Id;
			Entity entity = new Entity("pluginassembly", guid);
			entity["content"] = value;
			_service.Update(entity);
			Console.WriteLine($"  ✓ Plugin Assembly已更新 (ID: {guid})");
		}
		else
		{
			Entity entity2 = new Entity("pluginassembly");
			entity2["name"] = fileNameWithoutExtension;
			entity2["content"] = value;
			entity2["sourcetype"] = new OptionSetValue(0);
			entity2["isolationmode"] = new OptionSetValue(2);
			entity2["culture"] = "neutral";
			entity2["version"] = "1.0.0.0";
			entity2["publickeytoken"] = "null";
			guid = _service.Create(entity2);
			Console.WriteLine($"  ✓ Plugin Assembly已创建 (ID: {guid})");
		}
		queryExpression = new QueryExpression("plugintype");
		queryExpression.ColumnSet = new ColumnSet("plugintypeid");
		queryExpression.Criteria = new FilterExpression
		{
			Conditions = 
			{
				new ConditionExpression("typename", ConditionOperator.Equal, className)
			}
		};
		QueryExpression query2 = queryExpression;
		EntityCollection entityCollection2 = _service.RetrieveMultiple(query2);
		if (entityCollection2.Entities.Count > 0)
		{
			Console.WriteLine($"  ✓ Plugin Type已存在 (ID: {entityCollection2.Entities[0].Id})");
			return entityCollection2.Entities[0].Id;
		}
		Entity entity3 = new Entity("plugintype");
		entity3["pluginassemblyid"] = new EntityReference("pluginassembly", guid);
		entity3["typename"] = className;
		entity3["friendlyname"] = className.Split('.').Last();
		entity3["name"] = className.Split('.').Last();
		Guid guid2 = _service.Create(entity3);
		Console.WriteLine($"  ✓ Plugin Type已创建 (ID: {guid2})");
		return guid2;
	}

	public void RegisterPluginWithFilter(string dllPath, string className, string entityName, string messageName, int stage, int mode, string filteringAttributes = null)
	{
		Console.WriteLine("注册Plugin: " + className + "...");
		Console.WriteLine($"  消息: {messageName}, 实体: {entityName}, 阶段: {stage}, 筛选属性: {filteringAttributes ?? "无"}");
		if (!File.Exists(dllPath))
		{
			Console.WriteLine("  ✗ DLL不存在: " + dllPath);
			return;
		}
		byte[] inArray = File.ReadAllBytes(dllPath);
		string value = Convert.ToBase64String(inArray);
		string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(dllPath);
		QueryExpression queryExpression = new QueryExpression("pluginassembly");
		queryExpression.ColumnSet = new ColumnSet("pluginassemblyid");
		queryExpression.Criteria = new FilterExpression
		{
			Conditions = 
			{
				new ConditionExpression("name", ConditionOperator.Equal, fileNameWithoutExtension)
			}
		};
		QueryExpression query = queryExpression;
		EntityCollection entityCollection = _service.RetrieveMultiple(query);
		Guid guid;
		if (entityCollection.Entities.Count > 0)
		{
			guid = entityCollection.Entities[0].Id;
			Entity entity = new Entity("pluginassembly", guid);
			entity["content"] = value;
			_service.Update(entity);
			Console.WriteLine($"  ✓ Plugin Assembly已更新 (ID: {guid})");
		}
		else
		{
			Entity entity2 = new Entity("pluginassembly");
			entity2["name"] = fileNameWithoutExtension;
			entity2["content"] = value;
			entity2["sourcetype"] = new OptionSetValue(0);
			entity2["isolationmode"] = new OptionSetValue(2);
			entity2["culture"] = "neutral";
			entity2["version"] = "1.0.0.0";
			entity2["publickeytoken"] = "null";
			guid = _service.Create(entity2);
			Console.WriteLine($"  ✓ Plugin Assembly已创建 (ID: {guid})");
		}
		queryExpression = new QueryExpression("plugintype");
		queryExpression.ColumnSet = new ColumnSet("plugintypeid");
		queryExpression.Criteria = new FilterExpression
		{
			Conditions = 
			{
				new ConditionExpression("typename", ConditionOperator.Equal, className)
			}
		};
		QueryExpression query2 = queryExpression;
		EntityCollection entityCollection2 = _service.RetrieveMultiple(query2);
		Guid guid2;
		if (entityCollection2.Entities.Count > 0)
		{
			guid2 = entityCollection2.Entities[0].Id;
			Console.WriteLine($"  ✓ Plugin Type已存在 (ID: {guid2})");
		}
		else
		{
			Entity entity3 = new Entity("plugintype");
			entity3["pluginassemblyid"] = new EntityReference("pluginassembly", guid);
			entity3["typename"] = className;
			entity3["friendlyname"] = className.Split('.').Last();
			entity3["name"] = className.Split('.').Last();
			guid2 = _service.Create(entity3);
			Console.WriteLine($"  ✓ Plugin Type已创建 (ID: {guid2})");
		}
		queryExpression = new QueryExpression("sdkmessage");
		queryExpression.ColumnSet = new ColumnSet("sdkmessageid");
		queryExpression.Criteria = new FilterExpression
		{
			Conditions = 
			{
				new ConditionExpression("name", ConditionOperator.Equal, messageName)
			}
		};
		QueryExpression query3 = queryExpression;
		EntityCollection entityCollection3 = _service.RetrieveMultiple(query3);
		if (entityCollection3.Entities.Count == 0)
		{
			Console.WriteLine("  ✗ 消息 " + messageName + " 未找到");
			return;
		}
		Guid attributeValue = entityCollection3.Entities[0].GetAttributeValue<Guid>("sdkmessageid");
		queryExpression = new QueryExpression("sdkmessagefilter");
		queryExpression.ColumnSet = new ColumnSet("sdkmessagefilterid");
		queryExpression.Criteria = new FilterExpression
		{
			Conditions = 
			{
				new ConditionExpression("sdkmessageid", ConditionOperator.Equal, attributeValue),
				new ConditionExpression("primaryobjecttypecode", ConditionOperator.Equal, entityName)
			}
		};
		QueryExpression query4 = queryExpression;
		EntityCollection entityCollection4 = _service.RetrieveMultiple(query4);
		if (entityCollection4.Entities.Count == 0)
		{
			Console.WriteLine("  ✗ 消息过滤器未找到");
			return;
		}
		Guid attributeValue2 = entityCollection4.Entities[0].GetAttributeValue<Guid>("sdkmessagefilterid");
		queryExpression = new QueryExpression("sdkmessageprocessingstep");
		queryExpression.ColumnSet = new ColumnSet("sdkmessageprocessingstepid", "filteringattributes");
		queryExpression.Criteria = new FilterExpression
		{
			Conditions = 
			{
				new ConditionExpression("plugintypeid", ConditionOperator.Equal, guid2),
				new ConditionExpression("sdkmessagefilterid", ConditionOperator.Equal, attributeValue2),
				new ConditionExpression("stage", ConditionOperator.Equal, stage)
			}
		};
		QueryExpression query5 = queryExpression;
		EntityCollection entityCollection5 = _service.RetrieveMultiple(query5);
		if (entityCollection5.Entities.Count > 0)
		{
			Entity entity4 = entityCollection5.Entities[0];
			Guid id = entity4.Id;
			if (!string.IsNullOrEmpty(filteringAttributes))
			{
				Entity entity5 = new Entity("sdkmessageprocessingstep", id);
				entity5["filteringattributes"] = filteringAttributes;
				_service.Update(entity5);
				Console.WriteLine($"  ✓ Plugin Step已更新筛选属性 (ID: {id})");
			}
			else
			{
				Console.WriteLine("  ⊘ Step已存在，跳过");
			}
		}
		else
		{
			Entity entity6 = new Entity("sdkmessageprocessingstep");
			entity6["plugintypeid"] = new EntityReference("plugintype", guid2);
			entity6["sdkmessageid"] = new EntityReference("sdkmessage", attributeValue);
			entity6["sdkmessagefilterid"] = new EntityReference("sdkmessagefilter", attributeValue2);
			entity6["name"] = $"{className.Split('.').Last()}: {messageName} of {entityName}";
			entity6["stage"] = new OptionSetValue(stage);
			entity6["mode"] = new OptionSetValue(mode);
			entity6["rank"] = 1;
			entity6["supporteddeployment"] = new OptionSetValue(0);
			if (!string.IsNullOrEmpty(filteringAttributes))
			{
				entity6["filteringattributes"] = filteringAttributes;
			}
			Guid value2 = _service.Create(entity6);
			Console.WriteLine($"  ✓ Plugin Step已注册 (ID: {value2})");
		}
		Console.WriteLine("  ✓ Plugin注册完成");
	}

	public void RegisterPluginPreImage(Guid stepId, string imageName, string imageAlias, string attributes)
	{
		Console.WriteLine($">>> 为 Step {stepId} 注册 PreEntityImage...");
		QueryExpression queryExpression = new QueryExpression("sdkmessageprocessingstepimage");
		queryExpression.ColumnSet = new ColumnSet("sdkmessageprocessingstepimageid", "name");
		queryExpression.Criteria = new FilterExpression
		{
			Conditions = 
			{
				new ConditionExpression("sdkmessageprocessingstepid", ConditionOperator.Equal, stepId),
				new ConditionExpression("name", ConditionOperator.Equal, imageName)
			}
		};
		QueryExpression query = queryExpression;
		EntityCollection entityCollection = _service.RetrieveMultiple(query);
		if (entityCollection.Entities.Count > 0)
		{
			Console.WriteLine("  ⊘ PreEntityImage '" + imageName + "' 已存在，跳过");
			return;
		}
		Entity entity = new Entity("sdkmessageprocessingstepimage");
		entity["name"] = imageName;
		entity["entityalias"] = imageAlias;
		entity["imagetype"] = new OptionSetValue(0);
		entity["sdkmessageprocessingstepid"] = new EntityReference("sdkmessageprocessingstep", stepId);
		entity["messagepropertyname"] = "Target";
		entity["attributes"] = attributes;
		entity["description"] = "Pre-image for " + imageAlias;
		Guid value = _service.Create(entity);
		Console.WriteLine($"  ✓ PreEntityImage 已注册 (ID: {value})");
	}

	public void CreateCreditItemRecords()
	{
		Console.WriteLine("创建评分项目记录...");
		try
		{
			InsertOptionValue("mcs_credit_items", "mcs_group", "综合指标", 100000004);
		}
		catch (Exception ex)
		{
			Console.WriteLine("  综合指标选项可能已存在: " + ex.Message);
		}
		(string, string, string, int, int, int, bool, bool)[] array = new(string, string, string, int, int, int, bool, bool)[25]
		{
			("ExternalRating", "外部评级", "外部资信机构给出的评级（1-10分）", 100000000, 100000000, 100000001, false, true),
			("RegisteredCapital", "注册资本", "注册资本,金额单位元,货币USD", 100000000, 100000000, 100000001, true, true),
			("RegistrationDate", "从业年限", "从业年限的年数", 100000000, 100000000, 100000001, false, true),
			("LatePaymentIndex", "迟付指数", "迟付指数比率,采用小数两位", 100000000, 100000000, 100000001, false, true),
			("LegalEvents", "诉讼债权金额", "诉讼债权标的金额,金额单位元,货币USD", 100000000, 100000000, 100000001, true, true),
			("ProjectAmt", "在手项目", "在手项目合同额合计,金额单位元,货币USD", 100000000, 100000000, 100000000, true, false),
			("ProductNum", "自有设备", "自有设备数", 100000000, 100000000, 100000000, true, false),
			("NetAssets", "净资产", "净资产金额单位元,货币USD", 100000000, 100000001, 100000001, true, true),
			("DebtRatio", "资产负债率", "资产负债率,采用小数两位", 100000000, 100000001, 100000001, true, true),
			("CurrentRatio", "流动比率", "流动比率,采用小数两位", 100000000, 100000001, 100000001, true, true),
			("NetProfit", "净利润率", "净利润率,采用小数两位", 100000000, 100000001, 100000001, true, true),
			("DebtAmount", "还款来源", "近半年个人银行账户借方月平均值,单位元,货币USD", 100000000, 100000001, 100000000, true, false),
			("TotalAssets", "资产证明", "个人名下资产合计金额,单位元,货币USD", 100000000, 100000001, 100000000, true, false),
			("CountryRisk", "国别风险", "国别风险（低、中、高）", 100000001, 100000002, 100000001, false, true),
			("SectorRisk", "行业风险", "行业风险（低、中、高）", 100000001, 100000002, 100000001, false, true),
			("Sectors", "行业属性", "行业属性", 100000001, 100000002, 100000001, false, true),
			("OverdueModel", "逾期未回收率模型分", "逾期未回收率模型分（0-100）", 100000000, 100000003, 100000000, true, false),
			("BigAccount", "客户评级", "客户评级 S/A/S或A级控股参股公司", 100000001, 100000003, 100000000, false, false),
			("SalesAmount", "历史采购金额", "累计采购金额,单位元,货币USD", 100000000, 100000003, 100000000, false, false),
			("ARAmount", "历史逾期金额", "最大逾期付款金额（过去两年）USD/元", 100000000, 100000003, 100000000, false, false),
			("ARAge", "历史逾期账龄", "最大逾期账龄天数（过去两年）USD/元", 100000000, 100000003, 100000000, false, false),
			("DealerRating", "经销商分级", "经销商分级（钻石、铂金、白银等）", 100000001, 100000003, 100000000, false, false),
			("NewOldCust", "新老客户", "老客户标签需有历史销售订单交易", 100000001, 100000004, 100000000, false, false),
			("CreditScore", "客户信用评分", "客户信用评分基于评分卡计算获得", 100000000, 100000004, 100000000, false, false),
			("CreditGrade", "客户等级", "客户等级标准采用A0-A4(A0最高):A0≥80/A1≥70/A2≥60/A3≥50/A4<50客户信用分", 100000001, 100000004, 100000000, false, false)
		};
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		(string, string, string, int, int, int, bool, bool)[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			(string, string, string, int, int, int, bool, bool) tuple = array2[i];
			QueryExpression queryExpression = new QueryExpression("mcs_credit_items");
			queryExpression.ColumnSet = new ColumnSet("mcs_credit_itemsid", "mcs_itemname", "mcs_itemdesc", "mcs_datatype", "mcs_group", "mcs_source", "mcs_validate", "mcs__3p");
			queryExpression.Criteria = new FilterExpression
			{
				Conditions = 
				{
					new ConditionExpression("mcs_credit_itemsno", ConditionOperator.Equal, tuple.Item1)
				}
			};
			queryExpression.TopCount = 1;
			QueryExpression query = queryExpression;
			EntityCollection entityCollection = _service.RetrieveMultiple(query);
			if (entityCollection.Entities.Count > 0)
			{
				Entity entity = entityCollection.Entities[0];
				bool flag = false;
				string text = (entity.Contains("mcs_itemname") ? entity.GetAttributeValue<string>("mcs_itemname") : null);
				string text2 = (entity.Contains("mcs_itemdesc") ? entity.GetAttributeValue<string>("mcs_itemdesc") : null);
				if (string.IsNullOrEmpty(text) || text != tuple.Item2)
				{
					flag = true;
				}
				if (string.IsNullOrEmpty(text2) || text2 != tuple.Item3)
				{
					flag = true;
				}
				if (!entity.Contains("mcs_datatype"))
				{
					flag = true;
				}
				if (!entity.Contains("mcs_group"))
				{
					flag = true;
				}
				if (!entity.Contains("mcs_source"))
				{
					flag = true;
				}
				if (!entity.Contains("mcs_validate"))
				{
					flag = true;
				}
				if (!entity.Contains("mcs__3p"))
				{
					flag = true;
				}
				if (flag)
				{
					Entity entity2 = new Entity("mcs_credit_items");
					entity2.Id = entity.Id;
					entity2["mcs_itemname"] = tuple.Item2;
					entity2["mcs_itemdesc"] = tuple.Item3;
					entity2["mcs_datatype"] = new OptionSetValue(tuple.Item4);
					entity2["mcs_group"] = new OptionSetValue(tuple.Item5);
					entity2["mcs_source"] = new OptionSetValue(tuple.Item6);
					entity2["mcs_validate"] = tuple.Item7;
					entity2["mcs__3p"] = tuple.Rest.Item1;
					_service.Update(entity2);
					Console.WriteLine("  ↻ 更新补全: " + tuple.Item1);
					num2++;
				}
				else
				{
					Console.WriteLine("  ⊘ 已存在(完整): " + tuple.Item1);
					num3++;
				}
			}
			else
			{
				Entity entity3 = new Entity("mcs_credit_items");
				entity3["mcs_credit_itemsno"] = tuple.Item1;
				entity3["mcs_itemname"] = tuple.Item2;
				entity3["mcs_itemdesc"] = tuple.Item3;
				entity3["mcs_datatype"] = new OptionSetValue(tuple.Item4);
				entity3["mcs_group"] = new OptionSetValue(tuple.Item5);
				entity3["mcs_source"] = new OptionSetValue(tuple.Item6);
				entity3["mcs_validate"] = tuple.Item7;
				entity3["mcs__3p"] = tuple.Rest.Item1;
				Guid value = _service.Create(entity3);
				Console.WriteLine($"  ✓ 创建成功: {tuple.Item1}, ID={value}");
				num++;
			}
		}
		Console.WriteLine($"\n完成: 创建{num}条, 更新{num2}条, 已存在{num3}条, 总计{num + num2 + num3}条");
	}

	public void CreateQualitativeEnumRecords()
	{
		Console.WriteLine("创建定性评分项目枚举值记录...");
		(string, string, string, int)[] array = new(string, string, string, int)[48]
		{
			("CountryRisk", "A1", "低风险", 5),
			("CountryRisk", "A2", "低风险", 5),
			("CountryRisk", "A3", "中风险", 3),
			("CountryRisk", "A4", "中风险", 3),
			("CountryRisk", "B", "高风险", -1),
			("CountryRisk", "C", "高风险", -1),
			("CountryRisk", "D", "高风险", -1),
			("CountryRisk", "E", "高风险", -1),
			("CountryRisk", "O", "缺失", 2),
			("SectorRisk", "1", "低风险", 5),
			("SectorRisk", "2", "中风险", 3),
			("SectorRisk", "3", "高风险", -1),
			("SectorRisk", "4", "高风险", -1),
			("SectorRisk", "O", "缺失", 2),
			("ExternalRating", "0", "违约/资不抵债", -1),
			("ExternalRating", "1", "极端风险", -1),
			("ExternalRating", "2", "极高风险", -1),
			("ExternalRating", "3", "高风险", -1),
			("ExternalRating", "4", "显著风险", -1),
			("ExternalRating", "5", "中等风险", -1),
			("ExternalRating", "6", "可接受风险", -1),
			("ExternalRating", "7", "一般风险", -1),
			("ExternalRating", "8", "低风险", -1),
			("ExternalRating", "9", "极低风险", -1),
			("ExternalRating", "10", "极佳风险", -1),
			("ExternalRating", "O", "缺失", 2),
			("Sectors", "矿业", "矿业", 5),
			("Sectors", "港务", "港务", 4),
			("Sectors", "建工", "建工", 4),
			("Sectors", "吊装", "吊装", 4),
			("Sectors", "集装箱运力", "集装箱运力", 3),
			("Sectors", "租赁", "租赁", 3),
			("Sectors", "商混", "商混", 3),
			("Sectors", "林业", "林业", 3),
			("Sectors", "农业", "农业", 3),
			("Sectors", "制造业", "制造业", 4),
			("Sectors", "交通运输", "交通运输", 3),
			("Sectors", "其他", "其他", 2),
			("Sectors", "O", "缺失", 3),
			("BigAccount", "S", "S级", 10),
			("BigAccount", "A", "A级", 8),
			("BigAccount", "S_JV", "S级控股/参股公司", 6),
			("BigAccount", "A_JV", "A级控股/参股公司", 3),
			("DealerRating", "Diamond", "钻石", 10),
			("DealerRating", "Platinum", "铂金", 8),
			("DealerRating", "Silver", "白银", 6),
			("DealerRating", "Certified", "认证", 4),
			("DealerRating", "Intention", "意向", 2)
		};
		Dictionary<string, Guid> dictionary = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
		QueryExpression queryExpression = new QueryExpression("mcs_credit_items");
		queryExpression.ColumnSet = new ColumnSet("mcs_credit_itemsid", "mcs_credit_itemsno");
		QueryExpression query = queryExpression;
		EntityCollection entityCollection = _service.RetrieveMultiple(query);
		foreach (Entity entity4 in entityCollection.Entities)
		{
			string attributeValue = entity4.GetAttributeValue<string>("mcs_credit_itemsno");
			Guid attributeValue2 = entity4.GetAttributeValue<Guid>("mcs_credit_itemsid");
			if (!string.IsNullOrEmpty(attributeValue))
			{
				dictionary[attributeValue] = attributeValue2;
			}
		}
		Console.WriteLine($"  预加载 {dictionary.Count} 个评分项目GUID");
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		int num4 = 0;
		(string, string, string, int)[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			(string, string, string, int) tuple = array2[i];
			if (!dictionary.TryGetValue(tuple.Item1, out var value))
			{
				Console.WriteLine("  ✗ 跳过: 未找到评分项目 " + tuple.Item1);
				num4++;
				continue;
			}
			queryExpression = new QueryExpression("mcs_credititem_value");
			queryExpression.ColumnSet = new ColumnSet("mcs_credititem_valueid");
			queryExpression.Criteria = new FilterExpression
			{
				Conditions = 
				{
					new ConditionExpression("mcs_credititemno", ConditionOperator.Equal, value),
					new ConditionExpression("mcs_listvalue", ConditionOperator.Equal, tuple.Item2)
				}
			};
			queryExpression.TopCount = 1;
			QueryExpression query2 = queryExpression;
			EntityCollection entityCollection2 = _service.RetrieveMultiple(query2);
			if (entityCollection2.Entities.Count > 0)
			{
				Entity entity = entityCollection2.Entities[0];
				string attributeValue3 = entity.GetAttributeValue<string>("mcs_listname");
				if (attributeValue3 != tuple.Item3)
				{
					Entity entity2 = new Entity("mcs_credititem_value", entity.Id) { ["mcs_listname"] = tuple.Item3 };
					_service.Update(entity2);
					Console.WriteLine($"  ↻ 更新名称: {tuple.Item1}/{tuple.Item2} => {tuple.Item3}");
					num2++;
				}
				else
				{
					Console.WriteLine("  ⊘ 已存在: " + tuple.Item1 + "/" + tuple.Item2);
					num3++;
				}
			}
			else
			{
				Entity entity3 = new Entity("mcs_credititem_value");
				entity3["mcs_credititemno"] = new EntityReference("mcs_credit_items", value);
				entity3["mcs_listvalue"] = tuple.Item2;
				entity3["mcs_listname"] = tuple.Item3;
				Guid value2 = _service.Create(entity3);
				Console.WriteLine($"  ✓ 创建成功: {tuple.Item1}/{tuple.Item2}={tuple.Item3}, ID={value2}");
				num++;
			}
		}
		Console.WriteLine($"\n完成: 创建{num}条, 更新{num2}条, 已存在{num3}条, 跳过{num4}条, 总计{num + num2 + num3 + num4}条");
	}

	public void CreateTradePtTypeSampleData()
	{
		Console.WriteLine("=== 创建成交条件产品分类示例数据 ===");
		(string, string, string)[] array = new(string, string, string)[3]
		{
			("01", "燃油牵引车", "Fuel Tractor"),
			("02", "电动牵引车", "Electric Tractor"),
			("03", "泵车", "Concrete Pump Truck")
		};
		Dictionary<string, Guid> dictionary = new Dictionary<string, Guid>();
		int num = 0;
		int num2 = 0;
		(string, string, string)[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			(string, string, string) tuple = array2[i];
			QueryExpression queryExpression = new QueryExpression("mcs_trade_pttype");
			queryExpression.ColumnSet = new ColumnSet("mcs_trade_pttypeid");
			queryExpression.Criteria = new FilterExpression
			{
				Conditions = 
				{
					new ConditionExpression("mcs_typeid", ConditionOperator.Equal, tuple.Item1)
				}
			};
			queryExpression.TopCount = 1;
			QueryExpression query = queryExpression;
			EntityCollection entityCollection = _service.RetrieveMultiple(query);
			if (entityCollection.Entities.Count > 0)
			{
				dictionary[tuple.Item1] = entityCollection.Entities[0].Id;
				Console.WriteLine("  ⊘ 产品分类已存在: " + tuple.Item1 + "=" + tuple.Item2);
				num2++;
				continue;
			}
			Entity entity = new Entity("mcs_trade_pttype");
			entity["mcs_trade_pttypename"] = tuple.Item2;
			entity["mcs_typeid"] = tuple.Item1;
			entity["mcs_typenameen"] = tuple.Item3;
			Guid value = _service.Create(entity);
			dictionary[tuple.Item1] = value;
			Console.WriteLine($"  ✓ 创建产品分类: {tuple.Item1}={tuple.Item2}, ID={value}");
			num++;
		}
		Console.WriteLine($"\n产品分类: 创建{num}条, 已存在{num2}条");
		Console.WriteLine("\n=== 创建成交条件产品分类关系示例数据 ===");
		(string, string, string)[] array3 = new(string, string, string)[3]
		{
			("AK", "牵引车", "01"),
			("EL", "电动牵引车", "02"),
			("PM", "泵车", "03")
		};
		int num3 = 0;
		int num4 = 0;
		(string, string, string)[] array4 = array3;
		for (int j = 0; j < array4.Length; j++)
		{
			(string groupId, string groupName, string typeCode) pg = array4[j];
			if (!dictionary.TryGetValue(pg.typeCode, out var value2))
			{
				Console.WriteLine("  ✗ 未找到产品分类 " + pg.typeCode + "，跳过: " + pg.groupId);
				continue;
			}
			QueryExpression queryExpression = new QueryExpression("mcs_trade_ptgrouptype");
			queryExpression.ColumnSet = new ColumnSet("mcs_trade_ptgrouptypeid");
			queryExpression.Criteria = new FilterExpression
			{
				Conditions = 
				{
					new ConditionExpression("mcs_groupid", ConditionOperator.Equal, pg.groupId),
					new ConditionExpression("mcs_typeid", ConditionOperator.Equal, pg.typeCode)
				}
			};
			queryExpression.TopCount = 1;
			QueryExpression query2 = queryExpression;
			EntityCollection entityCollection2 = _service.RetrieveMultiple(query2);
			if (entityCollection2.Entities.Count > 0)
			{
				Console.WriteLine("  ⊘ 产品分类关系已存在: " + pg.groupId + "=" + pg.groupName);
				num4++;
				continue;
			}
			Entity entity2 = new Entity("mcs_trade_ptgrouptype");
			entity2["mcs_trade_ptgrouptypename"] = pg.groupName + "-" + pg.typeCode;
			entity2["mcs_groupid"] = pg.groupId;
			entity2["mcs_groupname"] = pg.groupName;
			entity2["mcs_typeid"] = pg.typeCode;
			entity2["mcs_typename"] = array.First(t => t.Item1 == pg.typeCode).Item2;
			entity2["mcs_trade_pttypeid"] = new EntityReference("mcs_trade_pttype", value2);
			Guid value3 = _service.Create(entity2);
			Console.WriteLine($"  ✓ 创建产品分类关系: {pg.groupId}={pg.groupName}, ID={value3}");
			num3++;
		}
		Console.WriteLine($"\n产品分类关系: 创建{num3}条, 已存在{num4}条");
	}

	public void FixCofaceQualitativeEnums()
	{
		Console.WriteLine("=== 修复 Coface 定性指标枚举值 ===");
		Dictionary<string, Guid> dictionary = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
		QueryExpression queryExpression = new QueryExpression("mcs_credit_items");
		queryExpression.ColumnSet = new ColumnSet("mcs_credit_itemsid", "mcs_credit_itemsno");
		QueryExpression query = queryExpression;
		EntityCollection entityCollection = _service.RetrieveMultiple(query);
		foreach (Entity entity in entityCollection.Entities)
		{
			string attributeValue = entity.GetAttributeValue<string>("mcs_credit_itemsno");
			Guid attributeValue2 = entity.GetAttributeValue<Guid>("mcs_credit_itemsid");
			if (!string.IsNullOrEmpty(attributeValue))
			{
				dictionary[attributeValue] = attributeValue2;
			}
		}
		List<(string, string)> list = new List<(string, string)>
		{
			("CountryRisk", "1"),
			("CountryRisk", "2"),
			("CountryRisk", "3"),
			("Sectors", "Mining"),
			("Sectors", "Port"),
			("Sectors", "Construction"),
			("Sectors", "Lifting"),
			("Sectors", "Container"),
			("Sectors", "Rental"),
			("Sectors", "Concrete"),
			("Sectors", "Forestry"),
			("Sectors", "Agriculture"),
			("Sectors", "Manufacturing"),
			("Sectors", "Transportation"),
			("Sectors", "Other")
		};
		int num = 0;
		foreach (var (text, text2) in list)
		{
			if (!dictionary.TryGetValue(text, out var value))
			{
				Console.WriteLine("  ⚠\ufe0f 未找到评分项目 " + text + "，跳过删除 " + text2);
				continue;
			}
			queryExpression = new QueryExpression("mcs_credititem_value");
			queryExpression.ColumnSet = new ColumnSet("mcs_credititem_valueid");
			queryExpression.Criteria = new FilterExpression
			{
				Conditions = 
				{
					new ConditionExpression("mcs_credititemno", ConditionOperator.Equal, value),
					new ConditionExpression("mcs_listvalue", ConditionOperator.Equal, text2)
				}
			};
			queryExpression.TopCount = 1;
			QueryExpression query2 = queryExpression;
			EntityCollection entityCollection2 = _service.RetrieveMultiple(query2);
			if (entityCollection2.Entities.Count > 0)
			{
				Guid id = entityCollection2.Entities[0].Id;
				try
				{
					_service.Delete("mcs_credititem_value", id);
					Console.WriteLine("  ✓ 删除旧记录: " + text + "/" + text2);
					num++;
				}
				catch (Exception ex)
				{
					Console.WriteLine($"  ❌ 删除失败 {text}/{text2}: {ex.Message}");
				}
			}
			else
			{
				Console.WriteLine("  ⊘ 不存在: " + text + "/" + text2);
			}
		}
		Console.WriteLine($"\n删除完成: {num} 条");
		CreateQualitativeEnumRecords();
	}

	public void CleanupQualitativeEnumRecords()
	{
		Console.WriteLine("清理定性评分项目枚举值记录...");
		QueryExpression queryExpression = new QueryExpression("mcs_credititem_value");
		queryExpression.ColumnSet = new ColumnSet("mcs_credititem_valueid");
		queryExpression.TopCount = 100;
		QueryExpression query = queryExpression;
		int num = 0;
		int num2 = 0;
		while (true)
		{
			EntityCollection entityCollection = _service.RetrieveMultiple(query);
			if (entityCollection.Entities.Count == 0)
			{
				break;
			}
			foreach (Entity entity in entityCollection.Entities)
			{
				_service.Delete("mcs_credititem_value", entity.Id);
				num++;
			}
			num2++;
			Console.WriteLine($"  第{num2}批: 已删除 {num} 条");
		}
		Console.WriteLine($"\n完成: 共删除 {num} 条记录");
	}

	public void CreateTradeStPayTermTestRecord()
	{
		Console.WriteLine("=== 创建成交条件样板库测试记录 ===");
		CleanupTradeStPayTermTestRecords();
		TestInvalidRecord("首付款比例 > 1", delegate(Entity e)
		{
			e["mcs_downpay"] = 1.5m;
		});
		TestInvalidRecord("账期不是 30 倍数", delegate(Entity e)
		{
			e["mcs_payterm"] = 45;
		});
		TestInvalidRecord("首付款100%但账期不为0", delegate(Entity e)
		{
			e["mcs_downpay"] = 1.0m;
			e["mcs_payterm"] = 30;
			e["mcs_payfreq"] = 0;
		});
		try
		{
			Entity entity = CreateTestEntity("US", "美国");
			Guid guid = _service.Create(entity);
			Console.WriteLine($"  ✓ 合法测试记录创建成功，ID: {guid}");
			Entity entity2 = _service.Retrieve("mcs_trade_stpayterm", guid, new ColumnSet("mcs_trade_stpaytermname"));
			string attributeValue = entity2.GetAttributeValue<string>("mcs_trade_stpaytermname");
			Console.WriteLine("  ✓ 自动生成标准条件编码: " + attributeValue);
		}
		catch (Exception ex)
		{
			Console.WriteLine("  ❌ 合法测试记录创建失败: " + ex.Message);
			throw;
		}
		try
		{
			Entity entity3 = CreateTestEntity("DE", "德国");
			entity3.Attributes.Remove("mcs_status");
			Guid id = _service.Create(entity3);
			Entity entity4 = _service.Retrieve("mcs_trade_stpayterm", id, new ColumnSet("mcs_status"));
			int? num = entity4.GetAttributeValue<OptionSetValue>("mcs_status")?.Value;
			if (num == 0)
			{
				Console.WriteLine("  ✓ 状态默认值校验通过: mcs_status = 0（未传入时 Plugin 正确默认）");
			}
			else
			{
				Console.WriteLine($"  ❌ 状态默认值校验失败: mcs_status = {num}");
			}
		}
		catch (Exception ex2)
		{
			Console.WriteLine("  ❌ 状态默认值测试失败: " + ex2.Message);
			throw;
		}
		try
		{
			Entity entity5 = CreateTestEntity("US", "美国");
			_service.Create(entity5);
			Console.WriteLine("  ❌ 重复校验未生效，相同维度记录创建成功");
		}
		catch (Exception ex3) when (ex3.Message.Contains("保存失败") || ex3.Message.Contains("存在有重复记录"))
		{
			Console.WriteLine("  ✓ 重复校验生效: " + ex3.Message);
		}
	}

	private void CleanupTradeStPayTermTestRecords()
	{
		QueryExpression queryExpression = new QueryExpression("mcs_trade_stpayterm");
		queryExpression.ColumnSet = new ColumnSet("mcs_trade_stpaytermid");
		queryExpression.Criteria = new FilterExpression
		{
			Conditions = 
			{
				new ConditionExpression("mcs_buid", ConditionOperator.Equal, "BU-TEST")
			}
		};
		QueryExpression query = queryExpression;
		EntityCollection entityCollection = _service.RetrieveMultiple(query);
		int num = 0;
		foreach (Entity entity in entityCollection.Entities)
		{
			_service.Delete("mcs_trade_stpayterm", entity.Id);
			num++;
		}
		if (num > 0)
		{
			Console.WriteLine($"  ✓ 已清理 {num} 条历史测试记录");
		}
	}

	private Entity CreateTestEntity(string countryCode, string countryName)
	{
		return new Entity("mcs_trade_stpayterm")
		{
			["mcs_buid"] = "BU-TEST",
			["mcs_buname"] = "测试事业部",
			["mcs_subid"] = "SUB-TEST",
			["mcs_subname"] = "测试子公司",
			["mcs_countrycode"] = countryCode,
			["mcs_countryname"] = countryName,
			["mcs_typeid"] = "01",
			["mcs_typename"] = "燃油牵引车",
			["mcs_buyergrade"] = new OptionSetValueCollection
			{
				new OptionSetValue(100000000),
				new OptionSetValue(100000001)
			},
			["mcs_creditgrade"] = new OptionSetValueCollection
			{
				new OptionSetValue(100000000)
			},
			["mcs_downpay"] = 0.21m,
			["mcs_payterm"] = 60,
			["mcs_payfreq"] = 30,
			["mcs_status"] = new OptionSetValue(0)
		};
	}

	private void TestInvalidRecord(string testName, Action<Entity> setup)
	{
		try
		{
			Entity entity = CreateTestEntity("US", "美国");
			setup(entity);
			_service.Create(entity);
			Console.WriteLine("  ❌ [" + testName + "] 未触发校验，创建成功");
		}
		catch (Exception ex) when (ex.Message.Contains("保存失败") || ex.Message.Contains("valid range") || ex.Message.Contains("outside"))
		{
			Console.WriteLine("  ✓ [" + testName + "] 校验生效: " + ex.Message);
		}
	}

	public void CheckCreditItemRecords()
	{
		Console.WriteLine("查询评分项目记录...");
		QueryExpression queryExpression = new QueryExpression("mcs_credit_items");
		queryExpression.ColumnSet = new ColumnSet("mcs_credit_itemsid", "mcs_credit_itemsno", "mcs_itemname", "mcs_itemdesc", "mcs_group", "mcs_datatype", "mcs_source", "mcs_validate", "mcs__3p", "createdon");
		queryExpression.Orders.Add(new OrderExpression("mcs_itemname", OrderType.Ascending));
		QueryExpression query = queryExpression;
		EntityCollection entityCollection = _service.RetrieveMultiple(query);
		Console.WriteLine($"系统中共有 {entityCollection.Entities.Count} 条评分项目记录\n");
		Console.WriteLine("评分项目编码         评分项目名称      分类          类型      来源      补录    3P      创建日期              ");
		Console.WriteLine(new string('-', 110));
		foreach (Entity entity in entityCollection.Entities)
		{
			string value = (entity.Contains("mcs_credit_itemsno") ? (entity.GetAttributeValue<string>("mcs_credit_itemsno") ?? "(空)") : "(空)");
			string value2 = (entity.Contains("mcs_itemname") ? (entity.GetAttributeValue<string>("mcs_itemname") ?? "(空)") : "(空)");
			string value3 = (entity.Contains("mcs_group") ? ((OptionSetValue)entity["mcs_group"]).Value.ToString() : "(空)");
			string value4 = (entity.Contains("mcs_datatype") ? ((OptionSetValue)entity["mcs_datatype"]).Value.ToString() : "(空)");
			string value5 = (entity.Contains("mcs_source") ? ((OptionSetValue)entity["mcs_source"]).Value.ToString() : "(空)");
			string value6 = (entity.Contains("mcs_validate") ? entity.GetAttributeValue<bool>("mcs_validate").ToString() : "(空)");
			string value7 = (entity.Contains("mcs__3p") ? entity.GetAttributeValue<bool>("mcs__3p").ToString() : "(空)");
			string value8 = entity.GetAttributeValue<DateTime>("createdon").ToLocalTime().ToString("yyyy-MM-dd HH:mm");
			Console.WriteLine($"{value:<20} {value2:<15} {value3:<12} {value4:<8} {value5:<8} {value6:<6} {value7:<6} {value8:<22}");
		}
		Console.WriteLine("\n=== 重复名称检查 ===");
		IEnumerable<IGrouping<string, Entity>> enumerable = from e in entityCollection.Entities
			where e.Contains("mcs_itemname") && !string.IsNullOrEmpty(e.GetAttributeValue<string>("mcs_itemname"))
			group e by e.GetAttributeValue<string>("mcs_itemname") into g
			where g.Count() > 1
			select g;
		if (enumerable.Any())
		{
			foreach (IGrouping<string, Entity> item in enumerable)
			{
				Console.WriteLine($"名称 '{item.Key}' 有 {item.Count()} 条记录:");
				foreach (Entity item2 in item)
				{
					string text = (item2.Contains("mcs_credit_itemsno") ? (item2.GetAttributeValue<string>("mcs_credit_itemsno") ?? "(空)") : "(空)");
					string text2 = item2.Id.ToString().Substring(0, 8);
					Console.WriteLine("  - ID=" + text2 + "..., 编码=" + text);
				}
			}
		}
		else
		{
			Console.WriteLine("未发现重复名称");
		}
		Console.WriteLine("\n=== 空编码检查 ===");
		IEnumerable<Entity> enumerable2 = entityCollection.Entities.Where((Entity e) => !e.Contains("mcs_credit_itemsno") || string.IsNullOrEmpty(e.GetAttributeValue<string>("mcs_credit_itemsno")));
		if (enumerable2.Any())
		{
			foreach (Entity item3 in enumerable2)
			{
				string value9 = (item3.Contains("mcs_itemname") ? (item3.GetAttributeValue<string>("mcs_itemname") ?? "(空)") : "(空)");
				string value10 = item3.Id.ToString().Substring(0, 8);
				Console.WriteLine($"ID={value10}..., 名称={value9}, 编码=空");
			}
			return;
		}
		Console.WriteLine("未发现空编码记录");
	}

	public void CleanupCreditItemRecords()
	{
		Console.WriteLine("清理评分项目记录...");
		string[] array = new string[3] { "EstablishedYear", "NetProfitMargin", "NaceCodes" };
		int num = 0;
		string[] array2 = array;
		foreach (string value in array2)
		{
			QueryExpression queryExpression = new QueryExpression("mcs_credit_items");
			queryExpression.ColumnSet = new ColumnSet("mcs_credit_itemsid", "mcs_itemname");
			queryExpression.Criteria = new FilterExpression
			{
				Conditions = 
				{
					new ConditionExpression("mcs_credit_itemsno", ConditionOperator.Equal, value)
				}
			};
			QueryExpression query = queryExpression;
			EntityCollection entityCollection = _service.RetrieveMultiple(query);
			foreach (Entity entity in entityCollection.Entities)
			{
				_service.Delete("mcs_credit_items", entity.Id);
				string value2 = entity.GetAttributeValue<string>("mcs_itemname") ?? "(空)";
				Console.WriteLine($"  ✗ 删除重复记录: {value} ({value2}), ID={entity.Id}");
				num++;
			}
		}
		string[] array3 = new string[5] { "交易记录", "信用分", "市场风险", "综合评分", "财务状况" };
		int num2 = 0;
		string[] array4 = array3;
		foreach (string value3 in array4)
		{
			QueryExpression queryExpression = new QueryExpression("mcs_credit_items");
			queryExpression.ColumnSet = new ColumnSet("mcs_credit_itemsid");
			queryExpression.Criteria = new FilterExpression
			{
				Conditions = 
				{
					new ConditionExpression("mcs_itemname", ConditionOperator.Equal, value3),
					new ConditionExpression("mcs_credit_itemsno", ConditionOperator.Null)
				}
			};
			QueryExpression query2 = queryExpression;
			EntityCollection entityCollection2 = _service.RetrieveMultiple(query2);
			foreach (Entity entity2 in entityCollection2.Entities)
			{
				_service.Delete("mcs_credit_items", entity2.Id);
				Console.WriteLine($"  ✗ 删除空编码记录: {value3}, ID={entity2.Id}");
				num2++;
			}
		}
		Console.WriteLine($"\n完成: 删除重复{num}条, 删除空编码{num2}条, 总计删除{num + num2}条");
	}

	public void DeleteCreditItemByCode(string code)
	{
		Console.WriteLine("删除编码为 " + code + " 的评分项目记录...");
		QueryExpression queryExpression = new QueryExpression("mcs_credit_items");
		queryExpression.ColumnSet = new ColumnSet("mcs_credit_itemsid", "mcs_itemname");
		queryExpression.Criteria = new FilterExpression
		{
			Conditions = 
			{
				new ConditionExpression("mcs_credit_itemsno", ConditionOperator.Equal, code)
			}
		};
		QueryExpression query = queryExpression;
		EntityCollection entityCollection = _service.RetrieveMultiple(query);
		foreach (Entity entity in entityCollection.Entities)
		{
			_service.Delete("mcs_credit_items", entity.Id);
			string value = entity.GetAttributeValue<string>("mcs_itemname") ?? "(空)";
			Console.WriteLine($"  ✗ 删除: {code} ({value}), ID={entity.Id}");
		}
		Console.WriteLine("完成");
	}

	public void UpdateLookupView(string entityName, string[] fields)
	{
		Console.WriteLine("更新 " + entityName + " 的Lookup视图...");
		QueryExpression queryExpression = new QueryExpression("savedquery");
		queryExpression.ColumnSet = new ColumnSet("savedqueryid", "name", "layoutxml", "fetchxml");
		queryExpression.Criteria = new FilterExpression
		{
			Conditions = 
			{
				new ConditionExpression("returnedtypecode", ConditionOperator.Equal, entityName),
				new ConditionExpression("querytype", ConditionOperator.Equal, 64)
			}
		};
		QueryExpression query = queryExpression;
		EntityCollection entityCollection = _service.RetrieveMultiple(query);
		if (entityCollection.Entities.Count == 0)
		{
			Console.WriteLine("  ✗ 未找到Lookup视图");
			return;
		}
		foreach (Entity entity2 in entityCollection.Entities)
		{
			string attributeValue = entity2.GetAttributeValue<string>("fetchxml");
			string attributeValue2 = entity2.GetAttributeValue<string>("name");
			Console.WriteLine("  视图: " + attributeValue2);
			string text = "";
			int num = 0;
			foreach (string text2 in fields)
			{
				if (attributeValue.Contains("name=\"" + text2 + "\""))
				{
					Console.WriteLine("    ⊘ fetchxml " + text2 + " 已存在，跳过");
					continue;
				}
				text = text + "<attribute name=\"" + text2 + "\" />";
				Console.WriteLine("    + fetchxml " + text2);
				num++;
			}
			if (num > 0)
			{
				Entity entity = new Entity("savedquery", entity2.Id);
				int num2 = attributeValue.IndexOf(">", attributeValue.IndexOf("<entity"));
				if (num2 > 0)
				{
					string value = attributeValue.Insert(num2 + 1, text);
					entity["fetchxml"] = value;
					_service.Update(entity);
					Console.WriteLine($"  ✓ Lookup视图已更新，添加 {num} 个attribute");
				}
			}
			else
			{
				Console.WriteLine("  没有需要更新的内容");
			}
		}
		PublishEntity(entityName);
	}

	public void ExportLookupViewFetchXml(string entityName, string outputPath)
	{
		Console.WriteLine("导出 " + entityName + " 的Lookup视图fetchxml...");
		QueryExpression queryExpression = new QueryExpression("savedquery");
		queryExpression.ColumnSet = new ColumnSet("savedqueryid", "name", "fetchxml", "layoutxml");
		queryExpression.Criteria = new FilterExpression
		{
			Conditions = 
			{
				new ConditionExpression("returnedtypecode", ConditionOperator.Equal, entityName),
				new ConditionExpression("querytype", ConditionOperator.Equal, 64)
			}
		};
		QueryExpression query = queryExpression;
		EntityCollection entityCollection = _service.RetrieveMultiple(query);
		if (entityCollection.Entities.Count == 0)
		{
			Console.WriteLine("  ✗ 未找到Lookup视图");
			return;
		}
		foreach (Entity entity in entityCollection.Entities)
		{
			string attributeValue = entity.GetAttributeValue<string>("fetchxml");
			string attributeValue2 = entity.GetAttributeValue<string>("layoutxml");
			string attributeValue3 = entity.GetAttributeValue<string>("name");
			string text = "=== " + attributeValue3 + " ===\n\n";
			text = text + "fetchxml:\n" + attributeValue + "\n\n";
			text = text + "layoutxml:\n" + attributeValue2 + "\n";
			File.WriteAllText(outputPath, text);
			Console.WriteLine("  ✓ 已导出到: " + outputPath);
		}
	}

	public void UpdateAssemblyOnly(string dllPath)
	{
		if (!File.Exists(dllPath))
		{
			Console.WriteLine("✗ DLL不存在: " + dllPath);
			return;
		}
		byte[] array = File.ReadAllBytes(dllPath);
		string value = Convert.ToBase64String(array);
		string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(dllPath);
		Console.WriteLine("更新Assembly: " + fileNameWithoutExtension);
		Console.WriteLine($"DLL大小: {array.Length / 1024} KB");
		QueryExpression queryExpression = new QueryExpression("pluginassembly");
		queryExpression.ColumnSet = new ColumnSet("pluginassemblyid");
		queryExpression.Criteria = new FilterExpression
		{
			Conditions = 
			{
				new ConditionExpression("name", ConditionOperator.Equal, fileNameWithoutExtension)
			}
		};
		QueryExpression query = queryExpression;
		EntityCollection entityCollection = _service.RetrieveMultiple(query);
		if (entityCollection.Entities.Count > 0)
		{
			Guid id = entityCollection.Entities[0].Id;
			Entity entity = new Entity("pluginassembly", id);
			entity["content"] = value;
			_service.Update(entity);
			Console.WriteLine($"✓ Assembly已更新 (ID: {id})");
		}
		else
		{
			Entity entity2 = new Entity("pluginassembly");
			entity2["name"] = fileNameWithoutExtension;
			entity2["content"] = value;
			entity2["sourcetype"] = new OptionSetValue(0);
			entity2["isolationmode"] = new OptionSetValue(2);
			entity2["culture"] = "neutral";
			entity2["version"] = "1.0.0.0";
			entity2["publickeytoken"] = "null";
			Guid value2 = _service.Create(entity2);
			Console.WriteLine($"✓ Assembly已创建 (ID: {value2})");
		}
	}

	public void QueryContractsSummary(string accountId = null)
	{
		QueryExpression queryExpression = new QueryExpression("mcs_contract")
		{
			ColumnSet = new ColumnSet(allColumns: false),
			TopCount = 1
		};
		if (!string.IsNullOrEmpty(accountId))
		{
			queryExpression.Criteria.AddCondition("mcs_contractbuyer", ConditionOperator.Equal, new Guid(accountId));
		}
		EntityCollection entityCollection = _service.RetrieveMultiple(queryExpression);
		Console.WriteLine("mcs_contract 存在记录: " + ((entityCollection.Entities.Count > 0) ? "是" : "否"));
		QueryExpression queryExpression2 = new QueryExpression("mcs_contract");
		queryExpression2.ColumnSet = new ColumnSet("mcs_contractstatus", "mcs_contractbuyer", "mcs_customermaster", "mcs_name", "mcs_totalcontractamount");
		queryExpression2.PageInfo = new PagingInfo
		{
			Count = 5000,
			PageNumber = 1
		};
		QueryExpression queryExpression3 = queryExpression2;
		if (!string.IsNullOrEmpty(accountId))
		{
			queryExpression3.Criteria.AddCondition("mcs_contractbuyer", ConditionOperator.Equal, new Guid(accountId));
		}
		Dictionary<int, int> dictionary = new Dictionary<int, int>();
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		int num4 = 0;
		while (true)
		{
			EntityCollection entityCollection2 = _service.RetrieveMultiple(queryExpression3);
			foreach (Entity entity in entityCollection2.Entities)
			{
				num++;
				int num5 = (entity.Contains("mcs_contractstatus") ? entity.GetAttributeValue<OptionSetValue>("mcs_contractstatus").Value : 0);
				if (!dictionary.ContainsKey(num5))
				{
					dictionary[num5] = 0;
				}
				dictionary[num5]++;
				if (entity.Contains("mcs_contractbuyer") || entity.Contains("mcs_customermaster"))
				{
					num2++;
				}
				if (entity.Contains("mcs_totalcontractamount"))
				{
					num3++;
				}
				if (num4 < 5)
				{
					object value = (entity.Contains("mcs_name") ? entity["mcs_name"] : "(无名称)");
					Console.WriteLine($"  样本[{num4 + 1}] ID={entity.Id}, 状态={num5}, 名称={value}");
					num4++;
				}
			}
			if (!entityCollection2.MoreRecords)
			{
				break;
			}
			queryExpression3.PageInfo.PageNumber++;
			queryExpression3.PageInfo.PagingCookie = entityCollection2.PagingCookie;
		}
		Console.WriteLine($"\n总记录数: {num}");
		Console.WriteLine($"有关联客户字段数: {num2}");
		Console.WriteLine($"有合同金额字段数: {num3}");
		Console.WriteLine("按 mcs_contractstatus 分布:");
		foreach (KeyValuePair<int, int> item in dictionary.OrderBy((KeyValuePair<int, int> x) => x.Key))
		{
			Console.WriteLine($"  值 {item.Key}: {item.Value} 条");
		}
	}

	public void QueryOptionSetLabels(string entityName, string fieldName, int? langId = null)
	{
		QueryExpression queryExpression = new QueryExpression("stringmap");
		queryExpression.ColumnSet = new ColumnSet("attributevalue", "value", "langid", "displayorder");
		queryExpression.Criteria = new FilterExpression
		{
			Conditions = 
			{
				new ConditionExpression("objecttypecode", ConditionOperator.Equal, entityName),
				new ConditionExpression("attributename", ConditionOperator.Equal, fieldName)
			}
		};
		queryExpression.Orders.Add(new OrderExpression("attributevalue", OrderType.Ascending));
		QueryExpression queryExpression2 = queryExpression;
		if (langId.HasValue)
		{
			queryExpression2.Criteria.AddCondition("langid", ConditionOperator.Equal, langId.Value);
		}
		EntityCollection entityCollection = _service.RetrieveMultiple(queryExpression2);
		Console.WriteLine($"=== {entityName}.{fieldName} 选项集标签 ===");
		foreach (Entity entity in entityCollection.Entities)
		{
			Console.WriteLine($"  值: {entity["attributevalue"]}, 标签: {entity["value"]}, LangId: {entity["langid"]}");
		}
	}

	public void UpdateOptionSetLabels(string entityName, string fieldName, string labelsJsonFile, int? languageCode = null)
	{
		if (!File.Exists(labelsJsonFile))
		{
			Console.WriteLine($"标签定义文件不存在: {labelsJsonFile}");
			return;
		}
		string text = File.ReadAllText(labelsJsonFile);
		Dictionary<int, string> dictionary = System.Text.Json.JsonSerializer.Deserialize<Dictionary<int, string>>(text);
		if (dictionary == null || dictionary.Count == 0)
		{
			Console.WriteLine("标签定义文件为空或格式错误");
			return;
		}
		MetadataFieldService metadataFieldService = new MetadataFieldService(_service);
		metadataFieldService.UpdateOptionSetLabels(entityName, fieldName, dictionary, languageCode ?? 2052);
	}

	/// <summary>
	/// 将 mcs_fca_proc 的 mcs_modelgrant / mcs_initigrant 从 Money 重建成 Decimal
	/// 流程：从主表单移除引用 → 发布 → 删除字段 → 创建 Decimal 字段 → 恢复表单引用（classid 改为 Decimal） → 发布
	/// </summary>
	public void RecreateFcaDecimalFields()
	{
		string entityName = "mcs_fca_proc";
		string[] fieldNames = new[] { "mcs_modelgrant", "mcs_initigrant" };
		string decimalClassId = "{C3EFE9C6-EAFF-4e70-90AA-9FDB34E0FEA1}";

		Console.WriteLine($">>> 开始处理 {entityName} 的 Money→Decimal 字段重建");

		// 1. 收集所有依赖该字段的表单 ID（含 BPF/相关实体表单）
		var dependentFormIds = new HashSet<Guid>();
		foreach (var field in fieldNames)
		{
			foreach (var formId in GetFieldDependentFormIds(entityName, field))
			{
				dependentFormIds.Add(formId);
			}
		}

		// 2. 加载这些表单
		var forms = new List<Entity>();
		foreach (var formId in dependentFormIds)
		{
			var form = GetFormById(formId);
			if (form != null)
			{
				forms.Add(form);
			}
		}

		if (forms.Count == 0)
		{
			Console.WriteLine($"未找到引用字段的表单");
			return;
		}
		Console.WriteLine($"找到 {forms.Count} 个引用字段的表单");

		var originalXmls = new Dictionary<Guid, string>();
		bool anyFormUpdated = false;

		// 3. 从所有表单移除字段引用
		foreach (var form in forms)
		{
			Guid formId = form.Id;
			string originalXml = form.GetAttributeValue<string>("formxml");
			originalXmls[formId] = originalXml;
			string formName = form.GetAttributeValue<string>("name");
			int formType = form.GetAttributeValue<OptionSetValue>("type")?.Value ?? -1;
			string formObjectType = form.GetAttributeValue<string>("objecttypecode");
			Console.WriteLine($"  表单: {formName} ({formId}), type={formType}, entity={formObjectType}");

			string xmlWithoutFields = RemoveFieldsFromFormXml(originalXml, fieldNames);
			if (xmlWithoutFields != originalXml)
			{
				Console.WriteLine($"    从表单移除字段引用");
				UpdateFormXml(formId, xmlWithoutFields);
				anyFormUpdated = true;
			}
		}

		if (anyFormUpdated)
		{
			Console.WriteLine($">>> 发布实体 {entityName}...");
			PublishEntity(entityName);
		}
		else
		{
			Console.WriteLine("表单中未找到目标字段控件，继续执行字段删除/重建");
		}

		// 4. 删除旧字段
		foreach (var field in fieldNames)
		{
			DeleteField(entityName, field);
		}

		// 5. 创建 Decimal 字段（保留原显示名）
		CreateDecimalField(entityName, "mcs_modelgrant", "模型计算额度USD", "", 0m, 999999999.99m, 2, "模型计算额度USD", "Model Calculated Quota USD");
		CreateDecimalField(entityName, "mcs_initigrant", "调整模型额度USD", "", 0m, 999999999.99m, 2, "调整模型额度USD", "Adjusted Model Quota USD");

		// 6. 恢复所有表单 XML，并将控件 classid 改为 Decimal
		foreach (var formId in dependentFormIds)
		{
			if (originalXmls.TryGetValue(formId, out string originalXml))
			{
				string restoredXml = UpdateFieldClassIds(originalXml, fieldNames, decimalClassId);
				if (restoredXml != originalXml)
				{
					UpdateFormXml(formId, restoredXml);
				}
			}
		}

		// 7. 再次发布
		Console.WriteLine($">>> 再次发布实体 {entityName}...");
		PublishEntity(entityName);

		Console.WriteLine("✓ mcs_modelgrant / mcs_initigrant Money→Decimal 重建完成");
	}

	private Entity GetFormById(Guid formId)
	{
		var query = new QueryExpression("systemform")
		{
			ColumnSet = new ColumnSet("formid", "name", "formxml", "type", "objecttypecode"),
			Criteria = new FilterExpression
			{
				Conditions =
				{
					new ConditionExpression("formid", ConditionOperator.Equal, formId)
				}
			}
		};
		var results = _service.RetrieveMultiple(query);
		return results.Entities.FirstOrDefault();
	}

	private void UpdateFormXml(Guid formId, string formXml)
	{
		var update = new Entity("systemform", formId);
		update["formxml"] = formXml;
		_service.Update(update);
		Console.WriteLine($"  ✓ 表单已更新: {formId}");
	}

	private string RemoveFieldsFromFormXml(string formXml, string[] fieldNames)
	{
		var doc = XDocument.Parse(formXml);
		var ns = doc.Root.GetDefaultNamespace();
		bool changed = false;

		foreach (var field in fieldNames)
		{
			// 查找所有 datafieldname 匹配的 control / parameter / 等控件元素
			var elements = doc.Descendants()
				.Where(e =>
				{
					var attr = e.Attribute("datafieldname");
					return attr != null && string.Equals(attr.Value, field, StringComparison.OrdinalIgnoreCase);
				})
				.ToList();

			foreach (var element in elements)
			{
				var cell = element.Parent;
				if (cell != null && cell.Name == ns + "cell")
				{
					var row = cell.Parent;
					cell.Remove();
					changed = true;

					if (row != null && row.Name == ns + "row" && !row.Elements(ns + "cell").Any())
					{
						row.Remove();
					}
				}
				else
				{
					// 不在 cell 内（如事件/参数等），直接移除该元素
					element.Remove();
					changed = true;
				}
			}
		}

		return changed ? doc.ToString(SaveOptions.None) : formXml;
	}

	private string UpdateFieldClassIds(string formXml, string[] fieldNames, string classId)
	{
		var doc = XDocument.Parse(formXml);
		var ns = doc.Root.GetDefaultNamespace();

		foreach (var field in fieldNames)
		{
			var controls = doc.Descendants(ns + "control")
				.Where(c => (string)c.Attribute("datafieldname") == field);

			foreach (var control in controls)
			{
				control.SetAttributeValue("classid", classId);
			}
		}

		return doc.ToString(SaveOptions.None);
	}

	private List<Guid> GetFieldDependentFormIds(string entityName, string fieldName)
	{
		var formIds = new List<Guid>();
		try
		{
			// 获取属性元数据 ID
			var attrRequest = new RetrieveAttributeRequest
			{
				EntityLogicalName = entityName,
				LogicalName = fieldName,
				RetrieveAsIfPublished = true
			};
			var attrResponse = (RetrieveAttributeResponse)_service.Execute(attrRequest);
			Guid attrId = attrResponse.AttributeMetadata.MetadataId.GetValueOrDefault();
			if (attrId == Guid.Empty)
			{
				Console.WriteLine($"  未找到字段 {fieldName}");
				return formIds;
			}

			var request = new RetrieveDependenciesForDeleteRequest
			{
				ObjectId = attrId,
				ComponentType = 2
			};
			var response = (RetrieveDependenciesForDeleteResponse)_service.Execute(request);

			foreach (var dep in response.EntityCollection.Entities)
			{
				var depComponentType = dep.GetAttributeValue<OptionSetValue>("dependentcomponenttype")?.Value;
				var depObjectId = dep.GetAttributeValue<Guid>("dependentcomponentobjectid");
				Console.WriteLine($"  依赖: ComponentType={depComponentType}, ObjectId={depObjectId}");
				if (depComponentType == 26)
				{
					formIds.Add(depObjectId);
				}
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"  查询依赖失败: {ex.Message}");
		}
		return formIds;
	}
}
