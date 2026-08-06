// 临时只读审计工具（2026-08-05 生产发版前）：枚举 DEV1 中 createdby=gw_qiuzw 的非托管组件，
// 与主清单 AllComponent_Peter_NoUAT 交叉比对，找出「我创建但不在主清单」的遗漏组件。
// 用法: dotnet run [环境URL]
using D365ToolCommon.Connection;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

var url = args.Length > 0 ? args[0] : "https://dev1.crm5.dynamics.com";
Console.WriteLine($"目标环境: {url}");
var service = await D365ConnectionFactory.CreateAsync(url);


void ConfigCheck()
{
    Console.WriteLine("\n【配置数据核查（当前环境）】");
    var qc = new QueryExpression("ms_systemconfiguration")
    {
        ColumnSet = new ColumnSet("ms_name", "ms_content"),
        Criteria = new FilterExpression { Conditions = { new ConditionExpression("ms_name", ConditionOperator.Equal, "UploadFileTypeMapping") } }
    };
    var recs = new List<Entity>();
    qc.PageInfo = new PagingInfo { Count = 10, PageNumber = 1 };
    var rc = service.RetrieveMultiple(qc);
    recs.AddRange(rc.Entities);
    var rec = recs.FirstOrDefault();
    if (rec == null) { Console.WriteLine("  ❌ 无 UploadFileTypeMapping 记录"); }
    else
    {
        var content = rec.GetAttributeValue<string>("ms_content") ?? "";
        var json = System.Text.Json.JsonDocument.Parse(content);
        int count = json.RootElement.EnumerateObject().Count();
        Console.WriteLine($"  UploadFileTypeMapping 总 key 数: {count}");
        foreach (var k in new[] { "mcs_fsm_resource", "mcs_fsm_detail_data" })
        {
            bool has = json.RootElement.TryGetProperty(k, out var v);
            Console.WriteLine($"  {(has ? "✅" : "❌ 缺失")} {k}: {(has ? v.GetRawText() : "")}");
        }
    }
    var q2 = new QueryExpression("ms_versionconfiguration")
    {
        ColumnSet = new ColumnSet("ms_name", "ms_version"),
        Criteria = new FilterExpression { Conditions = { new ConditionExpression("ms_name", ConditionOperator.Equal, "CommonCacheVersion") } }
    };
    q2.PageInfo = new PagingInfo { Count = 10, PageNumber = 1 };
    var ver = service.RetrieveMultiple(q2).Entities.FirstOrDefault();
    Console.WriteLine($"  CommonCacheVersion 记录: {(ver == null ? "❌ 无" : "✅ " + string.Join(",", ver.Attributes.Select(a => a.Key + "=" + a.Value)))}");
}

List<Entity> All(QueryExpression q)
{
    var all = new List<Entity>();
    q.PageInfo = new PagingInfo { Count = 5000, PageNumber = 1 };
    while (true)
    {
        var r = service.RetrieveMultiple(q);
        all.AddRange(r.Entities);
        if (!r.MoreRecords) break;
        q.PageInfo.PageNumber++;
        q.PageInfo.PagingCookie = r.PagingCookie;
    }
    return all;
}

// 1. 我的 systemuserid
var me = service.RetrieveMultiple(new QueryExpression("systemuser")
{
    ColumnSet = new ColumnSet("systemuserid", "fullname", "domainname"),
    Criteria = new FilterExpression
    {
        Conditions = { new ConditionExpression("domainname", ConditionOperator.Equal, "gw_qiuzw@sanyglobal.onmicrosoft.com") }
    }
}).Entities.FirstOrDefault();
if (me == null) { Console.WriteLine("❌ 未找到用户 gw_qiuzw"); return; }
var myId = me.Id;
Console.WriteLine($"创建人: {me.GetAttributeValue<string>("fullname")} ({myId})");

if (args.Any(a => a == "config-check")) { ConfigCheck(); return; }

