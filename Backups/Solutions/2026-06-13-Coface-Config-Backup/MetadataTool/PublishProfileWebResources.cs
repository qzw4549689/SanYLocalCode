using Microsoft.Crm.Sdk.Messages;
using Microsoft.PowerPlatform.Dataverse.Client;
using System;
using System.Threading;

namespace D365MetadataTool
{
    /// <summary>
    /// 客户信用画像 WebResource 发布器（带重试）
    /// </summary>
    public class PublishProfileWebResources
    {
        public static void Run(ServiceClient service, int maxRetries = 30, int delaySeconds = 15)
        {
            var resourceNames = new[] { "mcs_credit_profile.html", "mcs_credit_wheel.html" };
            var resourceXml = string.Join("", Array.ConvertAll(resourceNames, n => $"<webresource>{n}</webresource>"));

            var request = new PublishXmlRequest
            {
                ParameterXml = $"<importexportxml><webresources>{resourceXml}</webresources></importexportxml>"
            };

            for (int i = 1; i <= maxRetries; i++)
            {
                try
                {
                    Console.WriteLine($">>> 尝试发布画像 WebResource... (第 {i}/{maxRetries} 次)");
                    service.Execute(request);
                    Console.WriteLine($"  ✅ WebResource 发布成功: {string.Join(", ", resourceNames)}");
                    return;
                }
                catch (Exception ex)
                {
                    var msg = ex.Message;
                    if (msg.Contains("another") && (msg.Contains("PublishAll") || msg.Contains("Publish") || msg.Contains("EntityCustomization")))
                    {
                        Console.WriteLine($"  ⏳ 环境中有其他发布操作正在进行，{delaySeconds}秒后重试...");
                        if (i < maxRetries) Thread.Sleep(delaySeconds * 1000);
                    }
                    else
                    {
                        Console.WriteLine($"  ❌ 发布失败: {msg}");
                        throw;
                    }
                }
            }

            Console.WriteLine($"  ⚠ 达到最大重试次数 ({maxRetries})，发布未完成。请稍后手动重试。");
            Console.WriteLine($"     命令: dotnet run publish-webresource {string.Join(" ", resourceNames)}");
        }
    }
}
