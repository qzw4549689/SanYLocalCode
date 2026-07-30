using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace D365MetadataTool;

/// <summary>
/// 设置客户主数据上的重点尽调标志（mcs_isdd）
/// </summary>
public class SetMasterDataIsddHelper
{
    private readonly ServiceClient _service;

    public SetMasterDataIsddHelper(ServiceClient service)
    {
        _service = service;
    }

    public void SetByAccountName(string accountName, bool isDD)
    {
        Console.WriteLine($"\n=== 设置客户主数据重点尽调标志: {accountName} => {isDD} ===");

        var accountQuery = new QueryExpression("account")
        {
            ColumnSet = new ColumnSet("accountid", "name", "mcs_customermasterdata"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("name", ConditionOperator.Equal, accountName) }
            }
        };

        var accountResult = _service.RetrieveMultiple(accountQuery);
        if (accountResult.Entities.Count == 0)
        {
            Console.WriteLine($"❌ 未找到客户: {accountName}");
            return;
        }

        foreach (var account in accountResult.Entities)
        {
            var masterRef = account.GetAttributeValue<EntityReference>("mcs_customermasterdata");
            Console.WriteLine($"找到客户: {account.GetAttributeValue<string>("name")} ({account.Id})");

            if (masterRef == null)
            {
                Console.WriteLine("❌ 该客户未关联客户主数据");
                continue;
            }

            var update = new Entity("mcs_customermasterdata", masterRef.Id);
            update["mcs_isdd"] = isDD;
            _service.Update(update);

            Console.WriteLine($"✅ 已更新客户主数据 {masterRef.Id} 的 mcs_isdd 为 {isDD}");
        }

        Console.WriteLine("\n=== 完成 ===");
    }
}
