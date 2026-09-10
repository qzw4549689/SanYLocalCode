using Microsoft.Xrm.Sdk;
using System;

namespace SanyD365.Plugins.CreditPool.Api
{
    /// <summary>
    /// 查询授信 Custom API（816 授信池）
    /// 唯一名：mcs_queryCreditBalance
    /// 主要给合同模块查询使用：客户编码必填；合同编码选传，传了则额外返回合同授信金额。
    /// 详见：Documents/Planning/授信池816/两接口实施方案v3.md、查询接口_出入参定义.md
    /// </summary>
    public class QueryCreditBalancePlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = factory.CreateOrganizationService(context.UserId);
            ITracingService tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracer.Trace("QueryCreditBalancePlugin 开始执行");

            string accountCode = GetStringInput(context, "mcs_accountid");
            string contractCode = GetStringInput(context, "mcs_contractid");
            tracer.Trace($"入参: accountCode={accountCode}, contractCode={contractCode}");

            var result = new QueryCreditBalanceResult();
            try
            {
                if (string.IsNullOrWhiteSpace(accountCode))
                {
                    result.Success = false;
                    result.FailReason = "客户编码不能为空";
                }
                else
                {
                    var queryService = new QueryCreditBalanceService(service, tracer);
                    result = queryService.Query(accountCode, contractCode);
                }
            }
            catch (Exception ex)
            {
                tracer.Trace($"QueryCreditBalancePlugin 异常: {ex}");
                result.Success = false;
                result.FailReason = $"查询失败: {ex.Message}";
            }

            SetResult(context, accountCode, contractCode, result);
            tracer.Trace($"QueryCreditBalancePlugin 执行完成: Success={result.Success}");
        }

        private string GetStringInput(IPluginExecutionContext context, string key)
        {
            if (context.InputParameters.Contains(key) && context.InputParameters[key] != null)
            {
                return context.InputParameters[key].ToString().Trim();
            }
            return string.Empty;
        }

        private void SetResult(IPluginExecutionContext context, string accountCode, string contractCode, QueryCreditBalanceResult r)
        {
            // 调用标识回显
            context.OutputParameters["mcs_accountid"] = accountCode;
            context.OutputParameters["mcs_contractid"] = contractCode;
            // 合同授信金额（传了合同编码才查）
            context.OutputParameters["mcs_credit_limit_usd"] = r.CreditLimitUSD;
            context.OutputParameters["mcs_credit_limit_cny"] = r.CreditLimitCNY;
            // 中信保批复限额 / 批复限额余额（T+1） / 上浮限额 / 净占用 / 上浮余额
            context.OutputParameters["mcs_sinosure_limit_usd"] = r.SinosureLimitUSD;
            context.OutputParameters["mcs_sinosure_limit_cny"] = r.SinosureLimitCNY;
            context.OutputParameters["mcs_sinosure_balance_usd"] = r.SinosureBalanceUSD;
            context.OutputParameters["mcs_sinosure_balance_cny"] = r.SinosureBalanceCNY;
            context.OutputParameters["mcs_sinosure_uplift_limit_usd"] = r.SinosureUpliftLimitUSD;
            context.OutputParameters["mcs_sinosure_uplift_limit_cny"] = r.SinosureUpliftLimitCNY;
            context.OutputParameters["mcs_sinosure_netused_usd"] = r.SinosureNetUsedUSD;
            context.OutputParameters["mcs_sinosure_netused_cny"] = r.SinosureNetUsedCNY;
            context.OutputParameters["mcs_sinosure_uplift_balance_usd"] = r.SinosureUpliftBalanceUSD;
            context.OutputParameters["mcs_sinosure_uplift_balance_cny"] = r.SinosureUpliftBalanceCNY;
            // 厂端交易基线额度 / 基线余额 / 客户风险敞口（=厂端净占用） / 客户签约占用
            context.OutputParameters["mcs_factory_limit_usd"] = r.FactoryLimitUSD;
            context.OutputParameters["mcs_factory_limit_cny"] = r.FactoryLimitCNY;
            context.OutputParameters["mcs_factory_balance_usd"] = r.FactoryBalanceUSD;
            context.OutputParameters["mcs_factory_balance_cny"] = r.FactoryBalanceCNY;
            context.OutputParameters["mcs_risk_exposure_usd"] = r.RiskExposureUSD;
            context.OutputParameters["mcs_risk_exposure_cny"] = r.RiskExposureCNY;
            context.OutputParameters["mcs_signing_occupy_usd"] = r.SigningOccupyUSD;
            context.OutputParameters["mcs_signing_occupy_cny"] = r.SigningOccupyCNY;
            // 调用结果
            context.OutputParameters["mcs_usedflag"] = r.Success ? "1" : "0";
            context.OutputParameters["mcs_failreason"] = r.FailReason ?? string.Empty;
        }
    }
}
