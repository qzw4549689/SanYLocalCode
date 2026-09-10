using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SanyD365.Plugins.CreditPool.Api
{
    /// <summary>
    /// 查询授信结果（816 授信池，客户维度 + 合同维度合并）
    /// </summary>
    public class QueryCreditBalanceResult
    {
        public bool Success { get; set; }
        public string FailReason { get; set; } = string.Empty;

        /// <summary>合同授信金额USD（传了合同编码才查）</summary>
        public decimal CreditLimitUSD { get; set; }
        public decimal CreditLimitCNY { get; set; }

        /// <summary>中信保批复限额USD（mcs_approvedquota 有效记录汇总）</summary>
        public decimal SinosureLimitUSD { get; set; }
        public decimal SinosureLimitCNY { get; set; }
        /// <summary>中信保批复限额余额USD（中信保 T+1 动态余额，仅参考）</summary>
        public decimal SinosureBalanceUSD { get; set; }
        public decimal SinosureBalanceCNY { get; set; }
        /// <summary>中信保批复限额上浮限额USD = min(批复限额×系数, 封顶)，排除国家不上浮</summary>
        public decimal SinosureUpliftLimitUSD { get; set; }
        public decimal SinosureUpliftLimitCNY { get; set; }
        /// <summary>中信保净占用额度USD = 台账 Σ占用 − Σ释放（credit_type=2）</summary>
        public decimal SinosureNetUsedUSD { get; set; }
        public decimal SinosureNetUsedCNY { get; set; }
        /// <summary>中信保批复限额上浮余额USD = 上浮限额 − 净占用</summary>
        public decimal SinosureUpliftBalanceUSD { get; set; }
        public decimal SinosureUpliftBalanceCNY { get; set; }

        /// <summary>客户交易基线额度USD（厂端授信额度 mcs_fca_quota.mcs_sellergrant）</summary>
        public decimal FactoryLimitUSD { get; set; }
        public decimal FactoryLimitCNY { get; set; }
        /// <summary>客户交易基线余额USD（厂端授信余额 mcs_fca_quota.mcs_sellerbalance）</summary>
        public decimal FactoryBalanceUSD { get; set; }
        public decimal FactoryBalanceCNY { get; set; }
        /// <summary>客户风险敞口USD = 厂端授信净占用（mcs_fca_quota.mcs_usedsellerbalance）</summary>
        public decimal RiskExposureUSD { get; set; }
        public decimal RiskExposureCNY { get; set; }
        /// <summary>客户签约占用USD（取数来源待业务确认，暂返回 0）</summary>
        public decimal SigningOccupyUSD { get; set; }
        public decimal SigningOccupyCNY { get; set; }
    }

    /// <summary>
    /// 查询授信服务（816 授信池）
    /// 取数口径：批复限额读 mcs_approvedquota（T+1）；净占用走台账聚合；CNY 按 transactioncurrency 汇率实时换算。
    /// </summary>
    public class QueryCreditBalanceService
    {
        // mcs_approvedquota.mcs_quotastate 选项集值：1-有效
        private const int QUOTA_STATE_VALID = 1;

        private readonly IOrganizationService _service;
        private readonly ITracingService _tracer;

        public QueryCreditBalanceService(IOrganizationService service, ITracingService tracer)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _tracer = tracer ?? throw new ArgumentNullException(nameof(tracer));
        }

        public QueryCreditBalanceResult Query(string customerCode, string contractCode)
        {
            // 1. 解析客户
            var customer = ResolveCustomer(customerCode);
            if (customer == null)
            {
                return Fail($"未找到客户编码[{customerCode}]对应的客户主数据");
            }
            var accountRef = customer.ToEntityReference();
            string countryCode = customer.GetAttributeValue<string>("mcs_countrycode") ?? string.Empty;

            // 2. 汇率（CNY = USD × rate；transactioncurrency 同 CurrencyConversionHelper 口径）
            decimal rate = GetExchangeRate("CNY");
            _tracer.Trace($"CNY 汇率: {rate}");

            var result = new QueryCreditBalanceResult { Success = true };

            // 3. 合同授信金额（传了合同编码才查；字段 mcs_credit_limit_usd/cny 待业务在合同实体落地，未落地前返回 0）
            if (!string.IsNullOrWhiteSpace(contractCode))
            {
                var contract = ResolveContract(contractCode);
                if (contract == null)
                {
                    return Fail($"未找到合同编码[{contractCode}]对应的合同");
                }
                result.CreditLimitUSD = GetContractCreditLimit(contract, "mcs_credit_limit_usd");
                result.CreditLimitCNY = GetContractCreditLimit(contract, "mcs_credit_limit_cny");
                if (result.CreditLimitUSD == 0m && result.CreditLimitCNY == 0m)
                {
                    _tracer.Trace("⚠️ 合同授信金额字段未落地或无值，返回 0（待确认点④）");
                }
            }

            // 4. 中信保：批复限额 / 批复限额余额（T+1） / 上浮限额 / 净占用 / 上浮余额
            decimal officialLimit = 0m;
            decimal officialBalance = 0m;
            GetSinosureQuota(accountRef, out officialLimit, out officialBalance);
            var config = SinosureUpliftConfigHelper.GetConfig(_service, _tracer);
            decimal upliftLimit = SinosureUpliftConfigHelper.CalcUpliftLimit(config, officialLimit, countryCode);
            decimal netUsed = GetSinosureNetOccupied(accountRef);
            decimal upliftBalance = upliftLimit - netUsed;

            result.SinosureLimitUSD = officialLimit;
            result.SinosureBalanceUSD = officialBalance;
            result.SinosureUpliftLimitUSD = upliftLimit;
            result.SinosureNetUsedUSD = netUsed;
            result.SinosureUpliftBalanceUSD = upliftBalance;

            // 5. 厂端：交易基线额度/余额、客户风险敞口（=厂端净占用）
            var quota = GetFactoryQuota(accountRef);
            decimal factoryLimit = quota != null ? GetMoney(quota, "mcs_sellergrant") : 0m;
            decimal factoryBalance = quota != null ? GetMoney(quota, "mcs_sellerbalance") : 0m;
            decimal riskExposure = quota != null ? GetMoney(quota, "mcs_usedsellerbalance") : 0m;
            if (quota == null)
            {
                _tracer.Trace("客户无厂端授信额度记录，厂端相关字段返回 0");
            }

            result.FactoryLimitUSD = factoryLimit;
            result.FactoryBalanceUSD = factoryBalance;
            result.RiskExposureUSD = riskExposure;

            // 6. 客户签约占用：取数来源待业务确认（816 划归合同模块），暂返回 0
            result.SigningOccupyUSD = 0m;
            _tracer.Trace("⚠️ 客户签约占用取数来源待确认，暂返回 0（待确认点⑥）");

            // 7. CNY 实时换算
            result.CreditLimitCNY = result.CreditLimitCNY != 0m ? result.CreditLimitCNY : Convert(result.CreditLimitUSD, rate);
            result.SinosureLimitCNY = Convert(result.SinosureLimitUSD, rate);
            result.SinosureBalanceCNY = Convert(result.SinosureBalanceUSD, rate);
            result.SinosureUpliftLimitCNY = Convert(result.SinosureUpliftLimitUSD, rate);
            result.SinosureNetUsedCNY = Convert(result.SinosureNetUsedUSD, rate);
            result.SinosureUpliftBalanceCNY = Convert(result.SinosureUpliftBalanceUSD, rate);
            result.FactoryLimitCNY = Convert(result.FactoryLimitUSD, rate);
            result.FactoryBalanceCNY = Convert(result.FactoryBalanceUSD, rate);
            result.RiskExposureCNY = Convert(result.RiskExposureUSD, rate);
            result.SigningOccupyCNY = Convert(result.SigningOccupyUSD, rate);

            return result;
        }

        /// <summary>
        /// 中信保批复限额与批复限额余额：mcs_approvedquota 按客户汇总有效记录（quotastate=1 + 生效/失效区间）。
        /// 批复限额=mcs_quotasum（Decimal）求和；批复限额余额=mcs_quotabalance（String，T+1 动态余额）求和。
        /// </summary>
        private void GetSinosureQuota(EntityReference accountRef, out decimal officialLimit, out decimal officialBalance)
        {
            var now = DateTime.UtcNow;
            var query = new QueryExpression("mcs_approvedquota")
            {
                ColumnSet = new ColumnSet("mcs_quotasum", "mcs_quotabalance"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_buyername", ConditionOperator.Equal, accountRef.Id),
                        new ConditionExpression("mcs_quotastate", ConditionOperator.Equal, QUOTA_STATE_VALID)
                    }
                }
            };
            query.Criteria.AddFilter(new FilterExpression(LogicalOperator.Or)
            {
                Conditions =
                {
                    new ConditionExpression("mcs_effectdatestr", ConditionOperator.Null),
                    new ConditionExpression("mcs_effectdatestr", ConditionOperator.LessEqual, now)
                }
            });
            query.Criteria.AddFilter(new FilterExpression(LogicalOperator.Or)
            {
                Conditions =
                {
                    new ConditionExpression("mcs_lapsedatestr", ConditionOperator.Null),
                    new ConditionExpression("mcs_lapsedatestr", ConditionOperator.GreaterEqual, now)
                }
            });

            decimal limit = 0m;
            decimal balance = 0m;
            foreach (var entity in RetrieveAll(query))
            {
                limit += entity.GetAttributeValue<decimal?>("mcs_quotasum") ?? 0m;
                string balanceStr = entity.GetAttributeValue<string>("mcs_quotabalance");
                if (!string.IsNullOrWhiteSpace(balanceStr) && decimal.TryParse(balanceStr, out decimal b))
                {
                    balance += b;
                }
            }
            _tracer.Trace($"信保批复限额汇总: 客户={accountRef.Id}, 批复限额={limit}, 批复限额余额(T+1)={balance}");
            officialLimit = limit;
            officialBalance = balance;
        }

        /// <summary>
        /// 中信保净占用 USD：台账 credit_type=2 的 Σ占用 − Σ释放（与使用授信 API 同口径）
        /// </summary>
        private decimal GetSinosureNetOccupied(EntityReference accountRef)
        {
            var query = new QueryExpression("mcs_fca_records")
            {
                ColumnSet = new ColumnSet("mcs_adjust", "mcs_adjustamt"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_accountid", ConditionOperator.Equal, accountRef.Id),
                        new ConditionExpression("mcs_credit_type", ConditionOperator.Equal, RecordCreditDetailService.CREDIT_SINOSURE),
                        new ConditionExpression("mcs_adjust", ConditionOperator.In, RecordCreditDetailService.ADJUST_OCCUPY, RecordCreditDetailService.ADJUST_RELEASE)
                    }
                }
            };

            decimal occupied = 0m;
            decimal released = 0m;
            foreach (var entity in RetrieveAll(query))
            {
                int adjust = entity.GetAttributeValue<OptionSetValue>("mcs_adjust")?.Value ?? 0;
                decimal amount = GetMoney(entity, "mcs_adjustamt");
                if (adjust == RecordCreditDetailService.ADJUST_OCCUPY) occupied += amount;
                else if (adjust == RecordCreditDetailService.ADJUST_RELEASE) released += amount;
            }
            _tracer.Trace($"信保净占用: 客户={accountRef.Id}, Σ占用={occupied}, Σ释放={released}");
            return occupied - released;
        }

        /// <summary>
        /// 合同授信金额读取：字段不存在（未落地）时按 0 处理，不报错
        /// </summary>
        private decimal GetContractCreditLimit(Entity contract, string fieldName)
        {
            try
            {
                if (contract.Contains(fieldName))
                {
                    var money = contract.GetAttributeValue<Money>(fieldName);
                    if (money != null) return money.Value;
                    var dec = contract.GetAttributeValue<decimal?>(fieldName);
                    if (dec.HasValue) return dec.Value;
                }
            }
            catch (Exception ex)
            {
                _tracer.Trace($"读取合同字段 {fieldName} 失败: {ex.Message}");
            }
            return 0m;
        }

        /// <summary>
        /// 按客户编码（SAP客户代码）解析客户主数据
        /// </summary>
        private Entity ResolveCustomer(string customerCode)
        {
            var query = new QueryExpression("mcs_customermasterdata")
            {
                ColumnSet = new ColumnSet("mcs_name", "mcs_sapnumber", "mcs_countrycode"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_sapnumber", ConditionOperator.Equal, customerCode)
                    }
                },
                TopCount = 1
            };

            var customer = _service.RetrieveMultiple(query).Entities.FirstOrDefault();
            _tracer.Trace($"解析客户: code={customerCode}, found={(customer != null)}");
            return customer;
        }

        /// <summary>
        /// 按合同编码（mcs_name）解析合同
        /// </summary>
        private Entity ResolveContract(string contractCode)
        {
            var query = new QueryExpression("mcs_contract")
            {
                ColumnSet = new ColumnSet("mcs_name"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_name", ConditionOperator.Equal, contractCode)
                    }
                },
                TopCount = 1
            };

            var contract = _service.RetrieveMultiple(query).Entities.FirstOrDefault();
            _tracer.Trace($"解析合同: code={contractCode}, found={(contract != null)}");
            return contract;
        }

        /// <summary>
        /// 按客户查询最新一条厂端授信额度记录
        /// </summary>
        private Entity GetFactoryQuota(EntityReference accountRef)
        {
            var query = new QueryExpression("mcs_fca_quota")
            {
                ColumnSet = new ColumnSet("mcs_sellergrant", "mcs_sellerbalance", "mcs_usedsellerbalance"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_accountid", ConditionOperator.Equal, accountRef.Id)
                    }
                },
                TopCount = 1
            };
            query.AddOrder("createdon", OrderType.Descending);

            return _service.RetrieveMultiple(query).Entities.FirstOrDefault();
        }

        /// <summary>
        /// 从 TransactionCurrency 实体获取汇率（CNY = USD / 1 × rate，同 CurrencyConversionHelper 口径）
        /// </summary>
        private decimal GetExchangeRate(string isoCurrencyCode)
        {
            try
            {
                var query = new QueryExpression("transactioncurrency")
                {
                    ColumnSet = new ColumnSet("exchangerate"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("isocurrencycode", ConditionOperator.Equal, isoCurrencyCode)
                        }
                    },
                    TopCount = 1
                };

                var entity = _service.RetrieveMultiple(query).Entities.FirstOrDefault();
                if (entity != null)
                {
                    return entity.GetAttributeValue<decimal>("exchangerate");
                }
            }
            catch (Exception ex)
            {
                _tracer.Trace($"获取 {isoCurrencyCode} 汇率失败: {ex.Message}");
            }
            return 0m;
        }

        /// <summary>
        /// USD → CNY 换算（保留两位小数）；汇率为 0 时返回 0
        /// </summary>
        private decimal Convert(decimal usd, decimal rate)
        {
            if (usd == 0m || rate == 0m)
            {
                return 0m;
            }
            return Math.Round(usd * rate, 2);
        }

        /// <summary>
        /// 分页取全部记录
        /// </summary>
        private List<Entity> RetrieveAll(QueryExpression query)
        {
            var all = new List<Entity>();
            query.PageInfo = new PagingInfo { Count = 500, PageNumber = 1 };
            while (true)
            {
                var page = _service.RetrieveMultiple(query);
                all.AddRange(page.Entities);
                if (!page.MoreRecords)
                {
                    break;
                }
                query.PageInfo.PageNumber++;
                query.PageInfo.PagingCookie = page.PagingCookie;
            }
            return all;
        }

        private decimal GetMoney(Entity entity, string fieldName)
        {
            return entity.GetAttributeValue<Money>(fieldName)?.Value ?? 0m;
        }

        private QueryCreditBalanceResult Fail(string reason)
        {
            _tracer.Trace($"查询授信失败: {reason}");
            return new QueryCreditBalanceResult { Success = false, FailReason = reason };
        }
    }
}
