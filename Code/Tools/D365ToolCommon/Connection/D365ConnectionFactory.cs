using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;
using Microsoft.PowerPlatform.Dataverse.Client;

namespace D365ToolCommon.Connection
{
    /// <summary>
    /// D365 连接工厂，统一处理 ClientSecret / OAuth / DeviceCode 三种认证方式。
    /// 认证优先级：ClientSecret > Username/Password > Device Code Flow
    /// </summary>
    public static class D365ConnectionFactory
    {
        /// <summary>
        /// 默认应用 ID（D365 官方示例 AppId）。
        /// 可通过 D365_APPID 环境变量覆盖。
        /// </summary>
        public static string DefaultAppId => Environment.GetEnvironmentVariable("D365_APPID") ?? "51f81489-12ee-4a9e-aaae-a2591f45987d";

        /// <summary>
        /// 默认目标 URL。解析优先级：D365_URL 环境变量 > D365_ENV 命名环境（查 environments.json）> 默认 dev1。
        /// 默认环境固定为 dev1，绝不默认指向生产。
        /// </summary>
        public static string DefaultUrl => ResolveUrl();

        /// <summary>
        /// 解析当前目标环境 URL：D365_URL > D365_ENV（命名环境）> dev1 默认。
        /// </summary>
        public static string ResolveUrl()
        {
            var explicitUrl = Environment.GetEnvironmentVariable("D365_URL");
            if (!string.IsNullOrWhiteSpace(explicitUrl))
                return explicitUrl.TrimEnd('/');

            var envName = Environment.GetEnvironmentVariable("D365_ENV");
            if (!string.IsNullOrWhiteSpace(envName))
            {
                var url = LookupEnvironmentUrl(envName.Trim());
                if (!string.IsNullOrWhiteSpace(url))
                    return url.TrimEnd('/');
                Console.WriteLine($"⚠️ D365_ENV={envName} 未在 environments.json 配置有效 URL，回退默认 dev1");
            }

            return "https://dev1.crm5.dynamics.com";
        }

        /// <summary>
        /// 从 environments.json 查询命名环境的 URL。未配置或 URL 为空时返回 null。
        /// </summary>
        public static string? LookupEnvironmentUrl(string envName)
        {
            var file = FindEnvironmentsFile();
            if (file == null) return null;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(file));
                if (doc.RootElement.TryGetProperty(envName, out var env) &&
                    env.TryGetProperty("url", out var urlProp))
                {
                    var url = urlProp.GetString();
                    return string.IsNullOrWhiteSpace(url) ? null : url;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 读取 environments.json 失败：{ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// 定位 environments.json：D365_ENV_FILE 指定 > 程序输出目录 > 从当前目录向上找 Code/Tools/environments.json。
        /// </summary>
        private static string? FindEnvironmentsFile()
        {
            var specified = Environment.GetEnvironmentVariable("D365_ENV_FILE");
            if (!string.IsNullOrWhiteSpace(specified) && File.Exists(specified))
                return specified;

            var besideDll = Path.Combine(AppContext.BaseDirectory, "environments.json");
            if (File.Exists(besideDll))
                return besideDll;

            var dir = new DirectoryInfo(Environment.CurrentDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "Code", "Tools", "environments.json");
                if (File.Exists(candidate))
                    return candidate;
                var candidate2 = Path.Combine(dir.FullName, "environments.json");
                if (File.Exists(candidate2))
                    return candidate2;
                dir = dir.Parent;
            }
            return null;
        }

        /// <summary>
        /// 构建同步连接字符串（ClientSecret 或 Username/Password）。
        /// 如果连这两种环境变量都没有，则抛出异常。
        /// </summary>
        public static string BuildConnectionString(string url, string? appId = null, string? tenantId = null)
        {
            appId ??= DefaultAppId;
            tenantId ??= Environment.GetEnvironmentVariable("D365_TENANTID");
            var clientSecret = Environment.GetEnvironmentVariable("D365_CLIENTSECRET");
            var username = Environment.GetEnvironmentVariable("D365_USERNAME");
            var password = Environment.GetEnvironmentVariable("D365_PASSWORD");

            if (!string.IsNullOrEmpty(clientSecret))
            {
                var cs = $"AuthType=ClientSecret;Url={url};AppId={appId};ClientSecret={clientSecret};MaxConnectionTimeout=00:05:00;";
                if (!string.IsNullOrEmpty(tenantId)) cs += $"TenantId={tenantId};";
                return cs;
            }

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                var cs = $"AuthType=OAuth;Url={url};AppId={appId};RedirectUri=http://localhost;LoginPrompt=Never;MaxConnectionTimeout=00:05:00;Username={username};Password={password}";
                if (!string.IsNullOrEmpty(tenantId)) cs += $";TenantId={tenantId}";
                return cs;
            }

            throw new InvalidOperationException("同步连接需要配置 D365_CLIENTSECRET 或 D365_USERNAME/D365_PASSWORD 环境变量。");
        }

