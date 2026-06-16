using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using System;
using System.Linq;

namespace DeployTool
{
    public class CheckFieldExists
    {
        public static void Check(ServiceClient service)
        {
            Console.WriteLine(">>> 检查字段是否存在...");

            // 检查 mcs_credit_record 实体
            var entityRequest = new RetrieveEntityRequest
            {
                EntityFilters = EntityFilters.Attributes,
                LogicalName = "mcs_credit_record"
            };

            var entityResponse = (RetrieveEntityResponse)service.Execute(entityRequest);
            var attributes = entityResponse.EntityMetadata.Attributes;

            string[] fieldsToCheck = new[]
            {
                "mcs_overduerate",      // 逾期未回收率模型分
                "mcs_overdue_score",    // 可能的名字
                "mcs_ar_score",         // AR模型分
                "mcs_collection_score", // 回款评分
                "mcs_overdue_model",    // 逾期模型
                "mcs_dso_score",        // DSO评分
            };

            foreach (var field in fieldsToCheck)
            {
                var attr = attributes.FirstOrDefault(a => a.LogicalName == field);
                if (attr != null)
                {
                    Console.WriteLine($"  ✅ {field}: 存在 (类型: {attr.AttributeType})");
                }
                else
                {
                    Console.WriteLine($"  ❌ {field}: 不存在");
                }
            }

            // 也检查 account 实体
            Console.WriteLine("\n  检查 account 实体...");
            var accountRequest = new RetrieveEntityRequest
            {
                EntityFilters = EntityFilters.Attributes,
                LogicalName = "account"
            };

            var accountResponse = (RetrieveEntityResponse)service.Execute(accountRequest);
            var accountAttrs = accountResponse.EntityMetadata.Attributes;

            foreach (var field in fieldsToCheck)
            {
                var attr = accountAttrs.FirstOrDefault(a => a.LogicalName == field);
                if (attr != null)
                {
                    Console.WriteLine($"  ✅ {field}: 存在 (类型: {attr.AttributeType})");
                }
                else
                {
                    Console.WriteLine($"  ❌ {field}: 不存在");
                }
            }
        }
    }
}
