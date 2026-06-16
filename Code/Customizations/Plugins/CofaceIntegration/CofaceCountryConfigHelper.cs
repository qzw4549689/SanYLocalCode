using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;
using System.Text.Json;

namespace SanyD365.Plugins.CofaceIntegration
{
    /// <summary>
    /// Coface 国家特殊处理配置读取帮助类
    /// </summary>
    public static class CofaceCountryConfigHelper
    {
        public const string ConfigName = "CofaceCountryConfig";
        public const string ConfigEntityName = "ms_systemconfiguration";
        public const string ConfigNameField = "ms_name";
        public const string ConfigContentField = "ms_content";

        /// <summary>
        /// 从 D365 System Configuration 读取 Coface 国家特殊处理配置
        /// </summary>
        public static CofaceCountryConfig GetConfig(IOrganizationService service)
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));

            var query = new QueryExpression(ConfigEntityName)
            {
                ColumnSet = new ColumnSet(ConfigContentField)
            };
            query.Criteria.AddCondition(ConfigNameField, ConditionOperator.Equal, ConfigName);

            var results = service.RetrieveMultiple(query);
            var entity = results.Entities.FirstOrDefault();
            if (entity == null)
            {
                // 配置不存在时返回默认空配置，避免阻塞正常流程
                return new CofaceCountryConfig();
            }

            var json = entity.GetAttributeValue<string>(ConfigContentField);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new CofaceCountryConfig();
            }

            try
            {
                var config = JsonSerializer.Deserialize<CofaceCountryConfig>(json);
                if (config == null)
                {
                    return new CofaceCountryConfig();
                }
                return config;
            }
            catch (JsonException ex)
            {
                throw new InvalidPluginExecutionException($"系统配置 {ConfigName} JSON 格式错误: {ex.Message}");
            }
        }
    }
}
