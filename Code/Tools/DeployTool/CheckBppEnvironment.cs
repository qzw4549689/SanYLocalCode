using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace DeployTool
{
    public class CheckBppEnvironment
    {
        public static void Check(ServiceClient service)
        {
            Console.WriteLine(">>> 探测BPP环境...");

            // 1. 检查自定义Action: mcs_bppstartapi
            Console.WriteLine("  1. 检查自定义Action [mcs_bppstartapi]...");
            try
            {
                var actionQuery = new QueryExpression("workflow")
                {
                    ColumnSet = new ColumnSet("workflowid", "name", "type", "statecode"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("name", ConditionOperator.Equal, "mcs_bppstartapi"),
                            new ConditionExpression("type", ConditionOperator.Equal, 1) // 1=Definition
                        }
                    }
                };
                var actions = service.RetrieveMultiple(actionQuery);
                if (actions.Entities.Count > 0)
                {
                    var a = actions.Entities[0];
                    Console.WriteLine($"    ✅ 存在: {a.GetAttributeValue<string>("name")}, State={a.GetAttributeValue<OptionSetValue>("statecode")?.Value}");
                }
                else
                {
                    Console.WriteLine("    ❌ 不存在");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    ❌ 查询失败: {ex.Message}");
            }

            // 2. 检查实体上是否有BPP相关字段
            Console.WriteLine("  2. 检查mcs_credit_record实体BPP字段...");
            string[] bppFields = new[] { "mcs_workflowid", "mcs_nextapprover", "mcs_bppstatus", "mcs_bppid", "mcs_bpperrormsg", "mcs_bpprejectreason", "mcs_bppappriver" };
            foreach (var field in bppFields)
            {
                try
                {
                    var attrQuery = new QueryExpression("attribute")
                    {
                        ColumnSet = new ColumnSet("attributename"),
                        Criteria = new FilterExpression
                        {
                            Conditions =
                            {
                                new ConditionExpression("attributename", ConditionOperator.Equal, field)
                            }
                        }
                    };
                    // 简化：直接尝试读取字段
                    var testRecord = service.Retrieve("mcs_credit_record", new Guid("b7e04e27-0974-4fec-8994-7c409c83b620"), new ColumnSet(field));
                    var hasValue = testRecord.Contains(field) && testRecord[field] != null;
                    Console.WriteLine($"    {field}: {(hasValue ? "✅ 有值=" + testRecord[field] : "⬜ 字段存在但为空")}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"    {field}: ❌ 不存在或查询失败 ({ex.Message})");
                }
            }

            // 3. 检查Plugin Assembly中是否有BPP相关Plugin
            Console.WriteLine("  3. 检查已注册Plugin Assembly...");
            try
            {
                var asmQuery = new QueryExpression("pluginassembly")
                {
                    ColumnSet = new ColumnSet("name"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("name", ConditionOperator.Like, "%BPP%")
                        }
                    }
                };
                var asms = service.RetrieveMultiple(asmQuery);
                if (asms.Entities.Count > 0)
                {
                    foreach (var asm in asms.Entities)
                    {
                        Console.WriteLine($"    ✅ {asm.GetAttributeValue<string>("name")}");
                    }
                }
                else
                {
                    Console.WriteLine("    ⬜ 未找到含BPP名称的Plugin Assembly");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    ❌ 查询失败: {ex.Message}");
            }

            // 4. 检查自定义API (Web API)
            Console.WriteLine("  4. 检查自定义API...");
            try
            {
                var apiQuery = new QueryExpression("customapi")
                {
                    ColumnSet = new ColumnSet("name", "uniquename"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("name", ConditionOperator.Like, "%bpp%")
                        }
                    }
                };
                var apis = service.RetrieveMultiple(apiQuery);
                if (apis.Entities.Count > 0)
                {
                    foreach (var api in apis.Entities)
                    {
                        Console.WriteLine($"    ✅ {api.GetAttributeValue<string>("uniquename")}");
                    }
                }
                else
                {
                    Console.WriteLine("    ⬜ 未找到含bpp名称的自定义API");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    ⚠️ 查询失败(可能权限不足): {ex.Message}");
            }

            Console.WriteLine("  探测完成。");
        }
    }
}
