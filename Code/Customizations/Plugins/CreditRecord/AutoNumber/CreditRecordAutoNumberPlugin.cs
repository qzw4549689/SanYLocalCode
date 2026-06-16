using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace SanyD365.Plugins.CreditRecord
{
    /// <summary>
    /// 信用评估记录表 - 编码自动生成Plugin
    /// 触发时机：Create前
    /// 规则：SCO + YYYYMMDD + 4位序列号
    /// 影响范围：仅限mcs_credit_record实体
    /// </summary>
    public class CreditRecordAutoNumberPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = factory.CreateOrganizationService(context.UserId);
            ITracingService tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracer.Trace("CreditRecordAutoNumberPlugin 开始执行");

            // 严格校验：只处理Create前事件
            if (context.MessageName != "Create" || context.Stage != 20)
            {
                tracer.Trace("非Create前事件，跳过");
                return;
            }

            // 严格校验：只处理mcs_credit_record实体
            if (!context.InputParameters.Contains("Target") || !(context.InputParameters["Target"] is Entity))
            {
                tracer.Trace("未找到Target实体");
                return;
            }

            Entity target = (Entity)context.InputParameters["Target"];
            
            if (target.LogicalName != "mcs_credit_record")
            {
                tracer.Trace($"实体不匹配: {target.LogicalName}，跳过");
                return;
            }

            // 如果编码已填写，不覆盖
            if (target.Contains("mcs_scoreid") && target["mcs_scoreid"] != null)
            {
                tracer.Trace("编码已存在，跳过自动生成");
                return;
            }

            try
            {
                string newCode = GenerateCode(service, tracer);
                target["mcs_scoreid"] = newCode;
                tracer.Trace($"生成编码: {newCode}");
            }
            catch (Exception ex)
            {
                tracer.Trace($"生成编码失败: {ex.Message}");
                throw new InvalidPluginExecutionException($"生成信用评估编码失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 生成编码：SCO + YYYYMMDD + 4位序列号
        /// </summary>
        private string GenerateCode(IOrganizationService service, ITracingService tracer)
        {
            string prefix = "SCO";
            string datePart = DateTime.Now.ToString("yyyyMMdd");
            
            string todayPattern = $"{prefix}{datePart}%";
            
            var query = new QueryExpression("mcs_credit_record")
            {
                ColumnSet = new ColumnSet("mcs_scoreid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_scoreid", ConditionOperator.Like, todayPattern)
                    }
                },
                Orders =
                {
                    new OrderExpression("mcs_scoreid", OrderType.Descending)
                },
                TopCount = 1
            };

            var result = service.RetrieveMultiple(query);
            
            int sequence = 1;
            
            if (result.Entities.Count > 0)
            {
                string lastCode = result.Entities[0].GetAttributeValue<string>("mcs_scoreid");
                tracer.Trace($"当天最大编码: {lastCode}");
                
                if (!string.IsNullOrEmpty(lastCode) && lastCode.Length >= 15)
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
