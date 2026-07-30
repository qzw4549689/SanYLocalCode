using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace DeployTool
{
    public class QueryUserAccount
    {
        public static void Query(ServiceClient service)
        {
            Console.WriteLine(">>> 查询当前用户（邱正卫）的组织/事业部信息...");

            try
            {
                // 1. 查找 systemuser
                var userQuery = new QueryExpression("systemuser")
                {
                    ColumnSet = new ColumnSet("systemuserid", "fullname", "businessunitid"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("fullname", ConditionOperator.Like, "%邱%")
                        }
                    }
                };
                var users = service.RetrieveMultiple(userQuery);
                if (users.Entities.Count == 0)
                {
                    Console.WriteLine("  ❌ 未找到用户 邱正卫");
                    return;
                }

                Console.WriteLine($"  匹配到 {users.Entities.Count} 个用户:");
                foreach (var u in users.Entities)
                {
                    Console.WriteLine($"    id={u.Id}, fullname={u.GetAttributeValue<string>("fullname")}, domainname={u.GetAttributeValue<string>("domainname")}");
                }

                var user = users.Entities[0];
                var userId = user.Id;
                Console.WriteLine($"  处理第一个用户: {user.GetAttributeValue<string>("fullname")}, systemuserid={userId}");

                // 2. 查找 mcs_useraccount
                var uaQuery = new QueryExpression("mcs_useraccount")
                {
                    ColumnSet = new ColumnSet("mcs_useraccountid", "mcs_name", "mcs_systemuserid", "mcs_orgid"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("mcs_systemuserid", ConditionOperator.Equal, userId),
                            new ConditionExpression("statecode", ConditionOperator.Equal, 0)
                        }
                    }
                };
                var uaResults = service.RetrieveMultiple(uaQuery);
                if (uaResults.Entities.Count == 0)
                {
                    Console.WriteLine("  ❌ 未找到 mcs_useraccount 记录");
                    return;
                }

                var ua = uaResults.Entities[0];
                var uaId = ua.Id;
                var orgRef = ua.GetAttributeValue<EntityReference>("mcs_orgid");
                var personnelRef = ua.GetAttributeValue<EntityReference>("mcs_personnelid");
                Console.WriteLine($"  mcs_useraccount: id={uaId}, name={ua.GetAttributeValue<string>("mcs_name")}");
                Console.WriteLine($"  mcs_useraccount.mcs_orgid = {(orgRef == null ? "空" : orgRef.Id.ToString())}");
                Console.WriteLine($"  mcs_useraccount.mcs_personnelid = {(personnelRef == null ? "空" : personnelRef.Id.ToString())}");

                // 如果 useraccount 的 orgid 为空，尝试从 personnel 取
                if (orgRef == null && personnelRef != null)
                {
                    Console.WriteLine("  尝试从 mcs_personnel 获取 mcs_orgid...");
                    var personnel = service.Retrieve("mcs_personnel", personnelRef.Id, new ColumnSet("mcs_name", "mcs_orgid"));
                    if (personnel != null)
                    {
                        Console.WriteLine($"  mcs_personnel: name={personnel.GetAttributeValue<string>("mcs_name")}");
                        orgRef = personnel.GetAttributeValue<EntityReference>("mcs_orgid");
                        Console.WriteLine($"  mcs_personnel.mcs_orgid = {(orgRef == null ? "空" : orgRef.Id.ToString())}");
                    }
                }

                if (orgRef == null)
                {
                    Console.WriteLine("  ⚠️ 无法从 mcs_useraccount 或 mcs_personnel 获取 mcs_orgid");
                    Console.WriteLine("  尝试用 systemuser.businessunitid 反查 mcs_org/mcs_bu...");

                    var businessUnitRef = user.GetAttributeValue<EntityReference>("businessunitid");
                    Console.WriteLine($"  systemuser.businessunitid = {(businessUnitRef == null ? "空" : businessUnitRef.Id.ToString())}");

                    if (businessUnitRef != null)
                    {
                        // 查 mcs_org 中 mcs_businessunitid 匹配的记录
                        var orgByBuQuery = new QueryExpression("mcs_org")
                        {
                            ColumnSet = new ColumnSet("mcs_orgid", "mcs_name", "mcs_code", "mcs_buid"),
                            Criteria = new FilterExpression
                            {
                                Conditions =
                                {
                                    new ConditionExpression("mcs_businessunitid", ConditionOperator.Equal, businessUnitRef.Id)
                                }
                            }
                        };
                        var orgByBuResults = service.RetrieveMultiple(orgByBuQuery);
                        Console.WriteLine($"  通过 businessunitid 找到 {orgByBuResults.Entities.Count} 条 mcs_org 记录");
                        foreach (var o in orgByBuResults.Entities)
                        {
                            Console.WriteLine($"    org id={o.Id}, name={o.GetAttributeValue<string>("mcs_name")}, code={o.GetAttributeValue<string>("mcs_code")}");
                        }

                        // 查 mcs_bu 中 mcs_businessunitid 匹配的记录
                        var buQuery = new QueryExpression("mcs_bu")
                        {
                            ColumnSet = new ColumnSet("mcs_buid", "mcs_name", "mcs_code"),
                            Criteria = new FilterExpression
                            {
                                Conditions =
                                {
                                    new ConditionExpression("mcs_businessunitid", ConditionOperator.Equal, businessUnitRef.Id)
                                }
                            }
                        };
                        var buResults = service.RetrieveMultiple(buQuery);
                        Console.WriteLine($"  通过 businessunitid 找到 {buResults.Entities.Count} 条 mcs_bu 记录");
                        foreach (var b in buResults.Entities)
                        {
                            Console.WriteLine($"    bu id={b.Id}, name={b.GetAttributeValue<string>("mcs_name")}, code={b.GetAttributeValue<string>("mcs_code")}");
                        }
                    }

                    // 列出所有 mcs_org 和 mcs_bu 记录
                    var orgAll = service.RetrieveMultiple(new QueryExpression("mcs_org")
                    {
                        ColumnSet = new ColumnSet("mcs_orgid", "mcs_name", "mcs_code", "mcs_buid"),
                        TopCount = 50
                    });
                    var buAll = service.RetrieveMultiple(new QueryExpression("mcs_bu")
                    {
                        ColumnSet = new ColumnSet("mcs_buid", "mcs_name", "mcs_code"),
                        TopCount = 50
                    });
                    Console.WriteLine($"  DEV1 中 mcs_org 总记录数: {orgAll.Entities.Count}");
                    foreach (var o in orgAll.Entities)
                    {
                        var buIdRef = o.GetAttributeValue<EntityReference>("mcs_buid");
                        var orgBuName = "";
                        if (buIdRef != null)
                        {
                            var buEnt = buAll.Entities.FirstOrDefault(b => b.Id == buIdRef.Id);
                            orgBuName = buEnt != null ? $"{buEnt.GetAttributeValue<string>("mcs_name")}({buEnt.GetAttributeValue<string>("mcs_code")})" : "未找到";
                        }
                        Console.WriteLine($"    org: id={o.Id}, name={o.GetAttributeValue<string>("mcs_name")}, code={o.GetAttributeValue<string>("mcs_code")}, bu={orgBuName}");
                    }
                    Console.WriteLine($"  DEV1 中 mcs_bu 总记录数: {buAll.Entities.Count}");
                    foreach (var b in buAll.Entities)
                    {
                        Console.WriteLine($"    bu: id={b.Id}, name={b.GetAttributeValue<string>("mcs_name")}, code={b.GetAttributeValue<string>("mcs_code")}");
                    }
                    return;
                }

                // 3. 查找 mcs_org
                var org = service.Retrieve("mcs_org", orgRef.Id, new ColumnSet("mcs_name", "mcs_code", "mcs_buid"));
                if (org == null)
                {
                    Console.WriteLine("  ❌ 未找到 mcs_org 记录");
                    return;
                }
                var orgCode = org.GetAttributeValue<string>("mcs_code");
                var orgName = org.GetAttributeValue<string>("mcs_name");
                var buRef = org.GetAttributeValue<EntityReference>("mcs_buid");
                Console.WriteLine($"  mcs_org: name={orgName}, code={orgCode}, mcs_buid={buRef?.Id}");

                // 4. 查找 mcs_bu
                if (buRef == null)
                {
                    Console.WriteLine("  ❌ mcs_org.mcs_buid 为空");
                    return;
                }
                var bu = service.Retrieve("mcs_bu", buRef.Id, new ColumnSet("mcs_name", "mcs_code", "mcs_englishname"));
                if (bu == null)
                {
                    Console.WriteLine("  ❌ 未找到 mcs_bu 记录");
                    return;
                }
                var buCode = bu.GetAttributeValue<string>("mcs_code");
                var buName = bu.GetAttributeValue<string>("mcs_name");
                var buEnglishName = bu.GetAttributeValue<string>("mcs_englishname");
                Console.WriteLine($"  mcs_bu: name={buName}, code={buCode}, englishname={buEnglishName}");

                Console.WriteLine();
                Console.WriteLine("===== 汇总 =====");
                Console.WriteLine($"用户: 邱正卫");
                Console.WriteLine($"归属组织名称: {orgName}");
                Console.WriteLine($"归属组织编码: {orgCode}");
                Console.WriteLine($"事业部名称: {buName}");
                Console.WriteLine($"事业部编码: {buCode}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  查询失败: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        /// <summary>
        /// 为当前测试用户（# 邱正卫）的 mcs_useraccount 补 mcs_orgid
        /// </summary>
        public static void Fill(ServiceClient service, string orgIdStr)
        {
            Console.WriteLine($">>> 为 # 邱正卫 的 mcs_useraccount 补 mcs_orgid: {orgIdStr}");

            try
            {
                if (!Guid.TryParse(orgIdStr, out var orgId))
                {
                    Console.WriteLine("  ❌ orgId 格式不正确");
                    return;
                }

                // 校验 org 存在
                var org = service.Retrieve("mcs_org", orgId, new ColumnSet("mcs_name", "mcs_code", "mcs_buid"));
                if (org == null)
                {
                    Console.WriteLine("  ❌ 未找到指定的 mcs_org 记录");
                    return;
                }
                var orgName = org.GetAttributeValue<string>("mcs_name");
                var orgCode = org.GetAttributeValue<string>("mcs_code");
                var buRef = org.GetAttributeValue<EntityReference>("mcs_buid");
                Console.WriteLine($"  将使用组织: {orgName} ({orgCode})");
                if (buRef != null)
                {
                    var bu = service.Retrieve("mcs_bu", buRef.Id, new ColumnSet("mcs_name", "mcs_code"));
                    Console.WriteLine($"  对应事业部: {bu.GetAttributeValue<string>("mcs_name")} ({bu.GetAttributeValue<string>("mcs_code")})");
                }

                // 查找用户
                var userQuery = new QueryExpression("systemuser")
                {
                    ColumnSet = new ColumnSet("systemuserid", "fullname"),
                    Criteria = new FilterExpression
                    {
                        Conditions = { new ConditionExpression("fullname", ConditionOperator.Like, "%邱%") }
                    }
                };
                var users = service.RetrieveMultiple(userQuery);
                if (users.Entities.Count == 0)
                {
                    Console.WriteLine("  ❌ 未找到用户");
                    return;
                }
                var userId = users.Entities[0].Id;

                // 查找 mcs_useraccount
                var uaQuery = new QueryExpression("mcs_useraccount")
                {
                    ColumnSet = new ColumnSet("mcs_useraccountid", "mcs_name"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("mcs_systemuserid", ConditionOperator.Equal, userId),
                            new ConditionExpression("statecode", ConditionOperator.Equal, 0)
                        }
                    }
                };
                var uaResults = service.RetrieveMultiple(uaQuery);
                if (uaResults.Entities.Count == 0)
                {
                    Console.WriteLine("  ❌ 未找到 mcs_useraccount 记录");
                    return;
                }

                var uaId = uaResults.Entities[0].Id;
                var update = new Entity("mcs_useraccount") { Id = uaId };
                update["mcs_orgid"] = new EntityReference("mcs_org", orgId);
                service.Update(update);

                Console.WriteLine($"  ✅ mcs_useraccount ({uaId}) 的 mcs_orgid 已更新为 {orgId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  更新失败: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        /// <summary>
        /// 为 LTC客户-1 创建/更新 mcs_outstanding 逾期数据，使其满足不予授信场景 3
        /// 条件：逾期天数 >= 180 且 逾期金额 / 在外货款余额 > 50%
        /// </summary>
        public static void SetupLtcOverdue(ServiceClient service)
        {
            Console.WriteLine(">>> 为 LTC客户-1 设置逾期数据（触发不予授信场景 3）...");

            try
            {
                // 1. 查找 LTC客户-1
                var accountQuery = new QueryExpression("account")
                {
                    ColumnSet = new ColumnSet("accountid", "name", "accountnumber"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("name", ConditionOperator.Equal, "LTC客户-1")
                        }
                    },
                    TopCount = 5
                };
                var accounts = service.RetrieveMultiple(accountQuery);
                if (accounts.Entities.Count == 0)
                {
                    Console.WriteLine("  ❌ 未找到 LTC客户-1");
                    return;
                }

                Console.WriteLine($"  找到 {accounts.Entities.Count} 个客户:");
                foreach (var a in accounts.Entities)
                {
                    Console.WriteLine($"    id={a.Id}, name={a.GetAttributeValue<string>("name")}, code={a.GetAttributeValue<string>("accountnumber")}");
                }

                var account = accounts.Entities[0];
                var accountId = account.Id;
                Console.WriteLine($"  处理客户: {account.GetAttributeValue<string>("name")}, id={accountId}");

                // 2. 查找现有 mcs_outstanding 记录
                var outstandingQuery = new QueryExpression("mcs_outstanding")
                {
                    ColumnSet = new ColumnSet("mcs_outstandingid", "mcs_name", "mcs_overdurationdays", "mcs_newoverdueamountrmb", "mcs_newremainingamountrmb"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("mcs_account", ConditionOperator.Equal, accountId)
                        }
                    },
                    TopCount = 10
                };
                var existing = service.RetrieveMultiple(outstandingQuery);
                Console.WriteLine($"  找到 {existing.Entities.Count} 条现有 mcs_outstanding 记录");

                // 场景 3 需要的值
                var overdueDays = 200;
                var overdueAmount = 100000m;
                var remainingAmount = 100000m;

                if (existing.Entities.Count > 0)
                {
                    // 更新第一条
                    var recordId = existing.Entities[0].Id;
                    var update = new Entity("mcs_outstanding") { Id = recordId };
                    update["mcs_overdurationdays"] = overdueDays;
                    update["mcs_newoverdueamountrmb"] = overdueAmount;
                    update["mcs_newremainingamountrmb"] = remainingAmount;
                    update["mcs_overdue"] = true;
                    service.Update(update);
                    Console.WriteLine($"  ✅ 已更新 mcs_outstanding 记录 {recordId}");
                }
                else
                {
                    // 创建新记录
                    var create = new Entity("mcs_outstanding");
                    create["mcs_name"] = "TEST-OUTSTANDING-" + DateTime.Now.ToString("yyyyMMddHHmmss");
                    create["mcs_account"] = new EntityReference("account", accountId);
                    create["mcs_overdurationdays"] = overdueDays;
                    create["mcs_newoverdueamountrmb"] = overdueAmount;
                    create["mcs_newremainingamountrmb"] = remainingAmount;
                    create["mcs_overdue"] = true;
                    var newId = service.Create(create);
                    Console.WriteLine($"  ✅ 已创建 mcs_outstanding 记录 {newId}");
                }

                Console.WriteLine();
                Console.WriteLine("===== 设置值 =====");
                Console.WriteLine($"客户: {account.GetAttributeValue<string>("name")}");
                Console.WriteLine($"逾期天数: {overdueDays}");
                Console.WriteLine($"逾期金额(RMB): {overdueAmount}");
                Console.WriteLine($"在外货款余额(RMB): {remainingAmount}");
                Console.WriteLine($"逾期占比: {(overdueAmount / remainingAmount):P}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  设置失败: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        /// <summary>
        /// 为 LTC客户-1 设置黑名单标志（触发不予授信场景 4），同时清除场景 3 的逾期数据
        /// </summary>
        public static void SetupLtcBlacklist(ServiceClient service)
        {
            Console.WriteLine(">>> 为 LTC客户-1 设置黑名单（触发不予授信场景 4）...");

            try
            {
                // 1. 查找 LTC客户-1
                var accountQuery = new QueryExpression("account")
                {
                    ColumnSet = new ColumnSet("accountid", "name", "accountnumber", "mcs_customermasterdata"),
                    Criteria = new FilterExpression
                    {
                        Conditions = { new ConditionExpression("name", ConditionOperator.Equal, "LTC客户-1") }
                    },
                    TopCount = 5
                };
                var accounts = service.RetrieveMultiple(accountQuery);
                if (accounts.Entities.Count == 0)
                {
                    Console.WriteLine("  ❌ 未找到 LTC客户-1");
                    return;
                }

                var account = accounts.Entities[0];
                var accountId = account.Id;
                var masterDataRef = account.GetAttributeValue<EntityReference>("mcs_customermasterdata");
                Console.WriteLine($"  客户: {account.GetAttributeValue<string>("name")}, id={accountId}");
                Console.WriteLine($"  客户主数据: {(masterDataRef == null ? "空" : masterDataRef.Id.ToString())}");

                // 2. 设置黑名单标志
                if (masterDataRef != null)
                {
                    var update = new Entity("mcs_customermasterdata") { Id = masterDataRef.Id };
                    update["mcs_blacklist"] = true;
                    service.Update(update);
                    Console.WriteLine($"  ✅ 已设置 mcs_customermasterdata {masterDataRef.Id} 的黑名单标志为 true");
                }
                else
                {
                    Console.WriteLine("  ⚠️ 客户主数据为空，无法设置黑名单标志");
                }

                // 3. 清除场景 3 的逾期数据，避免同时触发场景 3
                var outstandingQuery = new QueryExpression("mcs_outstanding")
                {
                    ColumnSet = new ColumnSet("mcs_outstandingid"),
                    Criteria = new FilterExpression
                    {
                        Conditions = { new ConditionExpression("mcs_account", ConditionOperator.Equal, accountId) }
                    },
                    TopCount = 10
                };
                var existing = service.RetrieveMultiple(outstandingQuery);
                foreach (var record in existing.Entities)
                {
                    var update = new Entity("mcs_outstanding") { Id = record.Id };
                    update["mcs_overdurationdays"] = 0;
                    update["mcs_newoverdueamountrmb"] = 0m;
                    update["mcs_newremainingamountrmb"] = 0m;
                    update["mcs_overdue"] = false;
                    service.Update(update);
                    Console.WriteLine($"  ✅ 已清除 mcs_outstanding {record.Id} 的逾期数据");
                }

                Console.WriteLine();
                Console.WriteLine("===== 设置完成 =====");
                Console.WriteLine("LTC客户-1 现在只触发不予授信场景 4（集团黑名单客户）");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  设置失败: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
}
