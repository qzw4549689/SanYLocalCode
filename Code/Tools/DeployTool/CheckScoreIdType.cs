using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using System;

namespace DeployTool
{
    public class CheckScoreIdType
    {
        public static void Check(ServiceClient service)
        {
            Console.WriteLine(">>> 检查 mcs_credit_record 字段类型...");
            
            var req1 = new RetrieveAttributeRequest
            {
                EntityLogicalName = "mcs_credit_record",
                LogicalName = "mcs_scoreid"
            };
            var resp1 = (RetrieveAttributeResponse)service.Execute(req1);
            Console.WriteLine($"  mcs_credit_record.mcs_scoreid: {resp1.AttributeMetadata.AttributeType}");
            
            var req2 = new RetrieveAttributeRequest
            {
                EntityLogicalName = "mcs_credit_scoringcard",
                LogicalName = "mcs_credititem"
            };
            var resp2 = (RetrieveAttributeResponse)service.Execute(req2);
            Console.WriteLine($"  mcs_credit_scoringcard.mcs_credititem: {resp2.AttributeMetadata.AttributeType}");
        }
    }
}
