using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace SanyD365.Plugins.BppIntegration.Plugin
{
    /// <summary>
    /// BPP审批结果回调处理Plugin
    /// 触发条件: mcs_credit_record Update/PostOperation, mcs_bppstatus变更
    /// 
    /// 说明:
    /// - BPP平台审批完成后，BPP框架会回调D365并更新mcs_bppstatus字段
    /// - 本Plugin监听mcs_bppstatus变更，根据状态值更新业务状态
    /// - 状态值可能是字符串(Approved/Rejected)或数字(11/30)，具体取决于BPP平台和模板配置
    /// </summary>
    public class CreditRecordBppCallbackPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var service = serviceFactory.CreateOrganizationService(context.UserId);
            var tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            try
            {
                tracer.Trace("=== CreditRecordBppCallbackPlugin Execute ===");
                tracer.Trace($"Message: {context.MessageName}, Stage: {context.Stage}, Depth: {context.Depth}");

                // 防递归
                if (context.Depth > 3)
                {
                    tracer.Trace("递归深度超过3，跳过处理");
                    return;
                }

                // 获取Target
                if (!context.InputParameters.Contains("Target") || !(context.InputParameters["Target"] is Entity target))
                {
                    tracer.Trace("未找到Target参数");
                    return;
                }

                // 严格校验实体名
                if (!target.LogicalName.Equals("mcs_credit_record", StringComparison.OrdinalIgnoreCase))
                {
                    tracer.Trace($"非信用评估记录，跳过: {target.LogicalName}");
                    return;
                }

                // 只处理mcs_bppstatus变更
                if (!target.Contains("mcs_bppstatus"))
                {
                    tracer.Trace("mcs_bppstatus未变更，跳过");
                    return;
                }

                // 获取新的BPP状态值（可能是字符串或数字）
                string bppStatus = GetBppStatusValue(target, tracer);
                tracer.Trace($"BPP回调状态: {bppStatus}");

                if (string.IsNullOrWhiteSpace(bppStatus))
                {
                    tracer.Trace("BPP状态为空，跳过处理");
                    return;
                }

                // 中间状态不处理（由BPP框架或BppIntegrationPlugin写入）
                if (IsIntermediateStatus(bppStatus))
                {
                    tracer.Trace($"BPP中间状态: {bppStatus}，跳过处理");
                    return;
                }

                var updateRecord = new Entity("mcs_credit_record") { Id = target.Id };

                switch (bppStatus.ToLowerInvariant())
                {
                    case "approved":
                    case "30": // 部分模板可能使用数字状态
                        tracer.Trace("BPP审批通过，更新状态为15");
                        updateRecord["mcs_status"] = new OptionSetValue(15); // 审批通过
                        updateRecord["mcs_active"] = true;
                        updateRecord["mcs_approvedate"] = DateTime.Now;
                        service.Update(updateRecord);

                        // 同步客户主数据信用信息
                        UpdateCustomerMasterDataCreditInfo(service, tracer, target.Id);
                        break;

                    case "rejected":
                    case "11": // 部分模板可能使用数字状态
                        tracer.Trace("BPP审批驳回，状态回到12(人工复核)");
                        updateRecord["mcs_status"] = new OptionSetValue(12); // 人工复核（非16！）
                        // 驳回原因由BPP框架回写到mcs_bpprejectreason，这里不覆盖
                        service.Update(updateRecord);
                        break;

                    case "withdrawn":
                    case "withdraw":
                        tracer.Trace("BPP审批撤回，状态回到12(人工复核)");
                        updateRecord["mcs_status"] = new OptionSetValue(12);
                        updateRecord["mcs_workflowid"] = null;
                        updateRecord["mcs_nextapprover"] = null;
                        service.Update(updateRecord);
                        break;

                    case "abandoned":
                    case "abandon":
                        tracer.Trace("BPP审批废弃，状态回到12(人工复核)");
                        updateRecord["mcs_status"] = new OptionSetValue(12);
                        updateRecord["mcs_workflowid"] = null;
                        updateRecord["mcs_nextapprover"] = null;
                        service.Update(updateRecord);
                        break;

                    default:
                        tracer.Trace($"未知的BPP回调状态: {bppStatus}，暂不处理");
                        break;
                }
            }
            catch (Exception ex)
            {
                tracer.Trace($"CreditRecordBppCallbackPlugin异常: {ex.Message}");
                tracer.Trace($"异常堆栈: {ex.StackTrace}");
                // 回调处理异常不应阻断主流程，记录后抛出以便D365记录
                throw new InvalidPluginExecutionException($"BPP回调处理失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取BPP状态值，兼容字符串和数字类型
        /// </summary>
        private string GetBppStatusValue(Entity target, ITracingService tracer)
        {
            // 当前mcs_bppstatus是字符串类型
            if (target["mcs_bppstatus"] is string strValue)
            {
                return strValue.Trim();
            }

            // 如果未来改为选项集，兼容处理
            if (target["mcs_bppstatus"] is OptionSetValue optionValue)
            {
                return optionValue.Value.ToString();
            }

            tracer.Trace($"mcs_bppstatus类型未识别: {target["mcs_bppstatus"].GetType().FullName}");
            return target["mcs_bppstatus"]?.ToString() ?? "";
        }

        /// <summary>
        /// 判断是否为中间状态（无需业务处理）
        /// </summary>
        private bool IsIntermediateStatus(string bppStatus)
        {
            var status = bppStatus.ToLowerInvariant();
            return status == "submitted" ||
                   status == "pending" ||
                   status == "10" ||
                   status == "20";
        }

        /// <summary>
        /// 审批通过后同步客户主数据信用信息
        /// 目标实体: mcs_customermasterdata（通过 account.mcs_customermasterdata 关联）
        /// </summary>
        private void UpdateCustomerMasterDataCreditInfo(IOrganizationService service, ITracingService tracer, Guid creditRecordId)
        {
            try
            {
                var creditRecord = service.Retrieve("mcs_credit_record", creditRecordId,
                    new ColumnSet("mcs_accountid", "mcs_creditscore"));

                if (!creditRecord.Contains("mcs_accountid") ||
                    !(creditRecord["mcs_accountid"] is EntityReference accountRef))
                {
                    tracer.Trace("mcs_accountid为空或不是Lookup，跳过更新客户主数据");
                    return;
                }

                var accountId = accountRef.Id;
                decimal? creditScore = creditRecord.GetAttributeValue<decimal?>("mcs_creditscore");

                if (!creditScore.HasValue)
                {
                    tracer.Trace("信用分为空，跳过更新客户主数据");
                    return;
                }

                // 通过 account 查找关联的 mcs_customermasterdata
                var account = service.Retrieve("account", accountId,
                    new ColumnSet("mcs_customermasterdata"));

                if (!account.Contains("mcs_customermasterdata") ||
                    !(account["mcs_customermasterdata"] is EntityReference customerMasterDataRef))
                {
                    tracer.Trace($"accountId={accountId} 未关联 mcs_customermasterdata，跳过更新");
                    return;
                }

                var customerMasterDataId = customerMasterDataRef.Id;
                string creditGrade = CalculateCreditGrade(creditScore);
                int? creditGradeValue = MapCreditGradeToOptionSetValue(creditGrade);

                var updateCustomerMasterData = new Entity("mcs_customermasterdata", customerMasterDataId);
                updateCustomerMasterData["mcs_creditscore"] = creditScore.Value;
                if (creditGradeValue.HasValue)
                {
                    updateCustomerMasterData["mcs_creditgrade"] = new OptionSetValue(creditGradeValue.Value);
                }
                updateCustomerMasterData["mcs_creditvalid"] = true;

                service.Update(updateCustomerMasterData);
                tracer.Trace($"更新客户主数据: customerMasterDataId={customerMasterDataId}, score={creditScore}, grade={creditGrade}({creditGradeValue})");
            }
            catch (Exception ex)
            {
                tracer.Trace($"更新客户主数据失败: {ex.Message}");
                // 不阻断主流程，但记录异常
            }
        }

        /// <summary>
        /// 计算信用等级
        /// TODO: 具体分值区间需业务确认
        /// </summary>
        private string CalculateCreditGrade(decimal? score)
        {
            if (!score.HasValue) return "";
            decimal s = score.Value;
            if (s >= 80) return "A0";
            if (s >= 70) return "A1";
            if (s >= 60) return "A2";
            if (s >= 50) return "A3";
            return "A4";
        }

        /// <summary>
        /// 将信用等级字符串映射为 D365 选项集值
        /// account.mcs_creditgrade: A0=100000000, A1=100000001, A2=100000002, A3=100000003, A4=100000004
        /// </summary>
        private int? MapCreditGradeToOptionSetValue(string creditGrade)
        {
            switch (creditGrade?.ToUpperInvariant())
            {
                case "A0": return 100000000;
                case "A1": return 100000001;
                case "A2": return 100000002;
                case "A3": return 100000003;
                case "A4": return 100000004;
                default: return null;
            }
        }
    }
}
