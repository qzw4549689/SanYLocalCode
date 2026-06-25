using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace SanyD365.Plugins.TradeStPayTerm.Api
{
    /// <summary>
    /// 成交条件样板库查询服务
    /// </summary>
    public class TradeStPayTermQueryService
    {
        private readonly IOrganizationService _service;
        private readonly ITracingService _tracer;

        // 泵路事业部编码（示例，需业务确认）
        public const string PumbuBusinessUnitCode = "BU-1018";

        public TradeStPayTermQueryService(IOrganizationService service, ITracingService tracer)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _tracer = tracer;
        }

        /// <summary>
        /// 根据入参查询匹配的成交条件样板库记录
        /// </summary>
        public QueryResult Query(string buId, string subId, string countryCode, string prdGroupId, string buyerCode)
        {
            _tracer.Trace($"成交条件查询开始: buId={buId}, subId={subId}, countryCode={countryCode}, prdGroupId={prdGroupId}, buyerCode={buyerCode}");

            // 1. 根据客户编码计算客户分类和客户等级
            var buyerInfo = GetBuyerInfo(buyerCode);
            _tracer.Trace($"客户分类: {buyerInfo.BuyerGrade}");

            // 2. 根据产品线编码查询成交条件产品分类
            var typeIds = GetProductTypeIds(prdGroupId);
            string typeId = typeIds.FirstOrDefault();
            _tracer.Trace($"产品分类编码: {typeId ?? "(空)"}");

            // 3. 构建查询
            var query = BuildQuery(buId, subId, countryCode, typeId, buyerInfo.BuyerGrade);

            // 4. 执行查询
            var records = _service.RetrieveMultiple(query).Entities;
            _tracer.Trace($"查询到 {records.Count} 条记录");

            return new QueryResult
            {
                Status = "1",
                Message = "",
                Records = records.Select(MapToRecord).ToList()
            };
        }

        /// <summary>
        /// 根据客户编码从客户主数据表获取客户分类和客户等级
        /// </summary>
        private BuyerInfo GetBuyerInfo(string buyerCode)
        {
            var result = new BuyerInfo();

            if (string.IsNullOrWhiteSpace(buyerCode))
                return result;

            var query = new QueryExpression("mcs_customermasterdata")
            {
                ColumnSet = new ColumnSet("mcs_accountcategory", "mcs_accountlevel", "mcs_dealerrank"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_accountnumber", ConditionOperator.Equal, buyerCode)
                    }
                },
                TopCount = 1
            };

            var records = _service.RetrieveMultiple(query).Entities;
            if (records.Count == 0)
            {
                _tracer.Trace($"未找到客户主数据: {buyerCode}");
                return result;
            }

            var customer = records[0];
            var accountCategory = customer.GetAttributeValue<OptionSetValue>("mcs_accountcategory")?.Value;
            var accountLevel = customer.GetAttributeValue<OptionSetValue>("mcs_accountlevel")?.Value;
            var dealerRank = customer.GetAttributeValue<OptionSetValue>("mcs_dealerrank")?.Value;

            // 判断是否为经销商
            bool isDealer = IsDealer(accountCategory, dealerRank);

            if (isDealer)
            {
                result.BuyerGrade = MapDealerRankToBuyerGrade(dealerRank);
            }
            else
            {
                result.BuyerGrade = MapDirectCustomerToBuyerGrade(accountLevel);
            }

            return result;
        }

        /// <summary>
        /// 判断是否为经销商
        /// </summary>
        private bool IsDealer(int? accountCategory, int? dealerRank)
        {
            // 客户类别包含"经销商"字样：
            // 10=Official Dealer, 30=Dealer End Customer, 60=Dealer Key Account, 90=Prospective Dealer
            if (accountCategory == 10 || accountCategory == 30 || accountCategory == 60 || accountCategory == 90)
                return true;

            // 经销商分级有值
            if (dealerRank.HasValue)
                return true;

            return false;
        }

        /// <summary>
        /// 经销商分级映射到客户分类
        /// </summary>
        private string MapDealerRankToBuyerGrade(int? dealerRank)
        {
            switch (dealerRank)
            {
                case 1: return "D1"; // 钻石
                case 2: return "D2"; // 铂金
                case 3: return "D3"; // 白银
                case 4: return "D4"; // 认证
                case 5: return "D5"; // 意向
                default: return string.Empty;
            }
        }

        /// <summary>
        /// 直销客户映射到客户分类
        /// </summary>
        private string MapDirectCustomerToBuyerGrade(int? accountLevel)
        {
            // TODO: 个人客户判断逻辑待业务确认（客户主数据表暂无客户类型字段）
            // 当前所有非经销商均按公司大客户级别处理
            switch (accountLevel)
            {
                case 4: return "S"; // Diamond
                case 3: return "A"; // Gold
                case 2: return "B"; // Silver
                default: return "C"; // Other / 未设置
            }
        }

        /// <summary>
        /// 根据产品线编码查询成交条件产品分类编码
        /// </summary>
        private List<string> GetProductTypeIds(string prdGroupId)
        {
            var result = new List<string>();

            if (string.IsNullOrWhiteSpace(prdGroupId))
                return result;

            var query = new QueryExpression("mcs_trade_ptgrouptype")
            {
                ColumnSet = new ColumnSet("mcs_typeid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_groupid", ConditionOperator.Equal, prdGroupId)
                    }
                }
            };

            var records = _service.RetrieveMultiple(query).Entities;
            foreach (var record in records)
            {
                var typeId = record.GetAttributeValue<string>("mcs_typeid");
                if (!string.IsNullOrWhiteSpace(typeId) && !result.Contains(typeId))
                    result.Add(typeId);
            }

            return result;
        }

        /// <summary>
        /// 构建成交条件样板库查询
        /// </summary>
        private QueryExpression BuildQuery(string buId, string subId, string countryCode, string typeId, string buyerGrade)
        {
            bool isPumpBu = IsPumpBusinessUnit(buId);

            var query = new QueryExpression("mcs_trade_stpayterm")
            {
                ColumnSet = new ColumnSet(
                    "mcs_trade_stpaytermname", "mcs_buid", "mcs_buname", "mcs_subid", "mcs_subname",
                    "mcs_countrycode", "mcs_countryname", "mcs_typeid", "mcs_typename", "mcs_buyergrade",
                    "mcs_creditgrade", "mcs_downpay", "mcs_payterm", "mcs_payfreq")
            };

            var filter = new FilterExpression(LogicalOperator.And);
            filter.Conditions.Add(new ConditionExpression("mcs_buid", ConditionOperator.Equal, buId));
            filter.Conditions.Add(new ConditionExpression("mcs_status", ConditionOperator.Equal, 2));

            if (isPumpBu)
            {
                // 泵路事业部：不校验国家，子公司必填
                filter.Conditions.Add(new ConditionExpression("mcs_subid", ConditionOperator.Equal, subId));

                // 产品分类包含匹配
                if (!string.IsNullOrWhiteSpace(typeId))
                    filter.Conditions.Add(new ConditionExpression("mcs_typeid", ConditionOperator.Like, $"%{typeId}%"));

                // 客户分类包含匹配
                if (!string.IsNullOrWhiteSpace(buyerGrade))
                    filter.Conditions.Add(new ConditionExpression("mcs_buyergrade", ConditionOperator.Like, $"%{buyerGrade}%"));

            }
            else
            {
                // 其他事业部：子公司/国家/产品分类允许 NA
                var subFilter = new FilterExpression(LogicalOperator.Or);
                subFilter.Conditions.Add(new ConditionExpression("mcs_subid", ConditionOperator.Equal, subId));
                subFilter.Conditions.Add(new ConditionExpression("mcs_subid", ConditionOperator.Equal, "NA"));
                filter.AddFilter(subFilter);

                var countryFilter = new FilterExpression(LogicalOperator.Or);
                countryFilter.Conditions.Add(new ConditionExpression("mcs_countrycode", ConditionOperator.Like, $"%{countryCode}%"));
                countryFilter.Conditions.Add(new ConditionExpression("mcs_countrycode", ConditionOperator.Equal, "NA"));
                filter.AddFilter(countryFilter);

                var typeFilter = new FilterExpression(LogicalOperator.Or);
                if (!string.IsNullOrWhiteSpace(typeId))
                    typeFilter.Conditions.Add(new ConditionExpression("mcs_typeid", ConditionOperator.Like, $"%{typeId}%"));
                typeFilter.Conditions.Add(new ConditionExpression("mcs_typeid", ConditionOperator.Equal, "NA"));
                filter.AddFilter(typeFilter);

                // 客户分类包含匹配
                if (!string.IsNullOrWhiteSpace(buyerGrade))
                    filter.Conditions.Add(new ConditionExpression("mcs_buyergrade", ConditionOperator.Like, $"%{buyerGrade}%"));
            }

            query.Criteria = filter;
            return query;
        }

        /// <summary>
        /// 判断是否为泵路事业部
        /// </summary>
        private bool IsPumpBusinessUnit(string buId)
        {
            // TODO: 业务确认泵路事业部编码
            return !string.IsNullOrWhiteSpace(buId) &&
                   buId.Equals(PumbuBusinessUnitCode, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 将 D365 实体映射为返回记录
        /// </summary>
        private TradeStPayTermRecord MapToRecord(Entity entity)
        {
            return new TradeStPayTermRecord
            {
                TradeTermId = entity.GetAttributeValue<string>("mcs_trade_stpaytermname"),
                BuId = entity.GetAttributeValue<string>("mcs_buid"),
                BuName = entity.GetAttributeValue<string>("mcs_buname"),
                SubId = entity.GetAttributeValue<string>("mcs_subid"),
                SubName = entity.GetAttributeValue<string>("mcs_subname"),
                CountryCode = entity.GetAttributeValue<string>("mcs_countrycode"),
                CountryName = entity.GetAttributeValue<string>("mcs_countryname"),
                TypeId = entity.GetAttributeValue<string>("mcs_typeid"),
                TypeName = entity.GetAttributeValue<string>("mcs_typename"),
                BuyerGrade = entity.GetAttributeValue<string>("mcs_buyergrade"),
                DownPay = entity.GetAttributeValue<decimal>("mcs_downpay"),
                PayTerm = entity.GetAttributeValue<int>("mcs_payterm"),
                PayFreq = entity.GetAttributeValue<int>("mcs_payfreq")
            };
        }

        /// <summary>
        /// 将查询结果序列化为 JSON
        /// </summary>
        public static string SerializeResult(QueryResult result)
        {
            return JsonConvert.SerializeObject(result, new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                Converters = { new StringEnumConverter() },
                Formatting = Formatting.None
            });
        }
    }

    public class BuyerInfo
    {
        public string BuyerGrade { get; set; } = string.Empty;
    }

    public class QueryResult
    {
        public string Status { get; set; } = "0";
        public string Message { get; set; } = string.Empty;
        public List<TradeStPayTermRecord> Records { get; set; } = new List<TradeStPayTermRecord>();
    }

    public class TradeStPayTermRecord
    {
        public string TradeTermId { get; set; }
        public string BuId { get; set; }
        public string BuName { get; set; }
        public string SubId { get; set; }
        public string SubName { get; set; }
        public string CountryCode { get; set; }
        public string CountryName { get; set; }
        public string TypeId { get; set; }
        public string TypeName { get; set; }
        public string BuyerGrade { get; set; }
        public decimal DownPay { get; set; }
        public int PayTerm { get; set; }
        public int PayFreq { get; set; }
    }
}
