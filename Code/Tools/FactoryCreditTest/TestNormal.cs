using Microsoft.Xrm.Sdk;
using System;

namespace FactoryCreditTest
{
    public static class TestNormal
    {
        public static void Run(IOrganizationService service, Guid masterDataId)
        {
            Console.WriteLine($"=== 测试正常三因子计算场景 {masterDataId} ===");

            Guid? accountId = FindAccount.ByMasterDataId(service, masterDataId);
            if (!accountId.HasValue)
            {
                Console.WriteLine("未找到关联 Account");
                return;
            }

            Entity update = new Entity("mcs_customermasterdata", masterDataId);
            update["mcs_creditscore"] = 85m;
            update["mcs_creditvalid"] = true;
            update["mcs_blacklist"] = false;
            service.Update(update);
            Console.WriteLine("已恢复客户信用分=85，信用评估有效=true，黑名单=false");

            // 重置在外货款为非逾期，确保不触发场景3
            Program.ResetOutstanding(service, accountId.Value, 30, 0m, 500000m);

            Program.TriggerCalculation(service, masterDataId);
        }
    }
}
