using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using System.Threading.Tasks;
using D365MetadataTool;
using D365ToolCommon.Connection;

class Program
{
    // DEV环境默认URL
    static string GetDefaultUrl() => Environment.GetEnvironmentVariable("D365_URL") ?? "https://dev1.crm5.dynamics.com";

    static async Task Main(string[] args)
    {
        Console.WriteLine("=== D365 实体管理工具 ===");
        var url = Environment.GetEnvironmentVariable("D365_URL") ?? "https://dev1.crm5.dynamics.com";
        Console.WriteLine($"目标环境: {url}");
        Console.WriteLine("用法:");
        Console.WriteLine("  dotnet run create <实体定义文件.json>   - 从JSON文件创建实体和字段");
        Console.WriteLine("  dotnet run check <解决方案名>           - 检查解决方案中的实体");
        Console.WriteLine("  dotnet run add <实体名> <解决方案名>    - 添加实体到解决方案");
        Console.WriteLine("  dotnet run remove <实体名> <解决方案名> - 从解决方案移除实体");
        Console.WriteLine("  dotnet run publish [实体名]             - 发布所有或指定实体");
        Console.WriteLine("  dotnet run delete-field <实体名> <字段名> - 删除字段");
        Console.WriteLine("  dotnet run list-fields <实体名>         - 列出实体所有字段");
        Console.WriteLine("  dotnet run update-entity-displayname <实体名> <显示名> - 更新实体显示名称（多语言）");
        Console.WriteLine("  dotnet run update-field-displayname <实体名> <字段名> <显示名> - 更新字段显示名称（多语言）");
        Console.WriteLine("  dotnet run get-entity-displayname <实体名> - 查询实体显示名称（诊断）");
        Console.WriteLine("  dotnet run export <解决方案名> <路径>    - 导出解决方案为 ZIP");
        Console.WriteLine("  dotnet run create-credit-items          - 创建评分项目测试数据(22条)");
        Console.WriteLine("  dotnet run create-qualitative-enums     - 创建定性枚举值测试数据(30条)");
        Console.WriteLine("  dotnet run update-credit-record <scoreid> [status] - 更新信用评估记录状态(默认→12)");
        Console.WriteLine("  dotnet run query-plugin-steps <类名>    - 查询已注册的 Plugin Steps");
        Console.WriteLine("  dotnet run query-plugin-namespace <前缀> - 查询命名空间下所有 Plugin Steps");
        Console.WriteLine("  dotnet run fix-dev-scoring-cards        - 重建DEV评分卡配置（补全mcs_itemid/mcs_datatype/mcs_cardname）");
        Console.WriteLine("  dotnet run export-webresource <名称> <路径> - 导出 WebResource 内容");
        Console.WriteLine("  dotnet run update-webresource <名称> <文件路径> - 更新 WebResource 内容");
        Console.WriteLine("  dotnet run list-form-webresources <实体> - 列出实体主窗体引用的 JS WebResource");
        Console.WriteLine("  dotnet run add-webresource-to-solution <WebResource名称> <解决方案唯一名> - 将 WebResource 加入解决方案");
        Console.WriteLine("  dotnet run test-common                  - 隔离测试 D365ToolCommon 共享库（自动创建/删除测试实体）");
        Console.WriteLine("  dotnet run cleanup-test-common          - 仅清理 D365ToolCommon 测试实体");
        Console.WriteLine();

        if (args.Length < 1)
        {
            ShowHelp();
            return;
        }

        string command = args[0].ToLower();

        try
        {
            using (ServiceClient service = await D365ConnectionFactory.CreateAsync(url))
            {
                if (!service.IsReady)
                {
                    Console.WriteLine("连接失败!");
                    return;
                }

                Console.WriteLine($"连接成功! 用户: {service.OAuthUserId}\n");

                var manager = new EntityManager(service);

                switch (command)
                {
                    case "create":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("请指定JSON定义文件路径");
                            return;
                        }
                        CreateFromJson(manager, args[1]);
                        break;

                    case "check":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("请指定解决方案名称");
                            return;
                        }
                        CheckSolution(manager, args[1]);
                        break;

                    case "add":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run add <实体名> <解决方案名>");
                            return;
                        }
                        manager.AddEntityToSolution(args[1], args[2]);
                        break;

                    case "remove":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run remove <实体名> <解决方案名>");
                            return;
                        }
                        manager.RemoveEntityFromSolution(args[1], args[2]);
                        break;

                    case "publish":
                        if (args.Length >= 2)
                        {
                            manager.PublishEntity(args[1]);
                        }
                        else
                        {
                            manager.PublishAll();
                        }
                        break;

