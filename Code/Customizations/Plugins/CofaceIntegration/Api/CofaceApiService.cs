using Microsoft.Xrm.Sdk;
using SanyD365.Plugins.CofaceIntegration;
using SanyD365.Plugins.CofaceIntegration.Token;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;

namespace SanyD365.Plugins.CofaceIntegration.Api
{
    /// <summary>
    /// Coface API服务封装
    /// 提供各接口的调用方法
    /// </summary>
    public class CofaceApiService
    {
        private readonly IOrganizationService _service;
        private readonly ITracingService _tracer;
        private readonly CofaceTokenManager _tokenManager;
        private readonly CofaceApiConfig _config;

        public CofaceApiService(IOrganizationService service, ITracingService tracer)
        {
            _service = service;
            _tracer = tracer;
            _config = CofaceConfigHelper.GetConfig(service);
            _tokenManager = new CofaceTokenManager(service, tracer);
        }

        #region 公司搜索

        /// <summary>
        /// 搜索公司（通过英文名称+国家编码）
        /// </summary>
        public JsonDocument SearchCompany(string companyName, string countryCode)
        {
            _tracer.Trace($"SearchCompany: name={companyName}, country={countryCode}");
            string url = $"{_config.BaseUrl}/companies?companyName={Uri.EscapeDataString(companyName)}&countryCode={countryCode}";
            return ExecuteGet(url);
        }

        /// <summary>
        /// 搜索公司（通过externalId+国家编码）
        /// </summary>
        public JsonDocument SearchCompanyByExternalId(string externalId, string countryCode)
        {
            _tracer.Trace($"SearchCompanyByExternalId: externalId={externalId}, country={countryCode}");
            string url = $"{_config.BaseUrl}/companies?externalId={Uri.EscapeDataString(externalId)}&countryCode={countryCode}";
            return ExecuteGet(url);
        }

        #endregion

        #region URBA360接口

        /// <summary>
        /// 查询URBA360监控订单列表
        /// </summary>
        public JsonDocument GetUrbaMonitoringOrders(string externalId, string countryCode)
        {
            _tracer.Trace($"GetUrbaMonitoringOrders: externalId={externalId}, country={countryCode}");
            string url = $"{_config.BaseUrl}/urba360/monitorings/orders?externalId={Uri.EscapeDataString(externalId)}&countryCode={countryCode}";
            return ExecuteGet(url);
        }

        /// <summary>
        /// 获取URBA360监控订单状态
        /// </summary>
        public JsonDocument GetUrbaMonitoringOrderStatus(string orderId)
        {
            _tracer.Trace($"GetUrbaMonitoringOrderStatus: orderId={orderId}");
            string url = $"{_config.BaseUrl}/urba360/monitorings/orders/{orderId}";
            return ExecuteGet(url);
        }

        /// <summary>
        /// 获取URBA360内容
        /// </summary>
        public JsonDocument GetUrbaContent(string orderId)
        {
            _tracer.Trace($"GetUrbaContent: orderId={orderId}");
            string url = $"{_config.BaseUrl}/urba360/content?id={orderId}";
            return ExecuteGet(url);
        }

        #endregion

        #region Report接口

        /// <summary>
        /// 查询Report订单列表
        /// </summary>
        /// <param name="externalId">Coface 企业标识（icon#xxx）</param>
        /// <param name="countryCode">国家代码</param>
        /// <param name="productSlug">Report 产品 slug（如 customized-report / full-report）</param>
        /// <param name="productCode">Report 产品 code（customReportId，如 301 / 21000），可为空</param>
        public JsonDocument GetReportOrders(string externalId, string countryCode, string productSlug, string productCode = null)
        {
            _tracer.Trace($"GetReportOrders: externalId={externalId}, country={countryCode}, productSlug={productSlug}, productCode={productCode}");
            string url = $"{_config.BaseUrl}/publications/orders?externalId={Uri.EscapeDataString(externalId)}&countryCode={countryCode}&productSlug={Uri.EscapeDataString(productSlug)}";
            if (!string.IsNullOrEmpty(productCode))
            {
                url += $"&productCode={Uri.EscapeDataString(productCode)}";
            }
            return ExecuteGet(url);
        }

        /// <summary>
        /// 获取Report内容（通过publicationId）
        /// </summary>
        public JsonDocument GetReportContent(string publicationId)
        {
            _tracer.Trace($"GetReportContent: publicationId={publicationId}");
            // 必须指定 format=json，否则 Coface 接口返回 400
            string url = $"{_config.BaseUrl}/publications?id={publicationId}&format=json";
            return ExecuteGet(url);
        }

        /// <summary>
        /// 下载 Full Report PDF（通过publicationId）
        /// 返回 PDF 文件二进制字节数组
        /// </summary>
        public byte[] GetReportPdf(string publicationId)
        {
            _tracer.Trace($"GetReportPdf: publicationId={publicationId}");
            string url = $"{_config.BaseUrl}/publications?id={publicationId}&format=pdf";
            return ExecuteDownload(url);
        }

        #endregion

        #region HTTP执行

