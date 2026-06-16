using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace DeployTool
{
    public class FixPersonnelRecord
    {
        public static void Fix(ServiceClient service)
        {
            Console.WriteLine(">>> 修正personnel记录...");
            Guid userId = new Guid("6ad92d8f-ab60-f111-a826-000d3aa333b3");
            
            try
            {
                // 1. 查找当前用户的personnel记录
                var query = new QueryExpression("mcs_personnel")
                {
                    ColumnSet = new ColumnSet("mcs_personnelid", "mcs_systemuserid", "mcs_systemuseraccount", 
                                               "mcs_domainaccount", "mcs_name", "mcs_feishuemail"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("mcs_systemuserid", ConditionOperator.Equal, userId)
                        }
                    }
                };
                
                var results = service.RetrieveMultiple(query);
                if (results.Entities.Count == 0)
                {
                    Console.WriteLine("  ❌ 未找到personnel记录");
                    return;
                }
                
                var personnel = results.Entities[0];
                Console.WriteLine($"  找到personnel记录: {personnel.Id}");
                
                var su = personnel.GetAttributeValue<EntityReference>("mcs_systemuserid");
                var sua = personnel.GetAttributeValue<EntityReference>("mcs_systemuseraccount");
                string da = personnel.GetAttributeValue<string>("mcs_domainaccount");
                
                Console.WriteLine($"  当前: systemuserid={su?.Id}, systemuseraccount={sua?.Id}, domainaccount={da}");
                
                // 2. 更新记录，确保systemuseraccount也指向同一个systemuser
                if (sua == null || sua.Id != userId)
                {
                    Console.WriteLine("  更新mcs_systemuseraccount...");
                    var update = new Entity("mcs_personnel") { Id = personnel.Id };
                    update["mcs_systemuseraccount"] = new EntityReference("systemuser", userId);
                    service.Update(update);
                    Console.WriteLine("  ✅ mcs_systemuseraccount已更新");
                }
                else
                {
                    Console.WriteLine("  mcs_systemuseraccount已正确设置");
                }
                
                // 3. 同时检查ownerid是否也是当前用户
                var ownerQuery = new QueryExpression("mcs_personnel")
                {
                    ColumnSet = new ColumnSet("ownerid"),
                    Criteria = new FilterExpression
                    {
                        Conditions = { new ConditionExpression("mcs_personnelid", ConditionOperator.Equal, personnel.Id) }
                    }
                };
                var ownerResult = service.RetrieveMultiple(ownerQuery);
                var owner = ownerResult.Entities[0].GetAttributeValue<EntityReference>("ownerid");
                Console.WriteLine($"  ownerid={owner?.Id} (类型: {owner?.LogicalName})");
                
                // 4. 再次查询确认
                var verify = service.Retrieve("mcs_personnel", personnel.Id, 
                    new ColumnSet("mcs_systemuserid", "mcs_systemuseraccount", "mcs_domainaccount", "mcs_name", "statecode"));
                var vSu = verify.GetAttributeValue<EntityReference>("mcs_systemuserid");
                var vSua = verify.GetAttributeValue<EntityReference>("mcs_systemuseraccount");
                string vDa = verify.GetAttributeValue<string>("mcs_domainaccount");
                int vState = verify.GetAttributeValue<OptionSetValue>("statecode")?.Value ?? -1;
                Console.WriteLine($"  验证: systemuserid={vSu?.Id}, systemuseraccount={vSua?.Id}, domainaccount={vDa}, state={vState}");
                
                // 5. 尝试用FetchXML查询，模拟BPP框架的方式
                Console.WriteLine("  用FetchXML查询...");
                var fetchXml = $@"
                <fetch top='5'>
                  <entity name='mcs_personnel'>
                    <attribute name='mcs_domainaccount' />
                    <attribute name='mcs_name' />
                    <filter>
                      <condition attribute='mcs_systemuseraccount' operator='eq' value='{userId}' />
                      <condition attribute='statecode' operator='eq' value='0' />
                    </filter>
                  </entity>
                </fetch>";
                var fetchResults = service.RetrieveMultiple(new FetchExpression(fetchXml));
                Console.WriteLine($"  FetchXML结果数: {fetchResults.Entities.Count}");
                foreach (var r in fetchResults.Entities)
                {
                    Console.WriteLine($"    {r.GetAttributeValue<string>("mcs_name")} | domainaccount={r.GetAttributeValue<string>("mcs_domainaccount")}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  失败: {ex.Message}");
            }
        }
    }
}
