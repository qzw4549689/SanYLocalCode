using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace DeployTool
{
    public class CheckBppCustomApi
    {
        public static void Check(ServiceClient service)
        {
            Console.WriteLine(">>> 探测BPP自定义API详情...");
            
            string[] apiNames = new[] { "mcs_bppstartapi", "mcs_bppwithdrawapi", "mcs_bppstartapiv2", "mcs_bppcheckapi", "mcs_bppabandonapi" };
            
            foreach (var apiName in apiNames)
            {
                Console.WriteLine($"  --- {apiName} ---");
                try
                {
                    // 查自定义API
                    var apiQuery = new QueryExpression("customapi")
                    {
                        ColumnSet = new ColumnSet("name", "uniquename", "description", "boundentitylogicalname", "isfunction", "executeprivilegename"),
                        Criteria = new FilterExpression
                        {
                            Conditions = { new ConditionExpression("uniquename", ConditionOperator.Equal, apiName) }
                        }
                    };
                    var apis = service.RetrieveMultiple(apiQuery);
                    if (apis.Entities.Count == 0)
                    {
                        Console.WriteLine("    ❌ API不存在");
                        continue;
                    }
                    
                    var api = apis.Entities[0];
                    Console.WriteLine($"    绑定实体: {api.GetAttributeValue<string>("boundentitylogicalname") ?? "(无)"}");
                    Console.WriteLine($"    IsFunction: {api.GetAttributeValue<bool>("isfunction")}");
                    Console.WriteLine($"    描述: {api.GetAttributeValue<string>("description") ?? "(无)"}");
                    
                    // 查输入参数
                    var reqQuery = new QueryExpression("customapirequestparameter")
                    {
                        ColumnSet = new ColumnSet("name", "uniquename", "description", "type", "logicalentityname", "isoptional"),
                        Criteria = new FilterExpression
                        {
                            Conditions = { new ConditionExpression("customapiid", ConditionOperator.Equal, api.Id) }
                        }
                    };
                    var reqs = service.RetrieveMultiple(reqQuery);
                    Console.WriteLine($"    输入参数({reqs.Entities.Count}个):");
                    foreach (var req in reqs.Entities)
                    {
                        var typeVal = req.GetAttributeValue<OptionSetValue>("type")?.Value;
                        var typeName = typeVal.HasValue ? GetTypeName(typeVal.Value) : "?";
                        Console.WriteLine($"      - {req.GetAttributeValue<string>("uniquename")} ({typeName}) {(req.GetAttributeValue<bool>("isoptional") ? "[可选]" : "[必填]")} {req.GetAttributeValue<string>("description")}");
                    }
                    
                    // 查输出参数
                    var respQuery = new QueryExpression("customapiresponseproperty")
                    {
                        ColumnSet = new ColumnSet("name", "uniquename", "description", "type", "logicalentityname"),
                        Criteria = new FilterExpression
                        {
                            Conditions = { new ConditionExpression("customapiid", ConditionOperator.Equal, api.Id) }
                        }
                    };
                    var resps = service.RetrieveMultiple(respQuery);
                    Console.WriteLine($"    输出参数({resps.Entities.Count}个):");
                    foreach (var resp in resps.Entities)
                    {
                        var typeVal = resp.GetAttributeValue<OptionSetValue>("type")?.Value;
                        var typeName = typeVal.HasValue ? GetTypeName(typeVal.Value) : "?";
                        Console.WriteLine($"      - {resp.GetAttributeValue<string>("uniquename")} ({typeName}) {resp.GetAttributeValue<string>("description")}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"    ❌ 查询失败: {ex.Message}");
                }
            }
            
            // 检查Plugin Steps
            Console.WriteLine("  --- 已注册的BPP Plugin Steps ---");
            try
            {
                var stepQuery = new QueryExpression("sdkmessageprocessingstep")
                {
                    ColumnSet = new ColumnSet("name", "sdkmessageidname", "primaryobjecttypecode", "stage", "mode"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("name", ConditionOperator.Like, "%bpp%")
                        }
                    }
                };
                var steps = service.RetrieveMultiple(stepQuery);
                if (steps.Entities.Count > 0)
                {
                    foreach (var step in steps.Entities)
                    {
                        var stage = step.GetAttributeValue<OptionSetValue>("stage")?.Value;
                        var stageName = stage == 10 ? "PreValidation" : stage == 20 ? "PreOperation" : stage == 40 ? "PostOperation" : $"Stage{stage}";
                        var mode = step.GetAttributeValue<OptionSetValue>("mode")?.Value == 0 ? "Sync" : "Async";
                        Console.WriteLine($"    ✅ {step.GetAttributeValue<string>("name")} | Message={step.GetAttributeValue<string>("sdkmessageidname")} | Entity={step.GetAttributeValue<string>("primaryobjecttypecode")} | {stageName} | {mode}");
                    }
                }
                else
                {
                    Console.WriteLine("    ⬜ 未找到含bpp名称的Plugin Step");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    ❌ 查询失败: {ex.Message}");
            }
            
            Console.WriteLine("  探测完成。");
        }
        
        static string GetTypeName(int type)
        {
            return type switch
            {
                0 => "Boolean",
                1 => "DateTime",
                2 => "Decimal",
                3 => "Entity",
                4 => "EntityCollection",
                5 => "EntityReference",
                6 => "Float",
                7 => "Integer",
                8 => "Money",
                9 => "Picklist",
                10 => "String",
                11 => "StringArray",
                12 => "Guid",
                _ => $"Type{type}"
            };
        }
    }
}
