using D365ToolCommon.WebResource;
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
                // 使用通用 WebResource 发布服务发布相关 WebResource
                var webResourceService = new WebResourceService(service);
                webResourceService.PublishWebResources("mcs_credit_record.js", "mcs_credit_record_progress.html");
                Console.WriteLine("  ✅ WebResource 缓存已清除");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ 清除缓存失败: {ex.Message}");
            }
        }
    }
}
