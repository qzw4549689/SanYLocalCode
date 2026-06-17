using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace D365MetadataTool;

/// <summary>
/// 检查并修复客户主数据上的基础字段（客户编码、英文名称、国家编码）
/// </summary>
public class CheckAndFixAccountMasterDataHelper
{
    private readonly ServiceClient _service;

    public CheckAndFixAccountMasterDataHelper(ServiceClient service)
    {
        _service = service;
    }

    public void CheckAndFixByAccountName(string accountName)
    {
        Console.WriteLine($"\n=== 检查并修复客户主数据基础字段: {accountName} ===");

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
            var masterRef = account.GetAttributeValue<EntityReference>("mcs_customermasterdata");
            Console.WriteLine($"\n找到客户: {account.GetAttributeValue<string>("name")} ({accountId})");

            if (masterRef == null)
            {
                Console.WriteLine("❌ 该客户未关联客户主数据");
                continue;
            }

            var master = _service.Retrieve("mcs_customermasterdata", masterRef.Id,
                new ColumnSet("mcs_accountnumber", "mcs_englishname", "mcs_countrycode", "mcs_name"));

            Console.WriteLine($"关联客户主数据: {masterRef.Id}");
            Console.WriteLine("当前主数据字段值:");
            PrintFieldValue(master, "mcs_accountnumber", "客户编码");
            PrintFieldValue(master, "mcs_englishname", "客户英文名称");
            PrintFieldValue(master, "mcs_countrycode", "国家编码");

            var accountNumber = master.GetAttributeValue<string>("mcs_accountnumber");
            var englishName = master.GetAttributeValue<string>("mcs_englishname");
            var countryCode = master.GetAttributeValue<string>("mcs_countrycode");

            var needUpdate = string.IsNullOrEmpty(accountNumber) ||
                             string.IsNullOrEmpty(englishName) ||
                             string.IsNullOrEmpty(countryCode);

            if (!needUpdate)
            {
                Console.WriteLine("✅ 所有字段已有值，无需修复");
                continue;
            }

            // 准备修复：从 account name 提取英文名称，用默认值填充
            var update = new Entity("mcs_customermasterdata", masterRef.Id);

            if (string.IsNullOrEmpty(accountNumber))
            {
                // 尝试从 account 的 accountnumber 字段获取，没有则用固定值
                var acc = _service.Retrieve("account", accountId, new ColumnSet("accountnumber"));
                var accNumber = acc.GetAttributeValue<string>("accountnumber");
                update["mcs_accountnumber"] = !string.IsNullOrEmpty(accNumber) ? accNumber : "ACN202605280000";
            }

            if (string.IsNullOrEmpty(englishName))
            {
                update["mcs_englishname"] = accountName;
            }

            if (string.IsNullOrEmpty(countryCode))
            {
                update["mcs_countrycode"] = "PL";
            }

            _service.Update(update);
            Console.WriteLine("✅ 已修复客户主数据字段:");
            if (string.IsNullOrEmpty(accountNumber)) Console.WriteLine($"   mcs_accountnumber: {update["mcs_accountnumber"]}");
            if (string.IsNullOrEmpty(englishName)) Console.WriteLine($"   mcs_englishname: {update["mcs_englishname"]}");
            if (string.IsNullOrEmpty(countryCode)) Console.WriteLine($"   mcs_countrycode: {update["mcs_countrycode"]}");
        }

        Console.WriteLine("\n=== 完成 ===");
    }

    private void PrintFieldValue(Entity entity, string fieldName, string displayName)
    {
        if (entity.Contains(fieldName) && entity[fieldName] != null)
        {
            Console.WriteLine($"   {displayName}({fieldName}): {entity[fieldName]}");
        }
        else
        {
            Console.WriteLine($"   {displayName}({fieldName}): (空)");
        }
    }
}
