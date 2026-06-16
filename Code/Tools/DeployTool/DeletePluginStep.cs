using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace DeployTool
{
    public class DeletePluginStep
    {
        public static void Run(ServiceClient service)
        {
            Console.WriteLine(">>> 删除 CustomerTagValidationPlugin 旧 Step...");
            
            // 用 name 查询（不含命名空间）
            var typeQuery = new QueryExpression("plugintype")
            {
                ColumnSet = new ColumnSet("plugintypeid"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("name", ConditionOperator.Equal, "CustomerTagValidationPlugin") }
                }
            };
            
            var types = service.RetrieveMultiple(typeQuery);
            if (types.Entities.Count == 0) { Console.WriteLine("  未找到 Plugin Type"); return; }
            
            var typeId = types.Entities[0].Id;
            
            // 查找并删除所有 Step
            var stepQuery = new QueryExpression("sdkmessageprocessingstep")
            {
                ColumnSet = new ColumnSet("sdkmessageprocessingstepid", "name", "stage"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("plugintypeid", ConditionOperator.Equal, typeId) }
                }
            };
            
            var steps = service.RetrieveMultiple(stepQuery);
            Console.WriteLine($"  找到 {steps.Entities.Count} 个 Step");
            
            foreach (var step in steps.Entities)
            {
                var stage = step.GetAttributeValue<OptionSetValue>("stage")?.Value;
                var name = step.GetAttributeValue<string>("name");
                Console.WriteLine($"  删除: {name} (stage={stage})");
                service.Delete("sdkmessageprocessingstep", step.Id);
                Console.WriteLine($"  ✓ 已删除");
            }
            
            Console.WriteLine("  ✓ 旧 Step 清理完成");
        }
    }
}
