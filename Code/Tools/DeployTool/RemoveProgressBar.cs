using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace DeployTool
{
    public class RemoveProgressBar
    {
        public static void Run(ServiceClient service)
        {
            Console.WriteLine(">>> 移除自定义进度条...");

            var query = new QueryExpression("systemform")
            {
                ColumnSet = new ColumnSet("formxml"),
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
            if (results.Entities.Count == 0)
            {
                Console.WriteLine("  ❌ 未找到表单");
                return;
            }

            var form = results.Entities[0];
            var formXml = form.GetAttributeValue<string>("formxml");
            var formId = form.Id;

            // 检查是否包含进度条 section
            if (!formXml.Contains("section_progress_bar"))
            {
                Console.WriteLine("  表单中未找到进度条 section");
                return;
            }

            // 找到 section_progress_bar 并移除
            var sectionStart = formXml.IndexOf("<section name=\"section_progress_bar\"");
            if (sectionStart >= 0)
            {
                // 找到对应的 </section>
                var sectionEnd = formXml.IndexOf("</section>", sectionStart);
                if (sectionEnd > sectionStart)
                {
                    sectionEnd += "</section>".Length;
                    formXml = formXml.Substring(0, sectionStart) + formXml.Substring(sectionEnd);
                    
                    // 更新表单
                    var updatedForm = new Entity("systemform", formId);
                    updatedForm["formxml"] = formXml;
                    service.Update(updatedForm);
                    
                    Console.WriteLine("  ✅ 进度条 section 已移除");
                }
            }
        }
    }
}
