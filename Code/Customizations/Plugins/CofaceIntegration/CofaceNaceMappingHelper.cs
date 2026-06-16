using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace SanyD365.Plugins.CofaceIntegration
{
    /// <summary>
    /// Coface NACE 行业映射配置读取帮助类
    /// 从 mcs_coface_nace_mapping 实体读取 NACE Division → 三一行业映射
    /// </summary>
    public static class CofaceNaceMappingHelper
    {
        public const string EntityName = "mcs_coface_nace_mapping";
        public const string DivisionFromField = "mcs_nacedivisionfrom";
        public const string DivisionToField = "mcs_nacedivisionto";
        public const string DivisionNameField = "mcs_nacedivisionname";
        public const string SanyIndustryField = "mcs_sanyindustry";
        public const string EffectiveDateField = "mcs_effectivedate";
        public const string IsActiveField = "mcs_isactive";
        public const string RemarkField = "mcs_remark";

        /// <summary>
        /// 根据 NACE Code 获取映射的三一行业名称
        /// 提取 NACE Code 前两位 Division，按范围匹配；精确匹配优先于范围匹配
        /// </summary>
        /// <param name="service">组织服务</param>
        /// <param name="tracer">跟踪服务</param>
        /// <param name="naceCode">NACE Rev.2.1 代码，如 1011 / 2511 / 7112</param>
        /// <returns>三一行业名称；未配置时返回空字符串</returns>
        public static string GetSanyIndustry(
            IOrganizationService service,
            ITracingService tracer,
            string naceCode)
        {
            if (string.IsNullOrWhiteSpace(naceCode) || naceCode.Length < 2)
            {
                tracer.Trace("NACE 代码为空或长度不足，跳过行业映射");
                return string.Empty;
            }

            string divisionText = naceCode.Substring(0, 2);
            if (!int.TryParse(divisionText, out int division))
            {
                tracer.Trace($"NACE 代码前两位不是有效数字: {divisionText}，跳过行业映射");
                return string.Empty;
            }

            if (service == null)
            {
                tracer.Trace("IOrganizationService 为空，无法查询 Coface NACE 映射配置");
                return string.Empty;
            }

            try
            {
                var query = new QueryExpression(EntityName)
                {
                    ColumnSet = new ColumnSet(
                        DivisionFromField,
                        DivisionToField,
                        DivisionNameField,
                        SanyIndustryField,
                        EffectiveDateField,
                        RemarkField),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression(DivisionFromField, ConditionOperator.LessEqual, division),
                            new ConditionExpression(DivisionToField, ConditionOperator.GreaterEqual, division),
                            new ConditionExpression(IsActiveField, ConditionOperator.Equal, true)
                        }
                    },
                    Orders =
                    {
                        // 精确匹配优先：范围越小越靠前（to - from 越小越精确）
                        new OrderExpression(DivisionToField, OrderType.Ascending),
                        new OrderExpression(DivisionFromField, OrderType.Descending),
                        new OrderExpression(EffectiveDateField, OrderType.Descending)
                    },
                    TopCount = 1
                };

                var records = service.RetrieveMultiple(query);
                var record = records.Entities.FirstOrDefault();

                if (record == null)
                {
                    tracer.Trace($"NACE Division {division} 未配置行业映射");
                    return string.Empty;
                }

                var fromValue = record.GetAttributeValue<int>(DivisionFromField);
                var toValue = record.GetAttributeValue<int>(DivisionToField);
                var industry = record.GetAttributeValue<string>(SanyIndustryField) ?? string.Empty;
                var effectiveDate = record.GetAttributeValue<DateTime?>(EffectiveDateField);

                tracer.Trace($"NACE 行业映射: Division {division} ({fromValue}-{toValue}) => {industry}, effectiveDate={effectiveDate:yyyy-MM-dd}");

                return industry;
            }
            catch (Exception ex)
            {
                tracer.Trace($"查询 Coface NACE 映射异常 [Division {division}]: {ex.Message}");
                return string.Empty;
            }
        }
    }
}
