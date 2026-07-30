using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace SanyD365.Plugins.FactoryCredit.Calculation.Services
{
    /// <summary>
    /// 货币汇率转换辅助类
    /// </summary>
    public class CurrencyConversionHelper
    {
        private readonly IOrganizationService _service;
        private readonly ITracingService _tracer;

        public CurrencyConversionHelper(IOrganizationService service, ITracingService tracer)
        {
            _service = service;
            _tracer = tracer;
        }

        /// <summary>
        /// 将人民币金额转换为 USD
        /// </summary>
        public decimal ConvertRmbToUsd(decimal amountRmb)
        {
            if (amountRmb == 0m)
            {
                return 0m;
            }

            decimal rate = GetExchangeRate("CNY");
            if (rate == 0m)
            {
                _tracer.Trace("CNY 汇率未找到，按 0 处理");
                return 0m;
            }

            // PRD: 本地货币 L 兑换美元 = L / R
            decimal result = amountRmb / rate;
            _tracer.Trace($"汇率转换: {amountRmb} CNY / {rate} = {result} USD");
            return result;
        }

        /// <summary>
        /// 从 TransactionCurrency 实体获取汇率
        /// </summary>
        private decimal GetExchangeRate(string isoCurrencyCode)
        {
            try
            {
                QueryExpression query = new QueryExpression("transactioncurrency")
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

                EntityCollection result = _service.RetrieveMultiple(query);
                if (result.Entities.Count > 0)
                {
                    decimal rate = result.Entities[0].GetAttributeValue<decimal>("exchangerate");
                    _tracer.Trace($"获取 {isoCurrencyCode} 汇率: {rate}");
                    return rate;
                }
            }
            catch (Exception ex)
            {
                _tracer.Trace($"获取 {isoCurrencyCode} 汇率失败: {ex.Message}");
            }

            return 0m;
        }
    }
}
