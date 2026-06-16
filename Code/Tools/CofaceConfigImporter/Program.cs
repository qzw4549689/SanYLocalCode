using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using System.Text.Json;
using System.Text.Json.Serialization;

class Program
{
    static string GetDefaultUrl() => Environment.GetEnvironmentVariable("D365_URL") ?? "https://dev1.crm5.dynamics.com";

    static async Task<ServiceClient> CreateServiceClientAsync(string url)
    {
        var appId = Environment.GetEnvironmentVariable("D365_APPID") ?? "51f81489-12ee-4a9e-aaae-a2591f45987d";
        var tenantId = Environment.GetEnvironmentVariable("D365_TENANTID");
        var clientSecret = Environment.GetEnvironmentVariable("D365_CLIENTSECRET");
        var username = Environment.GetEnvironmentVariable("D365_USERNAME");
        var password = Environment.GetEnvironmentVariable("D365_PASSWORD");

        // 1. ClientSecret
        if (!string.IsNullOrEmpty(clientSecret))
        {
            var cs = "AuthType=ClientSecret;" +
                $"Url={url};" +
                $"AppId={appId};" +
                $"ClientSecret={clientSecret};" +
                "MaxConnectionTimeout=00:05:00;";
            if (!string.IsNullOrEmpty(tenantId)) cs += $"TenantId={tenantId};";
            Console.WriteLine("认证方式: ClientSecret");
            return new ServiceClient(cs);
        }

        // 2. Username/Password
        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
        {
            var cs = "AuthType=OAuth;" +
                $"Url={url};" +
                $"AppId={appId};" +
                "RedirectUri=http://localhost;" +
                "LoginPrompt=Never;" +
                "MaxConnectionTimeout=00:05:00;" +
                $"Username={username};" +
                $"Password={password}";
            if (!string.IsNullOrEmpty(tenantId)) cs += $";TenantId={tenantId}";
            Console.WriteLine("认证方式: OAuth Username/Password");
            return new ServiceClient(cs);
        }

        // 3. Device Code Flow
        Console.WriteLine("认证方式: Device Code Flow（第一次需要浏览器登录）");
        return await CreateServiceClientWithDeviceCodeAsync(url, appId, tenantId);
    }

