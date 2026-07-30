using Microsoft.Xrm.Sdk;
using System;

namespace FactoryCreditTest
{
    public static class TestBlacklist
    {
        public static void Run(IOrganizationService service, Guid masterDataId)
        {
            Console.WriteLine($"=== 测试黑名单不予授信场景 {masterDataId} ===");

            Guid? accountId = FindAccount.ByMasterDataId(service, masterDataId);
            if (!accountId.HasValue)
            {
                Console.WriteLine("未找到关联 Account");
                return;
            }

            Entity update = new Entity("mcs_customermasterdata", masterDataId);
            update["mcs_creditscore"] = 85m;
            update["mcs_creditvalid"] = true;
            update["mcs_blacklist"] = true;
            service.Update(update);
            Console.WriteLine("已设置客户黑名单=true");

            // 重置在外货款为非逾期，避免场景3与场景4同时触发，确保只验证场景4
            Program.ResetOutstanding(service, accountId.Value, 30, 0m, 500000m);

            Program.TriggerCalculation(service, masterDataId);
        }
    }
}
