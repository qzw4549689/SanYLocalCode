// 临时只读审计工具（2026-08-20 生产发版前）：枚举 DEV1 中 createdby=gw_qiuzw 的非托管组件，
// 与主清单 AllComponent_Peter_NoUAT 及固定包（McsPlugin/McsWebResource/McsCustomAPI）交叉比对，
// 找出「我创建但不在主清单/固定包」的遗漏组件。全程只读。
// 用法: dotnet run [环境URL]
using D365ToolCommon.Connection;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

var url = args.Length > 0 ? args[0] : "https://dev1.crm5.dynamics.com";

// 一次性改名模式（2026-09-02 用户授权生产执行）：dotnet run rename-sdkmessage <环境URL> <sdkmessageId> <新名称>
if (args.Length >= 4 && args[0] == "rename-sdkmessage")
{
    var targetUrl = args[1];
    var msgId = Guid.Parse(args[2]);
    var newName = args[3];
    Console.WriteLine($"目标环境: {targetUrl}");
    var svc = await D365ConnectionFactory.CreateAsync(targetUrl);
    var before = svc.Retrieve("sdkmessage", msgId, new ColumnSet("name"));
    var oldName = before.GetAttributeValue<string>("name");
    Console.WriteLine($"改名前: {oldName}  (id={msgId})");
    svc.Update(new Entity("sdkmessage", msgId) { ["name"] = newName });
    var after = svc.Retrieve("sdkmessage", msgId, new ColumnSet("name"));
    Console.WriteLine($"改名后回读: {after.GetAttributeValue<string>("name")}");
    Console.WriteLine("✅ 完成");
    return;
}

// 一次性停用 workflow 模式（2026-09-02 用户授权生产执行）：dotnet run deactivate-workflow <环境URL> <workflowId>
if (args.Length >= 3 && args[0] == "deactivate-workflow")
{
    var targetUrl = args[1];
    var wfId = Guid.Parse(args[2]);
    Console.WriteLine($"目标环境: {targetUrl}");
    var svc = await D365ConnectionFactory.CreateAsync(targetUrl);
    var before = svc.Retrieve("workflow", wfId, new ColumnSet("name", "statecode", "statuscode"));
    Console.WriteLine($"停用前: {before.GetAttributeValue<string>("name")} state={before.GetAttributeValue<OptionSetValue>("statecode")?.Value} status={before.GetAttributeValue<OptionSetValue>("statuscode")?.Value}");
    svc.Execute(new Microsoft.Crm.Sdk.Messages.SetStateRequest
    {
        EntityMoniker = new EntityReference("workflow", wfId),
        State = new OptionSetValue(0),
        Status = new OptionSetValue(-1)
    });
    var after = svc.Retrieve("workflow", wfId, new ColumnSet("statecode", "statuscode"));
    Console.WriteLine($"停用后回读: state={after.GetAttributeValue<OptionSetValue>("statecode")?.Value} status={after.GetAttributeValue<OptionSetValue>("statuscode")?.Value}");
    Console.WriteLine("✅ 完成");
    return;
}

Console.WriteLine($"目标环境: {url}");
var service = await D365ConnectionFactory.CreateAsync(url);

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

Guid SolutionId(string uniqueName) => service.RetrieveMultiple(new QueryExpression("solution")
{
    ColumnSet = new ColumnSet("solutionid"),
    Criteria = new FilterExpression { Conditions = { new ConditionExpression("uniquename", ConditionOperator.Equal, uniqueName) } }
}).Entities.First().Id;

HashSet<Guid> ComponentIds(Guid solId) => All(new QueryExpression("solutioncomponent")
{
    ColumnSet = new ColumnSet("objectid"),
    Criteria = new FilterExpression { Conditions = { new ConditionExpression("solutionid", ConditionOperator.Equal, solId) } }
}).Select(c => c.GetAttributeValue<Guid>("objectid")).ToHashSet();

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

// 2. 主清单 + 固定包组件集合
var manifestIds = ComponentIds(SolutionId("AllComponent_Peter_NoUAT"));
Console.WriteLine($"主清单组件数: {manifestIds.Count}");
var mcsPluginIds = ComponentIds(SolutionId("McsPlugin"));
Console.WriteLine($"McsPlugin 组件数: {mcsPluginIds.Count}");
var mcsWrIds = ComponentIds(SolutionId("McsWebResource"));
Console.WriteLine($"McsWebResource 组件数: {mcsWrIds.Count}");
var mcsApiIds = ComponentIds(SolutionId("McsCustomAPI"));
Console.WriteLine($"McsCustomAPI 组件数: {mcsApiIds.Count}");

