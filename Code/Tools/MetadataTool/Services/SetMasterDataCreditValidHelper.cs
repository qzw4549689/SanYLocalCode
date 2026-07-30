using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace D365MetadataTool;

/// <summary>
/// 设置客户主数据上的信用评估有效状态（mcs_creditvalid）
/// </summary>
public class SetMasterDataCreditValidHelper
{
    private readonly ServiceClient _service;

    public SetMasterDataCreditValidHelper(ServiceClient service)
    {
        _service = service;
    }

    public void SetByAccountName(string accountName, bool creditValid)
    {
        Console.WriteLine($"\n=== 设置客户主数据信用评估有效状态: {accountName} => {creditValid} ===");

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
            update["mcs_creditvalid"] = creditValid;
            _service.Update(update);

            Console.WriteLine($"✅ 已更新客户主数据 {masterRef.Id} 的 mcs_creditvalid 为 {creditValid}");
        }

        Console.WriteLine("\n=== 完成 ===");
    }
}
