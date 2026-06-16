using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace DeployTool
{
    public class FindBppHandlerMain
    {
        public static void Find(ServiceClient service)
        {
            Console.WriteLine(">>> 查找BPPHandlerServiceMain所在Assembly...");
            
            try
            {
                // 查询所有Plugin Assembly
                var query = new QueryExpression("pluginassembly")
                {
                    ColumnSet = new ColumnSet("pluginassemblyid", "name", "version"),
                    Orders = { new OrderExpression("name", OrderType.Ascending) }
                };
                
                var results = service.RetrieveMultiple(query);
                Console.WriteLine($"  共有 {results.Entities.Count} 个Plugin Assembly");
                
                // 筛选含BPP/SanyD365的Assembly
                var sanyAssemblies = results.Entities
                    .Where(e => e.GetAttributeValue<string>("name").Contains("SanyD365"))
                    .ToList();
                
                Console.WriteLine($"  SanyD365 Assembly ({sanyAssemblies.Count}):");
                foreach (var a in sanyAssemblies)
                {
                    string name = a.GetAttributeValue<string>("name");
                    string version = a.GetAttributeValue<string>("version");
                    Console.WriteLine($"    {name} (v{version})");
                }
                
                // 查找特定Assembly的Plugin Type
                var targetAssemblies = new[] { "SanyD365.D365ExtensionMain", "SanyD365.D365Extension" };
                foreach (var asmName in targetAssemblies)
                {
                    Console.WriteLine($"  查询 {asmName} 的Plugin Types...");
                    var typeQuery = new QueryExpression("plugintype")
                    {
                        ColumnSet = new ColumnSet("plugintypeid", "typename", "friendlyname"),
                        Criteria = new FilterExpression
                        {
                            Conditions =
                            {
                                new ConditionExpression("assemblyname", ConditionOperator.Equal, asmName)
                            }
                        }
                    };
                    var types = service.RetrieveMultiple(typeQuery);
                    var bppTypes = types.Entities
                        .Where(t => {
                            string tn = t.GetAttributeValue<string>("typename") ?? "";
                            return tn.Contains("BPP") || tn.Contains("Handler") || tn.Contains("Bpp");
                        })
                        .ToList();
                    
                    Console.WriteLine($"    找到 {bppTypes.Count} 个BPP相关Type:");
                    foreach (var t in bppTypes)
                    {
                        Console.WriteLine($"      {t.GetAttributeValue<string>("typename")}");
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
