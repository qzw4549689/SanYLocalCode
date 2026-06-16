using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace DeployTool
{
    public class CheckBppConfig
    {
        public static void Check(ServiceClient service)
        {
            Console.WriteLine(">>> 检查BPP配置数据...");
            
            // 1. 检查是否有BPP工作流配置实体
            string[] possibleEntities = new[] { "mcs_bpp_config", "mcs_bpp_workflow", "mcs_bpp_template", "mcs_approval_config" };
            foreach (var ent in possibleEntities)
            {
                try
                {
                    var query = new QueryExpression(ent) { TopCount = 3 };
                    var results = service.RetrieveMultiple(query);
                    Console.WriteLine($"  ✅ 实体存在: {ent}");
                    Console.WriteLine($"    记录数: {results.Entities.Count}");
                    foreach (var r in results.Entities)
                    {
                        Console.WriteLine($"    记录: {r.Id}");
                        foreach (var attr in r.Attributes)
                        {
                            if (attr.Key.Contains("name") || attr.Key.Contains("code") || attr.Key.Contains("id") || attr.Key.Contains("template"))
                            {
                                Console.WriteLine($"      {attr.Key} = {attr.Value}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("doesn't exist") || ex.Message.Contains("entity doesn't contain"))
                        Console.WriteLine($"  ⬜ 实体不存在: {ent}");
                    else
                        Console.WriteLine($"  ⚠️ 查询失败({ent}): {ex.Message}");
                }
            }
            
            // 2. 检查D365系统设置中是否有BPP相关配置
            Console.WriteLine("  2. 系统配置...");
            try
            {
                var settingQuery = new QueryExpression("setting")
                {
                    ColumnSet = new ColumnSet("name", "value"),
                    Criteria = new FilterExpression
                    {
                        Conditions = { new ConditionExpression("name", ConditionOperator.Like, "%bpp%") }
                    }
                };
                var settings = service.RetrieveMultiple(settingQuery);
                if (settings.Entities.Count == 0)
                {
                    Console.WriteLine("    ⬜ 无BPP相关系统设置");
                }
                foreach (var s in settings.Entities)
                {
                    Console.WriteLine($"    {s.GetAttributeValue<string>("name")} = {s.GetAttributeValue<string>("value")}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    ❌ 查询失败: {ex.Message}");
            }
            
            // 3. 检查environmentvariable中是否有BPP配置
            Console.WriteLine("  3. 环境变量...");
            try
            {
                var envQuery = new QueryExpression("environmentvariabledefinition")
                {
                    ColumnSet = new ColumnSet("schemaname", "displayname"),
                    Criteria = new FilterExpression
                    {
                        Conditions = { new ConditionExpression("schemaname", ConditionOperator.Like, "%bpp%") }
                    }
                };
                var envs = service.RetrieveMultiple(envQuery);
                if (envs.Entities.Count == 0)
                {
                    Console.WriteLine("    ⬜ 无BPP相关环境变量");
                }
                foreach (var e in envs.Entities)
                {
                    Console.WriteLine($"    {e.GetAttributeValue<string>("schemaname")}: {e.GetAttributeValue<string>("displayname")}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    ❌ 查询失败: {ex.Message}");
            }
            
            // 4. 查看D365中所有含bpp的实体
            Console.WriteLine("  4. 含BPP名称的实体...");
            try
            {
                var entQuery = new QueryExpression("entitydefinition")
                {
                    ColumnSet = new ColumnSet("logicalname"),
                    Criteria = new FilterExpression
                    {
                        Conditions = { new ConditionExpression("logicalname", ConditionOperator.Like, "%bpp%") }
                    }
                };
                // 这查询可能很慢，跳过
                Console.WriteLine("    (跳过，通过metadata查询)");
            }
            catch { }
            
            Console.WriteLine("  检查完成。");
        }
    }
}
