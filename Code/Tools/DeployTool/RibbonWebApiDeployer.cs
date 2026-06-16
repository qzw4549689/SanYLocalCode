using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DeployTool
{
    /// <summary>
    /// 通过 Web API 导入 RibbonDiff.xml
    /// 使用 Dataverse Web API 的 ImportRibbonXml 操作
    /// </summary>
    public class RibbonWebApiDeployer
    {
        private readonly ServiceClient _service;
        private readonly string _ribbonXmlPath;

        public RibbonWebApiDeployer(ServiceClient service, string ribbonXmlPath)
        {
            _service = service;
            _ribbonXmlPath = ribbonXmlPath;
        }

        public void Deploy()
        {
            Console.WriteLine(">>> 通过 Web API 部署 Ribbon...");

            var ribbonXml = File.ReadAllText(_ribbonXmlPath);

            // 使用 OrganizationRequest 调用 ImportRibbonXml
            var request = new OrganizationRequest("ImportRibbonXml");
            request["RibbonXml"] = ribbonXml;

            try
            {
                _service.Execute(request);
                Console.WriteLine("  ✅ Ribbon 部署成功");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠️ ImportRibbonXml 失败: {ex.Message}");
                Console.WriteLine("  尝试备用方案: 通过 Solution 导入 Ribbon...");
                
                // 备用: 使用 Web API 直接 POST
                DeployViaWebApi(ribbonXml);
            }
        }

        private void DeployViaWebApi(string ribbonXml)
        {
            try
            {
                // 构建 Web API URL
                var webApiUrl = "https://dev1.crm5.dynamics.com/api/data/v9.2/ImportRibbonXml";
                
                using var client = new HttpClient();
                // 使用 ServiceClient 的 OAuth token
                var token = _service.CurrentAccessToken;
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                client.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
                client.DefaultRequestHeaders.Add("OData-Version", "4.0");
                client.DefaultRequestHeaders.Add("Accept", "application/json");

                var jsonBody = $"{{\"RibbonXml\":\"{EscapeJson(ribbonXml)}\"}}";
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                var response = client.PostAsync(webApiUrl, content).Result;
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("  ✅ Web API Ribbon 部署成功");
                }
                else
                {
                    var error = response.Content.ReadAsStringAsync().Result;
                    Console.WriteLine($"  ❌ Web API 失败: {response.StatusCode} - {error}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ Web API 备用方案失败: {ex.Message}");
            }
        }

        private string EscapeJson(string str)
        {
            return str
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }
    }
}
