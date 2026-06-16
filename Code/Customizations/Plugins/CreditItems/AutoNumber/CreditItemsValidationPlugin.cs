using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace SanyD365.Plugins.CreditItems
{
    /// <summary>
    /// 客户评分项目表 - 保存前校验Plugin
    /// 触发时机：Create前 / Update前
    /// 功能：
    /// 1. 校验评分项目编码格式（大写英文+数字，不含特殊字符）
    /// 2. 校验评分项目编码唯一性
    /// 3. 校验数据类型与相关配置一致性
    /// 影响范围：仅限mcs_credit_items实体
    /// </summary>
    public class CreditItemsValidationPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = factory.CreateOrganizationService(context.UserId);
            ITracingService tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracer.Trace("CreditItemsValidationPlugin 开始执行");

            // 只处理Create前或Update前事件
            if ((context.MessageName != "Create" && context.MessageName != "Update") || context.Stage != 20)
            {
                tracer.Trace("非Create/Update前事件，跳过");
                return;
            }

            if (!context.InputParameters.Contains("Target") || !(context.InputParameters["Target"] is Entity))
            {
                tracer.Trace("未找到Target实体");
                return;
            }

            Entity target = (Entity)context.InputParameters["Target"];
            
            if (target.LogicalName != "mcs_credit_items")
            {
                tracer.Trace($"实体不匹配: {target.LogicalName}");
                return;
            }

            try
            {
                // 校验评分项目编码
                if (target.Contains("mcs_itemid"))
                {
                    ValidateItemId(target, service, tracer, context);
                }

                // 校验必填字段
                ValidateRequiredFields(target, tracer);

                tracer.Trace("CreditItemsValidationPlugin 校验通过");
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                tracer.Trace($"校验失败: {ex.Message}");
                throw new InvalidPluginExecutionException($"评分项目校验失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 校验评分项目编码
        /// </summary>
        private void ValidateItemId(Entity target, IOrganizationService service, ITracingService tracer, IPluginExecutionContext context)
        {
            string itemId = target.GetAttributeValue<string>("mcs_itemid");
            
            if (string.IsNullOrWhiteSpace(itemId))
            {
                throw new InvalidPluginExecutionException("评分项目编码不能为空");
            }

            // 校验格式：只允许英文大写字母和数字（如 ExternalRating, RegisteredCapital）
            if (!System.Text.RegularExpressions.Regex.IsMatch(itemId, @"^[A-Za-z][A-Za-z0-9]*$"))
            {
                throw new InvalidPluginExecutionException("评分项目编码格式不正确，必须以字母开头，只能包含英文字母和数字");
            }

            tracer.Trace($"评分项目编码格式校验通过: {itemId}");

            // 校验唯一性（Update时排除自身）
            var query = new QueryExpression("mcs_credit_items")
            {
                ColumnSet = new ColumnSet("mcs_credit_itemsid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_itemid", ConditionOperator.Equal, itemId)
                    }
                },
                TopCount = 1
            };

            // Update时排除当前记录
            if (context.MessageName == "Update" && target.Contains("mcs_credit_itemsid"))
            {
                query.Criteria.Conditions.Add(
                    new ConditionExpression("mcs_credit_itemsid", ConditionOperator.NotEqual, target.Id)
                );
            }

            var result = service.RetrieveMultiple(query);
            
            if (result.Entities.Count > 0)
            {
                throw new InvalidPluginExecutionException($"评分项目编码 '{itemId}' 已存在，不能重复");
            }

            tracer.Trace("评分项目编码唯一性校验通过");
        }

        /// <summary>
        /// 校验必填字段
        /// </summary>
        private void ValidateRequiredFields(Entity target, ITracingService tracer)
        {
            // Create时校验所有必填字段
            if (target.Contains("mcs_itemname"))
            {
                string itemName = target.GetAttributeValue<string>("mcs_itemname");
                if (string.IsNullOrWhiteSpace(itemName))
                {
                    throw new InvalidPluginExecutionException("评分项目名称不能为空");
                }
            }

            if (target.Contains("mcs_itemdesc"))
            {
                string itemDesc = target.GetAttributeValue<string>("mcs_itemdesc");
                if (string.IsNullOrWhiteSpace(itemDesc))
                {
                    throw new InvalidPluginExecutionException("评分项目说明不能为空");
                }
            }

            // 校验数据类型值范围
            if (target.Contains("mcs_datatype"))
            {
                int? dataType = target.GetAttributeValue<int?>("mcs_datatype");
                if (dataType.HasValue && dataType != 1 && dataType != 2)
                {
                    throw new InvalidPluginExecutionException("数据类型只能是 1-定量 或 2-定性");
                }
            }

            // 校验内外部值范围
            if (target.Contains("mcs_source"))
            {
                string source = target.GetAttributeValue<string>("mcs_source");
                if (!string.IsNullOrEmpty(source) && source != "1" && source != "2")
                {
                    throw new InvalidPluginExecutionException("内外部只能是 1-内部 或 2-外部");
                }
            }

            // 校验人工补录值范围
            if (target.Contains("mcs_validate"))
            {
                int? validate = target.GetAttributeValue<int?>("mcs_validate");
                if (validate.HasValue && validate != 0 && validate != 1)
                {
                    throw new InvalidPluginExecutionException("人工补录只能是 0-否 或 1-是");
                }
            }

            // 校验外部提供值范围
            if (target.Contains("mcs_3p"))
            {
                int? thirdParty = target.GetAttributeValue<int?>("mcs_3p");
                if (thirdParty.HasValue && thirdParty != 0 && thirdParty != 1)
                {
                    throw new InvalidPluginExecutionException("外部提供只能是 0-否 或 1-是");
                }
            }

            tracer.Trace("必填字段校验通过");
        }
    }
}
