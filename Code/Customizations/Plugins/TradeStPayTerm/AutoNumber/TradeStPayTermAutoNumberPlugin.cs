using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace SanyD365.Plugins.TradeStPayTerm
{
    /// <summary>
    /// 成交条件样板库 - 标准条件编码自动生成 Plugin
    /// 触发时机：Create PreOperation
    /// 规则：TC + YYMMDD + 2位序列号（共10位）
    /// </summary>
    public class TradeStPayTermAutoNumberPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = factory.CreateOrganizationService(context.UserId);
            ITracingService tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracer.Trace("TradeStPayTermAutoNumberPlugin 开始执行");

            if (context.MessageName != "Create" || context.Stage != 20)
            {
                tracer.Trace("非 Create PreOperation 事件，跳过");
                return;
            }

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

            // 如果主字段已填写，不覆盖
            if (target.Contains("mcs_trade_stpaytermname") && target["mcs_trade_stpaytermname"] != null)
            {
                tracer.Trace("标准条件编码已存在，跳过自动生成");
                return;
            }

            try
            {
                string newCode = GenerateCode(service, tracer);
                target["mcs_trade_stpaytermname"] = newCode;
                tracer.Trace($"生成标准条件编码: {newCode}");
            }
            catch (Exception ex)
            {
                tracer.Trace($"生成编码失败: {ex.Message}");
                throw new InvalidPluginExecutionException($"生成标准条件编码失败: {ex.Message}");
            }
        }

        private string GenerateCode(IOrganizationService service, ITracingService tracer)
        {
            string prefix = "TC";
            string datePart = DateTime.Now.ToString("yyMMdd");
            string todayPattern = $"{prefix}{datePart}%";

            var query = new QueryExpression("mcs_trade_stpayterm")
            {
                ColumnSet = new ColumnSet("mcs_trade_stpaytermname"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_trade_stpaytermname", ConditionOperator.Like, todayPattern)
                    }
                },
                Orders =
                {
                    new OrderExpression("mcs_trade_stpaytermname", OrderType.Descending)
                },
                TopCount = 1
            };

            var result = service.RetrieveMultiple(query);
            int sequence = 1;

            if (result.Entities.Count > 0)
            {
                string lastCode = result.Entities[0].GetAttributeValue<string>("mcs_trade_stpaytermname");
                tracer.Trace($"当天最大编码: {lastCode}");

                if (!string.IsNullOrEmpty(lastCode) && lastCode.Length >= 10)
                {
                    string seqPart = lastCode.Substring(lastCode.Length - 2);
                    if (int.TryParse(seqPart, out int lastSeq))
                    {
                        sequence = lastSeq + 1;
                    }
                }
            }

            if (sequence > 99)
            {
                throw new InvalidOperationException("当天标准条件编码已超过 99 条，请调整编码规则");
            }

            string newCode = $"{prefix}{datePart}{sequence:D2}";
            tracer.Trace($"新编码: {newCode}");

            return newCode;
        }
    }
}