if (args.Any(a => a == "export-solution"))
{
    var sIdx = Array.IndexOf(args, "export-solution");
    var solName = args[sIdx + 1];
    var outPath = args[sIdx + 2];
    var managed = args.Any(a => a == "managed");
    Console.WriteLine($"导出解决方案: {solName} (managed={managed})");
    var req = new Microsoft.Crm.Sdk.Messages.ExportSolutionRequest
    {
        SolutionName = solName,
        Managed = managed,
        ExportAutoNumberingSettings = false,
        ExportCalendarSettings = false,
        ExportCustomizationSettings = false,
        ExportEmailTrackingSettings = false,
        ExportGeneralSettings = false,
        ExportIsvConfig = false,
        ExportMarketingSettings = false,
        ExportOutlookSynchronizationSettings = false,
        ExportRelationshipRoles = false
    };
    var resp = (Microsoft.Crm.Sdk.Messages.ExportSolutionResponse)service.Execute(req);
    File.WriteAllBytes(outPath, resp.ExportSolutionFile);
    Console.WriteLine($"  ✓ 导出完成: {outPath}  大小: {resp.ExportSolutionFile.Length / 1024} KB");
    return;
}

if (args.Any(a => a == "wr-date"))
{
    var wr = service.RetrieveMultiple(new QueryExpression("webresource")
    {
        ColumnSet = new ColumnSet("name", "createdon", "modifiedon"),
        Criteria = new FilterExpression { Conditions = { new ConditionExpression("name", ConditionOperator.Equal, "mcs_fsm_resource_multiselect.html") } }
    }).Entities.FirstOrDefault();
    if (wr == null) { Console.WriteLine("❌ 当前环境无此 WebResource"); return; }
    Console.WriteLine($"WebResource: {wr.GetAttributeValue<string>("name")}  id={wr.Id}");
    Console.WriteLine($"  createdon (UTC): {wr.GetAttributeValue<DateTime?>("createdon")}");
    Console.WriteLine($"  modifiedon(UTC): {wr.GetAttributeValue<DateTime?>("modifiedon")}");
    var comps = All(new QueryExpression("solutioncomponent")
    {
        ColumnSet = new ColumnSet("solutionid", "createdon", "modifiedon"),
        Criteria = new FilterExpression { Conditions = { new ConditionExpression("objectid", ConditionOperator.Equal, wr.Id) } }
    });
    foreach (var c in comps)
    {
        var sol = service.Retrieve("solution", c.GetAttributeValue<EntityReference>("solutionid").Id, new ColumnSet("uniquename"));
        Console.WriteLine($"  入包: {sol.GetAttributeValue<string>("uniquename")}  createdon(UTC): {c.GetAttributeValue<DateTime?>("createdon")}  modifiedon(UTC): {c.GetAttributeValue<DateTime?>("modifiedon")}");
    }
    return;
}

// 2. 主清单全部组件 objectid 集合
var manifestSolution = service.RetrieveMultiple(new QueryExpression("solution")
{
    ColumnSet = new ColumnSet("solutionid"),
    Criteria = new FilterExpression
    {
        Conditions = { new ConditionExpression("uniquename", ConditionOperator.Equal, "AllComponent_Peter_NoUAT") }
    }
}).Entities.First();
var manifestIds = All(new QueryExpression("solutioncomponent")
{
    ColumnSet = new ColumnSet("objectid"),
    Criteria = new FilterExpression
    {
        Conditions = { new ConditionExpression("solutionid", ConditionOperator.Equal, manifestSolution.Id) }
    }
}).Select(c => c.GetAttributeValue<Guid>("objectid")).ToHashSet();
Console.WriteLine($"主清单组件数: {manifestIds.Count}");

// 3. 也收集 entity_20260805 的组件（辅助判断）
var relSolution = service.RetrieveMultiple(new QueryExpression("solution")
{
    ColumnSet = new ColumnSet("solutionid"),
    Criteria = new FilterExpression
    {
        Conditions = { new ConditionExpression("uniquename", ConditionOperator.Equal, "entity_20260805") }
    }
}).Entities.First();
var relIds = All(new QueryExpression("solutioncomponent")
{
    ColumnSet = new ColumnSet("objectid"),
    Criteria = new FilterExpression
    {
        Conditions = { new ConditionExpression("solutionid", ConditionOperator.Equal, relSolution.Id) }
    }
}).Select(c => c.GetAttributeValue<Guid>("objectid")).ToHashSet();

