using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using D365ToolCommon.Plugin;
using D365ToolCommon.Plugin.Models;
using System;
using System.IO;
using System.Linq;

namespace DeployTool
{
    public class DeployPlugin
    {
        private static readonly string CofaceDllPath = "/Users/peterqiu/Work/AIWorkSpace/SanYi/Code/Customizations/Plugins/CofaceIntegration/bin/Debug/net462/SanyD365.Plugins.CofaceIntegration.dll";
        private static readonly string CofaceAssemblyName = "SanyD365.Plugins.CofaceIntegration";

        public static void DeployCofacePlugin(ServiceClient service)
        {
            Console.WriteLine(">>> 部署 CofaceDataSyncPlugin...");

            var pluginService = new PluginRegistrationService(service);
            pluginService.DeployPlugin(CofaceDllPath, CofaceAssemblyName,
                "SanyD365.Plugins.CofaceIntegration.Plugin.CofaceDataSyncPlugin",
                new[]
                {
                    new StepConfig
                    {
                        MessageName = "Update",
                        PrimaryEntity = "mcs_credit_record",
                        Stage = 40, // PostOperation
                        Mode = 0,   // Synchronous
                        FilteringAttributes = "mcs_status",
                        Rank = 1
                    }
                });

            Console.WriteLine("  ✅ CofaceDataSyncPlugin 部署完成");
        }

        public static void DeployCofaceSearchCompanyPlugin(ServiceClient service)
        {
            Console.WriteLine(">>> 部署 CofaceSearchCompanyPlugin...");

            var pluginService = new PluginRegistrationService(service);
            pluginService.DeployPlugin(CofaceDllPath, CofaceAssemblyName,
                "SanyD365.Plugins.CofaceIntegration.Plugin.CofaceSearchCompanyPlugin",
                new[]
                {
                    new StepConfig
                    {
                        MessageName = "mcs_CofaceSearchCompany",
                        PrimaryEntity = "none",
                        Stage = 40, // PostOperation
                        Mode = 0,   // Synchronous
                        Rank = 1
                    }
                });

            Console.WriteLine("  ✅ CofaceSearchCompanyPlugin 部署完成");
        }

        public static void DeployTradeStPayTermPlugin(ServiceClient service)
        {
            Console.WriteLine(">>> 部署 TradeStPayTerm Plugins...");

            var dllPath = "/tmp/SanyD365.D365Extension.Sales.dll";
            var assemblyName = "SanyD365.D365Extension.Sales";

            if (!File.Exists(dllPath))
            {
                Console.WriteLine("  ❌ Plugin DLL 不存在: /tmp/SanyD365.D365Extension.Sales.dll");
                Console.WriteLine("     请先用 sync-plugin-to-remote.py --pull-dll 拉回远程编译的 DLL。");
                return;
            }

            var pluginService = new PluginRegistrationService(service);

            // 1. 注册/更新 Assembly
            var assemblyId = pluginService.RegisterOrUpdateAssemblyFromFile(dllPath, assemblyName);
            if (assemblyId == Guid.Empty)
            {
                Console.WriteLine("  ❌ Plugin Assembly 注册失败");
                return;
            }

            // 2. 注册/更新 AutoNumber Plugin Type + Step
            var autoNumberTypeId = pluginService.RegisterOrUpdatePluginType(assemblyId,
                "SanyD365.D365Extension.Sales.Plugins.TradeStPayTerm.TradeStPayTermAutoNumberPlugin");
            if (autoNumberTypeId != Guid.Empty)
            {
                RegisterTradeStPayTermStep(service, autoNumberTypeId, "Create", "mcs_trade_stpayterm", 20, 0);
            }

            // 3. 注册/更新 Validation Plugin Type + Steps
            var validationTypeId = pluginService.RegisterOrUpdatePluginType(assemblyId,
                "SanyD365.D365Extension.Sales.Plugins.TradeStPayTerm.TradeStPayTermValidationPlugin");
            if (validationTypeId != Guid.Empty)
            {
                RegisterTradeStPayTermStep(service, validationTypeId, "Create", "mcs_trade_stpayterm", 20, 0);
                var updateStepId = RegisterTradeStPayTermStep(service, validationTypeId, "Update", "mcs_trade_stpayterm", 20, 0);
                if (updateStepId != Guid.Empty)
                {
                    RegisterPreImage(service, updateStepId, "PreImage");
                }
            }

            Console.WriteLine("  ✅ TradeStPayTerm Plugins 部署完成");
        }

