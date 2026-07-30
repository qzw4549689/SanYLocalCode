using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace D365MetadataTool;

/// <summary>
/// 客户/客户主数据 科法斯客户代码（mcs_cofaceid）查询与设置
/// 用法：
///   查询：dotnet run customer-coface <客户编号或名称关键字>
///   设置：dotnet run set-customer-coface <客户编号> <icon#数字>
/// </summary>
public class CustomerCofaceIdHelper
{
    private readonly ServiceClient _service;

    public CustomerCofaceIdHelper(ServiceClient service)
    {
        _service = service;
    }

    /// <summary>
    /// 只读查询：按客户编号（mcs_sapnumber / accountnumber）或名称关键字查找客户主数据与客户，打印当前 mcs_cofaceid
    /// </summary>
    public void Query(string keyword)
    {
        Console.WriteLine($"\n=== 查询客户/客户主数据 Coface ID: {keyword} ===");

        // 1. 查客户主数据：先按 SAP 编号精确匹配，再按名称模糊匹配
        var masters = QueryMasterData(keyword);
        if (masters.Count == 0)
        {
            Console.WriteLine($"❌ 未找到客户主数据: {keyword}");
        }
        foreach (var master in masters)
        {
            PrintMasterData(master);
        }

        // 2. 查客户（account）：按 accountnumber 精确匹配，或按名称模糊匹配
        var accounts = QueryAccounts(keyword);
        if (accounts.Count == 0)
        {
            Console.WriteLine($"❌ 未找到客户(account): {keyword}");
        }
        foreach (var account in accounts)
        {
            PrintAccount(account);
        }

        Console.WriteLine("\n=== 查询完成 ===");
    }

    /// <summary>
    /// 清除客户主数据及关联客户的 mcs_cofaceid（撤销误绑），key 支持客户编号或主数据 GUID
    /// </summary>
    public void Clear(string key)
    {
        Console.WriteLine($"\n=== 清除客户 Coface ID: {key} ===");

        Entity master;
        if (Guid.TryParse(key, out var id))
        {
            master = _service.Retrieve("mcs_customermasterdata", id, MasterDataColumns());
        }
        else
        {
            var masters = QueryMasterData(key);
            if (masters.Count == 0)
            {
                Console.WriteLine($"❌ 未找到客户主数据: {key}");
                return;
            }
            if (masters.Count > 1)
            {
                Console.WriteLine($"❌ 客户编号 {key} 匹配到 {masters.Count} 条客户主数据，请改用主数据 GUID，已中止");
                foreach (var m in masters) PrintMasterData(m);
                return;
            }
            master = masters[0];
        }

        PrintMasterData(master);

        var oldCofaceId = master.GetAttributeValue<string>("mcs_cofaceid");
        if (string.IsNullOrEmpty(oldCofaceId))
        {
            Console.WriteLine("⏭️ 客户主数据 mcs_cofaceid 本就为空，无需清除");
        }
        else
        {
            var update = new Entity("mcs_customermasterdata", master.Id);
            update["mcs_cofaceid"] = null;
            _service.Update(update);
            Console.WriteLine($"✅ 客户主数据 mcs_cofaceid: {oldCofaceId} => (空)");
        }

        // 同步清除关联客户（account.mcs_cofaceid）
        var accountQuery = new QueryExpression("account")
        {
            ColumnSet = new ColumnSet("accountid", "accountnumber", "name", "mcs_cofaceid"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("mcs_customermasterdata", ConditionOperator.Equal, master.Id) }
            }
        };
        foreach (var account in _service.RetrieveMultiple(accountQuery).Entities)
        {
            var oldAccountCofaceId = account.GetAttributeValue<string>("mcs_cofaceid");
            if (string.IsNullOrEmpty(oldAccountCofaceId)) continue;
            var accountUpdate = new Entity("account", account.Id);
            accountUpdate["mcs_cofaceid"] = null;
            _service.Update(accountUpdate);
            Console.WriteLine($"✅ 客户(account) {account.GetAttributeValue<string>("name")} ({account.GetAttributeValue<string>("accountnumber")}) mcs_cofaceid: {oldAccountCofaceId} => (空)");
        }

