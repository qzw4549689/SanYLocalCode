using D365ToolCommon.Connection;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;

namespace Peter.ServiceJobs.CreditRecordExpiration
{
    /// <summary>
    /// 信用评估记录过期处理服务。
    /// 业务规则：BPP 审批通过日期超过 365 天的评估记录，自动将有效状态置为失效。
    /// </summary>
    public class CreditRecordExpirationService
    {
        private readonly ServiceClient _service;
        private readonly ILogger<CreditRecordExpirationService> _logger;

        // 常量定义
        private const string EntityName = "mcs_credit_record";
        private const string ActiveField = "mcs_active";
        private const string ApproveDateField = "mcs_approvedate";
        private const string AccountField = "mcs_accountid";
        private const string CustomerMasterDataEntityName = "mcs_customermasterdata";
        private const string CreditValidField = "mcs_creditvalid";
        private const string CustomerMasterDataAccountField = "mcs_customermasterdataid";

        public CreditRecordExpirationService(ServiceClient service, ILogger<CreditRecordExpirationService>? logger = null)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _logger = logger ?? new NullCreditRecordExpirationLogger();
        }

        /// <summary>
        /// 预览待失效记录数量及受影响的客户主数据数量，不修改数据。
        /// </summary>
        /// <param name="expireAfterDays">过期天数，默认 365 天</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>待失效记录数量及受影响客户主数据数量</returns>
        public async Task<ExpirationPreviewResult> PreviewAsync(int expireAfterDays = 365, CancellationToken cancellationToken = default)
        {
            var expireBeforeDate = DateTime.UtcNow.Date.AddDays(-expireAfterDays);
            _logger.LogInformation("预览：审批日期早于 {ExpireBeforeDate:yyyy-MM-dd} 的过期评估记录", expireBeforeDate);

            var expiredRecords = await QueryExpiredRecordsAsync(expireBeforeDate, cancellationToken);
            var affectedAccountIds = expiredRecords
                .Where(r => r.AccountId.HasValue)
                .Select(r => r.AccountId!.Value)
                .Distinct()
                .ToList();

            var affectedMasterDataCount = await CountAffectedCustomerMasterDataAsync(
                affectedAccountIds, expireBeforeDate, cancellationToken);

            _logger.LogInformation(
                "预览完成：发现 {RecordCount} 条待失效评估记录，涉及 {AccountCount} 个客户，预计 {MasterDataCount} 个客户主数据会被标记为失效",
                expiredRecords.Count,
                affectedAccountIds.Count,
                affectedMasterDataCount);

            return new ExpirationPreviewResult
            {
                ExpiredRecordCount = expiredRecords.Count,
                AffectedAccountCount = affectedAccountIds.Count,
                AffectedCustomerMasterDataCount = affectedMasterDataCount
            };
        }

        /// <summary>
        /// 预览待失效记录数量（兼容旧接口）。
        /// </summary>
        public async Task<int> PreviewCountAsync(int expireAfterDays = 365, CancellationToken cancellationToken = default)
        {
            var result = await PreviewAsync(expireAfterDays, cancellationToken);
            return result.ExpiredRecordCount;
        }

        /// <summary>
        /// 执行过期处理。
        /// </summary>
        /// <param name="expireAfterDays">过期天数，默认 365 天</param>
        /// <param name="batchSize">每批处理数量，默认 200</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>实际失效的记录数及被标记为失效的客户主数据数</returns>
        public async Task<ExpirationResult> ExpireAsync(int expireAfterDays = 365, int batchSize = 200, CancellationToken cancellationToken = default)
        {
            if (expireAfterDays <= 0)
            {
                throw new ArgumentException("过期天数必须大于 0", nameof(expireAfterDays));
            }

            var expireBeforeDate = DateTime.UtcNow.Date.AddDays(-expireAfterDays);
            _logger.LogInformation("开始处理审批日期早于 {ExpireBeforeDate:yyyy-MM-dd} 的过期评估记录", expireBeforeDate);

            var expiredRecords = await QueryExpiredRecordsAsync(expireBeforeDate, cancellationToken);

            if (expiredRecords.Count == 0)
            {
                _logger.LogInformation("未发现过期评估记录");
                return new ExpirationResult { ExpiredRecordCount = 0, ExpiredCustomerMasterDataCount = 0 };
            }

            _logger.LogInformation("发现 {Count} 条过期评估记录", expiredRecords.Count);

            var affectedAccountIds = expiredRecords
                .Where(r => r.AccountId.HasValue)
                .Select(r => r.AccountId!.Value)
                .Distinct()
                .ToList();

            var expiredCount = 0;
            foreach (var batch in expiredRecords.Select(r => r.CreditRecordId).Chunk(batchSize))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var batchList = batch.ToList();
                var expiredInBatch = await ExpireBatchAsync(batchList, cancellationToken);
                expiredCount += expiredInBatch;

                _logger.LogInformation("已处理 {Processed}/{Total} 条评估记录", expiredCount, expiredRecords.Count);
            }

