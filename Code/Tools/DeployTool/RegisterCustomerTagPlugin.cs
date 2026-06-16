using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.IO;

namespace DeployTool
{
    public class RegisterCustomerTagPlugin
    {
        public static void Register(ServiceClient service)
        {
            Console.WriteLine(">>> 注册 CustomerTagValidationPlugin (PreOperation)...");
            
            var dllPath = "/Users/peterqiu/Work/AIWorkSpace/SanYi/Code/Customizations/Plugins/CustomerTag/AutoNumber/bin/Debug/net462/SanyD365.Plugins.CustomerTag.dll";
            if (!File.Exists(dllPath))
            {
                Console.WriteLine("  ❌ DLL 不存在");
                return;
            }
            
            // 检查是否已注册
            var typeQuery = new QueryExpression("plugintype")
            {
                ColumnSet = new ColumnSet("plugintypeid"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("name", ConditionOperator.Equal, "CustomerTagValidationPlugin") }
                }
            };
            
            var types = service.RetrieveMultiple(typeQuery);
            if (types.Entities.Count == 0)
            {
                Console.WriteLine("  ❌ Plugin Type 不存在，需先注册");
                return;
            }
            
            var typeId = types.Entities[0].Id;
            
            // 注册 Step (PreOperation, Update, mcs_customer_tag)
            var newStep = new Entity("sdkmessageprocessingstep");
            newStep["name"] = "CustomerTagValidationPlugin: Update of mcs_customer_tag (PreOp)";
            newStep["plugintypeid"] = new EntityReference("plugintype", typeId);
            newStep["sdkmessageid"] = new EntityReference("sdkmessage", GetMessageId(service, "Update"));
            newStep["sdkmessagefilterid"] = new EntityReference("sdkmessagefilter", GetFilterId(service, "mcs_customer_tag"));
            newStep["stage"] = new OptionSetValue(20); // PreOperation
            newStep["mode"] = new OptionSetValue(0); // Synchronous
            newStep["rank"] = 1;
            newStep["supporteddeployment"] = new OptionSetValue(0); // ServerOnly
            newStep["filteringattributes"] = "mcs_itemintvalue2,mcs_itemtxtvalue2";
            
            var stepId = service.Create(newStep);
            Console.WriteLine($"  ✓ Step 已注册 (PreOperation): {stepId}");
        }
        
        static Guid GetMessageId(ServiceClient service, string messageName)
        {
            var query = new QueryExpression("sdkmessage")
            {
                ColumnSet = new ColumnSet("sdkmessageid"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("name", ConditionOperator.Equal, messageName) }
                }
            };
            return service.RetrieveMultiple(query).Entities[0].Id;
        }
        
        static Guid GetFilterId(ServiceClient service, string entityName)
        {
            var query = new QueryExpression("sdkmessagefilter")
            {
                ColumnSet = new ColumnSet("sdkmessagefilterid"),
                Criteria = new FilterExpression
                {
                    Conditions = 
                    {
                        new ConditionExpression("primaryobjecttypecode", ConditionOperator.Equal, entityName)
                    }
                }
            };
            var results = service.RetrieveMultiple(query);
            if (results.Entities.Count > 0) return results.Entities[0].Id;
            return Guid.Empty;
        }
    }
}
