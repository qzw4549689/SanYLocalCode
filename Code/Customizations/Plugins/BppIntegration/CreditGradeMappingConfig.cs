using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SanyD365.Plugins.BppIntegration
{
    /// <summary>
    /// 信用等级映射配置（禅道 #2091）
    /// 从 ms_systemconfiguration 读取，配置名 CreditGradeMapping，JSON 示例：
    /// {"A0":70,"A1":58,"A2":49,"A3":40,"A4":0}
    /// 语义：信用分 >= 阈值即命中该等级（下限含，按阈值从高到低匹配首个命中档，双闭区间）。
    /// 配置不存在/内容为空/解析失败时返回内置默认值（新口径 70/58/49/40/0），不阻塞流程。
    /// </summary>
    public class CreditGradeMappingConfig
    {
        /// <summary>A0 档下限（默认 70）</summary>
        [JsonPropertyName("A0")]
        public decimal A0 { get; set; } = 70m;

        /// <summary>A1 档下限（默认 58）</summary>
        [JsonPropertyName("A1")]
        public decimal A1 { get; set; } = 58m;

        /// <summary>A2 档下限（默认 49）</summary>
        [JsonPropertyName("A2")]
        public decimal A2 { get; set; } = 49m;

        /// <summary>A3 档下限（默认 40）</summary>
        [JsonPropertyName("A3")]
        public decimal A3 { get; set; } = 40m;

        /// <summary>A4 档下限（默认 0）</summary>
        [JsonPropertyName("A4")]
        public decimal A4 { get; set; } = 0m;
    }

    /// <summary>
    /// 信用等级映射配置读取帮助类（禅道 #2091，模式同 SinosureUpliftConfigHelper）
    /// </summary>
    public static class CreditGradeMappingHelper
    {
        public const string ConfigName = "CreditGradeMapping";
        public const string ConfigEntityName = "ms_systemconfiguration";
        public const string ConfigNameField = "ms_name";
        public const string ConfigContentField = "ms_content";

        /// <summary>
        /// 读取信用等级映射配置；配置缺失/解析失败时返回内置新口径默认值，不阻塞流程
        /// </summary>
        public static CreditGradeMappingConfig GetConfig(IOrganizationService service, ITracingService tracer)
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));

            try
            {
                var query = new QueryExpression(ConfigEntityName)
                {
                    ColumnSet = new ColumnSet(ConfigContentField)
                };
                query.Criteria.AddCondition(ConfigNameField, ConditionOperator.Equal, ConfigName);

                var entity = service.RetrieveMultiple(query).Entities.FirstOrDefault();
                if (entity == null)
                {
                    tracer?.Trace($"系统配置 {ConfigName} 不存在，使用内置默认值（A0>=70/A1>=58/A2>=49/A3>=40/A4）");
                    return new CreditGradeMappingConfig();
                }

                var json = entity.GetAttributeValue<string>(ConfigContentField);
                if (string.IsNullOrWhiteSpace(json))
                {
                    tracer?.Trace($"系统配置 {ConfigName} 内容为空，使用内置默认值");
                    return new CreditGradeMappingConfig();
                }

                return JsonSerializer.Deserialize<CreditGradeMappingConfig>(json) ?? new CreditGradeMappingConfig();
            }
            catch (JsonException ex)
            {
                tracer?.Trace($"⚠️ 系统配置 {ConfigName} JSON 格式错误，使用内置默认值: {ex.Message}");
                return new CreditGradeMappingConfig();
            }
        }

        /// <summary>
        /// 按映射计算信用等级：信用分 >= 阈值即命中（下限含），按阈值从高到低匹配首个命中档
        /// </summary>
        public static string CalculateGrade(CreditGradeMappingConfig config, decimal? score)
        {
            if (!score.HasValue) return "";
            if (config == null)
            {
                config = new CreditGradeMappingConfig();
            }

            decimal s = score.Value;
            var thresholds = new List<KeyValuePair<string, decimal>>
            {
                new KeyValuePair<string, decimal>("A0", config.A0),
                new KeyValuePair<string, decimal>("A1", config.A1),
                new KeyValuePair<string, decimal>("A2", config.A2),
                new KeyValuePair<string, decimal>("A3", config.A3),
                new KeyValuePair<string, decimal>("A4", config.A4)
            };

            foreach (var kv in thresholds.OrderByDescending(kv => kv.Value))
            {
                if (s >= kv.Value) return kv.Key;
            }
            return "A4";
        }
    }
}
