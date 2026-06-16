using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace DeployTool
{
    public class ProbeBppAssembly
    {
        public static void Probe(ServiceClient service)
        {
            Console.WriteLine(">>> 深入探测BPP框架...");
            
            // 1. 找到SanyD365.D365ExtensionApi的Assembly ID
            Guid extApiId = Guid.Empty;
            try
            {
                var query = new QueryExpression("pluginassembly")
                {
                    ColumnSet = new ColumnSet("pluginassemblyid"),
                    Criteria = new FilterExpression
                    {
                        Conditions = { new ConditionExpression("name", ConditionOperator.Equal, "SanyD365.D365ExtensionApi") }
                    }
                };
                var results = service.RetrieveMultiple(query);
                if (results.Entities.Count > 0)
                {
                    extApiId = results.Entities[0].Id;
                    Console.WriteLine($"  SanyD365.D365ExtensionApi Assembly ID: {extApiId}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ 查询失败: {ex.Message}");
                return;
            }
            
            // 2. 查这个Assembly下的所有Type
            if (extApiId != Guid.Empty)
            {
                Console.WriteLine("  该Assembly下的所有Type:");
                try
                {
                    var typeQuery = new QueryExpression("plugintype")
                    {
                        ColumnSet = new ColumnSet("typename", "friendlyname"),
                        Criteria = new FilterExpression
                        {
                            Conditions = { new ConditionExpression("pluginassemblyid", ConditionOperator.Equal, extApiId) }
                        }
                    };
                    var types = service.RetrieveMultiple(typeQuery);
                    foreach (var t in types.Entities)
                    {
                        var typeName = t.GetAttributeValue<string>("typename");
                        // 只显示含BPP或Handler或Service的
                        if (typeName.Contains("BPP") || typeName.Contains("Handler") || typeName.Contains("Service") || typeName.Contains("Bpp"))
                        {
                            Console.WriteLine($"    {typeName}");
                        }
                    }
                    Console.WriteLine($"    (共{types.Entities.Count}个Type，过滤显示含BPP/Handler/Service/Bpp的)");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"    ❌ 查询失败: {ex.Message}");
                }
            }
            
            // 3. 查找SanyD365.D365ExtensionApi.Sales Assembly
            Console.WriteLine("  SanyD365.D365ExtensionApi.Sales Assembly:");
            try
            {
                var query = new QueryExpression("pluginassembly")
                {
                    ColumnSet = new ColumnSet("pluginassemblyid", "name"),
                    Criteria = new FilterExpression
                    {
                        Conditions = { new ConditionExpression("name", ConditionOperator.Equal, "SanyD365.D365ExtensionApi.Sales") }
                    }
                };
                var results = service.RetrieveMultiple(query);
                if (results.Entities.Count > 0)
                {
                    var asmId = results.Entities[0].Id;
                    var typeQuery = new QueryExpression("plugintype")
                    {
                        ColumnSet = new ColumnSet("typename"),
                        Criteria = new FilterExpression
                        {
                            Conditions = { new ConditionExpression("pluginassemblyid", ConditionOperator.Equal, asmId) }
                        }
                    };
                    var types = service.RetrieveMultiple(typeQuery);
                    foreach (var t in types.Entities)
                    {
                        var typeName = t.GetAttributeValue<string>("typename");
                        if (typeName.Contains("BPP") || typeName.Contains("Handler") || typeName.Contains("Service") || typeName.Contains("Bpp"))
                        {
                            Console.WriteLine($"    {typeName}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    ❌ 查询失败: {ex.Message}");
            }
            
            // 4. 查找所有含BppStart的Plugin Step
            Console.WriteLine("  含BppStart的Plugin Steps:");
            try
            {
                var stepQuery = new QueryExpression("sdkmessageprocessingstep")
                {
                    ColumnSet = new ColumnSet("name", "plugintypeid", "eventhandler"),
                    Criteria = new FilterExpression
                    {
                        Conditions = { new ConditionExpression("name", ConditionOperator.Like, "%BppStart%") }
                    }
                };
                var steps = service.RetrieveMultiple(stepQuery);
                foreach (var step in steps.Entities)
                {
                    Console.WriteLine($"    {step.GetAttributeValue<string>("name")}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    ❌ 查询失败: {ex.Message}");
            }
            
            // 5. 查找自定义API mcs_bppstartapi的实现Plugin
            Console.WriteLine("  mcs_bppstartapi的实现:");
            try
            {
                var apiQuery = new QueryExpression("customapi")
                {
                    ColumnSet = new ColumnSet("uniquename", "plugintypeid"),
                    Criteria = new FilterExpression
                    {
                        Conditions = { new ConditionExpression("uniquename", ConditionOperator.Equal, "mcs_bppstartapi") }
                    }
                };
                var apis = service.RetrieveMultiple(apiQuery);
                if (apis.Entities.Count > 0)
                {
                    var api = apis.Entities[0];
                    var pluginTypeId = api.GetAttributeValue<EntityReference>("plugintypeid")?.Id;
                    if (pluginTypeId.HasValue)
                    {
                        var type = service.Retrieve("plugintype", pluginTypeId.Value, new ColumnSet("typename"));
                        Console.WriteLine($"    实现Type: {type.GetAttributeValue<string>("typename")}");
                    }
                    else
                    {
                        Console.WriteLine("    ⬜ 未关联Plugin Type");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    ❌ 查询失败: {ex.Message}");
            }
            
            Console.WriteLine("  探测完成。");
        }
    }
}
