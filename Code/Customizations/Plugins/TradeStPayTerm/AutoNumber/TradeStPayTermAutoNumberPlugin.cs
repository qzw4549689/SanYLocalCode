using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace SanyD365.Plugins.TradeStPayTerm
{
    /// <summary>
    /// 成交条件样板库 - 标准条件编码自动生成 Plugin
    /// 触发时机：Create PreOperation
    /// 规则：TC + YYMMDD + 4位序列号（共12位，当天上限9999条）
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
                }
            };

            var result = service.RetrieveMultiple(query);

            // 内存中取当天最大序号：取「TC+日期(8位)」之后的部分解析，兼容存量 2 位编码
            // （不用字符串排序取最大值：2 位与 4 位序号混排时 TC...99 会排在 TC...0100 前面）
            int maxSeq = 0;
            foreach (var record in result.Entities)
            {
                string code = record.GetAttributeValue<string>("mcs_trade_stpaytermname");
                if (string.IsNullOrEmpty(code) || code.Length <= 8)
                {
                    continue;
                }
                if (int.TryParse(code.Substring(8), out int seq) && seq > maxSeq)
                {
                    maxSeq = seq;
                }
            }
            tracer.Trace($"当天最大序号: {maxSeq}");

            int sequence = maxSeq + 1;
            if (sequence > 9999)
            {
                throw new InvalidOperationException("当天标准条件编码已超过 9999 条，请调整编码规则");
            }

            string newCode = $"{prefix}{datePart}{sequence:D4}";
            tracer.Trace($"新编码: {newCode}");

            return newCode;
        }
    }
}
