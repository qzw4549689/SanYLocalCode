using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace SanyD365.Plugins.CreditItemValue
{
    /// <summary>
    /// 评分项目枚举值表 - 保存前校验Plugin
    /// 触发时机：Create前 / Update前
    /// 功能：
    /// 1. 校验关联的评分项目是否为定性类型
    /// 2. 校验选择项编码唯一性（同一评分项目下）
    /// 3. 校验必填字段
    /// 影响范围：仅限mcs_credititem_value实体
    /// </summary>
    public class CreditItemValueValidationPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = factory.CreateOrganizationService(context.UserId);
            ITracingService tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracer.Trace("CreditItemValueValidationPlugin 开始执行");

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
            
            if (target.LogicalName != "mcs_credititem_value")
            {
                tracer.Trace($"实体不匹配: {target.LogicalName}");
                return;
            }

            try
            {
                // 校验关联的评分项目是否为定性
                if (target.Contains("mcs_itemid"))
                {
                    ValidateQualitativeItem(target, service, tracer);
                }

                // 校验选择项编码唯一性
                if (target.Contains("mcs_listvalue"))
                {
                    ValidateListValueUnique(target, service, tracer, context);
                }

                // 校验必填字段
                ValidateRequiredFields(target, tracer);

                tracer.Trace("CreditItemValueValidationPlugin 校验通过");
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                tracer.Trace($"校验失败: {ex.Message}");
                throw new InvalidPluginExecutionException($"枚举值校验失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 校验关联的评分项目是否为定性类型
        /// </summary>
        private void ValidateQualitativeItem(Entity target, IOrganizationService service, ITracingService tracer)
        {
            // 获取评分项目编码（Lookup字段的值）
            EntityReference itemRef = null;
            
            if (target.Contains("mcs_itemid"))
            {
                var itemAttr = target["mcs_itemid"];
                if (itemAttr is EntityReference)
                {
                    itemRef = (EntityReference)itemAttr;
                }
                else if (itemAttr is string)
                {
                    // 如果是文本类型，通过编码查询
                    string itemCode = (string)itemAttr;
                    var query = new QueryExpression("mcs_credit_items")
                    {
                        ColumnSet = new ColumnSet("mcs_datatype"),
                        Criteria = new FilterExpression
                        {
                            Conditions =
                            {
                                new ConditionExpression("mcs_itemid", ConditionOperator.Equal, itemCode)
                            }
                        },
                        TopCount = 1
                    };
                    
                    var result = service.RetrieveMultiple(query);
                    if (result.Entities.Count > 0)
                    {
                        int dataType = result.Entities[0].GetAttributeValue<int>("mcs_datatype");
                        if (dataType != 2)
                        {
                            throw new InvalidPluginExecutionException("该评分项目为定量指标，不能配置枚举值");
                        }
                        tracer.Trace($"评分项目 {itemCode} 为定性类型，校验通过");
                        return;
                    }
                    else
                    {
                        throw new InvalidPluginExecutionException($"评分项目编码 '{itemCode}' 不存在");
                    }
                }
            }

            if (itemRef != null)
            {
                // 通过Lookup查询评分项目数据类型
                var item = service.Retrieve("mcs_credit_items", itemRef.Id, new ColumnSet("mcs_datatype", "mcs_itemid"));
                if (item != null)
                {
                    int dataType = item.GetAttributeValue<int>("mcs_datatype");
                    if (dataType != 2)
                    {
                        string itemCode = item.GetAttributeValue<string>("mcs_itemid") ?? "";
                        throw new InvalidPluginExecutionException($"评分项目 '{itemCode}' 为定量指标，不能配置枚举值");
                    }
                    tracer.Trace($"评分项目 {itemRef.Id} 为定性类型，校验通过");
                }
            }
        }

        /// <summary>
        /// 校验选择项编码唯一性（同一评分项目下）
        /// </summary>
        private void ValidateListValueUnique(Entity target, IOrganizationService service, ITracingService tracer, IPluginExecutionContext context)
        {
            string listValue = target.GetAttributeValue<string>("mcs_listvalue");
            
            if (string.IsNullOrWhiteSpace(listValue))
            {
                throw new InvalidPluginExecutionException("选择项编码不能为空");
            }

            // 获取评分项目编码
            string itemCode = null;
            
            if (target.Contains("mcs_itemid"))
            {
                var itemAttr = target["mcs_itemid"];
                if (itemAttr is EntityReference)
                {
                    var itemRef = (EntityReference)itemAttr;
                    var item = service.Retrieve("mcs_credit_items", itemRef.Id, new ColumnSet("mcs_itemid"));
                    if (item != null)
                    {
                        itemCode = item.GetAttributeValue<string>("mcs_itemid");
                    }
                }
                else if (itemAttr is string)
                {
                    itemCode = (string)itemAttr;
                }
            }
            else if (context.MessageName == "Update")
            {
                // Update时从数据库获取当前记录的评分项目编码
                var currentRecord = service.Retrieve("mcs_credititem_value", target.Id, new ColumnSet("mcs_itemid"));
                if (currentRecord != null && currentRecord.Contains("mcs_itemid"))
                {
                    var itemAttr = currentRecord["mcs_itemid"];
                    if (itemAttr is EntityReference)
                    {
                        var itemRef = (EntityReference)itemAttr;
                        var item = service.Retrieve("mcs_credit_items", itemRef.Id, new ColumnSet("mcs_itemid"));
                        if (item != null)
                        {
                            itemCode = item.GetAttributeValue<string>("mcs_itemid");
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(itemCode))
            {
                tracer.Trace("无法获取评分项目编码，跳过唯一性校验");
                return;
            }

            // 查询同一评分项目下是否已存在该选择项编码
            var query = new QueryExpression("mcs_credititem_value")
            {
                ColumnSet = new ColumnSet("mcs_credititem_valueid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_itemid", ConditionOperator.Equal, itemCode),
                        new ConditionExpression("mcs_listvalue", ConditionOperator.Equal, listValue)
                    }
                },
                TopCount = 1
            };

            // Update时排除自身
            if (context.MessageName == "Update")
            {
                query.Criteria.Conditions.Add(
                    new ConditionExpression("mcs_credititem_valueid", ConditionOperator.NotEqual, target.Id)
                );
            }

            var result = service.RetrieveMultiple(query);
            
            if (result.Entities.Count > 0)
            {
                throw new InvalidPluginExecutionException($"评分项目 '{itemCode}' 下已存在选择项编码 '{listValue}'");
            }

            tracer.Trace($"选择项编码唯一性校验通过: {itemCode}/{listValue}");
        }

        /// <summary>
        /// 校验必填字段
        /// </summary>
        private void ValidateRequiredFields(Entity target, ITracingService tracer)
        {
            if (target.Contains("mcs_listname"))
            {
                string listName = target.GetAttributeValue<string>("mcs_listname");
                if (string.IsNullOrWhiteSpace(listName))
                {
                    throw new InvalidPluginExecutionException("选择项目名称不能为空");
                }
            }

            tracer.Trace("必填字段校验通过");
        }
    }
}
