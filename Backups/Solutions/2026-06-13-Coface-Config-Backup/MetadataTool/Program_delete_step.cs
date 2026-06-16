using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

class Program_delete_step
{
    static string connectionString = "AuthType=OAuth;" +
        "Url=https://dev1.crm5.dynamics.com;" +
        "AppId=51f81489-12ee-4a9e-aaae-a2591f45987d;" +
        "RedirectUri=http://localhost;" +
        "LoginPrompt=Never;" +
        "TokenCacheStorePath=/Users/peterqiu/.dv-token-cache;" +
        "Username=gw_duanqy@sanyglobal.onmicrosoft.com";

    static void Main()
    {
        using (var service = new ServiceClient(connectionString))
        {
            if (!service.IsReady) { Console.WriteLine("连接失败"); return; }
            
            // 1. 查找 Plugin Type
            var typeQuery = new QueryExpression("plugintype")
            {
                ColumnSet = new ColumnSet("plugintypeid"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("name", ConditionOperator.Equal, "SanyD365.Plugins.CustomerTag.CustomerTagValidationPlugin") }
                }
            };
            
            var types = service.RetrieveMultiple(typeQuery);
            if (types.Entities.Count == 0) { Console.WriteLine("未找到 Plugin Type"); return; }
            
            var typeId = types.Entities[0].Id;
            Console.WriteLine($"Plugin Type ID: {typeId}");
            
            // 2. 查找并删除所有 Step
            var stepQuery = new QueryExpression("sdkmessageprocessingstep")
            {
                ColumnSet = new ColumnSet("sdkmessageprocessingstepid", "name", "stage"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("plugintypeid", ConditionOperator.Equal, typeId) }
                }
            };
            
            var steps = service.RetrieveMultiple(stepQuery);
            Console.WriteLine($"找到 {steps.Entities.Count} 个 Step");
            
            foreach (var step in steps.Entities)
            {
                var stage = step.GetAttributeValue<OptionSetValue>("stage")?.Value;
                var name = step.GetAttributeValue<string>("name");
                Console.WriteLine($"  删除: {name} (stage={stage})");
                service.Delete("sdkmessageprocessingstep", step.Id);
                Console.WriteLine($"  ✓ 已删除");
            }
            
            Console.WriteLine("完成");
        }
    }
}
