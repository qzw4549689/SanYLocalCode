using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace DeployTool
{
    public class CheckBppPluginDetails
    {
        public static void Check(ServiceClient service)
        {
            Console.WriteLine(">>> 检查BPP Plugin注册详情...");

            // 1. 查Plugin Assembly
            Console.WriteLine("  1. Plugin Assembly...");
            try
            {
                var asmQuery = new QueryExpression("pluginassembly")
                {
                    ColumnSet = new ColumnSet("name", "version", "createdon"),
                    Criteria = new FilterExpression
                    {
                        Conditions = { new ConditionExpression("name", ConditionOperator.Like, "%BPP%") }
                    }
                };
                var asms = service.RetrieveMultiple(asmQuery);
                foreach (var asm in asms.Entities)
                {
                    var asmId = asm.Id;
                    Console.WriteLine($"    Assembly: {asm.GetAttributeValue<string>("name")} v{asm.GetAttributeValue<string>("version")}");
                    
                    // 查Plugin Type
                    var typeQuery = new QueryExpression("plugintype")
                    {
                        ColumnSet = new ColumnSet("name", "typename"),
                        Criteria = new FilterExpression
                        {
                            Conditions = { new ConditionExpression("pluginassemblyid", ConditionOperator.Equal, asmId) }
                        }
                    };
                    var types = service.RetrieveMultiple(typeQuery);
                    foreach (var t in types.Entities)
                    {
                        var typeId = t.Id;
                        var typeName = t.GetAttributeValue<string>("typename");
                        Console.WriteLine($"      Type: {typeName}");
                        
                        // 查Steps
                        var stepQuery = new QueryExpression("sdkmessageprocessingstep")
                        {
                            ColumnSet = new ColumnSet("name", "stage", "mode", "rank", "filteringattributes"),
                            Criteria = new FilterExpression
                            {
                                Conditions = { new ConditionExpression("plugintypeid", ConditionOperator.Equal, typeId) }
                            }
                        };
                        var steps = service.RetrieveMultiple(stepQuery);
                        foreach (var step in steps.Entities)
                        {
                            var stage = step.GetAttributeValue<OptionSetValue>("stage")?.Value;
                            var stageName = stage == 10 ? "PreVal" : stage == 20 ? "PreOp" : stage == 40 ? "PostOp" : $"S{stage}";
                            var mode = step.GetAttributeValue<OptionSetValue>("mode")?.Value == 0 ? "Sync" : "Async";
                            Console.WriteLine($"        Step: {step.GetAttributeValue<string>("name")} | {stageName} | {mode} | Rank={step.GetAttributeValue<int>("rank")} | Filter={step.GetAttributeValue<string>("filteringattributes")}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    ❌ 查询失败: {ex.Message}");
            }

            // 2. 查BPP相关的Plugin Trace
            Console.WriteLine("  2. BPP相关Plugin Trace日志...");
            try
            {
                var traceQuery = new QueryExpression("plugintracelog")
                {
                    ColumnSet = new ColumnSet("createdon", "messageblock", "exceptiondetails"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("messageblock", ConditionOperator.Like, "%BPP%"),
                            new ConditionExpression("createdon", ConditionOperator.LastXDays, 3)
                        }
                    },
                    Orders = { new OrderExpression("createdon", OrderType.Descending) },
                    TopCount = 5
                };
                var traces = service.RetrieveMultiple(traceQuery);
                if (traces.Entities.Count == 0)
                {
                    Console.WriteLine("    ⬜ 最近7天无BPP相关Trace");
                }
                foreach (var trace in traces.Entities)
                {
                    var msg = trace.GetAttributeValue<string>("messageblock") ?? "";
                    var exc = trace.GetAttributeValue<string>("exceptiondetails") ?? "";
                    Console.WriteLine($"    [{trace.GetAttributeValue<DateTime>("createdon")}]");
                    if (!string.IsNullOrEmpty(msg))
                        Console.WriteLine($"      Msg: {msg.Substring(0, Math.Min(200, msg.Length))}");
                    if (!string.IsNullOrEmpty(exc))
                        Console.WriteLine($"      Exc: {exc.Substring(0, Math.Min(300, exc.Length))}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    ❌ 查询失败: {ex.Message}");
            }
            
            // 3. 查看Custom API的Plugin Type（谁实现了这个API）
            Console.WriteLine("  3. 自定义API的实现Plugin...");
            try
            {
                var apiQuery = new QueryExpression("customapi")
                {
                    ColumnSet = new ColumnSet("name", "uniquename"),
                    Criteria = new FilterExpression
                    {
                        Conditions = { new ConditionExpression("uniquename", ConditionOperator.Equal, "mcs_bppstartapi") }
                    }
                };
                var apis = service.RetrieveMultiple(apiQuery);
                if (apis.Entities.Count > 0)
                {
                    var api = apis.Entities[0];
                    // 通过sdkmessagefilter查关联的Plugin
                    Console.WriteLine($"    API: {api.GetAttributeValue<string>("uniquename")}");
                    
                    // 查实现此API的Plugin
                    var implQuery = new QueryExpression("sdkmessageprocessingstep")
                    {
                        ColumnSet = new ColumnSet("name", "plugintypeid"),
                        Criteria = new FilterExpression
                        {
                            Conditions =
                            {
                                new ConditionExpression("sdkmessagefilteridname", ConditionOperator.Like, "%mcs_bppstartapi%")
                            }
                        }
                    };
                    var impls = service.RetrieveMultiple(implQuery);
                    if (impls.Entities.Count > 0)
                    {
                        foreach (var impl in impls.Entities)
                        {
                            Console.WriteLine($"      实现Step: {impl.GetAttributeValue<string>("name")}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("      ⬜ 未直接找到关联Step");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    ❌ 查询失败: {ex.Message}");
            }
            
            Console.WriteLine("  检查完成。");
        }
    }
}
