using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Text.Json;
using SanyD365.Plugins.CofaceIntegration;

namespace SanyD365.Plugins.CofaceIntegration.Parser
{
    /// <summary>
    /// Full Report JSON数据解析器
    /// 从Full Report内容中提取3个指标：注册资本、从业年限、诉讼记录
    /// </summary>
    public class FullReportParser
    {
        private readonly ITracingService _tracer;
        private readonly IOrganizationService _service;

        public FullReportParser(ITracingService tracer)
        {
            _tracer = tracer;
        }

        /// <summary>
        /// 使用 D365 服务构造解析器，支持从配置表读取汇率
        /// </summary>
        public FullReportParser(ITracingService tracer, IOrganizationService service)
            : this(tracer)
        {
            _service = service;
        }

        /// <summary>
        /// 解析Full Report数据，返回指标字典
        /// </summary>
        public Dictionary<string, object> Parse(JsonDocument reportDoc)
        {
            var result = new Dictionary<string, object>();

            try
            {
                var root = reportDoc.RootElement;

                // 获取creditReport节点
                if (!root.TryGetProperty("icon", out var icon) ||
                    !icon.TryGetProperty("creditReport", out var creditReport))
                {
                    _tracer.Trace("Full Report中未找到creditReport节点");
                    return result;
                }

                // 1. 注册资本 - shareCapital.nominalCapitalAmount (定量)
                decimal? registeredCapital = ParseRegisteredCapital(creditReport);
                result["RegisteredCapital"] = registeredCapital ?? -1;
                _tracer.Trace($"注册资本: {registeredCapital}");

                // 2. 从业年限 - company.established (定量，格式YYYYMM00)
                int? establishedYear = ParseEstablishedYear(creditReport);
                result["EstablishedYear"] = establishedYear ?? -1;
                _tracer.Trace($"从业年限(成立年份): {establishedYear}");

                // 3. 诉讼记录 - additionalInsolvencies数组长度 (定量)
                int litigationCount = ParseLitigationCount(creditReport);
                result["LitigationCount"] = litigationCount;
                _tracer.Trace($"诉讼记录数: {litigationCount}");

                return result;
            }
            catch (Exception ex)
            {
                _tracer.Trace($"解析Full Report数据失败: {ex.Message}");
                throw new InvalidPluginExecutionException($"解析Full Report数据失败: {ex.Message}");
            }
        }

        #region 各指标解析方法

