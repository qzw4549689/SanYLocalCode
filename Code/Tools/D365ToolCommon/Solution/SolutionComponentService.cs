using Microsoft.Crm.Sdk.Messages;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace D365ToolCommon.Solution
{
    /// <summary>
    /// Solution 基本信息（用于发版自检的版本对比等场景）。
    /// </summary>
    public record SolutionInfo(
        Guid SolutionId,
        string UniqueName,
        string FriendlyName,
        string Version,
        bool IsManaged,
        DateTime? ModifiedOn);

    /// <summary>
    /// Solution 组件管理通用服务。
    /// 封装 WebResource、实体等组件添加到 Solution 的操作。
    /// </summary>
    public class SolutionComponentService
    {
        private readonly ServiceClient _service;

        public SolutionComponentService(ServiceClient service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        /// <summary>
        /// 将 WebResource 添加到指定 Solution。
        /// </summary>
        public Guid AddWebResourceToSolution(string webResourceName, string solutionUniqueName)
        {
            var webResourceId = QueryWebResourceId(webResourceName);
            if (webResourceId == Guid.Empty)
                throw new InvalidOperationException($"未找到 WebResource: {webResourceName}");

            return AddComponentToSolution(webResourceId, 61, solutionUniqueName);
        }

        /// <summary>
        /// 将实体添加到指定 Solution。
        /// </summary>
        public Guid AddEntityToSolution(string entityLogicalName, string solutionUniqueName)
        {
            var entityId = QueryEntityId(entityLogicalName);
            if (entityId == Guid.Empty)
                throw new InvalidOperationException($"未找到实体: {entityLogicalName}");

            return AddComponentToSolution(entityId, 1, solutionUniqueName);
        }

        /// <summary>
        /// 通用：将组件添加到 Solution。
        /// </summary>
        public Guid AddComponentToSolution(Guid componentId, int componentType, string solutionUniqueName)
        {
            if (string.IsNullOrWhiteSpace(solutionUniqueName))
                throw new ArgumentException("Solution 唯一名称不能为空", nameof(solutionUniqueName));

            var solutionId = QuerySolutionId(solutionUniqueName);
            if (solutionId == Guid.Empty)
                throw new InvalidOperationException($"未找到 Solution: {solutionUniqueName}");

            if (IsComponentInSolution(componentId, componentType, solutionId))
            {
                Console.WriteLine($"  组件已在 Solution {solutionUniqueName} 中，跳过");
                return Guid.Empty;
            }

            var request = new AddSolutionComponentRequest
            {
                ComponentId = componentId,
                ComponentType = componentType,
                SolutionUniqueName = solutionUniqueName,
                AddRequiredComponents = false
            };

            var response = (AddSolutionComponentResponse)_service.Execute(request);
            Console.WriteLine($"  ✅ 已添加到 Solution {solutionUniqueName}: {response.id}");
            return response.id;
        }

        /// <summary>
        /// 通用：将组件从 Solution 移除（只移出 Solution，不删除环境中的组件本体）。幂等。
        /// </summary>
        /// <returns>true=已移除；false=组件本就不在 Solution 中</returns>
        public bool RemoveComponentFromSolution(Guid componentId, int componentType, string solutionUniqueName)
        {
            if (string.IsNullOrWhiteSpace(solutionUniqueName))
                throw new ArgumentException("Solution 唯一名称不能为空", nameof(solutionUniqueName));

            var solutionId = QuerySolutionId(solutionUniqueName);
            if (solutionId == Guid.Empty)
                throw new InvalidOperationException($"未找到 Solution: {solutionUniqueName}");

            var query = new QueryExpression("solutioncomponent")
            {
                ColumnSet = new ColumnSet("solutioncomponentid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("solutionid", ConditionOperator.Equal, solutionId),
                        new ConditionExpression("componenttype", ConditionOperator.Equal, componentType),
                        new ConditionExpression("objectid", ConditionOperator.Equal, componentId)
                    }
                }
            };
            var existing = _service.RetrieveMultiple(query);
            if (existing.Entities.Count == 0)
            {
                Console.WriteLine($"  组件不在 Solution {solutionUniqueName} 中，跳过");
                return false;
            }

            foreach (var component in existing.Entities)
            {
                // solutioncomponent 实体不支持 Delete，须用 RemoveSolutionComponentRequest
                var request = new Microsoft.Crm.Sdk.Messages.RemoveSolutionComponentRequest
                {
                    ComponentId = componentId,
                    ComponentType = componentType,
                    SolutionUniqueName = solutionUniqueName
                };
                _service.Execute(request);
                Console.WriteLine($"  ✅ 已从 Solution {solutionUniqueName} 移除: {componentId}（组件本体保留在环境中）");
            }
            return true;
        }

        private Guid QuerySolutionId(string uniqueName)
        {
            var query = new QueryExpression("solution")
            {
                ColumnSet = new ColumnSet("solutionid"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("uniquename", ConditionOperator.Equal, uniqueName) }
                }
            };
            var result = _service.RetrieveMultiple(query).Entities.FirstOrDefault();
            return result?.Id ?? Guid.Empty;
        }

        private Guid QueryWebResourceId(string name)
        {
            var query = new QueryExpression("webresource")
            {
                ColumnSet = new ColumnSet("webresourceid"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("name", ConditionOperator.Equal, name) }
                }
            };
            var result = _service.RetrieveMultiple(query).Entities.FirstOrDefault();
            return result?.Id ?? Guid.Empty;
        }

        private Guid QueryEntityId(string logicalName)
        {
            var query = new QueryExpression("entity")
            {
                ColumnSet = new ColumnSet("entityid"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("name", ConditionOperator.Equal, logicalName) }
                }
            };
            var result = _service.RetrieveMultiple(query).Entities.FirstOrDefault();
            return result?.Id ?? Guid.Empty;
        }

        private bool IsComponentInSolution(Guid componentId, int componentType, Guid solutionId)
        {
            var query = new QueryExpression("solutioncomponent")
            {
                ColumnSet = new ColumnSet("solutioncomponentid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("solutionid", ConditionOperator.Equal, solutionId),
                        new ConditionExpression("objectid", ConditionOperator.Equal, componentId),
                        new ConditionExpression("componenttype", ConditionOperator.Equal, componentType)
                    }
                }
            };
            return _service.RetrieveMultiple(query).Entities.Any();
        }

        // ==================== 以下为发版只读自检能力 ====================

        /// <summary>
        /// 按唯一名查询 Solution 基本信息。未找到时返回 null。
        /// </summary>
        public SolutionInfo? GetSolutionInfo(string solutionUniqueName)
        {
            var query = new QueryExpression("solution")
            {
                ColumnSet = new ColumnSet("solutionid", "uniquename", "friendlyname", "version", "ismanaged", "modifiedon"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("uniquename", ConditionOperator.Equal, solutionUniqueName) }
                }
            };
            var entity = _service.RetrieveMultiple(query).Entities.FirstOrDefault();
            if (entity == null) return null;

            return new SolutionInfo(
                entity.Id,
                entity.GetAttributeValue<string>("uniquename") ?? solutionUniqueName,
                entity.GetAttributeValue<string>("friendlyname") ?? "",
                entity.GetAttributeValue<string>("version") ?? "",
                entity.GetAttributeValue<bool?>("ismanaged") ?? false,
                entity.GetAttributeValue<DateTime?>("modifiedon"));
        }

        /// <summary>
        /// 列出指定 Solution 的全部组件（componenttype + objectid）。
        /// Solution 不存在时返回空列表。
        /// </summary>
        public List<(int ComponentType, Guid ObjectId)> ListSolutionComponents(string solutionUniqueName)
        {
            var components = new List<(int, Guid)>();
            var solutionId = QuerySolutionId(solutionUniqueName);
            if (solutionId == Guid.Empty)
            {
                Console.WriteLine($"  ⚠️ 未找到 Solution: {solutionUniqueName}");
                return components;
            }

            var query = new QueryExpression("solutioncomponent")
            {
                ColumnSet = new ColumnSet("componenttype", "objectid"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("solutionid", ConditionOperator.Equal, solutionId) }
                },
                // 组件量大时分页拉取，避免默认 5000 条上限截断
                PageInfo = new PagingInfo { Count = 5000, PageNumber = 1 }
            };

            while (true)
            {
                var result = _service.RetrieveMultiple(query);
                foreach (var e in result.Entities)
                {
                    var type = UnwrapToInt(e.GetAttributeValue<object>("componenttype"));
                    var objectId = UnwrapToGuid(e.GetAttributeValue<object>("objectid"));
                    if (objectId != Guid.Empty)
                        components.Add((type, objectId));
                }
                if (!result.MoreRecords) break;
                query.PageInfo.PageNumber++;
                query.PageInfo.PagingCookie = result.PagingCookie;
            }
            return components;
        }

        /// <summary>
        /// 调用 RetrieveRequiredComponentsRequest 查询指定组件的依赖组件。
        /// 调用失败时捕获异常返回空列表并输出警告，不让单个组件失败中断整体。
        /// </summary>
        public List<(int RequiredType, Guid RequiredId)> GetRequiredComponents(int componentType, Guid objectId)
        {
            var required = new List<(int, Guid)>();
            try
            {
                var response = (RetrieveRequiredComponentsResponse)_service.Execute(new RetrieveRequiredComponentsRequest
                {
                    ComponentType = componentType,
                    ObjectId = objectId
                });

                foreach (var dep in response.EntityCollection.Entities)
                {
                    // 兼容 AliasedValue 或直接属性两种返回形式
                    var type = UnwrapToInt(dep.GetAttributeValue<object>("requiredcomponenttype"));
                    var id = UnwrapToGuid(dep.GetAttributeValue<object>("requiredcomponentid"));
                    if (id != Guid.Empty)
                        required.Add((type, id));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠️ 查询组件依赖失败 (componenttype={componentType}, objectid={objectId}): {ex.Message}");
            }
            return required;
        }

        /// <summary>
        /// componenttype 到表名/主键名的映射（发版自检支持的类型）。
        /// </summary>
        private static readonly Dictionary<int, (string EntityName, string PrimaryKey)> ComponentTypeTableMap = new()
        {
            { 1, ("entity", "entityid") },
            { 2, ("attribute", "attributeid") },
            { 61, ("webresource", "webresourceid") },
            { 90, ("plugintype", "plugintypeid") },
            { 91, ("pluginassembly", "pluginassemblyid") },
            { 92, ("sdkmessageprocessingstep", "sdkmessageprocessingstepid") },
            { 10023, ("customapi", "customapiid") },
            { 10024, ("customapirequestparameter", "customapirequestparameterid") },
            { 10025, ("customapiresponseproperty", "customapiresponsepropertyid") },
            { 29, ("savedquery", "savedqueryid") },
            { 26, ("systemform", "systemformid") },
            { 60, ("systemform", "formid") },
        };

        /// <summary>
        /// 检查指定组件在当前环境中是否存在。
        /// 未知类型或查询异常时返回 true（跳过），避免误报缺失。
        /// </summary>
        public bool ComponentExists(int componentType, Guid objectId)
        {
            if (!ComponentTypeTableMap.TryGetValue(componentType, out var map))
            {
                Console.WriteLine($"  ⚠️ 未知组件类型 {componentType}，无法验证存在性，按存在处理");
                return true;
            }

            try
            {
                var query = new QueryExpression(map.EntityName)
                {
                    ColumnSet = new ColumnSet(map.PrimaryKey),
                    Criteria = new FilterExpression
                    {
                        Conditions = { new ConditionExpression(map.PrimaryKey, ConditionOperator.Equal, objectId) }
                    },
                    TopCount = 1
                };
                return _service.RetrieveMultiple(query).Entities.Any();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠️ 无法验证组件存在性 (componenttype={componentType}, objectid={objectId}): {ex.Message}，按存在处理");
                return true;
            }
        }

        /// <summary>
        /// 排除 Active Layer 检测中的元数据噪声属性（时间戳/类型ID 等）。
        /// </summary>
        private static bool IsExcludedActiveLayerProperty(string propName)
        {
            return propName.Equals("displaymask", StringComparison.OrdinalIgnoreCase) ||
                   propName.Equals("createdon", StringComparison.OrdinalIgnoreCase) ||
                   propName.Equals("modifiedon", StringComparison.OrdinalIgnoreCase) ||
                   propName.Equals("attributetypeid", StringComparison.OrdinalIgnoreCase) ||
                   propName.Equals("attributelogicaltypeid", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 通过 msdyn_componentlayer 虚拟实体检测组件是否存在 Active Layer 自定义。
        /// 返回有效变更属性名列表；空列表 = 无 Active Layer 或仅元数据噪声。
        /// 查询/解析异常时抛出自带说明的异常，由调用方决定如何处理。
        /// </summary>
        /// <param name="componentTypeName">msdyn_solutioncomponentname，如 WebResource/Entity/Custom API/Plugin Assembly/App Action</param>
        /// <param name="componentId">组件 ID</param>
        public List<string> CheckActiveLayer(string componentTypeName, Guid componentId)
        {
            List<Entity> layers;
            try
            {
                var layerQuery = new QueryExpression("msdyn_componentlayer")
                {
                    ColumnSet = new ColumnSet("msdyn_componentlayerid", "msdyn_changes"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("msdyn_solutionname", ConditionOperator.Equal, "Active"),
                            new ConditionExpression("msdyn_solutioncomponentname", ConditionOperator.Equal, componentTypeName),
                            // msdyn_componentid 官方文档标注为 String，但实际可能返回 Guid 或 String，统一按字符串传参
                            new ConditionExpression("msdyn_componentid", ConditionOperator.Equal, componentId.ToString())
                        }
                    }
                };
                layers = _service.RetrieveMultiple(layerQuery).Entities.ToList();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"查询 Active Layer 失败 (component={componentTypeName}, id={componentId}): {ex.Message}", ex);
            }

            var changedProperties = new List<string>();
            foreach (var layer in layers)
            {
                var changesJson = layer.GetAttributeValue<string>("msdyn_changes") ?? "";
                if (string.IsNullOrWhiteSpace(changesJson)) continue;

                try
                {
                    using var doc = JsonDocument.Parse(changesJson);
                    if (doc.RootElement.TryGetProperty("Attributes", out var attributes))
                    {
                        if (attributes.ValueKind == JsonValueKind.Array)
                        {
                            // msdyn_changes 中 Attributes 可能是 [{"Key":"...","Value":"..."}, ...] 数组
                            foreach (var item in attributes.EnumerateArray())
                            {
                                if (item.TryGetProperty("Key", out var keyProp) && keyProp.ValueKind == JsonValueKind.String)
                                {
                                    var propName = keyProp.GetString() ?? "";
                                    if (IsExcludedActiveLayerProperty(propName)) continue;
                                    changedProperties.Add(propName);
                                }
                            }
                        }
                        else if (attributes.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var prop in attributes.EnumerateObject())
                            {
                                if (IsExcludedActiveLayerProperty(prop.Name)) continue;
                                changedProperties.Add(prop.Name);
                            }
                        }
                    }
                    else if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in doc.RootElement.EnumerateObject())
                        {
                            if (IsExcludedActiveLayerProperty(prop.Name)) continue;
                            changedProperties.Add(prop.Name);
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"解析 Active Layer Changes JSON 失败 (component={componentTypeName}, id={componentId}): {ex.Message}", ex);
                }
            }

            return changedProperties.Distinct().ToList();
        }

        /// <summary>
        /// 兼容 AliasedValue / OptionSetValue / int / string 等多种返回形式，解包为 int。
        /// </summary>
        private static int UnwrapToInt(object? value)
        {
            return value switch
            {
                null => 0,
                AliasedValue av => UnwrapToInt(av.Value),
                OptionSetValue osv => osv.Value,
                int i => i,
                string s when int.TryParse(s, out var p) => p,
                _ => Convert.ToInt32(value)
            };
        }

        /// <summary>
        /// 兼容 AliasedValue / Guid / string 等多种返回形式，解包为 Guid。
        /// </summary>
        private static Guid UnwrapToGuid(object? value)
        {
            return value switch
            {
                null => Guid.Empty,
                AliasedValue av => UnwrapToGuid(av.Value),
                Guid g => g,
                string s when Guid.TryParse(s, out var p) => p,
                _ => Guid.Empty
            };
        }
    }
}
