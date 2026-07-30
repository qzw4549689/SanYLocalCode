using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace SanyD365.Plugins.CustomerTag
{
    /// <summary>
    /// 客户信用标签校验Plugin
    /// 触发时机：mcs_customer_tag Update前（PreOperation）
    /// 功能：校验关联的信用评估记录状态是否为人工复核（12），非复核阶段禁止修改标签数据
    /// </summary>
    public class CustomerTagValidationPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = factory.CreateOrganizationService(context.UserId);
            ITracingService tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracer.Trace("===== CustomerTagValidationPlugin 开始执行 =====");

            // 只处理 Update 前事件
            if (context.MessageName != "Update" || context.Stage != 20)
            {
                tracer.Trace("非Update前事件，跳过");
                return;
            }

            // Plugin内部调用(如Coface数据集成)时跳过校验，允许在数据集成阶段写入标签
            if (context.Depth > 1)
            {
                tracer.Trace($"Plugin内部调用(depth={context.Depth})，跳过状态校验");
                return;
            }

            // 获取Target实体
            if (!context.InputParameters.Contains("Target") || !(context.InputParameters["Target"] is Entity))
            {
                tracer.Trace("未找到Target实体");
                return;
            }

            Entity target = (Entity)context.InputParameters["Target"];

            if (target.LogicalName != "mcs_customer_tag")
            {
                tracer.Trace($"实体不匹配: {target.LogicalName}");
                return;
            }

            try
            {
                // 获取关联的信用评估记录
                Guid? creditRecordId = null;
                
                // 先从Target中尝试获取
                if (target.Contains("mcs_credit_record") && target["mcs_credit_record"] is EntityReference creditRef)
                {
                    creditRecordId = creditRef.Id;
                }
                else
                {
                    // 从数据库中查询当前标签记录
                    var tagRecord = service.Retrieve("mcs_customer_tag", target.Id, 
                        new ColumnSet("mcs_credit_record"));
                    
                    if (tagRecord.Contains("mcs_credit_record") && tagRecord["mcs_credit_record"] is EntityReference existingRef)
                    {
                        creditRecordId = existingRef.Id;
                    }
                }

                if (!creditRecordId.HasValue)
                {
                    tracer.Trace("标签记录未关联信用评估，跳过校验");
                    return;
                }

                // 查询关联的信用评估记录状态
                var creditRecord = service.Retrieve("mcs_credit_record", creditRecordId.Value,
                    new ColumnSet("mcs_status"));

                int status = creditRecord.GetAttributeValue<OptionSetValue>("mcs_status")?.Value ?? 0;
                tracer.Trace($"关联评估记录状态: {status}");

                // 只有状态12（人工复核）才允许修改
                if (status != 12)
                {
                    string statusName = GetStatusName(status);
                    tracer.Trace($"当前状态为{statusName}({status})，不允许修改标签数据");
                    throw new InvalidPluginExecutionException(
                        $"当前评估状态为【{statusName}】，只有在【人工复核】阶段才能修改信用标签数据。");
                }

                // 校验只能修改复核字段
                ValidateReviewFieldsOnly(target, service, tracer);

                tracer.Trace("校验通过：人工复核阶段，允许修改标签数据");
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                tracer.Trace($"校验异常: {ex.Message}");
                throw new InvalidPluginExecutionException($"标签数据校验失败: {ex.Message}");
            }

            tracer.Trace("===== CustomerTagValidationPlugin 执行完成 =====");
        }

        /// <summary>
        /// 复核阶段只允许修改复核定量指标和复核定性指标。
        /// 根据关联评分项目的数据类型进一步限制：定量只能改定量复核字段，定性只能改定性复核字段。
        /// </summary>
        private void ValidateReviewFieldsOnly(Entity target, IOrganizationService service, ITracingService tracer)
        {
            // D365 Update Target 中常携带的系统字段，应统一忽略，避免误拦截
            var ignoredSystemFields = new System.Collections.Generic.HashSet<string>(
                System.StringComparer.OrdinalIgnoreCase)
            {
                "mcs_customer_tagid",
                "createdon",
                "createdby",
                "createdonbehalfby",
                "modifiedon",
                "modifiedby",
                "modifiedonbehalfby",
                "statecode",
                "statuscode",
                "ownerid",
                "owninguser",
                "owningteam",
                "owningbusinessunit",
                "versionnumber",
                "importsequencenumber",
                "overriddencreatedon",
                "timezoneruleversionnumber",
                "utcconversiontimezonecode",
                "transactioncurrencyid",
                "exchangerate"
            };

            // 根据关联评分项目的数据类型决定允许修改的复核字段
            var allowedFields = GetAllowedReviewFields(target, service, tracer);

            // 生成明确的错误提示
            string errorMessage = GetDataTypeSpecificErrorMessage(target, service, allowedFields);

            foreach (var attrName in target.Attributes.Keys)
            {
                // 跳过主键及系统字段
                if (ignoredSystemFields.Contains(attrName))
                {
                    continue;
                }

                if (!allowedFields.Contains(attrName))
                {
                    tracer.Trace($"复核阶段不允许修改字段: {attrName}");
                    throw new InvalidPluginExecutionException(
                        $"{errorMessage}，其他字段不可修改");
                }
            }
        }

        /// <summary>
        /// 根据评分项目数据类型返回针对性的错误提示。
        /// </summary>
        private string GetDataTypeSpecificErrorMessage(Entity target, IOrganizationService service, System.Collections.Generic.HashSet<string> allowedFields)
        {
            Guid? creditItemId = null;

            if (target.Contains("mcs_credit_item") && target["mcs_credit_item"] is EntityReference itemRef)
            {
                creditItemId = itemRef.Id;
            }
            else
            {
                var tagRecord = service.Retrieve("mcs_customer_tag", target.Id, new ColumnSet("mcs_credit_item"));
                if (tagRecord.Contains("mcs_credit_item") && tagRecord["mcs_credit_item"] is EntityReference existingRef)
                {
                    creditItemId = existingRef.Id;
                }
            }

            if (creditItemId.HasValue)
            {
                var creditItem = service.Retrieve("mcs_credit_items", creditItemId.Value, new ColumnSet("mcs_datatype"));
                int dataType = creditItem.GetAttributeValue<OptionSetValue>("mcs_datatype")?.Value ?? 0;

                if (dataType == 100000000)
                {
                    return "当前评分项目的数据类型=【定量】，复核阶段只可修改【复核定量指标】";
                }

                if (dataType == 100000001)
                {
                    return "当前评分项目的数据类型=【定性】，复核阶段只可修改【复核定性指标】";
                }
            }

            return "复核阶段只允许修改【复核定量指标】和【复核定性指标】";
        }

        /// <summary>
        /// 根据 customer tag 关联的评分项目数据类型，返回允许修改的复核字段集合。
        /// mcs_credit_items.mcs_datatype: 100000000=定量, 100000001=定性
        /// </summary>
        private System.Collections.Generic.HashSet<string> GetAllowedReviewFields(
            Entity target, IOrganizationService service, ITracingService tracer)
        {
            Guid? creditItemId = null;

            // 优先从 Target 中获取
            if (target.Contains("mcs_credit_item") && target["mcs_credit_item"] is EntityReference itemRef)
            {
                creditItemId = itemRef.Id;
            }
            else
            {
                // 从数据库查询当前标签记录的关联评分项目
                var tagRecord = service.Retrieve("mcs_customer_tag", target.Id, new ColumnSet("mcs_credit_item"));
                if (tagRecord.Contains("mcs_credit_item") && tagRecord["mcs_credit_item"] is EntityReference existingRef)
                {
                    creditItemId = existingRef.Id;
                }
            }

            if (creditItemId.HasValue)
            {
                var creditItem = service.Retrieve("mcs_credit_items", creditItemId.Value, new ColumnSet("mcs_datatype"));
                int dataType = creditItem.GetAttributeValue<OptionSetValue>("mcs_datatype")?.Value ?? 0;
                tracer.Trace($"关联评分项目数据类型: {dataType}");

                if (dataType == 100000000)
                {
                    // 定量：只允许修改复核定量指标
                    return new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
                    {
                        "mcs_itemintvalue2"
                    };
                }

                if (dataType == 100000001)
                {
                    // 定性：只允许修改复核定性指标
                    return new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
                    {
                        "mcs_credititem_value"
                    };
                }
            }

            tracer.Trace("未识别评分项目数据类型，默认允许修改两个复核字段");
            return new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            {
                "mcs_itemintvalue2",
                "mcs_credititem_value"
            };
        }

        private string GetStatusName(int status)
        {
            switch (status)
            {
                case 9: return "发起信用评估";
                case 10: return "关联客户代码";
                case 11: return "内外部数据集成";
                case 12: return "人工复核";
                case 13: return "信用分计算";
                case 14: return "审核申请";
                case 15: return "审批通过";
                case 16: return "审批未通过";
                default: return "未知";
            }
        }
    }
}
