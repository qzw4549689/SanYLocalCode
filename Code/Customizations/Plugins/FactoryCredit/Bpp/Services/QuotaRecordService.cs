using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace SanyD365.Plugins.FactoryCredit.Bpp.Services
{
    /// <summary>
    /// 额度调整申请审批通过后，写入厂端授信额度台账
    /// </summary>
    public class QuotaRecordService
    {
        private readonly IOrganizationService _service;
        private readonly ITracingService _tracer;

        // mcs_fca_records.mcs_proccess 选项集值：2-环节2（额度调整申请）
        private const int PROCESS_QUOTA_APP = 2;

        // mcs_fca_records.mcs_adjust 选项集值：1-初始化
        // 注：开发计划定义为初始化，如业务需要可改为其他值
        private const int ADJUST_INIT = 1;

        public QuotaRecordService(IOrganizationService service, ITracingService tracer)
        {
            _service = service;
            _tracer = tracer;
        }

        /// <summary>
        /// 写入台账记录。
        /// 口径：初始化动作的调整金额=0（决策点B，PRD口径）；
        /// 调整后余额 = 调整后额度 - 当前占用（从额度表实时读取，不依赖前端传入的 tobeBalance）。
        /// </summary>
        public void AddQuotaRecord(EntityReference accountRef, string custName,
            Money currentGrant, Money currentBalance, Money tobeGrant, Money tobeBalance)
        {
            if (accountRef == null)
            {
                throw new InvalidPluginExecutionException("客户编码不能为空，无法写入台账。");
            }

            decimal currentGrantValue = currentGrant?.Value ?? 0m;
            decimal currentBalanceValue = currentBalance?.Value ?? 0m;
            decimal tobeGrantValue = tobeGrant?.Value ?? 0m;

            // 从额度表实时读取占用金额；读不到时按 现有额度-现有余额 推算
            decimal usedValue = QueryUsedBalance(accountRef) ?? (currentGrantValue - currentBalanceValue);
            decimal tobeBalanceValue = tobeGrantValue - usedValue;

            _tracer.Trace($"开始写入台账: accountId={accountRef.Id}, currentGrant={currentGrantValue}, " +
                          $"currentBalance={currentBalanceValue}, tobeGrant={tobeGrantValue}, used={usedValue}, tobeBalance={tobeBalanceValue}");

            Entity ledger = new Entity("mcs_fca_records");
            ledger["mcs_accountid"] = accountRef;
            ledger["mcs_custname"] = custName;
            ledger["mcs_proccess"] = new OptionSetValue(PROCESS_QUOTA_APP);
            ledger["mcs_adjust"] = new OptionSetValue(ADJUST_INIT);
            ledger["mcs_sellergrant"] = new Money(tobeGrantValue);
            ledger["mcs_asisbalance"] = new Money(currentBalanceValue);
            ledger["mcs_adjustamt"] = new Money(0m);
            ledger["mcs_tobebalance"] = new Money(tobeBalanceValue);

            Guid ledgerId = _service.Create(ledger);
            _tracer.Trace($"已创建台账记录: {ledgerId}");
        }

        /// <summary>
        /// 查询客户最新额度记录的占用金额；无额度记录时返回 null
        /// </summary>
        private decimal? QueryUsedBalance(EntityReference accountRef)
        {
            var query = new QueryExpression("mcs_fca_quota")
            {
                ColumnSet = new ColumnSet("mcs_usedsellerbalance"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_accountid", ConditionOperator.Equal, accountRef.Id)
                    }
                },
                TopCount = 1
            };
            query.AddOrder("createdon", OrderType.Descending);

            var quota = _service.RetrieveMultiple(query).Entities.FirstOrDefault();
            return quota?.GetAttributeValue<Money>("mcs_usedsellerbalance")?.Value;
        }
    }
}