    static async Task<ServiceClient> CreateServiceClientWithDeviceCodeAsync(string url, string appId, string? tenantId)
    {
        var authority = string.IsNullOrEmpty(tenantId)
            ? "https://login.microsoftonline.com/common"
            : $"https://login.microsoftonline.com/{tenantId}";

        var app = PublicClientApplicationBuilder
            .Create(appId)
            .WithAuthority(authority)
            .WithRedirectUri("http://localhost")
            .Build();

        var cachePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "D365MetadataTool");
        Directory.CreateDirectory(cachePath);
        var storageProperties = new StorageCreationPropertiesBuilder(
            "msal_cache.dat",
            cachePath)
            .WithMacKeyChain("D365CofaceConfigImporter", "msal_cache")
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
            (instanceUri) => Task.FromResult(result.AccessToken),
            true,
            null);
    }

    static async Task Main(string[] args)
    {
        var url = GetDefaultUrl();

        // 测试 Coface 数据集成命令
        if (args.Length > 0 && args[0].ToLower() == "test-coface")
        {
            Console.WriteLine("=== Coface 数据集成测试工具 ===");
            Console.WriteLine($"目标环境: {url}\n");

            using var service = await CreateServiceClientAsync(url);
            if (!service.IsReady)
            {
                Console.WriteLine("❌ 连接失败");
                return;
            }
            Console.WriteLine($"✅ 连接成功! 用户: {service.OAuthUserId}\n");

            if (args.Length == 1 || args[1].ToLower() == "list")
            {
                TestCofaceSync.ListCandidates(service);
            }
            else if (args[1].ToLower() == "trigger" && args.Length > 2 && Guid.TryParse(args[2], out var recordId))
            {
                TestCofaceSync.Trigger(service, recordId);
            }
            else
            {
                Console.WriteLine("用法:");
                Console.WriteLine("  dotnet run test-coface list                  - 列出候选记录");
                Console.WriteLine("  dotnet run test-coface trigger <recordId>    - 触发指定记录的 Coface 数据集成");
            }
            return;
        }

        var jsonPath = args.Length > 0 ? args[0] : "coface_financial_indicators.json";
        var clean = args.Contains("--clean");

        Console.WriteLine("=== Coface 财务指标配置导入工具 ===");
        Console.WriteLine($"目标环境: {url}");
        Console.WriteLine($"数据文件: {Path.GetFullPath(jsonPath)}");
        Console.WriteLine();

        if (!File.Exists(jsonPath))
        {
            Console.WriteLine($"❌ 找不到数据文件: {jsonPath}");
            return;
        }

        var json = await File.ReadAllTextAsync(jsonPath);
        var countries = JsonSerializer.Deserialize<List<CountryConfig>>(json);
        if (countries == null || countries.Count == 0)
        {
            Console.WriteLine("❌ 数据文件为空或解析失败");
            return;
        }

        var records = new List<IndicatorRecord>();
        foreach (var c in countries)
        {
            foreach (var i in c.Indicators)
            {
                records.Add(new IndicatorRecord
                {
                    CountryCode = c.CountryCode,
                    CountryName = c.CountryName,
                    IndicatorName = i.Name,
                    TypeValue = i.TypeValue,
                    IndicatorType = i.IndicatorType,
                    Priority = i.Priority,
                    FormulaFallback = i.FormulaFallback
                });
            }
        }

        Console.WriteLine($"准备导入 {records.Count} 条配置记录（{countries.Count} 个国家）");
        Console.WriteLine();

        try
        {
            using var service = await CreateServiceClientAsync(url);
            if (!service.IsReady)
            {
                Console.WriteLine("❌ 连接失败");
                return;
            }
            Console.WriteLine($"✅ 连接成功! 用户: {service.OAuthUserId}\n");

            const string entityName = "mcs_coface_financial_indicator";

            if (clean)
            {
                Console.WriteLine("正在清理已有数据...");
                var query = new Microsoft.Xrm.Sdk.Query.QueryExpression(entityName)
                {
                    ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("mcs_coface_financial_indicatorid")
                };
                var existing = service.RetrieveMultiple(query);
                int deleted = 0;
                foreach (var e in existing.Entities)
                {
                    service.Delete(entityName, e.Id);
                    deleted++;
                }
                Console.WriteLine($"✅ 已删除 {deleted} 条旧记录\n");
            }

            int created = 0;
            int skipped = 0;
            foreach (var r in records)
            {
                var exists = CheckExists(service, entityName, r);
                if (exists)
                {
                    Console.WriteLine($"⚠️ 已存在，跳过: {r.CountryCode} / {r.IndicatorName} / {r.TypeValue}");
                    skipped++;
                    continue;
                }

                var entity = new Entity(entityName)
                {
                    ["mcs_countrycode"] = r.CountryCode,
                    ["mcs_countryname"] = r.CountryName,
                    ["mcs_indicatorname"] = r.IndicatorName,
                    ["mcs_typevalue"] = r.TypeValue,
                    ["mcs_indicatortype"] = new OptionSetValue(r.IndicatorType),
                    ["mcs_priority"] = r.Priority,
                    ["mcs_formulafallback"] = r.FormulaFallback,
                    ["mcs_isactive"] = true
                };

                service.Create(entity);
                Console.WriteLine($"✅ 创建: {r.CountryCode} / {r.IndicatorName} / {r.TypeValue}");
                created++;
            }

            Console.WriteLine();
            Console.WriteLine("=== 导入完成 ===");
            Console.WriteLine($"创建: {created}");
            Console.WriteLine($"跳过: {skipped}");
            Console.WriteLine($"总计: {created + skipped}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 导入失败: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

    static bool CheckExists(ServiceClient service, string entityName, IndicatorRecord r)
    {
        var query = new Microsoft.Xrm.Sdk.Query.QueryExpression(entityName)
        {
            ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("mcs_coface_financial_indicatorid"),
            Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression
            {
                Conditions =
                {
                    new Microsoft.Xrm.Sdk.Query.ConditionExpression("mcs_countrycode", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, r.CountryCode),
                    new Microsoft.Xrm.Sdk.Query.ConditionExpression("mcs_indicatorname", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, r.IndicatorName),
                    new Microsoft.Xrm.Sdk.Query.ConditionExpression("mcs_typevalue", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, r.TypeValue)
                }
            }
        };
        var result = service.RetrieveMultiple(query);
        return result.Entities.Count > 0;
    }
}

public class CountryConfig
{
    [JsonPropertyName("countryCode")]
    public string CountryCode { get; set; } = "";

    [JsonPropertyName("countryName")]
    public string CountryName { get; set; } = "";

    [JsonPropertyName("indicators")]
    public List<IndicatorConfig> Indicators { get; set; } = new();
}

public class IndicatorConfig
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("typeValue")]
    public string TypeValue { get; set; } = "";

    [JsonPropertyName("indicatorType")]
    public int IndicatorType { get; set; }

    [JsonPropertyName("priority")]
    public int Priority { get; set; }

    [JsonPropertyName("formulaFallback")]
    public string FormulaFallback { get; set; } = "";
}

public class IndicatorRecord
{
    public string CountryCode { get; set; } = "";
    public string CountryName { get; set; } = "";
    public string IndicatorName { get; set; } = "";
    public string TypeValue { get; set; } = "";
    public int IndicatorType { get; set; }
    public int Priority { get; set; }
    public string FormulaFallback { get; set; } = "";
}
