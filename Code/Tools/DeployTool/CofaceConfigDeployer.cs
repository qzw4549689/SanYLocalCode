using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace DeployTool
{
    /// <summary>
    /// 部署 Coface 相关配置数据：
    /// 1. ms_systemconfiguration: CofaceCountryConfig
    /// 2. mcs_credit_items: 标记无外部数据源指标 mcs_source=内部
    /// </summary>
    public class CofaceConfigDeployer
    {
        private readonly ServiceClient _service;

        public CofaceConfigDeployer(ServiceClient service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public void Deploy()
        {
            Console.WriteLine(">>> 部署 Coface 配置数据...");
            DeployCountryConfig();
            UpdateInternalCreditItems();
            Console.WriteLine("  ✅ Coface 配置数据部署完成");
        }

        /// <summary>
        /// 创建/更新 CofaceCountryConfig 系统配置。
        /// </summary>
        private void DeployCountryConfig()
        {
            var config = new CofaceCountryConfigData
            {
                SplitFormatCountries = new List<string>
                {
                    "AF", "AG", "BI", "BT", "BZ", "CF", "CU", "DJ", "ER", "ET",
                    "GD", "GN", "GQ", "GW", "IQ", "IR", "KI", "KP", "KV", "LS",
                    "LY", "MN", "MZ", "NE", "RU", "SB", "SD", "SO", "SS", "ST",
                    "SY", "TL", "TV", "UA", "VE", "VU", "WS", "YE", "ZW"
                },
                CeeCountries = new List<string> { "RU" },
                CeeReportProductSlug = "full-report-cee",
                DefaultReportProductSlug = "full-report"
            };

            string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });

            var query = new QueryExpression("ms_systemconfiguration")
            {
                ColumnSet = new ColumnSet("ms_systemconfigurationid", "ms_name", "ms_content"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("ms_name", ConditionOperator.Equal, "CofaceCountryConfig") }
                }
            };

            var existing = _service.RetrieveMultiple(query).Entities.FirstOrDefault();

            var entity = new Entity("ms_systemconfiguration");
            entity["ms_name"] = "CofaceCountryConfig";
            entity["ms_content"] = json;

            if (existing != null)
            {
                entity.Id = existing.Id;
                _service.Update(entity);
                Console.WriteLine($"  ✅ CofaceCountryConfig 已更新: {existing.Id}");
            }
            else
            {
                var id = _service.Create(entity);
                Console.WriteLine($"  ✅ CofaceCountryConfig 已创建: {id}");
            }
        }

        /// <summary>
        /// 将 6 个无外部数据源指标标记为内部（mcs_source=100000000），其余保持/恢复为外部。
        /// 注意：DEV 环境中当前缺少"行业地位"、"财务报表真实性"两个指标，仅更新存在的 4 项。
        /// </summary>
        private void UpdateInternalCreditItems()
        {
            // 明确的无外部数据源指标编码（Coface 不提供的内部评估指标）
            var internalItemCodes = new[]
            {
                "ProjectAmt",      // 在手项目合同
                "ProductNum",      // 自有设备数量
                "DebtAmount",      // 还款来源
                "TotalAssets",     // 资产证明
                "OverdueModel",    // 逾期模型
                "BigAccount",      // 大客户占比
                "SalesAmount",     // 销售额
                "ARAmount",        // 应收账款金额
                "ARAge",           // 应收账款账龄
                "DealerRating"     // 经销商评级
                // "IndustryStatus"  // 行业地位 - DEV 中不存在
                // "FinancialStatementReliability" // 财务报表真实性 - DEV 中不存在
            };

            // 将指定指标设为内部
            UpdateSourceByCodes(internalItemCodes, 100000000);

            // 将其余已误标为内部的指标恢复为外部
            var query = new QueryExpression("mcs_credit_items")
            {
                ColumnSet = new ColumnSet("mcs_credit_itemsid", "mcs_credit_itemsno", "mcs_source"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_source", ConditionOperator.Equal, 100000000),
                        new ConditionExpression("mcs_credit_itemsno", ConditionOperator.NotIn, internalItemCodes)
                    }
                }
            };

            var toReset = _service.RetrieveMultiple(query).Entities;
            foreach (var item in toReset)
            {
                var update = new Entity("mcs_credit_items", item.Id);
                update["mcs_source"] = new OptionSetValue(100000001);
                _service.Update(update);
                var code = item.GetAttributeValue<string>("mcs_credit_itemsno");
                Console.WriteLine($"  ✅ 评分项目 {code} 已恢复为外部(100000001)");
            }
        }

        private void UpdateSourceByCodes(string[] itemCodes, int sourceValue)
        {
            var query = new QueryExpression("mcs_credit_items")
            {
                ColumnSet = new ColumnSet("mcs_credit_itemsid", "mcs_credit_itemsno", "mcs_source"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("mcs_credit_itemsno", ConditionOperator.In, itemCodes) }
                }
            };

            var items = _service.RetrieveMultiple(query).Entities;
            foreach (var item in items)
            {
                var currentSource = item.GetAttributeValue<OptionSetValue>("mcs_source")?.Value;
                if (currentSource == sourceValue) continue;

                var update = new Entity("mcs_credit_items", item.Id);
                update["mcs_source"] = new OptionSetValue(sourceValue);
                _service.Update(update);

                var code = item.GetAttributeValue<string>("mcs_credit_itemsno");
                Console.WriteLine($"  ✅ 评分项目 {code} 已设置 mcs_source={sourceValue}");
            }
        }

        private class CofaceCountryConfigData
        {
            public List<string> SplitFormatCountries { get; set; } = new();
            public List<string> CeeCountries { get; set; } = new();
            public string CeeReportProductSlug { get; set; } = "full-report-cee";
            public string DefaultReportProductSlug { get; set; } = "full-report";
        }
    }
}
