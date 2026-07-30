using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace SanyD365.Plugins.FactoryCredit.Calculation.Services
{
    /// <summary>
    /// 模型参数读取服务
    /// </summary>
    public class ModelParameterService
    {
        private readonly IOrganizationService _service;
        private readonly ITracingService _tracer;

        public ModelParameterService(IOrganizationService service, ITracingService tracer)
        {
            _service = service;
            _tracer = tracer;
        }

        /// <summary>
        /// 按客户分类+客户等级读取模型参数（系数和聚合方法）；若按具体等级未取到，再按 ALL 兜底
        /// </summary>
        public ModelParameterInfo GetModelParameters(int buyerGradeValue, int creditGradeValue)
        {
            ModelParameterInfo info = QueryParameters(buyerGradeValue, creditGradeValue);
            if (info.HasModelParams)
            {
                return info;
            }

            if (creditGradeValue != 6)
            {
                _tracer.Trace($"未找到 {buyerGradeValue}/{creditGradeValue} 参数，尝试 ALL 兜底");
                info = QueryParameters(buyerGradeValue, 6);
            }

            return info;
        }

        /// <summary>
        /// 按客户分类读取历史基准额度（客户等级 = ALL）
        /// </summary>
        public decimal GetHistoricalBenchmark(int buyerGradeValue)
        {
            ModelParameterInfo info = QueryParameters(buyerGradeValue, 6, true);
            return info.CountryBenchmark;
        }

        private ModelParameterInfo QueryParameters(int buyerGradeValue, int creditGradeValue, bool benchmarkOnly = false)
        {
            ModelParameterInfo info = new ModelParameterInfo
            {
                BuyerGradeValue = buyerGradeValue,
                CreditGradeValue = creditGradeValue
            };

            try
            {
                ColumnSet columns;
                if (benchmarkOnly)
                {
                    columns = new ColumnSet("mcs_buyergrade", "mcs_creditgrade", "mcs_countryname");
                }
                else
                {
                    columns = new ColumnSet("mcs_buyergrade", "mcs_creditgrade", "mcs_adjust1", "mcs_adjust2", "mcs_adjust3", "mcs_aggfunc", "mcs_countryname");
                }

                QueryExpression query = new QueryExpression("mcs_fca_mdlconfig")
                {
                    ColumnSet = columns,
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("mcs_buyergrade", ConditionOperator.Equal, buyerGradeValue),
                            new ConditionExpression("mcs_creditgrade", ConditionOperator.Equal, creditGradeValue)
                        }
                    },
                    TopCount = 1
                };

                EntityCollection result = _service.RetrieveMultiple(query);
                if (result.Entities.Count > 0)
                {
                    Entity entity = result.Entities[0];
                    info.BuyerGradeLabel = GetOptionSetLabel(entity, "mcs_buyergrade");
                    info.CreditGradeLabel = GetOptionSetLabel(entity, "mcs_creditgrade");
                    info.CountryBenchmark = entity.GetAttributeValue<decimal>("mcs_countryname");

                    if (!benchmarkOnly)
                    {
                        info.Adjust1 = entity.GetAttributeValue<decimal>("mcs_adjust1");
                        info.Adjust2 = entity.GetAttributeValue<decimal>("mcs_adjust2");
                        info.Adjust3 = entity.GetAttributeValue<decimal>("mcs_adjust3");
                        info.AggFuncLabel = GetOptionSetLabel(entity, "mcs_aggfunc")?.ToUpperInvariant() ?? "MIN";

                        _tracer.Trace($"模型参数: {info.BuyerGradeLabel}/{info.CreditGradeLabel}, 系数=({info.Adjust1},{info.Adjust2},{info.Adjust3}), 聚合={info.AggFuncLabel}, 基准={info.CountryBenchmark}");
                    }
                    else
                    {
                        _tracer.Trace($"历史基准额度: {info.BuyerGradeLabel}/ALL, 基准={info.CountryBenchmark}");
                    }
                }
                else
                {
                    _tracer.Trace($"未找到模型参数: buyerGrade={buyerGradeValue}, creditGrade={creditGradeValue}");
                }
            }
            catch (Exception ex)
            {
                _tracer.Trace($"读取模型参数失败: {ex.Message}");
            }

            return info;
        }

        private string GetOptionSetLabel(Entity entity, string fieldName)
        {
            if (entity.FormattedValues.Contains(fieldName))
            {
                return entity.FormattedValues[fieldName];
            }

            if (!entity.Contains(fieldName))
            {
                return null;
            }

            OptionSetValue value = entity.GetAttributeValue<OptionSetValue>(fieldName);
            return value?.Value.ToString();
        }
    }
}
