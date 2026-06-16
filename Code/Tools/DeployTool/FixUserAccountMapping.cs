using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace DeployTool
{
    public class FixUserAccountMapping
    {
        public static void Fix(ServiceClient service)
        {
            Console.WriteLine(">>> 查询和修复mcs_useraccount映射...");
            Guid userId = new Guid("6ad92d8f-ab60-f111-a826-000d3aa333b3");
            
            try
            {
                // 1. 查询当前用户对应的mcs_useraccount记录
                var query = new QueryExpression("mcs_useraccount")
                {
                    ColumnSet = new ColumnSet("mcs_useraccountid", "mcs_name", "mcs_email", "mcs_systemuserid", "mcs_orgid"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("mcs_systemuserid", ConditionOperator.Equal, userId),
                            new ConditionExpression("statecode", ConditionOperator.Equal, 0)
                        }
                    }
                };
                
                var results = service.RetrieveMultiple(query);
                Console.WriteLine($"  找到 {results.Entities.Count} 条关联的mcs_useraccount记录");
                
                if (results.Entities.Count > 0)
                {
                    foreach (var r in results.Entities)
                    {
                        Console.WriteLine($"  记录: {r.Id}");
                        Console.WriteLine($"    name={r.GetAttributeValue<string>("mcs_name")}");
                        Console.WriteLine($"    email={r.GetAttributeValue<string>("mcs_email")}");
                        Console.WriteLine($"    systemuserid={r.GetAttributeValue<EntityReference>("mcs_systemuserid")?.Id}");
                    }
                }
                else
                {
                    Console.WriteLine("  ⚠️ 当前用户没有关联的mcs_useraccount记录！");
                    
                    // 2. 创建mcs_useraccount记录
                    Console.WriteLine("  尝试创建mcs_useraccount记录...");
                    try
                    {
                        var user = service.Retrieve("systemuser", userId, new ColumnSet("domainname", "fullname", "internalemailaddress"));
                        string domainName = user.GetAttributeValue<string>("domainname");
                        string fullName = user.GetAttributeValue<string>("fullname");
                        string internalEmail = user.GetAttributeValue<string>("internalemailaddress");
                        
                        var userAccount = new Entity("mcs_useraccount");
                        userAccount["mcs_name"] = domainName ?? internalEmail ?? fullName;
                        userAccount["mcs_email"] = internalEmail ?? domainName;
                        userAccount["mcs_lastname"] = fullName ?? "邱正卫";
                        userAccount["mcs_systemuserid"] = new EntityReference("systemuser", userId);
                        userAccount["ownerid"] = new EntityReference("systemuser", userId);
                        
                        var newId = service.Create(userAccount);
                        Console.WriteLine($"  ✅ 创建mcs_useraccount记录成功: {newId}");
                    }
                    catch (Exception createEx)
                    {
                        Console.WriteLine($"  ❌ 创建失败: {createEx.Message}");
                    }
                }
                
                // 3. 更新personnel记录的mcs_systemuseraccount
                Console.WriteLine("  更新personnel记录的mcs_systemuseraccount...");
                var personnelQuery = new QueryExpression("mcs_personnel")
                {
                    ColumnSet = new ColumnSet("mcs_personnelid", "mcs_systemuseraccount"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("mcs_systemuserid", ConditionOperator.Equal, userId)
                        }
                    }
                };
                var personnelResults = service.RetrieveMultiple(personnelQuery);
                
                if (personnelResults.Entities.Count > 0)
                {
                    var personnel = personnelResults.Entities[0];
                    
                    // 查询刚创建的mcs_useraccount（或已有的）
                    var uaQuery = new QueryExpression("mcs_useraccount")
                    {
                        ColumnSet = new ColumnSet("mcs_useraccountid"),
                        Criteria = new FilterExpression
                        {
                            Conditions =
                            {
                                new ConditionExpression("mcs_systemuserid", ConditionOperator.Equal, userId),
                                new ConditionExpression("statecode", ConditionOperator.Equal, 0)
                            }
                        }
                    };
                    var uaResults = service.RetrieveMultiple(uaQuery);
                    
                    if (uaResults.Entities.Count > 0)
                    {
                        var uaId = uaResults.Entities[0].Id;
                        var update = new Entity("mcs_personnel") { Id = personnel.Id };
                        update["mcs_systemuseraccount"] = new EntityReference("mcs_useraccount", uaId);
                        service.Update(update);
                        Console.WriteLine($"  ✅ personnel记录的mcs_systemuseraccount已更新为: {uaId}");
                    }
                    else
                    {
                        Console.WriteLine("  ❌ 未找到mcs_useraccount记录，无法更新personnel");
                    }
                }
                else
                {
                    Console.WriteLine("  ❌ 未找到personnel记录");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  失败: {ex.Message}");
            }
        }
    }
}