// 3. 逐类枚举 createdby=我 的组件
int totalMiss = 0;
void Report(string table, string nameField, string label, HashSet<Guid>? fixedPkg = null, string fixedPkgName = "", FilterExpression? extra = null)
{
    var f = new FilterExpression();
    f.Conditions.Add(new ConditionExpression("createdby", ConditionOperator.Equal, myId));
    if (extra != null) f.AddFilter(extra);
    List<Entity> rows;
    try { rows = All(new QueryExpression(table) { ColumnSet = new ColumnSet(nameField, "createdon"), Criteria = f }); }
    catch (Exception ex) { Console.WriteLine($"\n【{label} ({table})】跳过（{ex.Message.Split('\n')[0]}）"); return; }
    int miss = 0;
    Console.WriteLine($"\n【{label} ({table})】我创建的共 {rows.Count} 个");
    foreach (var r in rows.OrderBy(r => r.GetAttributeValue<DateTime?>("createdon")))
    {
        bool inManifest = manifestIds.Contains(r.Id);
        bool inFixed = fixedPkg?.Contains(r.Id) ?? false;
        var name = r.GetAttributeValue<string>(nameField) ?? r.Id.ToString();
        var state = r.Contains("statecode") ? r.GetAttributeValue<OptionSetValue>("statecode")?.Value : null;
        var created = r.GetAttributeValue<DateTime?>("createdon")?.ToLocalTime().ToString("MM-dd");
        string tag = state == 1 ? " [停用]" : "";
        if (!inManifest)
        {
            miss++;
            Console.WriteLine($"  ❌ 不在主清单: {name}{tag}  {r.Id}  ({created}){(inFixed ? $"（但在 {fixedPkgName}）" : "")}");
        }
        else if (fixedPkg != null && !inFixed)
        {
            Console.WriteLine($"  ⚠️ 主清单✅ 但不在 {fixedPkgName}: {name}{tag}  {r.Id}  ({created})");
        }
        else
        {
            Console.WriteLine($"  ✅ {name}{tag}  ({created})");
        }
    }
    totalMiss += miss;
    if (miss > 0) Console.WriteLine($"  → 本类缺口 {miss} 个");
}

var notManaged = new FilterExpression { Conditions = { new ConditionExpression("ismanaged", ConditionOperator.Equal, false) } };

// 实体（元数据无 createdby，按模块前缀候选 + 人工甄别）
{
    var prefixes = new[] { "mcs_credit%", "mcs_coface%", "mcs_trade%", "mcs_fca%", "mcs_fsm%", "mcs_fm%" };
    var f = new FilterExpression(LogicalOperator.Or);
    foreach (var px in prefixes) f.Conditions.Add(new ConditionExpression("name", ConditionOperator.Like, px));
    var rows = All(new QueryExpression("entity") { ColumnSet = new ColumnSet("name"), Criteria = f });
    int miss = 0;
    Console.WriteLine($"\n【实体 (entity)】我方模块前缀候选共 {rows.Count} 个（无创建人，人工甄别）");
    foreach (var r in rows.OrderBy(r => r.GetAttributeValue<string>("name")))
    {
        var name = r.GetAttributeValue<string>("name") ?? r.Id.ToString();
        if (!manifestIds.Contains(r.Id)) { miss++; Console.WriteLine($"  ❓ 不在主清单: {name}  {r.Id}"); }
        else Console.WriteLine($"  ✅ {name}");
    }
    totalMiss += miss;
}

Report("webresource", "name", "Web资源", mcsWrIds, "McsWebResource", notManaged);
Report("workflow", "name", "工作流/BPF");
Report("customapi", "uniquename", "CustomAPI", mcsApiIds, "McsCustomAPI");
Report("appaction", "uniquename", "AppAction");
Report("sdkmessageprocessingstep", "name", "Plugin Step", mcsPluginIds, "McsPlugin");
Report("pluginassembly", "name", "Plugin Assembly", mcsPluginIds, "McsPlugin", notManaged);
Report("savedquery", "name", "视图");
Report("systemform", "name", "表单");
Report("optionset", "name", "全局选项集", null, "", notManaged);
Report("sitemap", "sitemapname", "站点地图");
Report("appmodule", "name", "应用");

Console.WriteLine($"\n═══ 反向审计完成：我创建但不在主清单的组件共 {totalMiss} 个 ═══");

