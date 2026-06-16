using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace DeployTool
{
    public class FindPluginStep
    {
        public static void Find(ServiceClient service)
        {
            Console.WriteLine(">>> 查找 CustomerTagValidationPlugin...");
            
            // 按 typename 查询
            var typeQuery = new QueryExpression("plugintype")
            {
                ColumnSet = new ColumnSet("plugintypeid", "name", "typename"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("typename", ConditionOperator.Like, "%CustomerTagValidation%") }
                }
            };
            
            var types = service.RetrieveMultiple(typeQuery);
            Console.WriteLine($"  找到 {types.Entities.Count} 个 Plugin Type (by typename)");
            
            foreach (var t in types.Entities)
            {
                Console.WriteLine($"    Type: {t.GetAttributeValue<string>("typename")}, ID: {t.Id}");
            }
            
            // 也按 name 查询
            var typeQuery2 = new QueryExpression("plugintype")
            {
                ColumnSet = new ColumnSet("plugintypeid", "name", "typename"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("name", ConditionOperator.Like, "%CustomerTagValidation%") }
                }
            };
            
            var types2 = service.RetrieveMultiple(typeQuery2);
            Console.WriteLine($"  找到 {types2.Entities.Count} 个 Plugin Type (by name)");
            
            foreach (var t in types2.Entities)
            {
                Console.WriteLine($"    Name: {t.GetAttributeValue<string>("name")}, TypeName: {t.GetAttributeValue<string>("typename")}, ID: {t.Id}");
            }
        }
    }
}
