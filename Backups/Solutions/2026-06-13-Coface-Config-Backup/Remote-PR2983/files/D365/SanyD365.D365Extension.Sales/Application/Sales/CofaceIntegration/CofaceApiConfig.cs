using System.Text.Json.Serialization;

namespace SanyD365.D365Extension.Sales.Application.Sales.CofaceIntegration
{
    /// <summary>
    /// Coface API configuration, loaded from ms_systemconfiguration (name = CofaceApiConfig)
    /// </summary>
    public class CofaceApiConfig
    {
        [JsonPropertyName("baseUrl")]
        public string BaseUrl { get; set; } = string.Empty;

        [JsonPropertyName("authUrl")]
        public string AuthUrl { get; set; } = string.Empty;

        [JsonPropertyName("apiKey")]
        public string ApiKey { get; set; } = string.Empty;

        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;

        [JsonPropertyName("password")]
        public string Password { get; set; } = string.Empty;
    }
}
