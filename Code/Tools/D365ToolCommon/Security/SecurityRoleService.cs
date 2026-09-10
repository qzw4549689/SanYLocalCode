using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace D365ToolCommon.Security
{
    /// <summary>
    /// 安全角色与权限服务。
    /// 提供：角色查询、角色权限查询、用户角色（直接+团队继承）、用户有效权限汇总（只读）；
    /// 以及角色权限调整（AddPrivilegesRole/RemovePrivilegeRole）、用户角色挂摘（systemuserroles）写能力。
    /// 供 MetadataTool 等工具项目的权限诊断/调整命令复用，禁止在工具中直接散落实现。
    /// </summary>
    public class SecurityRoleService
    {
        private readonly IOrganizationService _service;
        private Dictionary<Guid, string>? _privNameMapCache;

        public SecurityRoleService(IOrganizationService service)
        {
            _service = service;
        }

        /// <summary>权限深度标签（0=本人 1=本部门 2=本部门及子部门 3=组织）。</summary>
        public static string DepthLabel(int depth) => depth switch
        {
            0 => "本人",
            1 => "本部门",
            2 => "本部门及子部门",
            3 => "组织",
            _ => $"未知({depth})"
        };

        /// <summary>操作简称 → privilege 操作名映射（read→Read、write→Write...）。</summary>
        public static readonly IReadOnlyDictionary<string, string> OperationMap =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["read"] = "Read",
                ["write"] = "Write",
                ["create"] = "Create",
                ["delete"] = "Delete",
                ["append"] = "Append",
                ["appendto"] = "AppendTo",
                ["assign"] = "Assign",
                ["share"] = "Share",
            };

        /// <summary>深度别名 → PrivilegeDepth 映射（user/bu/childbu/org）。</summary>
        public static readonly IReadOnlyDictionary<string, PrivilegeDepth> DepthMap =
            new Dictionary<string, PrivilegeDepth>(StringComparer.OrdinalIgnoreCase)
            {
                ["user"] = PrivilegeDepth.Basic,
                ["bu"] = PrivilegeDepth.Local,
                ["childbu"] = PrivilegeDepth.Deep,
                ["org"] = PrivilegeDepth.Global,
            };

        /// <summary>privilege 表 id→name 全量映射（带实例缓存）。</summary>
        public Dictionary<Guid, string> BuildPrivilegeNameMap()
        {
            if (_privNameMapCache != null) return _privNameMapCache;
            var map = new Dictionary<Guid, string>();
            var query = new QueryExpression("privilege") { ColumnSet = new ColumnSet("name") };
            foreach (var p in RetrieveAllPages(query))
                map[p.Id] = p.GetAttributeValue<string>("name") ?? "";
            _privNameMapCache = map;
            return map;
        }

        /// <summary>
        /// 按名称 Like 查角色（含 BU、IsManaged），按名称排序。
        /// rootOnly=true 时只返回根 BU 副本（parentrootroleid=自身 id），与 D365 界面看到的一致；
        /// 子 BU 副本是平台自动生成的镜像，权限与根副本一致，通常无需关心。
        /// </summary>
        public List<Entity> FindRoles(string keyword, bool rootOnly = false)
        {
            var query = new QueryExpression("role")
            {
                ColumnSet = new ColumnSet("name", "businessunitid", "ismanaged", "parentrootroleid"),
                Orders = { new OrderExpression("name", OrderType.Ascending) }
            };
            if (!string.IsNullOrEmpty(keyword))
                query.Criteria.AddCondition("name", ConditionOperator.Like, $"%{keyword}%");
            var roles = RetrieveAllPages(query);
            if (rootOnly)
                roles = roles.Where(r =>
                {
                    var root = r.GetAttributeValue<EntityReference>("parentrootroleid");
                    return root == null || root.Id == r.Id;
                }).ToList();
            return roles;
        }

        /// <summary>角色的权限明细：privilege name → 深度。</summary>
        public Dictionary<string, PrivilegeDepth> GetRolePrivilegeDepths(Guid roleId)
        {
            var resp = (RetrieveRolePrivilegesRoleResponse)_service.Execute(
                new RetrieveRolePrivilegesRoleRequest { RoleId = roleId });
            var map = new Dictionary<string, PrivilegeDepth>();
            var nameMap = BuildPrivilegeNameMap();
            foreach (var rp in resp.RolePrivileges)
            {
                if (!nameMap.TryGetValue(rp.PrivilegeId, out var pname)) continue;
                map[pname] = rp.Depth;
            }
            return map;
        }

        /// <summary>按 domainname 查用户，未找到返回 null。</summary>
        public Entity? FindUser(string domainName)
        {
            var query = new QueryExpression("systemuser")
            {
                ColumnSet = new ColumnSet("fullname", "domainname", "businessunitid", "isdisabled"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("domainname", ConditionOperator.Equal, domainName) }
                }
            };
            return _service.RetrieveMultiple(query).Entities.FirstOrDefault();
        }

        /// <summary>用户的全部安全角色（直接分配 + 团队继承），去重并标注来源。</summary>
        public List<(Entity Role, string Source)> GetUserRoles(Guid userId)
        {
            // 直接分配角色（systemuserroles）
            var directQuery = new QueryExpression("role") { ColumnSet = new ColumnSet("name", "businessunitid") };
            var linkUserRoles = directQuery.AddLink("systemuserroles", "roleid", "roleid");
            linkUserRoles.LinkCriteria.AddCondition("systemuserid", ConditionOperator.Equal, userId);
            var directRoles = _service.RetrieveMultiple(directQuery).Entities;

            // 团队继承角色（teammembership → teamroles）
            var teamQuery = new QueryExpression("role") { ColumnSet = new ColumnSet("name", "businessunitid") };
            var linkTeamRoles = teamQuery.AddLink("teamroles", "roleid", "roleid");
            var linkMembership = linkTeamRoles.AddLink("teammembership", "teamid", "teamid");
            linkMembership.LinkCriteria.AddCondition("systemuserid", ConditionOperator.Equal, userId);
            var teamRoles = _service.RetrieveMultiple(teamQuery).Entities;

            return directRoles.Select(r => (Role: r, Source: "直接"))
                .Concat(teamRoles.Select(r => (Role: r, Source: "团队")))
                .GroupBy(x => x.Role.Id)
                .Select(g => g.First())
                .ToList();
        }

        /// <summary>用户有效权限汇总（RetrieveUserPrivileges，含直接+团队继承角色）：privilege name → 最大深度。</summary>
        public Dictionary<string, int> GetUserEffectivePrivileges(Guid userId)
        {
            var resp = (RetrieveUserPrivilegesResponse)_service.Execute(
                new RetrieveUserPrivilegesRequest { UserId = userId });
            var nameMap = BuildPrivilegeNameMap();
            var map = new Dictionary<string, int>();
            foreach (var rp in resp.RolePrivileges)
            {
                if (!nameMap.TryGetValue(rp.PrivilegeId, out var pname)) continue;
                var depth = (int)rp.Depth;
                if (!map.ContainsKey(pname) || map[pname] < depth) map[pname] = depth;
            }
            return map;
        }

        /// <summary>privilege name → PrivilegeId，查不到返回 null。</summary>
        public Guid? FindPrivilegeId(string privilegeName)
        {
            var map = BuildPrivilegeNameMap();
            foreach (var kv in map)
                if (string.Equals(kv.Value, privilegeName, StringComparison.OrdinalIgnoreCase))
                    return kv.Key;
            return null;
        }

        /// <summary>
        /// 【写】设置角色权限：depth 为 null 时移除该权限（RemovePrivilegeRole），
        /// 否则添加/覆盖为目标深度（AddPrivilegesRole）。幂等：当前已是目标状态时返回 false 不改动。
        /// </summary>
        /// <returns>是否发生了变更</returns>
        public bool SetRolePrivilege(Guid roleId, string privilegeName, PrivilegeDepth? depth)
        {
            var privilegeId = FindPrivilegeId(privilegeName)
                ?? throw new InvalidOperationException($"环境中不存在权限 {privilegeName}（该实体可能不支持此权限类型）");

            var current = GetRolePrivilegeDepths(roleId);
            var hasCurrent = current.TryGetValue(privilegeName, out var currentDepth);

            if (depth == null)
            {
                if (!hasCurrent) return false; // 本就无此权限
                _service.Execute(new RemovePrivilegeRoleRequest
                {
                    RoleId = roleId,
                    PrivilegeId = privilegeId
                });
                return true;
            }

            if (hasCurrent && currentDepth == depth.Value) return false; // 已是目标深度
            _service.Execute(new AddPrivilegesRoleRequest
            {
                RoleId = roleId,
                Privileges = new[]
                {
                    new RolePrivilege { PrivilegeId = privilegeId, Depth = depth.Value }
                }
            });
            return true;
        }

        /// <summary>【写】给用户分配角色（systemuserroles Associate）。幂等：已分配返回 false。</summary>
        public bool AssignRole(Guid userId, Guid roleId)
        {
            if (UserHasRole(userId, roleId)) return false;
            _service.Associate("systemuser", userId,
                new Relationship("systemuserroles"),
                new EntityReferenceCollection { new EntityReference("role", roleId) });
            return true;
        }

        /// <summary>【写】移除用户的角色（systemuserroles Disassociate）。幂等：未分配返回 false。</summary>
        public bool RemoveRole(Guid userId, Guid roleId)
        {
            if (!UserHasRole(userId, roleId)) return false;
            _service.Disassociate("systemuser", userId,
                new Relationship("systemuserroles"),
                new EntityReferenceCollection { new EntityReference("role", roleId) });
            return true;
        }

        /// <summary>用户是否已直接分配指定角色。</summary>
        public bool UserHasRole(Guid userId, Guid roleId)
        {
            var query = new QueryExpression("systemuserroles")
            {
                ColumnSet = new ColumnSet("systemuserroleid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("systemuserid", ConditionOperator.Equal, userId),
                        new ConditionExpression("roleid", ConditionOperator.Equal, roleId)
                    }
                }
            };
            return _service.RetrieveMultiple(query).Entities.Count > 0;
        }

        private List<Entity> RetrieveAllPages(QueryExpression query)
        {
            var all = new List<Entity>();
            query.PageInfo = new PagingInfo { Count = 5000, PageNumber = 1 };
            while (true)
            {
                var result = _service.RetrieveMultiple(query);
                all.AddRange(result.Entities);
                if (!result.MoreRecords) break;
                query.PageInfo.PageNumber++;
                query.PageInfo.PagingCookie = result.PagingCookie;
            }
            return all;
        }
    }
}
