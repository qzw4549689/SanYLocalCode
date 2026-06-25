using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SanyD365.Plugins.TradeStPayTerm
{
    /// <summary>
    /// 成交条件样板库 - 保存校验 Plugin
    /// 触发时机：Create/Update PreOperation
    /// </summary>
    public class TradeStPayTermValidationPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = factory.CreateOrganizationService(context.UserId);
            ITracingService tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracer.Trace("TradeStPayTermValidationPlugin 开始执行");

            if (!context.InputParameters.Contains("Target") || !(context.InputParameters["Target"] is Entity))
            {
                tracer.Trace("未找到 Target 实体");
                return;
            }

            Entity target = (Entity)context.InputParameters["Target"];

            if (target.LogicalName != "mcs_trade_stpayterm")
            {
                tracer.Trace($"实体不匹配: {target.LogicalName}");
                return;
            }

            try
            {
                ValidateBusinessRules(target, context, service, tracer);
            }
            catch (Exception ex)
            {
                tracer.Trace($"校验失败: {ex.Message}");
                throw new InvalidPluginExecutionException($"保存失败: {ex.Message}");
            }
        }

        private void ValidateBusinessRules(Entity target, IPluginExecutionContext context, IOrganizationService service, ITracingService tracer)
        {
            // 0. 创建时状态默认值 = 0（未生效）
            if (context.MessageName == "Create" && (!target.Contains("mcs_status") || target["mcs_status"] == null))
            {
                target["mcs_status"] = new OptionSetValue(0);
            }

            // 1. 首付款比例校验
            if (target.Contains("mcs_downpay"))
            {
                decimal downPay = target.GetAttributeValue<decimal>("mcs_downpay");
                if (downPay < 0 || downPay > 1)
                {
                    throw new InvalidPluginExecutionException("首付款比例必须在 0% 到 100% 之间");
                }

                // 100% 首付款一致性校验
                if (downPay == 1)
                {
                    if (target.Contains("mcs_payterm") && target.GetAttributeValue<int>("mcs_payterm") != 0)
                    {
                        throw new InvalidPluginExecutionException("首付款比例为 100% 时，账期必须为 0");
                    }
                    if (target.Contains("mcs_payfreq") && target.GetAttributeValue<int>("mcs_payfreq") != 0)
                    {
                        throw new InvalidPluginExecutionException("首付款比例为 100% 时，付款频次必须为 0");
                    }
                }
            }

            // 2. 账期/付款频次 30 倍数校验
            if (target.Contains("mcs_payterm"))
            {
                int payTerm = target.GetAttributeValue<int>("mcs_payterm");
                if (payTerm < 0 || (payTerm != 0 && payTerm % 30 != 0))
                {
                    throw new InvalidPluginExecutionException("账期（天）必须是 0 或 30 的倍数");
                }
            }

            if (target.Contains("mcs_payfreq"))
            {
                int payFreq = target.GetAttributeValue<int>("mcs_payfreq");
                if (payFreq < 0 || (payFreq != 0 && payFreq % 30 != 0))
                {
                    throw new InvalidPluginExecutionException("付款频次（天）必须是 0 或 30 的倍数");
                }
            }

            // 3. 状态流转校验（Update 时）
            if (context.MessageName == "Update" && target.Contains("mcs_status"))
            {
                ValidateStatusTransition(target, context, service, tracer);
            }

            // 4. 重复记录校验（Create/Update 涉及关键维度变更时）
            // 注：客户等级 mcs_creditgrade 已改为 Picklist，按业务规则暂不参与重复校验
            if (context.MessageName == "Create" ||
                target.Contains("mcs_buid") ||
                target.Contains("mcs_subid") ||
                target.Contains("mcs_countrycode") ||
                target.Contains("mcs_typeid") ||
                target.Contains("mcs_buyergrade"))
            {
                ValidateDuplicate(target, context, service, tracer);
            }
        }

        private void ValidateStatusTransition(Entity target, IPluginExecutionContext context, IOrganizationService service, ITracingService tracer)
        {
            if (!context.PreEntityImages.Contains("PreImage"))
            {
                tracer.Trace("未找到 PreImage，跳过状态流转校验");
                return;
            }

            Entity preImage = context.PreEntityImages["PreImage"];
            int oldStatus = preImage.GetAttributeValue<OptionSetValue>("mcs_status")?.Value ?? 0;
            int newStatus = target.GetAttributeValue<OptionSetValue>("mcs_status")?.Value ?? 0;

            if (oldStatus == newStatus)
            {
                return;
            }

            // 合法流转：
            // 0(未生效) -> 1(待审批): 申请
            // 1(待审批) -> 2(生效): 审批
            // 1(待审批) -> 0(未生效): 拒绝
            bool valid = (oldStatus == 0 && newStatus == 1) ||
                         (oldStatus == 1 && newStatus == 2) ||
                         (oldStatus == 1 && newStatus == 0);

            if (!valid)
            {
                throw new InvalidPluginExecutionException($"非法的状态流转: {oldStatus} -> {newStatus}");
            }
        }

        private void ValidateDuplicate(Entity target, IPluginExecutionContext context, IOrganizationService service, ITracingService tracer)
        {
            tracer.Trace("开始重复记录校验");

            // 获取当前记录 ID
            Guid currentId = target.Id;
            if (currentId == Guid.Empty && context.MessageName == "Create")
            {
                currentId = Guid.NewGuid(); // 新建记录临时 ID，仅用于排除
            }

            // 获取当前维度值
            string buId = GetFieldValue(target, context, service, "mcs_buid");
            string subId = GetFieldValue(target, context, service, "mcs_subid");
            string countryCode = GetFieldValue(target, context, service, "mcs_countrycode");
            string typeId = GetFieldValue(target, context, service, "mcs_typeid");
            string buyerGrade = GetFieldValue(target, context, service, "mcs_buyergrade");

            if (string.IsNullOrEmpty(buId))
            {
                tracer.Trace("事业部编码为空，跳过重复校验");
                return;
            }

            // 查询同事业部其他记录
            var query = new QueryExpression("mcs_trade_stpayterm")
            {
                ColumnSet = new ColumnSet("mcs_subid", "mcs_countrycode", "mcs_typeid", "mcs_buyergrade", "mcs_creditgrade"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_buid", ConditionOperator.Equal, buId)
                    }
                }
            };

            if (context.MessageName == "Update")
            {
                query.Criteria.AddCondition("mcs_trade_stpaytermid", ConditionOperator.NotEqual, currentId);
            }

            var result = service.RetrieveMultiple(query);
            tracer.Trace($"查询到同事业部记录 {result.Entities.Count} 条");

            foreach (var record in result.Entities)
            {
                if (IsDuplicate(subId, countryCode, typeId, buyerGrade, record))
                {
                    throw new InvalidPluginExecutionException("存在有重复记录，需核查！");
                }
            }
        }

        private string GetFieldValue(Entity target, IPluginExecutionContext context, IOrganizationService service, string fieldName)
        {
            if (target.Contains(fieldName))
            {
                return target.GetAttributeValue<string>(fieldName) ?? string.Empty;
            }

            if (context.MessageName == "Update")
            {
                if (context.PreEntityImages.Contains("PreImage"))
                {
                    var preImage = context.PreEntityImages["PreImage"];
                    if (preImage.Contains(fieldName))
                    {
                        return preImage.GetAttributeValue<string>(fieldName) ?? string.Empty;
                    }
                }
                else if (target.Id != Guid.Empty)
                {
                    // 没有 PreImage 时，从数据库查询当前记录
                    try
                    {
                        var current = service.Retrieve("mcs_trade_stpayterm", target.Id, new ColumnSet(fieldName));
                        if (current.Contains(fieldName))
                        {
                            return current.GetAttributeValue<string>(fieldName) ?? string.Empty;
                        }
                    }
                    catch (Exception ex)
                    {
                        // 记录可能尚未创建，忽略异常
                    }
                }
            }

            return string.Empty;
        }

        private bool IsDuplicate(string subId, string countryCode, string typeId, string buyerGrade, Entity record)
        {
            // 子公司匹配
            string recordSubId = record.GetAttributeValue<string>("mcs_subid") ?? string.Empty;
            if (!IsWildcardMatch(subId, recordSubId))
            {
                return false;
            }

            // 国家代码匹配
            string recordCountryCode = record.GetAttributeValue<string>("mcs_countrycode") ?? string.Empty;
            if (!IsMultiSelectMatch(countryCode, recordCountryCode, ","))
            {
                return false;
            }

            // 产品分类匹配
            string recordTypeId = record.GetAttributeValue<string>("mcs_typeid") ?? string.Empty;
            if (!IsMultiSelectMatch(typeId, recordTypeId, ","))
            {
                return false;
            }

            // 客户分类匹配
            string recordBuyerGrade = record.GetAttributeValue<string>("mcs_buyergrade") ?? string.Empty;
            if (!IsMultiSelectMatch(buyerGrade, recordBuyerGrade, "/"))
            {
                return false;
            }

            return true;
        }

        private bool IsWildcardMatch(string value1, string value2)
        {
            // 任一方为空或 NA，视为通配
            if (string.IsNullOrWhiteSpace(value1) || value1.Trim().Equals("NA", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(value2) || value2.Trim().Equals("NA", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.Equals(value1.Trim(), value2.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private bool IsMultiSelectMatch(string value1, string value2, string separator)
        {
            // 任一方为空或 NA，视为通配
            if (string.IsNullOrWhiteSpace(value1) || value1.Trim().Equals("NA", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(value2) || value2.Trim().Equals("NA", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var set1 = ParseMultiSelect(value1, separator);
            var set2 = ParseMultiSelect(value2, separator);

            return set1.Any(x => set2.Contains(x, StringComparer.OrdinalIgnoreCase));
        }

        private HashSet<string> ParseMultiSelect(string value, string separator)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(value))
            {
                return result;
            }

            foreach (var item in value.Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = item.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    result.Add(trimmed);
                }
            }

            return result;
        }
    }
}
