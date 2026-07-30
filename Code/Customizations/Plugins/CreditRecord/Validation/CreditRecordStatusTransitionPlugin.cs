using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SanyD365.Plugins.CreditRecord.Validation
{
    /// <summary>
    /// 信用评估记录表 - 状态流转合法性校验Plugin
    /// 触发时机：Create / Update PreOperation
    /// 功能：阻止任何非自定义按钮触发的非法 mcs_status 状态变更
    /// 影响范围：仅限 mcs_credit_record 实体
    /// </summary>
    public class CreditRecordStatusTransitionPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = factory.CreateOrganizationService(context.UserId);
            ITracingService tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracer.Trace("CreditRecordStatusTransitionPlugin 开始执行");

            // 严格校验：只处理 Create / Update PreOperation 事件
            if (context.Stage != 20 || (context.MessageName != "Update" && context.MessageName != "Create"))
            {
                tracer.Trace("非 Create/Update PreOperation 事件，跳过");
                return;
            }

            // 严格校验：只处理 mcs_credit_record 实体
            if (!context.InputParameters.Contains("Target") || !(context.InputParameters["Target"] is Entity))
            {
                tracer.Trace("未找到 Target 实体");
                return;
            }

            Entity target = (Entity)context.InputParameters["Target"];

            if (target.LogicalName != "mcs_credit_record")
            {
                tracer.Trace($"实体不匹配: {target.LogicalName}，跳过");
                return;
            }

            // Create 时：如果指定了状态，只能是 9（发起）或 10（关联客户代码）
            if (context.MessageName == "Create")
            {
                if (!target.Contains("mcs_status")) return;

                var createStatusValue = target.GetAttributeValue<OptionSetValue>("mcs_status");
                if (createStatusValue == null) return;

                int createStatus = createStatusValue.Value;
                if (createStatus != 9 && createStatus != 10)
                {
                    string msg = $"新建信用评估记录时，初始状态只能是“发起信用评估”或“关联客户代码”，当前值：{GetStatusName(createStatus)}。";
                    tracer.Trace(msg);
                    throw new InvalidPluginExecutionException(msg);
                }

                tracer.Trace($"Create 状态 {createStatus} 合法，放行");
                return;
            }

            // Update 时：必须包含 mcs_status 才校验
            if (!target.Contains("mcs_status"))
            {
                tracer.Trace("未修改 mcs_status，跳过");
                return;
            }

            var newStatusValue = target.GetAttributeValue<OptionSetValue>("mcs_status");
            if (newStatusValue == null)
            {
                tracer.Trace("新状态为空，跳过");
                return;
            }

            int newStatus = newStatusValue.Value;

            // 从 PreEntityImages 获取旧状态
            int? oldStatus = null;
            if (context.PreEntityImages.Contains("PreImage"))
            {
                var preImage = context.PreEntityImages["PreImage"];
                var oldStatusValue = preImage.GetAttributeValue<OptionSetValue>("mcs_status");
                if (oldStatusValue != null)
                {
                    oldStatus = oldStatusValue.Value;
                }
            }

            // 兜底：如果没有 PreImage，直接从数据库查询当前旧状态（PreOperation 阶段数据库仍为旧值）
            if (!oldStatus.HasValue && target.Id != Guid.Empty)
            {
                try
                {
                    var currentRecord = service.Retrieve("mcs_credit_record", target.Id, new ColumnSet("mcs_status"));
                    var oldStatusValue = currentRecord.GetAttributeValue<OptionSetValue>("mcs_status");
                    if (oldStatusValue != null)
                    {
                        oldStatus = oldStatusValue.Value;
                        tracer.Trace($"从数据库查询到旧状态: {oldStatus.Value}");
                    }
                }
                catch (Exception ex)
                {
                    tracer.Trace($"查询旧状态失败: {ex.Message}");
                }
            }

            // 如果仍然没有旧状态（如记录刚创建后的首次更新），只允许 9 或 10
            if (!oldStatus.HasValue)
            {
                if (newStatus == 9 || newStatus == 10)
                {
                    tracer.Trace("未获取到旧状态，新状态为 9/10，放行");
                    return;
                }

                string msg = $"首次更新状态不允许从空状态直接变更为 {GetStatusName(newStatus)}。";
                tracer.Trace(msg);
                throw new InvalidPluginExecutionException(msg);
            }

            // 状态没有变化，放行
            if (oldStatus.Value == newStatus)
            {
                tracer.Trace("状态未变化，放行");
                return;
            }

            tracer.Trace($"状态变更: {oldStatus.Value} -> {newStatus}");

            // 校验状态转换是否合法
            if (!IsValidTransition(oldStatus.Value, newStatus))
            {
                string msg = $"非法的状态流转：从 {GetStatusName(oldStatus.Value)} 到 {GetStatusName(newStatus)}。请使用《进入下一阶段》按钮操作。";
                tracer.Trace(msg);
                throw new InvalidPluginExecutionException(msg);
            }

            tracer.Trace("状态流转校验通过");
        }

        /// <summary>
        /// 判断状态转换是否合法
        /// </summary>
        private bool IsValidTransition(int oldStatus, int newStatus)
        {
            var validTransitions = new Dictionary<int, int[]>
            {
                [9] = new[] { 10 },                       // 发起 -> 关联客户代码
                [10] = new[] { 11 },                      // 关联客户代码 -> 数据集成
                [11] = new[] { 12 },                      // 数据集成 -> 人工复核
                [12] = new[] { 13, 11 },                  // 人工复核 -> 信用分计算 / 数据集成刷新
                [13] = new[] { 14 },                      // 信用分计算 -> 审核申请
                [14] = new[] { 15, 12 },                  // 审核申请 -> 审批通过 / BPP 驳回/撤回/废弃
                [15] = new int[0],                        // 审批通过：终态
                [16] = new[] { 11 }                       // 审批未通过 -> 数据集成（重新发起）
            };

            return validTransitions.ContainsKey(oldStatus) &&
                   validTransitions[oldStatus].Contains(newStatus);
        }

        /// <summary>
        /// 获取状态名称（用于提示信息）
        /// </summary>
        private string GetStatusName(int status)
        {
            switch (status)
            {
                case 9: return "发起信用评估";
                case 10: return "关联客户代码";
                case 11: return "数据集成";
                case 12: return "人工复核";
                case 13: return "信用分计算";
                case 14: return "审核申请";
                case 15: return "审批通过";
                case 16: return "审批未通过";
                default: return $"未知状态({status})";
            }
        }
    }
}
