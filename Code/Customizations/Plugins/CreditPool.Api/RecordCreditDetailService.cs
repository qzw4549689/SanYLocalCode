using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace SanyD365.Plugins.CreditPool.Api
{
    /// <summary>
    /// 使用授信（占用/释放）处理结果
    /// </summary>
    public class RecordCreditDetailResult
    {
        public bool Success { get; set; }
        public string FailReason { get; set; } = string.Empty;
        /// <summary>实际调整授信金额USD（失败时为 0）</summary>
        public decimal UsedBalance { get; set; }
        /// <summary>调整后余额USD：FACTORY=厂端授信余额；SINOSURE=信保上浮余额（允许负数表示超额）</summary>
        public decimal SellerBalance { get; set; }
        /// <summary>台账编号（成功时填入）</summary>
        public string RecordId { get; set; } = string.Empty;
        /// <summary>是否幂等重放（重复调用直接返回首次结果，未重复记账）</summary>
        public bool IdempotentReplay { get; set; }
    }

    /// <summary>
    /// 使用授信服务（816 授信池·哑记账）：
    /// 金额由调用方算好传入，我方只校验 + 幂等保存台账 + 更新余额。
    /// FACTORY：复用厂端授信额度表 mcs_fca_quota（不变式：授信额度 = 余额 + 占用）；
    /// SINOSURE：不落余额库，净占用由台账聚合（Σ占用 − Σ释放），余额 = 上浮限额 − 净占用。
    /// </summary>
    public class RecordCreditDetailService
    {
        // 额度调整动作
        public const int ADJUST_INIT = 1;    // 初始化
        public const int ADJUST_OCCUPY = 3;  // 占用
        public const int ADJUST_RELEASE = 4; // 释放

        // 授信类型（台账 mcs_credit_type 选项集值）
        public const int CREDIT_FACTORY = 1;
        public const int CREDIT_SINOSURE = 2;

        // mcs_fca_quota.mcs_isactive 选项集值：1-是
        private const int IS_ACTIVE_YES = 1;

        // mcs_approvedquota.mcs_quotastate 选项集值：1-有效
        private const int QUOTA_STATE_VALID = 1;

        private readonly IOrganizationService _service;
        private readonly ITracingService _tracer;

        public RecordCreditDetailService(IOrganizationService service, ITracingService tracer)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _tracer = tracer ?? throw new ArgumentNullException(nameof(tracer));
        }

        /// <summary>
        /// 执行占用/释放/初始化（哑记账）
        /// </summary>
        /// <param name="customerCode">客户编码（SAP客户代码，mcs_customermasterdata.mcs_sapnumber）</param>
        /// <param name="creditType">授信类型：FACTORY/SINOSURE（空=FACTORY，调用前已归一化）</param>
        /// <param name="amountUSD">使用或者释放的授信金额USD</param>
        /// <param name="amountCNY">使用或者释放的授信金额人民币（快照保存）</param>
        /// <param name="stage">流程环节 1-12</param>
        /// <param name="action">额度调整动作 1初始化/3占用/4释放</param>
        /// <param name="contractCode">合同编码（mcs_contract.mcs_name）</param>
        /// <param name="orderCode">订单编码（mcs_order.mcs_name）</param>
        /// <param name="deliveryNo">发货单编码</param>
        /// <param name="settleId">解款单明细guid</param>
        /// <param name="settleNo">解款单号</param>
        public RecordCreditDetailResult Record(string customerCode, string creditType, decimal amountUSD, decimal amountCNY,
            int stage, int action, string contractCode, string orderCode, string deliveryNo, string settleId, string settleNo)
        {
            bool isSinosure = string.Equals(creditType, "SINOSURE", StringComparison.OrdinalIgnoreCase);

            // 1. 解析客户
            var customer = ResolveCustomer(customerCode);
            if (customer == null)
            {
                return Fail($"未找到客户编码[{customerCode}]对应的客户主数据");
            }
            var accountRef = customer.ToEntityReference();
            string custName = customer.GetAttributeValue<string>("mcs_name") ?? string.Empty;

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

            // 3. 幂等检查：命中直接返回首次结果，不重复记账
            string idempotencyKey = BuildIdempotencyKey(stage, action, amountUSD, contractCode, orderCode, deliveryNo, settleId);
            var existing = FindLedgerByIdempotencyKey(idempotencyKey);
            if (existing != null)
            {
                string existRecordId = existing.GetAttributeValue<string>("mcs_recordid") ?? string.Empty;
                decimal existAmount = GetMoney(existing, "mcs_adjustamt");
                decimal existTobe = GetMoney(existing, "mcs_tobebalance");
                _tracer.Trace($"幂等命中: key={idempotencyKey}, 台账={existRecordId}，直接返回首次结果");
                return new RecordCreditDetailResult
                {
                    Success = true,
                    UsedBalance = existAmount,
                    SellerBalance = existTobe,
                    RecordId = existRecordId,
                    IdempotentReplay = true
                };
            }

            // 4. 按授信类型分发
            if (isSinosure)
            {
                if (action == ADJUST_INIT)
                {
                    return Fail("中信保授信不支持初始化动作（批复限额由中信保模块 T+1 同步维护）");
                }
                return ProcessSinosure(customer, amountUSD, amountCNY, stage, action,
                    contractRef, orderRef, deliveryNo, settleId, settleNo, idempotencyKey);
            }
            else
            {
                return ProcessFactory(customer, amountUSD, amountCNY, stage, action,
                    contractRef, orderRef, deliveryNo, settleId, settleNo, idempotencyKey);
            }
        }

        #region 厂端授信（复用 mcs_fca_quota 额度表）

        /// <summary>
        /// 厂端授信：初始化/占用/释放，更新 mcs_fca_quota 并写台账。
        /// 与旧 mcs_AdjustFcaQuotaBalance 同一套不变式，差异：台账带新字段 + 幂等键防重（替代旧的按订单/合同防重）。
        /// </summary>
        private RecordCreditDetailResult ProcessFactory(Entity customer, decimal amountUSD, decimal amountCNY,
            int stage, int action, EntityReference contractRef, EntityReference orderRef,
            string deliveryNo, string settleId, string settleNo, string idempotencyKey)
        {
            var accountRef = customer.ToEntityReference();
            string custName = customer.GetAttributeValue<string>("mcs_name") ?? string.Empty;
            var quota = GetQuota(accountRef);

            if (action == ADJUST_INIT)
            {
                return ProcessFactoryInit(customer, quota, amountUSD, amountCNY, stage,
                    contractRef, orderRef, deliveryNo, settleId, settleNo, idempotencyKey);
            }

            // 占用/释放：额度必须存在且生效
            if (quota == null)
            {
                return Fail($"客户编码[{customer.GetAttributeValue<string>("mcs_sapnumber")}]的厂端授信额度不存在，无法执行{(action == ADJUST_OCCUPY ? "占用" : "释放")}");
            }
            if (!IsActive(quota))
            {
                return Fail($"客户厂端授信额度未生效，无法执行{(action == ADJUST_OCCUPY ? "占用" : "释放")}");
            }

            decimal grant = GetMoney(quota, "mcs_sellergrant");
            decimal oldBalance = GetMoney(quota, "mcs_sellerbalance");
            decimal oldUsed = GetMoney(quota, "mcs_usedsellerbalance");
            decimal newBalance = action == ADJUST_OCCUPY ? oldBalance - amountUSD : oldBalance + amountUSD;
            decimal newUsed = action == ADJUST_OCCUPY ? oldUsed + amountUSD : oldUsed - amountUSD;
            // 释放超过累计占用时占用金额按 0 兜底（mcs_usedsellerbalance 元数据下限 0，不允许负数）；
            // 余额照常累加（超额释放余额可超过额度，与"余额允许负数"同为敞口口径）。哑记账不拦截，由调用方保证金额正确。
            if (newUsed < 0)
            {
                _tracer.Trace($"⚠️ 释放金额 {amountUSD} 超过累计占用 {oldUsed}，占用金额按 0 兜底");
                newUsed = 0m;
            }
            bool quotaUpdated = false;

            try
            {
                var update = new Entity("mcs_fca_quota", quota.Id);
                update["mcs_sellerbalance"] = new Money(newBalance);
                update["mcs_usedsellerbalance"] = new Money(newUsed);
                _service.Update(update);
                quotaUpdated = true;
                _tracer.Trace($"厂端{(action == ADJUST_OCCUPY ? "占用" : "释放")}-更新额度: {quota.Id}, balance {oldBalance} -> {newBalance}, used {oldUsed} -> {newUsed}");

                string recordId = CreateLedger(accountRef, custName, GetCustomerOwner(customer), contractRef, orderRef,
                    stage, action, CREDIT_FACTORY, grant, oldBalance, amountUSD, newBalance,
                    amountCNY, deliveryNo, settleId, settleNo, idempotencyKey);

                return Success(amountUSD, newBalance, recordId);
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
        /// 厂端初始化：新增或更新额度记录。新建：余额=额度、占用=0；更新：占用不清零，余额=新额度-现有占用。
        /// 台账调整金额=0（PRD 口径，同旧 API）。
        /// </summary>
        private RecordCreditDetailResult ProcessFactoryInit(Entity customer, Entity quota, decimal grant, decimal amountCNY,
            int stage, EntityReference contractRef, EntityReference orderRef,
            string deliveryNo, string settleId, string settleNo, string idempotencyKey)
        {
            var accountRef = customer.ToEntityReference();
            string custName = customer.GetAttributeValue<string>("mcs_name") ?? string.Empty;

            decimal oldBalance;
            decimal newBalance;
            bool quotaCreated = false;
            bool quotaUpdated = false;
            Guid quotaId = Guid.Empty;
            decimal oldGrant = 0m;

            if (quota != null)
            {
                decimal used = GetMoney(quota, "mcs_usedsellerbalance");
                oldGrant = GetMoney(quota, "mcs_sellergrant");
                oldBalance = GetMoney(quota, "mcs_sellerbalance");
                newBalance = grant - used;
                quotaId = quota.Id;
            }
            else
            {
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
                    var customerOwner = GetCustomerOwner(customer);
                    if (customerOwner != null)
                    {
                        create["ownerid"] = customerOwner;
                    }
                    quotaId = _service.Create(create);
                    quotaCreated = true;
                }

                string recordId = CreateLedger(accountRef, custName, GetCustomerOwner(customer), contractRef, orderRef,
                    stage, ADJUST_INIT, CREDIT_FACTORY, grant, oldBalance, 0m, newBalance,
                    amountCNY, deliveryNo, settleId, settleNo, idempotencyKey);

                return Success(0m, newBalance, recordId);
            }
            catch (Exception ex)
            {
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

        #endregion

        #region 中信保授信（不落余额库，台账聚合净占用）

        /// <summary>
        /// 中信保授信：占用/释放只写台账（credit_type=2），不更新任何额度表。
        /// 余额口径：上浮限额 − 净占用；上浮限额 = min(批复限额 × 系数, 封顶)，排除国家不上浮。
        /// </summary>
        private RecordCreditDetailResult ProcessSinosure(Entity customer, decimal amountUSD, decimal amountCNY,
            int stage, int action, EntityReference contractRef, EntityReference orderRef,
            string deliveryNo, string settleId, string settleNo, string idempotencyKey)
        {
            var accountRef = customer.ToEntityReference();
            string custName = customer.GetAttributeValue<string>("mcs_name") ?? string.Empty;
            string countryCode = customer.GetAttributeValue<string>("mcs_countrycode") ?? string.Empty;

            // 批复限额（mcs_approvedquota 有效记录汇总）
            decimal officialLimit = GetSinosureOfficialLimit(accountRef);
            // 上浮限额（配置参数化）
            var config = SinosureUpliftConfigHelper.GetConfig(_service, _tracer);
            decimal upliftLimit = SinosureUpliftConfigHelper.CalcUpliftLimit(config, officialLimit, countryCode);
            // 净占用（台账聚合：Σ占用 − Σ释放，credit_type=2）
            decimal netBefore = GetSinosureNetOccupied(accountRef);

            decimal asisBalance = upliftLimit - netBefore;
            decimal netAfter = action == ADJUST_OCCUPY ? netBefore + amountUSD : netBefore - amountUSD;
            decimal tobeBalance = upliftLimit - netAfter;

            _tracer.Trace($"信保{(action == ADJUST_OCCUPY ? "占用" : "释放")}: 批复限额={officialLimit}, 上浮限额={upliftLimit}, 净占用 {netBefore} -> {netAfter}, 余额 {asisBalance} -> {tobeBalance}");

            try
            {
                string recordId = CreateLedger(accountRef, custName, GetCustomerOwner(customer), contractRef, orderRef,
                    stage, action, CREDIT_SINOSURE, upliftLimit, asisBalance, amountUSD, tobeBalance,
                    amountCNY, deliveryNo, settleId, settleNo, idempotencyKey);

                return Success(amountUSD, tobeBalance, recordId);
            }
            catch (Exception ex)
            {
                return Fail($"调整失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 中信保批复限额 USD：mcs_approvedquota 按客户汇总有效记录（quotastate=1 + 生效/失效区间）
        /// </summary>
        private decimal GetSinosureOfficialLimit(EntityReference accountRef)
        {
            var now = DateTime.UtcNow;
            var query = new QueryExpression("mcs_approvedquota")
            {
                ColumnSet = new ColumnSet("mcs_quotasum"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_buyername", ConditionOperator.Equal, accountRef.Id),
                        new ConditionExpression("mcs_quotastate", ConditionOperator.Equal, QUOTA_STATE_VALID)
                    }
                }
            };
            // 生效日期为空或已生效
            query.Criteria.AddFilter(new FilterExpression(LogicalOperator.Or)
            {
                Conditions =
                {
                    new ConditionExpression("mcs_effectdatestr", ConditionOperator.Null),
                    new ConditionExpression("mcs_effectdatestr", ConditionOperator.LessEqual, now)
                }
            });
            // 失效日期为空或未失效
            query.Criteria.AddFilter(new FilterExpression(LogicalOperator.Or)
            {
                Conditions =
                {
                    new ConditionExpression("mcs_lapsedatestr", ConditionOperator.Null),
                    new ConditionExpression("mcs_lapsedatestr", ConditionOperator.GreaterEqual, now)
                }
            });

            decimal sum = 0m;
            foreach (var entity in RetrieveAll(query))
            {
                sum += entity.GetAttributeValue<decimal?>("mcs_quotasum") ?? 0m;
            }
            _tracer.Trace($"信保批复限额汇总: 客户={accountRef.Id}, 批复限额={sum}");
            return sum;
        }

        /// <summary>
        /// 中信保净占用 USD：台账 credit_type=2 的 Σ占用 − Σ释放
        /// </summary>
        private decimal GetSinosureNetOccupied(EntityReference accountRef)
        {
            var query = new QueryExpression("mcs_fca_records")
            {
                ColumnSet = new ColumnSet("mcs_adjust", "mcs_adjustamt"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_accountid", ConditionOperator.Equal, accountRef.Id),
                        new ConditionExpression("mcs_credit_type", ConditionOperator.Equal, CREDIT_SINOSURE),
                        new ConditionExpression("mcs_adjust", ConditionOperator.In, ADJUST_OCCUPY, ADJUST_RELEASE)
                    }
                }
            };

            decimal occupied = 0m;
            decimal released = 0m;
            foreach (var entity in RetrieveAll(query))
            {
                int adjust = entity.GetAttributeValue<OptionSetValue>("mcs_adjust")?.Value ?? 0;
                decimal amount = GetMoney(entity, "mcs_adjustamt");
                if (adjust == ADJUST_OCCUPY) occupied += amount;
                else if (adjust == ADJUST_RELEASE) released += amount;
            }
            _tracer.Trace($"信保净占用: 客户={accountRef.Id}, Σ占用={occupied}, Σ释放={released}");
            return occupied - released;
        }

        #endregion

        #region 幂等键

        /// <summary>
        /// 幂等键：解款类=解款明细guid；发货类=发货单号+环节+动作；兜底=合同+订单+环节+动作+金额
        /// </summary>
        private string BuildIdempotencyKey(int stage, int action, decimal amountUSD,
            string contractCode, string orderCode, string deliveryNo, string settleId)
        {
            if (!string.IsNullOrWhiteSpace(settleId))
            {
                return $"S:{settleId.Trim()}";
            }
            if (!string.IsNullOrWhiteSpace(deliveryNo))
            {
                return $"D:{deliveryNo.Trim()}:{stage}:{action}";
            }
            return $"G:{(contractCode ?? string.Empty).Trim()}:{(orderCode ?? string.Empty).Trim()}:{stage}:{action}:{amountUSD}";
        }

        /// <summary>
        /// 按幂等键查台账（命中即重复调用）
        /// </summary>
        private Entity FindLedgerByIdempotencyKey(string idempotencyKey)
        {
            var query = new QueryExpression("mcs_fca_records")
            {
                ColumnSet = new ColumnSet("mcs_recordid", "mcs_adjustamt", "mcs_tobebalance"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_idempotency_key", ConditionOperator.Equal, idempotencyKey)
                    }
                },
                TopCount = 1
            };
            query.AddOrder("createdon", OrderType.Ascending);

            return _service.RetrieveMultiple(query).Entities.FirstOrDefault();
        }

        #endregion

        #region 台账/额度/解析基础方法

        /// <summary>
        /// 创建台账记录（含 816 新增字段），返回台账编号（mcs_recordid 由系统统一自动编号 Plugin 生成）
        /// </summary>
        private string CreateLedger(EntityReference accountRef, string custName, EntityReference owner,
            EntityReference contractRef, EntityReference orderRef, int stage, int action, int creditType,
            decimal grant, decimal asisBalance, decimal adjustAmt, decimal tobeBalance,
            decimal amountCNY, string deliveryNo, string settleId, string settleNo, string idempotencyKey)
        {
            var ledger = new Entity("mcs_fca_records");
            ledger["mcs_accountid"] = accountRef;
            ledger["mcs_custname"] = custName;
            if (owner != null)
            {
                ledger["ownerid"] = owner;
            }
            if (contractRef != null)
            {
                ledger["mcs_contractid"] = contractRef;
            }
            if (orderRef != null)
            {
                ledger["mcs_orderid"] = orderRef;
            }
            // 旧字段（语义不变）
            ledger["mcs_proccess"] = new OptionSetValue(stage);
            ledger["mcs_adjust"] = new OptionSetValue(action);
            ledger["mcs_sellergrant"] = new Money(grant);
            ledger["mcs_asisbalance"] = new Money(asisBalance);
            ledger["mcs_adjustamt"] = new Money(adjustAmt);
            ledger["mcs_tobebalance"] = new Money(tobeBalance);
            // 816 新增字段
            ledger["mcs_credit_type"] = new OptionSetValue(creditType);
            ledger["mcs_usebalance_cny"] = new Money(amountCNY);
            if (!string.IsNullOrWhiteSpace(deliveryNo))
            {
                ledger["mcs_delivery_no"] = deliveryNo.Trim();
            }
            if (!string.IsNullOrWhiteSpace(settleNo))
            {
                ledger["mcs_settle_no"] = settleNo.Trim();
            }
            if (!string.IsNullOrWhiteSpace(settleId))
            {
                ledger["mcs_settle_id"] = settleId.Trim();
            }
            ledger["mcs_idempotency_key"] = idempotencyKey;

            Guid ledgerId = _service.Create(ledger);
            _tracer.Trace($"已创建台账记录: {ledgerId}, key={idempotencyKey}");

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
                ColumnSet = new ColumnSet("mcs_name", "mcs_sapnumber", "mcs_countrycode", "ownerid"),
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

        /// <summary>
        /// 分页取全部记录（信保限额/净占用聚合用）
        /// </summary>
        private System.Collections.Generic.List<Entity> RetrieveAll(QueryExpression query)
        {
            var all = new System.Collections.Generic.List<Entity>();
            query.PageInfo = new PagingInfo { Count = 500, PageNumber = 1 };
            while (true)
            {
                var page = _service.RetrieveMultiple(query);
                all.AddRange(page.Entities);
                if (!page.MoreRecords)
                {
                    break;
                }
                query.PageInfo.PageNumber++;
                query.PageInfo.PagingCookie = page.PagingCookie;
            }
            return all;
        }

        private bool IsActive(Entity quota)
        {
            return quota.GetAttributeValue<OptionSetValue>("mcs_isactive")?.Value == IS_ACTIVE_YES;
        }

        private decimal GetMoney(Entity entity, string fieldName)
        {
            return entity.GetAttributeValue<Money>(fieldName)?.Value ?? 0m;
        }

        /// <summary>
        /// 读取客户主数据负责人（#1643），未设置时返回 null
        /// </summary>
        private EntityReference GetCustomerOwner(Entity customer)
        {
            return customer.GetAttributeValue<EntityReference>("ownerid");
        }

        private RecordCreditDetailResult Fail(string reason)
        {
            _tracer.Trace($"使用授信失败: {reason}");
            return new RecordCreditDetailResult { Success = false, FailReason = reason, UsedBalance = 0m };
        }

        private RecordCreditDetailResult Success(decimal usedBalance, decimal sellerBalance, string recordId)
        {
            return new RecordCreditDetailResult
            {
                Success = true,
                UsedBalance = usedBalance,
                SellerBalance = sellerBalance,
                RecordId = recordId ?? string.Empty
            };
        }

        #endregion
    }
}