// 4. 逐类枚举 createdby=我 的组件
(int total, int missing) Report(string table, string nameField, string label, FilterExpression? extra = null)
{
    var f = new FilterExpression();
    f.Conditions.Add(new ConditionExpression("createdby", ConditionOperator.Equal, myId));
    if (extra != null) f.AddFilter(extra);
    List<Entity> rows;
    try { rows = All(new QueryExpression(table) { ColumnSet = new ColumnSet(nameField), Criteria = f }); }
    catch (Exception ex) { Console.WriteLine($"\n【{label} ({table})】跳过（{ex.Message.Split('\n')[0]}）"); return (0, 0); }
    int miss = 0;
    Console.WriteLine($"\n【{label} ({table})】我创建的共 {rows.Count} 个");
    foreach (var r in rows.OrderBy(r => r.GetAttributeValue<string>(nameField)))
    {
        bool inManifest = manifestIds.Contains(r.Id);
        bool inRel = relIds.Contains(r.Id);
        var name = r.GetAttributeValue<string>(nameField) ?? r.Id.ToString();
        if (!inManifest)
        {
            miss++;
            Console.WriteLine($"  ❌ 不在主清单: {name}  {r.Id}{(inRel ? "（但在 entity_20260805）" : "")}");
        }
        else
        {
            Console.WriteLine($"  ✅ {name}");
        }
    }
    return (rows.Count, miss);
}

int totalMiss = 0;
var onlyCustom = new FilterExpression { Conditions = { new ConditionExpression("iscustomizable", ConditionOperator.Equal, true) } };
var notManaged = new FilterExpression { Conditions = { new ConditionExpression("ismanaged", ConditionOperator.Equal, false) } };


// entity 元数据表查询列受限且全量元数据过大 —— 按我方模块前缀 Like 查询候选实体
{
    var prefixes = new[] { "mcs_credit%", "mcs_coface%", "mcs_trade%", "mcs_fca%", "mcs_fsm%", "mcs_fm%" };
    var f = new FilterExpression(LogicalOperator.Or);
    foreach (var px in prefixes) f.Conditions.Add(new ConditionExpression("name", ConditionOperator.Like, px));
    var rows = All(new QueryExpression("entity") { ColumnSet = new ColumnSet("name"), Criteria = f });
    int miss = 0;
    Console.WriteLine($"\n【实体 (entity)】我方模块前缀（credit/coface/trade/fca/fsm/fm）候选实体共 {rows.Count} 个（元数据无创建人，人工甄别）");
    foreach (var r in rows.OrderBy(r => r.GetAttributeValue<string>("name")))
    {
        var name = r.GetAttributeValue<string>("name") ?? r.Id.ToString();
        bool inManifest = manifestIds.Contains(r.Id);
        if (!inManifest)
        {
            miss++;
            Console.WriteLine($"  ❓ 不在主清单: {name}  {r.Id}{(relIds.Contains(r.Id) ? "（已在 entity_20260805）" : "")}");
        }
        else Console.WriteLine($"  ✅ {name}");
    }
    totalMiss += miss;
}

totalMiss += Report("webresource", "name", "Web资源", notManaged).missing;
totalMiss += Report("workflow", "name", "工作流/BPF", null).missing;
totalMiss += Report("customapi", "uniquename", "CustomAPI", null).missing;
totalMiss += Report("appaction", "uniquename", "AppAction", null).missing;
totalMiss += Report("sdkmessageprocessingstep", "name", "Plugin Step", null).missing;
totalMiss += Report("pluginassembly", "name", "Plugin Assembly", notManaged).missing;
totalMiss += Report("savedquery", "name", "视图", null).missing;
totalMiss += Report("systemform", "name", "表单", null).missing;
totalMiss += Report("optionset", "name", "全局选项集", notManaged).missing;
totalMiss += Report("sitemap", "sitemapname", "站点地图", null).missing;
totalMiss += Report("appmodule", "name", "应用", null).missing;

Console.WriteLine($"\n═══ 审计完成：我创建但不在主清单的组件共 {totalMiss} 个 ═══");

