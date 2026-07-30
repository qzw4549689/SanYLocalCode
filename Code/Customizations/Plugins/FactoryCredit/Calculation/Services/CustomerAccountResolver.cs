using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace SanyD365.Plugins.FactoryCredit.Calculation.Services
{
    /// <summary>
    /// 根据客户主数据ID解析对应的 Account ID
    /// </summary>
    public class CustomerAccountResolver
    {
        private readonly IOrganizationService _service;
        private readonly ITracingService _tracer;

        public CustomerAccountResolver(IOrganizationService service, ITracingService tracer)
        {
            _service = service;
            _tracer = tracer;
        }

        /// <summary>
        /// 根据 mcs_customermasterdata ID 查找对应的 account ID
        /// </summary>
        public Guid? ResolveAccountId(Guid masterDataId)
        {
            try
            {
                QueryExpression query = new QueryExpression("account")
                {
                    ColumnSet = new ColumnSet("accountid"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("mcs_customermasterdata", ConditionOperator.Equal, masterDataId)
                        }
                    },
                    TopCount = 1
                };

                EntityCollection result = _service.RetrieveMultiple(query);
                if (result.Entities.Count > 0)
                {
                    Guid accountId = result.Entities[0].Id;
                    _tracer.Trace($"解析到 Account ID: {accountId}");
                    return accountId;
                }
            }
            catch (Exception ex)
            {
                _tracer.Trace($"解析 Account ID 失败: {ex.Message}");
            }

            return null;
        }
    }
}
