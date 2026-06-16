using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace DeployTool
{
    public class UpdateFormLayout
    {
        public static void Update(ServiceClient service)
        {
            Console.WriteLine(">>> 更新表单布局，添加BPP字段...");
            
            try
            {
                var query = new QueryExpression("systemform")
                {
                    ColumnSet = new ColumnSet("formid", "name", "type", "formxml"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("objecttypecode", ConditionOperator.Equal, "mcs_credit_record"),
                            new ConditionExpression("type", ConditionOperator.Equal, 2)
                        }
                    }
                };
                
                var results = service.RetrieveMultiple(query);
                Console.WriteLine($"  找到 {results.Entities.Count} 个主表单");
                
                foreach (var form in results.Entities)
                {
                    string formName = form.GetAttributeValue<string>("name");
                    Guid formId = form.Id;
                    string formXml = form.GetAttributeValue<string>("formxml");
                    
                    Console.WriteLine($"  表单: {formName} ({formId})");
                    
                    bool hasWorkflowId = formXml.Contains("mcs_workflowid");
                    bool hasNextApprover = formXml.Contains("mcs_nextapprover");
                    
                    Console.WriteLine($"    有mcs_workflowid: {hasWorkflowId}");
                    Console.WriteLine($"    有mcs_nextapprover: {hasNextApprover}");
                    
                    if (!hasWorkflowId || !hasNextApprover)
                    {
                        string updatedXml = AddFieldsToFormXml(formXml, hasWorkflowId, hasNextApprover);
                        if (updatedXml != formXml)
                        {
                            var updateForm = new Entity("systemform") { Id = formId };
                            updateForm["formxml"] = updatedXml;
                            service.Update(updateForm);
                            Console.WriteLine($"    ✅ 表单已更新");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"    ⬜ 字段已存在，跳过");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  失败: {ex.Message}");
            }
        }
        
        private static string AddFieldsToFormXml(string formXml, bool hasWorkflowId, bool hasNextApprover)
        {
            if (hasWorkflowId && hasNextApprover) return formXml;
            
            string newRows = "";
            
            if (!hasWorkflowId)
            {
                newRows += $@"
                    <row>
                      <cell id=""{{{Guid.NewGuid()}}}"" showlabel=""true"" locklevel=""0"">
                        <labels>
                          <label description=""BPP流程实例ID"" languagecode=""2052"" />
                        </labels>
                        <control id=""mcs_workflowid"" classid=""{{4273EDBD-AC1D-40d3-9FB2-095C621B552D}}"" datafieldname=""mcs_workflowid"" disabled=""true"" />
                      </cell>
                    </row>";
            }
            
            if (!hasNextApprover)
            {
                newRows += $@"
                    <row>
                      <cell id=""{{{Guid.NewGuid()}}}"" showlabel=""true"" locklevel=""0"">
                        <labels>
                          <label description=""当前审批人"" languagecode=""2052"" />
                        </labels>
                        <control id=""mcs_nextapprover"" classid=""{{4273EDBD-AC1D-40d3-9FB2-095C621B552D}}"" datafieldname=""mcs_nextapprover"" disabled=""true"" />
                      </cell>
                    </row>";
            }
            
            // 在最后一个 </rows> 标签之前插入新行
            int lastRowsEnd = formXml.LastIndexOf("</rows>");
            if (lastRowsEnd > 0)
            {
                return formXml.Insert(lastRowsEnd, newRows);
            }
            
            return formXml;
        }
    }
}
