using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace SanyD365.Plugins.FactoryCredit.Api
{
    /// <summary>
    /// 厂端授信余额调整结果
    /// </summary>
    public class FcaQuotaAdjustResult
    {
        public bool Success { get; set; }
        public string FailReason { get; set; } = string.Empty;
        /// <summary>实际调整厂端授信余额USD（失败时为 0）</summary>
        public decimal UsedBalance { get; set; }
        /// <summary>调整后厂端授信余额USD</summary>
        public decimal SellerBalance { get; set; }
        /// <summary>台账编号（成功时填入）</summary>
        public string RecordId { get; set; } = string.Empty;
    }

    /// <summary>
    /// 厂端授信余额调整服务：初始化 / 占用 / 释放
    /// 核心不变式：授信额度 = 授信余额 + 占用金额（sellergrant = sellerbalance + usedsellerbalance）
    /// </summary>
    public class FcaQuotaAdjustService
    {
        // 额度调整动作
        public const int ADJUST_INIT = 1;    // 初始化
        public const int ADJUST_OCCUPY = 3;  // 占用
        public const int ADJUST_RELEASE = 4; // 释放

        // mcs_fca_quota.mcs_isactive 选项集值：1-是
        private const int IS_ACTIVE_YES = 1;

        private readonly IOrganizationService _service;
        private readonly ITracingService _tracer;

        public FcaQuotaAdjustService(IOrganizationService service, ITracingService tracer)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _tracer = tracer ?? throw new ArgumentNullException(nameof(tracer));
        }

        /// <summary>
        /// 执行厂端授信余额调整
        /// </summary>
        /// <param name="customerCode">客户编码（SAP客户代码，mcs_customermasterdata.mcs_sapnumber）</param>
        /// <param name="amount">调整金额USD；初始化动作时表示新的授信额度</param>
        /// <param name="stage">流程环节（台账表 1-11 口径）</param>
        /// <param name="action">额度调整动作（1初始化/3占用/4释放）</param>
        /// <param name="contractCode">合同编码（mcs_contract.mcs_name），可选</param>
        /// <param name="orderCode">订单编码（mcs_order.mcs_name），可选</param>
        public FcaQuotaAdjustResult Adjust(string customerCode, decimal amount, int stage, int action,
            string contractCode, string orderCode)
        {
            // 1. 解析客户
            var customer = ResolveCustomer(customerCode);
            if (customer == null)
            {
                return Fail($"未找到客户编码[{customerCode}]对应的客户主数据");
            }

            // 2. 解析合同/订单（有传入则必须能找到）
            EntityReference contractRef = null;
            if (!string.IsNullOrWhiteSpace(contractCode))
            {
                contractRef = ResolveByName("mcs_contract", contractCode);
                if (contractRef == null)
                {
                    return Fail($"未找到合同编码[{contractCode}]对应的合同");
                }
            }

            EntityReference orderRef = null;
            if (!string.IsNullOrWhiteSpace(orderCode))
            {
                orderRef = ResolveByName("mcs_order", orderCode);
                if (orderRef == null)
                {
                    return Fail($"未找到订单编码[{orderCode}]对应的订单");
                }
            }

            // 3. 按动作分发
            switch (action)
            {
                case ADJUST_INIT:
                    return ProcessInit(customer, amount, stage, contractRef, orderRef);
                case ADJUST_OCCUPY:
                    return ProcessOccupy(customer, amount, stage, contractRef, orderRef);
                case ADJUST_RELEASE:
                    return ProcessRelease(customer, amount, stage, contractRef, orderRef);
                default:
                    return Fail($"不支持的额度调整动作: {action}");
            }
        }

        /// <summary>
        /// 初始化：新增或更新额度记录。
        /// 新建：余额=额度、占用=0；更新：占用不清零，余额=新额度-现有占用（决策点A）。
        /// 台账调整金额=0（决策点B，PRD口径）。
        /// </summary>
        private FcaQuotaAdjustResult ProcessInit(Entity customer, decimal grant, int stage,
            EntityReference contractRef, EntityReference orderRef)
        {
            var accountRef = customer.ToEntityReference();
            string custName = customer.GetAttributeValue<string>("mcs_name") ?? string.Empty;
            var quota = GetQuota(accountRef);

            decimal oldBalance;
            decimal newBalance;
            bool quotaCreated = false;
            bool quotaUpdated = false;
            Guid quotaId = Guid.Empty;
            decimal oldGrant = 0m;

            if (quota != null)
            {
                // 更新已有额度：占用不清零，余额 = 新额度 - 现有占用
                decimal used = GetMoney(quota, "mcs_usedsellerbalance");
                oldGrant = GetMoney(quota, "mcs_sellergrant");
                oldBalance = GetMoney(quota, "mcs_sellerbalance");
                newBalance = grant - used;
                quotaId = quota.Id;
            }
            else
            {
                // 新建额度：余额=额度、占用=0
                oldBalance = 0m;
                newBalance = grant;
            }

            try
            {
                if (quota != null)
                {
                    var update = new Entity("mcs_fca_quota", quota.Id);
                    update["mcs_sellergrant"] = new Money(grant);
                    update["mcs_sellerbalance"] = new Money(newBalance);
                    update["mcs_isactive"] = new OptionSetValue(IS_ACTIVE_YES);
                    update["mcs_validfrom"] = DateTime.UtcNow;
                    _service.Update(update);
                    quotaUpdated = true;
                    _tracer.Trace($"初始化-更新额度记录: {quota.Id}, grant={grant}, balance={newBalance}");
                }
                else
                {
                    var create = new Entity("mcs_fca_quota");
                    create["mcs_accountid"] = accountRef;
                    create["mcs_custname"] = custName;
                    create["mcs_sellergrant"] = new Money(grant);
                    create["mcs_sellerbalance"] = new Money(newBalance);
                    create["mcs_usedsellerbalance"] = new Money(0m);
                    create["mcs_isactive"] = new OptionSetValue(IS_ACTIVE_YES);
                    create["mcs_validfrom"] = DateTime.UtcNow;
                    quotaId = _service.Create(create);
                    quotaCreated = true;
                    _tracer.Trace($"初始化-新建额度记录: {quotaId}, grant={grant}");
                }

                // 台账：初始化调整金额=0
                string recordId = CreateLedger(accountRef, custName, contractRef, orderRef, stage, ADJUST_INIT,
                    grant, oldBalance, 0m, newBalance);

                return Success(0m, newBalance, recordId);
            }
            catch (Exception ex)
            {
                // 补偿回滚：新建则删除额度记录，更新则恢复原额度/余额
                if (quotaCreated)
                {
                    DeleteQuotaQuietly(quotaId);
                }
                else if (quotaUpdated)
                {
                    RevertQuota(quotaId, oldGrant, oldBalance, null);
                }
                return Fail($"调整失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 占用：订单发货后扣减余额、增加占用。余额不足不拦截（允许负余额表示超额）。
        /// 重复性校验：按 订单编号优先、为空用合同编号 + 客户编码 查台账最新记录，最新动作已是占用则判定重复。
        /// </summary>
        private FcaQuotaAdjustResult ProcessOccupy(Entity customer, decimal amount, int stage,
            EntityReference contractRef, EntityReference orderRef)
        {
            var accountRef = customer.ToEntityReference();
            string custName = customer.GetAttributeValue<string>("mcs_name") ?? string.Empty;

            var quota = GetQuota(accountRef);
            if (quota == null)
            {
                return Fail($"客户编码[{customer.GetAttributeValue<string>("mcs_sapnumber")}]的厂端授信额度不存在，无法执行占用");
            }
            if (!IsActive(quota))
            {
                return Fail("客户厂端授信额度未生效，无法执行占用");
            }

            // 重复性校验：订单编号优先，为空用合同编号
            if (IsDuplicateOccupy(accountRef, orderRef ?? contractRef))
            {
                return Fail("对应合同已执行占用，无法再次执行动作占用");
            }

            decimal grant = GetMoney(quota, "mcs_sellergrant");
            decimal oldBalance = GetMoney(quota, "mcs_sellerbalance");
            decimal oldUsed = GetMoney(quota, "mcs_usedsellerbalance");
            decimal newBalance = oldBalance - amount;
            bool quotaUpdated = false;

            try
            {
                var update = new Entity("mcs_fca_quota", quota.Id);
                update["mcs_sellerbalance"] = new Money(newBalance);
                update["mcs_usedsellerbalance"] = new Money(oldUsed + amount);
                _service.Update(update);
                quotaUpdated = true;
                _tracer.Trace($"占用-更新额度记录: {quota.Id}, balance {oldBalance} -> {newBalance}, used {oldUsed} -> {oldUsed + amount}");

                string recordId = CreateLedger(accountRef, custName, contractRef, orderRef, stage, ADJUST_OCCUPY,
                    grant, oldBalance, amount, newBalance);

                return Success(amount, newBalance, recordId);
            }
            catch (Exception ex)
            {
                if (quotaUpdated)
                {
                    RevertQuota(quota.Id, grant, oldBalance, oldUsed);
                }
                return Fail($"调整失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 释放：订单取消/退货、回款解款后加回余额、减少占用。不做防重复校验。
        /// </summary>
        private FcaQuotaAdjustResult ProcessRelease(Entity customer, decimal amount, int stage,
            EntityReference contractRef, EntityReference orderRef)
        {
            var accountRef = customer.ToEntityReference();
            string custName = customer.GetAttributeValue<string>("mcs_name") ?? string.Empty;

            var quota = GetQuota(accountRef);
            if (quota == null)
            {
                return Fail($"客户编码[{customer.GetAttributeValue<string>("mcs_sapnumber")}]的厂端授信额度不存在，无法执行释放");
            }
            if (!IsActive(quota))
            {
                return Fail("客户厂端授信额度未生效，无法执行释放");
            }

            decimal grant = GetMoney(quota, "mcs_sellergrant");
            decimal oldBalance = GetMoney(quota, "mcs_sellerbalance");
            decimal oldUsed = GetMoney(quota, "mcs_usedsellerbalance");
            decimal newBalance = oldBalance + amount;
            bool quotaUpdated = false;

            try
            {
                var update = new Entity("mcs_fca_quota", quota.Id);
                update["mcs_sellerbalance"] = new Money(newBalance);
                update["mcs_usedsellerbalance"] = new Money(oldUsed - amount);
                _service.Update(update);
                quotaUpdated = true;
                _tracer.Trace($"释放-更新额度记录: {quota.Id}, balance {oldBalance} -> {newBalance}, used {oldUsed} -> {oldUsed - amount}");

                string recordId = CreateLedger(accountRef, custName, contractRef, orderRef, stage, ADJUST_RELEASE,
                    grant, oldBalance, amount, newBalance);

                return Success(amount, newBalance, recordId);
            }
            catch (Exception ex)
            {
                if (quotaUpdated)
                {
                    RevertQuota(quota.Id, grant, oldBalance, oldUsed);
                }
                return Fail($"调整失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 占用重复性校验：按 客户 + 订单（或合同）查台账最新记录，最新动作已是占用则重复
        /// </summary>
        private bool IsDuplicateOccupy(EntityReference accountRef, EntityReference keyRef)
        {
            if (keyRef == null)
            {
                return false;
            }

            string keyField = keyRef.LogicalName == "mcs_order" ? "mcs_orderid" : "mcs_contractid";

            var query = new QueryExpression("mcs_fca_records")
            {
                ColumnSet = new ColumnSet("mcs_adjust"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_accountid", ConditionOperator.Equal, accountRef.Id),
                        new ConditionExpression(keyField, ConditionOperator.Equal, keyRef.Id)
                    }
                },
                TopCount = 1
            };
            query.AddOrder("createdon", OrderType.Descending);

            var latest = _service.RetrieveMultiple(query).Entities.FirstOrDefault();
            if (latest == null)
            {
                return false;
            }

            int latestAdjust = latest.GetAttributeValue<OptionSetValue>("mcs_adjust")?.Value ?? 0;
            _tracer.Trace($"占用重复性校验: {keyField}={keyRef.Id}, 最新台账动作={latestAdjust}");
            return latestAdjust == ADJUST_OCCUPY;
        }

        /// <summary>
        /// 创建台账记录，返回台账编号（mcs_recordid 由系统统一自动编号 Plugin 生成）
        /// </summary>
        private string CreateLedger(EntityReference accountRef, string custName,
            EntityReference contractRef, EntityReference orderRef, int stage, int action,
            decimal grant, decimal asisBalance, decimal adjustAmt, decimal tobeBalance)
        {
            var ledger = new Entity("mcs_fca_records");
            ledger["mcs_accountid"] = accountRef;
            ledger["mcs_custname"] = custName;
            if (contractRef != null)
            {
                ledger["mcs_contractid"] = contractRef;
            }
            if (orderRef != null)
            {
                ledger["mcs_orderid"] = orderRef;
            }
            ledger["mcs_proccess"] = new OptionSetValue(stage);
            ledger["mcs_adjust"] = new OptionSetValue(action);
            ledger["mcs_sellergrant"] = new Money(grant);
            ledger["mcs_asisbalance"] = new Money(asisBalance);
            ledger["mcs_adjustamt"] = new Money(adjustAmt);
            ledger["mcs_tobebalance"] = new Money(tobeBalance);

            Guid ledgerId = _service.Create(ledger);
            _tracer.Trace($"已创建台账记录: {ledgerId}");

            // 读取自动编号生成的台账编号
            var created = _service.Retrieve("mcs_fca_records", ledgerId, new ColumnSet("mcs_recordid"));
            return created.GetAttributeValue<string>("mcs_recordid") ?? string.Empty;
        }

        /// <summary>
        /// 按客户编码（SAP客户代码）解析客户主数据
        /// </summary>
        private Entity ResolveCustomer(string customerCode)
        {
            var query = new QueryExpression("mcs_customermasterdata")
            {
                ColumnSet = new ColumnSet("mcs_name", "mcs_sapnumber"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_sapnumber", ConditionOperator.Equal, customerCode)
                    }
                },
                TopCount = 1
            };

            var customer = _service.RetrieveMultiple(query).Entities.FirstOrDefault();
            _tracer.Trace($"解析客户: code={customerCode}, found={(customer != null)}");
            return customer;
        }

        /// <summary>
        /// 按主名称字段（mcs_name）解析合同/订单
        /// </summary>
        private EntityReference ResolveByName(string entityName, string code)
        {
            var query = new QueryExpression(entityName)
            {
                ColumnSet = new ColumnSet("mcs_name"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_name", ConditionOperator.Equal, code)
                    }
                },
                TopCount = 1
            };

            var entity = _service.RetrieveMultiple(query).Entities.FirstOrDefault();
            _tracer.Trace($"解析{entityName}: code={code}, found={(entity != null)}");
            return entity?.ToEntityReference();
        }

        /// <summary>
        /// 按客户查询最新一条厂端授信额度记录
        /// </summary>
        private Entity GetQuota(EntityReference accountRef)
        {
            var query = new QueryExpression("mcs_fca_quota")
            {
                ColumnSet = new ColumnSet("mcs_sellergrant", "mcs_sellerbalance", "mcs_usedsellerbalance", "mcs_isactive"),
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

            return _service.RetrieveMultiple(query).Entities.FirstOrDefault();
        }

        /// <summary>
        /// 补偿回滚额度记录（台账写入失败等异常时恢复额度/余额/占用）
        /// </summary>
        private void RevertQuota(Guid quotaId, decimal oldGrant, decimal oldBalance, decimal? oldUsed)
        {
            try
            {
                var revert = new Entity("mcs_fca_quota", quotaId);
                revert["mcs_sellergrant"] = new Money(oldGrant);
                revert["mcs_sellerbalance"] = new Money(oldBalance);
                if (oldUsed.HasValue)
                {
                    revert["mcs_usedsellerbalance"] = new Money(oldUsed.Value);
                }
                _service.Update(revert);
                _tracer.Trace($"已补偿回滚额度记录: {quotaId}");
            }
            catch (Exception revertEx)
            {
                _tracer.Trace($"⚠️ 额度记录补偿回滚失败: {quotaId}, {revertEx.Message}");
            }
        }

        /// <summary>
        /// 补偿删除新建的额度记录（台账写入失败等异常时）
        /// </summary>
        private void DeleteQuotaQuietly(Guid quotaId)
        {
            try
            {
                _service.Delete("mcs_fca_quota", quotaId);
                _tracer.Trace($"已补偿删除额度记录: {quotaId}");
            }
            catch (Exception deleteEx)
            {
                _tracer.Trace($"⚠️ 额度记录补偿删除失败: {quotaId}, {deleteEx.Message}");
            }
        }

        private bool IsActive(Entity quota)
        {
            return quota.GetAttributeValue<OptionSetValue>("mcs_isactive")?.Value == IS_ACTIVE_YES;
        }

        private decimal GetMoney(Entity entity, string fieldName)
        {
            return entity.GetAttributeValue<Money>(fieldName)?.Value ?? 0m;
        }

        private FcaQuotaAdjustResult Fail(string reason)
        {
            _tracer.Trace($"调整失败: {reason}");
            return new FcaQuotaAdjustResult { Success = false, FailReason = reason, UsedBalance = 0m };
        }

        private FcaQuotaAdjustResult Success(decimal usedBalance, decimal sellerBalance, string recordId)
        {
            return new FcaQuotaAdjustResult
            {
                Success = true,
                UsedBalance = usedBalance,
                SellerBalance = sellerBalance,
                RecordId = recordId ?? string.Empty
            };
        }
    }
}
