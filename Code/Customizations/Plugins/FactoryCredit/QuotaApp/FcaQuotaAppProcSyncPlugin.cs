using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace SanyD365.Plugins.FactoryCredit.QuotaApp
{
    /// <summary>
    /// 厂端授信额度生效申请 - 模型计算/额度数据带出 Plugin
    /// 触发时机：mcs_fca_quotaapp Create / Update（仅 mcs_doid 变更）PreOperation
    /// 禅道 #1855：模型计算（mcs_fca_proc）与厂端授信额度（mcs_fca_quota）为基础数据，
    /// 由系统(admin)身份代为查询，当前用户只需具备 mcs_fca_quotaapp 的增改查权限，
    /// 无需模型计算/额度表的读取权限。前端不再实时带出，统一在保存时由本插件回填：
    /// 1. Create：已选序列号 → 校验客户一致并回填模型计算额度；
    ///    未选序列号 → 查客户最新生效模型计算，回填序列号 + 模型计算额度
    /// 2. Create：回填当前厂端授信额度/余额（mcs_fca_quota 最新生效记录）
    /// 3. 「安全交易基线额度调整为」为空时默认 = 基准值（有模型计算取模型计算额度，否则取当前额度）；
    ///    并重算调整后余额 = 调整为 - 当前额度 + 当前余额
    /// 4. 服务端校验：当前额度为 0 且无序列号 → 拦截；发生调整（调整为 ≠ 基准值）且调整原因为空 → 拦截（禅道 #1637 服务端兜底）
    /// </summary>
    public class FcaQuotaAppProcSyncPlugin : IPlugin
    {
        // mcs_fca_proc.mcs_status 选项集值：3 - 生效启用
        private const int PROC_STATUS_ACTIVE = 3;

        // mcs_fca_quota.mcs_isactive 选项集值：1 - 是
        private const int IS_ACTIVE_YES = 1;

        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = factory.CreateOrganizationService(context.UserId);
            ITracingService tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            // 系统上下文：模型计算/额度基础数据由系统(admin)代为查询（禅道 #1855）
            IOrganizationService systemService = factory.CreateOrganizationService(null);

            // service（用户上下文）当前未直接使用：本插件读写均落在 Target/PreImage 与基础数据（系统身份）上，
            // 保留该行以保持同步指南 11.1/11.2 的入口固定 4 行写法（sync-plugin-to-remote.py PluginBase 转换断言要求）

            if (context.Stage != 20)
            {
                return;
            }

            if (context.MessageName != "Create" && context.MessageName != "Update")
            {
                return;
            }

            if (!context.InputParameters.Contains("Target") || !(context.InputParameters["Target"] is Entity))
            {
                return;
            }

            Entity target = (Entity)context.InputParameters["Target"];

            if (target.LogicalName != "mcs_fca_quotaapp")
            {
                tracer.Trace($"实体不匹配: {target.LogicalName}");
                return;
            }

            try
            {
                if (context.MessageName == "Create")
                {
                    ProcessCreate(systemService, tracer, target);
                }
                else
                {
                    // Update：仅序列号变更时处理（Step Filter=mcs_doid，双保险）
                    if (!target.Contains("mcs_doid"))
                    {
                        return;
                    }
                    ProcessUpdate(systemService, tracer, context, target);
                }
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                tracer.Trace($"FcaQuotaAppProcSyncPlugin 异常: {ex}");
                throw new InvalidPluginExecutionException($"额度申请数据带出失败: {ex.Message}");
            }
        }

        /// <summary>
        /// Create：回填序列号/模型计算额度/当前额度余额，默认值与服务端校验
        /// </summary>
        private void ProcessCreate(IOrganizationService systemService, ITracingService tracer, Entity target)
        {
            EntityReference accountRef = target.GetAttributeValue<EntityReference>("mcs_accountid");
            if (accountRef == null)
            {
                // 客户编码必填由前端/平台校验拦截，后端不重复处理
                return;
            }

            decimal? initigrant = null;
            EntityReference doidRef = target.GetAttributeValue<EntityReference>("mcs_doid");

            if (doidRef == null)
            {
                // 未选序列号：查客户最新生效模型计算，回填序列号 + 模型计算额度
                Entity proc = QueryLatestEffectiveProc(systemService, tracer, accountRef.Id);
                if (proc != null)
                {
                    Money grant = proc.GetAttributeValue<Money>("mcs_initigrant") ?? new Money(0m);
                    target["mcs_doid"] = proc.ToEntityReference();
                    target["mcs_initigrant"] = grant;
                    initigrant = grant.Value;
                    tracer.Trace($"已回填最新生效模型计算: {proc.GetAttributeValue<string>("mcs_doid")}({proc.Id})，模型计算额度={grant.Value}");
                }
                else
                {
                    tracer.Trace("客户暂无生效模型计算记录，序列号保持为空");
                }
            }
            else
            {
                // 已选序列号：校验客户一致 + 回填模型计算额度
                initigrant = FillInitigrantFromProc(systemService, tracer, target, doidRef, accountRef);
            }

            // 回填当前厂端授信额度/余额（系统身份查 mcs_fca_quota）
            GetCurrentQuota(systemService, tracer, accountRef.Id, out decimal sellerGrant, out decimal sellerBalance);
            target["mcs_sellergrant"] = new Money(sellerGrant);
            target["mcs_sellerbalance"] = new Money(sellerBalance);

            // 校验：当前额度为 0 且仍无序列号 → 必须选择模型计算序列号（原前端校验挪服务端）
            EntityReference finalDoid = target.GetAttributeValue<EntityReference>("mcs_doid");
            if (sellerGrant == 0m && finalDoid == null)
            {
                throw new InvalidPluginExecutionException("当前安全交易基线额度为 0，必须选择模型计算序列号。");
            }

            // 「调整为」为空时默认 = 基准值（有模型计算取模型计算额度，否则取当前额度）
            decimal benchmark = finalDoid != null ? (initigrant ?? 0m) : sellerGrant;
            Money tobeGrant = target.GetAttributeValue<Money>("mcs_tobegrant");
            if (tobeGrant == null)
            {
                tobeGrant = new Money(benchmark);
                target["mcs_tobegrant"] = tobeGrant;
                tracer.Trace($"「调整为」为空，默认取基准值: {benchmark}");
            }

            if (tobeGrant.Value < 0m)
            {
                throw new InvalidPluginExecutionException("安全交易基线额度调整为不能小于 0。");
            }

            // 发生调整（调整为 ≠ 基准值）且调整原因为空 → 拦截（禅道 #1637 服务端兜底）
            string reason = target.GetAttributeValue<string>("mcs_reason");
            if (tobeGrant.Value != benchmark && string.IsNullOrWhiteSpace(reason))
            {
                throw new InvalidPluginExecutionException("已调整安全交易基线额度，请填写调整原因。");
            }

            // 重算调整后余额 = 调整为 - 当前额度 + 当前余额
            target["mcs_tobebalance"] = new Money(tobeGrant.Value - sellerGrant + sellerBalance);
            tracer.Trace($"回填完成: 额度={sellerGrant}, 余额={sellerBalance}, 调整为={tobeGrant.Value}, 调整后余额={tobeGrant.Value - sellerGrant + sellerBalance}");
        }

        /// <summary>
        /// Update（仅序列号变更）：校验客户一致 + 回填模型计算额度 + 重算调整后余额
        /// </summary>
        private void ProcessUpdate(IOrganizationService systemService, ITracingService tracer, IPluginExecutionContext context, Entity target)
        {
            EntityReference doidRef = target.GetAttributeValue<EntityReference>("mcs_doid");
            Entity preImage = context.PreEntityImages.Contains("PreImage") ? context.PreEntityImages["PreImage"] : null;
            EntityReference accountRef = target.GetAttributeValue<EntityReference>("mcs_accountid")
                ?? preImage?.GetAttributeValue<EntityReference>("mcs_accountid");

            if (doidRef != null)
            {
                FillInitigrantFromProc(systemService, tracer, target, doidRef, accountRef);
            }
            else
            {
                // 序列号被清空：模型计算额度归零（与原前端 onDoidChanged 口径一致）
                target["mcs_initigrant"] = new Money(0m);
                tracer.Trace("序列号已清空，模型计算额度归零");
            }

            // 重算调整后余额（当前额度/余额/调整为取 PreImage 存量值，不重复查询额度表）
            if (preImage != null)
            {
                decimal sellerGrant = preImage.GetAttributeValue<Money>("mcs_sellergrant")?.Value ?? 0m;
                decimal sellerBalance = preImage.GetAttributeValue<Money>("mcs_sellerbalance")?.Value ?? 0m;
                Money tobeGrant = target.GetAttributeValue<Money>("mcs_tobegrant")
                    ?? preImage.GetAttributeValue<Money>("mcs_tobegrant");
                if (tobeGrant != null)
                {
                    target["mcs_tobebalance"] = new Money(tobeGrant.Value - sellerGrant + sellerBalance);
                    tracer.Trace($"序列号变更，重算调整后余额={tobeGrant.Value - sellerGrant + sellerBalance}");
                }
            }
        }

        /// <summary>
        /// 按序列号查模型计算记录：校验客户一致并回填模型计算额度，返回模型计算额度值
        /// </summary>
        private decimal FillInitigrantFromProc(IOrganizationService systemService, ITracingService tracer,
            Entity target, EntityReference doidRef, EntityReference accountRef)
        {
            Entity proc = systemService.Retrieve("mcs_fca_proc", doidRef.Id,
                new ColumnSet("mcs_doid", "mcs_initigrant", "mcs_accountid"));

            EntityReference procAccount = proc.GetAttributeValue<EntityReference>("mcs_accountid");
            if (accountRef != null && procAccount != null && procAccount.Id != accountRef.Id)
            {
                throw new InvalidPluginExecutionException("所选模型计算序列号的客户与当前申请单客户不一致，请重新选择。");
            }

            Money grant = proc.GetAttributeValue<Money>("mcs_initigrant") ?? new Money(0m);
            target["mcs_initigrant"] = grant;
            tracer.Trace($"已回填模型计算额度: {grant.Value}（序列号 {proc.GetAttributeValue<string>("mcs_doid")}）");
            return grant.Value;
        }

        /// <summary>
        /// 查询客户最新生效的模型计算记录（status=3 生效启用；mcs_active=是或空视同有效；createdon 最新一条）
        /// 与前端 retrieveLatestEffectiveProc 原口径一致（禅道 #1644 有效状态过滤）
        /// </summary>
        private Entity QueryLatestEffectiveProc(IOrganizationService systemService, ITracingService tracer, Guid accountId)
        {
            QueryExpression query = new QueryExpression("mcs_fca_proc")
            {
                ColumnSet = new ColumnSet("mcs_doid", "mcs_initigrant"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_accountid", ConditionOperator.Equal, accountId),
                        new ConditionExpression("mcs_status", ConditionOperator.Equal, PROC_STATUS_ACTIVE)
                    },
                    Filters =
                    {
                        new FilterExpression(LogicalOperator.Or)
                        {
                            Conditions =
                            {
                                new ConditionExpression("mcs_active", ConditionOperator.Equal, true),
                                new ConditionExpression("mcs_active", ConditionOperator.Null)
                            }
                        }
                    }
                },
                TopCount = 1
            };
            query.AddOrder("createdon", OrderType.Descending);

            EntityCollection result = systemService.RetrieveMultiple(query);
            tracer.Trace($"最新生效模型计算查询: 命中 {result.Entities.Count} 条");
            return result.Entities.Count > 0 ? result.Entities[0] : null;
        }

        /// <summary>
        /// 读取客户当前生效的厂端授信额度和余额（mcs_isactive=1，createdon 最新一条）；无记录时返回 0
        /// 与 FcaProcActivationPlugin.GetCurrentQuota 口径一致
        /// </summary>
        private void GetCurrentQuota(IOrganizationService systemService, ITracingService tracer,
            Guid accountId, out decimal sellerGrant, out decimal sellerBalance)
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
                        new ConditionExpression("mcs_accountid", ConditionOperator.Equal, accountId),
                        new ConditionExpression("mcs_isactive", ConditionOperator.Equal, IS_ACTIVE_YES)
                    }
                },
                TopCount = 1
            };
            query.AddOrder("createdon", OrderType.Descending);

            EntityCollection result = systemService.RetrieveMultiple(query);
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
    }
}
