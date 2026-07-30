using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SanyD365.Plugins.TradeStPayTerm
{
    /// <summary>
    /// 成交条件样板库 - 保存校验 Plugin
    /// 触发时机：Create/Update PreOperation
    /// </summary>
    public class TradeStPayTermValidationPlugin : IPlugin
    {
        // 角色权限（2026-07-20 按业务角色矩阵）：按 D365 安全角色"名称"匹配，环境中角色改名需同步修改
        // 注意：仅检查直接分配给用户的角色（systemuserroles），不含通过团队继承的角色
        // 2026-07-24 禅道 #1151：角色名按环境实际创建改为英文
        private const string RoleCreator = "LTC Risk Control Configuration Admin";    // 成交条件制定人：发起配置/申请审批
        private const string RoleApprover = "LTC Regional Overseas Risk Director"; // 成交条件审批人：审核配置数据
        private const string RoleAdmin = "System Administrator"; // 系统管理员放行

        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = factory.CreateOrganizationService(context.UserId);
            ITracingService tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            // 系统身份服务：用于查询用户角色（普通用户可能无 role 实体读权限）
            // 注意：本行必须紧跟上方 4 行初始化代码之后，sync-plugin-to-remote.py 依赖该顺序做远程转换
            IOrganizationService systemService = factory.CreateOrganizationService(null);

            tracer.Trace("TradeStPayTermValidationPlugin 开始执行");

            if (!context.InputParameters.Contains("Target") || !(context.InputParameters["Target"] is Entity))
            {
                tracer.Trace("未找到 Target 实体");
                return;
            }

            Entity target = (Entity)context.InputParameters["Target"];

            if (target.LogicalName != "mcs_trade_stpayterm")
            {
                tracer.Trace($"实体不匹配: {target.LogicalName}");
                return;
            }

            try
            {
                ValidateBusinessRules(target, context, service, systemService, tracer);
            }
            catch (Exception ex)
            {
                tracer.Trace($"校验失败: {ex.Message}");
                throw new InvalidPluginExecutionException($"保存失败: {ex.Message}");
            }
        }

        private void ValidateBusinessRules(Entity target, IPluginExecutionContext context, IOrganizationService service, IOrganizationService systemService, ITracingService tracer)
        {
            // 0. 创建时状态默认值 = 0（未生效）
            if (context.MessageName == "Create" && (!target.Contains("mcs_status") || target["mcs_status"] == null))
            {
                target["mcs_status"] = new OptionSetValue(0);
            }

            // 0.5 Excel 导入/手工录入兼容：国家/产品分类「名称 → GUID」解析
            // 用户只会填名称（mcs_countryname/mcs_typename），自定义多选控件依赖 GUID 字段（mcs_countries/mcs_trade_type）显示
            // 必须在重复校验之前解析，校验逻辑使用解析后的 GUID
            ResolveLookupNameToGuid(target, context, service, tracer,
                "mcs_countryname", "mcs_countries", "mcs_countrycode",
                "mcs_country", "mcs_name", "mcs_countrycode", "国家", "mcs_chinesename");
            ResolveLookupNameToGuid(target, context, service, tracer,
                "mcs_typename", "mcs_trade_type", "mcs_typeid",
                "mcs_trade_pttype", "mcs_trade_pttypename", "mcs_typeid", "产品分类", null);

            // 0.6 必填字段服务端兜底校验（2026-07-27 新增）
            // 元数据 ApplicationRequired 仅表单客户端强制，Excel 导入/API Create 不经过表单可绕过，必须服务端兜底
            ValidateRequiredFields(target, context, service, tracer);

            // 1. 首付款比例校验
            if (target.Contains("mcs_downpay"))
            {
                decimal downPay = target.GetAttributeValue<decimal>("mcs_downpay");
                if (downPay < 0 || downPay > 1)
                {
                    throw new InvalidPluginExecutionException("首付款比例必须在 0% 到 100% 之间");
                }

                // 100% 首付款一致性校验
                if (downPay == 1)
                {
                    if (target.Contains("mcs_payterm") && target.GetAttributeValue<int>("mcs_payterm") != 0)
                    {
                        throw new InvalidPluginExecutionException("首付款比例为 100% 时，账期必须为 0");
                    }
                    if (target.Contains("mcs_payfreq") && target.GetAttributeValue<int>("mcs_payfreq") != 0)
                    {
                        throw new InvalidPluginExecutionException("首付款比例为 100% 时，付款频次必须为 0");
                    }
                }
            }

            // 2. 账期/付款频次 30 倍数校验
            if (target.Contains("mcs_payterm"))
            {
                int payTerm = target.GetAttributeValue<int>("mcs_payterm");
                if (payTerm < 0 || (payTerm != 0 && payTerm % 30 != 0))
                {
                    throw new InvalidPluginExecutionException("账期（天）必须是 0 或 30 的倍数");
                }
            }

            if (target.Contains("mcs_payfreq"))
            {
                int payFreq = target.GetAttributeValue<int>("mcs_payfreq");
                if (payFreq < 0 || (payFreq != 0 && payFreq % 30 != 0))
                {
                    throw new InvalidPluginExecutionException("付款频次（天）必须是 0 或 30 的倍数");
                }
            }

            // 3. 状态流转校验（Update 时）
            if (context.MessageName == "Update" && target.Contains("mcs_status"))
            {
                ValidateStatusTransition(target, context, systemService, tracer);
            }

            // 4. 重复记录校验（Create/Update 涉及关键维度变更时）
            // 维度：事业部、子公司、国家、产品分类、客户分类、客户等级
            // 国家/产品分类已改为多行文本字段（mcs_countries/mcs_trade_type），存储 GUID 逗号分隔
            // 空值语义（2026-07-27 调整）：空/NA 作为具体值参与比较，空仅与空相同，不再通配
            if (context.MessageName == "Create" ||
                target.Contains("mcs_buid") ||
                target.Contains("mcs_subid") ||
                target.Contains("mcs_countries") ||
                target.Contains("mcs_trade_type") ||
                target.Contains("mcs_buyergrade") ||
                target.Contains("mcs_creditgrade"))
            {
                ValidateDuplicate(target, context, service, tracer);
            }
        }

        /// <summary>
        /// 将名称文本字段解析为目标实体的 GUID 列表（Excel 导入场景：用户只填名称，自定义多选控件依赖 GUID 显示）。
        /// 仅当 GUID 字段为空且名称字段非空时解析；名称与目标实体名称字段精确匹配（不区分大小写）；
        /// 匹配不到时抛出错误阻断创建（Excel 导入会显示行级错误）；NA 视为通配跳过。
        /// </summary>
        private void ResolveLookupNameToGuid(Entity target, IPluginExecutionContext context, IOrganizationService service, ITracingService tracer,
            string nameField, string guidField, string codeField,
            string targetEntity, string targetNameField, string targetCodeField, string displayName, string altNameField)
        {
            // GUID 字段已提交非空值（UI 手工选择/接口直传），不覆盖
            if (target.Contains(guidField) && !string.IsNullOrWhiteSpace(target.GetAttributeValue<string>(guidField)))
            {
                return;
            }

            // Update 时 GUID 字段未提交：查 PreImage/数据库，已有值则不覆盖
            if (context.MessageName == "Update")
            {
                string existing = GetFieldValue(target, context, service, guidField);
                if (!string.IsNullOrWhiteSpace(existing))
                {
                    return;
                }
            }

            if (!target.Contains(nameField))
            {
                return;
            }
            string rawNames = target.GetAttributeValue<string>(nameField);
            if (string.IsNullOrWhiteSpace(rawNames))
            {
                return;
            }

            // 兼容英文逗号/中文逗号/顿号分隔
            var names = rawNames.Split(new[] { ',', '，', '、' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(n => n.Trim())
                .Where(n => n.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (names.Count == 0)
            {
                return;
            }

            // NA 视为通配（与重复校验的空值通配语义一致）：全为 NA 则保持 GUID 字段为空
            names = names.Where(n => !n.Equals("NA", StringComparison.OrdinalIgnoreCase)).ToList();
            if (names.Count == 0)
            {
                tracer.Trace($"{displayName}名称为 NA，按通配处理，不解析 GUID");
                return;
            }

            // 查询目标实体全部有效记录，内存中按名称匹配
            // （不用 In 条件：CJK 名称精确匹配可能被隐藏字符/排序规则影响；目标实体均为小数据量基础表）
            var query = new QueryExpression(targetEntity)
            {
                ColumnSet = altNameField != null
                    ? new ColumnSet(targetNameField, targetCodeField, altNameField)
                    : new ColumnSet(targetNameField, targetCodeField),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("statecode", ConditionOperator.Equal, 0)
                    }
                },
                PageInfo = new PagingInfo { Count = 5000, PageNumber = 1 }
            };
            var result = service.RetrieveMultiple(query);

            // 名称 -> (Id, Code) 映射（名称 Trim 后比较，同名多条取第一条并记 Trace）
            // altNameField（如 mcs_country.mcs_chinesename 中文名）作为主名称的别名参与匹配，
            // 解决主名称被 MDM 同步为英文后中文名称匹配不到的问题
            var map = new Dictionary<string, Tuple<Guid, string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var record in result.Entities)
            {
                var code = (record.GetAttributeValue<string>(targetCodeField) ?? string.Empty).Trim();
                var names2 = new List<string>();
                var mainName = (record.GetAttributeValue<string>(targetNameField) ?? string.Empty).Trim();
                if (mainName.Length > 0)
                {
                    names2.Add(mainName);
                }
                if (altNameField != null)
                {
                    var altName = (record.GetAttributeValue<string>(altNameField) ?? string.Empty).Trim();
                    if (altName.Length > 0 && !altName.Equals(mainName, StringComparison.OrdinalIgnoreCase))
                    {
                        names2.Add(altName);
                    }
                }
                foreach (var name in names2)
                {
                    if (!map.ContainsKey(name))
                    {
                        map[name] = Tuple.Create(record.Id, code);
                    }
                    else
                    {
                        tracer.Trace($"{displayName}名称【{name}】存在多条记录，取第一条（{map[name].Item1}）");
                    }
                }
            }

            // 存在匹配不到的名称：阻断创建（Excel 导入会显示行级错误）
            var unmatched = names.Where(n => !map.ContainsKey(n)).ToList();
            if (unmatched.Count > 0)
            {
                throw new InvalidPluginExecutionException(
                    $"无法识别的{displayName}名称【{string.Join("、", unmatched)}】：在{displayName}基础数据中不存在，" +
                    $"请确认名称与基础数据完全一致后重试。");
            }

            var guids = names.Select(n => map[n].Item1.ToString()).ToList();
            var codes = names.Select(n => map[n].Item2).Where(c => !string.IsNullOrEmpty(c)).ToList();

            target[guidField] = string.Join(",", guids);
            if (codes.Count > 0)
            {
                target[codeField] = string.Join(",", codes);
            }
            tracer.Trace($"{displayName}名称解析完成：{names.Count} 个名称 -> {guids.Count} 个 GUID");
        }

        /// <summary>
        /// 必填字段服务端兜底校验（2026-07-27 新增）：
        /// 事业部/客户分类代码/首付款比例/账期/付款频次 元数据均为 ApplicationRequired（仅表单客户端强制），
        /// Excel 导入/API Create 不经过表单即可写入空值，必须在服务端兜底拦截。
        /// Create/Update 均按「target 优先，PreImage/数据库回退」的合并后状态校验；数值字段 0 为合法值，仅拦截空。
        /// </summary>
        private void ValidateRequiredFields(Entity target, IPluginExecutionContext context, IOrganizationService service, ITracingService tracer)
        {
            tracer.Trace("开始必填字段校验");

            // 事业部（Lookup）
            if (GetLookupValue(target, context, service, "mcs_businessunit") == null)
            {
                throw new InvalidPluginExecutionException("事业部为必填项，不能为空");
            }

            // 客户分类代码（多选选项集，空集合/空字符串均视为空）
            if (string.IsNullOrWhiteSpace(GetBuyerGradeString(target, context, service)))
            {
                throw new InvalidPluginExecutionException("客户分类代码为必填项，不能为空");
            }

            // 首付款比例（Decimal，0% 为合法值，仅拦截未填写）
            if (GetNullableDecimalValue(target, context, service, "mcs_downpay") == null)
            {
                throw new InvalidPluginExecutionException("首付款比例为必填项，不能为空");
            }

            // 账期（天）（Integer，0 为合法值，仅拦截未填写）
            if (GetNullableIntValue(target, context, service, "mcs_payterm") == null)
            {
                throw new InvalidPluginExecutionException("账期（天）为必填项，不能为空");
            }

            // 付款频次（天）（Integer，0 为合法值，仅拦截未填写）
            if (GetNullableIntValue(target, context, service, "mcs_payfreq") == null)
            {
                throw new InvalidPluginExecutionException("付款频次（天）为必填项，不能为空");
            }
        }

        /// <summary>
        /// 获取 Decimal 字段合并值（可空）：target 优先，Update 时 PreImage/数据库回退；未填写返回 null
        /// </summary>
        private decimal? GetNullableDecimalValue(Entity target, IPluginExecutionContext context, IOrganizationService service, string fieldName)
        {
            if (target.Contains(fieldName))
            {
                return target.GetAttributeValue<decimal?>(fieldName);
            }

            if (context.MessageName == "Update")
            {
                if (context.PreEntityImages.Contains("PreImage"))
                {
                    var preImage = context.PreEntityImages["PreImage"];
                    if (preImage.Contains(fieldName))
                    {
                        return preImage.GetAttributeValue<decimal?>(fieldName);
                    }
                }
                // PreImage 未注册该列时也回退数据库，避免必填校验误判为空
                if (target.Id != Guid.Empty)
                {
                    try
                    {
                        var current = service.Retrieve("mcs_trade_stpayterm", target.Id, new ColumnSet(fieldName));
                        if (current.Contains(fieldName))
                        {
                            return current.GetAttributeValue<decimal?>(fieldName);
                        }
                    }
                    catch (Exception)
                    {
                        // 记录可能尚未创建，忽略异常
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 获取 Integer 字段合并值（可空）：target 优先，Update 时 PreImage/数据库回退；未填写返回 null
        /// </summary>
        private int? GetNullableIntValue(Entity target, IPluginExecutionContext context, IOrganizationService service, string fieldName)
        {
            if (target.Contains(fieldName))
            {
                return target.GetAttributeValue<int?>(fieldName);
            }

            if (context.MessageName == "Update")
            {
                if (context.PreEntityImages.Contains("PreImage"))
                {
                    var preImage = context.PreEntityImages["PreImage"];
                    if (preImage.Contains(fieldName))
                    {
                        return preImage.GetAttributeValue<int?>(fieldName);
                    }
                }
                // PreImage 未注册该列时也回退数据库，避免必填校验误判为空
                if (target.Id != Guid.Empty)
                {
                    try
                    {
                        var current = service.Retrieve("mcs_trade_stpayterm", target.Id, new ColumnSet(fieldName));
                        if (current.Contains(fieldName))
                        {
                            return current.GetAttributeValue<int?>(fieldName);
                        }
                    }
                    catch (Exception)
                    {
                        // 记录可能尚未创建，忽略异常
                    }
                }
            }

            return null;
        }

        private void ValidateStatusTransition(Entity target, IPluginExecutionContext context, IOrganizationService systemService, ITracingService tracer)
        {
            if (!context.PreEntityImages.Contains("PreImage"))
            {
                tracer.Trace("未找到 PreImage，跳过状态流转校验");
                return;
            }

            Entity preImage = context.PreEntityImages["PreImage"];
            int oldStatus = preImage.GetAttributeValue<OptionSetValue>("mcs_status")?.Value ?? 0;
            int newStatus = target.GetAttributeValue<OptionSetValue>("mcs_status")?.Value ?? 0;

            if (oldStatus == newStatus)
            {
                return;
            }

            // 合法流转：
            // 0(未生效) -> 1(待审批): 申请
            // 1(待审批) -> 2(生效): 审批
            // 1(待审批) -> 0(未生效): 拒绝
            bool valid = (oldStatus == 0 && newStatus == 1) ||
                         (oldStatus == 1 && newStatus == 2) ||
                         (oldStatus == 1 && newStatus == 0);

            if (!valid)
            {
                string oldStatusName = MapStatusValueToName(oldStatus);
                string newStatusName = MapStatusValueToName(newStatus);
                throw new InvalidPluginExecutionException(
                    $"状态流转不合法：当前记录状态为【{oldStatusName}】，不允许直接变更为【{newStatusName}】。" +
                    "正确流程：未生效 → 待审批 → 生效，或待审批 → 未生效（拒绝）。");
            }

            // 角色权限校验（后端兜底，防止绕过前端按钮直接调用 API 改状态）
            // 0->1（申请）：制定人；1->2（审批）/1->0（拒绝）：审批人；系统管理员均放行
            string actionName;
            string requiredRole;
            if (oldStatus == 0 && newStatus == 1)
            {
                actionName = "申请";
                requiredRole = RoleCreator;
            }
            else
            {
                actionName = newStatus == 2 ? "审批" : "拒绝";
                requiredRole = RoleApprover;
            }

            if (!UserHasAnyRole(systemService, context.UserId, requiredRole, RoleAdmin))
            {
                throw new InvalidPluginExecutionException(
                    $"没有【{actionName}】权限：只有【{requiredRole}】或【{RoleAdmin}】角色才能执行{actionName}操作。");
            }
        }

        /// <summary>
        /// 判断用户是否拥有指定角色中的任意一个（按角色名匹配，使用系统身份查询）
        /// </summary>
        private bool UserHasAnyRole(IOrganizationService systemService, Guid userId, params string[] roleNames)
        {
            var query = new QueryExpression("role")
            {
                ColumnSet = new ColumnSet("roleid"),
                TopCount = 1,
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("name", ConditionOperator.In, roleNames)
                    }
                },
                LinkEntities =
                {
                    new LinkEntity("role", "systemuserroles", "roleid", "roleid", JoinOperator.Inner)
                    {
                        LinkCriteria = new FilterExpression
                        {
                            Conditions =
                            {
                                new ConditionExpression("systemuserid", ConditionOperator.Equal, userId)
                            }
                        }
                    }
                }
            };

            return systemService.RetrieveMultiple(query).Entities.Count > 0;
        }

        private void ValidateDuplicate(Entity target, IPluginExecutionContext context, IOrganizationService service, ITracingService tracer)
        {
            tracer.Trace("开始重复记录校验");

            // 获取当前记录 ID
            Guid currentId = target.Id;
            if (currentId == Guid.Empty && context.MessageName == "Create")
            {
                currentId = Guid.NewGuid(); // 新建记录临时 ID，仅用于排除
            }

            // 获取当前维度值
            string buId = GetFieldValue(target, context, service, "mcs_buid");
            string subId = GetFieldValue(target, context, service, "mcs_subid");
            string countries = GetFieldValue(target, context, service, "mcs_countries");
            string tradeType = GetFieldValue(target, context, service, "mcs_trade_type");
            string buyerGrade = GetBuyerGradeString(target, context, service);
            string creditGrade = GetCreditGradeString(target, context, service);

            // 禅道 #1282：Excel 导入/接口创建时 mcs_buid 为空（编码靠表单 JS 带出，导入时 JS 不执行），
            // 从事业部 Lookup（mcs_businessunit）反查 mcs_bu.mcs_code 作校验锚点，并回填 target 保证入库数据完整
            EntityReference buRef = GetLookupValue(target, context, service, "mcs_businessunit");
            if (string.IsNullOrEmpty(buId) && buRef != null)
            {
                buId = ResolveBuCode(service, buRef, tracer);
                if (!string.IsNullOrEmpty(buId) &&
                    (!target.Contains("mcs_buid") || string.IsNullOrEmpty(target.GetAttributeValue<string>("mcs_buid"))))
                {
                    target["mcs_buid"] = buId;
                    tracer.Trace($"mcs_buid 为空，已从事业部 Lookup 解析并回填: {buId}");
                }
            }

            // 查询同事业部其他记录
            // 禅道 #1282：条件改为「mcs_buid == 编码 OR mcs_businessunit == Lookup」，
            // 覆盖存量 buid 为空但 Lookup 有值的导入记录，否则修复后对存量记录仍然漏判
            var buFilter = new FilterExpression(LogicalOperator.Or);
            if (!string.IsNullOrEmpty(buId))
            {
                buFilter.AddCondition("mcs_buid", ConditionOperator.Equal, buId);
            }
            if (buRef != null)
            {
                buFilter.AddCondition("mcs_businessunit", ConditionOperator.Equal, buRef.Id);
            }
            if (buFilter.Conditions.Count == 0)
            {
                tracer.Trace("事业部编码与事业部 Lookup 均为空，跳过重复校验");
                return;
            }

            var query = new QueryExpression("mcs_trade_stpayterm")
            {
                ColumnSet = new ColumnSet("mcs_subid", "mcs_countries", "mcs_trade_type", "mcs_buyergrade", "mcs_creditgrade"),
                Criteria = new FilterExpression()
            };
            query.Criteria.AddFilter(buFilter);

            if (context.MessageName == "Update")
            {
                query.Criteria.AddCondition("mcs_trade_stpaytermid", ConditionOperator.NotEqual, currentId);
            }

            var result = service.RetrieveMultiple(query);
            tracer.Trace($"查询到同事业部记录 {result.Entities.Count} 条");

            foreach (var record in result.Entities)
            {
                if (IsDuplicate(subId, countries, tradeType, buyerGrade, creditGrade, record))
                {
                    throw new InvalidPluginExecutionException("存在有重复记录，需核查！");
                }
            }
        }

        /// <summary>
        /// 获取 Lookup 字段值（禅道 #1282）：target 优先，Update 时 PreImage/数据库回退
        /// </summary>
        private EntityReference GetLookupValue(Entity target, IPluginExecutionContext context, IOrganizationService service, string fieldName)
        {
            if (target.Contains(fieldName))
            {
                return target.GetAttributeValue<EntityReference>(fieldName);
            }

            if (context.MessageName == "Update")
            {
                if (context.PreEntityImages.Contains("PreImage"))
                {
                    var preImage = context.PreEntityImages["PreImage"];
                    if (preImage.Contains(fieldName))
                    {
                        return preImage.GetAttributeValue<EntityReference>(fieldName);
                    }
                }
                // PreImage 未注册该列时也回退数据库，避免必填校验误判为空
                if (target.Id != Guid.Empty)
                {
                    try
                    {
                        var current = service.Retrieve("mcs_trade_stpayterm", target.Id, new ColumnSet(fieldName));
                        if (current.Contains(fieldName))
                        {
                            return current.GetAttributeValue<EntityReference>(fieldName);
                        }
                    }
                    catch (Exception)
                    {
                        // 记录可能尚未创建，忽略异常
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 反查事业部主数据编码（mcs_bu.mcs_code，禅道 #1282）
        /// </summary>
        private string ResolveBuCode(IOrganizationService service, EntityReference buRef, ITracingService tracer)
        {
            try
            {
                var bu = service.Retrieve(buRef.LogicalName, buRef.Id, new ColumnSet("mcs_code"));
                return bu.GetAttributeValue<string>("mcs_code") ?? string.Empty;
            }
            catch (Exception ex)
            {
                tracer.Trace($"反查事业部编码失败: {ex.Message}");
                return string.Empty;
            }
        }

        private string GetFieldValue(Entity target, IPluginExecutionContext context, IOrganizationService service, string fieldName)
        {
            if (target.Contains(fieldName))
            {
                return target.GetAttributeValue<string>(fieldName) ?? string.Empty;
            }

            if (context.MessageName == "Update")
            {
                if (context.PreEntityImages.Contains("PreImage"))
                {
                    var preImage = context.PreEntityImages["PreImage"];
                    if (preImage.Contains(fieldName))
                    {
                        return preImage.GetAttributeValue<string>(fieldName) ?? string.Empty;
                    }
                }
                else if (target.Id != Guid.Empty)
                {
                    // 没有 PreImage 时，从数据库查询当前记录
                    try
                    {
                        var current = service.Retrieve("mcs_trade_stpayterm", target.Id, new ColumnSet(fieldName));
                        if (current.Contains(fieldName))
                        {
                            return current.GetAttributeValue<string>(fieldName) ?? string.Empty;
                        }
                    }
                    catch (Exception)
                    {
                        // 记录可能尚未创建，忽略异常
                    }
                }
            }

            return string.Empty;
        }

        private bool IsDuplicate(string subId, string countries, string tradeType, string buyerGrade, string creditGrade, Entity record)
        {
            // 子公司匹配（空值参与校验：空仅与空相同）
            string recordSubId = record.GetAttributeValue<string>("mcs_subid") ?? string.Empty;
            if (!IsExactDimMatch(subId, recordSubId))
            {
                return false;
            }

            // 国家匹配（多行文本 GUID 逗号分隔；空值参与校验：空仅与空相同，非空取交集）
            string recordCountries = record.GetAttributeValue<string>("mcs_countries") ?? string.Empty;
            if (!IsMultiSelectDimMatch(countries, recordCountries, ","))
            {
                return false;
            }

            // 产品分类匹配（多行文本 GUID 逗号分隔；空值参与校验：空仅与空相同，非空取交集）
            string recordTradeType = record.GetAttributeValue<string>("mcs_trade_type") ?? string.Empty;
            if (!IsMultiSelectDimMatch(tradeType, recordTradeType, ","))
            {
                return false;
            }

            // 客户分类匹配（支持 OptionSetValueCollection 和旧字符串格式；空仅与空相同，非空取交集）
            string recordBuyerGrade = GetBuyerGradeString(record);
            if (!IsMultiSelectDimMatch(buyerGrade, recordBuyerGrade, "/"))
            {
                return false;
            }

            // 客户等级匹配（DEV1 当前为单选 Picklist，同时兼容多选选项集/字符串；空仅与空相同）
            string recordCreditGrade = GetCreditGradeString(record);
            if (!IsExactDimMatch(creditGrade, recordCreditGrade))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 将客户分类多选选项集转换为 / 分隔的字符串（按标签）
        /// </summary>
        private string GetBuyerGradeString(Entity entity)
        {
            var collection = entity.GetAttributeValue<OptionSetValueCollection>("mcs_buyergrade");
            if (collection == null || collection.Count == 0)
            {
                return entity.GetAttributeValue<string>("mcs_buyergrade") ?? string.Empty;
            }

            var labels = new List<string>();
            foreach (var option in collection)
            {
                labels.Add(MapBuyerGradeValueToLabel(option.Value));
            }
            labels.Sort(StringComparer.OrdinalIgnoreCase);
            return string.Join("/", labels);
        }

        /// <summary>
        /// 将客户等级转换为标签字符串（DEV1 当前为单选 Picklist，兼容多选选项集/字符串）
        /// </summary>
        private string GetCreditGradeString(Entity entity)
        {
            // 单选 Picklist
            var option = entity.GetAttributeValue<OptionSetValue>("mcs_creditgrade");
            if (option != null)
            {
                return MapCreditGradeValueToLabel(option.Value);
            }

            // 兼容多选选项集
            var collection = entity.GetAttributeValue<OptionSetValueCollection>("mcs_creditgrade");
            if (collection != null && collection.Count > 0)
            {
                var labels = new List<string>();
                foreach (var item in collection)
                {
                    labels.Add(MapCreditGradeValueToLabel(item.Value));
                }
                labels.Sort(StringComparer.OrdinalIgnoreCase);
                return string.Join("/", labels);
            }

            // 兼容字符串
            return entity.GetAttributeValue<string>("mcs_creditgrade") ?? string.Empty;
        }

        /// <summary>
        /// 获取当前记录的客户等级字符串（用于重复校验）
        /// </summary>
        private string GetCreditGradeString(Entity target, IPluginExecutionContext context, IOrganizationService service)
        {
            if (target.Contains("mcs_creditgrade"))
            {
                return GetCreditGradeString(target);
            }

            if (context.MessageName == "Update")
            {
                if (context.PreEntityImages.Contains("PreImage"))
                {
                    var preImage = context.PreEntityImages["PreImage"];
                    if (preImage.Contains("mcs_creditgrade"))
                    {
                        return GetCreditGradeString(preImage);
                    }
                }
                else if (target.Id != Guid.Empty)
                {
                    try
                    {
                        var current = service.Retrieve("mcs_trade_stpayterm", target.Id, new ColumnSet("mcs_creditgrade"));
                        if (current.Contains("mcs_creditgrade"))
                        {
                            return GetCreditGradeString(current);
                        }
                    }
                    catch (Exception)
                    {
                        // 记录可能尚未创建，忽略异常
                    }
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// 客户等级选项值 → 标签映射
        /// </summary>
        private string MapCreditGradeValueToLabel(int value)
        {
            switch (value)
            {
                case 100000000: return "A0";
                case 100000001: return "A1";
                case 100000002: return "A2";
                case 100000003: return "A3";
                case 100000004: return "A4";
                default: return value.ToString();
            }
        }

        /// <summary>
        /// 生效状态值 → 显示名称映射
        /// </summary>
        private string MapStatusValueToName(int value)
        {
            switch (value)
            {
                case 0: return "未生效";
                case 1: return "待审批";
                case 2: return "生效";
                default: return $"未知({value})";
            }
        }

        /// <summary>
        /// 获取当前记录的客户分类字符串（用于重复校验）
        /// </summary>
        private string GetBuyerGradeString(Entity target, IPluginExecutionContext context, IOrganizationService service)
        {
            if (target.Contains("mcs_buyergrade"))
            {
                return GetBuyerGradeString(target);
            }

            if (context.MessageName == "Update")
            {
                if (context.PreEntityImages.Contains("PreImage"))
                {
                    var preImage = context.PreEntityImages["PreImage"];
                    if (preImage.Contains("mcs_buyergrade"))
                    {
                        return GetBuyerGradeString(preImage);
                    }
                }
                else if (target.Id != Guid.Empty)
                {
                    try
                    {
                        var current = service.Retrieve("mcs_trade_stpayterm", target.Id, new ColumnSet("mcs_buyergrade"));
                        if (current.Contains("mcs_buyergrade"))
                        {
                            return GetBuyerGradeString(current);
                        }
                    }
                    catch (Exception)
                    {
                        // 记录可能尚未创建，忽略异常
                    }
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// 客户分类选项值 → 标签映射
        /// </summary>
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

        /// <summary>
        /// 单值维度匹配（2026-07-27 调整：空值参与重复校验，禅道 #1282 需求原文「考虑字段空情况」）：
        /// 空/NA 归一化为空后精确比较 —— 空仅与空相同，空与非空不同，不再是「任一方空即通配」。
        /// </summary>
        private bool IsExactDimMatch(string value1, string value2)
        {
            string v1 = NormalizeDimValue(value1);
            string v2 = NormalizeDimValue(value2);
            return string.Equals(v1, v2, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 多值维度匹配（2026-07-27 调整：空值参与重复校验）：
        /// 空/NA 归一化为空 —— 空仅与空相同；双方均非空时存在交集即视为重复。
        /// </summary>
        private bool IsMultiSelectDimMatch(string value1, string value2, string separator)
        {
            string v1 = NormalizeDimValue(value1);
            string v2 = NormalizeDimValue(value2);

            // 任一方为空：仅「空 == 空」视为相同，空与非空不重复
            if (string.IsNullOrEmpty(v1) || string.IsNullOrEmpty(v2))
            {
                return string.Equals(v1, v2, StringComparison.OrdinalIgnoreCase);
            }

            var set1 = ParseMultiSelect(v1, separator);
            var set2 = ParseMultiSelect(v2, separator);

            return set1.Any(x => set2.Contains(x, StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 维度值归一化：空白与 NA 统一归一为空字符串（NA 是导入模板中「不填」的显式写法，语义等同空值）
        /// </summary>
        private string NormalizeDimValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }
            string trimmed = value.Trim();
            return trimmed.Equals("NA", StringComparison.OrdinalIgnoreCase) ? string.Empty : trimmed;
        }

        private HashSet<string> ParseMultiSelect(string value, string separator)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(value))
            {
                return result;
            }

            foreach (var item in value.Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = item.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    result.Add(trimmed);
                }
            }

            return result;
        }
    }
}
