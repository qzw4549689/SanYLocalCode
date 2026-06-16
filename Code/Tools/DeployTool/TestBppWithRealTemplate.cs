using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using System;
using System.Threading;

namespace DeployTool
{
    public class TestBppWithRealTemplate
    {
        public static void Test(ServiceClient service)
        {
            Console.WriteLine(">>> BPP快速测试（模板Code: 781094754802827383）...");
            Guid recordId = new Guid("b7e04e27-0974-4fec-8994-7c409c83b620");

            try
            {
                // 重置到13
                Console.WriteLine("  重置状态到13...");
                var reset = new Entity("mcs_credit_record") { Id = recordId };
                reset["mcs_status"] = new OptionSetValue(13);
                reset["mcs_workflowid"] = null;
                reset["mcs_bppstatus"] = null;
                reset["mcs_bpperrormsg"] = null;
                reset["mcs_nextapprover"] = null;
                service.Update(reset);
                Console.WriteLine("  ✅ 已重置");

                // 触发到14
                Console.WriteLine("  更新状态到14，触发BPP Plugin...");
                var trigger = new Entity("mcs_credit_record") { Id = recordId };
                trigger["mcs_status"] = new OptionSetValue(14);
                service.Update(trigger);
                Console.WriteLine("  ✅ 已触发");

                // 等待并查询
                Console.WriteLine("  等待异步处理（30秒）...");
                Thread.Sleep(30000);

                var result = service.Retrieve("mcs_credit_record", recordId,
                    new Microsoft.Xrm.Sdk.Query.ColumnSet("mcs_status", "mcs_workflowid", "mcs_bppstatus", "mcs_bpperrormsg", "mcs_nextapprover"));
                int status = result.GetAttributeValue<OptionSetValue>("mcs_status")?.Value ?? 0;
                string wf = result.GetAttributeValue<string>("mcs_workflowid");
                string bs = result.GetAttributeValue<string>("mcs_bppstatus");
                string err = result.GetAttributeValue<string>("mcs_bpperrormsg");
                string next = result.GetAttributeValue<string>("mcs_nextapprover");

                Console.WriteLine($"  状态={status}, workflowId={wf}, bppStatus={bs}, nextApprover={next}");
                if (!string.IsNullOrEmpty(err)) Console.WriteLine($"  错误={err}");

                if (status == 14 && !string.IsNullOrEmpty(wf))
                    Console.WriteLine("  ✅ BPP审批发起成功！");
                else if (status == 13 && !string.IsNullOrEmpty(err))
                    Console.WriteLine($"  ❌ 失败: {err}");
                else
                    Console.WriteLine("  ⚠️ 状态未变或异步处理中");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ 异常: {ex.Message}");
            }
        }
    }
}
