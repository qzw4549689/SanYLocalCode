using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using MSLibrary.D365.Common;
using MSLibrary.D365.Common.Context;
using MSLibrary.D365.Common.Plugins;
using System;
using System.Linq;

namespace SanyD365.D365Extension.Sales.Plugins.ScoringCard
{
    /// <summary>
    /// 评分卡配置校验 Plugin
    /// 触发时机：mcs_credit_scoringcard Create/Update PreOperation
    /// 功能：同一 category + 评分项目 下，定性值不能重复，定量区间不能重叠
    /// </summary>
    public class CreditScoringCardValidationPlugin : PluginBase
    {
        public override void InnerExecute(IPluginExecutionContext context)
        {
            var service = ContextContainer.GetValue<IOrganizationService>(ContextTypes.OrgService);
            var tracer = ContextContainer.GetValue<ITracingService>(ContextTypes.TracingService);

            tracer?.Trace("CreditScoringCardValidationPlugin 开始执行");

            if (context.MessageName != "Create" && context.MessageName != "Update")
            {
                tracer?.Trace("非Create/Update事件，跳过");
                return;
            }

            if (context.Stage != 20)
            {
                tracer?.Trace("非PreOperation阶段，跳过");
                return;
            }

            if (!context.InputParameters.Contains("Target") || !(context.InputParameters["Target"] is Entity target))
            {
                tracer?.Trace("未找到Target实体");
                return;
            }

            if (target.LogicalName != "mcs_credit_scoringcard")
            {
                tracer?.Trace($"实体不匹配: {target.LogicalName}");
                return;
            }

            bool isCreate = context.MessageName == "Create";
            Guid currentId = target.Id;

            // 获取 category 和评分项目
            int? categoryId = GetCategoryId(target, service, currentId, isCreate, tracer);
            EntityReference creditItem = GetCreditItem(target, service, currentId, isCreate, tracer);

            if (categoryId == null || creditItem == null)
            {
                tracer?.Trace("category或评分项目为空，跳过校验");
                return;
            }

            // 获取当前数据类型和校验所需值
            int dataType = GetDataType(target, service, currentId, isCreate, tracer);
            string currentListValue = GetListValueRaw(target, service, currentId, isCreate, tracer);
            decimal? currentMin = GetDecimalValue(target, "mcs_minvalue", service, currentId, isCreate, tracer);
            decimal? currentMax = GetDecimalValue(target, "mcs_maxvalue", service, currentId, isCreate, tracer);

            tracer?.Trace($"校验参数: categoryId={categoryId}, creditItem={creditItem.Id}, dataType={dataType}, listValue={currentListValue}, min={currentMin}, max={currentMax}");

            // 查询同一 category + 评分项目 下的其他配置
            var query = new QueryExpression("mcs_credit_scoringcard")
            {
                ColumnSet = new ColumnSet("mcs_datatype", "mcs_minvalue", "mcs_maxvalue"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_categoryid", ConditionOperator.Equal, categoryId.Value),
                        new ConditionExpression("mcs_credititem", ConditionOperator.Equal, creditItem.Id)
                    }
                }
            };

            // Create 时记录尚未写入，不需要也不应该排除 currentId
            if (!isCreate && currentId != Guid.Empty)
            {
                query.Criteria.AddCondition("mcs_credit_scoringcardid", ConditionOperator.NotEqual, currentId);
            }

            // 关联获取定性枚举原始值
            var enumLink = new LinkEntity("mcs_credit_scoringcard", "mcs_credititem_value", "mcs_listvalue", "mcs_credititem_valueid", JoinOperator.LeftOuter)
            {
                Columns = new ColumnSet("mcs_listvalue"),
                EntityAlias = "enum"
            };
            query.LinkEntities.Add(enumLink);

            var existingRecords = service.RetrieveMultiple(query);

            foreach (var record in existingRecords.Entities)
            {
                int existingDataType = record.GetAttributeValue<OptionSetValue>("mcs_datatype")?.Value ?? dataType;

                if (IsQualitative(dataType) || IsQualitative(existingDataType))
                {
                    string existingListValue = GetRawListValue(record);
                    if (!string.IsNullOrEmpty(currentListValue) &&
                        !string.IsNullOrEmpty(existingListValue) &&
                        currentListValue.Equals(existingListValue, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidPluginExecutionException(
                            $"同一评分项目下已存在定性值 '{existingListValue}' 的评分卡配置，不允许重复录入。");
                    }
                }
                else
                {
                    decimal? existingMin = record.GetAttributeValue<decimal?>("mcs_minvalue");
                    decimal? existingMax = record.GetAttributeValue<decimal?>("mcs_maxvalue");

                    if (IntervalsOverlap(currentMin, currentMax, existingMin, existingMax))
                    {
                        throw new InvalidPluginExecutionException(
                            $"定量区间 [{currentMin}, {currentMax}) 与已有区间 [{existingMin}, {existingMax}) 重叠，请检查评分卡配置。");
                    }
                }
            }

            tracer?.Trace("CreditScoringCardValidationPlugin 校验通过");
        }

