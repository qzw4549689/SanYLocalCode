using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace SanyD365.Plugins.RiskExposure.Api
{
    /// <summary>
    /// 风险敞口计算结果
    /// </summary>
    public class RiskExposureResult
    {
        public bool Success { get; set; }
        public string FailReason { get; set; } = string.Empty;
        public decimal RiskExposure { get; set; }
    }

    /// <summary>
    /// 风险敞口计算服务
    /// 唯一名：mcs_CalcContractRiskExposure
    /// </summary>
    public class RiskExposureService
    {
        // 厂端授信额度是否生效：1=是
        private const int IS_ACTIVE_YES = 1;

        // 中信保限额类型：非信用证（无担保）
        private const int SINOSURE_APPLY_GENRE_NON_LC_NO_GUARANTEE = 1;

        // 中信保支付方式：OA
        private const int SINOSURE_PAY_MODE_OA = 4;

        // 中信保限额状态：0=无效
        private const int SINOSURE_QUOTA_STATE_INVALID = 0;

        private readonly IOrganizationService _service;
        private readonly ITracingService _tracer;

        public RiskExposureService(IOrganizationService service, ITracingService tracer)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _tracer = tracer ?? throw new ArgumentNullException(nameof(tracer));
        }

        /// <summary>
        /// 计算风险敞口
        /// </summary>
        /// <param name="type">A=厂端授信/中信保，B=外部融资</param>
        /// <param name="buyerCode">客户编码（mcs_customermasterdata.mcs_sapnumber）</param>
        /// <param name="riskAmount">合同风险赊销金额</param>
        /// <param name="signedAmount">合同签约占用金额</param>
        /// <param name="contractCode">合同编码（mcs_contract.mcs_name），B类必填</param>
        public RiskExposureResult Calculate(string type, string buyerCode, decimal riskAmount,
            decimal signedAmount, string contractCode)
        {
            _tracer.Trace($"Calculate 入参: type={type}, buyerCode={buyerCode}, riskAmount={riskAmount}, signedAmount={signedAmount}, contractCode={contractCode}");

            // 1. 解析客户
            var customer = ResolveCustomer(buyerCode);
            if (customer == null)
            {
                return Fail($"未找到客户编码[{buyerCode}]对应的客户主数据");
            }

            var customerRef = customer.ToEntityReference();
            string sinosureCode = customer.GetAttributeValue<string>("mcs_sinosurecode") ?? string.Empty;
            _tracer.Trace($"解析客户成功: customerId={customerRef.Id}, sinosureCode={sinosureCode}");

            if (string.Equals(type, "A", StringComparison.OrdinalIgnoreCase))
            {
                return CalculateTypeA(customerRef, sinosureCode, riskAmount, signedAmount);
            }
            else if (string.Equals(type, "B", StringComparison.OrdinalIgnoreCase))
            {
                return CalculateTypeB(riskAmount, contractCode);
            }
            else
            {
                return Fail($"不支持的类型: {type}（A=厂端授信/中信保, B=外部融资）");
            }
        }

        /// <summary>
        /// A类：厂端授信/中信保
        /// risk_exposure = signed_amount + used_balance + risk_amount - seller_grant - sinosure_balance
        /// </summary>
        private RiskExposureResult CalculateTypeA(EntityReference customerRef, string sinosureCode,
            decimal riskAmount, decimal signedAmount)
        {
            // 取厂端授信额度
            var quota = GetFcaQuota(customerRef);
            decimal sellerGrant = 0m;
            decimal usedBalance = 0m;
            if (quota != null)
            {
                sellerGrant = GetMoney(quota, "mcs_sellergrant");
                usedBalance = GetMoney(quota, "mcs_usedsellerbalance");
                _tracer.Trace($"厂端授信额度: sellerGrant={sellerGrant}, usedBalance={usedBalance}");
            }
            else
            {
                _tracer.Trace("未找到厂端授信额度记录，按0计算");
            }

            // 取中信保余额
            decimal sinosureBalance = GetSinosureBalance(sinosureCode);
            _tracer.Trace($"中信保余额汇总: {sinosureBalance}");

            // 计算
            decimal riskExposure = signedAmount + usedBalance + riskAmount - sellerGrant - sinosureBalance;
            _tracer.Trace($"A类风险敞口计算: {signedAmount} + {usedBalance} + {riskAmount} - {sellerGrant} - {sinosureBalance} = {riskExposure}");

            return Success(riskExposure);
        }

        /// <summary>
        /// B类：外部融资
        /// risk_exposure = risk_amount - financing_amount
        /// </summary>
        private RiskExposureResult CalculateTypeB(decimal riskAmount, string contractCode)
        {
            decimal financingAmount = 0m;

            if (!string.IsNullOrWhiteSpace(contractCode))
            {
                financingAmount = GetFinancingAmount(contractCode);
                _tracer.Trace($"外部融资金额: {financingAmount}");
            }
            else
            {
                _tracer.Trace("B类未传入合同编码，外部融资金额按0计算");
            }

            decimal riskExposure = riskAmount - financingAmount;
            _tracer.Trace($"B类风险敞口计算: {riskAmount} - {financingAmount} = {riskExposure}");

            return Success(riskExposure);
        }

        /// <summary>
        /// 按客户编码解析客户主数据
        /// </summary>
        private Entity ResolveCustomer(string buyerCode)
        {
            var query = new QueryExpression("mcs_customermasterdata")
            {
                ColumnSet = new ColumnSet("mcs_name", "mcs_sapnumber", "mcs_sinosurecode"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_sapnumber", ConditionOperator.Equal, buyerCode)
                    }
                },
                TopCount = 1
            };

            var customer = _service.RetrieveMultiple(query).Entities.FirstOrDefault();
            _tracer.Trace($"解析客户: code={buyerCode}, found={(customer != null)}");
            return customer;
        }

        /// <summary>
        /// 按客户查询最新一条生效的厂端授信额度记录
        /// </summary>
        private Entity GetFcaQuota(EntityReference customerRef)
        {
            var query = new QueryExpression("mcs_fca_quota")
            {
                ColumnSet = new ColumnSet("mcs_sellergrant", "mcs_usedsellerbalance", "mcs_isactive"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_accountid", ConditionOperator.Equal, customerRef.Id),
                        new ConditionExpression("mcs_isactive", ConditionOperator.Equal, IS_ACTIVE_YES)
                    }
                },
                TopCount = 1
            };
            query.AddOrder("createdon", OrderType.Descending);

            return _service.RetrieveMultiple(query).Entities.FirstOrDefault();
        }

        /// <summary>
        /// 按中信保买方代码查询有效余额（限额类型=非信用证无担保，支付方式=OA）
        /// </summary>
        private decimal GetSinosureBalance(string sinosureCode)
        {
            if (string.IsNullOrWhiteSpace(sinosureCode))
            {
                _tracer.Trace("中信保买方代码为空，余额按0计算");
                return 0m;
            }

            var query = new QueryExpression("mcs_approvedquota")
            {
                ColumnSet = new ColumnSet("mcs_quotabalance"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_buyerno", ConditionOperator.Equal, sinosureCode),
                        new ConditionExpression("mcs_quotastate", ConditionOperator.NotEqual, SINOSURE_QUOTA_STATE_INVALID),
                        new ConditionExpression("mcs_applygenre", ConditionOperator.Equal, SINOSURE_APPLY_GENRE_NON_LC_NO_GUARANTEE),
                        new ConditionExpression("mcs_paymode", ConditionOperator.Equal, SINOSURE_PAY_MODE_OA)
                    }
                }
            };

            var records = _service.RetrieveMultiple(query).Entities;
            decimal totalBalance = 0m;

            foreach (var record in records)
            {
                string balanceStr = record.GetAttributeValue<string>("mcs_quotabalance") ?? string.Empty;
                // DEV1 该字段为字符串类型，可能包含千分位逗号，先清理再解析
                string normalized = balanceStr.Replace(",", "").Trim();
                if (decimal.TryParse(normalized, out decimal balance))
                {
                    totalBalance += balance;
                }
                else
                {
                    _tracer.Trace($"中信保余额解析失败: {balanceStr}");
                }
            }

            _tracer.Trace($"中信保余额汇总: buyerNo={sinosureCode}, count={records.Count}, totalBalance={totalBalance}");
            return totalBalance;
        }

        /// <summary>
        /// 按合同编码查询外部融资授信金额（USD）
        /// 禅道 #2150：融资管理合同改多选（mcs_contract_ids，GUID 逗号分隔），匹配改 contains 模糊匹配；
        /// 授信金额口径改为：合同总金额（美元，mcs_totalcontractamount_base）×（1 − 首付比例 mcs_fsm_payment_ratio）
        /// </summary>
        private decimal GetFinancingAmount(string contractCode)
        {
            // 先解析合同（同时取合同总金额美元口径）
            var contractQuery = new QueryExpression("mcs_contract")
            {
                ColumnSet = new ColumnSet("mcs_contractid", "mcs_totalcontractamount_base"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_name", ConditionOperator.Equal, contractCode)
                    }
                },
                TopCount = 1
            };

            var contract = _service.RetrieveMultiple(contractQuery).Entities.FirstOrDefault();
            if (contract == null)
            {
                _tracer.Trace($"未找到合同编码[{contractCode}]对应的合同");
                return 0m;
            }

            // 禅道 #2150：多选合同字段 contains 模糊匹配（GUID 唯一 36 位，子串误匹配可忽略），取最新一条
            var fsmQuery = new QueryExpression("mcs_fsm_data")
            {
                ColumnSet = new ColumnSet("mcs_fsm_payment_ratio"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_contract_ids", ConditionOperator.Like, "%" + contract.Id.ToString("D") + "%")
                    }
                },
                TopCount = 1
            };
            fsmQuery.AddOrder("createdon", OrderType.Descending);

            var fsm = _service.RetrieveMultiple(fsmQuery).Entities.FirstOrDefault();
            if (fsm == null)
            {
                _tracer.Trace($"合同[{contractCode}]未找到关联的外部融资记录");
                return 0m;
            }

            // 授信金额 = 合同总金额（美元）×（1 − 首付比例）
            decimal totalAmountUsd = contract.GetAttributeValue<Money>("mcs_totalcontractamount_base")?.Value ?? 0m;
            decimal downPaymentRatio = fsm.GetAttributeValue<decimal?>("mcs_fsm_payment_ratio") ?? 0m;
            decimal amount = totalAmountUsd * (1m - downPaymentRatio);
            _tracer.Trace($"外部融资授信金额(#2150): contractCode={contractCode}, 合同总金额USD={totalAmountUsd}, 首付比例={downPaymentRatio}, amount={amount}");
            return amount;
        }

        private decimal GetMoney(Entity entity, string fieldName)
        {
            return entity.GetAttributeValue<Money>(fieldName)?.Value ?? 0m;
        }

        private RiskExposureResult Fail(string reason)
        {
            _tracer.Trace($"计算失败: {reason}");
            return new RiskExposureResult { Success = false, FailReason = reason, RiskExposure = 0m };
        }

        private RiskExposureResult Success(decimal riskExposure)
        {
            return new RiskExposureResult
            {
                Success = true,
                RiskExposure = Math.Round(riskExposure, 2)
            };
        }
    }
}
