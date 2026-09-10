using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace SanyD365.Plugins.TradeStPayTerm
{
    /// <summary>
    /// 成交条件产品分类关系：保存时校验产品线/产品分类 Lookup 并同步编码和名称
    /// </summary>
    public class TradePtGroupTypeProductLineSyncPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var service = serviceFactory.CreateOrganizationService(context.UserId);
            var tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity target)
            {
                if (target.LogicalName != "mcs_trade_ptgrouptype")
                {
                    return;
                }

                var isCreate = context.MessageName == "Create";
                var isUpdate = context.MessageName == "Update";

                if (!isCreate && !isUpdate)
                {
                    return;
                }

                // #1839：Update 必须合并数据库当前值校验最终状态，
                // 防止存量脏数据（产品线/产品分类为空）只改其他字段仍能保存
                Entity current = null;
                if (isUpdate)
                {
                    current = service.Retrieve(target.LogicalName, target.Id,
                        new ColumnSet("mcs_productlineid", "mcs_trade_pttypeid"));
                }

                ValidateAndSyncProductLine(service, tracer, target, current);
                ValidateAndSyncTradePtType(service, tracer, target, current);
            }
        }

        /// <summary>
        /// 校验产品线 Lookup 必须指向有效的基础数据记录，并同步编码/名称
        /// </summary>
        private void ValidateAndSyncProductLine(IOrganizationService service, ITracingService tracer, Entity target, Entity current)
        {
            // Target 中有变更用新值，否则取数据库当前值（Update 合并校验最终状态）
            var changed = target.Contains("mcs_productlineid");
            var lookupValue = changed
                ? target.GetAttributeValue<EntityReference>("mcs_productlineid")
                : current?.GetAttributeValue<EntityReference>("mcs_productlineid");
            if (lookupValue == null || lookupValue.Id == Guid.Empty)
            {
                throw new InvalidPluginExecutionException("产品线不能为空，请选择有效的产品线。");
            }

            var productLine = service.Retrieve("mcs_productline", lookupValue.Id, new ColumnSet("mcs_code", "mcs_name"));
            if (productLine == null)
            {
                throw new InvalidPluginExecutionException($"未找到对应的产品线记录（ID: {lookupValue.Id}），请确认产品线基础数据存在。");
            }

            var code = productLine.GetAttributeValue<string>("mcs_code");
            var name = productLine.GetAttributeValue<string>("mcs_name");

            if (string.IsNullOrWhiteSpace(code))
            {
                throw new InvalidPluginExecutionException("所选产品线的产品线编码为空，请完善产品线基础数据。");
            }

            // 仅产品线发生变更（或新建带入）时才回写编码/名称
            if (changed)
            {
                target["mcs_groupid"] = code;
                target["mcs_groupname"] = name;
            }

            tracer.Trace($"产品线同步完成: {code} - {name}");
        }

        /// <summary>
        /// 校验成交条件产品分类 Lookup 必须指向有效的基础数据记录，并同步编码/名称
        /// </summary>
        private void ValidateAndSyncTradePtType(IOrganizationService service, ITracingService tracer, Entity target, Entity current)
        {
            // Target 中有变更用新值，否则取数据库当前值（Update 合并校验最终状态）
            var changed = target.Contains("mcs_trade_pttypeid");
            var lookupValue = changed
                ? target.GetAttributeValue<EntityReference>("mcs_trade_pttypeid")
                : current?.GetAttributeValue<EntityReference>("mcs_trade_pttypeid");
            if (lookupValue == null || lookupValue.Id == Guid.Empty)
            {
                throw new InvalidPluginExecutionException("成交条件产品分类不能为空，请选择有效的产品分类。");
            }

            var tradePtType = service.Retrieve("mcs_trade_pttype", lookupValue.Id, new ColumnSet("mcs_typeid", "mcs_trade_pttypename"));
            if (tradePtType == null)
            {
                throw new InvalidPluginExecutionException($"未找到对应的成交条件产品分类记录（ID: {lookupValue.Id}），请确认产品分类基础数据存在。");
            }

            var code = tradePtType.GetAttributeValue<string>("mcs_typeid");
            var name = tradePtType.GetAttributeValue<string>("mcs_trade_pttypename");

            if (string.IsNullOrWhiteSpace(code))
            {
                throw new InvalidPluginExecutionException("所选成交条件产品分类的产品分类编码为空，请完善产品分类基础数据。");
            }

            // 仅产品分类发生变更（或新建带入）时才回写编码/名称
            if (changed)
            {
                target["mcs_typeid"] = code;
                target["mcs_typename"] = name;
            }

            tracer.Trace($"成交条件产品分类同步完成: {code} - {name}");
        }
    }
}
