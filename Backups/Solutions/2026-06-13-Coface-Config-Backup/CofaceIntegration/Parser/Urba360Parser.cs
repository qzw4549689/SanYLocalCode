using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using SanyD365.Plugins.CofaceIntegration;

namespace SanyD365.Plugins.CofaceIntegration.Parser
{
    /// <summary>
    /// URBA360 JSON数据解析器
    /// 从URBA360内容中提取9个指标
    /// 财务指标（净资产、资产负债率、流动比率、净利润率）的科目编码按国家从D365配置表读取
    /// </summary>
    public class Urba360Parser
    {
        private readonly ITracingService _tracer;
        private readonly IOrganizationService _service;
        private readonly string _countryCode;
        private readonly Dictionary<string, List<IndicatorConfig>> _indicatorConfigs;

        public Urba360Parser(ITracingService tracer)
        {
            _tracer = tracer;
        }

        /// <summary>
        /// 使用D365配置表构造解析器
        /// </summary>
        public Urba360Parser(ITracingService tracer, IOrganizationService service, string countryCode)
            : this(tracer)
        {
            _service = service;
            _countryCode = countryCode;
            _indicatorConfigs = LoadIndicatorConfigs(countryCode);
        }

        /// <summary>
        /// 解析URBA360数据，返回指标字典
        /// </summary>
        public Dictionary<string, object> Parse(JsonDocument urbaDoc)
        {
            var result = new Dictionary<string, object>();

            try
            {
                var root = urbaDoc.RootElement;

                // 必须先判断 productStatus.value != 0，NOT_AVAILABLE 时 productDetails 为空
                if (root.TryGetProperty("productStatus", out var productStatus))
                {
                    if (productStatus.TryGetProperty("value", out var psValue))
                    {
                        int statusValue = psValue.GetInt32();
                        if (statusValue == 0)
                        {
                            _tracer.Trace("URBA360 productStatus=0 (NOT_AVAILABLE)，productDetails为空，返回缺失值");
                            FillUrbaMissingValues(result);
                            return result;
                        }
                    }
                }

                // 1. 外部评级 - debtorRiskValue (定性)
                string externalRating = ParseExternalRating(root);
                result["ExternalRating"] = externalRating;
                _tracer.Trace($"外部评级: {externalRating}");

                // 2. 迟付指数 - latePaymentIndex value (定量)
                decimal? latePaymentIndex = ParseLatePaymentIndex(root);
                result["LatePaymentIndex"] = latePaymentIndex ?? -1;
                _tracer.Trace($"迟付指数: {latePaymentIndex}");

                // 3. 国别风险 - countryRiskValue (定性)
                string countryRisk = ParseCountryRisk(root);
                result["CountryRisk"] = countryRisk;
                _tracer.Trace($"国别风险: {countryRisk}");

                // 4. 行业风险 - sectorRiskValue (定性)
                string sectorRisk = ParseSectorRisk(root);
                result["SectorRisk"] = sectorRisk;
                _tracer.Trace($"行业风险: {sectorRisk}");

                // 5. 行业属性 - NACE Codes (定性)
                string naceCodes = ParseNaceCodes(root);
                result["NaceCodes"] = naceCodes;
                _tracer.Trace($"行业属性(NACE): {naceCodes}");

                // 6. 净资产 - Net Assets/Equity (定量)
                decimal? netAssets = ParseNetAssets(root);
                result["NetAssets"] = netAssets ?? -1;
                _tracer.Trace($"净资产: {netAssets}");

                // 7. 资产负债率 - Debt ratio (定量)
                decimal? debtRatio = ParseFinancialRatio(root, "DebtRatio");
                result["DebtRatio"] = debtRatio ?? -1;
                _tracer.Trace($"资产负债率: {debtRatio}");

                // 8. 流动比率 - Current Ratio (定量)
                decimal? currentRatio = ParseFinancialRatio(root, "CurrentRatio");
                result["CurrentRatio"] = currentRatio ?? -1;
                _tracer.Trace($"流动比率: {currentRatio}");

                // 9. 净利润率 - Net Profit Margin / ROS (定量)
                decimal? netProfitMargin = ParseFinancialRatio(root, "NetProfitMargin");
                result["NetProfitMargin"] = netProfitMargin ?? -1;
                _tracer.Trace($"净利润率: {netProfitMargin}");

                return result;
            }
            catch (Exception ex)
            {
                _tracer.Trace($"解析URBA360数据失败: {ex.Message}");
                throw new InvalidPluginExecutionException($"解析URBA360数据失败: {ex.Message}");
            }
        }