        /// <summary>
        /// 创建并返回已就绪的 ServiceClient。
        /// </summary>
        public static async Task<ServiceClient> CreateAsync(string? url = null, string? appId = null, string? tenantId = null)
        {
            url ??= DefaultUrl;
            appId ??= DefaultAppId;
            tenantId ??= Environment.GetEnvironmentVariable("D365_TENANTID");

            var clientSecret = Environment.GetEnvironmentVariable("D365_CLIENTSECRET");
            var username = Environment.GetEnvironmentVariable("D365_USERNAME");
            var password = Environment.GetEnvironmentVariable("D365_PASSWORD");

            // 1. 优先 ClientSecret
            if (!string.IsNullOrEmpty(clientSecret))
            {
                var cs = $"AuthType=ClientSecret;Url={url};AppId={appId};ClientSecret={clientSecret};MaxConnectionTimeout=00:05:00;";
                if (!string.IsNullOrEmpty(tenantId)) cs += $"TenantId={tenantId};";
                Console.WriteLine("认证方式: ClientSecret");
                return new ServiceClient(cs);
            }

            // 2. 其次 Username/Password
            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                var cs = $"AuthType=OAuth;Url={url};AppId={appId};RedirectUri=http://localhost;LoginPrompt=Never;MaxConnectionTimeout=00:05:00;Username={username};Password={password}";
                if (!string.IsNullOrEmpty(tenantId)) cs += $";TenantId={tenantId}";
                Console.WriteLine("认证方式: OAuth Username/Password");
                return new ServiceClient(cs);
            }

            // 3. 最后 Device Code Flow
            Console.WriteLine("认证方式: Device Code Flow（第一次需要浏览器登录）");
            return await CreateWithDeviceCodeAsync(url, appId, tenantId);
        }

        /// <summary>
        /// 使用 Device Code Flow 获取 access token，支持持久化 token 缓存。
        /// 供 Web API 调用或需要显式 token 的场景使用。
        /// </summary>
        public static async Task<string> GetAccessTokenAsync(string? url = null, string? appId = null, string? tenantId = null)
        {
            url ??= DefaultUrl;
            appId ??= DefaultAppId;
            tenantId ??= Environment.GetEnvironmentVariable("D365_TENANTID");

            var authority = string.IsNullOrEmpty(tenantId)
                ? "https://login.microsoftonline.com/common"
                : $"https://login.microsoftonline.com/{tenantId}";

            var app = PublicClientApplicationBuilder
                .Create(appId)
                .WithAuthority(authority)
                .WithRedirectUri("http://localhost")
                .Build();

            // token 缓存按目标环境隔离（文件名带 host），不同环境/账号互不覆盖、互不顶号
            var host = new Uri(url).Host;
            var cachePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "D365MetadataTool");
            Directory.CreateDirectory(cachePath);

            var storageProperties = new StorageCreationPropertiesBuilder(
                $"msal_cache_{host}.dat",
                cachePath)
                .WithMacKeyChain("D365MetadataTool", $"msal_cache_{host}")
                .Build();

            var cacheHelper = await MsalCacheHelper.CreateAsync(storageProperties);
            cacheHelper.RegisterCache(app.UserTokenCache);

            var scopes = new[] { $"{url}/.default" };

            AuthenticationResult? result = null;
            var accounts = await app.GetAccountsAsync();
            // 多账号时逐个尝试静默取 token，避免固定取第一个账号导致拿错/失败
            foreach (var account in accounts)
            {
                try
                {
                    result = await app.AcquireTokenSilent(scopes, account).ExecuteAsync();
                    Console.WriteLine($"✅ 使用缓存的 token 登录（{account.Username}）");
                    break;
                }
                catch (MsalUiRequiredException)
                {
                    // 该账号缓存失效，尝试下一个
                }
            }

            if (result == null)
            {
                result = await app.AcquireTokenWithDeviceCode(scopes, deviceCodeResult =>
                {
                    Console.WriteLine(deviceCodeResult.Message);
                    return Task.FromResult(0);
                }).ExecuteAsync();
                Console.WriteLine("✅ Device Code 登录成功");
            }

            return result.AccessToken;
        }

        /// <summary>
        /// 使用 Device Code Flow 创建 ServiceClient，支持持久化 token 缓存。
        /// </summary>
        public static async Task<ServiceClient> CreateWithDeviceCodeAsync(string url, string appId, string? tenantId)
        {
            var accessToken = await GetAccessTokenAsync(url, appId, tenantId);
            var serviceUri = new Uri(url);
            // Device Code Flow 创建的 ServiceClient 默认超时较短，上传大 Assembly / 导出大 Solution 时需要延长
            ServiceClient.MaxConnectionTimeout = TimeSpan.FromMinutes(30);
            return new ServiceClient(
                serviceUri,
                _ => Task.FromResult(accessToken),
                true,
                null);
        }
    }
}
