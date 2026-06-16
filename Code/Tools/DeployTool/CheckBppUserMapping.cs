using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace DeployTool
{
    public class CheckBppUserMapping
    {
        public static void Check(ServiceClient service, Guid userId)
        {
            Console.WriteLine(">>> 检查BPP用户映射...");
            Console.WriteLine($"  查询用户: {userId}");
            
            // 1. 查询systemuser的所有自定义字段
            try
            {
                var user = service.Retrieve("systemuser", userId, new ColumnSet(true));
                Console.WriteLine("  SystemUser字段（可能相关的）:");
                foreach (var attr in user.Attributes)
                {
                    var key = attr.Key.ToLower();
                    if (key.Contains("domain") || key.Contains("account") || key.Contains("bpp") || 
                        key.Contains("login") || key.Contains("user") || key.Contains("mapping") ||
                        key.Contains("sync") || key.Contains("external"))
                    {
                        Console.WriteLine($"    {attr.Key} = {attr.Value}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ 查询用户失败: {ex.Message}");
            }
            
            // 2. 尝试查询可能的BPP用户映射实体
            string[] possibleEntities = new[] { 
                "mcs_bpp_user", "mcs_bpp_usermap", "mcs_bpp_account", 
                "sany_bpp_user", "sany_bpp_account", "bpp_user_mapping",
                "mcs_user_mapping", "mcs_domainaccount", "mcs_userconfig"
            };
            foreach (var ent in possibleEntities)
            {
                try
                {
                    var query = new Microsoft.Xrm.Sdk.Query.QueryExpression(ent) { TopCount = 3 };
                    var results = service.RetrieveMultiple(query);
                    Console.WriteLine($"  ✅ 实体存在: {ent}, 记录数={results.Entities.Count}");
                    foreach (var r in results.Entities)
                    {
                        Console.WriteLine($"    记录: {r.Id}");
                        foreach (var attr in r.Attributes)
                        {
                            if (attr.Key.ToLower().Contains("user") || attr.Key.ToLower().Contains("domain") || 
                                attr.Key.ToLower().Contains("account") || attr.Key.ToLower().Contains("bpp"))
                            {
                                Console.WriteLine($"      {attr.Key} = {attr.Value}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("doesn't exist") || ex.Message.Contains("was not found"))
                        Console.WriteLine($"  ⬜ 实体不存在: {ent}");
                    else
                        Console.WriteLine($"  ⚠️ 查询失败({ent}): {ex.Message}");
                }
            }
            
            // 3. 查看Team中是否有BPP相关团队
            Console.WriteLine("  查询Team...");
            try
            {
                var teamQuery = new Microsoft.Xrm.Sdk.Query.QueryExpression("team")
                {
                    ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("name"),
                    Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression
                    {
                        Conditions = { new Microsoft.Xrm.Sdk.Query.ConditionExpression("name", Microsoft.Xrm.Sdk.Query.ConditionOperator.Like, "%bpp%") }
                    }
                };
                var teams = service.RetrieveMultiple(teamQuery);
                if (teams.Entities.Count == 0)
                    Console.WriteLine("    ⬜ 无BPP相关Team");
                foreach (var t in teams.Entities)
                {
                    Console.WriteLine($"    {t.GetAttributeValue<string>("name")}");
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
