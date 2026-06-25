/**************************************************************
*
* 命名空间  ：SanyD365.D365Extension.Sales.Plugins.CustomerTag
* 文 件 名  ：CustomerTagInitPlugin.cs
* 功能描述  ：客户信用标签表 - 创建初始化Plugin
* 注册信息  ：Create|mcs_customer_tag|PostOperation
*
*************************************************************/

using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using MSLibrary.D365.Common;
using MSLibrary.D365.Common.Context;
using MSLibrary.D365.Common.Plugins;
using System;

namespace SanyD365.D365Extension.Sales.Plugins.CustomerTag
{
    /// <summary>
    /// 客户信用标签表 - 创建初始化Plugin
    /// </summary>
    public class CustomerTagInitPlugin : PluginBase
    {
        public override void InnerExecute(IPluginExecutionContext context)
        {
            var service = ContextContainer.GetValue<IOrganizationService>(ContextTypes.OrgService);
            var tracer = ContextContainer.GetValue<ITracingService>(ContextTypes.TracingService);

            tracer?.Trace("CustomerTagInitPlugin 开始执行");

            if (context.MessageName != "Create" || context.Stage != 40)
            {
                tracer?.Trace("非Create后事件，跳过");
                return;
            }

            if (!context.InputParameters.Contains("Target") || !(context.InputParameters["Target"] is Entity target))
            {
                tracer?.Trace("未找到Target实体");
                return;
            }

            if (target.LogicalName != "mcs_customer_tag")
            {
                tracer?.Trace($"实体不匹配: {target.LogicalName}");
                return;
            }

            try
            {
                UpdateTagWithItemInfo(target, service, tracer);
                CopyIntegrationToReview(target, service, tracer);
                tracer?.Trace("CustomerTagInitPlugin 执行完成");
            }
            catch (Exception ex)
            {
                tracer?.Trace($"初始化失败: {ex.Message}");
            }
        }

        private void UpdateTagWithItemInfo(Entity target, IOrganizationService service, ITracingService tracer)
        {
            string itemCode = null;
            EntityReference creditItemRef = null;

            if (target.Contains("mcs_itemid"))
            {
                var itemAttr = target["mcs_itemid"];
                if (itemAttr is string)
                    itemCode = (string)itemAttr;
                else if (itemAttr is EntityReference itemRef)
                    creditItemRef = itemRef;
            }

            // 若表单上直接传了评分项目Lookup（如人工复核新建标签），优先使用
            if (creditItemRef == null && target.Contains("mcs_credit_item"))
                creditItemRef = target.GetAttributeValue<EntityReference>("mcs_credit_item");

            // 有引用但无编码时，反查编码
            if (creditItemRef != null && string.IsNullOrEmpty(itemCode))
            {
                var itemEntity = service.Retrieve("mcs_credit_items", creditItemRef.Id, new ColumnSet("mcs_credit_itemsno"));
                if (itemEntity != null)
                    itemCode = itemEntity.GetAttributeValue<string>("mcs_credit_itemsno");
            }

            if (string.IsNullOrEmpty(itemCode) && creditItemRef == null)
            {
                tracer?.Trace("评分项目编码/引用为空，跳过信息带出");
                return;
            }

            var query = new QueryExpression("mcs_credit_items")
            {
                ColumnSet = new ColumnSet("mcs_itemname", "mcs_itemdesc", "mcs_datatype", "mcs_group"),
                Criteria = new FilterExpression(),
                TopCount = 1
            };

            if (!string.IsNullOrEmpty(itemCode))
                query.Criteria.Conditions.Add(new ConditionExpression("mcs_credit_itemsno", ConditionOperator.Equal, itemCode));
            else
                query.Criteria.Conditions.Add(new ConditionExpression("mcs_credit_itemsid", ConditionOperator.Equal, creditItemRef.Id));

            var result = service.RetrieveMultiple(query);
            if (result.Entities.Count == 0)
            {
                tracer?.Trace($"评分项目 {itemCode} 不存在");
                return;
            }

            var item = result.Entities[0];
            var updateEntity = new Entity("mcs_customer_tag") { Id = target.Id };

            string itemName = item.GetAttributeValue<string>("mcs_itemname");
            if (!string.IsNullOrEmpty(itemName))
            {
                updateEntity["mcs_itemname"] = itemName;
                tracer?.Trace($"带出评分项目名称: {itemName}");
            }

            string itemDesc = item.GetAttributeValue<string>("mcs_itemdesc");
            if (!string.IsNullOrEmpty(itemDesc))
            {
                updateEntity["mcs_itemdesc"] = itemDesc;
                tracer?.Trace($"带出评分项目说明: {itemDesc}");
            }

            if (item.Contains("mcs_datatype"))
            {
                int rawDataType = item.GetAttributeValue<int>("mcs_datatype");
                // 评分项目表选项集值（100000000=定量，100000001=定性）→ 标签表（1=定量，2=定性）
                int dataType = rawDataType == 100000000 ? 1 : 2;
                updateEntity["mcs_datatype"] = dataType;
                tracer?.Trace($"带出数据类型: raw={rawDataType}, converted={dataType}");
            }

            if (item.Contains("mcs_group"))
            {
                int rawGroup = item.GetAttributeValue<int>("mcs_group");
                int group = ConvertGroupValue(rawGroup);
                updateEntity["mcs_group"] = group;
                tracer?.Trace($"带出评分项目分类: raw={rawGroup}, converted={group}");
            }

            // 若客户编码为空，从关联的信用评估记录带出
            if (!target.Contains("mcs_accountid") && target.Contains("mcs_credit_record"))
            {
                var creditRecordRef = target.GetAttributeValue<EntityReference>("mcs_credit_record");
                if (creditRecordRef != null)
                {
                    var creditRecord = service.Retrieve("mcs_credit_record", creditRecordRef.Id, new ColumnSet("mcs_accountid"));
                    var accountRef = creditRecord.GetAttributeValue<EntityReference>("mcs_accountid");
                    if (accountRef != null)
                    {
                        updateEntity["mcs_accountid"] = accountRef;
                        tracer?.Trace($"带出客户编码: {accountRef.Id}");
                    }
                }
            }

            service.Update(updateEntity);
            tracer?.Trace("评分项目信息带出完成");
        }

