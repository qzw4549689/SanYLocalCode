using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using System;

namespace DeployTool
{
    public class CreateOverdueField
    {
        public static void Create(ServiceClient service)
        {
            Console.WriteLine(">>> 创建 逾期未回收率模型分 字段...");

            // 检查字段是否已存在
            try
            {
                var retrieveRequest = new RetrieveAttributeRequest
                {
                    EntityLogicalName = "mcs_credit_record",
                    LogicalName = "mcs_overduerate"
                };
                service.Execute(retrieveRequest);
                Console.WriteLine("  ⚠️ 字段 mcs_overduerate 已存在，跳过创建");
                return;
            }
            catch
            {
                // 字段不存在，继续创建
            }

            // 创建整数字段（0-100）
            var integerAttribute = new IntegerAttributeMetadata
            {
                SchemaName = "mcs_overduerate",
                LogicalName = "mcs_overduerate",
                DisplayName = new Label("逾期未回收率模型分", 2052),
                Description = new Label("逾期未回收率模型分（0-100分），人工复核阶段录入。目前通过线下计算，后续可对接SAP模型自动获取。", 2052),
                RequiredLevel = new AttributeRequiredLevelManagedProperty(AttributeRequiredLevel.None),
                Format = IntegerFormat.None,
                MinValue = 0,
                MaxValue = 100
            };

            var createRequest = new CreateAttributeRequest
            {
                EntityName = "mcs_credit_record",
                Attribute = integerAttribute
            };

            service.Execute(createRequest);
            Console.WriteLine("  ✅ 字段 mcs_overduerate 创建成功");
            Console.WriteLine("     显示名称: 逾期未回收率模型分");
            Console.WriteLine("     类型: 整数 (0-100)");
            Console.WriteLine("     必填: 否");
        }
    }
}