// UAT UploadFileTypeMapping / CommonCacheVersion 完整性核查
{
    Console.WriteLine("\n【UAT 配置数据核查】");
    var q = new QueryExpression("ms_systemconfiguration")
    {
        ColumnSet = new ColumnSet("ms_name", "ms_content"),
        Criteria = new FilterExpression { Conditions = { new ConditionExpression("ms_name", ConditionOperator.Equal, "UploadFileTypeMapping") } }
    };
    var rec = All(q).FirstOrDefault();
    if (rec == null) { Console.WriteLine("  ❌ UAT 无 UploadFileTypeMapping 记录"); }
    else
    {
        var content = rec.GetAttributeValue<string>("ms_content") ?? "";
        var json = System.Text.Json.JsonDocument.Parse(content);
        int count = json.RootElement.EnumerateObject().Count();
        Console.WriteLine($"  UploadFileTypeMapping 总 key 数: {count}");
        foreach (var k in new[] { "mcs_fsm_resource", "mcs_fsm_detail_data" })
            Console.WriteLine($"  {(json.RootElement.TryGetProperty(k, out var v) ? "✅" : "❌ 缺失")} {k}: {(json.RootElement.TryGetProperty(k, out var vv) ? vv.GetRawText() : "")}");
    }
    var q2 = new QueryExpression("ms_versionconfiguration")
    {
        ColumnSet = new ColumnSet("ms_name", "ms_version"),
        Criteria = new FilterExpression { Conditions = { new ConditionExpression("ms_name", ConditionOperator.Equal, "CommonCacheVersion") } }
    };
    var ver = All(q2).FirstOrDefault();
    Console.WriteLine($"  CommonCacheVersion: {(ver == null ? "❌ 无记录" : ver.GetAttributeValue<decimal?>("ms_version")?.ToString() ?? ver.GetAttributeValue<int?>("ms_version")?.ToString() ?? "(字段类型待查)")}");
}

// 复核 c33f9d6f 是否在 McsPlugin（check-release 报缺失 vs add 报成功 矛盾）
{
    var mcsPlugin = service.RetrieveMultiple(new QueryExpression("solution")
    {
        ColumnSet = new ColumnSet("solutionid"),
        Criteria = new FilterExpression { Conditions = { new ConditionExpression("uniquename", ConditionOperator.Equal, "McsPlugin") } }
    }).Entities.First().Id;
    var rows = All(new QueryExpression("solutioncomponent")
    {
        ColumnSet = new ColumnSet("objectid", "componenttype"),
        Criteria = new FilterExpression
        {
            Conditions =
            {
                new ConditionExpression("solutionid", ConditionOperator.Equal, mcsPlugin),
                new ConditionExpression("objectid", ConditionOperator.Equal, Guid.Parse("c33f9d6f-eb74-f111-ab0f-7ced8db4d37f"))
            }
        }
    });
    Console.WriteLine($"\n【c33f9d6f 在 McsPlugin 复核】component 行数={rows.Count}");
    foreach (var r in rows) Console.WriteLine($"  type={r.GetAttributeValue<OptionSetValue>("componenttype")?.Value} objectid={r.GetAttributeValue<Guid>("objectid")}");
}

// 注销前检查：SanyD365.Plugins.CustomerFile 及 Step/Type 是否挂在任何 Solution
{
    var asmId = Guid.Parse("1bca65a9-6968-f111-ab0c-7ced8db4dd60");
    var typeId = Guid.Parse("1eca65a9-6968-f111-ab0c-7ced8db4dd60");
    var stepId = Guid.Parse("28ca65a9-6968-f111-ab0c-7ced8db4dd60");
    Console.WriteLine("\n【CustomerFile 独立 Assembly Solution 挂载检查】");
    var q = new QueryExpression("solutioncomponent")
    {
        ColumnSet = new ColumnSet("objectid", "componenttype", "solutionid"),
        Criteria = new FilterExpression(LogicalOperator.Or)
        {
            Conditions =
            {
                new ConditionExpression("objectid", ConditionOperator.Equal, asmId),
                new ConditionExpression("objectid", ConditionOperator.Equal, typeId),
                new ConditionExpression("objectid", ConditionOperator.Equal, stepId),
            }
        }
    };
    var rows = All(q);
    if (rows.Count == 0) Console.WriteLine("  ✅ Assembly/Type/Step 均未挂在任何 Solution");
    foreach (var r in rows)
    {
        var sol = service.Retrieve("solution", r.GetAttributeValue<EntityReference>("solutionid").Id, new ColumnSet("uniquename"));
        Console.WriteLine($"  ⚠️ 挂在 Solution {sol.GetAttributeValue<string>("uniquename")}: type={r.GetAttributeValue<OptionSetValue>("componenttype")?.Value} objectid={r.GetAttributeValue<Guid>("objectid")}");
    }
}

