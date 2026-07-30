using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SanyD365.Plugins.CofaceIntegration
{
    /// <summary>
    /// Coface 汇率配置读取帮助类
    /// 从 D365 transactioncurrency 实体读取标准汇率，并转换为 1 LC => USD 方向返回
    /// </summary>
    public static class CofaceExchangeRateHelper
    {
        // 原自定义汇率实体字段常量保留，便于未来回滚或审计
        public const string LegacyEntityName = "mcs_coface_exchange_rate";
        public const string CurrencyCodeField = "mcs_currencycode";
        public const string CurrencyNameField = "mcs_currencyname";
        public const string RateToUsdField = "mcs_rate_to_usd";
        public const string EffectiveDateField = "mcs_effectivedate";
        public const string IsActiveField = "mcs_isactive";
        public const string RemarkField = "mcs_remark";

        /// <summary>
        /// 获取 1 LC => USD 的汇率
        /// 从 D365 transactioncurrency 读取标准汇率，并取倒数转换方向
        /// </summary>
        /// <param name="service">组织服务</param>
        /// <param name="tracer">跟踪服务</param>
        /// <param name="currencyCode">ISO 货币代码，如 EUR/CNY</param>
        /// <returns>汇率值（1 LC => USD）；未配置或异常时返回 0</returns>
        public static decimal GetRateToUsd(
            IOrganizationService service,
            ITracingService tracer,
            string currencyCode)
        {
            if (string.IsNullOrWhiteSpace(currencyCode))
            {
                tracer.Trace("货币代码为空，保持原值");
                return 0m;
            }

            if (currencyCode.Equals("USD", StringComparison.OrdinalIgnoreCase))
            {
                return 1m;
            }

            if (service == null)
            {
                tracer.Trace("IOrganizationService 为空，无法查询 D365 标准汇率");
                return 0m;
            }

            var normalizedCode = currencyCode.ToUpperInvariant();

            try
            {
                var query = new QueryExpression("transactioncurrency")
                {
                    ColumnSet = new ColumnSet("isocurrencycode", "currencyname", "exchangerate"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("isocurrencycode", ConditionOperator.Equal, normalizedCode)
                        }
                    },
                    TopCount = 1
                };

                var records = service.RetrieveMultiple(query);
                var record = records.Entities.FirstOrDefault();

                if (record == null)
                {
                    tracer.Trace($"D365 标准汇率未配置: {normalizedCode}，保持原值");
                    return 0m;
                }

                var d365Rate = record.GetAttributeValue<decimal>("exchangerate");

                if (d365Rate <= 0m)
                {
                    tracer.Trace($"D365 标准汇率值无效: {normalizedCode}={d365Rate}，保持原值");
                    return 0m;
                }

                // D365 汇率方向：1 USD => LC
                // Coface 需要方向：1 LC => USD
                var rateToUsd = 1m / d365Rate;

                tracer.Trace($"D365 标准汇率: {normalizedCode} => USD, d365Rate={d365Rate}, rateToUsd={rateToUsd}");

                return rateToUsd;
            }
            catch (Exception ex)
            {
                tracer.Trace($"查询 D365 标准汇率异常 [{normalizedCode}]: {ex.Message}");
                return 0m;
            }
        }

        /// <summary>
        /// 加载所有可用的 D365 标准汇率（按货币代码，转换为 1 LC => USD 方向）
        /// </summary>
        public static Dictionary<string, decimal> LoadAllRates(
            IOrganizationService service,
            ITracingService tracer)
        {
            var rates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var query = new QueryExpression("transactioncurrency")
                {
                    ColumnSet = new ColumnSet("isocurrencycode", "exchangerate"),
                    Orders =
                    {
                        new OrderExpression("isocurrencycode", OrderType.Ascending)
                    }
                };

                var records = service.RetrieveMultiple(query);
                foreach (var record in records.Entities)
                {
                    var code = record.GetAttributeValue<string>("isocurrencycode");
                    if (string.IsNullOrWhiteSpace(code) || rates.ContainsKey(code))
                        continue;

                    var d365Rate = record.GetAttributeValue<decimal>("exchangerate");
                    if (d365Rate > 0m)
                    {
                        rates[code.ToUpperInvariant()] = 1m / d365Rate;
                    }
                }

                tracer.Trace($"加载 D365 标准汇率: {rates.Count} 条");
            }
            catch (Exception ex)
            {
                tracer.Trace($"加载 D365 标准汇率异常: {ex.Message}");
            }

            return rates;
        }
    }
}
