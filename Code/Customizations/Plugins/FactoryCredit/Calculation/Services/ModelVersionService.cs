using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace SanyD365.Plugins.FactoryCredit.Calculation.Services
{
    /// <summary>
    /// 模型版本选择服务
    /// </summary>
    public class ModelVersionService
    {
        private readonly IOrganizationService _service;
        private readonly ITracingService _tracer;

        public ModelVersionService(IOrganizationService service, ITracingService tracer)
        {
            _service = service;
            _tracer = tracer;
        }

        /// <summary>
        /// 获取当前有效的最新模型版本
        /// </summary>
        public Entity GetActiveVersion()
        {
            try
            {
                DateTime now = DateTime.UtcNow;

                QueryExpression query = new QueryExpression("mcs_fca_mdlversion")
                {
                    ColumnSet = new ColumnSet("mcs_versionid", "mcs_modeldesc"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("mcs_isactive", ConditionOperator.Equal, 1),
                            new ConditionExpression("mcs_validfrom", ConditionOperator.LessEqual, now),
                            new ConditionExpression("mcs_validend", ConditionOperator.GreaterEqual, now)
                        }
                    },
                    Orders =
                    {
                        new OrderExpression("createdon", OrderType.Descending)
                    },
                    TopCount = 1
                };

                EntityCollection result = _service.RetrieveMultiple(query);
                if (result.Entities.Count > 0)
                {
                    Entity version = result.Entities[0];
                    string versionCode = version.GetAttributeValue<string>("mcs_versionid");
                    string desc = version.GetAttributeValue<string>("mcs_modeldesc");
                    _tracer.Trace($"选中模型版本: ID={version.Id}, 编码={versionCode}, 描述={desc}");
                    return version;
                }
            }
            catch (Exception ex)
            {
                _tracer.Trace($"获取模型版本失败: {ex.Message}");
            }

            return null;
        }
    }
}
