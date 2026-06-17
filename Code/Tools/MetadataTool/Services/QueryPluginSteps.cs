using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace D365MetadataTool;

/// <summary>
/// 查询 D365 环境中的 Plugin Step
/// </summary>
public class QueryPluginSteps
{
    private readonly ServiceClient _service;

    public QueryPluginSteps(ServiceClient service)
    {
        _service = service;
    }

    /// <summary>
    /// 查询指定命名空间下的所有 Plugin Steps
    /// </summary>
    public void QueryStepsByNamespace(string namespacePrefix)
    {
        Console.WriteLine($"\n=== 查询命名空间下所有 Plugin Steps: {namespacePrefix} ===");
        Console.WriteLine($"环境: {_service.ConnectedOrgUriActual}\n");

        // 1. 查询所有 PluginType，按 typename 排序
        var ptQuery = new QueryExpression("plugintype")
        {
            ColumnSet = new ColumnSet("plugintypeid", "typename", "assemblyname"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("typename", ConditionOperator.BeginsWith, namespacePrefix)
                }
            },
            Orders = { new OrderExpression("typename", OrderType.Ascending) }
        };

        var ptResult = _service.RetrieveMultiple(ptQuery);
        if (ptResult.Entities.Count == 0)
        {
            Console.WriteLine($"❌ 未找到命名空间下的 Plugin Type: {namespacePrefix}");
            return;
        }

        Console.WriteLine($"找到 {ptResult.Entities.Count} 个 Plugin Type(s):\n");

