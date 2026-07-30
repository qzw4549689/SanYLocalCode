namespace SanyD365.Plugins.FactoryCredit.Calculation.Services
{
    /// <summary>
    /// 厂端授信模型参数信息
    /// </summary>
    public class ModelParameterInfo
    {
        /// <summary>客户分类标签</summary>
        public string BuyerGradeLabel { get; set; }

        /// <summary>客户分类选项集值</summary>
        public int BuyerGradeValue { get; set; }

        /// <summary>客户等级标签</summary>
        public string CreditGradeLabel { get; set; }

        /// <summary>客户等级选项集值</summary>
        public int CreditGradeValue { get; set; }

        /// <summary>系数1：财务能力</summary>
        public decimal Adjust1 { get; set; }

        /// <summary>系数2：历史回款能力</summary>
        public decimal Adjust2 { get; set; }

        /// <summary>系数3：历史基准额度</summary>
        public decimal Adjust3 { get; set; }

        /// <summary>聚合方法标签：MAX 或 MIN</summary>
        public string AggFuncLabel { get; set; }

        /// <summary>历史基准额度（USD），按客户分类+ALL查询</summary>
        public decimal CountryBenchmark { get; set; }

        public bool HasModelParams => Adjust1 != 0m || Adjust2 != 0m || Adjust3 != 0m;
    }
}
