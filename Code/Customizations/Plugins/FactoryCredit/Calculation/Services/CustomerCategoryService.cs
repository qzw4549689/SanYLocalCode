using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System.Collections.Generic;

namespace SanyD365.Plugins.FactoryCredit.Calculation.Services
{
    /// <summary>
    /// 客户分类计算结果
    /// </summary>
    public class CategoryResult
    {
        /// <summary>客户分类标签：S/A/B/C/I/D1-D5</summary>
        public string BuyerGradeLabel { get; set; }

        /// <summary>客户分类在 mcs_fca_mdlconfig.mcs_buyergrade 中的选项集值</summary>
        public int BuyerGradeValue { get; set; }

        /// <summary>客户等级标签：A0-A4 或 ALL</summary>
        public string CreditGradeLabel { get; set; }

        /// <summary>客户等级在 mcs_fca_mdlconfig.mcs_creditgrade 中的选项集值</summary>
        public int CreditGradeValue { get; set; }
    }

    /// <summary>
    /// 客户分类计算服务
    /// </summary>
    public class CustomerCategoryService
    {
        private const int ACCOUNT_TYPE_INDIVIDUAL = 1;
        private const int ACCOUNT_TYPE_COMPANY = 2;
        private static readonly int[] DEALER_CATEGORIES = { 10, 30, 60, 90 };
        private static readonly int[] KA_LEVELS = { 1, 2, 3 };

        /// <summary>
        /// 根据客户主数据计算客户分类和客户等级
        /// 禅道#2189（2026-09-08）：经销商/等级改禅道#2147 聚合口径——主数据关联全部客户记录聚合：
        /// 任一记录类别=经销商(10/90)即经销商，等级取最高（经销商场景在经销商记录中取，直销场景在全部记录中取），
        /// 与信用评估 CreditScorePlugin/CofaceDataSyncPlugin.GetAggregatedCustomerAttributes 同源，两模块口径彻底一致；
        /// 主数据无关联客户记录时回退主数据单字段旧逻辑（兜底）。
        /// </summary>
        public CategoryResult CalculateCategory(IOrganizationService service, ITracingService tracer, Entity customer, System.Guid masterDataId)
        {
            string creditGradeLabel = GetCustomerCreditGradeLabel(customer);
            int creditGradeValue = MapCreditGradeToConfigValue(creditGradeLabel);

            // 聚合主数据关联的全部客户记录（#2147 口径：任一经销商(10/90)即经销商，等级取最高）
            var records = QueryAccountRecords(service, tracer, masterDataId);
            if (records.Count > 0)
            {
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
                tracer.Trace($"客户分类聚合(#2189/#2147口径): 主数据关联客户记录数={records.Count}, 是否经销商={isDealer}, 最高等级={level}");

                if (isDealer)
                {
                    // 等级映射 D1-D5：钻4→D1、铂3→D2、银2→D3、认证1→D4、无等级→D5(意向兜底)
                    int rank = (level >= 1 && level <= 4) ? 5 - level : 5;
                    return new CategoryResult
                    {
                        BuyerGradeLabel = $"D{rank}",
                        BuyerGradeValue = 5 + rank, // D1=6, D2=7...
                        CreditGradeLabel = creditGradeLabel,
                        CreditGradeValue = creditGradeValue
                    };
                }

                // 直销：4→S、3→A、2→B、其余→C（与信用评估客户等级 4=S/3=A/2=B/1=C 同源）
                string directLabel;
                int directValue;
                switch (level)
                {
                    case 4: directLabel = "S"; directValue = 1; break;
                    case 3: directLabel = "A"; directValue = 2; break;
                    case 2: directLabel = "B"; directValue = 3; break;
                    default: directLabel = "C"; directValue = 4; break;
                }
                return new CategoryResult
                {
                    BuyerGradeLabel = directLabel,
                    BuyerGradeValue = directValue,
                    CreditGradeLabel = creditGradeLabel,
                    CreditGradeValue = creditGradeValue
                };
            }

            // 回退：主数据无关联客户记录时按主数据单字段旧逻辑（兜底）
            tracer.Trace("主数据无关联客户记录，回退主数据单字段判定客户分类（兜底）");
            return CalculateCategoryByMasterData(customer, creditGradeLabel, creditGradeValue);
        }

