using Microsoft.Xrm.Sdk;

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
        /// </summary>
        public CategoryResult CalculateCategory(Entity customer)
        {
            int accountType = GetOptionSetValue(customer, "mcs_accounttype");
            if (accountType == ACCOUNT_TYPE_INDIVIDUAL)
            {
                return new CategoryResult
                {
                    BuyerGradeLabel = "I",
                    BuyerGradeValue = 5,
                    CreditGradeLabel = GetCustomerCreditGradeLabel(customer),
                    CreditGradeValue = MapCreditGradeToConfigValue(GetCustomerCreditGradeLabel(customer))
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
                        CreditGradeLabel = GetCustomerCreditGradeLabel(customer),
                        CreditGradeValue = MapCreditGradeToConfigValue(GetCustomerCreditGradeLabel(customer))
                    };
                }

                return new CategoryResult
                {
                    BuyerGradeLabel = "D5",
                    BuyerGradeValue = 10,
                    CreditGradeLabel = GetCustomerCreditGradeLabel(customer),
                    CreditGradeValue = MapCreditGradeToConfigValue(GetCustomerCreditGradeLabel(customer))
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
                    CreditGradeLabel = GetCustomerCreditGradeLabel(customer),
                    CreditGradeValue = MapCreditGradeToConfigValue(GetCustomerCreditGradeLabel(customer))
                };
            }

            return new CategoryResult
            {
                BuyerGradeLabel = "C",
                BuyerGradeValue = 4,
                CreditGradeLabel = GetCustomerCreditGradeLabel(customer),
                CreditGradeValue = MapCreditGradeToConfigValue(GetCustomerCreditGradeLabel(customer))
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
