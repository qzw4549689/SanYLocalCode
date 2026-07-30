using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CofaceApiTest
{
    /// <summary>
    /// Coface 测试配置。从应用目录下的 appsettings.json 读取，
    /// 文件缺失或字段为空时回退到默认值（测试沙盒地址）。
    /// </summary>
    public class CofaceTestConfig
    {
        private const string DefaultBaseUrl = "https://icon-api-test.coface.com/dataapi-v1";
        private const string DefaultAuthUrl = "https://api.coface.com/authentication/v1/token";

        /// <summary>
        /// 数据 API 基地址
        /// </summary>
        [JsonPropertyName("baseUrl")]
        public string BaseUrl { get; set; } = DefaultBaseUrl;

        /// <summary>
        /// 认证接口地址
        /// </summary>
        [JsonPropertyName("authUrl")]
        public string AuthUrl { get; set; } = DefaultAuthUrl;

        private class ConfigRoot
        {
            [JsonPropertyName("coface")]
            public CofaceTestConfig Coface { get; set; }
        }

        /// <summary>
        /// 加载配置：appsettings.json → 默认值
        /// </summary>
        public static CofaceTestConfig Load()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(path))
            {
                Console.WriteLine($"[配置] 未找到 {path}，使用默认 Coface 测试地址");
                return new CofaceTestConfig();
            }

            try
            {
                var json = File.ReadAllText(path);
                var root = JsonSerializer.Deserialize<ConfigRoot>(json);
                var config = root?.Coface ?? new CofaceTestConfig();

                if (string.IsNullOrWhiteSpace(config.BaseUrl)) config.BaseUrl = DefaultBaseUrl;
                if (string.IsNullOrWhiteSpace(config.AuthUrl)) config.AuthUrl = DefaultAuthUrl;

                Console.WriteLine($"[配置] BaseUrl={config.BaseUrl}");
                Console.WriteLine($"[配置] AuthUrl={config.AuthUrl}");
                return config;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[配置] 读取 {path} 失败: {ex.Message}，使用默认 Coface 测试地址");
                return new CofaceTestConfig();
            }
        }
    }
}
