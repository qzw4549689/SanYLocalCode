using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace DeployTool
{
    public class ProbePersonnelFields
    {
        public static void Probe(ServiceClient service)
        {
            Console.WriteLine(">>> 探测mcs_personnel和domainaccount...");
            Guid userId = new Guid("6ad92d8f-ab60-f111-a826-000d3aa333b3");
            
            try
            {
                // 1. 获取mcs_personnel的关键属性
                var request = new RetrieveEntityRequest
                {
                    EntityFilters = EntityFilters.Attributes,
                    LogicalName = "mcs_personnel"
                };
                var response = (RetrieveEntityResponse)service.Execute(request);
                
                // 查找关键字段
                var keyAttrs = response.EntityMetadata.Attributes
                    .Where(a => a.LogicalName == "mcs_domainaccount" || 
                                a.LogicalName == "mcs_systemuserid" ||
                                a.LogicalName == "mcs_systemuseraccount" ||
                                a.LogicalName == "mcs_feishuemail" ||
                                a.LogicalName == "mcs_name" ||
                                a.LogicalName == "mcs_email")
                    .OrderBy(a => a.LogicalName)
                    .ToList();
                
                Console.WriteLine("  关键字段:");
                foreach (var attr in keyAttrs)
                {
                    string attrType = attr.AttributeType?.ToString() ?? "Unknown";
                    bool isRequired = attr.RequiredLevel?.Value == AttributeRequiredLevel.ApplicationRequired ||
                                     attr.RequiredLevel?.Value == AttributeRequiredLevel.SystemRequired;
                    Console.WriteLine($"    {attr.LogicalName} ({attrType}) {(isRequired ? "[必填]" : "")}");
                }

                // 2. 查询当前用户对应的mcs_personnel记录
                Console.WriteLine("  查询当前用户对应的personnel记录...");
                
                var query = new QueryExpression("mcs_personnel")
                {
                    ColumnSet = new ColumnSet("mcs_domainaccount", "mcs_systemuserid", "mcs_feishuemail", 
                                               "mcs_name", "mcs_email", "statecode"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("mcs_systemuserid", ConditionOperator.Equal, userId)
                        }
                    },
                    TopCount = 5
                };
                
                var results = service.RetrieveMultiple(query);
                Console.WriteLine($"  找到 {results.Entities.Count} 条personnel记录");
                
                if (results.Entities.Count > 0)
                {
                    foreach (var record in results.Entities)
                    {
                        string name = record.GetAttributeValue<string>("mcs_name");
                        string domainAccount = record.GetAttributeValue<string>("mcs_domainaccount");
                        string feishuEmail = record.GetAttributeValue<string>("mcs_feishuemail");
                        string email = record.GetAttributeValue<string>("mcs_email");
                        int state = record.GetAttributeValue<OptionSetValue>("statecode")?.Value ?? 0;
                        Console.WriteLine($"  记录: {name}");
                        Console.WriteLine($"    domainaccount={domainAccount}");
                        Console.WriteLine($"    feishuemail={feishuEmail}");
                        Console.WriteLine($"    email={email}");
                        Console.WriteLine($"    statecode={state}");
                    }
                }
                else
                {
                    Console.WriteLine("  ⚠️ 当前用户没有关联的mcs_personnel记录！");
                    Console.WriteLine("  这是domainaccount为空的根本原因。");
                    
                    // 3. 查询几条现有的personnel记录作为参考
                    Console.WriteLine("  参考：现有personnel记录示例:");
                    var sampleQuery = new QueryExpression("mcs_personnel")
                    {
                        ColumnSet = new ColumnSet("mcs_domainaccount", "mcs_name", "mcs_feishuemail", "mcs_systemuserid"),
                        TopCount = 3
                    };
                    var samples = service.RetrieveMultiple(sampleQuery);
                    foreach (var s in samples.Entities)
                    {
                        string name = s.GetAttributeValue<string>("mcs_name");
                        string da = s.GetAttributeValue<string>("mcs_domainaccount");
                        string fe = s.GetAttributeValue<string>("mcs_feishuemail");
                        var su = s.GetAttributeValue<EntityReference>("mcs_systemuserid");
                        Console.WriteLine($"    {name} | domainaccount={da} | feishuemail={fe} | systemuserid={su?.Id}");
                    }
                    
                    // 4. 创建personnel记录
                    Console.WriteLine("  尝试创建当前用户的personnel记录...");
                    try
                    {
                        var user = service.Retrieve("systemuser", userId, new ColumnSet("domainname", "fullname", "internalemailaddress"));
                        string domainName = user.GetAttributeValue<string>("domainname");
                        string fullName = user.GetAttributeValue<string>("fullname");
                        string internalEmail = user.GetAttributeValue<string>("internalemailaddress");
                        string feishuAccount = domainName?.Split('@')[0] ?? "gw_qiuzw";
                        
                        var personnel = new Entity("mcs_personnel");
                        personnel["mcs_name"] = fullName ?? "邱正卫";
                        personnel["mcs_domainaccount"] = feishuAccount;
                        personnel["mcs_feishuemail"] = $"{feishuAccount}@sany.com.cn";
                        personnel["mcs_email"] = internalEmail ?? domainName;
                        personnel["mcs_systemuserid"] = new EntityReference("systemuser", userId);
                        personnel["ownerid"] = new EntityReference("systemuser", userId);
                        
                        var newId = service.Create(personnel);
                        Console.WriteLine($"  ✅ 创建personnel记录成功: {newId}");
                        Console.WriteLine($"    domainaccount={feishuAccount}");
                    }
                    catch (Exception createEx)
                    {
                        Console.WriteLine($"  ❌ 创建失败: {createEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  探测失败: {ex.Message}");
            }
        }
    }
}
