using Microsoft.Crm.Sdk.Messages;
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
        /// 部署厂端授信余额调整 Custom API
        /// </summary>
        public Guid DeployFcaQuotaAdjustApi(string pluginTypeName, string solutionName = "McsCustomAPI")
        {
            const string uniqueName = "mcs_AdjustFcaQuotaBalance";
            Console.WriteLine($">>> 部署 Custom API: {uniqueName}");

            // 1. 查找 Plugin Type
            var pluginTypeId = QueryPluginType(pluginTypeName);
            if (pluginTypeId == Guid.Empty)
            {
                Console.WriteLine($"  ❌ 未找到 Plugin Type: {pluginTypeName}");
                return Guid.Empty;
            }
            Console.WriteLine($"  Plugin Type: {pluginTypeId}");

            // 2. 创建/更新 Custom API
            var apiId = CreateOrUpdateCustomApi(pluginTypeId, solutionName, uniqueName,
                "厂端授信余额调整",
                "厂端授信余额统一调整接口：初始化/占用/释放厂端授信额度，并写入台账（供合同评审、订单发货/取消/退货、回款解款等环节调用）");
            if (apiId == Guid.Empty)
            {
                Console.WriteLine("  ❌ Custom API 创建/更新失败");
                return Guid.Empty;
            }

            // 3. 创建请求参数
            var requestParamIds = new List<Guid>
            {
                CreateRequestParameter(apiId, "mcs_accountid", "客户编码（SAP客户代码）", "String", false, solutionName),
                CreateRequestParameter(apiId, "mcs_usebalance", "调整厂端授信金额USD", "Decimal", false, solutionName),
                CreateRequestParameter(apiId, "mcs_proccess", "流程环节（1-11）", "String", false, solutionName),
                CreateRequestParameter(apiId, "mcs_adjust", "额度调整动作（1初始化/3占用/4释放）", "String", false, solutionName),
                CreateRequestParameter(apiId, "mcs_contractid", "合同编码", "String", true, solutionName),
                CreateRequestParameter(apiId, "mcs_orderid", "订单编码", "String", true, solutionName)
            };

            // 4. 创建响应属性
            var responsePropIds = new List<Guid>
            {
                CreateResponseProperty(apiId, "mcs_accountid", "客户编码", "String", solutionName),
                CreateResponseProperty(apiId, "mcs_usebalance", "调整厂端授信金额USD", "Decimal", solutionName),
                CreateResponseProperty(apiId, "mcs_proccess", "流程环节", "String", solutionName),
                CreateResponseProperty(apiId, "mcs_adjust", "额度调整动作", "String", solutionName),
                CreateResponseProperty(apiId, "mcs_contractid", "合同编码", "String", solutionName),
                CreateResponseProperty(apiId, "mcs_orderid", "订单编码", "String", solutionName),
                CreateResponseProperty(apiId, "mcs_usedbalance", "实际调整厂端授信余额USD", "Decimal", solutionName),
                CreateResponseProperty(apiId, "mcs_sellerbalance", "调整后厂端授信余额USD", "Decimal", solutionName),
                CreateResponseProperty(apiId, "mcs_usedflag", "是否调整成功（1是/0否）", "String", solutionName),
                CreateResponseProperty(apiId, "mcs_recordid", "台账编号", "String", solutionName),
                CreateResponseProperty(apiId, "mcs_failreason", "使用失败原因", "String", solutionName)
            };

            // 5. 加入解决方案
            AddToSolution(apiId, requestParamIds, responsePropIds, solutionName);

            Console.WriteLine("  ✅ Custom API 部署完成");
            return apiId;
        }

        /// <summary>
        /// 删除指定 Custom Action（流程/操作）
        /// 仅删除 category=1 的 workflow，避免误删 Custom API 或工作流
        /// </summary>
        public void DeleteCustomAction(string uniqueName)
        {
            Console.WriteLine($">>> 删除 Custom Action: {uniqueName}");

            var query = new QueryExpression("workflow")
            {
                ColumnSet = new ColumnSet("workflowid", "name", "uniquename", "category", "type", "statecode", "statuscode", "sdkmessageid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("name", ConditionOperator.Equal, "Coface Search Company")
                    }
                }
            };

            var result = _service.RetrieveMultiple(query);

            // 同时查询 sdkmessage，确认是否有重复 message
            var msgQuery = new QueryExpression("sdkmessage")
            {
                ColumnSet = new ColumnSet("sdkmessageid", "name"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("name", ConditionOperator.Equal, uniqueName) }
                }
            };
            var msgResult = _service.RetrieveMultiple(msgQuery);
            Console.WriteLine($"  查询 sdkmessage: 找到 {msgResult.Entities.Count} 条 name='{uniqueName}' 的记录");
            foreach (var msg in msgResult.Entities)
            {
                var msgId = msg.Id;
                Console.WriteLine($"    - sdkmessageid={msgId}");

                // 查询关联的 Custom API
                var apiQuery2 = new QueryExpression("customapi")
                {
                    ColumnSet = new ColumnSet("customapiid", "name"),
                    Criteria = new FilterExpression
                    {
                        Conditions = { new ConditionExpression("sdkmessageid", ConditionOperator.Equal, msgId) }
                    }
                };
                var apiResult2 = _service.RetrieveMultiple(apiQuery2);
                if (apiResult2.Entities.Count > 0)
                {
                    Console.WriteLine($"      关联 Custom API: {apiResult2.Entities[0].GetAttributeValue<string>("name")} ({apiResult2.Entities[0].Id})");
                }

                // 查询关联的 workflow
                var wfQuery2 = new QueryExpression("workflow")
                {
                    ColumnSet = new ColumnSet("workflowid", "name", "category"),
                    Criteria = new FilterExpression
                    {
                        Conditions = { new ConditionExpression("sdkmessageid", ConditionOperator.Equal, msgId) }
                    }
                };
                var wfResult2 = _service.RetrieveMultiple(wfQuery2);
                foreach (var wf in wfResult2.Entities)
                {
                    var cat = wf.GetAttributeValue<OptionSetValue>("category")?.Value;
                    Console.WriteLine($"      关联 workflow: {wf.GetAttributeValue<string>("name")} (category={cat}, id={wf.Id})");
                }
            }

            if (result.Entities.Count == 0)
            {
                Console.WriteLine("  ⚠️ workflow 不存在，无需删除");
                return;
            }

            // 先删除关联的旧 Plugin Steps
            var oldMsgIds = result.Entities
                .Where(e => e.GetAttributeValue<EntityReference>("sdkmessageid") != null)
                .Select(e => e.GetAttributeValue<EntityReference>("sdkmessageid").Id)
                .Distinct()
                .ToList();

            if (oldMsgIds.Count > 0)
            {
                var stepQuery = new QueryExpression("sdkmessageprocessingstep")
                {
                    ColumnSet = new ColumnSet("sdkmessageprocessingstepid", "name", "sdkmessageid", "stage"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("sdkmessageid", ConditionOperator.In, oldMsgIds.ToArray())
                        }
                    }
                };
                var steps = _service.RetrieveMultiple(stepQuery).Entities;
                foreach (var step in steps)
                {
                    var stage = step.GetAttributeValue<OptionSetValue>("stage")?.Value ?? -1;
                    var stepName = step.GetAttributeValue<string>("name");
                    // Custom API 自动生成的 MainOperation stage Step 不能通过 SDK 删除
                    if (stage == 30)
                    {
                        Console.WriteLine($"  ⏭️ 跳过 Custom API Step: {stepName} (stage=MainOperation, {step.Id})");
                        continue;
                    }
                    try
                    {
                        _service.Delete("sdkmessageprocessingstep", step.Id);
                        Console.WriteLine($"  ✅ Plugin Step 已删除: {stepName} ({step.Id})");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  ❌ Plugin Step 删除失败: {stepName} - {ex.Message}");
                    }
                }
            }

            foreach (var workflow in result.Entities)
            {
                var id = workflow.Id;
                var name = workflow.GetAttributeValue<string>("name");
                var category = workflow.GetAttributeValue<OptionSetValue>("category")?.Value;
                var type = workflow.GetAttributeValue<OptionSetValue>("type")?.Value;
                var stateCode = workflow.GetAttributeValue<OptionSetValue>("statecode")?.Value ?? -1;
                var statusCode = workflow.GetAttributeValue<OptionSetValue>("statuscode")?.Value ?? -1;
                Console.WriteLine($"  找到旧 Dialog/流程: {name} (category={category}, type={type}, state={stateCode}, status={statusCode}, id={id})");
            }

            Console.WriteLine("  ⚠️ 旧的 Dialog/流程无法通过 SDK 自动删除，请按以下步骤在 D365 UI 中手动删除:");
            Console.WriteLine("     1. 进入 D365 后台: https://dev1.crm5.dynamics.com/tools/Solution/home_solution.aspx?etc=7100");
            Console.WriteLine("     2. 打开包含 'Coface Search Company' 流程的解决方案");
            Console.WriteLine("     3. 找到 category=3 的旧 Dialog 'Coface Search Company'");
            Console.WriteLine("     4. 先停用/关闭 activation 记录，再删除 definition 记录");
            Console.WriteLine("     5. 删除后，Custom API 'mcs_CofaceSearchCompany' 即可正常调用");
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

        /// <summary>
        /// 绑定 Custom API 到指定 Plugin Type
        /// </summary>
        public void BindPluginType(string customApiUniqueName, string pluginTypeName)
        {
            Console.WriteLine($">>> 绑定 Custom API: {customApiUniqueName} -> Plugin Type: {pluginTypeName}");

            // 1. 查询 Custom API
            var apiQuery = new QueryExpression("customapi")
            {
                ColumnSet = new ColumnSet("customapiid", "uniquename", "name"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("uniquename", ConditionOperator.Equal, customApiUniqueName) }
                }
            };

            var apiResult = _service.RetrieveMultiple(apiQuery);
            if (apiResult.Entities.Count == 0)
            {
                Console.WriteLine($"  ❌ Custom API {customApiUniqueName} 不存在");
                return;
            }

            var apiId = apiResult.Entities[0].Id;

            // 2. 查询 Plugin Type
            var pluginTypeId = QueryPluginType(pluginTypeName);
            if (pluginTypeId == Guid.Empty)
            {
                Console.WriteLine($"  ❌ Plugin Type {pluginTypeName} 不存在，请先注册 Plugin Assembly");
                return;
            }

            // 3. 更新 Custom API 的 Plugin Type
            var api = new Entity("customapi", apiId);
            api["plugintypeid"] = new EntityReference("plugintype", pluginTypeId);
            _service.Update(api);

            Console.WriteLine($"  ✅ Custom API {customApiUniqueName} 已绑定 Plugin Type: {pluginTypeName} ({pluginTypeId})");
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

        private Guid CreateOrUpdateCustomApi(Guid pluginTypeId, string solutionName,
            string uniqueName = "mcs_QueryTradeStPayTerm",
            string displayName = "成交条件样板库查询",
            string description = "根据事业部、子公司、国家、产品线、客户编码查询匹配的成交条件样板库记录")
        {
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
            api["displayname"] = displayName;
            api["description"] = description;
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
