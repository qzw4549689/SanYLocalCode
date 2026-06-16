using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using System;

namespace DeployTool
{
    public class CheckItemFieldType
    {
        public static void Check(ServiceClient service)
        {
            Console.WriteLine(">>> 检查 mcs_credit_itemsno 字段类型...");
            
            var req = new RetrieveAttributeRequest
            {
                EntityLogicalName = "mcs_credit_items",
                LogicalName = "mcs_credit_itemsno"
            };
            var resp = (RetrieveAttributeResponse)service.Execute(req);
            Console.WriteLine($"  mcs_credit_items.mcs_credit_itemsno 类型: {resp.AttributeMetadata.AttributeType}");
        }
    }
}
