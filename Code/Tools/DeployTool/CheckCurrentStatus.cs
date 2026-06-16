using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using System;

namespace DeployTool
{
    public class CheckCurrentStatus
    {
        public static void Check(ServiceClient service, Guid recordId)
        {
            Console.WriteLine(">>> 检查当前记录状态...");
            try
            {
                var record = service.Retrieve("mcs_credit_record", recordId, 
                    new Microsoft.Xrm.Sdk.Query.ColumnSet("mcs_status", "mcs_creditscore", "mcs_bppid", "mcs_bppstatus", "mcs_bpperrormsg", "mcs_scoreid"));
                
                var status = record.GetAttributeValue<OptionSetValue>("mcs_status")?.Value;
                var score = record.GetAttributeValue<decimal?>("mcs_creditscore");
                var bppId = record.GetAttributeValue<string>("mcs_bppid");
                var bppStatus = record.GetAttributeValue<string>("mcs_bppstatus");
                var bppError = record.GetAttributeValue<string>("mcs_bpperrormsg");
                var scoreId = record.GetAttributeValue<string>("mcs_scoreid");
                
                Console.WriteLine($"  记录ID: {recordId}");
                Console.WriteLine($"  ScoreID: {scoreId}");
                Console.WriteLine($"  状态: {status} (12=人工复核, 13=信用分计算, 14=审核申请)");
                Console.WriteLine($"  信用分: {score}");
                Console.WriteLine($"  BPP ID: {bppId}");
                Console.WriteLine($"  BPP状态: {bppStatus}");
                Console.WriteLine($"  BPP错误: {bppError}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ 查询失败: {ex.Message}");
            }
        }
    }
}
