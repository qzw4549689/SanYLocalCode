using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace SanyD365.Plugins.FactoryCredit.Bpp.Services
{
    /// <summary>
    /// 额度调整申请审批通过后，更新厂端授信额度表
    /// </summary>
    public class QuotaActivationService
    {
        private readonly IOrganizationService _service;
        private readonly ITracingService _tracer;

        // mcs_fca_quota.mcs_isactive 选项集值：1-是
        private const int IS_ACTIVE_YES = 1;

        public QuotaActivationService(IOrganizationService service, ITracingService tracer)
        {
            _service = service;
            _tracer = tracer;
        }

        /// <summary>
        /// 审批通过后更新额度表。
        /// 核心不变式：授信额度 = 授信余额 + 占用金额（sellergrant = sellerbalance + usedsellerbalance）。
        /// 余额按 tobeGrant - 现有占用 计算，不再使用前端传入的 tobeBalance；占用字段不重置。
        /// 不予授信归零场景（tobeGrant=0）：占用保持不变，余额 = -占用（负余额保留敞口，后续回款释放可加回）。
        /// owner：新建额度记录的负责人（#1856，申请人），仅 Create 分支生效；Update 既有记录不动 owner。
        /// procRef：模型计算序列号 Lookup（#2025），非空时同步写入 mcs_fca_procid（视图列绑定的字段）。
        /// </summary>
        public void ActivateQuota(EntityReference accountRef, string custName, Money tobeGrant, Money tobeBalance, string doid, EntityReference owner, EntityReference procRef)
        {
            if (accountRef == null)
            {
                throw new InvalidPluginExecutionException("客户编码不能为空，无法更新额度表。");
            }

            if (tobeGrant == null)
            {
                throw new InvalidPluginExecutionException("调整后额度不能为空，无法更新额度表。");
            }

            Entity quota = GetOrCreateQuota(accountRef, custName);
            bool isNewQuota = quota.Id == Guid.Empty;
            decimal used = isNewQuota ? 0m : (quota.GetAttributeValue<Money>("mcs_usedsellerbalance")?.Value ?? 0m);
            decimal newBalance = tobeGrant.Value - used;

            _tracer.Trace($"开始更新额度表: accountId={accountRef.Id}, tobeGrant={tobeGrant.Value}, used={used}, newBalance={newBalance}（忽略前端 tobeBalance={(tobeBalance?.Value ?? 0)}）");

            quota["mcs_accountid"] = accountRef;
            quota["mcs_custname"] = custName;
            quota["mcs_sellergrant"] = tobeGrant;
            quota["mcs_sellerbalance"] = new Money(newBalance);
            if (isNewQuota)
            {
                quota["mcs_usedsellerbalance"] = new Money(0m);
                if (owner != null)
                {
                    quota["ownerid"] = owner;
                }
            }
            quota["mcs_isactive"] = new OptionSetValue(IS_ACTIVE_YES);
            quota["mcs_validfrom"] = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(doid))
            {
                quota["mcs_doid"] = doid;
            }

            // #2025 视图「模型计算序列号」列绑定的是 Lookup mcs_fca_procid，必须与文本 mcs_doid 同步写入，否则该列空白
            if (procRef != null)
            {
                quota["mcs_fca_procid"] = procRef;
            }

            if (quota.Id != Guid.Empty)
            {
                _service.Update(quota);
                _tracer.Trace($"已更新额度记录: {quota.Id}");
            }
            else
            {
                quota.Id = _service.Create(quota);
                _tracer.Trace($"已创建额度记录: {quota.Id}");
            }
        }

        private Entity GetOrCreateQuota(EntityReference accountRef, string custName)
        {
            QueryExpression query = new QueryExpression("mcs_fca_quota")
            {
                ColumnSet = new ColumnSet("mcs_fca_quotaid", "mcs_sellergrant", "mcs_sellerbalance", "mcs_usedsellerbalance"),
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

            EntityCollection result = _service.RetrieveMultiple(query);

            if (result.Entities.Count > 0)
            {
                _tracer.Trace("找到已有额度记录，执行更新");
                return result.Entities[0];
            }

            _tracer.Trace("未找到额度记录，创建新记录");
            Entity quota = new Entity("mcs_fca_quota");
            quota["mcs_accountid"] = accountRef;
            quota["mcs_custname"] = custName;
            return quota;
        }
    }
}
