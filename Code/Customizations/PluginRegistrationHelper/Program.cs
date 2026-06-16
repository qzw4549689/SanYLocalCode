using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Tooling.Connector;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PluginRegistrationHelper
{
    class StepConfig
    {
        public string PluginTypeName { get; set; }
        public string MessageName { get; set; }
        public string PrimaryEntity { get; set; }
        public Stage Stage { get; set; }
        public string FilteringAttributes { get; set; }
        public int Rank { get; set; } = 1;
    }

    enum Stage { PreValidation = 10, PreOperation = 20, PostOperation = 40 }

    class Program
    {
        // ================== 配置区：请根据实际情况修改 ==================
        private const string Dev1Url = "https://dev1.crm5.dynamics.com";
        private const string DllPath = @"C:\Users\Peter\source\repos\D365\D365\SanyD365.D365Extension.Sales\bin\Debug\SanyD365.D365Extension.Sales.dll";
        
        // 认证方式1：OAuth (推荐，如果启用了 MFA)
        // private const string ConnectionString = $"AuthType=OAuth;Url={Dev1Url};Username=你的邮箱;Password=你的密码;AppId=51f81489-12ee-4a9e-aaae-a2591f45987d;RedirectUri=app://58145B91-0C36-4500-8554-080854F2AC97;LoginPrompt=Auto";
        
        // 认证方式2：Office365 (较旧环境)
        // private const string ConnectionString = $"AuthType=Office365;Url={Dev1Url};Username=你的邮箱;Password=你的密码";
        
        // 认证方式3：ClientSecret (Service Principal，推荐用于自动化)
        // private const string ConnectionString = $"AuthType=ClientSecret;Url={Dev1Url};ClientId=你的ClientId;ClientSecret=你的ClientSecret";

        // 当前使用的连接字符串
        private const string ConnectionString = "请修改此处"; // <-- 修改这里

        static void Main(string[] args)
        {
            if (ConnectionString.Contains("请修改此处"))
            {
                Console.WriteLine("错误：请先修改 Program.cs 中的 ConnectionString！");
                Console.ReadKey();
                return;
            }

            Console.WriteLine($"正在连接 {Dev1Url} ...");
            var svc = new CrmServiceClient(ConnectionString);
            if (!svc.IsReady)
            {
                Console.WriteLine($"连接失败: {svc.LastCrmError}");
                Console.ReadKey();
                return;
            }
            Console.WriteLine("连接成功！");

            if (!File.Exists(DllPath))
            {
                Console.WriteLine($"错误：找不到 DLL 文件: {DllPath}");
                Console.ReadKey();
                return;
            }

            var dllBytes = File.ReadAllBytes(DllPath);
            var assemblyContent = Convert.ToBase64String(dllBytes);
            Console.WriteLine($"DLL 大小: {dllBytes.Length} bytes");

            // 1. 注册/更新 Assembly
            var assemblyName = "SanyD365.D365Extension.Sales";
            var assemblyId = RegisterAssembly(svc, assemblyName, assemblyContent);
            if (assemblyId == Guid.Empty) return;

            // 2. 注册 Plugin Type
            var pluginTypes = new[]
            {
                "SanyD365.D365Extension.Sales.Plugins.Account.AccountCreditValidationPlugin",
                "SanyD365.D365Extension.Sales.Plugins.CofaceIntegration.CofaceIntegrationDataSyncPlugin",
                "SanyD365.D365Extension.Sales.Plugins.CreditRecord.CreditRecordAutoNumberPlugin",
                "SanyD365.D365Extension.Sales.Plugins.CreditRecord.CreditRecordBppCallbackPlugin",
                "SanyD365.D365Extension.Sales.Plugins.CreditRecord.CreditRecordBppIntegrationPlugin",
                "SanyD365.D365Extension.Sales.Plugins.CreditScore.CreditScoreBpfStageSyncPlugin",
                "SanyD365.D365Extension.Sales.Plugins.CreditScore.CreditScoreCalculationPlugin",
                "SanyD365.D365Extension.Sales.Plugins.CreditItems.CreditItemsValidationPlugin",
                "SanyD365.D365Extension.Sales.Plugins.CreditItemValue.CreditItemValueValidationPlugin",
                "SanyD365.D365Extension.Sales.Plugins.CustomerTag.CustomerTagInitPlugin",
                "SanyD365.D365Extension.Sales.Plugins.CustomerTag.CustomerTagValidationPlugin",
                "SanyD365.D365Extension.Sales.Plugins.ScoringCard.ScoringCardAutoNumberPlugin",
            };

            var typeIds = RegisterPluginTypes(svc, assemblyId, pluginTypes);

            // 3. 注册 Steps
            var steps = GetStepConfigs();
            RegisterSteps(svc, typeIds, steps);

            Console.WriteLine("\n全部完成！按任意键退出。");
            Console.ReadKey();
        }

        static Guid RegisterAssembly(IOrganizationService svc, string name, string content)
        {
            var query = new QueryExpression("pluginassembly");
            query.Criteria.AddCondition("name", ConditionOperator.Equal, name);
            query.ColumnSet = new ColumnSet("pluginassemblyid");
            var existing = svc.RetrieveMultiple(query).Entities.FirstOrDefault();

            var assembly = new Entity("pluginassembly");
            assembly["name"] = name;
            assembly["content"] = content;
            assembly["sourcetype"] = new OptionSetValue(0); // Database
            assembly["isolationmode"] = new OptionSetValue(2); // Sandbox

            if (existing != null)
            {
                assembly.Id = existing.Id;
                svc.Update(assembly);
                Console.WriteLine($"[更新] Assembly: {name} ({existing.Id})");
                return existing.Id;
            }
            else
            {
                var id = svc.Create(assembly);
                Console.WriteLine($"[新建] Assembly: {name} ({id})");
                return id;
            }
        }

        static Dictionary<string, Guid> RegisterPluginTypes(IOrganizationService svc, Guid assemblyId, string[] typeNames)
        {
            var result = new Dictionary<string, Guid>();
            
            foreach (var typeName in typeNames)
            {
                var shortName = typeName.Split('.').Last();
                var query = new QueryExpression("plugintype");
                query.Criteria.AddCondition("typename", ConditionOperator.Equal, typeName);
                query.ColumnSet = new ColumnSet("plugintypeid");
                var existing = svc.RetrieveMultiple(query).Entities.FirstOrDefault();

                var pluginType = new Entity("plugintype");
                pluginType["pluginassemblyid"] = new EntityReference("pluginassembly", assemblyId);
                pluginType["typename"] = typeName;
                pluginType["friendlyname"] = shortName;
                pluginType["name"] = shortName;

                if (existing != null)
                {
                    pluginType.Id = existing.Id;
                    svc.Update(pluginType);
                    Console.WriteLine($"[更新] PluginType: {shortName}");
                    result[typeName] = existing.Id;
                }
                else
                {
                    var id = svc.Create(pluginType);
                    Console.WriteLine($"[新建] PluginType: {shortName}");
                    result[typeName] = id;
                }
            }

            return result;
        }

        static List<StepConfig> GetStepConfigs()
        {
            return new List<StepConfig>
            {
                // Account
                new StepConfig { PluginTypeName = "AccountCreditValidationPlugin", MessageName = "Create", PrimaryEntity = "account", Stage = Stage.PreValidation },
                new StepConfig { PluginTypeName = "AccountCreditValidationPlugin", MessageName = "Update", PrimaryEntity = "account", Stage = Stage.PreValidation },
                
                // CreditRecord
                new StepConfig { PluginTypeName = "CreditRecordAutoNumberPlugin", MessageName = "Create", PrimaryEntity = "mcs_credit_record", Stage = Stage.PreOperation },
                new StepConfig { PluginTypeName = "CreditRecordBppIntegrationPlugin", MessageName = "Update", PrimaryEntity = "mcs_credit_record", Stage = Stage.PostOperation, FilteringAttributes = "mcs_status" },
                new StepConfig { PluginTypeName = "CreditRecordBppCallbackPlugin", MessageName = "Update", PrimaryEntity = "mcs_credit_record", Stage = Stage.PostOperation, FilteringAttributes = "mcs_bppstatus" },
                new StepConfig { PluginTypeName = "CreditScoreCalculationPlugin", MessageName = "Update", PrimaryEntity = "mcs_credit_record", Stage = Stage.PostOperation, FilteringAttributes = "mcs_status" },
                new StepConfig { PluginTypeName = "CreditScoreBpfStageSyncPlugin", MessageName = "Update", PrimaryEntity = "mcs_credit_record", Stage = Stage.PostOperation, FilteringAttributes = "mcs_status" },
                new StepConfig { PluginTypeName = "CofaceIntegrationDataSyncPlugin", MessageName = "Update", PrimaryEntity = "mcs_credit_record", Stage = Stage.PostOperation, FilteringAttributes = "mcs_status" },
                
                // CreditItems
                new StepConfig { PluginTypeName = "CreditItemsValidationPlugin", MessageName = "Create", PrimaryEntity = "mcs_credit_items", Stage = Stage.PreValidation },
                new StepConfig { PluginTypeName = "CreditItemsValidationPlugin", MessageName = "Update", PrimaryEntity = "mcs_credit_items", Stage = Stage.PreValidation },
                
                // CreditItemValue
                new StepConfig { PluginTypeName = "CreditItemValueValidationPlugin", MessageName = "Create", PrimaryEntity = "mcs_credit_itemvalue", Stage = Stage.PreValidation },
                new StepConfig { PluginTypeName = "CreditItemValueValidationPlugin", MessageName = "Update", PrimaryEntity = "mcs_credit_itemvalue", Stage = Stage.PreValidation },
                
                // CustomerTag
                new StepConfig { PluginTypeName = "CustomerTagInitPlugin", MessageName = "Create", PrimaryEntity = "mcs_customer_tag", Stage = Stage.PostOperation },
                new StepConfig { PluginTypeName = "CustomerTagValidationPlugin", MessageName = "Update", PrimaryEntity = "mcs_customer_tag", Stage = Stage.PreOperation },
                
                // ScoringCard
                new StepConfig { PluginTypeName = "ScoringCardAutoNumberPlugin", MessageName = "Create", PrimaryEntity = "mcs_scoring_card", Stage = Stage.PreOperation },
            };
        }

        static void RegisterSteps(IOrganizationService svc, Dictionary<string, Guid> typeIds, List<StepConfig> steps)
        {
            var messageCache = new Dictionary<string, Guid>();
            var filterCache = new Dictionary<string, Guid>();

            foreach (var step in steps)
            {
                var fullTypeName = typeIds.Keys.FirstOrDefault(k => k.Contains(step.PluginTypeName));
                if (fullTypeName == null || !typeIds.TryGetValue(fullTypeName, out var pluginTypeId))
                {
                    Console.WriteLine($"[跳过] 找不到 PluginType: {step.PluginTypeName}");
                    continue;
                }

                if (!messageCache.TryGetValue(step.MessageName, out var messageId))
                {
                    messageId = GetSdkMessageId(svc, step.MessageName);
                    if (messageId == Guid.Empty)
                    {
                        Console.WriteLine($"[跳过] 找不到 Message: {step.MessageName}");
                        continue;
                    }
                    messageCache[step.MessageName] = messageId;
                }

                var filterKey = $"{step.MessageName}|{step.PrimaryEntity}";
                if (!filterCache.TryGetValue(filterKey, out var filterId))
                {
                    filterId = GetSdkMessageFilterId(svc, messageId, step.PrimaryEntity);
                    filterCache[filterKey] = filterId;
                }

                var stepName = $"{step.PluginTypeName}: {step.MessageName} of {step.PrimaryEntity}";
                var query = new QueryExpression("sdkmessageprocessingstep");
                query.Criteria.AddCondition("plugintypeid", ConditionOperator.Equal, pluginTypeId);
                query.Criteria.AddCondition("sdkmessageid", ConditionOperator.Equal, messageId);
                query.Criteria.AddCondition("stage", ConditionOperator.Equal, (int)step.Stage);
                query.Criteria.AddCondition("mode", ConditionOperator.Equal, 0);
                if (filterId != Guid.Empty)
                    query.Criteria.AddCondition("sdkmessagefilterid", ConditionOperator.Equal, filterId);
                query.ColumnSet = new ColumnSet("sdkmessageprocessingstepid", "filteringattributes", "rank");
                var existing = svc.RetrieveMultiple(query).Entities.FirstOrDefault();

                var stepEntity = new Entity("sdkmessageprocessingstep");
                stepEntity["name"] = stepName;
                stepEntity["plugintypeid"] = new EntityReference("plugintype", pluginTypeId);
                stepEntity["sdkmessageid"] = new EntityReference("sdkmessage", messageId);
                stepEntity["stage"] = new OptionSetValue((int)step.Stage);
                stepEntity["mode"] = new OptionSetValue(0);
                stepEntity["rank"] = step.Rank;
                stepEntity["supporteddeployment"] = new OptionSetValue(0); // ServerOnly
                
                if (filterId != Guid.Empty)
                    stepEntity["sdkmessagefilterid"] = new EntityReference("sdkmessagefilter", filterId);
                
                if (!string.IsNullOrEmpty(step.FilteringAttributes))
                    stepEntity["filteringattributes"] = step.FilteringAttributes;

                if (existing != null)
                {
                    stepEntity.Id = existing.Id;
                    svc.Update(stepEntity);
                    Console.WriteLine($"[更新] Step: {stepName}");
                }
                else
                {
                    var id = svc.Create(stepEntity);
                    Console.WriteLine($"[新建] Step: {stepName} ({id})");
                }
            }
        }

        static Guid GetSdkMessageId(IOrganizationService svc, string messageName)
        {
            var query = new QueryExpression("sdkmessage");
            query.Criteria.AddCondition("name", ConditionOperator.Equal, messageName);
            query.ColumnSet = new ColumnSet("sdkmessageid");
            var result = svc.RetrieveMultiple(query).Entities.FirstOrDefault();
            return result?.Id ?? Guid.Empty;
        }

        static Guid GetSdkMessageFilterId(IOrganizationService svc, Guid messageId, string entityLogicalName)
        {
            var query = new QueryExpression("sdkmessagefilter");
            query.Criteria.AddCondition("sdkmessageid", ConditionOperator.Equal, messageId);
            query.Criteria.AddCondition("primaryobjecttypecode", ConditionOperator.Equal, entityLogicalName);
            query.ColumnSet = new ColumnSet("sdkmessagefilterid");
            var result = svc.RetrieveMultiple(query).Entities.FirstOrDefault();
            return result?.Id ?? Guid.Empty;
        }
    }
}
