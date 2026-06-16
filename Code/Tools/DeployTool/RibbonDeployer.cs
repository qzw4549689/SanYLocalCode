using Microsoft.Crm.Sdk.Messages;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace DeployTool
{
    /// <summary>
    /// RibbonDiff.xml 直接部署器
    /// 通过操作 RibbonCustomization / RibbonCommand / RibbonRule / RibbonTabToCommandMap 等隐藏实体实现
    /// </summary>
    public class RibbonDeployer
    {
        private readonly ServiceClient _service;
        private readonly string _entityName;
        private readonly string _ribbonXmlPath;
        private XmlDocument _ribbonDoc;
        private Guid _entityId;
        private Guid _ribbonCustomizationId;

        public RibbonDeployer(ServiceClient service, string entityName, string ribbonXmlPath)
        {
            _service = service;
            _entityName = entityName;
            _ribbonXmlPath = ribbonXmlPath;
        }

        public void Deploy()
        {
            Console.WriteLine($">>> 开始部署 Ribbon: {_entityName}");

            // 1. 加载并验证RibbonDiff XML
            LoadRibbonXml();

            // 2. 获取实体ID
            _entityId = GetEntityId();
            Console.WriteLine($"  实体ID: {_entityId}");

            // 3. 获取或创建RibbonCustomization记录
            _ribbonCustomizationId = GetOrCreateRibbonCustomization();
            Console.WriteLine($"  RibbonCustomization ID: {_ribbonCustomizationId}");

            // 4. 部署CustomActions（按钮定义）
            DeployCustomActions();

            // 5. 部署CommandDefinitions（命令定义）
            DeployCommandDefinitions();

            // 6. 部署RuleDefinitions（规则定义）
            DeployRuleDefinitions();

            // 7. 部署LocLabels（本地化标签）
            DeployLocLabels();

            // 8. 更新实体的RibbonDiffXml字段
            UpdateEntityRibbonDiff();

            Console.WriteLine($"  ✅ Ribbon部署完成");
        }

        private void LoadRibbonXml()
        {
            _ribbonDoc = new XmlDocument();
            _ribbonDoc.Load(_ribbonXmlPath);
            Console.WriteLine($"  加载RibbonDiff.xml成功");
        }

        private Guid GetEntityId()
        {
            var query = new QueryExpression("entity")
            {
                ColumnSet = new ColumnSet("entityid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("name", ConditionOperator.Equal, _entityName)
                    }
                }
            };

            var result = _service.RetrieveMultiple(query);
            if (result.Entities.Count == 0)
                throw new Exception($"未找到实体: {_entityName}");

            return result.Entities[0].Id;
        }

        private Guid GetOrCreateRibbonCustomization()
        {
            // 查询现有RibbonCustomization
            var query = new QueryExpression("ribboncustomization")
            {
                ColumnSet = new ColumnSet("ribboncustomizationid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("entity", ConditionOperator.Equal, _entityId)
                    }
                }
            };

            var result = _service.RetrieveMultiple(query);
            if (result.Entities.Count > 0)
            {
                return result.Entities[0].Id;
            }

            // 创建新的RibbonCustomization
            var ribbonCustomization = new Entity("ribboncustomization");
            ribbonCustomization["entity"] = new EntityReference("entity", _entityId);
            ribbonCustomization["name"] = $"{_entityName}_Ribbon";

            return _service.Create(ribbonCustomization);
        }

        private void DeployCustomActions()
        {
            Console.WriteLine("  部署CustomActions...");
            var customActions = _ribbonDoc.SelectNodes("//CustomActions/CustomAction");
            if (customActions == null) return;

            foreach (XmlNode action in customActions)
            {
                var actionId = action.Attributes?["Id"]?.Value;
                if (string.IsNullOrEmpty(actionId)) continue;

                // 删除旧的CustomAction
                DeleteRibbonElement("ribboncustomaction", "ribboncustomactionid", actionId);

                // 创建新的CustomAction
                var customAction = new Entity("ribboncustomaction");
                customAction["ribboncustomactionid"] = Guid.NewGuid();
                customAction["name"] = actionId;
                customAction["entity"] = new EntityReference("entity", _entityId);
                customAction["ribboncustomizationid"] = new EntityReference("ribboncustomization", _ribbonCustomizationId);
                customAction["location"] = action.Attributes?["Location"]?.Value;
                customAction["sequence"] = int.Parse(action.Attributes?["Sequence"]?.Value ?? "0");

                // 提取CommandUIDefinition的XML
                var cmdDef = action.SelectSingleNode("CommandUIDefinition");
                if (cmdDef != null)
                {
                    customAction["commanddefinition"] = cmdDef.InnerXml;
                }

                _service.Create(customAction);
                Console.WriteLine($"    + CustomAction: {actionId}");
            }
        }

        private void DeployCommandDefinitions()
        {
            Console.WriteLine("  部署CommandDefinitions...");
            var commands = _ribbonDoc.SelectNodes("//CommandDefinitions/CommandDefinition");
            if (commands == null) return;

            foreach (XmlNode cmd in commands)
            {
                var cmdId = cmd.Attributes?["Id"]?.Value;
                if (string.IsNullOrEmpty(cmdId)) continue;

                // 删除旧的Command
                DeleteRibbonElement("ribboncommand", "ribboncommandid", cmdId);

                // 创建新的Command
                var command = new Entity("ribboncommand");
                command["ribboncommandid"] = Guid.NewGuid();
                command["name"] = cmdId;
                command["entity"] = new EntityReference("entity", _entityId);
                command["ribboncustomizationid"] = new EntityReference("ribboncustomization", _ribbonCustomizationId);
                command["commanddefinition"] = cmd.OuterXml;

                _service.Create(command);
                Console.WriteLine($"    + Command: {cmdId}");
            }
        }

        private void DeployRuleDefinitions()
        {
            Console.WriteLine("  部署RuleDefinitions...");

            // DisplayRules
            var displayRules = _ribbonDoc.SelectNodes("//RuleDefinitions/DisplayRules/DisplayRule");
            if (displayRules != null)
            {
                foreach (XmlNode rule in displayRules)
                {
                    var ruleId = rule.Attributes?["Id"]?.Value;
                    if (string.IsNullOrEmpty(ruleId)) continue;

                    DeleteRibbonElement("ribbonrule", "ribbonruleid", ruleId);

                    var ribbonRule = new Entity("ribbonrule");
                    ribbonRule["ribbonruleid"] = Guid.NewGuid();
                    ribbonRule["name"] = ruleId;
                    ribbonRule["entity"] = new EntityReference("entity", _entityId);
                    ribbonRule["ribboncustomizationid"] = new EntityReference("ribboncustomization", _ribbonCustomizationId);
                    ribbonRule["ruletype"] = new OptionSetValue(0); // DisplayRule
                    ribbonRule["ruledefinition"] = rule.OuterXml;

                    _service.Create(ribbonRule);
                    Console.WriteLine($"    + DisplayRule: {ruleId}");
                }
            }

            // EnableRules
            var enableRules = _ribbonDoc.SelectNodes("//RuleDefinitions/EnableRules/EnableRule");
            if (enableRules != null)
            {
                foreach (XmlNode rule in enableRules)
                {
                    var ruleId = rule.Attributes?["Id"]?.Value;
                    if (string.IsNullOrEmpty(ruleId)) continue;

                    DeleteRibbonElement("ribbonrule", "ribbonruleid", ruleId);

                    var ribbonRule = new Entity("ribbonrule");
                    ribbonRule["ribbonruleid"] = Guid.NewGuid();
                    ribbonRule["name"] = ruleId;
                    ribbonRule["entity"] = new EntityReference("entity", _entityId);
                    ribbonRule["ribboncustomizationid"] = new EntityReference("ribboncustomization", _ribbonCustomizationId);
                    ribbonRule["ruletype"] = new OptionSetValue(1); // EnableRule
                    ribbonRule["ruledefinition"] = rule.OuterXml;

                    _service.Create(ribbonRule);
                    Console.WriteLine($"    + EnableRule: {ruleId}");
                }
            }
        }

        private void DeployLocLabels()
        {
            Console.WriteLine("  部署LocLabels...");
            var labels = _ribbonDoc.SelectNodes("//LocLabels/LocLabel");
            if (labels == null) return;

            foreach (XmlNode label in labels)
            {
                var labelId = label.Attributes?["Id"]?.Value;
                if (string.IsNullOrEmpty(labelId)) continue;

                // 删除旧的LocLabel
                DeleteRibbonElement("ribbonloclabel", "ribbonloclabelid", labelId);

                // 创建新的LocLabel
                var locLabel = new Entity("ribbonloclabel");
                locLabel["ribbonloclabelid"] = Guid.NewGuid();
                locLabel["name"] = labelId;
                locLabel["entity"] = new EntityReference("entity", _entityId);
                locLabel["ribboncustomizationid"] = new EntityReference("ribboncustomization", _ribbonCustomizationId);
                locLabel["labeldefinition"] = label.OuterXml;

                _service.Create(locLabel);
                Console.WriteLine($"    + LocLabel: {labelId}");
            }
        }

        private void UpdateEntityRibbonDiff()
        {
            Console.WriteLine("  更新实体RibbonDiffXml...");

            // 读取完整的RibbonDiff.xml内容
            var ribbonDiffContent = File.ReadAllText(_ribbonXmlPath);

            // 更新实体的RibbonDiffXml字段
            var entity = new Entity("entity", _entityId);
            entity["ribbondiffxml"] = ribbonDiffContent;

            _service.Update(entity);
            Console.WriteLine("  ✅ 实体RibbonDiffXml已更新");
        }

        private void DeleteRibbonElement(string entityName, string idField, string name)
        {
            try
            {
                var query = new QueryExpression(entityName)
                {
                    ColumnSet = new ColumnSet(idField),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("name", ConditionOperator.Equal, name)
                        }
                    }
                };

                var result = _service.RetrieveMultiple(query);
                foreach (var item in result.Entities)
                {
                    _service.Delete(entityName, item.Id);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    删除旧{entityName}失败: {ex.Message}");
            }
        }
    }
}
