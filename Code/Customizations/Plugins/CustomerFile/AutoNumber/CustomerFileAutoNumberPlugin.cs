using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace SanyD365.Plugins.CustomerFile
{
    /// <summary>
    /// 客户资信附件表 - 编码自动生成 Plugin
    /// 触发时机：Create 前
    /// 规则：ATT + YYYYMMDD + 4位序列号
    /// 影响范围：仅限 mcs_customer_file 实体
    /// </summary>
    public class CustomerFileAutoNumberPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = factory.CreateOrganizationService(context.UserId);
            ITracingService tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracer.Trace("CustomerFileAutoNumberPlugin 开始执行");

            // 严格校验：只处理 Create 前事件
            if (context.MessageName != "Create" || context.Stage != 20)
            {
                tracer.Trace("非 Create 前事件，跳过");
                return;
            }

            // 严格校验：只处理 mcs_customer_file 实体
            if (!context.InputParameters.Contains("Target") || !(context.InputParameters["Target"] is Entity))
            {
                tracer.Trace("未找到 Target 实体");
                return;
            }

            Entity target = (Entity)context.InputParameters["Target"];

            if (target.LogicalName != "mcs_customer_file")
            {
                tracer.Trace($"实体不匹配: {target.LogicalName}，跳过");
                return;
            }

            try
            {
                // 生成文件统一编号
                if (!target.Contains("mcs_fileid") || target["mcs_fileid"] == null)
                {
                    string newCode = GenerateFileId(service, tracer);
                    target["mcs_fileid"] = newCode;
                    tracer.Trace($"生成文件统一编号: {newCode}");
                }

                // 默认文件上传日期
                if (!target.Contains("mcs_filedate") || target["mcs_filedate"] == null)
                {
                    target["mcs_filedate"] = DateTime.Now;
                    tracer.Trace("设置文件上传日期为当前日期");
                }
            }
            catch (Exception ex)
            {
                tracer.Trace($"处理失败: {ex.Message}");
                throw new InvalidPluginExecutionException($"生成客户资信附件编号失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 生成文件统一编号：ATT + YYYYMMDD + 4位序列号
        /// </summary>
        private string GenerateFileId(IOrganizationService service, ITracingService tracer)
        {
            string prefix = "ATT";
            string datePart = DateTime.Now.ToString("yyyyMMdd");
            string todayPattern = $"{prefix}{datePart}%";

            var query = new QueryExpression("mcs_customer_file")
            {
                ColumnSet = new ColumnSet("mcs_fileid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_fileid", ConditionOperator.Like, todayPattern)
                    }
                },
                Orders =
                {
                    new OrderExpression("mcs_fileid", OrderType.Descending)
                },
                TopCount = 1
            };

            var result = service.RetrieveMultiple(query);

            int sequence = 1;

            if (result.Entities.Count > 0)
            {
                string lastCode = result.Entities[0].GetAttributeValue<string>("mcs_fileid");
                tracer.Trace($"当天最大编号: {lastCode}");

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
            tracer.Trace($"新文件统一编号: {newCode}");

            return newCode;
        }
    }
}
