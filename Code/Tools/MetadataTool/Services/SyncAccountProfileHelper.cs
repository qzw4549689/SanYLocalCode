using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System.Linq;

namespace D365MetadataTool;

/// <summary>
/// 查询/同步客户画像测试数据（account + mcs_customermasterdata 关键字段）
/// </summary>
public class SyncAccountProfileHelper
{
    private readonly ServiceClient _service;

    public SyncAccountProfileHelper(ServiceClient service)
    {
        _service = service;
    }

    public void ShowProfile(string accountName)
    {
        Console.WriteLine($"\n=== 查询客户画像属性: {accountName} ===");
        var account = FindAccount(accountName);
        if (account == null) return;

        PrintAccountFields(account);
        PrintMasterDataFields(account);
        Console.WriteLine("\n=== 完成 ===");
    }

    public void SetCategory(string accountName, int accountCategory, int accountLevel)
    {
        Console.WriteLine($"\n=== 设置客户分类属性: {accountName} ===");
        var account = FindAccount(accountName);
        if (account == null) return;

        var update = new Entity("account", account.Id);
        update["mcs_accountcategory"] = new OptionSetValue(accountCategory);
        update["mcs_accountlevel"] = new OptionSetValue(accountLevel);
        _service.Update(update);

        Console.WriteLine($"✅ 已设置 mcs_accountcategory={accountCategory}, mcs_accountlevel={accountLevel}");
        Console.WriteLine("\n=== 完成 ===");
    }

    public void SetCountryCode(string accountKey, string countryCode)
    {
        Console.WriteLine($"\n=== 设置客户国家代码: {accountKey} => {countryCode} ===");
        var account = FindAccount(accountKey);
        if (account == null) return;

        var masterRef = account.GetAttributeValue<EntityReference>("mcs_customermasterdata");
        if (masterRef == null)
        {
            Console.WriteLine("❌ 该客户未关联客户主数据");
            return;
        }

        // 根据国家代码查找 mcs_country 记录
        var countryQuery = new QueryExpression("mcs_country")
        {
            ColumnSet = new ColumnSet("mcs_countryid", "mcs_name"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("mcs_countrycode", ConditionOperator.Equal, countryCode) }
            }
        };
        var countryResult = _service.RetrieveMultiple(countryQuery);
        if (countryResult.Entities.Count == 0)
        {
            Console.WriteLine($"❌ 未找到国家代码对应的国家记录: {countryCode}");
            return;
        }

        var country = countryResult.Entities[0];
        var countryId = country.GetAttributeValue<Guid>("mcs_countryid");
        var countryName = country.GetAttributeValue<string>("mcs_name");

        var update = new Entity("mcs_customermasterdata", masterRef.Id);
        update["mcs_countrycode"] = countryCode;
        update["mcs_country"] = new EntityReference("mcs_country", countryId);
        update["mcs_countryname"] = countryName;
        _service.Update(update);

