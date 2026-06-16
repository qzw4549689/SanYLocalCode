using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CofaceApiTest
{
    class Program
    {
        static readonly string API_KEY = "0vneRg8vLjzPQlIfSkzO8kIDg04kfaKafTzg5sX1";
        static readonly string AUTH_URL = "https://api.coface.com/authentication/v1/token";
        static readonly string DATA_URL = "https://icon-api-test.coface.com/dataapi-v1";
        
        static async Task Main(string[] args)
        {
            if (args.Length > 0 && args[0].Equals("countrycode", StringComparison.OrdinalIgnoreCase))
            {
                await TestCountryCode.Run(args);
                return;
            }

            Console.WriteLine("===== Coface API 测试 =====");
            
            // 1. 获取 Token
            Console.WriteLine("\n1. 获取认证 Token...");
            string idToken = await GetToken();
            if (string.IsNullOrEmpty(idToken))
            {
                Console.WriteLine("❌ 获取 Token 失败");
                return;
            }
            Console.WriteLine("✅ Token 获取成功");
            
            string cofaceId = "icon#5415240";
            string countryCode = "PL";
            
            // 2. 查询 URBA360 监控订单
            Console.WriteLine($"\n2. 查询 URBA360 监控订单 (cofaceId={cofaceId}, country={countryCode})...");
            string urbaOrdersUrl = $"{DATA_URL}/urba360/monitorings/orders?externalId={Uri.EscapeDataString(cofaceId)}&countryCode={countryCode}";
            string urbaOrdersResponse = await CallApi(urbaOrdersUrl, idToken);
            Console.WriteLine($"URBA360 订单响应:\n{urbaOrdersResponse}");
            
            // 3. 查询 Report 订单
            Console.WriteLine($"\n3. 查询 Report 订单 (cofaceId={cofaceId}, country={countryCode})...");
            string reportOrdersUrl = $"{DATA_URL}/publications/orders?externalId={Uri.EscapeDataString(cofaceId)}&countryCode={countryCode}";
            string reportOrdersResponse = await CallApi(reportOrdersUrl, idToken);
            Console.WriteLine($"Report 订单响应:\n{reportOrdersResponse}");
            
            // 4. 如果有订单，获取内容
            if (!string.IsNullOrEmpty(urbaOrdersResponse) && urbaOrdersResponse.Trim().StartsWith("["))
            {
                Console.WriteLine("\n4. 尝试获取 URBA360 内容...");
                // 解析订单 ID
                try
                {
                    var orders = JsonSerializer.Deserialize<JsonElement>(urbaOrdersResponse);
                    if (orders.ValueKind == JsonValueKind.Array && orders.GetArrayLength() > 0)
                    {
                        var firstOrder = orders[0];
                        if (firstOrder.TryGetProperty("id", out var idProp))
                        {
                            string orderId = idProp.GetString();
                            Console.WriteLine($"找到订单 ID: {orderId}");
                            
                            string urbaContentUrl = $"{DATA_URL}/urba360/content?id={orderId}";
                            string urbaContentResponse = await CallApi(urbaContentUrl, idToken);
                            Console.WriteLine($"URBA360 内容响应 (前500字符):\n{urbaContentResponse?.Substring(0, Math.Min(500, urbaContentResponse?.Length ?? 0))}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("订单列表为空");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"解析订单响应失败: {ex.Message}");
                }
            }
            
            Console.WriteLine("\n===== 测试完成 =====");
        }
        
        static async Task<string> GetToken()
        {
            try
            {
                using var client = new HttpClient();
                var request = new HttpRequestMessage(HttpMethod.Post, AUTH_URL);
                request.Headers.Add("x-api-key", API_KEY);
                request.Content = new StringContent(
                    JsonSerializer.Serialize(new
                    {
                        username = "tangys12@sany.com.cn",
                        password = "1qaz!QAZ",
                        grant_type = "password"
                    }),
                    Encoding.UTF8,
                    "application/json"
                );
                
                var response = await client.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();
                
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"认证失败: {response.StatusCode}, {content}");
                    return null;
                }
                
                var tokenData = JsonSerializer.Deserialize<JsonElement>(content);
                if (tokenData.TryGetProperty("idToken", out var idTokenProp))
                {
                    return idTokenProp.GetString();
                }
                
                Console.WriteLine($"响应中未找到 idToken: {content}");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取 Token 异常: {ex.Message}");
                return null;
            }
        }
        
        static async Task<string> CallApi(string url, string idToken)
        {
            try
            {
                using var client = new HttpClient();
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);
                request.Headers.Add("x-api-key", API_KEY);
                
                var response = await client.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();
                
                Console.WriteLine($"HTTP 状态: {response.StatusCode}");
                
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"API 调用失败: {content}");
                    return null;
                }
                
                return content;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"API 调用异常: {ex.Message}");
                return null;
            }
        }
    }
}
