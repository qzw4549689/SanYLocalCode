// 发版增量扫描（只读，2026-08-18 发版专用）：dump 我方实体在环境中的元数据现状，
// 供与 805 基线包（历史 Solution zip）diff，得出「805 后新增/修改」的组件清单。
// 维度：实体显示名 / 字段（类型/必填/格式/标签/选项集/范围/长度）/ 窗体+视图（modifiedon）/ Ribbon / BPF / AppAction / CustomAPI / WebResource。
// 用法: dotnet run [环境URL] [输出json路径]
using D365ToolCommon.Connection;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using System.Text.Json;


// 探针模式: dotnet run attrid <实体> <字段> —— 对比 published/unpublished 元数据的 MetadataId
if (args.Length >= 3 && args[0] == "attrid")
{
    var svc = await D365ConnectionFactory.CreateAsync(D365ConnectionFactory.ResolveUrl());
    foreach (var asIf in new[] { false, true })
    {
        var r = (Microsoft.Xrm.Sdk.Messages.RetrieveAttributeResponse)svc.Execute(new Microsoft.Xrm.Sdk.Messages.RetrieveAttributeRequest
        { EntityLogicalName = args[1], LogicalName = args[2], RetrieveAsIfPublished = asIf });
        var meta = r.AttributeMetadata;
        Console.WriteLine($"RetrieveAsIfPublished={asIf}: MetadataId={meta.MetadataId} Type={meta.AttributeType}");
        if (meta is LookupAttributeMetadata lk)
            Console.WriteLine($"  Targets: {string.Join(",", lk.Targets ?? Array.Empty<string>())}");
    }
    return;
}


// 探针模式: dotnet run setview <savedqueryId> <fetchxml文件> <layoutxml文件> —— SDK Update 视图 fetchxml/layoutxml（Web API PATCH 对这两字段静默不落盘）
if (args.Length >= 4 && args[0] == "setview")
{
    var svc = await D365ConnectionFactory.CreateAsync(D365ConnectionFactory.ResolveUrl());
    var ent = new Entity("savedquery", Guid.Parse(args[1]));
    ent["fetchxml"] = File.ReadAllText(args[2]);
    ent["layoutxml"] = File.ReadAllText(args[3]);
    svc.Update(ent);
    var back = svc.Retrieve("savedquery", ent.Id, new ColumnSet("fetchxml", "layoutxml"));
    Console.WriteLine($"回读 fetchxml 长度={back.GetAttributeValue<string>("fetchxml")?.Length}，layoutxml 长度={back.GetAttributeValue<string>("layoutxml")?.Length}");
    return;
}


// 探针模式: dotnet run ribbonid <实体名> —— 打印 ribboncustomizationid / ribboncustomizationuniqueid
if (args.Length >= 2 && args[0] == "ribbonid")
{
    var svc = await D365ConnectionFactory.CreateAsync("https://dev1.crm5.dynamics.com");
    var rows = svc.RetrieveMultiple(new QueryExpression("ribboncustomization")
    {
        ColumnSet = new ColumnSet("entity", "ribboncustomizationuniqueid"),
        Criteria = new FilterExpression { Conditions = { new ConditionExpression("entity", ConditionOperator.Equal, args[1]) } }
    }).Entities;
    foreach (var r in rows)
        Console.WriteLine($"{args[1]}: id={r.Id} uniqueid={r.GetAttributeValue<Guid>("ribboncustomizationuniqueid")}");
    return;
}

var url = args.Length > 0 ? args[0] : "https://dev1.crm5.dynamics.com";
var outPath = args.Length > 1 ? args[1] : "dev1_meta.json";
var outDir = Path.GetDirectoryName(Path.GetFullPath(outPath))!;
Console.WriteLine($"目标环境: {url}");
var service = await D365ConnectionFactory.CreateAsync(url);

