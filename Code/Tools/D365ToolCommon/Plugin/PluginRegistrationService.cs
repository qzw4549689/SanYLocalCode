using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using D365ToolCommon.Plugin.Models;

namespace D365ToolCommon.Plugin
{
    /// <summary>
    /// Plugin 注册/更新/注销通用服务。
    /// 封装 Assembly、PluginType、SdkMessageProcessingStep 的增删改。
    /// </summary>
    public class PluginRegistrationService
    {
        private readonly ServiceClient _service;

        public PluginRegistrationService(ServiceClient service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        /// <summary>
        /// 注册或更新 Plugin Assembly（从 DLL 文件路径读取内容）。
        /// </summary>
        public Guid RegisterOrUpdateAssemblyFromFile(string dllPath, string assemblyName)
        {
            if (!File.Exists(dllPath))
                throw new FileNotFoundException($"Plugin DLL 不存在: {dllPath}");

            var assemblyBytes = File.ReadAllBytes(dllPath);
            var assemblyBase64 = Convert.ToBase64String(assemblyBytes);
            return RegisterOrUpdateAssembly(assemblyName, assemblyBase64);
        }

        /// <summary>
        /// 注册或更新 Plugin Assembly（直接传入 Base64 内容）。
        /// </summary>
        public Guid RegisterOrUpdateAssembly(string assemblyName, string base64Content)
        {
            if (string.IsNullOrWhiteSpace(assemblyName))
                throw new ArgumentException("Assembly 名称不能为空", nameof(assemblyName));

            var existing = QueryAssemblyByName(assemblyName);

            var assembly = new Entity("pluginassembly");
            assembly["name"] = assemblyName;
            assembly["content"] = base64Content;
            assembly["sourcetype"] = new OptionSetValue(0); // Database
            assembly["isolationmode"] = new OptionSetValue(2); // Sandbox

            if (existing != null)
            {
                assembly.Id = existing.Id;
                _service.Update(assembly);
                Console.WriteLine($"  ✅ Plugin Assembly 已更新: {existing.Id}");
                return existing.Id;
            }

            var id = _service.Create(assembly);
            Console.WriteLine($"  ✅ Plugin Assembly 已创建: {id}");
            return id;
        }

        /// <summary>
        /// 注册或更新 Plugin Type。
        /// </summary>
        public Guid RegisterOrUpdatePluginType(Guid assemblyId, string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                throw new ArgumentException("Type 名称不能为空", nameof(typeName));

            var existing = QueryPluginType(assemblyId, typeName);

            var pluginType = new Entity("plugintype");
            pluginType["pluginassemblyid"] = new EntityReference("pluginassembly", assemblyId);
            pluginType["typename"] = typeName;

            var shortName = typeName.Split('.').Last();
            pluginType["friendlyname"] = shortName;
            pluginType["name"] = shortName;

            if (existing != null)
            {
                pluginType.Id = existing.Id;
                _service.Update(pluginType);
                Console.WriteLine($"  ✅ Plugin Type 已更新: {existing.Id}");
                return existing.Id;
            }

            var id = _service.Create(pluginType);
            Console.WriteLine($"  ✅ Plugin Type 已创建: {id}");
            return id;
        }

        /// <summary>
        /// 注册或更新一组 Plugin Steps。
        /// </summary>
        public void RegisterOrUpdateSteps(Guid pluginTypeId, IEnumerable<StepConfig> steps)
        {
            foreach (var step in steps)
            {
                RegisterOrUpdateStep(pluginTypeId, step);
            }
        }

        /// <summary>
        /// 注册或更新单个 Plugin Step。
        /// </summary>
        public Guid RegisterOrUpdateStep(Guid pluginTypeId, StepConfig step)
        {
            var messageId = GetSdkMessageId(step.MessageName);
            if (messageId == Guid.Empty)
                throw new InvalidOperationException($"找不到 SdkMessage: {step.MessageName}");

            var filterId = GetSdkMessageFilterId(messageId, step.PrimaryEntity);

            var stepName = $"{pluginTypeId:D}: {step.MessageName} of {step.PrimaryEntity}";

            var existing = QueryStep(pluginTypeId, messageId, step.Stage, step.Mode, filterId);

            var stepEntity = new Entity("sdkmessageprocessingstep");
            stepEntity["name"] = stepName;
            stepEntity["plugintypeid"] = new EntityReference("plugintype", pluginTypeId);
            stepEntity["sdkmessageid"] = new EntityReference("sdkmessage", messageId);
            stepEntity["stage"] = new OptionSetValue(step.Stage);
            stepEntity["mode"] = new OptionSetValue(step.Mode);
            stepEntity["rank"] = step.Rank;
            stepEntity["supporteddeployment"] = new OptionSetValue(step.SupportedDeployment);

            if (filterId != Guid.Empty)
                stepEntity["sdkmessagefilterid"] = new EntityReference("sdkmessagefilter", filterId);

            if (!string.IsNullOrEmpty(step.FilteringAttributes))
                stepEntity["filteringattributes"] = step.FilteringAttributes;

            if (existing != null)
            {
                stepEntity.Id = existing.Id;
                _service.Update(stepEntity);
                Console.WriteLine($"  ✅ Plugin Step 已更新: {stepName} ({existing.Id})");
                return existing.Id;
            }

            var id = _service.Create(stepEntity);
            Console.WriteLine($"  ✅ Plugin Step 已创建: {stepName} ({id})");
            return id;
        }

        /// <summary>
        /// 根据名称查询 Plugin Assembly。
        /// </summary>
        public Entity? QueryAssemblyByName(string assemblyName)
        {
            var query = new QueryExpression("pluginassembly")
            {
                ColumnSet = new ColumnSet("pluginassemblyid", "name"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("name", ConditionOperator.Equal, assemblyName) }
                }
            };
            return _service.RetrieveMultiple(query).Entities.FirstOrDefault();
        }