        #region 配置加载

        /// <summary>
        /// 从D365配置表 mcs_coface_financial_indicator 加载指定国家的财务指标配置
        /// Key=指标名(NetAssets/DebtRatio/CurrentRatio/NetProfitMargin), Value=按优先级排序的配置列表
        /// </summary>
        private Dictionary<string, List<IndicatorConfig>> LoadIndicatorConfigs(string countryCode)
        {
            var configs = new Dictionary<string, List<IndicatorConfig>>(StringComparer.OrdinalIgnoreCase);

            if (_service == null || string.IsNullOrEmpty(countryCode))
            {
                _tracer.Trace("未提供 IOrganizationService 或国家编码，无法加载Coface财务指标配置");
                return configs;
            }

            try
            {
                var query = new QueryExpression("mcs_coface_financial_indicator")
                {
                    ColumnSet = new ColumnSet(
                        "mcs_countrycode",
                        "mcs_indicatorname",
                        "mcs_typevalue",
                        "mcs_indicatortype",
                        "mcs_priority",
                        "mcs_formulafallback",
                        "mcs_isactive"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("mcs_countrycode", ConditionOperator.Equal, countryCode),
                            new ConditionExpression("mcs_isactive", ConditionOperator.Equal, true)
                        }
                    },
                    Orders =
                    {
                        new OrderExpression("mcs_indicatorname", OrderType.Ascending),
                        new OrderExpression("mcs_priority", OrderType.Ascending)
                    }
                };

                var records = _service.RetrieveMultiple(query);
                _tracer.Trace($"加载Coface财务指标配置: 国家={countryCode}, 记录数={records.Entities.Count}");

                foreach (var record in records.Entities)
                {
                    var config = new IndicatorConfig
                    {
                        CountryCode = record.GetAttributeValue<string>("mcs_countrycode") ?? countryCode,
                        IndicatorName = record.GetAttributeValue<string>("mcs_indicatorname") ?? "",
                        TypeValue = record.GetAttributeValue<string>("mcs_typevalue") ?? "",
                        IndicatorType = record.GetAttributeValue<OptionSetValue>("mcs_indicatortype")?.Value ?? 1,
                        Priority = record.GetAttributeValue<int>("mcs_priority"),
                        FormulaFallback = record.GetAttributeValue<string>("mcs_formulafallback") ?? "",
                        IsActive = record.GetAttributeValue<bool>("mcs_isactive")
                    };

                    if (string.IsNullOrEmpty(config.IndicatorName) || string.IsNullOrEmpty(config.TypeValue))
                    {
                        _tracer.Trace($"配置记录 {record.Id} 缺少指标名或编码，跳过");
                        continue;
                    }

                    string key = config.IndicatorName.Trim();
                    if (!configs.ContainsKey(key))
                    {
                        configs[key] = new List<IndicatorConfig>();
                    }
                    configs[key].Add(config);
                    _tracer.Trace($"配置: {key} => type.value={config.TypeValue}, priority={config.Priority}, type={config.IndicatorType}");
                }

                // 每个指标按优先级排序
                foreach (var key in configs.Keys.ToList())
                {
                    configs[key] = configs[key].OrderBy(c => c.Priority).ToList();
                }
            }
            catch (Exception ex)
            {
                _tracer.Trace($"加载Coface财务指标配置异常: {ex.Message}");
            }