// 修复 fca_proc 损坏 Step：备用键删除 → 显式 ID 重建
{
    var brokenId = Guid.Parse("c33f9d6f-eb74-f111-ab0f-7ced8db4d37f");
    var idUnique = Guid.Parse("6d7940b5-ff45-46a2-ba94-371d579c6338");
    Console.WriteLine("\n【修复尝试 1】按备用键 sdkmessageprocessingstepidunique 删除");
    try
    {
        var keys = new KeyAttributeCollection { { "sdkmessageprocessingstepidunique", idUnique } };
        service.Execute(new Microsoft.Xrm.Sdk.Messages.DeleteRequest { Target = new EntityReference("sdkmessageprocessingstep", keys) });
        Console.WriteLine("  ✅ 备用键删除成功");
    }
    catch (Exception ex) { Console.WriteLine($"  ❌ {ex.Message.Split('\n')[0]}"); }

    // 删除后复查名称查询
    var checkQ = new QueryExpression("sdkmessageprocessingstep")
    {
        ColumnSet = new ColumnSet("name"),
        Criteria = new FilterExpression { Conditions = { new ConditionExpression("name", ConditionOperator.Equal, "EntityValidateCreateForGenerateNumber: Create of mcs_fca_proc") } }
    };
    var remain = All(checkQ);
    Console.WriteLine($"  名称查询残留行数: {remain.Count}");

    Console.WriteLine("\n【修复尝试 2】显式 ID 重建 Step（配置与兄弟 Step 完全一致）");
    try
    {
        var st = new Entity("sdkmessageprocessingstep") { Id = brokenId };
        st["name"] = "EntityValidateCreateForGenerateNumber: Create of mcs_fca_proc";
        st["plugintypeid"] = new EntityReference("plugintype", Guid.Parse("601edf86-b9ae-4cfa-a240-7abb5bc2ae4b"));
        st["sdkmessageid"] = new EntityReference("sdkmessage", Guid.Parse("9ebdbb1b-ea3e-db11-86a7-000a3a5473e8"));
        st["sdkmessagefilterid"] = new EntityReference("sdkmessagefilter", Guid.Parse("211502a6-6873-f111-ab0e-7ced8de4e391"));
        st["stage"] = new OptionSetValue(10);
        st["mode"] = new OptionSetValue(0);
        st["rank"] = 1;
        st["supporteddeployment"] = new OptionSetValue(0);
        var newId = service.Create(st);
        Console.WriteLine($"  ✅ 创建成功: {newId}");
    }
    catch (Exception ex) { Console.WriteLine($"  ❌ {ex.Message.Split('\n')[0]}"); }

    // 最终验证：Retrieve + 名称查询
    try { var r = service.Retrieve("sdkmessageprocessingstep", brokenId, new ColumnSet("name", "statecode")); Console.WriteLine($"  ✅ Retrieve 恢复: {r.GetAttributeValue<string>("name")} state={r.GetAttributeValue<OptionSetValue>("statecode")?.Value}"); }
    catch (Exception ex) { Console.WriteLine($"  ❌ Retrieve 仍失败: {ex.Message.Split('\n')[0]}"); }
}

// 查 EntityValidateCreateForGenerateNumber 全部 plugintype 记录（确认是否同名多条）
{
    var q = new QueryExpression("plugintype")
    {
        ColumnSet = new ColumnSet("typename", "name", "assemblyname", "pluginassemblyid"),
        Criteria = new FilterExpression { Conditions = { new ConditionExpression("typename", ConditionOperator.Equal, "MSLibrary.D365.Common.Plugins.EntityValidateCreateForGenerateNumber") } }
    };
    Console.WriteLine("\n【同名 PluginType 记录】");
    foreach (var t in All(q))
        Console.WriteLine($"  {t.Id}  name={t.GetAttributeValue<string>("name")}  assembly={t.GetAttributeValue<string>("assemblyname")}");
}