        private bool IsQualitative(int dataType)
        {
            return dataType == 2 || dataType == 100000001;
        }

        private bool IntervalsOverlap(decimal? aMin, decimal? aMax, decimal? bMin, decimal? bMax)
        {
            if (!aMin.HasValue || !aMax.HasValue || !bMin.HasValue || !bMax.HasValue)
                return false;

            return aMin.Value < bMax.Value && bMin.Value < aMax.Value;
        }

        private int? GetCategoryId(Entity target, IOrganizationService service, Guid recordId, bool isCreate, ITracingService tracer)
        {
            if (target.Contains("mcs_categoryid"))
            {
                var opt = target["mcs_categoryid"] as OptionSetValue;
                if (opt != null) return opt.Value;
            }

            // Create 时记录尚未写入数据库，不能 Retrieve 自己
            if (!isCreate && recordId != Guid.Empty)
            {
                var record = service.Retrieve("mcs_credit_scoringcard", recordId, new ColumnSet("mcs_categoryid"));
                if (record != null && record.Contains("mcs_categoryid"))
                {
                    var opt = record["mcs_categoryid"] as OptionSetValue;
                    if (opt != null) return opt.Value;
                }
            }

            tracer?.Trace("无法获取categoryId");
            return null;
        }

        private EntityReference GetCreditItem(Entity target, IOrganizationService service, Guid recordId, bool isCreate, ITracingService tracer)
        {
            if (target.Contains("mcs_credititem"))
            {
                var er = target["mcs_credititem"] as EntityReference;
                if (er != null) return er;
            }

            if (!isCreate && recordId != Guid.Empty)
            {
                var record = service.Retrieve("mcs_credit_scoringcard", recordId, new ColumnSet("mcs_credititem"));
                if (record != null && record.Contains("mcs_credititem"))
                {
                    var er = record["mcs_credititem"] as EntityReference;
                    if (er != null) return er;
                }
            }

            tracer?.Trace("无法获取creditItem");
            return null;
        }

        private int GetDataType(Entity target, IOrganizationService service, Guid recordId, bool isCreate, ITracingService tracer)
        {
            if (target.Contains("mcs_datatype"))
            {
                var opt = target["mcs_datatype"] as OptionSetValue;
                if (opt != null) return opt.Value;
            }

            if (!isCreate && recordId != Guid.Empty)
            {
                var record = service.Retrieve("mcs_credit_scoringcard", recordId, new ColumnSet("mcs_datatype"));
                if (record != null && record.Contains("mcs_datatype"))
                {
                    var opt = record["mcs_datatype"] as OptionSetValue;
                    if (opt != null) return opt.Value;
                }
            }

            tracer?.Trace("无法获取dataType，默认按定量处理");
            return 1;
        }

        private string GetListValueRaw(Entity target, IOrganizationService service, Guid recordId, bool isCreate, ITracingService tracer)
        {
            if (target.Contains("mcs_listvalue"))
            {
                var er = target["mcs_listvalue"] as EntityReference;
                if (er != null)
                {
                    var enumRec = service.Retrieve("mcs_credititem_value", er.Id, new ColumnSet("mcs_listvalue"));
                    if (enumRec != null)
                    {
                        return enumRec.GetAttributeValue<string>("mcs_listvalue") ?? "";
                    }
                }
            }

            if (!isCreate && recordId != Guid.Empty)
            {
                var record = service.Retrieve("mcs_credit_scoringcard", recordId, new ColumnSet("mcs_listvalue"));
                if (record != null && record.Contains("mcs_listvalue"))
                {
                    var er = record["mcs_listvalue"] as EntityReference;
                    if (er != null)
                    {
                        var enumRec = service.Retrieve("mcs_credititem_value", er.Id, new ColumnSet("mcs_listvalue"));
                        if (enumRec != null)
                        {
                            return enumRec.GetAttributeValue<string>("mcs_listvalue") ?? "";
                        }
                    }
                }
            }

            return null;
        }

        private string GetRawListValue(Entity record)
        {
            if (record.Contains("enum.mcs_listvalue"))
            {
                var aliased = record.GetAttributeValue<AliasedValue>("enum.mcs_listvalue");
                if (aliased?.Value != null)
                {
                    return aliased.Value.ToString();
                }
            }

            return null;
        }

        private decimal? GetDecimalValue(Entity target, string attributeName, IOrganizationService service, Guid recordId, bool isCreate, ITracingService tracer)
        {
            if (target.Contains(attributeName) && target[attributeName] != null)
            {
                return target.GetAttributeValue<decimal?>(attributeName);
            }

            if (!isCreate && recordId != Guid.Empty)
            {
                var record = service.Retrieve("mcs_credit_scoringcard", recordId, new ColumnSet(attributeName));
                if (record != null && record.Contains(attributeName) && record[attributeName] != null)
                {
                    return record.GetAttributeValue<decimal?>(attributeName);
                }
            }

            return null;
        }
    }
}