        /// <summary>
        /// 根据 Assembly 和 TypeName 查询 Plugin Type。
        /// </summary>
        public Entity? QueryPluginType(Guid assemblyId, string typeName)
        {
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
            return _service.RetrieveMultiple(query).Entities.FirstOrDefault();
        }

        /// <summary>
        /// 查询单个 Step（用于判断是创建还是更新）。
        /// </summary>
        public Entity? QueryStep(Guid pluginTypeId, Guid messageId, int stage, int mode, Guid? filterId)
        {
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

            if (filterId.HasValue && filterId.Value != Guid.Empty)
                query.Criteria.AddCondition("sdkmessagefilterid", ConditionOperator.Equal, filterId.Value);

            return _service.RetrieveMultiple(query).Entities.FirstOrDefault();
        }

        /// <summary>
        /// 查询 SdkMessageId。
        /// </summary>
        public Guid GetSdkMessageId(string messageName)
        {
            var query = new QueryExpression("sdkmessage")
            {
                ColumnSet = new ColumnSet("sdkmessageid"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("name", ConditionOperator.Equal, messageName) }
                }
            };
            var result = _service.RetrieveMultiple(query).Entities.FirstOrDefault();
            return result?.Id ?? Guid.Empty;
        }

        /// <summary>
        /// 查询 SdkMessageFilterId。
        /// </summary>
        public Guid GetSdkMessageFilterId(Guid messageId, string entityLogicalName)
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
            var result = _service.RetrieveMultiple(query).Entities.FirstOrDefault();
            return result?.Id ?? Guid.Empty;
        }

        /// <summary>
        /// 一次性注册 Assembly + Type + Steps。
        /// </summary>
        public void DeployPlugin(string dllPath, string assemblyName, string pluginTypeName, IEnumerable<StepConfig> steps)
        {
            var assemblyId = RegisterOrUpdateAssemblyFromFile(dllPath, assemblyName);
            var pluginTypeId = RegisterOrUpdatePluginType(assemblyId, pluginTypeName);
            RegisterOrUpdateSteps(pluginTypeId, steps);
        }
    }
}