                    case "publish-webresource":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run publish-webresource <WebResource名称1> [WebResource名称2] ...");
                            return;
                        }
                        var resourceNames = args.Skip(1).ToArray();
                        var resourceXml = string.Join("", resourceNames.Select(n => $"<webresource>{n}</webresource>"));
                        var pubRequest = new Microsoft.Crm.Sdk.Messages.PublishXmlRequest
                        {
                            ParameterXml = $"<importexportxml><webresources>{resourceXml}</webresources></importexportxml>"
                        };
                        service.Execute(pubRequest);
                        Console.WriteLine($"✅ WebResource 发布成功: {string.Join(", ", resourceNames)}");
                        break;

                    case "publish-profile":
                        // 自动重试发布画像 WebResource（带阻塞检测）
                        PublishProfileWebResources.Run(service);
                        break;

                    case "query-account-masterdata":
                        string? targetAccountNumber = args.Length >= 2 ? args[1] : null;
                        QueryAccountMasterData(service, targetAccountNumber);
                        break;

                    case "query-optionset":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run query-optionset <实体名> <字段名>");
                            Console.WriteLine("  示例: dotnet run query-optionset account mcs_dealerrank");
                            return;
                        }
                        QueryOptionSet(service, args[1], args[2]);
                        break;

                    case "update-credit-record":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run update-credit-record <scoreid> [status]");
                            Console.WriteLine("  示例: dotnet run update-credit-record SCO202606080001");
                            Console.WriteLine("  示例: dotnet run update-credit-record SCO202606080001 12");
                            return;
                        }
                        string targetScoreId = args[1];
                        int targetStatus = args.Length >= 3 && int.TryParse(args[2], out int s) ? s : 12;
                        UpdateCreditRecordStatus(service, targetScoreId, targetStatus);
                        break;

                    case "query-tags":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run query-tags <scoreid>");
                            return;
                        }
                        QueryCreditRecordTags(service, args[1]);
                        break;

                    case "query-items":
                        QueryCreditItems(service);
                        break;

                    case "query-enums":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run query-enums <评分项目编码>");
                            Console.WriteLine("  示例: dotnet run query-enums SectorRisk");
                            return;
                        }
                        QueryEnums(service, args[1]);
                        break;

                    case "mock-scores":
                        MockScores(service);
                        break;

                    case "add-sector-card":
                        AddSectorScoringCard(service);
                        break;

                    case "query-system-config":
                        QuerySystemConfigurations(service);
                        break;

                    case "upsert-system-config":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run upsert-system-config <配置名> <JSON内容> [描述]");
                            Console.WriteLine("  示例: dotnet run upsert-system-config CofaceApiConfig '{\"baseUrl\":\"...\"}' \"Coface API配置\"");
                            return;
                        }
                        UpsertSystemConfiguration(service, args[1], args[2], args.Length >= 4 ? args[3] : "");
                        break;

                    case "test-uploader-init":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run test-uploader-init <entityName> <entityId>");
                            Console.WriteLine("  示例: dotnet run test-uploader-init mcs_customer_file 00000000-0000-0000-0000-000000000000");
                            return;
                        }
                        TestUploadFileInitInfo(service, args[1], args[2]);
                        break;

                    case "query-forms-by-name":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run query-forms-by-name <名称片段>");
                            return;
                        }
                        QueryFormsByName(service, args[1]);
                        break;

                    case "query-entities":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run query-entities <名称片段>");
                            return;
                        }
                        QueryEntitiesByName(service, args[1]);
                        break;

                    case "query-system-config-like":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run query-system-config-like <名称片段>");
                            return;
                        }
                        QuerySystemConfigurationsLike(service, args[1]);
                        break;

                    case "query-coface-indicators":
                        QueryCofaceFinancialIndicators(service, args.Length >= 2 ? args[1] : null);
                        break;

                    case "query-credit-records":
                        QueryRecentCreditRecords(service, args.Length >= 2 ? args[1] : null);
                        break;

                    case "query-urba-json":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run query-urba-json <scoreid>");
                            return;
                        }
                        QueryUrbaJson(service, args[1]);
                        break;

                    case "query-customer-tags":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run query-customer-tags <scoreid>");
                            return;
                        }
                        QueryCustomerTags(service, args[1]);
                        break;

                    case "query-credit-items":
                        QueryCreditItems(service);
                        break;

                    case "query-bppapply":
                        QueryRecentBppApply(service);
                        break;

                    case "query-form-fields":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run query-form-fields <实体名> [字段显示名]");
                            Console.WriteLine("  示例: dotnet run query-form-fields mcs_credit_record BPP工作流ID");
                            return;
                        }
                        QueryFormFields(service, args[1], args.Length >= 3 ? args[2] : null);
                        break;

                    case "query-squeue":
                        QuerySQueue(service, args.Length >= 2 ? args[1] : "Common");
                        break;

                    case "query-smessage":
                        QuerySMessage(service, args.Length >= 2 ? args[1] : "BPPStartWorkflow");
                        break;

                    case "find-smessage-by-entity":
                        if (args.Length < 2) { Console.WriteLine("用法: find-smessage-by-entity <entityId>"); return; }
                        FindSMessageByEntity(service, args[1]);
                        break;

                    case "list-assemblies":
                        ListPluginAssemblies(service, args.Length >= 2 ? args[1] : "");
                        break;

                    case "query-trace-log":
                        QueryPluginTraceLog(service, args.Length >= 2 ? args[1] : "BppStartApis");
                        break;

                    case "query-steps-by-entity":
                        if (args.Length < 2) { Console.WriteLine("用法: query-steps-by-entity <entityName>"); return; }
                        QueryStepsByEntity(service, args[1]);
                        break;

                    case "query-plugin-steps":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run query-plugin-steps <Plugin类名(部分匹配)>");
                            Console.WriteLine("  示例: dotnet run query-plugin-steps BppCallbackPlugin");
                            Console.WriteLine("  示例: dotnet run query-plugin-steps Bpp");
                            return;
                        }
                        var querySteps = new QueryPluginSteps(service);
                        querySteps.QueryStepsByPluginName(args[1]);
                        break;

                    case "query-plugin-namespace":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run query-plugin-namespace <命名空间前缀>");
                            Console.WriteLine("  示例: dotnet run query-plugin-namespace SanyD365.D365Extension.Sales.Plugins");
                            return;
                        }
                        var queryNs = new QueryPluginSteps(service);
                        queryNs.QueryStepsByNamespace(args[1]);
                        break;

                    case "export-form":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run export-form <实体名> <输出路径>");
                            return;
                        }
                        ExportFormXml(service, args[1], args[2]);
                        break;

                    case "update-form-xml":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run update-form-xml <实体名> <XML文件路径>");
                            return;
                        }
                        UpdateFormXmlFromFile(service, args[1], args[2]);
                        break;

                    case "add-uploader-tab":
                        if (args.Length < 4)
                        {
                            Console.WriteLine("用法: dotnet run add-uploader-tab <实体名> <tab名称> <WebResource URL>");
                            Console.WriteLine("  示例: dotnet run add-uploader-tab mcs_credit_record Attachments mcs_/CommonCore/Html/Uploader.html");
                            return;
                        }
                        AddUploaderTab(service, args[1], args[2], args[3]);
                        break;

                    case "unregister-plugin":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run unregister-plugin <assembly名>");
                            Console.WriteLine("  示例: dotnet run unregister-plugin SanyD365.Plugins.CofaceIntegration");
                            return;
                        }
                        UnregisterPlugin(service, args[1]);
                        break;

                    case "query-assembly-version":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run query-assembly-version <Assembly名称(部分匹配)>");
                            Console.WriteLine("  示例: dotnet run query-assembly-version SanyD365.D365Extension.Sales");
                            return;
                        }
                        var queryAsm = new QueryPluginSteps(service);
                        queryAsm.QueryAssemblyVersion(args[1]);
                        break;

                    case "check-assembly-in-solution":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run check-assembly-in-solution <Assembly名称> <Solution唯一名> [PluginType过滤]");
                            Console.WriteLine("  示例: dotnet run check-assembly-in-solution SanyD365.D365Extension.Sales McsPlugin Coface");
                            return;
                        }
                        CheckAssemblyInSolution(service, args[1], args[2], args.Length >= 4 ? args[3] : null);
                        break;

                    case "add-step-to-solution":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run add-step-to-solution <StepId或Step名称> <Solution唯一名>");
                            Console.WriteLine("  示例: dotnet run add-step-to-solution b13fa909-2a69-f111-ab0c-6045bd1c0925 McsPlugin");
                            return;
                        }
                        AddPluginStepToSolution(service, args[1], args[2]);
                        break;

                    case "query-custom-api":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run query-custom-api <CustomAPI唯一名>");
                            Console.WriteLine("  示例: dotnet run query-custom-api mcs_bppstartapi");
                            return;
                        }
                        QueryCustomApiParameters(service, args[1]);
                        break;

                    case "check-webresource":
                        // 查询 WebResource 是否存在
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run check-webresource <WebResource名称>");
                            return;
                        }
                        var wrQuery = new Microsoft.Xrm.Sdk.Query.QueryExpression("webresource")
                        {
                            ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("webresourceid", "name", "webresourcetype", "displayname", "ismanaged"),
                            Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression()
                            {
                                Conditions = { new Microsoft.Xrm.Sdk.Query.ConditionExpression("name", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, args[1]) }
                            }
                        };
                        var wrResult = service.RetrieveMultiple(wrQuery);
                        if (wrResult.Entities.Count > 0)
                        {
                            var wr = wrResult.Entities[0];
                            Console.WriteLine($"✅ 找到 WebResource:");
                            Console.WriteLine($"   ID: {wr.Id}");
                            Console.WriteLine($"   名称: {wr["name"]}");
                            Console.WriteLine($"   类型: {wr["webresourcetype"]}");
                            Console.WriteLine($"   显示名: {wr["displayname"]}");
                            Console.WriteLine($"   托管: {wr["ismanaged"]}");
                        }
                        else
                        {
                            Console.WriteLine($"❌ 未找到 WebResource: {args[1]}");
                        }
                        break;

                    case "export-webresource":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run export-webresource <WebResource名称> <输出文件路径>");
                            return;
                        }
                        ExportWebResource(service, args[1], args[2]);
                        break;

                    case "update-webresource":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run update-webresource <WebResource名称> <文件路径>");
                            return;
                        }
                        UpdateWebResource(service, args[1], args[2]);
                        break;

                    case "list-form-webresources":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run list-form-webresources <实体名>");
                            return;
                        }
                        ListFormWebResources(service, args[1]);
                        break;

                    case "add-webresource-to-solution":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run add-webresource-to-solution <WebResource名称> <解决方案唯一名>");
                            return;
                        }
                        AddWebResourceToSolution(service, args[1], args[2]);
                        break;

                    case "delete-field":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run delete-field <实体名> <字段名>");
                            return;
                        }
                        manager.DeleteField(args[1], args[2]);
                        break;

                    case "remove-field-from-views":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run remove-field-from-views <实体名> <字段名>");
                            return;
                        }
                        manager.RemoveFieldFromViews(args[1], args[2]);
                        break;

                    case "list-fields":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run list-fields <实体名>");
                            return;
                        }
                        ListFields(manager, args[1]);
                        break;

                    case "update-entity-displayname":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run update-entity-displayname <实体名> <显示名>");
                            return;
                        }
                        manager.UpdateEntityDisplayName(args[1], args[2]);
                        break;

                    case "update-field-displayname":
                        if (args.Length < 4)
                        {
                            Console.WriteLine("用法: dotnet run update-field-displayname <实体名> <字段名> <显示名>");
                            return;
                        }
                        manager.UpdateAttributeDisplayName(args[1], args[2], args[3]);
                        break;

                    case "get-entity-displayname":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run get-entity-displayname <实体名>");
                            return;
                        }
                        manager.PrintEntityDisplayName(args[1]);
                        break;

                    case "export":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run export <解决方案名> <输出路径>");
                            return;
                        }
                        manager.ExportSolution(args[1], args[2]);
                        break;

                    case "update-options":
                        if (args.Length < 4)
                        {
                            Console.WriteLine("用法: dotnet run update-options <实体名> <字段名> <选项JSON>");
                            Console.WriteLine("示例: dotnet run update-options mcs_credit_scoringcard mcs_categoryid '{\"SA级老客户\":1,\"SA级新客户\":2}'");
                            return;
                        }
                        var opts = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>(args[3]);
                        manager.UpdatePicklistOptions(args[1], args[2], opts ?? new Dictionary<string, int>());
                        break;

                    case "rename-options":
                        if (args.Length < 4)
                        {
                            Console.WriteLine("用法: dotnet run rename-options <实体名> <字段名> <值-标签JSON>");
                            Console.WriteLine("示例: dotnet run rename-options mcs_credit_items mcs_group '{\"100000000\":\"客户实力\",\"100000001\":\"客户财务\"}'");
                            return;
                        }
                        var renameOpts = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(args[3]);
                        var valueToLabel = (renameOpts ?? new Dictionary<string, string>())
                            .ToDictionary(kv => int.Parse(kv.Key), kv => kv.Value);
                        manager.UpdateOptionLabels(args[1], args[2], valueToLabel);
                        break;

                    case "update-form":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run update-form <实体名>");
                            return;
                        }
                        UpdateForm(manager, args[1]);
                        break;

                    case "check-form":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run check-form <实体名>");
                            return;
                        }
                        manager.CheckFormFields(args[1]);
                        break;

                    case "export-formxml":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run export-formxml <实体名> <输出路径>");
                            return;
                        }
                        manager.ExportFormXml(args[1], args[2]);
                        break;

                    case "check-view":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run check-view <实体名>");
                            return;
                        }
                        manager.CheckViews(args[1]);
                        break;

                    case "update-view":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run update-view <实体名>");
                            return;
                        }
                        UpdateView(manager, args[1]);
                        break;

                    case "update-lookup-view":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run update-lookup-view <实体名>");
                            return;
                        }
                        UpdateLookupView(manager, args[1]);
                        break;

                    case "export-lookup-view":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run export-lookup-view <实体名> <输出路径>");
                            return;
                        }
                        manager.ExportLookupViewFetchXml(args[1], args[2]);
                        break;

                    case "deploy-js":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run deploy-js <JS文件路径>");
                            return;
                        }
                        var jsPath = args[1];
                        var jsName = System.IO.Path.GetFileName(jsPath);
                        manager.DeployWebResource(jsName, "评分卡配置表-表单逻辑", jsPath, "entity_20260603_peter");
                        break;

                    case "bind-js":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run bind-js <JS文件名> [实体名]");
                            return;
                        }
                        string bindEntity = args.Length >= 3 ? args[2] : "mcs_credit_scoringcard";
                        manager.BindJsToForm(bindEntity, args[1], "Information");
                        break;

                    case "register-plugin":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run register-plugin <DLL路径> <类名> [实体名]");
                            return;
                        }
                        string entityForPlugin = args.Length >= 4 ? args[3] : "mcs_credit_scoringcard";
                        manager.RegisterPlugin(args[1], args[2], entityForPlugin, "Create", 20, 0);
                        break;

                    case "register-plugin-update":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run register-plugin-update <DLL路径> <类名> [实体名] [筛选属性]");
                            return;
                        }
                        string entityForUpdate = args.Length >= 4 ? args[3] : "mcs_credit_record";
                        string filteringAttr = args.Length >= 5 ? args[4] : null;
                        manager.RegisterPluginWithFilter(args[1], args[2], entityForUpdate, "Update", 20, 0, filteringAttr);
                        break;

                    case "register-plugin-advanced":
                        if (args.Length < 6)
                        {
                            Console.WriteLine("用法: dotnet run register-plugin-advanced <DLL路径> <类名> <实体名> <消息名> <阶段> [筛选属性]");
                            Console.WriteLine("  阶段: 10=PreValidation, 20=PreOperation, 40=PostOperation");
                            return;
                        }
                        string advDllPath = args[1];
                        string advClassName = args[2];
                        string advEntity = args[3];
                        string advMessage = args[4];
                        int advStage = int.Parse(args[5]);
                        string advFilter = args.Length >= 7 ? args[6] : null;
                        if (!string.IsNullOrEmpty(advFilter))
                        {
                            manager.RegisterPluginWithFilter(advDllPath, advClassName, advEntity, advMessage, advStage, 0, advFilter);
                        }
                        else
                        {
                            manager.RegisterPlugin(advDllPath, advClassName, advEntity, advMessage, advStage, 0);
                        }
                        break;

                    case "register-step-only":
                        if (args.Length < 5)
                        {
                            Console.WriteLine("用法: dotnet run register-step-only <PluginType类名> <实体名> <消息名> <阶段> [筛选属性]");
                            Console.WriteLine("  阶段: 10=PreValidation, 20=PreOperation, 40=PostOperation");
                            return;
                        }
                        string stepClassName = args[1];
                        string stepEntityName = args[2];
                        string stepMessageName = args[3];
                        int stepStageValue = int.Parse(args[4]);
                        string stepFilterAttr = args.Length >= 6 ? args[5] : null;
                        
                        // 1. 查 PluginType ID
                        var ptQuery = new QueryExpression("plugintype")
                        {
                            ColumnSet = new ColumnSet("plugintypeid"),
                            Criteria = new FilterExpression { Conditions = { new ConditionExpression("typename", ConditionOperator.Equal, stepClassName) } }
                        };
                        var ptResult = service.RetrieveMultiple(ptQuery);
                        if (ptResult.Entities.Count == 0) { Console.WriteLine($"错误: 找不到 PluginType: {stepClassName}"); return; }
                        Guid stepPluginTypeId = ptResult.Entities[0].Id;
                        
                        // 2. 查 SdkMessage ID
                        var msgQuery2 = new QueryExpression("sdkmessage")
                        {
                            ColumnSet = new ColumnSet("sdkmessageid"),
                            Criteria = new FilterExpression { Conditions = { new ConditionExpression("name", ConditionOperator.Equal, stepMessageName) } }
                        };
                        var msgResult2 = service.RetrieveMultiple(msgQuery2);
                        if (msgResult2.Entities.Count == 0) { Console.WriteLine($"错误: 找不到 Message: {stepMessageName}"); return; }
                        Guid stepMsgId = msgResult2.Entities[0].Id;
                        
                        // 3. 查 SdkMessageFilter ID
                        var sfQuery = new QueryExpression("sdkmessagefilter")
                        {
                            ColumnSet = new ColumnSet("sdkmessagefilterid"),
                            Criteria = new FilterExpression
                            {
                                Conditions =
                                {
                                    new ConditionExpression("sdkmessageid", ConditionOperator.Equal, stepMsgId),
                                    new ConditionExpression("primaryobjecttypecode", ConditionOperator.Equal, stepEntityName)
                                }
                            }
                        };
                        var sfResult = service.RetrieveMultiple(sfQuery);
                        Guid stepFilterId = sfResult.Entities.Count > 0 ? sfResult.Entities[0].Id : Guid.Empty;
                        
                        // 4. 查 Step 是否已存在
                        var stQuery = new QueryExpression("sdkmessageprocessingstep")
                        {
                            ColumnSet = new ColumnSet("sdkmessageprocessingstepid", "filteringattributes"),
                            Criteria = new FilterExpression
                            {
                                Conditions =
                                {
                                    new ConditionExpression("plugintypeid", ConditionOperator.Equal, stepPluginTypeId),
                                    new ConditionExpression("sdkmessageid", ConditionOperator.Equal, stepMsgId),
                                    new ConditionExpression("stage", ConditionOperator.Equal, stepStageValue)
                                }
                            }
                        };
                        if (stepFilterId != Guid.Empty)
                            stQuery.Criteria.Conditions.Add(new ConditionExpression("sdkmessagefilterid", ConditionOperator.Equal, stepFilterId));
                        var stResult = service.RetrieveMultiple(stQuery);
                        
                        // 5. 创建或更新 Step
                        var stepEnt = new Entity("sdkmessageprocessingstep");
                        stepEnt["name"] = $"{stepClassName.Split('.').Last()}: {stepMessageName} of {stepEntityName}";
                        stepEnt["plugintypeid"] = new EntityReference("plugintype", stepPluginTypeId);
                        stepEnt["sdkmessageid"] = new EntityReference("sdkmessage", stepMsgId);
                        stepEnt["stage"] = new OptionSetValue(stepStageValue);
                        stepEnt["mode"] = new OptionSetValue(0);
                        stepEnt["rank"] = 1;
                        stepEnt["supporteddeployment"] = new OptionSetValue(0);
                        if (stepFilterId != Guid.Empty)
                            stepEnt["sdkmessagefilterid"] = new EntityReference("sdkmessagefilter", stepFilterId);
                        if (!string.IsNullOrEmpty(stepFilterAttr))
                            stepEnt["filteringattributes"] = stepFilterAttr;
                        
                        if (stResult.Entities.Count > 0)
                        {
                            stepEnt.Id = stResult.Entities[0].Id;
                            service.Update(stepEnt);
                            Console.WriteLine($"  [更新] Step: {stepEnt["name"]}");
                        }
                        else
                        {
                            var newStepId = service.Create(stepEnt);
                            Console.WriteLine($"  [新建] Step: {stepEnt["name"]} ({newStepId})");
                        }
                        break;

                    case "update-assembly":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run update-assembly <DLL路径>");
                            return;
                        }
                        manager.UpdateAssemblyOnly(args[1]);
                        break;

                    case "deploy-js-14":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run deploy-js-14 <JS文件路径>");
                            return;
                        }
                        var jsPath14 = args[1];
                        var jsName14 = System.IO.Path.GetFileName(jsPath14);
                        manager.DeployWebResource(jsName14, "信用评估记录表-表单逻辑", jsPath14, "entity_20260603_peter");
                        break;

                    case "deploy-html":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run deploy-html <HTML文件路径> [显示名称]");
                            return;
                        }
                        var htmlPath = args[1];
                        var htmlName = System.IO.Path.GetFileName(htmlPath);
                        var htmlDisplayName = args.Length >= 3 ? args[2] : htmlName;
                        manager.DeployWebResource(htmlName, htmlDisplayName, htmlPath, "entity_20260603_peter", 1); // 1 = HTML
                        break;

                    case "bind-js-14":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run bind-js-14 <JS文件名>");
                            return;
                        }
                        manager.BindJsToForm("mcs_credit_record", args[1], "Information");
                        break;

                    case "bind-js-tag":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run bind-js-tag <JS文件名>");
                            return;
                        }
                        manager.BindJsToForm("mcs_customer_tag", args[1], "Information");
                        break;

                    case "bind-js-enum":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run bind-js-enum <JS文件名>");
                            return;
                        }
                        manager.BindJsToForm("mcs_credititem_value", args[1], "Information");
                        break;

                    case "query-credit-record-bpp":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run query-credit-record-bpp <scoreid>");
                            return;
                        }
                        QueryCreditRecordBpp(service, args[1]);
                        break;

                    case "query-recent-credit-record-bpp":
                        QueryRecentCreditRecordBpp(service, args.Length >= 2 ? int.Parse(args[1]) : 10);
                        break;

                    case "query-bpp-debug":
                        QueryBppDebug(service);
                        break;

                    case "create-credit-items":
                        CreateCreditItems(manager);
                        break;

                    case "create-qualitative-enums":
                        CreateQualitativeEnums(manager);
                        break;

                    case "fix-coface-qualitative-enums":
                        manager.FixCofaceQualitativeEnums();
                        break;

                    case "cleanup-qualitative-enums":
                        manager.CleanupQualitativeEnumRecords();
                        break;

                    case "create-lookup-field":
                        if (args.Length < 6)
                        {
                            Console.WriteLine("用法: dotnet run create-lookup-field <实体名> <字段名> <显示名> <描述> <目标实体名> <目标实体显示名>");
                            return;
                        }
                        manager.CreateLookupField(args[1], args[2], args[3], args[4], args[5], args[6]);
                        break;

                    case "check-credit-items":
                        CheckCreditItems(manager);
                        break;

                    case "cleanup-credit-items":
                        CleanupCreditItems(manager);
                        break;

                    case "delete-credit-item":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run delete-credit-item <编码>");
                            return;
                        }
                        manager.DeleteCreditItemByCode(args[1]);
                        break;

                    case "rearrange-form":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run rearrange-form <实体名>");
                            return;
                        }
                        RearrangeForm(manager, args[1]);
                        break;

                    case "add-fields":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run add-fields <实体名>");
                            return;
                        }
                        AddFieldsToEntity(manager, args[1]);
                        break;

                    case "list-types":
                        {
                            string filter = args.Length >= 2 ? args[1] : null;
                            var query = new QueryExpression("plugintype")
                            {
                                ColumnSet = new ColumnSet("plugintypeid", "typename", "friendlyname", "pluginassemblyid"),
                                Orders = { new OrderExpression("typename", OrderType.Ascending) }
                            };
                            if (!string.IsNullOrEmpty(filter))
                            {
                                query.Criteria.AddCondition("typename", ConditionOperator.Like, $"%{filter}%");
                            }
                            var result = service.RetrieveMultiple(query);
                            Console.WriteLine($"=== Plugin Types ({result.Entities.Count}) ===");
                            foreach (var e in result.Entities)
                            {
                                var asmRef = e.GetAttributeValue<EntityReference>("pluginassemblyid");
                                Console.WriteLine($"  {e.GetAttributeValue<string>("typename")} | Assembly: {asmRef?.Name ?? asmRef?.Id.ToString() ?? "N/A"}");
                            }
                        }
                        break;

                    case "query-scoring-cards":
                        {
                            var scQuery = new QueryExpression("mcs_credit_scoringcard")
                            {
                                ColumnSet = new ColumnSet("mcs_credit_scoringcardid", "mcs_cardname", "mcs_categoryid", "mcs_typeid", "mcs_credititem", "mcs_weight", "mcs_minvalue", "mcs_maxvalue", "mcs_listvalue"),
                                Orders = { new OrderExpression("mcs_categoryid", OrderType.Ascending), new OrderExpression("mcs_credititem", OrderType.Ascending) }
                            };
                            var scResult = service.RetrieveMultiple(scQuery);
                            var groups = scResult.Entities.GroupBy(e => e.GetAttributeValue<OptionSetValue>("mcs_categoryid")?.Value ?? -1).OrderBy(g => g.Key);
                            Console.WriteLine($"=== 评分卡配置 (共 {scResult.Entities.Count} 条) ===\n");
                            int badLinks = 0;
                            foreach (var g in groups)
                            {
                                var catName = g.Key switch { 1 => "SA老客户", 2 => "SA新客户", 3 => "BC老客户", 4 => "BC新客户", 5 => "个人客户", 6 => "老经销商", 7 => "新经销商", _ => $"未知({g.Key})" };
                                Console.WriteLine($"Category {g.Key} [{catName}]: {g.Count()} 条");
                                foreach (var card in g)
                                {
                                    var itemRef = card.GetAttributeValue<EntityReference>("mcs_credititem");
                                    var weight = card.GetAttributeValue<int?>("mcs_weight") ?? 0;
                                    var typeId = card.GetAttributeValue<OptionSetValue>("mcs_typeid")?.Value ?? 0;
                                    var typeName = typeId switch { 1 => "实力", 2 => "财务", 3 => "宏观", _ => "?" };
                                    var listValue = card.GetAttributeValue<string>("mcs_listvalue") ?? "";
                                    var minV = card.GetAttributeValue<decimal?>("mcs_minvalue");
                                    var maxV = card.GetAttributeValue<decimal?>("mcs_maxvalue");
                                    var range = (minV.HasValue || maxV.HasValue) ? $"[{minV}-{maxV}]" : "";
                                    var lv = !string.IsNullOrEmpty(listValue) ? $" list={listValue}" : "";
                                    var itemName = itemRef?.Name ?? "?";
                                    var linkStatus = (itemRef == null) ? " ❌无Lookup" : (string.IsNullOrEmpty(itemRef.Name) ? $" ❌坏关联({itemRef.Id.ToString()[..8]}...)" : "");
                                    if (itemRef == null || string.IsNullOrEmpty(itemRef.Name)) badLinks++;
                                    Console.WriteLine($"  - {itemName}{linkStatus} | {typeName} | weight={weight} {range}{lv}");
                                }
                                Console.WriteLine();
                            }
                            Console.WriteLine($"=== 坏关联统计: {badLinks}/{scResult.Entities.Count} 条 ===");
                        }
                        break;

                    case "rebuild-scoring-cards":
                        {
                            Console.WriteLine("\n=== 重建 UAT Scoring Card 配置 ===");
                            Console.WriteLine("步骤1: 从 DEV1 读取配置...");

                            // DEV1 连接
                            var devCs = D365ConnectionFactory.BuildConnectionString("https://dev1.crm5.dynamics.com");
                            using var devService = new ServiceClient(devCs);
                            if (!devService.IsReady) { Console.WriteLine("DEV1 连接失败"); break; }

                            var devQuery = new QueryExpression("mcs_credit_scoringcard")
                            {
                                ColumnSet = new ColumnSet("mcs_categoryid", "mcs_typeid", "mcs_weight", "mcs_minvalue", "mcs_maxvalue", "mcs_credititem", "mcs_listvalue"),
                                Orders = { new OrderExpression("mcs_categoryid", OrderType.Ascending) }
                            };
                            var link1 = new LinkEntity("mcs_credit_scoringcard", "mcs_credit_items", "mcs_credititem", "mcs_credit_itemsid", JoinOperator.LeftOuter)
                            {
                                Columns = new ColumnSet("mcs_credit_itemsno", "mcs_itemname"),
                                EntityAlias = "item"
                            };
                            devQuery.LinkEntities.Add(link1);
                            var devResult = devService.RetrieveMultiple(devQuery);
                            Console.WriteLine($"DEV1 读取到 {devResult.Entities.Count} 条配置");

                            var configs = new List<Dictionary<string, object>>();
                            foreach (var r in devResult.Entities)
                            {
                                var cat = r.GetAttributeValue<OptionSetValue>("mcs_categoryid")?.Value ?? 0;
                                var type = r.GetAttributeValue<OptionSetValue>("mcs_typeid")?.Value ?? 0;
                                var weight = r.GetAttributeValue<int?>("mcs_weight") ?? 0;
                                var minV = r.GetAttributeValue<decimal?>("mcs_minvalue");
                                var maxV = r.GetAttributeValue<decimal?>("mcs_maxvalue");

                                string itemNo = "";
                                var aliased = r.GetAttributeValue<AliasedValue>("item.mcs_credit_itemsno");
                                if (aliased?.Value != null) itemNo = aliased.Value.ToString() ?? "";
                                if (string.IsNullOrEmpty(itemNo))
                                {
                                    var aliasedName = r.GetAttributeValue<AliasedValue>("item.mcs_itemname");
                                    if (aliasedName?.Value != null) itemNo = aliasedName.Value.ToString() ?? "";
                                }

                                string listValue = "";
                                if (r.Contains("mcs_listvalue"))
                                {
                                    var lvObj = r["mcs_listvalue"];
                                    if (lvObj is string strVal) listValue = strVal;
                                    else if (lvObj is EntityReference er)
                                    {
                                        try {
                                            var lvEnt = devService.Retrieve("mcs_credititem_value", er.Id, new ColumnSet("mcs_listvalue", "mcs_listname"));
                                            if (lvEnt != null) listValue = lvEnt.GetAttributeValue<string>("mcs_listvalue") ?? "";
                                        } catch { }
                                    }
                                }

                                configs.Add(new Dictionary<string, object>
                                {
                                    ["category"] = cat, ["type"] = type, ["weight"] = weight,
                                    ["min"] = minV, ["max"] = maxV, ["itemNo"] = itemNo, ["listValue"] = listValue
                                });
                            }

                            Console.WriteLine("步骤2: 在 UAT 获取 credit_items / credititem_value GUID 映射...");
                            var uatItemsQuery = new QueryExpression("mcs_credit_items")
                            {
                                ColumnSet = new ColumnSet("mcs_credit_itemsid", "mcs_credit_itemsno", "mcs_itemname")
                            };
                            var uatItemsResult = service.RetrieveMultiple(uatItemsQuery);
                            var itemMap = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
                            foreach (var it in uatItemsResult.Entities)
                            {
                                var id = it.Id;
                                var no = it.GetAttributeValue<string>("mcs_credit_itemsno") ?? "";
                                var name = it.GetAttributeValue<string>("mcs_itemname") ?? "";
                                if (!string.IsNullOrEmpty(no)) itemMap[no] = id;
                                if (!string.IsNullOrEmpty(name)) itemMap[name] = id;
                            }
                            Console.WriteLine($"UAT credit_items 映射: {itemMap.Count} 条");

                            var uatValueQuery = new QueryExpression("mcs_credititem_value")
                            {
                                ColumnSet = new ColumnSet("mcs_credititem_valueid", "mcs_listvalue", "mcs_listname")
                            };
                            var uatValueResult = service.RetrieveMultiple(uatValueQuery);
                            var valueMap = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
                            foreach (var v in uatValueResult.Entities)
                            {
                                var id = v.Id;
                                var lv = v.GetAttributeValue<string>("mcs_listvalue") ?? "";
                                var ln = v.GetAttributeValue<string>("mcs_listname") ?? "";
                                if (!string.IsNullOrEmpty(lv)) valueMap[lv] = id;
                                if (!string.IsNullOrEmpty(ln)) valueMap[ln] = id;
                            }
                            Console.WriteLine($"UAT credititem_value 映射: {valueMap.Count} 条");

                            Console.WriteLine("步骤3: 删除 UAT 现有 scoring cards...");
                            var delQuery = new QueryExpression("mcs_credit_scoringcard") { ColumnSet = new ColumnSet("mcs_credit_scoringcardid") };
                            var delResult = service.RetrieveMultiple(delQuery);
                            int delCount = 0;
                            foreach (var d in delResult.Entities)
                            {
                                try { service.Delete("mcs_credit_scoringcard", d.Id); delCount++; } catch (Exception ex) { Console.WriteLine($"  删除失败 {d.Id}: {ex.Message}"); }
                            }
                            Console.WriteLine($"已删除 {delCount} 条");

                            Console.WriteLine("步骤4: 在 UAT 重新创建...");
                            int createCount = 0;
                            int skipCount = 0;
                            foreach (var cfg in configs)
                            {
                                string itemNo = cfg["itemNo"]?.ToString() ?? "";
                                if (string.IsNullOrEmpty(itemNo) || !itemMap.ContainsKey(itemNo))
                                {
                                    Console.WriteLine($"  ⚠️ 跳过: 找不到 credit_items '{itemNo}'");
                                    skipCount++;
                                    continue;
                                }

                                var ent = new Entity("mcs_credit_scoringcard");
                                ent["mcs_categoryid"] = new OptionSetValue((int)cfg["category"]);
                                ent["mcs_typeid"] = new OptionSetValue((int)cfg["type"]);
                                ent["mcs_weight"] = (int)cfg["weight"];
                                ent["mcs_credititem"] = new EntityReference("mcs_credit_items", itemMap[itemNo]);
                                if (cfg["min"] is decimal minv) ent["mcs_minvalue"] = minv;
                                if (cfg["max"] is decimal maxv) ent["mcs_maxvalue"] = maxv;

                                string lv = cfg["listValue"]?.ToString() ?? "";
                                if (!string.IsNullOrEmpty(lv))
                                {
                                    if (valueMap.ContainsKey(lv))
                                        ent["mcs_listvalue"] = new EntityReference("mcs_credititem_value", valueMap[lv]);
                                    else
                                        Console.WriteLine($"  ⚠️ {itemNo}: 找不到 credititem_value '{lv}'，listvalue 未设置");
                                }

                                try { service.Create(ent); createCount++; }
                                catch (Exception ex) { Console.WriteLine($"  创建失败 {itemNo}: {ex.Message}"); skipCount++; }
                            }
                            Console.WriteLine($"\n✅ 完成: 创建 {createCount} 条, 跳过 {skipCount} 条");
                        }
                        break;

                    case "unregister-assembly":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run unregister-assembly <Assembly名称>");
                            return;
                        }
                        string asmName = args[1];
                        var asmQuery = new QueryExpression("pluginassembly")
                        {
                            ColumnSet = new ColumnSet("pluginassemblyid"),
                            Criteria = new FilterExpression
                            {
                                Conditions = { new ConditionExpression("name", ConditionOperator.Equal, asmName) }
                            }
                        };
                        var asmResult = service.RetrieveMultiple(asmQuery);
                        if (asmResult.Entities.Count == 0)
                        {
                            Console.WriteLine($"Assembly '{asmName}' 不存在");
                            return;
                        }
                        Guid asmId = asmResult.Entities[0].Id;
                        
                        // 删除 Steps 和 PluginTypes
                        var typeQuery2 = new QueryExpression("plugintype")
                        {
                            ColumnSet = new ColumnSet("plugintypeid", "typename"),
                            Criteria = new FilterExpression
                            {
                                Conditions = { new ConditionExpression("pluginassemblyid", ConditionOperator.Equal, asmId) }
                            }
                        };
                        var typeResult2 = service.RetrieveMultiple(typeQuery2);
                        Console.WriteLine($"找到 {typeResult2.Entities.Count} 个 PluginType...");
                        foreach (var pt in typeResult2.Entities)
                        {
                            Guid typeId = pt.Id;
                            var stepQuery2 = new QueryExpression("sdkmessageprocessingstep")
                            {
                                ColumnSet = new ColumnSet("sdkmessageprocessingstepid", "name"),
                                Criteria = new FilterExpression
                                {
                                    Conditions = { new ConditionExpression("plugintypeid", ConditionOperator.Equal, typeId) }
                                }
                            };
                            var stepResult2 = service.RetrieveMultiple(stepQuery2);
                            foreach (var st in stepResult2.Entities)
                            {
                                service.Delete("sdkmessageprocessingstep", st.Id);
                                Console.WriteLine($"  [-] Step: {st.GetAttributeValue<string>("name")}");
                            }
                            service.Delete("plugintype", typeId);
                            Console.WriteLine($"  [-] PluginType: {pt.GetAttributeValue<string>("typename")}");
                        }
                        service.Delete("pluginassembly", asmId);
                        Console.WriteLine($"[✓] Assembly '{asmName}' 已删除");
                        break;

                    case "uat-debug":
                        UatDebugQuery(service);
                        break;

                    case "check-credit-fields":
                        CheckCreditRecordFields(service);
                        break;

                    case "fix-scoring-cards":
                        FixScoringCardsUat(service);
                        break;

                    case "fix-account-type":
                        FixAccountTypeUat(service);
                        break;

                    case "query-credit-steps":
                        QueryAllCreditRecordSteps(service);
                        break;

                    case "check-scoringcard-fields":
                        CheckScoringCardFields(service);
                        break;

                    case "fix-dev-scoring-cards":
                        FixDevScoringCards(service);
                        break;

                    case "seed-coface-exchange-rates":
                        SeedCofaceExchangeRates(service);
                        break;

                    case "seed-coface-nace-mappings":
                        SeedCofaceNaceMappings(service);
                        break;

                    case "test-common":
                        new TestCommonService(service).Run();
                        break;

                    case "cleanup-test-common":
                        new TestCommonService(service).CleanupOnly();
                        break;

                    default:
                        Console.WriteLine($"未知命令: {command}");
                        ShowHelp();
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"错误: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"内部错误: {ex.InnerException.Message}");
            }
        }
    }

    static void ShowHelp()
    {
        Console.WriteLine("可用命令:");
        Console.WriteLine("  create <json文件>    - 从JSON创建实体和字段");
        Console.WriteLine("  check <解决方案名>    - 检查解决方案实体");
        Console.WriteLine("  add <实体> <解决方案> - 添加实体到解决方案");
        Console.WriteLine("  remove <实体> <解决方案> - 从解决方案移除实体");
        Console.WriteLine("  publish [实体]        - 发布");
        Console.WriteLine("  delete-field <实体> <字段> - 删除字段");
        Console.WriteLine("  list-fields <实体>    - 列出字段");
        Console.WriteLine("  export <解决方案> <路径> - 导出 ZIP");
        Console.WriteLine("  update-options <实体> <字段> <JSON> - 更新选项集");
        Console.WriteLine("  update-form <实体>    - 更新实体主窗体字段");
        Console.WriteLine("  check-form <实体>     - 检查实体窗体字段");
        Console.WriteLine("  check-view <实体>     - 检查实体视图");
        Console.WriteLine("  update-view <实体>    - 更新实体默认视图");
        Console.WriteLine("  add-fields <实体>     - 为实体批量添加预定义字段");
        Console.WriteLine("  export-webresource <名称> <路径> - 导出 WebResource 内容");
        Console.WriteLine("  list-form-webresources <实体> - 列出实体主窗体引用的 JS WebResource");
        Console.WriteLine("  seed-coface-exchange-rates - 初始化 Coface 2026 Budget 汇率数据");
        Console.WriteLine("  seed-coface-nace-mappings  - 初始化 Coface NACE 行业映射数据");
        Console.WriteLine("  fix-coface-qualitative-enums - 修复 Coface 定性指标枚举值");
        Console.WriteLine("  query-urba-json <scoreid> - 查询指定记录的 URBA JSON");
        Console.WriteLine("  query-customer-tags <scoreid> - 查询指定记录的客户标签明细");
        Console.WriteLine("  query-credit-items - 查询所有评分项目");
        Console.WriteLine("  add-webresource-to-solution <WebResource名称> <解决方案唯一名> - 将 WebResource 加入解决方案");
    }

    static void AddWebResourceToSolution(ServiceClient service, string webResourceName, string solutionUniqueName)
    {
        var solutionService = new D365ToolCommon.Solution.SolutionComponentService(service);
        solutionService.AddWebResourceToSolution(webResourceName, solutionUniqueName);
        Console.WriteLine($"✅ WebResource {webResourceName} 已加入解决方案 {solutionUniqueName}");
    }

    static void CheckAssemblyInSolution(ServiceClient service, string assemblyName, string solutionUniqueName, string? typeFilter = null)
    {
        // 查询 PluginAssembly
        var asmQuery = new QueryExpression("pluginassembly")
        {
            ColumnSet = new ColumnSet("pluginassemblyid", "name"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("name", ConditionOperator.Equal, assemblyName) }
            }
        };
        var asmResult = service.RetrieveMultiple(asmQuery).Entities.FirstOrDefault();
        if (asmResult == null)
        {
            Console.WriteLine($"❌ 未找到 Plugin Assembly: {assemblyName}");
            return;
        }
        var assemblyId = asmResult.Id;
        Console.WriteLine($"找到 Plugin Assembly: {assemblyName}, ID={assemblyId}");

        // 查询 Solution
        var solQuery = new QueryExpression("solution")
        {
            ColumnSet = new ColumnSet("solutionid", "friendlyname"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("uniquename", ConditionOperator.Equal, solutionUniqueName) }
            }
        };
        var solResult = service.RetrieveMultiple(solQuery).Entities.FirstOrDefault();
        if (solResult == null)
        {
            Console.WriteLine($"❌ 未找到 Solution: {solutionUniqueName}");
            return;
        }
        var solutionId = solResult.Id;
        Console.WriteLine($"找到 Solution: {solutionUniqueName}, ID={solutionId}");

        // 检查 Plugin Assembly (componenttype=91)
        CheckComponentTypeInSolution(service, assemblyId, 91, $"Plugin Assembly {assemblyName}", solutionId, solutionUniqueName);

        // 查询该 Assembly 下的 Plugin Types
        var typeCriteria = new FilterExpression
        {
            Conditions = { new ConditionExpression("pluginassemblyid", ConditionOperator.Equal, assemblyId) }
        };
        if (!string.IsNullOrWhiteSpace(typeFilter))
        {
            typeCriteria.Conditions.Add(new ConditionExpression("typename", ConditionOperator.Like, $"%{typeFilter}%"));
        }
        var typeQuery = new QueryExpression("plugintype")
        {
            ColumnSet = new ColumnSet("plugintypeid", "typename"),
            Criteria = typeCriteria
        };
        var types = service.RetrieveMultiple(typeQuery).Entities;
        Console.WriteLine($"\n该 Assembly 包含 {types.Count} 个 Plugin Type{(string.IsNullOrWhiteSpace(typeFilter) ? "" : $" (过滤: {typeFilter})")}:");
        foreach (var type in types)
        {
            var typeId = type.Id;
            var typeName = type.GetAttributeValue<string>("typename");
            CheckComponentTypeInSolution(service, typeId, 90, $"  Plugin Type {typeName}", solutionId, solutionUniqueName);

            // 查询该 Type 下的 Steps
            var stepQuery = new QueryExpression("sdkmessageprocessingstep")
            {
                ColumnSet = new ColumnSet("sdkmessageprocessingstepid", "name"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("plugintypeid", ConditionOperator.Equal, typeId) }
                }
            };
            var steps = service.RetrieveMultiple(stepQuery).Entities;
            foreach (var step in steps)
            {
                CheckComponentTypeInSolution(service, step.Id, 92, $"    Step {step.GetAttributeValue<string>("name")}", solutionId, solutionUniqueName);
            }
        }
    }

    static void QueryCustomApiParameters(ServiceClient service, string customApiUniqueName)
    {
        // 查询 Custom API
        var apiQuery = new QueryExpression("customapi")
        {
            ColumnSet = new ColumnSet("customapiid", "name", "uniquename", "displayname"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("uniquename", ConditionOperator.Equal, customApiUniqueName) }
            }
        };
        var apiResult = service.RetrieveMultiple(apiQuery).Entities.FirstOrDefault();
        if (apiResult == null)
        {
            Console.WriteLine($"❌ 未找到 Custom API: {customApiUniqueName}");
            return;
        }
        Console.WriteLine($"Custom API: {apiResult.GetAttributeValue<string>("uniquename")} (ID: {apiResult.Id})");
        Console.WriteLine($"  显示名: {apiResult.GetAttributeValue<string>("displayname")}");

        // 查询请求参数
        var paramQuery = new QueryExpression("customapirequestparameter")
        {
            ColumnSet = new ColumnSet("name", "uniquename", "displayname", "type", "isoptional"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("customapiid", ConditionOperator.Equal, apiResult.Id) }
            },
            Orders = { new OrderExpression("name", OrderType.Ascending) }
        };
        var parameters = service.RetrieveMultiple(paramQuery).Entities;
        Console.WriteLine($"\n请求参数 ({parameters.Count} 个):");
        foreach (var p in parameters)
        {
            var name = p.GetAttributeValue<string>("uniquename");
            var display = p.GetAttributeValue<string>("displayname");
            var type = p.GetAttributeValue<OptionSetValue>("type")?.Value ?? 0;
            var optional = p.GetAttributeValue<bool>("isoptional");
            Console.WriteLine($"  - {name} (显示名: {display}, 类型: {type}, 可选: {optional})");
        }

        // 查询响应属性
        var respQuery = new QueryExpression("customapiresponseproperty")
        {
            ColumnSet = new ColumnSet("name", "uniquename", "displayname", "type"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("customapiid", ConditionOperator.Equal, apiResult.Id) }
            }
        };
        var resps = service.RetrieveMultiple(respQuery).Entities;
        Console.WriteLine($"\n响应属性 ({resps.Count} 个):");
        foreach (var r in resps)
        {
            var name = r.GetAttributeValue<string>("uniquename");
            var display = r.GetAttributeValue<string>("displayname");
            Console.WriteLine($"  - {name} (显示名: {display})");
        }
    }

    static void AddPluginStepToSolution(ServiceClient service, string stepIdOrName, string solutionUniqueName)
    {
        Guid stepId;
        if (!Guid.TryParse(stepIdOrName, out stepId))
        {
            // 按名称查询 Step
            var query = new QueryExpression("sdkmessageprocessingstep")
            {
                ColumnSet = new ColumnSet("sdkmessageprocessingstepid", "name"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("name", ConditionOperator.Equal, stepIdOrName) }
                }
            };
            var result = service.RetrieveMultiple(query).Entities.FirstOrDefault();
            if (result == null)
            {
                Console.WriteLine($"❌ 未找到 Plugin Step: {stepIdOrName}");
                return;
            }
            stepId = result.Id;
        }

        var solutionService = new D365ToolCommon.Solution.SolutionComponentService(service);
        solutionService.AddComponentToSolution(stepId, 92, solutionUniqueName);
        Console.WriteLine($"✅ Plugin Step {stepId} 已加入 Solution {solutionUniqueName}");
    }

    static void CheckComponentTypeInSolution(ServiceClient service, Guid objectId, int componentType, string label, Guid solutionId, string solutionUniqueName)
    {
        var scQuery = new QueryExpression("solutioncomponent")
        {
            ColumnSet = new ColumnSet("solutioncomponentid"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("solutionid", ConditionOperator.Equal, solutionId),
                    new ConditionExpression("objectid", ConditionOperator.Equal, objectId),
                    new ConditionExpression("componenttype", ConditionOperator.Equal, componentType)
                }
            }
        };
        var scResult = service.RetrieveMultiple(scQuery).Entities.FirstOrDefault();
        if (scResult != null)
        {
            Console.WriteLine($"✅ {label} 已在 Solution {solutionUniqueName} 中");
        }
        else
        {
            Console.WriteLine($"⚠️ {label} 不在 Solution {solutionUniqueName} 中");
        }
    }

    static void ExportWebResource(IOrganizationService service, string name, string outputPath)
    {
        var query = new Microsoft.Xrm.Sdk.Query.QueryExpression("webresource")
        {
            ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("webresourceid", "name", "webresourcetype", "content"),
            Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression()
            {
                Conditions = { new Microsoft.Xrm.Sdk.Query.ConditionExpression("name", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, name) }
            }
        };
        var result = service.RetrieveMultiple(query);
        if (result.Entities.Count == 0)
        {
            Console.WriteLine($"❌ 未找到 WebResource: {name}");
            return;
        }
        var wr = result.Entities[0];
        var contentBase64 = wr.GetAttributeValue<string>("content");
        if (string.IsNullOrEmpty(contentBase64))
        {
            Console.WriteLine($"⚠️ WebResource {name} 内容为空");
            return;
        }
        var bytes = Convert.FromBase64String(contentBase64);
        File.WriteAllBytes(outputPath, bytes);
        Console.WriteLine($"✅ WebResource {name} 已导出到: {outputPath} ({bytes.Length} bytes)");
    }

    static void UpdateWebResource(IOrganizationService service, string name, string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"❌ 文件不存在: {filePath}");
            return;
        }

        var query = new Microsoft.Xrm.Sdk.Query.QueryExpression("webresource")
        {
            ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("webresourceid", "name", "webresourcetype"),
            Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression()
            {
                Conditions = { new Microsoft.Xrm.Sdk.Query.ConditionExpression("name", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, name) }
            }
        };
        var result = service.RetrieveMultiple(query);
        if (result.Entities.Count == 0)
        {
            Console.WriteLine($"❌ 未找到 WebResource: {name}");
            return;
        }

        var wr = result.Entities[0];
        var bytes = File.ReadAllBytes(filePath);
        var contentBase64 = Convert.ToBase64String(bytes);

        var update = new Entity("webresource") { Id = wr.Id };
        update["content"] = contentBase64;
        service.Update(update);
        Console.WriteLine($"✅ WebResource {name} 已更新 ({bytes.Length} bytes)");
    }

    static void ListFormWebResources(IOrganizationService service, string entityName)
    {
        var query = new Microsoft.Xrm.Sdk.Query.QueryExpression("systemform")
        {
            ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("formid", "name", "formxml"),
            Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression()
            {
                Conditions =
                {
                    new Microsoft.Xrm.Sdk.Query.ConditionExpression("objecttypecode", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, entityName),
                    new Microsoft.Xrm.Sdk.Query.ConditionExpression("type", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, 2)
                }
            }
        };
        var forms = service.RetrieveMultiple(query);
        if (forms.Entities.Count == 0)
        {
            Console.WriteLine($"❌ 未找到 {entityName} 的主窗体");
            return;
        }
        foreach (var form in forms.Entities)
        {
            var formName = form.GetAttributeValue<string>("name");
            var formXml = form.GetAttributeValue<string>("formxml") ?? "";
            Console.WriteLine($"\n=== 窗体: {formName} ===");
            var matches = System.Text.RegularExpressions.Regex.Matches(formXml, "libraryName=\"([^\"]+)\"");
            if (matches.Count == 0)
            {
                Console.WriteLine("  (无 JS WebResource 引用)");
                continue;
            }
            var seen = new HashSet<string>();
            foreach (System.Text.RegularExpressions.Match m in matches)
            {
                var libName = m.Groups[1].Value;
                if (seen.Add(libName))
                    Console.WriteLine($"  - {libName}");
            }
        }
    }

    /// <summary>
    /// 更新信用评估记录状态
    /// </summary>
    static void UpdateCreditRecordStatus(ServiceClient service, string scoreId, int targetStatus)
    {
        Console.WriteLine($"=== 更新信用评估记录: {scoreId} ===");

        // 1. 查询记录
        var query = new Microsoft.Xrm.Sdk.Query.QueryExpression("mcs_credit_record")
        {
            ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet(
                "mcs_credit_recordid", "mcs_scoreid", "mcs_status",
                "statecode", "statuscode", "mcs_creditscore", "mcs_accountid",
                "createdon", "mcs_abidate", "mcs_api_status"
            ),
            Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression()
            {
                Conditions =
                {
                    new Microsoft.Xrm.Sdk.Query.ConditionExpression("mcs_scoreid", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, scoreId)
                }
            }
        };

        var records = service.RetrieveMultiple(query);
        if (records.Entities.Count == 0)
        {
            Console.WriteLine($"❌ 记录不存在: {scoreId}");
            return;
        }

        var record = records.Entities[0];
        Guid recordId = record.Id;
        int currentStatus = record.GetAttributeValue<Microsoft.Xrm.Sdk.OptionSetValue>("mcs_status")?.Value ?? -1;
        int stateCode = record.GetAttributeValue<Microsoft.Xrm.Sdk.OptionSetValue>("statecode")?.Value ?? -1;
        var score = record.GetAttributeValue<decimal?>("mcs_creditscore");
        var accountRef = record.GetAttributeValue<Microsoft.Xrm.Sdk.EntityReference>("mcs_accountid");

        Console.WriteLine($"  记录ID: {recordId}");
        Console.WriteLine($"  当前状态: {currentStatus} ({GetStatusName(currentStatus)})");
        Console.WriteLine($"  实体状态: {(stateCode == 0 ? "Active" : "Inactive")}");
        Console.WriteLine($"  信用分: {score}");
        Console.WriteLine($"  客户: {accountRef?.Name}");
        Console.WriteLine($"  API状态: {record.GetAttributeValue<string>("mcs_api_status")}");
        Console.WriteLine($"  数据集成日期: {record.GetAttributeValue<DateTime?>("mcs_abidate")}");
        
        // 输出URBA JSON中NACE相关片段
        string urbaJson = record.GetAttributeValue<string>("mcs_urbajson") ?? "";
        if (!string.IsNullOrEmpty(urbaJson))
        {
            Console.WriteLine($"  URBA JSON长度: {urbaJson.Length}");
            // 查找naceCodes
            int naceIdx = urbaJson.IndexOf("naceCodes", StringComparison.OrdinalIgnoreCase);
            if (naceIdx >= 0)
            {
                int start = Math.Max(0, naceIdx - 20);
                int end = Math.Min(urbaJson.Length, naceIdx + 300);
                Console.WriteLine($"  NACE片段: {urbaJson.Substring(start, end - start)}");
            }
            else
            {
                Console.WriteLine($"  ⚠️ URBA JSON中未找到 naceCodes 字段");
            }
            // 查找companyGeneralInformation
            int cgiIdx = urbaJson.IndexOf("companyGeneralInformation", StringComparison.OrdinalIgnoreCase);
            if (cgiIdx >= 0)
            {
                int start = Math.Max(0, cgiIdx);
                int end = Math.Min(urbaJson.Length, cgiIdx + 500);
                Console.WriteLine($"  companyGeneralInformation片段: {urbaJson.Substring(start, end - start)}");
            }
        }
        else
        {
            Console.WriteLine($"  URBA JSON: 为空");
        }

        // 2. 查询关联标签数量
        var tagQuery = new Microsoft.Xrm.Sdk.Query.QueryExpression("mcs_customer_tag")
        {
            ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("mcs_customer_tagid", "mcs_itemvalue1", "mcs_itemintvalue1", "mcs_itemtxtvalue1"),
            Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression()
            {
                Conditions =
                {
                    new Microsoft.Xrm.Sdk.Query.ConditionExpression("mcs_credit_record", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, recordId)
                }
            }
        };
        var tags = service.RetrieveMultiple(tagQuery);
        Console.WriteLine($"  关联标签数: {tags.Entities.Count}");

        // 打印标签明细
        if (tags.Entities.Count > 0)
        {
            Console.WriteLine("  标签明细:");
            foreach (var tag in tags.Entities)
            {
                string value1 = tag.GetAttributeValue<string>("mcs_itemvalue1") ?? "N/A";
                var intVal = tag.GetAttributeValue<decimal?>("mcs_itemintvalue1");
                string intStr = intVal.HasValue ? intVal.Value.ToString("F2") : "N/A";
                string txtVal = tag.GetAttributeValue<string>("mcs_itemtxtvalue1") ?? "N/A";
                Console.WriteLine($"    - value1={value1}, txtvalue={txtVal}, intvalue={intStr}");
            }
        }

        // 3. 状态检查与更新
        if (stateCode == 1)
        {
            Console.WriteLine($"\n⚠️ 记录已 Inactive，无法修改状态。需要先 Reactivate。");
            Console.WriteLine($"   建议: 新建一条评估记录重新走流程。");
            return;
        }

        if (currentStatus == targetStatus)
        {
            Console.WriteLine($"\n✅ 记录当前已经是状态 {targetStatus}，无需更新。");
            return;
        }

        // 确认更新
        Console.WriteLine($"\n>>> 准备更新状态: {currentStatus} → {targetStatus}");
        try
        {
            var update = new Microsoft.Xrm.Sdk.Entity("mcs_credit_record", recordId);
            update["mcs_status"] = new Microsoft.Xrm.Sdk.OptionSetValue(targetStatus);
            service.Update(update);
            Console.WriteLine($"✅ 更新成功! 状态已变为 {targetStatus} ({GetStatusName(targetStatus)})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 更新失败: {ex.Message}");
        }
    }

    static string GetStatusName(int status)
    {
        switch (status)
        {
            case 9: return "发起";
            case 10: return "关联客户代码";
            case 11: return "内外部数据集成";
            case 12: return "人工复核";
            case 13: return "信用分计算";
            case 14: return "审核申请";
            case 15: return "审批通过";
            case 16: return "审批未通过";
            default: return $"未知({status})";
        }
    }

    static void UatDebugQuery(ServiceClient service)
    {
        Console.WriteLine("=== UAT Debug 查询 ===");
        string scoreId = "SCO202606110001";

        // 1. 查评估记录
        var creditQuery = new QueryExpression("mcs_credit_record")
        {
            ColumnSet = new ColumnSet("mcs_scoreid", "mcs_accountid", "mcs_custnameen", "mcs_countrycode", "mcs_cofaceid", "mcs_status", "mcs_api_status", "mcs_api_msg", "mcs_api_name"),
            Criteria = new FilterExpression { Conditions = { new ConditionExpression("mcs_scoreid", ConditionOperator.Equal, scoreId) } }
        };
        var creditResult = service.RetrieveMultiple(creditQuery);
        if (creditResult.Entities.Count == 0) { Console.WriteLine("评估记录不存在"); return; }
        var creditRecord = creditResult.Entities[0];
        var creditId = creditRecord.Id;
        var accountRef = creditRecord.GetAttributeValue<EntityReference>("mcs_accountid");
        var accountId = accountRef?.Id ?? Guid.Empty;
        Console.WriteLine($"=== 评估记录 ===");
        Console.WriteLine($"ID: {creditId}");
        Console.WriteLine($"ScoreID: {creditRecord.GetAttributeValue<string>("mcs_scoreid")}");
        Console.WriteLine($"AccountID: {accountId}");
        Console.WriteLine($"CountryCode: {creditRecord.GetAttributeValue<string>("mcs_countrycode")}");
        Console.WriteLine($"CofaceID: {creditRecord.GetAttributeValue<string>("mcs_cofaceid")}");
        Console.WriteLine($"Status: {creditRecord.GetAttributeValue<OptionSetValue>("mcs_status")?.Value}");
        Console.WriteLine($"APIStatus: {creditRecord.GetAttributeValue<string>("mcs_api_status")}");
        Console.WriteLine($"APIMsg: {creditRecord.GetAttributeValue<string>("mcs_api_msg")}");
        Console.WriteLine($"APIName: {creditRecord.GetAttributeValue<string>("mcs_api_name")}");

        if (accountId == Guid.Empty) { Console.WriteLine("\n没有关联客户"); return; }

        // 2. 查 Account 属性
        var account = service.Retrieve("account", accountId, new ColumnSet("name", "mcs_accountcategory", "mcs_accountlevel", "mcs_accounttype"));
        Console.WriteLine($"\n=== 客户信息 ===");
        Console.WriteLine($"Name: {account.GetAttributeValue<string>("name")}");
        var acctCat = account.GetAttributeValue<OptionSetValue>("mcs_accountcategory")?.Value ?? -1;
        var acctLvl = account.GetAttributeValue<OptionSetValue>("mcs_accountlevel")?.Value ?? -1;
        var acctType = account.GetAttributeValue<OptionSetValue>("mcs_accounttype")?.Value ?? -1;
        Console.WriteLine($"AccountCategory: {acctCat}");
        Console.WriteLine($"AccountLevel: {acctLvl}");
        Console.WriteLine($"AccountType: {acctType}");

        // 3. 查是否有销售订单（判断新老客户）
        var orderQuery = new QueryExpression("salesorder")
        {
            ColumnSet = new ColumnSet("salesorderid"),
            Criteria = new FilterExpression { Conditions = { new ConditionExpression("customerid", ConditionOperator.Equal, accountId) } },
            TopCount = 1
        };
        var orders = service.RetrieveMultiple(orderQuery);
        bool isOldCustomer = orders.Entities.Count > 0;
        Console.WriteLine($"IsOldCustomer: {isOldCustomer}");

        // 4. 匹配评分卡类型
        int categoryId = MatchScoringCardType(isOldCustomer, acctCat, acctLvl, acctType);
        Console.WriteLine($"CategoryId: {categoryId}");

        if (categoryId == 0) { Console.WriteLine("无法匹配评分卡类型"); return; }

        // 5. 查评分卡配置
        var cardQuery = new QueryExpression("mcs_credit_scoringcard")
        {
            ColumnSet = new ColumnSet("mcs_itemid", "mcs_itemname", "mcs_datatype", "mcs_weight"),
            Criteria = new FilterExpression { Conditions = { new ConditionExpression("mcs_categoryid", ConditionOperator.Equal, categoryId) } }
        };
        var cardResult = service.RetrieveMultiple(cardQuery);
        Console.WriteLine($"\n=== 评分卡配置 ({cardResult.Entities.Count} 条) ===");
        foreach (var card in cardResult.Entities)
        {
            var itemRef2 = card.GetAttributeValue<EntityReference>("mcs_itemid");
            string itemCode = "";
            string itemName = card.GetAttributeValue<string>("mcs_itemname") ?? "";
            int dataType = card.GetAttributeValue<OptionSetValue>("mcs_datatype")?.Value ?? -1;
            int weight = card.GetAttributeValue<int>("mcs_weight");

            if (itemRef2 != null)
            {
                try
                {
                    var item = service.Retrieve("mcs_credit_items", itemRef2.Id, new ColumnSet("mcs_credit_itemsno", "mcs_group"));
                    itemCode = item?.GetAttributeValue<string>("mcs_credit_itemsno") ?? "";
                }
                catch (Exception ex)
                {
                    itemCode = $"ERROR:{ex.Message}";
                }
            }
            Console.WriteLine($"  [{itemCode}] {itemName} | datatype={dataType} | weight={weight} | itemId={itemRef2?.Id}");
        }

        // 6. 查已有标签
        var tagQuery = new QueryExpression("mcs_customer_tag")
        {
            ColumnSet = new ColumnSet("mcs_itemcode", "mcs_itemname", "mcs_itemvalue1", "mcs_itemtxtvalue1", "mcs_itemintvalue1", "mcs_datatype"),
            Criteria = new FilterExpression { Conditions = { new ConditionExpression("mcs_credit_record", ConditionOperator.Equal, creditId) } }
        };
        var tagResult = service.RetrieveMultiple(tagQuery);
        Console.WriteLine($"\n=== 已有标签 ({tagResult.Entities.Count} 条) ===");
        foreach (var tag in tagResult.Entities)
        {
            var code = tag.GetAttributeValue<string>("mcs_itemcode");
            var name = tag.GetAttributeValue<string>("mcs_itemname");
            var val1 = tag.GetAttributeValue<string>("mcs_itemvalue1");
            var txt1 = tag.GetAttributeValue<string>("mcs_itemtxtvalue1");
            var int1 = tag.GetAttributeValue<decimal?>("mcs_itemintvalue1");
            var dt = tag.GetAttributeValue<OptionSetValue>("mcs_datatype")?.Value ?? -1;
            Console.WriteLine($"  [{code}] {name} | val1={val1} | txt1={txt1} | int1={int1} | datatype={dt}");
        }

        // 7. 查询 CofaceToD365Mapping 中是否包含这些评分项目编码
        Console.WriteLine($"\n=== CofaceToD365Mapping 覆盖检查 ===");
        var mappingValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ExternalRating", "LatePaymentIndex", "CountryRisk", "SectorRisk", "Sectors",
            "NetAssets", "DebtRatio", "CurrentRatio", "NetProfit",
            "RegisteredCapital", "RegistrationDate", "LegalEvents"
        };
        foreach (var card in cardResult.Entities)
        {
            var itemRef2 = card.GetAttributeValue<EntityReference>("mcs_itemid");
            string itemCode = "";
            if (itemRef2 != null)
            {
                try { var item = service.Retrieve("mcs_credit_items", itemRef2.Id, new ColumnSet("mcs_credit_itemsno")); itemCode = item?.GetAttributeValue<string>("mcs_credit_itemsno") ?? ""; } catch { }
            }
            bool inMapping = mappingValues.Contains(itemCode);
            if (!inMapping && !string.IsNullOrEmpty(itemCode))
            {
                Console.WriteLine($"  ⚠️ [{itemCode}] 不在 CofaceToD365Mapping 中!");
            }
        }

        Console.WriteLine("\n=== 查询完成 ===");
    }

    static int MatchScoringCardType(bool isOldCustomer, int accountCategory, int accountLevel, int accountType)
    {
        if (accountType == 1) return 5;
        bool isDealer = (accountCategory == 10 || accountCategory == 90);
        if (isDealer) return isOldCustomer ? 6 : 7;
        bool isBigAccount = (accountLevel == 4 || accountLevel == 3);
        if (isOldCustomer) return isBigAccount ? 1 : 3;
        return isBigAccount ? 2 : 4;
    }

    static void CheckCreditRecordFields(ServiceClient service)
    {
        Console.WriteLine("=== 检查 mcs_credit_record 字段 ===");
        var fields = new[] { "mcs_urba360id", "mcs_urbastatus", "mcs_abidate", "mcs_rptorderid", "mcs_publicationid", "mcs_rptstatus", "mcs_urbajson", "mcs_reportjson", "mcs_api_status", "mcs_api_name", "mcs_api_msg" };
        foreach (var f in fields)
        {
            try
            {
                var req = new Microsoft.Xrm.Sdk.Messages.RetrieveAttributeRequest
                {
                    EntityLogicalName = "mcs_credit_record",
                    LogicalName = f
                };
                var resp = (Microsoft.Xrm.Sdk.Messages.RetrieveAttributeResponse)service.Execute(req);
                var attr = resp.AttributeMetadata;
                Console.WriteLine($"  ✅ {f,-20} | {attr.AttributeType,-12} | {attr.DisplayName.UserLocalizedLabel?.Label}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ {f,-20} | 不存在 | {ex.Message}");
            }
        }
    }

    static void CheckScoringCardFields(ServiceClient service)
    {
        Console.WriteLine("=== 检查 mcs_credit_scoringcard 字段 ===");
        var fields = new[] { "mcs_itemid", "mcs_itemname", "mcs_datatype", "mcs_weight", "mcs_minvalue", "mcs_maxvalue", "mcs_categoryid", "mcs_typeid", "mcs_listvalue" };
        foreach (var f in fields)
        {
            try
            {
                var req = new Microsoft.Xrm.Sdk.Messages.RetrieveAttributeRequest
                {
                    EntityLogicalName = "mcs_credit_scoringcard",
                    LogicalName = f
                };
                var resp = (Microsoft.Xrm.Sdk.Messages.RetrieveAttributeResponse)service.Execute(req);
                var attr = resp.AttributeMetadata;
                Console.WriteLine($"  ✅ {f,-20} | {attr.AttributeType,-12} | {attr.DisplayName.UserLocalizedLabel?.Label}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ {f,-20} | 不存在 | {ex.Message}");
            }
        }
    }

    static void QueryAllCreditRecordSteps(ServiceClient service)
    {
        Console.WriteLine("=== 所有 Update mcs_credit_record 的 Plugin Steps ===\n");
        // 先查 sdkmessagefilter
        var filterQuery = new QueryExpression("sdkmessagefilter")
        {
            ColumnSet = new ColumnSet("sdkmessagefilterid", "sdkmessageid", "primaryobjecttypecode"),
            Criteria = new FilterExpression { Conditions = { new ConditionExpression("primaryobjecttypecode", ConditionOperator.Equal, "mcs_credit_record") } }
        };
        var linkMsg = new LinkEntity("sdkmessagefilter", "sdkmessage", "sdkmessageid", "sdkmessageid", JoinOperator.Inner)
        {
            Columns = new ColumnSet("name"),
            EntityAlias = "msg"
        };
        filterQuery.LinkEntities.Add(linkMsg);
        var filterResult = service.RetrieveMultiple(filterQuery);
        var filterMap = new Dictionary<Guid, (string msgName, string entityName)>();
        foreach (var f in filterResult.Entities)
        {
            var msgName = f.GetAttributeValue<AliasedValue>("msg.name")?.Value?.ToString() ?? "";
            var entityName = f.GetAttributeValue<string>("primaryobjecttypecode") ?? "";
            filterMap[f.Id] = (msgName, entityName);
        }

        // 再查 sdkmessageprocessingstep
        var stepQuery = new QueryExpression("sdkmessageprocessingstep")
        {
            ColumnSet = new ColumnSet("name", "sdkmessagefilterid", "plugintypeid", "filteringattributes", "stage", "mode", "statecode"),
            Criteria = new FilterExpression { Conditions = { new ConditionExpression("statecode", ConditionOperator.Equal, 0) } }
        };
        var linkType = new LinkEntity("sdkmessageprocessingstep", "plugintype", "plugintypeid", "plugintypeid", JoinOperator.Inner)
        {
            Columns = new ColumnSet("typename"),
            EntityAlias = "pt"
        };
        stepQuery.LinkEntities.Add(linkType);
        var stepResult = service.RetrieveMultiple(stepQuery);

        int count = 0;
        foreach (var step in stepResult.Entities)
        {
            var filterId = step.GetAttributeValue<EntityReference>("sdkmessagefilterid")?.Id ?? Guid.Empty;
            if (!filterMap.TryGetValue(filterId, out var info)) continue;
            if (info.msgName != "Update") continue;

            var typeName = step.GetAttributeValue<AliasedValue>("pt.typename")?.Value?.ToString() ?? "";
            var filterAttrs = step.GetAttributeValue<string>("filteringattributes") ?? "";
            var stage = step.GetAttributeValue<OptionSetValue>("stage")?.Value ?? -1;
            var mode = step.GetAttributeValue<OptionSetValue>("mode")?.Value ?? -1;

            string stageName = stage == 10 ? "PreValidation" : stage == 20 ? "PreOp" : stage == 40 ? "PostOp" : $"Stage{stage}";
            string modeName = mode == 0 ? "Sync" : "Async";
            Console.WriteLine($"🔹 {typeName}");
            Console.WriteLine($"   Step: {step.GetAttributeValue<string>("name")}");
            Console.WriteLine($"   {stageName}/{modeName} | Filter:[{filterAttrs}]");
            Console.WriteLine();
            count++;
        }
        Console.WriteLine($"总计: {count} 个 Steps");
    }

    static void FixAccountTypeUat(ServiceClient service)
    {
        Console.WriteLine("=== 修改 UAT Account 类型 ===");
        // Kedai Kek: AIN202602260000
        var query = new QueryExpression("account")
        {
            ColumnSet = new ColumnSet("accountid", "name", "mcs_accountcategory", "mcs_accountlevel", "mcs_accounttype"),
            Criteria = new FilterExpression { Conditions = { new ConditionExpression("name", ConditionOperator.Equal, "Kedai Kek") } }
        };
        var result = service.RetrieveMultiple(query);
        if (result.Entities.Count == 0) { Console.WriteLine("客户不存在"); return; }

        var account = result.Entities[0];
        var id = account.Id;
        Console.WriteLine($"找到客户: {account.GetAttributeValue<string>("name")} ({id})");
        Console.WriteLine($"当前 AccountCategory: {account.GetAttributeValue<OptionSetValue>("mcs_accountcategory")?.Value}");
        Console.WriteLine($"当前 AccountLevel: {account.GetAttributeValue<OptionSetValue>("mcs_accountlevel")?.Value}");
        Console.WriteLine($"当前 AccountType: {account.GetAttributeValue<OptionSetValue>("mcs_accounttype")?.Value}");

        var update = new Entity("account") { Id = id };
        update["mcs_accounttype"] = new OptionSetValue(2);  // 企业
        update["mcs_accountlevel"] = new OptionSetValue(4); // Diamond=S级
        service.Update(update);
        Console.WriteLine("已修改: AccountType=2, AccountLevel=4");
    }

    static void FixScoringCardsUat(ServiceClient service)
    {
        Console.WriteLine("=== 修复 UAT Scoring Card (CategoryId=5) ===");

        // 1. 从 DEV1 读取 CategoryId=5 的正确配置
        Console.WriteLine("步骤1: 从 DEV1 读取 CategoryId=5 配置...");
        var devCs = D365ConnectionFactory.BuildConnectionString("https://dev1.crm5.dynamics.com");
        using var devService = new ServiceClient(devCs);
        if (!devService.IsReady) { Console.WriteLine("DEV1 连接失败"); return; }

        var devQuery = new QueryExpression("mcs_credit_scoringcard")
        {
            ColumnSet = new ColumnSet("mcs_categoryid", "mcs_itemid", "mcs_itemname", "mcs_datatype", "mcs_weight", "mcs_minvalue", "mcs_maxvalue"),
            Criteria = new FilterExpression { Conditions = { new ConditionExpression("mcs_categoryid", ConditionOperator.Equal, 5) } }
        };
        // 使用 mcs_credititem (Lookup) 做 Link，mcs_itemid 是字符串类型无法用于 LinkEntity
        var link1 = new LinkEntity("mcs_credit_scoringcard", "mcs_credit_items", "mcs_credititem", "mcs_credit_itemsid", JoinOperator.LeftOuter)
        {
            Columns = new ColumnSet("mcs_credit_itemsno", "mcs_itemname"),
            EntityAlias = "item"
        };
        devQuery.LinkEntities.Add(link1);
        var devResult = devService.RetrieveMultiple(devQuery);
        Console.WriteLine($"DEV1 CategoryId=5 共 {devResult.Entities.Count} 条");

        // 2. 获取 UAT 的 credit_items GUID 映射
        Console.WriteLine("步骤2: 获取 UAT credit_items 映射...");
        var uatItemsQuery = new QueryExpression("mcs_credit_items")
        {
            ColumnSet = new ColumnSet("mcs_credit_itemsid", "mcs_credit_itemsno", "mcs_itemname")
        };
        var uatItemsResult = service.RetrieveMultiple(uatItemsQuery);
        var uatItemMap = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var it in uatItemsResult.Entities)
        {
            var no = it.GetAttributeValue<string>("mcs_credit_itemsno") ?? "";
            var name = it.GetAttributeValue<string>("mcs_itemname") ?? "";
            if (!string.IsNullOrEmpty(no)) uatItemMap[no] = it.Id;
            if (!string.IsNullOrEmpty(name) && !uatItemMap.ContainsKey(name)) uatItemMap[name] = it.Id;
        }
        Console.WriteLine($"UAT credit_items 共 {uatItemMap.Count} 条");

        // 3. 获取 UAT 上 CategoryId=5 的现有配置
        Console.WriteLine("步骤3: 获取 UAT CategoryId=5 现有配置...");
        var uatQuery = new QueryExpression("mcs_credit_scoringcard")
        {
            ColumnSet = new ColumnSet("mcs_credit_scoringcardid", "mcs_itemid", "mcs_itemname", "mcs_datatype", "mcs_weight"),
            Criteria = new FilterExpression { Conditions = { new ConditionExpression("mcs_categoryid", ConditionOperator.Equal, 5) } }
        };
        var uatResult = service.RetrieveMultiple(uatQuery);
        Console.WriteLine($"UAT CategoryId=5 共 {uatResult.Entities.Count} 条");

        int fixCount = 0;
        int skipCount = 0;
        int delCount = 0;

        // 4. 先删除 UAT 上 mcs_itemid 为空的无效配置
        foreach (var uatCard in uatResult.Entities)
        {
            var uatItemRef = uatCard.GetAttributeValue<EntityReference>("mcs_itemid");
            if (uatItemRef == null)
            {
                service.Delete("mcs_credit_scoringcard", uatCard.Id);
                Console.WriteLine($"  [-] 删除无效配置: {uatCard.Id}");
                delCount++;
            }
        }

        // 5. 从 DEV1 同步配置到 UAT
        foreach (var devCard in devResult.Entities)
        {
            string itemNo = "";
            string itemName = devCard.GetAttributeValue<string>("mcs_itemname") ?? "";

            var aliasedNo = devCard.GetAttributeValue<AliasedValue>("item.mcs_credit_itemsno");
            if (aliasedNo?.Value != null) itemNo = aliasedNo.Value.ToString() ?? "";

            var aliasedName = devCard.GetAttributeValue<AliasedValue>("item.mcs_itemname");
            if (aliasedName?.Value != null && string.IsNullOrEmpty(itemName)) itemName = aliasedName.Value.ToString() ?? "";

            if (string.IsNullOrEmpty(itemNo) && !string.IsNullOrEmpty(itemName))
            {
                itemNo = itemName;
            }

            if (!uatItemMap.TryGetValue(itemNo, out Guid uatItemId) && !uatItemMap.TryGetValue(itemName, out uatItemId))
            {
                Console.WriteLine($"  ⚠️ 跳过: 找不到对应 credit_items [{itemNo}/{itemName}]");
                skipCount++;
                continue;
            }

            var newCard = new Entity("mcs_credit_scoringcard");
            newCard["mcs_categoryid"] = new OptionSetValue(5);
            newCard["mcs_itemid"] = itemNo;  // String 类型：评分项目编码
            newCard["mcs_credititem"] = new EntityReference("mcs_credit_items", uatItemId);  // Lookup 类型
            newCard["mcs_itemname"] = itemName;

            var dt = devCard.GetAttributeValue<OptionSetValue>("mcs_datatype");
            if (dt != null) newCard["mcs_datatype"] = dt;

            var wt = devCard.GetAttributeValue<int?>("mcs_weight");
            if (wt.HasValue) newCard["mcs_weight"] = wt.Value;

            var minv = devCard.GetAttributeValue<decimal?>("mcs_minvalue");
            if (minv.HasValue) newCard["mcs_minvalue"] = minv.Value;

            var maxv = devCard.GetAttributeValue<decimal?>("mcs_maxvalue");
            if (maxv.HasValue) newCard["mcs_maxvalue"] = maxv.Value;

            try
            {
                service.Create(newCard);
                Console.WriteLine($"  [+] 创建: [{itemNo}] {itemName}");
                fixCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ 创建失败 [{itemNo}] {itemName}: {ex.Message}");
                skipCount++;
            }
        }

        Console.WriteLine($"\n=== 修复完成 ===");
        Console.WriteLine($"删除: {delCount} 条");
        Console.WriteLine($"创建: {fixCount} 条");
        Console.WriteLine($"跳过: {skipCount} 条");
    }

    /// <summary>
    /// 重建DEV环境评分卡配置
    /// 解决mcs_itemid/mcs_datatype/mcs_cardname为空的问题
    /// 原理：删除现有配置 → 通过LinkEntity读取credit_items信息 → 重新创建并填充缺失字段
    /// </summary>
    static void FixDevScoringCards(ServiceClient service)
    {
        Console.WriteLine("\n=== 重建DEV评分卡配置 ===");

        // 1. 读取现有配置（通过LinkEntity获取credit_items的datatype和itemsno）
        var query = new QueryExpression("mcs_credit_scoringcard")
        {
            ColumnSet = new ColumnSet("mcs_credit_scoringcardno", "mcs_categoryid", "mcs_typeid", "mcs_weight", "mcs_minvalue", "mcs_maxvalue", "mcs_credititem", "mcs_listvalue"),
            Orders = { new OrderExpression("mcs_categoryid", OrderType.Ascending) }
        };
        var link = new LinkEntity("mcs_credit_scoringcard", "mcs_credit_items", "mcs_credititem", "mcs_credit_itemsid", JoinOperator.LeftOuter)
        {
            Columns = new ColumnSet("mcs_credit_itemsno", "mcs_itemname", "mcs_datatype"),
            EntityAlias = "item"
        };
        query.LinkEntities.Add(link);
        var result = service.RetrieveMultiple(query);
        Console.WriteLine($"读取到现有配置: {result.Entities.Count} 条");

        var configs = new List<(int cat, int type, int weight, decimal? min, decimal? max, string itemNo, string itemName, Guid creditItemId, string listValue, int dataType)>();
        foreach (var r in result.Entities)
        {
            var cat = r.GetAttributeValue<OptionSetValue>("mcs_categoryid")?.Value ?? 0;
            var type = r.GetAttributeValue<OptionSetValue>("mcs_typeid")?.Value ?? 0;
            var weight = r.GetAttributeValue<int?>("mcs_weight") ?? 0;
            var minV = r.GetAttributeValue<decimal?>("mcs_minvalue");
            var maxV = r.GetAttributeValue<decimal?>("mcs_maxvalue");
            var itemRef = r.GetAttributeValue<EntityReference>("mcs_credititem");
            var listValue = r.GetAttributeValue<string>("mcs_listvalue") ?? "";

            string itemNo = "";
            var aliasedNo = r.GetAttributeValue<AliasedValue>("item.mcs_credit_itemsno");
            if (aliasedNo?.Value != null) itemNo = aliasedNo.Value.ToString() ?? "";
            string itemName = "";
            var aliasedName = r.GetAttributeValue<AliasedValue>("item.mcs_itemname");
            if (aliasedName?.Value != null) itemName = aliasedName.Value.ToString() ?? "";
            int dataType = 0;
            var aliasedDt = r.GetAttributeValue<AliasedValue>("item.mcs_datatype");
            if (aliasedDt?.Value is OptionSetValue osv) dataType = osv.Value;

            configs.Add((cat, type, weight, minV, maxV, itemNo, itemName, itemRef?.Id ?? Guid.Empty, listValue, dataType));
        }

        // 2. 获取credit_items GUID映射
        var itemQuery = new QueryExpression("mcs_credit_items")
        {
            ColumnSet = new ColumnSet("mcs_credit_itemsid", "mcs_credit_itemsno", "mcs_itemname")
        };
        var itemResult = service.RetrieveMultiple(itemQuery);
        var itemMap = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var it in itemResult.Entities)
        {
            var id = it.Id;
            var no = it.GetAttributeValue<string>("mcs_credit_itemsno") ?? "";
            var name = it.GetAttributeValue<string>("mcs_itemname") ?? "";
            if (!string.IsNullOrEmpty(no)) itemMap[no] = id;
            if (!string.IsNullOrEmpty(name)) itemMap[name] = id;
        }
        Console.WriteLine($"credit_items映射: {itemMap.Count} 条");

        // 3. 删除现有记录
        Console.WriteLine("\n步骤1: 删除现有评分卡配置...");
        var delQuery = new QueryExpression("mcs_credit_scoringcard") { ColumnSet = new ColumnSet("mcs_credit_scoringcardid") };
        var delResult = service.RetrieveMultiple(delQuery);
        int delCount = 0;
        foreach (var d in delResult.Entities)
        {
            try { service.Delete("mcs_credit_scoringcard", d.Id); delCount++; }
            catch (Exception ex) { Console.WriteLine($"  删除失败 {d.Id}: {ex.Message}"); }
        }
        Console.WriteLine($"删除完成: {delCount}/{delResult.Entities.Count}");

        // 4. 重新创建（补全所有字段）
        Console.WriteLine("\n步骤2: 重建评分卡配置...");
        int createCount = 0;
        int skipCount = 0;
        foreach (var cfg in configs)
        {
            if (!itemMap.TryGetValue(cfg.itemNo, out Guid itemId) && !itemMap.TryGetValue(cfg.itemName, out itemId))
            {
                Console.WriteLine($"  跳过: 找不到credit_items [{cfg.itemNo}/{cfg.itemName}]");
                skipCount++;
                continue;
            }

            var card = new Entity("mcs_credit_scoringcard");
            card["mcs_categoryid"] = new OptionSetValue(cfg.cat);
            card["mcs_typeid"] = new OptionSetValue(cfg.type);
            card["mcs_itemid"] = cfg.itemNo;                          // 评分项目编码
            card["mcs_credititem"] = new EntityReference("mcs_credit_items", itemId);  // Lookup
            card["mcs_itemname"] = cfg.itemName;                      // 评分项目名称
            card["mcs_cardname"] = cfg.itemName;                      // 评分卡名称
            if (cfg.dataType != 0) card["mcs_datatype"] = new OptionSetValue(cfg.dataType);  // 数据类型
            card["mcs_weight"] = cfg.weight;
            if (cfg.min.HasValue) card["mcs_minvalue"] = cfg.min.Value;
            if (cfg.max.HasValue) card["mcs_maxvalue"] = cfg.max.Value;
            if (!string.IsNullOrEmpty(cfg.listValue)) card["mcs_listvalue"] = cfg.listValue;

            try
            {
                service.Create(card);
                Console.WriteLine($"  + [{cfg.cat}] {cfg.itemNo} ({cfg.itemName})");
                createCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  x [{cfg.cat}] {cfg.itemNo}: {ex.Message}");
                skipCount++;
            }
        }

        Console.WriteLine($"\n=== 完成 ===");
        Console.WriteLine($"删除: {delCount} 条");
        Console.WriteLine($"创建: {createCount} 条");
        Console.WriteLine($"跳过: {skipCount} 条");
    }

    static void QueryCreditRecordTags(ServiceClient service, string scoreId)
    {
        Console.WriteLine($"=== 查询标签详情: {scoreId} ===");

        // 1. 查客户属性
        var aQ = new QueryExpression("account")
        {
            ColumnSet = new ColumnSet("mcs_accountcategory", "mcs_accountlevel", "mcs_accounttype"),
            Criteria = new FilterExpression { Conditions = { new ConditionExpression("name", ConditionOperator.Equal, "LTC客户-1") } }
        };
        var accs = service.RetrieveMultiple(aQ);
        if (accs.Entities.Count > 0)
        {
            var a = accs.Entities[0];
            Console.WriteLine($"客户属性: category={a.GetAttributeValue<Microsoft.Xrm.Sdk.OptionSetValue>("mcs_accountcategory")?.Value}, level={a.GetAttributeValue<Microsoft.Xrm.Sdk.OptionSetValue>("mcs_accountlevel")?.Value}, type={a.GetAttributeValue<Microsoft.Xrm.Sdk.OptionSetValue>("mcs_accounttype")?.Value}");
        }

        // 2. 查评分卡配置
        Console.WriteLine("\n=== 评分卡配置(SA级新客户 categoryId=2) ===");
        var cQ = new QueryExpression("mcs_credit_scoringcard")
        {
            ColumnSet = new ColumnSet("mcs_credit_scoringcardno", "mcs_categoryid", "mcs_itemname", "mcs_weight"),
            Orders = { new OrderExpression("mcs_categoryid", OrderType.Ascending) }
        };
        var cs = service.RetrieveMultiple(cQ);
        int saCount = 0;
        foreach (var c in cs.Entities)
        {
            int? cat = c.GetAttributeValue<Microsoft.Xrm.Sdk.OptionSetValue>("mcs_categoryid")?.Value;
            string name = c.GetAttributeValue<string>("mcs_itemname") ?? "";
            string no = c.GetAttributeValue<string>("mcs_credit_scoringcardno") ?? "";
            int? w = c.GetAttributeValue<int?>("mcs_weight");
            if (cat == 2)
            {
                saCount++;
                Console.WriteLine($"  {no} | {name} | 权重={w}");
            }
        }
        Console.WriteLine($"SA级新客户配置共 {saCount} 项");

        // 3. 查标签
        Console.WriteLine($"\n=== {scoreId} 标签明细 ===");
        var rQ = new QueryExpression("mcs_credit_record")
        {
            ColumnSet = new ColumnSet("mcs_credit_recordid"),
            Criteria = new FilterExpression { Conditions = { new ConditionExpression("mcs_scoreid", ConditionOperator.Equal, scoreId) } }
        };
        var rs = service.RetrieveMultiple(rQ);
        if (rs.Entities.Count == 0)
        {
            Console.WriteLine("记录不存在");
            return;
        }

        Guid rid = rs.Entities[0].Id;
        var tQ = new QueryExpression("mcs_customer_tag")
        {
            ColumnSet = new ColumnSet("mcs_credit_item", "mcs_itemvalue1", "mcs_itemtxtvalue1", "mcs_itemintvalue1"),
            Criteria = new FilterExpression { Conditions = { new ConditionExpression("mcs_credit_record", ConditionOperator.Equal, rid) } }
        };
        tQ.LinkEntities.Add(new LinkEntity("mcs_customer_tag", "mcs_credit_items", "mcs_credit_item", "mcs_credit_itemsid", JoinOperator.LeftOuter)
        {
            Columns = new ColumnSet("mcs_credit_itemsno", "mcs_itemname"),
            EntityAlias = "item"
        });
        var ts = service.RetrieveMultiple(tQ);
        foreach (var t in ts.Entities)
        {
            string itemName = "N/A";
            string itemNo = "N/A";
            if (t.Attributes.Contains("item.mcs_itemname"))
            {
                var av = t["item.mcs_itemname"] as Microsoft.Xrm.Sdk.AliasedValue;
                if (av?.Value != null) itemName = av.Value.ToString()!;
            }
            if (t.Attributes.Contains("item.mcs_credit_itemsno"))
            {
                var av = t["item.mcs_credit_itemsno"] as Microsoft.Xrm.Sdk.AliasedValue;
                if (av?.Value != null) itemNo = av.Value.ToString()!;
            }
            string v1 = t.GetAttributeValue<string>("mcs_itemvalue1") ?? "N/A";
            Console.WriteLine($"  {itemNo,-18} | {itemName,-12} | value1={v1}");
        }
        Console.WriteLine($"共 {ts.Entities.Count} 条标签");
    }

    static void QueryCreditItems(ServiceClient service)
    {
        Console.WriteLine("=== 评分项目表 ===");
        var q = new QueryExpression("mcs_credit_items")
        {
            ColumnSet = new ColumnSet("mcs_credit_itemsno", "mcs_itemname", "mcs_datatype", "mcs_group"),
            Orders = { new OrderExpression("mcs_group", OrderType.Ascending) }
        };
        var rs = service.RetrieveMultiple(q);
        foreach (var e in rs.Entities)
        {
            string no = e.GetAttributeValue<string>("mcs_credit_itemsno") ?? "";
            string name = e.GetAttributeValue<string>("mcs_itemname") ?? "";
            int? dt = e.GetAttributeValue<OptionSetValue>("mcs_datatype")?.Value;
            int? gp = e.GetAttributeValue<OptionSetValue>("mcs_group")?.Value;
            Console.WriteLine($"{no,-18} | {name,-12} | 类型={dt} | 分组={gp}");
        }
    }

    static void QueryEnums(ServiceClient service, string itemNo)
    {
        Console.WriteLine($"=== 枚举值表: {itemNo} ===");

        // 先查评分项目ID
        var itemQ = new QueryExpression("mcs_credit_items")
        {
            ColumnSet = new ColumnSet("mcs_credit_itemsid"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("mcs_credit_itemsno", ConditionOperator.Equal, itemNo) }
            }
        };
        var items = service.RetrieveMultiple(itemQ);
        if (items.Entities.Count == 0)
        {
            Console.WriteLine($"未找到评分项目 {itemNo}");
            return;
        }
        Guid itemId = items.Entities[0].Id;

        // 查枚举值（用Lookup字段过滤）
        var q = new QueryExpression("mcs_credititem_value")
        {
            ColumnSet = new ColumnSet("mcs_listname", "mcs_listvalue"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("mcs_credititemno", ConditionOperator.Equal, itemId) }
            },
            Orders = { new OrderExpression("mcs_listvalue", OrderType.Ascending) }
        };
        var rs = service.RetrieveMultiple(q);
        if (rs.Entities.Count == 0)
        {
            Console.WriteLine($"未找到 {itemNo} 的枚举值配置");
            return;
        }
        foreach (var e in rs.Entities)
        {
            string name = e.GetAttributeValue<string>("mcs_listname") ?? "";
            string val = e.GetAttributeValue<string>("mcs_listvalue") ?? "";
            Console.WriteLine($"  值={val} | 名称={name}");
        }
    }

    static void MockScores(ServiceClient service)
    {
        Console.WriteLine("=== 虚拟评分卡配置（让当前测试数据匹配得高分）===");

        // 查询SA级新客户评分卡配置
        var q = new QueryExpression("mcs_credit_scoringcard")
        {
            ColumnSet = new ColumnSet("mcs_credit_scoringcardid", "mcs_credit_scoringcardno", "mcs_itemid", "mcs_itemname", "mcs_minvalue", "mcs_maxvalue"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("mcs_categoryid", ConditionOperator.Equal, 2) }
            }
        };
        var cards = service.RetrieveMultiple(q);

        int updated = 0;
        foreach (var card in cards.Entities)
        {
            Guid cardId = card.Id;
            string cardNo = card.GetAttributeValue<string>("mcs_credit_scoringcardno") ?? "";
            string itemCode = card.GetAttributeValue<string>("mcs_itemid") ?? "";

            var update = new Entity("mcs_credit_scoringcard", cardId);
            bool needUpdate = false;

            switch (itemCode)
            {
                case "LatePaymentIndex":
                    // 当前测试数据=2.00，原min=3，改min=0让2.00匹配
                    update["mcs_minvalue"] = 0m;
                    needUpdate = true;
                    Console.WriteLine($"  {cardNo} 迟付指数: min 3→0");
                    break;

                case "CountryRisk":
                    // 清空listvalue，让"无匹配配置默认给分"
                    update["mcs_listvalue"] = null;
                    needUpdate = true;
                    Console.WriteLine($"  {cardNo} 国别风险: listvalue 清空（默认给分）");
                    break;

                case "SectorRisk":
                    // 清空listvalue，让"无匹配配置默认给分"
                    update["mcs_listvalue"] = null;
                    needUpdate = true;
                    Console.WriteLine($"  {cardNo} 行业风险: listvalue 清空（默认给分）");
                    break;

                case "ExternalRating":
                    // listvalue已经是空的，默认给分，无需修改
                    Console.WriteLine($"  {cardNo} 外部评级: listvalue为空，已默认给分");
                    break;
            }

            if (needUpdate)
            {
                service.Update(update);
                updated++;
            }
        }

        Console.WriteLine($"\n✅ 已更新 {updated} 条评分卡配置");
        Console.WriteLine("预期得分：外部评级30 + 迟付指数10 + 国别风险10 + 行业风险10 + 净资产20 = 80分");
        Console.WriteLine("（资产负债率和流动比率是N/A，得0分）");
    }

    /// <summary>
    /// 初始化 Coface 2026 Budget 汇率数据到 mcs_coface_exchange_rate
    /// </summary>
    static void SeedCofaceExchangeRates(ServiceClient service)
    {
        const string entityName = "mcs_coface_exchange_rate";
        Console.WriteLine("=== 初始化 Coface 2026 Budget 汇率数据 ===");

        // 2026 Budget 汇率：1 LC => USD
        // 来源: 海外客户评分卡项目和科法斯接口字段取数反馈表20260408-货币代码与汇率.md
        var rates = new (string Code, string Name, decimal Rate)[]
        {
            ("USD", "US Dollar", 1.00m),
            ("EUR", "Euro", 1.21m),
            ("AUD", "Australian Dollar", 0.6648m),
            ("CNY", "Chinese Yuan", 0.1463m),
            ("HKD", "Hong Kong Dollar", 0.1301m),
            ("INR", "Indian Rupee", 0.0122m),
            ("IDR", "Indonesian Rupiah", 0.0000644m),
            ("JPY", "Japanese Yen", 0.00725m),
            ("KRW", "Korean Won", 0.000769m),
            ("MYR", "Malaysian Ringgit", 0.2479m),
            ("NZD", "New Zealand Dollar", 0.6335m),
            ("PHP", "Philippine Peso", 0.0186m),
            ("PLN", "Polish Zloty", 0.26m),
            ("SGD", "Singapore Dollar", 0.8121m),
            ("TWD", "New Taiwan Dollar", 0.0354m),
            ("THB", "Thai Baht", 0.0322m),
            ("VND", "Vietnamese Dong", 0.0000402m)
        };

        var effectiveDate = new DateTime(2026, 1, 1);
        int created = 0;
        int updated = 0;

        foreach (var (code, name, rate) in rates)
        {
            // 查询是否已存在
            var query = new QueryExpression(entityName)
            {
                ColumnSet = new ColumnSet("mcs_coface_exchange_rateid", "mcs_rate_to_usd"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_currencycode", ConditionOperator.Equal, code),
                        new ConditionExpression("mcs_effectivedate", ConditionOperator.Equal, effectiveDate)
                    }
                },
                TopCount = 1
            };

            var existing = service.RetrieveMultiple(query);
            if (existing.Entities.Count > 0)
            {
                var record = existing.Entities[0];
                var update = new Entity(entityName, record.Id)
                {
                    ["mcs_currencyname"] = name,
                    ["mcs_rate_to_usd"] = rate,
                    ["mcs_isactive"] = true,
                    ["mcs_remark"] = "2026 Budget 汇率，来源：海外客户评分卡项目和科法斯接口字段取数反馈表20260408"
                };
                service.Update(update);
                updated++;
                Console.WriteLine($"  [更新] {code} ({name}) => {rate}");
            }
            else
            {
                var create = new Entity(entityName)
                {
                    ["mcs_currencycode"] = code,
                    ["mcs_currencyname"] = name,
                    ["mcs_rate_to_usd"] = rate,
                    ["mcs_effectivedate"] = effectiveDate,
                    ["mcs_isactive"] = true,
                    ["mcs_remark"] = "2026 Budget 汇率，来源：海外客户评分卡项目和科法斯接口字段取数反馈表20260408"
                };
                service.Create(create);
                created++;
                Console.WriteLine($"  [新建] {code} ({name}) => {rate}");
            }
        }

        Console.WriteLine($"\n✅ Coface 汇率初始化完成：新建 {created} 条，更新 {updated} 条");
    }

    /// <summary>
    /// 初始化 Coface NACE 行业映射数据到 mcs_coface_nace_mapping
    /// </summary>
    static void SeedCofaceNaceMappings(ServiceClient service)
    {
        const string entityName = "mcs_coface_nace_mapping";
        Console.WriteLine("=== 初始化 Coface NACE 行业映射数据 ===");

        // NACE Rev.2.1 Division → 三一行业映射
        // 来源：Urba360Parser 原硬编码映射（基于 Joyce 提供的材料）
        // 注意：23 单独映射为"商混"，与 10-33 的"制造业"范围重叠，精确匹配优先
        var mappings = new (int From, int To, string DivisionName, string SanyIndustry)[]
        {
            (1, 1, "Crop and animal production, hunting and related service activities", "农业"),
            (2, 2, "Forestry and logging", "林业"),
            (5, 9, "Mining and quarrying", "矿业"),
            (10, 33, "Manufacturing", "制造业"),
            (23, 23, "Manufacture of other non-metallic mineral products", "商混"),
            (41, 42, "Construction of buildings / Civil engineering", "建工"),
            (43, 43, "Specialised construction activities", "吊装"),
            (49, 51, "Land transport / Water transport / Air transport", "集装箱运力"),
            (52, 52, "Warehousing and support activities for transportation", "港务"),
            (77, 77, "Rental and leasing activities", "租赁")
        };

        var effectiveDate = new DateTime(2026, 1, 1);
        int created = 0;
        int updated = 0;

        foreach (var (from, to, divisionName, sanyIndustry) in mappings)
        {
            // 查询是否已存在（按 from/to/sanyIndustry 唯一）
            var query = new QueryExpression(entityName)
            {
                ColumnSet = new ColumnSet("mcs_coface_nace_mappingid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_nacedivisionfrom", ConditionOperator.Equal, from),
                        new ConditionExpression("mcs_nacedivisionto", ConditionOperator.Equal, to),
                        new ConditionExpression("mcs_sanyindustry", ConditionOperator.Equal, sanyIndustry)
                    }
                },
                TopCount = 1
            };

            var existing = service.RetrieveMultiple(query);
            if (existing.Entities.Count > 0)
            {
                var record = existing.Entities[0];
                var update = new Entity(entityName, record.Id)
                {
                    ["mcs_nacedivisionname"] = divisionName,
                    ["mcs_isactive"] = true,
                    ["mcs_effectivedate"] = effectiveDate,
                    ["mcs_remark"] = "NACE Rev.2.1 Division → 三一行业映射，来源：原 Urba360Parser 硬编码配置"
                };
                service.Update(update);
                updated++;
                Console.WriteLine($"  [更新] Division {from:D2}-{to:D2} ({divisionName}) => {sanyIndustry}");
            }
            else
            {
                var create = new Entity(entityName)
                {
                    ["mcs_nacedivisionfrom"] = from,
                    ["mcs_nacedivisionto"] = to,
                    ["mcs_nacedivisionname"] = divisionName,
                    ["mcs_sanyindustry"] = sanyIndustry,
                    ["mcs_isactive"] = true,
                    ["mcs_effectivedate"] = effectiveDate,
                    ["mcs_remark"] = "NACE Rev.2.1 Division → 三一行业映射，来源：原 Urba360Parser 硬编码配置"
                };
                service.Create(create);
                created++;
                Console.WriteLine($"  [新建] Division {from:D2}-{to:D2} ({divisionName}) => {sanyIndustry}");
            }
        }

        Console.WriteLine($"\n✅ Coface NACE 行业映射初始化完成：新建 {created} 条，更新 {updated} 条");
    }

    static void AddSectorScoringCard(ServiceClient service)
    {
        Console.WriteLine("=== 添加行业属性到SA级新客户评分卡 ===");

        // 1. 查找Sectors评分项目
        var itemQuery = new QueryExpression("mcs_credit_items")
        {
            ColumnSet = new ColumnSet("mcs_credit_itemsid", "mcs_itemname", "mcs_datatype"),
            Criteria = new FilterExpression()
            {
                Conditions = { new ConditionExpression("mcs_credit_itemsno", ConditionOperator.Equal, "Sectors") }
            }
        };
        var items = service.RetrieveMultiple(itemQuery);
        if (items.Entities.Count == 0)
        {
            Console.WriteLine("❌ 未找到Sectors评分项目");
            return;
        }
        var sectorItem = items.Entities[0];
        Guid sectorItemId = sectorItem.Id;
        Console.WriteLine($"✅ 找到行业属性评分项目: ID={sectorItemId}, 名称={sectorItem.GetAttributeValue<string>("mcs_itemname")}");

        // 2. 检查是否已存在SA级+行业属性的配置
        var cardQuery = new QueryExpression("mcs_credit_scoringcard")
        {
            ColumnSet = new ColumnSet("mcs_credit_scoringcardid"),
            Criteria = new FilterExpression()
            {
                Conditions =
                {
                    new ConditionExpression("mcs_categoryid", ConditionOperator.Equal, 2), // SA级新客户
                    new ConditionExpression("mcs_itemid", ConditionOperator.Equal, sectorItemId)
                }
            }
        };
        var existing = service.RetrieveMultiple(cardQuery);
        if (existing.Entities.Count > 0)
        {
            Console.WriteLine("⚠️ SA级评分卡已包含行业属性配置，无需重复添加");
            return;
        }

        // 3. 查询当前SA级配置的最大序号
        var maxQuery = new QueryExpression("mcs_credit_scoringcard")
        {
            ColumnSet = new ColumnSet("mcs_credit_scoringcardno"),
            Criteria = new FilterExpression()
            {
                Conditions = { new ConditionExpression("mcs_categoryid", ConditionOperator.Equal, 2) }
            },
            Orders = { new OrderExpression("mcs_credit_scoringcardno", OrderType.Descending) }
        };
        var maxResult = service.RetrieveMultiple(maxQuery);
        int seq = 8;
        if (maxResult.Entities.Count > 0)
        {
            string lastNo = maxResult.Entities[0].GetAttributeValue<string>("mcs_credit_scoringcardno") ?? "SA002-007";
            if (lastNo.StartsWith("SA002-") && int.TryParse(lastNo.Substring(6), out int n))
            {
                seq = n + 1;
            }
        }
        string cardNo = $"SA002-{seq:D3}";

        // 4. 创建评分卡配置
        var card = new Entity("mcs_credit_scoringcard");
        card["mcs_credit_scoringcardno"] = cardNo;
        card["mcs_categoryid"] = new OptionSetValue(2); // SA级新客户
        card["mcs_typeid"] = new OptionSetValue(3); // 宏观指标 (同CountryRisk/SectorRisk)
        card["mcs_itemid"] = "Sectors"; // String类型
        card["mcs_itemname"] = "行业属性";
        card["mcs_datatype"] = new OptionSetValue(100000001); // 定性
        card["mcs_weight"] = 0; // 权重0，不影响总分
        card["mcs_listvalue"] = null; // Lookup类型，先不填

        try
        {
            Guid cardId = service.Create(card);
            Console.WriteLine($"✅ 创建成功! 评分卡配置ID={cardId}, 编码={cardNo}");
            Console.WriteLine($"   评分卡类型=SA级新客户, 评分项目=行业属性, 权重=0");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 创建失败: {ex.Message}");
        }
    }

    static void CreateFromJson(EntityManager manager, string jsonFile)
    {
        if (!File.Exists(jsonFile))
        {
            Console.WriteLine($"文件不存在: {jsonFile}");
            return;
        }

        var definition = EntityDefinition.LoadFromJson(jsonFile);
        
        // 检查实体是否已存在
        if (manager.EntityExists(definition.EntityName))
        {
            Console.WriteLine($"实体 {definition.EntityName} 已存在，跳过创建");
        }
        else
        {
            // 创建实体
            manager.CreateEntity(
                definition.EntityName,
                definition.DisplayName,
                definition.PrimaryAttribute,
                definition.PrimaryAttributeDisplayName,
                definition.PrimaryAttributeLength
            );
        }

        // 创建字段
        Console.WriteLine($"\n创建字段...");
        int success = 0;
        int failed = 0;
        int skipped = 0;

        var existingFields = manager.GetFields(definition.EntityName);
        var existingFieldNames = existingFields.Select(f => f.LogicalName).ToHashSet();

        foreach (var field in definition.Fields)
        {
            // 跳过已存在的字段
            if (existingFieldNames.Contains(field.SchemaName.ToLower()))
            {
                Console.WriteLine($"  ⊘ {field.SchemaName} ({field.DisplayName}) - 已存在");
                skipped++;
                continue;
            }

            try
            {
                switch (field.Type.ToLower())
                {
                    case "string":
                        manager.CreateStringField(definition.EntityName, field.SchemaName, field.DisplayName, field.Description, field.MaxLength ?? 100, field.Required);
                        break;
                    case "memo":
                        manager.CreateMemoField(definition.EntityName, field.SchemaName, field.DisplayName, field.Description, field.MaxLength ?? 4000);
                        break;
                    case "integer":
                        manager.CreateIntegerField(definition.EntityName, field.SchemaName, field.DisplayName, field.Description, field.MinValue ?? 0, field.MaxValue ?? 100);
                        break;
                    case "decimal":
                        manager.CreateDecimalField(definition.EntityName, field.SchemaName, field.DisplayName, field.Description, field.MinDecimalValue ?? 0, field.MaxDecimalValue ?? 999999.99m, field.Precision ?? 2);
                        break;
                    case "money":
                        manager.CreateMoneyField(definition.EntityName, field.SchemaName, field.DisplayName, field.Description, field.MinDecimalValue ?? 0, field.MaxDecimalValue ?? 1000000, field.Precision ?? 2);
                        break;
                    case "datetime":
                        manager.CreateDateTimeField(definition.EntityName, field.SchemaName, field.DisplayName, field.Description, field.DateOnly ?? true);
                        break;
                    case "picklist":
                        manager.CreatePicklistField(definition.EntityName, field.SchemaName, field.DisplayName, field.Description, field.Options ?? new Dictionary<string, int>());
                        break;
                    case "boolean":
                        manager.CreateBooleanField(definition.EntityName, field.SchemaName, field.DisplayName, field.Description, field.TrueLabel ?? "是", field.FalseLabel ?? "否");
                        break;
                    case "lookup":
                        manager.CreateLookupField(definition.EntityName, field.SchemaName, field.DisplayName, field.Description, field.TargetEntity, field.TargetEntityDisplayName);
                        break;
                    default:
                        Console.WriteLine($"  ✗ {field.SchemaName} - 未知类型: {field.Type}");
                        failed++;
                        continue;
                }
                success++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ {field.SchemaName} 失败: {ex.Message}");
                failed++;
            }
        }

        Console.WriteLine($"\n  完成: {success} 成功, {failed} 失败, {skipped} 跳过");

        // 发布
        if (definition.AutoPublish)
        {
            Console.WriteLine($"\n发布实体...");
            manager.PublishEntity(definition.EntityName);
        }
    }

    static void CheckSolution(EntityManager manager, string solutionName)
    {
        Console.WriteLine($"检查解决方案: {solutionName}\n");
        
        var entities = manager.GetSolutionEntities(solutionName);
        Console.WriteLine($"实体数量: {entities.Count}\n");
        
        foreach (var (logicalName, entityId) in entities)
        {
            Console.WriteLine($"  - {logicalName} (ID: {entityId})");
        }
    }

    static void ListFields(EntityManager manager, string entityName)
    {
        Console.WriteLine($"实体 {entityName} 的字段列表:\n");
        
        var fields = manager.GetFields(entityName);
        foreach (var field in fields.OrderBy(f => f.LogicalName))
        {
            string typeName = field.AttributeType?.ToString() ?? "Unknown";
            string displayName = field.DisplayName?.UserLocalizedLabel?.Label ?? "";
            Console.WriteLine($"  - {field.LogicalName,-30} {typeName,-15} {displayName}");
        }
        
        Console.WriteLine($"\n总计: {fields.Count} 个字段");
    }

    static void UpdateForm(EntityManager manager, string entityName)
    {
        var fields = new Dictionary<string, string>();
        
        switch (entityName)
        {
            case "mcs_credit_items":
                fields = new Dictionary<string, string>
                {
                    { "mcs_credit_itemsno", "评分项目编码" },
                    { "mcs_group", "评分项目分类" },
                    { "mcs_itemdesc", "评分项目说明" },
                    { "mcs_datatype", "数据类型" },
                    { "mcs_source", "内外部" },
                    { "mcs_validate", "人工补录" },
                    { "mcs__3p", "外部提供" }
                };
                break;

            case "mcs_credit_scoringcard":
                fields = new Dictionary<string, string>
                {
                    { "mcs_credit_scoringcardno", "评分分档编码" },
                    { "mcs_categoryid", "评分卡类型" },
                    { "mcs_cardname", "评分卡名称" },
                    { "mcs_typeid", "评分项目分类" },
                    { "mcs_credititem", "评分项目" },
                    { "mcs_itemid", "评分项目编码" },
                    { "mcs_itemname", "评分项目名称" },
                    { "mcs_datatype", "数据类型" },
                    { "mcs_listvalue", "定性项目值" },
                    { "mcs_minvalue", "定量最小值（含）" },
                    { "mcs_maxvalue", "定量最大值（不含）" },
                    { "mcs_weight", "赋分" }
                };
                break;

            case "mcs_credititem_value":
                fields = new Dictionary<string, string>
                {
                    { "mcs_credititemno", "评分项目编码" },
                    { "mcs_listvalue", "选择项编码" }
                };
                break;

            case "mcs_customer_file":
                fields = new Dictionary<string, string>
                {
                    { "mcs_fileid", "文件统一编号" },
                    { "mcs_accountid", "客户编码" },
                    { "mcs_filename", "文件名称" },
                    { "mcs_filetype", "文件分类" },
                    { "mcs_filebyte", "文件信息流" },
                    { "mcs_filedate", "文件上传日期" },
                    { "mcs_api_fileid", "外部附件接口ID" },
                    { "mcs_api_status", "外部附件接口状态" },
                    { "mcs_api_msg", "外部附件接口消息" }
                };
                break;

            case "mcs_customer_tag":
                fields = new Dictionary<string, string>
                {
                    { "mcs_credit_record", "信用评估" },
                    { "mcs_accountid", "客户编码" },
                    { "mcs_credit_item", "评分项目" },
                    { "mcs_itemid", "指标编码" },
                    { "mcs_datatype", "数据类型" },
                    { "mcs_group", "评分项目分类" },
                    { "mcs_itemdesc", "评分项目说明" },
                    { "mcs_isscore", "是否评分" },
                    { "mcs_active", "有效状态" }
                };
                break;

            case "mcs_credit_record":
                fields = new Dictionary<string, string>
                {
                    { "mcs_accountid", "客户编码" },
                    { "mcs_applicant", "申请人" },
                    { "mcs_approvedate", "BPP审批完成日期" },
                    { "mcs_bppid", "BPP审批ID" },
                    { "mcs_bppstatus", "BPP状态" },
                    { "mcs_bppappriver", "BPP审批人" },
                    { "mcs_bpperrormsg", "BPP错误信息" },
                    { "mcs_bpprejectreason", "BPP驳回原因" },
                    { "mcs_api_name", "API名称" },
                    { "mcs_api_status", "API状态" },
                    { "mcs_api_msg", "API消息" },
                    { "mcs_abidate", "数据集成日期" },
                    { "mcs_checkdate", "复核日期" },
                    { "mcs_cofaceid", "科法斯客户代码" },
                    { "mcs_countrycode", "国家编码" },
                    { "mcs_active", "有效状态" }
                };
                break;

            default:
                Console.WriteLine($"暂不支持实体 {entityName} 的表单更新");
                return;
        }
        
        // 先清理 footer 中的错误字段
        manager.CleanFormFooter(entityName);
        
        // 然后添加字段到主 section
        manager.UpdateMainForm(entityName, fields);
    }

    static void CreateCreditItems(EntityManager manager)
    {
        Console.WriteLine("创建评分项目测试数据...");
        manager.CreateCreditItemRecords();
    }

    static void CreateQualitativeEnums(EntityManager manager)
    {
        Console.WriteLine("创建定性评分项目枚举值测试数据...");
        manager.CreateQualitativeEnumRecords();
    }

    static void CheckCreditItems(EntityManager manager)
    {
        Console.WriteLine("检查评分项目数据...");
        manager.CheckCreditItemRecords();
    }

    static void CleanupCreditItems(EntityManager manager)
    {
        Console.WriteLine("清理评分项目重复和空编码记录...");
        manager.CleanupCreditItemRecords();
    }

    static void AddFieldsToEntity(EntityManager manager, string entityName)
    {
        Console.WriteLine($"为 {entityName} 添加字段...");

        if (entityName == "account")
        {
            // 客户表新增字段（信用评估项目需要）
            Console.WriteLine("添加客户表字段（信用评估项目）...");
            
            // 1. mcs_cofaceid - 科法斯客户代码
            manager.CreateStringField("account", "mcs_cofaceid", "科法斯客户代码", 
                "Coface ICON平台唯一企业编码，用于对接Coface客户数据", 30);
            
            // 2. mcs_dealerrank - 经销商分级
            manager.CreatePicklistField("account", "mcs_dealerrank", "经销商分级",
                "经销商分级（1-钻石、2-铂金、3-白银、4-认证、5-意向）",
                new Dictionary<string, int>
                {
                    { "钻石", 1 },
                    { "铂金", 2 },
                    { "白银", 3 },
                    { "认证", 4 },
                    { "意向", 5 }
                });
            
            // 3. mcs_externalrate - 客户信用外部评级
            manager.CreateStringField("account", "mcs_externalrate", "客户信用外部评级",
                "Coface对客户信用评级，采用1-10分（10分最高）", 10);
            
            // 4. mcs_overduemodel - 逾期未回收率模型分
            manager.CreateDecimalField("account", "mcs_overduemodel", "逾期未回收率模型分",
                "逾期未回收率模型分，通过数据模型计算获得", 0, 999.99m, 2);
            
            // 5. mcs_creditscore - 客户信用评分
            manager.CreateDecimalField("account", "mcs_creditscore", "客户信用评分",
                "客户信用分计算结果", 0, 999.99m, 2);
            
            // 6. mcs_creditgrade - 客户等级
            manager.CreateStringField("account", "mcs_creditgrade", "客户等级",
                "客户等级A0-A4（A0最高），用于成交条件矩阵查询", 2);
            
            // 7. mcs_isdd - 重点尽调
            manager.CreateBooleanField("account", "mcs_isdd", "重点尽调",
                "是否需要重点尽调，用于重点尽调模块判断");
            
            // 8. mcs_creditvalid - 信用评估有效状态
            manager.CreateBooleanField("account", "mcs_creditvalid", "信用评估有效状态",
                "信用评估有效状态：1-有效 0-失效");
            
            Console.WriteLine("客户表字段添加完成！");
        }
        else if (entityName == "mcs_customer_file")
        {
            Console.WriteLine("添加客户资信附件表字段...");
            // 关联客户信用评估记录，用于把附件挂在评估记录下
            manager.CreateLookupField("mcs_customer_file", "mcs_credit_recordid", "客户信用评估记录",
                "关联的客户信用评估记录，用于在评估记录表单中管理附件",
                "mcs_credit_record", "客户信用评估记录");
            Console.WriteLine("客户资信附件表字段添加完成！");
        }
        else
        {
            Console.WriteLine($"暂不支持为实体 {entityName} 批量添加字段");
        }
    }

    static void RearrangeForm(EntityManager manager, string entityName)
    {
        Console.WriteLine($"重新排列 {entityName} 窗体...");
        
        if (entityName == "mcs_credit_record")
        {
            var groups = new Dictionary<string, List<(string fieldName, string displayName)>>
            {
                ["基本信息"] = new List<(string, string)>
                {
                    ("mcs_scoreid", "信用评估编码"),
                    ("mcs_accountid", "客户编码"),
                    ("mcs_custname", "客户名称"),
                    ("mcs_custnameen", "客户英文名称"),
                    ("mcs_countrycode", "国家编码"),
                    ("mcs_cofaceid", "科法斯客户代码"),
                },
                ["Coface订单信息"] = new List<(string, string)>
                {
                    ("mcs_urba360id", "URBA订单ID"),
                    ("mcs_urbastatus", "URBA订单状态"),
                    ("mcs_rptorderid", "Report订单ID"),
                    ("mcs_rptstatus", "Report订单状态"),
                    ("mcs_publicationid", "Publication ID"),
                },
                ["状态与评分"] = new List<(string, string)>
                {
                    ("mcs_status", "评估状态"),
                    ("mcs_creditscore", "客户信用评分"),
                    ("mcs_active", "有效状态"),
                    ("mcs_remark", "备注说明"),
                },
                ["人员与日期"] = new List<(string, string)>
                {
                    ("mcs_applicant", "申请人"),
                    ("mcs_initdate", "发起评估日期"),
                    ("mcs_abidate", "数据集成日期"),
                    ("mcs_checkdate", "人工复核日期"),
                    ("mcs_scoredate", "信用评分日期"),
                    ("mcs_approvedate", "BPP审批完成日期"),
                },
                ["API接口信息"] = new List<(string, string)>
                {
                    ("mcs_api_status", "Coface接口返回状态"),
                    ("mcs_api_name", "接口名称"),
                    ("mcs_api_msg", "Coface接口返回信息"),
                },
                ["Coface原始数据"] = new List<(string, string)>
                {
                    ("mcs_urbajson", "Coface URBA360数据"),
                    ("mcs_reportjson", "Coface Report数据"),
                },
                ["BPP审批信息"] = new List<(string, string)>
                {
                    ("mcs_bppstatus", "BPP审批状态"),
                    ("mcs_bppappriver", "当前审批人"),
                    ("mcs_bppid", "BPP工作流ID"),
                    ("mcs_bpperrormsg", "BPP错误信息"),
                    ("mcs_bpprejectreason", "BPP驳回原因"),
                },
            };
            var lookupFields = new HashSet<string> { "mcs_accountid" };
            manager.RearrangeForm(entityName, groups, lookupFields);
        }
        else if (entityName == "mcs_credit_items")
        {
            var groups = new Dictionary<string, List<(string fieldName, string displayName)>>
            {
                ["基本信息"] = new List<(string, string)>
                {
                    ("mcs_credit_itemsno", "评分项目编码"),
                    ("mcs_itemname", "评分项目名称"),
                    ("mcs_itemdesc", "评分项目说明"),
                },
                ["分类与属性"] = new List<(string, string)>
                {
                    ("mcs_group", "评分项目分类"),
                    ("mcs_datatype", "数据类型"),
                    ("mcs_source", "内外部"),
                    ("mcs_validate", "人工补录"),
                    ("mcs__3p", "外部提供"),
                },
            };
            manager.RearrangeForm(entityName, groups);
        }
        else if (entityName == "mcs_credit_scoringcard")
        {
            var groups = new Dictionary<string, List<(string fieldName, string displayName)>>
            {
                ["基本信息"] = new List<(string, string)>
                {
                    ("mcs_credit_scoringcardno", "评分分档编码"),
                    ("mcs_cardname", "评分卡名称"),
                    ("mcs_categoryid", "评分卡类型"),
                },
                ["评分项目"] = new List<(string, string)>
                {
                    ("mcs_typeid", "评分项目分类"),
                    ("mcs_credititem", "评分项目"),
                    ("mcs_itemid", "评分项目编码"),
                    ("mcs_itemname", "评分项目名称"),
                    ("mcs_datatype", "数据类型"),
                },
                ["分值配置"] = new List<(string, string)>
                {
                    ("mcs_listvalue", "定性项目值"),
                    ("mcs_minvalue", "定量最小值（含）"),
                    ("mcs_maxvalue", "定量最大值（不含）"),
                    ("mcs_weight", "赋分"),
                },
            };
            var lookupFields = new HashSet<string> { "mcs_credititem", "mcs_listvalue" };
            var lookupFilterMap = new Dictionary<string, (string dependentField, string dependentEntity, string filterRelationship)>
            {
                ["mcs_listvalue"] = ("mcs_credit_scoringcard.mcs_credititem", "mcs_credit_items", "mcs_credit_items_mcs_credititem_value_mcs_credititemno")
            };
            manager.RearrangeForm(entityName, groups, lookupFields, lookupFilterMap);
        }
        else if (entityName == "mcs_customer_file")
        {
            var groups = new Dictionary<string, List<(string fieldName, string displayName)>>
            {
                ["基本信息"] = new List<(string, string)>
                {
                    ("mcs_fileid", "文件统一编号"),
                    ("mcs_accountid", "客户编码"),
                    ("mcs_filename", "文件名称"),
                    ("mcs_filetype", "文件分类"),
                },
                ["文件内容"] = new List<(string, string)>
                {
                    ("mcs_filebyte", "文件信息流"),
                    ("mcs_filedate", "文件上传日期"),
                },
                ["接口信息"] = new List<(string, string)>
                {
                    ("mcs_api_fileid", "外部附件接口ID"),
                    ("mcs_api_status", "外部附件接口状态"),
                    ("mcs_api_msg", "外部附件接口消息"),
                },
            };
            var lookupFields = new HashSet<string> { "mcs_accountid" };
            manager.RearrangeForm(entityName, groups, lookupFields);
        }
        else if (entityName == "mcs_credititem_value")
        {
            var groups = new Dictionary<string, List<(string fieldName, string displayName)>>
            {
                ["基本信息"] = new List<(string, string)>
                {
                    ("mcs_credititemno", "评分项目编码"),
                    ("mcs_listvalue", "选择项编码"),
                    ("mcs_listname", "选择项名称"),
                },
            };
            var lookupFields = new HashSet<string> { "mcs_credititemno" };
            manager.RearrangeForm(entityName, groups, lookupFields);
        }
        else if (entityName == "mcs_customer_tag")
        {
            var groups = new Dictionary<string, List<(string fieldName, string displayName)>>
            {
                ["关联信息"] = new List<(string, string)>
                {
                    ("mcs_scoreid", "信用评估编码"),
                    ("mcs_credit_record", "信用评估"),
                    ("mcs_accountid", "客户编码"),
                },
                ["评分项目"] = new List<(string, string)>
                {
                    ("mcs_credit_item", "评分项目"),
                    ("mcs_itemid", "指标编码"),
                    ("mcs_itemname", "评分项目名称"),
                    ("mcs_datatype", "数据类型"),
                    ("mcs_group", "评分项目分类"),
                    ("mcs_itemdesc", "评分项目说明"),
                },
                ["指标值"] = new List<(string, string)>
                {
                    ("mcs_itemvalue1", "集成指标"),
                    ("mcs_itemvalue2", "复核指标"),
                    ("mcs_itemintvalue1", "集成定量指标"),
                    ("mcs_itemintvalue2", "复核定量指标"),
                    ("mcs_itemtxtvalue1", "集成定性指标"),
                    ("mcs_itemtxtvalue2", "复核定性指标"),
                },
                ["评分与状态"] = new List<(string, string)>
                {
                    ("mcs_isscore", "是否评分"),
                    ("mcs_scorevalue", "得分值"),
                    ("mcs_active", "有效状态"),
                },
            };
            var lookupFields = new HashSet<string> { "mcs_credit_record", "mcs_accountid", "mcs_credit_item" };
            manager.RearrangeForm(entityName, groups, lookupFields);
        }
        else
        {
            Console.WriteLine($"暂不支持实体 {entityName} 的窗体重排");
        }
    }

    static void UpdateLookupView(EntityManager manager, string entityName)
    {
        var fields = new string[] { "mcs_credit_itemsno", "mcs_itemname", "mcs_group", "mcs_datatype" };
        manager.UpdateLookupView(entityName, fields);
    }

    static void UpdateView(EntityManager manager, string entityName)
    {
        var fields = new Dictionary<string, string>();
        
        switch (entityName)
        {
            case "mcs_credititem_value":
                fields = new Dictionary<string, string>
                {
                    { "mcs_credititemno", "评分项目编码" },
                    { "mcs_listvalue", "选择项编码" }
                };
                break;

            case "mcs_credit_items":
                fields = new Dictionary<string, string>
                {
                    { "mcs_credit_itemsno", "评分项目编码" },
                    { "mcs_group", "评分项目分类" },
                    { "mcs_datatype", "数据类型" },
                    { "mcs_source", "内外部" },
                    { "mcs_validate", "人工补录" },
                    { "mcs__3p", "外部提供" }
                };
                break;

            case "mcs_credit_scoringcard":
                fields = new Dictionary<string, string>
                {
                    { "mcs_credit_scoringcardno", "评分分档编码" },
                    { "mcs_categoryid", "评分卡类型" },
                    { "mcs_typeid", "评分项目分类" },
                    { "mcs_itemid", "评分项目编码" },
                    { "mcs_itemname", "评分项目名称" },
                    { "mcs_datatype", "数据类型" },
                    { "mcs_listvalue", "定性项目值" },
                    { "mcs_minvalue", "定量最小值" },
                    { "mcs_maxvalue", "定量最大值" },
                    { "mcs_weight", "赋分" }
                };
                break;

            case "mcs_customer_file":
                fields = new Dictionary<string, string>
                {
                    { "mcs_fileid", "文件统一编号" },
                    { "mcs_accountid", "客户编码" },
                    { "mcs_filename", "文件名称" },
                    { "mcs_filetype", "文件分类" },
                    { "mcs_filedate", "文件上传日期" },
                    { "mcs_api_status", "外部接口状态" }
                };
                break;

            case "mcs_customer_tag":
                fields = new Dictionary<string, string>
                {
                    { "mcs_credit_record", "信用评估" },
                    { "mcs_accountid", "客户编码" },
                    { "mcs_credit_item", "评分项目" },
                    { "mcs_itemid", "指标编码" },
                    { "mcs_datatype", "数据类型" },
                    { "mcs_group", "评分项目分类" },
                    { "mcs_isscore", "是否评分" },
                    { "mcs_active", "有效状态" }
                };
                break;

            case "mcs_credit_record":
                fields = new Dictionary<string, string>
                {
                    { "mcs_accountid", "客户编码" },
                    { "mcs_applicant", "申请人" },
                    { "mcs_approvedate", "BPP审批完成日期" },
                    { "mcs_bppstatus", "BPP状态" },
                    { "mcs_api_status", "API状态" },
                    { "mcs_abidate", "数据集成日期" },
                    { "mcs_checkdate", "复核日期" },
                    { "mcs_active", "有效状态" }
                };
                break;

            default:
                Console.WriteLine($"暂不支持实体 {entityName} 的视图更新");
                return;
        }
        
        manager.UpdateDefaultView(entityName, fields);
    }

    /// <summary>
    /// 查询指定信用评估记录的 BPP 相关字段
    /// </summary>
    static void QueryCreditRecordBpp(ServiceClient service, string scoreId)
    {
        Console.WriteLine($"=== 查询信用评估记录 BPP 状态: {scoreId} ===");

        var query = new Microsoft.Xrm.Sdk.Query.QueryExpression("mcs_credit_record")
        {
            ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet(
                "mcs_credit_recordid", "mcs_scoreid", "mcs_status",
                "mcs_bppstatus", "mcs_workflowid", "mcs_bpperrormsg",
                "mcs_nextapprover", "mcs_bpprejectreason",
                "createdon", "modifiedon", "mcs_accountid",
                "mcs_active", "mcs_creditscore", "mcs_creditgrade"
            ),
            Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression()
            {
                Conditions =
                {
                    new Microsoft.Xrm.Sdk.Query.ConditionExpression("mcs_scoreid", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, scoreId)
                }
            }
        };

        var records = service.RetrieveMultiple(query);
        if (records.Entities.Count == 0)
        {
            Console.WriteLine($"❌ 记录不存在: {scoreId}");
            return;
        }

        PrintCreditRecordBpp(records.Entities[0]);
    }

    /// <summary>
    /// 查询最近修改的信用评估记录（用于排查 BPP 回调）
    /// </summary>
    static void QueryRecentCreditRecordBpp(ServiceClient service, int topCount)
    {
        Console.WriteLine($"=== 最近 {topCount} 条信用评估记录（按修改时间倒序）===");

        var query = new Microsoft.Xrm.Sdk.Query.QueryExpression("mcs_credit_record")
        {
            ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet(
                "mcs_credit_recordid", "mcs_scoreid", "mcs_status",
                "mcs_bppstatus", "mcs_workflowid", "mcs_bpperrormsg",
                "mcs_nextapprover", "mcs_bpprejectreason",
                "createdon", "modifiedon", "mcs_accountid"
            ),
            Orders =
            {
                new Microsoft.Xrm.Sdk.Query.OrderExpression("modifiedon", Microsoft.Xrm.Sdk.Query.OrderType.Descending)
            },
            TopCount = topCount
        };

        var records = service.RetrieveMultiple(query);
        Console.WriteLine($"查询到 {records.Entities.Count} 条记录\n");

        int index = 1;
        foreach (var record in records.Entities)
        {
            Console.WriteLine($"--- 记录 {index} ---");
            PrintCreditRecordBpp(record);
            Console.WriteLine();
            index++;
        }
    }

    static void PrintCreditRecordBpp(Microsoft.Xrm.Sdk.Entity record)
    {
        Guid recordId = record.Id;
        var statusOpt = record.GetAttributeValue<Microsoft.Xrm.Sdk.OptionSetValue>("mcs_status");
        int currentStatus = statusOpt?.Value ?? -1;

        Console.WriteLine($"  记录ID: {recordId}");
        Console.WriteLine($"  评估编号 (mcs_scoreid): {record.GetAttributeValue<string>("mcs_scoreid") ?? "N/A"}");
        Console.WriteLine($"  业务状态 (mcs_status): {currentStatus} ({GetStatusName(currentStatus)})");
        Console.WriteLine($"  BPP状态 (mcs_bppstatus): {record.GetAttributeValue<string>("mcs_bppstatus") ?? "(null)"}");
        Console.WriteLine($"  工作流ID (mcs_workflowid): {record.GetAttributeValue<string>("mcs_workflowid") ?? "(null)"}");
        Console.WriteLine($"  下一审批人 (mcs_nextapprover): {record.GetAttributeValue<string>("mcs_nextapprover") ?? "(null)"}");
        Console.WriteLine($"  BPP错误信息 (mcs_bpperrormsg): {record.GetAttributeValue<string>("mcs_bpperrormsg") ?? "(null)"}");
        Console.WriteLine($"  BPP驳回原因 (mcs_bpprejectreason): {record.GetAttributeValue<string>("mcs_bpprejectreason") ?? "(null)"}");
        Console.WriteLine($"  信用分: {record.GetAttributeValue<decimal?>("mcs_creditscore")?.ToString("F2") ?? "N/A"}");
        Console.WriteLine($"  信用等级: {record.GetAttributeValue<string>("mcs_creditgrade") ?? "N/A"}");
        Console.WriteLine($"  有效状态 (mcs_active): {record.GetAttributeValue<bool?>("mcs_active")?.ToString() ?? "N/A"}");
        Console.WriteLine($"  创建时间: {record.GetAttributeValue<DateTime?>("createdon")}");
        Console.WriteLine($"  修改时间: {record.GetAttributeValue<DateTime?>("modifiedon")}");

        var accountRef = record.GetAttributeValue<Microsoft.Xrm.Sdk.EntityReference>("mcs_accountid");
        Console.WriteLine($"  客户: {accountRef?.Name ?? "N/A"} ({accountRef?.Id.ToString() ?? ""})");
    }

    static void QueryBppDebug(ServiceClient service)
    {
        Console.WriteLine("\n=== BPP 调试查询 ===\n");

        // 1. 查询 mcs_bppapply (只查最近3条，精简输出)
        Console.WriteLine("【1】mcs_bppapply 最近3条:");
        var q1 = new QueryExpression("mcs_bppapply")
        {
            ColumnSet = new ColumnSet("mcs_name", "mcs_workflowid", "mcs_entityname", "createdon"),
            Orders = { new OrderExpression("createdon", OrderType.Descending) },
            TopCount = 3
        };
        var applies = service.RetrieveMultiple(q1);
        foreach (var a in applies.Entities)
            Console.WriteLine($"  - {a.GetAttributeValue<string>("mcs_entityname")} | {a.GetAttributeValue<string>("mcs_name")?.Substring(0, Math.Min(40, a.GetAttributeValue<string>("mcs_name")?.Length ?? 0))}");

        // 2. 查询 CreditRecordBppIntegrationPlugin trace (最近2条，含完整messageblock)
        Console.WriteLine("\n【2】CreditRecordBppIntegrationPlugin Trace (最近2条):");
        var q2 = new QueryExpression("plugintracelog")
        {
            ColumnSet = new ColumnSet("createdon", "messagename", "performanceexecutionduration", "messageblock"),
            Criteria = new FilterExpression { Conditions = { new ConditionExpression("typename", ConditionOperator.Like, "%CreditRecordBppIntegrationPlugin%") } },
            Orders = { new OrderExpression("createdon", OrderType.Descending) },
            TopCount = 2
        };
        var logs1 = service.RetrieveMultiple(q2);
        foreach (var log in logs1.Entities)
        {
            Console.WriteLine($"\n--- {log.GetAttributeValue<DateTime?>("createdon")} | {log.GetAttributeValue<string>("messagename")} | {log.GetAttributeValue<int>("performanceexecutionduration")}ms ---");
            var msg = log.GetAttributeValue<string>("messageblock") ?? "";
            foreach (var line in msg.Split('\n'))
                if (!string.IsNullOrWhiteSpace(line)) Console.WriteLine(line.Trim());
        }

        // 3. 查询 CreditRecordBppCallbackPlugin trace (最近2条，含完整messageblock)
        Console.WriteLine("\n【3】CreditRecordBppCallbackPlugin Trace (最近2条):");
        var q3 = new QueryExpression("plugintracelog")
        {
            ColumnSet = new ColumnSet("createdon", "messagename", "performanceexecutionduration", "messageblock"),
            Criteria = new FilterExpression { Conditions = { new ConditionExpression("typename", ConditionOperator.Like, "%CreditRecordBppCallbackPlugin%") } },
            Orders = { new OrderExpression("createdon", OrderType.Descending) },
            TopCount = 2
        };
        var logs2 = service.RetrieveMultiple(q3);
        foreach (var log in logs2.Entities)
        {
            Console.WriteLine($"\n--- {log.GetAttributeValue<DateTime?>("createdon")} | {log.GetAttributeValue<string>("messagename")} | {log.GetAttributeValue<int>("performanceexecutionduration")}ms ---");
            var msg = log.GetAttributeValue<string>("messageblock") ?? "";
            foreach (var line in msg.Split('\n'))
                if (!string.IsNullOrWhiteSpace(line)) Console.WriteLine(line.Trim());
        }

        // 4. 查询 mcs_credit_record (status=14)
        Console.WriteLine("\n【4】mcs_credit_record (status=14):");
        var q4 = new QueryExpression("mcs_credit_record")
        {
            ColumnSet = new ColumnSet("mcs_scoreid", "mcs_bppstatus", "mcs_workflowid", "mcs_bpperrormsg", "modifiedon"),
            Criteria = new FilterExpression { Conditions = { new ConditionExpression("mcs_status", ConditionOperator.Equal, 14) } },
            Orders = { new OrderExpression("modifiedon", OrderType.Descending) },
            TopCount = 3
        };
        var recs = service.RetrieveMultiple(q4);
        foreach (var r in recs.Entities)
            Console.WriteLine($"  - {r.GetAttributeValue<string>("mcs_scoreid")} | bpp={r.GetAttributeValue<string>("mcs_bppstatus")} | wf={r.GetAttributeValue<string>("mcs_workflowid")} | err={r.GetAttributeValue<string>("mcs_bpperrormsg")}");
    }

    static void QuerySystemConfigurations(IOrganizationService service)
    {
        var names = new[] { "BPP_WorkFlowTemplateCode", "D365BaseUrl", "Bpp_ApprovalFlowBaseUrl", "UploadFileTypeMapping" };
        foreach (var name in names)
        {
            var query = new QueryExpression("ms_systemconfiguration")
            {
                ColumnSet = new ColumnSet("ms_name", "ms_content")
            };
            query.Criteria.AddCondition("ms_name", ConditionOperator.Equal, name);
            var results = service.RetrieveMultiple(query);
            Console.WriteLine($"\n=== {name} ===");
            Console.WriteLine($"  记录数: {results.Entities.Count}");
            if (results.Entities.Count > 0)
            {
                var e = results.Entities[0];
                Console.WriteLine(e.GetAttributeValue<string>("ms_content"));
            }
            else
            {
                Console.WriteLine("NOT FOUND");
            }
        }
    }

    static void QueryCofaceFinancialIndicators(IOrganizationService service, string? countryCodeFilter)
    {
        var query = new QueryExpression("mcs_coface_financial_indicator")
        {
            ColumnSet = new ColumnSet("mcs_countrycode", "mcs_countryname", "mcs_indicatorname", "mcs_typevalue", "mcs_indicatortype", "mcs_priority", "mcs_formulafallback", "mcs_isactive"),
            Orders = { new OrderExpression("mcs_countrycode", OrderType.Ascending), new OrderExpression("mcs_priority", OrderType.Ascending) }
        };
        if (!string.IsNullOrWhiteSpace(countryCodeFilter))
        {
            query.Criteria.AddCondition("mcs_countrycode", ConditionOperator.Equal, countryCodeFilter.ToUpper());
        }
        var results = service.RetrieveMultiple(query);
        Console.WriteLine($"共 {results.Entities.Count} 条 Coface 财务指标配置");
        var countries = new HashSet<string>();
        foreach (var e in results.Entities)
        {
            var code = e.GetAttributeValue<string>("mcs_countrycode");
            countries.Add(code ?? "");
            Console.WriteLine($"  {code} | {e.GetAttributeValue<string>("mcs_countryname")} | {e.GetAttributeValue<string>("mcs_indicatorname")} | type={e.GetAttributeValue<string>("mcs_typevalue")} | priority={e.GetAttributeValue<int>("mcs_priority")}");
        }
        Console.WriteLine($"涉及 {countries.Count} 个国家/地区");
    }

    static void QuerySystemConfigurationsLike(IOrganizationService service, string nameFragment)
    {
        var query = new QueryExpression("ms_systemconfiguration")
        {
            ColumnSet = new ColumnSet("ms_name", "ms_content", "ms_description"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("ms_name", ConditionOperator.Like, $"%{nameFragment}%") }
            }
        };
        var results = service.RetrieveMultiple(query);
        Console.WriteLine($"找到 {results.Entities.Count} 条配置包含 '{nameFragment}':");
        foreach (var e in results.Entities)
        {
            var content = e.GetAttributeValue<string>("ms_content") ?? "";
            Console.WriteLine($"\n=== {e.GetAttributeValue<string>("ms_name")} ===");
            Console.WriteLine($"desc: {e.GetAttributeValue<string>("ms_description")}");
            Console.WriteLine($"content: {content.Substring(0, Math.Min(500, content.Length))}{(content.Length > 500 ? "..." : "")}");
        }
    }

    static void QueryEntitiesByName(IOrganizationService service, string nameFragment)
    {
        var request = new RetrieveAllEntitiesRequest
        {
            EntityFilters = EntityFilters.Entity,
            RetrieveAsIfPublished = false
        };
        var response = (RetrieveAllEntitiesResponse)service.Execute(request);
        var lowerFragment = nameFragment.ToLowerInvariant();
        var matches = response.EntityMetadata
            .Where(e => (e.LogicalName?.ToLowerInvariant().Contains(lowerFragment) == true) ||
                        (e.DisplayName?.UserLocalizedLabel?.Label?.ToLowerInvariant().Contains(lowerFragment) == true))
            .OrderBy(e => e.LogicalName)
            .ToList();
        Console.WriteLine($"找到 {matches.Count} 个实体包含 '{nameFragment}':");
        foreach (var e in matches)
        {
            var display = e.DisplayName?.UserLocalizedLabel?.Label ?? "";
            Console.WriteLine($"  {e.LogicalName} | {display}");
        }
    }

    static void QueryFormsByName(IOrganizationService service, string nameFragment)
    {
        var query = new QueryExpression("systemform")
        {
            ColumnSet = new ColumnSet("formid", "name", "objecttypecode", "type"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("name", ConditionOperator.Like, $"%{nameFragment}%") }
            }
        };
        var results = service.RetrieveMultiple(query);
        Console.WriteLine($"找到 {results.Entities.Count} 个窗体包含 '{nameFragment}':");
        foreach (var e in results.Entities)
        {
            var typeValue = e.GetAttributeValue<OptionSetValue>("type");
            Console.WriteLine($"  {e.GetAttributeValue<string>("name")} | entity={e.GetAttributeValue<string>("objecttypecode")} | type={(typeValue?.Value.ToString() ?? "null")} | {e.Id}");
        }
    }

    static void TestUploadFileInitInfo(IOrganizationService service, string entityName, string entityId)
    {
        try
        {
            // 如果 entityId 是空 Guid，自动查询第一个对应实体记录用于测试
            if (entityId == "00000000-0000-0000-0000-000000000000")
            {
                var testEntity = entityName;
                var testIdField = entityName + "id";
                try
                {
                    var testQ = new QueryExpression(testEntity) { ColumnSet = new ColumnSet(testIdField), TopCount = 1 };
                    var testR = service.RetrieveMultiple(testQ);
                    if (testR.Entities.Count > 0)
                    {
                        entityId = testR.Entities[0].GetAttributeValue<Guid>(testIdField).ToString();
                        Console.WriteLine($"使用测试 {testEntity} ID: {entityId}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"无法自动查询 {testEntity} 测试记录: {ex.Message}");
                }
            }
            var req = new OrganizationRequest("mcs_GetUploadFilePageInitInfo");
            req["EntityID"] = entityId;
            req["EntityName"] = entityName;
            Console.WriteLine($"\n=== Calling mcs_GetUploadFilePageInitInfo(EntityName={entityName}, EntityID={entityId}) ===");
            var resp = service.Execute(req);
            var resultJson = resp.Results.Contains("Result") ? resp["Result"].ToString() : resp.Results.ToString();
            Console.WriteLine($"Success. Result: {resultJson?.Substring(0, Math.Min(resultJson.Length, 500))}...");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed: {ex.Message}");
            if (ex.InnerException != null) Console.WriteLine($"Inner: {ex.InnerException.Message}");
        }
    }

    static void UpsertSystemConfiguration(IOrganizationService service, string name, string content, string description)
    {
        try
        {
            var query = new QueryExpression("ms_systemconfiguration")
            {
                ColumnSet = new ColumnSet("ms_systemconfigurationid")
            };
            query.Criteria.AddCondition("ms_name", ConditionOperator.Equal, name);
            var results = service.RetrieveMultiple(query);

            var entity = new Entity("ms_systemconfiguration");
            entity["ms_name"] = name;
            entity["ms_content"] = content;
            if (!string.IsNullOrWhiteSpace(description))
            {
                entity["ms_description"] = description;
            }

            if (results.Entities.Count > 0)
            {
                entity.Id = results.Entities[0].Id;
                service.Update(entity);
                Console.WriteLine($"✅ 已更新系统配置: {name}");
            }
            else
            {
                var id = service.Create(entity);
                Console.WriteLine($"✅ 已创建系统配置: {name}, ID={id}");
            }
            Console.WriteLine($"   内容: {content}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 更新系统配置失败: {ex.Message}");
        }
    }

    static void QueryRecentCreditRecords(IOrganizationService service, string? statusFilter)
    {
        var query = new QueryExpression("mcs_credit_record")
        {
            ColumnSet = new ColumnSet("mcs_credit_recordid", "mcs_scoreid", "mcs_status", "mcs_bppstatus", "mcs_workflowid", "mcs_bpperrormsg", "mcs_creditscore", "mcs_api_status", "mcs_api_msg", "mcs_urba360id", "modifiedon"),
            Orders = { new OrderExpression("modifiedon", OrderType.Descending) },
            TopCount = 10
        };
        if (!string.IsNullOrWhiteSpace(statusFilter) && int.TryParse(statusFilter, out var statusValue))
        {
            query.Criteria.AddCondition("mcs_status", ConditionOperator.Equal, statusValue);
        }
        var results = service.RetrieveMultiple(query);
        Console.WriteLine($"\n=== 最近 {(string.IsNullOrWhiteSpace(statusFilter) ? "" : $"status={statusFilter} ")}mcs_credit_record 记录 ===");
        if (results.Entities.Count == 0)
        {
            Console.WriteLine("无记录");
            return;
        }
        foreach (var r in results.Entities)
        {
            var status = r.GetAttributeValue<OptionSetValue>("mcs_status")?.Value.ToString() ?? "?";
            var errMsg = r.GetAttributeValue<string>("mcs_bpperrormsg") ?? "";
            var apiMsg = r.GetAttributeValue<string>("mcs_api_msg") ?? "";
            Console.WriteLine($"  {r.GetAttributeValue<string>("mcs_scoreid")} ({r.Id}) | status={status} | api_status={r.GetAttributeValue<string>("mcs_api_status")} | bpp={r.GetAttributeValue<string>("mcs_bppstatus")} | wf={r.GetAttributeValue<string>("mcs_workflowid")} | score={r.GetAttributeValue<decimal?>("mcs_creditscore")} | urba={r.GetAttributeValue<string>("mcs_urba360id")} | api_msg={(apiMsg.Length > 80 ? apiMsg[..80] + "..." : apiMsg)} | err={(errMsg.Length > 50 ? errMsg[..50] + "..." : errMsg)}");
        }
    }

    static void QueryUrbaJson(IOrganizationService service, string scoreId)
    {
        var query = new QueryExpression("mcs_credit_record")
        {
            ColumnSet = new ColumnSet("mcs_credit_recordid", "mcs_scoreid", "mcs_urbajson"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("mcs_scoreid", ConditionOperator.Equal, scoreId) }
            },
            TopCount = 1
        };
        var results = service.RetrieveMultiple(query);
        if (results.Entities.Count == 0)
        {
            Console.WriteLine($"未找到记录: {scoreId}");
            return;
        }

        var json = results.Entities[0].GetAttributeValue<string>("mcs_urbajson") ?? "";
        Console.WriteLine($"=== URBA JSON for {scoreId} (长度: {json.Length}) ===");
        Console.WriteLine(json);
    }

    static void QueryCustomerTags(IOrganizationService service, string scoreId)
    {
        var recordQuery = new QueryExpression("mcs_credit_record")
        {
            ColumnSet = new ColumnSet("mcs_credit_recordid", "mcs_scoreid"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("mcs_scoreid", ConditionOperator.Equal, scoreId) }
            },
            TopCount = 1
        };
        var records = service.RetrieveMultiple(recordQuery);
        if (records.Entities.Count == 0)
        {
            Console.WriteLine($"未找到记录: {scoreId}");
            return;
        }

        var recordId = records.Entities[0].Id;
        var tagQuery = new QueryExpression("mcs_customer_tag")
        {
            ColumnSet = new ColumnSet("mcs_itemcode", "mcs_itemname", "mcs_itemvalue1", "mcs_itemtxtvalue1", "mcs_itemintvalue1"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("mcs_credit_record", ConditionOperator.Equal, recordId) }
            }
        };

        var tags = service.RetrieveMultiple(tagQuery);
        Console.WriteLine($"=== 客户标签明细 for {scoreId} (共 {tags.Entities.Count} 条) ===");
        foreach (var tag in tags.Entities)
        {
            var itemCode = tag.GetAttributeValue<string>("mcs_itemcode") ?? "";
            var itemName = tag.GetAttributeValue<string>("mcs_itemname") ?? "";
            var value1 = tag.GetAttributeValue<string>("mcs_itemvalue1") ?? "N/A";
            var txtvalue = tag.GetAttributeValue<string>("mcs_itemtxtvalue1") ?? "N/A";
            var intvalue = tag.GetAttributeValue<decimal?>("mcs_itemintvalue1")?.ToString() ?? "N/A";
            Console.WriteLine($"  [{itemCode}] {itemName}: value1={value1}, txt={txtvalue}, int={intvalue}");
        }
    }

    static void QueryCreditItems(IOrganizationService service)
    {
        var query = new QueryExpression("mcs_credit_items")
        {
            ColumnSet = new ColumnSet("mcs_credit_itemsno", "mcs_itemname", "mcs_datatype", "mcs_group"),
            Orders = { new OrderExpression("mcs_credit_itemsno", OrderType.Ascending) }
        };
        var results = service.RetrieveMultiple(query);
        Console.WriteLine($"=== 评分项目列表 (共 {results.Entities.Count} 条) ===");
        foreach (var r in results.Entities)
        {
            var no = r.GetAttributeValue<string>("mcs_credit_itemsno") ?? "";
            var name = r.GetAttributeValue<string>("mcs_itemname") ?? "";
            var type = r.GetAttributeValue<OptionSetValue>("mcs_datatype")?.Value ?? 0;
            var group = r.GetAttributeValue<OptionSetValue>("mcs_group")?.Value ?? 0;
            Console.WriteLine($"  [{no}] {name} | datatype={type} | group={group}");
        }
    }

    static void QueryRecentBppApply(IOrganizationService service)
    {
        var query = new QueryExpression("mcs_bppapply")
        {
            ColumnSet = new ColumnSet("mcs_name", "mcs_entityid", "mcs_entityname", "mcs_workflowid", "statuscode", "createdon"),
            Orders = { new OrderExpression("createdon", OrderType.Descending) },
            TopCount = 10
        };
        var results = service.RetrieveMultiple(query);
        Console.WriteLine("\n=== 最近 mcs_bppapply 记录 ===");
        if (results.Entities.Count == 0)
        {
            Console.WriteLine("无记录");
            return;
        }
        foreach (var r in results.Entities)
        {
            Console.WriteLine($"  {r.GetAttributeValue<string>("mcs_name")} | entity={r.GetAttributeValue<string>("mcs_entityname")} | id={r.GetAttributeValue<string>("mcs_entityid")} | wf={r.GetAttributeValue<string>("mcs_workflowid")} | created={r.GetAttributeValue<DateTime?>("createdon")}");
        }
    }

    static void QueryFormFields(IOrganizationService service, string entityName, string? fieldDisplayName)
    {
        try
        {
            var entityReq = new RetrieveEntityRequest { EntityFilters = EntityFilters.Entity, LogicalName = entityName };
            var entityResp = (RetrieveEntityResponse)service.Execute(entityReq);
            var objectTypeCode = entityResp.EntityMetadata.ObjectTypeCode.Value;

            var query = new QueryExpression("systemform")
            {
                ColumnSet = new ColumnSet("name", "formxml", "type"),
                Criteria = new FilterExpression
                {
                    Conditions = {
                        new ConditionExpression("objecttypecode", ConditionOperator.Equal, objectTypeCode),
                        new ConditionExpression("type", ConditionOperator.Equal, 2)
                    }
                }
            };
            var results = service.RetrieveMultiple(query);
            Console.WriteLine($"\n=== {entityName} 主窗体字段绑定 ===");
            if (results.Entities.Count == 0)
            {
                Console.WriteLine("未找到主窗体");
                return;
            }

            foreach (var form in results.Entities)
            {
                var formName = form.GetAttributeValue<string>("name");
                var formXml = form.GetAttributeValue<string>("formxml");
                Console.WriteLine($"\n窗体: {formName}");
                if (string.IsNullOrWhiteSpace(formXml)) continue;

                var doc = new System.Xml.XmlDocument();
                doc.LoadXml(formXml);
                // 输出表头 header 字段
                var header = doc.SelectSingleNode("//header");
                if (header != null)
                {
                    Console.WriteLine("  [表头 header]");
                    var headerControls = header.SelectNodes(".//control[@datafieldname]");
                    if (headerControls == null || headerControls.Count == 0)
                    {
                        Console.WriteLine("    （表头无字段）");
                    }
                    else
                    {
                        foreach (System.Xml.XmlNode ctrl in headerControls)
                        {
                            if (ctrl.Attributes == null) continue;
                            var dataField = ctrl.Attributes["datafieldname"]?.Value ?? "";
                            var controlId = ctrl.Attributes["id"]?.Value ?? "";
                            var label = ctrl.SelectSingleNode("labels/label")?.Attributes?["description"]?.Value ?? "";
                            Console.WriteLine($"    controlId={controlId}, datafieldname={dataField}, label={label}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("  [表头 header] 不存在");
                }

                // 输出 body 字段
                Console.WriteLine("  [正文 body]");
                var controls = doc.GetElementsByTagName("control");
                bool foundAny = false;
                foreach (System.Xml.XmlNode ctrl in controls)
                {
                    if (ctrl.Attributes == null) continue;
                    var dataField = ctrl.Attributes["datafieldname"]?.Value ?? "";
                    var classId = ctrl.Attributes["classid"]?.Value ?? "";
                    var controlId = ctrl.Attributes["id"]?.Value ?? "";

                    // 跳过表头已输出的
                    if (IsNodeInHeader(ctrl)) continue;

                    // 查找 label
                    string label = "";
                    var labels = ctrl.SelectNodes("labels/label");
                    if (labels != null && labels.Count > 0)
                    {
                        label = labels[0].Attributes?["description"]?.Value ?? "";
                    }

                    if (string.IsNullOrWhiteSpace(fieldDisplayName) ||
                        label.Contains(fieldDisplayName, StringComparison.OrdinalIgnoreCase) ||
                        dataField.Contains(fieldDisplayName, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"    controlId={controlId}, datafieldname={dataField}, label={label}, classid={classId}");
                        foundAny = true;
                    }
                }
                if (!foundAny && !string.IsNullOrWhiteSpace(fieldDisplayName))
                {
                    Console.WriteLine($"    未找到匹配 '{fieldDisplayName}' 的字段");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"查询窗体字段失败: {ex.Message}");
        }
    }

    static bool IsNodeInHeader(System.Xml.XmlNode node)
    {
        var current = node;
        while (current != null)
        {
            if (current.Name.Equals("header", StringComparison.OrdinalIgnoreCase))
                return true;
            current = current.ParentNode;
        }
        return false;
    }

    static void QuerySQueue(IOrganizationService service, string groupName)
    {
        var query = new QueryExpression("ms_squeue")
        {
            ColumnSet = new ColumnSet("ms_name", "ms_groupname", "ms_code", "ms_storetype", "ms_servername", "ms_processid", "ms_interval", "statecode", "statuscode"),
            Orders = { new OrderExpression("ms_groupname", OrderType.Ascending), new OrderExpression("ms_code", OrderType.Ascending) }
        };
        if (!string.IsNullOrWhiteSpace(groupName))
        {
            query.Criteria = new FilterExpression { Conditions = { new ConditionExpression("ms_groupname", ConditionOperator.Equal, groupName) } };
        }
        var results = service.RetrieveMultiple(query);
        Console.WriteLine($"\n=== ms_squeue group={groupName} 配置 ===");
        if (results.Entities.Count == 0)
        {
            Console.WriteLine("无记录");
            return;
        }
        foreach (var r in results.Entities)
        {
            Console.WriteLine($"  code={r.GetAttributeValue<int>("ms_code")} entity={r.GetAttributeValue<string>("ms_name")} group={r.GetAttributeValue<string>("ms_groupname")} store={r.GetAttributeValue<string>("ms_storetype")} server={r.GetAttributeValue<string>("ms_servername")} process={r.GetAttributeValue<string>("ms_processid")} interval={r.GetAttributeValue<int?>("ms_interval")}");
        }
    }

    static void QuerySMessage(IOrganizationService service, string messageType)
    {
        var entities = new[] { "ms_smessage_common_01", "ms_smessage_common_02", "ms_smessage_common_dead_01" };
        Console.WriteLine($"\n=== 最近 {(messageType.Equals("all", StringComparison.OrdinalIgnoreCase) ? "所有" : messageType)} 消息 (ms_smessage_common_*) ===");
        bool any = false;
        foreach (var entityName in entities)
        {
            var query = new QueryExpression(entityName)
            {
                ColumnSet = new ColumnSet("ms_name", "ms_type", "ms_data", "ms_isdead", "ms_retrynumber", "ms_exceptionmessage", "ms_lastexecutetime", "createdon"),
                Orders = { new OrderExpression("createdon", OrderType.Descending) },
                TopCount = messageType.Equals("all", StringComparison.OrdinalIgnoreCase) ? 10 : 50
            };
            if (!messageType.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                query.Criteria = new FilterExpression { Conditions = { new ConditionExpression("ms_type", ConditionOperator.Equal, "Transfer") } };
            }
            try
            {
                var results = service.RetrieveMultiple(query);
                foreach (var r in results.Entities)
                {
                    var data = r.GetAttributeValue<string>("ms_data") ?? "";
                    if (!messageType.Equals("all", StringComparison.OrdinalIgnoreCase) && !data.Contains(messageType, StringComparison.OrdinalIgnoreCase)) continue;
                    any = true;
                    Console.WriteLine($"\n-- {entityName} --");
                    var err = r.GetAttributeValue<string>("ms_exceptionmessage") ?? "";
                    Console.WriteLine($"  {r.GetAttributeValue<string>("ms_name")} | type={r.GetAttributeValue<string>("ms_type")} | dead={r.GetAttributeValue<bool>("ms_isdead")} | retry={r.GetAttributeValue<int>("ms_retrynumber")} | created={r.GetAttributeValue<DateTime?>("createdon")} | last={r.GetAttributeValue<DateTime?>("ms_lastexecutetime")}");
                    Console.WriteLine($"    data={(data.Length > 300 ? data[..300] + "..." : data)}");
                    if (!string.IsNullOrEmpty(err))
                        Console.WriteLine($"    err={(err.Length > 300 ? err[..300] + "..." : err)}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠️ 查询 {entityName} 失败: {ex.Message}");
            }
        }
        if (!any) Console.WriteLine("无匹配记录");
    }

    static void UnregisterPlugin(IOrganizationService service, string assemblyName)
    {
        try
        {
            var assemblyQuery = new QueryExpression("pluginassembly")
            {
                ColumnSet = new ColumnSet("pluginassemblyid"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("name", ConditionOperator.Equal, assemblyName) }
                }
            };
            var assembly = service.RetrieveMultiple(assemblyQuery).Entities.FirstOrDefault();
            if (assembly == null)
            {
                Console.WriteLine($"⚠️ 找不到 Assembly: {assemblyName}");
                return;
            }
            var assemblyId = assembly.Id;

            // 查询并删除 Types 及其 Steps
            var typeQuery = new QueryExpression("plugintype")
            {
                ColumnSet = new ColumnSet("plugintypeid"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("pluginassemblyid", ConditionOperator.Equal, assemblyId) }
                }
            };
            foreach (var type in service.RetrieveMultiple(typeQuery).Entities)
            {
                var typeId = type.Id;
                var stepQuery = new QueryExpression("sdkmessageprocessingstep")
                {
                    ColumnSet = new ColumnSet("sdkmessageprocessingstepid"),
                    Criteria = new FilterExpression
                    {
                        Conditions = { new ConditionExpression("plugintypeid", ConditionOperator.Equal, typeId) }
                    }
                };
                foreach (var step in service.RetrieveMultiple(stepQuery).Entities)
                {
                    service.Delete("sdkmessageprocessingstep", step.Id);
                    Console.WriteLine($"✅ 已删除 Step: {step.Id}");
                }
                service.Delete("plugintype", typeId);
                Console.WriteLine($"✅ 已删除 PluginType: {typeId}");
            }

            // 删除 Assembly
            service.Delete("pluginassembly", assemblyId);
            Console.WriteLine($"✅ 已删除 Assembly: {assemblyName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 注销 Plugin 失败: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

    static void ListPluginAssemblies(IOrganizationService service, string filter)
    {
        var query = new QueryExpression("pluginassembly")
        {
            ColumnSet = new ColumnSet("name", "version", "createdon", "modifiedon"),
            Orders = { new OrderExpression("modifiedon", OrderType.Descending) },
            TopCount = 100
        };
        var results = service.RetrieveMultiple(query);
        Console.WriteLine($"\n=== Plugin Assemblies (filter={filter}) ===");
        int count = 0;
        foreach (var r in results.Entities)
        {
            var name = r.GetAttributeValue<string>("name") ?? "";
            if (!string.IsNullOrWhiteSpace(filter) && !name.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
            count++;
            Console.WriteLine($"  {name} | modified={r.GetAttributeValue<DateTime?>("modifiedon")}");
        }
        Console.WriteLine($"总计: {count}");
    }

    static void QueryPluginTraceLog(IOrganizationService service, string typeNameFilter)
    {
        var query = new QueryExpression("plugintracelog")
        {
            ColumnSet = new ColumnSet("typename", "messagename", "primaryentity", "performanceexecutionduration", "exceptiondetails", "messageblock", "createdon"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("typename", ConditionOperator.Like, $"%{typeNameFilter}%"),
                    new ConditionExpression("createdon", ConditionOperator.GreaterThan, DateTime.UtcNow.AddHours(-2))
                }
            },
            Orders = { new OrderExpression("createdon", OrderType.Descending) },
            TopCount = 10
        };
        var results = service.RetrieveMultiple(query);
        Console.WriteLine($"\n=== 最近 Plugin Trace Log (typename like {typeNameFilter}) ===");
        if (results.Entities.Count == 0)
        {
            Console.WriteLine("无记录（可能未开启 Plugin Trace）");
            return;
        }
        foreach (var r in results.Entities)
        {
            Console.WriteLine($"\n  {r.GetAttributeValue<string>("typename")} | msg={r.GetAttributeValue<string>("messagename")} | entity={r.GetAttributeValue<string>("primaryentity")} | duration={r.GetAttributeValue<int>("performanceexecutionduration")}ms | created={r.GetAttributeValue<DateTime?>("createdon")}");
            var msg = r.GetAttributeValue<string>("messageblock") ?? "";
            var ex = r.GetAttributeValue<string>("exceptiondetails") ?? "";
            if (!string.IsNullOrEmpty(msg))
                Console.WriteLine($"  trace={(msg.Length > 10000 ? msg[..10000] + "..." : msg)}");
            if (!string.IsNullOrEmpty(ex))
                Console.WriteLine($"  exception={(ex.Length > 500 ? ex[..500] + "..." : ex)}");
        }
    }

    static void QueryStepsByEntity(IOrganizationService service, string entityName)
    {
        var query = new QueryExpression("sdkmessageprocessingstep")
        {
            ColumnSet = new ColumnSet("name", "sdkmessagefilterid", "plugintypeid", "filteringattributes", "stage", "mode", "statecode"),
            Criteria = new FilterExpression { Conditions = { new ConditionExpression("statecode", ConditionOperator.Equal, 0) } }
        };
        var linkFilter = new LinkEntity("sdkmessageprocessingstep", "sdkmessagefilter", "sdkmessagefilterid", "sdkmessagefilterid", JoinOperator.Inner)
        {
            Columns = new ColumnSet("primaryobjecttypecode"),
            EntityAlias = "filter"
        };
        linkFilter.LinkCriteria.AddCondition("primaryobjecttypecode", ConditionOperator.Equal, entityName);
        query.LinkEntities.Add(linkFilter);
        var linkType = new LinkEntity("sdkmessageprocessingstep", "plugintype", "plugintypeid", "plugintypeid", JoinOperator.Inner)
        {
            Columns = new ColumnSet("typename"),
            EntityAlias = "type"
        };
        query.LinkEntities.Add(linkType);
        var results = service.RetrieveMultiple(query);
        Console.WriteLine($"\n=== {entityName} 上的 Plugin Steps ===");
        if (results.Entities.Count == 0)
        {
            Console.WriteLine("无记录");
            return;
        }
        foreach (var r in results.Entities)
        {
            var typeName = r.GetAttributeValue<AliasedValue>("type.typename")?.Value?.ToString() ?? "";
            var name = r.GetAttributeValue<string>("name");
            var msg = r.GetAttributeValue<EntityReference>("sdkmessagefilterid")?.Name ?? "";
            var stage = r.GetAttributeValue<OptionSetValue>("stage")?.Value.ToString() ?? "";
            var mode = r.GetAttributeValue<OptionSetValue>("mode")?.Value == 0 ? "Sync" : "Async";
            var filter = r.GetAttributeValue<string>("filteringattributes") ?? "";
            Console.WriteLine($"  {typeName}\n    {name} | {msg} | stage={stage} | mode={mode} | filter={filter}");
        }
    }

    static void FindSMessageByEntity(IOrganizationService service, string entityId)
    {
        var entities = new[] { "ms_smessage_common_01", "ms_smessage_common_02" };
        Console.WriteLine($"\n=== 查找 entityId={entityId} 的消息 ===");
        bool any = false;
        foreach (var entityName in entities)
        {
            var query = new QueryExpression(entityName)
            {
                ColumnSet = new ColumnSet("ms_name", "ms_type", "ms_data", "ms_isdead", "ms_retrynumber", "ms_exceptionmessage", "ms_lastexecutetime", "createdon"),
                Orders = { new OrderExpression("createdon", OrderType.Descending) },
                TopCount = 100
            };
            try
            {
                var results = service.RetrieveMultiple(query);
                foreach (var r in results.Entities)
                {
                    var data = r.GetAttributeValue<string>("ms_data") ?? "";
                    if (!data.Contains(entityId, StringComparison.OrdinalIgnoreCase)) continue;
                    any = true;
                    var err = r.GetAttributeValue<string>("ms_exceptionmessage") ?? "";
                    Console.WriteLine($"\n-- {entityName} --");
                    Console.WriteLine($"  {r.GetAttributeValue<string>("ms_name")} | type={r.GetAttributeValue<string>("ms_type")} | dead={r.GetAttributeValue<bool>("ms_isdead")} | retry={r.GetAttributeValue<int>("ms_retrynumber")} | created={r.GetAttributeValue<DateTime?>("createdon")} | last={r.GetAttributeValue<DateTime?>("ms_lastexecutetime")}");
                    Console.WriteLine($"    data={(data.Length > 400 ? data[..400] + "..." : data)}");
                    if (!string.IsNullOrEmpty(err))
                        Console.WriteLine($"    err={(err.Length > 400 ? err[..400] + "..." : err)}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠️ 查询 {entityName} 失败: {ex.Message}");
            }
        }
        if (!any) Console.WriteLine("无匹配记录");
    }

    static void UpdateFormXmlFromFile(IOrganizationService service, string entityName, string xmlPath)
    {
        Console.WriteLine($">>> 更新 {entityName} 主窗体 XML...");
        if (!File.Exists(xmlPath))
        {
            Console.WriteLine($"  ✗ 文件不存在: {xmlPath}");
            return;
        }

        var xml = File.ReadAllText(xmlPath);
        var query = new QueryExpression("systemform")
        {
            ColumnSet = new ColumnSet("formid"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("objecttypecode", ConditionOperator.Equal, entityName),
                    new ConditionExpression("type", ConditionOperator.Equal, 2)
                }
            }
        };

        var results = service.RetrieveMultiple(query);
        Console.WriteLine($"  找到 {results.Entities.Count} 个主窗体");
        foreach (var form in results.Entities)
        {
            var updateForm = new Entity("systemform") { Id = form.Id };
            updateForm["formxml"] = xml;
            service.Update(updateForm);
            Console.WriteLine($"  ✅ 窗体 {form.Id} 已更新");
        }
    }

    static void ExportFormXml(IOrganizationService service, string entityName, string outputPath)
    {
        Console.WriteLine($">>> 导出 {entityName} 主窗体 XML...");
        var query = new QueryExpression("systemform")
        {
            ColumnSet = new ColumnSet("formid", "name", "type", "formxml"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("objecttypecode", ConditionOperator.Equal, entityName),
                    new ConditionExpression("type", ConditionOperator.Equal, 2)
                }
            }
        };

        var results = service.RetrieveMultiple(query);
        Console.WriteLine($"  找到 {results.Entities.Count} 个主窗体");
        foreach (var form in results.Entities)
        {
            var name = form.GetAttributeValue<string>("name");
            var id = form.Id;
            var xml = form.GetAttributeValue<string>("formxml");
            var fileName = $"{outputPath}.{id}.xml";
            File.WriteAllText(fileName, xml);
            Console.WriteLine($"  ✅ 窗体 '{name}' ({id}) 导出到: {fileName}");
            Console.WriteLine($"     包含 'Credit Tags': {xml.Contains("Credit Tags", StringComparison.OrdinalIgnoreCase)}");
            Console.WriteLine($"     包含 '附件': {xml.Contains("附件", StringComparison.OrdinalIgnoreCase)}");
            Console.WriteLine($"     包含 'Uploader': {xml.Contains("Uploader", StringComparison.OrdinalIgnoreCase)}");
        }
    }

    /// <summary>
    /// 为指定实体的主窗体添加一个包含 HTML WebResource 上传组件的 Tab。
    /// 会先导出 D365 当前窗体 XML，插入新 Tab 后再更新回 D365。
    /// </summary>
    static void AddUploaderTab(IOrganizationService service, string entityName, string tabName, string webResourceUrl)
    {
        Console.WriteLine($">>> 为 {entityName} 添加 Uploader Tab: {tabName} ...");
        Console.WriteLine($"    WebResource: {webResourceUrl}");

        // 1. 查询主窗体
        var query = new QueryExpression("systemform")
        {
            ColumnSet = new ColumnSet("formid", "name", "formxml"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("objecttypecode", ConditionOperator.Equal, entityName),
                    new ConditionExpression("type", ConditionOperator.Equal, 2)
                }
            }
        };

        var results = service.RetrieveMultiple(query);
        if (results.Entities.Count == 0)
        {
            Console.WriteLine($"  ✗ 未找到 {entityName} 的主窗体");
            return;
        }

        foreach (var form in results.Entities)
        {
            var formId = form.Id;
            var formXml = form.GetAttributeValue<string>("formxml");
            if (string.IsNullOrWhiteSpace(formXml))
            {
                Console.WriteLine($"  ✗ 窗体 {formId} 的 XML 为空");
                continue;
            }

            // 2. 检查是否已存在同名 Tab
            if (formXml.Contains($"name=\"{tabName}\"", StringComparison.OrdinalIgnoreCase) ||
                formXml.Contains($">{tabName}<", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"  ⊘ Tab '{tabName}' 已存在，跳过");
                continue;
            }

            // 3. 生成新 Tab XML
            var newTabXml = BuildUploaderTabXml(tabName, webResourceUrl);

            // 4. 在 </tabs> 前插入新 Tab
            int tabsEndIndex = formXml.LastIndexOf("</tabs>");
            if (tabsEndIndex < 0)
            {
                Console.WriteLine($"  ✗ 窗体 XML 中未找到 </tabs> 标签");
                continue;
            }

            var updatedXml = formXml.Insert(tabsEndIndex, newTabXml);

            // 5. 更新窗体
            var updateForm = new Entity("systemform") { Id = formId };
            updateForm["formxml"] = updatedXml;
            service.Update(updateForm);
            Console.WriteLine($"  ✅ 窗体 {formId} 已添加 '{tabName}' Tab");
        }
    }

    /// <summary>
    /// 构建一个包含 HTML WebResource 的 Tab XML 片段。
    /// </summary>
    static string BuildUploaderTabXml(string tabName, string webResourceUrl)
    {
        var tabId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var cellId = Guid.NewGuid();
        var controlId = $"{tabName.ToLowerInvariant().Replace(" ", "_")}_uploader";

        return $@"    <tab name=""{tabName}"" id=""{{{tabId}}}"" IsUserDefined=""1"" locklevel=""0"" showlabel=""true"">
      <labels>
        <label description=""{tabName}"" languagecode=""1033""/>
      </labels>
      <columns>
        <column width=""100%"">
          <sections>
            <section name=""tab_{tabName.ToLowerInvariant().Replace(" ", "_")}_section_1"" id=""{{{sectionId}}}"" IsUserDefined=""1"" locklevel=""0"" showlabel=""false"" showbar=""false"" layout=""varwidth"" celllabelalignment=""Left"" celllabelposition=""Left"" columns=""1"" labelwidth=""115"">
              <labels>
                <label description=""Uploader"" languagecode=""1033""/>
              </labels>
              <rows>
                <row>
                  <cell locklevel=""0"" id=""{{{cellId}}}"" rowspan=""12"" colspan=""1"" auto=""false"" showlabel=""false"">
                    <labels>
                      <label description=""Uploader"" languagecode=""1033""/>
                    </labels>
                    <control id=""{controlId}"" classid=""{{9FDF5F91-88E1-47cd-9CAE-4C7186A64CBD}}"">
                      <parameters>
                        <Url>{webResourceUrl}</Url>
                        <PassParameters>true</PassParameters>
                      </parameters>
                    </control>
                  </cell>
                </row>
              </rows>
            </section>
          </sections>
        </column>
      </columns>
    </tab>
";
    }

    static void QueryAccountMasterData(ServiceClient service, string? accountNumber = null)
    {
        var query = new QueryExpression("account")
        {
            ColumnSet = new ColumnSet("accountid", "accountnumber", "name", "mcs_englishname", "mcs_country", "mcs_accountcategory", "mcs_accounttype", "mcs_customermasterdata"),
            TopCount = 1
        };
        query.Criteria.AddCondition("mcs_customermasterdata", ConditionOperator.NotNull);
        if (!string.IsNullOrWhiteSpace(accountNumber))
        {
            query.Criteria.AddCondition("accountnumber", ConditionOperator.Equal, accountNumber);
        }

        var accounts = service.RetrieveMultiple(query);
        if (accounts.Entities.Count == 0)
        {
            Console.WriteLine("❌ 未找到关联了客户主数据的 Account 记录");
            return;
        }

        var account = accounts.Entities[0];
        var masterRef = account.GetAttributeValue<EntityReference>("mcs_customermasterdata");

        Console.WriteLine("=== Account 记录 ===");
        Console.WriteLine($"  accountid: {account.Id}");
        Console.WriteLine($"  accountnumber: {account.GetAttributeValue<string>("accountnumber")}");
        Console.WriteLine($"  name: {account.GetAttributeValue<string>("name")}");
        Console.WriteLine($"  mcs_englishname: {account.GetAttributeValue<string>("mcs_englishname")}");
        Console.WriteLine($"  mcs_country: {GetLookupName(account, "mcs_country")}");
        // account 上没有 mcs_countrycode， countrycode 从 mcs_country Lookup 展开获取
        Console.WriteLine($"  mcs_accountcategory: {GetOptionSetLabel(account, "mcs_accountcategory")}");
        Console.WriteLine($"  mcs_accounttype: {GetOptionSetLabel(account, "mcs_accounttype")}");
        Console.WriteLine($"  备注: account 上无 mcs_blacklist / mcs_creditgrant 字段");
        Console.WriteLine($"  mcs_customermasterdata: {masterRef?.Id} ({masterRef?.Name})");

        if (masterRef == null) return;

        var masterQuery = new QueryExpression("mcs_customermasterdata")
        {
            ColumnSet = new ColumnSet("mcs_customermasterdataid", "mcs_name", "mcs_englishname", "mcs_country", "mcs_countrycode", "mcs_accountcategory", "mcs_accounttype"),
            Criteria = new FilterExpression { Conditions = { new ConditionExpression("mcs_customermasterdataid", ConditionOperator.Equal, masterRef.Id) } }
        };
        var masters = service.RetrieveMultiple(masterQuery);
        if (masters.Entities.Count == 0)
        {
            Console.WriteLine("❌ 未找到对应的 mcs_customermasterdata 记录");
            return;
        }

        var master = masters.Entities[0];
        Console.WriteLine("\n=== mcs_customermasterdata 记录 ===");
        Console.WriteLine($"  mcs_customermasterdataid: {master.Id}");
        Console.WriteLine($"  mcs_name: {master.GetAttributeValue<string>("mcs_name")}");
        Console.WriteLine($"  mcs_englishname: {master.GetAttributeValue<string>("mcs_englishname")}");
        Console.WriteLine($"  mcs_country: {GetLookupName(master, "mcs_country")}");
        Console.WriteLine($"  mcs_countrycode: {master.GetAttributeValue<string>("mcs_countrycode")}");
        Console.WriteLine($"  mcs_accountcategory: {GetOptionSetLabel(master, "mcs_accountcategory")}");
        Console.WriteLine($"  mcs_accounttype: {GetOptionSetLabel(master, "mcs_accounttype")}");
        Console.WriteLine($"  备注: mcs_customermasterdata 上无 mcs_blacklist / mcs_creditgrant 字段");

        Console.WriteLine("\n=== 字段一致性对比 ===");
        CompareField("mcs_englishname", account.GetAttributeValue<string>("mcs_englishname"), master.GetAttributeValue<string>("mcs_englishname"));
        CompareField("mcs_countrycode", "(account无此字段)", master.GetAttributeValue<string>("mcs_countrycode"));
        CompareField("mcs_accountcategory", GetOptionSetLabel(account, "mcs_accountcategory"), GetOptionSetLabel(master, "mcs_accountcategory"));
        CompareField("mcs_accounttype", GetOptionSetLabel(account, "mcs_accounttype"), GetOptionSetLabel(master, "mcs_accounttype"));
        Console.WriteLine("  mcs_blacklist: Account=(无字段), MasterData=(无字段)  → 需新建");
        Console.WriteLine("  mcs_creditgrant: Account=(无字段), MasterData=(无字段)  → 需新建");
    }

    static string GetLookupName(Entity entity, string fieldName)
    {
        var lookup = entity.GetAttributeValue<EntityReference>(fieldName);
        return lookup == null ? "(null)" : $"{lookup.Id} ({lookup.Name})";
    }

    static string GetOptionSetLabel(Entity entity, string fieldName)
    {
        var option = entity.GetAttributeValue<OptionSetValue>(fieldName);
        var name = entity.GetAttributeValue<string>($"{fieldName}name");
        return option == null ? "(null)" : $"{option.Value} ({name})";
    }

    static void CompareField(string fieldName, string? accountValue, string? masterValue)
    {
        var a = accountValue ?? "(null)";
        var m = masterValue ?? "(null)";
        var status = a == m ? "✅ 一致" : "❌ 不一致";
        Console.WriteLine($"  {fieldName}: Account={a}, MasterData={m}  {status}");
    }

    static void QueryOptionSet(ServiceClient service, string entityName, string fieldName)
    {
        try
        {
            var request = new RetrieveAttributeRequest
            {
                EntityLogicalName = entityName,
                LogicalName = fieldName,
                RetrieveAsIfPublished = true
            };
            var response = (RetrieveAttributeResponse)service.Execute(request);

            if (response.AttributeMetadata is PicklistAttributeMetadata picklist)
            {
                Console.WriteLine($"=== {entityName}.{fieldName} 选项集 ===");
                Console.WriteLine($"选项集名称: {picklist.OptionSet.Name}");
                foreach (var option in picklist.OptionSet.Options)
                {
                    Console.WriteLine($"  值: {option.Value}, 标签: {option.Label.UserLocalizedLabel?.Label}");
                }
            }
            else if (response.AttributeMetadata is BooleanAttributeMetadata booleanAttr)
            {
                Console.WriteLine($"=== {entityName}.{fieldName} 布尔选项 ===");
                Console.WriteLine($"  True: {booleanAttr.OptionSet.TrueOption.Value} - {booleanAttr.OptionSet.TrueOption.Label.UserLocalizedLabel?.Label}");
                Console.WriteLine($"  False: {booleanAttr.OptionSet.FalseOption.Value} - {booleanAttr.OptionSet.FalseOption.Label.UserLocalizedLabel?.Label}");
            }
            else
            {
                Console.WriteLine($"{entityName}.{fieldName} 不是 Picklist/Boolean，实际类型: {response.AttributeMetadata.AttributeType}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 查询失败: {ex.Message}");
        }
    }
}

public class PluginStepConfig
{
    public string PluginTypeName { get; set; } = string.Empty;
    public string MessageName { get; set; } = string.Empty;
    public string PrimaryEntity { get; set; } = string.Empty;
    public int Stage { get; set; } = 40;
    public string FilteringAttributes { get; set; } = string.Empty;
    public int Rank { get; set; } = 1;
}