        /// <summary>
        /// 执行二进制文件下载（用于 PDF 等附件）
        /// </summary>
        private byte[] ExecuteDownload(string url)
        {
            string token = _tokenManager.GetValidToken();

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Headers.Add("Authorization", $"Bearer {token}");
            request.Headers.Add("x-api-key", _config.ApiKey);
            request.Timeout = 60000; // PDF 可能较大，60 秒超时

            _tracer.Trace($"HTTP Download: {url}");

            try
            {
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    string contentType = response.ContentType?.ToLowerInvariant() ?? "";
                    _tracer.Trace($"HTTP响应: Status={response.StatusCode}, ContentType={contentType}, Length={response.ContentLength}");

                    if (contentType.Contains("application/json"))
                    {
                        // 某些场景下 format=pdf 仍可能返回 JSON 错误信息
                        using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                        {
                            string errorText = reader.ReadToEnd();
                            throw new InvalidPluginExecutionException($"Coface PDF 接口返回 JSON 而非 PDF: {errorText}");
                        }
                    }

                    using (Stream responseStream = response.GetResponseStream())
                    using (MemoryStream ms = new MemoryStream())
                    {
                        responseStream.CopyTo(ms);
                        return ms.ToArray();
                    }
                }
            }
            catch (WebException ex)
            {
                return HandleDownloadException(ex, url);
            }
        }

        /// <summary>
        /// 处理下载异常
        /// </summary>
        private byte[] HandleDownloadException(WebException ex, string url)
        {
            string errorDetail = "";
            int statusCode = 0;

            if (ex.Response != null)
            {
                var response = (HttpWebResponse)ex.Response;
                statusCode = (int)response.StatusCode;
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    errorDetail = reader.ReadToEnd();
                }
            }

            string errorMsg = $"Coface PDF 下载失败: URL={url}, Status={statusCode}, Error={ex.Message}";
            if (!string.IsNullOrEmpty(errorDetail))
            {
                errorMsg += $", Detail={errorDetail}";
            }

            _tracer.Trace(errorMsg);

            // 401/403 时尝试刷新 Token 重试一次
            if (statusCode == 401 || statusCode == 403)
            {
                _tracer.Trace("Token 可能过期，尝试刷新后重试 PDF 下载");
                try
                {
                    string newToken = _tokenManager.RefreshToken();
                    return RetryDownload(url, newToken);
                }
                catch (Exception retryEx)
                {
                    _tracer.Trace($"重试失败: {retryEx.Message}");
                    throw new InvalidPluginExecutionException($"Coface PDF 下载认证失败: {errorMsg}");
                }
            }

            throw new InvalidPluginExecutionException(errorMsg);
        }

        /// <summary>
        /// 使用新 Token 重试下载
        /// </summary>
        private byte[] RetryDownload(string url, string token)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Headers.Add("Authorization", $"Bearer {token}");
            request.Headers.Add("x-api-key", _config.ApiKey);
            request.Timeout = 60000;

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream responseStream = response.GetResponseStream())
            using (MemoryStream ms = new MemoryStream())
            {
                responseStream.CopyTo(ms);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// 执行GET请求
        /// </summary>
        private JsonDocument ExecuteGet(string url)
        {
            string token = _tokenManager.GetValidToken();

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.ContentType = "application/json";
            request.Headers.Add("Authorization", $"Bearer {token}");
            request.Headers.Add("x-api-key", _config.ApiKey);
            request.Timeout = 30000; // 30秒超时

            _tracer.Trace($"HTTP GET: {url}");

            try
            {
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    string responseText = reader.ReadToEnd();
                    _tracer.Trace($"HTTP响应: Status={response.StatusCode}, Length={responseText.Length}");
                    return JsonDocument.Parse(responseText);
                }
            }
            catch (WebException ex)
            {
                return HandleWebException(ex, url);
            }
        }

        /// <summary>
        /// 处理Web异常
        /// </summary>
        private JsonDocument HandleWebException(WebException ex, string url)
        {
            string errorDetail = "";
            int statusCode = 0;

            if (ex.Response != null)
            {
                var response = (HttpWebResponse)ex.Response;
                statusCode = (int)response.StatusCode;

                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    errorDetail = reader.ReadToEnd();
                }
            }

            string errorMsg = $"Coface API调用失败: URL={url}, Status={statusCode}, Error={ex.Message}";
            if (!string.IsNullOrEmpty(errorDetail))
            {
                errorMsg += $", Detail={errorDetail}";
            }

            _tracer.Trace(errorMsg);

            // 401/403时尝试刷新Token重试一次
            if (statusCode == 401 || statusCode == 403)
            {
                _tracer.Trace("Token可能过期，尝试刷新后重试");
                try
                {
                    string newToken = _tokenManager.RefreshToken();
                    return RetryGet(url, newToken);
                }
                catch (Exception retryEx)
                {
                    _tracer.Trace($"重试失败: {retryEx.Message}");
                    throw new InvalidPluginExecutionException($"Coface API认证失败: {errorMsg}");
                }
            }

            throw new InvalidPluginExecutionException(errorMsg);
        }

        /// <summary>
        /// 使用新Token重试GET请求
        /// </summary>
        private JsonDocument RetryGet(string url, string token)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.ContentType = "application/json";
            request.Headers.Add("Authorization", $"Bearer {token}");
            request.Headers.Add("x-api-key", _config.ApiKey);
            request.Timeout = 30000;

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (StreamReader reader = new StreamReader(response.GetResponseStream()))
            {
                string responseText = reader.ReadToEnd();
                _tracer.Trace($"重试HTTP响应: Status={response.StatusCode}, Length={responseText.Length}");
                return JsonDocument.Parse(responseText);
            }
        }

        #endregion
    }
}
