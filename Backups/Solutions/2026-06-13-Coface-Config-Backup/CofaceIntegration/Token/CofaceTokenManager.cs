using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;

namespace SanyD365.Plugins.CofaceIntegration.Token
{
    /// <summary>
    /// Coface Token管理器
    /// 负责Token的获取、缓存和自动刷新
    /// Token有效期3600秒，存储在D365系统配置实体中
    /// </summary>
    public class CofaceTokenManager
    {
        private readonly IOrganizationService _service;
        private readonly ITracingService _tracer;
        private readonly CofaceApiConfig _config;

        private const int TOKEN_EXPIRY_SECONDS = 3600;
        private const int REFRESH_BUFFER_SECONDS = 300; // 提前5分钟刷新

        // D365配置实体信息
        private const string CONFIG_ENTITY = "ms_systemconfiguration"; // 系统配置实体
        private const string CONFIG_TOKEN = "coface_idtoken";
        private const string CONFIG_EXPIRY = "coface_token_expiry";

        public CofaceTokenManager(IOrganizationService service, ITracingService tracer)
        {
            _service = service;
            _tracer = tracer;
            _config = CofaceConfigHelper.GetConfig(service);
        }

        /// <summary>
        /// 获取有效Token（每次重新获取）
        /// </summary>
        public string GetValidToken()
        {
            _tracer.Trace("CofaceTokenManager.GetValidToken 开始");
            _tracer.Trace("每次重新获取Token（不缓存）");
            return RefreshToken();
        }

        /// <summary>
        /// 强制刷新Token
        /// </summary>
        public string RefreshToken()
        {
            _tracer.Trace("CofaceTokenManager.RefreshToken 开始");

            try
            {
                var request = (HttpWebRequest)WebRequest.Create(_config.AuthUrl);
                request.Method = "POST";
                request.ContentType = "application/json";
                request.Headers.Add("x-api-key", _config.ApiKey);

                var body = new
                {
                    username = _config.Username,
                    password = _config.Password,
                    grant_type = "password"
                };

                string jsonBody = JsonSerializer.Serialize(body);
                byte[] byteArray = Encoding.UTF8.GetBytes(jsonBody);
                request.ContentLength = byteArray.Length;

                using (var stream = request.GetRequestStream())
                {
                    stream.Write(byteArray, 0, byteArray.Length);
                }

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    string responseText = reader.ReadToEnd();
                    _tracer.Trace($"认证响应: {responseText.Substring(0, Math.Min(200, responseText.Length))}...");

                    using (var doc = JsonDocument.Parse(responseText))
                    {
                        var root = doc.RootElement;
                        string idToken = root.GetProperty("idToken").GetString();
                        string accessToken = root.GetProperty("accessToken").GetString();

                        _tracer.Trace($"获取Token成功, idToken长度: {idToken?.Length}");

                        return idToken;
                    }
                }
            }
            catch (WebException ex)
            {
                string errorMsg = $"获取Token失败: {ex.Message}";
                if (ex.Response != null)
                {
                    using (var reader = new StreamReader(ex.Response.GetResponseStream()))
                    {
                        errorMsg += $", 响应: {reader.ReadToEnd()}";
                    }
                }
                _tracer.Trace(errorMsg);
                throw new InvalidPluginExecutionException(errorMsg);
            }
        }


    }
}
