using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace SanyD365.Plugins.ScoringCard
{
    /// <summary>
    /// 评分卡配置表 - 编码自动生成Plugin
    /// 触发时机：Create前
    /// 规则：SC + YYYYMMDD + 4位序列号
    /// </summary>
    public class ScoringCardAutoNumberPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            // 获取上下文
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = factory.CreateOrganizationService(context.UserId);
            ITracingService tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracer.Trace("ScoringCardAutoNumberPlugin 开始执行");

            // 只处理Create前事件
            if (context.MessageName != "Create" || context.Stage != 20)
            {
                tracer.Trace("非Create前事件，跳过");
                return;
            }

            // 获取目标实体
            if (!context.InputParameters.Contains("Target") || !(context.InputParameters["Target"] is Entity))
            {
                tracer.Trace("未找到Target实体");
                return;
            }

            Entity target = (Entity)context.InputParameters["Target"];
            
            // 只处理mcs_credit_scoringcard实体
            if (target.LogicalName != "mcs_credit_scoringcard")
            {
                tracer.Trace($"实体不匹配: {target.LogicalName}");
                return;
            }

            // 如果编码已填写，不覆盖
            if (target.Contains("mcs_credit_scoringcardno") && target["mcs_credit_scoringcardno"] != null)
            {
                tracer.Trace("编码已存在，跳过自动生成");
                return;
            }

            try
            {
                // 生成编码
                string newCode = GenerateCode(service, tracer);
                target["mcs_credit_scoringcardno"] = newCode;
                tracer.Trace($"生成编码: {newCode}");
            }
            catch (Exception ex)
            {
                tracer.Trace($"生成编码失败: {ex.Message}");
                throw new InvalidPluginExecutionException($"生成评分卡编码失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 生成编码：SC + YYYYMMDD + 4位序列号
        /// </summary>
        private string GenerateCode(IOrganizationService service, ITracingService tracer)
        {
            string prefix = "SC";
            string datePart = DateTime.Now.ToString("yyyyMMdd");
            
            // 查询当天最大序列号
            string todayPattern = $"{prefix}{datePart}%";
            
            var query = new QueryExpression("mcs_credit_scoringcard")
            {
                ColumnSet = new ColumnSet("mcs_credit_scoringcardno"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_credit_scoringcardno", ConditionOperator.Like, todayPattern)
                    }
                },
                Orders =
                {
                    new OrderExpression("mcs_credit_scoringcardno", OrderType.Descending)
                },
                TopCount = 1
            };

            var result = service.RetrieveMultiple(query);
            
            int sequence = 1;
            
            if (result.Entities.Count > 0)
            {
                string lastCode = result.Entities[0].GetAttributeValue<string>("mcs_credit_scoringcardno");
                tracer.Trace($"当天最大编码: {lastCode}");
                
                if (!string.IsNullOrEmpty(lastCode) && lastCode.Length >= 14)
                {
                    string seqPart = lastCode.Substring(lastCode.Length - 4);
                    if (int.TryParse(seqPart, out int lastSeq))
                    {
                        sequence = lastSeq + 1;
                    }
                }
            }
            
            string newCode = $"{prefix}{datePart}{sequence:D4}";
            tracer.Trace($"新编码: {newCode}");
            
            return newCode;
        }
    }
}
