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
        /// Report 产品需要 JSON/PDF 分开下单的国家代码列表（39 个受限国家）
        /// </summary>
        public List<string> RestrictedCountriesForSplitFormat { get; set; } = new List<string>();

        /// <summary>
        /// 需要走 Full Report CEE 产品的国家代码列表（如俄罗斯 RU）
        /// </summary>
        public List<string> CeeCountries { get; set; } = new List<string>();

        /// <summary>
        /// 标准 Full Report 产品标识（用于查询订单）
        /// </summary>
        public string DefaultReportProductSlug { get; set; } = "full-report";

        /// <summary>
        /// Full Report CEE 产品标识（用于查询订单）
        /// </summary>
        public string CeeReportProductSlug { get; set; } = "full-report-cee";

        /// <summary>
        /// 判断指定国家是否需要 JSON/PDF 分开下单
        /// </summary>
        public bool IsSplitFormatCountry(string countryCode)
        {
            return !string.IsNullOrEmpty(countryCode) &&
                   RestrictedCountriesForSplitFormat.Contains(countryCode.ToUpperInvariant());
        }

        /// <summary>
        /// 判断指定国家是否需要走 CEE 产品
        /// </summary>
        public bool IsCeeCountry(string countryCode)
        {
            return !string.IsNullOrEmpty(countryCode) &&
                   CeeCountries.Contains(countryCode.ToUpperInvariant());
        }
    }
}