        public static void DeployBppPlugin(ServiceClient service)
        {
            Console.WriteLine(">>> 部署 BppIntegrationPlugin...");

            var dllPath = "/Users/peterqiu/Work/AIWorkSpace/SanYi/Code/Customizations/Plugins/BppIntegration/bin/Release/net462/SanyD365.Plugins.BppIntegration.dll";
            var assemblyName = "SanyD365.Plugins.BppIntegration";

            if (!File.Exists(dllPath))
            {
                Console.WriteLine("  ❌ Plugin DLL 不存在");
                return;
            }

            try
            {
                var assemblyBytes = File.ReadAllBytes(dllPath);
                var assemblyBase64 = Convert.ToBase64String(assemblyBytes);

                var query = new QueryExpression("pluginassembly")
                {
                    ColumnSet = new ColumnSet("pluginassemblyid", "name"),
                    Criteria = new FilterExpression
                    {
                        Conditions = { new ConditionExpression("name", ConditionOperator.Equal, assemblyName) }
                    }
                };

                var results = service.RetrieveMultiple(query);

                if (results.Entities.Count > 0)
                {
                    var assembly = results.Entities[0];
                    assembly["content"] = assemblyBase64;
                    service.Update(assembly);
                    Console.WriteLine($"  ✅ BppIntegration Plugin Assembly 已更新: {assembly.Id}");
                }
                else
                {
                    Console.WriteLine("  ❌ 未找到已注册的 BppIntegration Plugin Assembly，需要手动注册");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ BppIntegration Plugin 部署失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 通用 Plugin 注册/更新：Assembly + Type + Steps
        /// </summary>
        private static void RegisterPlugin(
            ServiceClient service,
            string dllPath,
            string assemblyName,
            string pluginTypeName,
            StepConfig[] steps)
        {
            if (!File.Exists(dllPath))
            {
                Console.WriteLine("  ❌ Plugin DLL 不存在");
                return;
            }

            try
            {
                var assemblyBytes = File.ReadAllBytes(dllPath);
                var assemblyBase64 = Convert.ToBase64String(assemblyBytes);

                // 1. 注册/更新 Assembly
                var assemblyId = RegisterOrUpdateAssembly(service, assemblyName, assemblyBase64);
                if (assemblyId == Guid.Empty)
                {
                    Console.WriteLine("  ❌ Plugin Assembly 注册失败");
                    return;
                }

                // 2. 注册/更新 Plugin Type
                var pluginTypeId = RegisterOrUpdatePluginType(service, assemblyId, pluginTypeName);
                if (pluginTypeId == Guid.Empty)
                {
                    Console.WriteLine("  ❌ Plugin Type 注册失败");
                    return;
                }

                // 3. 注册 Steps
                RegisterSteps(service, pluginTypeId, steps);

                Console.WriteLine("  ✅ Plugin 部署完成");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ Plugin 部署失败: {ex.Message}");
            }
        }

        private static Guid RegisterOrUpdateAssembly(ServiceClient service, string name, string content)
        {
            var query = new QueryExpression("pluginassembly")
            {
                ColumnSet = new ColumnSet("pluginassemblyid"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("name", ConditionOperator.Equal, name) }
                }
            };

            var existing = service.RetrieveMultiple(query).Entities.FirstOrDefault();

            var assembly = new Entity("pluginassembly");
            assembly["name"] = name;
            assembly["content"] = content;
            assembly["sourcetype"] = new OptionSetValue(0); // Database
            assembly["isolationmode"] = new OptionSetValue(2); // Sandbox

            if (existing != null)
            {
                assembly.Id = existing.Id;
                service.Update(assembly);
                Console.WriteLine($"  ✅ Plugin Assembly 已更新: {existing.Id}");
                return existing.Id;
            }
            else
            {
                var id = service.Create(assembly);
                Console.WriteLine($"  ✅ Plugin Assembly 已创建: {id}");
                return id;
            }
        }

        private static Guid RegisterOrUpdatePluginType(ServiceClient service, Guid assemblyId, string typeName)
        {
            // 创建 Assembly 后，Dataverse 会自动解析出 Plugin Type，先查询
            var query = new QueryExpression("plugintype")
            {
                ColumnSet = new ColumnSet("plugintypeid", "typename"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("pluginassemblyid", ConditionOperator.Equal, assemblyId),
                        new ConditionExpression("typename", ConditionOperator.Equal, typeName)
                    }
                }
            };

            var existing = service.RetrieveMultiple(query).Entities.FirstOrDefault();

            var pluginType = new Entity("plugintype");
            pluginType["pluginassemblyid"] = new EntityReference("pluginassembly", assemblyId);
            pluginType["typename"] = typeName;

            var shortName = typeName.Split('.').Last();
            pluginType["friendlyname"] = shortName;
            pluginType["name"] = shortName;

            if (existing != null)
            {
                pluginType.Id = existing.Id;
                service.Update(pluginType);
                Console.WriteLine($"  ✅ Plugin Type 已更新: {existing.Id}");
                return existing.Id;
            }
            else
            {
                var id = service.Create(pluginType);
                Console.WriteLine($"  ✅ Plugin Type 已创建: {id}");
                return id;
            }
        }

        private static void RegisterSteps(ServiceClient service, Guid pluginTypeId, StepConfig[] steps)
        {
            foreach (var step in steps)
            {
                try
                {
                    var messageId = GetSdkMessageId(service, step.MessageName);
                    if (messageId == Guid.Empty)
                    {
                        Console.WriteLine($"  ⚠️ 找不到 SdkMessage: {step.MessageName}");
                        continue;
                    }

                    var filterId = GetSdkMessageFilterId(service, messageId, step.PrimaryEntity);

                    var stepName = $"{pluginTypeId:D}: {step.MessageName} of {step.PrimaryEntity}";

                    var query = new QueryExpression("sdkmessageprocessingstep")
                    {
                        ColumnSet = new ColumnSet("sdkmessageprocessingstepid"),
                        Criteria = new FilterExpression
                        {
                            Conditions =
                            {
                                new ConditionExpression("plugintypeid", ConditionOperator.Equal, pluginTypeId),
                                new ConditionExpression("sdkmessageid", ConditionOperator.Equal, messageId),
                                new ConditionExpression("stage", ConditionOperator.Equal, step.Stage),
                                new ConditionExpression("mode", ConditionOperator.Equal, step.Mode)
                            }
                        }
                    };

                    if (filterId != Guid.Empty)
                    {
                        query.Criteria.AddCondition("sdkmessagefilterid", ConditionOperator.Equal, filterId);
                    }

                    var existing = service.RetrieveMultiple(query).Entities.FirstOrDefault();

                    var stepEntity = new Entity("sdkmessageprocessingstep");
                    stepEntity["name"] = stepName;
                    stepEntity["plugintypeid"] = new EntityReference("plugintype", pluginTypeId);
                    stepEntity["sdkmessageid"] = new EntityReference("sdkmessage", messageId);
                    stepEntity["stage"] = new OptionSetValue(step.Stage);
                    stepEntity["mode"] = new OptionSetValue(step.Mode);
                    stepEntity["rank"] = step.Rank;
                    stepEntity["supporteddeployment"] = new OptionSetValue(step.SupportedDeployment); // ServerOnly

                    if (filterId != Guid.Empty)
                        stepEntity["sdkmessagefilterid"] = new EntityReference("sdkmessagefilter", filterId);

                    if (!string.IsNullOrEmpty(step.FilteringAttributes))
                        stepEntity["filteringattributes"] = step.FilteringAttributes;

                    if (existing != null)
                    {
                        stepEntity.Id = existing.Id;
                        service.Update(stepEntity);
                        Console.WriteLine($"  ✅ Plugin Step 已更新: {stepName} ({existing.Id})");
                    }
                    else
                    {
                        var id = service.Create(stepEntity);
                        Console.WriteLine($"  ✅ Plugin Step 已创建: {stepName} ({id})");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ⚠️ 注册 Step [{step.MessageName} of {step.PrimaryEntity}] 失败: {ex.Message}");
                }
            }
        }

        private static Guid GetSdkMessageId(ServiceClient service, string messageName)
        {
            var query = new QueryExpression("sdkmessage")
            {
                ColumnSet = new ColumnSet("sdkmessageid"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("name", ConditionOperator.Equal, messageName) }
                }
            };

            var result = service.RetrieveMultiple(query).Entities.FirstOrDefault();
            return result?.Id ?? Guid.Empty;
        }

        private static Guid GetSdkMessageFilterId(ServiceClient service, Guid messageId, string entityLogicalName)
        {
            var query = new QueryExpression("sdkmessagefilter")
            {
                ColumnSet = new ColumnSet("sdkmessagefilterid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("sdkmessageid", ConditionOperator.Equal, messageId),
                        new ConditionExpression("primaryobjecttypecode", ConditionOperator.Equal, entityLogicalName)
                    }
                }
            };

            var result = service.RetrieveMultiple(query).Entities.FirstOrDefault();
            return result?.Id ?? Guid.Empty;
        }

