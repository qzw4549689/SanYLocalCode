using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SanyD365.Plugins.CreditPool.Api
{
    /// <summary>
    /// 中信保上浮配置（816 授信池）
    /// 从 ms_systemconfiguration 读取，配置名 SinosureUpliftConfig，JSON 示例：
    /// {"factor":1.5,"cap":8000000,"excludedCountries":["IR","KP","CU"]}
    /// 配置不存在时返回默认值（×1.5 封顶 8M，无排除国家），不阻塞流程。
    /// </summary>
    public class SinosureUpliftConfig
    {
        /// <summary>上浮系数（默认 1.5）</summary>
        [JsonPropertyName("factor")]
        public decimal Factor { get; set; } = 1.5m;

        /// <summary>上浮封顶 USD（默认 8,000,000）</summary>
        [JsonPropertyName("cap")]
        public decimal Cap { get; set; } = 8000000m;

        /// <summary>不适用上浮的国家代码清单（客户主数据 mcs_countrycode）</summary>
        [JsonPropertyName("excludedCountries")]
        public List<string> ExcludedCountries { get; set; } = new List<string>();
    }

    /// <summary>
    /// 中信保上浮配置读取帮助类（模式同 CofaceCountryConfigHelper）
    /// </summary>
    public static class SinosureUpliftConfigHelper
    {
        public const string ConfigName = "SinosureUpliftConfig";
        public const string ConfigEntityName = "ms_systemconfiguration";
        public const string ConfigNameField = "ms_name";
        public const string ConfigContentField = "ms_content";

        public static SinosureUpliftConfig GetConfig(IOrganizationService service, ITracingService tracer)
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));

            var query = new QueryExpression(ConfigEntityName)
            {
                ColumnSet = new ColumnSet(ConfigContentField)
            };
            query.Criteria.AddCondition(ConfigNameField, ConditionOperator.Equal, ConfigName);

            var entity = service.RetrieveMultiple(query).Entities.FirstOrDefault();
            if (entity == null)
            {
                tracer?.Trace($"系统配置 {ConfigName} 不存在，使用默认值（×1.5 封顶 8M）");
                return new SinosureUpliftConfig();
            }

            var json = entity.GetAttributeValue<string>(ConfigContentField);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new SinosureUpliftConfig();
            }

            try
            {
                return JsonSerializer.Deserialize<SinosureUpliftConfig>(json) ?? new SinosureUpliftConfig();
            }
            catch (JsonException ex)
            {
                tracer?.Trace($"⚠️ 系统配置 {ConfigName} JSON 格式错误，使用默认值: {ex.Message}");
                return new SinosureUpliftConfig();
            }
        }

        /// <summary>
        /// 计算上浮限额：min(批复限额 × 系数, 封顶)；排除国家不上浮（=批复限额）
        /// </summary>
        public static decimal CalcUpliftLimit(SinosureUpliftConfig config, decimal officialLimit, string countryCode)
        {
            if (config == null)
            {
                config = new SinosureUpliftConfig();
            }

            if (!string.IsNullOrWhiteSpace(countryCode) &&
                config.ExcludedCountries != null &&
                config.ExcludedCountries.Any(c => string.Equals(c, countryCode, StringComparison.OrdinalIgnoreCase)))
            {
                return officialLimit;
            }

            return Math.Min(officialLimit * config.Factor, config.Cap);
        }
    }
}
