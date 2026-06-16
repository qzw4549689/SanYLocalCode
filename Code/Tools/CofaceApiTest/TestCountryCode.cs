using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CofaceApiTest
{
    class TestCountryCode
    {
        static readonly string API_KEY = "0vneRg8vLjzPQlIfSkzO8kIDg04kfaKafTzg5sX1";
        static readonly string AUTH_URL = "https://api.coface.com/authentication/v1/token";
        static readonly string DATA_URL = "https://icon-api-test.coface.com/dataapi-v1";

        public static async Task Run(string[] args)
        {
            string idToken = await GetToken();
            if (string.IsNullOrEmpty(idToken)) return;

            string cofaceId = "icon#5415240";

            // 测试 CN
            Console.WriteLine("\n=== 测试 countryCode=CN ===");
            string urlCN = $"{DATA_URL}/urba360/monitorings/orders?externalId={Uri.EscapeDataString(cofaceId)}&countryCode=CN";
            string respCN = await CallApi(urlCN, idToken);
            Console.WriteLine($"响应: {respCN}");

            // 测试 PL
            Console.WriteLine("\n=== 测试 countryCode=PL ===");
            string urlPL = $"{DATA_URL}/urba360/monitorings/orders?externalId={Uri.EscapeDataString(cofaceId)}&countryCode=PL";
            string respPL = await CallApi(urlPL, idToken);
            Console.WriteLine($"响应: {respPL}");
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
            var tokenData = JsonSerializer.Deserialize<JsonElement>(content);
            return tokenData.GetProperty("idToken").GetString();
        }

        static async Task<string> CallApi(string url, string idToken)
        {
            using var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);
            request.Headers.Add("x-api-key", API_KEY);
            var response = await client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"HTTP 状态: {response.StatusCode}");
            return content;
        }
    }
}