        private void CopyIntegrationToReview(Entity target, IOrganizationService service, ITracingService tracer)
        {
            var currentRecord = service.Retrieve("mcs_customer_tag", target.Id,
                new ColumnSet("mcs_datatype", "mcs_credit_item", "mcs_itemintvalue1", "mcs_itemtxtvalue1", "mcs_itemvalue1", "mcs_credititem_value"));

            if (currentRecord == null)
            {
                tracer?.Trace("无法读取当前记录");
                return;
            }

            int dataType = currentRecord.GetAttributeValue<int>("mcs_datatype");
            var creditItemRef = currentRecord.GetAttributeValue<EntityReference>("mcs_credit_item");
            var lookupRef = currentRecord.GetAttributeValue<EntityReference>("mcs_credititem_value");
            var updateEntity = new Entity("mcs_customer_tag") { Id = target.Id };
            string displayValue = "";

            if (dataType == 1)
            {
                if (currentRecord.Contains("mcs_itemintvalue1"))
                {
                    decimal intValue = currentRecord.GetAttributeValue<decimal>("mcs_itemintvalue1");
                    updateEntity["mcs_itemintvalue2"] = intValue;
                    displayValue = intValue.ToString("F2");
                    tracer?.Trace($"复制定量值: {intValue}");
                }
            }
            else if (dataType == 2)
            {
                if (lookupRef != null)
                {
                    // 人工新建时已选择Lookup：用Lookup名称回写展示字段
                    var enumRecord = service.Retrieve("mcs_credititem_value", lookupRef.Id,
                        new ColumnSet("mcs_listvalue", "mcs_listname"));
                    string listName = enumRecord.GetAttributeValue<string>("mcs_listname") ?? "";
                    string listValue = enumRecord.GetAttributeValue<string>("mcs_listvalue") ?? "";

                    updateEntity["mcs_itemtxtvalue2"] = listName;
                    updateEntity["mcs_itemvalue2"] = listName;
                    if (!string.IsNullOrEmpty(listValue))
                        updateEntity["mcs_itemvalue1"] = listValue;
                    displayValue = listName;
                    tracer?.Trace($"人工选择Lookup: name={listName}, value={listValue}");
                }
                else if (currentRecord.Contains("mcs_itemtxtvalue1") || currentRecord.Contains("mcs_itemvalue1"))
                {
                    // Coface集成：按原始值/显示名反查枚举值表
                    string txtValue = currentRecord.GetAttributeValue<string>("mcs_itemtxtvalue1") ?? "";
                    string rawValue = currentRecord.GetAttributeValue<string>("mcs_itemvalue1") ?? "";

                    var enumId = ResolveCreditItemValue(service, tracer, creditItemRef, rawValue, txtValue);
                    if (enumId.HasValue)
                    {
                        updateEntity["mcs_credititem_value"] = new EntityReference("mcs_credititem_value", enumId.Value);
                        tracer?.Trace($"反查定性枚举Lookup: {enumId.Value}");
                    }
                    else
                    {
                        tracer?.Trace($"未找到定性枚举映射: item={creditItemRef?.Id}, raw={rawValue}, txt={txtValue}");
                    }

                    updateEntity["mcs_itemtxtvalue2"] = txtValue;
                    displayValue = txtValue;
                    tracer?.Trace($"复制定性值: {txtValue}");
                }
            }

            if (!updateEntity.Contains("mcs_itemvalue1") && (currentRecord.Contains("mcs_itemintvalue1") || currentRecord.Contains("mcs_itemtxtvalue1")))
            {
                string intDisplay = "";
                string txtDisplay = "";
                if (currentRecord.Contains("mcs_itemintvalue1"))
                    intDisplay = currentRecord.GetAttributeValue<decimal>("mcs_itemintvalue1").ToString("F2");
                if (currentRecord.Contains("mcs_itemtxtvalue1"))
                    txtDisplay = currentRecord.GetAttributeValue<string>("mcs_itemtxtvalue1") ?? "";
                updateEntity["mcs_itemvalue1"] = dataType == 1 ? intDisplay : txtDisplay;
                tracer?.Trace($"更新集成展示值: {updateEntity["mcs_itemvalue1"]}");
            }

            if (!string.IsNullOrEmpty(displayValue))
            {
                updateEntity["mcs_itemvalue2"] = displayValue;
                tracer?.Trace($"更新复核展示值: {displayValue}");
            }

            updateEntity["mcs_active"] = true;
            updateEntity["mcs_isscore"] = false;

            service.Update(updateEntity);
            tracer?.Trace("复制集成值到复核值完成");
        }