        /// <summary>
        /// 解析注册资本
        /// JSON Path: icon.creditReport.shareCapital
        /// 三种资本: nominalCapitalAmount / issuedCapitalAmount / paidUpCapitalAmount
        /// 业务要求: max(三资本)，各自做dimension+货币转换后比较
        /// 备选路径: icon.creditReport.company.capital.indicator.fromAmount
        /// 注意: 单位是千非元，需×1000；本地货币→USD
        /// </summary>
        private decimal? ParseRegisteredCapital(JsonElement creditReport)
        {
            try
            {
                // 路径1: shareCapital 下三种资本取最大值
                if (creditReport.TryGetProperty("shareCapital", out var shareCapital))
                {
                    decimal? maxValue = null;

                    // 尝试取 nominalCapitalAmount
                    if (shareCapital.TryGetProperty("nominalCapitalAmount", out var nominalAmount))
                    {
                        decimal value = nominalAmount.GetDecimal();
                        value = ApplyDimension(shareCapital, value);
                        value = ConvertCurrency(shareCapital, value);
                        _tracer.Trace($"shareCapital.nominalCapitalAmount={value}");
                        if (!maxValue.HasValue || value > maxValue.Value)
                            maxValue = value;
                    }

                    // 尝试取 issuedCapitalAmount
                    if (shareCapital.TryGetProperty("issuedCapitalAmount", out var issuedAmount))
                    {
                        decimal value = issuedAmount.GetDecimal();
                        value = ApplyDimension(shareCapital, value);
                        value = ConvertCurrency(shareCapital, value);
                        _tracer.Trace($"shareCapital.issuedCapitalAmount={value}");
                        if (!maxValue.HasValue || value > maxValue.Value)
                            maxValue = value;
                    }

                    // 尝试取 paidUpCapitalAmount
                    if (shareCapital.TryGetProperty("paidUpCapitalAmount", out var paidUpAmount))
                    {
                        decimal value = paidUpAmount.GetDecimal();
                        value = ApplyDimension(shareCapital, value);
                        value = ConvertCurrency(shareCapital, value);
                        _tracer.Trace($"shareCapital.paidUpCapitalAmount={value}");
                        if (!maxValue.HasValue || value > maxValue.Value)
                            maxValue = value;
                    }

                    if (maxValue.HasValue)
                    {
                        _tracer.Trace($"注册资本取最大值: {maxValue.Value}");
                        return maxValue.Value;
                    }
                }

                // 路径2: company.capital.indicator.fromAmount (备选)
                if (creditReport.TryGetProperty("company", out var company) &&
                    company.TryGetProperty("capital", out var capital))
                {
                    if (capital.TryGetProperty("indicator", out var indicator) &&
                        indicator.TryGetProperty("fromAmount", out var fromAmount))
                    {
                        decimal value = fromAmount.GetDecimal();
                        // 处理dimension和货币转换
                        value = ApplyDimension(indicator, value);
                        value = ConvertCurrency(indicator, value);
                        return value;
                    }
                }
            }
            catch (Exception ex)
            {
                _tracer.Trace($"解析注册资本异常: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// 解析成立年份（从业年限）
        /// JSON Path: icon.creditReport.company.established
        /// 格式: YYYYMM00（如19470000表示1947年成立）
        /// 返回: 成立年份（如1947）
        /// </summary>
        private int? ParseEstablishedYear(JsonElement creditReport)
        {
            try
            {
                if (creditReport.TryGetProperty("company", out var company) &&
                    company.TryGetProperty("established", out var established))
                {
                    int establishedValue = established.GetInt32();
                    // 格式: YYYYMM00，取前4位作为年份
                    int year = establishedValue / 10000;
                    if (year > 1800 && year <= DateTime.Now.Year)
                    {
                        return year;
                    }
                }

                // 备选路径: registration.date
                if (creditReport.TryGetProperty("company", out var company2) &&
                    company2.TryGetProperty("registration", out var registration) &&
                    registration.TryGetProperty("date", out var regDate))
                {
                    int dateValue = regDate.GetInt32();
                    int year = dateValue / 10000;
                    if (year > 1800 && year <= DateTime.Now.Year)
                    {
                        return year;
                    }
                }
            }
            catch (Exception ex)
            {
                _tracer.Trace($"解析成立年份异常: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// 解析诉讼记录数量
        /// JSON Path: icon.creditReport.additionalInsolvencies (数组或对象)
        /// 返回: 诉讼记录条数
        /// </summary>
        private int ParseLitigationCount(JsonElement creditReport)
        {
            try
            {
                // 路径1: additionalInsolvencies (可能是Array或Object)
                if (creditReport.TryGetProperty("additionalInsolvencies", out var insolvencies))
                {
                    if (insolvencies.ValueKind == JsonValueKind.Array)
                    {
                        int count = 0;
                        foreach (var item in insolvencies.EnumerateArray())
                        {
                            count++;
                        }
                        _tracer.Trace($"additionalInsolvencies为Array, 数量: {count}");
                        return count;
                    }
                    else if (insolvencies.ValueKind == JsonValueKind.Object)
                    {
                        // Object类型，通常表示有1条记录（或包含记录信息的对象）
                        _tracer.Trace("additionalInsolvencies为Object, 按1条记录处理");
                        return 1;
                    }
                }

                // 路径2: litigations (可能是Array或Object)
                if (creditReport.TryGetProperty("litigations", out var litigations))
                {
                    if (litigations.ValueKind == JsonValueKind.Array)
                    {
                        int count = 0;
                        foreach (var item in litigations.EnumerateArray())
                        {
                            count++;
                        }
                        _tracer.Trace($"litigations为Array, 数量: {count}");
                        return count;
                    }
                    else if (litigations.ValueKind == JsonValueKind.Object)
                    {
                        _tracer.Trace("litigations为Object, 按1条记录处理");
                        return 1;
                    }
                }

                // 路径3: legalProceedings
                if (creditReport.TryGetProperty("legalProceedings", out var legalProceedings))
                {
                    if (legalProceedings.ValueKind == JsonValueKind.Array)
                    {
                        int count = 0;
                        foreach (var item in legalProceedings.EnumerateArray()) count++;
                        _tracer.Trace($"legalProceedings为Array, 数量: {count}");
                        return count;
                    }
                    else if (legalProceedings.ValueKind == JsonValueKind.Object)
                    {
                        _tracer.Trace("legalProceedings为Object, 按1条记录处理");
                        return 1;
                    }
                }

                _tracer.Trace("未找到任何诉讼记录字段");
            }
            catch (Exception ex)
            {
                _tracer.Trace($"解析诉讼记录异常: {ex.Message}");
            }
            return 0; // 无记录返回0
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
                            case 1: return amount * 1000;       // Thousand
                            case 2: return amount * 1000000;    // Million
                            case 3: return amount * 1000000000; // Billion
                            case 4: return amount;              // Percent
                            default: return amount;             // Unit/Blank
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _tracer.Trace($"dimension换算异常: {ex.Message}");
            }
            return amount;
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

        #endregion
    }
}
