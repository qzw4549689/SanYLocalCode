using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace SanyD365.Plugins.TradeStPayTerm.Sync
{
    /// <summary>
    /// 成交条件产品分类关系：根据产品线 Lookup 自动同步产品线编码和名称
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
                if (!target.Contains("mcs_productlineid"))
                {
                    return;
                }

                var lookupValue = target.GetAttributeValue<EntityReference>("mcs_productlineid");
                if (lookupValue == null || lookupValue.Id == Guid.Empty)
                {
                    target["mcs_groupid"] = null;
                    target["mcs_groupname"] = null;
                    tracer.Trace("产品线 Lookup 已清空，同步清空产品线编码和名称");
                    return;
                }

                var productLine = service.Retrieve("mcs_productline", lookupValue.Id, new ColumnSet("mcs_code", "mcs_name"));
                if (productLine == null)
                {
                    tracer.Trace($"未找到产品线记录: {lookupValue.Id}");
                    return;
                }

                var code = productLine.GetAttributeValue<string>("mcs_code");
                var name = productLine.GetAttributeValue<string>("mcs_name");

                target["mcs_groupid"] = code;
                target["mcs_groupname"] = name;

                tracer.Trace($"产品线同步完成: {code} - {name}");
            }
        }
    }
}
