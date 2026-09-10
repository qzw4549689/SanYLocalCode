using Microsoft.Xrm.Sdk;
using System;

namespace SanyD365.Plugins.RiskExposure.Api
{
    /// <summary>
    /// 风险敞口计算 Custom API
    /// 唯一名：mcs_CalcContractRiskExposure
    /// 供合同调用，按业务类型计算风险敞口金额。
    /// </summary>
    public class CalculateRiskExposurePlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = factory.CreateOrganizationService(context.UserId);
            ITracingService tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracer.Trace("CalculateRiskExposurePlugin 开始执行");

            // 读取输入参数
            string type = GetStringInput(context, "mcs_type");
            string buyerCode = GetStringInput(context, "mcs_buyercode");
            decimal riskAmount = GetDecimalInput(context, "mcs_risk_amount");
            decimal signedAmount = GetDecimalInput(context, "mcs_signed_amount");
            string contractCode = GetStringInput(context, "mcs_contractid");

            tracer.Trace($"入参: type={type}, buyerCode={buyerCode}, riskAmount={riskAmount}, signedAmount={signedAmount}, contractCode={contractCode}");

            try
            {
                // 入参校验
                string validationError = ValidateInput(type, buyerCode, riskAmount, signedAmount, contractCode);
                if (!string.IsNullOrEmpty(validationError))
                {
                    SetResult(context, buyerCode, 0m, validationError);
                    return;
                }

                // 执行计算
                var serviceInstance = new RiskExposureService(service, tracer);
                var result = serviceInstance.Calculate(type, buyerCode, riskAmount, signedAmount, contractCode);

                if (result.Success)
                {
                    SetResult(context, buyerCode, result.RiskExposure, string.Empty);
                }
                else
                {
                    SetResult(context, buyerCode, 0m, result.FailReason);
                }

                tracer.Trace($"CalculateRiskExposurePlugin 执行完成: Success={result.Success}, RiskExposure={result.RiskExposure}");
            }
            catch (Exception ex)
            {
                tracer.Trace($"CalculateRiskExposurePlugin 异常: {ex}");
                SetResult(context, buyerCode, 0m, $"计算失败: {ex.Message}");
            }
        }

        private string GetStringInput(IPluginExecutionContext context, string key)
        {
            if (context.InputParameters.Contains(key) && context.InputParameters[key] != null)
            {
                return context.InputParameters[key].ToString().Trim();
            }
            return string.Empty;
        }

        private decimal GetDecimalInput(IPluginExecutionContext context, string key)
        {
            if (context.InputParameters.Contains(key) && context.InputParameters[key] != null)
            {
                return Convert.ToDecimal(context.InputParameters[key]);
            }
            return 0m;
        }

        /// <summary>
        /// 入参校验
        /// </summary>
        private string ValidateInput(string type, string buyerCode, decimal riskAmount, decimal signedAmount, string contractCode)
        {
            if (string.IsNullOrWhiteSpace(type))
                return "类型不能为空";

            if (!string.Equals(type, "A", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(type, "B", StringComparison.OrdinalIgnoreCase))
                return $"类型取值非法: {type}（A=厂端授信/中信保, B=外部融资）";

            if (string.IsNullOrWhiteSpace(buyerCode))
                return "客户编码不能为空";

            if (string.Equals(type, "B", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(contractCode))
                return "外部融资类型必须传入合同编码";

            return string.Empty;
        }

        private void SetResult(IPluginExecutionContext context, string buyerCode, decimal riskExposure, string failReason)
        {
            context.OutputParameters["mcs_buyercode"] = buyerCode;
            context.OutputParameters["mcs_risk_exposure"] = riskExposure;

            if (!string.IsNullOrEmpty(failReason))
            {
                throw new InvalidPluginExecutionException(failReason);
            }
        }
    }
}
