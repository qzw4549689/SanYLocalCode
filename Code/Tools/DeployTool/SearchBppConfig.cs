using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace DeployTool
{
    public class SearchBppConfig
    {
        public static void Search(ServiceClient service)
        {
            Console.WriteLine(">>> 搜索D365中的BPP配置...");
            
            try
            {
                // 1. 搜索环境变量
                Console.WriteLine("  1. 环境变量:");
                var envQuery = new QueryExpression("environmentvariabledefinition")
                {
                    ColumnSet = new ColumnSet("schemaname", "defaultvalue", "displayname"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("schemaname", ConditionOperator.Like, "%bpp%")
                        }
                    }
                };
                var envResults = service.RetrieveMultiple(envQuery);
                foreach (var e in envResults.Entities)
                {
                    Console.WriteLine($"    {e.GetAttributeValue<string>("schemaname")} = {e.GetAttributeValue<string>("defaultvalue")}");
                }
                if (envResults.Entities.Count == 0) Console.WriteLine("    无");
                
                // 2. 搜索自定义实体（含bpp/config/setting的）
                Console.WriteLine("  2. 含bpp的自定义实体:");
                var entityQuery = new QueryExpression("entitydefinition")
                {
                    ColumnSet = new ColumnSet("logicalname"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("logicalname", ConditionOperator.Like, "%bpp%")
                        }
                    }
                };
                // 注意：entitydefinition可能无法直接查询
                // 改用查询所有自定义配置实体
                var configQuery = new QueryExpression("msdyn_customcontrolextendedsettings")
                {
                    ColumnSet = new ColumnSet("msdyn_name", "msdyn_value"),
                    TopCount = 10
                };
                try
                {
                    var configResults = service.RetrieveMultiple(configQuery);
                    foreach (var c in configResults.Entities)
                    {
                        string name = c.GetAttributeValue<string>("msdyn_name");
                        if (name != null && name.ToLower().Contains("bpp"))
                        {
                            Console.WriteLine($"    {name} = {c.GetAttributeValue<string>("msdyn_value")}");
                        }
                    }
                }
                catch { }
                
                // 3. 搜索其他模块的BPP模板配置（如果有的话）
                Console.WriteLine("  3. 查找其他模块的BPP相关字段（作为参考）:");
                var sampleEntities = new[] { "mcs_companyinspection", "mcs_paymentloss" };
                foreach (var entityName in sampleEntities)
                {
                    try
                    {
                        var fieldQuery = new QueryExpression("attribute")
                        {
                            ColumnSet = new ColumnSet("logicalname"),
                            Criteria = new FilterExpression
                            {
                                Conditions =
                                {
                                    new ConditionExpression("logicalname", ConditionOperator.Like, "%workflow%")
                                }
                            }
                        };
                        // 这个查询可能需要在特定实体上执行
                        Console.WriteLine($"    {entityName}: 跳过元数据查询");
                    }
                    catch { }
                }
                
                // 4. 查询BPP流程模板（如果有实体存储）
                Console.WriteLine("  4. 尝试查询mcs_bpptemplate:");
                try
                {
                    var templateQuery = new QueryExpression("mcs_bpptemplate")
                    {
                        ColumnSet = new ColumnSet(true),
                        TopCount = 5
                    };
                    var templateResults = service.RetrieveMultiple(templateQuery);
                    Console.WriteLine($"    找到 {templateResults.Entities.Count} 条模板记录");
                    foreach (var t in templateResults.Entities)
                    {
                        Console.WriteLine($"    {t.Id}: {t.GetAttributeValue<string>("mcs_name")}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"    实体不存在: {ex.Message}");
                }
                
                // 5. 查询所有含mcs_workflowid的实体（看哪些模块已对接BPP）
                Console.WriteLine("  5. 已对接BPP的实体参考:");
                var recordQuery = new QueryExpression("mcs_companyinspection")
                {
                    ColumnSet = new ColumnSet("mcs_workflowid", "mcs_bppstatus"),
                    TopCount = 3
                };
                try
                {
                    var records = service.RetrieveMultiple(recordQuery);
                    Console.WriteLine($"    mcs_companyinspection有{records.Entities.Count}条记录含workflowid");
                    foreach (var r in records.Entities)
                    {
                        string wf = r.GetAttributeValue<string>("mcs_workflowid");
                        string bs = r.GetAttributeValue<string>("mcs_bppstatus");
                        if (!string.IsNullOrEmpty(wf))
                        {
                            Console.WriteLine($"      workflowid={wf}, bppstatus={bs}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"    查询失败: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  搜索失败: {ex.Message}");
            }
        }
    }
}
