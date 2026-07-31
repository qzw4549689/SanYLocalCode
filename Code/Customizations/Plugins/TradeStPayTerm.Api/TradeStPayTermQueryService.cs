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

            // 1. 根据客户编码计算客户分类
            var buyerInfo = GetBuyerInfo(buyerCode);
            _tracer.Trace($"客户分类: {buyerInfo.BuyerGrade}");

            // 2. 根据国家代码查询国家 GUID
            var countryIds = GetCountryIds(countryCode);
            _tracer.Trace($"国家GUID数: {countryIds.Count}");

            // 3. 根据产品线编码查询成交条件产品分类 GUID 列表
            var tradeTypeIds = GetTradeTypeIds(prdGroupId);
            _tracer.Trace($"产品分类GUID数: {tradeTypeIds.Count}");

            // 4. 加载国家/产品分类映射缓存（用于返回时 GUID→编码/名称转换）
            LoadCountryCache();
            LoadTradeTypeCache();

            // 5. 查询同事业部生效记录，并在内存中按多选 GUID 集合交集过滤
            var query = BuildBaseQuery(buId);
            var allRecords = _service.RetrieveMultiple(query).Entities;
            _tracer.Trace($"同事业部生效记录 {allRecords.Count} 条");

            bool isPumpBu = IsPumpBusinessUnit(buId);
            var matchedRecords = allRecords
                .Where(r => IsRecordMatch(r, isPumpBu, subId, countryIds, tradeTypeIds, buyerInfo.BuyerGrade))
                .Select(MapToRecord)
                .ToList();

            _tracer.Trace($"匹配记录 {matchedRecords.Count} 条");

            return new QueryResult
            {
                Status = "1",
                Message = "",
                Records = matchedRecords
            };
        }

        #region 买家信息

        /// <summary>
        /// 根据客户编码从客户主数据表获取客户分类
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

        private bool IsDealer(int? accountCategory, int? dealerRank)
        {
            if (accountCategory == 10 || accountCategory == 30 || accountCategory == 60 || accountCategory == 90)
                return true;

            if (dealerRank.HasValue)
                return true;

            return false;
        }

        private string MapDealerRankToBuyerGrade(int? dealerRank)
        {
            switch (dealerRank)
            {
                case 1: return "D1";
                case 2: return "D2";
                case 3: return "D3";
                case 4: return "D4";
                case 5: return "D5";
                default: return string.Empty;
            }
        }

        private string MapDirectCustomerToBuyerGrade(int? accountLevel)
        {
            switch (accountLevel)
            {
                case 4: return "S";
                case 3: return "A";
                case 2: return "B";
                default: return "C";
            }
        }

        #endregion

        #region 国家/产品分类 GUID 解析

        /// <summary>
        /// 根据国家代码查询国家 GUID
        /// </summary>
        private List<Guid> GetCountryIds(string countryCode)
        {
            var result = new List<Guid>();

            if (string.IsNullOrWhiteSpace(countryCode))
                return result;

            var query = new QueryExpression("mcs_country")
            {
                ColumnSet = new ColumnSet("mcs_countryid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_countrycode", ConditionOperator.Equal, countryCode)
                    }
                }
            };

            foreach (var record in _service.RetrieveMultiple(query).Entities)
            {
                result.Add(record.Id);
            }

            return result;
        }

        /// <summary>
        /// 根据产品线编码查询成交条件产品分类 GUID 列表
        /// 支持逗号分隔传多个产品线编码，如 "2,4"
        /// </summary>
        private List<Guid> GetTradeTypeIds(string prdGroupId)
        {
            var result = new List<Guid>();

            if (string.IsNullOrWhiteSpace(prdGroupId))
                return result;

            // 0. 拆分逗号分隔的多个产品线编码
            var groupIds = prdGroupId
                .Split(',')
                .Select(g => g.Trim())
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (groupIds.Length == 0)
                return result;

            // 1. 产品线 → 产品分类编码
            var groupQuery = new QueryExpression("mcs_trade_ptgrouptype")
            {
                ColumnSet = new ColumnSet("mcs_typeid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_groupid", ConditionOperator.In, groupIds)
                    }
                }
            };

            var typeIds = new List<string>();
            foreach (var record in _service.RetrieveMultiple(groupQuery).Entities)
            {
                var typeId = record.GetAttributeValue<string>("mcs_typeid");
                if (!string.IsNullOrWhiteSpace(typeId) && !typeIds.Contains(typeId))
                    typeIds.Add(typeId);
            }

            if (typeIds.Count == 0)
                return result;

            // 2. 产品分类编码 → 产品分类 GUID
            var typeQuery = new QueryExpression("mcs_trade_pttype")
            {
                ColumnSet = new ColumnSet("mcs_trade_pttypeid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_typeid", ConditionOperator.In, typeIds.ToArray())
                    }
                }
            };

            foreach (var record in _service.RetrieveMultiple(typeQuery).Entities)
            {
                result.Add(record.Id);
            }

            return result;
        }

        #endregion

        #region 查询与内存过滤

        /// <summary>
        /// 构建基础查询：同事业部 + 生效状态
        /// </summary>
        private QueryExpression BuildBaseQuery(string buId)
        {
            var query = new QueryExpression("mcs_trade_stpayterm")
            {
                ColumnSet = new ColumnSet(
                    "mcs_trade_stpaytermname", "mcs_buid", "mcs_buname", "mcs_subid", "mcs_subname",
                    "mcs_countries", "mcs_trade_type", "mcs_buyergrade",
                    "mcs_creditgrade", "mcs_downpay", "mcs_payterm", "mcs_payfreq")
            };

            var filter = new FilterExpression(LogicalOperator.And);
            filter.Conditions.Add(new ConditionExpression("mcs_buid", ConditionOperator.Equal, buId));
            filter.Conditions.Add(new ConditionExpression("mcs_status", ConditionOperator.Equal, 2));
            query.Criteria = filter;

            return query;
        }

        /// <summary>
        /// 判断单条记录是否匹配所有维度（支持 NA 通配：空值）
        /// </summary>
        private bool IsRecordMatch(Entity record, bool isPumpBu, string subId, List<Guid> countryIds, List<Guid> tradeTypeIds, string buyerGrade)
        {
            // 子公司匹配
            string recordSubId = record.GetAttributeValue<string>("mcs_subid") ?? string.Empty;
            if (!IsWildcardMatch(subId, recordSubId))
                return false;

            // 国家匹配：泵路事业部不校验国家；其他事业部按 GUID 集合交集匹配
            if (!isPumpBu)
            {
                string recordCountries = record.GetAttributeValue<string>("mcs_countries") ?? string.Empty;
                if (!IsGuidSetMatch(countryIds, recordCountries))
                    return false;
            }

            // 产品分类匹配
            string recordTradeType = record.GetAttributeValue<string>("mcs_trade_type") ?? string.Empty;
            if (!IsGuidSetMatch(tradeTypeIds, recordTradeType))
                return false;

            // 客户分类匹配
            if (!IsBuyerGradeMatch(buyerGrade, record.GetAttributeValue<OptionSetValueCollection>("mcs_buyergrade")))
                return false;

            return true;
        }

        /// <summary>
        /// 任一方为空或 NA 视为通配；否则精确匹配
        /// </summary>
        private bool IsWildcardMatch(string value1, string value2)
        {
            if (string.IsNullOrWhiteSpace(value1) || value1.Trim().Equals("NA", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(value2) || value2.Trim().Equals("NA", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.Equals(value1.Trim(), value2.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// GUID 集合是否有交集；空集合或空字符串视为 NA 通配
        /// </summary>
        private bool IsGuidSetMatch(List<Guid> inputIds, string recordValue)
        {
            if (inputIds == null || inputIds.Count == 0 ||
                string.IsNullOrWhiteSpace(recordValue))
            {
                return true;
            }

            var recordIds = ParseGuidSet(recordValue);
            if (recordIds.Count == 0)
                return true;

            return inputIds.Any(id => recordIds.Contains(id));
        }

        private HashSet<Guid> ParseGuidSet(string value)
        {
            var result = new HashSet<Guid>();
            if (string.IsNullOrWhiteSpace(value))
                return result;

            foreach (var part in value.Split(','))
            {
                var trimmed = part.Trim();
                if (Guid.TryParse(trimmed, out var guid))
                    result.Add(guid);
            }

            return result;
        }

        /// <summary>
        /// 客户分类匹配：空值视为 NA 通配
        /// </summary>
        private bool IsBuyerGradeMatch(string buyerGrade, OptionSetValueCollection recordGrades)
        {
            if (string.IsNullOrWhiteSpace(buyerGrade))
                return true;

            var optionValue = MapBuyerGradeToOptionValue(buyerGrade.Trim());
            if (!optionValue.HasValue)
                return false;

            if (recordGrades == null || recordGrades.Count == 0)
                return true;

            return recordGrades.Any(o => o.Value == optionValue.Value);
        }

        private int? MapBuyerGradeToOptionValue(string grade)
        {
            switch (grade)
            {
                case "S": return 100000000;
                case "A": return 100000001;
                case "B": return 100000002;
                case "C": return 100000003;
                case "I": return 100000004;
                case "D1": return 100000005;
                case "D2": return 100000006;
                case "D3": return 100000007;
                case "D4": return 100000008;
                case "D5": return 100000009;
                default: return null;
            }
        }

        #endregion

        #region 返回映射

        private Dictionary<Guid, CountryInfo> _countryCache = new Dictionary<Guid, CountryInfo>();
        private Dictionary<Guid, TradeTypeInfo> _tradeTypeCache = new Dictionary<Guid, TradeTypeInfo>();

        private void LoadCountryCache()
        {
            _countryCache = new Dictionary<Guid, CountryInfo>();
            var query = new QueryExpression("mcs_country")
            {
                ColumnSet = new ColumnSet("mcs_countryid", "mcs_countrycode", "mcs_name")
            };

            foreach (var record in _service.RetrieveMultiple(query).Entities)
            {
                _countryCache[record.Id] = new CountryInfo
                {
                    Code = record.GetAttributeValue<string>("mcs_countrycode") ?? string.Empty,
                    Name = record.GetAttributeValue<string>("mcs_name") ?? string.Empty
                };
            }
        }

        private void LoadTradeTypeCache()
        {
            _tradeTypeCache = new Dictionary<Guid, TradeTypeInfo>();
            var query = new QueryExpression("mcs_trade_pttype")
            {
                ColumnSet = new ColumnSet("mcs_trade_pttypeid", "mcs_typeid", "mcs_trade_pttypename")
            };

            foreach (var record in _service.RetrieveMultiple(query).Entities)
            {
                _tradeTypeCache[record.Id] = new TradeTypeInfo
                {
                    Code = record.GetAttributeValue<string>("mcs_typeid") ?? string.Empty,
                    Name = record.GetAttributeValue<string>("mcs_trade_pttypename") ?? string.Empty
                };
            }
        }

        /// <summary>
        /// 将 D365 实体映射为返回记录
        /// </summary>
        private TradeStPayTermRecord MapToRecord(Entity entity)
        {
            var countryGuids = ParseGuidSet(entity.GetAttributeValue<string>("mcs_countries") ?? string.Empty);
            var tradeTypeGuids = ParseGuidSet(entity.GetAttributeValue<string>("mcs_trade_type") ?? string.Empty);

            var countryCodes = new List<string>();
            var countryNames = new List<string>();
            foreach (var guid in countryGuids)
            {
                if (_countryCache.TryGetValue(guid, out var countryInfo))
                {
                    countryCodes.Add(countryInfo.Code);
                    countryNames.Add(countryInfo.Name);
                }
            }

            var typeIds = new List<string>();
            var typeNames = new List<string>();
            foreach (var guid in tradeTypeGuids)
            {
                if (_tradeTypeCache.TryGetValue(guid, out var tradeTypeInfo))
                {
                    typeIds.Add(tradeTypeInfo.Code);
                    typeNames.Add(tradeTypeInfo.Name);
                }
            }

            return new TradeStPayTermRecord
            {
                TradeTermId = entity.GetAttributeValue<string>("mcs_trade_stpaytermname"),
                BuId = entity.GetAttributeValue<string>("mcs_buid"),
                BuName = entity.GetAttributeValue<string>("mcs_buname"),
                SubId = entity.GetAttributeValue<string>("mcs_subid"),
                SubName = entity.GetAttributeValue<string>("mcs_subname"),
                CountryCode = string.Join(",", countryCodes),
                CountryName = string.Join(",", countryNames),
                TypeId = string.Join(",", typeIds),
                TypeName = string.Join(",", typeNames),
                BuyerGrade = FormatOptionSetValueCollection(entity.GetAttributeValue<OptionSetValueCollection>("mcs_buyergrade")),
                DownPay = entity.GetAttributeValue<decimal>("mcs_downpay"),
                PayTerm = entity.GetAttributeValue<int>("mcs_payterm"),
                PayFreq = entity.GetAttributeValue<int>("mcs_payfreq")
            };
        }

        private string FormatOptionSetValueCollection(OptionSetValueCollection collection)
        {
            if (collection == null || collection.Count == 0) return string.Empty;
            return string.Join("/", collection.Select(o => MapBuyerGradeValueToLabel(o.Value)).OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        }

        private string MapBuyerGradeValueToLabel(int value)
        {
            switch (value)
            {
                case 100000000: return "S";
                case 100000001: return "A";
                case 100000002: return "B";
                case 100000003: return "C";
                case 100000004: return "I";
                case 100000005: return "D1";
                case 100000006: return "D2";
                case 100000007: return "D3";
                case 100000008: return "D4";
                case 100000009: return "D5";
                default: return value.ToString();
            }
        }

        #endregion

        /// <summary>
        /// 判断是否为泵路事业部
        /// </summary>
        private bool IsPumpBusinessUnit(string buId)
        {
            return !string.IsNullOrWhiteSpace(buId) &&
                   buId.Equals(PumbuBusinessUnitCode, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 将查询结果序列化为 JSON（完整包装结构）
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

        /// <summary>
        /// 将记录列表序列化为 JSON 数组（records 输出参数用）
        /// </summary>
        public static string SerializeRecords(List<TradeStPayTermRecord> records)
        {
            return JsonConvert.SerializeObject(records ?? new List<TradeStPayTermRecord>(), new JsonSerializerSettings
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

    public class CountryInfo
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class TradeTypeInfo
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