// 按名称取异常行全部可读列 + 健康兄弟 Step（fca_records）完整配置
{
    Console.WriteLine("\n【按名称查 fca_proc 异常行】");
    var q1 = new QueryExpression("sdkmessageprocessingstep")
    {
        ColumnSet = new ColumnSet(true),
        Criteria = new FilterExpression { Conditions = { new ConditionExpression("name", ConditionOperator.Equal, "EntityValidateCreateForGenerateNumber: Create of mcs_fca_proc") } }
    };
    foreach (var st in All(q1))
    {
        Console.WriteLine($"  rows=1 id={st.Id}");
        foreach (var kv in st.Attributes.OrderBy(x => x.Key))
        {
            var v = kv.Value is EntityReference er ? $"{er.LogicalName}:{er.Name}({er.Id})" : kv.Value is OptionSetValue os ? $"os:{os.Value}" : kv.Value;
            Console.WriteLine($"    {kv.Key} = {v}");
        }
    }
    Console.WriteLine("\n【健康兄弟 fca_records Step 完整配置】");
    var healthy = service.Retrieve("sdkmessageprocessingstep", Guid.Parse("11de51c5-f674-f111-ab0e-6045bd1c0cde"), new ColumnSet(true));
    foreach (var kv in healthy.Attributes.OrderBy(x => x.Key))
    {
        var v = kv.Value is EntityReference er ? $"{er.LogicalName}:{er.Name}({er.Id})" : kv.Value is OptionSetValue os ? $"os:{os.Value}" : kv.Value;
        Console.WriteLine($"    {kv.Key} = {v}");
    }
}

// 取 fca_proc 异常 Step 完整配置（list 查询可读，Retrieve 不可读）
{
    var q = new QueryExpression("sdkmessageprocessingstep")
    {
        ColumnSet = new ColumnSet("name", "statecode", "statuscode", "stage", "mode", "rank", "filteringattributes", "sdkmessageid", "sdkmessagefilterid", "plugintypeid", "eventhandler", "supporteddeployment", "invocationsource", "asyncautodelete", "description", "configuration"),
        Criteria = new FilterExpression { Conditions = { new ConditionExpression("sdkmessageprocessingstepid", ConditionOperator.Equal, Guid.Parse("c33f9d6f-eb74-f111-ab0f-7ced8db4d37f")) } }
    };
    var rows = All(q);
    Console.WriteLine($"\n【fca_proc 异常 Step 配置】rows={rows.Count}");
    foreach (var st in rows)
    {
        foreach (var kv in st.Attributes)
        {
            var v = kv.Value is EntityReference er ? $"{er.LogicalName}:{er.Name}({er.Id})" : kv.Value is OptionSetValue os ? $"os:{os.Value}" : kv.Value;
            Console.WriteLine($"  {kv.Key} = {v}");
        }
    }
}

// 复核：5 个缺口 Step 当前在主清单/McsPlugin 的真实状态 + fca_proc Step 存在性
{
    var checkIds = new (string Label, Guid Id)[]
    {
        ("CreditScoreBpfStageSync", Guid.Parse("e9f26b5c-ad64-f111-ab0d-000d3aa3319c")),
        ("TradePtGroupTypeSync-Create", Guid.Parse("4ff9c14e-6770-f111-ab0f-7ced8de4e391")),
        ("TradePtGroupTypeSync-Update", Guid.Parse("b9fb3ecf-6970-f111-ab0f-7ced8de4edac")),
        ("AutoNum-fca_proc", Guid.Parse("c33f9d6f-eb74-f111-ab0f-7ced8db4d37f")),
        ("AutoNum-fca_quotaapp", Guid.Parse("23592f41-0079-f111-ab0e-7ced8db4d7a7")),
    };
    var mcsPlugin = service.RetrieveMultiple(new QueryExpression("solution")
    {
        ColumnSet = new ColumnSet("solutionid"),
        Criteria = new FilterExpression { Conditions = { new ConditionExpression("uniquename", ConditionOperator.Equal, "McsPlugin") } }
    }).Entities.First().Id;
    var mcsPluginIds = All(new QueryExpression("solutioncomponent")
    {
        ColumnSet = new ColumnSet("objectid"),
        Criteria = new FilterExpression { Conditions = { new ConditionExpression("solutionid", ConditionOperator.Equal, mcsPlugin) } }
    }).Select(c => c.GetAttributeValue<Guid>("objectid")).ToHashSet();
    Console.WriteLine("\n【5 Step 复核（主清单 / McsPlugin / 环境中是否可Retrieve）】");
    foreach (var (label, id) in checkIds)
    {
        string exist;
        try { service.Retrieve("sdkmessageprocessingstep", id, new ColumnSet("name")); exist = "✅存在"; }
        catch { exist = "❌Retrieve失败"; }
        Console.WriteLine($"  {label}: 主清单={(manifestIds.Contains(id) ? "✅" : "❌")} McsPlugin={(mcsPluginIds.Contains(id) ? "✅" : "❌")} 环境={exist}");
    }
}

