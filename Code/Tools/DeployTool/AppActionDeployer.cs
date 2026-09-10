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

            // 创建【Coface 下单】按钮（Coface 系统内下单，显隐由 JS 校验：仅关联客户代码阶段可用）
            // 2026-08-01 用户决策：该按钮无特殊显隐控制，用 App Action 即可，不用 Ribbon
            CreateButton(
                "mcs_credit_record_place_coface_order",
                "Coface 下单",
                "Coface 系统内下单：调查单/URBA监控单/Report单，每次点击推进一个下单阶段",
                "CreditRecordForm.placeCofaceOrder",
                webResourceId,
                entityId,
                "mcs_credit_record",
                100100019,
                "ShoppingCart",
                "entity_20260727_peter"
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
            // 2026-08-14 Bug #1834：取消审批功能，不删除/不重建批量按钮
            // DeleteAppActionIfExists("mcs_trade_stpayterm_apply");
            // DeleteAppActionIfExists("mcs_trade_stpayterm_approve");
            // DeleteAppActionIfExists("mcs_trade_stpayterm_reject");

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

            // 2026-08-14 Bug #1834：取消审批功能，批量申请/审批/拒绝按钮不再创建（已在 Ribbon XML 中注释隐藏）
            // 列表批量按钮统一参数：SelectedControlSelectedItemIds（type=23）+ SelectedControl（type=12）
            // 对应 JS 函数签名 TradeStPayTermGrid.apply/approve/reject(selectedIds, selectedControl)
            // 误配 PrimaryControl（type=5）会导致勾选记录后按钮消失（2026-07-27 修复）
            // const string gridParams = "[{\"type\":23},{\"type\":12}]";

            // 创建【批量申请】按钮（列表命令栏）
            // CreateButton(
            //     "mcs_trade_stpayterm_apply",
            //     "批量申请",
            //     "将选中的未生效成交条件样板提交为待审批",
            //     "TradeStPayTermGrid.apply",
            //     webResourceId,
            //     entityId,
            //     "mcs_trade_stpayterm",
            //     100100020,
            //     "Send",
            //     "entity_20260603_peter",
            //     1,
            //     gridParams
            // );

            // 创建【批量审批】按钮（列表命令栏）
            // CreateButton(
            //     "mcs_trade_stpayterm_approve",
            //     "批量审批",
            //     "将选中的待审批成交条件样板审批通过并生效",
            //     "TradeStPayTermGrid.approve",
            //     webResourceId,
            //     entityId,
            //     "mcs_trade_stpayterm",
            //     100100021,
            //     "CheckMark",
            //     "entity_20260603_peter",
            //     1,
            //     gridParams
            // );

            // 创建【批量拒绝】按钮（列表命令栏）
            // CreateButton(
            //     "mcs_trade_stpayterm_reject",
            //     "批量拒绝",
            //     "将选中的待审批成交条件样板拒绝并退回未生效",
            //     "TradeStPayTermGrid.reject",
            //     webResourceId,
            //     entityId,
            //     "mcs_trade_stpayterm",
            //     100100022,
            //     "Cancel",
            //     "entity_20260603_peter",
            //     1,
            //     gridParams
            // );

            Console.WriteLine("  ✅ 成交条件样板库按钮部署完成（批量按钮已按 Bug #1834 隐藏）");
        }

        /// <summary>
        /// 部署融资管理（mcs_fsm_data）BPP 提交按钮：提交立项审批 / 提交融资方案审批
        /// JS 函数 FsmDataForm.submitInitiationApproval / submitProjectApproval，参数 PrimaryControl
        /// 显隐规则需在 Command Designer 手动配置 Power Fx（状态=2且can_initiated / 状态=3且can_project）
        /// </summary>
        public void DeployFsmDataButtons()
        {
            Console.WriteLine(">>> 部署融资管理 Modern Command Bar 按钮...");

            var webResourceId = GetWebResourceId("mcs_fsm_data.js");
            if (webResourceId == Guid.Empty)
            {
                Console.WriteLine("  ❌ 未找到 WebResource mcs_fsm_data.js");
                return;
            }
            Console.WriteLine($"  WebResource ID: {webResourceId}");

            var entityId = GetEntityId("mcs_fsm_data");
            if (entityId == Guid.Empty)
            {
                Console.WriteLine("  ❌ 未找到实体 mcs_fsm_data");
                return;
            }
            Console.WriteLine($"  实体 ID: {entityId}");

            // 删除可能已存在的旧按钮，确保能重新创建并加入解决方案
            DeleteAppActionIfExists("mcs_fsm_data_submit_initiation");
            DeleteAppActionIfExists("mcs_fsm_data_submit_project");

            // 创建【提交立项审批】按钮（表单命令栏，勾号图标，位于保存并关闭之后、方案审批之前）
            CreateButton(
                "mcs_fsm_data_submit_initiation",
                "提交立项审批",
                "将融资立项提交 BPP 立项审批",
                "FsmDataForm.submitInitiationApproval",
                webResourceId,
                entityId,
                "mcs_fsm_data",
                100100040,
                "CheckMark",
                "entity_20260713",
                0
            );

            // 创建【提交融资方案审批】按钮（表单命令栏）
            CreateButton(
                "mcs_fsm_data_submit_project",
                "提交融资方案审批",
                "将融资解决方案提交 BPP 融资方案审批",
                "FsmDataForm.submitProjectApproval",
                webResourceId,
                entityId,
                "mcs_fsm_data",
                100100041,
                "CheckMark",
                "entity_20260713",
                0
            );

            Console.WriteLine("  ✅ 融资管理按钮部署完成");
        }

        /// <summary>
        /// 为成交条件样板库 3 个列表批量按钮挂 Classic SelectionCount Display Rule（选中记录数 ≥ 1 时显示）。
        /// 背景：UCI 设计使然（KB 4481268），勾选记录后只显示带选中计数规则的 item-specific 按钮，
        /// 不带规则的按钮一律隐藏——这是「勾选后按钮消失」的根因。
        /// 规则组件（appactionrule）创建时直接入指定 Solution，可随包发布到 UAT。
        /// </summary>
        /// <param name="solutionName">规则组件要加入的 Solution 唯一名（默认当期发版包）</param>
        // 2026-08-14 Bug #1834：取消审批功能，批量按钮已隐藏，不再设置 Display Rule
        public void SetTradeStPayTermBatchDisplayRules(string solutionName = "entity_20260726_peter")
        {
            Console.WriteLine(">>> 为成交条件批量按钮设置 SelectionCount Display Rule...");
            Console.WriteLine("  ⏸️ 批量按钮已按 Bug #1834 隐藏，跳过 Display Rule 设置");

            // var entityId = GetEntityId("mcs_trade_stpayterm");
            // if (entityId == Guid.Empty)
            // {
            //     Console.WriteLine("  ❌ 未找到实体 mcs_trade_stpayterm");
            //     return;
            // }
            //
            // foreach (var buttonName in new[] { "mcs_trade_stpayterm_apply", "mcs_trade_stpayterm_approve", "mcs_trade_stpayterm_reject" })
            // {
            //     var buttonId = GetAppActionIdByUniqueName(buttonName);
            //     if (buttonId == Guid.Empty)
            //     {
            //         Console.WriteLine($"  ❌ 未找到按钮 {buttonName}，跳过");
            //         continue;
            //     }
            //     SetSelectionCountDisplayRule(buttonId, entityId, $"{buttonName}_selection_rule", solutionName);
            // }
            //
            // Console.WriteLine("  ✅ 批量按钮 Display Rule 设置完成（请发布实体 mcs_trade_stpayterm 后硬刷新验证）");
        }

        /// <summary>
        /// 为单个 App Action 设置 Classic Display Rule：选中记录数 ≥ 1 时显示
        /// </summary>
        public void SetSelectionCountDisplayRule(Guid appActionId, Guid entityId, string ruleUniqueName, string solutionName)
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

                // 创建 Display Rule（不能传 SolutionUniqueName——平台限制 appactionrule 只能在微软第一方 Solution 中直接创建，
                // 2026-07-27 实测报错 "App Action rules should define only under Microsoft First party Solutions"，
                // 必须先建到默认 Solution，再用 AddSolutionComponentRequest 加入目标 Solution）
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

                ruleId = _service.Create(rule);
                Console.WriteLine($"  ✅ 创建 Display Rule: {ruleUniqueName} (ID: {ruleId})");

                // 加入目标 Solution（componenttype 运行时反查，不硬编码）
                AddRuleToSolution(ruleId, ruleUniqueName, solutionName);

                // 建立 appaction 与 appactionrule 的多对多关联（N:N 必须用 Associate 消息）
                _service.Associate(
                    "appaction", appActionId,
                    new Microsoft.Xrm.Sdk.Relationship("appaction_appactionrule_classicrules"),
                    new Microsoft.Xrm.Sdk.EntityReferenceCollection { new EntityReference("appactionrule", ruleId) });
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

        /// <summary>
        /// 将 appactionrule 加入指定 Solution（幂等）。
        /// componenttype 不硬编码：用已知第一方规则 GUID 反查 solutioncomponent 获得。
        /// </summary>
        private void AddRuleToSolution(Guid ruleId, string ruleUniqueName, string solutionName)
        {
            // 已知第一方 appactionrule（Mscrm.HideOnMobile / Mscrm.NotIpadOnpremiseOrClaimsAuth），用于反查 componenttype
            var knownRuleIds = new[]
            {
                Guid.Parse("3fdb3783-c5d2-47c1-bb50-ac8a3f298055"),
                Guid.Parse("1c554df5-66ca-4a80-8706-9120c897bf8e")
            };

            int componentType = -1;
            var scQuery = new Microsoft.Xrm.Sdk.Query.QueryExpression("solutioncomponent")
            {
                ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("componenttype"),
                TopCount = 1,
                Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression
                {
                    Conditions =
                    {
                        new Microsoft.Xrm.Sdk.Query.ConditionExpression("objectid", Microsoft.Xrm.Sdk.Query.ConditionOperator.In, knownRuleIds.Cast<object>().ToArray())
                    }
                }
            };
            var scResult = _service.RetrieveMultiple(scQuery);
            if (scResult.Entities.Count > 0)
            {
                componentType = scResult.Entities[0].GetAttributeValue<Microsoft.Xrm.Sdk.OptionSetValue>("componenttype")?.Value
                                ?? scResult.Entities[0].GetAttributeValue<int>("componenttype");
            }

            if (componentType <= 0)
            {
                Console.WriteLine($"  ⚠️ 未能反查 appactionrule 的 componenttype，规则 {ruleUniqueName} (ID: {ruleId}) 未入 {solutionName}，请用 MetadataTool add-solution-component 手动补加");
                return;
            }

            try
            {
                _service.Execute(new AddSolutionComponentRequest
                {
                    SolutionUniqueName = solutionName,
                    ComponentId = ruleId,
                    ComponentType = componentType,
                    AddRequiredComponents = false,
                    DoNotIncludeSubcomponents = true
                });
                Console.WriteLine($"  ✅ 规则已加入 Solution {solutionName}（componenttype={componentType}）");
            }
            catch (Exception ex) when (ex.Message.Contains("already") || ex.Message.Contains("duplicate"))
            {
                Console.WriteLine($"  ⊘ 规则已在 Solution {solutionName} 中，跳过");
            }
        }

        /// <summary>
        /// 设置按钮可见性（Power Fx 公式方式）。
        /// 背景：UCI 设计使然（KB 4481268），勾选记录后只显示「与选中项相关」的按钮；
        /// 平台又禁止第三方创建 appactionrule（经典显隐规则），因此改用 Power Fx 显隐公式，
        /// 公式直接存在 appaction 记录字段上（visibilitytype=1 + visibilityformulafunctionname），
        /// 可随 Solution 打包，不依赖 Command Designer、不引用命令组件库。
        /// </summary>
        /// <param name="uniqueName">按钮 uniquename（先精确匹配，再前缀匹配）</param>
        /// <param name="visibilityType">0=始终显示 1=Power Fx 公式</param>
        /// <param name="formula">Power Fx 公式，type=1 时必填，如 CountRows(Self.Selected.AllItems) &gt; 0</param>
        public void SetButtonVisibility(string uniqueName, int visibilityType, string formula)
        {
            var query = new Microsoft.Xrm.Sdk.Query.QueryExpression("appaction")
            {
                ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("appactionid", "uniquename", "visibilitytype", "visibilityformulafunctionname"),
                Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression
                {
                    Conditions =
                    {
                        new Microsoft.Xrm.Sdk.Query.ConditionExpression("uniquename", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, uniqueName)
                    }
                }
            };
            var exact = _service.RetrieveMultiple(query);
            Microsoft.Xrm.Sdk.Entity target;
            if (exact.Entities.Count > 0)
            {
                target = exact.Entities[0];
            }
            else
            {
                query.Criteria.Conditions[0].Operator = Microsoft.Xrm.Sdk.Query.ConditionOperator.BeginsWith;
                var fuzzy = _service.RetrieveMultiple(query);
                if (fuzzy.Entities.Count == 0)
                {
                    Console.WriteLine($"  ❌ 未找到按钮: {uniqueName}");
                    return;
                }
                target = fuzzy.Entities[0];
            }

            var oldType = target.GetAttributeValue<Microsoft.Xrm.Sdk.OptionSetValue>("visibilitytype")?.Value;
            var oldFormula = target.GetAttributeValue<string>("visibilityformulafunctionname");

            var update = new Microsoft.Xrm.Sdk.Entity("appaction", target.Id);
            update["visibilitytype"] = new Microsoft.Xrm.Sdk.OptionSetValue(visibilityType);
            if (visibilityType == 1 && !string.IsNullOrEmpty(formula))
            {
                update["visibilityformulafunctionname"] = formula;
            }
            _service.Update(update);
            Console.WriteLine($"  ✅ 已更新可见性: {target.GetAttributeValue<string>("uniquename")}（type {oldType} → {visibilityType}，formula [{oldFormula}] → [{formula}]）");
        }

        /// <summary>
        /// 将第一方经典规则 Mscrm.SelectionCountAtLeastOne（选中≥1条时显示）关联到按钮，
        /// 并把 visibilitytype 设为 Classic Rules（2），同时清理 Power Fx 公式字段。
        /// 背景：KB 4481268——勾选后 UCI 只显示带选中计数规则的 item-specific 按钮；
        /// 平台禁止第三方创建 appactionrule，但允许复用关联第一方规则（2026-07-27 验证路径）。
        /// </summary>
        public void AttachFirstPartySelectionCountRule(string uniqueName)
        {
            // msdyn_Mscrm.SelectionCountAtLeastOne!0（第一方规则，各环境均内置）
            // ⚠️ 规则 ID 各环境不同（DEV1=f39219dc-...，UAT=3d0c1736-...），必须按 uniquename 反查，禁止硬编码 GUID
            var firstPartyRuleId = Guid.Empty;
            var ruleQuery = new Microsoft.Xrm.Sdk.Query.QueryExpression("appactionrule")
            {
                ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("appactionruleid", "uniquename"),
                Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression
                {
                    Conditions = { new Microsoft.Xrm.Sdk.Query.ConditionExpression("uniquename", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, "msdyn_Mscrm.SelectionCountAtLeastOne!0") }
                }
            };
            var rules = _service.RetrieveMultiple(ruleQuery);
            if (rules.Entities.Count > 0)
            {
                firstPartyRuleId = rules.Entities[0].Id;
                Console.WriteLine($"  ℹ️ 第一方规则 SelectionCountAtLeastOne ID（当前环境）: {firstPartyRuleId}");
            }
            else
            {
                Console.WriteLine($"  ❌ 当前环境未找到第一方规则 msdyn_Mscrm.SelectionCountAtLeastOne!0");
                return;
            }

            var buttonId = GetAppActionIdByUniqueName(uniqueName);
            if (buttonId == Guid.Empty)
            {
                // 精确未命中再试前缀
                var query = new Microsoft.Xrm.Sdk.Query.QueryExpression("appaction")
                {
                    ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("appactionid"),
                    Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression
                    {
                        Conditions = { new Microsoft.Xrm.Sdk.Query.ConditionExpression("uniquename", Microsoft.Xrm.Sdk.Query.ConditionOperator.BeginsWith, uniqueName) }
                    }
                };
                var fuzzy = _service.RetrieveMultiple(query);
                if (fuzzy.Entities.Count == 0)
                {
                    Console.WriteLine($"  ❌ 未找到按钮: {uniqueName}");
                    return;
                }
                buttonId = fuzzy.Entities[0].Id;
            }

            // 建立 appaction 与第一方规则的多对多关联（幂等：已关联则跳过）
            // N:N 关联必须用 Associate 消息，直接 Create 交叉实体不受支持
            try
            {
                _service.Associate(
                    "appaction", buttonId,
                    new Microsoft.Xrm.Sdk.Relationship("appaction_appactionrule_classicrules"),
                    new Microsoft.Xrm.Sdk.EntityReferenceCollection { new EntityReference("appactionrule", firstPartyRuleId) });
                Console.WriteLine($"  ✅ 已关联 Mscrm.SelectionCountAtLeastOne → {uniqueName}");
            }
            catch (Exception ex) when (ex.Message.Contains("duplicate") || ex.Message.Contains("already") || ex.Message.Contains("0x80040237"))
            {
                Console.WriteLine($"  ⊘ 关联已存在，跳过");
            }

            // visibilitytype=2（Classic Rules），并清掉 Power Fx 公式字段（回退前次实验）
            var update = new Entity("appaction", buttonId);
            update["visibilitytype"] = new OptionSetValue(2);
            update["visibilityformulafunctionname"] = null;
            _service.Update(update);
            Console.WriteLine($"  ✅ {uniqueName} visibilitytype=2（Classic Rules），Power Fx 公式字段已清空");
        }

        /// <summary>
        /// 删除指定 App Action 按钮（先精确匹配 uniquename，再前缀匹配）。
        /// 用途：按钮从 appaction 体系迁移到 RibbonDiffXml 后清理旧记录（2026-07-27 批量按钮随包化改造）。
        /// 注意：仅删记录，不从 Solution 移除组件引用（ dangling 组件需另行移除）。
        /// </summary>
        public void DeleteAppAction(string uniqueName)
        {
            var buttonId = GetAppActionIdByUniqueName(uniqueName);
            string resolvedName = uniqueName;
            if (buttonId == Guid.Empty)
            {
                var query = new Microsoft.Xrm.Sdk.Query.QueryExpression("appaction")
                {
                    ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("appactionid", "uniquename"),
                    Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression
                    {
                        Conditions = { new Microsoft.Xrm.Sdk.Query.ConditionExpression("uniquename", Microsoft.Xrm.Sdk.Query.ConditionOperator.BeginsWith, uniqueName) }
                    }
                };
                var fuzzy = _service.RetrieveMultiple(query);
                if (fuzzy.Entities.Count == 0)
                {
                    Console.WriteLine($"  ❌ 未找到按钮: {uniqueName}");
                    return;
                }
                buttonId = fuzzy.Entities[0].Id;
                resolvedName = fuzzy.Entities[0].GetAttributeValue<string>("uniquename");
            }
            // 先解除全部经典规则关联（N:N 外键约束，不解除会报 appaction_appactionrule_classicrulesOne 冲突）
            var linkQuery = new Microsoft.Xrm.Sdk.Query.QueryExpression("appaction_appactionrule_classicrules")
            {
                ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("appactionruleid"),
                Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression
                {
                    Conditions = { new Microsoft.Xrm.Sdk.Query.ConditionExpression("appactionid", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, buttonId) }
                }
            };
            var links = _service.RetrieveMultiple(linkQuery);
            foreach (var link in links.Entities)
            {
                var ruleId = link.GetAttributeValue<Guid>("appactionruleid");
                _service.Disassociate(
                    "appaction", buttonId,
                    new Microsoft.Xrm.Sdk.Relationship("appaction_appactionrule_classicrules"),
                    new Microsoft.Xrm.Sdk.EntityReferenceCollection { new EntityReference("appactionrule", ruleId) });
                Console.WriteLine($"  ℹ️ 已解除规则关联: {ruleId}");
            }

            _service.Delete("appaction", buttonId);
            Console.WriteLine($"  ✅ 已删除 App Action: {resolvedName} ({buttonId})");
        }

        private Guid GetAppActionIdByUniqueName(string uniqueName)
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
            var result = _service.RetrieveMultiple(query);
            return result.Entities.Count > 0 ? result.Entities[0].Id : Guid.Empty;
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

        /// <summary>
        /// 启用/禁用 App Action 按钮（isdisabled=true 时现代按钮停用并回退显示经典 Ribbon 按钮；幂等）
        /// </summary>
        public void SetButtonDisabled(string uniqueName, bool disabled)
        {
            var query = new Microsoft.Xrm.Sdk.Query.QueryExpression("appaction")
            {
                ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("uniquename", "isdisabled"),
                Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression
                {
                    Conditions =
                    {
                        new Microsoft.Xrm.Sdk.Query.ConditionExpression("uniquename", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, uniqueName)
                    }
                }
            };

            var existing = _service.RetrieveMultiple(query);
            if (existing.Entities.Count == 0)
            {
                query.Criteria.Conditions[0] = new Microsoft.Xrm.Sdk.Query.ConditionExpression("uniquename", Microsoft.Xrm.Sdk.Query.ConditionOperator.BeginsWith, uniqueName);
                existing = _service.RetrieveMultiple(query);
            }

            if (existing.Entities.Count == 0)
            {
                Console.WriteLine($"  ⚠️ 未找到按钮: {uniqueName}");
                return;
            }

            foreach (var entity in existing.Entities)
            {
                var actualUniqueName = entity.GetAttributeValue<string>("uniquename");
                if (entity.GetAttributeValue<bool>("isdisabled") == disabled)
                {
                    Console.WriteLine($"  ⏭️ 已是目标状态（isdisabled={disabled}），跳过: {actualUniqueName}");
                    continue;
                }
                var update = new Microsoft.Xrm.Sdk.Entity("appaction", entity.Id);
                update["isdisabled"] = disabled;
                _service.Update(update);
                Console.WriteLine($"  ✅ 已设置 isdisabled={disabled}: {actualUniqueName}");
            }
        }

        /// <summary>
        /// 启用/停用 App Action 按钮（statecode=1 真正隐藏按钮；幂等）
        /// 2026-08-15 Bug #1834：隐藏批量申请/审批/拒绝按钮用（不删组件红线）。
        /// ⚠️ 与 isdisabled 不同：isdisabled 只禁用点击、按钮仍渲染；statecode=Inactive 才不渲染。
        /// ⚠️ 各环境 appaction GUID 不同（UAT/生产为导入重建），必须按 uniquename 操作。
        /// </summary>
        public void SetButtonInactive(string uniqueName, bool inactive)
        {
            var query = new Microsoft.Xrm.Sdk.Query.QueryExpression("appaction")
            {
                ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("uniquename", "statecode"),
                Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression
                {
                    Conditions =
                    {
                        new Microsoft.Xrm.Sdk.Query.ConditionExpression("uniquename", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, uniqueName)
                    }
                }
            };

            var existing = _service.RetrieveMultiple(query);
            if (existing.Entities.Count == 0)
            {
                query.Criteria.Conditions[0] = new Microsoft.Xrm.Sdk.Query.ConditionExpression("uniquename", Microsoft.Xrm.Sdk.Query.ConditionOperator.BeginsWith, uniqueName);
                existing = _service.RetrieveMultiple(query);
            }

            if (existing.Entities.Count == 0)
            {
                Console.WriteLine($"  ⚠️ 未找到按钮: {uniqueName}");
                return;
            }

            int targetState = inactive ? 1 : 0;
            foreach (var entity in existing.Entities)
            {
                var actualUniqueName = entity.GetAttributeValue<string>("uniquename");
                if (entity.GetAttributeValue<Microsoft.Xrm.Sdk.OptionSetValue>("statecode")?.Value == targetState)
                {
                    Console.WriteLine($"  ⏭️ 已是目标状态（statecode={targetState}），跳过: {actualUniqueName} ({entity.Id})");
                    continue;
                }
                var update = new Microsoft.Xrm.Sdk.Entity("appaction", entity.Id);
                update["statecode"] = new Microsoft.Xrm.Sdk.OptionSetValue(targetState);
                _service.Update(update);
                Console.WriteLine($"  ✅ 已设置 statecode={targetState}（{(inactive ? "停用/隐藏" : "启用")}）: {actualUniqueName} ({entity.Id})");
            }
        }

        /// <summary>
        /// 更新已有 App Action 按钮的 JS 参数（幂等，只改 onclickeventjavascriptparameters）
        /// 禅道 #1160：批量按钮追加 SelectedControl(type=12) 参数，用于成功后刷新列表
        /// </summary>
        /// <param name="uniqueName">按钮 uniquename（先精确匹配，无结果再前缀匹配，兼容 cr0c0__xxx!app!entity!1 形式）</param>
        /// <param name="parametersJson">参数 JSON，如 [{"type":23},{"type":12}]</param>
        public void UpdateButtonParameters(string uniqueName, string parametersJson)
        {
            var query = new Microsoft.Xrm.Sdk.Query.QueryExpression("appaction")
            {
                ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("uniquename", "onclickeventjavascriptparameters"),
                Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression
                {
                    Conditions =
                    {
                        new Microsoft.Xrm.Sdk.Query.ConditionExpression("uniquename", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, uniqueName)
                    }
                }
            };

            var existing = _service.RetrieveMultiple(query);

            if (existing.Entities.Count == 0)
            {
                // 前缀匹配，兼容 Command Designer 生成的带 ! 后缀 uniquename
                query.Criteria.Conditions[0] = new Microsoft.Xrm.Sdk.Query.ConditionExpression("uniquename", Microsoft.Xrm.Sdk.Query.ConditionOperator.BeginsWith, uniqueName);
                existing = _service.RetrieveMultiple(query);
            }

            if (existing.Entities.Count == 0)
            {
                Console.WriteLine($"  ⚠️ 未找到按钮: {uniqueName}");
                return;
            }

            foreach (var entity in existing.Entities)
            {
                var current = entity.GetAttributeValue<string>("onclickeventjavascriptparameters");
                var actualUniqueName = entity.GetAttributeValue<string>("uniquename");
                if (string.Equals(current, parametersJson, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"  ⏭️ 参数已是目标值，跳过: {actualUniqueName}");
                    continue;
                }

                var update = new Microsoft.Xrm.Sdk.Entity("appaction", entity.Id);
                update["onclickeventjavascriptparameters"] = parametersJson;
                _service.Update(update);
                Console.WriteLine($"  ✅ 已更新参数: {actualUniqueName}（{current} → {parametersJson}）");
            }
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

        private Guid CreateButton(string uniqueName, string label, string tooltip, string functionName, Guid webResourceId, Guid entityId, string contextValue, int sequence, string iconName = null, string solutionName = "entity_20260603_peter", int location = 0, string jsParameters = null)
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
                // 默认 PrimaryControl（type=5）；列表批量按钮传 SelectedControlSelectedItemIds+SelectedControl（type=23+12）
                appAction["onclickeventjavascriptparameters"] = jsParameters ?? "[{\"type\":5}]";
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
