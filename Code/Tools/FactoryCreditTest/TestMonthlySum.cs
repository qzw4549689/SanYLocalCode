using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FactoryCreditTest
{
    /// <summary>
    /// 禅道 #1403 临时测试辅助：验证 GetAverageMonthlyPayment 按月分组求和逻辑。
    /// 造数前把客户现有 mcs_outstanding 全量备份到本地 JSON，清理时原样恢复。
    /// </summary>
    public static class TestMonthlySum
    {
        static string BackupFile(Guid accountId) => $"monthly-sum-backup-{accountId}.json";

        /// <summary>
        /// 备份并清空客户现有 mcs_outstanding，创建 3 条按月分组求和测试记录。
        /// </summary>
        public static void Setup(IOrganizationService service, string accountName)
        {
            Console.WriteLine($"=== #1403 测试数据准备: {accountName} ===");

            // 1. 找 Account
            var accountQuery = new QueryExpression("account")
            {
                ColumnSet = new ColumnSet("accountid", "name", "mcs_customermasterdata"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("name", ConditionOperator.Equal, accountName) }
                },
                TopCount = 1
            };
            var accountResult = service.RetrieveMultiple(accountQuery);
            if (accountResult.Entities.Count == 0) { Console.WriteLine("❌ 未找到 Account"); return; }
            var account = accountResult.Entities[0];
            Guid accountId = account.Id;
            var masterRef = account.GetAttributeValue<EntityReference>("mcs_customermasterdata");
            Console.WriteLine($"Account: {accountId}");
            Console.WriteLine($"客户主数据ID: {(masterRef != null ? masterRef.Id.ToString() : "(空)")}");

            // 2. 备份现有 mcs_outstanding（全字段）
            var existing = QueryOutstanding(service, accountId, allColumns: true);
            Console.WriteLine($"现有 mcs_outstanding 记录数: {existing.Entities.Count}");
            var backup = new JsonObject
            {
                ["accountId"] = accountId.ToString(),
                ["accountName"] = accountName,
                ["backupTimeUtc"] = DateTime.UtcNow.ToString("o"),
                ["records"] = new JsonArray()
            };
            var recordsArray = (JsonArray)backup["records"];
            foreach (var e in existing.Entities)
            {
                var recObj = new JsonObject();
                foreach (var attr in e.Attributes)
                {
                    var serialized = SerializeValue(attr.Value);
                    if (serialized != null)
                    {
                        recObj[attr.Key] = serialized;
                    }
                }
                recordsArray.Add(recObj);
                Console.WriteLine($"  备份记录 {e.Id}: 序列化字段数={recObj.Count}");
            }
            Console.WriteLine($"  recordsArray.Count={recordsArray.Count}");
            File.WriteAllText(BackupFile(accountId), backup.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"✓ 已备份 {existing.Entities.Count} 条到 {BackupFile(accountId)}");

            // 3. 删除现有记录
            foreach (var old in existing.Entities)
            {
                service.Delete("mcs_outstanding", old.Id);
                Console.WriteLine($"删除旧记录: {old.Id}");
            }

            // 4. 创建 3 条测试记录（同月 2 条 + 上月 1 条；时间取中午避免时区跨月）
            var testData = new[]
            {
                new { CreateOn = new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc), Amount = 600000m },
                new { CreateOn = new DateTime(2026, 6, 20, 12, 0, 0, DateTimeKind.Utc), Amount = 400000m },
                new { CreateOn = new DateTime(2026, 5, 15, 12, 0, 0, DateTimeKind.Utc), Amount = 200000m },
            };
            foreach (var td in testData)
            {
                Entity outstanding = new Entity("mcs_outstanding");
                outstanding["mcs_account"] = new EntityReference("account", accountId);
                outstanding["mcs_name"] = $"OUT1403TEST{td.CreateOn:yyyyMMdd}";
                outstanding["mcs_createon"] = td.CreateOn;
                outstanding["mcs_paymentcollectioncurrentmonthrmb"] = td.Amount;
                outstanding["mcs_overdurationdays"] = 30;
                outstanding["mcs_newoverdueamountrmb"] = 0m;
                outstanding["mcs_newremainingamountrmb"] = 500000m;
                Guid id = service.Create(outstanding);
                Console.WriteLine($"创建测试记录: {id}, mcs_createon={td.CreateOn:yyyy-MM-dd}, 金额={td.Amount}");
            }

            Console.WriteLine("=== 准备完成，预期: 2026-06 合计 1000000, 2026-05 合计 200000, 累计 1200000, ÷12=100000 RMB ===");
        }

        /// <summary>
        /// 清理：删除测试 proc（多个用分号分隔）、删除测试 outstanding、从备份恢复原记录。
        /// </summary>
        public static void Cleanup(IOrganizationService service, string accountName, string procIds)
        {
            Console.WriteLine($"=== #1403 测试清理: {accountName} ===");

            // 1. 删除测试 mcs_fca_proc
            foreach (var procIdText in procIds.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                Guid procId = Guid.Parse(procIdText.Trim());
                try
                {
                    service.Delete("mcs_fca_proc", procId);
                    Console.WriteLine($"✓ 已删除测试 proc: {procId}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠ 删除 proc {procId} 失败: {ex.Message}");
                }
            }

            // 2. 找 Account 并删除现有 outstanding（即 3 条测试记录）
            var accountQuery = new QueryExpression("account")
            {
                ColumnSet = new ColumnSet("accountid"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("name", ConditionOperator.Equal, accountName) }
                },
                TopCount = 1
            };
            var accountResult = service.RetrieveMultiple(accountQuery);
            if (accountResult.Entities.Count == 0) { Console.WriteLine("❌ 未找到 Account，请人工核对清理"); return; }
            Guid accountId = accountResult.Entities[0].Id;

            var current = QueryOutstanding(service, accountId, allColumns: false);
            foreach (var e in current.Entities)
            {
                service.Delete("mcs_outstanding", e.Id);
                Console.WriteLine($"✓ 已删除 outstanding: {e.Id}");
            }

            // 3. 从备份恢复原记录
            string file = BackupFile(accountId);
            if (!File.Exists(file))
            {
                Console.WriteLine("无备份文件（原本就无数据），恢复跳过");
                return;
            }
            var backup = JsonNode.Parse(File.ReadAllText(file)).AsObject();
            var records = backup["records"].AsArray();
            int restored = 0;
            foreach (var recNode in records)
            {
                var recObj = recNode.AsObject();
                Entity entity = new Entity("mcs_outstanding");
                foreach (var kv in recObj)
                {
                    // 跳过主键、系统字段、Money 的 _base 伴随字段（系统自动计算）
                    if (kv.Key == "mcs_outstandingid" || kv.Key.EndsWith("_base", StringComparison.Ordinal))
                    {
                        continue;
                    }
                    if (!kv.Key.StartsWith("mcs_", StringComparison.Ordinal) &&
                        kv.Key != "transactioncurrencyid" && kv.Key != "name" && kv.Key != "ownerid")
                    {
                        continue;
                    }
                    object value = DeserializeValue(kv.Value.AsObject());
                    if (value != null)
                    {
                        entity[kv.Key] = value;
                    }
                }
                Guid newId = service.Create(entity);
                restored++;
                Console.WriteLine($"✓ 恢复原记录 -> 新ID: {newId}");
            }
            Console.WriteLine($"=== 清理完成：删除 outstanding {current.Entities.Count} 条，恢复原记录 {restored} 条 ===");
        }

        static EntityCollection QueryOutstanding(IOrganizationService service, Guid accountId, bool allColumns)
        {
            var query = new QueryExpression("mcs_outstanding")
            {
                ColumnSet = allColumns ? new ColumnSet(true) : new ColumnSet("mcs_outstandingid"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("mcs_account", ConditionOperator.Equal, accountId) }
                }
            };
            return service.RetrieveMultiple(query);
        }

        static JsonObject SerializeValue(object value)
        {
            switch (value)
            {
                case string s:
                    return new JsonObject { ["kind"] = "string", ["v"] = s };
                case int i:
                    return new JsonObject { ["kind"] = "int", ["v"] = i };
                case long l:
                    return new JsonObject { ["kind"] = "long", ["v"] = l };
                case decimal d:
                    return new JsonObject { ["kind"] = "decimal", ["v"] = d };
                case double db:
                    return new JsonObject { ["kind"] = "double", ["v"] = db };
                case bool b:
                    return new JsonObject { ["kind"] = "bool", ["v"] = b };
                case Guid g:
                    return new JsonObject { ["kind"] = "guid", ["v"] = g.ToString() };
                case DateTime dt:
                    return new JsonObject { ["kind"] = "datetime", ["v"] = dt.ToString("o") };
                case Money m:
                    return new JsonObject { ["kind"] = "money", ["v"] = m.Value };
                case OptionSetValue osv:
                    return new JsonObject { ["kind"] = "optionset", ["v"] = osv.Value };
                case EntityReference er:
                    return new JsonObject { ["kind"] = "entityref", ["v"] = er.LogicalName, ["v2"] = er.Id.ToString() };
                case OptionSetValueCollection osc:
                    return new JsonObject
                    {
                        ["kind"] = "optionsetcollection",
                        ["v"] = new JsonArray(osc.Select(o => JsonValue.Create(o.Value)).ToArray())
                    };
                default:
                    return null; // EntityCollection/AliasedValue 等不备份
            }
        }

        static object DeserializeValue(JsonObject obj)
        {
            string kind = obj["kind"].GetValue<string>();
            switch (kind)
            {
                case "string": return obj["v"].GetValue<string>();
                case "int": return obj["v"].GetValue<int>();
                case "long": return obj["v"].GetValue<long>();
                case "decimal": return obj["v"].GetValue<decimal>();
                case "double": return obj["v"].GetValue<double>();
                case "bool": return obj["v"].GetValue<bool>();
                case "guid": return Guid.Parse(obj["v"].GetValue<string>());
                case "datetime": return DateTime.Parse(obj["v"].GetValue<string>(), null, System.Globalization.DateTimeStyles.RoundtripKind);
                case "money": return new Money(obj["v"].GetValue<decimal>());
                case "optionset": return new OptionSetValue(obj["v"].GetValue<int>());
                case "entityref": return new EntityReference(obj["v"].GetValue<string>(), Guid.Parse(obj["v2"].GetValue<string>()));
                case "optionsetcollection":
                    var coll = new OptionSetValueCollection();
                    foreach (var item in obj["v"].AsArray())
                    {
                        coll.Add(new OptionSetValue(item.GetValue<int>()));
                    }
                    return coll;
                default: return null;
            }
        }
    }
}
