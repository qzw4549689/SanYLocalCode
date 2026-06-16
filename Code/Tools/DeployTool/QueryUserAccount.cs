using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace DeployTool
{
    public class QueryUserAccount
    {
        public static void Query(ServiceClient service)
        {
            Console.WriteLine(">>> 查询mcs_useraccount实体...");
            Guid userId = new Guid("6ad92d8f-ab60-f111-a826-000d3aa333b3");
            
            try
            {
                // 1. 获取mcs_useraccount的字段信息
                Console.WriteLine("  mcs_useraccount字段:");
                var query = new QueryExpression("mcs_useraccount")
                {
                    ColumnSet = new ColumnSet(true),
                    TopCount = 5
                };
                var results = service.RetrieveMultiple(query);
                Console.WriteLine($"  找到 {results.Entities.Count} 条mcs_useraccount记录");
                
                foreach (var r in results.Entities)
                {
                    Console.WriteLine($"  记录 {r.Id}:");
                    foreach (var attr in r.Attributes)
                    {
                        if (attr.Value != null)
                        {
                            string valStr = attr.Value.ToString();
                            if (valStr.Length > 100) valStr = valStr.Substring(0, 100) + "...";
                            Console.WriteLine($"    {attr.Key} = {valStr}");
                        }
                    }
                }
                
                // 2. 尝试查找与当前用户关联的mcs_useraccount记录
                // 可能通过systemuserid或ownerid关联
                Console.WriteLine("  查找与当前用户关联的记录...");
                
                // 检查是否有systemuserid字段
                var sample = results.Entities.Count > 0 ? results.Entities[0] : null;
                if (sample != null)
                {
                    bool hasSystemUserId = sample.Contains("systemuserid");
                    bool hasOwnerId = sample.Contains("ownerid");
                    Console.WriteLine($"  有systemuserid字段: {hasSystemUserId}");
                    Console.WriteLine($"  有ownerid字段: {hasOwnerId}");
                }
                
                // 3. 尝试创建mcs_useraccount记录（如果需要）
                if (results.Entities.Count > 0)
                {
                    // 查看第一条记录的结构，找到关联systemuser的字段
                    var first = results.Entities[0];
                    foreach (var attr in first.Attributes)
                    {
                        if (attr.Value is EntityReference er)
                        {
                            Console.WriteLine($"  查找字段 {attr.Key} -> {er.LogicalName}({er.Id})");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  查询失败: {ex.Message}");
            }
        }
    }
}