        /// <summary>
        /// 根据评分项目和值反查 mcs_credititem_value 记录
        /// 优先按 mcs_listvalue（原始值）匹配，其次按 mcs_listname（显示名）匹配
        /// </summary>
        private Guid? ResolveCreditItemValue(IOrganizationService service, ITracingService tracer,
            EntityReference creditItemRef, string rawValue, string displayValue)
        {
            if (creditItemRef == null)
            {
                tracer?.Trace("ResolveCreditItemValue: 评分项目引用为空");
                return null;
            }

            var query = new QueryExpression("mcs_credititem_value")
            {
                ColumnSet = new ColumnSet("mcs_credititem_valueid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_credititemno", ConditionOperator.Equal, creditItemRef.Id),
                        new ConditionExpression("statecode", ConditionOperator.Equal, 0)
                    }
                },
                TopCount = 1
            };

            var orFilter = new FilterExpression(LogicalOperator.Or);
            if (!string.IsNullOrEmpty(rawValue))
            {
                orFilter.Conditions.Add(new ConditionExpression("mcs_listvalue", ConditionOperator.Equal, rawValue));
                orFilter.Conditions.Add(new ConditionExpression("mcs_listname", ConditionOperator.Equal, rawValue));
            }
            if (!string.IsNullOrEmpty(displayValue))
            {
                orFilter.Conditions.Add(new ConditionExpression("mcs_listvalue", ConditionOperator.Equal, displayValue));
                orFilter.Conditions.Add(new ConditionExpression("mcs_listname", ConditionOperator.Equal, displayValue));
            }

            if (orFilter.Conditions.Count == 0)
            {
                tracer?.Trace("ResolveCreditItemValue: 无可用匹配值");
                return null;
            }

            query.Criteria.AddFilter(orFilter);

            var result = service.RetrieveMultiple(query);
            if (result.Entities.Count > 0)
                return result.Entities[0].Id;

            return null;
        }

        /// <summary>
        /// 转换mcs_group值：评分项目表的选项集值 → 标签表的有效选项集值
        /// 评分项目表: 100000000=客户实力, 100000001=客户财务, 100000002=宏观市场, 100000003=历史交易, 100000004=综合指标
        /// 标签表: 1=客户实力, 2=客户财务, 3=宏观市场, 4=历史交易, 5=综合指标
        /// </summary>
        private int ConvertGroupValue(int rawValue)
        {
            switch (rawValue)
            {
                case 100000000: return 1;
                case 100000001: return 2;
                case 100000002: return 3;
                case 100000003: return 4;
                case 100000004: return 5;
                default: return rawValue <= 5 ? rawValue : 1;
            }
        }
    }
}
