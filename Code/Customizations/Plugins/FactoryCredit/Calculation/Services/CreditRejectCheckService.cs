using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace SanyD365.Plugins.FactoryCredit.Calculation.Services
{
    /// <summary>
    /// 不予授信校验服务
    /// </summary>
    public class CreditRejectCheckService
    {
        private readonly IOrganizationService _service;
        private readonly ITracingService _tracer;

        public CreditRejectCheckService(IOrganizationService service, ITracingService tracer)
        {
            _service = service;
            _tracer = tracer;
        }

        /// <summary>
        /// 不予授信校验结果
        /// </summary>
        public class RejectResult
        {
            public bool IsRejected { get; set; }
            public string Reason { get; set; }
            public int[] TriggeredScenarios { get; set; }
        }

        /// <summary>
        /// 检查是否触发不予授信场景3或4，并回写客户主数据
        /// </summary>
        /// <param name="masterDataId">客户主数据ID</param>
        /// <param name="accountId">Account ID（用于查询月度在外货款）</param>
        public RejectResult CheckAndUpdate(Guid masterDataId, Guid accountId)
        {
            bool scenario3 = IsScenario3Triggered(accountId);
            bool scenario4 = IsScenario4Triggered(masterDataId);

            RejectResult result = new RejectResult
            {
                IsRejected = scenario3 || scenario4,
                TriggeredScenarios = new int[0]
            };

            if (scenario3 && scenario4)
            {
                result.TriggeredScenarios = new[] { 3, 4 };
                result.Reason = "触发不予授信场景3（实质性逾期）和场景4（集团黑名单客户）";
            }
            else if (scenario3)
            {
                result.TriggeredScenarios = new[] { 3 };
                result.Reason = "触发不予授信场景3：实际逾期账龄≥6个月且逾期金额/在外货款余额>50%";
            }
            else if (scenario4)
            {
                result.TriggeredScenarios = new[] { 4 };
                result.Reason = "触发不予授信场景4：集团黑名单客户";
            }
            else
            {
                result.Reason = "未触发系统自动判断的不予授信场景";
            }

            _tracer.Trace(result.Reason);

            if (result.IsRejected)
            {
                UpdateCustomerRejectFlag(masterDataId);
            }

            return result;
        }

        /// <summary>
        /// 场景3：逾期账龄≥6个月且逾期金额/在外货款余额>50%
        /// </summary>
        private bool IsScenario3Triggered(Guid accountId)
        {
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

                if (maxDays >= 180 && totalOutstanding > 0m && totalOverdue / totalOutstanding > 0.5m)
                {
                    _tracer.Trace($"场景3触发: maxDays={maxDays}, totalOverdue={totalOverdue}, totalOutstanding={totalOutstanding}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                _tracer.Trace($"场景3判断失败: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// 场景4：黑名单客户
        /// </summary>
        private bool IsScenario4Triggered(Guid masterDataId)
        {
            try
            {
                Entity customer = _service.Retrieve("mcs_customermasterdata", masterDataId,
                    new ColumnSet("mcs_blacklist"));

                bool? blackList = customer.GetAttributeValue<bool?>("mcs_blacklist");
                _tracer.Trace($"黑名单标记={blackList}");
                return blackList == true;
            }
            catch (Exception ex)
            {
                _tracer.Trace($"场景4判断失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 回写客户主数据【不予授信客户】字段
        /// </summary>
        private void UpdateCustomerRejectFlag(Guid masterDataId)
        {
            try
            {
                Entity update = new Entity("mcs_customermasterdata", masterDataId);
                update["mcs_creditgrant"] = true;
                _service.Update(update);
                _tracer.Trace($"已回写客户主数据 {masterDataId} 不予授信标记=true");
            }
            catch (Exception ex)
            {
                // 字段可能尚未创建，记录日志但不阻断流程
                _tracer.Trace($"回写客户不予授信标记失败（字段可能不存在）: {ex.Message}");
            }
        }

    }
}