        /// <summary>
        /// 查询主数据关联的全部客户记录（同一法人的各区域关系记录）
        /// </summary>
        private List<Entity> QueryAccountRecords(IOrganizationService service, ITracingService tracer, System.Guid masterDataId)
        {
            var result = new List<Entity>();
            try
            {
                var query = new QueryExpression("account")
                {
                    ColumnSet = new ColumnSet("mcs_accountcategory", "mcs_accountlevel"),
                    Criteria = new FilterExpression
                    {
                        Conditions = { new ConditionExpression("mcs_customermasterdata", ConditionOperator.Equal, masterDataId) }
                    }
                };
                foreach (var acc in service.RetrieveMultiple(query).Entities)
                {
                    result.Add(acc);
                }
            }
            catch (System.Exception ex)
            {
                tracer.Trace($"查询主数据关联客户记录异常: {ex.Message}，按无记录回退兜底");
            }
            return result;
        }

        /// <summary>
        /// 主数据单字段旧逻辑（兜底）：个人(I)/经销商(D1-D5)/大客户(S/A/B)/其他(C)
        /// </summary>
        private CategoryResult CalculateCategoryByMasterData(Entity customer, string creditGradeLabel, int creditGradeValue)
        {
            int accountType = GetOptionSetValue(customer, "mcs_accounttype");
            if (accountType == ACCOUNT_TYPE_INDIVIDUAL)
            {
                return new CategoryResult
                {
                    BuyerGradeLabel = "I",
                    BuyerGradeValue = 5,
                    CreditGradeLabel = creditGradeLabel,
                    CreditGradeValue = creditGradeValue
                };
            }

            int accountCategory = GetOptionSetValue(customer, "mcs_accountcategory");
            if (System.Array.IndexOf(DEALER_CATEGORIES, accountCategory) >= 0)
            {
                int dealerRank = GetOptionSetValue(customer, "mcs_dealerrank");
                if (dealerRank >= 1 && dealerRank <= 5)
                {
                    return new CategoryResult
                    {
                        BuyerGradeLabel = $"D{dealerRank}",
                        BuyerGradeValue = 5 + dealerRank, // D1=6, D2=7...
                        CreditGradeLabel = creditGradeLabel,
                        CreditGradeValue = creditGradeValue
                    };
                }

                return new CategoryResult
                {
                    BuyerGradeLabel = "D5",
                    BuyerGradeValue = 10,
                    CreditGradeLabel = creditGradeLabel,
                    CreditGradeValue = creditGradeValue
                };
            }

            int kaCategory = GetOptionSetValue(customer, "mcs_kacategory");
            if (System.Array.IndexOf(KA_LEVELS, kaCategory) >= 0)
            {
                string label = kaCategory == 1 ? "S" : (kaCategory == 2 ? "A" : "B");
                return new CategoryResult
                {
                    BuyerGradeLabel = label,
                    BuyerGradeValue = kaCategory,
                    CreditGradeLabel = creditGradeLabel,
                    CreditGradeValue = creditGradeValue
                };
            }

            return new CategoryResult
            {
                BuyerGradeLabel = "C",
                BuyerGradeValue = 4,
                CreditGradeLabel = creditGradeLabel,
                CreditGradeValue = creditGradeValue
            };
        }

        /// <summary>
        /// 获取客户主数据上的客户等级标签（A0-A4），空值返回 ALL
        /// </summary>
        private string GetCustomerCreditGradeLabel(Entity customer)
        {
            if (!customer.Contains("mcs_creditgrade"))
            {
                return "ALL";
            }

            OptionSetValue value = customer.GetAttributeValue<OptionSetValue>("mcs_creditgrade");
            if (value == null)
            {
                return "ALL";
            }

            switch (value.Value)
            {
                case 100000000: return "A0";
                case 100000001: return "A1";
                case 100000002: return "A2";
                case 100000003: return "A3";
                case 100000004: return "A4";
                default: return "ALL";
            }
        }

        /// <summary>
        /// 将客户等级标签映射为 mcs_fca_mdlconfig.mcs_creditgrade 选项集值
        /// </summary>
        private int MapCreditGradeToConfigValue(string gradeLabel)
        {
            switch (gradeLabel?.ToUpperInvariant())
            {
                case "A0": return 1;
                case "A1": return 2;
                case "A2": return 3;
                case "A3": return 4;
                case "A4": return 5;
                default: return 6; // ALL
            }
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
