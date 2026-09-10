using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using SanyD365.Plugins.FactoryCredit.Calculation.Services;
using System;
using System.Globalization;

namespace SanyD365.Plugins.FactoryCredit.Calculation
{
    /// <summary>
    /// 厂端授信模型计算表 - 模型计算 Plugin
    /// 触发时机：mcs_fca_proc Update PostOperation
    /// 业务规则：当计算状态从非"模型计算"变为 2（模型计算）时
    /// 0. 前置校验：必须存在生效且在有效期内的模型版本，否则中断计算
    /// 1. 计算客户分类并读取模型参数
    /// 2. 计算三因子并生成初始额度
    /// 3. 根据逾期情况调整额度
    /// 4. 执行不予授信校验并回写客户主数据
    /// 5. 生成计算日志
    /// 6. 将结果写回 mcs_fca_proc
    /// </summary>
    public class FcaProcCalculationPlugin : IPlugin
    {
        // 计算状态选项集值
        private const int STATUS_MODEL_CALC = 2;

        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = factory.CreateOrganizationService(context.UserId);
            ITracingService tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            // 系统上下文：仅用于查询模型版本(mcs_fca_mdlversion)和模型参数配置(mcs_fca_mdlconfig)
            // 禅道 #1854：当前用户只需具备 mcs_fca_proc 的增改查权限，模型版本/参数配置由系统(admin)代为查询，
            // 其余基础数据（客户主数据/在外货款/客户标签等）由角色正常赋权，仍走用户上下文
            IOrganizationService systemService = factory.CreateOrganizationService(null);

            tracer.Trace("FcaProcCalculationPlugin 开始执行");

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

            if (!target.Contains("mcs_status"))
            {
                tracer.Trace("状态字段未变更，跳过");
                return;
            }

            int newStatus = GetOptionSetValue(target, "mcs_status");
            tracer.Trace($"新状态: {newStatus}");

            if (newStatus != STATUS_MODEL_CALC)
            {
                tracer.Trace("新状态不是模型计算，跳过");
                return;
            }

            int oldStatus = 0;
            if (context.PreEntityImages.Contains("PreImage"))
            {
                Entity preImage = context.PreEntityImages["PreImage"];
                oldStatus = GetOptionSetValue(preImage, "mcs_status");
                tracer.Trace($"旧状态: {oldStatus}");
            }

            if (oldStatus == STATUS_MODEL_CALC)
            {
                tracer.Trace("原状态已是模型计算，不重复处理");
                return;
            }

            try
            {
                ProcessCalculation(service, systemService, tracer, target.Id);
            }
            catch (Exception ex)
            {
                tracer.Trace($"模型计算失败: {ex.Message}");
                throw new InvalidPluginExecutionException($"模型计算失败: {ex.Message}");
            }
        }

