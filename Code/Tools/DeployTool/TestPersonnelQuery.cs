using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace DeployTool
{
    public class TestPersonnelQuery
    {
        public static void Test(ServiceClient service)
        {
            Console.WriteLine(">>> 测试personnel查询方式...");
            Guid userId = new Guid("6ad92d8f-ab60-f111-a826-000d3aa333b3");
            
            try
            {
                // 方式1: 通过mcs_systemuserid查询
                Console.WriteLine("  方式1: mcs_systemuserid关联");
                var fetch1 = $@"
                <fetch top='5'>
                  <entity name='mcs_personnel'>
                    <attribute name='mcs_domainaccount' />
                    <attribute name='mcs_name' />
                    <attribute name='mcs_feishuemail' />
                    <filter>
                      <condition attribute='mcs_systemuserid' operator='eq' value='{userId}' />
                      <condition attribute='statecode' operator='eq' value='0' />
                    </filter>
                  </entity>
                </fetch>";
                var results1 = service.RetrieveMultiple(new FetchExpression(fetch1));
                Console.WriteLine($"    结果数: {results1.Entities.Count}");
                foreach (var r in results1.Entities)
                {
                    Console.WriteLine($"    {r.GetAttributeValue<string>("mcs_name")} | domainaccount={r.GetAttributeValue<string>("mcs_domainaccount")}");
                }

                // 方式2: 通过ownerid查询
                Console.WriteLine("  方式2: ownerid关联");
                var fetch2 = $@"
                <fetch top='5'>
                  <entity name='mcs_personnel'>
                    <attribute name='mcs_domainaccount' />
                    <attribute name='mcs_name' />
                    <filter>
                      <condition attribute='ownerid' operator='eq' value='{userId}' />
                      <condition attribute='statecode' operator='eq' value='0' />
                    </filter>
                  </entity>
                </fetch>";
                var results2 = service.RetrieveMultiple(new FetchExpression(fetch2));
                Console.WriteLine($"    结果数: {results2.Entities.Count}");

                // 方式3: 通过mcs_systemuseraccount查询
                Console.WriteLine("  方式3: mcs_systemuseraccount关联");
                var fetch3 = $@"
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
                var results3 = service.RetrieveMultiple(new FetchExpression(fetch3));
                Console.WriteLine($"    结果数: {results3.Entities.Count}");

                // 方式4: 查询刚创建的personnel记录（不通过systemuser关联）
                Console.WriteLine("  方式4: 查询所有活跃的personnel记录");
                var fetch4 = $@"
                <fetch top='10'>
                  <entity name='mcs_personnel'>
                    <attribute name='mcs_domainaccount' />
                    <attribute name='mcs_name' />
                    <attribute name='mcs_systemuserid' />
                    <attribute name='ownerid' />
                    <attribute name='mcs_feishuemail' />
                    <filter>
                      <condition attribute='statecode' operator='eq' value='0' />
                    </filter>
                  </entity>
                </fetch>";
                var results4 = service.RetrieveMultiple(new FetchExpression(fetch4));
                Console.WriteLine($"    结果数: {results4.Entities.Count}");
                foreach (var r in results4.Entities)
                {
                    var su = r.GetAttributeValue<EntityReference>("mcs_systemuserid");
                    var owner = r.GetAttributeValue<EntityReference>("ownerid");
                    Console.WriteLine($"    {r.GetAttributeValue<string>("mcs_name")} | domainaccount={r.GetAttributeValue<string>("mcs_domainaccount")} | systemuserid={su?.Id} | ownerid={owner?.Id}");
                }

                // 方式5: 检查创建的personnel记录详情
                Console.WriteLine("  方式5: 查看创建的personnel记录详情");
                var createdQuery = new QueryExpression("mcs_personnel")
                {
                    ColumnSet = new ColumnSet(true),
                    Criteria = new FilterExpression
                    {
                        Conditions = { new ConditionExpression("mcs_domainaccount", ConditionOperator.Equal, "gw_qiuzw") }
                    }
                };
                var createdResults = service.RetrieveMultiple(createdQuery);
                Console.WriteLine($"    找到 {createdResults.Entities.Count} 条domainaccount=gw_qiuzw的记录");
                foreach (var r in createdResults.Entities)
                {
                    Console.WriteLine($"    记录ID: {r.Id}");
                    foreach (var attr in r.Attributes)
                    {
                        if (attr.Value != null)
                        {
                            string valStr = attr.Value.ToString();
                            if (valStr.Length > 100) valStr = valStr.Substring(0, 100) + "...";
                            Console.WriteLine($"      {attr.Key} = {valStr}");
                        }
                    }
                }

                // 方式6: 可能BPP框架使用不同的状态检查
                Console.WriteLine("  方式6: 不限制statecode查询");
                var fetch6 = $@"
                <fetch top='5'>
                  <entity name='mcs_personnel'>
                    <attribute name='mcs_domainaccount' />
                    <attribute name='mcs_name' />
                    <attribute name='statecode' />
                    <filter>
                      <condition attribute='mcs_systemuserid' operator='eq' value='{userId}' />
                    </filter>
                  </entity>
                </fetch>";
                var results6 = service.RetrieveMultiple(new FetchExpression(fetch6));
                Console.WriteLine($"    结果数: {results6.Entities.Count}");
                foreach (var r in results6.Entities)
                {
                    int state = r.GetAttributeValue<OptionSetValue>("statecode")?.Value ?? -1;
                    Console.WriteLine($"    {r.GetAttributeValue<string>("mcs_name")} | domainaccount={r.GetAttributeValue<string>("mcs_domainaccount")} | state={state}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  查询失败: {ex.Message}");
            }
        }
    }
}
