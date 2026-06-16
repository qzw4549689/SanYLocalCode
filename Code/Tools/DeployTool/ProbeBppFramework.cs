using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace DeployTool
{
    public class ProbeBppFramework
    {
        public static void Probe(ServiceClient service)
        {
            Console.WriteLine(">>> 探测BPP框架类型...");
            
            // 1. 查找SanyD365.D365ExtensionApi Assembly
            try
            {
                var query = new QueryExpression("pluginassembly")
                {
                    ColumnSet = new ColumnSet("name", "version", "pluginassemblyid"),
                    Criteria = new FilterExpression
                    {
                        Conditions = { new ConditionExpression("name", ConditionOperator.Like, "%Extension%") }
                    }
                };
                var asms = service.RetrieveMultiple(query);
                Console.WriteLine($"  找到 {asms.Entities.Count} 个含Extension的Assembly:");
                foreach (var asm in asms.Entities)
                {
                    Console.WriteLine($"    {asm.GetAttributeValue<string>("name")} v{asm.GetAttributeValue<string>("version")}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ 查询失败: {ex.Message}");
            }
            
            // 2. 查找含BPP或Handler的Plugin Type
            Console.WriteLine("  查找含BPP/Handler的Plugin Type...");
            try
            {
                var typeQuery = new QueryExpression("plugintype")
                {
                    ColumnSet = new ColumnSet("name", "typename", "pluginassemblyid"),
                    Criteria = new FilterExpression
                    {
                        Conditions = { new ConditionExpression("typename", ConditionOperator.Like, "%BPP%") }
                    }
                };
                var types = service.RetrieveMultiple(typeQuery);
                Console.WriteLine($"    找到 {types.Entities.Count} 个含BPP的Type:");
                foreach (var t in types.Entities)
                {
                    Console.WriteLine($"      {t.GetAttributeValue<string>("typename")}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    ❌ 查询失败: {ex.Message}");
            }
            
            // 3. 查找含Handler的Type
            try
            {
                var typeQuery = new QueryExpression("plugintype")
                {
                    ColumnSet = new ColumnSet("name", "typename"),
                    Criteria = new FilterExpression
                    {
                        Conditions = { new ConditionExpression("typename", ConditionOperator.Like, "%Handler%") }
                    }
                };
                var types = service.RetrieveMultiple(typeQuery);
                Console.WriteLine($"    找到 {types.Entities.Count} 个含Handler的Type:");
                foreach (var t in types.Entities)
                {
                    Console.WriteLine($"      {t.GetAttributeValue<string>("typename")}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    ❌ 查询失败: {ex.Message}");
            }
            
            // 4. 查找自定义配置实体
            Console.WriteLine("  查找BPP配置相关实体...");
            string[] possibleEntities = new[] { 
                "mcs_bppconfig", "mcs_bppsetting", "sany_bppconfig",
                "bpp_config", "bpp_setting", "mcs_workflowconfig"
            };
            foreach (var ent in possibleEntities)
            {
                try
                {
                    var q = new QueryExpression(ent) { TopCount = 1 };
                    var r = service.RetrieveMultiple(q);
                    Console.WriteLine($"    ✅ 实体存在: {ent}, 记录数={r.Entities.Count}");
                }
                catch
                {
                    // 忽略
                }
            }
            
            // 5. 查看systemuser中是否有domainaccount相关字段
            Console.WriteLine("  查看systemuser中domain相关字段...");
            try
            {
                var user = service.Retrieve("systemuser", new Guid("6ad92d8f-ab60-f111-a826-000d3aa333b3"), new ColumnSet(true));
                foreach (var attr in user.Attributes)
                {
                    var key = attr.Key.ToLower();
                    if (key.Contains("domain") || key.Contains("feishu") || key.Contains("bpp") || key.Contains("accountname"))
                    {
                        Console.WriteLine($"    {attr.Key} = {attr.Value}");
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
