using System.Text.Json.Serialization;

namespace SanyD365.Plugins.CofaceIntegration
{
    /// <summary>
    /// Coface API 配置
    /// 从 ms_systemconfiguration 实体读取，配置名为 CofaceApiConfig
    /// </summary>
    public class CofaceApiConfig
    {
        /// <summary>
        /// 数据 API 基地址，如 https://icon-api-test.coface.com/dataapi-v1
        /// </summary>
        [JsonPropertyName("baseUrl")]
        public string BaseUrl { get; set; } = string.Empty;

        /// <summary>
        /// 认证接口地址，如 https://api.coface.com/authentication/v1/token
        /// </summary>
        [JsonPropertyName("authUrl")]
        public string AuthUrl { get; set; } = string.Empty;

        /// <summary>
        /// Coface API Key
        /// </summary>
        [JsonPropertyName("apiKey")]
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Coface 用户名
        /// </summary>
        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Coface 密码
        /// </summary>
        [JsonPropertyName("password")]
        public string Password { get; set; } = string.Empty;
    }
}
