using Microsoft.Xrm.Sdk;
using System;

namespace SanyD365.Plugins.CreditPool.Api
{
    /// <summary>
    /// 使用授信 Custom API（816 授信池·哑记账）
    /// 唯一名：mcs_recordCreditDetail
    /// 供交货单（发货单推出/取消）、订单（退货/退货取消）、解款记录（解款/解款红冲）等上游系统调用，
    /// 金额由调用方算好传入，我方只校验 + 幂等保存台账 mcs_fca_records + 更新余额（厂端走 mcs_fca_quota，信保走台账聚合）。
    /// 详见：Documents/Planning/授信池816/两接口实施方案v3.md
    /// </summary>
    public class RecordCreditDetailPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = factory.CreateOrganizationService(context.UserId);
            ITracingService tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracer.Trace("RecordCreditDetailPlugin 开始执行");

            // 读取输入参数
            string accountCode = GetStringInput(context, "mcs_accountid");
            string creditType = GetStringInput(context, "mcs_creditType");
            decimal useBalanceUSD = GetDecimalInput(context, "mcs_usebalanceUSD");
            decimal useBalanceCNY = GetDecimalInput(context, "mcs_usebalanceCNY");
            string proccess = GetStringInput(context, "mcs_proccess");
            string adjust = GetStringInput(context, "mcs_adjust");
            string contractCode = GetStringInput(context, "mcs_contractid");
            string orderCode = GetStringInput(context, "mcs_orderid");
            string deliveryNo = GetStringInput(context, "mcs_deliveryordid");
            string settleId = GetStringInput(context, "mcs_settle_id");
            string settleNo = GetStringInput(context, "mcs_settle_no");

            tracer.Trace($"入参: accountCode={accountCode}, creditType={creditType}, usebalanceUSD={useBalanceUSD}, usebalanceCNY={useBalanceCNY}, proccess={proccess}, adjust={adjust}, contractCode={contractCode}, orderCode={orderCode}, deliveryNo={deliveryNo}, settleId={settleId}, settleNo={settleNo}");

            try
            {
                // 入参校验
                string validationError = ValidateInput(accountCode, ref creditType, useBalanceUSD, useBalanceCNY,
                    proccess, adjust, contractCode, orderCode, deliveryNo, settleId, settleNo);
                if (!string.IsNullOrEmpty(validationError))
                {
                    SetResult(context, accountCode, useBalanceUSD, proccess, adjust, contractCode, orderCode,
                        false, 0m, 0m, string.Empty, validationError);
                    return;
                }

                // 执行占用/释放/初始化（哑记账）
                var recordService = new RecordCreditDetailService(service, tracer);
                var result = recordService.Record(accountCode, creditType, useBalanceUSD, useBalanceCNY,
                    int.Parse(proccess), int.Parse(adjust), contractCode, orderCode, deliveryNo, settleId, settleNo);

                SetResult(context, accountCode, useBalanceUSD, proccess, adjust, contractCode, orderCode,
                    result.Success, result.UsedBalance, result.SellerBalance, result.RecordId,
                    result.IdempotentReplay ? string.Empty : result.FailReason);

                tracer.Trace($"RecordCreditDetailPlugin 执行完成: Success={result.Success}, IdempotentReplay={result.IdempotentReplay}, RecordId={result.RecordId}");
            }
            catch (Exception ex)
            {
                tracer.Trace($"RecordCreditDetailPlugin 异常: {ex}");
                SetResult(context, accountCode, useBalanceUSD, proccess, adjust, contractCode, orderCode,
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
        /// 入参校验：必填、金额非负、环节 1-12/动作取值、授信类型归一化、条件必填规则。
        /// 哑记账原则：不做动作-环节交叉业务校验（业务正确性由调用方负责）。
        /// </summary>
        private string ValidateInput(string accountCode, ref string creditType, decimal useBalanceUSD, decimal useBalanceCNY,
            string proccess, string adjust, string contractCode, string orderCode, string deliveryNo, string settleId, string settleNo)
        {
            if (string.IsNullOrWhiteSpace(accountCode))
                return "客户编码不能为空";
            if (useBalanceUSD < 0)
                return "使用或者释放的授信金额美元必须大于等于0";
            if (useBalanceCNY < 0)
                return "使用或者释放的授信金额人民币必须大于等于0";
            if (string.IsNullOrWhiteSpace(proccess))
                return "流程环节不能为空";
            if (string.IsNullOrWhiteSpace(adjust))
                return "额度调整动作不能为空";

            if (!int.TryParse(proccess, out int stage) || stage < 1 || stage > 12)
                return $"流程环节取值非法: {proccess}（有效范围1-12）";
            if (!int.TryParse(adjust, out int action) || (action != RecordCreditDetailService.ADJUST_INIT
                && action != RecordCreditDetailService.ADJUST_OCCUPY && action != RecordCreditDetailService.ADJUST_RELEASE))
                return $"额度调整动作取值非法: {adjust}（1=初始化, 3=占用, 4=释放）";

            // 授信类型：选填，空默认 FACTORY；仅接受 FACTORY/SINOSURE
            if (string.IsNullOrWhiteSpace(creditType))
            {
                creditType = "FACTORY";
            }
            else
            {
                creditType = creditType.Trim().ToUpperInvariant();
                if (creditType != "FACTORY" && creditType != "SINOSURE")
                    return $"授信类型取值非法: {creditType}（FACTORY=厂端授信 / SINOSURE=中信保授信）";
            }

            // 初始化动作仅限流程环节 1（厂端授信模型计算）/ 2（厂端授信限额额度申请），同旧 API 口径
            if (action == RecordCreditDetailService.ADJUST_INIT && stage != 1 && stage != 2)
                return "初始化动作仅限流程环节 1（厂端授信模型计算）/ 2（厂端授信限额额度申请）使用";

            // 条件必填：环节≥3 必传合同；环节 6-10 必传订单（接口契约）
            if (stage >= 3 && string.IsNullOrWhiteSpace(contractCode))
                return $"流程环节 {stage} 必须传入合同编码";
            if (stage >= 6 && stage <= 10 && string.IsNullOrWhiteSpace(orderCode))
                return $"流程环节 {stage} 必须传入订单编码";
            // 条件必填：回款解款环节必传解款单号+明细guid；订单执行验收（发货）环节必传发货单编码
            if (stage == 9 && (string.IsNullOrWhiteSpace(settleId) || string.IsNullOrWhiteSpace(settleNo)))
                return "流程环节 9（回款解款）必须传入解款单号和解款单明细guid";
            if (stage == 7 && string.IsNullOrWhiteSpace(deliveryNo))
                return "流程环节 7（订单执行验收）必须传入发货单编码";

            return string.Empty;
        }

        private void SetResult(IPluginExecutionContext context,
            string accountCode, decimal useBalanceUSD, string proccess, string adjust, string contractCode, string orderCode,
            bool success, decimal usedBalance, decimal sellerBalance, string recordId, string failReason)
        {
            // 请求参数原样回显（按接口契约）
            context.OutputParameters["mcs_accountid"] = accountCode;
            context.OutputParameters["mcs_usebalance"] = useBalanceUSD;
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