        Console.WriteLine("\n=== 完成 ===");
    }

    /// <summary>
    /// 设置客户主数据国家编码（mcs_countrycode），key 支持客户编号或主数据 GUID（编号重复时中止并提示用 GUID）
    /// </summary>
    public void SetCountryCode(string key, string countryCode)
    {
        Console.WriteLine($"\n=== 设置客户主数据国家编码: {key} => {countryCode} ===");

        if (string.IsNullOrWhiteSpace(countryCode))
        {
            Console.WriteLine("❌ 国家编码不能为空");
            return;
        }

        Entity master;
        if (Guid.TryParse(key, out var id))
        {
            master = _service.Retrieve("mcs_customermasterdata", id, MasterDataColumns());
        }
        else
        {
            var masters = QueryMasterData(key);
            if (masters.Count == 0)
            {
                Console.WriteLine($"❌ 未找到客户主数据: {key}");
                return;
            }
            if (masters.Count > 1)
            {
                Console.WriteLine($"❌ 客户编号 {key} 匹配到 {masters.Count} 条客户主数据，请改用主数据 GUID，已中止");
                foreach (var m in masters) PrintMasterData(m);
                return;
            }
            master = masters[0];
        }

        PrintMasterData(master);

        var oldCode = master.GetAttributeValue<string>("mcs_countrycode");
        if (oldCode == countryCode)
        {
            Console.WriteLine($"⏭️ mcs_countrycode 已是 {countryCode}，无需更新");
        }
        else
        {
            var update = new Entity("mcs_customermasterdata", master.Id);
            update["mcs_countrycode"] = countryCode;
            _service.Update(update);
            Console.WriteLine($"✅ 客户主数据 mcs_countrycode: {oldCode ?? "(空)"} => {countryCode}");
        }

        Console.WriteLine("\n=== 完成 ===");
    }

    /// <summary>
    /// 设置：按客户主数据 GUID 直接定位（用于客户编号重复的场景），同步更新客户主数据与关联客户的 mcs_cofaceid
    /// </summary>
    public void SetById(Guid masterDataId, string cofaceId)
    {
        Console.WriteLine($"\n=== 设置客户 Coface ID: {masterDataId} => {cofaceId} ===");

        if (!System.Text.RegularExpressions.Regex.IsMatch(cofaceId, @"^icon#\d+$"))
        {
            Console.WriteLine($"❌ 科法斯客户代码格式不正确: '{cofaceId}'，正确格式应为 icon#数字，如 icon#164031501");
            return;
        }

        var master = _service.Retrieve("mcs_customermasterdata", masterDataId, MasterDataColumns());
        PrintMasterData(master);

        var oldCofaceId = master.GetAttributeValue<string>("mcs_cofaceid");
        if (oldCofaceId == cofaceId)
        {
            Console.WriteLine($"⏭️ 客户主数据 mcs_cofaceid 已是 {cofaceId}，无需更新");
        }
        else
        {
            var update = new Entity("mcs_customermasterdata", master.Id);
            update["mcs_cofaceid"] = cofaceId;
            _service.Update(update);
            Console.WriteLine($"✅ 客户主数据 mcs_cofaceid: {oldCofaceId ?? "(空)"} => {cofaceId}");
        }

        UpdateLinkedAccounts(master.Id, cofaceId);
        Console.WriteLine("\n=== 完成 ===");
    }

    /// <summary>
    /// 设置：按客户编号（mcs_sapnumber）定位客户主数据，同步更新客户主数据与关联客户的 mcs_cofaceid
    /// </summary>
    public void SetBySapNumber(string sapNumber, string cofaceId)
    {
        Console.WriteLine($"\n=== 设置客户 Coface ID: {sapNumber} => {cofaceId} ===");

        // 格式校验与客户主数据 ValidationPlugin 保持一致：icon#数字
        if (!System.Text.RegularExpressions.Regex.IsMatch(cofaceId, @"^icon#\d+$"))
        {
            Console.WriteLine($"❌ 科法斯客户代码格式不正确: '{cofaceId}'，正确格式应为 icon#数字，如 icon#164031501");
            return;
        }

        var masters = QueryMasterData(sapNumber);
        if (masters.Count == 0)
        {
            Console.WriteLine($"❌ 未找到客户主数据: {sapNumber}");
            return;
        }
        if (masters.Count > 1)
        {
            Console.WriteLine($"❌ 客户编号 {sapNumber} 匹配到 {masters.Count} 条客户主数据，请先人工核对，已中止");
            foreach (var m in masters) PrintMasterData(m);
            return;
        }

        var master = masters[0];
        PrintMasterData(master);

        var oldMasterCofaceId = master.GetAttributeValue<string>("mcs_cofaceid");
        if (oldMasterCofaceId == cofaceId)
        {
            Console.WriteLine($"⏭️ 客户主数据 mcs_cofaceid 已是 {cofaceId}，无需更新");
        }
        else
        {
            var masterUpdate = new Entity("mcs_customermasterdata", master.Id);
            masterUpdate["mcs_cofaceid"] = cofaceId;
            _service.Update(masterUpdate);
            Console.WriteLine($"✅ 客户主数据 mcs_cofaceid: {oldMasterCofaceId ?? "(空)"} => {cofaceId}");
        }

        UpdateLinkedAccounts(master.Id, cofaceId);

        Console.WriteLine("\n=== 完成 ===");
    }

    private void UpdateLinkedAccounts(Guid masterDataId, string cofaceId)
    {
        // 同步更新关联客户（account.mcs_cofaceid）
        var accountQuery = new QueryExpression("account")
        {
            ColumnSet = new ColumnSet("accountid", "accountnumber", "name", "mcs_englishname", "mcs_cofaceid"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("mcs_customermasterdata", ConditionOperator.Equal, masterDataId) }
            }
        };
        var accounts = _service.RetrieveMultiple(accountQuery).Entities;
        if (accounts.Count == 0)
        {
            Console.WriteLine("⚠️ 未找到关联该客户主数据的 account，仅更新了客户主数据");
        }
        foreach (var account in accounts)
        {
            var oldAccountCofaceId = account.GetAttributeValue<string>("mcs_cofaceid");
            if (oldAccountCofaceId == cofaceId)
            {
                Console.WriteLine($"⏭️ 客户(account) {account.GetAttributeValue<string>("name")} mcs_cofaceid 已是 {cofaceId}，无需更新");
                continue;
            }
            var accountUpdate = new Entity("account", account.Id);
            accountUpdate["mcs_cofaceid"] = cofaceId;
            _service.Update(accountUpdate);
            Console.WriteLine($"✅ 客户(account) {account.GetAttributeValue<string>("name")} ({account.GetAttributeValue<string>("accountnumber")}) mcs_cofaceid: {oldAccountCofaceId ?? "(空)"} => {cofaceId}");
        }
    }

    private List<Entity> QueryMasterData(string keyword)
    {
        // 先按 SAP 编号精确匹配
        var bySap = new QueryExpression("mcs_customermasterdata")
        {
            ColumnSet = MasterDataColumns(),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("mcs_sapnumber", ConditionOperator.Equal, keyword) }
            }
        };
        var result = _service.RetrieveMultiple(bySap).Entities.ToList();
        if (result.Count > 0) return result;

        // 再按名称模糊匹配
        var byName = new QueryExpression("mcs_customermasterdata")
        {
            ColumnSet = MasterDataColumns(),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("mcs_name", ConditionOperator.Like, $"%{keyword}%") }
            },
            TopCount = 10
        };
        return _service.RetrieveMultiple(byName).Entities.ToList();
    }

    private List<Entity> QueryAccounts(string keyword)
    {
        var byNumber = new QueryExpression("account")
        {
            ColumnSet = AccountColumns(),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("accountnumber", ConditionOperator.Equal, keyword) }
            }
        };
        var result = _service.RetrieveMultiple(byNumber).Entities.ToList();
        if (result.Count > 0) return result;

        var byName = new QueryExpression("account")
        {
            ColumnSet = AccountColumns(),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("name", ConditionOperator.Like, $"%{keyword}%") }
            },
            TopCount = 10
        };
        return _service.RetrieveMultiple(byName).Entities.ToList();
    }

    private static ColumnSet MasterDataColumns() => new ColumnSet(
        "mcs_customermasterdataid", "mcs_name", "mcs_englishname", "mcs_sapnumber", "mcs_countrycode", "mcs_cofaceid");

    private static ColumnSet AccountColumns() => new ColumnSet(
        "accountid", "accountnumber", "name", "mcs_englishname", "mcs_cofaceid", "mcs_customermasterdata");

    private static void PrintMasterData(Entity master)
    {
        Console.WriteLine("--- 客户主数据 (mcs_customermasterdata) ---");
        Console.WriteLine($"  ID: {master.Id}");
        Console.WriteLine($"  客户名: {master.GetAttributeValue<string>("mcs_name")}");
        Console.WriteLine($"  英文名称: {master.GetAttributeValue<string>("mcs_englishname")}");
        Console.WriteLine($"  客户编号: {master.GetAttributeValue<string>("mcs_sapnumber")}");
        Console.WriteLine($"  国家编码: {master.GetAttributeValue<string>("mcs_countrycode")}");
        Console.WriteLine($"  科法斯客户代码: {master.GetAttributeValue<string>("mcs_cofaceid") ?? "(空)"}");
    }

    private static void PrintAccount(Entity account)
    {
        Console.WriteLine("--- 客户 (account) ---");
        Console.WriteLine($"  ID: {account.Id}");
        Console.WriteLine($"  客户名: {account.GetAttributeValue<string>("name")}");
        Console.WriteLine($"  英文名称: {account.GetAttributeValue<string>("mcs_englishname")}");
        Console.WriteLine($"  客户编号: {account.GetAttributeValue<string>("accountnumber")}");
        Console.WriteLine($"  科法斯客户代码: {account.GetAttributeValue<string>("mcs_cofaceid") ?? "(空)"}");
    }
}
