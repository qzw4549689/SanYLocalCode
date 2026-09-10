using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

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

                // BPP流程实例ID一旦生成，立即拼接并更新BPP审批链接
                UpdateBppLinkIfAvailable(service, tracer, target.Id);

                // 中间状态不处理业务状态流转（由BPP框架或BppIntegrationPlugin写入）
                if (IsIntermediateStatus(bppStatus))
                {
                    tracer.Trace($"BPP中间状态: {bppStatus}，跳过业务状态流转");
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

                        // 禅道 #1645：一个客户永远只能存在一条有效评估，将同客户其他有效记录及其名下标签置否
                        DeactivateOtherActiveRecords(service, tracer, target.Id);
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
        /// 如果记录已有BPP流程实例ID，则拼接BPP审批链接并更新到mcs_bpplink字段
        /// </summary>
        private void UpdateBppLinkIfAvailable(IOrganizationService service, ITracingService tracer, Guid creditRecordId)
        {
            try
            {
                var creditRecord = service.Retrieve("mcs_credit_record", creditRecordId,
                    new ColumnSet("mcs_bppid", "mcs_workflowid", "mcs_bpplink"));

                string workflowId = creditRecord.GetAttributeValue<string>("mcs_bppid")
                    ?? creditRecord.GetAttributeValue<string>("mcs_workflowid");

                if (string.IsNullOrWhiteSpace(workflowId))
                {
                    tracer.Trace("BPP流程实例ID为空，无法生成BPP链接");
                    return;
                }

                // 与限额申请保持一致：审批门户基础地址从系统配置 Bpp_ApprovalFlowBaseUrl 读取（各环境独立配置）
                // 2026-09-03 修复：原硬编码 uat 门户地址，会把服务端 UpdateEntityStatusForStart 按配置写入的正确链接覆盖成 uat 链接
                var configQuery = new QueryExpression("ms_systemconfiguration")
                {
                    ColumnSet = new ColumnSet("ms_content")
                };
                configQuery.Criteria.AddCondition("ms_name", ConditionOperator.Equal, "Bpp_ApprovalFlowBaseUrl");
                var configEntity = service.RetrieveMultiple(configQuery).Entities.FirstOrDefault();
                var baseUrl = configEntity?.GetAttributeValue<string>("ms_content");
                if (string.IsNullOrWhiteSpace(baseUrl))
                {
                    tracer.Trace("系统配置 Bpp_ApprovalFlowBaseUrl 不存在或内容为空，跳过更新BPP链接");
                    return;
                }
                string bppLink = $"{baseUrl}{workflowId}";

                var existingLink = creditRecord.GetAttributeValue<string>("mcs_bpplink");
                if (!string.Equals(existingLink, bppLink, StringComparison.OrdinalIgnoreCase))
                {
                    var updateRecord = new Entity("mcs_credit_record") { Id = creditRecordId };
                    updateRecord["mcs_bpplink"] = bppLink;
                    service.Update(updateRecord);
                    tracer.Trace($"已更新BPP链接: {bppLink}");
                }
                else
                {
                    tracer.Trace("BPP链接未变化，跳过更新");
                }
            }
            catch (Exception ex)
            {
                tracer.Trace($"更新BPP链接失败: {ex.Message}");
                // 不影响主流程
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
        /// 禅道 #1645：审批通过后，将同客户其他有效评估记录置为无效（mcs_active=false），
        /// 并将其名下客户信用标签联动置否，保证一个客户永远只有一条有效评估。
        /// 说明：仅更新 mcs_active，Target 不含 mcs_bppstatus，不会递归触发本 Plugin。
        /// </summary>
        private void DeactivateOtherActiveRecords(IOrganizationService service, ITracingService tracer, Guid approvedRecordId)
        {
            try
            {
                var approved = service.Retrieve("mcs_credit_record", approvedRecordId,
                    new ColumnSet("mcs_accountid"));
                var accountRef = approved.GetAttributeValue<EntityReference>("mcs_accountid");
                if (accountRef == null)
                {
                    tracer.Trace("客户为空，跳过旧有效记录失效处理");
                    return;
                }

                var query = new QueryExpression("mcs_credit_record")
                {
                    ColumnSet = new ColumnSet("mcs_scoreid"),
                    Criteria =
                    {
                        Conditions =
                        {
                            new ConditionExpression("mcs_accountid", ConditionOperator.Equal, accountRef.Id),
                            new ConditionExpression("mcs_active", ConditionOperator.Equal, true),
                            new ConditionExpression("statecode", ConditionOperator.Equal, 0),
                            new ConditionExpression("mcs_credit_recordid", ConditionOperator.NotEqual, approvedRecordId)
                        }
                    }
                };

                var others = service.RetrieveMultiple(query);
                if (others.Entities.Count == 0)
                {
                    tracer.Trace("同客户无其他有效评估记录，无需失效处理");
                    return;
                }

                foreach (var oldRecord in others.Entities)
                {
                    var deactivate = new Entity("mcs_credit_record") { Id = oldRecord.Id };
                    deactivate["mcs_active"] = false;
                    service.Update(deactivate);
                    tracer.Trace($"旧有效评估已置否: {oldRecord.GetAttributeValue<string>("mcs_scoreid")}({oldRecord.Id})");

                    // 名下客户信用标签联动置否（用户确认口径：画像页/厂端授信按客户查标签，不能读到旧评估标签）
                    DeactivateTagsOfRecord(service, tracer, oldRecord.Id);
                }
            }
            catch (Exception ex)
            {
                tracer.Trace($"失效旧有效评估记录失败: {ex.Message}");
                // 不阻断主流程，但记录异常
            }
        }

        /// <summary>
        /// 将指定评估记录名下所有有效客户信用标签联动置否（禅道 #1645）
        /// </summary>
        private void DeactivateTagsOfRecord(IOrganizationService service, ITracingService tracer, Guid creditRecordId)
        {
            var tagQuery = new QueryExpression("mcs_customer_tag")
            {
                ColumnSet = new ColumnSet(false),
                Criteria =
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_credit_record", ConditionOperator.Equal, creditRecordId),
                        new ConditionExpression("mcs_active", ConditionOperator.Equal, true)
                    }
                }
            };

            var tags = service.RetrieveMultiple(tagQuery);
            foreach (var tag in tags.Entities)
            {
                var updateTag = new Entity("mcs_customer_tag") { Id = tag.Id };
                updateTag["mcs_active"] = false;
                service.Update(updateTag);
            }
            tracer.Trace($"评估记录 {creditRecordId} 名下 {tags.Entities.Count} 个有效标签已联动置否");
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
                // 禅道 #2091：信用等级映射改配置化（ms_systemconfiguration.CreditGradeMapping），
                // 配置缺失/解析失败时用内置新口径默认值兜底，不阻断流程
                var gradeConfig = CreditGradeMappingHelper.GetConfig(service, tracer);
                string creditGrade = CreditGradeMappingHelper.CalculateGrade(gradeConfig, creditScore);
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
