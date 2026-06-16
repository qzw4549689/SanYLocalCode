using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.IO;

namespace DeployTool
{
    public class DeployCreditScorePlugin
    {
        public static void Deploy(ServiceClient service)
        {
            Console.WriteLine(">>> 部署 CreditScorePlugin...");

            var dllPath = "/Users/peterqiu/Work/AIWorkSpace/SanYi/Code/Customizations/Plugins/CreditScore/bin/Debug/net462/SanyD365.Plugins.CreditScore.dll";
            
            if (!File.Exists(dllPath))
            {
                Console.WriteLine("  ❌ Plugin DLL 不存在");
                return;
            }

            try
            {
                var assemblyBytes = File.ReadAllBytes(dllPath);
                var assemblyBase64 = Convert.ToBase64String(assemblyBytes);

                // 查找已注册的 Plugin Assembly
                var query = new QueryExpression("pluginassembly")
                {
                    ColumnSet = new ColumnSet("pluginassemblyid", "name"),
                    Criteria = new FilterExpression
                    {
                        Conditions = { new ConditionExpression("name", ConditionOperator.Equal, "SanyD365.Plugins.CreditScore") }
                    }
                };

                var results = service.RetrieveMultiple(query);
                
                if (results.Entities.Count > 0)
                {
                    var assembly = results.Entities[0];
                    assembly["content"] = assemblyBase64;
                    service.Update(assembly);
                    Console.WriteLine($"  ✅ CreditScore Plugin Assembly 已更新: {assembly.Id}");
                }
                else
                {
                    Console.WriteLine("  ❌ 未找到已注册的 CreditScore Plugin Assembly");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ CreditScore Plugin 部署失败: {ex.Message}");
            }
        }
    }
}