// 4. 8-5 后新增 Step 专项（不限创建人，防他人代建/平台代建漏网）
{
    var since = new DateTime(2026, 8, 5, 16, 0, 0, DateTimeKind.Utc); // 北京时间 8-6 00:00，留余量覆盖 8-5 发版后
    var q = new QueryExpression("sdkmessageprocessingstep")
    {
        ColumnSet = new ColumnSet("name", "createdon", "createdby", "statecode"),
        Criteria = new FilterExpression { Conditions = { new ConditionExpression("createdon", ConditionOperator.GreaterThan, since) } }
    };
    var rows = All(q).OrderBy(r => r.GetAttributeValue<DateTime?>("createdon")).ToList();
    Console.WriteLine($"\n【8-5 后全环境新增 Step】共 {rows.Count} 个");
    foreach (var r in rows)
    {
        var cb = r.GetAttributeValue<EntityReference>("createdby")?.Name;
        var state = r.GetAttributeValue<OptionSetValue>("statecode")?.Value;
        Console.WriteLine($"  {(manifestIds.Contains(r.Id) ? "✅主清单" : "❌不在主清单")}/{(mcsPluginIds.Contains(r.Id) ? "✅McsPlugin" : "❌不在McsPlugin")} {r.GetAttributeValue<string>("name")}  by={cb} state={state} ({r.GetAttributeValue<DateTime?>("createdon")?.ToLocalTime():MM-dd HH:mm})");
    }
}

// 5. 8-5 后新增/变更 WebResource 专项（modifiedon>8-5 且为 mcs_ 前缀我方资源）
{
    var since = new DateTime(2026, 8, 5, 16, 0, 0, DateTimeKind.Utc);
    var q = new QueryExpression("webresource")
    {
        ColumnSet = new ColumnSet("name", "modifiedon", "modifiedby"),
        Criteria = new FilterExpression
        {
            Conditions =
            {
                new ConditionExpression("modifiedon", ConditionOperator.GreaterThan, since),
                new ConditionExpression("name", ConditionOperator.BeginsWith, "mcs_"),
            }
        }
    };
    var rows = All(q).OrderBy(r => r.GetAttributeValue<DateTime?>("modifiedon")).ToList();
    Console.WriteLine($"\n【8-5 后变更的 mcs_ WebResource】共 {rows.Count} 个");
    foreach (var r in rows)
    {
        var mb = r.GetAttributeValue<EntityReference>("modifiedby")?.Name;
        Console.WriteLine($"  {(mcsWrIds.Contains(r.Id) ? "✅McsWebResource" : "❌不在McsWebResource")} {r.GetAttributeValue<string>("name")}  by={mb} ({r.GetAttributeValue<DateTime?>("modifiedon")?.ToLocalTime():MM-dd HH:mm})");
    }
}

// 6. 8-5 后新增 Custom API 专项（不限创建人）
{
    var since = new DateTime(2026, 8, 5, 16, 0, 0, DateTimeKind.Utc);
    var q = new QueryExpression("customapi")
    {
        ColumnSet = new ColumnSet("uniquename", "createdon", "createdby"),
        Criteria = new FilterExpression { Conditions = { new ConditionExpression("createdon", ConditionOperator.GreaterThan, since) } }
    };
    var rows = All(q);
    Console.WriteLine($"\n【8-5 后新增 Custom API】共 {rows.Count} 个");
    foreach (var r in rows)
    {
        var cb = r.GetAttributeValue<EntityReference>("createdby")?.Name;
        Console.WriteLine($"  {(manifestIds.Contains(r.Id) ? "✅主清单" : "❌不在主清单")}/{(mcsApiIds.Contains(r.Id) ? "✅McsCustomAPI" : "❌不在McsCustomAPI")} {r.GetAttributeValue<string>("uniquename")}  by={cb} ({r.GetAttributeValue<DateTime?>("createdon")?.ToLocalTime():MM-dd HH:mm})");
    }
}

// 7. 8-5 后新增 AppAction 专项（不限创建人，防 Command Designer 副本漏网）
{
    var since = new DateTime(2026, 8, 5, 16, 0, 0, DateTimeKind.Utc);
    var q = new QueryExpression("appaction")
    {
        ColumnSet = new ColumnSet("uniquename", "createdon", "createdby", "statecode"),
        Criteria = new FilterExpression { Conditions = { new ConditionExpression("createdon", ConditionOperator.GreaterThan, since) } }
    };
    var rows = All(q);
    Console.WriteLine($"\n【8-5 后新增 AppAction】共 {rows.Count} 个");
    foreach (var r in rows)
    {
        var cb = r.GetAttributeValue<EntityReference>("createdby")?.Name;
        var state = r.GetAttributeValue<OptionSetValue>("statecode")?.Value;
        Console.WriteLine($"  {(manifestIds.Contains(r.Id) ? "✅主清单" : "❌不在主清单")} {r.GetAttributeValue<string>("uniquename")}  by={cb} state={state} ({r.GetAttributeValue<DateTime?>("createdon")?.ToLocalTime():MM-dd HH:mm})");
    }
}

