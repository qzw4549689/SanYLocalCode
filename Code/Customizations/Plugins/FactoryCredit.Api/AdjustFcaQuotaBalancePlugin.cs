using Microsoft.Xrm.Sdk;
using System;

namespace SanyD365.Plugins.FactoryCredit.Api
{
    /// <summary>
    /// 厂端授信余额调整 Custom API
    /// 唯一名：mcs_AdjustFcaQuotaBalance
    /// 供合同评审、订单发货/取消/退货、回款解款等业务环节统一调用，
    /// 处理厂端授信余额的 初始化/占用/释放，并写入台账 mcs_fca_records。
    /// 详见：Documents/Planning/厂端授信余额调整接口_实施方案.md
    /// </summary>
    public class AdjustFcaQuotaBalancePlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = factory.CreateOrganizationService(context.UserId);
            ITracingService tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracer.Trace("AdjustFcaQuotaBalancePlugin 开始执行");

            // 读取输入参数
            string accountCode = GetStringInput(context, "mcs_accountid");
            decimal useBalance = GetDecimalInput(context, "mcs_usebalance");
            string proccess = GetStringInput(context, "mcs_proccess");
            string adjust = GetStringInput(context, "mcs_adjust");
            string contractCode = GetStringInput(context, "mcs_contractid");
            string orderCode = GetStringInput(context, "mcs_orderid");

            tracer.Trace($"入参: accountCode={accountCode}, useBalance={useBalance}, proccess={proccess}, adjust={adjust}, contractCode={contractCode}, orderCode={orderCode}");

            try
            {
                // 入参校验
                string validationError = ValidateInput(accountCode, useBalance, proccess, adjust, contractCode, orderCode);
                if (!string.IsNullOrEmpty(validationError))
                {
                    SetResult(context, accountCode, useBalance, proccess, adjust, contractCode, orderCode,
                        false, 0m, 0m, string.Empty, validationError);
                    return;
                }

                // 执行调整
                var adjustService = new FcaQuotaAdjustService(service, tracer);
                var result = adjustService.Adjust(accountCode, useBalance, int.Parse(proccess), int.Parse(adjust), contractCode, orderCode);

                SetResult(context, accountCode, useBalance, proccess, adjust, contractCode, orderCode,
                    result.Success, result.UsedBalance, result.SellerBalance, result.RecordId, result.FailReason);

                tracer.Trace($"AdjustFcaQuotaBalancePlugin 执行完成: Success={result.Success}, RecordId={result.RecordId}");
            }
            catch (Exception ex)
            {
                tracer.Trace($"AdjustFcaQuotaBalancePlugin 异常: {ex}");
                SetResult(context, accountCode, useBalance, proccess, adjust, contractCode, orderCode,
                    false, 0m, 0m, string.Empty, $"调整失败: {ex.Message}");
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
        /// 入参校验：必填、金额非负、环节/动作取值、动作-环节交叉校验、合同/订单必选规则
        /// </summary>
        private string ValidateInput(string accountCode, decimal useBalance, string proccess, string adjust,
            string contractCode, string orderCode)
        {
            if (string.IsNullOrWhiteSpace(accountCode))
                return "客户编码不能为空";
            if (useBalance < 0)
                return "调整厂端授信金额USD必须大于等于0";
            if (string.IsNullOrWhiteSpace(proccess))
                return "流程环节不能为空";
            if (string.IsNullOrWhiteSpace(adjust))
                return "额度调整动作不能为空";

            if (!int.TryParse(proccess, out int stage) || stage < 1 || stage > 11)
                return $"流程环节取值非法: {proccess}（有效范围1-11）";
            if (!int.TryParse(adjust, out int action) || (action != 1 && action != 3 && action != 4))
                return $"额度调整动作取值非法: {adjust}（1=初始化, 3=占用, 4=释放）";

            // 动作-环节交叉校验（环节以台账表 1-11 口径为准）
            if (action == FcaQuotaAdjustService.ADJUST_INIT && stage != 1 && stage != 2)
                return "初始化动作仅限流程环节 1（厂端授信模型计算）/ 2（厂端授信限额额度申请）使用";
            if (action == FcaQuotaAdjustService.ADJUST_OCCUPY && stage != 6)
                return "占用动作仅限流程环节 6（订单执行验收/发货）使用";
            if (action == FcaQuotaAdjustService.ADJUST_RELEASE && stage != 5 && stage != 7 && stage != 8 && stage != 9 && stage != 10)
                return "释放动作仅限流程环节 5（合同取消）/ 7（订单变更信用类别）/ 8（回款解款）/ 9（订单退货）/ 10（订单取消）使用";

            // 从合同阶段开始（环节>=3）必选合同；订单发货及后续环节必选订单
            if (stage >= 3 && string.IsNullOrWhiteSpace(contractCode))
                return $"流程环节 {stage} 必须传入合同编码";
            if (stage >= 6 && stage <= 10 && string.IsNullOrWhiteSpace(orderCode))
                return $"流程环节 {stage} 必须传入订单编码";

            return string.Empty;
        }

        private void SetResult(IPluginExecutionContext context,
            string accountCode, decimal useBalance, string proccess, string adjust, string contractCode, string orderCode,
            bool success, decimal usedBalance, decimal sellerBalance, string recordId, string failReason)
        {
            // 请求参数原样回显
            context.OutputParameters["mcs_accountid"] = accountCode;
            context.OutputParameters["mcs_usebalance"] = useBalance;
            context.OutputParameters["mcs_proccess"] = proccess;
            context.OutputParameters["mcs_adjust"] = adjust;
            context.OutputParameters["mcs_contractid"] = contractCode;
            context.OutputParameters["mcs_orderid"] = orderCode;
            // 处理结果
            context.OutputParameters["mcs_usedbalance"] = usedBalance;
            context.OutputParameters["mcs_sellerbalance"] = sellerBalance;
            context.OutputParameters["mcs_usedflag"] = success ? "1" : "0";
            context.OutputParameters["mcs_recordid"] = recordId ?? string.Empty;
            context.OutputParameters["mcs_failreason"] = failReason ?? string.Empty;
        }
    }
}