        Console.WriteLine($"✅ 已更新客户主数据 {masterRef.Id} 的国家字段:");
        Console.WriteLine($"   mcs_countrycode = {countryCode}");
        Console.WriteLine($"   mcs_country = {countryName} ({countryId})");
        Console.WriteLine($"   mcs_countryname = {countryName}");
        Console.WriteLine("\n=== 完成 ===");
    }

    public void SimulateApproval(string accountKey, string scoreId)
    {
        Console.WriteLine($"\n=== 模拟审批通过: {accountKey} / {scoreId} ===");

        var account = FindAccount(accountKey);
        if (account == null) return;

        var masterRef = account.GetAttributeValue<EntityReference>("mcs_customermasterdata");
        if (masterRef == null)
        {
            Console.WriteLine("❌ 该客户未关联客户主数据");
            return;
        }

        // 读取信用评估记录的信用分
        var creditQuery = new QueryExpression("mcs_credit_record")
        {
            ColumnSet = new ColumnSet("mcs_credit_recordid", "mcs_scoreid", "mcs_creditscore", "mcs_status"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("mcs_scoreid", ConditionOperator.Equal, scoreId) }
            }
        };
        var creditResult = _service.RetrieveMultiple(creditQuery);
        if (creditResult.Entities.Count == 0)
        {
            Console.WriteLine($"❌ 未找到评估记录: {scoreId}");
            return;
        }

        var creditRecord = creditResult.Entities[0];
        var creditScore = creditRecord.GetAttributeValue<decimal?>("mcs_creditscore");
        if (!creditScore.HasValue)
        {
            Console.WriteLine($"❌ 评估记录 {scoreId} 尚未计算信用分（当前状态: {creditRecord.GetAttributeValue<OptionSetValue>("mcs_status")?.Value}）");
            return;
        }

        var grade = CalculateCreditGrade(creditScore.Value);
        var gradeValue = MapCreditGradeToOptionSetValue(grade);

        var update = new Entity("mcs_customermasterdata", masterRef.Id);
        update["mcs_creditscore"] = creditScore.Value;
        update["mcs_creditvalid"] = true;
        if (gradeValue.HasValue)
        {
            update["mcs_creditgrade"] = new OptionSetValue(gradeValue.Value);
        }
        _service.Update(update);

        Console.WriteLine($"✅ 已更新客户主数据 {masterRef.Id}:");
        Console.WriteLine($"   mcs_creditscore = {creditScore.Value}");
        Console.WriteLine($"   mcs_creditgrade = {grade} ({gradeValue})");
        Console.WriteLine($"   mcs_creditvalid = true");
        Console.WriteLine("\n=== 完成 ===");
    }

    private string CalculateCreditGrade(decimal score)
    {
        if (score >= 80) return "A0";
        if (score >= 70) return "A1";
        if (score >= 60) return "A2";
        if (score >= 50) return "A3";
        return "A4";
    }

    private int? MapCreditGradeToOptionSetValue(string creditGrade)
    {
        switch (creditGrade?.ToUpperInvariant())
        {
            case "A0": return 100000000;
            case "A1": return 100000001;
            case "A2": return 100000002;
            case "A3": return 100000003;
            case "A4": return 100000004;
            default: return null;
        }
    }

    public void SyncProfile(string sourceName, string targetName)
    {
        Console.WriteLine($"\n=== 同步客户画像属性: {sourceName} -> {targetName} ===");

        var sourceAccount = FindAccount(sourceName);
        var targetAccount = FindAccount(targetName);
        if (sourceAccount == null || targetAccount == null) return;

        // 同步 account 字段
        var accountUpdate = new Entity("account", targetAccount.Id);
        CopyIfPresent(sourceAccount, accountUpdate, "mcs_accountcategory");
        CopyIfPresent(sourceAccount, accountUpdate, "mcs_accountlevel");
        if (accountUpdate.Attributes.Count > 0)
        {
            _service.Update(accountUpdate);
            Console.WriteLine("✅ 已同步 account 字段");
        }

        // 同步 mcs_customermasterdata 字段
        var sourceMasterRef = sourceAccount.GetAttributeValue<EntityReference>("mcs_customermasterdata");
        var targetMasterRef = targetAccount.GetAttributeValue<EntityReference>("mcs_customermasterdata");

        if (sourceMasterRef != null && targetMasterRef != null)
        {
            var sourceMaster = _service.Retrieve("mcs_customermasterdata", sourceMasterRef.Id,
                new ColumnSet("mcs_customerclassification", "mcs_dealerrank"));
            var targetMasterUpdate = new Entity("mcs_customermasterdata", targetMasterRef.Id);
            CopyIfPresent(sourceMaster, targetMasterUpdate, "mcs_customerclassification");
            CopyIfPresent(sourceMaster, targetMasterUpdate, "mcs_dealerrank");
            if (targetMasterUpdate.Attributes.Count > 0)
            {
                _service.Update(targetMasterUpdate);
                Console.WriteLine("✅ 已同步 mcs_customermasterdata 字段");
            }
        }
        else
        {
            Console.WriteLine("⚠ 源或目标客户未关联客户主数据，跳过主数据同步");
        }

        Console.WriteLine("\n=== 完成 ===");
    }

    private Entity? FindAccount(string accountKey)
    {
        var columnSet = new ColumnSet("accountid", "name", "accountnumber", "mcs_sapnumber", "mcs_accountcategory", "mcs_accountlevel", "mcs_customermasterdata");

        // 先按名称精确匹配
        var query = new QueryExpression("account")
        {
            ColumnSet = columnSet,
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("name", ConditionOperator.Equal, accountKey) }
            }
        };

        var result = _service.RetrieveMultiple(query);

        // 再按客户编码精确匹配
        if (result.Entities.Count == 0)
        {
            query = new QueryExpression("account")
            {
                ColumnSet = columnSet,
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("accountnumber", ConditionOperator.Equal, accountKey) }
                }
            };
            result = _service.RetrieveMultiple(query);
        }

        // 再按 SAP 编码精确匹配
        if (result.Entities.Count == 0)
        {
            query = new QueryExpression("account")
            {
                ColumnSet = columnSet,
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("mcs_sapnumber", ConditionOperator.Equal, accountKey) }
                }
            };
            result = _service.RetrieveMultiple(query);
        }

        // 最后按名称包含匹配
        if (result.Entities.Count == 0)
        {
            var likeQuery = new QueryExpression("account")
            {
                ColumnSet = columnSet,
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("name", ConditionOperator.Like, $"%{accountKey}%") }
                }
            };
            result = _service.RetrieveMultiple(likeQuery);
        }

        if (result.Entities.Count == 0)
        {
            Console.WriteLine($"❌ 未找到客户: {accountKey}");
            return null;
        }

        if (result.Entities.Count > 1)
        {
            Console.WriteLine($"⚠ 找到 {result.Entities.Count} 个匹配客户，使用第一个：");
            foreach (var e in result.Entities.Take(10))
            {
                Console.WriteLine($"   - {e.GetAttributeValue<string>("name")} ({e.GetAttributeValue<string>("accountnumber")})");
            }
        }

        var account = result.Entities[0];
        Console.WriteLine($"使用客户: {account.GetAttributeValue<string>("name")} ({account.GetAttributeValue<string>("accountnumber")})");
        return account;
    }

    private void PrintAccountFields(Entity account)
    {
        Console.WriteLine("\n[account 字段]");
        PrintField(account, "mcs_accountcategory");
        PrintField(account, "mcs_accountlevel");
    }

    private void PrintMasterDataFields(Entity account)
    {
        var masterRef = account.GetAttributeValue<EntityReference>("mcs_customermasterdata");
        if (masterRef == null)
        {
            Console.WriteLine("\n[mcs_customermasterdata] 未关联");
            return;
        }

        var master = _service.Retrieve("mcs_customermasterdata", masterRef.Id,
            new ColumnSet("mcs_customerclassification", "mcs_dealerrank", "mcs_countrycode", "mcs_country", "mcs_creditvalid", "mcs_creditscore", "mcs_creditgrade"));
        Console.WriteLine("\n[mcs_customermasterdata 字段]");
        PrintField(master, "mcs_customerclassification");
        PrintField(master, "mcs_dealerrank");
        PrintField(master, "mcs_creditvalid");
        PrintField(master, "mcs_creditscore");
        PrintField(master, "mcs_creditgrade");
        PrintField(master, "mcs_countrycode");
        PrintField(master, "mcs_country");
        // mcs_countryname 是 lookup 的 child attribute，不能直接 Retrieve，从 lookup 引用中取 name
        var countryRef = master.GetAttributeValue<EntityReference>("mcs_country");
        Console.WriteLine($"  mcs_countryname: {countryRef?.Name ?? "(空)"}");
    }

    private void PrintField(Entity entity, string fieldName)
    {
        if (entity.Contains(fieldName) && entity[fieldName] != null)
        {
            var value = entity[fieldName];
            if (value is OptionSetValue osv)
            {
                Console.WriteLine($"  {fieldName}: {osv.Value}");
            }
            else
            {
                Console.WriteLine($"  {fieldName}: {value}");
            }
        }
        else
        {
            Console.WriteLine($"  {fieldName}: (空)");
        }
    }

    private void CopyIfPresent(Entity source, Entity target, string fieldName)
    {
        if (source.Contains(fieldName) && source[fieldName] != null)
        {
            target[fieldName] = source[fieldName];
        }
    }
}
