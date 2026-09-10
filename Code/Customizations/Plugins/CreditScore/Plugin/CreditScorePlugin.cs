using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using SanyD365.Plugins.CreditScore.Calculator;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SanyD365.Plugins.CreditScore.Plugin
{
    /// <summary>
    /// 信用分计算Plugin
    /// 触发时机：信用评估记录Update后，状态变为13(信用分计算)时
    /// 功能：
    /// 1. 根据评分卡配置计算信用分
    /// 2. 更新信用评估记录的信用分
    /// 3. 更新客户信用标签的得分值
    /// 4. 更新状态为14(审核申请)
    /// </summary>
    public class CreditScoreCalculationPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = factory.CreateOrganizationService(context.UserId);
            ITracingService tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            // 系统上下文：计算所需的关联数据读取（account/客户主数据/销售订单/评分卡/评分项目/客户标签）
            // 不依赖操作员的记录级权限，避免事业部风控计算非本部门客户评估单时被安全模型拦截
            IOrganizationService systemService = factory.CreateOrganizationService(null);

            tracer.Trace("===== CreditScoreCalculationPlugin 开始执行 =====");

            // 严格校验：只处理Update后事件
            if (context.MessageName != "Update" || context.Stage != 40)
            {
                tracer.Trace("非Update后事件，跳过");
                return;
            }

            // 获取Target实体
            if (!context.InputParameters.Contains("Target") || !(context.InputParameters["Target"] is Entity))
            {
                tracer.Trace("未找到Target实体");
                return;
            }

            Entity target = (Entity)context.InputParameters["Target"];

            if (target.LogicalName != "mcs_credit_record")
            {
                tracer.Trace($"实体不匹配: {target.LogicalName}");
                return;
            }

            // 检查状态是否变为13(信用分计算) - 选项集实际值
            if (!target.Contains("mcs_status"))
            {
                tracer.Trace("状态未变更，跳过");
                return;
            }

            int status = target.GetAttributeValue<OptionSetValue>("mcs_status")?.Value ?? 0;
            if (status != 13)
            {
                tracer.Trace($"状态不是13(信用分计算)，当前状态={status}，跳过");
                return;
            }

            tracer.Trace("状态=13，开始信用分计算");

            try
            {
                // 强制校验：进入信用分计算前，所有标签必须已补录完成
                ValidateTagsCompleted(service, tracer, target.Id);

                // 获取完整记录信息
                Entity creditRecord = service.Retrieve("mcs_credit_record", target.Id,
                    new ColumnSet("mcs_scoreid", "mcs_accountid"));

                string scoreId = creditRecord.GetAttributeValue<string>("mcs_scoreid");

                // 获取评分卡类型（实时从Account查询客户属性匹配）
                // 读取 account/客户主数据/销售订单使用系统上下文，不依赖操作员对客户的记录级权限
                int categoryId = GetCategoryId(systemService, tracer, creditRecord);
                tracer.Trace($"评分卡类型: {categoryId}");

                // 计算信用分（评分卡/评分项目/客户标签读取走系统上下文，标签得分回写保留用户上下文）
                var calculator = new ScoreCalculator(systemService, service, tracer);
                int totalScore = calculator.CalculateScore(target.Id, categoryId);

                tracer.Trace($"信用分计算结果: {totalScore}");

                // 更新信用评估记录
                var updateRecord = new Entity("mcs_credit_record")
                {
                    Id = target.Id
                };
                updateRecord["mcs_creditscore"] = (decimal)totalScore;
                updateRecord["mcs_scoredate"] = DateTime.Now;
                // 如人工复核日期为空，补设为当前日期（经过人工复核阶段后进入计算）
                var creditRecordForCheck = service.Retrieve("mcs_credit_record", target.Id, new ColumnSet("mcs_checkdate"));
                if (!creditRecordForCheck.Contains("mcs_checkdate") || creditRecordForCheck["mcs_checkdate"] == null)
                {
                    updateRecord["mcs_checkdate"] = DateTime.Now;
                    tracer.Trace("人工复核日期为空，补设为当前日期");
                }
                // 状态保持为13(信用分计算)，不自动推进到14
                // 用户在状态13查看计算结果后，手动点击下一步进入14(审核申请)
                // BppIntegrationPlugin在状态14时触发，设置BPP审批字段

                service.Update(updateRecord);

                tracer.Trace("===== CreditScoreCalculationPlugin 执行完成，状态保持13(信用分计算) =====");
            }
            catch (InvalidPluginExecutionException)
            {
                // 业务提示异常直接抛出，保持友好提示
                throw;
            }
            catch (Exception ex)
            {
                tracer.Trace($"信用分计算系统异常: {ex.Message}");
                throw new InvalidPluginExecutionException($"信用分计算发生系统错误，请联系管理员。详细信息: {ex.Message}");
            }
        }

        /// <summary>
        /// 校验所有客户信用标签是否已补录完成
        /// 定量：mcs_itemintvalue2 有有效值且 mcs_itemvalue2 != "N/A"
        /// 定性：mcs_credititem_value 有值
        /// </summary>
        private void ValidateTagsCompleted(IOrganizationService service, ITracingService tracer, Guid creditRecordId)
        {
            var query = new QueryExpression("mcs_customer_tag")
            {
                ColumnSet = new ColumnSet("mcs_customer_tagid", "mcs_itemcode", "mcs_datatype", "mcs_itemintvalue2", "mcs_itemvalue2", "mcs_credititem_value"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_credit_record", ConditionOperator.Equal, creditRecordId),
                        new ConditionExpression("mcs_active", ConditionOperator.Equal, true)
                    }
                }
            };

            var tags = service.RetrieveMultiple(query).Entities;
            var missingItems = new List<string>();

            foreach (var tag in tags)
            {
                int dataType = tag.GetAttributeValue<OptionSetValue>("mcs_datatype")?.Value ?? 0;
                if (dataType == 1) // 定量
                {
                    bool hasIntValue = tag.Contains("mcs_itemintvalue2") && tag["mcs_itemintvalue2"] != null;
                    string strValue = tag.GetAttributeValue<string>("mcs_itemvalue2");
                    bool hasStrValue = !string.IsNullOrEmpty(strValue) && strValue != "N/A";
                    if (!hasIntValue && !hasStrValue)
                    {
                        missingItems.Add(tag.GetAttributeValue<string>("mcs_itemcode") ?? "");
                    }
                }
                else // 定性
                {
                    var lookupValue = tag.GetAttributeValue<EntityReference>("mcs_credititem_value");
                    if (lookupValue == null)
                    {
                        missingItems.Add(tag.GetAttributeValue<string>("mcs_itemcode") ?? "");
                    }
                }
            }

            if (missingItems.Count > 0)
            {
                string msg = $"以下信用标签尚未补录完成，无法进入信用分计算：{string.Join("、", missingItems)}";
                tracer.Trace(msg);
                throw new InvalidPluginExecutionException(msg);
            }

            tracer.Trace("所有标签已补录完成，允许计算信用分");
        }

        /// <summary>
        /// 获取评分卡类型（实时从Account查询客户属性匹配）
        /// 规则：
        /// 1. 新老客户：主数据关联全部客户记录查 mcs_order，任一有一单即老客户
        /// 2. 经销商/直销认定（禅道#2147，2026-09-05 用户确认）：主数据关联全部客户记录中任一条类别=经销商(10/90)即经销商，否则为直销
        /// 3. 客户等级（禅道#2147）：取最高等级（4=S/3=A/2=B/1=C）；经销商场景在经销商记录中取，直销场景在全部记录中取
        /// 4. 个人客户概念已取消（2026-09-05 用户明确），非经销商即直销，不再按 accounttype 匹配类别5
        /// </summary>
        private int GetCategoryId(IOrganizationService service, ITracingService tracer, Entity creditRecord)
        {
            // 获取客户ID
            if (!creditRecord.Contains("mcs_accountid") || 
                !(creditRecord["mcs_accountid"] is EntityReference))
            {
                tracer.Trace("未找到客户编码，使用默认值1(SA级老客户)");
                return 1;
            }

            var accountRef = creditRecord.GetAttributeValue<EntityReference>("mcs_accountid");
            Guid accountId = accountRef.Id;
            tracer.Trace($"客户ID: {accountId}");

            // 查询Account找到关联的客户主数据
            var account = service.Retrieve("account", accountId, new ColumnSet("mcs_customermasterdata"));
            
            if (account == null)
            {
                tracer.Trace("未找到客户记录，使用默认值1(SA级老客户)");
                return 1;
            }

            // 输入因子2/3：经销商认定 + 客户等级（禅道#2147 聚合口径，不再读客户主数据单一字段）
            var (isDealer, accountLevel) = GetAggregatedCustomerAttributes(service, tracer, account, accountId);
            tracer.Trace($"是否经销商: {isDealer}, 客户等级: {accountLevel}");

            // 输入因子1：新老客户
            // 口径（2026-09-03 用户拍板）：当前客户 → 客户主数据 → 主数据关联的全部客户记录 → 查这些客户的 mcs_order，任一有一单即老客户。
            // 订单以自定义订单表 mcs_order（客户字段 mcs_contractbuyer）为准，不再用标准销售订单 salesorder（生产为空表）。
            bool isOldCustomer = IsOldCustomerByMasterDataOrders(service, tracer, account, accountId);
            tracer.Trace($"是否老客户: {isOldCustomer}");

            // 匹配评分卡类型
            return MatchScoringCardType(isOldCustomer, isDealer, accountLevel, tracer);
        }

        /// <summary>
        /// 聚合客户属性（禅道#2147，2026-09-05 用户确认口径）：
        /// 客户类别/等级不再读客户主数据单一字段——同一法人按「客户×大区关系」建多条客户记录，各记录类别/等级可不同，主数据字段不能代表整体。
        /// 改为主数据关联的全部客户记录聚合：
        /// 1. 任一记录类别=正式经销商(10)/意向经销商(90) → 整体认定为经销商，否则为直销客户；
        /// 2. 客户等级取最高（钻4>金3>银2>C1，空值不参与）：经销商场景在经销商记录中取，直销场景在全部记录中取（直销场景全部记录均非经销商，两者等价）；
        /// 3. 条件相同多条并列不影响结果（规则3），无需排序。
        /// 与 CofaceDataSyncPlugin.GetAggregatedCustomerAttributes 同口径，保证「按哪套卡生成标签」与「按哪套卡打分」一致。
        /// </summary>
        private (bool IsDealer, int Level) GetAggregatedCustomerAttributes(IOrganizationService service, ITracingService tracer, Entity account, Guid accountId)
        {
            var cmRef = account.GetAttributeValue<EntityReference>("mcs_customermasterdata");

            List<Entity> records;
            if (cmRef != null)
            {
                // 主数据下全部客户记录（同一法人的各区域关系记录）
                var accQuery = new QueryExpression("account")
                {
                    ColumnSet = new ColumnSet("mcs_accountcategory", "mcs_accountlevel"),
                    Criteria = new FilterExpression
                    {
                        Conditions = { new ConditionExpression("mcs_customermasterdata", ConditionOperator.Equal, cmRef.Id) }
                    }
                };
                records = service.RetrieveMultiple(accQuery).Entities.ToList();
                // 防御性补充：确保当前客户记录在内
                if (!records.Any(r => r.Id == accountId))
                {
                    records.Add(service.Retrieve("account", accountId, new ColumnSet("mcs_accountcategory", "mcs_accountlevel")));
                }
            }
            else
            {
                tracer.Trace("account 未关联 mcs_customermasterdata，仅按当前客户记录判断（兼容）");
                records = new List<Entity> { service.Retrieve("account", accountId, new ColumnSet("mcs_accountcategory", "mcs_accountlevel")) };
            }

            bool isDealer = false;
            int dealerMaxLevel = 0;
            int allMaxLevel = 0;
            foreach (var r in records)
            {
                int cat = r.GetAttributeValue<OptionSetValue>("mcs_accountcategory")?.Value ?? 0;
                int lvl = r.GetAttributeValue<OptionSetValue>("mcs_accountlevel")?.Value ?? 0;
                if (lvl > allMaxLevel) allMaxLevel = lvl;
                if (cat == 10 || cat == 90)
                {
                    isDealer = true;
                    if (lvl > dealerMaxLevel) dealerMaxLevel = lvl;
                }
            }

            int level = isDealer ? dealerMaxLevel : allMaxLevel;
            tracer.Trace($"聚合客户属性: 主数据关联客户记录数={records.Count}, 是否经销商={isDealer}, 最高等级={level}");
            return (isDealer, level);
        }

        /// <summary>
        /// 判断新老客户（2026-09-03 用户拍板口径）：当前客户 → 客户主数据 → 主数据关联的全部客户记录 → 查这些客户的自定义订单 mcs_order，任一有一单即老客户。
        /// 与 CofaceDataSyncPlugin.IsOldCustomerByMasterDataOrders 同口径，保证「按哪套卡生成标签」与「按哪套卡打分」一致。
        /// </summary>
        private bool IsOldCustomerByMasterDataOrders(IOrganizationService service, ITracingService tracer, Entity account, Guid accountId)
        {
            var accountIds = new List<Guid> { accountId };

            // 当前客户关联了客户主数据时，查出主数据下全部客户记录（同一法人的各区域关系记录）
            var cmRef = account.GetAttributeValue<EntityReference>("mcs_customermasterdata");
            if (cmRef != null)
            {
                var accQuery = new QueryExpression("account")
                {
                    ColumnSet = new ColumnSet("accountid"),
                    Criteria = new FilterExpression
                    {
                        Conditions = { new ConditionExpression("mcs_customermasterdata", ConditionOperator.Equal, cmRef.Id) }
                    }
                };
                foreach (var acc in service.RetrieveMultiple(accQuery).Entities)
                {
                    if (!accountIds.Contains(acc.Id))
                    {
                        accountIds.Add(acc.Id);
                    }
                }
            }
            else
            {
                tracer.Trace("account 未关联 mcs_customermasterdata，新老客户仅按当前客户记录判断（兼容）");
            }

            // 查这些客户记录名下的自定义订单，任一有一单即老客户
            var orderQuery = new QueryExpression("mcs_order")
            {
                ColumnSet = new ColumnSet("mcs_orderid"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("mcs_contractbuyer", ConditionOperator.In, accountIds.Cast<object>().ToArray()) }
                },
                TopCount = 1
            };
            bool isOld = service.RetrieveMultiple(orderQuery).Entities.Count > 0;
            tracer.Trace($"新老客户判断: 主数据关联客户记录数={accountIds.Count}, 有mcs_order订单={isOld}");
            return isOld;
        }

        /// <summary>
        /// 根据输入因子匹配评分卡类型
        /// 评分卡类型：1=SA级老客户, 2=SA级新客户, 3=BC级老客户, 4=BC级新客户, 5=个人客户(已取消), 6=老经销商, 7=新经销商
        /// 
        /// 口径（禅道#2147，2026-09-05 用户确认）：
        /// - isDealer/等级已由 GetAggregatedCustomerAttributes 按主数据全量客户记录聚合
        /// - 个人客户概念取消，不再按 accounttype=1 匹配类别5
        /// - 直销分级：4=S级, 3=A级 → SA级大客户；2=B级, 1=C级/无等级 → BC级
        /// </summary>
        private int MatchScoringCardType(bool isOldCustomer, bool isDealer, int accountLevel, ITracingService tracer)
        {
            // ========== 经销商 ==========
            if (isDealer)
            {
                if (isOldCustomer)
                {
                    tracer.Trace("匹配评分卡: 6-老经销商");
                    return 6;
                }
                else
                {
                    tracer.Trace("匹配评分卡: 7-新经销商");
                    return 7;
                }
            }

            // ========== 直销客户（S/A/B/C） ==========
            // accountlevel映射：4=S级, 3=A级, 2=B级, 1=C级
            // SA级大客户 = S级(4) 或 A级(3)
            bool isBigAccount = (accountLevel == 4 || accountLevel == 3);
            
            if (isOldCustomer)
            {
                if (isBigAccount)
                {
                    tracer.Trace("匹配评分卡: 1-SA级老客户");
                    return 1;
                }
                else
                {
                    tracer.Trace("匹配评分卡: 3-BC级老客户");
                    return 3;
                }
            }
            else // 新客户
            {
                if (isBigAccount)
                {
                    tracer.Trace("匹配评分卡: 2-SA级新客户");
                    return 2;
                }
                else
                {
                    tracer.Trace("匹配评分卡: 4-BC级新客户");
                    return 4;
                }
            }
        }
    }
}