        private static Guid RegisterTradeStPayTermStep(ServiceClient service, Guid pluginTypeId, string messageName, string entityName, int stage, int mode)
        {
            try
            {
                var messageId = GetSdkMessageId(service, messageName);
                if (messageId == Guid.Empty)
                {
                    Console.WriteLine($"  ⚠️ 找不到 SdkMessage: {messageName}");
                    return Guid.Empty;
                }

                var filterId = GetSdkMessageFilterId(service, messageId, entityName);
                var stepName = $"{pluginTypeId:D}: {messageName} of {entityName}";

                var query = new QueryExpression("sdkmessageprocessingstep")
                {
                    ColumnSet = new ColumnSet("sdkmessageprocessingstepid"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("plugintypeid", ConditionOperator.Equal, pluginTypeId),
                            new ConditionExpression("sdkmessageid", ConditionOperator.Equal, messageId),
                            new ConditionExpression("stage", ConditionOperator.Equal, stage),
                            new ConditionExpression("mode", ConditionOperator.Equal, mode)
                        }
                    }
                };

                if (filterId != Guid.Empty)
                {
                    query.Criteria.AddCondition("sdkmessagefilterid", ConditionOperator.Equal, filterId);
                }

                var existing = service.RetrieveMultiple(query).Entities.FirstOrDefault();

                var stepEntity = new Entity("sdkmessageprocessingstep");
                stepEntity["name"] = stepName;
                stepEntity["plugintypeid"] = new EntityReference("plugintype", pluginTypeId);
                stepEntity["sdkmessageid"] = new EntityReference("sdkmessage", messageId);
                stepEntity["stage"] = new OptionSetValue(stage);
                stepEntity["mode"] = new OptionSetValue(mode);
                stepEntity["rank"] = 1;
                stepEntity["supporteddeployment"] = new OptionSetValue(0); // ServerOnly

                if (filterId != Guid.Empty)
                    stepEntity["sdkmessagefilterid"] = new EntityReference("sdkmessagefilter", filterId);

                if (existing != null)
                {
                    stepEntity.Id = existing.Id;
                    service.Update(stepEntity);
                    Console.WriteLine($"  ✅ Plugin Step 已更新: {stepName} ({existing.Id})");
                    return existing.Id;
                }
                else
                {
                    var id = service.Create(stepEntity);
                    Console.WriteLine($"  ✅ Plugin Step 已创建: {stepName} ({id})");
                    return id;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠️ 注册 Step [{messageName} of {entityName}] 失败: {ex.Message}");
                return Guid.Empty;
            }
        }

