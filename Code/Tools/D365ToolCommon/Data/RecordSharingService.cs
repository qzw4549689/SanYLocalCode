using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace D365ToolCommon.Data
{
    /// <summary>
    /// 记录共享服务（禅道 #1151）
    /// 按记录上的事业部 Lookup 字段，将记录共享（Read+Write）给 mcs_bu.mcs_buteamid（BU的团队）。
    /// 供工具项目做成交条件样板库「提交审批共享」的验证与存量补共享。
    /// </summary>
    public class RecordSharingService
    {
        private readonly IOrganizationService _service;

        public RecordSharingService(IOrganizationService service)
        {
            _service = service;
        }

        /// <summary>
        /// 将记录共享（Read+Write）给其事业部对应的 BU 团队。GrantAccess 幂等，重复调用安全。
        /// </summary>
        /// <param name="entityName">记录实体逻辑名</param>
        /// <param name="recordId">记录 ID</param>
        /// <param name="buLookupField">记录上的事业部 Lookup 字段逻辑名</param>
        /// <param name="message">结果说明</param>
        /// <returns>共享的 BU 团队；失败返回 null</returns>
        public EntityReference? ShareToBuTeam(string entityName, Guid recordId, string buLookupField, out string message)
        {
            var record = _service.Retrieve(entityName, recordId, new ColumnSet(buLookupField));
            var buRef = record.GetAttributeValue<EntityReference>(buLookupField);
            if (buRef == null)
            {
                message = "记录未设置事业部";
                return null;
            }

            var bu = _service.Retrieve("mcs_bu", buRef.Id, new ColumnSet("mcs_buteamid"));
            var teamRef = bu.GetAttributeValue<EntityReference>("mcs_buteamid");
            if (teamRef == null)
            {
                message = $"事业部【{buRef.Name}】未维护 BU 团队（mcs_buteamid）";
                return null;
            }

            _service.Execute(new GrantAccessRequest
            {
                Target = new EntityReference(entityName, recordId),
                PrincipalAccess = new PrincipalAccess
                {
                    Principal = teamRef,
                    AccessMask = AccessRights.ReadAccess | AccessRights.WriteAccess
                }
            });

            message = $"已共享给团队 {teamRef.Name}({teamRef.Id})";
            return teamRef;
        }

        /// <summary>
        /// 查询记录是否已共享给指定团队（POA 表）
        /// </summary>
        /// <param name="recordId">记录 ID</param>
        /// <param name="teamId">团队 ID</param>
        /// <param name="accessMask">共享权限掩码（如 3 = Read+Write）</param>
        public bool HasShare(Guid recordId, Guid teamId, out int accessMask)
        {
            accessMask = 0;
            var query = new QueryExpression("principalobjectaccess")
            {
                ColumnSet = new ColumnSet("accessrightsmask"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("objectid", ConditionOperator.Equal, recordId),
                        new ConditionExpression("principalid", ConditionOperator.Equal, teamId)
                    }
                }
            };

            var result = _service.RetrieveMultiple(query);
            if (result.Entities.Count == 0) return false;

            accessMask = result.Entities[0].GetAttributeValue<int>("accessrightsmask");
            return true;
        }

        /// <summary>
        /// 批量将符合条件的记录共享给各自事业部的 BU 团队（存量补共享）
        /// </summary>
        /// <param name="entityName">记录实体逻辑名</param>
        /// <param name="buLookupField">记录上的事业部 Lookup 字段逻辑名</param>
        /// <param name="filter">记录过滤条件</param>
        /// <returns>(成功数, 跳过数, 明细)</returns>
        public (int Success, int Skipped, List<string> Details) ShareRecordsToBuTeam(
            string entityName, string buLookupField, FilterExpression filter)
        {
            var query = new QueryExpression(entityName)
            {
                ColumnSet = new ColumnSet(buLookupField),
                Criteria = filter
            };

            var records = _service.RetrieveMultiple(query);
            int success = 0, skipped = 0;
            var details = new List<string>();

            foreach (var record in records.Entities)
            {
                try
                {
                    var teamRef = ShareToBuTeam(entityName, record.Id, buLookupField, out string message);
                    if (teamRef != null)
                    {
                        success++;
                        details.Add($"  ✅ {record.Id}: {message}");
                    }
                    else
                    {
                        skipped++;
                        details.Add($"  ⏭️ {record.Id}: {message}");
                    }
                }
                catch (Exception ex)
                {
                    skipped++;
                    details.Add($"  ❌ {record.Id}: {ex.Message}");
                }
            }

            return (success, skipped, details);
        }
    }
}
