using Microsoft.Crm.Sdk.Messages;
using Microsoft.PowerPlatform.Dataverse.Client;
using System;

namespace DeployTool
{
    public class ClearWebResourceCache
    {
        public static void Run(ServiceClient service)
        {
            Console.WriteLine(">>> 清除 WebResource 缓存...");
            
            try
            {
                // 发布所有 WebResource
                var request = new PublishXmlRequest
                {
                    ParameterXml = @"<importexportxml><webresources><webresource>mcs_credit_record.js</webresource><webresource>mcs_credit_record_progress.html</webresource></webresources></importexportxml>"
                };
                service.Execute(request);
                Console.WriteLine("  ✅ WebResource 缓存已清除");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ 清除缓存失败: {ex.Message}");
            }
        }
    }
}
