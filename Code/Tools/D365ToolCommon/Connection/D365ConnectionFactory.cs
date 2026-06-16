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
        /// 默认目标 URL，可通过 D365_URL 环境变量覆盖。
        /// </summary>
        public static string DefaultUrl => Environment.GetEnvironmentVariable("D365_URL") ?? "https://dev1.crm5.dynamics.com";

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
        /// 使用 Device Code Flow 创建 ServiceClient，支持持久化 token 缓存。
        /// </summary>
        public static async Task<ServiceClient> CreateWithDeviceCodeAsync(string url, string appId, string? tenantId)
        {
            var authority = string.IsNullOrEmpty(tenantId)
                ? "https://login.microsoftonline.com/common"
                : $"https://login.microsoftonline.com/{tenantId}";

            var app = PublicClientApplicationBuilder
                .Create(appId)
                .WithAuthority(authority)
                .WithRedirectUri("http://localhost")
                .Build();

            // 保持与原有工具（MetadataTool/DeployTool/CofaceConfigImporter）共享 token 缓存
            var cachePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "D365MetadataTool");
            Directory.CreateDirectory(cachePath);

            var storageProperties = new StorageCreationPropertiesBuilder(
                "msal_cache.dat",
                cachePath)
                .WithMacKeyChain("D365MetadataTool", "msal_cache")
                .Build();

            var cacheHelper = await MsalCacheHelper.CreateAsync(storageProperties);
            cacheHelper.RegisterCache(app.UserTokenCache);

            var scopes = new[] { $"{url}/.default" };

            AuthenticationResult result;
            var accounts = await app.GetAccountsAsync();
            try
            {
                result = await app.AcquireTokenSilent(scopes, accounts.FirstOrDefault())
                    .ExecuteAsync();
                Console.WriteLine("✅ 使用缓存的 token 登录");
            }
            catch (MsalUiRequiredException)
            {
                result = await app.AcquireTokenWithDeviceCode(scopes, deviceCodeResult =>
                {
                    Console.WriteLine(deviceCodeResult.Message);
                    return Task.FromResult(0);
                }).ExecuteAsync();
                Console.WriteLine("✅ Device Code 登录成功");
            }

            var serviceUri = new Uri(url);
            return new ServiceClient(
                serviceUri,
                _ => Task.FromResult(result.AccessToken),
                true,
                null);
        }
    }
}
