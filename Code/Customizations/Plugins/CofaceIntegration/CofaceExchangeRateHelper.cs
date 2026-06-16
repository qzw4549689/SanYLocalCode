using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SanyD365.Plugins.CofaceIntegration
{
    /// <summary>
    /// Coface 汇率配置读取帮助类
    /// 从 mcs_coface_exchange_rate 实体读取年度预算汇率（1 LC => USD）
    /// </summary>
    public static class CofaceExchangeRateHelper
    {
        public const string EntityName = "mcs_coface_exchange_rate";
        public const string CurrencyCodeField = "mcs_currencycode";
        public const string CurrencyNameField = "mcs_currencyname";
        public const string RateToUsdField = "mcs_rate_to_usd";
        public const string EffectiveDateField = "mcs_effectivedate";
        public const string IsActiveField = "mcs_isactive";
        public const string RemarkField = "mcs_remark";

        /// <summary>
        /// 获取 1 LC => USD 的汇率
        /// </summary>
        /// <param name="service">组织服务</param>
        /// <param name="tracer">跟踪服务</param>
        /// <param name="currencyCode">ISO 货币代码，如 EUR/CNY</param>
        /// <returns>汇率值；未配置时返回 0</returns>
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
                tracer.Trace("IOrganizationService 为空，无法查询 Coface 汇率配置");
                return 0m;
            }

            var normalizedCode = currencyCode.ToUpperInvariant();

            try
            {
                var query = new QueryExpression(EntityName)
                {
                    ColumnSet = new ColumnSet(
                        CurrencyCodeField,
                        CurrencyNameField,
                        RateToUsdField,
                        EffectiveDateField,
                        RemarkField),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression(CurrencyCodeField, ConditionOperator.Equal, normalizedCode),
                            new ConditionExpression(IsActiveField, ConditionOperator.Equal, true)
                        }
                    },
                    Orders =
                    {
                        new OrderExpression(EffectiveDateField, OrderType.Descending)
                    },
                    TopCount = 1
                };

                var records = service.RetrieveMultiple(query);
                var record = records.Entities.FirstOrDefault();

                if (record == null)
                {
                    tracer.Trace($"Coface 汇率未配置: {normalizedCode}，保持原值");
                    return 0m;
                }

                var rate = record.GetAttributeValue<decimal>(RateToUsdField);

                if (rate <= 0m)
                {
                    tracer.Trace($"Coface 汇率值无效: {normalizedCode}={rate}，保持原值");
                    return 0m;
                }

                var effectiveDate = record.GetAttributeValue<DateTime?>(EffectiveDateField);
                tracer.Trace($"Coface 汇率: {normalizedCode} => USD, rate={rate}, effectiveDate={effectiveDate:yyyy-MM-dd}, remark={record.GetAttributeValue<string>(RemarkField)}");

                return rate;
            }
            catch (Exception ex)
            {
                tracer.Trace($"查询 Coface 汇率异常 [{normalizedCode}]: {ex.Message}");
                return 0m;
            }
        }

        /// <summary>
        /// 加载所有启用的 Coface 汇率（按货币代码取最新生效日期）
        /// </summary>
        public static Dictionary<string, decimal> LoadAllRates(
            IOrganizationService service,
            ITracingService tracer)
        {
            var rates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var query = new QueryExpression(EntityName)
                {
                    ColumnSet = new ColumnSet(CurrencyCodeField, RateToUsdField, EffectiveDateField),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression(IsActiveField, ConditionOperator.Equal, true)
                        }
                    },
                    Orders =
                    {
                        new OrderExpression(CurrencyCodeField, OrderType.Ascending),
                        new OrderExpression(EffectiveDateField, OrderType.Descending)
                    }
                };

                var records = service.RetrieveMultiple(query);
                foreach (var record in records.Entities)
                {
                    var code = record.GetAttributeValue<string>(CurrencyCodeField);
                    if (string.IsNullOrWhiteSpace(code) || rates.ContainsKey(code))
                        continue;

                    var rate = record.GetAttributeValue<decimal>(RateToUsdField);

                    if (rate > 0m)
                    {
                        rates[code.ToUpperInvariant()] = rate;
                    }
                }

                tracer.Trace($"加载 Coface 汇率配置: {rates.Count} 条");
            }
            catch (Exception ex)
            {
                tracer.Trace($"加载 Coface 汇率配置异常: {ex.Message}");
            }

            return rates;
        }
    }
}
