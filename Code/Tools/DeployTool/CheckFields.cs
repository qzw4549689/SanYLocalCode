using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using System;
using System.Linq;

namespace DeployTool
{
    public class CheckFields
    {
        public static void Check(ServiceClient service)
        {
            Console.WriteLine(">>> 检查字段名...");

            // 检查 mcs_customer_tag 的字段
            Console.WriteLine("\n  mcs_customer_tag 字段（含item/credit）:");
            var req1 = new RetrieveEntityRequest { EntityFilters = EntityFilters.Attributes, LogicalName = "mcs_customer_tag" };
            var resp1 = (RetrieveEntityResponse)service.Execute(req1);
            foreach (var attr in resp1.EntityMetadata.Attributes.OrderBy(a => a.LogicalName))
            {
                if (attr.LogicalName.Contains("item") || attr.LogicalName.Contains("credit"))
                {
                    Console.WriteLine($"    {attr.LogicalName} ({attr.AttributeType})");
                }
            }

            // 检查 mcs_credit_scoringcard 的字段
            Console.WriteLine("\n  mcs_credit_scoringcard 字段（含item/credit）:");
            var req2 = new RetrieveEntityRequest { EntityFilters = EntityFilters.Attributes, LogicalName = "mcs_credit_scoringcard" };
            var resp2 = (RetrieveEntityResponse)service.Execute(req2);
            foreach (var attr in resp2.EntityMetadata.Attributes.OrderBy(a => a.LogicalName))
            {
                if (attr.LogicalName.Contains("item") || attr.LogicalName.Contains("credit"))
                {
                    Console.WriteLine($"    {attr.LogicalName} ({attr.AttributeType})");
                }
            }
        }
    }
}