// 8. 退休 appaction 红线复核：stpayterm apply/approve/reject 在 DEV1 的停用状态 + 是否混入 entity_20260727_peter
{
    Console.WriteLine($"\n【退休 AppAction 红线复核（#1834，禁止加入任何发版包）】");
    var q = new QueryExpression("appaction")
    {
        ColumnSet = new ColumnSet("uniquename", "statecode"),
        Criteria = new FilterExpression { Conditions = { new ConditionExpression("uniquename", ConditionOperator.Like, "%stpayterm%") } }
    };
    var rel27 = ComponentIds(SolutionId("entity_20260727_peter"));
    foreach (var r in All(q))
    {
        var state = r.GetAttributeValue<OptionSetValue>("statecode")?.Value;
        Console.WriteLine($"  {r.GetAttributeValue<string>("uniquename")}  state={state}  在entity_20260727_peter={(rel27.Contains(r.Id) ? "⚠️是" : "否")}");
    }
}

// 9. 我方 5 个 Custom API 的实现 Step 是否在 McsCustomAPI（历史事故：Entity 'SdkMessage' Does Not Exist）
{
    Console.WriteLine($"\n【Custom API 实现 Step 在 McsCustomAPI 核对】");
    var implIds = new (string Label, Guid Id)[]
    {
        ("mcs_QueryTradeStPayTerm impl", Guid.Parse("49746c78-c78b-f111-8077-7ced8de4efcf")),
        ("mcs_CofaceSearchCompany impl", Guid.Parse("6001e2c0-cc75-f111-ab0e-6045bd1c0cde")),
        ("mcs_AdjustFcaQuotaBalance impl", Guid.Parse("7602c90d-7b91-f111-8077-6045bd1d22ee")),
        ("mcs_CofacePlaceOrder impl", Guid.Parse("2afacb82-a38d-f111-8077-7ced8de4efcf")),
        ("mcs_CalcContractRiskExposure impl", Guid.Parse("95609cd8-b797-f111-b8dc-6045bd1c0eeb")),
    };
    foreach (var (label, id) in implIds)
        Console.WriteLine($"  {label}: 主清单={(manifestIds.Contains(id) ? "✅" : "❌")} McsCustomAPI={(mcsApiIds.Contains(id) ? "✅" : "❌")} McsPlugin={(mcsPluginIds.Contains(id) ? "✅" : "❌")}");
}

// 10. mcs_feishu WebResource 创建人甄别（8-5 后变更但不在 McsWebResource）
{
    Console.WriteLine($"\n【mcs_feishu 甄别】");
    var q = new QueryExpression("webresource")
    {
        ColumnSet = new ColumnSet("name", "createdby", "createdon", "modifiedby", "modifiedon"),
        Criteria = new FilterExpression { Conditions = { new ConditionExpression("name", ConditionOperator.Like, "mcs_feishu%") } }
    };
    foreach (var r in All(q))
        Console.WriteLine($"  {r.GetAttributeValue<string>("name")}  createdby={r.GetAttributeValue<EntityReference>("createdby")?.Name} ({r.GetAttributeValue<DateTime?>("createdon")?.ToLocalTime():MM-dd}) modifiedby={r.GetAttributeValue<EntityReference>("modifiedby")?.Name} ({r.GetAttributeValue<DateTime?>("modifiedon")?.ToLocalTime():MM-dd HH:mm})  在McsWebResource={(mcsWrIds.Contains(r.Id) ? "✅" : "❌")}");
}