            return configs;
        }

        #endregion

        #region 各指标解析方法

        /// <summary>
        /// 解析外部评级
        /// JSON Path: productDetails.score[].debtorRiskValue
        /// 必须取 isCurrent=true 的记录
        /// </summary>
        private string ParseExternalRating(JsonElement root)
        {
            try
            {
                if (root.TryGetProperty("productDetails", out var productDetails) &&
                    productDetails.TryGetProperty("score", out var scoreArray))
                {
                    foreach (var score in scoreArray.EnumerateArray())
                    {
                        // 只取 isCurrent=true 的记录
                        bool isCurrent = false;
                        if (score.TryGetProperty("isCurrent", out var isCurrentProp))
                        {
                            isCurrent = isCurrentProp.GetBoolean();
                        }

                        if (isCurrent && score.TryGetProperty("debtorRiskValue", out var debtorRiskValue))
                        {
                            return debtorRiskValue.GetString();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _tracer.Trace($"解析外部评级异常: {ex.Message}");
            }
            return "O"; // 缺失值
        }

        /// <summary>
        /// 解析迟付指数
        /// JSON Path: productDetails.latePaymentIndex[].value
        /// </summary>
        private decimal? ParseLatePaymentIndex(JsonElement root)
        {
            try
            {
                if (root.TryGetProperty("productDetails", out var productDetails) &&
                    productDetails.TryGetProperty("latePaymentIndex", out var lpiArray))
                {
                    foreach (var lpi in lpiArray.EnumerateArray())
                    {
                        if (lpi.TryGetProperty("value", out var value))
                        {
                            return value.GetDecimal();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _tracer.Trace($"解析迟付指数异常: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// 解析国别风险
        /// JSON Path: productDetails.countryRiskAssessment[].countryRiskValue
        /// </summary>
        private string ParseCountryRisk(JsonElement root)
        {
            try
            {
                if (root.TryGetProperty("productDetails", out var productDetails) &&
                    productDetails.TryGetProperty("countryRiskAssessment", out var craArray))
                {
                    foreach (var cra in craArray.EnumerateArray())
                    {
                        if (cra.TryGetProperty("countryRiskValue", out var riskValue))
                        {
                            return riskValue.GetString();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _tracer.Trace($"解析国别风险异常: {ex.Message}");
            }
            return "O"; // 缺失值
        }

        /// <summary>
        /// 解析行业风险
        /// JSON Path: productDetails.sectorRiskAssessment[].countryRiskValue
        /// </summary>
        private string ParseSectorRisk(JsonElement root)
        {
            try
            {
                if (root.TryGetProperty("productDetails", out var productDetails) &&
                    productDetails.TryGetProperty("sectorRiskAssessment", out var sraArray))
                {
                    foreach (var sra in sraArray.EnumerateArray())
                    {
                        if (sra.TryGetProperty("countryRiskValue", out var riskValue))
                        {
                            return riskValue.GetString();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _tracer.Trace($"解析行业风险异常: {ex.Message}");
            }
            return "O"; // 缺失值
        }

        /// <summary>
        /// 解析NACE行业属性代码
        /// JSON Path: companyGeneralInformation.naceCodes[].code
        /// 提取4位Class前2位(Division)，映射到三一行业定义，多个结果拼接
        /// </summary>
        private string ParseNaceCodes(JsonElement root)
        {
            try
            {
                if (root.TryGetProperty("companyGeneralInformation", out var cgi) &&
                    cgi.TryGetProperty("naceCodes", out var naceArray))
                {
                    var industries = new List<string>();
                    foreach (var nace in naceArray.EnumerateArray())
                    {
                        if (nace.TryGetProperty("code", out var code))
                        {
                            string naceCode = code.GetString() ?? "";
                            string industry = MapNaceToSanyIndustry(naceCode);
                            if (!string.IsNullOrEmpty(industry) && !industries.Contains(industry))
                            {
                                industries.Add(industry);
                            }
                        }
                    }
                    return industries.Count > 0 ? string.Join(",", industries) : "O";
                }
            }
            catch (Exception ex)
            {
                _tracer.Trace($"解析NACE代码异常: {ex.Message}");
            }
            return "O"; // 缺失值
        }

        /// <summary>
        /// NACE Rev.2.1 代码映射到三一行业定义
        /// 提取4位Class前2位(Division)进行映射
        /// </summary>
        private string MapNaceToSanyIndustry(string naceCode)
        {
            if (string.IsNullOrEmpty(naceCode) || naceCode.Length < 2)
                return "";

            // 提取前2位Division
            string division = naceCode.Substring(0, 2);
            if (!int.TryParse(division, out int div))
                return "";

            // NACE Division → 三一行业映射（基于Joyce提供的材料）
            switch (div)
            {
                case 1: return "农业";
                case 2: return "林业";
                case int d when d >= 5 && d <= 9: return "矿业";
                case 23: return "商混";
                case int d when d >= 10 && d <= 33: return "制造业";
                case 41:
                case 42: return "建工";
                case 43: return "吊装";
                case 49:
                case 50:
                case 51: return "集装箱运力";
                case 52: return "港务";
                case 77: return "租赁";
                default: return "";
            }
        }

        /// <summary>
        /// 解析净资产
        /// JSON Path: productDetails.financials.balanceSheet.balanceSheetItems[].indicators[]
        /// 根据国家配置表匹配 type.value
        /// </summary>
        private decimal? ParseNetAssets(JsonElement root)
        {
            try
            {
                if (!root.TryGetProperty("productDetails", out var productDetails) ||
                    !productDetails.TryGetProperty("financials", out var financials) ||
                    !financials.TryGetProperty("balanceSheet", out var balanceSheet) ||
                    !balanceSheet.TryGetProperty("balanceSheetItems", out var items))
                {
                    return null;
                }

                // 获取配置表中 NetAssets 的配置列表（按优先级排序）
                var configs = GetIndicatorConfigs("NetAssets");
                if (configs.Count == 0)
                {
                    _tracer.Trace($"国家 {_countryCode} 未配置 NetAssets 科目编码，尝试使用默认名称匹配");
                    return ParseNetAssetsByDefaultNames(items);
                }

                foreach (var config in configs)
                {
                    decimal? value = FindBalanceSheetIndicatorValue(items, config.TypeValue);
                    if (value.HasValue)
                    {
                        _tracer.Trace($"NetAssets 匹配 type.value={config.TypeValue}, 值={value.Value}");
                        return value.Value;
                    }
                }

                _tracer.Trace($"NetAssets 未在 balanceSheet 中匹配到任何配置编码");
            }
            catch (Exception ex)
            {
                _tracer.Trace($"解析净资产异常: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// 无配置时的默认名称匹配（兼容旧逻辑）
        /// </summary>
        private decimal? ParseNetAssetsByDefaultNames(JsonElement items)
        {
            foreach (var item in items.EnumerateArray())
            {
                if (!item.TryGetProperty("indicators", out var indicators))
                    continue;

                foreach (var indicator in indicators.EnumerateArray())
                {
                    if (!indicator.TryGetProperty("type", out var type))
                        continue;

                    string typeName = "";
                    if (type.TryGetProperty("name", out var nameProp))
                    {
                        typeName = nameProp.GetString() ?? "";
                    }

                    // 匹配净资产相关类型
                    if (typeName == "Net Assets" || typeName == "Net Worth" ||
                        typeName == "Equity" || typeName == "Own funds")
                    {
                        if (indicator.TryGetProperty("fromAmount", out var fromAmount))
                        {
                            decimal amount = fromAmount.GetDecimal();
                            amount = ApplyDimension(indicator, amount);
                            amount = ConvertCurrency(indicator, amount);
                            return amount;
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 在 balanceSheetItems 中查找指定 type.value 的 indicator 值
        /// </summary>
        private decimal? FindBalanceSheetIndicatorValue(JsonElement items, string expectedTypeValue)
        {
            if (string.IsNullOrEmpty(expectedTypeValue))
                return null;

            foreach (var item in items.EnumerateArray())
            {
                if (!item.TryGetProperty("indicators", out var indicators))
                    continue;

                foreach (var indicator in indicators.EnumerateArray())
                {
                    if (!indicator.TryGetProperty("type", out var type))
                        continue;

                    string actualTypeValue = "";
                    if (type.TryGetProperty("value", out var valueProp))
                    {
                        actualTypeValue = valueProp.GetRawText().Trim('"');
                    }

                    if (string.Equals(actualTypeValue, expectedTypeValue, StringComparison.OrdinalIgnoreCase))
                    {
                        if (indicator.TryGetProperty("fromAmount", out var fromAmount))
                        {
                            decimal amount = fromAmount.GetDecimal();
                            amount = ApplyDimension(indicator, amount);
                            amount = ConvertCurrency(indicator, amount);
                            return amount;
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 解析财务比率
        /// JSON Path: productDetails.financials.ratios[]
        /// 根据国家配置表匹配 type.value
        /// </summary>
        private decimal? ParseFinancialRatio(JsonElement root, string indicatorName)
        {
            try
            {
                if (!root.TryGetProperty("productDetails", out var productDetails) ||
                    !productDetails.TryGetProperty("financials", out var financials) ||
                    !financials.TryGetProperty("ratios", out var ratios))
                {
                    return null;
                }

                // 获取配置表中指定指标的配置列表
                var configs = GetIndicatorConfigs(indicatorName);
                if (configs.Count == 0)
                {
                    _tracer.Trace($"国家 {_countryCode} 未配置 {indicatorName} 科目编码，尝试使用默认名称匹配");
                    return ParseFinancialRatioByDefaultName(ratios, indicatorName);
                }

                foreach (var config in configs)
                {
                    decimal? value = FindRatioValue(ratios, config.TypeValue);
                    if (value.HasValue)
                    {
                        _tracer.Trace($"{indicatorName} 匹配 type.value={config.TypeValue}, 值={value.Value}");
                        return value.Value;
                    }
                }

                _tracer.Trace($"{indicatorName} 未在 ratios 中匹配到任何配置编码");
            }
            catch (Exception ex)
            {
                _tracer.Trace($"解析财务比率[{indicatorName}]异常: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// 无配置时的默认名称匹配（兼容旧逻辑）
        /// </summary>
        private decimal? ParseFinancialRatioByDefaultName(JsonElement ratios, string indicatorName)
        {
            string ratioName;
            switch (indicatorName)
            {
                case "DebtRatio": ratioName = "Debt ratio"; break;
                case "CurrentRatio": ratioName = "Current Ratio"; break;
                case "NetProfitMargin": ratioName = "Net Profit Margin"; break;
                default: ratioName = indicatorName; break;
            }

            foreach (var ratio in ratios.EnumerateArray())
            {
                string currentName = "";
                if (ratio.TryGetProperty("name", out var nameProp))
                {
                    currentName = nameProp.GetString() ?? "";
                }

                if (currentName == ratioName ||
                    (indicatorName == "NetProfitMargin" && currentName == "ROS"))
                {
                    if (ratio.TryGetProperty("resultValue", out var resultValue))
                    {
                        return resultValue.GetDecimal();
                    }
                    if (ratio.TryGetProperty("fromAmount", out var fromAmount))
                    {
                        return fromAmount.GetDecimal();
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 在 ratios 中查找指定 type.value 的 ratio 值
        /// </summary>
        private decimal? FindRatioValue(JsonElement ratios, string expectedTypeValue)
        {
            if (string.IsNullOrEmpty(expectedTypeValue))
                return null;

            foreach (var ratio in ratios.EnumerateArray())
            {
                string actualTypeValue = "";
                if (ratio.TryGetProperty("type", out var type) &&
                    type.TryGetProperty("value", out var valueProp))
                {
                    actualTypeValue = valueProp.GetRawText().Trim('"');
                }

                if (string.Equals(actualTypeValue, expectedTypeValue, StringComparison.OrdinalIgnoreCase))
                {
                    if (ratio.TryGetProperty("resultValue", out var resultValue))
                    {
                        return resultValue.GetDecimal();
                    }
                    if (ratio.TryGetProperty("fromAmount", out var fromAmount))
                    {
                        return fromAmount.GetDecimal();
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 获取指定指标名的配置列表
        /// </summary>
        private List<IndicatorConfig> GetIndicatorConfigs(string indicatorName)
        {
            if (_indicatorConfigs == null)
                return new List<IndicatorConfig>();

            string key = indicatorName.Trim();
            if (_indicatorConfigs.TryGetValue(key, out var configs))
                return configs;

            return new List<IndicatorConfig>();
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 应用dimension单位换算
        /// </summary>
        private decimal ApplyDimension(JsonElement indicator, decimal amount)
        {
            try
            {
                if (indicator.TryGetProperty("dimension", out var dimension) &&
                    dimension.TryGetProperty("value", out var dimValue))
                {
                    string dimStr = dimValue.GetString();
                    if (!string.IsNullOrEmpty(dimStr) && int.TryParse(dimStr, out int dim))
                    {
                        switch (dim)
                        {
                            case 1: return amount * 1000;      // Thousand
                            case 2: return amount * 1000000;   // Million
                            case 3: return amount * 1000000000; // Billion
                            case 4: return amount;              // Percent
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _tracer.Trace($"dimension换算异常: {ex.Message}");
            }
            return amount; // 默认不变
        }

        /// <summary>
        /// 货币转换（统一转USD）
        /// 汇率来源: D365 mcs_coface_exchange_rate 配置表（Coface 年度预算汇率）
        /// </summary>
        private decimal ConvertCurrency(JsonElement indicator, decimal amount)
        {
            try
            {
                if (indicator.TryGetProperty("currency", out var currency) &&
                    currency.TryGetProperty("value", out var currencyValue))
                {
                    string currencyCode = currencyValue.GetString();
                    if (string.IsNullOrEmpty(currencyCode) || currencyCode == "USD")
                        return amount;

                    // 从 D365 mcs_coface_exchange_rate 配置表读取年度预算汇率
                    decimal rate = CofaceExchangeRateHelper.GetRateToUsd(_service, _tracer, currencyCode);
                    if (rate > 0)
                    {
                        _tracer.Trace($"货币转换: {currencyCode} => USD, 汇率={rate}, 原金额={amount}, 转换后={amount * rate}");
                        return amount * rate;
                    }
                    else
                    {
                        _tracer.Trace($"货币转换: {currencyCode} 汇率未配置，保持原值");
                    }
                }
            }
            catch (Exception ex)
            {
                _tracer.Trace($"货币转换异常: {ex.Message}");
            }
            return amount;
        }

        /// <summary>
        /// URBA360 数据不可用时填充所有缺失值
        /// </summary>
        private void FillUrbaMissingValues(Dictionary<string, object> result)
        {
            result["ExternalRating"] = "O";
            result["LatePaymentIndex"] = null;
            result["CountryRisk"] = "O";
            result["SectorRisk"] = "O";
            result["NaceCodes"] = "O";
            result["NetAssets"] = null;
            result["DebtRatio"] = null;
            result["CurrentRatio"] = null;
            result["NetProfitMargin"] = null;
        }

        #endregion

        #region 内部类

        /// <summary>
        /// Coface 财务指标配置项
        /// </summary>
        private class IndicatorConfig
        {
            public string CountryCode { get; set; }
            public string IndicatorName { get; set; }
            public string TypeValue { get; set; }
            public int IndicatorType { get; set; }
            public int Priority { get; set; }
            public string FormulaFallback { get; set; }
            public bool IsActive { get; set; }
        }

        #endregion
    }
}
