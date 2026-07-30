using System;
using System.Text;

namespace SanyD365.Plugins.FactoryCredit.Calculation.Services
{
    /// <summary>
    /// 计算日志生成服务
    /// </summary>
    public class CalculationLogService
    {
        /// <summary>
        /// 生成 1-9 步计算日志
        /// </summary>
        public string BuildLog(LogContext ctx)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"1.取得模型维度和参数：客户分类={ctx.BuyerGrade}, 客户等级={ctx.CreditGrade}, 系数1={ctx.Adjust1:N2}, 系数2={ctx.Adjust2:N2}, 系数3={ctx.Adjust3:N2}, 聚合方法={ctx.AggFunc}, 历史基准额度(USD)={ctx.CountryBenchmark:N2}");

            if (ctx.UsedFallback)
            {
                sb.AppendLine($"2.客户信用分为空或0，或信用评估有效状态≠有效，厂端授信初始额度=历史基准额度={ctx.InitialGrant:N2}，不记录以下计算过程。原因：{ctx.FallbackReason}");
            }
            else
            {
                sb.AppendLine($"2.客户信用评估有效，继续以下模型计算过程");
                sb.AppendLine($"3.计算财务能力=净资产 x 系数1 = {ctx.NetAssets:N2} x {ctx.Adjust1:N2} = {ctx.FinancialAbility:N2}");
                sb.AppendLine($"4.计算历史回款能力=近12个月客户回款月均值 x 系数2 = {ctx.AvgMonthlyPayment:N2} x {ctx.Adjust2:N2} = {ctx.HistoricalPaymentAbility:N2}");
                sb.AppendLine($"5.调节历史基准额度=历史基准额度 x 系数3 = {ctx.CountryBenchmark:N2} x {ctx.Adjust3:N2} = {ctx.HistoricalBenchmarkAdjusted:N2}");
                sb.AppendLine($"6.取三者【{ctx.AggFunc}】值作为厂端授信【模型计算额度】的初始额度={ctx.InitialGrant:N2}");
            }

            sb.AppendLine($"7.取值客户逾期账龄={ctx.MaxOverdueDays}天, 逾期金额/在外货款余额={ctx.OverdueRatio:P2}，判断如下条件，并记录日志。");
            sb.AppendLine($"   {ctx.AdjustmentDescription}");
            sb.AppendLine($"   厂端授信模型调整额度={ctx.AdjustedGrant:N2}");

            if (ctx.IsRejected)
            {
                sb.AppendLine($"   不予授信校验结果：{ctx.RejectReason}");
            }
            else
            {
                sb.AppendLine($"   不予授信校验结果：未触发系统自动判断的不予授信场景");
            }

            sb.AppendLine("8.计算厂端授信结束");
            sb.AppendLine($"9.计算时间：{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");

            return sb.ToString();
        }
    }

    /// <summary>
    /// 日志上下文
    /// </summary>
    public class LogContext
    {
        public string BuyerGrade { get; set; }
        public string CreditGrade { get; set; }
        public decimal Adjust1 { get; set; }
        public decimal Adjust2 { get; set; }
        public decimal Adjust3 { get; set; }
        public string AggFunc { get; set; }
        public decimal CountryBenchmark { get; set; }
        public decimal NetAssets { get; set; }
        public decimal AvgMonthlyPayment { get; set; }
        public decimal FinancialAbility { get; set; }
        public decimal HistoricalPaymentAbility { get; set; }
        public decimal HistoricalBenchmarkAdjusted { get; set; }
        public decimal InitialGrant { get; set; }
        public bool UsedFallback { get; set; }
        public string FallbackReason { get; set; }
        public int MaxOverdueDays { get; set; }
        public decimal OverdueRatio { get; set; }
        public decimal AdjustedGrant { get; set; }
        public string AdjustmentDescription { get; set; }
        public bool IsRejected { get; set; }
        public string RejectReason { get; set; }
    }
}
