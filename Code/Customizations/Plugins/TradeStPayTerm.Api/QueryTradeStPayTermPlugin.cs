using Microsoft.Xrm.Sdk;
using System;

namespace SanyD365.Plugins.TradeStPayTerm.Api
{
    /// <summary>
    /// 成交条件样板库查询 Custom API
    /// 唯一名：mcs_QueryTradeStPayTerm
    /// </summary>
    public class QueryTradeStPayTermPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = factory.CreateOrganizationService(context.UserId);
            ITracingService tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracer.Trace("QueryTradeStPayTermPlugin 开始执行");

            try
            {
                // 读取输入参数
                string buId = GetInputParameter(context, "mcs_buid");
                string subId = GetInputParameter(context, "mcs_subid");
                string countryCode = GetInputParameter(context, "mcs_countrycode");
                string prdGroupId = GetInputParameter(context, "mcs_prdgroupid");
                string buyerCode = GetInputParameter(context, "mcs_buyercode");

                tracer.Trace($"入参: buId={buId}, subId={subId}, countryCode={countryCode}, prdGroupId={prdGroupId}, buyerCode={buyerCode}");

                // 入参校验
                string validationError = ValidateInput(buId, subId, countryCode, prdGroupId, buyerCode);
                if (!string.IsNullOrEmpty(validationError))
                {
                    SetErrorResult(context, validationError);
                    return;
                }

                // 执行查询
                var queryService = new TradeStPayTermQueryService(service, tracer);
                var result = queryService.Query(buId, subId, countryCode, prdGroupId, buyerCode);

                // 设置输出参数
                context.OutputParameters["status"] = result.Status;
                context.OutputParameters["message"] = result.Message;
                context.OutputParameters["records"] = TradeStPayTermQueryService.SerializeResult(result);

                tracer.Trace("QueryTradeStPayTermPlugin 执行完成");
            }
            catch (Exception ex)
            {
                tracer.Trace($"QueryTradeStPayTermPlugin 异常: {ex.Message}");
                SetErrorResult(context, $"查询失败: {ex.Message}");
            }
        }

        private string GetInputParameter(IPluginExecutionContext context, string key)
        {
            if (context.InputParameters.Contains(key) && context.InputParameters[key] != null)
            {
                return context.InputParameters[key].ToString().Trim();
            }
            return string.Empty;
        }

        private string ValidateInput(string buId, string subId, string countryCode, string prdGroupId, string buyerCode)
        {
            if (string.IsNullOrWhiteSpace(buId))
                return "事业部编码不能为空";
            if (string.IsNullOrWhiteSpace(subId))
                return "子公司编码不能为空";
            if (string.IsNullOrWhiteSpace(countryCode))
                return "国家代码不能为空";
            if (string.IsNullOrWhiteSpace(prdGroupId))
                return "产品线编码不能为空";
            if (string.IsNullOrWhiteSpace(buyerCode))
                return "客户编码不能为空";

            return string.Empty;
        }

        private void SetErrorResult(IPluginExecutionContext context, string message)
        {
            context.OutputParameters["status"] = "0";
            context.OutputParameters["message"] = message;
            context.OutputParameters["records"] = "[]";
        }
    }
}
