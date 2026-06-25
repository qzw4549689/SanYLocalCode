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

        public CreditRecordExpirationService(ServiceClient service, ILogger<CreditRecordExpirationService>? logger = null)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _logger = logger ?? new NullCreditRecordExpirationLogger();
        }

        /// <summary>
        /// 预览待失效记录数量，不修改数据。
        /// </summary>
        /// <param name="expireAfterDays">过期天数，默认 365 天</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>待失效记录数量</returns>
        public async Task<int> PreviewCountAsync(int expireAfterDays = 365, CancellationToken cancellationToken = default)
        {
            var expireBeforeDate = DateTime.UtcNow.Date.AddDays(-expireAfterDays);
            _logger.LogInformation("预览：审批日期早于 {ExpireBeforeDate:yyyy-MM-dd} 的过期评估记录", expireBeforeDate);

            var expiredRecordIds = await QueryExpiredRecordIdsAsync(expireBeforeDate, cancellationToken);
            _logger.LogInformation("预览完成：发现 {Count} 条待失效记录", expiredRecordIds.Count);

            return expiredRecordIds.Count;
        }

        /// <summary>
        /// 执行过期处理。
        /// </summary>
        /// <param name="expireAfterDays">过期天数，默认 365 天</param>
        /// <param name="batchSize">每批处理数量，默认 200</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>实际失效的记录数</returns>
        public async Task<int> ExpireAsync(int expireAfterDays = 365, int batchSize = 200, CancellationToken cancellationToken = default)
        {
            if (expireAfterDays <= 0)
            {
                throw new ArgumentException("过期天数必须大于 0", nameof(expireAfterDays));
            }

            var expireBeforeDate = DateTime.UtcNow.Date.AddDays(-expireAfterDays);
            _logger.LogInformation("开始处理审批日期早于 {ExpireBeforeDate:yyyy-MM-dd} 的过期评估记录", expireBeforeDate);

            var expiredRecordIds = await QueryExpiredRecordIdsAsync(expireBeforeDate, cancellationToken);

            if (expiredRecordIds.Count == 0)
            {
                _logger.LogInformation("未发现过期评估记录");
                return 0;
            }

            _logger.LogInformation("发现 {Count} 条过期评估记录", expiredRecordIds.Count);

            var expiredCount = 0;
            foreach (var batch in expiredRecordIds.Chunk(batchSize))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var batchList = batch.ToList();
                var expiredInBatch = await ExpireBatchAsync(batchList, cancellationToken);
                expiredCount += expiredInBatch;

                _logger.LogInformation("已处理 {Processed}/{Total} 条", expiredCount, expiredRecordIds.Count);
            }

            _logger.LogInformation("过期处理完成，共失效 {Count} 条评估记录", expiredCount);
            return expiredCount;
        }

        /// <summary>
        /// 查询所有已审批通过且超过过期天数的评估记录 ID。
        /// </summary>
        private async Task<List<Guid>> QueryExpiredRecordIdsAsync(DateTime expireBeforeDate, CancellationToken cancellationToken)
        {
            var query = new QueryExpression(EntityName)
            {
                ColumnSet = new ColumnSet($"{EntityName}id"),
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

            var result = new List<Guid>();

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var response = await Task.Run(() => _service.RetrieveMultiple(query), cancellationToken);

                foreach (var entity in response.Entities)
                {
                    result.Add(entity.Id);
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
    }
}
