using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.IO;
using System.Linq;
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
        // 可选：系统身份服务，用于回写缓存 Token（普通业务员对配置实体可能无写权限）
        private readonly IOrganizationService _systemService;

        private const int TOKEN_EXPIRY_SECONDS = 3600;
        private const int REFRESH_BUFFER_SECONDS = 300; // 提前5分钟刷新

        // D365配置实体信息
        private const string CONFIG_ENTITY = "ms_systemconfiguration"; // 系统配置实体
        private const string CONFIG_TOKEN = "coface_idtoken";
        private const string CONFIG_EXPIRY = "coface_token_expiry";

        public CofaceTokenManager(IOrganizationService service, ITracingService tracer, IOrganizationService systemService = null)
        {
            _service = service;
            _tracer = tracer;
            _systemService = systemService;
            _config = CofaceConfigHelper.GetConfig(service);
        }

        /// <summary>
        /// 获取有效Token（优先读缓存，缓存失效/不可用时重新认证）
        /// </summary>
        public string GetValidToken()
        {
            _tracer.Trace("CofaceTokenManager.GetValidToken 开始");

            string cachedToken = TryGetCachedToken();
            if (!string.IsNullOrEmpty(cachedToken))
            {
                _tracer.Trace("使用缓存 Token（未过期）");
                return cachedToken;
            }

            string newToken = RefreshToken();
            TryCacheToken(newToken);
            return newToken;
        }

        /// <summary>
        /// 尝试从 ms_systemconfiguration（CofaceApiConfig 记录）读取缓存 Token
        /// 字段不存在 / 已过期 / 读取失败时返回 null，静默降级为重新认证
        /// </summary>
        private string TryGetCachedToken()
        {
            try
            {
                var query = new QueryExpression(CONFIG_ENTITY)
                {
                    ColumnSet = new ColumnSet(CONFIG_TOKEN, CONFIG_EXPIRY)
                };
                query.Criteria.AddCondition(CofaceConfigHelper.ConfigNameField, ConditionOperator.Equal, CofaceConfigHelper.ConfigName);

                var entity = _service.RetrieveMultiple(query).Entities.FirstOrDefault();
                if (entity == null)
                {
                    return null;
                }

                string token = entity.GetAttributeValue<string>(CONFIG_TOKEN);
                if (string.IsNullOrEmpty(token))
                {
                    return null;
                }

                // 过期时间字段兼容 DateTime / string 两种存储形式
                DateTime? expiry = null;
                if (entity.Contains(CONFIG_EXPIRY))
                {
                    var raw = entity[CONFIG_EXPIRY];
                    if (raw is DateTime dt)
                    {
                        expiry = dt.ToUniversalTime();
                    }
                    else if (raw is string s && DateTime.TryParse(s, out var parsed))
                    {
                        expiry = parsed.ToUniversalTime();
                    }
                }

                if (!expiry.HasValue)
                {
                    _tracer.Trace("缓存 Token 无过期时间，忽略缓存");
                    return null;
                }

                if (expiry.Value > DateTime.UtcNow.AddSeconds(REFRESH_BUFFER_SECONDS))
                {
                    return token;
                }

                _tracer.Trace($"缓存 Token 已过期（expiry={expiry.Value:u}），重新认证");
                return null;
            }
            catch (Exception ex)
            {
                // 字段不存在或权限不足等情况：仅记录，降级为每次重新认证
                _tracer.Trace($"读取缓存 Token 失败（降级为重新认证）: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 尝试将新 Token 回写到配置记录（失败仅记录不阻断）
        /// 无系统身份服务时跳过（退化为不缓存）
        /// </summary>
        private void TryCacheToken(string token)
        {
            if (_systemService == null)
            {
                _tracer.Trace("无系统身份服务，跳过 Token 缓存回写");
                return;
            }

            try
            {
                var query = new QueryExpression(CONFIG_ENTITY)
                {
                    ColumnSet = new ColumnSet()
                };
                query.Criteria.AddCondition(CofaceConfigHelper.ConfigNameField, ConditionOperator.Equal, CofaceConfigHelper.ConfigName);

                var entity = _systemService.RetrieveMultiple(query).Entities.FirstOrDefault();
                if (entity == null)
                {
                    return;
                }

                var update = new Entity(CONFIG_ENTITY, entity.Id);
                update[CONFIG_TOKEN] = token;
                update[CONFIG_EXPIRY] = DateTime.UtcNow.AddSeconds(TOKEN_EXPIRY_SECONDS);
                _systemService.Update(update);
                _tracer.Trace("Token 缓存回写成功");
            }
            catch (Exception ex)
            {
                // 回写失败不影响主流程（下次仍重新认证）
                _tracer.Trace($"Token 缓存回写失败（不阻断）: {ex.Message}");
            }
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
