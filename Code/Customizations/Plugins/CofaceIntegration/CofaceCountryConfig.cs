using System.Collections.Generic;

namespace SanyD365.Plugins.CofaceIntegration
{
    /// <summary>
    /// Coface 国家特殊处理配置
    /// 从 ms_systemconfiguration 读取，配置名为 CofaceCountryConfig
    /// </summary>
    public class CofaceCountryConfig
    {
        /// <summary>
        /// 受限国家代码列表（39 个受限国家，含 RU）
        /// 这些国家使用 RestrictedReportProduct 对应的 Report 产品
        /// </summary>
        public List<string> RestrictedCountries { get; set; } = new List<string>();

        /// <summary>
        /// 需要走 Full Report CEE 产品的国家代码列表（如俄罗斯 RU）
        /// CEE 国家优先于受限国家判断
        /// </summary>
        public List<string> CeeCountries { get; set; } = new List<string>();

        /// <summary>
        /// 非受限国家使用的 Report 产品 slug
        /// 对应截图中的 Full Report URBA：customized-report + 301
        /// </summary>
        public string DefaultReportProductSlug { get; set; } = "customized-report";

        /// <summary>
        /// 非受限国家使用的 Report 产品 code（customReportId）
        /// </summary>
        public string DefaultReportProductCode { get; set; } = "301";

        /// <summary>
        /// 受限国家（除 CEE 外）使用的 Report 产品 slug
        /// 对应截图中的 Full Report：full-report
        /// </summary>
        public string RestrictedReportProductSlug { get; set; } = "full-report";

        /// <summary>
        /// CEE 国家使用的 Report 产品 slug
        /// 对应截图中的 Full report CEE：customized-report + 21000
        /// </summary>
        public string CeeReportProductSlug { get; set; } = "customized-report";

        /// <summary>
        /// CEE 国家使用的 Report 产品 code（customReportId）
        /// </summary>
        public string CeeReportProductCode { get; set; } = "21000";

        /// <summary>
        /// 不支持一单双格式的国家代码列表（39 国，需 JSON/PDF 分别下两单）
        /// 来源：科法斯Report不支持1个订单双格式的国家列表20260520（不含 RU）
        /// TODO(C3 待确认)：两单是否两份费用待 Coface 书面确认
        /// </summary>
        public List<string> DualFormatCountries { get; set; } = new List<string>();

        /// <summary>
        /// 下单需附带 legitimateInterest 的国家代码 → 取值（如德国 DE）
        /// 值域参考 GET /legitimateinterestcodes
        /// </summary>
        public Dictionary<string, string> LegitimateInterestByCountry { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// 判断指定国家是否需要 JSON/PDF 分别下两单
        /// </summary>
        public bool IsDualFormatCountry(string countryCode)
        {
            return !string.IsNullOrEmpty(countryCode) &&
                   DualFormatCountries.Contains(countryCode.ToUpperInvariant());
        }

        /// <summary>
        /// 获取指定国家下单时需附带的 legitimateInterest 值，无配置返回 null
        /// </summary>
        public string GetLegitimateInterest(string countryCode)
        {
            if (string.IsNullOrEmpty(countryCode) || LegitimateInterestByCountry == null)
            {
                return null;
            }
            return LegitimateInterestByCountry.TryGetValue(countryCode.ToUpperInvariant(), out var value)
                ? value
                : null;
        }

        /// <summary>
        /// 判断指定国家是否属于受限国家（含 CEE）
        /// </summary>
        public bool IsRestrictedCountry(string countryCode)
        {
            return !string.IsNullOrEmpty(countryCode) &&
                   RestrictedCountries.Contains(countryCode.ToUpperInvariant());
        }

        /// <summary>
        /// 判断指定国家是否属于 CEE 国家
        /// </summary>
        public bool IsCeeCountry(string countryCode)
        {
            return !string.IsNullOrEmpty(countryCode) &&
                   CeeCountries.Contains(countryCode.ToUpperInvariant());
        }

        /// <summary>
        /// 根据 countryCode 获取对应的 Report 产品配置
        /// 优先级：CEE > 受限国家 > 非受限国家
        /// </summary>
        public ReportProduct GetReportProduct(string countryCode)
        {
            if (IsCeeCountry(countryCode))
            {
                return new ReportProduct
                {
                    Slug = CeeReportProductSlug,
                    ProductCode = CeeReportProductCode
                };
            }

            if (IsRestrictedCountry(countryCode))
            {
                return new ReportProduct
                {
                    Slug = RestrictedReportProductSlug,
                    ProductCode = null
                };
            }

            return new ReportProduct
            {
                Slug = DefaultReportProductSlug,
                ProductCode = DefaultReportProductCode
            };
        }
    }

    /// <summary>
    /// Coface Report 产品配置
    /// </summary>
    public class ReportProduct
    {
        /// <summary>
        /// Report 产品 slug（如 customized-report / full-report）
        /// </summary>
        public string Slug { get; set; }

        /// <summary>
        /// Report 产品 code，对应响应中的 customReportId（如 301 / 21000）
        /// 对于 full-report 这类不需要 code 的产品可为空
        /// </summary>
        public string ProductCode { get; set; }
    }
}
