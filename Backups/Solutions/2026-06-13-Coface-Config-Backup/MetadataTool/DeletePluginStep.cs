using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace D365MetadataTool
{
    public static class PluginStepHelper
    {
        public static void DeleteExistingStep(ServiceClient service, string pluginTypeName, string messageName, string entityName)
        {
            Console.WriteLine($">>> 删除已存在的 Plugin Step...");
            
            // 查询 Plugin Type
            var typeQuery = new QueryExpression("plugintype")
            {
                ColumnSet = new ColumnSet("plugintypeid"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("name", ConditionOperator.Equal, pluginTypeName) }
                }
            };
            
            var types = service.RetrieveMultiple(typeQuery);
            if (types.Entities.Count == 0)
            {
                Console.WriteLine("  未找到 Plugin Type");
                return;
            }
            
            var typeId = types.Entities[0].Id;
            
            // 查询 Step
            var stepQuery = new QueryExpression("sdkmessageprocessingstep")
            {
                ColumnSet = new ColumnSet("sdkmessageprocessingstepid"),
                Criteria = new FilterExpression
                {
                    Conditions = 
                    {
                        new ConditionExpression("plugintypeid", ConditionOperator.Equal, typeId),
                        new ConditionExpression("sdkmessageid_name", ConditionOperator.Equal, messageName)
                    }
                }
            };
            
            var steps = service.RetrieveMultiple(stepQuery);
            Console.WriteLine($"  找到 {steps.Entities.Count} 个 Step");
            
            foreach (var step in steps.Entities)
            {
                service.Delete("sdkmessageprocessingstep", step.Id);
                Console.WriteLine($"  ✓ 已删除 Step: {step.Id}");
            }
        }
    }
}