        private void ProcessCalculation(IOrganizationService service, IOrganizationService systemService, ITracingService tracer, Guid procId)
        {
            // 读取完整计算记录
            Entity proc = service.Retrieve("mcs_fca_proc", procId,
                new ColumnSet("mcs_accountid", "mcs_custname", "mcs_versionid"));

            EntityReference accountRef = proc.GetAttributeValue<EntityReference>("mcs_accountid");
            if (accountRef == null)
            {
                throw new InvalidPluginExecutionException("客户编码不能为空，无法执行模型计算。");
            }

            Guid masterDataId = accountRef.Id;
            tracer.Trace($"处理模型计算: procId={procId}, masterDataId={masterDataId}");

            // 0. 前置校验：必须存在生效且在有效期内的模型版本，否则禁止模型计算（#1854 系统身份查询）
            ModelVersionService versionService = new ModelVersionService(systemService, tracer);
            Entity version = versionService.GetActiveVersion();
            if (version == null)
            {
                throw new InvalidPluginExecutionException("未找到生效且处于有效期内的模型版本，请先维护【厂端授信模型版本】后再执行模型计算。");
            }

            // 解析对应的 Account ID
            CustomerAccountResolver resolver = new CustomerAccountResolver(service, tracer);
            Guid? accountIdNullable = resolver.ResolveAccountId(masterDataId);
            if (!accountIdNullable.HasValue)
            {
                throw new InvalidPluginExecutionException("未找到客户主数据对应的 Account 记录，无法执行模型计算。");
            }
            Guid accountId = accountIdNullable.Value;

            // 1. 读取客户主数据
            Entity customer = service.Retrieve("mcs_customermasterdata", masterDataId,
                new ColumnSet("mcs_accounttype", "mcs_accountcategory", "mcs_kacategory", "mcs_dealerrank",
                              "mcs_creditgrade", "mcs_creditscore", "mcs_creditvalid"));

            // 2. 计算客户分类（禅道#2189：改 #2147 聚合口径，系统身份查询关联客户记录，与信用评估同源）
            CustomerCategoryService categoryService = new CustomerCategoryService();
            CategoryResult category = categoryService.CalculateCategory(systemService, tracer, customer, masterDataId);
            tracer.Trace($"客户分类={category.BuyerGradeLabel}, 客户等级={category.CreditGradeLabel}");

            // 3. 读取模型参数（系数和聚合方法）（#1854 系统身份查询 mcs_fca_mdlconfig）
            ModelParameterService paramService = new ModelParameterService(systemService, tracer);
            ModelParameterInfo param = paramService.GetModelParameters(category.BuyerGradeValue, category.CreditGradeValue);
            if (!param.HasModelParams)
            {
                tracer.Trace("未找到有效模型参数，尝试使用 ALL 兜底参数");
                param = paramService.GetModelParameters(category.BuyerGradeValue, 6);
            }

            if (!param.HasModelParams)
            {
                throw new InvalidPluginExecutionException($"未找到客户分类 {category.BuyerGradeLabel} 的模型参数，无法计算。");
            }

            // 按客户分类读取历史基准额度（与客户等级无关）
            decimal historicalBenchmark = paramService.GetHistoricalBenchmark(category.BuyerGradeValue);
            param.CountryBenchmark = historicalBenchmark;

            // 参数回显：使用实际查询到的标签（如果查询到）
            if (string.IsNullOrEmpty(param.BuyerGradeLabel))
            {
                param.BuyerGradeLabel = category.BuyerGradeLabel;
            }
            if (string.IsNullOrEmpty(param.CreditGradeLabel))
            {
                param.CreditGradeLabel = category.CreditGradeLabel;
            }

            // 4. 三因子计算
            ThreeFactorCalculationService calcService = new ThreeFactorCalculationService(service, tracer);
            DateTime? versionDate = ResolveVersionDate(proc, systemService, tracer);
            ThreeFactorCalculationService.CalculationResult calcResult = calcService.Calculate(masterDataId, accountId, param, versionDate);

            // 5. 逾期调整
            OverdueAdjustmentService overdueService = new OverdueAdjustmentService(service, tracer);
            OverdueAdjustmentService.AdjustmentResult adjustmentResult = overdueService.Adjust(accountId, calcResult.InitialGrant);

            // 6. 不予授信校验
            CreditRejectCheckService rejectService = new CreditRejectCheckService(service, tracer);
            CreditRejectCheckService.RejectResult rejectResult = rejectService.CheckAndUpdate(masterDataId, accountId);

            // 如果不予授信触发，强制模型计算额度和调整额度都为 0
            decimal modelGrant = calcResult.InitialGrant;
            decimal finalGrant = adjustmentResult.AdjustedGrant;
            if (rejectResult.IsRejected)
            {
                modelGrant = 0m;
                finalGrant = 0m;
                tracer.Trace("不予授信触发，模型计算额度和调整额度都归0");
            }

            // 7. 选择模型版本（已在前置校验（步骤 0）中获取，此处直接取用）
            string versionCode = version.GetAttributeValue<string>("mcs_versionid");
            string modelDesc = version.GetAttributeValue<string>("mcs_modeldesc");

            // 8. 生成计算日志
            LogContext logContext = new LogContext
            {
                BuyerGrade = param.BuyerGradeLabel,
                CreditGrade = param.CreditGradeLabel,
                Adjust1 = param.Adjust1,
                Adjust2 = param.Adjust2,
                Adjust3 = param.Adjust3,
                AggFunc = param.AggFuncLabel,
                CountryBenchmark = param.CountryBenchmark,
                NetAssets = param.Adjust1 == 0m ? 0m : calcResult.FinancialAbility / param.Adjust1,
                AvgMonthlyPayment = param.Adjust2 == 0m ? 0m : calcResult.HistoricalPaymentAbility / param.Adjust2,
                FinancialAbility = calcResult.FinancialAbility,
                HistoricalPaymentAbility = calcResult.HistoricalPaymentAbility,
                HistoricalBenchmarkAdjusted = calcResult.HistoricalBenchmarkAdjusted,
                InitialGrant = calcResult.InitialGrant,
                UsedFallback = calcResult.UsedFallback,
                FallbackReason = calcResult.FallbackReason,
                MaxOverdueDays = adjustmentResult.MaxOverdueDays,
                OverdueRatio = adjustmentResult.OverdueRatio,
                AdjustedGrant = finalGrant,
                AdjustmentDescription = adjustmentResult.AdjustmentDescription,
                IsRejected = rejectResult.IsRejected,
                RejectReason = rejectResult.Reason
            };

            CalculationLogService logService = new CalculationLogService();
            string log = logService.BuildLog(logContext);
            tracer.Trace("计算日志:\n" + log);

            // 9. 回写 mcs_fca_proc
            Entity update = new Entity("mcs_fca_proc", procId);
            update["mcs_modelgrant"] = new Money(modelGrant);
            update["mcs_initigrant"] = new Money(finalGrant);
            update["mcs_validfrom"] = DateTime.UtcNow;
            update["mcs_versionid"] = new EntityReference("mcs_fca_mdlversion", version.Id);
            if (!string.IsNullOrEmpty(modelDesc))
            {
                update["mcs_modeldesc"] = modelDesc;
            }

            // 若系统触发不予授信场景 3/4，回写多选字段（覆盖人工选择）
            if (rejectResult.IsRejected && rejectResult.TriggeredScenarios != null && rejectResult.TriggeredScenarios.Length > 0)
            {
                var rejectOptions = new OptionSetValueCollection();
                foreach (var scenario in rejectResult.TriggeredScenarios)
                {
                    rejectOptions.Add(new OptionSetValue(scenario));
                }
                update["mcs_creditreject"] = rejectOptions;
            }

            update["mcs_doproc"] = log;

            service.Update(update);
            tracer.Trace("已回写模型计算结果到 mcs_fca_proc");
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

        /// <summary>
        /// 解析 mcs_fca_proc 关联的模型版本日期，用于确定历史回款 12 个月范围
        /// </summary>
        private DateTime? ResolveVersionDate(Entity proc, IOrganizationService service, ITracingService tracer)
        {
            try
            {
                EntityReference versionRef = proc.GetAttributeValue<EntityReference>("mcs_versionid");
                if (versionRef != null)
                {
                    Entity version = service.Retrieve("mcs_fca_mdlversion", versionRef.Id, new ColumnSet("mcs_versionid"));
                    string versionCode = version.GetAttributeValue<string>("mcs_versionid");
                    if (!string.IsNullOrWhiteSpace(versionCode) && versionCode.StartsWith("V", StringComparison.OrdinalIgnoreCase))
                    {
                        if (DateTime.TryParseExact(versionCode.Substring(1), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
                        {
                            tracer.Trace($"解析到模型版本日期: {dt:yyyy-MM-dd}");
                            return dt;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                tracer.Trace($"解析模型版本日期失败: {ex.Message}");
            }

            tracer.Trace("未解析到模型版本日期，使用当前时间作为历史回款范围基准");
            return null;
        }
    }
}
