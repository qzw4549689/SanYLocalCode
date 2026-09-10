using Microsoft.Crm.Sdk.Messages;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using D365ToolCommon.Connection;
using D365ToolCommon.WebResource;

namespace DeployTool
{
    class Program
    {
        static readonly string ServiceUrl = D365ConnectionFactory.ResolveUrl();
        static readonly string AppId = Environment.GetEnvironmentVariable("D365_APPID") ?? "51f81489-12ee-4a9e-aaae-a2591f45987d";
        static readonly string TenantId = Environment.GetEnvironmentVariable("D365_TENANTID") ?? "";

        static async Task Main(string[] args)
        {
            Console.WriteLine("===== D365 部署工具 =====");
            Console.WriteLine($"目标环境: {ServiceUrl}");
            Console.WriteLine();

            if (args.Length == 0)
            {
                ShowHelp();
                return;
            }

            var command = args[0].ToLower();

            try
            {
                using var serviceClient = await D365ConnectionFactory.CreateAsync(ServiceUrl);
                if (!serviceClient.IsReady)
                {
                    Console.WriteLine("❌ 连接失败!");
                    return;
                }
                Console.WriteLine("✅ 连接成功!");
                Console.WriteLine();

                switch (command)
                {
                    case "webresource":
                    case "wr":
                        UpdateWebResource(serviceClient);
                        break;
                    case "scoringcard-wr":
                        UpdateScoringCardWebResource(serviceClient);
                        break;
                    case "tradestpayterm-wr":
                        UpdateTradeStPayTermWebResource(serviceClient);
                        break;
                    case "profile":
                        UpdateProfileWebResources(serviceClient);
                        break;
                    case "tradestpayterm":
                        DeployPlugin.DeployTradeStPayTermPlugin(serviceClient);
                        break;
                    case "appactions":
                    case "buttons":
                        new AppActionDeployer(serviceClient).DeployButtons();
                        break;
                    case "update-appaction-params":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run update-appaction-params <按钮uniquename或前缀> <参数JSON>");
                            Console.WriteLine("  示例: dotnet run update-appaction-params cr0c0__mcs_trade_stpayterm_apply \"[{\\\"type\\\":23},{\\\"type\\\":12}]\"");
                            return;
                        }
                        new AppActionDeployer(serviceClient).UpdateButtonParameters(args[1], args[2]);
                        break;
                    case "set-appaction-disabled":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run set-appaction-disabled <按钮uniquename或前缀> <true|false>");
                            Console.WriteLine("  现代按钮禁用后回退显示经典 Ribbon 按钮（可逆）");
                            return;
                        }
                        new AppActionDeployer(serviceClient).SetButtonDisabled(args[1], bool.Parse(args[2]));
                        break;
                    case "set-appaction-inactive":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run set-appaction-inactive <按钮uniquename或前缀> <true|false>");
                            Console.WriteLine("  statecode=Inactive 真正隐藏按钮（isdisabled 只禁点不隐藏）；各环境 GUID 不同，按 uniquename 操作");
                            return;
                        }
                        new AppActionDeployer(serviceClient).SetButtonInactive(args[1], bool.Parse(args[2]));
                        break;
                    // 2026-08-14 Bug #1834：取消审批功能，批量按钮 Display Rule 设置入口已停用
                    // case "stpayterm-display-rules":
                    //     // 为 3 个列表批量按钮挂 SelectionCount Display Rule（修复勾选后按钮消失，KB 4481268）
                    //     // 用法: dotnet run stpayterm-display-rules [规则入包的Solution唯一名，默认 entity_20260726_peter]
                    //     new AppActionDeployer(serviceClient).SetTradeStPayTermBatchDisplayRules(args.Length >= 2 ? args[1] : "entity_20260726_peter");
                    //     break;
                    case "set-appaction-visibility":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run set-appaction-visibility <按钮uniquename或前缀> <0|1> [PowerFx公式]");
                            Console.WriteLine("  0=始终显示  1=Power Fx 公式（需传公式，如 CountRows(Self.Selected.AllItems) > 0）");
                            Console.WriteLine("  用途: 修复勾选记录后按钮消失（KB 4481268），公式存 appaction 字段可随包发布");
                            return;
                        }
                        new AppActionDeployer(serviceClient).SetButtonVisibility(args[1], int.Parse(args[2]), args.Length >= 4 ? args[3] : null);
                        break;
                    case "attach-selection-rule":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run attach-selection-rule <按钮uniquename或前缀>");
                            Console.WriteLine("  关联第一方规则 Mscrm.SelectionCountAtLeastOne（选中≥1条显示），修复勾选后按钮消失（KB 4481268）");
                            return;
                        }
                        new AppActionDeployer(serviceClient).AttachFirstPartySelectionCountRule(args[1]);
                        break;
                    case "delete-appaction":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run delete-appaction <按钮uniquename或前缀>");
                            Console.WriteLine("  删除指定 App Action 按钮记录（不从 Solution 移除组件引用）");
                            return;
                        }
                        new AppActionDeployer(serviceClient).DeleteAppAction(args[1]);
                        break;
                    case "fsmbuttons":
                        new AppActionDeployer(serviceClient).DeployFsmDataButtons();
                        break;
                    case "coface":
                        DeployPlugin.DeployCofacePlugin(serviceClient);
                        break;
                    case "coface-search":
                        new CofaceCustomActionDeployer(serviceClient).Deploy();
                        break;
                    case "coface-config":
                        new CofaceConfigDeployer(serviceClient).Deploy();
                        break;
                    case "coface-html":
                        DeployCofaceHtmlWebResource(serviceClient);
                        break;
                    case "creditscore":
                        DeployCreditScorePlugin.Deploy(serviceClient);
                        break;
                    case "bpp":
                        DeployPlugin.DeployBppPlugin(serviceClient);
                        break;
                    case "main-assembly":
                        DeployPlugin.UpdateMainSalesAssembly(serviceClient);
                        break;
                    case "probe":
                        ProbeBppAssembly.Probe(serviceClient);
                        break;
                    case "test-bpp-start":
                        if (args.Length < 2 || !Guid.TryParse(args[1], out var testRecordId))
                        {
                            Console.WriteLine("用法: dotnet run test-bpp-start <recordId>");
                            return;
                        }
                        TestBppStartApi.Test(serviceClient, testRecordId);
                        break;
                    case "download-assembly":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run download-assembly <assemblyName>");
                            return;
                        }
                        DownloadExtensionApiAssembly.Download(serviceClient, args[1]);
                        break;
                    case "formlayout":
                        UpdateFormLayout.Update(serviceClient);
                        break;
                    case "publish":
                        PublishEntity(serviceClient);
                        break;
                    case "publish-webresource":
                        PublishWebResourceOnly(serviceClient);
                        break;
                    case "query-user-org":
                        QueryUserAccount.Query(serviceClient);
                        break;
                    case "fill-user-org":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run fill-user-org <mcs_orgid GUID>");
                            return;
                        }
                        QueryUserAccount.Fill(serviceClient, args[1]);
                        break;
                    case "setup-ltc-overdue":
                        QueryUserAccount.SetupLtcOverdue(serviceClient);
                        break;
                    case "setup-ltc-blacklist":
                        QueryUserAccount.SetupLtcBlacklist(serviceClient);
                        break;
                    case "all":
                        UpdateWebResource(serviceClient);
                        UpdateScoringCardWebResource(serviceClient);
                        UpdateProfileWebResources(serviceClient);
                        DeployCofaceHtmlWebResource(serviceClient);
                        new CofaceCustomActionDeployer(serviceClient).Deploy();
                        new CofaceConfigDeployer(serviceClient).Deploy();
                        new AppActionDeployer(serviceClient).DeployButtons();
                        DeployPlugin.DeployCofacePlugin(serviceClient);
                        DeployPlugin.DeployCofaceSearchCompanyPlugin(serviceClient);
                        DeployCreditScorePlugin.Deploy(serviceClient);
                        DeployPlugin.DeployBppPlugin(serviceClient);
                        UpdateFormLayout.Update(serviceClient);
                        PublishEntity(serviceClient);
                        PublishWebResourceOnly(serviceClient);
                        break;
                    default:
                        Console.WriteLine($"❌ 未知命令: {command}");
                        ShowHelp();
                        return;
                }

                Console.WriteLine();
                Console.WriteLine("===== 部署完成 =====");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 错误: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        static void ShowHelp()
        {
            Console.WriteLine("用法: dotnet run <命令>");
            Console.WriteLine();
            Console.WriteLine("可用命令:");
            Console.WriteLine("  webresource          更新 mcs_credit_record.js WebResource");
            Console.WriteLine("  scoringcard-wr       更新 mcs_credit_scoringcard.js WebResource");
            Console.WriteLine("  tradestpayterm-wr    更新 mcs_trade_stpayterm.js WebResource");
            Console.WriteLine("  profile              更新信用画像 WebResource");
            Console.WriteLine("  appactions           部署 Modern Command Bar 按钮");
            Console.WriteLine("  tradestpayterm       部署 TradeStPayTerm Plugin（自动编号 + 保存校验）");
            Console.WriteLine("  coface               部署 CofaceDataSyncPlugin");
            Console.WriteLine("  coface-search        创建 Custom Action 并部署 CofaceSearchCompanyPlugin");
            Console.WriteLine("  coface-config        部署 CofaceCountryConfig 及内部评分项目标记");
            Console.WriteLine("  coface-html          部署 mcs_coface_company_search.html");
            Console.WriteLine("  creditscore          部署 CreditScore Plugin");
            Console.WriteLine("  bpp                  部署 BPP Integration Plugin");
            Console.WriteLine("  probe                探测BPP框架实现");
            Console.WriteLine("  test-bpp-start       测试调用mcs_bppstartapi");
            Console.WriteLine("  download-assembly    下载指定Plugin Assembly并反编译");
            Console.WriteLine("  formlayout           更新表单布局（添加BPP字段）");
            Console.WriteLine("  publish              发布 mcs_credit_record 实体");
            Console.WriteLine("  publish-webresource  发布画像 WebResource");
            Console.WriteLine("  query-user-org       查询当前用户（邱正卫）的组织/事业部信息");
            Console.WriteLine("  fill-user-org <guid> 为当前用户（邱正卫）补 mcs_useraccount.mcs_orgid");
            Console.WriteLine("  setup-ltc-overdue    为 LTC客户-1 设置逾期数据（触发场景 3）");
            Console.WriteLine("  setup-ltc-blacklist  为 LTC客户-1 设置黑名单并清除逾期数据（触发场景 4）");
            Console.WriteLine("  all                  执行上述所有操作");
            Console.WriteLine();
            Console.WriteLine("示例:");
            Console.WriteLine("  dotnet run webresource");
            Console.WriteLine("  dotnet run appactions");
            Console.WriteLine("  dotnet run all");
        }

        static void UpdateWebResource(ServiceClient service)
        {
            Console.WriteLine(">>> 更新 WebResource: mcs_credit_record.js");

            var jsPath = "/Users/peterqiu/Work/AIWorkSpace/SanYi/Code/Customizations/WebResources/JS/mcs_credit_record.js";
            var jsContent = File.ReadAllText(jsPath);

            var query = new QueryExpression("webresource")
            {
                ColumnSet = new ColumnSet("webresourceid", "name", "content"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("name", ConditionOperator.Equal, "mcs_credit_record.js") }
                }
            };

            var results = service.RetrieveMultiple(query);
            if (results.Entities.Count == 0)
            {
                Console.WriteLine("  未找到Web资源，跳过");
                return;
            }

            var webResource = results.Entities[0];
            var bytes = Encoding.UTF8.GetBytes(jsContent);
            webResource["content"] = Convert.ToBase64String(bytes);

            service.Update(webResource);
            Console.WriteLine($"  ✅ WebResource已更新");

            // 更新后使用通用发布服务发布单个 WebResource
            Console.WriteLine($">>> 发布 WebResource: mcs_credit_record.js...");
            new WebResourceService(service).PublishWebResources("mcs_credit_record.js");
            Console.WriteLine($"  ✅ mcs_credit_record.js 发布成功");
        }

        static void UpdateScoringCardWebResource(ServiceClient service)
        {
            Console.WriteLine(">>> 更新 WebResource: mcs_credit_scoringcard.js");

            var jsPath = "/Users/peterqiu/Work/AIWorkSpace/SanYi/Code/Customizations/WebResources/JS/mcs_credit_scoringcard.js";
            var jsContent = File.ReadAllText(jsPath);

            var query = new QueryExpression("webresource")
            {
                ColumnSet = new ColumnSet("webresourceid", "name", "content"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("name", ConditionOperator.Equal, "mcs_credit_scoringcard.js") }
                }
            };

            var results = service.RetrieveMultiple(query);
            if (results.Entities.Count == 0)
            {
                Console.WriteLine("  未找到Web资源，跳过");
                return;
            }

            var webResource = results.Entities[0];
            var bytes = Encoding.UTF8.GetBytes(jsContent);
            webResource["content"] = Convert.ToBase64String(bytes);

            service.Update(webResource);
            Console.WriteLine($"  ✅ WebResource已更新");
        }

        static void UpdateTradeStPayTermWebResource(ServiceClient service)
        {
            Console.WriteLine(">>> 更新 WebResource: mcs_trade_stpayterm.js");

            var jsPath = "/Users/peterqiu/Work/AIWorkSpace/SanYi/Code/Customizations/WebResources/JS/mcs_trade_stpayterm.js";
            var webResourceService = new WebResourceService(service);

            try
            {
                webResourceService.UpdateFromFile("mcs_trade_stpayterm.js", jsPath);
                Console.WriteLine($"  ✅ WebResource已更新");

                // 更新后使用通用发布服务发布单个 WebResource
                Console.WriteLine($">>> 发布 WebResource: mcs_trade_stpayterm.js...");
                webResourceService.PublishWebResources("mcs_trade_stpayterm.js");
                Console.WriteLine($"  ✅ mcs_trade_stpayterm.js 发布成功");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ 更新或发布失败: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"    内部异常: {ex.InnerException.Message}");
                }
            }
        }

        static void DeployCofaceHtmlWebResource(ServiceClient service)
        {
            Console.WriteLine(">>> 部署 WebResource: mcs_coface_company_search.html");

            var htmlPath = "/Users/peterqiu/Work/AIWorkSpace/SanYi/Code/Customizations/WebResources/HTML/mcs_coface_company_search.html";
            var webResourceService = new WebResourceService(service);

            try
            {
                var existing = webResourceService.QueryByName("mcs_coface_company_search.html");
                if (existing == null)
                {
                    var id = webResourceService.Create(
                        "mcs_coface_company_search.html",
                        "Coface 企业搜索弹窗",
                        1, // HTML
                        File.ReadAllText(htmlPath));
                    Console.WriteLine($"  ✅ HTML WebResource 已创建: {id}");
                }
                else
                {
                    webResourceService.UpdateFromFile("mcs_coface_company_search.html", htmlPath);
                    Console.WriteLine($"  ✅ HTML WebResource 已更新");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ HTML WebResource 部署失败: {ex.Message}");
            }
        }

        static void PublishEntity(ServiceClient service)
        {
            Console.WriteLine(">>> 发布实体: mcs_credit_record...");
            try
            {
                new D365ToolCommon.Publishing.PublishingService(service).PublishEntities("mcs_credit_record");
                Console.WriteLine("  ✅ 实体发布成功");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ 实体发布失败: {ex.Message}");
                PublishWebResourceOnly(service);
            }
        }

        static void PublishWebResourceOnly(ServiceClient service)
        {
            Console.WriteLine(">>> 尝试只发布WebResource...");
            var webResourceService = new WebResourceService(service);
            var resourceNames = new[]
            {
                "mcs_credit_profile.html",
                "mcs_credit_wheel.html",
                "mcs_credit_record.js",
                "mcs_credit_scoringcard.js",
                "mcs_coface_company_search.html",
                "mcs_trade_stpayterm.js"
            };

            try
            {
                // 使用通用 WebResource 发布服务
                webResourceService.PublishWebResources(resourceNames);
                Console.WriteLine("  ✅ WebResource 发布成功");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ WebResource 发布失败: {ex.Message}");

                // 降级：尝试只发布已有的 JS WebResource，验证发布机制本身是否正常
                try
                {
                    Console.WriteLine(">>> 降级尝试：只发布 JS WebResource...");
                    webResourceService.PublishWebResources("mcs_trade_stpayterm.js");
                    Console.WriteLine("  ✅ JS WebResource 发布成功（HTML 可能需手动发布）");
                }
                catch (Exception ex2)
                {
                    Console.WriteLine($"  ❌ JS WebResource 也发布失败: {ex2.Message}");
                }
            }
        }

        static void UpdateProfileWebResources(ServiceClient service)
        {
            var webResources = new[]
            {
                ("mcs_credit_profile.html", "/Users/peterqiu/Work/AIWorkSpace/SanYi/Code/Customizations/WebResources/HTML/mcs_credit_profile.html"),
                ("mcs_credit_wheel.html", "/Users/peterqiu/Work/AIWorkSpace/SanYi/Code/Customizations/WebResources/HTML/mcs_credit_wheel.html")
            };

            foreach (var (name, path) in webResources)
            {
                Console.WriteLine($">>> 更新 WebResource: {name}");
                try
                {
                    var content = File.ReadAllText(path);
                    var query = new QueryExpression("webresource")
                    {
                        ColumnSet = new ColumnSet("webresourceid", "name", "content"),
                        Criteria = new FilterExpression
                        {
                            Conditions = { new ConditionExpression("name", ConditionOperator.Equal, name) }
                        }
                    };

                    var results = service.RetrieveMultiple(query);
                    if (results.Entities.Count == 0)
                    {
                        Console.WriteLine($"  ⚠️ 未找到Web资源 {name}，跳过更新（请先在D365中创建WebResource）");
                        continue;
                    }

                    var webResource = results.Entities[0];
                    var bytes = Encoding.UTF8.GetBytes(content);
                    webResource["content"] = Convert.ToBase64String(bytes);

                    service.Update(webResource);
                    Console.WriteLine($"  ✅ {name} 已更新");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ❌ {name} 更新失败: {ex.Message}");
                }
            }
        }
    }
}
