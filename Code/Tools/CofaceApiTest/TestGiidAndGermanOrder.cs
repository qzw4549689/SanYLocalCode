using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CofaceApiTest
{
    /// <summary>
    /// 2026-08-27 验证 Coface 邮件提供的 demo 公司（主题：giid搜索以及德国公司下单测试）：
    /// Part1 giid 二次搜索：首查（companyName+countryCode）只返回 giid 无 icon → 用 giid 二次检索应取到邮件给定的 icon；
    /// Part2 德国下单 legitimateInterest：不传应报错 → 传 102 应下单成功（URBA 监控单 + Report 单）。
    /// 用法: dotnet run giid-order
    /// </summary>
    class TestGiidAndGermanOrder
    {
        static readonly string API_KEY = "0vneRg8vLjzPQlIfSkzO8kIDg04kfaKafTzg5sX1";
        static CofaceTestConfig _config = CofaceTestConfig.Load();
        static string AUTH_URL => _config.AuthUrl;
        static string DATA_URL => _config.BaseUrl;

        // 邮件给定的 giid 测试公司
        static readonly (string Country, string Name, string Giid, string ExpectedIcon)[] GiidCompanies = new[]
        {
            ("FR", "ABB SAS", "giid#33313532323834323940706e703d46522f373039", "icon#127701293"),
            ("DE", "Lear Corporation GmbH", "giid#303631393631393031323330363440706e703d44452f373238", "icon#127687874"),
            ("DE", "Implenia Hochbau GmbH", "giid#303230313230313036333434323840706e703d44452f373238", "icon#127691738"),
        };

        // 邮件给定的德国下单测试公司
        const string DeIconForUrba = "icon#127687875";
        const string DeIconForReportDefault = "icon#127701278";
        static string DeIconForReport = DeIconForReportDefault;

        static int _pass = 0, _fail = 0;

        public static async Task Run(string[] args)
        {
            string idToken = await GetToken();
            if (string.IsNullOrEmpty(idToken)) { Console.WriteLine("❌ Token 获取失败"); return; }
            Console.WriteLine("✅ Token 获取成功\n");

            // report-only：仅跑 Report 单验证（先补 URBA 监控单前提），用于 URBA 用例已通过后的续跑
            // report-retry：监控单已下过，查状态后直接重试 Report 单
            bool reportOnly = args.Length > 1 && args[1].Equals("report-only", StringComparison.OrdinalIgnoreCase);
            bool reportRetry = args.Length > 1 && args[1].Equals("report-retry", StringComparison.OrdinalIgnoreCase);
            // 可用第三个参数指定 Report 测试公司 icon（默认 icon#127701278）
            if ((reportOnly || reportRetry) && args.Length > 2) DeIconForReport = args[2];

            if (!reportOnly && !reportRetry)
            {
                await Part1GiidSearch(idToken);
                await Part2GermanUrbaOrder(idToken);
            }
            await Part3GermanReportOrder(idToken, skipUrbaPrerequisite: reportRetry);

            Console.WriteLine($"\n===== 汇总: 通过 {_pass} / 失败 {_fail} =====");
        }

        // ---------- Part1: giid 二次搜索 ----------
        static async Task Part1GiidSearch(string token)
        {
            Console.WriteLine("########## Part1: giid 二次搜索验证 ##########");
            foreach (var (country, name, giid, expectedIcon) in GiidCompanies)
            {
                Console.WriteLine($"\n--- {name} ({country}) ---");

                // Step A: 首查 companyName+countryCode，确认只有 giid 没有 icon
                var urlA = $"{DATA_URL}/companies?companyName={Uri.EscapeDataString(name)}&countryCode={country}";
                var (okA, bodyA) = await CallGet(urlA, token);
                if (!okA) { Fail($"首查失败: {bodyA}"); continue; }

                var firstHit = ParseFirstCompany(bodyA);
                if (firstHit == null) { Fail("首查无结果"); continue; }
                var (firstName, externals) = firstHit.Value;
                string iconInFirst = FindExternal(externals, "icon");
                string giidInFirst = FindExternal(externals, "giid");
                Console.WriteLine($"首查命中: {firstName}");
                Console.WriteLine($"首查 externalIds: {string.Join(", ", externals)}");
                Check(iconInFirst == null, "首查无 icon（符合 Cathy 描述）", $"首查意外返回 icon={iconInFirst}");
                Check(giidInFirst != null, $"首查返回 giid={giidInFirst}", "首查未返回 giid");
                Check(giidInFirst == giid, "首查 giid 与邮件一致", $"首查 giid={giidInFirst} 与邮件 {giid} 不一致（以接口返回为准）");

                // Step B: 用 giid 二次检索（与生产插件一致：用首查返回的 giid），应取到邮件给定的 icon
                await Task.Delay(1000); // 限流保护
                string giidForSearch = giidInFirst ?? giid;
                var urlB = $"{DATA_URL}/companies?countryCode={country}&externalId={Uri.EscapeDataString(giidForSearch)}";
                var (okB, bodyB) = await CallGet(urlB, token);
                if (!okB) { Fail($"giid 二次检索失败: {bodyB}"); continue; }

                var secondHit = ParseFirstCompany(bodyB);
                if (secondHit == null) { Fail("giid 二次检索无结果"); continue; }
                string iconInSecond = FindExternal(secondHit.Value.Externals, "icon");
                Console.WriteLine($"二次检索命中: {secondHit.Value.Name}, icon={iconInSecond ?? "(无)"}");
                Check(iconInSecond == expectedIcon, $"二次检索取到 icon={iconInSecond}（与邮件一致）",
                    $"二次检索 icon={iconInSecond ?? "(无)"}，期望 {expectedIcon}");

                await Task.Delay(1000);
            }
        }

        // ---------- Part2: 德国 URBA 监控单 legitimateInterest ----------
        static async Task Part2GermanUrbaOrder(string token)
        {
            Console.WriteLine("\n########## Part2: 德国 URBA 监控单 legitimateInterest 验证 ##########");

            // 2a URBA 监控单：不传 legitimateInterest → 预期报错
            Console.WriteLine($"\n--- 2a URBA 监控单（{DeIconForUrba}，不传 legitimateInterest，预期失败）---");
            var urbaBodyNoLI = new Dictionary<string, object>
            {
                ["externalId"] = DeIconForUrba,
                ["countryCode"] = "DE",
                ["customerReference"] = "SANY-DEMO-20260827-NOLI"
            };
            var (ok2a, resp2a) = await CallPost($"{DATA_URL}/urba360/monitorings/orders", urbaBodyNoLI, token);
            Check(!ok2a, $"不带 legitimateInterest 下单被拒（符合预期）: {Truncate(resp2a)}",
                $"不带 legitimateInterest 下单意外成功: {Truncate(resp2a)}");

            await Task.Delay(2000);

            // 2b URBA 监控单：传 legitimateInterest=102 → 预期成功
            Console.WriteLine($"\n--- 2b URBA 监控单（{DeIconForUrba}，legitimateInterest=102，预期成功）---");
            var urbaBody = new Dictionary<string, object>
            {
                ["externalId"] = DeIconForUrba,
                ["countryCode"] = "DE",
                ["legitimateInterest"] = "102",
                ["customerReference"] = "SANY-DEMO-20260827"
            };
            var (ok2b, resp2b) = await CallPost($"{DATA_URL}/urba360/monitorings/orders", urbaBody, token);
            Check(ok2b, $"URBA 监控单下单成功: {Truncate(resp2b)}", $"URBA 监控单下单失败: {Truncate(resp2b)}");
        }

        // ---------- Part3: 德国 Report 单（前提：该公司须已有 URBA360 监控包） ----------
        static async Task Part3GermanReportOrder(string token, bool skipUrbaPrerequisite = false)
        {
            Console.WriteLine("\n########## Part3: 德国 Report 单 legitimateInterest 验证 ##########");

            if (!skipUrbaPrerequisite)
            {
                // 3a 前提：先为该公司下 URBA 监控单（Coface 要求 Report 下单前须有 URBA360 包）
                Console.WriteLine($"\n--- 3a URBA 监控单（{DeIconForReport}，legitimateInterest=102，Report 前提）---");
                var urbaBody = new Dictionary<string, object>
                {
                    ["externalId"] = DeIconForReport,
                    ["countryCode"] = "DE",
                    ["legitimateInterest"] = "102",
                    ["customerReference"] = "SANY-DEMO-20260827"
                };
                var (ok3a, resp3a) = await CallPost($"{DATA_URL}/urba360/monitorings/orders", urbaBody, token);
                Check(ok3a, $"URBA 监控单下单成功: {Truncate(resp3a)}", $"URBA 监控单下单失败: {Truncate(resp3a)}");

                await Task.Delay(2000);
            }
            else
            {
                // 重试模式：先查该公司 URBA 监控订单状态
                Console.WriteLine($"\n--- 查询 {DeIconForReport} URBA 监控订单状态 ---");
                var (okQ, respQ) = await CallGet($"{DATA_URL}/urba360/monitorings/orders?externalId={Uri.EscapeDataString(DeIconForReport)}&countryCode=DE", token);
                Console.WriteLine($"订单响应（前 600 字符）: {(respQ ?? "").Substring(0, Math.Min(600, (respQ ?? "").Length))}");
            }

            // 3b Report 单：传 legitimateInterest=102 → 预期成功（DE 非受限国，customized-report/301）
            Console.WriteLine($"\n--- 3b Report 单（{DeIconForReport}，customized-report/301，legitimateInterest=102，预期成功）---");
            var reportBody = new Dictionary<string, object>
            {
                ["externalId"] = DeIconForReport,
                ["countryCode"] = "DE",
                ["legitimateInterest"] = "102",
                ["customerReference"] = "SANY-DEMO-20260827",
                ["report"] = new Dictionary<string, object>
                {
                    ["slug"] = "customized-report",
                    ["customReportId"] = 301,
                    ["format"] = new[] { "json", "pdf" },
                    ["language"] = "en"
                }
            };
            var (ok2c, resp2c) = await CallPost($"{DATA_URL}/publications/orders", reportBody, token);
            Check(ok2c, $"Report 单下单成功: {Truncate(resp2c)}", $"Report 单下单失败: {Truncate(resp2c)}");
        }
        // ----------  helpers ----------
        static void Check(bool condition, string passMsg, string failMsg)
        {
            if (condition) { _pass++; Console.WriteLine($"  ✅ {passMsg}"); }
            else { _fail++; Console.WriteLine($"  ❌ {failMsg}"); }
        }

        static void Fail(string msg) { _fail++; Console.WriteLine($"  ❌ {msg}"); }

        static string Truncate(string s) => string.IsNullOrEmpty(s) ? "(空)" : s.Substring(0, Math.Min(300, s.Length));

        /// <summary>解析 /companies 响应（数组或单对象）第一条，返回名称 + externalId 列表（"slug#id" 形式）</summary>
        static (string Name, List<string> Externals)? ParseFirstCompany(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                JsonElement company = root.ValueKind == JsonValueKind.Array
                    ? (root.GetArrayLength() > 0 ? root[0] : default)
                    : root;
                if (company.ValueKind == JsonValueKind.Undefined) return null;

                string name = company.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String
                    ? n.GetString()
                    : company.TryGetProperty("internationalName", out var in2) ? in2.GetString() : "(未知)";

                var externals = new List<string>();
                if (company.TryGetProperty("externalIds", out var extArr) && extArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var ext in extArr.EnumerateArray())
                    {
                        var slug = ext.TryGetProperty("repositorySlug", out var s) ? s.GetString() : null;
                        var id = ext.TryGetProperty("id", out var i) ? i.GetString() : null;
                        if (slug != null && id != null) externals.Add($"{slug}#{id}");
                    }
                }
                return (name, externals);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  解析响应异常: {ex.Message}");
                return null;
            }
        }

        static string FindExternal(List<string> externals, string slug)
        {
            var prefix = slug + "#";
            foreach (var e in externals)
                if (e.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return e;
            return null;
        }

        static async Task<string> GetToken()
        {
            using var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Post, AUTH_URL);
            request.Headers.Add("x-api-key", API_KEY);
            request.Content = new StringContent(
                JsonSerializer.Serialize(new { username = "tangys12@sany.com.cn", password = "1qaz!QAZ", grant_type = "password" }),
                Encoding.UTF8, "application/json"
            );
            var response = await client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) { Console.WriteLine($"认证失败: {response.StatusCode}, {content}"); return null; }
            var tokenData = JsonSerializer.Deserialize<JsonElement>(content);
            return tokenData.TryGetProperty("idToken", out var p) ? p.GetString() : null;
        }

        static async Task<(bool Ok, string Body)> CallGet(string url, string token)
        {
            using var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("x-api-key", API_KEY);
            var response = await client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"  GET {url}\n  HTTP {(int)response.StatusCode}");
            return (response.IsSuccessStatusCode, content);
        }

        static async Task<(bool Ok, string Body)> CallPost(string url, object body, string token)
        {
            using var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("x-api-key", API_KEY);
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            var response = await client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"  POST {url}\n  HTTP {(int)response.StatusCode}");
            return (response.IsSuccessStatusCode, content);
        }
    }
}
