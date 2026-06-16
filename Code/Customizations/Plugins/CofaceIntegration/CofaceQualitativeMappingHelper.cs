using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SanyD365.Plugins.CofaceIntegration
{
    /// <summary>
    /// Coface 定性指标值映射配置读取帮助类
    /// 从 mcs_credititem_value 实体读取 Coface 原始值 → 中文显示文本映射
    /// </summary>
    public static class CofaceQualitativeMappingHelper
    {
        public const string EntityName = "mcs_credititem_value";
        public const string ItemNoField = "mcs_credititemno";
        public const string ListValueField = "mcs_listvalue";
        public const string ListNameField = "mcs_listname";

        /// <summary>
        /// 根据评分项目编码和 Coface 原始值获取中文显示文本
        /// 查询 mcs_credititem_value 中 mcs_credititemno + mcs_listvalue 匹配的记录，返回 mcs_listname
        /// </summary>
        /// <param name="service">组织服务</param>
        /// <param name="tracer">跟踪服务</param>
        /// <param name="itemCode">评分项目编码，如 SectorRisk / CountryRisk / ExternalRating</param>
        /// <param name="value">Coface 原始值，如 2 / A3 / 10</param>
        /// <returns>中文显示文本；未配置时返回原值</returns>
        public static string GetDisplayName(
            IOrganizationService service,
            ITracingService tracer,
            string itemCode,
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                tracer.Trace($"定性指标 {itemCode} 的原始值为空，返回缺失");
                return "缺失";
            }

            if (string.IsNullOrWhiteSpace(itemCode))
            {
                tracer.Trace("评分项目编码为空，无法查询定性映射，返回原值");
                return value;
            }

            if (service == null)
            {
                tracer.Trace("IOrganizationService 为空，无法查询定性指标映射配置");
                return value;
            }

            try
            {
                // 1. 根据评分项目编码查找 mcs_credit_items 的 ID
                var itemQuery = new QueryExpression("mcs_credit_items")
                {
                    ColumnSet = new ColumnSet("mcs_credit_itemsid"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("mcs_credit_itemsno", ConditionOperator.Equal, itemCode)
                        }
                    },
                    TopCount = 1
                };

                var itemResults = service.RetrieveMultiple(itemQuery);
                var item = itemResults.Entities.FirstOrDefault();
                if (item == null)
                {
                    tracer.Trace($"未找到评分项目: {itemCode}，返回原值");
                    return value;
                }

                // 2. 查询 mcs_credititem_value
                var query = new QueryExpression(EntityName)
                {
                    ColumnSet = new ColumnSet(ListNameField),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression(ItemNoField, ConditionOperator.Equal, item.Id),
                            new ConditionExpression(ListValueField, ConditionOperator.Equal, value)
                        }
                    },
                    TopCount = 1
                };

                var records = service.RetrieveMultiple(query);
                var record = records.Entities.FirstOrDefault();
                if (record == null)
                {
                    tracer.Trace($"定性指标映射未配置: {itemCode}/{value}，返回原值");
                    return value;
                }

                var displayName = record.GetAttributeValue<string>(ListNameField) ?? value;
                tracer.Trace($"定性指标映射: {itemCode}/{value} => {displayName}");
                return displayName;
            }
            catch (Exception ex)
            {
                tracer.Trace($"查询定性指标映射异常 [{itemCode}/{value}]: {ex.Message}");
                return value;
            }
        }

        /// <summary>
        /// 批量加载指定评分项目的所有定性映射（用于性能优化场景）
        /// </summary>
        public static Dictionary<string, string> LoadMappings(
            IOrganizationService service,
            ITracingService tracer,
            string itemCode)
        {
            var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(itemCode) || service == null)
                return mappings;

            try
            {
                var itemQuery = new QueryExpression("mcs_credit_items")
                {
                    ColumnSet = new ColumnSet("mcs_credit_itemsid"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("mcs_credit_itemsno", ConditionOperator.Equal, itemCode)
                        }
                    },
                    TopCount = 1
                };

                var itemResults = service.RetrieveMultiple(itemQuery);
                var item = itemResults.Entities.FirstOrDefault();
                if (item == null)
                    return mappings;

                var query = new QueryExpression(EntityName)
                {
                    ColumnSet = new ColumnSet(ListValueField, ListNameField),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression(ItemNoField, ConditionOperator.Equal, item.Id)
                        }
                    }
                };

                var records = service.RetrieveMultiple(query);
                foreach (var record in records.Entities)
                {
                    var listValue = record.GetAttributeValue<string>(ListValueField);
                    var listName = record.GetAttributeValue<string>(ListNameField);
                    if (!string.IsNullOrWhiteSpace(listValue))
                    {
                        mappings[listValue] = listName ?? listValue;
                    }
                }

                tracer.Trace($"加载定性指标映射: {itemCode} 共 {mappings.Count} 条");
            }
            catch (Exception ex)
            {
                tracer.Trace($"加载定性指标映射异常 [{itemCode}]: {ex.Message}");
            }

            return mappings;
        }
    }
}
