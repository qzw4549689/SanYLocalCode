using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;
using System.Text.Json;

namespace SanyD365.Plugins.CofaceIntegration
{
    /// <summary>
    /// Coface 配置读取帮助类
    /// </summary>
    public static class CofaceConfigHelper
    {
        public const string ConfigName = "CofaceApiConfig";
        public const string ConfigEntityName = "ms_systemconfiguration";
        public const string ConfigNameField = "ms_name";
        public const string ConfigContentField = "ms_content";

        /// <summary>
        /// 从 D365 System Configuration 读取 Coface API 配置
        /// </summary>
        public static CofaceApiConfig GetConfig(IOrganizationService service)
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
                throw new InvalidPluginExecutionException($"找不到系统配置: {ConfigName}，请在 {ConfigEntityName} 中创建。");
            }

            var json = entity.GetAttributeValue<string>(ConfigContentField);
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidPluginExecutionException($"系统配置 {ConfigName} 的内容为空。");
            }

            try
            {
                var config = JsonSerializer.Deserialize<CofaceApiConfig>(json);
                if (config == null)
                {
                    throw new InvalidPluginExecutionException($"系统配置 {ConfigName} 反序列化失败。");
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
