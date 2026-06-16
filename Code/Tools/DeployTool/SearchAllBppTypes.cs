using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace DeployTool
{
    public class SearchAllBppTypes
    {
        public static void Search(ServiceClient service)
        {
            Console.WriteLine(">>> 搜索所有SanyD365 Assembly中的BPP类型...");
            
            try
            {
                // 查询所有SanyD365的Plugin Assembly
                var assemblyQuery = new QueryExpression("pluginassembly")
                {
                    ColumnSet = new ColumnSet("pluginassemblyid", "name"),
                    Criteria = new FilterExpression
                    {
                        Conditions = { new ConditionExpression("name", ConditionOperator.Like, "SanyD365%") }
                    }
                };
                
                var assemblies = service.RetrieveMultiple(assemblyQuery);
                Console.WriteLine($"  找到 {assemblies.Entities.Count} 个SanyD365 Assembly");
                
                foreach (var asm in assemblies.Entities)
                {
                    string asmName = asm.GetAttributeValue<string>("name");
                    
                    // 查询该Assembly中的所有Plugin Type
                    var typeQuery = new QueryExpression("plugintype")
                    {
                        ColumnSet = new ColumnSet("typename", "friendlyname"),
                        Criteria = new FilterExpression
                        {
                            Conditions = { new ConditionExpression("assemblyname", ConditionOperator.Equal, asmName) }
                        }
                    };
                    
                    var types = service.RetrieveMultiple(typeQuery);
                    var bppTypes = types.Entities
                        .Where(t => {
                            string tn = t.GetAttributeValue<string>("typename") ?? "";
                            return tn.Contains("BPP") || tn.Contains("Bpp") || tn.Contains("HandlerServiceMain") || tn.Contains("IBPP");
                        })
                        .Select(t => t.GetAttributeValue<string>("typename"))
                        .ToList();
                    
                    if (bppTypes.Count > 0)
                    {
                        Console.WriteLine($"  {asmName}:");
                        foreach (var tn in bppTypes)
                        {
                            Console.WriteLine($"    {tn}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  失败: {ex.Message}");
            }
        }
    }
}
