using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace D365ToolCommon.Plugin
{
    /// <summary>
    /// Plugin Step / Type / Assembly 删除服务。
    /// 注意：禁止使用 ConditionOperator.In 子查询，以避免 D365 查询异常。
    /// </summary>
    public class PluginStepDeletionService
    {
        private readonly ServiceClient _service;
        private readonly PluginQueryService _query;

        public PluginStepDeletionService(ServiceClient service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _query = new PluginQueryService(service);
        }

        /// <summary>
        /// 删除指定 Plugin Type 下的所有 Steps。
        /// </summary>
        public int DeleteStepsByTypeId(Guid pluginTypeId)
        {
            var query = new QueryExpression("sdkmessageprocessingstep")
            {
                ColumnSet = new ColumnSet("sdkmessageprocessingstepid", "name", "stage"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("plugintypeid", ConditionOperator.Equal, pluginTypeId) }
                }
            };

            var steps = _service.RetrieveMultiple(query).Entities;
            foreach (var step in steps)
            {
                _service.Delete("sdkmessageprocessingstep", step.Id);
            }
            return steps.Count;
        }

        /// <summary>
        /// 根据 TypeName 删除对应 Plugin Type 下的所有 Steps。
        /// </summary>
        public int DeleteStepsByTypeName(string pluginTypeName)
        {
            var typeQuery = new QueryExpression("plugintype")
            {
                ColumnSet = new ColumnSet("plugintypeid"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("name", ConditionOperator.Equal, pluginTypeName) }
                }
            };

            var types = _service.RetrieveMultiple(typeQuery).Entities;
            int total = 0;
            foreach (var type in types)
            {
                total += DeleteStepsByTypeId(type.Id);
            }
            return total;
        }

        /// <summary>
        /// 注销整个 Plugin Assembly，包括其所有 Types 和 Steps。
        /// </summary>
        public bool UnregisterAssembly(string assemblyName)
        {
            var assembly = _query.QueryAssembly(assemblyName);
            if (assembly == null)
            {
                Console.WriteLine($"⚠️ 找不到 Assembly: {assemblyName}");
                return false;
            }

            var assemblyId = assembly.Id;

            var types = _query.QueryTypesByAssembly(assemblyId);
            foreach (var type in types)
            {
                var typeId = type.Id;
                DeleteStepsByTypeId(typeId);
                _service.Delete("plugintype", typeId);
                Console.WriteLine($"✅ 已删除 PluginType: {typeId}");
            }

            _service.Delete("pluginassembly", assemblyId);
            Console.WriteLine($"✅ 已删除 Assembly: {assemblyName}");
            return true;
        }
    }
}
