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

                // 2. 迟付指数 - latePaymentIndex value (定性：Coface 返回 0~4 代码，经 mcs_credititem_value 映射为枚举)
                string latePaymentIndex = ParseLatePaymentIndex(root);
                result["LatePaymentIndex"] = latePaymentIndex;
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
        /// Coface 返回 0~4 整数代码（0/1=n/a、2=Considerable、3=Some、4=No negative experience），
        /// 定性指标：原样返回代码字符串，由 CofaceQualitativeMappingHelper 按 mcs_credititem_value.mcs_cofacevalue 映射为枚举
        /// </summary>
        private string ParseLatePaymentIndex(JsonElement root)
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
                            // value 可能是数字或字符串（Coface 文档标注 String），统一转字符串代码
                            if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var d))
                            {
                                // Coface 代码为 0~4 整数，规整掉小数尾零（如 4.0 → "4"），保证与枚举 cofaceValue 精确匹配
                                return (d % 1 == 0)
                                    ? ((long)d).ToString(System.Globalization.CultureInfo.InvariantCulture)
                                    : d.ToString(System.Globalization.CultureInfo.InvariantCulture);
                            }
                            return value.GetString();
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
        /// JSON Path: productDetails.sectorRiskAssessment[].inHouseRegionRiskValue
        /// 口径（业务反馈表20260408）：JSON 中是多期值，取最新（isCurrent=true）
        /// </summary>
        private string ParseSectorRisk(JsonElement root)
        {
            try
            {
                if (root.TryGetProperty("productDetails", out var productDetails) &&
                    productDetails.TryGetProperty("sectorRiskAssessment", out var sraArray))
                {
                    string fallback = null;
                    foreach (var sra in sraArray.EnumerateArray())
                    {
                        if (!sra.TryGetProperty("inHouseRegionRiskValue", out var riskValue))
                            continue;

                        string value = riskValue.GetString();
                        if (string.IsNullOrEmpty(value))
                            continue;

                        // 取最新（isCurrent=true）；无 isCurrent 标记时记录首个有效值兜底
                        bool isCurrent = sra.TryGetProperty("isCurrent", out var isCurrentProp) && isCurrentProp.GetBoolean();
                        if (isCurrent)
                            return value;
                        if (fallback == null)
                            fallback = value;
                    }
                    if (fallback != null)
                        return fallback;
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
                            string industry = CofaceNaceMappingHelper.GetSanyIndustry(_service, _tracer, naceCode);
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
                            decimal? amount = fromAmount.GetDecimalSafe();
                            if (!amount.HasValue) continue;
                            decimal resultAmount = ApplyDimension(indicator, amount.Value);
                            resultAmount = ConvertCurrency(indicator, resultAmount);
                            return resultAmount;
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
                            decimal? amount = fromAmount.GetDecimalSafe();
                            if (!amount.HasValue) continue;
                            decimal resultAmount = ApplyDimension(indicator, amount.Value);
                            resultAmount = ConvertCurrency(indicator, resultAmount);
                            return resultAmount;
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
        /// 资产负债率特殊处理：①匹配到的比率若 type.name 含「%」则 ÷100 归一为小数比率（Coface 各编码量纲不统一）；
        /// ②ratios 匹配失败时按固定公式从资产负债表计算 Total Liabilities / Total Assets
        /// （公式型配置国家：配置的 typeValue 是金额科目编码，ratios 中不存在现成比率，如 PH indicator(35523)/indicator(35519)）
        /// </summary>
        private decimal? ParseFinancialRatio(JsonElement root, string indicatorName)
        {
            try
            {
                if (!root.TryGetProperty("productDetails", out var productDetails) ||
                    !productDetails.TryGetProperty("financials", out var financials))
                {
                    return null;
                }

                bool hasRatios = financials.TryGetProperty("ratios", out var ratios);

                // 获取配置表中指定指标的配置列表
                var configs = GetIndicatorConfigs(indicatorName);
                if (configs.Count == 0)
                {
                    _tracer.Trace($"国家 {_countryCode} 未配置 {indicatorName} 科目编码，尝试使用默认名称匹配");
                    if (hasRatios)
                    {
                        decimal? byName = ParseFinancialRatioByDefaultName(ratios, indicatorName, out string nameUsed);
                        if (byName.HasValue)
                        {
                            return indicatorName == "DebtRatio" ? NormalizeDebtRatioToFraction(byName.Value, nameUsed) : byName.Value;
                        }
                    }
                }
                else if (hasRatios)
                {
                    foreach (var config in configs)
                    {
                        decimal? value = FindRatioValue(ratios, config.TypeValue, out string matchedName);
                        if (value.HasValue)
                        {
                            decimal finalValue = indicatorName == "DebtRatio" ? NormalizeDebtRatioToFraction(value.Value, matchedName, config.FormulaFallback) : value.Value;
                            _tracer.Trace($"{indicatorName} 匹配 type.value={config.TypeValue}, 值={finalValue}");
                            return finalValue;
                        }
                    }

                    _tracer.Trace($"{indicatorName} 未在 ratios 中匹配到任何配置编码");
                }

                // 资产负债率公式兜底：固定公式 总负债/总资产（用户 2026-09-08 拍板：公式固定，不按配置读取）
                if (indicatorName == "DebtRatio")
                {
                    return ComputeDebtRatioFromBalanceSheet(financials);
                }
            }
            catch (Exception ex)
            {
                _tracer.Trace($"解析财务比率[{indicatorName}]异常: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// 资产负债率量纲归一为小数比率：Coface 各编码量纲不统一，URBA 报文 type.name 不带「%」标记
        /// （如实锤：PL 190 在 Report 字典叫 General debt ratio (%)，URBA 里只叫 General debt ratio，值 12.7449=百分数）
        /// 判定为百分数的依据：①type.name 含「%」；②配置 formulaFallback 含「*100」（Excel 公式列原文，如 PL (…)*100、RU …*100）
        /// 归一为小数（0.13），标签写入侧再统一 ×100 对齐 0907 卡百分制区间
        /// </summary>
        private static decimal NormalizeDebtRatioToFraction(decimal value, string ratioTypeName, string formulaFallback = null)
        {
            bool isPercent = (!string.IsNullOrEmpty(ratioTypeName) && ratioTypeName.Contains("%"))
                || (!string.IsNullOrEmpty(formulaFallback) &&
                    (formulaFallback.Contains("*100") || formulaFallback.Contains("* 100") || formulaFallback.Contains("×100")));
            if (isPercent)
            {
                return value / 100m;
            }
            return value;
        }

        /// <summary>
        /// 资产负债率公式兜底：从资产负债表按固定公式计算 Total Liabilities / Total Assets
        /// 按科目名称取数（名称各国一致，编码各国不同），取两科目均有值的最新期
        /// 返回小数比率（如 0.2475），标签写入侧 ×100 后为 24.75 对齐 0907 卡百分制区间
        /// </summary>
        private decimal? ComputeDebtRatioFromBalanceSheet(JsonElement financials)
        {
            try
            {
                if (!financials.TryGetProperty("balanceSheet", out var balanceSheet) ||
                    !balanceSheet.TryGetProperty("balanceSheetItems", out var items))
                {
                    return null;
                }

                // 按期次收集 Total Liabilities / Total Assets（跳过空值期次）
                var liabByDate = new Dictionary<string, decimal>();
                var assetByDate = new Dictionary<string, decimal>();
                foreach (var item in items.EnumerateArray())
                {
                    if (!item.TryGetProperty("indicators", out var indicators))
                        continue;

                    foreach (var indicator in indicators.EnumerateArray())
                    {
                        if (!indicator.TryGetProperty("type", out var type) ||
                            !type.TryGetProperty("name", out var nameProp))
                            continue;

                        string name = nameProp.GetString() ?? "";
                        bool isLiab = string.Equals(name, "Total Liabilities", StringComparison.OrdinalIgnoreCase);
                        bool isAsset = string.Equals(name, "Total Assets", StringComparison.OrdinalIgnoreCase);
                        if (!isLiab && !isAsset)
                            continue;

                        decimal? amount = null;
                        if (indicator.TryGetProperty("fromAmount", out var fromAmount))
                        {
                            amount = fromAmount.GetDecimalSafe();
                        }
                        if (!amount.HasValue)
                            continue;

                        string date = indicator.TryGetProperty("date", out var dateProp)
                            ? dateProp.GetRawText().Trim('"') : "";
                        if (isLiab) liabByDate[date] = amount.Value;
                        else assetByDate[date] = amount.Value;
                    }
                }

                // 取两科目均有值的最新期（期次为 YYYYMMDD 文本，倒序首个即最新）
                foreach (var date in assetByDate.Keys.OrderByDescending(d => d, StringComparer.Ordinal))
                {
                    if (liabByDate.TryGetValue(date, out var liab) && assetByDate[date] != 0)
                    {
                        decimal ratio = liab / assetByDate[date];
                        _tracer.Trace($"DebtRatio 按固定公式从资产负债表计算: Total Liabilities({liab})/Total Assets({assetByDate[date]})={ratio} (期次 {date})");
                        return ratio;
                    }
                }

                _tracer.Trace("DebtRatio 资产负债表中未找到 Total Liabilities/Total Assets 科目");
            }
            catch (Exception ex)
            {
                _tracer.Trace($"DebtRatio 公式兜底计算异常: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// 无配置时的默认名称匹配（兼容旧逻辑）
        /// </summary>
        private decimal? ParseFinancialRatioByDefaultName(JsonElement ratios, string indicatorName, out string matchedName)
        {
            matchedName = null;
            string ratioName;
            switch (indicatorName)
            {
                case "DebtRatio": ratioName = "Debt ratio"; break;
                case "CurrentRatio": ratioName = "Current Ratio"; break;
                case "NetProfitMargin": ratioName = "Net Profit Margin"; break;
                default: ratioName = indicatorName; break;
            }

            decimal? best = null;
            string bestDate = null;
            foreach (var ratio in ratios.EnumerateArray())
            {
                // 名称嵌套在 type.name（2026-09-08 修复：原误读 ratio 顶层 name，该属性不存在，兜底恒失效）
                string currentName = "";
                if (ratio.TryGetProperty("type", out var typeProp) &&
                    typeProp.TryGetProperty("name", out var nameProp))
                {
                    currentName = nameProp.GetString() ?? "";
                }

                if (currentName == ratioName ||
                    (indicatorName == "NetProfitMargin" && currentName == "ROS"))
                {
                    // 跳过空值期次，取最新有值期（2026-09-08 修复：原首个匹配即返回，空值期次会直接返回 null）
                    decimal? v = null;
                    if (ratio.TryGetProperty("resultValue", out var resultValue))
                    {
                        v = resultValue.GetDecimalSafe();
                    }
                    if (!v.HasValue && ratio.TryGetProperty("fromAmount", out var fromAmount))
                    {
                        v = fromAmount.GetDecimalSafe();
                    }
                    if (!v.HasValue)
                        continue;

                    string date = ratio.TryGetProperty("date", out var dateProp)
                        ? dateProp.GetRawText().Trim('"') : "";
                    if (best == null || string.CompareOrdinal(date, bestDate) > 0)
                    {
                        best = v.Value;
                        bestDate = date;
                        matchedName = currentName;
                    }
                }
            }
            return best;
        }

        /// <summary>
        /// 在 ratios 中查找指定 type.value 的 ratio 值
        /// 跳过空值期次，取最新有值期（2026-09-08 修复：原首个匹配即返回，空值期次会直接返回 null）
        /// </summary>
        private decimal? FindRatioValue(JsonElement ratios, string expectedTypeValue, out string matchedTypeName)
        {
            matchedTypeName = null;
            if (string.IsNullOrEmpty(expectedTypeValue))
                return null;

            decimal? best = null;
            string bestDate = null;
            foreach (var ratio in ratios.EnumerateArray())
            {
                string actualTypeValue = "";
                string typeName = "";
                if (ratio.TryGetProperty("type", out var type))
                {
                    if (type.TryGetProperty("value", out var valueProp))
                    {
                        actualTypeValue = valueProp.GetRawText().Trim('"');
                    }
                    if (type.TryGetProperty("name", out var nameProp))
                    {
                        typeName = nameProp.GetString() ?? "";
                    }
                }

                if (!string.Equals(actualTypeValue, expectedTypeValue, StringComparison.OrdinalIgnoreCase))
                    continue;

                decimal? v = null;
                if (ratio.TryGetProperty("resultValue", out var resultValue))
                {
                    v = resultValue.GetDecimalSafe();
                }
                if (!v.HasValue && ratio.TryGetProperty("fromAmount", out var fromAmount))
                {
                    v = fromAmount.GetDecimalSafe();
                }
                if (!v.HasValue)
                    continue;

                string date = ratio.TryGetProperty("date", out var dateProp)
                    ? dateProp.GetRawText().Trim('"') : "";
                if (best == null || string.CompareOrdinal(date, bestDate) > 0)
                {
                    best = v.Value;
                    bestDate = date;
                    matchedTypeName = typeName;
                }
            }
            return best;
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
        /// 汇率来源: D365 transactioncurrency 标准汇率（1 LC => USD，由 CofaceExchangeRateHelper 取倒数转换方向，2026-06-23 起）
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

                    // 从 D365 transactioncurrency 标准汇率读取（取倒数 1 LC => USD）
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
