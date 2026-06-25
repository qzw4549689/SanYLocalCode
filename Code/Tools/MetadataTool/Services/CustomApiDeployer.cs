using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace D365MetadataTool.Services
{
    /// <summary>
    /// Custom API 部署服务：注册/更新 Custom API、请求参数、响应属性
    /// </summary>
    public class CustomApiDeployer
    {
        private readonly ServiceClient _service;

        public CustomApiDeployer(ServiceClient service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        /// <summary>
        /// 部署成交条件样板库查询 Custom API
        /// </summary>
        public Guid DeployTradeStPayTermQueryApi(string pluginTypeName, string solutionName = "McsCustomAPI")
        {
            Console.WriteLine(">>> 部署 Custom API: mcs_QueryTradeStPayTerm");

            // 1. 查找 Plugin Type
            var pluginTypeId = QueryPluginType(pluginTypeName);
            if (pluginTypeId == Guid.Empty)
            {
                Console.WriteLine($"  ❌ 未找到 Plugin Type: {pluginTypeName}");
                return Guid.Empty;
            }
            Console.WriteLine($"  Plugin Type: {pluginTypeId}");

            // 2. 创建/更新 Custom API
            var apiId = CreateOrUpdateCustomApi(pluginTypeId, solutionName);
            if (apiId == Guid.Empty)
            {
                Console.WriteLine("  ❌ Custom API 创建/更新失败");
                return Guid.Empty;
            }

            // 3. 创建请求参数
            var requestParamIds = new List<Guid>
            {
                CreateRequestParameter(apiId, "mcs_buid", "事业部编码", "String", false, solutionName),
                CreateRequestParameter(apiId, "mcs_subid", "子公司编码", "String", false, solutionName),
                CreateRequestParameter(apiId, "mcs_countrycode", "国家代码", "String", false, solutionName),
                CreateRequestParameter(apiId, "mcs_prdgroupid", "产品线编码", "String", false, solutionName),
                CreateRequestParameter(apiId, "mcs_buyercode", "客户编码", "String", false, solutionName)
            };

            // 4. 创建响应属性
            var responsePropIds = new List<Guid>
            {
                CreateResponseProperty(apiId, "status", "调用标识", "String", solutionName),
                CreateResponseProperty(apiId, "message", "调用结果", "String", solutionName),
                CreateResponseProperty(apiId, "records", "匹配记录集(JSON)", "String", solutionName)
            };

            // 5. 加入解决方案
            AddToSolution(apiId, requestParamIds, responsePropIds, solutionName);

            Console.WriteLine("  ✅ Custom API 部署完成");
            return apiId;
        }

        /// <summary>
        /// 删除指定 Custom API 及其参数、属性
        /// </summary>
        public void DeleteCustomApi(string uniqueName)
        {
            Console.WriteLine($">>> 删除 Custom API: {uniqueName}");

            var query = new QueryExpression("customapi")
            {
                ColumnSet = new ColumnSet("customapiid"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("uniquename", ConditionOperator.Equal, uniqueName) }
                }
            };

            var result = _service.RetrieveMultiple(query);
            if (result.Entities.Count == 0)
            {
                Console.WriteLine("  ⚠️ Custom API 不存在，无需删除");
                return;
            }

            var apiId = result.Entities[0].Id;

            // 删除请求参数
            DeleteChildren("customapirequestparameter", "customapiid", apiId);
            // 删除响应属性
            DeleteChildren("customapiresponseproperty", "customapiid", apiId);
            // 删除 Custom API
            _service.Delete("customapi", apiId);

            Console.WriteLine($"  ✅ Custom API 已删除: {apiId}");
        }

        private Guid QueryPluginType(string typeName)
        {
            var query = new QueryExpression("plugintype")
            {
                ColumnSet = new ColumnSet("plugintypeid"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("typename", ConditionOperator.Equal, typeName) }
                }
            };

            var result = _service.RetrieveMultiple(query).Entities.FirstOrDefault();
            return result?.Id ?? Guid.Empty;
        }

        private Guid CreateOrUpdateCustomApi(Guid pluginTypeId, string solutionName)
        {
            const string uniqueName = "mcs_QueryTradeStPayTerm";

            var query = new QueryExpression("customapi")
            {
                ColumnSet = new ColumnSet("customapiid"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("uniquename", ConditionOperator.Equal, uniqueName) }
                }
            };

            var existing = _service.RetrieveMultiple(query).Entities.FirstOrDefault();

            var api = new Entity("customapi");
            api["uniquename"] = uniqueName;
            api["name"] = uniqueName;
            api["displayname"] = "成交条件样板库查询";
            api["description"] = "根据事业部、子公司、国家、产品线、客户编码查询匹配的成交条件样板库记录";
            api["plugintypeid"] = new EntityReference("plugintype", pluginTypeId);
            api["isfunction"] = false;
            api["allowedcustomprocessingsteptype"] = new OptionSetValue(0); // None
            api["bindingtype"] = new OptionSetValue(0); // Global
            api["boundentitylogicalname"] = null;
            api["executeprivilegename"] = null;
            api["statecode"] = new OptionSetValue(0); // Active
            api["statuscode"] = new OptionSetValue(1); // Active

            if (existing != null)
            {
                api.Id = existing.Id;
                _service.Update(api);
                Console.WriteLine($"  ✅ Custom API 已更新: {existing.Id}");
                return existing.Id;
            }
            else
            {
                var id = CreateWithSolution(api, solutionName);
                Console.WriteLine($"  ✅ Custom API 已创建: {id}");
                return id;
            }
        }

        private Guid CreateRequestParameter(Guid apiId, string uniqueName, string displayName, string typeName, bool isOptional, string solutionName)
        {
            var query = new QueryExpression("customapirequestparameter")
            {
                ColumnSet = new ColumnSet("customapirequestparameterid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("customapiid", ConditionOperator.Equal, apiId),
                        new ConditionExpression("uniquename", ConditionOperator.Equal, uniqueName)
                    }
                }
            };

            var existing = _service.RetrieveMultiple(query).Entities.FirstOrDefault();

            var param = new Entity("customapirequestparameter");
            param["customapiid"] = new EntityReference("customapi", apiId);
            param["uniquename"] = uniqueName;
            param["name"] = uniqueName;
            param["displayname"] = displayName;
            param["description"] = displayName;
            param["type"] = new OptionSetValue(GetOptionSetTypeValue(typeName));
            param["isoptional"] = isOptional;
            param["statecode"] = new OptionSetValue(0);
            param["statuscode"] = new OptionSetValue(1);

            if (existing != null)
            {
                param.Id = existing.Id;
                _service.Update(param);
                Console.WriteLine($"  ✅ 请求参数已更新: {uniqueName}");
                return existing.Id;
            }
            else
            {
                var id = CreateWithSolution(param, solutionName);
                Console.WriteLine($"  ✅ 请求参数已创建: {uniqueName} ({id})");
                return id;
            }
        }

        private Guid CreateResponseProperty(Guid apiId, string uniqueName, string displayName, string typeName, string solutionName)
        {
            var query = new QueryExpression("customapiresponseproperty")
            {
                ColumnSet = new ColumnSet("customapiresponsepropertyid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("customapiid", ConditionOperator.Equal, apiId),
                        new ConditionExpression("uniquename", ConditionOperator.Equal, uniqueName)
                    }
                }
            };

            var existing = _service.RetrieveMultiple(query).Entities.FirstOrDefault();

            var prop = new Entity("customapiresponseproperty");
            prop["customapiid"] = new EntityReference("customapi", apiId);
            prop["uniquename"] = uniqueName;
            prop["name"] = uniqueName;
            prop["displayname"] = displayName;
            prop["description"] = displayName;
            prop["type"] = new OptionSetValue(GetOptionSetTypeValue(typeName));
            prop["statecode"] = new OptionSetValue(0);
            prop["statuscode"] = new OptionSetValue(1);

            if (existing != null)
            {
                prop.Id = existing.Id;
                _service.Update(prop);
                Console.WriteLine($"  ✅ 响应属性已更新: {uniqueName}");
                return existing.Id;
            }
            else
            {
                var id = CreateWithSolution(prop, solutionName);
                Console.WriteLine($"  ✅ 响应属性已创建: {uniqueName} ({id})");
                return id;
            }
        }

        private void DeleteChildren(string entityName, string parentField, Guid parentId)
        {
            var query = new QueryExpression(entityName)
            {
                ColumnSet = new ColumnSet(entityName + "id"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression(parentField, ConditionOperator.Equal, parentId) }
                }
            };

            var children = _service.RetrieveMultiple(query).Entities;
            foreach (var child in children)
            {
                _service.Delete(entityName, child.Id);
            }

            if (children.Count > 0)
            {
                Console.WriteLine($"  已删除 {children.Count} 条 {entityName}");
            }
        }

        private Guid CreateWithSolution(Entity entity, string solutionName)
        {
            var request = new CreateRequest { Target = entity };
            if (!string.IsNullOrWhiteSpace(solutionName))
            {
                request.Parameters["SolutionUniqueName"] = solutionName;
            }
            var response = (CreateResponse)_service.Execute(request);
            return response.id;
        }

        private void AddToSolution(Guid apiId, List<Guid> requestParamIds, List<Guid> responsePropIds, string solutionName)
        {
            try
            {
                var solutionService = new D365ToolCommon.Solution.SolutionComponentService(_service);
                // Custom API=10023, Request Parameter=10024, Response Property=10025
                solutionService.AddComponentToSolution(apiId, 10023, solutionName);
                foreach (var paramId in requestParamIds)
                {
                    solutionService.AddComponentToSolution(paramId, 10024, solutionName);
                }
                foreach (var propId in responsePropIds)
                {
                    solutionService.AddComponentToSolution(propId, 10025, solutionName);
                }
                Console.WriteLine($"  ✅ Custom API 已加入解决方案: {solutionName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠️ 加入解决方案失败: {ex.Message}");
            }
        }

        private int GetOptionSetTypeValue(string typeName)
        {
            // Custom API 参数/属性类型 OptionSet 值
            // 参考: https://learn.microsoft.com/en-us/power-apps/developer/data-platform/custom-api
            return typeName.ToLower() switch
            {
                "string" => 10,
                "boolean" => 0,
                "dateTime" => 1,
                "decimal" => 2,
                "entity" => 3,
                "entityCollection" => 4,
                "entityReference" => 5,
                "float" => 6,
                "integer" => 7,
                "money" => 8,
                "picklist" => 9,
                "guid" => 12,
                "memo" => 14,
                _ => 10 // 默认 String
            };
        }
    }
}