            _logger.LogInformation("评估记录过期处理完成，共失效 {Count} 条", expiredCount);

            // 同步更新客户主数据信用评估有效状态
            var expiredMasterDataCount = await ExpireCustomerMasterDataAsync(
                affectedAccountIds, expireBeforeDate, cancellationToken);

            _logger.LogInformation(
                "客户主数据同步完成，共将 {Count} 个客户主数据的 {Field} 标记为失效",
                expiredMasterDataCount,
                CreditValidField);

            return new ExpirationResult
            {
                ExpiredRecordCount = expiredCount,
                ExpiredCustomerMasterDataCount = expiredMasterDataCount
            };
        }

        /// <summary>
        /// 查询所有已审批通过且超过过期天数的评估记录。
        /// </summary>
        private async Task<List<ExpiredCreditRecord>> QueryExpiredRecordsAsync(DateTime expireBeforeDate, CancellationToken cancellationToken)
        {
            var query = new QueryExpression(EntityName)
            {
                ColumnSet = new ColumnSet($"{EntityName}id", AccountField),
                Criteria = new FilterExpression(LogicalOperator.And)
                {
                    Conditions =
                    {
                        new ConditionExpression(ActiveField, ConditionOperator.Equal, true),
                        new ConditionExpression(ApproveDateField, ConditionOperator.NotNull),
                        new ConditionExpression(ApproveDateField, ConditionOperator.LessThan, expireBeforeDate)
                    }
                },
                PageInfo = new PagingInfo
                {
                    Count = 5000,
                    PageNumber = 1
                }
            };

            var result = new List<ExpiredCreditRecord>();

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var response = await Task.Run(() => _service.RetrieveMultiple(query), cancellationToken);

                foreach (var entity in response.Entities)
                {
                    Guid? accountId = null;
                    if (entity.Contains(AccountField) && entity[AccountField] is EntityReference accountRef)
                    {
                        accountId = accountRef.Id;
                    }

                    result.Add(new ExpiredCreditRecord(entity.Id, accountId));
                }

                if (response.MoreRecords)
                {
                    query.PageInfo.PageNumber++;
                    query.PageInfo.PagingCookie = response.PagingCookie;
                }
                else
                {
                    break;
                }
            }

            return result;
        }

        /// <summary>
        /// 批量失效一批评估记录。
        /// </summary>
        private async Task<int> ExpireBatchAsync(List<Guid> recordIds, CancellationToken cancellationToken)
        {
            if (recordIds.Count == 0)
            {
                return 0;
            }

            var updateRequests = new ExecuteMultipleRequest
            {
                Settings = new ExecuteMultipleSettings
                {
                    ContinueOnError = true,
                    ReturnResponses = true
                },
                Requests = new OrganizationRequestCollection()
            };

            foreach (var recordId in recordIds)
            {
                var update = new Entity(EntityName, recordId)
                {
                    [ActiveField] = false
                };

                updateRequests.Requests.Add(new UpdateRequest { Target = update });
            }

            var response = (ExecuteMultipleResponse)await Task.Run(
                () => _service.Execute(updateRequests),
                cancellationToken);

            var successCount = 0;
            var failedCount = 0;

            foreach (var item in response.Responses)
            {
                if (item.Fault != null)
                {
                    failedCount++;
                    _logger.LogError("更新评估记录失败: {Fault}", item.Fault.Message);
                }
                else
                {
                    successCount++;
                }
            }

            if (failedCount > 0)
            {
                _logger.LogWarning("本批处理完成: 成功 {Success} 条, 失败 {Failed} 条", successCount, failedCount);
            }

            return successCount;
        }

        /// <summary>
        /// 将受影响客户中已无其他有效评估记录的客户主数据标记为失效。
        /// 注意：仅当客户不存在其他未过期且有效的评估记录时，才将 mcs_creditvalid 设为 false。
        /// </summary>
        private async Task<int> ExpireCustomerMasterDataAsync(
            List<Guid> affectedAccountIds,
            DateTime expireBeforeDate,
            CancellationToken cancellationToken)
        {
            if (affectedAccountIds.Count == 0)
            {
                return 0;
            }

            var expiredMasterDataIds = new List<Guid>();

            foreach (var accountId in affectedAccountIds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var customerMasterDataId = await GetCustomerMasterDataIdAsync(accountId, cancellationToken);
                if (!customerMasterDataId.HasValue)
                {
                    _logger.LogWarning("客户 {AccountId} 未关联客户主数据，跳过", accountId);
                    continue;
                }

                var hasValidRecord = await HasValidCreditRecordAsync(accountId, expireBeforeDate, cancellationToken);
                if (hasValidRecord)
                {
                    _logger.LogInformation(
                        "客户 {AccountId} 仍存在未过期的有效评估记录，保持客户主数据 {MasterDataId} 的信用评估有效状态",
                        accountId,
                        customerMasterDataId.Value);
                    continue;
                }

                expiredMasterDataIds.Add(customerMasterDataId.Value);
            }

            if (expiredMasterDataIds.Count == 0)
            {
                return 0;
            }

            var updateRequests = new ExecuteMultipleRequest
            {
                Settings = new ExecuteMultipleSettings
                {
                    ContinueOnError = true,
                    ReturnResponses = true
                },
                Requests = new OrganizationRequestCollection()
            };

            foreach (var masterDataId in expiredMasterDataIds)
            {
                var update = new Entity(CustomerMasterDataEntityName, masterDataId)
                {
                    [CreditValidField] = false
                };

                updateRequests.Requests.Add(new UpdateRequest { Target = update });
            }

            var response = (ExecuteMultipleResponse)await Task.Run(
                () => _service.Execute(updateRequests),
                cancellationToken);

            var successCount = 0;
            var failedCount = 0;

            foreach (var item in response.Responses)
            {
                if (item.Fault != null)
                {
                    failedCount++;
                    _logger.LogError("更新客户主数据失败: {Fault}", item.Fault.Message);
                }
                else
                {
                    successCount++;
                }
            }

            if (failedCount > 0)
            {
                _logger.LogWarning("客户主数据批量更新完成: 成功 {Success} 条, 失败 {Failed} 条", successCount, failedCount);
            }

            return successCount;
        }

        /// <summary>
        /// 统计受影响客户中会被标记为失效的客户主数据数量（预览用）。
        /// </summary>
        private async Task<int> CountAffectedCustomerMasterDataAsync(
            List<Guid> affectedAccountIds,
            DateTime expireBeforeDate,
            CancellationToken cancellationToken)
        {
            var count = 0;

            foreach (var accountId in affectedAccountIds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var customerMasterDataId = await GetCustomerMasterDataIdAsync(accountId, cancellationToken);
                if (!customerMasterDataId.HasValue)
                {
                    continue;
                }

                var hasValidRecord = await HasValidCreditRecordAsync(accountId, expireBeforeDate, cancellationToken);
                if (!hasValidRecord)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// 通过 account 查找关联的 mcs_customermasterdata ID。
        /// </summary>
        private async Task<Guid?> GetCustomerMasterDataIdAsync(Guid accountId, CancellationToken cancellationToken)
        {
            var account = await Task.Run(() => _service.Retrieve("account", accountId,
                new ColumnSet("mcs_customermasterdata")), cancellationToken);

            if (!account.Contains("mcs_customermasterdata") || !(account["mcs_customermasterdata"] is EntityReference masterDataRef))
            {
                return null;
            }

            return masterDataRef.Id;
        }

        /// <summary>
        /// 分析评分卡配置：按客户类型和分类汇总权重，帮助排查总分不等于 100 的问题。
        /// </summary>
        public async Task AnalyzeScoringCardsAsync(CancellationToken cancellationToken = default)
        {
            var categoryLabels = new Dictionary<int, string>
            {
                { 1, "SA Existing Customer" },
                { 2, "SA New Customer" },
                { 3, "BC Existing Customer" },
                { 4, "BC New Customer" },
                { 5, "Individual Customer" },
                { 6, "Existing Dealer" },
                { 7, "New Dealer" }
            };

            var typeLabels = new Dictionary<int, string>
            {
                { 1, "Customer Strength" },
                { 2, "Customer Financial" },
                { 3, "Macro Market" },
                { 4, "Historical Transaction" }
            };

            // 先查询评分项目的数据类型（mcs_credit_itemsno 是评分项目编码）
            var itemDataTypes = await QueryCreditItemDataTypesAsync(cancellationToken);

            var query = new QueryExpression("mcs_credit_scoringcard")
            {
                ColumnSet = new ColumnSet(
                    "mcs_credit_scoringcardid",
                    "mcs_cardname",
                    "mcs_categoryid",
                    "mcs_typeid",
                    "mcs_credititem",
                    "mcs_itemid",
                    "mcs_weight",
                    "mcs_minvalue",
                    "mcs_maxvalue"),
                PageInfo = new PagingInfo { Count = 5000, PageNumber = 1 }
            };

            var records = new List<Entity>();
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var response = await Task.Run(() => _service.RetrieveMultiple(query), cancellationToken);
                records.AddRange(response.Entities);

                if (response.MoreRecords)
                {
                    query.PageInfo.PageNumber++;
                    query.PageInfo.PagingCookie = response.PagingCookie;
                }
                else
                {
                    break;
                }
            }

            _logger.LogInformation("共查询到 {Count} 条评分卡配置记录", records.Count);

            var grouped = records
                .GroupBy(r => r.GetAttributeValue<OptionSetValue>("mcs_categoryid")?.Value ?? 0)
                .OrderBy(g => g.Key);

            foreach (var categoryGroup in grouped)
            {
                var categoryLabel = categoryLabels.ContainsKey(categoryGroup.Key)
                    ? categoryLabels[categoryGroup.Key]
                    : $"Unknown({categoryGroup.Key})";

                var totalWeight = categoryGroup.Sum(r => r.GetAttributeValue<int>("mcs_weight"));
                var quantitativeWeight = categoryGroup
                    .Where(r => IsQuantitative(r.GetAttributeValue<string>("mcs_itemid"), itemDataTypes))
                    .Sum(r => r.GetAttributeValue<int>("mcs_weight"));
                var qualitativeWeight = categoryGroup
                    .Where(r => !IsQuantitative(r.GetAttributeValue<string>("mcs_itemid"), itemDataTypes))
                    .Sum(r => r.GetAttributeValue<int>("mcs_weight"));

                Console.WriteLine($"\n===== {categoryLabel} (category={categoryGroup.Key}) =====");
                Console.WriteLine($"总权重: {totalWeight}");
                Console.WriteLine($"  定量权重: {quantitativeWeight}");
                Console.WriteLine($"  定性权重: {qualitativeWeight}");

                var typeGroups = categoryGroup
                    .GroupBy(r => r.GetAttributeValue<OptionSetValue>("mcs_typeid")?.Value ?? 0)
                    .OrderBy(g => g.Key);

                foreach (var typeGroup in typeGroups)
                {
                    var typeLabel = typeLabels.ContainsKey(typeGroup.Key)
                        ? typeLabels[typeGroup.Key]
                        : $"Unknown({typeGroup.Key})";
                    var typeWeight = typeGroup.Sum(r => r.GetAttributeValue<int>("mcs_weight"));
                    var typeQuantitativeWeight = typeGroup
                        .Where(r => IsQuantitative(r.GetAttributeValue<string>("mcs_itemid"), itemDataTypes))
                        .Sum(r => r.GetAttributeValue<int>("mcs_weight"));
                    var typeQualitativeWeight = typeGroup
                        .Where(r => !IsQuantitative(r.GetAttributeValue<string>("mcs_itemid"), itemDataTypes))
                        .Sum(r => r.GetAttributeValue<int>("mcs_weight"));

                    Console.WriteLine($"  [{typeLabel}] 小计: {typeWeight} (定量 {typeQuantitativeWeight}, 定性 {typeQualitativeWeight})");

                    foreach (var record in typeGroup.OrderBy(r => r.GetAttributeValue<string>("mcs_itemid")))
                    {
                        var itemId = record.GetAttributeValue<string>("mcs_itemid") ?? "(空)";
                        var itemName = record.GetAttributeValue<EntityReference>("mcs_credititem")?.Name ?? "(空)";
                        var weight = record.GetAttributeValue<int>("mcs_weight");
                        var minValue = record.GetAttributeValue<decimal?>("mcs_minvalue");
                        var maxValue = record.GetAttributeValue<decimal?>("mcs_maxvalue");
                        var dataType = itemDataTypes.ContainsKey(itemId)
                            ? (itemDataTypes[itemId] == 100000000 ? "定量" : "定性")
                            : "未知";

                        Console.WriteLine($"    - {itemId} | {itemName} | {dataType} | weight={weight} | min={minValue} | max={maxValue}");
                    }
                }
            }
        }

        /// <summary>
        /// 查询评分项目的数据类型。
        /// </summary>
        private async Task<Dictionary<string, int>> QueryCreditItemDataTypesAsync(CancellationToken cancellationToken)
        {
            var query = new QueryExpression("mcs_credit_items")
            {
                ColumnSet = new ColumnSet("mcs_credit_itemsno", "mcs_datatype"),
                PageInfo = new PagingInfo { Count = 5000, PageNumber = 1 }
            };

            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var response = await Task.Run(() => _service.RetrieveMultiple(query), cancellationToken);

                foreach (var entity in response.Entities)
                {
                    var itemId = entity.GetAttributeValue<string>("mcs_credit_itemsno");
                    var dataType = entity.GetAttributeValue<OptionSetValue>("mcs_datatype")?.Value ?? 0;
                    if (!string.IsNullOrWhiteSpace(itemId))
                    {
                        result[itemId] = dataType;
                    }
                }

                if (response.MoreRecords)
                {
                    query.PageInfo.PageNumber++;
                    query.PageInfo.PagingCookie = response.PagingCookie;
                }
                else
                {
                    break;
                }
            }

            return result;
        }

        /// <summary>
        /// 判断评分项目是否为定量。
        /// </summary>
        private bool IsQuantitative(string? itemId, Dictionary<string, int> itemDataTypes)
        {
            if (string.IsNullOrWhiteSpace(itemId)) return false;
            return itemDataTypes.TryGetValue(itemId, out var dataType) && dataType == 100000000;
        }

        /// <summary>
        /// 创建测试数据：将指定评估记录的 mcs_active 设为 true，mcs_approvedate 设为 N 天前。
        /// 仅用于 DEV 测试，生产环境禁止使用。
        /// </summary>
        public async Task<bool> SeedTestDataAsync(string scoreId, int daysAgo = 366, CancellationToken cancellationToken = default)
        {
            var query = new QueryExpression(EntityName)
            {
                ColumnSet = new ColumnSet($"{EntityName}id", "mcs_scoreid"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("mcs_scoreid", ConditionOperator.Equal, scoreId) }
                },
                PageInfo = new PagingInfo { Count = 1, PageNumber = 1 }
            };

            var response = await Task.Run(() => _service.RetrieveMultiple(query), cancellationToken);
            if (response.Entities.Count == 0)
            {
                _logger.LogWarning("未找到评估记录: {ScoreId}", scoreId);
                return false;
            }

            var recordId = response.Entities[0].Id;
            var approveDate = DateTime.UtcNow.Date.AddDays(-daysAgo);

            var update = new Entity(EntityName, recordId)
            {
                [ActiveField] = true,
                [ApproveDateField] = approveDate
            };

            await Task.Run(() => _service.Update(update), cancellationToken);

            _logger.LogInformation(
                "已创建测试数据: {ScoreId} ({RecordId}) 的 {ActiveField}=true, {ApproveDateField}={ApproveDate:yyyy-MM-dd}",
                scoreId,
                recordId,
                ActiveField,
                ApproveDateField,
                approveDate);

            return true;
        }

        /// <summary>
        /// 诊断统计：输出当前环境中评估记录有效状态分布。
        /// </summary>
        public async Task<DiagnoseResult> DiagnoseAsync(int expireAfterDays = 365, CancellationToken cancellationToken = default)
        {
            var expireBeforeDate = DateTime.UtcNow.Date.AddDays(-expireAfterDays);

            var totalQuery = new QueryExpression(EntityName)
            {
                ColumnSet = new ColumnSet($"{EntityName}id"),
                PageInfo = new PagingInfo { Count = 1, PageNumber = 1 }
            };

            var activeQuery = new QueryExpression(EntityName)
            {
                ColumnSet = new ColumnSet($"{EntityName}id"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression(ActiveField, ConditionOperator.Equal, true) }
                },
                PageInfo = new PagingInfo { Count = 1, PageNumber = 1 }
            };

            var withApproveDateQuery = new QueryExpression(EntityName)
            {
                ColumnSet = new ColumnSet($"{EntityName}id"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression(ActiveField, ConditionOperator.Equal, true),
                        new ConditionExpression(ApproveDateField, ConditionOperator.NotNull)
                    }
                },
                PageInfo = new PagingInfo { Count = 1, PageNumber = 1 }
            };

            var totalCount = await CountRecordsAsync(totalQuery, cancellationToken);
            var activeCount = await CountRecordsAsync(activeQuery, cancellationToken);
            var withApproveDateCount = await CountRecordsAsync(withApproveDateQuery, cancellationToken);

            var expiredRecords = await QueryExpiredRecordsAsync(expireBeforeDate, cancellationToken);

            _logger.LogInformation(
                "诊断统计: 总记录 {Total}, active=true {Active}, active=true且approvedate不为空 {WithDate}, 已过期 {Expired}",
                totalCount,
                activeCount,
                withApproveDateCount,
                expiredRecords.Count);

            return new DiagnoseResult
            {
                TotalRecordCount = totalCount,
                ActiveRecordCount = activeCount,
                ActiveWithApproveDateCount = withApproveDateCount,
                ExpiredRecordCount = expiredRecords.Count,
                ExpireBeforeDate = expireBeforeDate
            };
        }

        /// <summary>
        /// 统计查询结果数量。
        /// </summary>
        private async Task<int> CountRecordsAsync(QueryExpression query, CancellationToken cancellationToken)
        {
            var count = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var response = await Task.Run(() => _service.RetrieveMultiple(query), cancellationToken);
                count += response.Entities.Count;

                if (response.MoreRecords)
                {
                    query.PageInfo.PageNumber++;
                    query.PageInfo.PagingCookie = response.PagingCookie;
                }
                else
                {
                    break;
                }
            }

            return count;
        }

        /// <summary>
        /// 判断指定客户是否仍存在未过期且有效的评估记录。
        /// </summary>
        private async Task<bool> HasValidCreditRecordAsync(Guid accountId, DateTime expireBeforeDate, CancellationToken cancellationToken)
        {
            var query = new QueryExpression(EntityName)
            {
                ColumnSet = new ColumnSet($"{EntityName}id"),
                Criteria = new FilterExpression(LogicalOperator.And)
                {
                    Conditions =
                    {
                        new ConditionExpression(AccountField, ConditionOperator.Equal, accountId),
                        new ConditionExpression(ActiveField, ConditionOperator.Equal, true),
                        new ConditionExpression(ApproveDateField, ConditionOperator.NotNull),
                        new ConditionExpression(ApproveDateField, ConditionOperator.GreaterEqual, expireBeforeDate)
                    }
                },
                PageInfo = new PagingInfo
                {
                    Count = 1,
                    PageNumber = 1
                }
            };

            var response = await Task.Run(() => _service.RetrieveMultiple(query), cancellationToken);
            return response.Entities.Count > 0;
        }

        /// <summary>
        /// 过期评估记录摘要信息。
        /// </summary>
        private record ExpiredCreditRecord(Guid CreditRecordId, Guid? AccountId);

        /// <summary>
        /// 预览结果。
        /// </summary>
        public record ExpirationPreviewResult
        {
            public int ExpiredRecordCount { get; init; }
            public int AffectedAccountCount { get; init; }
            public int AffectedCustomerMasterDataCount { get; init; }
        }

        /// <summary>
        /// 执行结果。
        /// </summary>
        public record ExpirationResult
        {
            public int ExpiredRecordCount { get; init; }
            public int ExpiredCustomerMasterDataCount { get; init; }
        }

        /// <summary>
        /// 诊断统计结果。
        /// </summary>
        public record DiagnoseResult
        {
            public int TotalRecordCount { get; init; }
            public int ActiveRecordCount { get; init; }
            public int ActiveWithApproveDateCount { get; init; }
            public int ExpiredRecordCount { get; init; }
            public DateTime ExpireBeforeDate { get; init; }
        }
    }
}
