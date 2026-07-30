using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SanyD365.Plugins.FactoryCredit.Calculation.Services
{
    /// <summary>
    /// 三因子计算服务
    /// </summary>
    public class ThreeFactorCalculationService
    {
        private readonly IOrganizationService _service;
        private readonly ITracingService _tracer;
        private readonly CurrencyConversionHelper _currencyHelper;

        public ThreeFactorCalculationService(IOrganizationService service, ITracingService tracer)
        {
            _service = service;
            _tracer = tracer;
            _currencyHelper = new CurrencyConversionHelper(service, tracer);
        }

        /// <summary>
        /// 三因子计算结果
        /// </summary>
        public class CalculationResult
        {
            public decimal FinancialAbility { get; set; }
            public decimal HistoricalPaymentAbility { get; set; }
            public decimal HistoricalBenchmarkAdjusted { get; set; }
            public decimal InitialGrant { get; set; }
            public bool UsedFallback { get; set; }
            public string FallbackReason { get; set; }
        }

        /// <summary>
        /// 计算三因子并返回初始额度
        /// </summary>
        /// <param name="masterDataId">客户主数据ID</param>
        /// <param name="accountId">Account ID（用于查询客户信用标签和月度在外货款）</param>
        /// <param name="param">模型参数</param>
        public CalculationResult Calculate(Guid masterDataId, Guid accountId, ModelParameterInfo param, DateTime? versionDate = null)
        {
            var netAssetsResult = GetNetAssets(accountId);
            decimal netAssets = netAssetsResult.Value;
            decimal avgMonthlyPayment = GetAverageMonthlyPayment(accountId, versionDate);

            decimal financialAbility = netAssets * param.Adjust1;
            decimal historicalPaymentAbility = avgMonthlyPayment * param.Adjust2;
            decimal historicalBenchmarkAdjusted = param.CountryBenchmark * param.Adjust3;

            _tracer.Trace($"三因子: 财务能力={financialAbility}, 历史回款={historicalPaymentAbility}, 基准调整={historicalBenchmarkAdjusted}");

            decimal initialGrant;
            bool usedFallback = false;
            string fallbackReason = null;

            if (ShouldUseFallback(masterDataId, accountId, netAssetsResult.Found))
            {
                initialGrant = param.CountryBenchmark;
                usedFallback = true;
                fallbackReason = "客户信用分为空/0、信用评估有效状态≠有效，或客户信用标签表记录未匹配到，采用历史基准额度兜底";
                _tracer.Trace(fallbackReason);
            }
            else
            {
                List<decimal> factors = new List<decimal>
                {
                    financialAbility,
                    historicalPaymentAbility,
                    historicalBenchmarkAdjusted
                };

                if (string.Equals(param.AggFuncLabel, "MAX", StringComparison.OrdinalIgnoreCase))
                {
                    initialGrant = factors.Max();
                }
                else
                {
                    initialGrant = factors.Min();
                }

                _tracer.Trace($"聚合方法={param.AggFuncLabel}, 初始额度={initialGrant}");
            }

            return new CalculationResult
            {
                FinancialAbility = financialAbility,
                HistoricalPaymentAbility = historicalPaymentAbility,
                HistoricalBenchmarkAdjusted = historicalBenchmarkAdjusted,
                InitialGrant = initialGrant,
                UsedFallback = usedFallback,
                FallbackReason = fallbackReason
            };
        }

        /// <summary>
        /// 从客户信用标签表获取净资产（USD）
        /// </summary>
        private (bool Found, decimal Value) GetNetAssets(Guid accountId)
        {
            try
            {
                QueryExpression query = new QueryExpression("mcs_customer_tag")
                {
                    ColumnSet = new ColumnSet("mcs_itemintvalue2"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("mcs_accountid", ConditionOperator.Equal, accountId),
                            new ConditionExpression("mcs_itemcode", ConditionOperator.Equal, "NetAssets"),
                            new ConditionExpression("mcs_active", ConditionOperator.Equal, true),
                            // 与客户画像页签取数逻辑保持一致：仅取已评分标签（禅道 #1336）
                            new ConditionExpression("mcs_isscore", ConditionOperator.Equal, true),
                            // 兜底：历史同步数据复核定量值可能为空，跳过空值记录；
                            // 若全部为空则视为未匹配到，走历史基准额度兜底
                            new ConditionExpression("mcs_itemintvalue2", ConditionOperator.NotNull)
                        }
                    },
                    // 与客户画像一致：多条标签取更新日期最新的一条
                    Orders = { new OrderExpression("modifiedon", OrderType.Descending) },
                    TopCount = 1
                };

                EntityCollection result = _service.RetrieveMultiple(query);
                if (result.Entities.Count > 0)
                {
                    decimal value = result.Entities[0].GetAttributeValue<decimal>("mcs_itemintvalue2");
                    _tracer.Trace($"净资产: {value}");
                    return (true, value);
                }
            }
            catch (Exception ex)
            {
                _tracer.Trace($"获取净资产失败: {ex.Message}");
            }

            _tracer.Trace("未找到 NetAssets 标签");
            return (false, 0m);
        }

        /// <summary>
        /// 获取近12个月客户回款月均（USD）
        /// 口径（PRD #1403 复核 2026-07-29）：取 mcs_outstanding【本月实际回款金额】(RMB)，
        /// 按 mcs_createon（源创建时间，PRD【生成版本】YYYYMM 字段在实体上不存在，经用户裁决沿用本字段）取 12 个月范围，
        /// 同一客户同一月份有多条记录时先按月求和作为当月值，再累计 ÷ 12，最后按货币主数据表汇率转 USD。
        /// </summary>
        private decimal GetAverageMonthlyPayment(Guid accountId, DateTime? versionDate)
        {
            try
            {
                DateTime endDate = versionDate ?? DateTime.UtcNow;
                DateTime startDate = endDate.AddMonths(-11).Date;
                // 取版本年月向前 12 个月，例如 20260830 -> 20250901 ~ 20260831
                startDate = new DateTime(startDate.Year, startDate.Month, 1);
                DateTime rangeEnd = new DateTime(endDate.Year, endDate.Month, DateTime.DaysInMonth(endDate.Year, endDate.Month));

                _tracer.Trace($"历史回款查询范围: {startDate:yyyy-MM-dd} ~ {rangeEnd:yyyy-MM-dd}");

                QueryExpression query = new QueryExpression("mcs_outstanding")
                {
                    ColumnSet = new ColumnSet("mcs_paymentcollectioncurrentmonthrmb", "mcs_createon"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("mcs_account", ConditionOperator.Equal, accountId),
                            new ConditionExpression("mcs_createon", ConditionOperator.GreaterEqual, startDate),
                            new ConditionExpression("mcs_createon", ConditionOperator.LessEqual, rangeEnd)
                        }
                    }
                };

                EntityCollection result = _service.RetrieveMultiple(query);
                int count = 0;

                // 同一客户同一月份可能有多条记录：先按月（yyyy-MM）分组求和作为当月的【本月实际回款金额】
                var monthlyTotals = new Dictionary<string, decimal>();
                foreach (Entity entity in result.Entities)
                {
                    decimal amount = entity.GetAttributeValue<decimal>("mcs_paymentcollectioncurrentmonthrmb");
                    DateTime createOn = entity.GetAttributeValue<DateTime>("mcs_createon");
                    string monthKey = createOn.ToString("yyyy-MM");
                    monthlyTotals[monthKey] = (monthlyTotals.TryGetValue(monthKey, out decimal existing) ? existing : 0m) + amount;
                    count++;
                }

                decimal totalRmb = 0m;
                foreach (var month in monthlyTotals.OrderBy(kv => kv.Key))
                {
                    _tracer.Trace($"  {month.Key} 月回款合计(RMB): {month.Value:F2}");
                    totalRmb += month.Value;
                }

                _tracer.Trace($"近12个月回款记录数: {count}, 覆盖月份数: {monthlyTotals.Count}, 累计RMB: {totalRmb}");

                if (count == 0)
                {
                    return 0m;
                }

                decimal avgRmb = totalRmb / 12m;
                return _currencyHelper.ConvertRmbToUsd(avgRmb);
            }
            catch (Exception ex)
            {
                _tracer.Trace($"获取历史回款失败: {ex.Message}");
                return 0m;
            }
        }

        /// <summary>
        /// 判断是否需要使用历史基准额度兜底
        /// </summary>
        private bool ShouldUseFallback(Guid masterDataId, Guid accountId, bool netAssetsFound)
        {
            try
            {
                Entity customer = _service.Retrieve("mcs_customermasterdata", masterDataId,
                    new ColumnSet("mcs_creditscore", "mcs_creditvalid"));

                decimal creditScore = customer.GetAttributeValue<decimal>("mcs_creditscore");
                bool? creditValid = customer.GetAttributeValue<bool?>("mcs_creditvalid");

                _tracer.Trace($"信用分={creditScore}, 有效状态={creditValid}, NetAssets存在={netAssetsFound}");

                return creditScore == 0m || creditValid != true || !netAssetsFound;
            }
            catch (Exception ex)
            {
                _tracer.Trace($"判断兜底规则失败: {ex.Message}");
                return true;
            }
        }
    }
}