// 主清单 23 实体 + account（共享实体，仅 diff mcs_ 前缀字段）
var entityNames = new[]
{
    "mcs_coface_exchange_rate", "mcs_coface_financial_indicator", "mcs_coface_nace_mapping",
    "mcs_credit", "mcs_credititem_value", "mcs_creditmodelprocess", "mcs_credit_items",
    "mcs_credit_record", "mcs_credit_scoringcard", "mcs_customer_tag",
    "mcs_fca_mdlconfig", "mcs_fca_mdlversion", "mcs_fca_proc", "mcs_fca_quota",
    "mcs_fca_quotaapp", "mcs_fca_records", "mcs_fmprocess",
    "mcs_fsm_data", "mcs_fsm_detail_data", "mcs_fsm_resource",
    "mcs_trade_ptgrouptype", "mcs_trade_pttype", "mcs_trade_stpayterm",
    "account"
};

string? Label(Microsoft.Xrm.Sdk.Label? label, int lcid) =>
    label?.LocalizedLabels?.FirstOrDefault(l => l.LanguageCode == lcid)?.Label;

int? IntVal(object? v) => v switch
{
    null => null,
    OptionSetValue o => o.Value,
    int i => i,
    _ => int.TryParse(v.ToString(), out var p) ? p : null,
};

