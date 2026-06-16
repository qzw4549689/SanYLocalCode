using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace DeployTool
{
    public class CheckBppUsers
    {
        public static void Check(ServiceClient service)
        {
            Console.WriteLine(">>> 检查D365用户domainaccount...");
            
            try
            {
                var query = new QueryExpression("systemuser")
                {
                    ColumnSet = new ColumnSet("systemuserid", "fullname", "domainname", "internalemailaddress", "isdisabled"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("isdisabled", ConditionOperator.Equal, false)
                        }
                    },
                    TopCount = 20
                };
                var users = service.RetrieveMultiple(query);
                
                Console.WriteLine($"  找到 {users.Entities.Count} 个启用用户:");
                foreach (var u in users.Entities)
                {
                    var id = u.Id;
                    var name = u.GetAttributeValue<string>("fullname") ?? "(无名)";
                    var domain = u.GetAttributeValue<string>("domainname") ?? "(空)";
                    var email = u.GetAttributeValue<string>("internalemailaddress") ?? "(空)";
                    var hasDomain = !string.IsNullOrEmpty(domain) && domain != "(空)";
                    Console.WriteLine($"    {(hasDomain ? "✅" : "⬜")} {name} | domain={domain} | email={email} | {id}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ 查询失败: {ex.Message}");
            }
            
            // 检查当前连接用户
            Console.WriteLine("  当前连接用户...");
            try
            {
                var whoami = service.Execute(new OrganizationRequest("WhoAmI"));
                var userId = whoami["UserId"] as Guid?;
                Console.WriteLine($"    UserId: {userId}");
                
                if (userId.HasValue)
                {
                    var user = service.Retrieve("systemuser", userId.Value, new ColumnSet("fullname", "domainname", "internalemailaddress"));
                    Console.WriteLine($"    姓名: {user.GetAttributeValue<string>("fullname")}");
                    Console.WriteLine($"    Domain: {user.GetAttributeValue<string>("domainname")}");
                    Console.WriteLine($"    Email: {user.GetAttributeValue<string>("internalemailaddress")}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ 查询失败: {ex.Message}");
            }
            
            Console.WriteLine("  检查完成。");
        }
    }
}
