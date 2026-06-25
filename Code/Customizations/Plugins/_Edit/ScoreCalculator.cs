using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SanyD365.D365Extension.Sales.Application.Sales.CreditScore
{
    public class ScoreCalculator
    {
        private readonly IOrganizationService _service;
        private readonly ITracingService _tracer;

        public ScoreCalculator(IOrganizationService service, ITracingService tracer)
        {
            _service = service;
            _tracer = tracer;
        }

        public int CalculateScore(Guid creditRecordId, int categoryId)
        {
            _tracer.Trace($"开始计算信用分: creditRecordId={creditRecordId}, categoryId={categoryId}");

            // 1. 获取评分卡配置
            _tracer.Trace("步骤1: 获取评分卡配置...");
            var scoringCard = GetScoringCardConfig(categoryId);
            if (scoringCard.Count == 0)
            {
                throw new InvalidPluginExecutionException("该客户还未配置评分卡，请联系管理员配置后再进行信用分计算。");
            }
            _tracer.Trace($"评分卡配置项数: {scoringCard.Count}");

            // 2. 获取客户信用标签（复核值）
            _tracer.Trace("步骤2: 获取客户信用标签...");
            var tags = GetCustomerTags(creditRecordId);
            if (tags.Count == 0)
            {
                throw new InvalidPluginExecutionException("该客户还未完成数据集成，请先完成内外部数据集成后再进行信用分计算。");
            }
            _tracer.Trace($"客户标签数: {tags.Count}");

            // 3. 逐项计算得分
            _tracer.Trace("步骤3: 逐项计算得分...");
            int totalScore = 0;
            var scoreDetails = new List<ScoreDetail>();

            // 按评分项目分组，支持一个项目下多行评分卡配置
            var scoringCardGroups = scoringCard.GroupBy(c => c.ItemCode).ToDictionary(g => g.Key, g => g.ToList());
            _tracer.Trace($"评分卡分组数: {scoringCardGroups.Count}");

            foreach (var tag in tags)
            {
                string itemCode = tag.ItemCode;

                if (!scoringCardGroups.TryGetValue(itemCode, out var configs))
                {
                    _tracer.Trace($"❌ 指标 {itemCode} 无对应评分卡配置，跳过");
                    continue;
                }

                var firstConfig = configs.First();
                int dataType = tag.DataType;
                bool isQuantitative = (dataType == 1 || dataType == 100000000);

                _tracer.Trace($"=== 计算指标: {itemCode}, 数据类型={dataType}, 配置行数={configs.Count} ===");

                int itemScore = 0;

                if (isQuantitative)
                {
                    _tracer.Trace($"→ 定量评分: value={tag.DecimalValue}");
                    itemScore = CalculateQuantitativeScore(configs, tag.DecimalValue);
                }
                else // 定性
                {
                    _tracer.Trace($"→ 定性评分: tagValue={tag.StringValue}");
                    itemScore = CalculateQualitativeScore(configs, tag.StringValue);
                }

                totalScore += itemScore;
                scoreDetails.Add(new ScoreDetail
                {
                    TagId = tag.Id,
                    ItemCode = itemCode,
                    ItemName = firstConfig.ItemName,
                    RawValue = tag.RawValue,
                    Score = itemScore,
                    Weight = firstConfig.Weight
                });

                _tracer.Trace($"=== 指标 {itemCode} 结果: 原始值={tag.RawValue}, 得分={itemScore} ===");
            }

            // 4. 回写标签得分值和是否评分标记
            _tracer.Trace("步骤4: 回写标签得分...");
            SaveTagScores(scoreDetails);

            _tracer.Trace($"信用分计算完成: 总分={totalScore}");
            return totalScore;
        }

        private List<ScoringCardConfig> GetScoringCardConfig(int categoryId)
        {
            _tracer.Trace($"GetScoringCardConfig: categoryId={categoryId}");
            var result = new List<ScoringCardConfig>();

            try
            {
                var query = new QueryExpression("mcs_credit_scoringcard")
                {
                    ColumnSet = new ColumnSet("mcs_credititem", "mcs_itemname", "mcs_datatype", "mcs_listvalue", "mcs_minvalue", "mcs_maxvalue", "mcs_weight"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("mcs_categoryid", ConditionOperator.Equal, categoryId)
                        }
                    }
                };

                // 添加关联查询获取评分项目编码
                var link = new LinkEntity("mcs_credit_scoringcard", "mcs_credit_items", "mcs_credititem", "mcs_credit_itemsid", JoinOperator.LeftOuter)
                {
                    Columns = new ColumnSet("mcs_credit_itemsno"),
                    EntityAlias = "item"
                };
                query.LinkEntities.Add(link);

                // 添加关联查询获取定性项目枚举原始值 mcs_listvalue
                var enumLink = new LinkEntity("mcs_credit_scoringcard", "mcs_credititem_value", "mcs_listvalue", "mcs_credititem_valueid", JoinOperator.LeftOuter)
                {
                    Columns = new ColumnSet("mcs_listvalue"),
                    EntityAlias = "enum"
                };
                query.LinkEntities.Add(enumLink);

                _tracer.Trace("执行评分卡配置查询...");
                var records = _service.RetrieveMultiple(query);
                _tracer.Trace($"评分卡配置查询返回: {records.Entities.Count}条");

                foreach (var record in records.Entities)
                {
                    try
                    {
                        string itemCode = "";
                        
                        // 方式1: 通过LinkEntity获取
                        if (record.Contains("item.mcs_credit_itemsno"))
                        {
                            var aliased = record.GetAttributeValue<AliasedValue>("item.mcs_credit_itemsno");
                            if (aliased != null && aliased.Value != null)
                            {
                                itemCode = aliased.Value.ToString() ?? "";
                                _tracer.Trace($"通过LinkEntity获取编码: {itemCode}");
                            }
                        }
                        // 方式2: 直接Retrieve评分项目
                        else if (record.Contains("mcs_credititem"))
                        {
                            var itemObj = record["mcs_credititem"];
                            _tracer.Trace($"mcs_credititem类型: {itemObj?.GetType()?.Name ?? "null"}");
                            
                            if (itemObj is EntityReference itemRef)
                            {
                                _tracer.Trace($"通过EntityReference获取编码, ID={itemRef.Id}");
                                var item = _service.Retrieve("mcs_credit_items", itemRef.Id, new ColumnSet("mcs_credit_itemsno"));
                                if (item != null)
                                {
                                    var itemsNoObj = item["mcs_credit_itemsno"];
                                    _tracer.Trace($"mcs_credit_itemsno类型: {itemsNoObj?.GetType()?.Name ?? "null"}");
                                    itemCode = item.GetAttributeValue<string>("mcs_credit_itemsno") ?? "";
                                }
                            }
                        }

                        if (string.IsNullOrEmpty(itemCode))
                        {
                            _tracer.Trace("评分卡配置项缺少项目编码，跳过");
                            continue;
                        }

                        var dataTypeValue = record.GetAttributeValue<OptionSetValue>("mcs_datatype");
                        int dataType = dataTypeValue?.Value ?? 1;
                        
                        // mcs_listvalue 是 Lookup -> mcs_credititem_value，取枚举原始值 mcs_listvalue
                        string listValue = null;
                        if (record.Contains("enum.mcs_listvalue"))
                        {
                            var aliased = record.GetAttributeValue<AliasedValue>("enum.mcs_listvalue");
                            if (aliased?.Value != null)
                            {
                                listValue = aliased.Value.ToString();
                                _tracer.Trace($"mcs_listvalue 通过LinkEntity读取, 原始值={listValue}");
                            }
                        }
                        else if (record.Contains("mcs_listvalue"))
                        {
                            var lvObj = record["mcs_listvalue"];
                            if (lvObj is EntityReference lvRef)
                            {
                                // 备用：直接读取 Lookup 的 mcs_listvalue 字段
                                var enumRec = _service.Retrieve("mcs_credititem_value", lvRef.Id, new ColumnSet("mcs_listvalue"));
                                listValue = enumRec.GetAttributeValue<string>("mcs_listvalue") ?? "";
                                _tracer.Trace($"mcs_listvalue 是EntityReference, 反查原始值={listValue}");
                            }
                            else if (lvObj != null)
                            {
                                listValue = lvObj.ToString();
                                _tracer.Trace($"mcs_listvalue 是{lvObj.GetType().Name}, 值={listValue}");
                            }
                        }

                        result.Add(new ScoringCardConfig
                        {
                            ItemCode = itemCode,
                            ItemName = record.GetAttributeValue<string>("mcs_itemname") ?? itemCode,
                            DataType = dataType,
                            ListValue = listValue,
                            MinValue = record.GetAttributeValue<decimal?>("mcs_minvalue"),
                            MaxValue = record.GetAttributeValue<decimal?>("mcs_maxvalue"),
                            Weight = record.GetAttributeValue<int>("mcs_weight")
                        });
                        
                        _tracer.Trace($"添加评分卡配置: {itemCode}, 类型={dataType}");
                    }
                    catch (Exception ex)
                    {
                        _tracer.Trace($"处理评分卡配置项异常: {ex.Message}");
                        _tracer.Trace($"异常堆栈: {ex.StackTrace}");
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                _tracer.Trace($"GetScoringCardConfig异常: {ex.Message}");
                _tracer.Trace($"异常堆栈: {ex.StackTrace}");
                throw;
            }

            return result;
        }

        private List<CustomerTag> GetCustomerTags(Guid creditRecordId)
        {
            _tracer.Trace($"GetCustomerTags: creditRecordId={creditRecordId}");
            var result = new List<CustomerTag>();

            try
            {
                var query = new QueryExpression("mcs_customer_tag")
                {
                    ColumnSet = new ColumnSet("mcs_customer_tagid", "mcs_credit_item", "mcs_datatype", "mcs_itemintvalue2", "mcs_itemtxtvalue2", "mcs_itemvalue2", "mcs_itemintvalue1", "mcs_itemtxtvalue1", "mcs_itemvalue1", "mcs_credititem_value"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("mcs_credit_record", ConditionOperator.Equal, creditRecordId),
                            new ConditionExpression("mcs_active", ConditionOperator.Equal, true)
                        }
                    }
                };

                // 关联评分项目表获取编码
                var link = new LinkEntity("mcs_customer_tag", "mcs_credit_items", "mcs_credit_item", "mcs_credit_itemsid", JoinOperator.LeftOuter)
                {
                    Columns = new ColumnSet("mcs_credit_itemsno"),
                    EntityAlias = "item"
                };
                query.LinkEntities.Add(link);

                // 关联定性枚举表，优先读取复核 Lookup 的原始值 mcs_listvalue
                var enumLink = new LinkEntity("mcs_customer_tag", "mcs_credititem_value", "mcs_credititem_value", "mcs_credititem_valueid", JoinOperator.LeftOuter)
                {
                    Columns = new ColumnSet("mcs_listvalue"),
                    EntityAlias = "enum"
                };
                query.LinkEntities.Add(enumLink);

                _tracer.Trace("执行标签查询...");
                var records = _service.RetrieveMultiple(query);
                _tracer.Trace($"标签查询返回: {records.Entities.Count}条");

                foreach (var record in records.Entities)
                {
                    try
                    {
                        string itemCode = "";
                        
                        // 方式1: 通过LinkEntity获取
                        if (record.Contains("item.mcs_credit_itemsno"))
                        {
                            var aliased = record.GetAttributeValue<AliasedValue>("item.mcs_credit_itemsno");
                            _tracer.Trace($"标签ID={record.Id}, AliasedValue={aliased?.Value?.ToString() ?? "null"}");
                            if (aliased != null && aliased.Value != null)
                            {
                                itemCode = aliased.Value.ToString() ?? "";
                            }
                        }
                        
                        // 方式2: 通过mcs_credit_item EntityReference直接查询
                        if (string.IsNullOrEmpty(itemCode) && record.Contains("mcs_credit_item"))
                        {
                            var itemObj = record["mcs_credit_item"];
                            _tracer.Trace($"标签ID={record.Id}, mcs_credit_item类型={itemObj?.GetType()?.Name ?? "null"}");
                            if (itemObj is EntityReference itemRef)
                            {
                                var item = _service.Retrieve("mcs_credit_items", itemRef.Id, new ColumnSet("mcs_credit_itemsno"));
                                if (item != null && item.Contains("mcs_credit_itemsno"))
                                {
                                    itemCode = item.GetAttributeValue<string>("mcs_credit_itemsno") ?? "";
                                    _tracer.Trace($"标签ID={record.Id}, 通过EntityReference获取编码={itemCode}");
                                }
                            }
                        }

                        if (string.IsNullOrEmpty(itemCode))
                        {
                            _tracer.Trace($"❌ 标签记录缺少项目编码, ID={record.Id}, 所有属性={string.Join(",", record.Attributes.Keys)}");
                            continue;
                        }
                        _tracer.Trace($"✓ 标签记录编码={itemCode}, ID={record.Id}");

                        int dataType = record.GetAttributeValue<OptionSetValue>("mcs_datatype")?.Value ?? 1;
                        decimal decimalValue = -1;
                        string stringValue = "O";
                        string rawValue = "";

                        bool isQuantitative = (dataType == 1 || dataType == 100000000);
                        
                        if (isQuantitative)
                        {
                            // 优先读复核值(value2)，空则回退读原始值(value1)
                            if (record.Contains("mcs_itemintvalue2") && record["mcs_itemintvalue2"] != null)
                            {
                                decimalValue = record.GetAttributeValue<decimal>("mcs_itemintvalue2");
                                rawValue = decimalValue.ToString("F2");
                            }
                            else if (record.Contains("mcs_itemvalue2") && record["mcs_itemvalue2"] != null)
                            {
                                decimal.TryParse(record.GetAttributeValue<string>("mcs_itemvalue2"), out decimalValue);
                                rawValue = decimalValue.ToString("F2");
                            }
                            else if (record.Contains("mcs_itemintvalue1") && record["mcs_itemintvalue1"] != null)
                            {
                                decimalValue = record.GetAttributeValue<decimal>("mcs_itemintvalue1");
                                rawValue = decimalValue.ToString("F2");
                                _tracer.Trace($"标签{itemCode}: value2为空，回退读取value1={decimalValue}");
                            }
                            else if (record.Contains("mcs_itemvalue1") && record["mcs_itemvalue1"] != null)
                            {
                                decimal.TryParse(record.GetAttributeValue<string>("mcs_itemvalue1"), out decimalValue);
                                rawValue = decimalValue.ToString("F2");
                                _tracer.Trace($"标签{itemCode}: value2为空，回退读取value1={decimalValue}");
                            }
                        }
                        else // 定性
                        {
                            bool lookupResolved = false;
                            // 优先从复核定性指标 Lookup 读取原始枚举值 mcs_listvalue
                            if (record.Contains("enum.mcs_listvalue"))
                            {
                                var aliased = record.GetAttributeValue<AliasedValue>("enum.mcs_listvalue");
                                if (aliased?.Value != null)
                                {
                                    stringValue = aliased.Value.ToString();
                                    rawValue = stringValue;
                                    lookupResolved = true;
                                    _tracer.Trace($"标签{itemCode}: 从Lookup读取listvalue={stringValue}");
                                }
                            }

                            if (!lookupResolved)
                            {
                                // Lookup 无值时回退：优先读复核值(value2)，空则回退读原始值(value1)
                                if (record.Contains("mcs_itemtxtvalue2") && record["mcs_itemtxtvalue2"] != null)
                                {
                                    stringValue = record.GetAttributeValue<string>("mcs_itemtxtvalue2") ?? "O";
                                    rawValue = stringValue;
                                }
                                else if (record.Contains("mcs_itemvalue2") && record["mcs_itemvalue2"] != null)
                                {
                                    stringValue = record.GetAttributeValue<string>("mcs_itemvalue2") ?? "O";
                                    rawValue = stringValue;
                                }
                                else if (record.Contains("mcs_itemtxtvalue1") && record["mcs_itemtxtvalue1"] != null)
                                {
                                    stringValue = record.GetAttributeValue<string>("mcs_itemtxtvalue1") ?? "O";
                                    rawValue = stringValue;
                                    _tracer.Trace($"标签{itemCode}: value2为空，回退读取value1={stringValue}");
                                }
                                else if (record.Contains("mcs_itemvalue1") && record["mcs_itemvalue1"] != null)
                                {
                                    stringValue = record.GetAttributeValue<string>("mcs_itemvalue1") ?? "O";
                                    rawValue = stringValue;
                                    _tracer.Trace($"标签{itemCode}: value2为空，回退读取value1={stringValue}");
                                }
                            }
                        }

                        result.Add(new CustomerTag
                        {
                            Id = record.Id,
                            ItemCode = itemCode,
                            DataType = dataType,
                            DecimalValue = decimalValue,
                            StringValue = stringValue,
                            RawValue = rawValue
                        });
                    }
                    catch (Exception ex)
                    {
                        _tracer.Trace($"处理标签记录异常, ID={record.Id}: {ex.Message}");
                        _tracer.Trace($"异常堆栈: {ex.StackTrace}");
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                _tracer.Trace($"GetCustomerTags异常: {ex.Message}");
                _tracer.Trace($"异常堆栈: {ex.StackTrace}");
                throw;
            }

            return result;
        }

        private int CalculateQuantitativeScore(List<ScoringCardConfig> configs, decimal value)
        {
            string itemCode = configs.FirstOrDefault()?.ItemCode ?? "";

            // 先尝试按配置区间匹配；也允许通过配置行（如 min=-1, max=0）显式处理缺失值
            var matched = configs.FirstOrDefault(c =>
                c.MinValue.HasValue && c.MaxValue.HasValue &&
                value >= c.MinValue.Value && value < c.MaxValue.Value);

            if (matched != null)
            {
                _tracer.Trace($"定量指标 {matched.ItemCode}: 值 {value} 匹配区间 [{matched.MinValue}, {matched.MaxValue})，得分={matched.Weight}");
                return matched.Weight;
            }

            // 未匹配任何分档时，取该项目所有分档权重的平均分
            int avgScore = CalculateAverageWeight(configs);
            _tracer.Trace($"定量指标 {itemCode}: 值 {value} 未匹配任何区间，取平均分={avgScore}");
            return avgScore;
        }

        private int CalculateQualitativeScore(List<ScoringCardConfig> configs, string value)
        {
            string itemCode = configs.FirstOrDefault()?.ItemCode ?? "";
            bool isMissing = string.IsNullOrEmpty(value) || value == "O";

            // 先尝试按配置匹配（支持显式配置“无/缺失”行，如 listvalue=O 或空）
            var matched = configs.FirstOrDefault(c =>
                (!string.IsNullOrEmpty(c.ListValue) && value != null && value.Equals(c.ListValue, StringComparison.OrdinalIgnoreCase)) ||
                (string.IsNullOrEmpty(c.ListValue) && isMissing));

            if (matched != null)
            {
                _tracer.Trace($"定性指标 {matched.ItemCode}: 值 {value ?? "(空)"} 匹配 listvalue={matched.ListValue ?? "(空)"}，得分={matched.Weight}");
                return matched.Weight;
            }

            // 未匹配任何分档时，取该项目所有分档权重的平均分
            int avgScore = CalculateAverageWeight(configs);
            _tracer.Trace($"定性指标 {itemCode}: 值 {value ?? "(空)"} 未匹配任何配置，取平均分={avgScore}");
            return avgScore;
        }

        private int CalculateAverageWeight(List<ScoringCardConfig> configs)
        {
            if (configs == null || configs.Count == 0) return 0;
            double avg = configs.Average(c => c.Weight);
            return (int)Math.Round(avg, MidpointRounding.AwayFromZero);
        }

        private class ScoringCardConfig
        {
            public string ItemCode { get; set; }
            public string ItemName { get; set; }
            public int DataType { get; set; }
            public string ListValue { get; set; }
            public decimal? MinValue { get; set; }
            public decimal? MaxValue { get; set; }
            public int Weight { get; set; }
        }

        /// <summary>
        /// 将计算出的得分回写到客户信用标签表
        /// </summary>
        private void SaveTagScores(List<ScoreDetail> scoreDetails)
        {
            if (scoreDetails == null || scoreDetails.Count == 0) return;

            foreach (var detail in scoreDetails)
            {
                if (detail.TagId == Guid.Empty)
                {
                    _tracer.Trace($"指标 {detail.ItemCode} 缺少标签ID，跳过得分回写");
                    continue;
                }

                try
                {
                    var updateTag = new Entity("mcs_customer_tag", detail.TagId);
                    updateTag["mcs_scorevalue"] = detail.Score;
                    updateTag["mcs_isscore"] = true;
                    _service.Update(updateTag);
                    _tracer.Trace($"回写标签得分: TagId={detail.TagId}, ItemCode={detail.ItemCode}, Score={detail.Score}");
                }
                catch (Exception ex)
                {
                    _tracer.Trace($"回写标签得分失败: TagId={detail.TagId}, ItemCode={detail.ItemCode}, Error={ex.Message}");
                    // 不回抛，避免影响信用分计算主流程
                }
            }
        }

        private class CustomerTag
        {
            public Guid Id { get; set; }
            public string ItemCode { get; set; }
            public int DataType { get; set; }
            public decimal DecimalValue { get; set; }
            public string StringValue { get; set; }
            public string RawValue { get; set; }
        }

        public class ScoreDetail
        {
            public Guid TagId { get; set; }
            public string ItemCode { get; set; }
            public string ItemName { get; set; }
            public string RawValue { get; set; }
            public int Score { get; set; }
            public int Weight { get; set; }
        }
    }
}