// 11. fca_proc 编号 Step 幽灵行复核：c33f9d6f-...-dd60（07-01 建）vs 08-04 重建的健康行
{
    Console.WriteLine($"\n【fca_proc 编号 Step 幽灵行复核】");
    var ghostId = Guid.Parse("c33f9d6f-eb74-f111-ab0f-7ced8db4dd60");
    try
    {
        var st = service.Retrieve("sdkmessageprocessingstep", ghostId, new ColumnSet("name", "statecode", "plugintypeid", "sdkmessagefilterid"));
        Console.WriteLine($"  dd60 行 Retrieve 成功: {st.GetAttributeValue<string>("name")} state={st.GetAttributeValue<OptionSetValue>("statecode")?.Value} filter={st.GetAttributeValue<EntityReference>("sdkmessagefilterid")?.Name}");
    }
    catch (Exception ex) { Console.WriteLine($"  dd60 行 Retrieve 失败（幽灵行）: {ex.Message.Split('\n')[0]}"); }
    var q = new QueryExpression("sdkmessageprocessingstep")
    {
        ColumnSet = new ColumnSet("name", "createdon", "statecode"),
        Criteria = new FilterExpression { Conditions = { new ConditionExpression("name", ConditionOperator.Equal, "EntityValidateCreateForGenerateNumber: Create of mcs_fca_proc") } }
    };
    foreach (var r in All(q))
        Console.WriteLine($"  同名行: {r.Id} created={r.GetAttributeValue<DateTime?>("createdon")?.ToLocalTime():yyyy-MM-dd} state={r.GetAttributeValue<OptionSetValue>("statecode")?.Value} 主清单={(manifestIds.Contains(r.Id) ? "✅" : "❌")} McsPlugin={(mcsPluginIds.Contains(r.Id) ? "✅" : "❌")}");
}

// 12. 停用组件复核（停用状态不随非托管包同步，须登记上线核对清单手动项）
{
    Console.WriteLine($"\n【我方主清单内停用 Step/AppAction 复核】");
    var q = new QueryExpression("sdkmessageprocessingstep")
    {
        ColumnSet = new ColumnSet("name", "statecode"),
        Criteria = new FilterExpression
        {
            Conditions =
            {
                new ConditionExpression("createdby", ConditionOperator.Equal, myId),
                new ConditionExpression("statecode", ConditionOperator.Equal, 1),
            }
        }
    };
    foreach (var r in All(q))
        Console.WriteLine($"  [停用Step] {r.GetAttributeValue<string>("name")}  {r.Id}  主清单={(manifestIds.Contains(r.Id) ? "✅" : "❌")} McsPlugin={(mcsPluginIds.Contains(r.Id) ? "✅" : "❌")}");
}

// 13. account / mcs_customer_tag 在主清单核对（#1837/#1838 标签改动涉及 account.mcs_creditgrade）
{
    Console.WriteLine($"\n【account / mcs_customer_tag 主清单核对】");
    var q = new QueryExpression("entity")
    {
        ColumnSet = new ColumnSet("name"),
        Criteria = new FilterExpression(LogicalOperator.Or)
        {
            Conditions =
            {
                new ConditionExpression("name", ConditionOperator.Equal, "account"),
                new ConditionExpression("name", ConditionOperator.Equal, "mcs_customer_tag"),
                new ConditionExpression("name", ConditionOperator.Equal, "mcs_customermasterdata"),
            }
        }
    };
    foreach (var r in All(q))
        Console.WriteLine($"  {r.GetAttributeValue<string>("name")}  主清单={(manifestIds.Contains(r.Id) ? "✅" : "❌")}  {r.Id}");
}

// 14. entity_20260727_peter 内 Account 及各实体的 rootcomponentbehavior（0=含全部子组件，2=仅所选子组件壳）
{
    Console.WriteLine($"\n【entity_20260727_peter 实体 rootcomponentbehavior】");
    var rel27Id = SolutionId("entity_20260727_peter");
    var q = new QueryExpression("solutioncomponent")
    {
        ColumnSet = new ColumnSet("objectid", "componenttype", "rootcomponentbehavior"),
        Criteria = new FilterExpression
        {
            Conditions =
            {
                new ConditionExpression("solutionid", ConditionOperator.Equal, rel27Id),
                new ConditionExpression("componenttype", ConditionOperator.Equal, 1),
            }
        }
    };
    foreach (var c in All(q))
    {
        var oid = c.GetAttributeValue<Guid>("objectid");
        string name;
        try { name = service.Retrieve("entity", oid, new ColumnSet("name")).GetAttributeValue<string>("name") ?? oid.ToString(); }
        catch { name = oid.ToString(); }
        var rb = c.Contains("rootcomponentbehavior") ? c.GetAttributeValue<OptionSetValue>("rootcomponentbehavior")?.Value.ToString() ?? "null" : "(列不存在)";
        Console.WriteLine($"  {name}  rootcomponentbehavior={rb}");
    }
}