        private static void RegisterPreImage(ServiceClient service, Guid stepId, string alias)
        {
            try
            {
                var query = new QueryExpression("sdkmessageprocessingstepimage")
                {
                    ColumnSet = new ColumnSet("sdkmessageprocessingstepimageid"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("sdkmessageprocessingstepid", ConditionOperator.Equal, stepId),
                            new ConditionExpression("entityalias", ConditionOperator.Equal, alias)
                        }
                    }
                };

                var existing = service.RetrieveMultiple(query).Entities.FirstOrDefault();

                var image = new Entity("sdkmessageprocessingstepimage");
                image["name"] = alias;
                image["sdkmessageprocessingstepid"] = new EntityReference("sdkmessageprocessingstep", stepId);
                image["entityalias"] = alias;
                image["imagetype"] = new OptionSetValue(0); // PreImage
                image["messagepropertyname"] = "Target";
                image["attributes"] = "";

                if (existing != null)
                {
                    image.Id = existing.Id;
                    service.Update(image);
                    Console.WriteLine($"  ✅ PreImage 已更新: {alias}");
                }
                else
                {
                    var id = service.Create(image);
                    Console.WriteLine($"  ✅ PreImage 已创建: {alias} ({id})");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠️ 注册 PreImage [{alias}] 失败: {ex.Message}");
            }
        }
    }

}
