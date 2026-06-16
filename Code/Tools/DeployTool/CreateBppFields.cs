using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using System;

namespace DeployTool
{
    public class CreateBppFields
    {
        public static void Create(ServiceClient service)
        {
            Console.WriteLine(">>> 创建BPP相关字段到 mcs_credit_record...");

            // 1. mcs_workflowid - BPP流程实例ID
            CreateFieldIfNotExists(service, "mcs_credit_record", "mcs_workflowid", 
                "BPP流程实例ID", "发起成功后BPP返回的流程实例ID，用于拼接审批地址", 100);

            // 2. mcs_nextapprover - 当前审批人
            CreateFieldIfNotExists(service, "mcs_credit_record", "mcs_nextapprover", 
                "当前审批人", "BPP下一节点审批人飞书账号", 100);

            Console.WriteLine("  字段创建完成。");
        }

        static void CreateFieldIfNotExists(ServiceClient service, string entityName, string schemaName, string displayName, string description, int maxLength)
        {
            try
            {
                // 先检查字段是否已存在
                var retrieveRequest = new RetrieveAttributeRequest
                {
                    EntityLogicalName = entityName,
                    LogicalName = schemaName.ToLower()
                };
                service.Execute(retrieveRequest);
                Console.WriteLine($"  ⬜ 字段已存在: {schemaName}");
            }
            catch (Exception)
            {
                // 字段不存在，创建
                try
                {
                    var request = new CreateAttributeRequest
                    {
                        EntityName = entityName,
                        Attribute = new StringAttributeMetadata
                        {
                            SchemaName = schemaName,
                            LogicalName = schemaName.ToLower(),
                            DisplayName = new Label(displayName, 2052),
                            RequiredLevel = new AttributeRequiredLevelManagedProperty(AttributeRequiredLevel.None),
                            Description = new Label(description, 2052),
                            MaxLength = maxLength
                        }
                    };
                    service.Execute(request);
                    Console.WriteLine($"  ✅ 字段已创建: {schemaName} ({displayName})");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ❌ 创建字段失败({schemaName}): {ex.Message}");
                }
            }
        }
    }
}
