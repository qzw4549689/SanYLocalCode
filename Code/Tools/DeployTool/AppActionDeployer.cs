using Microsoft.Crm.Sdk.Messages;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
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
                "mcs_credit_record",
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
                "mcs_credit_record",
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
                "mcs_credit_record",
                100100018
            );

            // 部署评分卡相关按钮
            DeployScoringCardButtons();

            // 部署成交条件样板库按钮
            DeployTradeStPayTermButtons();

            Console.WriteLine("  ✅ Modern Command Bar 按钮部署完成");
        }

        /// <summary>
        /// 部署客户评分卡表单的 Modern Command Bar 按钮
        /// </summary>
        private void DeployScoringCardButtons()
        {
            Console.WriteLine(">>> 部署客户评分卡按钮...");

            var webResourceId = GetWebResourceId("mcs_credit_scoringcard.js");
            if (webResourceId == Guid.Empty)
            {
                Console.WriteLine("  ❌ 未找到 WebResource mcs_credit_scoringcard.js");
                return;
            }
            Console.WriteLine($"  WebResource ID: {webResourceId}");

            var entityId = GetEntityId("mcs_credit_scoringcard");
            if (entityId == Guid.Empty)
            {
                Console.WriteLine("  ❌ 未找到实体 mcs_credit_scoringcard");
                return;
            }
            Console.WriteLine($"  实体 ID: {entityId}");

            // 先删除可能因 contextvalue 错误而创建的旧按钮
            DeleteAppActionIfExists("mcs_credit_scoringcard_clone");

            // 创建【克隆新建】按钮
            CreateButton(
                "mcs_credit_scoringcard_clone",
                "克隆新建",
                "克隆当前评分卡分档记录，保留评分项目信息，可修改分档区间和赋分",
                "ScoringCardForm.cloneRecord",
                webResourceId,
                entityId,
                "mcs_credit_scoringcard",
                100100010,
                "Copy"
            );

            Console.WriteLine("  ✅ 客户评分卡按钮部署完成");
        }

        /// <summary>
        /// 部署成交条件样板库表单的 Modern Command Bar 按钮
        /// </summary>
        private void DeployTradeStPayTermButtons()
        {
            Console.WriteLine(">>> 部署成交条件样板库按钮...");

            var webResourceId = GetWebResourceId("mcs_trade_stpayterm.js");
            if (webResourceId == Guid.Empty)
            {
                Console.WriteLine("  ❌ 未找到 WebResource mcs_trade_stpayterm.js");
                return;
            }
            Console.WriteLine($"  WebResource ID: {webResourceId}");

            var entityId = GetEntityId("mcs_trade_stpayterm");
            if (entityId == Guid.Empty)
            {
                Console.WriteLine("  ❌ 未找到实体 mcs_trade_stpayterm");
                return;
            }
            Console.WriteLine($"  实体 ID: {entityId}");

            // 删除可能已存在的旧按钮，确保能重新创建并加入解决方案
            DeleteAppActionIfExists("mcs_trade_stpayterm_clone");
            DeleteAppActionIfExists("mcs_trade_stpayterm_apply");
            DeleteAppActionIfExists("mcs_trade_stpayterm_approve");
            DeleteAppActionIfExists("mcs_trade_stpayterm_reject");

            // 创建【克隆新增】按钮（表单命令栏）
            CreateButton(
                "mcs_trade_stpayterm_clone",
                "克隆新增",
                "克隆当前成交条件样板记录，生成一条新记录",
                "TradeStPayTermForm.cloneRecord",
                webResourceId,
                entityId,
                "mcs_trade_stpayterm",
                100100010,
                "Copy",
                "entity_20260603_peter",
                0
            );

            // 创建【批量申请】按钮（列表命令栏）
            var applyButtonId = CreateButton(
                "mcs_trade_stpayterm_apply",
                "批量申请",
                "将选中的未生效成交条件样板提交为待审批",
                "TradeStPayTermGrid.apply",
                webResourceId,
                entityId,
                "mcs_trade_stpayterm",
                100100020,
                "Send",
                "entity_20260603_peter",
                1
            );

            // 实验：只为【批量申请】按钮设置选中记录后显示的 Display Rule
            if (applyButtonId != Guid.Empty)
            {
                SetSelectionCountDisplayRule(applyButtonId, entityId, "mcs_trade_stpayterm_apply_selection_rule", "entity_20260603_peter");
            }

            // 创建【批量审批】按钮（列表命令栏）
            CreateButton(
                "mcs_trade_stpayterm_approve",
                "批量审批",
                "将选中的待审批成交条件样板审批通过并生效",
                "TradeStPayTermGrid.approve",
                webResourceId,
                entityId,
                "mcs_trade_stpayterm",
                100100021,
                "CheckMark",
                "entity_20260603_peter",
                1
            );

            // 创建【批量拒绝】按钮（列表命令栏）
            CreateButton(
                "mcs_trade_stpayterm_reject",
                "批量拒绝",
                "将选中的待审批成交条件样板拒绝并退回未生效",
                "TradeStPayTermGrid.reject",
                webResourceId,
                entityId,
                "mcs_trade_stpayterm",
                100100022,
                "Cancel",
                "entity_20260603_peter",
                1
            );

            Console.WriteLine("  ✅ 成交条件样板库按钮部署完成");
        }

        /// <summary>
        /// 为单个 App Action 设置 Classic Display Rule：选中记录数 ≥ 1 时显示
        /// 仅用于实验验证批量按钮在列表选中记录后是否保持显示
        /// </summary>
        private void SetSelectionCountDisplayRule(Guid appActionId, Guid entityId, string ruleUniqueName, string solutionName)
        {
            try
            {
                // 删除同名的旧 rule
                var query = new Microsoft.Xrm.Sdk.Query.QueryExpression("appactionrule")
                {
                    ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("appactionruleid"),
                    Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression
                    {
                        Conditions =
                        {
                            new Microsoft.Xrm.Sdk.Query.ConditionExpression("uniquename", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, ruleUniqueName)
                        }
                    }
                };
                var existing = _service.RetrieveMultiple(query);
                foreach (var e in existing.Entities)
                {
                    _service.Delete("appactionrule", e.Id);
                    Console.WriteLine($"  🗑️ 删除旧 Display Rule: {ruleUniqueName}");
                }

                // 创建 Display Rule
                var rule = new Entity("appactionrule");
                var ruleId = Guid.NewGuid();
                rule["appactionruleid"] = ruleId;
                rule["uniquename"] = ruleUniqueName;
                rule["name"] = ruleUniqueName;
                rule["context"] = new OptionSetValue(1); // Entity
                rule["contextentity"] = new EntityReference("entity", entityId);
                rule["contextvalue"] = "mcs_trade_stpayterm";
                rule["type"] = new OptionSetValue(1); // Display Rule
                rule["definition"] = "{\"Id\":\"" + Guid.NewGuid().ToString() + "\",\"Rules\":[{\"Id\":\"" + Guid.NewGuid().ToString() + "\",\"DefaultValue\":false,\"InvertResult\":false,\"RuleType\":2,\"AppliesTo\":\"SelectedEntity\",\"Minimum\":1,\"Maximum\":100}]}";
                rule["statecode"] = new OptionSetValue(0); // Active
                rule["statuscode"] = new OptionSetValue(1); // Active

                var createRequest = new CreateRequest { Target = rule };
                createRequest.Parameters.Add("SolutionUniqueName", solutionName);
                var response = (CreateResponse)_service.Execute(createRequest);
                ruleId = response.id;
                Console.WriteLine($"  ✅ 创建 Display Rule: {ruleUniqueName} (ID: {ruleId})");

                // 建立 appaction 与 appactionrule 的多对多关联
                var relationship = new Entity("appaction_appactionrule_classicrules");
                relationship["appactionid"] = new EntityReference("appaction", appActionId);
                relationship["appactionruleid"] = new EntityReference("appactionrule", ruleId);
                _service.Create(relationship);
                Console.WriteLine($"  ✅ 关联 Display Rule 到 App Action");

                // 更新 App Action 的 visibilitytype 为 Classic Rules
                var appAction = new Entity("appaction", appActionId);
                appAction["visibilitytype"] = new OptionSetValue(2); // Classic Rules
                _service.Update(appAction);
                Console.WriteLine($"  ✅ App Action visibilitytype 已设为 Classic Rules");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ 设置 Display Rule 失败 {ruleUniqueName}: {ex.Message}");
            }
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

        private void DeleteAppActionIfExists(string uniqueName)
        {
            try
            {
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
                foreach (var entity in existing.Entities)
                {
                    _service.Delete("appaction", entity.Id);
                    Console.WriteLine($"  🗑️ 删除旧按钮: {uniqueName} (ID: {entity.Id})");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠️ 删除旧按钮 {uniqueName} 失败: {ex.Message}");
            }
        }

        private Guid CreateButton(string uniqueName, string label, string tooltip, string functionName, Guid webResourceId, Guid entityId, string contextValue, int sequence, string iconName = null, string solutionName = "entity_20260603_peter", int location = 0)
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
                    return Guid.Empty;
                }

                // 创建 App Action
                var appAction = new Entity("appaction");
                appAction["uniquename"] = uniqueName;
                appAction["name"] = uniqueName;
                appAction["buttonlabeltext"] = label;
                appAction["buttontooltiptitle"] = tooltip;
                appAction["context"] = new OptionSetValue(1); // Entity
                appAction["contextentity"] = new EntityReference("entity", entityId);
                appAction["contextvalue"] = contextValue;
                if (!string.IsNullOrEmpty(iconName))
                {
                    appAction["fonticon"] = iconName;
                }
                appAction["hidden"] = false;
                appAction["isdisabled"] = false;
                appAction["location"] = new OptionSetValue(location); // 0=表单命令栏, 1=列表命令栏
                appAction["onclickeventtype"] = new OptionSetValue(2); // JavaScript
                appAction["onclickeventjavascriptfunctionname"] = functionName;
                appAction["onclickeventjavascriptwebresourceid"] = new EntityReference("webresource", webResourceId);
                appAction["onclickeventjavascriptparameters"] = "[{\"type\":5}]"; // PrimaryControl
                appAction["sequence"] = (decimal)sequence;
                appAction["statecode"] = new OptionSetValue(0); // Active
                appAction["statuscode"] = new OptionSetValue(1); // Active
                appAction["type"] = new OptionSetValue(0); // Button
                appAction["visibilitytype"] = new OptionSetValue(0); // Show

                // 通过 CreateRequest 传入 SolutionUniqueName，使按钮直接加入目标解决方案
                var createRequest = new CreateRequest
                {
                    Target = appAction
                };
                createRequest.Parameters.Add("SolutionUniqueName", solutionName);
                var response = (CreateResponse)_service.Execute(createRequest);
                Console.WriteLine($"  ✅ 创建按钮: {label} (ID: {response.id})");
                return response.id;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ 创建按钮失败 {label}: {ex.Message}");
                return Guid.Empty;
            }
        }

    }
}
