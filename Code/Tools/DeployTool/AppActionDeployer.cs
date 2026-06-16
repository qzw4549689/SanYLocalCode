using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using System;
using System.Linq;

namespace DeployTool
{
    /// <summary>
    /// 通过 C# SDK 直接创建 App Action (Modern Command Bar 按钮)
    /// </summary>
    public class AppActionDeployer
    {
        private readonly ServiceClient _service;

        public AppActionDeployer(ServiceClient service)
        {
            _service = service;
        }

        public void DeployButtons()
        {
            Console.WriteLine(">>> 部署 Modern Command Bar 按钮...");

            // 获取 WebResource ID
            var webResourceId = GetWebResourceId("mcs_credit_record.js");
            if (webResourceId == Guid.Empty)
            {
                Console.WriteLine("  ❌ 未找到 WebResource mcs_credit_record.js");
                return;
            }
            Console.WriteLine($"  WebResource ID: {webResourceId}");

            // 获取 mcs_credit_record 实体元数据 ID (用于 contextentity)
            var entityId = GetEntityId("mcs_credit_record");
            if (entityId == Guid.Empty)
            {
                Console.WriteLine("  ❌ 未找到实体 mcs_credit_record");
                return;
            }
            Console.WriteLine($"  实体 ID: {entityId}");

            // 创建【数据集成刷新】按钮
            CreateButton(
                "mcs_credit_record_refresh_data",
                "数据集成刷新",
                "数据集成刷新",
                "CreditRecordForm.refreshDataIntegration",
                webResourceId,
                entityId,
                100100016
            );

            // 创建【重新发起】按钮
            CreateButton(
                "mcs_credit_record_restart",
                "重新发起",
                "重新发起",
                "CreditRecordForm.restartEvaluation",
                webResourceId,
                entityId,
                100100017
            );

            // 创建【搜索 Coface 企业】按钮
            CreateButton(
                "mcs_credit_record_search_coface",
                "搜索 Coface 企业",
                "按客户英文名称和国家搜索 Coface 企业列表，选择匹配项后绑定 Coface ID",
                "CreditRecordForm.searchCofaceCompany",
                webResourceId,
                entityId,
                100100018
            );

            Console.WriteLine("  ✅ Modern Command Bar 按钮部署完成");
        }

        private Guid GetWebResourceId(string name)
        {
            var query = new Microsoft.Xrm.Sdk.Query.QueryExpression("webresource")
            {
                ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("webresourceid"),
                Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression
                {
                    Conditions =
                    {
                        new Microsoft.Xrm.Sdk.Query.ConditionExpression("name", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, name)
                    }
                }
            };

            var result = _service.RetrieveMultiple(query);
            if (result.Entities.Count > 0)
            {
                return result.Entities[0].Id;
            }
            return Guid.Empty;
        }

        private Guid GetEntityId(string entityLogicalName)
        {
            var query = new Microsoft.Xrm.Sdk.Query.QueryExpression("entity")
            {
                ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("entityid"),
                Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression
                {
                    Conditions =
                    {
                        new Microsoft.Xrm.Sdk.Query.ConditionExpression("name", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, entityLogicalName)
                    }
                }
            };

            var result = _service.RetrieveMultiple(query);
            if (result.Entities.Count > 0)
            {
                return result.Entities[0].Id;
            }
            return Guid.Empty;
        }

        private void CreateButton(string uniqueName, string label, string tooltip, string functionName, Guid webResourceId, Guid entityId, int sequence)
        {
            try
            {
                // 检查是否已存在
                var query = new Microsoft.Xrm.Sdk.Query.QueryExpression("appaction")
                {
                    ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("appactionid"),
                    Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression
                    {
                        Conditions =
                        {
                            new Microsoft.Xrm.Sdk.Query.ConditionExpression("uniquename", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, uniqueName)
                        }
                    }
                };

                var existing = _service.RetrieveMultiple(query);
                if (existing.Entities.Count > 0)
                {
                    Console.WriteLine($"  按钮已存在: {label}");
                    return;
                }

                // 创建 App Action
                var appAction = new Entity("appaction");
                appAction["uniquename"] = uniqueName;
                appAction["name"] = uniqueName;
                appAction["buttonlabeltext"] = label;
                appAction["buttontooltiptitle"] = tooltip;
                appAction["context"] = new OptionSetValue(1); // Entity
                appAction["contextentity"] = new EntityReference("entity", entityId);
                appAction["contextvalue"] = "mcs_credit_record";
                appAction["hidden"] = false;
                appAction["isdisabled"] = false;
                appAction["location"] = new OptionSetValue(0); // Form command bar
                appAction["onclickeventtype"] = new OptionSetValue(2); // JavaScript
                appAction["onclickeventjavascriptfunctionname"] = functionName;
                appAction["onclickeventjavascriptwebresourceid"] = new EntityReference("webresource", webResourceId);
                appAction["onclickeventjavascriptparameters"] = "[{\"type\":5}]"; // PrimaryControl
                appAction["sequence"] = (decimal)sequence;
                appAction["statecode"] = new OptionSetValue(0); // Active
                appAction["statuscode"] = new OptionSetValue(1); // Active
                appAction["type"] = new OptionSetValue(0); // Button
                appAction["visibilitytype"] = new OptionSetValue(0); // Show

                var id = _service.Create(appAction);
                Console.WriteLine($"  ✅ 创建按钮: {label} (ID: {id})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ 创建按钮失败 {label}: {ex.Message}");
            }
        }
    }
}
