#nullable enable
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using D365ToolCommon.Plugin;
using D365ToolCommon.Plugin.Models;
using System;
using System.Linq;

namespace DeployTool
{
    /// <summary>
    /// 注册 CofaceSearchCompanyPlugin 到已存在的 Custom Action。
    /// Custom Action 需在 D365 UI 中手动创建并激活：mcs_CofaceSearchCompany
    /// </summary>
    public class CofaceCustomActionDeployer
    {
        private readonly ServiceClient _service;
        private readonly PluginRegistrationService _pluginService;

        public CofaceCustomActionDeployer(ServiceClient service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _pluginService = new PluginRegistrationService(service);
        }

        /// <summary>
        /// 主入口：校验 Custom Action 已存在，然后注册 Plugin Step。
        /// </summary>
        public void Deploy()
        {
            Console.WriteLine(">>> 部署 Coface 企业搜索 Custom Action Plugin Step...");

            var actionNames = new[] { "CofaceSearchCompany", "mcs_CofaceSearchCompany" };
            Entity? existing = null;
            foreach (var name in actionNames)
            {
                existing = QueryCustomAction(name);
                if (existing != null) break;
            }

            if (existing == null)
            {
                Console.WriteLine("  ❌ Custom Action mcs_CofaceSearchCompany 不存在，请先在 D365 UI 中创建并激活");
                return;
            }

            Console.WriteLine($"  Custom Action 已存在: {existing.Id} (uniquename={existing.GetAttributeValue<string>("uniquename")})");

            // 注册 Plugin
            RegisterSearchPlugin();

            Console.WriteLine("  ✅ Coface 企业搜索 Plugin Step 部署完成");
        }

        private Entity? QueryCustomAction(string uniqueName)
        {
            var query = new QueryExpression("workflow")
            {
                ColumnSet = new ColumnSet("workflowid", "name", "uniquename", "category", "type", "statecode", "statuscode"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("uniquename", ConditionOperator.Equal, uniqueName) }
                }
            };
            return _service.RetrieveMultiple(query).Entities.FirstOrDefault();
        }

        private void RegisterSearchPlugin()
        {
            // CofaceSearchCompanyPlugin 已归并到远程主项目 SanyD365.D365Extension.Sales
            var dllPath = "/tmp/SanyD365.D365Extension.Sales.dll";
            var assemblyName = "SanyD365.D365Extension.Sales";
            var pluginTypeName = "SanyD365.D365Extension.Sales.Plugins.CofaceIntegration.CofaceSearchCompanyPlugin";

            var assemblyId = _pluginService.RegisterOrUpdateAssemblyFromFile(dllPath, assemblyName);
            var pluginTypeId = _pluginService.RegisterOrUpdatePluginType(assemblyId, pluginTypeName);

            // 注册到 Custom Action 的 SDK Message（全局，无 filter）
            _pluginService.RegisterOrUpdateStep(pluginTypeId, new StepConfig
            {
                MessageName = "mcs_CofaceSearchCompany",
                PrimaryEntity = "none",
                Stage = 40, // PostOperation
                Mode = 0,   // Synchronous
                Rank = 1,
                SupportedDeployment = 0 // ServerOnly
            });
        }
    }
}