Dictionary<string, object?> AttrDump(AttributeMetadata a)
{
    var d = new Dictionary<string, object?>
    {
        ["name"] = a.LogicalName,
        ["metadataId"] = a.MetadataId?.ToString(),
        ["type"] = a.AttributeType?.ToString(),
        ["isLogical"] = a.IsLogical,
        ["required"] = a.RequiredLevel?.Value.ToString(),
        ["label1033"] = Label(a.DisplayName, 1033),
        ["label2052"] = Label(a.DisplayName, 2052),
    };
    switch (a)
    {
        case StringAttributeMetadata s:
            d["maxLength"] = s.MaxLength; d["format"] = s.FormatName?.Value.ToString() ?? s.Format?.ToString(); break;
        case MemoAttributeMetadata m:
            d["maxLength"] = m.MaxLength; break;
        case IntegerAttributeMetadata i:
            d["min"] = i.MinValue; d["max"] = i.MaxValue; break;
        case DecimalAttributeMetadata dec:
            d["min"] = dec.MinValue; d["max"] = dec.MaxValue; d["precision"] = dec.Precision; break;
        case MoneyAttributeMetadata mon:
            d["min"] = mon.MinValue; d["max"] = mon.MaxValue; d["precision"] = mon.Precision; break;
        case DoubleAttributeMetadata dbl:
            d["min"] = dbl.MinValue; d["max"] = dbl.MaxValue; d["precision"] = dbl.Precision; break;
        case DateTimeAttributeMetadata dt:
            d["dateTimeFormat"] = dt.Format?.ToString(); break;
        case LookupAttributeMetadata lk:
            d["targets"] = lk.Targets; break;
        case BooleanAttributeMetadata b:
            d["options"] = new[]
            {
                new Dictionary<string, object?> { ["value"] = 1, ["label1033"] = Label(b.OptionSet?.TrueOption?.Label, 1033), ["label2052"] = Label(b.OptionSet?.TrueOption?.Label, 2052) },
                new Dictionary<string, object?> { ["value"] = 0, ["label1033"] = Label(b.OptionSet?.FalseOption?.Label, 1033), ["label2052"] = Label(b.OptionSet?.FalseOption?.Label, 2052) },
            };
            break;
        case PicklistAttributeMetadata p:
            d["options"] = p.OptionSet?.Options?.Select(o => new Dictionary<string, object?>
                { ["value"] = o.Value, ["label1033"] = Label(o.Label, 1033), ["label2052"] = Label(o.Label, 2052) }).ToArray(); break;
        case StatusAttributeMetadata st:
            d["options"] = st.OptionSet?.Options?.Cast<StatusOptionMetadata>().Select(o => new Dictionary<string, object?>
                { ["value"] = o.Value, ["state"] = o.State, ["label1033"] = Label(o.Label, 1033), ["label2052"] = Label(o.Label, 2052) }).ToArray(); break;
        case StateAttributeMetadata sta:
            d["options"] = sta.OptionSet?.Options?.Select(o => new Dictionary<string, object?>
                { ["value"] = o.Value, ["label1033"] = Label(o.Label, 1033), ["label2052"] = Label(o.Label, 2052) }).ToArray(); break;
        case MultiSelectPicklistAttributeMetadata ms:
            d["options"] = ms.OptionSet?.Options?.Select(o => new Dictionary<string, object?>
                { ["value"] = o.Value, ["label1033"] = Label(o.Label, 1033), ["label2052"] = Label(o.Label, 2052) }).ToArray(); break;
    }
    return d;
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

var output = new Dictionary<string, object?>
{
    ["environment"] = url,
    ["dumpedAtUtc"] = DateTime.UtcNow.ToString("o"),
    ["entities"] = new Dictionary<string, object?>(),
};
var outEntities = (Dictionary<string, object?>)output["entities"]!;

foreach (var name in entityNames)
{
    Console.WriteLine($"扫描实体: {name}");
    var resp = (RetrieveEntityResponse)service.Execute(new RetrieveEntityRequest
    {
        LogicalName = name,
        EntityFilters = EntityFilters.Entity | EntityFilters.Attributes,
        RetrieveAsIfPublished = false
    });
    var meta = resp.EntityMetadata;

    // account 是共享实体，只保留 mcs_ 前缀字段，避免把他人字段带入 diff
    var attrs = meta.Attributes
        .Where(a => a.MetadataId != null && (name != "account" || (a.LogicalName?.StartsWith("mcs_") ?? false)))
        .Select(AttrDump).ToArray();

    // 窗体（systemform 无 modifiedon/createdon，dump formxml 原文供与基线 zip diff）
    var forms = All(new QueryExpression("systemform")
    {
        ColumnSet = new ColumnSet("name", "type", "formxml"),
        Criteria = new FilterExpression { Conditions = { new ConditionExpression("objecttypecode", ConditionOperator.Equal, name) } }
    }).Select(f =>
    {
        var d = new Dictionary<string, object?>
        {
            ["id"] = f.Id.ToString(),
            ["name"] = f.GetAttributeValue<string>("name"),
            ["type"] = IntVal(f.GetAttributeValue<object>("type")),
        };
        var fx = f.GetAttributeValue<string>("formxml");
        if (!string.IsNullOrEmpty(fx))
        {
            var rel = Path.Combine("forms", $"{name}_{f.Id}.xml");
            var abs = Path.Combine(outDir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(abs)!);
            File.WriteAllText(abs, fx);
            d["formXmlFile"] = rel;
        }
        return d;
    }).ToArray();

    // 视图（savedquery 有 modifiedon，同时 dump fetchxml/layoutxml 供内容 diff 排除假阳性）
    var views = All(new QueryExpression("savedquery")
    {
        ColumnSet = new ColumnSet("name", "querytype", "modifiedon", "fetchxml", "layoutxml"),
        Criteria = new FilterExpression { Conditions = { new ConditionExpression("returnedtypecode", ConditionOperator.Equal, name) } }
    }).Select(v =>
    {
        var d = new Dictionary<string, object?>
        {
            ["id"] = v.Id.ToString(),
            ["name"] = v.GetAttributeValue<string>("name"),
            ["querytype"] = IntVal(v.GetAttributeValue<object>("querytype")),
            ["modifiedonUtc"] = v.GetAttributeValue<DateTime?>("modifiedon")?.ToString("o"),
        };
        foreach (var (field, key) in new[] { ("fetchxml", "fetchXmlFile"), ("layoutxml", "layoutXmlFile") })
        {
            var content = v.GetAttributeValue<string>(field);
            if (!string.IsNullOrEmpty(content))
            {
                var rel = Path.Combine("views", $"{name}_{v.Id}_{field}.xml");
                var abs = Path.Combine(outDir, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(abs)!);
                File.WriteAllText(abs, content);
                d[key] = rel;
            }
        }
        return d;
    }).ToArray();

    outEntities[name] = new Dictionary<string, object?>
    {
        ["metadataId"] = meta.MetadataId?.ToString(),
        ["label1033"] = Label(meta.DisplayName, 1033),
        ["label2052"] = Label(meta.DisplayName, 2052),
        ["collectionLabel1033"] = Label(meta.DisplayCollectionName, 1033),
        ["collectionLabel2052"] = Label(meta.DisplayCollectionName, 2052),
        ["attributes"] = attrs,
        ["forms"] = forms,
        ["views"] = views,
    };
}

// Ribbon（ribboncustomization 若可查，按实体给出 modifiedon；查不了则留空由 zip diff 兜底）
var ribbon = new List<Dictionary<string, object?>>();
try
{
    ribbon = All(new QueryExpression("ribboncustomization")
    {
        ColumnSet = new ColumnSet("entity", "publishedon")
    }).Select(r => new Dictionary<string, object?>
    {
        ["id"] = r.Id.ToString(),
        ["entity"] = r.GetAttributeValue<string>("entity"),
        ["publishedonUtc"] = r.GetAttributeValue<DateTime?>("publishedon")?.ToString("o"),
    }).ToList();
    Console.WriteLine($"ribboncustomization 查询成功: {ribbon.Count} 条");
}
catch (Exception ex)
{
    Console.WriteLine($"⚠️ ribboncustomization 不可查（{ex.Message.Split('\n')[0]}），Ribbon 增量走 zip diff");
}
output["ribbonCustomizations"] = ribbon;

// BPF / 工作流
output["workflows"] = All(new QueryExpression("workflow")
{
    ColumnSet = new ColumnSet("name", "category", "statecode", "modifiedon", "uniquename"),
    Criteria = new FilterExpression { Conditions = { new ConditionExpression("category", ConditionOperator.In, 0, 4) } }
}).Select(w => new Dictionary<string, object?>
{
    ["id"] = w.Id.ToString(),
    ["name"] = w.GetAttributeValue<string>("name"),
    ["uniquename"] = w.GetAttributeValue<string>("uniquename"),
    ["category"] = IntVal(w.GetAttributeValue<object>("category")),
    ["statecode"] = IntVal(w.GetAttributeValue<object>("statecode")),
    ["modifiedonUtc"] = w.GetAttributeValue<DateTime?>("modifiedon")?.ToString("o"),
}).ToArray();

// App Action
try
{
    output["appActions"] = All(new QueryExpression("appaction")
    {
        ColumnSet = new ColumnSet("uniquename", "name", "statecode", "createdon", "modifiedon")
    }).Select(a => new Dictionary<string, object?>
    {
        ["id"] = a.Id.ToString(),
        ["uniquename"] = a.GetAttributeValue<string>("uniquename"),
        ["name"] = a.GetAttributeValue<string>("name"),
        ["statecode"] = IntVal(a.GetAttributeValue<object>("statecode")),
        ["createdonUtc"] = a.GetAttributeValue<DateTime?>("createdon")?.ToString("o"),
        ["modifiedonUtc"] = a.GetAttributeValue<DateTime?>("modifiedon")?.ToString("o"),
    }).ToArray();
}
catch (Exception ex) { Console.WriteLine($"⚠️ appaction 查询失败（{ex.Message.Split('\n')[0]}）"); }

// Custom API
output["customApis"] = All(new QueryExpression("customapi")
{
    ColumnSet = new ColumnSet("uniquename", "createdon", "modifiedon")
}).Select(c => new Dictionary<string, object?>
{
    ["id"] = c.Id.ToString(),
    ["uniquename"] = c.GetAttributeValue<string>("uniquename"),
    ["createdonUtc"] = c.GetAttributeValue<DateTime?>("createdon")?.ToString("o"),
    ["modifiedonUtc"] = c.GetAttributeValue<DateTime?>("modifiedon")?.ToString("o"),
}).ToArray();

// WebResource（mcs_/ms_ 前缀）
output["webResources"] = All(new QueryExpression("webresource")
{
    ColumnSet = new ColumnSet("name", "modifiedon"),
    Criteria = new FilterExpression
    {
        Conditions = { new ConditionExpression("name", ConditionOperator.BeginsWith, "mcs_") }
    }
}).Select(w => new Dictionary<string, object?>
{
    ["id"] = w.Id.ToString(),
    ["name"] = w.GetAttributeValue<string>("name"),
    ["modifiedonUtc"] = w.GetAttributeValue<DateTime?>("modifiedon")?.ToString("o"),
}).ToArray();

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
File.WriteAllText(outPath, JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"✅ 已输出: {Path.GetFullPath(outPath)}");
