using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace DeployTool
{
    public class DeleteWebResource
    {
        public static void Run(ServiceClient service)
        {
            Console.WriteLine(">>> 删除进度条 WebResource...");
            
            var query = new QueryExpression("webresource")
            {
                ColumnSet = new ColumnSet("webresourceid"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("name", ConditionOperator.Equal, "mcs_credit_record_progress.html") }
                }
            };

            var results = service.RetrieveMultiple(query);
            if (results.Entities.Count == 0)
            {
                Console.WriteLine("  WebResource 不存在");
                return;
            }

            foreach (var wr in results.Entities)
            {
                service.Delete("webresource", wr.Id);
                Console.WriteLine($"  ✅ 已删除 WebResource: {wr.Id}");
            }
        }
    }
}
