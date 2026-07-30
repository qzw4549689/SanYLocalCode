using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace SanyD365.Plugins.FactoryCredit.Calculation.Services
{
    /// <summary>
    /// 逾期调整服务
    /// </summary>
    public class OverdueAdjustmentService
    {
        private readonly IOrganizationService _service;
        private readonly ITracingService _tracer;

        public OverdueAdjustmentService(IOrganizationService service, ITracingService tracer)
        {
            _service = service;
            _tracer = tracer;
        }

        /// <summary>
        /// 逾期调整结果
        /// </summary>
        public class AdjustmentResult
        {
            public decimal AdjustedGrant { get; set; }
            public int MaxOverdueDays { get; set; }
            public decimal OverdueAmount { get; set; }
            public decimal OutstandingBalance { get; set; }
            public decimal OverdueRatio { get; set; }
            public string AdjustmentDescription { get; set; }
            public bool TriggeredReject { get; set; }
        }

        /// <summary>
        /// 根据逾期情况调整额度
        /// </summary>
        /// <param name="accountId">Account ID</param>
        /// <param name="initialGrant">初始额度</param>
        public AdjustmentResult Adjust(Guid accountId, decimal initialGrant)
        {
            var overdueInfo = GetOverdueInfo(accountId);

            AdjustmentResult result = new AdjustmentResult
            {
                AdjustedGrant = initialGrant,
                MaxOverdueDays = overdueInfo.MaxOverdueDays,
                OverdueAmount = overdueInfo.OverdueAmount,
                OutstandingBalance = overdueInfo.OutstandingBalance,
                OverdueRatio = overdueInfo.OverdueRatio,
                TriggeredReject = false
            };

            if (overdueInfo.MaxOverdueDays < 180)
            {
                result.AdjustmentDescription = "逾期账龄未超过6个月，不做调整";
                _tracer.Trace(result.AdjustmentDescription);
                return result;
            }

            if (overdueInfo.OutstandingBalance == 0m)
            {
                result.AdjustmentDescription = "在外货款余额为0，不做调整";
                _tracer.Trace(result.AdjustmentDescription);
                return result;
            }

            if (overdueInfo.OverdueRatio > 0.5m)
            {
                result.AdjustedGrant = 0m;
                result.TriggeredReject = true;
                result.AdjustmentDescription = $"逾期账龄≥6个月且逾期金额/在外货款余额={overdueInfo.OverdueRatio:P0}>50%，触发不予授信，额度归0";
            }
            else if (overdueInfo.OverdueRatio > 0.2m)
            {
                result.AdjustedGrant = initialGrant * 0.5m;
                result.AdjustmentDescription = $"逾期账龄≥6个月且20%<逾期金额/在外货款余额={overdueInfo.OverdueRatio:P0}≤50%，额度下调至50%";
            }
            else
            {
                result.AdjustedGrant = initialGrant * 0.8m;
                result.AdjustmentDescription = $"逾期账龄≥6个月且逾期金额/在外货款余额={overdueInfo.OverdueRatio:P0}≤20%，额度下调至80%";
            }

            _tracer.Trace(result.AdjustmentDescription);
            return result;
        }

        /// <summary>
        /// 获取客户逾期信息
        /// </summary>
        private OverdueInfo GetOverdueInfo(Guid accountId)
        {
            OverdueInfo info = new OverdueInfo();

            try
            {
                QueryExpression query = new QueryExpression("mcs_outstanding")
                {
                    ColumnSet = new ColumnSet("mcs_overdurationdays", "mcs_newoverdueamount", "mcs_newremainingamount"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("mcs_account", ConditionOperator.Equal, accountId)
                        }
                    }
                };

                EntityCollection result = _service.RetrieveMultiple(query);
                int maxDays = 0;
                decimal totalOverdue = 0m;
                decimal totalOutstanding = 0m;

                foreach (Entity entity in result.Entities)
                {
                    int days = entity.GetAttributeValue<int>("mcs_overdurationdays");
                    if (days > maxDays)
                    {
                        maxDays = days;
                    }

                    totalOverdue += entity.GetAttributeValue<Money>("mcs_newoverdueamount")?.Value ?? 0m;
                    totalOutstanding += entity.GetAttributeValue<Money>("mcs_newremainingamount")?.Value ?? 0m;
                }

                info.MaxOverdueDays = maxDays;
                info.OverdueAmount = totalOverdue;
                info.OutstandingBalance = totalOutstanding;
                info.OverdueRatio = totalOutstanding == 0m ? 0m : totalOverdue / totalOutstanding;

                _tracer.Trace($"逾期信息: 最大逾期天数={maxDays}, 逾期金额={totalOverdue}, 在外货款余额={totalOutstanding}, 比例={info.OverdueRatio:P2}");
            }
            catch (Exception ex)
            {
                _tracer.Trace($"获取逾期信息失败: {ex.Message}");
            }

            return info;
        }

        private class OverdueInfo
        {
            public int MaxOverdueDays { get; set; }
            public decimal OverdueAmount { get; set; }
            public decimal OutstandingBalance { get; set; }
            public decimal OverdueRatio { get; set; }
        }
    }
}
