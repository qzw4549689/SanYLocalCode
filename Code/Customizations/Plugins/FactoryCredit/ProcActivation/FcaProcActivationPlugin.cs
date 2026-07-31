using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace SanyD365.Plugins.FactoryCredit
{
    /// <summary>
    /// 厂端授信模型计算表 - 生效启用 Plugin
    /// 触发时机：mcs_fca_proc Update PostOperation
    /// 业务规则（2026-07 需求变更）：当计算状态从非"生效启用"变为 3（生效启用）时，
    /// 不再直接写入 mcs_fca_quota（厂端授信额度表），而是自动创建 mcs_fca_quotaapp（额度生效申请单，
    /// 审批状态=申请），由业务在额度生效申请页面检查后人工提交 BPP 审批；审批通过后由
    /// FcaQuotaAppBppCallbackPlugin → QuotaActivationService 回写额度表并生效。
    /// </summary>
    public class FcaProcActivationPlugin : IPlugin
    {
        // 计算状态选项集值
        private const int STATUS_ACTIVE = 3;

        // mcs_fca_quotaapp.mcs_bppstatus 选项集值：1 - 申请
        private const int BPP_STATUS_APPLY = 1;

        // mcs_fca_quota.mcs_isactive 选项集值
        private const int IS_ACTIVE_YES = 1;

        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = factory.CreateOrganizationService(context.UserId);
            ITracingService tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracer.Trace("FcaProcActivationPlugin 开始执行");

            if (context.MessageName != "Update" || context.Stage != 40)
            {
                tracer.Trace("非 Update PostOperation 事件，跳过");
                return;
            }

            if (!context.InputParameters.Contains("Target") || !(context.InputParameters["Target"] is Entity))
            {
                tracer.Trace("未找到 Target 实体");
                return;
            }

            Entity target = (Entity)context.InputParameters["Target"];

            if (target.LogicalName != "mcs_fca_proc")
            {
                tracer.Trace($"实体不匹配: {target.LogicalName}");
                return;
            }

            // 只有状态字段变更时才需要处理
            if (!target.Contains("mcs_status"))
            {
                tracer.Trace("状态字段未变更，跳过");
                return;
            }

            int newStatus = GetOptionSetValue(target, "mcs_status");
            tracer.Trace($"新状态: {newStatus}");

            if (newStatus != STATUS_ACTIVE)
            {
                tracer.Trace("新状态不是生效启用，跳过");
                return;
            }

            // 获取旧状态，避免重复触发
            int oldStatus = 0;
            if (context.PreEntityImages.Contains("PreImage"))
            {
                Entity preImage = context.PreEntityImages["PreImage"];
                oldStatus = GetOptionSetValue(preImage, "mcs_status");
                tracer.Trace($"旧状态: {oldStatus}");
            }

            if (oldStatus == STATUS_ACTIVE)
            {
                tracer.Trace("原状态已是生效启用，不重复处理");
                return;
            }

            try
            {
                ProcessActivation(service, tracer, target.Id);
            }
            catch (Exception ex)
            {
                tracer.Trace($"生效启用处理失败: {ex.Message}");
                throw new InvalidPluginExecutionException($"生效启用处理失败: {ex.Message}");
            }
        }

        private void ProcessActivation(IOrganizationService service, ITracingService tracer, Guid procId)
        {
            // 读取完整计算记录（含组织字段，一并带出到申请单）
            Entity proc = service.Retrieve("mcs_fca_proc", procId,
                new ColumnSet("mcs_accountid", "mcs_initigrant", "mcs_orgid", "mcs_orgname", "mcs_buid", "mcs_buname"));

            EntityReference accountRef = proc.GetAttributeValue<EntityReference>("mcs_accountid");
            Money initGrant = proc.GetAttributeValue<Money>("mcs_initigrant");

            // 客户编码需从关联 Account 的 SAP 客户编号读取，避免取到客户名称或关系流水号
            string customerCode = GetCustomerCode(service, tracer, accountRef);

            if (accountRef == null)
            {
                throw new InvalidPluginExecutionException("客户编码不能为空，无法生效启用。");
            }

            if (initGrant == null || initGrant.Value <= 0)
            {
                throw new InvalidPluginExecutionException("调整模型额度必须大于 0，无法生效启用。");
            }

            tracer.Trace($"处理生效启用: procId={procId}, accountId={accountRef.Id}, initGrant={initGrant.Value}, customerCode={customerCode}");

            // 防重复：同一模型计算序列号已存在在途申请单（申请/审批中）时不再重复创建
            if (HasPendingQuotaApp(service, tracer, procId))
            {
                tracer.Trace("已存在该模型计算序列号的在途额度生效申请单（申请/审批中），跳过自动创建");
                return;
            }

            // 读取客户当前生效额度（无额度记录时按 0 处理）
            GetCurrentQuota(service, tracer, accountRef, out decimal sellerGrant, out decimal sellerBalance);

            // 需求公式：调整后厂端授信余额 = 厂端授信额度调整为 - 厂端授信额度 + 厂端授信余额
            decimal tobeBalance = initGrant.Value - sellerGrant + sellerBalance;

            // 自动创建额度生效申请单（审批状态=申请，由业务在页面检查后人工提交 BPP）
            Entity quotaApp = new Entity("mcs_fca_quotaapp");
            quotaApp["mcs_accountid"] = accountRef;
            quotaApp["mcs_custname"] = customerCode;
            quotaApp["mcs_doid"] = new EntityReference("mcs_fca_proc", procId);
            quotaApp["mcs_initigrant"] = initGrant;
            quotaApp["mcs_sellergrant"] = new Money(sellerGrant);
            quotaApp["mcs_sellerbalance"] = new Money(sellerBalance);
            quotaApp["mcs_tobegrant"] = initGrant;
            quotaApp["mcs_tobebalance"] = new Money(tobeBalance);
            quotaApp["mcs_quotasum"] = new Money(0m);
            quotaApp["mcs_quotabalance"] = new Money(0m);
            quotaApp["mcs_bppstatus"] = new OptionSetValue(BPP_STATUS_APPLY);
            // mcs_reason（调整原因）为平台必填字段，自动创建时填入默认说明，业务可在页面修改
            quotaApp["mcs_reason"] = "厂端授信模型计算生效启用自动生成，请确认后提交审批。";
            CopyOrgFields(proc, quotaApp);

            Guid quotaAppId = service.Create(quotaApp);
            tracer.Trace($"已自动创建额度生效申请单: {quotaAppId}（待人工提交 BPP 审批）");
        }

        /// <summary>
        /// 查询是否已存在同一模型计算序列号的在途额度生效申请单（审批状态=申请/审批中）
        /// </summary>
        private bool HasPendingQuotaApp(IOrganizationService service, ITracingService tracer, Guid procId)
        {
            QueryExpression query = new QueryExpression("mcs_fca_quotaapp")
            {
                ColumnSet = new ColumnSet("mcs_fca_quotaappid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_doid", ConditionOperator.Equal, procId),
                        new ConditionExpression("mcs_bppstatus", ConditionOperator.In, new object[] { BPP_STATUS_APPLY, 2 })
                    }
                },
                TopCount = 1
            };

            EntityCollection result = service.RetrieveMultiple(query);
            tracer.Trace($"在途申请单检查: 命中 {result.Entities.Count} 条");
            return result.Entities.Count > 0;
        }

        /// <summary>
        /// 读取客户当前生效的厂端授信额度和余额（mcs_isactive=1，最新一条）；无记录时返回 0
        /// </summary>
        private void GetCurrentQuota(IOrganizationService service, ITracingService tracer,
            EntityReference accountRef, out decimal sellerGrant, out decimal sellerBalance)
        {
            sellerGrant = 0m;
            sellerBalance = 0m;

            QueryExpression query = new QueryExpression("mcs_fca_quota")
            {
                ColumnSet = new ColumnSet("mcs_sellergrant", "mcs_sellerbalance"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_accountid", ConditionOperator.Equal, accountRef.Id),
                        new ConditionExpression("mcs_isactive", ConditionOperator.Equal, IS_ACTIVE_YES)
                    }
                },
                TopCount = 1
            };
            query.AddOrder("createdon", OrderType.Descending);

            EntityCollection result = service.RetrieveMultiple(query);
            if (result.Entities.Count > 0)
            {
                Entity quota = result.Entities[0];
                sellerGrant = quota.GetAttributeValue<Money>("mcs_sellergrant")?.Value ?? 0m;
                sellerBalance = quota.GetAttributeValue<Money>("mcs_sellerbalance")?.Value ?? 0m;
                tracer.Trace($"读取到当前生效额度: grant={sellerGrant}, balance={sellerBalance}");
            }
            else
            {
                tracer.Trace("客户暂无生效额度记录，额度/余额按 0 处理");
            }
        }

        /// <summary>
        /// 将模型计算记录上的组织字段带出到申请单（proc 与 quotaapp 字段同名）
        /// </summary>
        private void CopyOrgFields(Entity proc, Entity quotaApp)
        {
            string[] orgFields = { "mcs_orgid", "mcs_orgname", "mcs_buid", "mcs_buname" };
            foreach (string field in orgFields)
            {
                string value = proc.GetAttributeValue<string>(field);
                if (!string.IsNullOrEmpty(value))
                {
                    quotaApp[field] = value;
                }
            }
        }

        /// <summary>
        /// 从客户主数据读取 SAP 客户编号（真正的客户编号），避免取到客户名称或关系流水号。
        /// mcs_accountid 关联的是 mcs_customermasterdata，因此直接查客户主数据实体。
        /// </summary>
        public string GetCustomerCode(IOrganizationService service, ITracingService tracer, EntityReference accountRef)
        {
            if (accountRef == null)
            {
                return string.Empty;
            }

            Entity masterData = service.Retrieve("mcs_customermasterdata", accountRef.Id, new ColumnSet("mcs_sapnumber"));
            string sapNumber = masterData.GetAttributeValue<string>("mcs_sapnumber");
            tracer.Trace($"读取到客户编号(SAP): {sapNumber}");
            return sapNumber ?? string.Empty;
        }

        private int GetOptionSetValue(Entity entity, string fieldName)
        {
            if (!entity.Contains(fieldName))
            {
                return 0;
            }

            OptionSetValue value = entity.GetAttributeValue<OptionSetValue>(fieldName);
            return value?.Value ?? 0;
        }
    }
}
