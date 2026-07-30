using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace SanyD365.Plugins.FactoryCredit
{
    /// <summary>
    /// 厂端授信模型计算表 - 生效启用回写 Plugin
    /// 触发时机：mcs_fca_proc Update PostOperation
    /// 业务规则：当计算状态从非"生效启用"变为 3（生效启用）时
    /// 1. 在 mcs_fca_quota（厂端授信额度表）中创建或更新客户额度记录
    /// 2. 在 mcs_fca_records（厂端授信额度动态调整管理台账表）中生成一条初始化台账
    /// </summary>
    public class FcaProcActivationPlugin : IPlugin
    {
        // 计算状态选项集值
        private const int STATUS_ACTIVE = 3;

        // mcs_fca_quota.mcs_isactive 选项集值
        private const int IS_ACTIVE_YES = 1;

        // mcs_fca_records.mcs_proccess 选项集值：环节1 - 厂端授信模型计算
        private const int PROCESS_MODEL_CALC = 1;

        // mcs_fca_records.mcs_adjust 选项集值：1 - 初始化
        private const int ADJUST_INIT = 1;

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
                tracer.Trace($"生效启用回写失败: {ex.Message}");
                throw new InvalidPluginExecutionException($"生效启用回写失败: {ex.Message}");
            }
        }

        private void ProcessActivation(IOrganizationService service, ITracingService tracer, Guid procId)
        {
            // 读取完整计算记录
            Entity proc = service.Retrieve("mcs_fca_proc", procId,
                new ColumnSet("mcs_accountid", "mcs_initigrant", "mcs_validfrom", "mcs_doid"));

            EntityReference accountRef = proc.GetAttributeValue<EntityReference>("mcs_accountid");
            Money initGrant = proc.GetAttributeValue<Money>("mcs_initigrant");
            DateTime? validFrom = proc.GetAttributeValue<DateTime?>("mcs_validfrom");
            string doid = proc.GetAttributeValue<string>("mcs_doid");

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

            // 1. 回写/创建厂端授信额度表
            // 核心不变式：授信额度 = 授信余额 + 占用金额（sellergrant = sellerbalance + usedsellerbalance）
            Entity quota = GetOrCreateQuota(service, tracer, accountRef, customerCode);
            bool isNewQuota = quota.Id == Guid.Empty;
            // 决策点A：更新已有额度时占用不清零，余额 = 新额度 - 现有占用
            decimal usedAmount = isNewQuota ? 0m : (quota.GetAttributeValue<Money>("mcs_usedsellerbalance")?.Value ?? 0m);
            decimal oldBalance = isNewQuota ? 0m : (quota.GetAttributeValue<Money>("mcs_sellerbalance")?.Value ?? 0m);
            decimal newBalance = initGrant.Value - usedAmount;
            tracer.Trace($"额度记录准备更新/创建: quotaId={(isNewQuota ? "(新记录)" : quota.Id.ToString())}, custname={quota.GetAttributeValue<string>("mcs_custname")}, used={usedAmount}, oldBalance={oldBalance}, newBalance={newBalance}");
            quota["mcs_sellergrant"] = initGrant;
            quota["mcs_sellerbalance"] = new Money(newBalance);
            if (isNewQuota)
            {
                quota["mcs_usedsellerbalance"] = new Money(0m);
            }
            quota["mcs_isactive"] = new OptionSetValue(IS_ACTIVE_YES);
            quota["mcs_validfrom"] = validFrom ?? DateTime.UtcNow;
            quota["mcs_doid"] = doid;
            quota["mcs_fca_procid"] = new EntityReference("mcs_fca_proc", procId);

            if (!isNewQuota)
            {
                service.Update(quota);
                tracer.Trace($"已更新额度记录: {quota.Id}");
            }
            else
            {
                quota.Id = service.Create(quota);
                tracer.Trace($"已创建额度记录: {quota.Id}");
            }

            // 2. 生成台账记录（mcs_recordid 由通用自动编号服务生成）
            // 决策点B：初始化台账调整金额=0（PRD口径），调整前余额取额度记录原值
            Entity ledger = new Entity("mcs_fca_records");
            ledger["mcs_accountid"] = accountRef;
            ledger["mcs_custname"] = customerCode;
            ledger["mcs_proccess"] = new OptionSetValue(PROCESS_MODEL_CALC);
            ledger["mcs_adjust"] = new OptionSetValue(ADJUST_INIT);
            ledger["mcs_sellergrant"] = initGrant;
            ledger["mcs_asisbalance"] = new Money(oldBalance);
            ledger["mcs_tobebalance"] = new Money(newBalance);
            ledger["mcs_adjustamt"] = new Money(0m);

            Guid ledgerId = service.Create(ledger);
            tracer.Trace($"已创建台账记录: {ledgerId}");
        }

        private Entity GetOrCreateQuota(IOrganizationService service, ITracingService tracer,
            EntityReference accountRef, string customerCode)
        {
            QueryExpression query = new QueryExpression("mcs_fca_quota")
            {
                ColumnSet = new ColumnSet("mcs_fca_quotaid", "mcs_sellerbalance", "mcs_usedsellerbalance"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_accountid", ConditionOperator.Equal, accountRef.Id)
                    }
                },
                TopCount = 1
            };

            EntityCollection result = service.RetrieveMultiple(query);

            if (result.Entities.Count > 0)
            {
                tracer.Trace("找到已有额度记录，执行更新");
                return result.Entities[0];
            }

            tracer.Trace("未找到额度记录，创建新记录");
            Entity quota = new Entity("mcs_fca_quota");
            quota["mcs_accountid"] = accountRef;
            quota["mcs_custname"] = customerCode;
            return quota;
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
