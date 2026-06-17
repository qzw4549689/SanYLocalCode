using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace D365MetadataTool;

/// <summary>
/// 清空指定客户及其客户主数据上的信用相关字段（用于测试流程）
/// </summary>
public class ClearAccountCreditFieldsHelper
{
    private readonly ServiceClient _service;

    public ClearAccountCreditFieldsHelper(ServiceClient service)
    {
        _service = service;
    }

    public void ClearByName(string accountName)
    {
        Console.WriteLine($"\n=== 清空客户主数据的信用字段: {accountName} ===");

        // 1. 查询 account
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
            var accountId = account.GetAttributeValue<Guid>("accountid");
            Console.WriteLine($"\n找到客户: {account.GetAttributeValue<string>("name")} ({accountId})");

            // 2. 清空客户主数据字段
            var masterRef = account.GetAttributeValue<EntityReference>("mcs_customermasterdata");
            if (masterRef != null)
            {
                ClearCustomerMasterData(masterRef.Id);
            }
            else
            {
                Console.WriteLine("⚠️ 该客户未关联客户主数据，跳过主数据清理");
            }

            // account 上的8个字段已由用户删除，不再清理
            Console.WriteLine("ℹ️ account 上的8个字段已删除，跳过");
        }

        Console.WriteLine("\n=== 完成 ===");
    }

    private void ClearCustomerMasterData(Guid masterId)
    {
        // 先读取主数据当前值（用于日志）
        var master = _service.Retrieve("mcs_customermasterdata", masterId,
            new ColumnSet("mcs_cofaceid", "mcs_dealerrank", "mcs_externalrate", "mcs_overduemodel",
                "mcs_creditscore", "mcs_creditgrade", "mcs_creditvalid", "mcs_isdd"));

        Console.WriteLine($"关联客户主数据: {masterId}");
        Console.WriteLine("清理前字段值:");
        PrintFieldValue(master, "mcs_cofaceid");
        PrintFieldValue(master, "mcs_dealerrank");
        PrintFieldValue(master, "mcs_externalrate");
        PrintFieldValue(master, "mcs_overduemodel");
        PrintFieldValue(master, "mcs_creditscore");
        PrintFieldValue(master, "mcs_creditgrade");
        PrintFieldValue(master, "mcs_creditvalid");
        PrintFieldValue(master, "mcs_isdd");

        // 清空字段
        var update = new Entity("mcs_customermasterdata", masterId);
        update["mcs_cofaceid"] = null;
        update["mcs_dealerrank"] = null;
        update["mcs_externalrate"] = null;
        update["mcs_overduemodel"] = null;
        update["mcs_creditscore"] = null;
        update["mcs_creditgrade"] = null;
        update["mcs_creditvalid"] = false;
        update["mcs_isdd"] = false;
        _service.Update(update);

        Console.WriteLine("✅ 已清空客户主数据上的8个字段");
    }

    private void PrintFieldValue(Entity entity, string fieldName)
    {
        if (entity.Contains(fieldName) && entity[fieldName] != null)
        {
            Console.WriteLine($"   {fieldName}: {entity[fieldName]}");
        }
        else
        {
            Console.WriteLine($"   {fieldName}: (空)");
        }
    }
}
