using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using System;

namespace DeployTool
{
    public class ResetCreditRecordStatus
    {
        public static void Reset(ServiceClient service)
        {
            Console.WriteLine(">>> 重置信用评估记录状态到人工复核(12)...");

            var recordId = new Guid("b7e04e27-0974-4fec-8994-7c409c83b620");

            try
            {
                var record = new Entity("mcs_credit_record")
                {
                    Id = recordId
                };
                record["mcs_status"] = new OptionSetValue(12);
                // 清空BPP审批字段，方便重新测试
                record["mcs_bppstatus"] = null;
                record["mcs_bppappriver"] = null;
                record["mcs_bppid"] = null;
                record["mcs_bpperrormsg"] = null;
                record["mcs_bpprejectreason"] = null;
                record["mcs_approvedate"] = null;
                // 清空信用分相关字段，重新计算
                record["mcs_creditscore"] = null;
                record["mcs_scoredate"] = null;
                record["mcs_checkdate"] = null;

                service.Update(record);
                Console.WriteLine($"  ✅ 记录 {recordId} 状态已重置为 12(人工复核)，BPP字段和信用分字段已清空");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ 重置失败: {ex.Message}");
            }
        }
    }
}
