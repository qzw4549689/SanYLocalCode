using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace SanyD365.Plugins.CustomerTag
{
    /// <summary>
    /// 客户信用标签表 - 创建初始化Plugin
    /// 触发时机：Create后
    /// 功能：
    /// 1. 将集成指标值复制到复核指标值（初始化复核字段）
    /// 2. 更新合并展示值
    /// 3. 带出评分项目相关信息（名称、说明、数据类型等）
    /// 影响范围：仅限mcs_customer_tag实体
    /// </summary>
    public class CustomerTagInitPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = factory.CreateOrganizationService(context.UserId);
            ITracingService tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracer.Trace("CustomerTagInitPlugin 开始执行");

            // 只处理Create后事件
            if (context.MessageName != "Create" || context.Stage != 40)
            {
                tracer.Trace("非Create后事件，跳过");
                return;
            }

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
                // 获取评分项目信息并更新标签记录
                UpdateTagWithItemInfo(target, service, tracer);
                
                // 复制集成值到复核值
                CopyIntegrationToReview(target, service, tracer);

                tracer.Trace("CustomerTagInitPlugin 执行完成");
            }
            catch (Exception ex)
            {
                tracer.Trace($"初始化失败: {ex.Message}");
                // 初始化失败不阻断流程，记录日志即可
                // 因为标签数据可以在后续环节补充
            }
        }

        /// <summary>
        /// 从评分项目表带出信息到标签表
        /// </summary>
        private void UpdateTagWithItemInfo(Entity target, IOrganizationService service, ITracingService tracer)
        {
            // 获取评分项目编码
            string itemCode = null;
            
            if (target.Contains("mcs_itemid"))
            {
                var itemAttr = target["mcs_itemid"];
                if (itemAttr is string)
                {
                    itemCode = (string)itemAttr;
                }
                else if (itemAttr is EntityReference)
                {
                    var itemRef = (EntityReference)itemAttr;
                    var itemEntity = service.Retrieve("mcs_credit_items", itemRef.Id, new ColumnSet("mcs_itemid"));
                    if (itemEntity != null)
                    {
                        itemCode = itemEntity.GetAttributeValue<string>("mcs_itemid");
                    }
                }
            }

            if (string.IsNullOrEmpty(itemCode))
            {
                tracer.Trace("评分项目编码为空，跳过信息带出");
                return;
            }

            // 查询评分项目信息
            var query = new QueryExpression("mcs_credit_items")
            {
                ColumnSet = new ColumnSet("mcs_itemname", "mcs_itemdesc", "mcs_datatype", "mcs_group"),
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
            
            if (result.Entities.Count == 0)
            {
                tracer.Trace($"评分项目 {itemCode} 不存在");
                return;
            }

            var item = result.Entities[0];
            
            // 准备更新实体
            var updateEntity = new Entity("mcs_customer_tag")
            {
                Id = target.Id
            };

            // 带出评分项目名称
            string itemName = item.GetAttributeValue<string>("mcs_itemname");
            if (!string.IsNullOrEmpty(itemName))
            {
                updateEntity["mcs_itemname"] = itemName;
                tracer.Trace($"带出评分项目名称: {itemName}");
            }

            // 带出评分项目说明
            string itemDesc = item.GetAttributeValue<string>("mcs_itemdesc");
            if (!string.IsNullOrEmpty(itemDesc))
            {
                updateEntity["mcs_itemdesc"] = itemDesc;
                tracer.Trace($"带出评分项目说明: {itemDesc}");
            }

            // 带出数据类型
            if (item.Contains("mcs_datatype"))
            {
                int dataType = item.GetAttributeValue<int>("mcs_datatype");
                updateEntity["mcs_datatype"] = dataType;
                tracer.Trace($"带出数据类型: {dataType}");
            }

            // 带出评分项目分类
            if (item.Contains("mcs_group"))
            {
                int group = item.GetAttributeValue<int>("mcs_group");
                updateEntity["mcs_group"] = group;
                tracer.Trace($"带出评分项目分类: {group}");
            }

            // 执行更新
            service.Update(updateEntity);
            tracer.Trace("评分项目信息带出完成");
        }

        /// <summary>
        /// 复制集成值到复核值
        /// </summary>
        private void CopyIntegrationToReview(Entity target, IOrganizationService service, ITracingService tracer)
        {
            // 重新读取当前记录（可能已更新）
            var currentRecord = service.Retrieve("mcs_customer_tag", target.Id, 
                new ColumnSet("mcs_datatype", "mcs_itemintvalue1", "mcs_itemtxtvalue1"));
            
            if (currentRecord == null)
            {
                tracer.Trace("无法读取当前记录");
                return;
            }

            int dataType = currentRecord.GetAttributeValue<int>("mcs_datatype");
            
            var updateEntity = new Entity("mcs_customer_tag")
            {
                Id = target.Id
            };

            string displayValue = "";

            if (dataType == 1)
            {
                // 定量：复制集成定量指标到复核定量指标
                if (currentRecord.Contains("mcs_itemintvalue1"))
                {
                    decimal intValue = currentRecord.GetAttributeValue<decimal>("mcs_itemintvalue1");
                    updateEntity["mcs_itemintvalue2"] = intValue;
                    displayValue = intValue.ToString("F2");
                    tracer.Trace($"复制定量值: {intValue}");
                }
            }
            else if (dataType == 2)
            {
                // 定性：复制集成定性指标到复核定性指标
                if (currentRecord.Contains("mcs_itemtxtvalue1"))
                {
                    string txtValue = currentRecord.GetAttributeValue<string>("mcs_itemtxtvalue1");
                    updateEntity["mcs_itemtxtvalue2"] = txtValue;
                    displayValue = txtValue ?? "";
                    tracer.Trace($"复制定性值: {txtValue}");
                }
            }

            // 更新集成合并展示值
            if (currentRecord.Contains("mcs_itemintvalue1") || currentRecord.Contains("mcs_itemtxtvalue1"))
            {
                string intDisplay = "";
                string txtDisplay = "";
                
                if (currentRecord.Contains("mcs_itemintvalue1"))
                {
                    intDisplay = currentRecord.GetAttributeValue<decimal>("mcs_itemintvalue1").ToString("F2");
                }
                if (currentRecord.Contains("mcs_itemtxtvalue1"))
                {
                    txtDisplay = currentRecord.GetAttributeValue<string>("mcs_itemtxtvalue1") ?? "";
                }
                
                updateEntity["mcs_itemvalue1"] = dataType == 1 ? intDisplay : txtDisplay;
                tracer.Trace($"更新集成展示值: {updateEntity["mcs_itemvalue1"]}");
            }

            // 更新复核合并展示值
            if (!string.IsNullOrEmpty(displayValue))
            {
                updateEntity["mcs_itemvalue2"] = displayValue;
                tracer.Trace($"更新复核展示值: {displayValue}");
            }

            // 设置默认有效状态
            updateEntity["mcs_active"] = true;
            
            // 设置默认不参与评分（等评分卡计算时更新）
            updateEntity["mcs_isscore"] = false;

            service.Update(updateEntity);
            tracer.Trace("复制集成值到复核值完成");
        }
    }
}
