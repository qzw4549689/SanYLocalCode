using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using SanyD365.Plugins.CreditScore.Calculator;
using System;
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
    public class CreditScorePlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = factory.CreateOrganizationService(context.UserId);
            ITracingService tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracer.Trace("===== CreditScorePlugin 开始执行 =====");

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
                // 获取完整记录信息
                Entity creditRecord = service.Retrieve("mcs_credit_record", target.Id,
                    new ColumnSet("mcs_scoreid", "mcs_accountid"));

                string scoreId = creditRecord.GetAttributeValue<string>("mcs_scoreid");

                // 获取评分卡类型（实时从Account查询客户属性匹配）
                int categoryId = GetCategoryId(service, tracer, creditRecord);
                tracer.Trace($"评分卡类型: {categoryId}");

                // 计算信用分
                var calculator = new ScoreCalculator(service, tracer);
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

                tracer.Trace("===== CreditScorePlugin 执行完成，状态保持13(信用分计算) =====");
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
        /// 获取评分卡类型（实时从Account查询客户属性匹配）
        /// 规则：
        /// 1. 查询销售订单表判断新老客户（≥1次订单=老客户）
        /// 2. 从Account.mcs_accountcategory判断是否经销商（10=正式经销商, 90=意向经销商）
        /// 3. 从Account.mcs_accountlevel判断直销分级（4=Diamond/S级, 3=Gold/A级, 2=Silver/B级, 1=Other/C级）
        /// 4. 从Account.mcs_accounttype判断个人客户（1=Individual Account个人客户, 2=Company Account公司客户）
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

            // 查询Account客户属性（增加mcs_accounttype用于个人客户识别）
            var account = service.Retrieve("account", accountId, 
                new ColumnSet("mcs_accountcategory", "mcs_accountlevel", "mcs_accounttype"));
            
            if (account == null)
            {
                tracer.Trace("未找到客户记录，使用默认值1(SA级老客户)");
                return 1;
            }

            // 输入因子2：是否经销商
            // D365实际值：10=Official Dealer(正式经销商), 90=Prospective Dealer(意向经销商)
            int accountCategory = account.GetAttributeValue<OptionSetValue>("mcs_accountcategory")?.Value ?? 0;
            bool isDealer = (accountCategory == 10 || accountCategory == 90);
            tracer.Trace($"客户类别: {accountCategory}, 是否经销商: {isDealer}");

            // 输入因子3：直销客户分级
            // D365实际值：4=Diamond, 3=Gold, 2=Silver, 1=Other
            // 映射到PRD定义：4=S级, 3=A级, 2=B级, 1=C级
            int accountLevel = account.GetAttributeValue<OptionSetValue>("mcs_accountlevel")?.Value ?? 0;
            tracer.Trace($"客户级别: {accountLevel}");

            // 输入因子4：客户类型（个人客户识别）
            // D365实际值：1=Individual Account(个人客户), 2=Company Account(公司客户)
            int accountType = account.GetAttributeValue<OptionSetValue>("mcs_accounttype")?.Value ?? 0;
            tracer.Trace($"客户类型: {accountType}");

            // 输入因子1：新老客户（查询销售订单）
            bool isOldCustomer = CheckHasSalesOrder(service, accountId);
            tracer.Trace($"是否老客户: {isOldCustomer}");

            // 匹配评分卡类型
            return MatchScoringCardType(isOldCustomer, isDealer, accountLevel, accountCategory, accountType, tracer);
        }

        /// <summary>
        /// 查询销售订单表判断是否有历史交易（≥1次=老客户）
        /// </summary>
        private bool CheckHasSalesOrder(IOrganizationService service, Guid accountId)
        {
            var query = new QueryExpression("salesorder")
            {
                ColumnSet = new ColumnSet("salesorderid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("customerid", ConditionOperator.Equal, accountId)
                    }
                },
                TopCount = 1
            };

            var results = service.RetrieveMultiple(query);
            return results.Entities.Count > 0;
        }

        /// <summary>
        /// 根据输入因子匹配评分卡类型
        /// 评分卡类型：1=SA级老客户, 2=SA级新客户, 3=BC级老客户, 4=BC级新客户, 5=个人客户, 6=老经销商, 7=新经销商
        /// 
        /// D365选项集实际值映射：
        /// - accountcategory: 10=正式经销商, 90=意向经销商 → 经销商
        /// - accountlevel: 4=Diamond(S级), 3=Gold(A级), 2=Silver(B级), 1=Other(C级)
        /// - accounttype: 1=Individual Account(个人客户), 2=Company Account(公司客户)
        /// </summary>
        private int MatchScoringCardType(bool isOldCustomer, bool isDealer, int accountLevel, int accountCategory, int accountType, ITracingService tracer)
        {
            // ========== 个人客户判断 ==========
            // D365中 accounttype=1 表示 Individual Account（个人客户）
            // 如不正确请反馈，可回退到旧逻辑（category=20 && level=1）
            bool isPersonal = (accountType == 1);
            if (isPersonal)
            {
                tracer.Trace("匹配评分卡: 5-个人客户 (accounttype=1, Individual Account)");
                return 5;
            }

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
