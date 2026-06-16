using Microsoft.Xrm.Sdk;
using SanyD365.Plugins.CofaceIntegration.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace SanyD365.Plugins.CofaceIntegration.Plugin
{
    /// <summary>
    /// Coface 企业搜索 Custom Action Plugin
    /// 触发: mcs_CofaceSearchCompany
    /// 输入: CompanyName(英文名称), CountryCode(ISO-2国家编码)
    /// 输出: ResultJson(企业列表JSON)
    /// </summary>
    public class CofaceSearchCompanyPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = factory.CreateOrganizationService(context.UserId);
            ITracingService tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracer.Trace("===== CofaceSearchCompanyPlugin 开始执行 =====");

            try
            {
                // 读取输入参数
                string companyName = context.InputParameters.Contains("CompanyName") ? context.InputParameters["CompanyName"]?.ToString() : null;
                string countryCode = context.InputParameters.Contains("CountryCode") ? context.InputParameters["CountryCode"]?.ToString() : null;

                tracer.Trace($"搜索参数: companyName={companyName}, countryCode={countryCode}");

                if (string.IsNullOrWhiteSpace(companyName))
                {
                    throw new InvalidPluginExecutionException("客户英文名称不能为空");
                }

                if (string.IsNullOrWhiteSpace(countryCode))
                {
                    throw new InvalidPluginExecutionException("国家编码不能为空");
                }

                // 调用 Coface API 搜索企业
                var apiService = new CofaceApiService(service, tracer);
                using (var doc = apiService.SearchCompany(companyName, countryCode))
                {
                    var companies = ParseCompanies(doc, apiService, countryCode, tracer);
                    string resultJson = JsonSerializer.Serialize(companies);
                    tracer.Trace($"搜索完成，返回企业数: {companies.Count}, JSON长度: {resultJson.Length}");

                    context.OutputParameters["ResultJson"] = resultJson;
                }

                tracer.Trace("===== CofaceSearchCompanyPlugin 执行完成 =====");
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                tracer.Trace($"Coface企业搜索失败: {ex.Message}");
                throw new InvalidPluginExecutionException($"Coface企业搜索失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 解析 Coface 返回的企业列表
        /// </summary>
        private List<CompanySearchResult> ParseCompanies(JsonDocument doc, CofaceApiService apiService, string countryCode, ITracingService tracer)
        {
            var results = new List<CompanySearchResult>();

            try
            {
                var root = doc.RootElement;

                // 支持根元素为数组或包含 companies 属性的对象
                List<JsonElement> companyElements = new List<JsonElement>();

                if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in root.EnumerateArray())
                    {
                        companyElements.Add(item);
                    }
                }
                else if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("companies", out var companiesArray) &&
                        companiesArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in companiesArray.EnumerateArray())
                        {
                            companyElements.Add(item);
                        }
                    }
                    else if (root.TryGetProperty("id", out _))
                    {
                        // 单条记录对象
                        companyElements.Add(root);
                    }
                }

                tracer.Trace($"解析到 {companyElements.Count} 个企业候选");

                foreach (var company in companyElements)
                {
                    var result = new CompanySearchResult();

                    // 企业名称（支持 string 或对象）
                    result.Name = GetStringSafe(company, "name", tracer);
                    if (string.IsNullOrEmpty(result.Name))
                    {
                        result.Name = GetStringSafe(company, "internationalName", tracer);
                    }

                    // Coface ID：从 externalIds 中取 repositorySlug="icon" 的 id
                    result.CofaceId = ExtractIconExternalId(company, tracer);

                    // 若未找到 icon，但有 giid，则用 giid 二次检索获取 icon
                    if (string.IsNullOrEmpty(result.CofaceId))
                    {
                        var giidId = ExtractExternalId(company, "giid", tracer);
                        if (!string.IsNullOrEmpty(giidId))
                        {
                            tracer.Trace($"企业 {result.Name} 无 icon，尝试用 giid 二次搜索: {giidId}");
                            try
                            {
                                using (var giidDoc = apiService.SearchCompanyByExternalId($"giid#{giidId}", countryCode))
                                {
                                    var giidCompanies = ParseGiidResult(giidDoc, tracer);
                                    if (giidCompanies.Count > 0)
                                    {
                                        result.CofaceId = giidCompanies[0].CofaceId;
                                        if (string.IsNullOrEmpty(result.Address)) result.Address = giidCompanies[0].Address;
                                        if (string.IsNullOrEmpty(result.City)) result.City = giidCompanies[0].City;
                                        if (string.IsNullOrEmpty(result.CountryCode)) result.CountryCode = giidCompanies[0].CountryCode;
                                        if (string.IsNullOrEmpty(result.RegistrationNumber)) result.RegistrationNumber = giidCompanies[0].RegistrationNumber;
                                        if (string.IsNullOrEmpty(result.TaxNumber)) result.TaxNumber = giidCompanies[0].TaxNumber;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                tracer.Trace($"giid 二次搜索失败: {ex.Message}");
                            }
                        }
                    }

                    // 地址：支持 string 或对象
                    if (company.TryGetProperty("address", out var addressProp))
                    {
                        if (addressProp.ValueKind == JsonValueKind.String)
                        {
                            result.Address = addressProp.GetString();
                        }
                        else if (addressProp.ValueKind == JsonValueKind.Object)
                        {
                            // 尝试对象中的常用字段
                            result.Address = GetStringSafe(addressProp, "address1", tracer)
                                ?? GetStringSafe(addressProp, "street", tracer)
                                ?? GetStringSafe(addressProp, "fullAddress", tracer);
                            if (string.IsNullOrEmpty(result.City))
                            {
                                result.City = GetStringSafe(addressProp, "city", tracer);
                            }
                            if (string.IsNullOrEmpty(result.CountryCode))
                            {
                                result.CountryCode = GetStringSafe(addressProp, "countryCode", tracer);
                            }
                        }
                    }

                    // 城市（若 address 对象中未取到）
                    if (string.IsNullOrEmpty(result.City))
                    {
                        result.City = GetStringSafe(company, "city", tracer);
                    }

                    // 国家
                    if (string.IsNullOrEmpty(result.CountryCode))
                    {
                        result.CountryCode = GetStringSafe(company, "countryCode", tracer);
                    }

                    // 注册号
                    result.RegistrationNumber = GetStringSafe(company, "registrationNumber", tracer);

                    // 税号
                    result.TaxNumber = GetStringSafe(company, "taxNumber", tracer);

                    results.Add(result);
                }
            }
            catch (Exception ex)
            {
                tracer.Trace($"解析企业列表异常: {ex.Message}");
                throw;
            }

            return results;
        }

        /// <summary>
        /// 安全读取字符串属性（支持 string / 对象 / null）
        /// </summary>
        private string GetStringSafe(JsonElement element, string propertyName, ITracingService tracer)
        {
            try
            {
                if (!element.TryGetProperty(propertyName, out var prop))
                    return null;

                if (prop.ValueKind == JsonValueKind.String)
                    return prop.GetString();

                if (prop.ValueKind == JsonValueKind.Null || prop.ValueKind == JsonValueKind.Undefined)
                    return null;

                // 对象或数组：尝试取常见文本字段，否则返回原始 JSON
                if (prop.ValueKind == JsonValueKind.Object)
                {
                    if (prop.TryGetProperty("value", out var valueProp) && valueProp.ValueKind == JsonValueKind.String)
                        return valueProp.GetString();
                    if (prop.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
                        return nameProp.GetString();

                    return prop.GetRawText();
                }

                if (prop.ValueKind == JsonValueKind.Array)
                {
                    return string.Join(", ", prop.EnumerateArray().Select(a => a.ValueKind == JsonValueKind.String ? a.GetString() : a.GetRawText()).Where(s => !string.IsNullOrEmpty(s)));
                }

                return prop.ToString();
            }
            catch (Exception ex)
            {
                tracer.Trace($"读取属性 {propertyName} 异常: {ex.Message}");
                return null;
            }
        }


        /// <summary>
        /// 从 externalIds 中提取指定类型的外部 ID
        /// </summary>
        private string ExtractExternalId(JsonElement company, string repositorySlug, ITracingService tracer)
        {
            try
            {
                if (company.TryGetProperty("externalIds", out var externalIds) &&
                    externalIds.ValueKind == JsonValueKind.Array)
                {
                    foreach (var extId in externalIds.EnumerateArray())
                    {
                        if (extId.TryGetProperty("repositorySlug", out _) &&
                            extId.TryGetProperty("id", out _))
                        {
                            string slug = GetStringSafe(extId, "repositorySlug", tracer);
                            string id = GetStringSafe(extId, "id", tracer);

                            if (string.Equals(slug, repositorySlug, StringComparison.OrdinalIgnoreCase) &&
                                !string.IsNullOrEmpty(id))
                            {
                                return id;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                tracer.Trace($"提取{repositorySlug}外部ID异常: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// 从 externalIds 中提取 ICON 类型的 Coface ID
        /// </summary>
        private string ExtractIconExternalId(JsonElement company, ITracingService tracer)
        {
            var iconId = ExtractExternalId(company, "icon", tracer);
            return string.IsNullOrEmpty(iconId) ? null : $"icon#{iconId}";
        }

        /// <summary>
        /// 解析 giid 二次搜索返回的结果（支持数组或单对象）
        /// </summary>
        private List<CompanySearchResult> ParseGiidResult(JsonDocument doc, ITracingService tracer)
        {
            var results = new List<CompanySearchResult>();
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in root.EnumerateArray())
                {
                    results.Add(MapCompanyElement(item, tracer));
                }
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                results.Add(MapCompanyElement(root, tracer));
            }

            return results;
        }

        private CompanySearchResult MapCompanyElement(JsonElement company, ITracingService tracer)
        {
            var result = new CompanySearchResult();

            result.Name = GetStringSafe(company, "name", tracer);
            if (string.IsNullOrEmpty(result.Name))
            {
                result.Name = GetStringSafe(company, "internationalName", tracer);
            }

            result.CofaceId = ExtractIconExternalId(company, tracer);

            if (company.TryGetProperty("address", out var addressProp))
            {
                if (addressProp.ValueKind == JsonValueKind.String)
                {
                    result.Address = addressProp.GetString();
                }
                else if (addressProp.ValueKind == JsonValueKind.Object)
                {
                    result.Address = GetStringSafe(addressProp, "address1", tracer)
                        ?? GetStringSafe(addressProp, "street", tracer)
                        ?? GetStringSafe(addressProp, "fullAddress", tracer);
                    result.City = GetStringSafe(addressProp, "city", tracer);
                    result.CountryCode = GetStringSafe(addressProp, "countryCode", tracer);
                }
            }

            if (string.IsNullOrEmpty(result.City))
            {
                result.City = GetStringSafe(company, "city", tracer);
            }
            if (string.IsNullOrEmpty(result.CountryCode))
            {
                result.CountryCode = GetStringSafe(company, "countryCode", tracer);
            }

            result.RegistrationNumber = GetStringSafe(company, "registrationNumber", tracer);
            result.TaxNumber = GetStringSafe(company, "taxNumber", tracer);

            return result;
        }

        /// <summary>
        /// 企业搜索结果
        /// </summary>
        private class CompanySearchResult
        {
            public string CofaceId { get; set; }
            public string Name { get; set; }
            public string Address { get; set; }
            public string City { get; set; }
            public string CountryCode { get; set; }
            public string RegistrationNumber { get; set; }
            public string TaxNumber { get; set; }
        }
    }
}