// 追加：核查 6 个可疑 Step 的启用状态
var stepIds = new (string Label, Guid Id)[]
{
    ("CreditScoreBpfStageSyncPlugin: Update of mcs_credit_record", Guid.Parse("e9f26b5c-ad64-f111-ab0d-000d3aa3319c")),
    ("TradePtGroupTypeProductLineSyncPlugin: Create of mcs_trade_ptgrouptype", Guid.Parse("4ff9c14e-6770-f111-ab0f-7ced8de4e391")),
    ("TradePtGroupTypeProductLineSyncPlugin: Update of mcs_trade_ptgrouptype", Guid.Parse("b9fb3ecf-6970-f111-ab0f-7ced8de4edac")),
    ("EntityValidateCreateForGenerateNumber: Create of mcs_fca_proc", Guid.Parse("c33f9d6f-eb74-f111-ab0f-7ced8db4d37f")),
    ("EntityValidateCreateForGenerateNumber: Create of mcs_fca_quotaapp", Guid.Parse("23592f41-0079-f111-ab0e-7ced8db4d7a7")),
    ("CustomerFileAutoNumberPlugin: Create of mcs_customer_file", Guid.Parse("28ca65a9-6968-f111-ab0c-7ced8db4dd60")),
};
Console.WriteLine("\n【EntityValidateCreateForGenerateNumber 全部 Step】");
{
    var q = new QueryExpression("sdkmessageprocessingstep")
    {
        ColumnSet = new ColumnSet("name", "statecode", "plugintypeid", "sdkmessagefilterid", "stage"),
        Criteria = new FilterExpression
        {
            Conditions = { new ConditionExpression("name", ConditionOperator.BeginsWith, "EntityValidateCreateForGenerateNumber") }
        }
    };
    LinkEntity pt = q.AddLink("plugintype", "plugintypeid", "plugintypeid");
    pt.Columns = new ColumnSet("assemblyname", "typename");
    pt.EntityAlias = "pt";
    foreach (var st in All(q).OrderBy(x => x.GetAttributeValue<string>("name")))
    {
        var asm = st.GetAttributeValue<AliasedValue>("pt.assemblyname")?.Value;
        var msg = st.GetAttributeValue<EntityReference>("sdkmessagefilterid");
        Console.WriteLine($"  {st.GetAttributeValue<string>("name")}  {st.Id}\n    statecode={st.GetAttributeValue<OptionSetValue>("statecode")?.Value} assembly={asm} 主清单={(manifestIds.Contains(st.Id) ? "✅" : "❌")}");
    }
}

Console.WriteLine("\n【可疑 Step 状态核查】");
foreach (var (label, id) in stepIds)
{
    try
    {
        var st = service.Retrieve("sdkmessageprocessingstep", id, new ColumnSet("name", "statecode", "statuscode", "stage", "mode", "filteringattributes", "sdkmessageid", "plugintypeid"));
        var state = st.GetAttributeValue<OptionSetValue>("statecode")?.Value;
        var status = st.GetAttributeValue<OptionSetValue>("statuscode")?.Value;
        var pt = st.GetAttributeValue<EntityReference>("plugintypeid");
        Console.WriteLine($"  {label}\n    statecode={state} statuscode={status}（0/-1=启用） stage={st.GetAttributeValue<OptionSetValue>("stage")?.Value} mode={st.GetAttributeValue<OptionSetValue>("mode")?.Value} filter=[{st.GetAttributeValue<string>("filteringattributes")}]\n    plugintype={pt?.Name} ({pt?.Id})");
    }
    catch (Exception ex) { Console.WriteLine($"  {label} —— 查询失败: {ex.Message.Split('\n')[0]}"); }
}