        int totalSteps = 0;
        foreach (var pt in ptResult.Entities)
        {
            var typeId = pt.GetAttributeValue<Guid>("plugintypeid");
            var typeName = pt.GetAttributeValue<string>("typename");
            var assemblyName = pt.GetAttributeValue<string>("assemblyname");
            Console.WriteLine($"🔹 {typeName}");
            Console.WriteLine($"   Assembly: {assemblyName}");

            // 2. 查关联的 Steps
            var stepQuery = new QueryExpression("sdkmessageprocessingstep")
            {
                ColumnSet = new ColumnSet(
                    "sdkmessageprocessingstepid",
                    "name",
                    "stage",
                    "filteringattributes",
                    "asyncautodelete",
                    "mode",
                    "rank",
                    "statecode",
                    "statuscode",
                    "supporteddeployment",
                    "invocationsource",
                    "description",
                    "sdkmessageid",
                    "sdkmessagefilterid",
                    "eventhandler",
                    "ismanaged",
                    "iscustomizable",
                    "solutionid",
                    "componentstate",
                    "overwritetime"
                ),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("plugintypeid", ConditionOperator.Equal, typeId)
                    }
                },
                LinkEntities =
                {
                    new LinkEntity("sdkmessageprocessingstep", "sdkmessagefilter", "sdkmessagefilterid", "sdkmessagefilterid", JoinOperator.LeftOuter)
                    {
                        Columns = new ColumnSet("primaryobjecttypecode"),
                        EntityAlias = "filter"
                    },
                    new LinkEntity("sdkmessageprocessingstep", "sdkmessage", "sdkmessageid", "sdkmessageid", JoinOperator.LeftOuter)
                    {
                        Columns = new ColumnSet("name"),
                        EntityAlias = "msg"
                    }
                }
            };

            var stepResult = _service.RetrieveMultiple(stepQuery);
            if (stepResult.Entities.Count == 0)
            {
                Console.WriteLine("   ❌ 无 Step");
            }
            else
            {
                totalSteps += stepResult.Entities.Count;
                foreach (var step in stepResult.Entities)
                {
                    var stage = step.GetAttributeValue<OptionSetValue>("stage")?.Value;
                    var stageName = stage switch { 10 => "PreValidation", 20 => "PreOperation", 40 => "PostOperation", _ => $"Stage_{stage}" };
                    var filteringAttrs = step.GetAttributeValue<string>("filteringattributes") ?? "(无)";
                    var mode = step.GetAttributeValue<OptionSetValue>("mode")?.Value == 1 ? "Async" : "Sync";
                    var state = step.GetAttributeValue<OptionSetValue>("statecode")?.Value == 0 ? "✅ 启用" : "❌ 禁用";
                    var entity = step.GetAttributeValue<AliasedValue>("filter.primaryobjecttypecode")?.Value?.ToString() ?? "N/A";
                    var message = step.GetAttributeValue<AliasedValue>("msg.name")?.Value?.ToString() ?? "N/A";

                    Console.WriteLine($"   📌 {message} / {entity} / {stageName} / {mode} / Filter:[{filteringAttrs}] / {state}");
                }
            }
            Console.WriteLine();
        }

        Console.WriteLine($"{'='.ToString().PadRight(80, '=')}");
        Console.WriteLine($"总计: {ptResult.Entities.Count} 个 Plugin Types, {totalSteps} 个 Steps");
        Console.WriteLine($"{'='.ToString().PadRight(80, '=')}");
    }

    /// <summary>
    /// 查询指定 Plugin Type 的所有 Step
    /// </summary>
    public void QueryStepsByPluginName(string pluginTypeName)
    {
        Console.WriteLine($"\n=== 查询 Plugin Steps: {pluginTypeName} ===");
        Console.WriteLine($"环境: {_service.ConnectedOrgUriActual}\n");

        // 1. 先查 PluginType
        var ptQuery = new QueryExpression("plugintype")
        {
            ColumnSet = new ColumnSet("plugintypeid", "typename", "assemblyname"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("typename", ConditionOperator.Like, $"%{pluginTypeName}%")
                }
            }
        };

        var ptResult = _service.RetrieveMultiple(ptQuery);
        if (ptResult.Entities.Count == 0)
        {
            Console.WriteLine($"❌ 未找到 Plugin Type: {pluginTypeName}");
            return;
        }

        foreach (var pt in ptResult.Entities)
        {
            var typeId = pt.GetAttributeValue<Guid>("plugintypeid");
            var typeName = pt.GetAttributeValue<string>("typename");
            var assemblyName = pt.GetAttributeValue<string>("assemblyname");
            Console.WriteLine($"PluginType: {typeName}");
            Console.WriteLine($"Assembly:   {assemblyName}");
            Console.WriteLine($"TypeId:     {typeId}");

            // 查询 PluginType 完整属性
            var ptDetailQuery = new QueryExpression("plugintype")
            {
                ColumnSet = new ColumnSet(
                    "name", "friendlyname", "typename", "assemblyname", "workflowactivitygroupname",
                    "ismanaged", "componentstate", "solutionid", "major", "minor",
                    "versionnumber", "description", "pluginassemblyid"
                ),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("plugintypeid", ConditionOperator.Equal, typeId) }
                }
            };
            var ptDetailResult = _service.RetrieveMultiple(ptDetailQuery);
            if (ptDetailResult.Entities.Count > 0)
            {
                var ptDetail = ptDetailResult.Entities[0];
                Console.WriteLine("PluginType Detail:");
                foreach (var attr in ptDetail.Attributes)
                {
                    if (attr.Value != null)
                    {
                        Console.WriteLine($"   {attr.Key}: {attr.Value} ({attr.Value.GetType().Name})");
                    }
                }
            }
            Console.WriteLine(new string('-', 80));

            // 2. 查关联的 Steps
            var stepQuery = new QueryExpression("sdkmessageprocessingstep")
            {
                ColumnSet = new ColumnSet(
                    "sdkmessageprocessingstepid",
                    "name",
                    "stage",
                    "filteringattributes",
                    "asyncautodelete",
                    "mode",
                    "rank",
                    "statecode",
                    "statuscode",
                    "supporteddeployment",
                    "invocationsource",
                    "description",
                    "sdkmessageid",
                    "sdkmessagefilterid",
                    "eventhandler",
                    "ismanaged",
                    "iscustomizable",
                    "solutionid",
                    "componentstate",
                    "overwritetime"
                ),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("plugintypeid", ConditionOperator.Equal, typeId)
                    }
                },
                LinkEntities =
                {
                    new LinkEntity("sdkmessageprocessingstep", "sdkmessagefilter", "sdkmessagefilterid", "sdkmessagefilterid", JoinOperator.LeftOuter)
                    {
                        Columns = new ColumnSet("primaryobjecttypecode"),
                        EntityAlias = "filter"
                    },
                    new LinkEntity("sdkmessageprocessingstep", "sdkmessage", "sdkmessageid", "sdkmessageid", JoinOperator.LeftOuter)
                    {
                        Columns = new ColumnSet("name"),
                        EntityAlias = "msg"
                    }
                }
            };

            var stepResult = _service.RetrieveMultiple(stepQuery);
            if (stepResult.Entities.Count == 0)
            {
                Console.WriteLine("  ❌ 该 Plugin Type 下没有注册任何 Step");
            }
            else
            {
                Console.WriteLine($"  找到 {stepResult.Entities.Count} 个 Step(s):\n");
                foreach (var step in stepResult.Entities)
                {
                    var stepId = step.GetAttributeValue<Guid>("sdkmessageprocessingstepid");
                    var name = step.GetAttributeValue<string>("name");
                    var stage = step.GetAttributeValue<OptionSetValue>("stage")?.Value;
                    var stageName = stage switch { 10 => "PreValidation", 20 => "PreOperation", 40 => "PostOperation", _ => $"Stage_{stage}" };
                    var filteringAttrs = step.GetAttributeValue<string>("filteringattributes") ?? "(无)";
                    var mode = step.GetAttributeValue<OptionSetValue>("mode")?.Value == 1 ? "Async" : "Sync";
                    var state = step.GetAttributeValue<OptionSetValue>("statecode")?.Value == 0 ? "✅ 启用" : "❌ 禁用";
                    var entity = step.GetAttributeValue<AliasedValue>("filter.primaryobjecttypecode")?.Value?.ToString() ?? "N/A";
                    var message = step.GetAttributeValue<AliasedValue>("msg.name")?.Value?.ToString() ?? "N/A";

                    var supportedDeployment = step.GetAttributeValue<OptionSetValue>("supporteddeployment")?.Value ?? -1;
                    var invocationSource = step.GetAttributeValue<OptionSetValue>("invocationsource")?.Value ?? -1;
                    var description = step.GetAttributeValue<string>("description") ?? "";
                    var eventHandler = step.GetAttributeValue<EntityReference>("eventhandler")?.Id.ToString() ?? "N/A";
                    var msgId = step.GetAttributeValue<EntityReference>("sdkmessageid")?.Id.ToString() ?? "N/A";
                    var filterId = step.GetAttributeValue<EntityReference>("sdkmessagefilterid")?.Id.ToString() ?? "N/A";

                    Console.WriteLine($"  📌 {name}");
                    Console.WriteLine($"     StepId:    {stepId}");
                    Console.WriteLine($"     Entity:    {entity}");
                    Console.WriteLine($"     Message:   {message}");
                    Console.WriteLine($"     Stage:     {stageName}");
                    Console.WriteLine($"     Mode:      {mode}");
                    Console.WriteLine($"     Filter:    {filteringAttrs}");
                    Console.WriteLine($"     State:     {state}");
                    Console.WriteLine($"     SupportedDeployment: {supportedDeployment}");
                    Console.WriteLine($"     InvocationSource:    {invocationSource}");
                    Console.WriteLine($"     Description:         {description}");
                    Console.WriteLine($"     EventHandler:        {eventHandler}");
                    Console.WriteLine($"     SdkMessageId:        {msgId}");
                    Console.WriteLine($"     SdkMessageFilterId:  {filterId}");
                    var isManaged = step.GetAttributeValue<bool?>("ismanaged");
                    var isCustomizable = step.GetAttributeValue<BooleanManagedProperty>("iscustomizable")?.Value;
                    var componentState = step.GetAttributeValue<OptionSetValue>("componentstate")?.Value;
                    var stepSolutionId = step.GetAttributeValue<Guid?>("solutionid");
                    Console.WriteLine($"     IsManaged:           {isManaged}");
                    Console.WriteLine($"     IsCustomizable:      {isCustomizable}");
                    Console.WriteLine($"     ComponentState:      {componentState}");
                    Console.WriteLine($"     SolutionId:          {stepSolutionId}");
                    Console.WriteLine("     --- Raw Attributes ---");
                    foreach (var attr in step.Attributes)
                    {
                        if (attr.Value != null)
                        {
                            Console.WriteLine($"       {attr.Key}: {attr.Value} ({attr.Value.GetType().Name})");
                        }
                    }
                    Console.WriteLine();
                }
            }
            Console.WriteLine(new string('=', 80));
        }
    }

    /// <summary>
    /// 列出所有包含 "Bpp" 或 "Credit" 的 Plugin Steps
    /// </summary>
    public void ListAllCreditBppSteps()
    {
        Console.WriteLine("\n=== 列出所有 Credit/BPP 相关 Plugin Steps ===\n");

        var keywords = new[] { "Bpp", "Credit", "Coface", "Scoring" };

        foreach (var kw in keywords)
        {
            QueryStepsByPluginName(kw);
        }
    }

    /// <summary>
    /// 查询指定 Assembly 的版本号
    /// </summary>
    public void QueryAssemblyVersion(string assemblyName)
    {
        Console.WriteLine($"\n=== 查询 Assembly 版本: {assemblyName} ===");
        Console.WriteLine($"环境: {_service.ConnectedOrgUriActual}\n");

        var query = new QueryExpression("pluginassembly")
        {
            ColumnSet = new ColumnSet("pluginassemblyid", "name", "version", "sourcetype", "isolationmode", "createdon", "modifiedon"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("name", ConditionOperator.Like, $"%{assemblyName}%")
                }
            }
        };

        var result = _service.RetrieveMultiple(query);
        if (result.Entities.Count == 0)
        {
            Console.WriteLine($"❌ 未找到 Assembly: {assemblyName}");
            return;
        }

        foreach (var asm in result.Entities)
        {
            var id = asm.GetAttributeValue<Guid>("pluginassemblyid");
            var name = asm.GetAttributeValue<string>("name");
            var version = asm.GetAttributeValue<string>("version");
            var sourceType = asm.GetAttributeValue<OptionSetValue>("sourcetype")?.Value == 0 ? "Database" : "Disk";
            var isolationMode = asm.GetAttributeValue<OptionSetValue>("isolationmode")?.Value == 2 ? "Sandbox" : "None";
            var createdOn = asm.GetAttributeValue<DateTime>("createdon");
            var modifiedOn = asm.GetAttributeValue<DateTime>("modifiedon");

            Console.WriteLine($"📦 {name}");
            Console.WriteLine($"   AssemblyId:  {id}");
            Console.WriteLine($"   Version:     {version}");
            Console.WriteLine($"   SourceType:  {sourceType}");
            Console.WriteLine($"   Isolation:   {isolationMode}");
            Console.WriteLine($"   CreatedOn:   {createdOn:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"   ModifiedOn:  {modifiedOn:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine();
        }
    }
}
