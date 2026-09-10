using Microsoft.Crm.Sdk.Messages;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using D365MetadataTool;
using D365ToolCommon.Connection;
using D365ToolCommon.Data;
using D365ToolCommon.Translation;
using D365ToolCommon.WebResource;
using D365ToolCommon.Security;

class Program
{
    // DEV环境默认URL
    static string GetDefaultUrl() => D365ConnectionFactory.ResolveUrl();

    static async Task Main(string[] args)
    {
        Console.WriteLine("=== D365 实体管理工具 ===");
        var url = D365ConnectionFactory.ResolveUrl();
        Console.WriteLine($"目标环境: {url}");
        Console.WriteLine("用法:");
        Console.WriteLine("  dotnet run create <实体定义文件.json>   - 从JSON文件创建实体和字段");
        Console.WriteLine("  dotnet run check <解决方案名>           - 检查解决方案中的实体");
        Console.WriteLine("  dotnet run add <实体名> <解决方案名>    - 添加实体到解决方案");
        Console.WriteLine("  dotnet run remove <实体名> <解决方案名> - 从解决方案移除实体");
        Console.WriteLine("  dotnet run publish [实体名]             - 发布指定实体（禁止无参数全局发布）");
        Console.WriteLine("  dotnet run delete-field <实体名> <字段名> - 删除字段");
        Console.WriteLine("  dotnet run list-fields <实体名>         - 列出实体所有字段");
        Console.WriteLine("  dotnet run update-entity-displayname <实体名> <中文> [英文] - 更新实体显示名称（多语言）");
        Console.WriteLine("  dotnet run set-entity-label <实体名> <中文> <英文> - 设置实体显示名称（Web API PUT，中英双语）");
        Console.WriteLine("  dotnet run update-field-displayname <实体名> <字段名> <中文> [英文] - 更新字段显示名称（多语言）");
        Console.WriteLine("  dotnet run set-field-label <实体名> <字段名> <中文> <英文> - 设置字段显示名称（Web API PUT，中英双语）");
        Console.WriteLine("  dotnet run update-field-required <实体名> <字段名> <true|false> - 更新字段必填性");
        Console.WriteLine("  dotnet run update-field-range <实体名> <字段名> <最小值> <最大值> - 更新 Money 字段取值范围");
        Console.WriteLine("  dotnet run update-field-format <实体名> <字段名> <text|url|...> - 更新 String 字段格式（如审批链接改 url 渲染为超链接）");
        Console.WriteLine("  dotnet run update-field-default <实体名> <字段名> <默认值> - 更新字段默认值（Picklist/Decimal）");
        Console.WriteLine("  dotnet run update-field-description <实体名> <字段名> <描述> - 更新字段描述");
        Console.WriteLine("  dotnet run get-entity-displayname <实体名> - 查询实体显示名称（诊断）");
        Console.WriteLine("  dotnet run export <解决方案名> <路径>    - 导出解决方案为 ZIP");
        Console.WriteLine("  dotnet run export-translations <解决方案唯一名> <输出ZIP路径> - 导出 translations ZIP（D365 标准翻译导出）");
        Console.WriteLine("  dotnet run import-translations <translations ZIP 路径> - 导入 translations ZIP（D365 标准翻译导入）");
        Console.WriteLine("  dotnet run create-credit-items          - 创建评分项目测试数据(22条)");
        Console.WriteLine("  dotnet run query-credit-item-descs      - 查询评分项目说明(mcs_itemdesc)");
        Console.WriteLine("  dotnet run create-qualitative-enums     - 创建定性枚举值测试数据(30条)");
        Console.WriteLine("  dotnet run fix-qualitative-enums-mapping - 整理国别/行业风险枚举值为三一标准L/M/H/O");
        Console.WriteLine("  dotnet run fix-percentage-scoring-cards - 修复百分比/比率类评分卡区间量纲（NetProfit/DebtRatio/LatePaymentIndex）");
        Console.WriteLine("  dotnet run cleanup-orphan-sectors-scoring-cards - 删除 Sectors 评分卡中无法匹配枚举值的孤儿记录");
        Console.WriteLine("  dotnet run dedup-credititem-values      - 清理 mcs_credititem_value 重复记录（保留最早，迁移引用）");
        Console.WriteLine("  dotnet run update-credit-record <scoreid> [status] - 更新信用评估记录状态(默认→12)");
        Console.WriteLine("  dotnet run simulate-bpp-callback <scoreid> [Approved|Rejected|Withdrawn|Abandoned] - 模拟BPP回调触发审批逻辑");
        Console.WriteLine("  dotnet run simulate-fca-bpp-callback <申请单编号|ID> [Approved|Rejected|Withdrawn|Abandoned] - 模拟BPP回调触发厂端授信额度调整申请审批逻辑");
        Console.WriteLine("  dotnet run test-fsm-bpp               - 融资管理BPP回调Plugin全场景验证（立项通过/方案通过/方案驳回/撤回）");
        Console.WriteLine("  dotnet run test-app-notification <用户domainname> [标题] - 发送测试小铃铛通知（SendAppNotification，Bug #1654 预研）");
        Console.WriteLine("  dotnet run test-tradestpayterm          - 测试成交条件样板库 Plugin（自动编号/校验，状态流转已按 Bug #1834 停用）");
        Console.WriteLine("  dotnet run query-plugin-steps <类名>    - 查询已注册的 Plugin Steps");
        Console.WriteLine("  dotnet run query-plugin-namespace <前缀> - 查询命名空间下所有 Plugin Steps");
        Console.WriteLine("  dotnet run register-plugin-image <StepId> <Image名称> <Image别名> <字段列表> - 为Plugin Step注册PreEntityImage");
        Console.WriteLine("  dotnet run disable-plugin-step <StepId>   - 停用指定 Plugin Step");
        Console.WriteLine("  dotnet run delete-plugin-step <StepId>    - 删除指定 Plugin Step");
        Console.WriteLine("  dotnet run delete-plugin-type <TypeId>    - 删除指定 Plugin Type");
        Console.WriteLine("  dotnet run recreate-uat-sync-steps <DLL路径> - Bug#1834修复专用：删除DEV误创建Sync Type/Step后，按UAT原GUID重建");
        Console.WriteLine("  dotnet run create-plugin-step-with-id <StepId> <TypeId> <Message> <Entity> <Stage> - 按指定GUID创建Plugin Step");
        Console.WriteLine("  dotnet run fix-dev-scoring-cards        - 重建DEV评分卡配置（补全mcs_itemid/mcs_datatype/mcs_cardname）");
        Console.WriteLine("  dotnet run fix-scoring-card-typeids     - 批量修复评分卡配置(mcs_typeid)根据mcs_credititem.mcs_group");
        Console.WriteLine("  dotnet run fix-scoring-card-display-fields [试跑条数] - 批量补全评分卡带出字段(mcs_cardname=评分项目名称, mcs_typeid按mcs_group映射)，仅更新缺失记录");
        Console.WriteLine("  dotnet run remove-duplicate-scoring-cards - 删除DEV1评分卡配置重复记录");
        Console.WriteLine("  dotnet run add-overdue-model-sa         - 为SA老客户评分卡补入OverdueModel(预计损失率)30分配置");
        Console.WriteLine("  dotnet run update-overdue-model-qualitative - 禅道#2090 OverdueModel改定性+缺失档（评分项目/枚举/评分卡重建/迟付指数缺失档）");
        Console.WriteLine("  dotnet run export-webresource <名称> <路径> - 导出 WebResource 内容");
        Console.WriteLine("  dotnet run update-webresource <名称> <文件路径> - 更新 WebResource 内容");
        Console.WriteLine("  dotnet run list-webresources [前缀]    - 列出 WebResource（默认前缀 ms_languagefile）");
        Console.WriteLine("  dotnet run list-form-webresources <实体> - 列出实体主窗体引用的 JS WebResource");
        Console.WriteLine("  dotnet run deploy-webresource <名称> <文件路径> <类型> [显示名] - 创建或更新 WebResource 并发布");
        Console.WriteLine("    类型: 1=HTML, 2=CSS, 3=JScript, 4=XML, 5=PNG, 6=JPG, 7=GIF, 8=XAP, 9=XSL, 10=ICO, 11=SVG, 12=RESX");
        Console.WriteLine("  dotnet run add-webresource-to-solution <WebResource名称> <解决方案唯一名> - 将 WebResource 加入解决方案");
        Console.WriteLine("  dotnet run test-common                  - 隔离测试 D365ToolCommon 共享库（自动创建/删除测试实体）");
        Console.WriteLine("  dotnet run cleanup-test-common          - 仅清理 D365ToolCommon 测试实体");
        Console.WriteLine("  dotnet run clear-account-credit-fields <客户名称> - 清空客户及客户主数据上的8个信用字段（用于测试）");
        Console.WriteLine("  dotnet run check-fix-masterdata <客户名称> - 检查并修复客户主数据上的基础字段");
        Console.WriteLine("  dotnet run set-masterdata-isdd <客户名称> <true|false> - 设置客户主数据重点尽调标志");
Console.WriteLine("  dotnet run set-masterdata-creditvalid <客户名称> <true|false> - 设置客户主数据信用评估有效状态");
        Console.WriteLine("  dotnet run show-profile <客户名称> - 查询客户画像关键属性");
        Console.WriteLine("  dotnet run sync-profile <源客户名称> <目标客户名称> - 同步客户画像关键属性（用于测试）");
        Console.WriteLine("  dotnet run simulate-approval <客户名称/编码> <评估编码> - 模拟审批通过，将信用分/等级回写客户主数据");
        Console.WriteLine("  dotnet run set-countrycode <客户名称/编码> <国家代码> - 设置客户主数据国家代码（用于测试）");
        Console.WriteLine("  dotnet run set-cofaceid <评估编码> <cofaceId> - 设置信用评估记录的科法斯客户代码");
        Console.WriteLine("  dotnet run export-coface-data <输出目录>  - 导出 Coface NACE映射和汇率配置数据");
        Console.WriteLine("  dotnet run import-coface-data <数据目录>  - 导入 Coface 基础数据到当前环境");
        Console.WriteLine("  dotnet run diagnose-credit-record <评估编码> - 诊断 credit record 数据集成问题");
        Console.WriteLine("  dotnet run list-entities <前缀>        - 列出指定前缀的实体");
        Console.WriteLine("  dotnet run create-test-salesorder <客户名称> - 为客户创建一条测试销售订单（用于老客户判定）");
        Console.WriteLine("  dotnet run create-credit-testdata <客户名称> - 为客户创建信用评估测试数据（销售订单 + 逾期在外货款）");
        Console.WriteLine("  dotnet run create-credit-record <客户名称> - 为客户创建一条新的信用评估记录");
        Console.WriteLine("  dotnet run list-app-actions [前缀]       - 列出 App Action (Modern Command Bar 按钮)");
        Console.WriteLine("  dotnet run list-security-roles [关键字]  - 列出安全角色");
        Console.WriteLine("  dotnet run check-role-privileges <角色关键字> - 查询安全角色权限明细（只读，重点核对实体/Excel导入导出权限）");
        Console.WriteLine("  dotnet run list-custom-apis [关键字]     - 列出 Custom API（含参数与响应属性）");
        Console.WriteLine("  dotnet run check-solution-customapi <解决方案名> - 检查解决方案包含的 Custom API");
        Console.WriteLine("  dotnet run deploy-tradestpayterm-api <DLL路径> [Plugin类名] - 部署成交条件样板库查询 Custom API");
        Console.WriteLine("  dotnet run bind-customapi <CustomAPI唯一名> <Plugin类名> - 重新绑定 Custom API 的 Plugin Type（不改 Assembly 内容）");
        Console.WriteLine("  dotnet run delete-tradestpayterm-api          - 删除成交条件样板库查询 Custom API");
        Console.WriteLine("  dotnet run deploy-fcaquota-api <DLL路径> [Plugin类名] - 部署厂端授信余额调整 Custom API");
        Console.WriteLine("  dotnet run delete-fcaquota-api                - 删除厂端授信余额调整 Custom API");
        Console.WriteLine("  dotnet run test-fcaquota-api <客户编码> <金额> <环节> <动作> [合同编码] [订单编码] - 测试厂端授信余额调整 Custom API");
        Console.WriteLine("  dotnet run deploy-cofaceorder-api <DLL路径> [Plugin类名] - 部署 Coface 系统内下单 Custom API");
        Console.WriteLine("  dotnet run delete-cofaceorder-api             - 删除 Coface 系统内下单 Custom API");
        Console.WriteLine("  dotnet run test-cofaceorder-api <信用评估记录ID> - 测试 Coface 系统内下单 Custom API");
        Console.WriteLine("  dotnet run deploy-riskexposure-api <DLL路径> [Plugin类名] - 部署风险敞口计算 Custom API");
        Console.WriteLine("  dotnet run delete-riskexposure-api                - 删除风险敞口计算 Custom API");
        Console.WriteLine("  dotnet run test-riskexposure-api <类型> <客户编码> <风险赊销金额> <签约占用金额> [合同编码] - 测试风险敞口计算 Custom API");
        Console.WriteLine("  dotnet run test-tradestpayterm-api <buId> <subId> <countryCode> <prdGroupId> <buyerCode> - 测试成交条件样板库查询 Custom API");
        Console.WriteLine("  dotnet run query-tradestpayterm-samples [条数] - 查询成交条件样板库样本数据");
        Console.WriteLine("  dotnet run create-tradestpayterm-testdata    - 创建一条生效的成交条件样板库测试数据");
        Console.WriteLine("  dotnet run test-tradestpayterm-share      - 已停用：审批共享逻辑已按 Bug #1834 注释");
        Console.WriteLine("  dotnet run share-tradestpayterm-pending   - 已停用：待审批状态已按 Bug #1834 停用");
        Console.WriteLine("  dotnet run rebuild-tradestpayterm-grade-fields [环境] - 重建成交条件样板库客户分类/客户等级为多选选项集");
        Console.WriteLine("  dotnet run recreate-fca-decimal-fields  - 将 mcs_fca_proc 的 mcs_modelgrant/mcs_initigrant 从 Money 重建为 Decimal");
        Console.WriteLine("  dotnet run query-optionset <实体名> <字段名>       - 查询选项集字段的标签");
        Console.WriteLine("  dotnet run list-transaction-currencies   - 列出 D365 标准交易货币及汇率");
        Console.WriteLine("  dotnet run test-coface-exchange-rate [币种列表] - 测试 Coface 汇率读取与转换（默认 USD,EUR,CNY,JPY,VND,XXX）");
        Console.WriteLine("  dotnet run test-upload-api <文件路径> <accountId> [mcp|mcs|all] - 测试上传 Custom API");
        Console.WriteLine("  dotnet run fix-form-lookup <实体名>    - 修复主窗体 Lookup 控件 classid");
        Console.WriteLine("  dotnet run query-contracts [accountId]  - 查询 mcs_contract 合同数据分布（只读诊断）");
        Console.WriteLine("  dotnet run query-contract-products <accountId> - 查询客户合同明细产品字段（只读诊断）");
        Console.WriteLine("  dotnet run query-optionset-labels <实体名> <字段名> [langId] - 查询选项集本地化标签");
        Console.WriteLine("  dotnet run update-optionset-labels <实体名> <字段名> <labels.json> [langId] - 更新选项集标签");
        Console.WriteLine("  dotnet run delete-option <实体名> <字段名> <选项值> - 删除选项集单个选项值（存量数据需先修复）");
        Console.WriteLine("  dotnet run create-credit-profile-fields  - 创建客户画像所需缺失字段（mcs_blacklist/mcs_creditgrant）");
        Console.WriteLine("  dotnet run list-number-configs [实体名] - 查询系统自动编号配置（只读）");
        Console.WriteLine("  dotnet run create-number-config <实体名> <属性名> <前缀模板> <数字模板> <序列号长度> [序号开始] [使用序列号服务true|false] - 新增自动编号配置");
        Console.WriteLine("  dotnet run delete-number-config <实体名> <属性名> - 删除指定实体的自动编号配置");
        Console.WriteLine("  dotnet run test-number-config <实体名> [属性名] - 创建并删除一条测试记录验证编号生成");
        Console.WriteLine("  dotnet run register-number-config-step <实体名> - 为实体注册通用自动编号 Plugin Step");
        Console.WriteLine("  dotnet run check-release <清单.json> [--with-fields] - 通用发版 Solution 归属自检（只读，--with-fields 逐字段核对）");
        Console.WriteLine("  dotnet run check-solution-coverage <源Solution唯一名> [实体包Solution唯一名] - 以源 Solution 为真相源，核对组件是否已分布到发版包（只读）");
        Console.WriteLine("  dotnet run list-solution-components <Solution唯一名> - 列出 Solution 全部组件（类型+名称+ObjectId，只读）");
        Console.WriteLine("  dotnet run check-webresource-release <基准Solution唯一名> --target <目标环境URL> - WebResource 发版排查：存在性/内容一致性/非托管Active层遮挡（只读）");
        Console.WriteLine("  dotnet run query-sitemap [sitemapId|名称关键字] - 查询 SiteMap 记录、App↔SiteMap 对应关系及解决方案分层（只读；单个时导出 XML 到 /tmp）");
        Console.WriteLine("  dotnet run query-app-components <App名称关键字> [实体名前缀] - 列出 App 包含的组件（只读）");
        Console.WriteLine("  dotnet run list-workflows [关键字] - 列出工作流/BPF 流程定义（只读）");
        Console.WriteLine("  dotnet run add-solution-component <componentType> <objectId> <Solution唯一名> - 通用加组件（写操作，需用户明确授权）");
        Console.WriteLine("  dotnet run check-solution-deps <Solution唯一名1> [Solution唯一名2] ... - 模拟导出时的缺少必需组件依赖检查（只读）");
        Console.WriteLine("  dotnet run check-step-assembly [Solution唯一名] - 跨包依赖检查：包内每个 Step 的实现类/程序集是否同包（只读，默认 McsPlugin，2026-08-20 #1641 防线）");
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

                    case "list-solutions":
                        manager.ListSolutions();
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

                    case "deploy-ribbon":
                        if (args.Length < 4)
                        {
                            Console.WriteLine("用法: dotnet run deploy-ribbon <实体名> <RibbonDiffXml片段文件路径> <载体Solution唯一名> [工作目录] [幂等前缀]");
                            Console.WriteLine("  示例: dotnet run deploy-ribbon mcs_fsm_data ../../Customizations/Ribbon/mcs_fsm_data.ribbon.xml entity_20260713 /tmp/ribbon_fsm");
                            Console.WriteLine("  说明: 导出载体Solution→合并实体RibbonDiffXml→重打包→非托管导入→发布实体（幂等）");
                            return;
                        }
                        manager.DeployRibbonDiff(args[1], args[2], args[3],
                            args.Length >= 5 ? args[4] : Path.Combine(Path.GetTempPath(), "ribbon_" + args[1]),
                            args.Length >= 6 ? args[5] : null);
                        break;

                    case "import-solution":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run import-solution <Solution ZIP路径>");
                            Console.WriteLine("  说明: 非托管叠加导入，导入后不自动发布，需另行 publish <实体名>");
                            return;
                        }
                        manager.ImportSolution(args[1]);
                        break;

                    case "get-entity-ribbon":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run get-entity-ribbon <实体名> <输出XML路径>");
                            Console.WriteLine("  说明: 读取实体生效 Ribbon（RetrieveEntityRibbonRequest，只读诊断）");
                            return;
                        }
                        {
                            var ribbonResp = (RetrieveEntityRibbonResponse)service.Execute(new RetrieveEntityRibbonRequest
                            {
                                EntityName = args[1],
                                RibbonLocationFilter = RibbonLocationFilters.All
                            });
                            var raw = ribbonResp.CompressedEntityXml;
                            string ribbonText;
                            try
                            {
                                // CompressedEntityXml 实为 ZIP 包，内含 RibbonXml.xml
                                using var ms = new MemoryStream(raw);
                                using var zip = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Read);
                                var entry = zip.GetEntry("RibbonXml.xml") ?? zip.Entries[0];
                                using var sr = new StreamReader(entry.Open());
                                ribbonText = sr.ReadToEnd();
                            }
                            catch
                            {
                                try
                                {
                                    using var ms = new MemoryStream(raw);
                                    using var gz = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionMode.Decompress);
                                    using var sr = new StreamReader(gz);
                                    ribbonText = sr.ReadToEnd();
                                }
                                catch { ribbonText = System.Text.Encoding.UTF8.GetString(raw); }
                            }
                            File.WriteAllText(args[2], ribbonText);
                            Console.WriteLine($"✅ 生效 Ribbon 已导出: {args[2]}（{ribbonText.Length} 字符）");
                        }
                        break;

                    case "publish-webresource":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run publish-webresource <WebResource名称1> [WebResource名称2] ...");
                            return;
                        }
                        new D365ToolCommon.WebResource.WebResourceService(service).PublishWebResources(args.Skip(1).ToArray());
                        Console.WriteLine($"✅ WebResource 发布成功: {string.Join(", ", args.Skip(1))}");
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

                    case "fix-fsm-resource-product11":
                        FixFsmResourceProduct11(service, args.Length >= 2 && args[1].Equals("apply", StringComparison.OrdinalIgnoreCase));
                        break;

                    case "fix-fsm-transaction-currency":
                        // 禅道 #1781：存量记录标准币种 transactioncurrencyid 同步为融资币种 mcs_fsm_currency
                        FixFsmTransactionCurrency(service, args.Length >= 2 && args[1].Equals("apply", StringComparison.OrdinalIgnoreCase));
                        break;

                    case "set-fsm-bppstatus":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run set-fsm-bppstatus <融资编号> <1申请|2审批中|3通过|4驳回|clear>");
                            Console.WriteLine("  示例: dotnet run set-fsm-bppstatus FSM202608010001 1   # 回到申请状态并清空审批状态码（解锁表单字段）");
                            return;
                        }
                        SetFsmBppStatus(service, args[1], args[2]);
                        break;

                    case "simulate-bpp-callback":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run simulate-bpp-callback <scoreid> [Approved|Rejected|Withdrawn|Abandoned]");
                            Console.WriteLine("  示例: dotnet run simulate-bpp-callback SCO202607010005");
                            Console.WriteLine("  示例: dotnet run simulate-bpp-callback SCO202607010005 Rejected");
                            return;
                        }
                        SimulateBppCallback(service, args[1], args.Length >= 3 ? args[2] : "Approved");
                        break;

                    case "simulate-fca-bpp-callback":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run simulate-fca-bpp-callback <申请单编号|ID> [Approved|Rejected|Withdrawn|Abandoned]");
                            Console.WriteLine("  示例: dotnet run simulate-fca-bpp-callback FCA202607080001");
                            Console.WriteLine("  示例: dotnet run simulate-fca-bpp-callback FCA202607080001 Rejected");
                            return;
                        }
                        SimulateFcaBppCallback(service, args[1], args.Length >= 3 ? args[2] : "Approved");
                        break;

                    case "test-fsm-bpp":
                        TestFsmBpp(service);
                        break;

                    case "test-tradestpayterm":
                        TestTradeStPayTerm(service);
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
                        QuerySystemConfigurations(service, args.Length >= 2 ? args[1] : null);
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

                    case "query-credit-trace":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run query-credit-trace <scoreid>");
                            return;
                        }
                        QueryCreditTrace(service, args[1]);
                        break;

                    case "query-scoring-card":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run query-scoring-card <scoreid>");
                            return;
                        }
                        QueryScoringCard(service, args[1]);
                        break;

                    case "fix-review-fields":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run fix-review-fields <scoreid>");
                            return;
                        }
                        FixReviewFields(service, args[1]);
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

                    case "disable-plugin-step":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run disable-plugin-step <StepId>");
                            return;
                        }
                        DisablePluginStep(service, args[1]);
                        break;

                    case "recreate-uat-sync-steps":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run recreate-uat-sync-steps <DLL路径>");
                            return;
                        }
                        RecreateUatSyncSteps(service, args[1]);
                        break;

                    case "create-plugin-step-with-id":
                        if (args.Length < 6)
                        {
                            Console.WriteLine("用法: dotnet run create-plugin-step-with-id <StepId> <TypeId> <Message> <Entity> <Stage>");
                            return;
                        }
                        CreatePluginStepWithId(service, args[1], args[2], args[3], args[4], int.Parse(args[5]));
                        break;

                    case "list-plugin-assemblies":
                        string? asmPrefix = args.Length >= 2 ? args[1] : null;
                        ListPluginAssemblies(service, asmPrefix);
                        break;

                    case "query-entity-solutions":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run query-entity-solutions <实体逻辑名>");
                            return;
                        }
                        QueryEntitySolutions(service, args[1]);
                        break;

                    case "delete-plugin-step":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run delete-plugin-step <StepId>");
                            Console.WriteLine("  示例: dotnet run delete-plugin-step 15a18fe4-4e69-f111-ab0c-6045bd1c0eeb");
                            return;
                        }
                        service.Delete("sdkmessageprocessingstep", Guid.Parse(args[1]));
                        Console.WriteLine($"✅ Plugin Step 已删除: {args[1]}");
                        break;

                    case "set-step-state":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run set-step-state <StepId> <enable|disable>");
                            Console.WriteLine("  示例: dotnet run set-step-state 15a18fe4-4e69-f111-ab0c-6045bd1c0eeb disable");
                            return;
                        }
                        var stepIdForState = Guid.Parse(args[1]);
                        bool enableStep = args[2].Equals("enable", StringComparison.OrdinalIgnoreCase);
                        service.Execute(new SetStateRequest
                        {
                            EntityMoniker = new EntityReference("sdkmessageprocessingstep", stepIdForState),
                            State = new OptionSetValue(enableStep ? 0 : 1),
                            Status = new OptionSetValue(enableStep ? 1 : 2)
                        });
                        Console.WriteLine($"✅ Plugin Step 已{(enableStep ? "启用" : "停用")}: {args[1]}");
                        break;

                    case "delete-plugin-type":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run delete-plugin-type <PluginTypeId>");
                            Console.WriteLine("  示例: dotnet run delete-plugin-type b2d63491-c14d-4d18-b8ac-36b0818de6dd");
                            return;
                        }
                        var pluginTypeIdToDelete = Guid.Parse(args[1]);
                        // 先删除该 Type 下的所有 Step
                        var stepQueryForType = new QueryExpression("sdkmessageprocessingstep")
                        {
                            ColumnSet = new ColumnSet("sdkmessageprocessingstepid", "name"),
                            Criteria = new FilterExpression
                            {
                                Conditions = { new ConditionExpression("plugintypeid", ConditionOperator.Equal, pluginTypeIdToDelete) }
                            }
                        };
                        foreach (var st in service.RetrieveMultiple(stepQueryForType).Entities)
                        {
                            service.Delete("sdkmessageprocessingstep", st.Id);
                            Console.WriteLine($"  [-] Step: {st.GetAttributeValue<string>("name")} ({st.Id})");
                        }
                        service.Delete("plugintype", pluginTypeIdToDelete);
                        Console.WriteLine($"✅ Plugin Type 已删除: {args[1]}");
                        break;

                    case "clear-account-credit-fields":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run clear-account-credit-fields <客户名称>");
                            Console.WriteLine("  示例: dotnet run clear-account-credit-fields \"LTC客户-1\"");
                            return;
                        }
                        var clearHelper = new ClearAccountCreditFieldsHelper(service);
                        clearHelper.ClearByName(args[1]);
                        break;

                    case "check-fix-masterdata":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run check-fix-masterdata <客户名称>");
                            Console.WriteLine("  示例: dotnet run check-fix-masterdata \"LTC客户-1\"");
                            return;
                        }
                        var fixHelper = new CheckAndFixAccountMasterDataHelper(service);
                        fixHelper.CheckAndFixByAccountName(args[1]);
                        break;

                    case "set-masterdata-isdd":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run set-masterdata-isdd <客户名称> <true|false>");
                            Console.WriteLine("  示例: dotnet run set-masterdata-isdd \"LTC客户-1\" true");
                            return;
                        }
                        var setIsddHelper = new SetMasterDataIsddHelper(service);
                        setIsddHelper.SetByAccountName(args[1], bool.TryParse(args[2], out var isDDValue) ? isDDValue : true);
                        break;

                    case "set-masterdata-creditvalid":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run set-masterdata-creditvalid <客户名称> <true|false>");
                            Console.WriteLine("  示例: dotnet run set-masterdata-creditvalid \"LTC客户-1\" false");
                            return;
                        }
                        var setCreditValidHelper = new SetMasterDataCreditValidHelper(service);
                        setCreditValidHelper.SetByAccountName(args[1], bool.TryParse(args[2], out var creditValidValue) ? creditValidValue : true);
                        break;

                    case "show-profile":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run show-profile <客户名称>");
                            Console.WriteLine("  示例: dotnet run show-profile \"LTC客户-1\"");
                            return;
                        }
                        new SyncAccountProfileHelper(service).ShowProfile(args[1]);
                        break;

                    case "customer-coface":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run customer-coface <客户编号或名称关键字>");
                            Console.WriteLine("  示例: dotnet run customer-coface 0214006316");
                            return;
                        }
                        new CustomerCofaceIdHelper(service).Query(args[1]);
                        break;

                    case "clear-customer-coface":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run clear-customer-coface <客户编号|客户主数据GUID>");
                            Console.WriteLine("  示例: dotnet run clear-customer-coface 0200001567");
                            return;
                        }
                        new CustomerCofaceIdHelper(service).Clear(args[1]);
                        break;

                    case "set-customer-country":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run set-customer-country <客户编号|客户主数据GUID> <国家编码>");
                            Console.WriteLine("  示例: dotnet run set-customer-country 0210050681 PL");
                            return;
                        }
                        new CustomerCofaceIdHelper(service).SetCountryCode(args[1], args[2]);
                        break;

                    case "set-customer-coface":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run set-customer-coface <客户编号|客户主数据GUID> <科法斯客户代码>");
                            Console.WriteLine("  示例: dotnet run set-customer-coface 0214006316 icon#5415240");
                            return;
                        }
                        if (Guid.TryParse(args[1], out var masterDataGuid))
                            new CustomerCofaceIdHelper(service).SetById(masterDataGuid, args[2]);
                        else
                            new CustomerCofaceIdHelper(service).SetBySapNumber(args[1], args[2]);
                        break;

                    case "sync-profile":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run sync-profile <源客户名称> <目标客户名称>");
                            Console.WriteLine("  示例: dotnet run sync-profile \"LTC客户-1\" \"DENSHA INDOGUNA JAYA\"");
                            return;
                        }
                        new SyncAccountProfileHelper(service).SyncProfile(args[1], args[2]);
                        break;

                    case "simulate-approval":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run simulate-approval <客户名称/编码> <评估编码>");
                            Console.WriteLine("  示例: dotnet run simulate-approval AID202307060000 SCO202606260004");
                            return;
                        }
                        new SyncAccountProfileHelper(service).SimulateApproval(args[1], args[2]);
                        break;

                    case "set-countrycode":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run set-countrycode <客户名称/编码> <国家代码>");
                            Console.WriteLine("  示例: dotnet run set-countrycode AID202307060000 PL");
                            return;
                        }
                        new SyncAccountProfileHelper(service).SetCountryCode(args[1], args[2]);
                        break;

                    case "set-cofaceid":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run set-cofaceid <评估编码> <cofaceId>");
                            Console.WriteLine("  示例: dotnet run set-cofaceid SCO202607020005 icon#5415240");
                            return;
                        }
                        SetCofaceId(service, args[1], args[2]);
                        break;

                    case "set-category":
                        if (args.Length < 4)
                        {
                            Console.WriteLine("用法: dotnet run set-category <客户名称> <accountCategory数值> <accountLevel数值>");
                            Console.WriteLine("  示例: dotnet run set-category \"LTC客户-1\" 20 4");
                            Console.WriteLine("  说明: 20=三一终端客户, 4=Diamond");
                            return;
                        }
                        if (!int.TryParse(args[2], out var catValue) || !int.TryParse(args[3], out var lvlValue))
                        {
                            Console.WriteLine("❌ accountCategory 和 accountLevel 必须是整数");
                            return;
                        }
                        new SyncAccountProfileHelper(service).SetCategory(args[1], catValue, lvlValue);
                        break;

                    case "export-coface-data":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run export-coface-data <输出目录>");
                            Console.WriteLine("  示例: dotnet run export-coface-data ./coface-data");
                            return;
                        }
                        var syncHelper = new CofaceDataSyncHelper(service);
                        Directory.CreateDirectory(args[1]);
                        syncHelper.ExportToFile("mcs_coface_nace_mapping", Path.Combine(args[1], "mcs_coface_nace_mapping.json"));
                        syncHelper.ExportToFile("mcs_coface_exchange_rate", Path.Combine(args[1], "mcs_coface_exchange_rate.json"));
                        break;

                    case "export-entity-data":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run export-entity-data <实体名> <输出JSON文件路径>");
                            Console.WriteLine("  示例: D365_URL=https://sany-uat.crm5.dynamics.com dotnet run export-entity-data mcs_credit_items /tmp/mcs_credit_items.json");
                            return;
                        }
                        new CofaceDataSyncHelper(service).ExportToFile(args[1], args[2]);
                        break;

                    case "import-entity-data":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run import-entity-data <实体名> <JSON文件路径>");
                            Console.WriteLine("  说明: 目标环境已有数据时跳过（防重复）；系统字段/主键/状态字段自动剔除");
                            Console.WriteLine("  示例: D365_URL=https://sany.crm5.dynamics.com dotnet run import-entity-data mcs_credit_items /tmp/mcs_credit_items.json");
                            return;
                        }
                        new CofaceDataSyncHelper(service).ImportFromFile(args[1], args[2]);
                        break;

                    case "import-coface-data":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run import-coface-data <数据目录>");
                            Console.WriteLine("  示例: D365_URL=https://sany-uat.crm5.dynamics.com dotnet run import-coface-data ./coface-data");
                            return;
                        }
                        var importHelper = new CofaceDataSyncHelper(service);
                        importHelper.CleanAndImport("mcs_coface_nace_mapping", Path.Combine(args[1], "mcs_coface_nace_mapping.json"));
                        importHelper.CleanAndImport("mcs_coface_exchange_rate", Path.Combine(args[1], "mcs_coface_exchange_rate.json"));
                        break;

                    case "diagnose-credit-record":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run diagnose-credit-record <评估编码>");
                            Console.WriteLine("  示例: D365_URL=https://sany-uat.crm5.dynamics.com dotnet run diagnose-credit-record SCO202606170003");
                            return;
                        }
                        var diagHelper = new CreditRecordDiagnosticHelper(service);
                        diagHelper.DiagnoseByScoreId(args[1]);
                        break;

                    case "diagnose-credit-record-by-account":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run diagnose-credit-record-by-account <客户名称>");
                            Console.WriteLine("  示例: dotnet run diagnose-credit-record-by-account LTC客户-1");
                            return;
                        }
                        var diagHelperByAccount = new CreditRecordDiagnosticHelper(service);
                        diagHelperByAccount.DiagnoseByAccountName(args[1]);
                        break;

                    case "query-outstanding-sample":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run query-outstanding-sample <客户名称>");
                            Console.WriteLine("  示例: dotnet run query-outstanding-sample LTC客户-1");
                            return;
                        }
                        QueryOutstandingSample(service, args[1]);
                        break;

                    case "list-entities":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run list-entities <前缀>");
                            Console.WriteLine("  示例: dotnet run list-entities mcs_");
                            return;
                        }
                        var listHelper = new ListEntitiesHelper(service);
                        listHelper.ListByPrefix(args[1]);
                        break;

                    case "count-records":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run count-records <实体名1> [实体名2] ...");
                            Console.WriteLine("  示例: dotnet run count-records mcs_credit_items mcs_credit_scoringcard");
                            return;
                        }
                        var countHelper = new ListEntitiesHelper(service);
                        countHelper.CountRecords(args.Skip(1).ToArray());
                        break;

                    case "list-appmodules":
                        ListAppModules(service);
                        break;

                    case "create-test-salesorder":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run create-test-salesorder <客户名称>");
                            Console.WriteLine("  示例: dotnet run create-test-salesorder LTC客户-1");
                            return;
                        }
                        CreateTestSalesOrder(service, args[1]);
                        break;

                    case "create-credit-testdata":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run create-credit-testdata <客户名称>");
                            Console.WriteLine("  示例: dotnet run create-credit-testdata LTC客户-1");
                            return;
                        }
                        CreateCreditTestData(service, args[1]);
                        break;

                    case "create-sinosure-testdata":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run create-sinosure-testdata <客户名称>");
                            Console.WriteLine("  示例: dotnet run create-sinosure-testdata LTC客户-1");
                            return;
                        }
                        CreateSinosureTestData(service, args[1]);
                        break;

                    case "create-credit-record":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run create-credit-record <客户名称>");
                            Console.WriteLine("  示例: dotnet run create-credit-record LTC客户-1");
                            return;
                        }
                        CreateCreditRecord(service, args[1]);
                        break;

                    case "create-dd-testdata":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run create-dd-testdata <客户名称>");
                            Console.WriteLine("  示例: dotnet run create-dd-testdata LTC客户-1");
                            return;
                        }
                        CreateDDTestData(service, args[1]);
                        break;

                    case "create-fca-quota-testdata":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run create-fca-quota-testdata <客户名称>");
                            Console.WriteLine("  示例: dotnet run create-fca-quota-testdata LTC客户-1");
                            return;
                        }
                        CreateFcaQuotaTestData(service, args[1]);
                        break;

                    case "create-fca-quotaapp-testdata":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run create-fca-quotaapp-testdata <客户名称>");
                            Console.WriteLine("  示例: dotnet run create-fca-quotaapp-testdata LTC客户-1");
                            return;
                        }
                        CreateFcaQuotaAppTestData(service, args[1]);
                        break;

                    case "create-fsm-resource-testdata":
                        CreateFsmResourceTestData(service, args.Length >= 2 ? int.Parse(args[1]) : 1);
                        break;

                    case "create-fsm-source-testdata":
                        CreateFsmSourceTestData(service, args.Length >= 2 ? args[1] : "Kedai Kek");
                        break;

                    case "query-fca-quotaapp":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run query-fca-quotaapp <申请单编号|ID>");
                            Console.WriteLine("  示例: dotnet run query-fca-quotaapp FCA202607130002");
                            return;
                        }
                        QueryFcaQuotaAppResult(service, args[1]);
                        break;


                    case "list-app-actions":
                        // 用法: dotnet run list-app-actions [前缀] [--entity <实体名>]
                        string? prefix = null, entityFilter = null;
                        for (int i = 1; i < args.Length; i++)
                        {
                            if (args[i] == "--entity" && i + 1 < args.Length) entityFilter = args[++i];
                            else prefix = args[i];
                        }
                        ListAppActions(service, prefix, entityFilter);
                        break;

                    case "list-appaction-rules":
                        // 用法: dotnet run list-appaction-rules [前缀] - 列出按钮关联的经典显隐规则（只读）
                        ListAppActionRules(service, args.Length > 1 ? args[1] : null);
                        break;

                    case "list-security-roles":
                        string? roleKeyword = args.Length >= 2 ? args[1] : null;
                        ListSecurityRoles(service, roleKeyword);
                        break;

                    case "check-role-privileges":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run check-role-privileges <角色关键字>");
                            break;
                        }
                        CheckRolePrivileges(service, args[1]);
                        break;

                    case "list-custom-apis":
                        string? apiKeyword = args.Length >= 2 ? args[1] : null;
                        new QueryCustomApis(service).ListCustomApis(apiKeyword);
                        break;

                    case "check-solution-customapi":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run check-solution-customapi <解决方案名>");
                            return;
                        }
                        CheckSolutionCustomApis(service, args[1]);
                        break;

                    case "check-solution-webresources":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run check-solution-webresources <解决方案名> [前缀]");
                            return;
                        }
                        CheckSolutionWebResources(service, args[1], args.Length >= 3 ? args[2] : null);
                        break;

                    case "check-webresource-layer":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run check-webresource-layer <WebResource名称>");
                            return;
                        }
                        CheckWebResourceLayer(service, args[1]);
                        break;

                    case "check-webresource-active-layer":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run check-webresource-active-layer <WebResource名称>");
                            return;
                        }
                        CheckWebResourceActiveLayer(service, args[1]);
                        break;

                    case "check-solution-version":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run check-solution-version <解决方案唯一名>");
                            return;
                        }
                        CheckSolutionVersion(service, args[1]);
                        break;

                    case "get-token":
                        {
                            var tokenUrl = D365ConnectionFactory.ResolveUrl();
                            var token = await D365ConnectionFactory.GetAccessTokenAsync(tokenUrl);
                            File.WriteAllText("/tmp/d365_token.txt", token);
                            File.WriteAllText("/tmp/d365_curl_header.txt", $"Authorization: Bearer {token}\r\nAccept: application/json\r\nOData-MaxVersion: 4.0\r\nOData-Version: 4.0");
                            Console.WriteLine($"Token saved to /tmp/d365_token.txt and /tmp/d365_curl_header.txt (length={token.Length})");
                        }
                        break;

                    case "test-retrieve-solution-metadata":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run test-retrieve-solution-metadata <WebResource名称>");
                            return;
                        }
                        TestRetrieveSolutionMetadata(service, args[1]);
                        break;

                    case "deploy-tradestpayterm-api":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run deploy-tradestpayterm-api <DLL路径> [Plugin类名]");
                            return;
                        }
                        string apiDllPath = args[1];
                        // 默认类名与 DEV1 当前绑定一致（ExtensionApi.Sales Assembly）；本地独立 Assembly 验证时需显式传类名
                        string apiClassName = args.Length >= 3 ? args[2] : "SanyD365.D365ExtensionApi.Sales.Apis.TradeStPayTerm.QueryTradeStPayTermPlugin";
                        manager.RegisterPluginAssemblyOnly(apiDllPath, apiClassName);
                        var deployer = new D365MetadataTool.Services.CustomApiDeployer(service);
                        deployer.DeployTradeStPayTermQueryApi(apiClassName);
                        break;

                    case "bind-customapi":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run bind-customapi <CustomAPI唯一名> <Plugin类名>");
                            return;
                        }
                        new D365MetadataTool.Services.CustomApiDeployer(service).BindPluginType(args[1], args[2]);
                        break;

                    case "delete-tradestpayterm-api":
                        new D365MetadataTool.Services.CustomApiDeployer(service).DeleteCustomApi("mcs_QueryTradeStPayTerm");
                        break;

                    case "deploy-fcaquota-api":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run deploy-fcaquota-api <DLL路径> [Plugin类名]");
                            return;
                        }
                        string fcaDllPath = args[1];
                        string fcaClassName = args.Length >= 3 ? args[2] : "SanyD365.D365ExtensionApi.Sales.Apis.FactoryCredit.AdjustFcaQuotaBalancePlugin";
                        manager.RegisterPluginAssemblyOnly(fcaDllPath, fcaClassName);
                        new D365MetadataTool.Services.CustomApiDeployer(service).DeployFcaQuotaAdjustApi(fcaClassName);
                        break;

                    case "delete-fcaquota-api":
                        new D365MetadataTool.Services.CustomApiDeployer(service).DeleteCustomApi("mcs_AdjustFcaQuotaBalance");
                        break;

                    case "deploy-recordcredit-api":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run deploy-recordcredit-api <DLL路径> [Plugin类名]");
                            return;
                        }
                        string rcDllPath = args[1];
                        string rcClassName = args.Length >= 3 ? args[2] : "SanyD365.Plugins.CreditPool.Api.RecordCreditDetailPlugin";
                        manager.RegisterPluginAssemblyOnly(rcDllPath, rcClassName);
                        new D365MetadataTool.Services.CustomApiDeployer(service).DeployRecordCreditDetailApi(rcClassName);
                        break;

                    case "delete-recordcredit-api":
                        new D365MetadataTool.Services.CustomApiDeployer(service).DeleteCustomApi("mcs_recordCreditDetail");
                        break;

                    case "deploy-querycredit-api":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run deploy-querycredit-api <DLL路径> [Plugin类名]");
                            return;
                        }
                        string qcDllPath = args[1];
                        string qcClassName = args.Length >= 3 ? args[2] : "SanyD365.Plugins.CreditPool.Api.QueryCreditBalancePlugin";
                        manager.RegisterPluginAssemblyOnly(qcDllPath, qcClassName);
                        new D365MetadataTool.Services.CustomApiDeployer(service).DeployQueryCreditBalanceApi(qcClassName);
                        break;

                    case "delete-querycredit-api":
                        new D365MetadataTool.Services.CustomApiDeployer(service).DeleteCustomApi("mcs_queryCreditBalance");
                        break;

                    case "test-querycredit-api":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run test-querycredit-api <客户编码> [合同编码]");
                            return;
                        }
                        TestQueryCreditBalanceApi(service, args[1], args.Length >= 3 ? args[2] : "");
                        break;

                    case "test-recordcredit-api":
                        if (args.Length < 6)
                        {
                            Console.WriteLine("用法: dotnet run test-recordcredit-api <客户编码> <金额USD> <金额CNY> <环节> <动作> [creditType] [合同编码] [订单编码] [发货单编码] [解款明细guid] [解款单号]");
                            Console.WriteLine("  示例: dotnet run test-recordcredit-api 0210000680 1000 7100 7 3 FACTORY HT2026001 SO2026001 DEL2026001");
                            return;
                        }
                        TestRecordCreditDetailApi(service, args[1], args[2], args[3], args[4], args[5],
                            args.Length >= 7 ? args[6] : "", args.Length >= 8 ? args[7] : "", args.Length >= 9 ? args[8] : "",
                            args.Length >= 10 ? args[9] : "", args.Length >= 11 ? args[10] : "", args.Length >= 12 ? args[11] : "");
                        break;

                    case "test-fcaquota-api":
                        if (args.Length < 5)
                        {
                            Console.WriteLine("用法: dotnet run test-fcaquota-api <客户编码> <金额> <环节> <动作> [合同编码] [订单编码]");
                            Console.WriteLine("  示例: dotnet run test-fcaquota-api 0210000680 1000 6 3 HT2026001 SO2026001");
                            return;
                        }
                        TestFcaQuotaApi(service, args[1], args[2], args[3], args[4],
                            args.Length >= 6 ? args[5] : "", args.Length >= 7 ? args[6] : "");
                        break;

                    case "bind-coface-search-api":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run bind-coface-search-api <DLL路径>");
                            Console.WriteLine("  示例: dotnet run bind-coface-search-api C:\\Projects\\D365\\D365\\SanyD365.D365ExtensionApi.Sales\\bin\\Debug\\SanyD365.D365ExtensionApi.Sales.dll");
                            return;
                        }
                        string cofaceDllPath = args[1];
                        string cofaceClassName = "SanyD365.D365ExtensionApi.Sales.Apis.Coface.CofaceSearchCompanyApi";
                        manager.RegisterPluginAssemblyOnly(cofaceDllPath, cofaceClassName);
                        new D365MetadataTool.Services.CustomApiDeployer(service).BindPluginType("mcs_CofaceSearchCompany", cofaceClassName);
                        break;

                    case "delete-coface-search-action":
                        new D365MetadataTool.Services.CustomApiDeployer(service).DeleteCustomAction("mcs_CofaceSearchCompany");
                        break;

                    case "test-coface-search-api":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run test-coface-search-api <CompanyName> <CountryCode>");
                            Console.WriteLine("  示例: dotnet run test-coface-search-api Sany CN");
                            return;
                        }
                        TestCofaceSearchApi(service, args[1], args[2]);
                        break;

                    case "deploy-cofaceorder-api":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run deploy-cofaceorder-api <DLL路径> [Plugin类名]");
                            Console.WriteLine("  示例(本地独立Assembly验证): dotnet run deploy-cofaceorder-api Code/Customizations/Plugins/CofaceIntegration/bin/Debug/net462/SanyD365.Plugins.CofaceIntegration.dll SanyD365.Plugins.CofaceIntegration.Plugin.CofacePlaceOrderPlugin");
                            return;
                        }
                        string cofaceOrderDllPath = args[1];
                        // 默认类名与归并后远程主项目一致（Extension.Sales Assembly）；本地独立 Assembly 验证时需显式传类名
                        string cofaceOrderClassName = args.Length >= 3 ? args[2] : "SanyD365.D365Extension.Sales.Plugins.CofaceIntegration.CofacePlaceOrderPlugin";
                        manager.RegisterPluginAssemblyOnly(cofaceOrderDllPath, cofaceOrderClassName);
                        new D365MetadataTool.Services.CustomApiDeployer(service).DeployCofacePlaceOrderApi(cofaceOrderClassName);
                        break;

                    case "delete-cofaceorder-api":
                        new D365MetadataTool.Services.CustomApiDeployer(service).DeleteCustomApi("mcs_CofacePlaceOrder");
                        break;

                    case "rebind-cofaceorder-api":
                        {
                            // 仅重绑 Custom API 到既有 Plugin Type（不更新 Assembly，规避 Assembly 差异红线）
                            string rebindClassName = args.Length >= 2 ? args[1] : "SanyD365.D365Extension.Sales.Plugins.CofaceIntegration.CofacePlaceOrderPlugin";
                            new D365MetadataTool.Services.CustomApiDeployer(service).DeployCofacePlaceOrderApi(rebindClassName);
                            break;
                        }

                    case "test-cofaceorder-api":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run test-cofaceorder-api <信用评估记录ID>");
                            return;
                        }
                        TestCofacePlaceOrderApi(service, args[1]);
                        break;

                    case "deploy-riskexposure-api":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run deploy-riskexposure-api <DLL路径> [Plugin类名]");
                            Console.WriteLine("  示例(本地独立Assembly验证): dotnet run deploy-riskexposure-api Code/Customizations/Plugins/RiskExposure.Api/bin/Debug/net462/SanyD365.Plugins.RiskExposure.Api.dll SanyD365.Plugins.RiskExposure.Api.CalculateRiskExposurePlugin");
                            return;
                        }
                        string riskExposureDllPath = args[1];
                        string riskExposureClassName = args.Length >= 3 ? args[2] : "SanyD365.D365ExtensionApi.Sales.Apis.RiskExposure.CalculateRiskExposurePlugin";
                        manager.RegisterPluginAssemblyOnly(riskExposureDllPath, riskExposureClassName);
                        new D365MetadataTool.Services.CustomApiDeployer(service).DeployRiskExposureApi(riskExposureClassName);
                        break;

                    case "delete-riskexposure-api":
                        new D365MetadataTool.Services.CustomApiDeployer(service).DeleteCustomApi("mcs_CalcContractRiskExposure");
                        break;

                    case "test-riskexposure-api":
                        if (args.Length < 5)
                        {
                            Console.WriteLine("用法: dotnet run test-riskexposure-api <类型> <客户编码> <风险赊销金额> <签约占用金额> [合同编码]");
                            Console.WriteLine("  示例(A类): dotnet run test-riskexposure-api A 0210000680 10000 5000");
                            Console.WriteLine("  示例(B类): dotnet run test-riskexposure-api B 0210000680 10000 5000 HT2026001");
                            return;
                        }
                        TestRiskExposureApi(service, args[1], args[2], args[3], args[4], args.Length >= 6 ? args[5] : "");
                        break;

                    case "test-tradestpayterm-api":
                        if (args.Length < 6)
                        {
                            Console.WriteLine("用法: dotnet run test-tradestpayterm-api <buId> <subId> <countryCode> <prdGroupId> <buyerCode>");
                            return;
                        }
                        TestTradeStPayTermApi(service, args[1], args[2], args[3], args[4], args[5]);
                        break;

                    case "query-tradestpayterm-samples":
                        int sampleCount = args.Length >= 2 && int.TryParse(args[1], out int n) ? n : 5;
                        QueryTradeStPayTermSamples(service, sampleCount);
                        break;

                    case "create-tradestpayterm-testdata":
                        CreateTradeStPayTermTestData(service);
                        break;

                    case "check-record-share":
                        if (args.Length < 3 || !Guid.TryParse(args[1], out var shareRecordId) || !Guid.TryParse(args[2], out var sharePrincipalId))
                        {
                            Console.WriteLine("用法: dotnet run check-record-share <记录GUID> <用户/团队GUID>");
                            Console.WriteLine("  查询 POA 表，输出该记录共享给该 Principal 的权限掩码（1=Read,2=Write,3=Read+Write，无则未共享）");
                            return;
                        }
                        CheckRecordShare(service, shareRecordId, sharePrincipalId);
                        break;

                    case "test-tradestpayterm-share":
                        TestTradeStPayTermShare(service);
                        break;

                    case "share-tradestpayterm-pending":
                        ShareTradeStPayTermPending(service);
                        break;

                    case "test-app-notification":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run test-app-notification <用户domainname> [标题]");
                            Console.WriteLine("  向指定用户发送 D365 小铃铛（In-App Notification）测试通知，Bug #1654 预研");
                            return;
                        }
                        TestAppNotification(service, args[1], args.Length >= 3 ? string.Join(" ", args.Skip(2)) : null);
                        break;

                    case "setup-bu-team":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run setup-bu-team <mcs_bu记录ID> <用户domainname>");
                            Console.WriteLine("  为事业部创建/复用 BuTeam（<事业部名>-BuTeam），挂到 mcs_bu.mcs_buteamid，并把用户加入团队（禅道#1151 环境配套）");
                            return;
                        }
                        SetupBuTeam(service, args[1], args[2]);
                        break;

                    case "rebuild-tradestpayterm-grade-fields":
                        RebuildTradeStPayTermGradeFields(service, manager);
                        break;

                    case "remove-trade-stpayterm-grade-fields":
                        RemoveTradeStPayTermGradeFields(service, manager);
                        break;

                    case "fix-tradestpayterm-form":
                        FixTradeStPayTermForm(manager);
                        break;

                    case "list-transaction-currencies":
                        ListTransactionCurrencies(service);
                        break;

                    case "test-coface-exchange-rate":
                        string currencyList = args.Length >= 2 ? args[1] : "USD,EUR,CNY,JPY,VND,XXX";
                        TestCofaceExchangeRate(service, currencyList);
                        break;

                    case "test-upload-api":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run test-upload-api <文件路径> <accountId> [mcp|mcs|all]");
                            Console.WriteLine("  示例: dotnet run test-upload-api /tmp/test.pdf 3c67d74c-445a-f111-a825-7ced8de5b9c3");
                            return;
                        }
                        string apiType = args.Length >= 4 ? args[3].ToLower() : "all";
                        new UploadApiTester(service).TestUploadApi(args[1], args[2], apiType);
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

                    case "register-plugin-image":
                        if (args.Length < 5)
                        {
                            Console.WriteLine("用法: dotnet run register-plugin-image <StepId> <Image名称> <Image别名> <包含字段逗号分隔>");
                            Console.WriteLine("  示例: dotnet run register-plugin-image 152d0a83-8f72-f111-ab0e-7ced8de4eab4 PreImage PreImage mcs_status");
                            return;
                        }
                        manager.RegisterPluginPreImage(Guid.Parse(args[1]), args[2], args[3], args[4]);
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

                    case "register-tradestpayterm-temp":
                        // 注册临时 Assembly 的 TradeStPayTermValidationPlugin（Type 名加 _Temp 避开备用键 2601 冲突）
                        RegisterTradeStPayTermTempValidation(service);
                        break;

                    case "test-tradestpayterm-resolve":
                        TestTradeStPayTermResolve(service);
                        break;

                    case "test-tradestpayterm-required":
                        // 必填兜底 + 空值重复校验语义测试（2026-07-27，禅道#1282 衍生）
                        // --main：主 Assembly 回归模式（不停用主 Assembly Step，直接验证正式链路）
                        TestTradeStPayTermRequired(service, args.Length >= 2 && args[1] == "--main");
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

                    case "export-form-xml":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run export-form-xml <实体名> <输出文件路径>");
                            return;
                        }
                        ExportFormXml(service, args[1], args[2]);
                        break;

                    case "update-webresource":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run update-webresource <WebResource名称> <文件路径>");
                            return;
                        }
                        UpdateWebResource(service, args[1], args[2]);
                        break;

                    case "deploy-webresource":
                        if (args.Length < 4)
                        {
                            Console.WriteLine("用法: dotnet run deploy-webresource <WebResource名称> <文件路径> <类型> [显示名]");
                            Console.WriteLine("  类型: 1=HTML, 2=CSS, 3=JScript, 4=XML, 5=PNG, 6=JPG, 7=GIF, 8=XAP, 9=XSL, 10=ICO, 11=SVG, 12=RESX");
                            return;
                        }
                        DeployWebResource(service, args[1], args[2], int.Parse(args[3]), args.Length >= 5 ? args[4] : null);
                        break;

                    case "list-webresources":
                        {
                            var wrPrefix = args.Length >= 2 ? args[1] : "ms_languagefile";
                            var resources = new WebResourceService(service).ListByPrefix(wrPrefix);
                            Console.WriteLine($"=== 列出以 '{wrPrefix}' 开头的 WebResource ===");
                            Console.WriteLine($"找到 {resources.Count} 个 WebResource:");
                            foreach (var r in resources)
                            {
                                var typeValue = r.GetAttributeValue<OptionSetValue>("webresourcetype");
                                var typeName = typeValue?.Value switch
                                {
                                    1 => "HTML",
                                    2 => "CSS",
                                    3 => "JScript",
                                    4 => "XML",
                                    5 => "PNG",
                                    6 => "JPG",
                                    7 => "GIF",
                                    8 => "XAP",
                                    9 => "XSL",
                                    10 => "ICO",
                                    11 => "SVG",
                                    12 => "RESX",
                                    _ => $"Type({typeValue?.Value})"
                                };
                                Console.WriteLine($"  - {r["name"]} | 类型: {typeName} | 显示名: {r["displayname"]} | 托管: {r["ismanaged"]}");
                            }
                        }
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
                            Console.WriteLine("用法: dotnet run delete-field <实体名> <字段名> [--force]");
                            Console.WriteLine("  ⚠️ 删除字段可能会影响生产环境发布版本，默认需交互确认，--force 跳过确认");
                            return;
                        }
                        if (!ConfirmDeleteField(args[1], args[2], args.Skip(3).Any(a => a == "--force")))
                            break;
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

                    case "remove-fields-from-form":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run remove-fields-from-form <实体名> <字段名1> [字段名2...]");
                            return;
                        }
                        manager.RemoveFieldsFromForm(args[1], args.Skip(2).ToArray());
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
                            Console.WriteLine("用法: dotnet run update-entity-displayname <实体名> <中文> [英文]");
                            return;
                        }
                        if (args.Length >= 4)
                        {
                            manager.UpdateEntityDisplayName(args[1], args[2], args[3]);
                        }
                        else
                        {
                            manager.UpdateEntityDisplayName(args[1], args[2]);
                        }
                        break;

                    case "set-entity-label":
                        if (args.Length < 4)
                        {
                            Console.WriteLine("用法: dotnet run set-entity-label <实体名> <中文> <英文>");
                            return;
                        }
                        {
                            var entityFieldService = new D365ToolCommon.Metadata.MetadataFieldService(service);
                            await entityFieldService.SetEntityDisplayNameAsync(args[1], args[2], args[3]);
                        }
                        break;

                    case "update-field-displayname":
                        if (args.Length < 4)
                        {
                            Console.WriteLine("用法: dotnet run update-field-displayname <实体名> <字段名> <中文> [英文]");
                            return;
                        }
                        if (args.Length >= 5)
                        {
                            manager.UpdateAttributeDisplayName(args[1], args[2], args[3], args[4]);
                        }
                        else
                        {
                            manager.UpdateAttributeDisplayName(args[1], args[2], args[3]);
                        }
                        break;

                    case "set-field-label":
                        if (args.Length < 5)
                        {
                            Console.WriteLine("用法: dotnet run set-field-label <实体名> <字段名> <中文> <英文>");
                            return;
                        }
                        {
                            var fieldService = new D365ToolCommon.Metadata.MetadataFieldService(service);
                            await fieldService.SetFieldDisplayNameAsync(args[1], args[2], args[3], args[4]);
                        }
                        break;

                    case "update-field-required":
                        if (args.Length < 4 || !bool.TryParse(args[3], out var requiredValue))
                        {
                            Console.WriteLine("用法: dotnet run update-field-required <实体名> <字段名> <true|false>");
                            return;
                        }
                        await manager.UpdateAttributeRequiredLevel(args[1], args[2], requiredValue);
                        break;

                    case "update-field-range":
                        if (args.Length < 5 || !decimal.TryParse(args[3], out var rangeMin) || !decimal.TryParse(args[4], out var rangeMax))
                        {
                            Console.WriteLine("用法: dotnet run update-field-range <实体名> <字段名> <最小值> <最大值>");
                            Console.WriteLine("  示例: dotnet run update-field-range mcs_fca_quota mcs_sellerbalance -999999999999.99 999999999999.99");
                            return;
                        }
                        new D365ToolCommon.Metadata.MetadataFieldService(service).UpdateMoneyRange(args[1], args[2], rangeMin, rangeMax);
                        break;

                    case "update-decimal-range":
                        if (args.Length < 5 || !decimal.TryParse(args[3], out var decRangeMin) || !decimal.TryParse(args[4], out var decRangeMax))
                        {
                            Console.WriteLine("用法: dotnet run update-decimal-range <实体名> <字段名> <最小值> <最大值>");
                            Console.WriteLine("  示例: dotnet run update-decimal-range mcs_customer_tag mcs_itemintvalue1 -999999999 99999999999");
                            return;
                        }
                        new D365ToolCommon.Metadata.MetadataFieldService(service).UpdateDecimalRange(args[1], args[2], decRangeMin, decRangeMax);
                        break;

                    case "update-field-maxlength":
                        if (args.Length < 4 || !int.TryParse(args[3], out var maxLen))
                        {
                            Console.WriteLine("用法: dotnet run update-field-maxlength <实体名> <字段名> <最大长度>");
                            Console.WriteLine("  示例: dotnet run update-field-maxlength mcs_trade_stpayterm mcs_trade_stpaytermname 12");
                            return;
                        }
                        new D365ToolCommon.Metadata.MetadataFieldService(service).UpdateStringMaxLength(args[1], args[2], maxLen);
                        break;

                    case "update-field-format":
                        if (args.Length < 4 || !Enum.TryParse<StringFormat>(args[3], ignoreCase: true, out var fieldFormat))
                        {
                            Console.WriteLine("用法: dotnet run update-field-format <实体名> <字段名> <text|url|email|phone|textarea|ticker|duration|timezone|language>");
                            Console.WriteLine("  示例: dotnet run update-field-format mcs_credit_record mcs_bpplink url");
                            return;
                        }
                        new D365ToolCommon.Metadata.MetadataFieldService(service).UpdateStringFormat(args[1], args[2], fieldFormat);
                        break;

                    case "update-field-description":
                        if (args.Length < 4)
                        {
                            Console.WriteLine("用法: dotnet run update-field-description <实体名> <字段名> <描述>");
                            return;
                        }
                        manager.UpdateAttributeDescription(args[1], args[2], args[3]);
                        break;

                    case "update-field-default":
                        if (args.Length < 4)
                        {
                            Console.WriteLine("用法: dotnet run update-field-default <实体名> <字段名> <默认值>");
                            return;
                        }
                        var defaultValueArg = args[3];
                        var fieldType = manager.GetAttributeType(args[1], args[2]);
                        if (fieldType == AttributeTypeCode.Picklist || fieldType == AttributeTypeCode.Boolean || fieldType == AttributeTypeCode.Integer)
                        {
                            if (!int.TryParse(defaultValueArg, out var intDefaultValue))
                            {
                                Console.WriteLine($"字段 {args[2]} 是 {fieldType} 类型，默认值必须是整数");
                                return;
                            }
                            await manager.UpdateAttributeDefaultValue(args[1], args[2], intDefaultValue);
                        }
                        else if (fieldType == AttributeTypeCode.Decimal || fieldType == AttributeTypeCode.Money || fieldType == AttributeTypeCode.Double)
                        {
                            if (!decimal.TryParse(defaultValueArg, out var decimalDefaultValue))
                            {
                                Console.WriteLine($"字段 {args[2]} 是 {fieldType} 类型，默认值必须是数字");
                                return;
                            }
                            await manager.UpdateAttributeDefaultValue(args[1], args[2], decimalDefaultValue);
                        }
                        else
                        {
                            Console.WriteLine($"字段 {args[2]} 类型 {fieldType} 暂不支持设置默认值");
                        }
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

                    case "export-translations":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run export-translations <解决方案唯一名> <输出ZIP路径>");
                            return;
                        }
                        new TranslationService(service).ExportTranslations(args[1], args[2]);
                        break;

                    case "import-translations":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run import-translations <translations ZIP 路径>");
                            return;
                        }
                        new TranslationService(service).ImportTranslations(args[1]);
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

                    case "delete-option":
                        if (args.Length < 4)
                        {
                            Console.WriteLine("用法: dotnet run delete-option <实体名> <字段名> <选项值>");
                            Console.WriteLine("  注意: 仅删选项定义，存量数据残留孤儿值需先修复");
                            return;
                        }
                        manager.DeleteOption(args[1], args[2], int.Parse(args[3]));
                        break;

                    case "update-form":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run update-form <实体名>");
                            return;
                        }
                        UpdateForm(manager, args[1]);
                        break;

                    case "add-form-field":
                        if (args.Length < 4)
                        {
                            Console.WriteLine("用法: dotnet run add-form-field <实体名> <字段名> <显示名> - 添加单个字段到主窗体（幂等，自动发布）");
                            return;
                        }
                        manager.UpdateMainForm(args[1], new Dictionary<string, string> { { args[2], args[3] } });
                        break;

                    case "update-form-field-label":
                        if (args.Length < 4)
                        {
                            Console.WriteLine("用法: dotnet run update-form-field-label <实体名> <字段名> <中文> [英文] - 更新主窗体字段单元格标签（2052/1033，自动发布）");
                            return;
                        }
                        manager.UpdateFormFieldLabel(args[1], args[2], args[3], args.Length >= 5 ? args[4] : null);
                        break;

                    case "replace-form-field":
                        if (args.Length < 5)
                        {
                            Console.WriteLine("用法: dotnet run replace-form-field <实体名> <旧字段名> <新字段名> <中文标签> [英文标签] - 原位替换主窗体字段（保留单元格位置，自动发布；2026-09-03 新增，禅道 #2138）");
                            return;
                        }
                        manager.ReplaceFormField(args[1], args[2], args[3], args[4], args.Length >= 6 ? args[5] : null);
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

                    case "clone-view":
                        if (args.Length < 6)
                        {
                            Console.WriteLine("用法: dotnet run clone-view <实体名> <新视图名> <过滤字段> <过滤值> <解决方案唯一名>");
                            Console.WriteLine("示例: dotnet run clone-view mcs_fca_proc \"生效启用\" mcs_status 3 entity_20260629_peter");
                            return;
                        }
                        {
                            string filterConditionXml = $"<condition attribute=\"{args[3]}\" operator=\"eq\" value=\"{args[4]}\" />";
                            manager.CloneDefaultView(args[1], args[2], filterConditionXml, args[5]);
                        }
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
                            Console.WriteLine("  不传实体名：仅注册 Assembly + PluginType，不创建 Step（2026-08-20 起，#1641 幽灵 Step 防线）");
                            return;
                        }
                        if (args.Length < 4)
                        {
                            Console.WriteLine("  ℹ️ 未指定实体，仅注册 Assembly + PluginType，不创建 Step");
                            manager.RegisterPluginAssemblyOnly(args[1], args[2]);
                            break;
                        }
                        manager.RegisterPlugin(args[1], args[2], args[3], "Create", 20, 0);
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
                            Console.WriteLine("用法: dotnet run register-step-only <PluginType类名> <实体名> <消息名> <阶段> [筛选属性] [Assembly名]");
                            Console.WriteLine("  阶段: 10=PreValidation, 20=PreOperation, 40=PostOperation");
                            Console.WriteLine("  [Assembly名]: 可选，同名 PluginType 存在于多个 Assembly 时按 assemblyname 精确匹配（2026-08-04 用户批准扩展）");
                            return;
                        }
                        string stepClassName = args[1];
                        string stepEntityName = args[2];
                        string stepMessageName = args[3];
                        int stepStageValue = int.Parse(args[4]);
                        string stepFilterAttr = args.Length >= 6 ? args[5] : null;
                        string stepAssemblyName = args.Length >= 7 ? args[6] : null;
                        
                        // 1. 查 PluginType ID（可选按 Assembly 名精确匹配，避免同名 Type 绑错 Assembly）
                        var ptQuery = new QueryExpression("plugintype")
                        {
                            ColumnSet = new ColumnSet("plugintypeid", "assemblyname"),
                            Criteria = new FilterExpression { Conditions = { new ConditionExpression("typename", ConditionOperator.Equal, stepClassName) } }
                        };
                        if (!string.IsNullOrEmpty(stepAssemblyName))
                            ptQuery.Criteria.Conditions.Add(new ConditionExpression("assemblyname", ConditionOperator.Equal, stepAssemblyName));
                        var ptResult = service.RetrieveMultiple(ptQuery);
                        if (ptResult.Entities.Count == 0) { Console.WriteLine($"错误: 找不到 PluginType: {stepClassName}" + (string.IsNullOrEmpty(stepAssemblyName) ? "" : $"（Assembly={stepAssemblyName}）")); return; }
                        if (ptResult.Entities.Count > 1) { Console.WriteLine($"错误: PluginType {stepClassName} 匹配到 {ptResult.Entities.Count} 条，请用第 6 参数指定 Assembly 名"); return; }
                        Guid stepPluginTypeId = ptResult.Entities[0].Id;
                        Console.WriteLine($"  PluginType: {stepPluginTypeId}（Assembly={ptResult.Entities[0].GetAttributeValue<string>("assemblyname")}）");

                        // 2026-08-20 #1641 防线：Custom API 实现类禁止再挂实体 Step
                        if (manager.IsPluginTypeBoundToCustomApi(stepPluginTypeId, out var stepBoundApis))
                        {
                            Console.WriteLine($"  ✗ 禁止注册实体 Step：该类已被 Custom API 绑定（{string.Join(", ", stepBoundApis)}）");
                            Console.WriteLine("    API 实现类不得兼职实体触发；确需实体触发逻辑请在主插件程序集新建独立类。");
                            return;
                        }
                        
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

                    case "fix-qualitative-enums-mapping":
                        FixQualitativeEnumsMapping(service);
                        break;

                    case "fix-percentage-scoring-cards":
                        FixPercentageScoringCards(service);
                        break;

                    case "cleanup-orphan-sectors-scoring-cards":
                        CleanupOrphanSectorsScoringCards(service);
                        break;

                    case "dedup-credititem-values":
                        DedupCreditItemValues(service);
                        break;

                    case "export-credititem-values":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run export-credititem-values <输出JSON文件路径>");
                            Console.WriteLine("  示例: D365_URL=https://dev1.crm5.dynamics.com dotnet run export-credititem-values /tmp/credititem_values.json");
                            return;
                        }
                        ExportCreditItemValues(service, args[1]);
                        break;

                    case "import-credititem-values":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run import-credititem-values <JSON文件路径>");
                            Console.WriteLine("  示例: D365_URL=https://sany-uat.crm5.dynamics.com dotnet run import-credititem-values /tmp/credititem_values.json");
                            return;
                        }
                        ImportCreditItemValues(service, args[1]);
                        break;

                    case "create-trade-pttype-sample-data":
                        manager.CreateTradePtTypeSampleData();
                        break;

                    case "create-trade-stpayterm-test":
                        manager.CreateTradeStPayTermTestRecord();
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

                    case "query-credit-item-descs":
                        QueryCreditItemDescs(service);
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
                                    string listValue = "";
                                    if (card.Contains("mcs_listvalue"))
                                    {
                                        var lvObj = card["mcs_listvalue"];
                                        if (lvObj is EntityReference lvRef)
                                        {
                                            try
                                            {
                                                var lvEnt = service.Retrieve("mcs_credititem_value", lvRef.Id, new ColumnSet("mcs_listvalue", "mcs_listname"));
                                                listValue = lvEnt.GetAttributeValue<string>("mcs_listvalue") ?? lvRef.Name ?? "";
                                            }
                                            catch
                                            {
                                                listValue = lvRef.Name ?? "";
                                            }
                                        }
                                        else if (lvObj != null)
                                        {
                                            listValue = lvObj.ToString() ?? "";
                                        }
                                    }
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

                    case "fix-scoring-card-typeids":
                        FixScoringCardTypeIds(service);
                        break;

                    case "fix-scoring-card-display-fields":
                        {
                            int fixLimit = 0;
                            if (args.Length >= 2) int.TryParse(args[1], out fixLimit);
                            FixScoringCardDisplayFields(service, fixLimit);
                        }
                        break;

                    case "remove-duplicate-scoring-cards":
                        RemoveDuplicateScoringCards(service);
                        break;

                    case "add-overdue-model-sa":
                        AddOverdueModelForSaExistingCustomer(service);
                        break;

                    case "update-overdue-model-qualitative":
                        UpdateOverdueModelToQualitative(service);
                        break;

                    case "recreate-fca-decimal-fields":
                        manager.RecreateFcaDecimalFields();
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

                    case "export-scoring-cards":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run export-scoring-cards <输出JSON文件路径>");
                            Console.WriteLine("  示例: D365_URL=https://dev1.crm5.dynamics.com dotnet run export-scoring-cards /tmp/scoring_cards.json");
                            return;
                        }
                        ExportScoringCards(service, args[1]);
                        break;

                    case "import-scoring-cards":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run import-scoring-cards <JSON文件路径>");
                            Console.WriteLine("  示例: D365_URL=https://sany-uat.crm5.dynamics.com dotnet run import-scoring-cards /tmp/scoring_cards.json");
                            return;
                        }
                        ImportScoringCards(service, args[1]);
                        break;

                    case "update-integer-range":
                        if (args.Length < 5)
                        {
                            Console.WriteLine("用法: dotnet run update-integer-range <实体名> <字段名> <最小值> <最大值>");
                            Console.WriteLine("  示例: dotnet run update-integer-range mcs_credit_scoringcard mcs_weight -100 100");
                            return;
                        }
                        if (!int.TryParse(args[3], out int intMin) || !int.TryParse(args[4], out int intMax))
                        {
                            Console.WriteLine("❌ 最小值和最大值必须是整数");
                            return;
                        }
                        manager.UpdateIntegerFieldRange(args[1], args[2], intMin, intMax);
                        break;

                    case "rename-plugin-step":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run rename-plugin-step <StepId> <新名称>");
                            return;
                        }
                        var renameStepId = Guid.Parse(args[1]);
                        var newStepName = args[2];
                        var renameStep = new Entity("sdkmessageprocessingstep", renameStepId);
                        renameStep["name"] = newStepName;
                        service.Update(renameStep);
                        Console.WriteLine($"✓ Step 已重命名: {renameStepId} -> {newStepName}");
                        break;

                    case "query-customapi-solution":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run query-customapi-solution <CustomAPI唯一名>");
                            return;
                        }
                        QueryCustomApiSolution(service, args[1]);
                        break;

                    case "fix-form-lookup":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run fix-form-lookup <实体名>");
                            Console.WriteLine("  示例: dotnet run fix-form-lookup mcs_trade_ptgrouptype");
                            return;
                        }
                        manager.FixFormLookupControls(args[1]);
                        break;

                    case "query-contracts":
                        manager.QueryContractsSummary(args.Length > 1 ? args[1] : null);
                        break;

                    case "import-tradestpayterm":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run import-tradestpayterm <导入JSON路径> [--dry-run] [--pilot N]");
                            Console.WriteLine("  说明: 逐行创建成交条件基线库记录（Lookup已在JSON中预解析为GUID），逐行输出成功/失败");
                            return;
                        }
                        ImportTradeStPayTerm(service, args[1],
                            args.Any(a => a == "--dry-run"),
                            args.SkipWhile(a => a != "--pilot").Skip(1).FirstOrDefault() is string pilotStr && int.TryParse(pilotStr, out int pilotN) ? pilotN : 0);
                        break;

                    case "query-records":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run query-records <实体名> <字段列表(逗号分隔)> [条数] [过滤字段=值(GUID/字符串/整数)]");
                            Console.WriteLine("  示例: dotnet run query-records mcs_order mcs_name,createdon 5");
                            return;
                        }
                        QueryRecords(service, args[1], args[2], args.Length >= 4 && int.TryParse(args[3], out int topN) ? topN : 5,
                            args.Length >= 5 && args[4].Contains('=') ? args[4] : null);
                        break;

                    case "delete-record":
                        if (args.Length < 3 || !Guid.TryParse(args[2], out var deleteGuid))
                        {
                            Console.WriteLine("用法: dotnet run delete-record <实体名> <记录GUID>");
                            return;
                        }
                        service.Delete(args[1], deleteGuid);
                        Console.WriteLine($"  ✓ 已删除 {args[1]} ({deleteGuid})");
                        break;

                    case "create-record":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run create-record <实体名> <JSON|@文件路径>");
                            Console.WriteLine("  类型后缀: 字段#int / #decimal / #money / #bool / #optionset / #optionsetcollection(逗号分隔) / #lookup(值=logicalName:guid)；无后缀按 string");
                            Console.WriteLine("  示例: dotnet run create-record mcs_trade_stpayterm '{\"mcs_buid\":\"BU-1018\",\"mcs_creditgrade#optionset\":100000000}'");
                            return;
                        }
                        CreateRecordFromJson(service, args[1], args[2]);
                        break;

                    case "update-record":
                        if (args.Length < 4 || !Guid.TryParse(args[2], out var updateGuid))
                        {
                            Console.WriteLine("用法: dotnet run update-record <实体名> <记录GUID> <JSON|@文件路径>");
                            Console.WriteLine("  类型后缀与 create-record 相同；#optionset 传 null 可清空选项集字段");
                            Console.WriteLine("  示例: dotnet run update-record mcs_fsm_data 8b690ba4-... '{\"mcs_bppstatus#optionset\":null}'");
                            return;
                        }
                        UpdateRecordFromJson(service, args[1], updateGuid, args[3]);
                        break;

                    case "list-failed-imports":
                        {
                            int topImports = args.Length >= 2 && int.TryParse(args[1], out int ti) ? ti : 30;
                            DateTime? since = args.Length >= 3 && DateTime.TryParse(args[2], out var sd) ? sd : (DateTime?)null;
                            ListFailedImports(service, topImports, since);
                        }
                        break;

                    case "query-import-log":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run query-import-log <Solution名称关键字> - 查最近一次导入的组件级告警/失败明细（只读）");
                            return;
                        }
                        QueryImportLog(service, args[1]);
                        break;

                    case "query-sitemap-layers":
                        if (args.Length < 2 || !Guid.TryParse(args[1], out var smGuid))
                        {
                            Console.WriteLine("用法: dotnet run query-sitemap-layers <sitemapId> - 查 sitemap 在哪些 Solution（含 Active 非托管层）中（只读）");
                            return;
                        }
                        QuerySitemapLayers(service, smGuid);
                        break;

                    case "query-contract-products":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run query-contract-products <accountId>");
                            Console.WriteLine("  示例: dotnet run query-contract-products 941757ed-a01b-ee11-8f6d-000d3a08d197");
                            return;
                        }
                        new ContractProductDiagnosticHelper(service).DiagnoseByAccountId(args[1]);
                        break;

                    case "set-contract-test-values":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run set-contract-test-values <accountId>");
                            Console.WriteLine("  示例: dotnet run set-contract-test-values 941757ed-a01b-ee11-8f6d-000d3a08d197");
                            return;
                        }
                        new ContractProductDiagnosticHelper(service).SetTestValuesByAccountId(args[1]);
                        break;

                    case "query-optionset-labels":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run query-optionset-labels <实体名> <字段名> [langId]");
                            return;
                        }
                        manager.QueryOptionSetLabels(args[1], args[2], args.Length > 3 && int.TryParse(args[3], out var lid) ? lid : null);
                        break;

                    case "update-optionset-labels":
                        if (args.Length < 4)
                        {
                            Console.WriteLine("用法: dotnet run update-optionset-labels <实体名> <字段名> <labels.json> [langId]");
                            return;
                        }
                        manager.UpdateOptionSetLabels(args[1], args[2], args[3], args.Length > 4 && int.TryParse(args[4], out var ulid) ? ulid : null);
                        break;

                    case "create-credit-profile-fields":
                        Console.WriteLine(">>> 创建客户画像所需缺失字段...");
                        manager.CreateBooleanField("mcs_customermasterdata", "mcs_blacklist", "黑名单客户", "用于厂端不予授信满足条件之一", "是", "否");
                        manager.CreateBooleanField("mcs_customermasterdata", "mcs_creditgrant", "不予授信客户", "用于厂端不予授信表标签", "是", "否");
                        Console.WriteLine(">>> 发布 mcs_customermasterdata 实体...");
                        manager.PublishEntity("mcs_customermasterdata");
                        Console.WriteLine("✅ 字段创建并发布完成");
                        break;

                    case "list-number-configs":
                        {
                            var targetEntity = args.Length >= 2 ? args[1] : null;
                            ListNumberGenerateConfigurations(service, targetEntity);
                        }
                        break;

                    case "create-number-config":
                        if (args.Length < 6)
                        {
                            Console.WriteLine("用法: dotnet run create-number-config <实体名> <属性名> <前缀模板> <数字模板> <序列号长度> [序号开始] [使用序列号服务true|false]");
                            Console.WriteLine("  示例: dotnet run create-number-config mcs_fca_mdlversion mcs_versionid 'V{$datetimeformat(false,yyyyMMdd)}' '{$prefix()}' 0");
                            Console.WriteLine("  示例: dotnet run create-number-config mcs_fca_proc mcs_doid 'FCM{$datetimeformat(false,yyyyMMdd)}' '{$prefix()}{$serialno()}' 4 1 true");
                            break;
                        }
                        {
                            int serialNoLength = int.Parse(args[5]);
                            int serialNoStart = args.Length >= 7 && int.TryParse(args[6], out var sns) ? sns : 1;
                            bool useSerialNoService = args.Length < 8 || !bool.TryParse(args[7], out var usns) ? true : usns;
                            CreateNumberGenerateConfiguration(service, args[1], args[2], args[3], args[4], serialNoLength, serialNoStart, useSerialNoService);
                        }
                        break;

                    case "delete-number-config":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run delete-number-config <实体名> <属性名>");
                            break;
                        }
                        DeleteNumberGenerateConfiguration(service, args[1], args[2]);
                        break;

                    case "test-number-config":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run test-number-config <实体名> [属性名] [--keep]（--keep 保留测试记录不删除）");
                            break;
                        }
                        {
                            var positional = args.Skip(1).Where(a => a != "--keep").ToArray();
                            TestNumberGenerateConfiguration(service, positional[0],
                                positional.Length >= 2 ? positional[1] : null, args.Contains("--keep"));
                        }
                        break;

                    case "register-number-config-step":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run register-number-config-step <实体名>");
                            break;
                        }
                        RegisterNumberGenerateStep(service, args[1]);
                        break;

                    case "test-fca-proc-activation":
                        TestFcaProcActivation(service);
                        break;

                    case "test-quota":
                        TestQuotaHelper.TestQuotaData(service);
                        break;

                    case "seed-quota":
                        TestQuotaHelper.SeedQuotaData(service, args.Length > 1 ? args[1] : "LTC客户-1");
                        break;

                    case "diagnose-profile":
                        TestQuotaHelper.DiagnoseProfileData(service, args.Length > 1 ? args[1] : "LTC客户-1");
                        break;

                    case "activate-approved-records":
                        TestQuotaHelper.ActivateApprovedRecords(service, args.Length > 1 ? args[1] : "LTC客户-1");
                        break;

                    case "copy-latest-tags-to-approved":
                        TestQuotaHelper.CopyLatestTagsToApprovedRecord(service, args.Length > 1 ? args[1] : "LTC客户-1");
                        break;

                    case "check-release":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run check-release <清单.json> [--with-fields]");
                            Console.WriteLine("  --with-fields: 额外核对实体全部 mcs_ 自定义字段是否已入包");
                            return;
                        }
                        CheckRelease(service, args[1], args.Any(a => a == "--with-fields"));
                        break;

                    case "check-solution-coverage":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run check-solution-coverage <源Solution唯一名> [实体包Solution唯一名]");
                            Console.WriteLine("  以源 Solution 为真相源，核对其组件是否已分布到发版包（只读）");
                            Console.WriteLine("  映射（开发手册 4.4）：WebResource→McsWebResource；OptionSet→McsOptionSet；普通工作流→McsAutomate；");
                            Console.WriteLine("    Custom API 本体/参数/响应及其实现 Assembly/Step→McsCustomAPI；其余 Assembly+Step→McsPlugin；");
                            Console.WriteLine("    实体/BPF/表单/App Action→实体包（每次发版新建，需传第二个参数才核对）；角色→role_XX（跳过）");
                            return;
                        }
                        CheckSolutionCoverage(service, args[1], args.Length > 2 ? args[2] : null);
                        break;

                    case "list-solution-components":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run list-solution-components <Solution唯一名>");
                            Console.WriteLine("  列出 Solution 全部组件（类型 + 名称 + ObjectId），只读");
                            return;
                        }
                        ListSolutionComponentsCmd(service, args[1]);
                        break;

                    case "check-webresource-release":
                        if (args.Length < 4 || args[2] != "--target")
                        {
                            Console.WriteLine("用法: dotnet run check-webresource-release <基准Solution唯一名> --target <目标环境URL>");
                            Console.WriteLine("  以当前环境(D365_URL)为基准，核对 Solution 内全部 WebResource 在目标环境的发版状态（只读）：");
                            Console.WriteLine("  ① 目标环境是否存在 ② 运行时内容是否与基准一致 ③ 是否被非托管 Active 层遮挡(content 覆盖)");
                            Console.WriteLine("  示例: D365_URL=DEV1 dotnet run check-webresource-release AllComponent_Peter_NoUAT --target https://sany-uat.crm5.dynamics.com");
                            return;
                        }
                        await CheckWebResourceRelease(service, args[1], args[3]);
                        break;

                    case "list-workflows":
                        ListWorkflows(service, args.Length > 1 ? args[1] : null);
                        break;

                    case "check-bpf-access":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run check-bpf-access <用户domainname> [BPF名称关键字=信用评估]");
                            Console.WriteLine("  只读诊断：用户角色 → 各角色对评估记录实体与BPF流程实体的Read权限 → 判断进度条不可见是否权限所致");
                            return;
                        }
                        CheckBpfAccess(service, args[1], args.Length > 2 ? args[2] : "信用评估");
                        break;

                    case "list-role-privileges":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run list-role-privileges <角色关键字> [实体名过滤]");
                            Console.WriteLine("  只读：查角色权限明细（读/写/建/删/追加/追加到/分派/共享 × 深度），传实体名则只看该实体");
                            return;
                        }
                        ListRolePrivileges(service, args[1], args.Length > 2 ? args[2] : null);
                        break;

                    case "query-user-permissions":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run query-user-permissions <用户domainname> [实体名过滤]");
                            Console.WriteLine("  只读：列出用户全部角色（直接/团队继承）+ 有效权限汇总（RetrieveUserPrivileges）");
                            return;
                        }
                        QueryUserPermissions(service, args[1], args.Length > 2 ? args[2] : null);
                        break;

                    case "set-role-privilege":
                        if (args.Length < 5)
                        {
                            Console.WriteLine("用法: dotnet run set-role-privilege <角色关键字> <实体名> <权限类型> <深度>");
                            Console.WriteLine("  权限类型: read|write|create|delete|append|appendto|assign|share");
                            Console.WriteLine("  深度: none(移除)|user(本人)|bu(本部门)|childbu(本部门及子部门)|org(组织)");
                            Console.WriteLine("  杂项权限: dotnet run set-role-privilege <角色> misc <杂项权限名> <org|none>，如 DocumentGeneration");
                            Console.WriteLine("  ⚠️ 写操作，需用户明确授权；同名角色多 BU 副本时会报错要求精确名称，绝不批量改");
                            return;
                        }
                        SetRolePrivilegeCommand(service, args[1], args[2], args[3], args[4]);
                        break;

                    case "assign-role":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run assign-role <用户domainname> <角色名>");
                            Console.WriteLine("  ⚠️ 写操作，需用户明确授权；幂等（已分配则跳过）");
                            return;
                        }
                        AssignOrRemoveRoleCommand(service, args[1], args[2], true);
                        break;

                    case "remove-role":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run remove-role <用户domainname> <角色名>");
                            Console.WriteLine("  ⚠️ 写操作，需用户明确授权；幂等（未分配则跳过）");
                            return;
                        }
                        AssignOrRemoveRoleCommand(service, args[1], args[2], false);
                        break;

                    case "query-sitemap":
                        QuerySitemaps(service, args.Length > 1 ? args[1] : null);
                        break;

                    case "query-app-components":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run query-app-components <App名称关键字> [实体名前缀]");
                            Console.WriteLine("  列出指定 App（appmodule）包含的组件，可选按实体名前缀过滤（只读）");
                            return;
                        }
                        QueryAppComponents(service, args[1], args.Length > 2 ? args[2] : null);
                        break;

                    case "add-solution-component":
                        if (args.Length < 4)
                        {
                            Console.WriteLine("用法: dotnet run add-solution-component <componentType> <objectId> <Solution唯一名> [nosub]");
                            Console.WriteLine("  通用加组件（写操作，需用户明确授权）；常用类型：1=实体 2=字段 6=Ribbon 9=OptionSet 10=关系 26=视图 29=工作流/BPF 60=SystemForm 61=WebResource 62=SiteMap 91=Assembly 92=Step 10156=App Action");
                            Console.WriteLine("  nosub：实体按「不包含子组件（含元数据）」加入（增量包用）");
                            return;
                        }
                        new D365ToolCommon.Solution.SolutionComponentService(service)
                            .AddComponentToSolution(Guid.Parse(args[2]), int.Parse(args[1]), args[3],
                                args.Length >= 5 && args[4] == "nosub");
                        break;

                    case "create-solution":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run create-solution <唯一名> <显示名> [版本]");
                            Console.WriteLine("  创建空 Solution（非托管，发布者固定为 MCS）；幂等，已存在则跳过（写操作，需用户明确授权）");
                            return;
                        }
                        new D365ToolCommon.Solution.SolutionComponentService(service)
                            .CreateSolution(args[1], args[2], Guid.Parse("421432de-d35a-41dc-87c7-3d73ad329d90"),
                                args.Length >= 4 ? args[3] : "1.0.0.0");
                        break;

                    case "set-component-behavior":
                        if (args.Length < 5)
                        {
                            Console.WriteLine("用法: dotnet run set-component-behavior <Solution唯一名> <componentType> <objectId> <behavior>");
                            Console.WriteLine("  设置实体组件 rootcomponentbehavior：0=含子组件 1=不含子组件含元数据 2=纯壳（写操作，需用户明确授权）");
                            return;
                        }
                        new D365ToolCommon.Solution.SolutionComponentService(service)
                            .SetRootComponentBehavior(args[1], int.Parse(args[2]), Guid.Parse(args[3]), int.Parse(args[4]));
                        break;

                    case "remove-solution-component":
                        if (args.Length < 4)
                        {
                            Console.WriteLine("用法: dotnet run remove-solution-component <componentType> <objectId> <Solution唯一名>");
                            Console.WriteLine("  通用移除组件（写操作，需用户明确授权；只移出 Solution，不删除环境中的组件本体）；类型同 add-solution-component");
                            return;
                        }
                        new D365ToolCommon.Solution.SolutionComponentService(service)
                            .RemoveComponentFromSolution(Guid.Parse(args[2]), int.Parse(args[1]), args[3]);
                        break;

                    case "check-solution-deps":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("用法: dotnet run check-solution-deps <Solution唯一名1> [Solution唯一名2] ...");
                            Console.WriteLine("  所有传入 Solution 视为本次一起发布的并集，检查其组件依赖但未随包的缺失项");
                            return;
                        }
                        CheckSolutionDependencies(service, args.Skip(1).ToArray());
                        break;

                    case "check-step-assembly":
                        CheckStepAssemblyCoverage(service, args.Length >= 2 ? args[1] : "McsPlugin");
                        break;

                    case "add-manifest-to-solution":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: dotnet run add-manifest-to-solution <清单.json> <Solution唯一名>");
                            Console.WriteLine("  按清单将实体/WebResource/PluginStep/CustomAPI/AppAction 查重后加入指定 Solution");
                            return;
                        }
                        AddManifestToSolution(service, args[1], args[2]);
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

    static void ExportScoringCards(ServiceClient service, string jsonPath)
    {
        Console.WriteLine("=== 导出评分卡配置 ===");

        var query = new QueryExpression("mcs_credit_scoringcard")
        {
            ColumnSet = new ColumnSet("mcs_categoryid", "mcs_credititem", "mcs_itemid", "mcs_itemname",
                "mcs_datatype", "mcs_minvalue", "mcs_maxvalue", "mcs_listvalue", "mcs_weight")
        };

        var result = service.RetrieveMultiple(query);
        Console.WriteLine($"读取到 {result.Entities.Count} 条记录");

        var records = result.Entities.Select(e => new ScoringCardImportRecord
        {
            CategoryId = e.GetAttributeValue<OptionSetValue>("mcs_categoryid")?.Value ?? 0,
            ItemCode = e.GetAttributeValue<string>("mcs_itemid") ?? "",
            ItemName = e.GetAttributeValue<string>("mcs_itemname") ?? "",
            DataType = e.GetAttributeValue<OptionSetValue>("mcs_datatype")?.Value ?? 0,
            Min = e.GetAttributeValue<decimal?>("mcs_minvalue"),
            Max = e.GetAttributeValue<decimal?>("mcs_maxvalue"),
            ListValue = e.GetAttributeValue<EntityReference>("mcs_listvalue")?.Name,
            Weight = e.GetAttributeValue<int>("mcs_weight")
        }).ToList();

        var json = System.Text.Json.JsonSerializer.Serialize(records,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(jsonPath, json);
        Console.WriteLine($"✅ 已导出到: {jsonPath}");
    }

    static void ImportScoringCards(ServiceClient service, string jsonPath)
    {
        if (!File.Exists(jsonPath))
        {
            Console.WriteLine($"❌ 文件不存在: {jsonPath}");
            return;
        }

        var json = File.ReadAllText(jsonPath);
        var records = System.Text.Json.JsonSerializer.Deserialize<List<ScoringCardImportRecord>>(json,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (records == null || records.Count == 0)
        {
            Console.WriteLine("❌ JSON 解析为空");
            return;
        }

        Console.WriteLine($"=== 评分卡全量导入 ===");
        Console.WriteLine($"JSON 记录数: {records.Count}");

        // 1. 将 ExternalRating 评分项目改为定量（Excel 标准为数值区间）
        UpdateExternalRatingItemToQuantitative(service);

        // 2. 确保 mcs_weight 允许负分（Excel 中存在 -1 / -3 扣分项）
        EnsureWeightFieldRange(service);

        // 3. 加载评分项目与枚举值映射
        var itemMap = LoadCreditItemsMap(service, out var itemGroupMap);
        var (enumByValue, enumByName) = LoadCreditItemValueMap(service);
        var groupTypeMap = GetScoringCardGroupTypeMap();

        // 检查所有 itemCode 是否存在
        var missingItems = records.Select(r => r.ItemCode).Distinct().Where(c => !itemMap.ContainsKey(c)).ToList();
        if (missingItems.Count > 0)
        {
            Console.WriteLine($"⚠️ 以下评分项目编码在 D365 中不存在，将跳过:");
            foreach (var c in missingItems) Console.WriteLine($"   - {c}");
        }

        // 3. 清空现有评分卡配置
        int deletedCount = DeleteAllScoringCards(service);
        Console.WriteLine($"已清空现有评分卡: {deletedCount} 条");

        // 4. 批量创建
        int createdCount = 0;
        int skipCount = 0;
        int index = 0;
        foreach (var rec in records)
        {
            index++;
            if (!itemMap.TryGetValue(rec.ItemCode, out var itemId))
            {
                Console.WriteLine($"  [{index}/{records.Count}] 跳过: 找不到评分项目 {rec.ItemCode}");
                skipCount++;
                continue;
            }

            var ent = new Entity("mcs_credit_scoringcard");
            ent["mcs_categoryid"] = new OptionSetValue(rec.CategoryId);
            ent["mcs_credititem"] = new EntityReference("mcs_credit_items", itemId);
            ent["mcs_itemid"] = rec.ItemCode;
            ent["mcs_itemname"] = rec.ItemName;
            ent["mcs_cardname"] = rec.ItemName;  // 评分卡名称=评分项目名称（与历史导入口径一致）
            if (itemGroupMap.TryGetValue(itemId, out var grp) && grp.HasValue && groupTypeMap.TryGetValue(grp.Value, out var typeVal))
                ent["mcs_typeid"] = new OptionSetValue(typeVal);  // 评分项目分类按 mcs_group 映射（与表单 JS 带出口径一致）
            ent["mcs_datatype"] = new OptionSetValue(rec.DataType);
            if (rec.Min.HasValue) ent["mcs_minvalue"] = rec.Min.Value;
            if (rec.Max.HasValue) ent["mcs_maxvalue"] = rec.Max.Value;

            if (!string.IsNullOrEmpty(rec.ListValue))
            {
                var enumKey = $"{rec.ItemCode}|{rec.ListValue}";
                if (enumByValue.TryGetValue(enumKey, out var enumId) || enumByName.TryGetValue(enumKey, out enumId))
                {
                    ent["mcs_listvalue"] = new EntityReference("mcs_credititem_value", enumId);
                }
                else
                {
                    Console.WriteLine($"  [{index}/{records.Count}] ⚠️ {rec.ItemCode} 找不到枚举值 '{rec.ListValue}'");
                    skipCount++;
                    continue;
                }
            }

            ent["mcs_weight"] = rec.Weight;

            try
            {
                service.Create(ent);
                createdCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [{index}/{records.Count}] ❌ 创建失败 {rec.ItemCode} ({rec.RawCriteria}): {ex.Message}");
                skipCount++;
            }
        }

        Console.WriteLine($"\n✅ 导入完成: 成功 {createdCount} 条, 跳过 {skipCount} 条");
    }

    static void EnsureWeightFieldRange(ServiceClient service)
    {
        try
        {
            var request = new RetrieveAttributeRequest
            {
                EntityLogicalName = "mcs_credit_scoringcard",
                LogicalName = "mcs_weight",
                RetrieveAsIfPublished = true
            };
            var response = (RetrieveAttributeResponse)service.Execute(request);
            if (response.AttributeMetadata is IntegerAttributeMetadata intAttr)
            {
                int currentMin = intAttr.MinValue ?? 0;
                int currentMax = intAttr.MaxValue ?? 0;
                if (currentMin > -100 || currentMax < 100)
                {
                    var updateAttr = new IntegerAttributeMetadata
                    {
                        LogicalName = "mcs_weight",
                        MinValue = -100,
                        MaxValue = 100
                    };
                    service.Execute(new UpdateAttributeRequest
                    {
                        EntityName = "mcs_credit_scoringcard",
                        Attribute = updateAttr
                    });
                    Console.WriteLine($"✅ 已更新 mcs_weight 范围为 [-100, 100]");

                    new D365ToolCommon.Publishing.PublishingService(service).PublishEntities("mcs_credit_scoringcard");
                    Console.WriteLine($"✅ 已发布实体 mcs_credit_scoringcard");
                }
                else
                {
                    Console.WriteLine($"mcs_weight 当前范围 [{currentMin}, {currentMax}]，无需调整");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ 检查/更新 mcs_weight 范围失败: {ex.Message}");
        }
    }

    static void UpdateExternalRatingItemToQuantitative(ServiceClient service)
    {
        var query = new QueryExpression("mcs_credit_items")
        {
            ColumnSet = new ColumnSet("mcs_credit_itemsid", "mcs_credit_itemsno", "mcs_datatype"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("mcs_credit_itemsno", ConditionOperator.Equal, "ExternalRating") }
            }
        };
        var result = service.RetrieveMultiple(query);
        if (result.Entities.Count == 0)
        {
            Console.WriteLine("⚠️ 未找到 ExternalRating 评分项目，跳过 datatype 更新");
            return;
        }

        var item = result.Entities[0];
        var currentDataType = item.GetAttributeValue<OptionSetValue>("mcs_datatype")?.Value;
        if (currentDataType == 100000000)
        {
            Console.WriteLine("ExternalRating 已是定量类型");
            return;
        }

        var update = new Entity("mcs_credit_items") { Id = item.Id };
        update["mcs_datatype"] = new OptionSetValue(100000000);
        service.Update(update);
        Console.WriteLine($"✅ 已更新 ExternalRating 为定量类型 (100000000)，原类型值: {currentDataType}");
    }

    static Dictionary<string, Guid> LoadCreditItemsMap(ServiceClient service)
    {
        return LoadCreditItemsMap(service, out _);
    }

    static Dictionary<string, Guid> LoadCreditItemsMap(ServiceClient service, out Dictionary<Guid, int?> groupByItemId)
    {
        var query = new QueryExpression("mcs_credit_items")
        {
            ColumnSet = new ColumnSet("mcs_credit_itemsid", "mcs_credit_itemsno", "mcs_itemname", "mcs_datatype", "mcs_group")
        };
        var result = service.RetrieveMultiple(query);
        var map = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        groupByItemId = new Dictionary<Guid, int?>();
        foreach (var e in result.Entities)
        {
            var id = e.Id;
            var no = e.GetAttributeValue<string>("mcs_credit_itemsno") ?? "";
            var name = e.GetAttributeValue<string>("mcs_itemname") ?? "";
            if (!string.IsNullOrEmpty(no)) map[no] = id;
            if (!string.IsNullOrEmpty(name)) map[name] = id;
            groupByItemId[id] = e.GetAttributeValue<OptionSetValue>("mcs_group")?.Value;
        }
        Console.WriteLine($"加载评分项目映射: {map.Count} 条");
        return map;
    }

    static (Dictionary<string, Guid> ByValue, Dictionary<string, Guid> ByName) LoadCreditItemValueMap(ServiceClient service)
    {
        // 先取评分项目 ID -> 编码
        var itemQuery = new QueryExpression("mcs_credit_items")
        {
            ColumnSet = new ColumnSet("mcs_credit_itemsid", "mcs_credit_itemsno")
        };
        var itemResult = service.RetrieveMultiple(itemQuery);
        var idToCode = new Dictionary<Guid, string>();
        foreach (var it in itemResult.Entities)
        {
            idToCode[it.Id] = it.GetAttributeValue<string>("mcs_credit_itemsno") ?? "";
        }

        var query = new QueryExpression("mcs_credititem_value")
        {
            ColumnSet = new ColumnSet("mcs_credititem_valueid", "mcs_credititemno", "mcs_listvalue", "mcs_listname")
        };
        var result = service.RetrieveMultiple(query);
        var byValue = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var byName = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in result.Entities)
        {
            var itemRef = e.GetAttributeValue<EntityReference>("mcs_credititemno");
            if (itemRef == null || !idToCode.TryGetValue(itemRef.Id, out var code) || string.IsNullOrEmpty(code)) continue;
            var val = e.GetAttributeValue<string>("mcs_listvalue") ?? "";
            var name = e.GetAttributeValue<string>("mcs_listname") ?? "";
            if (!string.IsNullOrEmpty(val)) byValue[$"{code}|{val}"] = e.Id;
            if (!string.IsNullOrEmpty(name)) byName[$"{code}|{name}"] = e.Id;
        }
        Console.WriteLine($"加载枚举值映射: value={byValue.Count} 条, name={byName.Count} 条");
        return (byValue, byName);
    }

    static int DeleteAllScoringCards(ServiceClient service)
    {
        int deletedCount = 0;
        var query = new QueryExpression("mcs_credit_scoringcard")
        {
            ColumnSet = new ColumnSet("mcs_credit_scoringcardid"),
            PageInfo = new PagingInfo { Count = 500, PageNumber = 1 }
        };
        EntityCollection result;
        do
        {
            result = service.RetrieveMultiple(query);
            foreach (var e in result.Entities)
            {
                try
                {
                    service.Delete("mcs_credit_scoringcard", e.Id);
                    deletedCount++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  删除失败 {e.Id}: {ex.Message}");
                }
            }
            if (result.MoreRecords)
            {
                query.PageInfo.PageNumber++;
                query.PageInfo.PagingCookie = result.PagingCookie;
            }
        } while (result.MoreRecords);
        return deletedCount;
    }

    static void ShowHelp()
    {
        Console.WriteLine("可用命令:");
        Console.WriteLine("  create <json文件>    - 从JSON创建实体和字段");
        Console.WriteLine("  check <解决方案名>    - 检查解决方案实体");
        Console.WriteLine("  add <实体> <解决方案> - 添加实体到解决方案");
        Console.WriteLine("  remove <实体> <解决方案> - 从解决方案移除实体");
        Console.WriteLine("  publish [实体]        - 发布指定实体（禁止无参数全局发布）");
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
        Console.WriteLine("  import-scoring-cards <JSON文件路径> - 清空现有评分卡并全量导入");
        Console.WriteLine("  update-integer-range <实体名> <字段名> <最小值> <最大值> - 更新整数字段取值范围");
        Console.WriteLine("  add-webresource-to-solution <WebResource名称> <解决方案唯一名> - 将 WebResource 加入解决方案");
        Console.WriteLine("  fix-form-lookup <实体名> - 修复主窗体中 Lookup 字段被错误配置为文本框的问题");
        Console.WriteLine("  list-role-privileges <角色关键字> [实体过滤] - 查角色权限明细（只读）");
        Console.WriteLine("  query-user-permissions <用户domainname> [实体过滤] - 查用户有效权限（只读）");
        Console.WriteLine("  set-role-privilege <角色> <实体> <权限类型> <深度> - 改角色权限（写，需授权）");
        Console.WriteLine("  assign-role/remove-role <用户domainname> <角色名> - 用户挂/摘角色（写，需授权）");
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

        var bytes = File.ReadAllBytes(filePath);
        var contentBase64 = Convert.ToBase64String(bytes);

        if (result.Entities.Count == 0)
        {
            // 创建新的 WebResource
            var create = new Entity("webresource");
            create["name"] = name;
            create["displayname"] = name;
            create["content"] = contentBase64;
            create["webresourcetype"] = new OptionSetValue(GetWebResourceTypeByExtension(filePath));
            var id = service.Create(create);
            Console.WriteLine($"✅ WebResource {name} 已创建 ({bytes.Length} bytes), ID: {id}");
            return;
        }

        var wr = result.Entities[0];
        var update = new Entity("webresource") { Id = wr.Id };
        update["content"] = contentBase64;
        service.Update(update);
        Console.WriteLine($"✅ WebResource {name} 已更新 ({bytes.Length} bytes)");
    }

    static int GetWebResourceTypeByExtension(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".js" => 3,      // Script/JScript
            ".html" or ".htm" => 1, // HTML
            ".css" => 2,     // CSS
            ".png" => 5,     // PNG
            ".jpg" or ".jpeg" => 6, // JPG
            ".gif" => 7,     // GIF
            ".svg" => 11,    // SVG
            ".xml" => 4,     // XML
            _ => 3           // 默认按脚本处理
        };
    }

    static void DeployWebResource(ServiceClient service, string name, string filePath, int type, string? displayName)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"❌ 文件不存在: {filePath}");
            return;
        }

        var webResourceService = new WebResourceService(service);
        var existing = webResourceService.QueryByName(name);
        var bytes = File.ReadAllBytes(filePath);
        var content = File.ReadAllText(filePath);

        if (existing == null)
        {
            var id = webResourceService.Create(name, displayName ?? name, type, content);
            Console.WriteLine($"✅ WebResource {name} 已创建 (ID: {id}, {bytes.Length} bytes)");
        }
        else
        {
            webResourceService.UpdateContent(name, bytes);
            Console.WriteLine($"✅ WebResource {name} 已更新 ({bytes.Length} bytes)");
        }

        Console.WriteLine($">>> 发布 WebResource: {name}...");
        webResourceService.PublishWebResources(name);
        Console.WriteLine($"✅ WebResource {name} 发布成功");
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
    /// <summary>
    /// Bug #1576 配套：融资资源产品多选（mcs_fsm_institution_products）旧值 11(Others) → 10 数据修复。
    /// 默认只读预检；传 apply 才真正更新。配合 D365_URL 切环境（UAT/生产导入删选项 11 前必须执行）。
    /// </summary>
    static void FixFsmResourceProduct11(ServiceClient service, bool apply)
    {
        Console.WriteLine($"=== mcs_fsm_resource 产品值 11→10 {(apply ? "修复执行" : "预检（只读）")} ===");

        var query = new Microsoft.Xrm.Sdk.Query.QueryExpression("mcs_fsm_resource")
        {
            ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet(
                "mcs_fsm_resource_no", "mcs_fsm_institution_name", "mcs_fsm_institution_products", "statecode")
        };
        var all = service.RetrieveMultiple(query);

        var targets = all.Entities.Where(e =>
        {
            var vals = e.GetAttributeValue<Microsoft.Xrm.Sdk.OptionSetValueCollection>("mcs_fsm_institution_products");
            return vals != null && vals.Any(v => v.Value == 11);
        }).ToList();

        if (targets.Count == 0)
        {
            Console.WriteLine("✅ 无含值 11 的记录，无需修复");
            return;
        }

        foreach (var e in targets)
        {
            var vals = e.GetAttributeValue<Microsoft.Xrm.Sdk.OptionSetValueCollection>("mcs_fsm_institution_products");
            var before = string.Join(",", vals.Select(v => v.Value).OrderBy(x => x));
            var afterVals = vals.Select(v => v.Value == 11 ? 10 : v.Value).Distinct().OrderBy(x => x).ToList();
            var no = e.GetAttributeValue<string>("mcs_fsm_resource_no") ?? "(空)";
            var name = e.GetAttributeValue<string>("mcs_fsm_institution_name") ?? "(空)";
            var state = e.GetAttributeValue<Microsoft.Xrm.Sdk.OptionSetValue>("statecode")?.Value == 0 ? "启用" : "停用";
            Console.WriteLine($"  {no}（{name}，{state}）: [{before}] → [{string.Join(",", afterVals)}]");

            if (apply)
            {
                var update = new Entity("mcs_fsm_resource", e.Id);
                var coll = new Microsoft.Xrm.Sdk.OptionSetValueCollection();
                foreach (var v in afterVals) coll.Add(new Microsoft.Xrm.Sdk.OptionSetValue(v));
                update["mcs_fsm_institution_products"] = coll;
                service.Update(update);
            }
        }

        Console.WriteLine(apply
            ? $"✅ 已修复 {targets.Count} 条（建议再跑一次不带 apply 预检确认无残留）"
            : $"共 {targets.Count} 条待修复，确认后执行: dotnet run fix-fsm-resource-product11 apply");
    }

    /// <summary>
    /// 禅道 #1781：mcs_fsm_data 存量记录标准币种字段 transactioncurrencyid 同步为融资币种 mcs_fsm_currency。
    /// 平台机制：Money 字段表单符号由 transactioncurrencyid 驱动，历史记录两者不一致导致符号与所选币种不符。
    /// 默认预检（只读），传 apply 执行修复。
    /// </summary>
    static void FixFsmTransactionCurrency(ServiceClient service, bool apply)
    {
        Console.WriteLine($"=== mcs_fsm_data 标准币种同步融资币种 {(apply ? "修复执行" : "预检（只读）")} ===");

        var query = new Microsoft.Xrm.Sdk.Query.QueryExpression("mcs_fsm_data")
        {
            ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("mcs_fsm_no", "mcs_fsm_currency", "transactioncurrencyid")
        };
        var all = service.RetrieveMultiple(query);

        var targets = all.Entities.Where(e =>
        {
            var fsmCur = e.GetAttributeValue<Microsoft.Xrm.Sdk.EntityReference>("mcs_fsm_currency");
            if (fsmCur == null) return false; // 融资币种为空不同步（与 JS 口径一致）
            var txnCur = e.GetAttributeValue<Microsoft.Xrm.Sdk.EntityReference>("transactioncurrencyid");
            return txnCur == null || txnCur.Id != fsmCur.Id;
        }).ToList();

        if (targets.Count == 0)
        {
            Console.WriteLine("✅ 无不一致记录，无需修复");
            return;
        }

        foreach (var e in targets)
        {
            var no = e.GetAttributeValue<string>("mcs_fsm_no") ?? "(空)";
            var fsmCur = e.GetAttributeValue<Microsoft.Xrm.Sdk.EntityReference>("mcs_fsm_currency");
            var txnCur = e.GetAttributeValue<Microsoft.Xrm.Sdk.EntityReference>("transactioncurrencyid");
            Console.WriteLine($"  {no}: 标准币种 {(txnCur?.Name ?? "(空)")} → 融资币种 {fsmCur.Name}");

            if (apply)
            {
                var update = new Entity("mcs_fsm_data", e.Id);
                update["transactioncurrencyid"] = new Microsoft.Xrm.Sdk.EntityReference("transactioncurrency", fsmCur.Id);
                service.Update(update);
            }
        }

        Console.WriteLine(apply
            ? $"✅ 已修复 {targets.Count} 条（建议再跑一次不带 apply 预检确认无残留）"
            : $"共 {targets.Count} 条待修复，确认后执行: dotnet run fix-fsm-transaction-currency apply");
    }

    /// <summary>
    /// 设置融资管理(mcs_fsm_data)审批状态 mcs_bppstatus，并同步清空审批状态码 mcs_bppstatuscode。
    /// 用于 DEV/UAT 复测时解锁表单锁定字段（JS 锁定条件：bppstatus=2 或 bppstatuscode=Submitted 等）。
    /// </summary>
    static void SetFsmBppStatus(ServiceClient service, string fsmNo, string statusArg)
    {
        Console.WriteLine($"=== 设置融资记录审批状态: {fsmNo} -> {statusArg} ===");

        var query = new Microsoft.Xrm.Sdk.Query.QueryExpression("mcs_fsm_data")
        {
            ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("mcs_fsm_no", "mcs_fsm_status", "mcs_bppstatus", "mcs_bppstatuscode"),
            Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression()
            {
                Conditions =
                {
                    new Microsoft.Xrm.Sdk.Query.ConditionExpression("mcs_fsm_no", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, fsmNo)
                }
            }
        };

        var records = service.RetrieveMultiple(query);
        if (records.Entities.Count == 0)
        {
            Console.WriteLine($"❌ 记录不存在: {fsmNo}");
            return;
        }

        var record = records.Entities[0];
        Console.WriteLine($"  记录ID: {record.Id}");
        Console.WriteLine($"  当前: mcs_fsm_status={record.GetAttributeValue<Microsoft.Xrm.Sdk.OptionSetValue>("mcs_fsm_status")?.Value.ToString() ?? "(空)"}, " +
                          $"mcs_bppstatus={record.GetAttributeValue<Microsoft.Xrm.Sdk.OptionSetValue>("mcs_bppstatus")?.Value.ToString() ?? "(空)"}, " +
                          $"mcs_bppstatuscode={record.GetAttributeValue<string>("mcs_bppstatuscode") ?? "(空)"}");

        var update = new Microsoft.Xrm.Sdk.Entity("mcs_fsm_data", record.Id);
        if (statusArg.Equals("clear", StringComparison.OrdinalIgnoreCase) || statusArg == "0")
        {
            update["mcs_bppstatus"] = null;
        }
        else if (int.TryParse(statusArg, out int st) && st >= 1 && st <= 4)
        {
            update["mcs_bppstatus"] = new Microsoft.Xrm.Sdk.OptionSetValue(st);
        }
        else
        {
            Console.WriteLine($"❌ 无效状态值: {statusArg}（允许 1|2|3|4|clear）");
            return;
        }
        // 状态码同步清空，避免 JS 按 Submitted 等码继续锁定
        update["mcs_bppstatuscode"] = null;

        service.Update(update);

        var after = service.Retrieve("mcs_fsm_data", record.Id,
            new Microsoft.Xrm.Sdk.Query.ColumnSet("mcs_bppstatus", "mcs_bppstatuscode"));
        Console.WriteLine($"✅ 已更新并回读: mcs_bppstatus={after.GetAttributeValue<Microsoft.Xrm.Sdk.OptionSetValue>("mcs_bppstatus")?.Value.ToString() ?? "(空)"}, " +
                          $"mcs_bppstatuscode={after.GetAttributeValue<string>("mcs_bppstatuscode") ?? "(空)"}");
    }

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
            Console.WriteLine($"\n⚠️ 记录当前已经是状态 {targetStatus}，强制触发更新以重新执行Plugin。");
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

    static void SimulateBppCallback(ServiceClient service, string scoreId, string bppStatus)
    {
        Console.WriteLine($"=== 模拟 BPP 回调: {scoreId} => {bppStatus} ===");

        var validStatuses = new[] { "approved", "rejected", "withdrawn", "abandoned" };
        if (!validStatuses.Contains(bppStatus.ToLowerInvariant()))
        {
            Console.WriteLine($"❌ 不支持的 BPP 状态: {bppStatus}。仅支持: Approved, Rejected, Withdrawn, Abandoned");
            return;
        }

        var query = new Microsoft.Xrm.Sdk.Query.QueryExpression("mcs_credit_record")
        {
            ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("mcs_credit_recordid", "mcs_scoreid", "mcs_status", "mcs_bppstatus", "mcs_workflowid"),
            Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression
            {
                Conditions = { new Microsoft.Xrm.Sdk.Query.ConditionExpression("mcs_scoreid", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, scoreId) }
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
        string currentBppStatus = record.GetAttributeValue<string>("mcs_bppstatus") ?? "(null)";
        string workflowId = record.GetAttributeValue<string>("mcs_workflowid") ?? "(null)";

        Console.WriteLine($"  记录ID: {recordId}");
        Console.WriteLine($"  当前业务状态: {currentStatus} ({GetStatusName(currentStatus)})");
        Console.WriteLine($"  当前 BPP 状态: {currentBppStatus}");
        Console.WriteLine($"  工作流ID: {workflowId}");

        try
        {
            var update = new Microsoft.Xrm.Sdk.Entity("mcs_credit_record", recordId);
            update["mcs_bppstatus"] = bppStatus;
            service.Update(update);
            Console.WriteLine($"✅ 已更新 mcs_bppstatus={bppStatus}，BppCallbackPlugin 应已触发");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 更新失败: {ex.Message}");
        }
    }

    static void SimulateFcaBppCallback(ServiceClient service, string grantIdOrId, string bppStatus)
    {
        Console.WriteLine($"=== 模拟厂端授信额度调整申请 BPP 回调: {grantIdOrId} => {bppStatus} ===");

        var validStatuses = new[] { "approved", "rejected", "withdrawn", "abandoned" };
        if (!validStatuses.Contains(bppStatus.ToLowerInvariant()))
        {
            Console.WriteLine($"❌ 不支持的 BPP 状态: {bppStatus}。仅支持: Approved, Rejected, Withdrawn, Abandoned");
            return;
        }

        Guid recordId;
        if (!Guid.TryParse(grantIdOrId, out recordId))
        {
            // 按申请单编号查询
            var query = new Microsoft.Xrm.Sdk.Query.QueryExpression("mcs_fca_quotaapp")
            {
                ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("mcs_fca_quotaappid", "mcs_grantid", "mcs_bppstatus", "mcs_bppstatuscode"),
                Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression
                {
                    Conditions = { new Microsoft.Xrm.Sdk.Query.ConditionExpression("mcs_grantid", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, grantIdOrId) }
                }
            };
            var records = service.RetrieveMultiple(query);
            if (records.Entities.Count == 0)
            {
                Console.WriteLine($"❌ 未找到申请单编号: {grantIdOrId}");
                return;
            }
            recordId = records.Entities[0].Id;
        }

        var record = service.Retrieve("mcs_fca_quotaapp", recordId,
            new Microsoft.Xrm.Sdk.Query.ColumnSet("mcs_fca_quotaappid", "mcs_grantid", "mcs_bppstatus", "mcs_bppstatuscode"));

        int currentStatus = record.GetAttributeValue<Microsoft.Xrm.Sdk.OptionSetValue>("mcs_bppstatus")?.Value ?? -1;
        string currentBppStatusCode = record.GetAttributeValue<string>("mcs_bppstatuscode") ?? "(null)";
        string grantId = record.GetAttributeValue<string>("mcs_grantid") ?? recordId.ToString();

        Console.WriteLine($"  记录ID: {recordId}");
        Console.WriteLine($"  申请单编号: {grantId}");
        Console.WriteLine($"  当前审批状态: {currentStatus}");
        Console.WriteLine($"  当前 BPP 状态码: {currentBppStatusCode}");

        try
        {
            var update = new Microsoft.Xrm.Sdk.Entity("mcs_fca_quotaapp", recordId);
            update["mcs_bppstatuscode"] = bppStatus;
            service.Update(update);
            Console.WriteLine($"✅ 已更新 mcs_bppstatuscode={bppStatus}，FcaQuotaAppBppCallbackPlugin 应已触发");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 更新失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 融资管理 BPP 回调 Plugin 全场景验证（DEV1 临时 Assembly 测试用）
    /// 场景：1 立项审批通过 2 方案审批通过 3 方案审批驳回 4 撤回清空
    /// 注意：准备阶段不更新 mcs_bppstatus=2，避免触发 FsmDataBppIntegrationPlugin
    /// </summary>
    static void TestFsmBpp(ServiceClient service)
    {
        Console.WriteLine("=== 融资管理 BPP 回调 Plugin 全场景验证 ===\n");

        // 1. 准备测试记录：复用最新一条，没有则创建
        var fsmQuery = new Microsoft.Xrm.Sdk.Query.QueryExpression("mcs_fsm_data")
        {
            ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("mcs_fsm_no"),
            TopCount = 1,
            Orders = { new Microsoft.Xrm.Sdk.Query.OrderExpression("createdon", Microsoft.Xrm.Sdk.Query.OrderType.Descending) }
        };
        var existing = service.RetrieveMultiple(fsmQuery);

        Guid recordId;
        if (existing.Entities.Count > 0)
        {
            recordId = existing.Entities[0].Id;
            Console.WriteLine($"复用现有记录: {existing.Entities[0].GetAttributeValue<string>("mcs_fsm_no")} ({recordId})");
        }
        else
        {
            Console.WriteLine("未找到 mcs_fsm_data 记录，创建测试记录...");
            var whoami = (Microsoft.Crm.Sdk.Messages.WhoAmIResponse)service.Execute(new Microsoft.Crm.Sdk.Messages.WhoAmIRequest());
            var currencyQuery = new Microsoft.Xrm.Sdk.Query.QueryExpression("transactioncurrency")
            {
                ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("isocurrencycode"),
                TopCount = 1,
                Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression
                {
                    Conditions = { new Microsoft.Xrm.Sdk.Query.ConditionExpression("isocurrencycode", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, "USD") }
                }
            };
            var currencies = service.RetrieveMultiple(currencyQuery);
            if (currencies.Entities.Count == 0) { Console.WriteLine("❌ 未找到 USD 币种"); return; }
            var cmdQuery = new Microsoft.Xrm.Sdk.Query.QueryExpression("mcs_customermasterdata") { ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet(false), TopCount = 1 };
            var cmds = service.RetrieveMultiple(cmdQuery);
            if (cmds.Entities.Count == 0) { Console.WriteLine("❌ 未找到客户主数据记录"); return; }

            var fsm = new Microsoft.Xrm.Sdk.Entity("mcs_fsm_data");
            fsm["mcs_fsm_no"] = $"FT{DateTime.Now:MMddHHmmss}"; // ≤20 字符
            fsm["mcs_fsm_status"] = new Microsoft.Xrm.Sdk.OptionSetValue(1);
            fsm["mcs_sub_company"] = "测试子公司";
            fsm["mcs_customer_id"] = "TEST-BPP-001";
            fsm["mcs_customer_name"] = cmds.Entities[0].ToEntityReference();
            fsm["mcs_fsm_amount"] = new Microsoft.Xrm.Sdk.Money(100000m);
            fsm["mcs_fsm_currency"] = currencies.Entities[0].ToEntityReference();
            fsm["mcs_fsm_period"] = 12;
            fsm["mcs_fsm_payment_ratio"] = 0.30m;
            fsm["mcs_fsm_interest_rate"] = 5.00m;
            fsm["mcs_fsm_product_desc"] = "BPP测试融资产品";
            fsm["mcs_fsm_device_count"] = 1;
            fsm["mcs_fsm_device_name"] = "BPP测试设备";
            fsm["mcs_fsm_manager"] = new Microsoft.Xrm.Sdk.EntityReference("systemuser", whoami.UserId);
            fsm["mcs_fsm_is_initiated"] = true;
            fsm["mcs_can_initiated"] = true;
            fsm["mcs_can_project"] = true;
            fsm["mcs_is_valid"] = false;
            recordId = service.Create(fsm);
            Console.WriteLine($"✅ 测试记录已创建: {fsm["mcs_fsm_no"]} ({recordId})");
        }

        int pass = 0, fail = 0;

        // ========== 场景1：立项审批通过（状态2→3） ==========
        Console.WriteLine("\n--- 场景1：立项审批通过（mcs_approve_type=1，fsm_status 2→3） ---");
        PrepareFsmRecord(service, recordId, fsmStatus: 2, canInitiated: true, canProject: false, approveType: 1, isValid: false);
        SimulateFsmCallback(service, recordId, "Approved");
        var r1 = service.Retrieve("mcs_fsm_data", recordId, new Microsoft.Xrm.Sdk.Query.ColumnSet("mcs_fsm_status", "mcs_can_initiated", "mcs_bppstatus", "mcs_approvedate"));
        bool s1 = GetFsmOpt(r1, "mcs_fsm_status") == 3 && GetFsmBool(r1, "mcs_can_initiated") == false && GetFsmOpt(r1, "mcs_bppstatus") == 3;
        ReportFsm("场景1", s1, $"fsm_status={GetFsmOpt(r1, "mcs_fsm_status")}(期望3), can_initiated={GetFsmBool(r1, "mcs_can_initiated")}(期望False), bppstatus={GetFsmOpt(r1, "mcs_bppstatus")}(期望3)");
        if (s1) pass++; else fail++;

        // ========== 场景2：融资方案审批通过（状态3→4，is_valid=true） ==========
        Console.WriteLine("\n--- 场景2：融资方案审批通过（mcs_approve_type=2，fsm_status 3→4） ---");
        PrepareFsmRecord(service, recordId, fsmStatus: 3, canInitiated: false, canProject: true, approveType: 2, isValid: false);
        SimulateFsmCallback(service, recordId, "Approved");
        var r2 = service.Retrieve("mcs_fsm_data", recordId, new Microsoft.Xrm.Sdk.Query.ColumnSet("mcs_fsm_status", "mcs_can_project", "mcs_is_valid", "mcs_bppstatus"));
        bool s2 = GetFsmOpt(r2, "mcs_fsm_status") == 4 && GetFsmBool(r2, "mcs_is_valid") == true && GetFsmBool(r2, "mcs_can_project") == false && GetFsmOpt(r2, "mcs_bppstatus") == 3;
        ReportFsm("场景2", s2, $"fsm_status={GetFsmOpt(r2, "mcs_fsm_status")}(期望4), is_valid={GetFsmBool(r2, "mcs_is_valid")}(期望True), can_project={GetFsmBool(r2, "mcs_can_project")}(期望False), bppstatus={GetFsmOpt(r2, "mcs_bppstatus")}(期望3)");
        if (s2) pass++; else fail++;

        // ========== 场景3：融资方案审批驳回（状态不变，can_project=1） ==========
        Console.WriteLine("\n--- 场景3：融资方案审批驳回（fsm_status 保持3，can_project=1） ---");
        PrepareFsmRecord(service, recordId, fsmStatus: 3, canInitiated: false, canProject: false, approveType: 2, isValid: false);
        SimulateFsmCallback(service, recordId, "Rejected");
        var r3 = service.Retrieve("mcs_fsm_data", recordId, new Microsoft.Xrm.Sdk.Query.ColumnSet("mcs_fsm_status", "mcs_can_project", "mcs_bppstatus"));
        bool s3 = GetFsmOpt(r3, "mcs_fsm_status") == 3 && GetFsmBool(r3, "mcs_can_project") == true && GetFsmOpt(r3, "mcs_bppstatus") == 4;
        ReportFsm("场景3", s3, $"fsm_status={GetFsmOpt(r3, "mcs_fsm_status")}(期望3), can_project={GetFsmBool(r3, "mcs_can_project")}(期望True), bppstatus={GetFsmOpt(r3, "mcs_bppstatus")}(期望4)");
        if (s3) pass++; else fail++;

        // ========== 场景4：撤回（bppstatus=1，清空 bppid/bppapprover） ==========
        Console.WriteLine("\n--- 场景4：审批撤回（bppstatus=1，清空 bppid/bppapprover） ---");
        var prep4 = new Microsoft.Xrm.Sdk.Entity("mcs_fsm_data", recordId);
        prep4["mcs_bppstatus"] = new Microsoft.Xrm.Sdk.OptionSetValue(1);
        prep4["mcs_bppid"] = "test-workflow-id-001";
        prep4["mcs_bppapprover"] = "tester";
        prep4["mcs_bppstatuscode"] = "Submitted";
        service.Update(prep4);
        SimulateFsmCallback(service, recordId, "Withdrawn");
        var r4 = service.Retrieve("mcs_fsm_data", recordId, new Microsoft.Xrm.Sdk.Query.ColumnSet("mcs_bppstatus", "mcs_bppid", "mcs_bppapprover"));
        bool s4 = GetFsmOpt(r4, "mcs_bppstatus") == 1 && string.IsNullOrEmpty(r4.GetAttributeValue<string>("mcs_bppid")) && string.IsNullOrEmpty(r4.GetAttributeValue<string>("mcs_bppapprover"));
        ReportFsm("场景4", s4, $"bppstatus={GetFsmOpt(r4, "mcs_bppstatus")}(期望1), bppid={r4.GetAttributeValue<string>("mcs_bppid") ?? "(空)"}(期望空), bppapprover={r4.GetAttributeValue<string>("mcs_bppapprover") ?? "(空)"}(期望空)");
        if (s4) pass++; else fail++;

        Console.WriteLine($"\n=== 验证完成: {pass} 通过, {fail} 失败 ===");
    }

    static void PrepareFsmRecord(ServiceClient service, Guid recordId, int fsmStatus, bool canInitiated, bool canProject, int approveType, bool isValid)
    {
        var prep = new Microsoft.Xrm.Sdk.Entity("mcs_fsm_data", recordId);
        prep["mcs_fsm_status"] = new Microsoft.Xrm.Sdk.OptionSetValue(fsmStatus);
        prep["mcs_can_initiated"] = canInitiated;
        prep["mcs_can_project"] = canProject;
        prep["mcs_approve_type"] = new Microsoft.Xrm.Sdk.OptionSetValue(approveType);
        prep["mcs_is_valid"] = isValid;
        prep["mcs_bppstatus"] = new Microsoft.Xrm.Sdk.OptionSetValue(1); // 保持申请状态，避免触发 Integration Plugin
        prep["mcs_bppstatuscode"] = "Submitted";
        service.Update(prep);
    }

    static void SimulateFsmCallback(ServiceClient service, Guid recordId, string bppStatus)
    {
        var update = new Microsoft.Xrm.Sdk.Entity("mcs_fsm_data", recordId);
        update["mcs_bppstatuscode"] = bppStatus;
        service.Update(update);
        Console.WriteLine($"  已模拟回调 mcs_bppstatuscode={bppStatus}");
    }

    static int GetFsmOpt(Microsoft.Xrm.Sdk.Entity e, string field) => e.GetAttributeValue<Microsoft.Xrm.Sdk.OptionSetValue>(field)?.Value ?? -1;
    static bool? GetFsmBool(Microsoft.Xrm.Sdk.Entity e, string field) => e.GetAttributeValue<bool?>(field);
    static void ReportFsm(string scene, bool success, string detail)
    {
        Console.WriteLine($"  {(success ? "✅" : "❌")} {scene}: {detail}");
    }

    static void TestTradeStPayTerm(ServiceClient service)
    {
        Console.WriteLine("=== 测试成交条件样板库 Plugin ===");

        var testRecords = new List<Guid>();

        try
        {
            // 1. 测试自动编号 + 创建时默认状态
            Console.WriteLine("\n--- 测试自动编号与默认状态 ---");
            var record1 = CreateTradeStPayTermTestRecord(service, "TEST", "测试事业部", "S", 0.3m, 30, 30);
            testRecords.Add(record1);

            var retrieved = service.Retrieve("mcs_trade_stpayterm", record1, new ColumnSet("mcs_trade_stpaytermname", "mcs_status"));
            var code = retrieved.GetAttributeValue<string>("mcs_trade_stpaytermname");
            var status = retrieved.GetAttributeValue<Microsoft.Xrm.Sdk.OptionSetValue>("mcs_status")?.Value;
            Console.WriteLine($"  记录ID: {record1}");
            Console.WriteLine($"  标准条件编码: {code}");
            Console.WriteLine($"  生效状态: {status}");

            if (!string.IsNullOrEmpty(code) && code.StartsWith("TC"))
                Console.WriteLine("  ✅ 自动编号正确");
            else
                Console.WriteLine("  ❌ 自动编号失败");

            // 2026-08-14 Bug #1834：取消审批功能，新增默认状态改为生效（2）
            if (status == 2)
                Console.WriteLine("  ✅ 默认状态正确（生效）");
            else
                Console.WriteLine("  ❌ 默认状态应为 2");

            // 2. 测试非法首付比例
            Console.WriteLine("\n--- 测试首付比例校验 ---");
            TryAction("首付比例 1.5 应被拦截", () =>
                CreateTradeStPayTermTestRecord(service, "TEST2", "测试事业部2", "S", 1.5m, 30, 30));

            // 3. 测试 100% 首付一致性
            Console.WriteLine("\n--- 测试 100% 首付一致性校验 ---");
            TryAction("100% 首付 + 账期 30 应被拦截", () =>
                CreateTradeStPayTermTestRecord(service, "TEST3", "测试事业部3", "S", 1.0m, 30, 30));

            // 4. 测试账期/频次 30 倍数校验
            Console.WriteLine("\n--- 测试账期倍数校验 ---");
            TryAction("账期 45 应被拦截", () =>
                CreateTradeStPayTermTestRecord(service, "TEST4", "测试事业部4", "S", 0.3m, 45, 30));

            // 2026-08-14 Bug #1834：取消审批功能，状态流转校验已停用，以下申请/审批/非法流转测试注释
            // // 5. 测试状态流转 0→1（申请）
            // Console.WriteLine("\n--- 测试申请（0→1）---");
            // UpdateTradeStPayTermStatus(service, record1, 1);
            // Console.WriteLine("  ✅ 申请成功");
            //
            // // 6. 测试状态流转 1→2（审批）
            // Console.WriteLine("\n--- 测试审批（1→2）---");
            // UpdateTradeStPayTermStatus(service, record1, 2);
            // Console.WriteLine("  ✅ 审批成功");
            //
            // // 7. 测试非法状态流转 2→0
            // Console.WriteLine("\n--- 测试非法状态流转（2→0）---");
            // TryAction("生效→未生效应被拦截", () =>
            //     UpdateTradeStPayTermStatus(service, record1, 0));

            // 8. 测试重复记录校验
            Console.WriteLine("\n--- 测试重复记录校验 ---");
            TryAction("相同维度记录应被拦截", () =>
                CreateTradeStPayTermTestRecord(service, "TEST", "测试事业部", "S", 0.3m, 30, 30));
        }
        finally
        {
            Console.WriteLine("\n--- 清理测试记录 ---");
            foreach (var id in testRecords)
            {
                try
                {
                    service.Delete("mcs_trade_stpayterm", id);
                    Console.WriteLine($"  ✅ 已删除: {id}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ⚠️ 删除失败 {id}: {ex.Message}");
                }
            }
        }

        Console.WriteLine("\n=== 测试完成 ===");
    }

    static void DisablePluginStep(ServiceClient service, string stepId)
    {
        Console.WriteLine($">>> 停用 Plugin Step: {stepId}");
        try
        {
            if (!Guid.TryParse(stepId, out var guid))
            {
                Console.WriteLine("  ✗ StepId 格式不正确");
                return;
            }

            var entity = new Microsoft.Xrm.Sdk.Entity("sdkmessageprocessingstep", guid);
            entity["statecode"] = new Microsoft.Xrm.Sdk.OptionSetValue(1);   // Inactive
            entity["statuscode"] = new Microsoft.Xrm.Sdk.OptionSetValue(2);  // Inactive
            service.Update(entity);
            Console.WriteLine("  ✅ Plugin Step 已停用");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ✗ 停用失败: {ex.Message}");
        }
    }

    static void DeletePluginStep(ServiceClient service, string stepId)
    {
        Console.WriteLine($">>> 删除 Plugin Step: {stepId}");
        try
        {
            if (!Guid.TryParse(stepId, out var guid))
            {
                Console.WriteLine("  ✗ StepId 格式不正确");
                return;
            }
            service.Delete("sdkmessageprocessingstep", guid);
            Console.WriteLine("  ✅ Plugin Step 已删除");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ✗ 删除失败: {ex.Message}");
        }
    }

    static void DeletePluginType(ServiceClient service, string typeId)
    {
        Console.WriteLine($">>> 删除 Plugin Type: {typeId}");
        try
        {
            if (!Guid.TryParse(typeId, out var guid))
            {
                Console.WriteLine("  ✗ TypeId 格式不正确");
                return;
            }
            service.Delete("plugintype", guid);
            Console.WriteLine("  ✅ Plugin Type 已删除");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ✗ 删除失败: {ex.Message}");
        }
    }

    static void RecreateUatSyncSteps(ServiceClient service, string dllPath)
    {
        Console.WriteLine(">>> Bug#1834 修复：按 UAT 原 GUID 重建 TradePtGroupTypeProductLineSyncPlugin Type/Steps");

        // UAT 上的原始 GUID
        var uatTypeId = Guid.Parse("40f9c14e-6770-f111-ab0f-7ced8de4e391");
        var uatCreateStepId = Guid.Parse("4ff9c14e-6970-f111-ab0f-7ced8de4edac");
        var uatUpdateStepId = Guid.Parse("b9fb3ecf-6970-f111-ab0f-7ced8de4edac");

        // 1. 更新 Assembly 内容（DLL 中类名已改为与 UAT 一致）
        if (!File.Exists(dllPath))
        {
            Console.WriteLine($"  ✗ DLL 不存在: {dllPath}");
            return;
        }
        byte[] dllBytes = File.ReadAllBytes(dllPath);
        string dllContent = Convert.ToBase64String(dllBytes);

        var asmQuery = new Microsoft.Xrm.Sdk.Query.QueryExpression("pluginassembly")
        {
            ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("pluginassemblyid"),
            Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression
            {
                Conditions = { new Microsoft.Xrm.Sdk.Query.ConditionExpression("name", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, "SanyD365.D365Extension.Sales") }
            }
        };
        var asmResult = service.RetrieveMultiple(asmQuery);
        if (asmResult.Entities.Count == 0)
        {
            Console.WriteLine("  ✗ 未找到 Assembly SanyD365.D365Extension.Sales");
            return;
        }
        var assemblyId = asmResult.Entities[0].Id;
        var asmUpdate = new Microsoft.Xrm.Sdk.Entity("pluginassembly", assemblyId);
        asmUpdate["content"] = dllContent;
        service.Update(asmUpdate);
        Console.WriteLine($"  ✅ Assembly 已更新 (ID: {assemblyId})");

        // 2. 按 UAT ID 创建 Plugin Type
        try
        {
            var typeEntity = new Microsoft.Xrm.Sdk.Entity("plugintype", uatTypeId);
            typeEntity["pluginassemblyid"] = new Microsoft.Xrm.Sdk.EntityReference("pluginassembly", assemblyId);
            typeEntity["typename"] = "SanyD365.D365Extension.Sales.Plugins.TradeStPayTerm.TradePtGroupTypeProductLineSyncPlugin";
            typeEntity["friendlyname"] = "TradePtGroupTypeProductLineSyncPlugin";
            typeEntity["name"] = "TradePtGroupTypeProductLineSyncPlugin";
            service.Create(typeEntity);
            Console.WriteLine($"  ✅ Plugin Type 已按 UAT ID 创建 (ID: {uatTypeId})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ✗ Plugin Type 创建失败: {ex.Message}");
            return;
        }

        // 3. 查询 Create/Update 消息的 sdkmessageid 和 filter
        var createMsgId = GetSdkMessageId(service, "Create");
        var updateMsgId = GetSdkMessageId(service, "Update");
        var createFilterId = GetSdkMessageFilterId(service, createMsgId, "mcs_trade_ptgrouptype");
        var updateFilterId = GetSdkMessageFilterId(service, updateMsgId, "mcs_trade_ptgrouptype");
        if (createMsgId == Guid.Empty || updateMsgId == Guid.Empty || createFilterId == Guid.Empty || updateFilterId == Guid.Empty)
        {
            Console.WriteLine("  ✗ 未找到 Create/Update 消息或过滤器");
            return;
        }

        // 4. 按 UAT ID 创建 Create Step
        try
        {
            var createStep = new Microsoft.Xrm.Sdk.Entity("sdkmessageprocessingstep", uatCreateStepId);
            createStep["plugintypeid"] = new Microsoft.Xrm.Sdk.EntityReference("plugintype", uatTypeId);
            createStep["sdkmessageid"] = new Microsoft.Xrm.Sdk.EntityReference("sdkmessage", createMsgId);
            createStep["sdkmessagefilterid"] = new Microsoft.Xrm.Sdk.EntityReference("sdkmessagefilter", createFilterId);
            createStep["name"] = "TradePtGroupTypeProductLineSyncPlugin: Create of mcs_trade_ptgrouptype";
            createStep["stage"] = new Microsoft.Xrm.Sdk.OptionSetValue(20);  // PreOperation
            createStep["mode"] = new Microsoft.Xrm.Sdk.OptionSetValue(0);    // Sync
            createStep["rank"] = 1;
            createStep["supporteddeployment"] = new Microsoft.Xrm.Sdk.OptionSetValue(0);
            createStep["invocationsource"] = new Microsoft.Xrm.Sdk.OptionSetValue(1);
            service.Create(createStep);
            Console.WriteLine($"  ✅ Create Step 已按 UAT ID 创建 (ID: {uatCreateStepId})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ✗ Create Step 创建失败: {ex.Message}");
        }

        // 5. 按 UAT ID 创建 Update Step
        try
        {
            var updateStep = new Microsoft.Xrm.Sdk.Entity("sdkmessageprocessingstep", uatUpdateStepId);
            updateStep["plugintypeid"] = new Microsoft.Xrm.Sdk.EntityReference("plugintype", uatTypeId);
            updateStep["sdkmessageid"] = new Microsoft.Xrm.Sdk.EntityReference("sdkmessage", updateMsgId);
            updateStep["sdkmessagefilterid"] = new Microsoft.Xrm.Sdk.EntityReference("sdkmessagefilter", updateFilterId);
            updateStep["name"] = "TradePtGroupTypeProductLineSyncPlugin: Update of mcs_trade_ptgrouptype";
            updateStep["stage"] = new Microsoft.Xrm.Sdk.OptionSetValue(20);  // PreOperation
            updateStep["mode"] = new Microsoft.Xrm.Sdk.OptionSetValue(0);    // Sync
            updateStep["rank"] = 1;
            updateStep["supporteddeployment"] = new Microsoft.Xrm.Sdk.OptionSetValue(0);
            updateStep["invocationsource"] = new Microsoft.Xrm.Sdk.OptionSetValue(1);
            service.Create(updateStep);
            Console.WriteLine($"  ✅ Update Step 已按 UAT ID 创建 (ID: {uatUpdateStepId})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ✗ Update Step 创建失败: {ex.Message}");
        }

        Console.WriteLine("  ✅ 重建完成");
    }

    static Guid GetSdkMessageId(ServiceClient service, string messageName)
    {
        var query = new Microsoft.Xrm.Sdk.Query.QueryExpression("sdkmessage")
        {
            ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("sdkmessageid"),
            Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression
            {
                Conditions = { new Microsoft.Xrm.Sdk.Query.ConditionExpression("name", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, messageName) }
            }
        };
        var result = service.RetrieveMultiple(query);
        return result.Entities.Count > 0 ? result.Entities[0].Id : Guid.Empty;
    }

    static void CreatePluginStepWithId(ServiceClient service, string stepIdStr, string typeIdStr, string messageName, string entityName, int stage)
    {
        Console.WriteLine($">>> 按指定 GUID 创建 Plugin Step: {stepIdStr}");
        try
        {
            var stepId = Guid.Parse(stepIdStr);
            var typeId = Guid.Parse(typeIdStr);
            // 2026-08-20 #1641 防线：Custom API 实现类禁止再挂实体 Step
            var guardManager = new EntityManager(service);
            if (guardManager.IsPluginTypeBoundToCustomApi(typeId, out var guardApis))
            {
                Console.WriteLine($"  ✗ 禁止注册实体 Step：该类已被 Custom API 绑定（{string.Join(", ", guardApis)}）");
                return;
            }
            var messageId = GetSdkMessageId(service, messageName);
            var filterId = GetSdkMessageFilterId(service, messageId, entityName);
            if (messageId == Guid.Empty || filterId == Guid.Empty)
            {
                Console.WriteLine("  ✗ 未找到消息或过滤器");
                return;
            }

            var step = new Microsoft.Xrm.Sdk.Entity("sdkmessageprocessingstep", stepId);
            step["plugintypeid"] = new Microsoft.Xrm.Sdk.EntityReference("plugintype", typeId);
            step["sdkmessageid"] = new Microsoft.Xrm.Sdk.EntityReference("sdkmessage", messageId);
            step["sdkmessagefilterid"] = new Microsoft.Xrm.Sdk.EntityReference("sdkmessagefilter", filterId);
            step["name"] = $"TradePtGroupTypeProductLineSyncPlugin: {messageName} of {entityName}";
            step["stage"] = new Microsoft.Xrm.Sdk.OptionSetValue(stage);
            step["mode"] = new Microsoft.Xrm.Sdk.OptionSetValue(0);
            step["rank"] = 1;
            step["supporteddeployment"] = new Microsoft.Xrm.Sdk.OptionSetValue(0);
            step["invocationsource"] = new Microsoft.Xrm.Sdk.OptionSetValue(1);
            service.Create(step);
            Console.WriteLine($"  ✅ Plugin Step 已创建 (ID: {stepId})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ✗ 创建失败: {ex.Message}");
        }
    }

    static Guid GetSdkMessageFilterId(ServiceClient service, Guid messageId, string entityName)
    {
        var query = new Microsoft.Xrm.Sdk.Query.QueryExpression("sdkmessagefilter")
        {
            ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("sdkmessagefilterid"),
            Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression
            {
                Conditions =
                {
                    new Microsoft.Xrm.Sdk.Query.ConditionExpression("sdkmessageid", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, messageId),
                    new Microsoft.Xrm.Sdk.Query.ConditionExpression("primaryobjecttypecode", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, entityName)
                }
            }
        };
        var result = service.RetrieveMultiple(query);
        return result.Entities.Count > 0 ? result.Entities[0].Id : Guid.Empty;
    }

    static Guid CreateTradeStPayTermTestRecord(ServiceClient service, string buId, string buName, string buyerGrade, decimal downPay, int payTerm, int payFreq)
    {
        var entity = new Microsoft.Xrm.Sdk.Entity("mcs_trade_stpayterm");
        entity["mcs_buid"] = buId;
        entity["mcs_buname"] = buName;
        entity["mcs_buyergrade"] = new Microsoft.Xrm.Sdk.OptionSetValueCollection(
            buyerGrade.Split('/')
                      .Select(g => new Microsoft.Xrm.Sdk.OptionSetValue(GetBuyerGradeOptionValue(g.Trim())))
                      .ToList());
        entity["mcs_creditgrade"] = new Microsoft.Xrm.Sdk.OptionSetValueCollection(
            new[] { new Microsoft.Xrm.Sdk.OptionSetValue(100000000) }); // A0
        entity["mcs_downpay"] = downPay;
        entity["mcs_payterm"] = payTerm;
        entity["mcs_payfreq"] = payFreq;
        return service.Create(entity);
    }

    static int GetBuyerGradeOptionValue(string grade)
    {
        return grade.ToUpperInvariant() switch
        {
            "S" => 100000000,
            "A" => 100000001,
            "B" => 100000002,
            "C" => 100000003,
            "I" => 100000004,
            "D1" => 100000005,
            "D2" => 100000006,
            "D3" => 100000007,
            "D4" => 100000008,
            "D5" => 100000009,
            _ => throw new ArgumentException($"未知客户分类: {grade}")
        };
    }

    static void UpdateTradeStPayTermStatus(ServiceClient service, Guid id, int status)
    {
        var entity = new Microsoft.Xrm.Sdk.Entity("mcs_trade_stpayterm", id);
        entity["mcs_status"] = new Microsoft.Xrm.Sdk.OptionSetValue(status);
        service.Update(entity);
    }

    static void TryAction(string description, Action action)
    {
        try
        {
            action();
            Console.WriteLine($"  ❌ {description}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ✅ {description}: {ex.Message}");
        }
    }

    static void CreateTestSalesOrder(ServiceClient service, string accountName)
    {
        Console.WriteLine($"=== 为客户 {accountName} 创建测试销售订单 ===");

        // 1. 查询客户
        var accountQuery = new Microsoft.Xrm.Sdk.Query.QueryExpression("account")
        {
            ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("accountid", "name"),
            Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression
            {
                Conditions = { new Microsoft.Xrm.Sdk.Query.ConditionExpression("name", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, accountName) }
            }
        };
        var accounts = service.RetrieveMultiple(accountQuery);
        if (accounts.Entities.Count == 0)
        {
            Console.WriteLine($"❌ 未找到客户: {accountName}");
            return;
        }
        var account = accounts.Entities[0];
        var accountId = account.Id;
        Console.WriteLine($"找到客户: {account.GetAttributeValue<string>("name")} ({accountId})");

        // 2. 查询默认价格列表（含货币）
        Guid? priceLevelId = null;
        Guid? priceLevelCurrencyId = null;
        try
        {
            var priceLevelQuery = new Microsoft.Xrm.Sdk.Query.QueryExpression("pricelevel")
            {
                ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("pricelevelid", "name", "transactioncurrencyid"),
                TopCount = 1
            };
            var priceLevels = service.RetrieveMultiple(priceLevelQuery);
            if (priceLevels.Entities.Count > 0)
            {
                var priceLevel = priceLevels.Entities[0];
                priceLevelId = priceLevel.Id;
                var priceCurrencyRef = priceLevel.GetAttributeValue<Microsoft.Xrm.Sdk.EntityReference>("transactioncurrencyid");
                priceLevelCurrencyId = priceCurrencyRef?.Id;
                Console.WriteLine($"使用默认价格列表: {priceLevel.GetAttributeValue<string>("name")} ({priceLevelId}), 货币ID={priceLevelCurrencyId}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ 查询默认价格列表失败: {ex.Message}");
        }

        // 3. 查询与价格列表匹配的货币
        Guid? currencyId = null;
        try
        {
            var currencyQuery = new Microsoft.Xrm.Sdk.Query.QueryExpression("transactioncurrency")
            {
                ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("transactioncurrencyid", "currencyname")
            };
            if (priceLevelCurrencyId.HasValue)
            {
                currencyQuery.Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression
                {
                    Conditions = { new Microsoft.Xrm.Sdk.Query.ConditionExpression("transactioncurrencyid", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, priceLevelCurrencyId.Value) }
                };
            }
            var currencies = service.RetrieveMultiple(currencyQuery);
            if (currencies.Entities.Count > 0)
            {
                currencyId = currencies.Entities[0].Id;
                Console.WriteLine($"使用默认货币: {currencies.Entities[0].GetAttributeValue<string>("currencyname")} ({currencyId})");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ 查询默认货币失败: {ex.Message}");
        }

        // 4. 创建销售订单
        var salesOrder = new Microsoft.Xrm.Sdk.Entity("salesorder");
        salesOrder["customerid"] = new Microsoft.Xrm.Sdk.EntityReference("account", accountId);
        salesOrder["name"] = $"测试订单-{accountName}-{DateTime.Now:yyyyMMddHHmmss}";
        salesOrder["description"] = "用于信用评估老客户判定的测试销售订单";
        if (currencyId.HasValue)
            salesOrder["transactioncurrencyid"] = new Microsoft.Xrm.Sdk.EntityReference("transactioncurrency", currencyId.Value);
        if (priceLevelId.HasValue)
            salesOrder["pricelevelid"] = new Microsoft.Xrm.Sdk.EntityReference("pricelevel", priceLevelId.Value);

        try
        {
            var orderId = service.Create(salesOrder);
            Console.WriteLine($"✅ 销售订单创建成功: {orderId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 销售订单创建失败: {ex.Message}");
            if (ex.InnerException != null)
                Console.WriteLine($"   内部异常: {ex.InnerException.Message}");
        }
    }

    static void CreateCreditTestData(ServiceClient service, string accountName)
    {
        Console.WriteLine($"=== 为客户 {accountName} 创建信用评估测试数据 ===");

        // 1. 查询客户
        var accountQuery = new Microsoft.Xrm.Sdk.Query.QueryExpression("account")
        {
            ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("accountid", "name"),
            Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression
            {
                Conditions = { new Microsoft.Xrm.Sdk.Query.ConditionExpression("name", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, accountName) }
            }
        };
        var accounts = service.RetrieveMultiple(accountQuery);
        if (accounts.Entities.Count == 0)
        {
            Console.WriteLine($"❌ 未找到客户: {accountName}");
            return;
        }
        var accountId = accounts.Entities[0].Id;
        Console.WriteLine($"找到客户: {accounts.Entities[0].GetAttributeValue<string>("name")} ({accountId})");

        // 2. 查询 USD 货币
        Guid? usdCurrencyId = null;
        var currencyQuery = new Microsoft.Xrm.Sdk.Query.QueryExpression("transactioncurrency")
        {
            ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("transactioncurrencyid", "currencyname", "isocurrencycode"),
            Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression
            {
                Conditions = { new Microsoft.Xrm.Sdk.Query.ConditionExpression("isocurrencycode", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, "USD") }
            }
        };
        var currencies = service.RetrieveMultiple(currencyQuery);
        if (currencies.Entities.Count > 0)
        {
            usdCurrencyId = currencies.Entities[0].Id;
            Console.WriteLine($"使用货币: USD ({usdCurrencyId})");
        }
        else
        {
            Console.WriteLine("⚠️ 未找到 USD 货币，尝试使用任意货币");
            var anyCurrencyQuery = new Microsoft.Xrm.Sdk.Query.QueryExpression("transactioncurrency")
            {
                ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("transactioncurrencyid"),
                TopCount = 1
            };
            var anyCurrencies = service.RetrieveMultiple(anyCurrencyQuery);
            if (anyCurrencies.Entities.Count > 0)
                usdCurrencyId = anyCurrencies.Entities[0].Id;
        }

        // 3. 创建销售订单（带明细，使 totalamount_base 有值）
        try
        {
            var salesOrder = new Microsoft.Xrm.Sdk.Entity("salesorder");
            salesOrder["customerid"] = new Microsoft.Xrm.Sdk.EntityReference("account", accountId);
            salesOrder["name"] = $"信用评估测试订单-{accountName}-{DateTime.Now:yyyyMMddHHmmss}";
            salesOrder["description"] = "用于验证 SalesAmount 的测试销售订单";
            salesOrder["transactioncurrencyid"] = new Microsoft.Xrm.Sdk.EntityReference("transactioncurrency", usdCurrencyId.Value);
            var orderId = service.Create(salesOrder);
            Console.WriteLine($"✅ 销售订单创建成功: {orderId}");

            // 尝试直接更新 totalamount（计算字段在创建后有时可更新）
            try
            {
                var updateOrder = new Microsoft.Xrm.Sdk.Entity("salesorder", orderId);
                updateOrder["totalamount"] = new Microsoft.Xrm.Sdk.Money(50000m);
                updateOrder["totalamount_base"] = new Microsoft.Xrm.Sdk.Money(50000m);
                service.Update(updateOrder);
                Console.WriteLine($"   销售订单总金额已更新为 USD 50,000");
            }
            catch (Exception updateEx)
            {
                Console.WriteLine($"   更新销售订单总金额失败: {updateEx.Message}");
            }

            // 查询销售订单总金额
            var orderResult = service.Retrieve("salesorder", orderId, new Microsoft.Xrm.Sdk.Query.ColumnSet("totalamount", "totalamount_base", "statecode"));
            var totalAmount = orderResult.GetAttributeValue<Microsoft.Xrm.Sdk.Money>("totalamount")?.Value ?? 0m;
            var totalAmountBase = orderResult.GetAttributeValue<Microsoft.Xrm.Sdk.Money>("totalamount_base")?.Value ?? 0m;
            var stateCode = orderResult.GetAttributeValue<Microsoft.Xrm.Sdk.OptionSetValue>("statecode")?.Value ?? -1;
            Console.WriteLine($"   销售订单总金额: totalamount={totalAmount:F2}, totalamount_base={totalAmountBase:F2}, statecode={stateCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 销售订单创建失败: {ex.Message}");
            if (ex.InnerException != null)
                Console.WriteLine($"   内部异常: {ex.InnerException.Message}");
        }

        // 4. 创建逾期的 mcs_outstanding 记录
        try
        {
            // 查询一个现有还款计划作为必填字段
            Guid? repaymentPlanId = null;
            var planQuery = new Microsoft.Xrm.Sdk.Query.QueryExpression("mcs_repaymentplan")
            {
                ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("mcs_repaymentplanid"),
                TopCount = 1
            };
            var plans = service.RetrieveMultiple(planQuery);
            if (plans.Entities.Count > 0)
            {
                repaymentPlanId = plans.Entities[0].Id;
                Console.WriteLine($"使用现有还款计划: {repaymentPlanId}");
            }
            else
            {
                Console.WriteLine("⚠️ 未找到还款计划，尝试创建临时还款计划");
                var tempPlan = new Microsoft.Xrm.Sdk.Entity("mcs_repaymentplan");
                tempPlan["mcs_name"] = $"临时还款计划-{DateTime.Now:yyyyMMddHHmmss}";
                repaymentPlanId = service.Create(tempPlan);
                Console.WriteLine($"✅ 临时还款计划创建成功: {repaymentPlanId}");
            }

            var outstanding = new Microsoft.Xrm.Sdk.Entity("mcs_outstanding");
            outstanding["mcs_name"] = $"测试逾期-{accountName}-{DateTime.Now:yyyyMMddHHmmss}";
            outstanding["mcs_account"] = new Microsoft.Xrm.Sdk.EntityReference("account", accountId);
            outstanding["mcs_repaymentplan"] = new Microsoft.Xrm.Sdk.EntityReference("mcs_repaymentplan", repaymentPlanId.Value);
            outstanding["mcs_overdue"] = true;
            outstanding["mcs_overdueamount"] = 350000m; // 人民币
            outstanding["mcs_overdurationdays"] = 120;
            outstanding["mcs_createon"] = DateTime.Now.AddDays(-10);
            outstanding["mcs_isocurrencycode"] = "CNY";
            var outstandingId = service.Create(outstanding);
            Console.WriteLine($"✅ 逾期在外货款创建成功: {outstandingId} (逾期金额: CNY 350,000, 逾期天数: 120)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 逾期在外货款创建失败: {ex.Message}");
            if (ex.InnerException != null)
                Console.WriteLine($"   内部异常: {ex.InnerException.Message}");
        }
    }

    static void CreateSinosureTestData(ServiceClient service, string accountName)
    {
        Console.WriteLine($"=== 为客户 {accountName} 创建中信保限额测试数据 ===");

        // 1. 查询客户主数据
        var accountQuery = new Microsoft.Xrm.Sdk.Query.QueryExpression("account")
        {
            ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("accountid", "name", "mcs_customermasterdata"),
            Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression
            {
                Conditions = { new Microsoft.Xrm.Sdk.Query.ConditionExpression("name", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, accountName) }
            }
        };
        var accounts = service.RetrieveMultiple(accountQuery);
        if (accounts.Entities.Count == 0)
        {
            Console.WriteLine($"❌ 未找到客户: {accountName}");
            return;
        }
        var account = accounts.Entities[0];
        var accountId = account.Id;
        var masterDataRef = account.GetAttributeValue<Microsoft.Xrm.Sdk.EntityReference>("mcs_customermasterdata");
        if (masterDataRef == null)
        {
            Console.WriteLine($"❌ 客户 {accountName} 未关联客户主数据");
            return;
        }
        var masterDataId = masterDataRef.Id;
        Console.WriteLine($"找到客户: {account.GetAttributeValue<string>("name")} ({accountId})");
        Console.WriteLine($"关联客户主数据: {masterDataId}");

        // 2. 设置中信保买方代码
        var sinosureCode = $"TESTBUYER{DateTime.Now:yyyyMMddHHmmss}";
        try
        {
            var updateMaster = new Microsoft.Xrm.Sdk.Entity("mcs_customermasterdata", masterDataId);
            updateMaster["mcs_sinosurecode"] = sinosureCode;
            service.Update(updateMaster);
            Console.WriteLine($"✅ 客户主数据中信保买方代码已设置: {sinosureCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 设置中信保买方代码失败: {ex.Message}");
            return;
        }

        // 3. 创建中信保限额测试记录
        var testRecords = new (int payModeApply, decimal quotaSum, string quotaBalance, string genre)[]
        {
            (2, 50000m, "45000.00", "非信用证"),  // DP
            (4, 80000m, "70000.00", "信用证"),    // OA
            (1, 100000m, "90000.00", "信用证")    // LC
        };

        foreach (var record in testRecords)
        {
            try
            {
                var quota = new Microsoft.Xrm.Sdk.Entity("mcs_approvedquota");
                quota["mcs_buyerno"] = sinosureCode;
                quota["mcs_quotastate"] = new Microsoft.Xrm.Sdk.OptionSetValue(1); // 有效
                quota["mcs_paymodeapply"] = new Microsoft.Xrm.Sdk.OptionSetValue(record.payModeApply);
                quota["mcs_quotasum"] = record.quotaSum;
                quota["mcs_quotabalance"] = record.quotaBalance;
                quota["mcs_corpserialno"] = $"SIN{DateTime.Now:yyyyMMddHHmmss}{record.payModeApply}";
                quota["mcs_quotaapplyid"] = $"QA{DateTime.Now:yyyyMMddHHmmss}{record.payModeApply}";

                var quotaId = service.Create(quota);
                Console.WriteLine($"✅ 中信保限额记录创建成功: {quotaId} (类型:{record.genre}, 额度:{record.quotaSum}, 余额:{record.quotaBalance})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 创建中信保限额记录失败 (payModeApply={record.payModeApply}): {ex.Message}");
                if (ex.InnerException != null)
                    Console.WriteLine($"   内部异常: {ex.InnerException.Message}");
            }
        }

        Console.WriteLine("\n预期带出结果:");
        Console.WriteLine("  信保额度类型: 非信用证,信用证");
        Console.WriteLine("  信保额度 USD: 230,000.00");
        Console.WriteLine("  信保余额 USD: 205,000.00");
    }

    static void CreateCreditRecord(ServiceClient service, string accountName)
    {
        Console.WriteLine($"=== 为客户 {accountName} 创建信用评估记录 ===");

        // 1. 查询客户
        var accountQuery = new Microsoft.Xrm.Sdk.Query.QueryExpression("account")
        {
            ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("accountid", "name", "mcs_customermasterdata", "ownerid"),
            Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression
            {
                Conditions = { new Microsoft.Xrm.Sdk.Query.ConditionExpression("name", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, accountName) }
            }
        };
        var accounts = service.RetrieveMultiple(accountQuery);
        if (accounts.Entities.Count == 0)
        {
            Console.WriteLine($"❌ 未找到客户: {accountName}");
            return;
        }
        var account = accounts.Entities[0];
        var accountId = account.Id;
        var accountNameValue = account.GetAttributeValue<string>("name") ?? accountName;
        string englishName = accountNameValue;
        string? countryCode = null;
        var ownerRef = account.GetAttributeValue<Microsoft.Xrm.Sdk.EntityReference>("ownerid");

        // 从客户主数据获取英文名和国家代码
        if (account.Contains("mcs_customermasterdata") && account["mcs_customermasterdata"] is Microsoft.Xrm.Sdk.EntityReference cmRef)
        {
            try
            {
                var cmData = service.Retrieve("mcs_customermasterdata", cmRef.Id, new Microsoft.Xrm.Sdk.Query.ColumnSet("mcs_countrycode", "mcs_englishname"));
                countryCode = cmData.GetAttributeValue<string>("mcs_countrycode");
                englishName = cmData.GetAttributeValue<string>("mcs_englishname") ?? accountNameValue;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 读取客户主数据失败: {ex.Message}");
            }
        }

        // 2. 创建信用评估记录
        try
        {
            var creditRecord = new Microsoft.Xrm.Sdk.Entity("mcs_credit_record");
            creditRecord["mcs_accountid"] = new Microsoft.Xrm.Sdk.EntityReference("account", accountId);
            creditRecord["mcs_custname"] = accountNameValue;
            creditRecord["mcs_custnameen"] = englishName;
            if (!string.IsNullOrEmpty(countryCode))
            {
                creditRecord["mcs_countrycode"] = countryCode;
            }
            if (ownerRef != null)
            {
                creditRecord["ownerid"] = ownerRef;
            }

            var recordId = service.Create(creditRecord);
            Console.WriteLine($"✅ 信用评估记录创建成功: {recordId}");

            // 查询自动生成的 scoreid
            var createdRecord = service.Retrieve("mcs_credit_record", recordId, new Microsoft.Xrm.Sdk.Query.ColumnSet("mcs_scoreid", "mcs_status"));
            var scoreId = createdRecord.GetAttributeValue<string>("mcs_scoreid");
            var status = createdRecord.GetAttributeValue<Microsoft.Xrm.Sdk.OptionSetValue>("mcs_status")?.Value ?? -1;
            Console.WriteLine($"   评估编码: {scoreId}");
            Console.WriteLine($"   当前状态: {status}");
            Console.WriteLine($"   下一步: dotnet run update-credit-record {scoreId} 11");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 信用评估记录创建失败: {ex.Message}");
            if (ex.InnerException != null)
                Console.WriteLine($"   内部异常: {ex.InnerException.Message}");
        }
    }

    static void CreateDDTestData(ServiceClient service, string accountName)
    {
        Console.WriteLine($"=== 为客户 {accountName} 创建重点尽调测试数据 ===");

        try
        {
            // 1. 查询 account 及客户主数据
            var accountQuery = new Microsoft.Xrm.Sdk.Query.QueryExpression("account")
            {
                ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("accountid", "name", "accountnumber", "mcs_customermasterdata"),
                Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression
                {
                    Conditions = { new Microsoft.Xrm.Sdk.Query.ConditionExpression("name", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, accountName) }
                }
            };
            var accounts = service.RetrieveMultiple(accountQuery);
            if (accounts.Entities.Count == 0)
            {
                Console.WriteLine($"❌ 未找到客户: {accountName}");
                return;
            }
            var account = accounts.Entities[0];
            var accountId = account.Id;
            var masterDataRef = account.GetAttributeValue<Microsoft.Xrm.Sdk.EntityReference>("mcs_customermasterdata");
            if (masterDataRef == null)
            {
                Console.WriteLine($"❌ 客户 {accountName} 未关联客户主数据");
                return;
            }
            var masterDataId = masterDataRef.Id;
            var sapNumber = account.GetAttributeValue<string>("accountnumber") ?? "";
            Console.WriteLine($"找到客户: {account.GetAttributeValue<string>("name")} ({accountId})");
            Console.WriteLine($"客户主数据: {masterDataId}");

            // 2. 找一条现有 mcs_dd_data 作为模板（避免必填字段缺失）
            var templateQuery = new Microsoft.Xrm.Sdk.Query.QueryExpression("mcs_dd_data")
            {
                ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet(true),
                TopCount = 1
            };
            var templates = service.RetrieveMultiple(templateQuery);

            var dd = new Microsoft.Xrm.Sdk.Entity("mcs_dd_data");

            if (templates.Entities.Count > 0)
            {
                var template = templates.Entities[0];
                Console.WriteLine($"使用现有记录作为模板: {template.Id}");

                foreach (var attr in template.Attributes)
                {
                    var key = attr.Key;
                    // 跳过主键、系统字段、BPP/工作流相关字段
                    if (key == "mcs_dd_dataid" ||
                        key == "mcs_name" ||
                        key == "mcs_customermasterdataid" ||
                        key == "mcs_businessstep" ||
                        key == "mcs_evaluation_all" ||
                        key == "mcs_workflowid" ||
                        key == "mcs_bppurl" ||
                        key == "mcs_dd_data_url" ||
                        key == "mcs_bppstatus" ||
                        key == "mcs_approvedate" ||
                        key == "mcs_bpprejectreason" ||
                        key == "mcs_bpperrormsg" ||
                        key == "mcs_nextapprover" ||
                        key == "createdon" ||
                        key == "modifiedon" ||
                        key == "createdby" ||
                        key == "modifiedby" ||
                        key == "ownerid" ||
                        key == "statecode" ||
                        key == "statuscode" ||
                        key.StartsWith("createdby") ||
                        key.StartsWith("modifiedby") ||
                        key.StartsWith("ownerid"))
                    {
                        continue;
                    }
                    dd[key] = attr.Value;
                }
            }
            else
            {
                Console.WriteLine("⚠️ 未找到现有重点尽调记录作为模板，尝试使用最小必填字段");

                // 尝试找一条线索
                var leadQuery = new Microsoft.Xrm.Sdk.Query.QueryExpression("mcs_leadmain")
                {
                    ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("mcs_leadmainid"),
                    TopCount = 1
                };
                var leads = service.RetrieveMultiple(leadQuery);
                if (leads.Entities.Count == 0)
                {
                    Console.WriteLine("❌ 系统中没有找到线索记录，无法创建重点尽调测试数据");
                    return;
                }
                dd["mcs_leadmainid"] = new Microsoft.Xrm.Sdk.EntityReference("mcs_leadmain", leads.Entities[0].Id);

                // 使用系统中第一个活动用户作为营销代表
                var userQuery = new Microsoft.Xrm.Sdk.Query.QueryExpression("systemuser")
                {
                    ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("systemuserid"),
                    Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression
                    {
                        Conditions = { new Microsoft.Xrm.Sdk.Query.ConditionExpression("isdisabled", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, false) }
                    },
                    TopCount = 1
                };
                var users = service.RetrieveMultiple(userQuery);
                if (users.Entities.Count == 0)
                {
                    Console.WriteLine("❌ 系统中没有找到活动用户，无法创建重点尽调测试数据");
                    return;
                }
                dd["mcs_salesrep"] = new Microsoft.Xrm.Sdk.EntityReference("systemuser", users.Entities[0].Id);

                // 必填字段使用默认值
                dd["mcs_address"] = "测试地址";
                dd["mcs_ddreason"] = new Microsoft.Xrm.Sdk.OptionSetValue(1);
                dd["mcs_ddstaffconfig"] = new Microsoft.Xrm.Sdk.OptionSetValue(1);
                dd["mcs_ddtype"] = new Microsoft.Xrm.Sdk.OptionSetValue(1);
            }

            // 3. 设置测试数据特有的字段
            dd["mcs_name"] = $"DD测试-{accountName}-{DateTime.Now:yyyyMMddHHmmss}";
            dd["mcs_customermasterdataid"] = new Microsoft.Xrm.Sdk.EntityReference("mcs_customermasterdata", masterDataId);
            dd["mcs_sapnumber"] = sapNumber;
            dd["mcs_businessstep"] = new Microsoft.Xrm.Sdk.OptionSetValue(1); // 创建申请（有效）
            dd["mcs_totalscore"] = 85m;
            dd["mcs_evaluation_all"] = $"【测试数据】这是为客户 {accountName} 模拟的重点尽调专家意见汇总。\n\n1. 客户基本情况良好；\n2. 财务状况稳定；\n3. 建议给予信用额度。";

            var ddId = service.Create(dd);
            Console.WriteLine($"✅ 重点尽调测试数据创建成功: {ddId}");
            Console.WriteLine($"   业务状态: 创建申请（有效）");
            Console.WriteLine($"   评估意见: {dd["mcs_evaluation_all"]}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 重点尽调测试数据创建失败: {ex.Message}");
            if (ex.InnerException != null)
                Console.WriteLine($"   内部异常: {ex.InnerException.Message}");
        }
    }

    static void CreateFcaQuotaTestData(ServiceClient service, string accountName)
    {
        Console.WriteLine($"=== 为客户 {accountName} 创建厂端授信额度测试数据 ===");

        // 1. 查询客户
        var accountQuery = new Microsoft.Xrm.Sdk.Query.QueryExpression("account")
        {
            ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("accountid", "name", "mcs_customermasterdata", "ownerid"),
            Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression
            {
                Conditions = { new Microsoft.Xrm.Sdk.Query.ConditionExpression("name", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, accountName) }
            }
        };
        var accounts = service.RetrieveMultiple(accountQuery);
        if (accounts.Entities.Count == 0)
        {
            Console.WriteLine($"❌ 未找到客户: {accountName}");
            return;
        }
        var account = accounts.Entities[0];
        var accountNameValue = account.GetAttributeValue<string>("name") ?? accountName;
        var ownerRef = account.GetAttributeValue<Microsoft.Xrm.Sdk.EntityReference>("ownerid");

        // 2. 获取关联客户主数据
        if (!account.Contains("mcs_customermasterdata") || account["mcs_customermasterdata"] is not Microsoft.Xrm.Sdk.EntityReference cmRef)
        {
            Console.WriteLine($"❌ 客户 {accountName} 未关联客户主数据");
            return;
        }

        var masterData = service.Retrieve("mcs_customermasterdata", cmRef.Id, new Microsoft.Xrm.Sdk.Query.ColumnSet("mcs_name"));
        var masterDataName = masterData.GetAttributeValue<string>("mcs_name") ?? accountNameValue;

        // 3. 创建 mcs_fca_quota 额度记录
        try
        {
            var quota = new Microsoft.Xrm.Sdk.Entity("mcs_fca_quota");
            quota["mcs_accountid"] = new Microsoft.Xrm.Sdk.EntityReference("mcs_customermasterdata", cmRef.Id);
            quota["mcs_custname"] = masterDataName;
            quota["mcs_sellergrant"] = new Microsoft.Xrm.Sdk.Money(100000m);
            quota["mcs_sellerbalance"] = new Microsoft.Xrm.Sdk.Money(80000m);
            quota["mcs_isactive"] = new Microsoft.Xrm.Sdk.OptionSetValue(1);
            quota["mcs_validfrom"] = DateTime.UtcNow.Date;
            quota["mcs_doid"] = "TEST202607010001";
            if (ownerRef != null)
            {
                quota["ownerid"] = ownerRef;
            }

            var quotaId = service.Create(quota);
            Console.WriteLine($"✅ 厂端授信额度记录创建成功: {quotaId}");
            Console.WriteLine($"   客户: {masterDataName}");
            Console.WriteLine($"   客户主数据ID: {cmRef.Id}");
            Console.WriteLine($"   厂端授信额度USD: 100,000");
            Console.WriteLine($"   厂端授信余额USD: 80,000");
            Console.WriteLine($"   是否生效: 是");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 额度记录创建失败: {ex.Message}");
            if (ex.InnerException != null)
                Console.WriteLine($"   内部异常: {ex.InnerException.Message}");
        }
    }

    static void CreateFcaQuotaAppTestData(ServiceClient service, string accountName)
    {
        Console.WriteLine($"=== 为客户 {accountName} 创建厂端授信额度调整申请测试数据 ===");

        // 1. 查询客户
        var accountQuery = new Microsoft.Xrm.Sdk.Query.QueryExpression("account")
        {
            ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("accountid", "name", "mcs_customermasterdata", "ownerid"),
            Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression
            {
                Conditions = { new Microsoft.Xrm.Sdk.Query.ConditionExpression("name", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, accountName) }
            }
        };
        var accounts = service.RetrieveMultiple(accountQuery);
        if (accounts.Entities.Count == 0)
        {
            Console.WriteLine($"❌ 未找到客户: {accountName}");
            return;
        }
        var account = accounts.Entities[0];
        var accountNameValue = account.GetAttributeValue<string>("name") ?? accountName;
        var ownerRef = account.GetAttributeValue<Microsoft.Xrm.Sdk.EntityReference>("ownerid");

        // 2. 获取关联客户主数据
        if (!account.Contains("mcs_customermasterdata") || account["mcs_customermasterdata"] is not Microsoft.Xrm.Sdk.EntityReference cmRef)
        {
            Console.WriteLine($"❌ 客户 {accountName} 未关联客户主数据");
            return;
        }

        var masterData = service.Retrieve("mcs_customermasterdata", cmRef.Id, new Microsoft.Xrm.Sdk.Query.ColumnSet("mcs_name", "mcs_sapnumber"));
        var masterDataName = masterData.GetAttributeValue<string>("mcs_name") ?? accountNameValue;
        var sapNumber = masterData.GetAttributeValue<string>("mcs_sapnumber") ?? accountNameValue;

        // 3. 查询当前生效的 mcs_fca_quota 额度（用于带出当前额度和余额）
        decimal currentGrant = 100000m;
        decimal currentBalance = 80000m;
        var quotaQuery = new Microsoft.Xrm.Sdk.Query.QueryExpression("mcs_fca_quota")
        {
            ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("mcs_fca_quotaid", "mcs_sellergrant", "mcs_sellerbalance"),
            Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression
            {
                Conditions = { new Microsoft.Xrm.Sdk.Query.ConditionExpression("mcs_accountid", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, cmRef.Id) }
            },
            TopCount = 1
        };
        quotaQuery.AddOrder("createdon", OrderType.Descending);
        var quotas = service.RetrieveMultiple(quotaQuery);
        if (quotas.Entities.Count > 0)
        {
            var quota = quotas.Entities[0];
            currentGrant = quota.GetAttributeValue<Microsoft.Xrm.Sdk.Money>("mcs_sellergrant")?.Value ?? 100000m;
            currentBalance = quota.GetAttributeValue<Microsoft.Xrm.Sdk.Money>("mcs_sellerbalance")?.Value ?? 80000m;
        }

        // 4. 创建 mcs_fca_quotaapp 额度调整申请记录
        try
        {
            var tobeGrant = currentGrant + 20000m; // 调整后额度增加 20,000
            var tobeBalance = currentBalance + 20000m;

            var quotaApp = new Microsoft.Xrm.Sdk.Entity("mcs_fca_quotaapp");
            quotaApp["mcs_accountid"] = new Microsoft.Xrm.Sdk.EntityReference("mcs_customermasterdata", cmRef.Id);
            quotaApp["mcs_custname"] = sapNumber;
            quotaApp["mcs_sellergrant"] = new Microsoft.Xrm.Sdk.Money(currentGrant);
            quotaApp["mcs_sellerbalance"] = new Microsoft.Xrm.Sdk.Money(currentBalance);
            quotaApp["mcs_tobegrant"] = new Microsoft.Xrm.Sdk.Money(tobeGrant);
            quotaApp["mcs_tobebalance"] = new Microsoft.Xrm.Sdk.Money(tobeBalance);
            quotaApp["mcs_bppstatus"] = new Microsoft.Xrm.Sdk.OptionSetValue(2); // 审批中
            quotaApp["mcs_bppstatuscode"] = "Submitted";
            if (ownerRef != null)
            {
                quotaApp["ownerid"] = ownerRef;
            }

            var quotaAppId = service.Create(quotaApp);
            Console.WriteLine($"✅ 厂端授信额度调整申请记录创建成功: {quotaAppId}");
            Console.WriteLine($"   客户: {masterDataName}");
            Console.WriteLine($"   客户主数据ID: {cmRef.Id}");
            Console.WriteLine($"   当前厂端授信额度USD: {currentGrant:N2}");
            Console.WriteLine($"   当前厂端授信余额USD: {currentBalance:N2}");
            Console.WriteLine($"   调整厂端授信额度USD: {tobeGrant:N2}");
            Console.WriteLine($"   调整后厂端授信余额USD: {tobeBalance:N2}");
            Console.WriteLine($"   审批状态: 2 (审批中)");
            Console.WriteLine($"   BPP 状态码: Submitted");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 额度调整申请记录创建失败: {ex.Message}");
            if (ex.InnerException != null)
                Console.WriteLine($"   内部异常: {ex.InnerException.Message}");
        }
    }

    static void CreateFsmResourceTestData(ServiceClient service, int institutionType = 1)
    {
        var typeLabel = institutionType == 2 ? "保险" : institutionType == 9 ? "其他" : "银行";
        Console.WriteLine($"=== 创建融资资源管理测试数据（机构类型: {typeLabel}） ===");

        try
        {
            // 查询基础数据（国家/州省/城市）作为 Lookup 值
            var countryQuery = new Microsoft.Xrm.Sdk.Query.QueryExpression("mcs_country")
            {
                ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("mcs_countryid"),
                TopCount = 1
            };
            var countries = service.RetrieveMultiple(countryQuery);

            var stateQuery = new Microsoft.Xrm.Sdk.Query.QueryExpression("mcs_state")
            {
                ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("mcs_stateid"),
                TopCount = 1
            };
            var states = service.RetrieveMultiple(stateQuery);

            var cityQuery = new Microsoft.Xrm.Sdk.Query.QueryExpression("mcs_city")
            {
                ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("mcs_cityid"),
                TopCount = 1
            };
            var cities = service.RetrieveMultiple(cityQuery);

            if (countries.Entities.Count == 0 || states.Entities.Count == 0 || cities.Entities.Count == 0)
            {
                Console.WriteLine("❌ 系统中缺少 mcs_country / mcs_state / mcs_city 基础数据，无法创建测试记录");
                return;
            }

            var resource = new Microsoft.Xrm.Sdk.Entity("mcs_fsm_resource");
            resource["mcs_fsm_resource_no"] = $"FSMR-TEST-{DateTime.Now:HHmmss}";
            resource["mcs_fsm_institution_type"] = new Microsoft.Xrm.Sdk.OptionSetValue(institutionType);
            resource["mcs_fsm_institution_code"] = $"TEST-{institutionType}-{DateTime.Now:HHmmss}";
            resource["mcs_fsm_institution_name"] = $"测试{typeLabel}机构";
            resource["mcs_fsm_institution_country"] = new Microsoft.Xrm.Sdk.EntityReference("mcs_country", countries.Entities[0].Id);
            resource["mcs_fsm_institution_province"] = new Microsoft.Xrm.Sdk.EntityReference("mcs_state", states.Entities[0].Id);
            resource["mcs_fsm_institution_city"] = new Microsoft.Xrm.Sdk.EntityReference("mcs_city", cities.Entities[0].Id);
            resource["mcs_fsm_institution_address"] = "测试地址";
            resource["mcs_fsm_institution_contact"] = "测试联系人";
            resource["mcs_fsm_institution_contact_position"] = "测试职位";
            resource["mcs_fsm_institution_contact_tel"] = "123456789";
            resource["mcs_fsm_institution_contact_email"] = "test@test.com";
            resource["mcs_fsm_institution_desc"] = "【测试数据】用于验证融资资源管理表单界面";
            resource["mcs_fsm_institution_products"] = new Microsoft.Xrm.Sdk.OptionSetValueCollection
            {
                new Microsoft.Xrm.Sdk.OptionSetValue(1),
                new Microsoft.Xrm.Sdk.OptionSetValue(2)
            };
            resource["mcs_fsm_institution_other_product"] = "其他测试产品";
            resource["mcs_fsm_institution_product_remark"] = "测试备注";
            resource["mcs_fsm_status"] = true;
            resource["mcs_fsm_rl_status"] = false;

            var resourceId = service.Create(resource);
            var url = $"https://dev1.crm5.dynamics.com/main.aspx?appid=&pagetype=entityrecord&etn=mcs_fsm_resource&id={resourceId}";

            Console.WriteLine($"✅ 融资资源管理测试记录创建成功: {resourceId}");
            Console.WriteLine($"   机构名称: 测试{typeLabel}机构");
            Console.WriteLine($"   机构类型: {typeLabel}({institutionType})");
            Console.WriteLine($"   金融产品: 1, 2");
            Console.WriteLine($"   记录链接: {url}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 融资资源管理测试记录创建失败: {ex.Message}");
            if (ex.InnerException != null)
                Console.WriteLine($"   内部异常: {ex.InnerException.Message}");
        }
    }

    /// <summary>
    /// 创建融资管理带出功能测试数据：指定客户下的 线索(mcs_leadmain) + 报价主表/报价单(mcs_quote_main/mcs_quoter) + 合同(mcs_contract)
    /// 字段对齐 mcs_fsm_data.js 级联带出逻辑：
    ///   线索：mcs_countryid / mcs_buid / mcs_customermasterdataid / mcs_accountnumber
    ///   报价主表：mcs_leadmainid；报价单：mcs_quote_mainid / mcs_countryid / mcs_customermasterdataid / mcs_customercode
    ///   合同：mcs_region / mcs_country / mcs_bu / mcs_customermaster / mcs_leadmain
    /// </summary>
    static void CreateFsmSourceTestData(ServiceClient service, string customerName)
    {
        Console.WriteLine($"=== 创建融资管理带出测试数据（客户: {customerName}） ===");

        EntityReference FindSingle(string entity, string nameField, string nameValue, string idField)
        {
            // 客户端内存匹配（规避服务端 Like/Equal 中文匹配异常）
            var q = new Microsoft.Xrm.Sdk.Query.QueryExpression(entity)
            {
                ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet(idField, nameField),
                TopCount = 500
            };
            var r = service.RetrieveMultiple(q);
            var hit = r.Entities.FirstOrDefault(e =>
                (e.GetAttributeValue<string>(nameField) ?? "").Contains(nameValue));
            if (hit == null)
            {
                Console.WriteLine($"   [调试] {entity} 共取 {r.Entities.Count} 条，内存匹配 '{nameValue}' 无结果");
                return null;
            }
            return hit.ToEntityReference();
        }

        try
        {
            // 1. 客户主数据
            var custQuery = new Microsoft.Xrm.Sdk.Query.QueryExpression("mcs_customermasterdata")
            {
                ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("mcs_name", "mcs_sapnumber"),
                TopCount = 5
            };
            custQuery.Criteria.AddCondition("mcs_name", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, customerName);
            var custResult = service.RetrieveMultiple(custQuery);
            if (custResult.Entities.Count == 0)
            {
                Console.WriteLine($"❌ 客户主数据中未找到: {customerName}");
                return;
            }
            var customer = custResult.Entities[0];
            var sapNumber = customer.GetAttributeValue<string>("mcs_sapnumber") ?? "";
            Console.WriteLine($"✓ 客户: {customer.GetAttributeValue<string>("mcs_name")} ({customer.Id}), SAP编码: {sapNumber}");

            // 2. 基础数据：国家（马来西亚）、事业部（泵路海外营销公司）、大区（含“印尼”的大区，兜底取第一条）
            var countryRef = FindSingle("mcs_country", "mcs_name", "马来西亚", "mcs_countryid");
            if (countryRef == null) { Console.WriteLine("❌ 未找到国家: 马来西亚"); return; }
            Console.WriteLine($"✓ 国家: {countryRef.Name} ({countryRef.Id})");

            var buRef = FindSingle("mcs_bu", "mcs_name", "泵路海外营销公司", "mcs_buid");
            if (buRef == null) { Console.WriteLine("❌ 未找到事业部: 泵路海外营销公司"); return; }
            Console.WriteLine($"✓ 事业部: {buRef.Name} ({buRef.Id})");

            var regionQuery = new Microsoft.Xrm.Sdk.Query.QueryExpression("mcs_region")
            {
                ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("mcs_name"),
                TopCount = 1
            };
            regionQuery.Criteria.AddCondition("mcs_name", Microsoft.Xrm.Sdk.Query.ConditionOperator.Like, "%印尼%");
            var regionResult = service.RetrieveMultiple(regionQuery);
            EntityReference regionRef = regionResult.Entities.Count > 0 ? regionResult.Entities[0].ToEntityReference() : null;
            if (regionRef == null)
            {
                var anyRegion = service.RetrieveMultiple(new Microsoft.Xrm.Sdk.Query.QueryExpression("mcs_region")
                {
                    ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("mcs_name"),
                    TopCount = 1
                });
                if (anyRegion.Entities.Count == 0) { Console.WriteLine("❌ 未找到任何大区(mcs_region)记录"); return; }
                regionRef = anyRegion.Entities[0].ToEntityReference();
            }
            Console.WriteLine($"✓ 大区: {regionRef.Name} ({regionRef.Id})");

            var customerRef = new EntityReference("mcs_customermasterdata", customer.Id);
            var created = new List<(string Entity, Guid Id, string Name)>();

            // 3. 线索（名称由自动编号生成）
            var lead = new Entity("mcs_leadmain");
            lead["mcs_channel"] = new Microsoft.Xrm.Sdk.OptionSetValue(429700000); // 线索渠道来源=D365（AutoNumberOnLeadMain 插件必填）
            lead["mcs_countryid"] = countryRef;
            lead["mcs_buid"] = buRef;
            lead["mcs_customermasterdataid"] = customerRef;
            lead["mcs_accountnumber"] = sapNumber;
            var leadId = service.Create(lead);
            var leadName = service.Retrieve("mcs_leadmain", leadId, new Microsoft.Xrm.Sdk.Query.ColumnSet("mcs_name")).GetAttributeValue<string>("mcs_name");
            created.Add(("mcs_leadmain", leadId, leadName));
            Console.WriteLine($"✅ 线索: {leadName} ({leadId})");

            // 4. 报价主表 → 报价单
            var quoteMain = new Entity("mcs_quote_main");
            quoteMain["mcs_leadmainid"] = new EntityReference("mcs_leadmain", leadId);
            var quoteMainId = service.Create(quoteMain);
            created.Add(("mcs_quote_main", quoteMainId, ""));
            Console.WriteLine($"✅ 报价主表: ({quoteMainId})");

            var quoter = new Entity("mcs_quoter");
            quoter["mcs_quote_mainid"] = new EntityReference("mcs_quote_main", quoteMainId);
            quoter["mcs_countryid"] = countryRef;
            quoter["mcs_customermasterdataid"] = customerRef;
            quoter["mcs_customercode"] = sapNumber;
            var quoterId = service.Create(quoter);
            var quoterName = service.Retrieve("mcs_quoter", quoterId, new Microsoft.Xrm.Sdk.Query.ColumnSet("mcs_name")).GetAttributeValue<string>("mcs_name");
            created.Add(("mcs_quoter", quoterId, quoterName));
            Console.WriteLine($"✅ 报价单: {quoterName} ({quoterId})");

            // 5. 合同（关联线索 + 买方客户）
            var contract = new Entity("mcs_contract");
            contract["mcs_region"] = regionRef;
            contract["mcs_country"] = countryRef;
            contract["mcs_bu"] = buRef;
            contract["mcs_customermaster"] = customerRef;
            contract["mcs_leadmain"] = new EntityReference("mcs_leadmain", leadId);
            var contractId = service.Create(contract);
            var contractName = service.Retrieve("mcs_contract", contractId, new Microsoft.Xrm.Sdk.Query.ColumnSet("mcs_name")).GetAttributeValue<string>("mcs_name");
            created.Add(("mcs_contract", contractId, contractName));
            Console.WriteLine($"✅ 合同: {contractName} ({contractId})");

            Console.WriteLine();
            Console.WriteLine("=== 测试数据创建完成，可在融资管理表单依次选择 线索/报价单/合同 验证带出 ===");
            Console.WriteLine("清理命令（如需）：");
            foreach (var (entity, id, _) in created.AsEnumerable().Reverse())
                Console.WriteLine($"  dotnet run --no-build -- delete-record {entity} {id}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 创建失败: {ex.Message}");
            if (ex.InnerException != null)
                Console.WriteLine($"   内部异常: {ex.InnerException.Message}");
        }
    }

    static void QueryFcaQuotaAppResult(ServiceClient service, string grantIdOrId)
    {
        Console.WriteLine($"=== 查询厂端授信额度调整申请结果: {grantIdOrId} ===");

        Guid recordId;
        if (!Guid.TryParse(grantIdOrId, out recordId))
        {
            var query = new Microsoft.Xrm.Sdk.Query.QueryExpression("mcs_fca_quotaapp")
            {
                ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("mcs_fca_quotaappid", "mcs_grantid", "mcs_bppstatus", "mcs_bppstatuscode", "mcs_approvedate"),
                Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression
                {
                    Conditions = { new Microsoft.Xrm.Sdk.Query.ConditionExpression("mcs_grantid", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, grantIdOrId) }
                }
            };
            var records = service.RetrieveMultiple(query);
            if (records.Entities.Count == 0)
            {
                Console.WriteLine($"❌ 未找到申请单编号: {grantIdOrId}");
                return;
            }
            recordId = records.Entities[0].Id;
        }

        var quotaApp = service.Retrieve("mcs_fca_quotaapp", recordId,
            new Microsoft.Xrm.Sdk.Query.ColumnSet("mcs_fca_quotaappid", "mcs_grantid", "mcs_accountid", "mcs_custname",
                "mcs_sellergrant", "mcs_sellerbalance", "mcs_tobegrant", "mcs_tobebalance",
                "mcs_bppstatus", "mcs_bppstatuscode", "mcs_approvedate"));

        Console.WriteLine("\n--- 申请单信息 ---");
        Console.WriteLine($"  记录ID: {quotaApp.Id}");
        Console.WriteLine($"  申请单编号: {quotaApp.GetAttributeValue<string>("mcs_grantid")}");
        Console.WriteLine($"  客户编码: {quotaApp.GetAttributeValue<string>("mcs_custname")}");
        Console.WriteLine($"  客户主数据: {quotaApp.GetAttributeValue<Microsoft.Xrm.Sdk.EntityReference>("mcs_accountid")?.Id}");
        Console.WriteLine($"  当前额度USD: {quotaApp.GetAttributeValue<Microsoft.Xrm.Sdk.Money>("mcs_sellergrant")?.Value:N2}");
        Console.WriteLine($"  当前余额USD: {quotaApp.GetAttributeValue<Microsoft.Xrm.Sdk.Money>("mcs_sellerbalance")?.Value:N2}");
        Console.WriteLine($"  调整额度USD: {quotaApp.GetAttributeValue<Microsoft.Xrm.Sdk.Money>("mcs_tobegrant")?.Value:N2}");
        Console.WriteLine($"  调整后余额USD: {quotaApp.GetAttributeValue<Microsoft.Xrm.Sdk.Money>("mcs_tobebalance")?.Value:N2}");
        Console.WriteLine($"  审批状态: {quotaApp.GetAttributeValue<Microsoft.Xrm.Sdk.OptionSetValue>("mcs_bppstatus")?.Value}");
        Console.WriteLine($"  BPP状态码: {quotaApp.GetAttributeValue<string>("mcs_bppstatuscode")}");
        Console.WriteLine($"  审批日期: {quotaApp.GetAttributeValue<DateTime>("mcs_approvedate")}");

        var accountRef = quotaApp.GetAttributeValue<Microsoft.Xrm.Sdk.EntityReference>("mcs_accountid");
        if (accountRef != null)
        {
            // 查询额度表
            var quotaQuery = new Microsoft.Xrm.Sdk.Query.QueryExpression("mcs_fca_quota")
            {
                ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("mcs_fca_quotaid", "mcs_sellergrant", "mcs_sellerbalance", "mcs_isactive", "mcs_validfrom"),
                Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression
                {
                    Conditions = { new Microsoft.Xrm.Sdk.Query.ConditionExpression("mcs_accountid", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, accountRef.Id) }
                },
                TopCount = 1
            };
            quotaQuery.AddOrder("createdon", OrderType.Descending);
            var quotas = service.RetrieveMultiple(quotaQuery);

            Console.WriteLine("\n--- 厂端授信额度表 (mcs_fca_quota) ---");
            if (quotas.Entities.Count > 0)
            {
                var quota = quotas.Entities[0];
                Console.WriteLine($"  记录ID: {quota.Id}");
                Console.WriteLine($"  厂端授信额度USD: {quota.GetAttributeValue<Microsoft.Xrm.Sdk.Money>("mcs_sellergrant")?.Value:N2}");
                Console.WriteLine($"  厂端授信余额USD: {quota.GetAttributeValue<Microsoft.Xrm.Sdk.Money>("mcs_sellerbalance")?.Value:N2}");
                Console.WriteLine($"  是否生效: {quota.GetAttributeValue<Microsoft.Xrm.Sdk.OptionSetValue>("mcs_isactive")?.Value}");
                Console.WriteLine($"  生效日期: {quota.GetAttributeValue<DateTime>("mcs_validfrom")}");
            }
            else
            {
                Console.WriteLine("  ❌ 未找到额度记录");
            }

            // 查询台账
            var recordQuery = new Microsoft.Xrm.Sdk.Query.QueryExpression("mcs_fca_records")
            {
                ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("mcs_fca_recordsid", "mcs_recordid", "mcs_proccess", "mcs_adjust",
                    "mcs_sellergrant", "mcs_asisbalance", "mcs_adjustamt", "mcs_tobebalance", "mcs_accountid", "createdon"),
                Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression
                {
                    Conditions = { new Microsoft.Xrm.Sdk.Query.ConditionExpression("mcs_accountid", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, accountRef.Id) }
                }
            };
            recordQuery.AddOrder("createdon", OrderType.Descending);
            var records = service.RetrieveMultiple(recordQuery);

            Console.WriteLine("\n--- 厂端授信额度动态调整管理台账 (mcs_fca_records) ---");
            if (records.Entities.Count > 0)
            {
                foreach (var record in records.Entities.Take(5))
                {
                    Console.WriteLine($"  记录ID: {record.Id}");
                    Console.WriteLine($"    台账编号: {record.GetAttributeValue<string>("mcs_recordid")}");
                    Console.WriteLine($"    流程环节: {record.GetAttributeValue<Microsoft.Xrm.Sdk.OptionSetValue>("mcs_proccess")?.Value}");
                    Console.WriteLine($"    调整动作: {record.GetAttributeValue<Microsoft.Xrm.Sdk.OptionSetValue>("mcs_adjust")?.Value}");
                    Console.WriteLine($"    授信限额USD: {record.GetAttributeValue<Microsoft.Xrm.Sdk.Money>("mcs_sellergrant")?.Value:N2}");
                    Console.WriteLine($"    现有授信余额USD: {record.GetAttributeValue<Microsoft.Xrm.Sdk.Money>("mcs_asisbalance")?.Value:N2}");
                    Console.WriteLine($"    调整金额USD: {record.GetAttributeValue<Microsoft.Xrm.Sdk.Money>("mcs_adjustamt")?.Value:N2}");
                    Console.WriteLine($"    调整后授信余额USD: {record.GetAttributeValue<Microsoft.Xrm.Sdk.Money>("mcs_tobebalance")?.Value:N2}");
                    Console.WriteLine($"    创建时间: {record.GetAttributeValue<DateTime>("createdon")}");
                }
            }
            else
            {
                Console.WriteLine("  ❌ 未找到台账记录");
            }
        }
    }

    static void SetCofaceId(ServiceClient service, string scoreId, string cofaceId)
    {
        Console.WriteLine($"=== 设置信用评估记录 {scoreId} 的科法斯客户代码 ===");

        var query = new Microsoft.Xrm.Sdk.Query.QueryExpression("mcs_credit_record")
        {
            ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("mcs_credit_recordid", "mcs_cofaceid"),
            Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression
            {
                Conditions = { new Microsoft.Xrm.Sdk.Query.ConditionExpression("mcs_scoreid", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, scoreId) }
            }
        };

        var records = service.RetrieveMultiple(query);
        if (records.Entities.Count == 0)
        {
            Console.WriteLine($"❌ 记录不存在: {scoreId}");
            return;
        }

        var record = records.Entities[0];
        var updateRecord = new Microsoft.Xrm.Sdk.Entity("mcs_credit_record") { Id = record.Id };
        updateRecord["mcs_cofaceid"] = cofaceId;
        service.Update(updateRecord);
        Console.WriteLine($"✅ 已设置 mcs_cofaceid = {cofaceId}");
    }

    static void ListAppActions(ServiceClient service, string? prefix, string? entityName = null)
    {
        Console.WriteLine($"=== 查询 App Action {(prefix != null ? $"(前缀: {prefix})" : "")}{(entityName != null ? $"(实体: {entityName})" : "")}===");
        var query = new Microsoft.Xrm.Sdk.Query.QueryExpression("appaction")
        {
            ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet(
                "appactionid", "uniquename", "name", "context", "contextentity", "contextvalue",
                "fonticon", "sequence", "statecode", "statuscode", "location",
                "onclickeventjavascriptfunctionname", "onclickeventjavascriptwebresourceid", "onclickeventjavascriptparameters",
                "buttonlabeltext", "buttontooltiptitle", "buttontooltipdescription", "visibilitytype"
            ),
            Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression(),
            Orders = { new Microsoft.Xrm.Sdk.Query.OrderExpression("uniquename", Microsoft.Xrm.Sdk.Query.OrderType.Ascending) }
        };
        if (!string.IsNullOrEmpty(prefix))
        {
            query.Criteria.Conditions.Add(new Microsoft.Xrm.Sdk.Query.ConditionExpression("uniquename", Microsoft.Xrm.Sdk.Query.ConditionOperator.BeginsWith, prefix));
        }
        if (!string.IsNullOrEmpty(entityName))
        {
            var entityId = FindComponentIdByName(service, "entity", "name", entityName);
            if (entityId == null)
            {
                Console.WriteLine($"❌ 环境中不存在实体: {entityName}");
                return;
            }
            query.Criteria.Conditions.Add(new Microsoft.Xrm.Sdk.Query.ConditionExpression("contextentity", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, entityId.Value));
        }

        try
        {
            var actions = service.RetrieveMultiple(query);
            Console.WriteLine($"找到 {actions.Entities.Count} 条 App Action");
            foreach (var action in actions.Entities)
            {
                var uniqueName = action.GetAttributeValue<string>("uniquename");
                var name = action.GetAttributeValue<string>("name");
                var context = action.GetAttributeValue<Microsoft.Xrm.Sdk.OptionSetValue>("context")?.Value;
                var contextEntity = action.GetAttributeValue<Microsoft.Xrm.Sdk.EntityReference>("contextentity");
                var contextValue = action.GetAttributeValue<string>("contextvalue");
                var fontIcon = action.GetAttributeValue<string>("fonticon");
                var sequence = action.GetAttributeValue<decimal?>("sequence");
                var state = action.GetAttributeValue<Microsoft.Xrm.Sdk.OptionSetValue>("statecode")?.Value;
                var jsFunction = action.GetAttributeValue<string>("onclickeventjavascriptfunctionname");
                var jsWebResourceRef = action.GetAttributeValue<Microsoft.Xrm.Sdk.EntityReference>("onclickeventjavascriptwebresourceid");
                var jsParams = action.GetAttributeValue<string>("onclickeventjavascriptparameters");
                var label = action.GetAttributeValue<string>("buttonlabeltext");
                var tooltipTitle = action.GetAttributeValue<string>("buttontooltiptitle");
                var tooltipDesc = action.GetAttributeValue<string>("buttontooltipdescription");
                var location = action.GetAttributeValue<Microsoft.Xrm.Sdk.OptionSetValue>("location")?.Value;
                var visibilityType = action.GetAttributeValue<Microsoft.Xrm.Sdk.OptionSetValue>("visibilitytype")?.Value;
                Console.WriteLine($"  - {uniqueName}");
                Console.WriteLine($"      名称: {name}");
                Console.WriteLine($"      标签: {label}");
                Console.WriteLine($"      提示标题: {tooltipTitle}");
                Console.WriteLine($"      提示描述: {tooltipDesc}");
                Console.WriteLine($"      上下文: {context} / {contextValue}");
                Console.WriteLine($"      上下文实体: {contextEntity?.LogicalName}({contextEntity?.Id})");
                Console.WriteLine($"      图标: {fontIcon}");
                Console.WriteLine($"      序号: {sequence}");
                Console.WriteLine($"      Location: {location}");
                Console.WriteLine($"      JS函数: {jsWebResourceRef?.LogicalName}({jsWebResourceRef?.Id}).{jsFunction}");
                Console.WriteLine($"      JS参数: {jsParams}");
                Console.WriteLine($"      VisibilityType: {visibilityType}");
                Console.WriteLine($"      状态: {(state == 0 ? "启用" : "停用")}");

                // 查询 LocalizedLabel 以确认多语言标签
                try
                {
                    foreach (var attrName in new[] { "buttonlabeltext", "buttontooltiptitle" })
                    {
                        var llRequest = new Microsoft.Crm.Sdk.Messages.RetrieveLocLabelsRequest
                        {
                            EntityMoniker = new Microsoft.Xrm.Sdk.EntityReference("appaction", action.Id),
                            AttributeName = attrName
                        };
                        var llResponse = (Microsoft.Crm.Sdk.Messages.RetrieveLocLabelsResponse)service.Execute(llRequest);
                        var labels = llResponse.Label.LocalizedLabels;
                        if (labels.Count > 0)
                        {
                            Console.WriteLine($"      {attrName} 多语言标签:");
                            foreach (var lbl in labels)
                            {
                                Console.WriteLine($"        - LCID={lbl.LanguageCode}: {lbl.Label}");
                            }
                        }
                    }
                }
                catch (Exception llEx)
                {
                    Console.WriteLine($"      ⚠️ 查询多语言标签失败: {llEx.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 查询 App Action 失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 列出 App Action 按钮关联的经典显隐规则（appaction_appactionrule_classicrules N:N）。
    /// 用途：发版验证点——按钮勾选消失修复依赖第一方规则 Mscrm.SelectionCountAtLeastOne 的关联，
    /// 该 N:N 关联是否随 Solution 导入目标环境需逐环境核对（只读）。
    /// </summary>
    static void ListAppActionRules(ServiceClient service, string? prefix)
    {
        Console.WriteLine($"=== 查询 App Action 经典显隐规则 {(prefix != null ? $"(前缀: {prefix})" : "")}===");
        var query = new Microsoft.Xrm.Sdk.Query.QueryExpression("appaction")
        {
            ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("appactionid", "uniquename", "visibilitytype"),
            Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression(),
            Orders = { new Microsoft.Xrm.Sdk.Query.OrderExpression("uniquename", Microsoft.Xrm.Sdk.Query.OrderType.Ascending) }
        };
        if (!string.IsNullOrEmpty(prefix))
        {
            query.Criteria.Conditions.Add(new Microsoft.Xrm.Sdk.Query.ConditionExpression("uniquename", Microsoft.Xrm.Sdk.Query.ConditionOperator.BeginsWith, prefix));
        }

        var actions = service.RetrieveMultiple(query);
        Console.WriteLine($"找到 {actions.Entities.Count} 条 App Action");
        foreach (var action in actions.Entities)
        {
            var uniqueName = action.GetAttributeValue<string>("uniquename");
            var visibilityType = action.GetAttributeValue<Microsoft.Xrm.Sdk.OptionSetValue>("visibilitytype")?.Value;
            Console.WriteLine($"  - {uniqueName}（VisibilityType={visibilityType}）");

            // 查 N:N 交叉实体（不支持排序字段 createdon，直接按 appactionid 过滤）
            var linkQuery = new Microsoft.Xrm.Sdk.Query.QueryExpression("appaction_appactionrule_classicrules")
            {
                ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("appactionid", "appactionruleid"),
                Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression
                {
                    Conditions = { new Microsoft.Xrm.Sdk.Query.ConditionExpression("appactionid", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, action.Id) }
                }
            };
            try
            {
                var links = service.RetrieveMultiple(linkQuery);
                if (links.Entities.Count == 0)
                {
                    Console.WriteLine($"      ⚠️ 无关联经典规则");
                    continue;
                }
                foreach (var link in links.Entities)
                {
                    var ruleId = link.GetAttributeValue<Guid>("appactionruleid");
                    try
                    {
                        var rule = service.Retrieve("appactionrule", ruleId, new Microsoft.Xrm.Sdk.Query.ColumnSet("uniquename", "name"));
                        Console.WriteLine($"      ✅ 规则: {rule.GetAttributeValue<string>("uniquename")} / {rule.GetAttributeValue<string>("name")} ({ruleId})");
                    }
                    catch (Exception ruleEx)
                    {
                        Console.WriteLine($"      ⚠️ 规则 {ruleId} 读取失败: {ruleEx.Message}");
                    }
                }
            }
            catch (Exception linkEx)
            {
                Console.WriteLine($"      ⚠️ 关联查询失败: {linkEx.Message}");
            }
        }
    }

    static void ListSecurityRoles(ServiceClient service, string? keyword)
    {
        Console.WriteLine($"=== 查询安全角色 {(keyword != null ? $"(关键字: {keyword})" : "")}===");
        var query = new Microsoft.Xrm.Sdk.Query.QueryExpression("role")
        {
            ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("roleid", "name", "businessunitid", "ismanaged", "parentroleid", "roletemplateid"),
            Orders = { new Microsoft.Xrm.Sdk.Query.OrderExpression("name", Microsoft.Xrm.Sdk.Query.OrderType.Ascending) }
        };
        if (!string.IsNullOrEmpty(keyword))
        {
            query.Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression
            {
                Conditions =
                {
                    new Microsoft.Xrm.Sdk.Query.ConditionExpression("name", Microsoft.Xrm.Sdk.Query.ConditionOperator.Like, $"%{keyword}%")
                }
            };
        }

        try
        {
            var roles = service.RetrieveMultiple(query);
            Console.WriteLine($"找到 {roles.Entities.Count} 条安全角色");
            foreach (var role in roles.Entities)
            {
                var id = role.Id;
                var name = role.GetAttributeValue<string>("name");
                var businessUnit = role.GetAttributeValue<Microsoft.Xrm.Sdk.EntityReference>("businessunitid");
                var isManaged = role.GetAttributeValue<bool?>("ismanaged") ?? false;
                var parentRole = role.GetAttributeValue<Microsoft.Xrm.Sdk.EntityReference>("parentroleid");
                var roleTemplate = role.GetAttributeValue<Microsoft.Xrm.Sdk.EntityReference>("roletemplateid");
                Console.WriteLine($"  - {name}");
                Console.WriteLine($"      RoleId: {id}");
                Console.WriteLine($"      业务部门: {businessUnit?.Name} ({businessUnit?.Id})");
                Console.WriteLine($"      IsManaged: {isManaged}");
                Console.WriteLine($"      ParentRoleId: {parentRole?.Id}");
                Console.WriteLine($"      RoleTemplateId: {roleTemplate?.Id}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 查询安全角色失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 只读诊断：查询指定安全角色的全部权限明细。
    /// 重点核对：信用评估两个配置实体（mcs_credit_items / mcs_credit_scoringcard）的 Read/Create，
    /// 以及 Excel 导入导出所需的 Data Import / Data Map / Import Source File / Export to Excel 权限。
    /// </summary>
    static void CheckRolePrivileges(ServiceClient service, string keyword)
    {
        Console.WriteLine($"=== 安全角色权限核对（只读）：关键字={keyword} ===");

        // 1. 查角色（可能有多个 BU 同名副本）
        var roleQuery = new QueryExpression("role")
        {
            ColumnSet = new ColumnSet("roleid", "name", "businessunitid", "parentroleid"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("name", ConditionOperator.Like, $"%{keyword}%") }
            }
        };
        var roles = RetrieveAllPages(service, roleQuery);
        if (roles.Count == 0)
        {
            Console.WriteLine($"  ❌ 当前环境未找到名称含 '{keyword}' 的安全角色");
            return;
        }
        Console.WriteLine($"找到 {roles.Count} 条匹配角色");

        // 2. privilegeid → name 映射
        var privNameMap = new Dictionary<Guid, string>();
        var allPrivQuery = new QueryExpression("privilege") { ColumnSet = new ColumnSet("name") };
        foreach (var p in RetrieveAllPages(service, allPrivQuery))
            privNameMap[p.Id] = p.GetAttributeValue<string>("name") ?? "";

        // 3. 需要重点核对的权限（Excel 导入导出按钮可见性相关）
        var checklist = new (string Priv, string 说明)[]
        {
            ("prvReadmcs_credit_items", "评分项目 读取（导出按钮需要）"),
            ("prvCreatemcs_credit_items", "评分项目 创建（导入按钮需要）"),
            ("prvWritemcs_credit_items", "评分项目 写入"),
            ("prvReadmcs_credit_scoringcard", "评分卡配置 读取（导出按钮需要）"),
            ("prvCreatemcs_credit_scoringcard", "评分卡配置 创建（导入按钮需要）"),
            ("prvWritemcs_credit_scoringcard", "评分卡配置 写入"),
            ("prvReadImport", "数据导入 读取"),
            ("prvCreateImport", "数据导入 创建"),
            ("prvReadImportMap", "数据映射 读取"),
            ("prvCreateImportMap", "数据映射 创建"),
            ("prvReadImportFile", "导入源文件 读取"),
            ("prvCreateImportFile", "导入源文件 创建"),
            ("prvExportToExcel", "导出到 Excel（业务管理页签杂项权限）"),
        };

        foreach (var role in roles)
        {
            var roleName = role.GetAttributeValue<string>("name");
            var bu = role.GetAttributeValue<EntityReference>("businessunitid");
            Console.WriteLine($"\n--- 角色: {roleName}  (BU: {bu?.Name})  id={role.Id} ---");

            var resp = (RetrieveRolePrivilegesRoleResponse)service.Execute(new RetrieveRolePrivilegesRoleRequest { RoleId = role.Id });
            // privilege name -> 最大深度
            var depthMap = new Dictionary<string, int>();
            foreach (var rp in resp.RolePrivileges)
            {
                if (!privNameMap.TryGetValue(rp.PrivilegeId, out var pname)) continue;
                var depth = (int)rp.Depth;
                if (!depthMap.ContainsKey(pname) || depthMap[pname] < depth) depthMap[pname] = depth;
            }
            Console.WriteLine($"  权限总数: {resp.RolePrivileges.Length}");

            Console.WriteLine("  【重点核对项】(深度: 0=本人 1=本部门 2=本部门及子部门 3=组织)");
            foreach (var (priv, desc) in checklist)
            {
                if (depthMap.TryGetValue(priv, out var d))
                    Console.WriteLine($"    ✅ {priv,-40} 深度={d}（{DepthLabel(d)}）  {desc}");
                else
                    Console.WriteLine($"    ❌ {priv,-40} 无权限        {desc}");
            }

            // 参考：角色所有含 import/excel 的权限实际名称（防止 privilege 名记错）
            var related = depthMap.Keys.Where(k => k.Contains("import", StringComparison.OrdinalIgnoreCase) || k.Contains("excel", StringComparison.OrdinalIgnoreCase)).OrderBy(k => k).ToList();
            Console.WriteLine($"  【该角色全部含 import/excel 的权限】{(related.Count == 0 ? "无" : "")}");
            foreach (var k in related)
                Console.WriteLine($"    {k} = 深度{depthMap[k]}（{DepthLabel(depthMap[k])}）");
        }
    }

    static void ListAppModules(ServiceClient service)
    {
        Console.WriteLine("=== 查询 Model-driven App ===");
        try
        {
            var query = new QueryExpression("appmodule")
            {
                ColumnSet = new ColumnSet("appmoduleid", "uniquename", "name", "description"),
                Criteria = new FilterExpression()
            };
            var result = service.RetrieveMultiple(query);
            Console.WriteLine($"找到 {result.Entities.Count} 个 App");
            foreach (var app in result.Entities)
            {
                Console.WriteLine($"  - {app.GetAttributeValue<string>("uniquename")}");
                Console.WriteLine($"      名称: {app.GetAttributeValue<string>("name")}");
                Console.WriteLine($"      ID: {app.Id}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 查询 App 失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 列出 D365 标准交易货币及汇率
    /// </summary>
    static void ListTransactionCurrencies(ServiceClient service)
    {
        Console.WriteLine("\n=== D365 标准交易货币及汇率 ===");

        try
        {
            // 1. 查询组织基础货币
            var orgQuery = new QueryExpression("organization")
            {
                ColumnSet = new ColumnSet("basecurrencyid", "name")
            };
            var orgResult = service.RetrieveMultiple(orgQuery);
            Guid? baseCurrencyId = null;
            if (orgResult.Entities.Count > 0)
            {
                var baseRef = orgResult.Entities[0].GetAttributeValue<EntityReference>("basecurrencyid");
                baseCurrencyId = baseRef?.Id;
                Console.WriteLine($"组织: {orgResult.Entities[0].GetAttributeValue<string>("name")}");
                Console.WriteLine($"基础货币 ID: {baseCurrencyId}");
            }

            // 2. 查询所有交易货币
            var query = new QueryExpression("transactioncurrency")
            {
                ColumnSet = new ColumnSet("transactioncurrencyid", "isocurrencycode", "currencyname", "currencysymbol", "exchangerate", "currencyprecision"),
                Orders = { new OrderExpression("isocurrencycode", OrderType.Ascending) }
            };

            var result = service.RetrieveMultiple(query);
            Console.WriteLine($"\n共维护 {result.Entities.Count} 种交易货币：\n");
            Console.WriteLine(string.Format("{0,-8} {1,-25} {2,-18} {3,-6} {4,-8}", "ISO代码", "货币名称", "汇率", "精度", "基础货币"));
            Console.WriteLine(new string('-', 75));

            foreach (var currency in result.Entities)
            {
                var id = currency.Id;
                var code = currency.GetAttributeValue<string>("isocurrencycode") ?? "";
                var name = currency.GetAttributeValue<string>("currencyname") ?? "";
                var symbol = currency.GetAttributeValue<string>("currencysymbol") ?? "";
                var rate = currency.GetAttributeValue<decimal>("exchangerate");
                var precision = currency.GetAttributeValue<int?>("currencyprecision") ?? 2;
                var isBase = baseCurrencyId.HasValue && id == baseCurrencyId.Value ? "是" : "";

                Console.WriteLine($"{code,-8} {name,-25} {rate,-18} {precision,-6} {isBase,-8}");
            }

            // 3. 尝试查询历史汇率记录（exchangerate 实体）
            try
            {
                var historyQuery = new QueryExpression("exchangerate")
                {
                    ColumnSet = new ColumnSet("exchangerateid", "transactioncurrencyid", "exchangerate1", "effectivedate"),
                    TopCount = 10,
                    Orders = { new OrderExpression("effectivedate", OrderType.Descending) }
                };
                var historyResult = service.RetrieveMultiple(historyQuery);
                Console.WriteLine($"\n历史汇率记录（exchangerate 实体）: {historyResult.Entities.Count} 条（Top 10）");
                foreach (var h in historyResult.Entities)
                {
                    var currencyRef = h.GetAttributeValue<EntityReference>("transactioncurrencyid");
                    var rate = h.GetAttributeValue<decimal>("exchangerate1");
                    var effective = h.GetAttributeValue<DateTime?>("effectivedate");
                    Console.WriteLine($"  {currencyRef?.Name ?? "?"}: {rate}, 生效: {effective:yyyy-MM-dd}");
                }
            }
            catch (Exception historyEx)
            {
                Console.WriteLine($"\n⚠️ 历史汇率实体（exchangerate）不可用或未启用: {historyEx.Message}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 查询失败: {ex.Message}");
        }
    }

    static void TestCofaceExchangeRate(ServiceClient service, string currencyList)
    {
        Console.WriteLine("\n=== Coface 汇率读取本地测试（方案 A：D365 标准汇率） ===");
        Console.WriteLine("测试逻辑：从 transactioncurrency 读取 1 USD -> LC 汇率，取倒数得到 1 LC -> USD 汇率\n");

        var codes = currencyList.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                .Select(c => c.ToUpperInvariant())
                                .Distinct()
                                .ToList();

        if (codes.Count == 0)
        {
            Console.WriteLine("未提供有效币种代码");
            return;
        }

        // 批量读取所有交易货币，减少查询次数
        var query = new QueryExpression("transactioncurrency")
        {
            ColumnSet = new ColumnSet("isocurrencycode", "currencyname", "exchangerate"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("isocurrencycode", ConditionOperator.In, codes.ToArray())
                }
            }
        };

        var result = service.RetrieveMultiple(query);
        var rateMap = result.Entities.ToDictionary(
            e => e.GetAttributeValue<string>("isocurrencycode")?.ToUpperInvariant() ?? "",
            e => e.GetAttributeValue<decimal>("exchangerate"),
            StringComparer.OrdinalIgnoreCase);

        const decimal sampleAmount = 10000m;

        Console.WriteLine(string.Format("{0,-6} {1,-22} {2,-22} {3,-16} {4,-16} {5,-10}", "币种", "D365汇率(1USD->LC)", "转换汇率(1LC->USD)", "测试金额(LC)", "转换后(USD)", "状态"));
        Console.WriteLine(new string('-', 100));

        foreach (var code in codes)
        {
            if (code == "USD")
            {
                Console.WriteLine(string.Format("{0,-6} {1,-22} {2,-22} {3,-16:F2} {4,-16:F2} {5,-10}", code, "1.0000000000", "1.0000000000", sampleAmount, sampleAmount, "固定汇率"));
                continue;
            }

            if (!rateMap.TryGetValue(code, out var d365Rate) || d365Rate <= 0m)
            {
                Console.WriteLine(string.Format("{0,-6} {1,-22} {2,-22} {3,-16:F2} {4,-16:F2} {5,-10}", code, "N/A", "N/A", sampleAmount, sampleAmount, "未配置"));
                continue;
            }

            var rateToUsd = 1m / d365Rate;
            var converted = sampleAmount * rateToUsd;
            Console.WriteLine(string.Format("{0,-6} {1,-22:F10} {2,-22:F10} {3,-16:F2} {4,-16:F2} {5,-10}", code, d365Rate, rateToUsd, sampleAmount, converted, "正常"));
        }

        Console.WriteLine("\n说明：");
        Console.WriteLine("- 转换汇率 = 1 / D365汇率");
        Console.WriteLine("- 转换后(USD) = 测试金额(LC) * 转换汇率");
        Console.WriteLine("- 未配置币种保持原金额不变（与 helper 行为一致）");
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

    /// <summary>
    /// 批量修复 mcs_credit_scoringcard.mcs_typeid
    /// 规则：根据 mcs_credititem 关联的 mcs_credit_items.mcs_group 映射
    /// 100000000->1 客户实力, 100000001->2 客户财务, 100000002->3 宏观市场, 100000003->4 历史交易
    /// </summary>
    static void FixScoringCardTypeIds(ServiceClient service)
    {
        Console.WriteLine("\n=== 批量修复评分卡项目分类(mcs_typeid) ===");

        var groupMap = new Dictionary<int, int>
        {
            { 100000000, 1 }, // 客户实力
            { 100000001, 2 }, // 客户财务
            { 100000002, 3 }, // 宏观市场
            { 100000003, 4 }  // 历史交易
        };

        // 查询所有评分卡配置及其关联的评分项目分类
        var query = new QueryExpression("mcs_credit_scoringcard")
        {
            ColumnSet = new ColumnSet("mcs_credit_scoringcardid", "mcs_typeid", "mcs_credititem"),
            PageInfo = new PagingInfo { Count = 500, PageNumber = 1 }
        };

        var link = new LinkEntity("mcs_credit_scoringcard", "mcs_credit_items", "mcs_credititem", "mcs_credit_itemsid", JoinOperator.LeftOuter)
        {
            Columns = new ColumnSet("mcs_group"),
            EntityAlias = "item"
        };
        query.LinkEntities.Add(link);

        int totalCount = 0;
        int updateCount = 0;
        int skipCount = 0;
        int failCount = 0;

        while (true)
        {
            var result = service.RetrieveMultiple(query);
            if (result.Entities.Count == 0) break;

            foreach (var record in result.Entities)
            {
                totalCount++;
                var recordId = record.Id;

                // 获取当前 mcs_typeid
                var currentType = record.GetAttributeValue<OptionSetValue>("mcs_typeid");

                // 获取关联的 mcs_group
                int? groupValue = null;
                var aliasedGroup = record.GetAttributeValue<AliasedValue>("item.mcs_group");
                if (aliasedGroup?.Value is OptionSetValue osv)
                {
                    groupValue = osv.Value;
                }

                if (!groupValue.HasValue)
                {
                    Console.WriteLine($"  跳过 {recordId}: 关联评分项目未设置 mcs_group");
                    skipCount++;
                    continue;
                }

                if (!groupMap.TryGetValue(groupValue.Value, out int targetTypeValue))
                {
                    Console.WriteLine($"  跳过 {recordId}: 未知的 mcs_group 值 {groupValue.Value}");
                    skipCount++;
                    continue;
                }

                // 如果当前值已经正确，跳过
                if (currentType != null && currentType.Value == targetTypeValue)
                {
                    Console.WriteLine($"  跳过 {recordId}: mcs_typeid 已经是 {targetTypeValue}");
                    skipCount++;
                    continue;
                }

                // 执行更新
                var update = new Entity("mcs_credit_scoringcard", recordId);
                update["mcs_typeid"] = new OptionSetValue(targetTypeValue);

                try
                {
                    service.Update(update);
                    Console.WriteLine($"  更新 {recordId}: mcs_group={groupValue.Value} -> mcs_typeid={targetTypeValue}");
                    updateCount++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  失败 {recordId}: {ex.Message}");
                    failCount++;
                }
            }

            if (result.MoreRecords)
            {
                query.PageInfo.PageNumber++;
                query.PageInfo.PagingCookie = result.PagingCookie;
            }
            else
            {
                break;
            }
        }

        Console.WriteLine("\n=== 完成 ===");
        Console.WriteLine($"总计: {totalCount} 条");
        Console.WriteLine($"更新: {updateCount} 条");
        Console.WriteLine($"跳过: {skipCount} 条");
        Console.WriteLine($"失败: {failCount} 条");
    }

    /// <summary>
    /// 评分项目分类映射：mcs_credit_items.mcs_group -> mcs_credit_scoringcard.mcs_typeid
    /// 与 mcs_credit_scoringcard.js 表单带出逻辑保持一致
    /// </summary>
    static Dictionary<int, int> GetScoringCardGroupTypeMap()
    {
        return new Dictionary<int, int>
        {
            { 100000000, 1 }, // 客户实力
            { 100000001, 2 }, // 客户财务
            { 100000002, 3 }, // 宏观市场
            { 100000003, 4 }  // 历史交易
        };
    }

    /// <summary>
    /// 批量补全评分卡配置的带出字段：mcs_cardname(=评分项目名称)、mcs_typeid(按 mcs_credititem.mcs_group 映射)
    /// 背景：0828 新评分卡经 import-scoring-cards 批量导入时未写这两个字段（表单上由 JS 按评分项目 Lookup 带出，批量导入不走表单 JS）
    /// 仅更新缺失的记录，已有值不覆盖；limit>0 时达上限即停（生产试跑用）
    /// </summary>
    static void FixScoringCardDisplayFields(ServiceClient service, int limit)
    {
        Console.WriteLine("\n=== 批量补全评分卡带出字段(mcs_cardname/mcs_typeid) ===");
        if (limit > 0) Console.WriteLine($"试跑模式：最多更新 {limit} 条");
        var groupMap = GetScoringCardGroupTypeMap();

        var query = new QueryExpression("mcs_credit_scoringcard")
        {
            ColumnSet = new ColumnSet("mcs_credit_scoringcardid", "mcs_cardname", "mcs_typeid", "mcs_itemname", "mcs_credititem"),
            Orders = { new OrderExpression("mcs_credit_scoringcardid", OrderType.Ascending) },
            PageInfo = new PagingInfo { Count = 500, PageNumber = 1 }
        };
        query.LinkEntities.Add(new LinkEntity("mcs_credit_scoringcard", "mcs_credit_items", "mcs_credititem", "mcs_credit_itemsid", JoinOperator.LeftOuter)
        {
            Columns = new ColumnSet("mcs_group"),
            EntityAlias = "item"
        });

        int totalCount = 0, updateCount = 0, skipCount = 0, failCount = 0, warnCount = 0;
        while (true)
        {
            var result = service.RetrieveMultiple(query);
            if (result.Entities.Count == 0) break;

            foreach (var record in result.Entities)
            {
                totalCount++;
                var cardname = record.GetAttributeValue<string>("mcs_cardname");
                var typeId = record.GetAttributeValue<OptionSetValue>("mcs_typeid");
                var itemName = record.GetAttributeValue<string>("mcs_itemname");
                int? groupValue = null;
                if (record.GetAttributeValue<AliasedValue>("item.mcs_group")?.Value is OptionSetValue osv)
                    groupValue = osv.Value;

                bool needName = string.IsNullOrWhiteSpace(cardname);
                bool needType = typeId == null;
                if (!needName && !needType)
                {
                    skipCount++;
                    continue;
                }

                // 可修复性判断
                bool canFixName = needName && !string.IsNullOrWhiteSpace(itemName);
                bool canFixType = needType && groupValue.HasValue && groupMap.ContainsKey(groupValue.Value);
                if (!canFixName && !canFixType)
                {
                    Console.WriteLine($"  ⚠️ 无法修复 {record.Id}: itemname='{itemName}' mcs_group={(groupValue?.ToString() ?? "无")}");
                    warnCount++;
                    continue;
                }

                if (limit > 0 && updateCount >= limit)
                {
                    Console.WriteLine($"\n已达试跑上限 {limit} 条，提前结束");
                    PrintDisplayFixSummary(totalCount, updateCount, skipCount, failCount, warnCount);
                    return;
                }

                var update = new Entity("mcs_credit_scoringcard", record.Id);
                if (canFixName) update["mcs_cardname"] = itemName;
                if (canFixType) update["mcs_typeid"] = new OptionSetValue(groupMap[groupValue.Value]);

                try
                {
                    service.Update(update);
                    updateCount++;
                    Console.WriteLine($"  更新 {record.Id}: cardname={(canFixName ? itemName : "保持")} typeid={(canFixType ? groupMap[groupValue.Value].ToString() : "保持")}");
                }
                catch (Exception ex)
                {
                    failCount++;
                    Console.WriteLine($"  失败 {record.Id}: {ex.Message}");
                }
            }

            if (result.MoreRecords)
            {
                query.PageInfo.PageNumber++;
                query.PageInfo.PagingCookie = result.PagingCookie;
            }
            else
            {
                break;
            }
        }

        PrintDisplayFixSummary(totalCount, updateCount, skipCount, failCount, warnCount);
    }

    static void PrintDisplayFixSummary(int total, int updated, int skipped, int failed, int warned)
    {
        Console.WriteLine("\n=== 完成 ===");
        Console.WriteLine($"扫描: {total} 条");
        Console.WriteLine($"更新: {updated} 条");
        Console.WriteLine($"跳过(字段齐全): {skipped} 条");
        Console.WriteLine($"警告(缺源数据无法修复): {warned} 条");
        Console.WriteLine($"失败: {failed} 条");
    }

    /// <summary>
    /// 删除 DEV1 上 mcs_credit_scoringcard 的重复记录
    /// 重复判定：category + 评分项目编码 + 数据类型 + min + max + weight + listvalue名称
    /// 保留一条，删除其余
    /// </summary>
    static void RemoveDuplicateScoringCards(ServiceClient service)
    {
        Console.WriteLine("\n=== 删除评分卡配置重复记录 ===");

        var query = new QueryExpression("mcs_credit_scoringcard")
        {
            ColumnSet = new ColumnSet("mcs_credit_scoringcardid", "mcs_categoryid", "mcs_itemid", "mcs_datatype", "mcs_minvalue", "mcs_maxvalue", "mcs_weight", "mcs_listvalue"),
            PageInfo = new PagingInfo { Count = 500, PageNumber = 1 }
        };

        var link = new LinkEntity("mcs_credit_scoringcard", "mcs_credititem_value", "mcs_listvalue", "mcs_credititem_valueid", JoinOperator.LeftOuter)
        {
            Columns = new ColumnSet("mcs_listname"),
            EntityAlias = "lv"
        };
        query.LinkEntities.Add(link);

        var records = new List<Entity>();

        while (true)
        {
            var result = service.RetrieveMultiple(query);
            records.AddRange(result.Entities);
            if (result.MoreRecords)
            {
                query.PageInfo.PageNumber++;
                query.PageInfo.PagingCookie = result.PagingCookie;
            }
            else
            {
                break;
            }
        }

        Console.WriteLine($"读取到 {records.Count} 条记录");

        // 按重复键分组（listvalue 用名称，避免同名不同ID导致的伪不重复）
        var groups = records.GroupBy(r =>
        {
            var categoryId = r.GetAttributeValue<OptionSetValue>("mcs_categoryid")?.Value ?? 0;
            var itemCode = r.GetAttributeValue<string>("mcs_itemid") ?? "";
            var dataType = r.GetAttributeValue<OptionSetValue>("mcs_datatype")?.Value ?? 0;
            var min = r.GetAttributeValue<decimal?>("mcs_minvalue");
            var max = r.GetAttributeValue<decimal?>("mcs_maxvalue");
            var weight = r.GetAttributeValue<int?>("mcs_weight") ?? int.MinValue;
            var listValueName = "";
            var aliasedName = r.GetAttributeValue<AliasedValue>("lv.mcs_listname");
            if (aliasedName?.Value != null) listValueName = aliasedName.Value.ToString() ?? "";

            return (categoryId, itemCode, dataType, min, max, weight, listValueName);
        }).ToList();

        int duplicateGroupCount = 0;
        int deleteCount = 0;
        int failCount = 0;

        foreach (var group in groups)
        {
            if (group.Count() <= 1) continue;

            duplicateGroupCount++;
            var keep = group.First();
            var duplicates = group.Skip(1).ToList();

            Console.WriteLine($"  发现重复组: {group.Key}, 共 {group.Count()} 条, 保留 {keep.Id}, 删除 {duplicates.Count} 条");

            foreach (var dup in duplicates)
            {
                try
                {
                    service.Delete("mcs_credit_scoringcard", dup.Id);
                    deleteCount++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"    删除失败 {dup.Id}: {ex.Message}");
                    failCount++;
                }
            }
        }

        Console.WriteLine("\n=== 完成 ===");
        Console.WriteLine($"重复组: {duplicateGroupCount} 个");
        Console.WriteLine($"删除: {deleteCount} 条");
        Console.WriteLine($"失败: {failCount} 条");
        Console.WriteLine($"预计剩余: {records.Count - deleteCount} 条");
    }

    /// <summary>
    /// 为 SA 老客户评分卡补入 OverdueModel（预计损失率）30 分配置
    /// Excel 中 SA 老客户历史交易包含：预计损失率 = 逾期未回收率模型分 × 30 / 100，最高 30 分
    /// </summary>
    static void AddOverdueModelForSaExistingCustomer(ServiceClient service)
    {
        Console.WriteLine("\n=== 为 SA 老客户补入 OverdueModel（预计损失率）配置 ===");

        const string itemCode = "OverdueModel";
        const int saExistingCustomerCategoryId = 1;

        // 1. 查询 OverdueModel 评分项目
        var itemQuery = new QueryExpression("mcs_credit_items")
        {
            ColumnSet = new ColumnSet("mcs_credit_itemsid", "mcs_itemname", "mcs_datatype"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("mcs_credit_itemsno", ConditionOperator.Equal, itemCode) }
            }
        };
        var itemResult = service.RetrieveMultiple(itemQuery);
        if (itemResult.Entities.Count == 0)
        {
            Console.WriteLine($"❌ 评分项目 {itemCode} 不存在，请先创建评分项目");
            return;
        }

        var item = itemResult.Entities[0];
        var itemId = item.Id;
        var itemName = item.GetAttributeValue<string>("mcs_itemname") ?? "预计损失率";
        var dataType = item.GetAttributeValue<OptionSetValue>("mcs_datatype")?.Value ?? 100000000;

        Console.WriteLine($"找到评分项目: {itemCode} ({itemName}), ID={itemId}, DataType={dataType}");

        // 2. 检查 SA 老客户是否已存在 OverdueModel 配置
        var existingQuery = new QueryExpression("mcs_credit_scoringcard")
        {
            ColumnSet = new ColumnSet("mcs_credit_scoringcardid"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("mcs_categoryid", ConditionOperator.Equal, saExistingCustomerCategoryId),
                    new ConditionExpression("mcs_itemid", ConditionOperator.Equal, itemCode)
                }
            }
        };
        var existingResult = service.RetrieveMultiple(existingQuery);
        if (existingResult.Entities.Count > 0)
        {
            Console.WriteLine($"⚠️ SA 老客户已存在 {itemCode} 配置，跳过创建");
            return;
        }

        // 3. 创建评分卡配置行
        var ent = new Entity("mcs_credit_scoringcard");
        ent["mcs_categoryid"] = new OptionSetValue(saExistingCustomerCategoryId);
        ent["mcs_credititem"] = new EntityReference("mcs_credit_items", itemId);
        ent["mcs_itemid"] = itemCode;
        ent["mcs_itemname"] = itemName;
        ent["mcs_datatype"] = new OptionSetValue(dataType);
        ent["mcs_typeid"] = new OptionSetValue(4); // Historical Transaction 历史交易
        ent["mcs_minvalue"] = 0m;
        ent["mcs_maxvalue"] = 100m;
        ent["mcs_weight"] = 30;

        try
        {
            var newId = service.Create(ent);
            Console.WriteLine($"✅ 已创建 SA 老客户 OverdueModel 配置，ID={newId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 创建失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 禅道 #2090：OverdueModel（内部交易等级）评分方式调整
    /// 1. 评分项目改定性(100000001) + 说明改 S01~S10 口径（名称已是「内部交易等级」#2009，不动）
    /// 2. 新建 S01~S10 + O(缺失) 枚举值（幂等）
    /// 3. 重建评分卡 OverdueModel 配置：删除旧定量行，按类别创建 S01~S10 分档行 + 缺失档行
    /// 4. 迟付指数（LatePaymentIndex）按类别补「缺失」档行（min/max 均空，缺失时取权重）
    /// ⚠️ S01~S10 各档权重为占位值（30→3 步进 3），待业务配置表确认后调整；
    ///    缺失档权重按 Bug 单：OverdueModel 直销 17/经销商 15；迟付指数 直销 2/经销商 3
    /// 幂等：可重复执行（评分卡行先删后建，枚举/项目按现状跳过）
    /// </summary>
    static void UpdateOverdueModelToQualitative(ServiceClient service)
    {
        Console.WriteLine("\n=== 禅道#2090 OverdueModel 改定性 + 缺失档赋分 ===");

        const string itemCode = "OverdueModel";
        const int qualitativeDataType = 100000001;
        const string newItemDesc = "内部交易等级（S01~S10，人工复核选择）";

        // 真实权重（与生产 0829 新卡 scoring_cards_prod_0829.json 一致）：
        // 直销=SA老/新(cat 1/2)，缺失档 17；经销商=新/老(cat 6/7)，缺失档 15
        var tierWeightsDirect = new List<(string tier, int weight)>
        {
            ("S01", 4), ("S02", 7), ("S03", 10), ("S04", 13), ("S05", 16),
            ("S06", 18), ("S07", 21), ("S08", 24), ("S09", 27), ("S10", 30)
        };
        var tierWeightsDealer = new List<(string tier, int weight)>
        {
            ("S01", 7), ("S02", 10), ("S03", 11), ("S04", 15), ("S05", 18),
            ("S06", 21), ("S07", 23), ("S08", 26), ("S09", 28), ("S10", 30)
        };

        // 0. 读取客户类别选项标签（直销/经销商 → 缺失档权重映射）
        var categoryLabels = new Dictionary<int, string>();
        try
        {
            var attrResp = (RetrieveAttributeResponse)service.Execute(new RetrieveAttributeRequest
            {
                EntityLogicalName = "mcs_credit_scoringcard",
                LogicalName = "mcs_categoryid",
                RetrieveAsIfPublished = true
            });
            if (attrResp.AttributeMetadata is PicklistAttributeMetadata picklist)
            {
                foreach (var opt in picklist.OptionSet.Options)
                {
                    var label = opt.Label?.UserLocalizedLabel?.Label ?? "";
                    if (opt.Value.HasValue) categoryLabels[opt.Value.Value] = label;
                }
            }
            Console.WriteLine($"客户类别选项: {string.Join("; ", categoryLabels.Select(kv => $"{kv.Key}={kv.Value}"))}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ 读取 mcs_categoryid 选项标签失败: {ex.Message}（缺失档权重按直销口径兜底）");
        }

        bool IsDealer(int cat)
        {
            if (categoryLabels.TryGetValue(cat, out var lbl)) return lbl.Contains("经销");
            return cat == 6 || cat == 7; // 标签读取失败时按生产新卡类别口径兜底
        }

        // 1. 查询并更新评分项目（改定性 + 说明）
        var itemQuery = new QueryExpression("mcs_credit_items")
        {
            ColumnSet = new ColumnSet("mcs_credit_itemsid", "mcs_itemname", "mcs_itemdesc", "mcs_datatype"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("mcs_credit_itemsno", ConditionOperator.Equal, itemCode) }
            }
        };
        var itemResult = service.RetrieveMultiple(itemQuery);
        if (itemResult.Entities.Count == 0)
        {
            Console.WriteLine($"❌ 评分项目 {itemCode} 不存在，终止");
            return;
        }
        var item = itemResult.Entities[0];
        var itemId = item.Id;
        var itemName = item.GetAttributeValue<string>("mcs_itemname") ?? "内部交易等级";
        var curDataType = item.GetAttributeValue<OptionSetValue>("mcs_datatype")?.Value ?? 0;
        var curDesc = item.GetAttributeValue<string>("mcs_itemdesc") ?? "";
        Console.WriteLine($"评分项目: {itemCode}（{itemName}）, ID={itemId}, 当前 datatype={curDataType}");

        if (curDataType != qualitativeDataType || curDesc != newItemDesc)
        {
            var upd = new Entity("mcs_credit_items") { Id = itemId };
            upd["mcs_datatype"] = new OptionSetValue(qualitativeDataType);
            upd["mcs_itemdesc"] = newItemDesc;
            service.Update(upd);
            Console.WriteLine($"✅ 评分项目已更新: datatype {curDataType}→{qualitativeDataType}（定性）, desc→「{newItemDesc}」");
        }
        else
        {
            Console.WriteLine("⊘ 评分项目已是定性且说明已为新口径，跳过");
        }

        // 2. 枚举值 S01~S10 + O(缺失)（幂等）
        Console.WriteLine("\n步骤2: 枚举值 S01~S10 + O(缺失)...");
        var enumIds = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var enumDefs = tierWeightsDirect.Select(t => (val: t.tier, name: t.tier)).Concat(new[] { (val: "O", name: "缺失") });
        foreach (var def in enumDefs)
        {
            var eq = new QueryExpression("mcs_credititem_value")
            {
                ColumnSet = new ColumnSet("mcs_credititem_valueid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_credititemno", ConditionOperator.Equal, itemId),
                        new ConditionExpression("mcs_listvalue", ConditionOperator.Equal, def.val)
                    }
                },
                TopCount = 1
            };
            var exist = service.RetrieveMultiple(eq);
            if (exist.Entities.Count > 0)
            {
                enumIds[def.val] = exist.Entities[0].Id;
                Console.WriteLine($"  ⊘ 已存在: {itemCode}/{def.val}");
            }
            else
            {
                var e = new Entity("mcs_credititem_value");
                e["mcs_credititemno"] = new EntityReference("mcs_credit_items", itemId);
                e["mcs_listvalue"] = def.val;
                e["mcs_listname"] = def.name;
                enumIds[def.val] = service.Create(e);
                Console.WriteLine($"  ✓ 已创建: {itemCode}/{def.val}={def.name}, ID={enumIds[def.val]}");
            }
        }

        // 3. 重建评分卡 OverdueModel 配置（先删后建保证幂等）
        Console.WriteLine("\n步骤3: 重建评分卡 OverdueModel 配置...");
        var cardQuery = new QueryExpression("mcs_credit_scoringcard")
        {
            ColumnSet = new ColumnSet("mcs_credit_scoringcardid", "mcs_categoryid", "mcs_typeid"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("mcs_credititem", ConditionOperator.Equal, itemId) }
            }
        };
        var oldCards = service.RetrieveMultiple(cardQuery);
        var catTypeMap = new Dictionary<int, int>();
        foreach (var c in oldCards.Entities)
        {
            var cat = c.GetAttributeValue<OptionSetValue>("mcs_categoryid")?.Value ?? 0;
            var typ = c.GetAttributeValue<OptionSetValue>("mcs_typeid")?.Value ?? 4;
            if (cat > 0) catTypeMap[cat] = typ;
        }
        Console.WriteLine($"现有 OverdueModel 配置 {oldCards.Entities.Count} 行，涉及类别: {string.Join(",", catTypeMap.Keys.OrderBy(k => k))}");

        int delCount = 0;
        foreach (var c in oldCards.Entities)
        {
            service.Delete("mcs_credit_scoringcard", c.Id);
            delCount++;
        }
        Console.WriteLine($"已删除旧配置: {delCount} 行");

        int createdCount = 0;
        foreach (var kv in catTypeMap.OrderBy(x => x.Key))
        {
            int cat = kv.Key;
            int typ = kv.Value;
            bool dealer = IsDealer(cat);
            var weights = dealer ? tierWeightsDealer : tierWeightsDirect;
            foreach (var tw in weights)
            {
                CreateScoringCardTierRow(service, cat, typ, itemId, itemCode, itemName, qualitativeDataType, enumIds[tw.tier], tw.weight);
                createdCount++;
            }
            int missingWeight = dealer ? 15 : 17;
            CreateScoringCardTierRow(service, cat, typ, itemId, itemCode, itemName, qualitativeDataType, enumIds["O"], missingWeight);
            createdCount++;
            Console.WriteLine($"  ✓ 类别 {cat}（{(dealer ? "经销商" : "直销")} {categoryLabels.GetValueOrDefault(cat, "")}）: S01~S10 + 缺失档({missingWeight}分)");
        }

        // 3.1 按生产新卡类别足迹补齐（1=SA老/2=SA新/6=老经销商/7=新经销商），旧卡没有的类别补建
        foreach (var cat in new[] { 1, 2, 6, 7 })
        {
            if (catTypeMap.ContainsKey(cat)) continue;
            bool dealer = IsDealer(cat);
            var weights = dealer ? tierWeightsDealer : tierWeightsDirect;
            foreach (var tw in weights)
            {
                CreateScoringCardTierRow(service, cat, 4, itemId, itemCode, itemName, qualitativeDataType, enumIds[tw.tier], tw.weight);
                createdCount++;
            }
            int mw = dealer ? 15 : 17;
            CreateScoringCardTierRow(service, cat, 4, itemId, itemCode, itemName, qualitativeDataType, enumIds["O"], mw);
            createdCount++;
            Console.WriteLine($"  ✓ 类别 {cat}（{(dealer ? "经销商" : "直销")} {categoryLabels.GetValueOrDefault(cat, "")}）: S01~S10 + 缺失档({mw}分)（按生产新卡补齐）");
        }

        // 4. 迟付指数（LatePaymentIndex）定性化重建（与生产新卡一致）
        // 背景：LPI 项目 08-25 已改定性（枚举 N/S/C/O），DEV1 旧卡仍是定量区间行；
        // 定量区间行与定性缺失档行共存会对同一标签双向重复计分（区间行按值命中 + 缺失档行按缺失命中），
        // 因此生产新卡类别（1/2/6/7）全量重建为定性 N/S/C/O 行；BC/个人(3/4/5) 待业务配置，仅清理误补的缺失档行、不动旧区间行
        Console.WriteLine("\n步骤4: 迟付指数定性化重建（类别 1/2/6/7 与生产新卡一致）...");
        const string lpCode = "LatePaymentIndex";
        var lpItemQuery = new QueryExpression("mcs_credit_items")
        {
            ColumnSet = new ColumnSet("mcs_credit_itemsid", "mcs_itemname"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("mcs_credit_itemsno", ConditionOperator.Equal, lpCode) }
            },
            TopCount = 1
        };
        var lpItemResult = service.RetrieveMultiple(lpItemQuery);
        if (lpItemResult.Entities.Count == 0)
        {
            Console.WriteLine($"⚠️ 评分项目 {lpCode} 不存在，跳过迟付指数重建");
        }
        else
        {
            var lpItemId = lpItemResult.Entities[0].Id;
            var lpItemName = lpItemResult.Entities[0].GetAttributeValue<string>("mcs_itemname") ?? "迟付指数";

            // 4.1 加载 N/S/C/O 枚举（缺 O 则补建，N/S/C 缺失则报警跳过）
            var lpEnumQuery = new QueryExpression("mcs_credititem_value")
            {
                ColumnSet = new ColumnSet("mcs_credititem_valueid", "mcs_listvalue"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("mcs_credititemno", ConditionOperator.Equal, lpItemId) }
                }
            };
            var lpEnumIds = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in service.RetrieveMultiple(lpEnumQuery).Entities)
            {
                var v = e.GetAttributeValue<string>("mcs_listvalue");
                if (!string.IsNullOrEmpty(v)) lpEnumIds[v] = e.Id;
            }
            if (!lpEnumIds.ContainsKey("O"))
            {
                var e = new Entity("mcs_credititem_value");
                e["mcs_credititemno"] = new EntityReference("mcs_credit_items", lpItemId);
                e["mcs_listvalue"] = "O";
                e["mcs_listname"] = "缺失";
                lpEnumIds["O"] = service.Create(e);
                Console.WriteLine($"  ✓ 已创建枚举: {lpCode}/O=缺失, ID={lpEnumIds["O"]}");
            }
            if (!lpEnumIds.ContainsKey("N") || !lpEnumIds.ContainsKey("S") || !lpEnumIds.ContainsKey("C"))
            {
                Console.WriteLine($"⚠️ {lpCode} 枚举 N/S/C 不完整（现有: {string.Join(",", lpEnumIds.Keys)}），跳过重建请先补齐枚举");
            }
            else
            {
                var lpQuery = new QueryExpression("mcs_credit_scoringcard")
                {
                    ColumnSet = new ColumnSet("mcs_credit_scoringcardid", "mcs_categoryid", "mcs_typeid", "mcs_datatype", "mcs_minvalue", "mcs_maxvalue", "mcs_listvalue"),
                    Criteria = new FilterExpression
                    {
                        Conditions = { new ConditionExpression("mcs_credititem", ConditionOperator.Equal, lpItemId) }
                    }
                };
                var lpCards = service.RetrieveMultiple(lpQuery).Entities;
                var lpTargetCats = new HashSet<int> { 1, 2, 6, 7 };

                // 4.2 清理非目标类别中误补的「缺失档」行（listvalue 空 且 min/max 均空，或 listvalue=O），旧区间行不动
                foreach (var c in lpCards)
                {
                    int cat = c.GetAttributeValue<OptionSetValue>("mcs_categoryid")?.Value ?? 0;
                    if (lpTargetCats.Contains(cat)) continue;
                    bool isQuantMissingRow = !c.Contains("mcs_minvalue") && !c.Contains("mcs_maxvalue") && !c.Contains("mcs_listvalue");
                    bool isQualMissingRow = c.GetAttributeValue<EntityReference>("mcs_listvalue")?.Id == lpEnumIds["O"];
                    if (isQuantMissingRow || isQualMissingRow)
                    {
                        service.Delete("mcs_credit_scoringcard", c.Id);
                        Console.WriteLine($"  🧹 类别 {cat} 清理误补缺失档行 {c.Id}");
                    }
                }

                // 4.3 目标类别全量重建：删除全部 LPI 行 → 定性 N=5/S=2/C=-3/O(直销2/经销商3)
                foreach (var g in lpCards.GroupBy(c => c.GetAttributeValue<OptionSetValue>("mcs_categoryid")?.Value ?? 0).OrderBy(x => x.Key))
                {
                    int cat = g.Key;
                    if (!lpTargetCats.Contains(cat)) continue;
                    int typ = g.First().GetAttributeValue<OptionSetValue>("mcs_typeid")?.Value ?? 1;
                    int del = 0;
                    foreach (var c in g)
                    {
                        service.Delete("mcs_credit_scoringcard", c.Id);
                        del++;
                    }
                    int ow = IsDealer(cat) ? 3 : 2;
                    CreateScoringCardTierRow(service, cat, typ, lpItemId, lpCode, lpItemName, qualitativeDataType, lpEnumIds["N"], 5);
                    CreateScoringCardTierRow(service, cat, typ, lpItemId, lpCode, lpItemName, qualitativeDataType, lpEnumIds["S"], 2);
                    CreateScoringCardTierRow(service, cat, typ, lpItemId, lpCode, lpItemName, qualitativeDataType, lpEnumIds["C"], -3);
                    CreateScoringCardTierRow(service, cat, typ, lpItemId, lpCode, lpItemName, qualitativeDataType, lpEnumIds["O"], ow);
                    Console.WriteLine($"  ✓ 类别 {cat}（{(IsDealer(cat) ? "经销商" : "直销")}）: 删 {del} 行 → 定性 N=5/S=2/C=-3/O={ow}");
                }
            }
        }

        Console.WriteLine($"\n=== 完成: 删除旧配置 {delCount} 行，新建 OverdueModel 配置 {createdCount} 行 ===");
        Console.WriteLine("权重口径：与生产 0829 新卡一致（直销缺失档 17/经销商 15；迟付指数缺失档直销 2/经销商 3）");
    }

    /// <summary>
    /// 创建评分卡配置行（禅道 #2090 复用）：listValueEnumId 为空表示定量/缺失档行（不设 mcs_listvalue、不设 min/max）
    /// </summary>
    static void CreateScoringCardTierRow(ServiceClient service, int categoryId, int typeId, Guid itemId, string itemCode, string itemName, int dataType, Guid? listValueEnumId, int weight)
    {
        var card = new Entity("mcs_credit_scoringcard");
        card["mcs_categoryid"] = new OptionSetValue(categoryId);
        card["mcs_typeid"] = new OptionSetValue(typeId);
        card["mcs_itemid"] = itemCode;
        card["mcs_credititem"] = new EntityReference("mcs_credit_items", itemId);
        card["mcs_itemname"] = itemName;
        card["mcs_cardname"] = itemName;
        card["mcs_datatype"] = new OptionSetValue(dataType);
        card["mcs_weight"] = weight;
        if (listValueEnumId.HasValue)
        {
            card["mcs_listvalue"] = new EntityReference("mcs_credititem_value", listValueEnumId.Value);
        }
        service.Create(card);
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
            ColumnSet = new ColumnSet("mcs_credit_item", "mcs_itemvalue1", "mcs_itemtxtvalue1", "mcs_itemintvalue1", "mcs_itemvalue2", "mcs_itemtxtvalue2", "mcs_itemintvalue2", "mcs_datatype", "mcs_accountid", "modifiedon", "mcs_credit_record"),
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
            string txt1 = t.GetAttributeValue<string>("mcs_itemtxtvalue1") ?? "N/A";
            var int1 = t.GetAttributeValue<decimal?>("mcs_itemintvalue1");
            string v2 = t.GetAttributeValue<string>("mcs_itemvalue2") ?? "N/A";
            string txt2 = t.GetAttributeValue<string>("mcs_itemtxtvalue2") ?? "N/A";
            var int2 = t.GetAttributeValue<decimal?>("mcs_itemintvalue2");
            int? dataType = t.GetAttributeValue<OptionSetValue>("mcs_datatype")?.Value;
            var accountRef = t.GetAttributeValue<EntityReference>("mcs_accountid");
            string accountInfo = accountRef != null ? $"{accountRef.Name}({accountRef.Id})" : "N/A";
            var recordRef = t.GetAttributeValue<EntityReference>("mcs_credit_record");
            string recordInfo = recordRef != null ? recordRef.Id.ToString() : "N/A";
            var modifiedOn = t.GetAttributeValue<DateTime?>("modifiedon");
            Console.WriteLine($"  {itemNo,-18} | {itemName,-12} | datatype={dataType} | v1={v1} | txt1={txt1} | int1={(int1.HasValue ? int1.Value.ToString("F2") : "N/A")} | v2={v2} | txt2={txt2} | int2={(int2.HasValue ? int2.Value.ToString("F2") : "N/A")} | modifiedon={modifiedOn} | record={recordInfo} | account={accountInfo}");
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
            ColumnSet = new ColumnSet("mcs_credit_scoringcardid", "mcs_credit_scoringcardno", "mcs_itemid", "mcs_itemname", "mcs_minvalue", "mcs_maxvalue", "mcs_listvalue"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("mcs_categoryid", ConditionOperator.Equal, 2) }
            }
        };
        var cards = service.RetrieveMultiple(q);

        // 预加载credit_items映射
        var itemQuery = new QueryExpression("mcs_credit_items")
        {
            ColumnSet = new ColumnSet("mcs_credit_itemsid", "mcs_credit_itemsno")
        };
        var itemResult = service.RetrieveMultiple(itemQuery);
        var itemMap = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var it in itemResult.Entities)
        {
            var no = it.GetAttributeValue<string>("mcs_credit_itemsno") ?? "";
            if (!string.IsNullOrEmpty(no)) itemMap[no] = it.Id;
        }

        // 目标：把定性评分卡的ListValue指向与当前标签值一致的枚举记录
        var targetQualitative = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ExternalRating"] = "3",
            ["CountryRisk"] = "A3",
            ["SectorRisk"] = "3"
        };

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

                case "ExternalRating":
                case "CountryRisk":
                case "SectorRisk":
                    if (targetQualitative.TryGetValue(itemCode, out string targetValue))
                    {
                        if (!itemMap.TryGetValue(itemCode, out Guid itemId))
                        {
                            Console.WriteLine($"  ⚠️ {cardNo} {itemCode}: 找不到评分项目");
                            break;
                        }
                        var enumQuery = new QueryExpression("mcs_credititem_value")
                        {
                            ColumnSet = new ColumnSet("mcs_credititem_valueid", "mcs_listvalue", "mcs_listname"),
                            Criteria = new FilterExpression
                            {
                                Conditions =
                                {
                                    new ConditionExpression("mcs_credititemno", ConditionOperator.Equal, itemId),
                                    new ConditionExpression("mcs_listvalue", ConditionOperator.Equal, targetValue)
                                }
                            },
                            TopCount = 1
                        };
                        var enums = service.RetrieveMultiple(enumQuery);
                        if (enums.Entities.Count == 0)
                        {
                            Console.WriteLine($"  ⚠️ {cardNo} {itemCode}: 找不到枚举值 {targetValue}");
                            break;
                        }
                        var enumId = enums.Entities[0].Id;
                        var enumName = enums.Entities[0].GetAttributeValue<string>("mcs_listname") ?? "";
                        update["mcs_listvalue"] = new EntityReference("mcs_credititem_value", enumId);
                        needUpdate = true;
                        Console.WriteLine($"  {cardNo} {itemCode}: listvalue → {targetValue} ({enumName})");
                    }
                    break;

                default:
                    Console.WriteLine($"  {cardNo} {itemCode}: 无需调整");
                    break;
            }

            if (needUpdate)
            {
                service.Update(update);
                updated++;
            }
        }

        Console.WriteLine($"\n✅ 已更新 {updated} 条评分卡配置");
        Console.WriteLine("预期得分：外部评级30 + 迟付指数10 + 国别风险10 + 行业风险10 + 净资产20 + 资产负债率10 + 流动比率10 = 100分");
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

        // NACE Rev.2.0 Division → 三一行业映射
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
            (53, 53, "Postal and courier activities", "交通运输"),
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
                    ["mcs_remark"] = "NACE Rev.2.0 Division → 三一行业映射，来源：原 Urba360Parser 硬编码配置"
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
                    ["mcs_remark"] = "NACE Rev.2.0 Division → 三一行业映射，来源：原 Urba360Parser 硬编码配置"
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
            // 创建实体（支持中英双语显示名）
            manager.CreateEntity(
                definition.EntityName,
                definition.DisplayName,
                definition.PrimaryAttribute,
                definition.PrimaryAttributeDisplayName,
                definition.PrimaryAttributeLength,
                definition.DisplayNameZh,
                definition.DisplayNameEn,
                definition.PrimaryAttributeDisplayNameZh,
                definition.PrimaryAttributeDisplayNameEn
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
                        manager.CreateStringField(definition.EntityName, field.SchemaName, field.DisplayName, field.Description, field.MaxLength ?? 100, field.Required, field.DisplayNameZh, field.DisplayNameEn, field.Format);
                        break;
                    case "memo":
                        manager.CreateMemoField(definition.EntityName, field.SchemaName, field.DisplayName, field.Description, field.MaxLength ?? 4000, field.DisplayNameZh, field.DisplayNameEn);
                        break;
                    case "integer":
                        manager.CreateIntegerField(definition.EntityName, field.SchemaName, field.DisplayName, field.Description, field.MinValue ?? 0, field.MaxValue ?? 100, field.DisplayNameZh, field.DisplayNameEn);
                        break;
                    case "decimal":
                        manager.CreateDecimalField(definition.EntityName, field.SchemaName, field.DisplayName, field.Description, field.MinDecimalValue ?? 0, field.MaxDecimalValue ?? 999999.99m, field.Precision ?? 2, field.DisplayNameZh, field.DisplayNameEn);
                        break;
                    case "money":
                        manager.CreateMoneyField(definition.EntityName, field.SchemaName, field.DisplayName, field.Description, field.MinDecimalValue ?? 0, field.MaxDecimalValue ?? 1000000, field.Precision ?? 2, field.DisplayNameZh, field.DisplayNameEn);
                        break;
                    case "datetime":
                        manager.CreateDateTimeField(definition.EntityName, field.SchemaName, field.DisplayName, field.Description, field.DateOnly ?? true, field.DisplayNameZh, field.DisplayNameEn);
                        break;
                    case "picklist":
                        manager.CreatePicklistField(definition.EntityName, field.SchemaName, field.DisplayName, field.Description, field.Options ?? new Dictionary<string, int>(), field.DisplayNameZh, field.DisplayNameEn);
                        break;
                    case "multiselectpicklist":
                        manager.CreateMultiSelectPicklistField(definition.EntityName, field.SchemaName, field.DisplayName, field.Description, field.Options ?? new Dictionary<string, int>(), field.DisplayNameZh, field.DisplayNameEn);
                        break;
                    case "boolean":
                        manager.CreateBooleanField(definition.EntityName, field.SchemaName, field.DisplayName, field.Description, field.TrueLabel ?? "是", field.FalseLabel ?? "否", field.DisplayNameZh, field.DisplayNameEn);
                        break;
                    case "lookup":
                        manager.CreateLookupField(definition.EntityName, field.SchemaName, field.DisplayName, field.Description, field.TargetEntity, field.TargetEntityDisplayName, field.DisplayNameZh, field.DisplayNameEn);
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
            var labels = field.DisplayName?.LocalizedLabels?.Select(l => $"{l.LanguageCode}:{l.Label}").ToArray();
            string labelInfo = labels != null ? string.Join(", ", labels) : "";
            string required = field.RequiredLevel?.Value.ToString() ?? "Unknown";
            Console.WriteLine($"  - {field.LogicalName,-30} {typeName,-15} {displayName} [{labelInfo}] Required={required} (Id: {field.MetadataId})");
        }
        
        Console.WriteLine($"\n总计: {fields.Count} 个字段");
    }

    static void UpdateForm(EntityManager manager, string entityName)
    {
        var fields = new Dictionary<string, string>();
        
        switch (entityName)
        {
            case "mcs_trade_pttype":
                fields = new Dictionary<string, string>
                {
                    { "mcs_typeid", "产品分类编码" },
                    { "mcs_trade_pttypename", "成交条件产品分类名称" },
                    { "mcs_typenameen", "产品分类英文名称" }
                };
                break;

            case "mcs_trade_ptgrouptype":
                fields = new Dictionary<string, string>
                {
                    { "mcs_groupid", "产品线编码" },
                    { "mcs_groupname", "产品线名称" },
                    { "mcs_trade_pttypeid", "成交条件产品分类" },
                    { "mcs_typeid", "产品分类编码" },
                    { "mcs_typename", "产品分类名称" }
                };
                break;

            case "mcs_trade_stpayterm":
                fields = new Dictionary<string, string>
                {
                    { "mcs_trade_stpaytermname", "标准条件编码" },
                    { "mcs_businessunit", "事业部" },
                    { "mcs_buid", "事业部编码" },
                    { "mcs_buname", "事业部名称" },
                    { "mcs_subsidiary", "大区/子公司" },
                    { "mcs_subid", "子公司编码" },
                    { "mcs_subname", "子公司名称" },
                    { "mcs_nation", "国家" },
                    { "mcs_countrycode", "国家代码" },
                    { "mcs_countryname", "国家名称" },
                    { "mcs_trade_pttype", "成交条件产品分类" },
                    { "mcs_typeid", "产品分类编码" },
                    { "mcs_typename", "产品分类名称" },
                    { "mcs_buyergrade", "客户分类" },
                    { "mcs_creditgrade", "客户等级" },
                    { "mcs_downpay", "首付款比例" },
                    { "mcs_payterm", "账期（天）" },
                    { "mcs_payfreq", "付款频次（天）" },
                    { "mcs_status", "生效状态" }
                };
                break;

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

    /// <summary>
    /// 整理定性评分项目枚举值：将国别风险/行业风险从 Coface 原始编码合并为三一标准编码 L/M/H/O
    /// 1. 创建/更新 4 条 L/M/H/O 记录并填充 mcs_cofacevalue
    /// 2. 更新评分卡配置 mcs_listvalue 指向新记录
    /// 3. 停用旧记录（不删除，避免影响历史标签数据引用）
    /// </summary>
    static void FixQualitativeEnumsMapping(ServiceClient service)
    {
        Console.WriteLine("=== 整理定性评分项目枚举值映射 ===");

        var mappings = new Dictionary<string, List<(string sanyCode, string displayName, string cofaceValues)>>
        {
            ["CountryRisk"] = new List<(string, string, string)>
            {
                ("L", "低风险", "A1,A2"),
                ("M", "中风险", "A3,A4"),
                ("H", "高风险", "B,C,D,E"),
                ("O", "缺失", "O")
            },
            ["SectorRisk"] = new List<(string, string, string)>
            {
                ("L", "低风险", "1"),
                ("M", "中风险", "2"),
                ("H", "高风险", "3,4"),
                ("O", "缺失", "O")
            }
        };

        foreach (var itemCode in mappings.Keys)
        {
            Console.WriteLine($"\n处理评分项目: {itemCode}");

            // 1. 查询评分项目 ID
            var itemQuery = new QueryExpression("mcs_credit_items")
            {
                ColumnSet = new ColumnSet("mcs_credit_itemsid"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("mcs_credit_itemsno", ConditionOperator.Equal, itemCode) }
                },
                TopCount = 1
            };
            var itemResult = service.RetrieveMultiple(itemQuery);
            if (itemResult.Entities.Count == 0)
            {
                Console.WriteLine($"  ⚠️ 找不到评分项目: {itemCode}");
                continue;
            }
            var itemId = itemResult.Entities[0].Id;

            // 2. 查询现有 active 记录
            var existingQuery = new QueryExpression("mcs_credititem_value")
            {
                ColumnSet = new ColumnSet("mcs_credititem_valueid", "mcs_listvalue", "mcs_listname", "mcs_cofacevalue"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_credititemno", ConditionOperator.Equal, itemId),
                        new ConditionExpression("statecode", ConditionOperator.Equal, 0)
                    }
                }
            };
            var existingRecords = service.RetrieveMultiple(existingQuery).Entities.ToList();
            Console.WriteLine($"  现有 active 记录: {existingRecords.Count} 条");

            // 3. 创建/更新 L/M/H/O 记录
            var newRecordMap = new Dictionary<Guid, (string sanyCode, string displayName, string cofaceValues)>();
            foreach (var (sanyCode, displayName, cofaceValues) in mappings[itemCode])
            {
                var existing = existingRecords.FirstOrDefault(r =>
                    string.Equals(r.GetAttributeValue<string>("mcs_listvalue") ?? "", sanyCode, StringComparison.OrdinalIgnoreCase));

                Guid recordId;
                if (existing != null)
                {
                    recordId = existing.Id;
                    // 更新 cofacevalue
                    var updateEnt = new Entity("mcs_credititem_value", recordId);
                    updateEnt["mcs_cofacevalue"] = cofaceValues;
                    service.Update(updateEnt);
                    Console.WriteLine($"  ✅ 更新 {sanyCode} ({displayName}): {recordId}");
                }
                else
                {
                    var createEnt = new Entity("mcs_credititem_value");
                    createEnt["mcs_credititemno"] = new EntityReference("mcs_credit_items", itemId);
                    createEnt["mcs_listvalue"] = sanyCode;
                    createEnt["mcs_listname"] = displayName;
                    createEnt["mcs_cofacevalue"] = cofaceValues;
                    recordId = service.Create(createEnt);
                    Console.WriteLine($"  ✅ 创建 {sanyCode} ({displayName}): {recordId}");
                }
                newRecordMap[recordId] = (sanyCode, displayName, cofaceValues);
            }

            // 4. 建立旧记录 ID -> 新记录 ID 映射
            var oldToNewMap = new Dictionary<Guid, Guid>();
            foreach (var oldRecord in existingRecords)
            {
                var oldListValue = oldRecord.GetAttributeValue<string>("mcs_listvalue") ?? "";
                string? targetSanyCode = null;

                foreach (var (sanyCode, displayName, cofaceValues) in mappings[itemCode])
                {
                    var values = cofaceValues.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                             .Select(v => v.Trim())
                                             .ToList();
                    if (values.Contains(oldListValue, StringComparer.OrdinalIgnoreCase) ||
                        string.Equals(oldListValue, sanyCode, StringComparison.OrdinalIgnoreCase))
                    {
                        targetSanyCode = sanyCode;
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(targetSanyCode))
                {
                    var newRecordId = newRecordMap.First(kvp => kvp.Value.sanyCode == targetSanyCode).Key;
                    if (oldRecord.Id != newRecordId)
                    {
                        oldToNewMap[oldRecord.Id] = newRecordId;
                    }
                }
            }

            // 5. 更新评分卡配置 mcs_listvalue Lookup 指向
            if (oldToNewMap.Count > 0)
            {
                Console.WriteLine($"  需要更新评分卡配置: {oldToNewMap.Count} 条旧记录映射");
                var scoringCardQuery = new QueryExpression("mcs_credit_scoringcard")
                {
                    ColumnSet = new ColumnSet("mcs_credit_scoringcardid", "mcs_listvalue"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("mcs_credititem", ConditionOperator.Equal, itemId),
                            new ConditionExpression("mcs_listvalue", ConditionOperator.NotNull)
                        }
                    }
                };
                var scoringCards = service.RetrieveMultiple(scoringCardQuery).Entities.ToList();
                int updatedCount = 0;
                foreach (var card in scoringCards)
                {
                    var lvRef = card.GetAttributeValue<EntityReference>("mcs_listvalue");
                    if (lvRef != null && oldToNewMap.TryGetValue(lvRef.Id, out var newId))
                    {
                        var updateCard = new Entity("mcs_credit_scoringcard", card.Id);
                        updateCard["mcs_listvalue"] = new EntityReference("mcs_credititem_value", newId);
                        service.Update(updateCard);
                        updatedCount++;
                    }
                }
                Console.WriteLine($"  ✅ 已更新评分卡配置: {updatedCount} 条");
            }

            // 6. 停用旧记录（排除新创建的 L/M/H/O 记录）
            int deactivatedCount = 0;
            foreach (var oldRecord in existingRecords)
            {
                if (newRecordMap.ContainsKey(oldRecord.Id))
                    continue;

                try
                {
                    var setState = new SetStateRequest
                    {
                        EntityMoniker = new EntityReference("mcs_credititem_value", oldRecord.Id),
                        State = new OptionSetValue(1), // Inactive
                        Status = new OptionSetValue(2)  // Inactive
                    };
                    service.Execute(setState);
                    deactivatedCount++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ⚠️ 停用旧记录失败 {oldRecord.Id}: {ex.Message}");
                }
            }
            Console.WriteLine($"  ✅ 已停用旧记录: {deactivatedCount} 条");
        }

        Console.WriteLine("\n=== 定性评分项目枚举值映射整理完成 ===");
    }

    /// <summary>
    /// 修复百分比/比率类评分卡区间的量纲不匹配问题
    /// Coface 返回的小数百分比（如 0.04 = 4%）与评分卡配置的百分比整数（如 [1,10)）不匹配
    /// 将 NetProfit / DebtRatio / LatePaymentIndex 的非极值边界统一除以 100
    /// 为避免同 category 内区间重叠校验失败，按 (itemCode, category) 分组：先停用 -> 更新 -> 再启用
    /// </summary>
    static void FixPercentageScoringCards(ServiceClient service)
    {
        Console.WriteLine("=== 修复百分比评分卡区间量纲 ===");

        var itemCodes = new[] { "NetProfit", "DebtRatio", "LatePaymentIndex" };

        // 目标小数边界（按 UAT 原始百分比整数 / 100 计算）
        var targetRanges = BuildPercentageTargetRanges();

        // 1. 查询所有涉及这三个指标且状态为 active 的评分卡配置
        var query = new QueryExpression("mcs_credit_scoringcard")
        {
            ColumnSet = new ColumnSet("mcs_credit_scoringcardid", "mcs_itemid", "mcs_itemname", "mcs_minvalue", "mcs_maxvalue", "mcs_weight", "mcs_categoryid", "statecode", "statuscode"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("mcs_itemid", ConditionOperator.In, itemCodes),
                    new ConditionExpression("statecode", ConditionOperator.Equal, 0) // Active only
                }
            },
            Orders =
            {
                new OrderExpression("mcs_itemid", OrderType.Ascending),
                new OrderExpression("mcs_categoryid", OrderType.Ascending),
                new OrderExpression("mcs_weight", OrderType.Descending)
            }
        };

        var records = service.RetrieveMultiple(query);
        Console.WriteLine($"找到 {records.Entities.Count} 条 active 百分比/比率类评分卡配置");

        // 2. 按 (itemCode, categoryId) 分组
        var groups = records.Entities
            .GroupBy(r => (ItemCode: r.GetAttributeValue<string>("mcs_itemid") ?? "", CategoryId: r.GetAttributeValue<OptionSetValue>("mcs_categoryid")?.Value ?? 0))
            .ToList();

        int updatedCount = 0;
        int skippedCount = 0;
        int groupCount = 0;

        foreach (var group in groups)
        {
            groupCount++;
            var itemCode = group.Key.ItemCode;
            var categoryId = group.Key.CategoryId;
            var itemName = group.First().GetAttributeValue<string>("mcs_itemname") ?? itemCode;
            Console.WriteLine($"\n[{groupCount}/{groups.Count}] 处理 {itemName}(category={categoryId}), 共 {group.Count()} 条");

            // 计算每个记录的目标边界
            var plannedUpdates = new List<(Entity Record, decimal NewMin, decimal NewMax)>();
            foreach (var record in group)
            {
                var weight = record.GetAttributeValue<int>("mcs_weight");
                var key = (itemCode, categoryId, weight);

                if (!targetRanges.TryGetValue(key, out var target))
                {
                    Console.WriteLine($"  ⚠️ weight={weight} 未在目标映射中找到，跳过");
                    skippedCount++;
                    continue;
                }

                plannedUpdates.Add((record, target.Min, target.Max));
            }

            if (!plannedUpdates.Any())
            {
                Console.WriteLine($"  ⏭️ 本组无匹配目标值");
                skippedCount += group.Count();
                continue;
            }

            // 3. 停用该组所有 active 记录，避免区间重叠校验
            var activeRecords = group.ToList();
            int deactivatedCount = 0;
            foreach (var record in activeRecords)
            {
                try
                {
                    var setState = new SetStateRequest
                    {
                        EntityMoniker = new EntityReference("mcs_credit_scoringcard", record.Id),
                        State = new OptionSetValue(1), // Inactive
                        Status = new OptionSetValue(2)  // Inactive
                    };
                    service.Execute(setState);
                    deactivatedCount++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ⚠️ 停用记录 {record.Id} 失败: {ex.Message}");
                }
            }
            Console.WriteLine($"  🚫 已停用 {deactivatedCount}/{activeRecords.Count} 条记录");

            // 4. 更新边界
            int groupUpdated = 0;
            foreach (var (record, newMin, newMax) in plannedUpdates)
            {
                var weight = record.GetAttributeValue<int>("mcs_weight");
                var oldMin = record.GetAttributeValue<decimal?>("mcs_minvalue");
                var oldMax = record.GetAttributeValue<decimal?>("mcs_maxvalue");

                // 如果当前值已与目标值一致，跳过
                if (oldMin == newMin && oldMax == newMax)
                {
                    Console.WriteLine($"  ⏭️ weight={weight} 已为目标值: [{newMin}, {newMax})");
                    skippedCount++;
                    continue;
                }

                try
                {
                    var update = new Entity("mcs_credit_scoringcard", record.Id);
                    update["mcs_minvalue"] = newMin;
                    update["mcs_maxvalue"] = newMax;
                    service.Update(update);
                    groupUpdated++;
                    updatedCount++;
                    Console.WriteLine($"  ✅ weight={weight}: [{oldMin}, {oldMax}) → [{newMin}, {newMax})");
                }
                catch (Exception ex)
                {
                    skippedCount++;
                    Console.WriteLine($"  ❌ weight={weight} 更新失败: {ex.Message}");
                }
            }

            // 5. 重新启用该组记录
            int reactivatedCount = 0;
            foreach (var record in activeRecords)
            {
                try
                {
                    var setState = new SetStateRequest
                    {
                        EntityMoniker = new EntityReference("mcs_credit_scoringcard", record.Id),
                        State = new OptionSetValue(0), // Active
                        Status = new OptionSetValue(1)  // Active
                    };
                    service.Execute(setState);
                    reactivatedCount++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ⚠️ 启用记录 {record.Id} 失败: {ex.Message}");
                }
            }
            Console.WriteLine($"  ✅ 已重新启用 {reactivatedCount}/{activeRecords.Count} 条记录 (更新 {groupUpdated} 条)");
        }

        Console.WriteLine($"\n=== 修复完成: 共 {groups.Count} 组, 更新 {updatedCount} 条, 跳过 {skippedCount} 条 ===");
    }

    /// <summary>
    /// 构建百分比/比率类评分卡目标小数边界映射
    /// Key: (ItemCode, CategoryId, Weight), Value: (Min, Max)
    /// </summary>
    static Dictionary<(string ItemCode, int CategoryId, int Weight), (decimal Min, decimal Max)> BuildPercentageTargetRanges()
    {
        var ranges = new Dictionary<(string, int, int), (decimal, decimal)>();
        const decimal pos = 99999999999m;
        const decimal neg = -999999999m;

        // NetProfit
        foreach (var cat in new[] { 1, 2, 6, 7 })
        {
            ranges[("NetProfit", cat, cat == 2 || cat == 7 ? 8 : 5)] = (0.10m, pos);
            ranges[("NetProfit", cat, cat == 2 || cat == 7 ? 5 : 3)] = (0.01m, 0.10m);
            ranges[("NetProfit", cat, 0)] = (neg, 0m);
        }
        foreach (var cat in new[] { 3, 4 })
        {
            ranges[("NetProfit", cat, cat == 4 ? 8 : 5)] = (0.11m, pos);
            ranges[("NetProfit", cat, cat == 4 ? 6 : 4)] = (0.06m, 0.11m);
            ranges[("NetProfit", cat, cat == 4 ? 3 : 2)] = (0.01m, 0.05m);
            ranges[("NetProfit", cat, 0)] = (neg, 0m);
        }

        // DebtRatio
        foreach (var cat in new[] { 1, 6 })
        {
            ranges[("DebtRatio", cat, 5)] = (neg, 0.71m);
            ranges[("DebtRatio", cat, 3)] = (0.71m, 1.01m);
            ranges[("DebtRatio", cat, 0)] = (1.01m, pos);
        }
        foreach (var cat in new[] { 2, 7 })
        {
            ranges[("DebtRatio", cat, 7)] = (neg, 0.71m);
            ranges[("DebtRatio", cat, cat == 2 ? 4 : 4)] = (0.71m, 1.01m);
            ranges[("DebtRatio", cat, 0)] = (1.01m, pos);
        }
        foreach (var cat in new[] { 3, 4 })
        {
            ranges[("DebtRatio", cat, cat == 3 ? 5 : 7)] = (neg, 0.61m);
            ranges[("DebtRatio", cat, cat == 3 ? 4 : 5)] = (0.61m, 0.81m);
            ranges[("DebtRatio", cat, cat == 3 ? 2 : 3)] = (0.81m, 1.01m);
            ranges[("DebtRatio", cat, 0)] = (1.01m, pos);
        }

        // LatePaymentIndex (所有 category 区间相同)
        foreach (var cat in new[] { 1, 2, 3, 4, 6, 7 })
        {
            ranges[("LatePaymentIndex", cat, 5)] = (neg, 0.21m);
            ranges[("LatePaymentIndex", cat, 4)] = (0.21m, 0.41m);
            ranges[("LatePaymentIndex", cat, 2)] = (0.41m, 0.61m);
            ranges[("LatePaymentIndex", cat, 0)] = (0.61m, 0.80m);
            ranges[("LatePaymentIndex", cat, -3)] = (0.80m, pos);
        }

        return ranges;
    }

    /// <summary>
    /// 清理 Sectors 评分卡中无法匹配当前 mcs_credititem_value 枚举值的孤儿记录
    /// UAT 评分卡中混用了独立行业名称（如'港务'）与合并分组枚举（如'矿业、港务'），
    /// 独立名称无法映射到当前枚举值，导致导入 DEV 时被跳过。此方法用于删除这些孤儿记录。
    /// </summary>
    static void CleanupOrphanSectorsScoringCards(ServiceClient service)
    {
        Console.WriteLine("=== 清理 Sectors 孤儿评分卡记录 ===");

        const string itemCode = "Sectors";

        // 1. 加载评分项目编码映射
        var itemQuery = new QueryExpression("mcs_credit_items")
        {
            ColumnSet = new ColumnSet("mcs_credit_itemsid", "mcs_credit_itemsno")
        };
        var itemResult = service.RetrieveMultiple(itemQuery);
        var itemMap = itemResult.Entities.ToDictionary(
            e => e.GetAttributeValue<string>("mcs_credit_itemsno") ?? "",
            e => e.Id,
            StringComparer.OrdinalIgnoreCase);

        if (!itemMap.TryGetValue(itemCode, out var sectorsItemId))
        {
            Console.WriteLine($"❌ 找不到评分项目 {itemCode}");
            return;
        }

        // 2. 加载 Sectors 当前所有 active 枚举值
        var enumQuery = new QueryExpression("mcs_credititem_value")
        {
            ColumnSet = new ColumnSet("mcs_credititem_valueid", "mcs_listvalue", "mcs_listname"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("mcs_credititemno", ConditionOperator.Equal, sectorsItemId),
                    new ConditionExpression("statecode", ConditionOperator.Equal, 0)
                }
            }
        };
        var enumResult = service.RetrieveMultiple(enumQuery);
        var enumById = enumResult.Entities.ToDictionary(
            e => e.Id,
            e => new
            {
                ListValue = (e.GetAttributeValue<string>("mcs_listvalue") ?? "").Trim(),
                ListName = (e.GetAttributeValue<string>("mcs_listname") ?? "").Trim()
            });

        var validListValues = new HashSet<string>(enumById.Values.Select(v => v.ListValue), StringComparer.OrdinalIgnoreCase);
        var validListNames = new HashSet<string>(enumById.Values.Select(v => v.ListName), StringComparer.OrdinalIgnoreCase);

        Console.WriteLine($"Sectors 有效枚举值: {enumById.Count} 条");
        Console.WriteLine($"  ListValue: {string.Join(", ", validListValues)}");
        Console.WriteLine($"  ListName: {string.Join(", ", validListNames)}");

        // 3. 查询所有 Sectors 评分卡配置
        var cardQuery = new QueryExpression("mcs_credit_scoringcard")
        {
            ColumnSet = new ColumnSet("mcs_credit_scoringcardid", "mcs_itemid", "mcs_itemname", "mcs_listvalue", "mcs_categoryid", "mcs_weight"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("mcs_itemid", ConditionOperator.Equal, itemCode),
                    new ConditionExpression("statecode", ConditionOperator.Equal, 0)
                }
            }
        };
        var cardResult = service.RetrieveMultiple(cardQuery);
        Console.WriteLine($"\n找到 {cardResult.Entities.Count} 条 Sectors active 评分卡配置");

        // 4. 找出无法匹配枚举值的孤儿记录
        var orphans = new List<Entity>();
        foreach (var card in cardResult.Entities)
        {
            var listValueRef = card.GetAttributeValue<EntityReference>("mcs_listvalue");
            string? listValueText = null;
            string? listNameText = null;
            bool isOrphan = false;

            if (listValueRef == null)
            {
                // 字段为空，可能是文本形式或真的为空
                var listValueStr = card.GetAttributeValue<string>("mcs_listvalue")?.Trim();
                if (string.IsNullOrEmpty(listValueStr))
                {
                    isOrphan = true;
                }
                else
                {
                    listValueText = listValueStr;
                    if (!validListValues.Contains(listValueStr) && !validListNames.Contains(listValueStr))
                        isOrphan = true;
                }
            }
            else
            {
                // Lookup 形式，根据 Id 判断
                if (!enumById.TryGetValue(listValueRef.Id, out var enumInfo))
                {
                    isOrphan = true;
                }
                else
                {
                    listValueText = enumInfo.ListValue;
                    listNameText = enumInfo.ListName;
                }
            }

            if (isOrphan)
            {
                var categoryId = card.GetAttributeValue<OptionSetValue>("mcs_categoryid")?.Value ?? 0;
                var weight = card.GetAttributeValue<int>("mcs_weight");
                var displayValue = listValueText ?? listNameText ?? (listValueRef?.Id.ToString() ?? "(空)");
                Console.WriteLine($"  ⚠️ 孤儿记录 {card.Id}: category={categoryId}, weight={weight}, ListValue='{displayValue}'");
                orphans.Add(card);
            }
        }

        Console.WriteLine($"\n发现 {orphans.Count} 条孤儿记录");

        if (orphans.Count == 0)
        {
            Console.WriteLine("✅ 无需清理");
            return;
        }

        // 5. 删除孤儿记录
        int deletedCount = 0;
        int failedCount = 0;
        foreach (var card in orphans)
        {
            try
            {
                service.Delete("mcs_credit_scoringcard", card.Id);
                deletedCount++;
                Console.WriteLine($"  🗑️ 已删除 {card.Id}");
            }
            catch (Exception ex)
            {
                failedCount++;
                Console.WriteLine($"  ❌ 删除失败 {card.Id}: {ex.Message}");
            }
        }

        Console.WriteLine($"\n=== 清理完成: 删除 {deletedCount} 条, 失败 {failedCount} 条 ===");
    }

    static void ExportCreditItemValues(ServiceClient service, string jsonPath)
    {
        Console.WriteLine("=== 导出 active 定性评分项目枚举值 ===");

        var query = new QueryExpression("mcs_credititem_value")
        {
            ColumnSet = new ColumnSet("mcs_listvalue", "mcs_listname", "mcs_cofacevalue"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("statecode", ConditionOperator.Equal, 0) }
            }
        };
        var link = new LinkEntity("mcs_credititem_value", "mcs_credit_items", "mcs_credititemno", "mcs_credit_itemsid", JoinOperator.Inner)
        {
            Columns = new ColumnSet("mcs_credit_itemsno"),
            EntityAlias = "item"
        };
        query.LinkEntities.Add(link);

        var result = service.RetrieveMultiple(query);
        Console.WriteLine($"读取到 {result.Entities.Count} 条记录");

        var records = result.Entities.Select(e => new
        {
            creditItemCode = e.GetAttributeValue<AliasedValue>("item.mcs_credit_itemsno")?.Value?.ToString() ?? "",
            listValue = e.GetAttributeValue<string>("mcs_listvalue") ?? "",
            listName = e.GetAttributeValue<string>("mcs_listname") ?? "",
            cofaceValue = e.GetAttributeValue<string>("mcs_cofacevalue") ?? ""
        }).ToList();

        var json = System.Text.Json.JsonSerializer.Serialize(records,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(jsonPath, json);
        Console.WriteLine($"✅ 已导出到: {jsonPath}");
    }

    static void ImportCreditItemValues(ServiceClient service, string jsonPath)
    {
        if (!File.Exists(jsonPath))
        {
            Console.WriteLine($"❌ 文件不存在: {jsonPath}");
            return;
        }

        Console.WriteLine("=== 导入定性评分项目枚举值（安全同步模式） ===");

        var json = File.ReadAllText(jsonPath);
        var sourceRecords = System.Text.Json.JsonSerializer.Deserialize<List<CreditItemValueImportRecord>>(json,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (sourceRecords == null || sourceRecords.Count == 0)
        {
            Console.WriteLine("❌ JSON 解析为空");
            return;
        }

        // 加载评分项目映射
        var itemQuery = new QueryExpression("mcs_credit_items")
        {
            ColumnSet = new ColumnSet("mcs_credit_itemsid", "mcs_credit_itemsno")
        };
        var itemResult = service.RetrieveMultiple(itemQuery);
        var itemMap = itemResult.Entities.ToDictionary(
            e => e.GetAttributeValue<string>("mcs_credit_itemsno") ?? "",
            e => e.Id);

        // 加载目标现有 active 枚举值
        var targetQuery = new QueryExpression("mcs_credititem_value")
        {
            ColumnSet = new ColumnSet("mcs_credititem_valueid", "mcs_listvalue", "mcs_listname", "mcs_cofacevalue"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("statecode", ConditionOperator.Equal, 0) }
            }
        };
        var targetLink = new LinkEntity("mcs_credititem_value", "mcs_credit_items", "mcs_credititemno", "mcs_credit_itemsid", JoinOperator.Inner)
        {
            Columns = new ColumnSet("mcs_credit_itemsno"),
            EntityAlias = "item"
        };
        targetQuery.LinkEntities.Add(targetLink);
        var targetResult = service.RetrieveMultiple(targetQuery);

        var targetRecords = targetResult.Entities.Select(e => new
        {
            Id = e.Id,
            CreditItemCode = (e.GetAttributeValue<AliasedValue>("item.mcs_credit_itemsno")?.Value?.ToString() ?? "").ToLowerInvariant(),
            ListValue = (e.GetAttributeValue<string>("mcs_listvalue") ?? "").ToLowerInvariant(),
            ListName = e.GetAttributeValue<string>("mcs_listname") ?? "",
            CofaceValue = e.GetAttributeValue<string>("mcs_cofacevalue") ?? ""
        }).ToList();

        var targetDict = targetRecords.ToDictionary(
            r => (r.CreditItemCode, r.ListValue),
            r => r);

        // 加载引用关系
        var scoringCardRefs = LoadCreditItemValueReferences(service, "mcs_credit_scoringcard", "mcs_listvalue");
        var customerTagRefs = LoadCreditItemValueReferences(service, "mcs_customer_tag", "mcs_credititem_value");
        var allRefs = scoringCardRefs.Concat(customerTagRefs).ToLookup(r => r.ValueId, r => r);

        int createdCount = 0;
        int updatedCount = 0;
        int unchangedCount = 0;
        int deletedCount = 0;
        int deactivatedCount = 0;
        int skippedCount = 0;

        var processedTargetKeys = new HashSet<(string, string)>();
        var createdOrUpdatedDict = new Dictionary<(string, string), Guid>();

        int index = 0;
        foreach (var rec in sourceRecords)
        {
            index++;
            var itemCode = rec.CreditItemCode ?? "";
            var listValue = rec.ListValue ?? "";
            var key = (itemCode.ToLowerInvariant(), listValue.ToLowerInvariant());

            if (!itemMap.TryGetValue(itemCode, out var itemId))
            {
                Console.WriteLine($"  [{index}/{sourceRecords.Count}] 跳过: 找不到评分项目 {itemCode}");
                skippedCount++;
                continue;
            }

            processedTargetKeys.Add(key);

            if (targetDict.TryGetValue(key, out var targetRec))
            {
                // 检查是否需要更新
                var newCoface = rec.CofaceValue ?? "";
                if (targetRec.ListName != (rec.ListName ?? "") || targetRec.CofaceValue != newCoface)
                {
                    try
                    {
                        var updateEnt = new Entity("mcs_credititem_value", targetRec.Id);
                        updateEnt["mcs_listname"] = rec.ListName ?? "";
                        updateEnt["mcs_cofacevalue"] = newCoface;
                        service.Update(updateEnt);
                        createdOrUpdatedDict[key] = targetRec.Id;
                        updatedCount++;
                        Console.WriteLine($"  [{index}/{sourceRecords.Count}] ✅ 更新 {itemCode}/{listValue}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  [{index}/{sourceRecords.Count}] ❌ 更新失败 {itemCode}/{listValue}: {ex.Message}");
                        skippedCount++;
                    }
                }
                else
                {
                    createdOrUpdatedDict[key] = targetRec.Id;
                    unchangedCount++;
                }
            }
            else
            {
                // 创建新记录
                var ent = new Entity("mcs_credititem_value");
                ent["mcs_credititemno"] = new EntityReference("mcs_credit_items", itemId);
                ent["mcs_listvalue"] = listValue;
                ent["mcs_listname"] = rec.ListName ?? "";
                ent["mcs_cofacevalue"] = rec.CofaceValue ?? "";
                try
                {
                    var newId = service.Create(ent);
                    createdOrUpdatedDict[key] = newId;
                    createdCount++;
                    Console.WriteLine($"  [{index}/{sourceRecords.Count}] ✅ 创建 {itemCode}/{listValue}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  [{index}/{sourceRecords.Count}] ❌ 创建失败 {itemCode}/{listValue}: {ex.Message}");
                    skippedCount++;
                }
            }
        }

        // 处理目标中多余的记录
        var sourceByItemCode = sourceRecords
            .Where(r => !string.IsNullOrEmpty(r.CreditItemCode))
            .GroupBy(r => r.CreditItemCode!.ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var targetRec in targetRecords)
        {
            if (processedTargetKeys.Contains((targetRec.CreditItemCode, targetRec.ListValue)))
                continue;

            var refs = allRefs[targetRec.Id].ToList();
            if (refs.Count == 0)
            {
                // 无引用，直接删除
                try
                {
                    service.Delete("mcs_credititem_value", targetRec.Id);
                    deletedCount++;
                    Console.WriteLine($"  🗑️ 删除 {targetRec.CreditItemCode}/{targetRec.ListValue} (无引用)");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ⚠️ 删除失败 {targetRec.CreditItemCode}/{targetRec.ListValue}: {ex.Message}");
                    skippedCount++;
                }
            }
            else
            {
                // 有被引用，尝试映射到同项目下 cofaceValue 包含该 listValue 的源记录
                bool remapped = false;
                if (sourceByItemCode.TryGetValue(targetRec.CreditItemCode, out var sourceList))
                {
                    var match = sourceList.FirstOrDefault(s =>
                    {
                        var cofaceValues = (s.CofaceValue ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(v => v.Trim().ToLowerInvariant());
                        return cofaceValues.Contains(targetRec.ListValue) ||
                               string.Equals(s.ListValue, targetRec.ListValue, StringComparison.OrdinalIgnoreCase);
                    });

                    if (match != null && itemMap.TryGetValue(match.CreditItemCode, out var matchItemId))
                    {
                        // 先确保目标存在该源记录（如果之前没创建成功则创建）
                        var sourceKey = (match.CreditItemCode.ToLowerInvariant(), (match.ListValue ?? "").ToLowerInvariant());
                        Guid newId;
                        if (targetDict.TryGetValue(sourceKey, out var existingSourceTarget))
                        {
                            newId = existingSourceTarget.Id;
                        }
                        else if (createdOrUpdatedDict.TryGetValue(sourceKey, out var createdId))
                        {
                            newId = createdId;
                        }
                        else
                        {
                            var createEnt = new Entity("mcs_credititem_value");
                            createEnt["mcs_credititemno"] = new EntityReference("mcs_credit_items", matchItemId);
                            createEnt["mcs_listvalue"] = match.ListValue ?? "";
                            createEnt["mcs_listname"] = match.ListName ?? "";
                            createEnt["mcs_cofacevalue"] = match.CofaceValue ?? "";
                            try
                            {
                                newId = service.Create(createEnt);
                                createdOrUpdatedDict[sourceKey] = newId;
                                createdCount++;
                                Console.WriteLine($"  ✅ 创建 {match.CreditItemCode}/{match.ListValue} 用于引用迁移");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"  ⚠️ 创建迁移目标失败 {match.CreditItemCode}/{match.ListValue}: {ex.Message}");
                                skippedCount++;
                                continue;
                            }
                        }

                        // 更新引用
                        int movedCount = 0;
                        bool moveFailed = false;
                        try
                        {
                            foreach (var reference in refs)
                            {
                                var updateEnt = new Entity(reference.EntityName, reference.RecordId);
                                updateEnt[reference.AttributeName] = new EntityReference("mcs_credititem_value", newId);
                                service.Update(updateEnt);
                                movedCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"  ⚠️ 迁移引用失败 {targetRec.CreditItemCode}/{targetRec.ListValue}: {ex.Message}");
                            moveFailed = true;
                        }

                        if (!moveFailed)
                        {
                            // 删除旧记录
                            try
                            {
                                service.Delete("mcs_credititem_value", targetRec.Id);
                                deletedCount++;
                                Console.WriteLine($"  🗑️ 删除 {targetRec.CreditItemCode}/{targetRec.ListValue} (迁移 {movedCount} 条引用到 {match.ListValue})");
                                remapped = true;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"  ⚠️ 迁移引用后删除失败 {targetRec.CreditItemCode}/{targetRec.ListValue}: {ex.Message}");
                                skippedCount++;
                            }
                        }
                    }
                }

                if (!remapped)
                {
                    // 无法映射，停用旧记录
                    try
                    {
                        var setState = new SetStateRequest
                        {
                            EntityMoniker = new EntityReference("mcs_credititem_value", targetRec.Id),
                            State = new OptionSetValue(1),
                            Status = new OptionSetValue(2)
                        };
                        service.Execute(setState);
                        deactivatedCount++;
                        Console.WriteLine($"  ⚠️ 停用 {targetRec.CreditItemCode}/{targetRec.ListValue} (存在 {refs.Count} 条引用且无法自动映射)");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  ⚠️ 停用失败 {targetRec.CreditItemCode}/{targetRec.ListValue}: {ex.Message}");
                        skippedCount++;
                    }
                }
            }
        }

        Console.WriteLine($"\n✅ 同步完成: 创建 {createdCount} 条, 更新 {updatedCount} 条, 不变 {unchangedCount} 条, 删除 {deletedCount} 条, 停用 {deactivatedCount} 条, 跳过/失败 {skippedCount} 条");
    }

    static List<(Guid ValueId, string EntityName, Guid RecordId, string AttributeName)> LoadCreditItemValueReferences(
        ServiceClient service, string entityName, string attributeName)
    {
        var result = new List<(Guid, string, Guid, string)>();
        var query = new QueryExpression(entityName)
        {
            ColumnSet = new ColumnSet($"{entityName}id", attributeName),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression(attributeName, ConditionOperator.NotNull) }
            }
        };
        var records = service.RetrieveMultiple(query);
        foreach (var e in records.Entities)
        {
            var refValue = e.GetAttributeValue<EntityReference>(attributeName);
            if (refValue != null)
            {
                result.Add((refValue.Id, entityName, e.Id, attributeName));
            }
        }
        return result;
    }

    static void DedupCreditItemValues(ServiceClient service)
    {
        Console.WriteLine("=== 清理 mcs_credititem_value 重复记录 ===");

        // 加载所有记录
        var query = new QueryExpression("mcs_credititem_value")
        {
            ColumnSet = new ColumnSet("mcs_credititem_valueid", "mcs_listvalue", "mcs_listname", "mcs_cofacevalue", "createdon", "statecode"),
            Orders = { new OrderExpression("createdon", OrderType.Ascending) }
        };
        var link = new LinkEntity("mcs_credititem_value", "mcs_credit_items", "mcs_credititemno", "mcs_credit_itemsid", JoinOperator.Inner)
        {
            Columns = new ColumnSet("mcs_credit_itemsno"),
            EntityAlias = "item"
        };
        query.LinkEntities.Add(link);

        var result = service.RetrieveMultiple(query);
        var records = result.Entities.Select(e => new
        {
            Id = e.Id,
            CreditItemCode = (e.GetAttributeValue<AliasedValue>("item.mcs_credit_itemsno")?.Value?.ToString() ?? "").ToLowerInvariant(),
            ListValue = (e.GetAttributeValue<string>("mcs_listvalue") ?? "").ToLowerInvariant(),
            CreatedOn = e.GetAttributeValue<DateTime>("createdon"),
            StateCode = e.GetAttributeValue<OptionSetValue>("statecode")?.Value ?? 0
        }).ToList();

        var groups = records.GroupBy(r => (r.CreditItemCode, r.ListValue)).Where(g => g.Count() > 1).ToList();
        if (groups.Count == 0)
        {
            Console.WriteLine("没有发现重复记录");
            return;
        }

        Console.WriteLine($"发现 {groups.Count} 组重复记录");

        // 加载引用关系
        var scoringCardRefs = LoadCreditItemValueReferences(service, "mcs_credit_scoringcard", "mcs_listvalue");
        var customerTagRefs = LoadCreditItemValueReferences(service, "mcs_customer_tag", "mcs_credititem_value");
        var allRefs = scoringCardRefs.Concat(customerTagRefs).ToLookup(r => r.ValueId, r => r);

        int deletedCount = 0;
        int skippedCount = 0;
        int migratedCount = 0;

        foreach (var g in groups)
        {
            var ordered = g.OrderBy(r => r.CreatedOn).ToList();
            var keeper = ordered.First();
            var duplicates = ordered.Skip(1).ToList();

            Console.WriteLine($"\n处理 {g.Key.CreditItemCode}/{g.Key.ListValue}: 保留 {keeper.Id} (创建于 {keeper.CreatedOn}), 删除 {duplicates.Count} 条重复");

            foreach (var dup in duplicates)
            {
                var refs = allRefs[dup.Id].ToList();
                if (refs.Count > 0)
                {
                    try
                    {
                        foreach (var reference in refs)
                        {
                            var updateEnt = new Entity(reference.EntityName, reference.RecordId);
                            updateEnt[reference.AttributeName] = new EntityReference("mcs_credititem_value", keeper.Id);
                            service.Update(updateEnt);
                            migratedCount++;
                        }
                        Console.WriteLine($"  迁移 {refs.Count} 条引用到保留记录");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  ⚠️ 迁移引用失败 {dup.Id}: {ex.Message}");
                        skippedCount++;
                        continue;
                    }
                }

                try
                {
                    service.Delete("mcs_credititem_value", dup.Id);
                    deletedCount++;
                    Console.WriteLine($"  🗑️ 删除重复记录 {dup.Id}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ⚠️ 删除失败 {dup.Id}: {ex.Message}");
                    skippedCount++;
                }
            }
        }

        Console.WriteLine($"\n✅ 清理完成: 删除 {deletedCount} 条, 迁移 {migratedCount} 条引用, 失败/跳过 {skippedCount} 条");
    }

    static void QueryOutstandingSample(ServiceClient service, string accountName)
    {
        Console.WriteLine($"=== 查询客户 [{accountName}] 的 mcs_outstanding 样本数据 ===");

        // 查找客户
        var accountQuery = new QueryExpression("account")
        {
            ColumnSet = new ColumnSet("accountid", "name"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("name", ConditionOperator.Equal, accountName) }
            },
            TopCount = 1
        };
        var accounts = service.RetrieveMultiple(accountQuery);
        if (accounts.Entities.Count == 0)
        {
            Console.WriteLine($"❌ 未找到客户: {accountName}");
            return;
        }
        var accountId = accounts.Entities[0].Id;
        Console.WriteLine($"客户ID: {accountId}");

        // 查询 mcs_outstanding
        var query = new QueryExpression("mcs_outstanding")
        {
            ColumnSet = new ColumnSet(
                "mcs_name", "mcs_createon", "createdon", "mcs_overdue",
                "mcs_newoverdueamountrmb", "mcs_overdueamount", "mcs_overdurationdays", "mcs_overduration",
                "mcs_newremainingamountrmb", "mcs_remainingamount", "mcs_dueamountrmb", "mcs_historicalpurchaseamount"
            ),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("mcs_account", ConditionOperator.Equal, accountId) }
            },
            Orders = { new OrderExpression("mcs_createon", OrderType.Descending) },
            TopCount = 10
        };
        var result = service.RetrieveMultiple(query);
        Console.WriteLine($"找到 {result.Entities.Count} 条记录\n");
        foreach (var e in result.Entities)
        {
            Console.WriteLine($"编号: {e.GetAttributeValue<string>("mcs_name") ?? ""}");
            Console.WriteLine($"  源创建时间(mcs_createon): {e.GetAttributeValue<DateTime?>("mcs_createon")}");
            Console.WriteLine($"  创建时间(createdon): {e.GetAttributeValue<DateTime?>("createdon")}");
            Console.WriteLine($"  逾期(mcs_overdue): {e.GetAttributeValue<bool?>("mcs_overdue")}");
            Console.WriteLine($"  最新逾期金额CNY(mcs_newoverdueamountrmb): {e.GetAttributeValue<decimal?>("mcs_newoverdueamountrmb")}");
            Console.WriteLine($"  逾期金额CNY(mcs_overdueamount): {e.GetAttributeValue<decimal?>("mcs_overdueamount")}");
            Console.WriteLine($"  逾期时长天(mcs_overdurationdays): {e.GetAttributeValue<int?>("mcs_overdurationdays")}");
            Console.WriteLine($"  逾期时长月(mcs_overduration): {e.GetAttributeValue<int?>("mcs_overduration")}");
            Console.WriteLine($"  历史采购额(mcs_historicalpurchaseamount): {e.GetAttributeValue<decimal?>("mcs_historicalpurchaseamount")}");
            Console.WriteLine();
        }
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

    static void QueryCreditItemDescs(ServiceClient service)
    {
        Console.WriteLine("=== 查询评分项目说明 (mcs_itemdesc) ===");
        var query = new QueryExpression("mcs_credit_items")
        {
            ColumnSet = new ColumnSet("mcs_credit_itemsno", "mcs_itemname", "mcs_itemdesc"),
            Orders = { new OrderExpression("mcs_credit_itemsno", OrderType.Ascending) }
        };
        var result = service.RetrieveMultiple(query);
        Console.WriteLine($"共 {result.Entities.Count} 条记录\n");
        Console.WriteLine("编码".PadRight(20) + " " + "名称".PadRight(18) + " 说明 (mcs_itemdesc)");
        Console.WriteLine(new string('-', 120));
        foreach (var e in result.Entities)
        {
            var code = e.GetAttributeValue<string>("mcs_credit_itemsno") ?? "";
            var name = e.GetAttributeValue<string>("mcs_itemname") ?? "";
            var desc = e.GetAttributeValue<string>("mcs_itemdesc") ?? "";
            Console.WriteLine($"{code,-20} {name,-18} {desc}");
        }
    }

    static void TryCreateField(Action createAction)
    {
        try
        {
            createAction();
        }
        catch (Exception ex) when (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"  ⊘ 字段已存在，跳过");
        }
    }

    static void AddFieldsToEntity(EntityManager manager, string entityName)
    {
        Console.WriteLine($"为 {entityName} 添加字段...");

        if (entityName == "account")
        {
            // 客户表新增字段（信用评估项目需要）
            Console.WriteLine("添加客户表字段（信用评估项目）...");
            AddAccountCreditFields(manager, "account");
            Console.WriteLine("客户表字段添加完成！");
        }
        else if (entityName == "mcs_customermasterdata")
        {
            // 客户主数据表新增字段（与 account 信用评估字段保持一致）
            Console.WriteLine("添加客户主数据表字段（信用评估项目）...");
            AddAccountCreditFields(manager, "mcs_customermasterdata");
            Console.WriteLine("客户主数据表字段添加完成！");
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
        else if (entityName == "mcs_credititem_value")
        {
            Console.WriteLine("添加定性评分项目枚举值表字段...");
            // Coface 原始值映射字段，用于三一标准编码与 Coface 编码的配置化映射
            manager.CreateStringField("mcs_credititem_value", "mcs_cofacevalue", "Coface 原始值",
                "存储 Coface 接口返回的原始值，多个值用英文逗号分隔，用于映射到三一标准编码", 255);
            Console.WriteLine("定性评分项目枚举值表字段添加完成！");
        }
        else if (entityName == "mcs_fca_quotaapp")
        {
            Console.WriteLine("添加厂端授信额度申请单字段...");
            // 信保额度类型：从 mcs_approvedquota 汇总，可逗号组合
            manager.CreateStringField("mcs_fca_quotaapp", "mcs_applygenre", "信保额度类型",
                "从 mcs_approvedquota 汇总，非信用证/信用证，可同时存在用逗号组合", 20);
            Console.WriteLine("厂端授信额度申请单字段添加完成！");
        }
        else if (entityName == "mcs_fsm_data")
        {
            Console.WriteLine("添加融资管理 BPP 审批字段...");
            // 审批状态：与 mcs_fca_quotaapp 保持一致，1申请/2审批中/3通过/4驳回
            TryCreateField(() => manager.CreatePicklistField("mcs_fsm_data", "mcs_bppstatus", "审批状态",
                "1申请/2审批中/3通过/4驳回",
                new Dictionary<string, int>
                {
                    { "申请", 1 },
                    { "审批中", 2 },
                    { "通过", 3 },
                    { "驳回", 4 }
                }));
            TryCreateField(() => manager.CreateStringField("mcs_fsm_data", "mcs_bppstatuscode", "BPP审批状态",
                "BPP回调状态码", 30));
            TryCreateField(() => manager.CreateStringField("mcs_fsm_data", "mcs_bppapprover", "当前审批人",
                "BPP当前审批人", 100));
            TryCreateField(() => manager.CreateStringField("mcs_fsm_data", "mcs_bppid", "BPP工作流ID",
                "BPP工作流ID", 50));
            TryCreateField(() => manager.CreateStringField("mcs_fsm_data", "mcs_bpperrormsg", "BPP错误信息",
                "BPP错误信息", 1000));
            TryCreateField(() => manager.CreateStringField("mcs_fsm_data", "mcs_bpprejectreason", "BPP驳回原因",
                "BPP驳回原因", 1000));
            TryCreateField(() => manager.CreateDateTimeField("mcs_fsm_data", "mcs_approvedate", "BPP审批完成日期",
                "BPP审批完成日期", dateOnly: true));
            TryCreateField(() => manager.CreateStringField("mcs_fsm_data", "mcs_fsm_data_url", "审批链接",
                "BPP审批链接", 1000));
            Console.WriteLine("融资管理 BPP 审批字段添加完成！");

            // Bug #1559（2026-08-04）：融资六要素/融资解决方案页面字段改造，新增 8 字段
            Console.WriteLine("添加融资管理 #1559 页面字段...");
            // 六要素「融资产品」单选下拉（仅银行类 1-11，编码与 mcs_fsm_institution_products 银行段一致）；
            // 六要素 tab + 方案 tab（标签=金融产品）双单元格展示
            TryCreateField(() => manager.CreatePicklistField("mcs_fsm_data", "mcs_fsm_product", "融资产品",
                "Bug #1559：六要素融资产品单选下拉，仅银行类金融产品（1-11）；六要素/方案双单元格展示",
                new Dictionary<string, int>
                {
                    { "Non-recourse Accounts Receivable Factoring", 1 },
                    { "Recourse Accounts Receivable Factoring", 2 },
                    { "Purchase Loan Financing", 3 },
                    { "Inventory Financing", 4 },
                    { "Dealer Financing", 5 },
                    { "Retail Factoring", 6 },
                    { "Leasing", 7 },
                    { "Consortium", 8 },
                    { "Investment Loan", 9 },
                    { "wholesale", 10 },
                    { "Others", 11 }
                },
                displayNameZh: "融资产品", displayNameEn: "Financing Product"));
            // 方案页「融资资源机构」多选（Memo 存 GUID 逗号分隔，多选查找组件写值）
            TryCreateField(() => manager.CreateMemoField("mcs_fsm_data", "mcs_fsm_resource_ids", "融资资源机构",
                "Bug #1559：融资资源机构多选，存 GUID 逗号分隔；按融资产品过滤，带入机构名称/编码", 2000,
                displayNameZh: "融资资源机构", displayNameEn: "Financing Institutions"));
            // 银行/保险/其它机构名称+编码（按所选机构类型自动带入，逗号分隔，只读）
            TryCreateField(() => manager.CreateStringField("mcs_fsm_data", "mcs_fsm_bank_names", "银行机构名称",
                "Bug #1559：机构类型=银行时自动带入机构名称，逗号分隔", 1000,
                displayNameZh: "银行机构名称", displayNameEn: "Bank Names"));
            TryCreateField(() => manager.CreateStringField("mcs_fsm_data", "mcs_fsm_bank_codes", "银行机构编码",
                "Bug #1559：机构类型=银行时自动带入机构编码，逗号分隔", 1000,
                displayNameZh: "银行机构编码", displayNameEn: "Bank Codes"));
            TryCreateField(() => manager.CreateStringField("mcs_fsm_data", "mcs_fsm_insurance_names", "保险机构名称",
                "Bug #1559：机构类型=保险时自动带入机构名称，逗号分隔", 1000,
                displayNameZh: "保险机构名称", displayNameEn: "Insurance Institution Names"));
            TryCreateField(() => manager.CreateStringField("mcs_fsm_data", "mcs_fsm_insurance_codes", "保险机构编码",
                "Bug #1559：机构类型=保险时自动带入机构编码，逗号分隔", 1000,
                displayNameZh: "保险机构编码", displayNameEn: "Insurance Institution Codes"));
            TryCreateField(() => manager.CreateStringField("mcs_fsm_data", "mcs_fsm_other_names", "其它机构名称",
                "Bug #1559：机构类型=其它时自动带入机构名称，逗号分隔", 1000,
                displayNameZh: "其它机构名称", displayNameEn: "Other Institution Names"));
            TryCreateField(() => manager.CreateStringField("mcs_fsm_data", "mcs_fsm_other_codes", "其它机构编码",
                "Bug #1559：机构类型=其它时自动带入机构编码，逗号分隔", 1000,
                displayNameZh: "其它机构编码", displayNameEn: "Other Institution Codes"));
            Console.WriteLine("融资管理 #1559 页面字段添加完成！");

            // Bug #1561（2026-08-04）：立项/方案两个提交审批各加一个备注字段（评审意见），提交时按 mcs_approve_type 推给 BPP（复用模板变量 mcs_remark）
            Console.WriteLine("添加融资管理 #1561 提交审批备注字段...");
            TryCreateField(() => manager.CreateMemoField("mcs_fsm_data", "mcs_fsm_initiation_remark", "立项提交审批备注",
                "Bug #1561：融资立项提交审批备注（评审意见），状态2（融资立项）可填，提交立项审批时推给BPP", 2000,
                displayNameZh: "立项提交审批备注", displayNameEn: "Initiation Submit Remark"));
            TryCreateField(() => manager.CreateMemoField("mcs_fsm_data", "mcs_fsm_project_remark", "方案提交审批备注",
                "Bug #1561：融资方案提交审批备注（评审意见），状态3（融资解决方案）可填，提交融资方案审批时推给BPP", 2000,
                displayNameZh: "方案提交审批备注", displayNameEn: "Project Submit Remark"));
            Console.WriteLine("融资管理 #1561 提交审批备注字段添加完成！");
        }
        else if (entityName == "mcs_credit_record")
        {
            Console.WriteLine("添加信用评估记录 Coface 系统内下单字段...");
            // Coface 系统内下单三字段（实施方案 V2.0 第 7 章）
            TryCreateField(() => manager.CreatePicklistField("mcs_credit_record", "mcs_cofaceorderstatus", "Coface 下单状态",
                "0未下单/1调查单已提交/2URBA已下单待就绪/3Report已下单待就绪/4已就绪/5下单失败",
                new Dictionary<string, int>
                {
                    { "未下单", 0 },
                    { "调查单已提交", 1 },
                    { "URBA已下单待就绪", 2 },
                    { "Report已下单待就绪", 3 },
                    { "已就绪", 4 },
                    { "下单失败", 5 }
                }));
            TryCreateField(() => manager.CreateStringField("mcs_credit_record", "mcs_cofaceordermsg", "Coface 下单信息",
                "各阶段订单号、publicationId、companyIdentificationId、失败原因", 500));
            TryCreateField(() => manager.CreateDateTimeField("mcs_credit_record", "mcs_cofaceorderdate", "Coface 下单时间",
                "最近一次下单/状态变更时间"));
            Console.WriteLine("信用评估记录 Coface 系统内下单字段添加完成！");
        }
        else
        {
            Console.WriteLine($"暂不支持为实体 {entityName} 批量添加字段");
        }
    }

    /// <summary>
    /// 为指定实体添加信用评估相关的 8 个自定义字段
    /// 用于 account 和 mcs_customermasterdata 保持字段定义一致
    /// </summary>
    static void AddAccountCreditFields(EntityManager manager, string entityName)
    {
        // 1. mcs_cofaceid - 科法斯客户代码
        manager.CreateStringField(entityName, "mcs_cofaceid", "科法斯客户代码",
            "Coface ICON平台唯一企业编码，用于对接Coface客户数据", 30);

        // 2. mcs_dealerrank - 经销商分级
        manager.CreatePicklistField(entityName, "mcs_dealerrank", "经销商分级",
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
        manager.CreateStringField(entityName, "mcs_externalrate", "客户信用外部评级",
            "Coface对客户信用评级，采用1-10分（10分最高）", 10);

        // 4. mcs_overduemodel - 逾期未回收率模型分
        manager.CreateDecimalField(entityName, "mcs_overduemodel", "逾期未回收率模型分",
            "逾期未回收率模型分，通过数据模型计算获得", 0, 999.99m, 2);

        // 5. mcs_creditscore - 客户信用评分
        manager.CreateDecimalField(entityName, "mcs_creditscore", "客户信用评分",
            "客户信用分计算结果", 0, 999.99m, 2);

        // 6. mcs_creditgrade - 客户等级
        manager.CreatePicklistField(entityName, "mcs_creditgrade", "客户等级",
            "客户等级A0-A4（A0最高），用于成交条件矩阵查询",
            new Dictionary<string, int>
            {
                { "A0", 100000000 },
                { "A1", 100000001 },
                { "A2", 100000002 },
                { "A3", 100000003 },
                { "A4", 100000004 }
            });

        // 7. mcs_isdd - 重点尽调
        manager.CreateBooleanField(entityName, "mcs_isdd", "重点尽调",
            "是否需要重点尽调，用于重点尽调模块判断");

        // 8. mcs_creditvalid - 信用评估有效状态
        manager.CreateBooleanField(entityName, "mcs_creditvalid", "信用评估有效状态",
            "信用评估有效状态：1-有效 0-失效");
    }

    static void RearrangeForm(EntityManager manager, string entityName)
    {
        Console.WriteLine($"重新排列 {entityName} 窗体...");
        
        if (entityName == "mcs_fca_mdlconfig")
        {
            var groups = new Dictionary<string, List<(string fieldName, string displayName)>>
            {
                ["参数管理维度"] = new List<(string, string)>
                {
                    ("mcs_buyergrade", "客户分类"),
                    ("mcs_creditgrade", "客户等级"),
                },
                ["模型系数和聚合方法"] = new List<(string, string)>
                {
                    ("mcs_adjust1", "系数1"),
                    ("mcs_adjust2", "系数2"),
                    ("mcs_adjust3", "系数3"),
                    ("mcs_aggfunc", "聚合方法"),
                },
                ["历史基准额度"] = new List<(string, string)>
                {
                    ("mcs_countryname", "历史基准额度"),
                },
                ["记录更新信息"] = new List<(string, string)>
                {
                    ("modifiedby", "编辑人员"),
                    ("modifiedon", "编辑日期"),
                },
            };
            manager.RearrangeForm(entityName, groups);
        }
        else if (entityName == "mcs_fca_mdlversion")
        {
            var groups = new Dictionary<string, List<(string, string)>>
            {
                ["常规"] = new List<(string, string)>
                {
                    ("mcs_isactive", "是否生效"),
                    ("mcs_modeldesc", "模型描述"),
                    ("mcs_validfrom", "开始日期"),
                    ("mcs_validend", "结束日期"),
                    ("modifiedby", "编辑人"),
                    ("modifiedon", "编辑时间"),
                },
            };
            manager.RearrangeForm(entityName, groups);
        }
        else if (entityName == "mcs_fca_quota")
        {
            var groups = new Dictionary<string, List<(string, string)>>
            {
                ["额度信息"] = new List<(string, string)>
                {
                    ("mcs_accountid", "客户编码"),
                    ("mcs_custname", "客户名称"),
                    ("mcs_sellergrant", "厂端授信额度USD"),
                    ("mcs_sellerbalance", "厂端授信余额USD"),
                    ("mcs_doid", "模型计算序列号"),
                    ("mcs_isactive", "是否生效"),
                    ("mcs_validfrom", "生效日期"),
                },
            };
            manager.RearrangeForm(entityName, groups);
        }
        else if (entityName == "mcs_fca_quotaapp")
        {
            var lookupFields = new HashSet<string> { "mcs_accountid", "mcs_doid", "mcs_typename", "createdby" };
            var picklistFields = new HashSet<string> { "mcs_paymode", "mcs_bppstatus" };

            string FieldCell(string fieldName, string displayName)
            {
                string classId = "{4273EDBD-AC1D-40d3-9FB2-095C621B552D}";
                if (lookupFields.Contains(fieldName)) classId = "{270BD3DB-D9AF-4782-9025-509E298DEC0A}";
                else if (picklistFields.Contains(fieldName)) classId = "{3EF39988-22BB-4f0b-BBBE-64B5A3748AEE}";
                return $"<cell id=\"{Guid.NewGuid():B}\" colspan=\"1\"><labels><label description=\"{displayName}\" languagecode=\"2052\" /></labels><control id=\"{fieldName}\" classid=\"{classId}\" datafieldname=\"{fieldName}\" /></cell>";
            }

            string SectionXml(string sectionName, List<(string fieldName, string displayName)> fields)
            {
                string sectionId = Guid.NewGuid().ToString("B");
                string rows = "";
                for (int i = 0; i < fields.Count; i += 2)
                {
                    rows += "<row>" + FieldCell(fields[i].fieldName, fields[i].displayName);
                    if (i + 1 < fields.Count)
                        rows += FieldCell(fields[i + 1].fieldName, fields[i + 1].displayName);
                    rows += "</row>";
                }
                return $"<section showlabel=\"true\" showbar=\"true\" IsUserDefined=\"1\" id=\"{sectionId}\" layout=\"varwidth\" celllabelalignment=\"Left\" celllabelposition=\"Left\" columns=\"11\" labelwidth=\"115\"><labels><label description=\"{sectionName}\" languagecode=\"2052\" /></labels><rows>{rows}</rows></section>";
            }

            string TabXml(string tabName, string sectionName, List<(string fieldName, string displayName)> fields)
            {
                string tabId = Guid.NewGuid().ToString("B");
                return $"<tab verticallayout=\"true\" id=\"{tabId}\" name=\"{tabName}\" showlabel=\"true\"><labels><label description=\"{tabName}\" languagecode=\"2052\" /></labels><columns><column width=\"100%\"><sections>{SectionXml(sectionName, fields)}</sections></column></columns></tab>";
            }

            var tab1 = new List<(string, string)>
            {
                ("mcs_grantid", "申请编号"),
                ("mcs_accountid", "客户编码"),
                ("mcs_custname", "客户名称"),
                ("mcs_creditgrade", "客户等级"),
                ("mcs_doid", "模型计算序列号"),
                ("mcs_initigrant", "模型计算额度USD"),
                ("mcs_applygenre", "信保额度类型"),
                ("mcs_quotasum", "信保额度USD"),
                ("mcs_quotabalance", "信保余额USD"),
                ("mcs_sellergrant", "厂端授信额度USD"),
                ("mcs_sellerbalance", "厂端授信余额USD"),
                ("mcs_typename", "产品类型"),
                ("mcs_contractamt", "意向合同金额"),
                ("mcs_paymode", "支付方式"),
                ("mcs_payterm", "账期（天）"),
            };
            var tab2 = new List<(string, string)>
            {
                ("mcs_tobegrant", "调整厂端授信额度"),
                ("mcs_tobebalance", "调整后厂端授信余额"),
                ("mcs_remark", "调整原因"),
            };
            var tab3 = new List<(string, string)>
            {
                ("createdby", "申请人"),
                ("createdon", "申请日期"),
                ("mcs_orgid", "申请组织"),
                ("mcs_orgname", "申请组织名称"),
                ("mcs_buid", "事业部"),
                ("mcs_buname", "事业部名称"),
                ("mcs_regionid", "大区"),
                ("mcs_regionname", "大区名称"),
                ("mcs_countryregionid", "国区"),
                ("mcs_countryregionname", "国区名称"),
                ("mcs_countrycode", "国家"),
                ("mcs_countryname", "国家名称"),
            };
            var tab4 = new List<(string, string)>
            {
                ("mcs_bppstatus", "审批状态"),
                ("mcs_bppstatuscode", "BPP审批状态"),
                ("mcs_bppapprover", "当前审批人"),
                ("mcs_bppid", "BPP工作流ID"),
                ("mcs_bpperrormsg", "BPP错误信息"),
                ("mcs_bpprejectreason", "BPP驳回原因"),
                ("mcs_approvedate", "BPP审批完成日期"),
            };

            string sectionId5 = Guid.NewGuid().ToString("B");
            string cellId5 = Guid.NewGuid().ToString("B");
            string tabId5 = Guid.NewGuid().ToString("B");
            string attachmentSection = $"<section showlabel=\"true\" showbar=\"true\" IsUserDefined=\"1\" id=\"{sectionId5}\" layout=\"varwidth\" celllabelalignment=\"Left\" celllabelposition=\"Left\" columns=\"11\" labelwidth=\"115\"><labels><label description=\"附件\" languagecode=\"2052\" /></labels><rows><row><cell id=\"{cellId5}\" colspan=\"2\" rowspan=\"12\"><labels><label description=\"附件\" languagecode=\"2052\" /></labels><control id=\"mcs_fca_quotaapp_uploader\" classid=\"{{9FDF5F91-88E1-47cd-9CAE-4C7186A64CBD}}\"><parameters><Url>mcs_/CommonCore/Html/Uploader.html</Url><PassParameters>true</PassParameters></parameters></control></cell></row></rows></section>";
            string tab5 = $"<tab verticallayout=\"true\" id=\"{tabId5}\" name=\"attachments\" showlabel=\"true\"><labels><label description=\"附件\" languagecode=\"2052\" /></labels><columns><column width=\"100%\"><sections>{attachmentSection}</sections></column></columns></tab>";

            string tabsXml = $"<tabs>{TabXml("常规/意向合同", "常规/意向合同", tab1)}{TabXml("调整额度", "调整额度", tab2)}{TabXml("申请组织", "申请组织", tab3)}{TabXml("审批(BPP)", "审批(BPP)", tab4)}{tab5}</tabs>";
            manager.ReplaceFormTabs(entityName, tabsXml);
        }
        else if (entityName == "mcs_fca_records")
        {
            var groups = new Dictionary<string, List<(string, string)>>
            {
                ["台账信息"] = new List<(string, string)>
                {
                    ("mcs_accountid", "客户编码"),
                    ("mcs_custname", "客户名称"),
                    ("mcs_contractid", "合同编码"),
                    ("mcs_orderid", "订单编码"),
                    ("mcs_proccess", "流程环节"),
                    ("mcs_adjust", "额度调整动作"),
                    ("mcs_sellergrant", "授信限额USD"),
                    ("mcs_asisbalance", "现有授信余额USD"),
                    ("mcs_adjustamt", "调整金额USD"),
                    ("mcs_tobebalance", "调整后授信余额USD"),
                    ("modifiedby", "记录修改人"),
                    ("modifiedon", "记录修改时间"),
                },
            };
            var picklistFields = new HashSet<string> { "mcs_proccess", "mcs_adjust" };
            manager.RearrangeForm(entityName, groups, null, null, picklistFields);
        }
        else if (entityName == "mcs_credit_record")
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
        else if (entityName == "mcs_trade_stpayterm")
        {
            var groups = new Dictionary<string, List<(string fieldName, string displayName)>>
            {
                ["基本信息"] = new List<(string, string)>
                {
                    ("mcs_trade_stpaytermname", "标准条件编码"),
                    ("mcs_status", "生效状态"),
                },
                ["组织信息"] = new List<(string, string)>
                {
                    ("mcs_businessunit", "事业部"),
                    ("mcs_buid", "事业部编码"),
                    ("mcs_buname", "事业部名称"),
                    ("mcs_subsidiary", "大区/子公司"),
                    ("mcs_subid", "大区/子公司编码"),
                    ("mcs_subname", "大区/子公司名称"),
                    ("mcs_nation", "国家"),
                    ("mcs_countrycode", "国家编码"),
                    ("mcs_countryname", "国家名称"),
                },
                ["产品分类"] = new List<(string, string)>
                {
                    ("mcs_trade_pttype", "成交条件产品分类"),
                    ("mcs_typeid", "产品分类编码"),
                    ("mcs_typename", "产品分类名称"),
                },
                ["客户等级"] = new List<(string, string)>
                {
                    ("mcs_buyergrade", "客户分类代码"),
                    ("mcs_creditgrade", "客户等级"),
                },
                ["付款条件"] = new List<(string, string)>
                {
                    ("mcs_downpay", "首付比例"),
                    ("mcs_payterm", "账期"),
                    ("mcs_payfreq", "付款频次"),
                },
            };
            var lookupFields = new HashSet<string> { "mcs_businessunit", "mcs_subsidiary", "mcs_nation", "mcs_trade_pttype" };
            var picklistFields = new HashSet<string> { "mcs_status", "mcs_creditgrade" };
            manager.RearrangeForm(entityName, groups, lookupFields, null, picklistFields);
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
            case "mcs_fsm_data":
                // 禅道 #2169②（#2150 后续）：Active 融资管理视图补「合同编号文本」列（多选记录旧单选合同字段为空）
                fields = new Dictionary<string, string>
                {
                    { "mcs_contract_nos", "合同编号文本" }
                };
                break;

            case "mcs_trade_pttype":
                fields = new Dictionary<string, string>
                {
                    { "mcs_typeid", "产品分类编码" },
                    { "mcs_trade_pttypename", "成交条件产品分类名称" },
                    { "mcs_typenameen", "产品分类英文名称" }
                };
                break;

            case "mcs_trade_ptgrouptype":
                fields = new Dictionary<string, string>
                {
                    { "mcs_productlineid", "产品线" },
                    { "mcs_groupid", "产品线编码" },
                    { "mcs_groupname", "产品线名称" },
                    { "mcs_trade_pttypeid", "成交条件产品分类" },
                    { "mcs_typeid", "产品分类编码" },
                    { "mcs_typename", "产品分类名称" }
                };
                break;

            case "mcs_trade_stpayterm":
                fields = new Dictionary<string, string>
                {
                    { "mcs_trade_stpaytermname", "标准条件编码" },
                    { "mcs_businessunit", "事业部" },
                    { "mcs_buid", "事业部编码" },
                    { "mcs_buname", "事业部名称" },
                    { "mcs_subsidiary", "大区/子公司" },
                    { "mcs_subid", "大区/子公司编码" },
                    { "mcs_subname", "大区/子公司名称" },
                    { "mcs_nation", "国家" },
                    { "mcs_countrycode", "国家代码" },
                    { "mcs_countryname", "国家名称" },
                    { "mcs_trade_pttype", "成交条件产品分类" },
                    { "mcs_typeid", "产品分类编码" },
                    { "mcs_typename", "产品分类名称" },
                    { "mcs_buyergrade", "客户分类" },
                    { "mcs_creditgrade", "客户等级" },
                    { "mcs_downpay", "首付款比例" },
                    { "mcs_payterm", "账期（天）" },
                    { "mcs_payfreq", "付款频次（天）" },
                    { "mcs_status", "生效状态" }
                };
                break;

            case "mcs_fca_mdlversion":
                fields = new Dictionary<string, string>
                {
                    { "mcs_versionid", "模型版本" },
                    { "mcs_isactive", "是否生效" },
                    { "mcs_modeldesc", "模型描述" },
                    { "mcs_validfrom", "开始日期" },
                    { "mcs_validend", "结束日期" },
                    { "modifiedby", "编辑人" },
                    { "modifiedon", "编辑时间" }
                };
                break;

            case "mcs_fca_mdlconfig":
                fields = new Dictionary<string, string>
                {
                    { "mcs_argid", "厂端授信参数编码" },
                    { "mcs_buyergrade", "客户分类" },
                    { "mcs_creditgrade", "客户等级" },
                    { "mcs_adjust1", "系数1" },
                    { "mcs_adjust2", "系数2" },
                    { "mcs_adjust3", "系数3" },
                    { "mcs_aggfunc", "聚合方法" },
                    { "mcs_countryname", "历史基准额度" },
                    { "modifiedby", "编辑人" },
                    { "modifiedon", "编辑时间" }
                };
                break;

            case "mcs_fca_proc":
                fields = new Dictionary<string, string>
                {
                    { "mcs_doid", "模型计算序列号" },
                    { "mcs_accountid", "客户编码" },
                    { "mcs_custname", "客户名称" },
                    { "mcs_status", "计算状态" },
                    { "mcs_validfrom", "计算日期" },
                    { "mcs_versionid", "模型版本" },
                    { "mcs_modeldesc", "模型描述" },
                    { "mcs_initigrant", "调整模型额度USD" },
                    { "modifiedby", "编辑人" },
                    { "modifiedon", "编辑时间" }
                };
                break;

            case "mcs_fca_quotaapp":
                fields = new Dictionary<string, string>
                {
                    { "mcs_grantid", "申请编号" },
                    { "mcs_accountid", "客户编码" },
                    { "mcs_custname", "客户名称" },
                    { "mcs_creditgrade", "客户等级" },
                    { "mcs_bppstatus", "审批状态" },
                    { "mcs_tobegrant", "调整厂端授信额度" },
                    { "mcs_tobebalance", "调整后厂端授信余额" },
                    { "mcs_sellergrant", "厂端授信额度USD" },
                    { "mcs_sellerbalance", "厂端授信余额USD" },
                    { "createdby", "申请人" },
                    { "createdon", "申请日期" }
                };
                break;

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
                "mcs_active", "mcs_creditscore"
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
        Console.WriteLine($"  信用等级: N/A (mcs_credit_record 已移除该字段，请从客户主数据查看)");
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

    static void QuerySystemConfigurations(IOrganizationService service, string? exactName = null)
    {
        var names = exactName != null
            ? new[] { exactName }
            : new[] { "BPP_WorkFlowTemplateCode", "D365BaseUrl", "Bpp_ApprovalFlowBaseUrl", "UploadFileTypeMapping" };
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
            ColumnSet = new ColumnSet("mcs_itemcode", "mcs_itemname", "mcs_itemvalue1", "mcs_itemtxtvalue1", "mcs_itemintvalue1",
                "mcs_itemvalue2", "mcs_itemtxtvalue2", "mcs_itemintvalue2", "mcs_credititem_value", "mcs_datatype", "mcs_scorevalue", "createdon", "modifiedon"),
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
            var value2 = tag.GetAttributeValue<string>("mcs_itemvalue2") ?? "N/A";
            var txtvalue2 = tag.GetAttributeValue<string>("mcs_itemtxtvalue2") ?? "N/A";
            var intvalue2 = tag.GetAttributeValue<decimal?>("mcs_itemintvalue2")?.ToString() ?? "N/A";
            var lookup2 = tag.GetAttributeValue<EntityReference>("mcs_credititem_value")?.Name ?? "N/A";
            var dataType = tag.GetAttributeValue<OptionSetValue>("mcs_datatype")?.Value.ToString() ?? "N/A";
            var scoreValue = tag.GetAttributeValue<int?>("mcs_scorevalue")?.ToString() ?? "N/A";
            var createdOn = tag.GetAttributeValue<DateTime>("createdon").ToString("yyyy-MM-dd HH:mm:ss");
            var modifiedOn = tag.GetAttributeValue<DateTime>("modifiedon").ToString("yyyy-MM-dd HH:mm:ss");
            Console.WriteLine($"  [{itemCode}] {itemName} (dt={dataType}, score={scoreValue}, cre={createdOn}, mod={modifiedOn}): value1={value1}, txt1={txtvalue}, int1={intvalue} | value2={value2}, txt2={txtvalue2}, int2={intvalue2}, lookup2={lookup2}");
        }
    }

    static void QueryCreditTrace(IOrganizationService service, string scoreId)
    {
        var q = new QueryExpression("plugintracelog")
        {
            ColumnSet = new ColumnSet("createdon", "messageblock", "exceptiondetails"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("typename", ConditionOperator.Equal, "SanyD365.D365Extension.Sales.Plugins.CreditScore.CreditScoreCalculationPlugin")
                }
            },
            Orders = { new OrderExpression("createdon", OrderType.Descending) },
            TopCount = 20
        };
        var records = service.RetrieveMultiple(q).Entities;
        var filtered = records.Where(r => 
        {
            var mb = r.GetAttributeValue<string>("messageblock") ?? "";
            return mb.Contains(scoreId);
        }).ToList();
        Console.WriteLine($"=== Plugin Trace for {scoreId} (共 {filtered.Count} 条) ===");
        foreach (var r in filtered.Take(5))
        {
            Console.WriteLine($"--- {r.GetAttributeValue<DateTime>("createdon")} ---");
            Console.WriteLine(r.GetAttributeValue<string>("messageblock") ?? "(空)");
            var ex = r.GetAttributeValue<string>("exceptiondetails");
            if (!string.IsNullOrEmpty(ex))
            {
                Console.WriteLine($"EXCEPTION: {ex}");
            }
        }
    }

    static void FixReviewFields(IOrganizationService service, string scoreId)
    {
        var recordQuery = new QueryExpression("mcs_credit_record")
        {
            ColumnSet = new ColumnSet("mcs_credit_recordid"),
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
            ColumnSet = new ColumnSet("mcs_customer_tagid", "mcs_itemcode", "mcs_itemintvalue1", "mcs_itemvalue1", "mcs_itemtxtvalue1", "mcs_itemintvalue2", "mcs_itemvalue2", "mcs_itemtxtvalue2", "mcs_credititem_value", "mcs_datatype"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("mcs_credit_record", ConditionOperator.Equal, recordId) }
            }
        };

        var tags = service.RetrieveMultiple(tagQuery);
        int fixedCount = 0;
        foreach (var tag in tags.Entities)
        {
            var itemCode = tag.GetAttributeValue<string>("mcs_itemcode") ?? "";
            int dataType = tag.GetAttributeValue<OptionSetValue>("mcs_datatype")?.Value ?? 0;
            var update = new Entity("mcs_customer_tag") { Id = tag.Id };
            bool needUpdate = false;

            if (dataType == 1) // 定量
            {
                var int1 = tag.GetAttributeValue<decimal?>("mcs_itemintvalue1");
                var value1 = tag.GetAttributeValue<string>("mcs_itemvalue1") ?? "N/A";
                var int2 = tag.GetAttributeValue<decimal?>("mcs_itemintvalue2");
                var value2 = tag.GetAttributeValue<string>("mcs_itemvalue2") ?? "N/A";
                if (int1.HasValue && (int2 == null || value2 == "N/A"))
                {
                    update["mcs_itemintvalue2"] = int1.Value;
                    update["mcs_itemvalue2"] = int1.Value.ToString("F2");
                    needUpdate = true;
                }
            }
            else // 定性
            {
                var txt1 = tag.GetAttributeValue<string>("mcs_itemtxtvalue1");
                var value1 = tag.GetAttributeValue<string>("mcs_itemvalue1");
                var txt2 = tag.GetAttributeValue<string>("mcs_itemtxtvalue2");
                var value2 = tag.GetAttributeValue<string>("mcs_itemvalue2");
                var lookup2 = tag.GetAttributeValue<EntityReference>("mcs_credititem_value");
                if (!string.IsNullOrEmpty(txt1) && (string.IsNullOrEmpty(txt2) || string.IsNullOrEmpty(value2)))
                {
                    update["mcs_itemtxtvalue2"] = txt1;
                    update["mcs_itemvalue2"] = txt1;
                    needUpdate = true;
                }
                if (lookup2 != null && string.IsNullOrEmpty(txt2))
                {
                    update["mcs_itemtxtvalue2"] = lookup2.Name;
                    update["mcs_itemvalue2"] = lookup2.Name;
                    needUpdate = true;
                }
            }

            if (needUpdate)
            {
                service.Update(update);
                fixedCount++;
                Console.WriteLine($"  已修复: [{itemCode}]");
            }
        }
        Console.WriteLine($"=== 共修复 {fixedCount} 条标签的复核字段 ===");
    }

    static void QueryScoringCard(IOrganizationService service, string scoreId)
    {
        var recordQuery = new QueryExpression("mcs_credit_record")
        {
            ColumnSet = new ColumnSet("mcs_credit_recordid", "mcs_scoreid", "mcs_accountid"),
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

        var creditRecord = records.Entities[0];
        var accountRef = creditRecord.GetAttributeValue<EntityReference>("mcs_accountid");
        if (accountRef == null)
        {
            Console.WriteLine("评估记录未关联客户");
            return;
        }

        var account = service.Retrieve("account", accountRef.Id, new ColumnSet("mcs_customermasterdata"));
        int accountCategory = 0, accountLevel = 0, accountType = 0;
        if (account.Contains("mcs_customermasterdata") && account["mcs_customermasterdata"] is EntityReference cmRef)
        {
            var cmd = service.Retrieve("mcs_customermasterdata", cmRef.Id,
                new ColumnSet("mcs_accountcategory", "mcs_accountlevel", "mcs_accounttype"));
            accountCategory = cmd.GetAttributeValue<OptionSetValue>("mcs_accountcategory")?.Value ?? 0;
            accountLevel = cmd.GetAttributeValue<OptionSetValue>("mcs_accountlevel")?.Value ?? 0;
            accountType = cmd.GetAttributeValue<OptionSetValue>("mcs_accounttype")?.Value ?? 0;
        }
        else
        {
            var accountFallback = service.Retrieve("account", accountRef.Id,
                new ColumnSet("mcs_accountcategory", "mcs_accountlevel", "mcs_accounttype"));
            accountCategory = accountFallback.GetAttributeValue<OptionSetValue>("mcs_accountcategory")?.Value ?? 0;
            accountLevel = accountFallback.GetAttributeValue<OptionSetValue>("mcs_accountlevel")?.Value ?? 0;
            accountType = accountFallback.GetAttributeValue<OptionSetValue>("mcs_accounttype")?.Value ?? 0;
        }

        var orderQuery = new QueryExpression("salesorder")
        {
            ColumnSet = new ColumnSet("salesorderid"),
            Criteria = new FilterExpression { Conditions = { new ConditionExpression("customerid", ConditionOperator.Equal, accountRef.Id) } },
            TopCount = 1
        };
        var orders = service.RetrieveMultiple(orderQuery);
        bool isOldCustomer = orders.Entities.Count > 0;
        bool isDealer = (accountCategory == 10 || accountCategory == 90);
        bool isBigAccount = (accountLevel == 4 || accountLevel == 3);
        int categoryId = 0;
        if (accountType == 1) categoryId = 5;
        else if (isDealer) categoryId = isOldCustomer ? 6 : 7;
        else if (isOldCustomer) categoryId = isBigAccount ? 1 : 3;
        else categoryId = isBigAccount ? 2 : 4;

        Console.WriteLine($"=== 评分卡配置 for {scoreId} ===");
        Console.WriteLine($"客户属性: category={accountCategory}, level={accountLevel}, type={accountType}, 老客户={isOldCustomer}");
        Console.WriteLine($"匹配评分卡类型: {categoryId}");

        if (categoryId == 0)
        {
            Console.WriteLine("无法匹配评分卡类型");
            return;
        }

        var query = new QueryExpression("mcs_credit_scoringcard")
        {
            ColumnSet = new ColumnSet("mcs_itemid", "mcs_itemname", "mcs_datatype", "mcs_credititem", "mcs_typeid", "mcs_cardname"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("mcs_categoryid", ConditionOperator.Equal, categoryId) }
            },
            Orders = { new OrderExpression("mcs_itemid", OrderType.Ascending) }
        };

        var results = service.RetrieveMultiple(query);
        Console.WriteLine($"评分卡配置项数量: {results.Entities.Count}");
        foreach (var record in results.Entities)
        {
            var itemId = record.GetAttributeValue<string>("mcs_itemid") ?? "";
            var itemName = record.GetAttributeValue<string>("mcs_itemname") ?? "";
            var dataType = record.GetAttributeValue<OptionSetValue>("mcs_datatype")?.Value ?? 0;
            var typeIdValue = record.GetAttributeValue<object>("mcs_typeid");
            string typeId = typeIdValue switch
            {
                OptionSetValue osv => osv.Value.ToString(),
                string s => s,
                _ => typeIdValue?.ToString() ?? ""
            };
            var cardName = record.GetAttributeValue<string>("mcs_cardname") ?? "";
            var creditItem = record.GetAttributeValue<EntityReference>("mcs_credititem");
            var creditItemName = creditItem != null ? $"[{creditItem.Name}]" : "";
            Console.WriteLine($"  [{itemId}] {itemName} | datatype={dataType} | typeid={typeId} | card={cardName} {creditItemName}");
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
            TopCount = 100
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
                Console.WriteLine($"  trace={(msg.Length > 50000 ? msg[..50000] + "..." : msg)}");
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
            else if (response.AttributeMetadata is MultiSelectPicklistAttributeMetadata multiPicklist)
            {
                Console.WriteLine($"=== {entityName}.{fieldName} 多选选项集 ===");
                Console.WriteLine($"选项集名称: {multiPicklist.OptionSet.Name}");
                foreach (var option in multiPicklist.OptionSet.Options)
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

    static void CreateTradeStPayTermTestData(ServiceClient service)
    {
        Console.WriteLine(">>> 创建成交条件样板库测试数据");

        try
        {
            // 创建记录
            var record = new Entity("mcs_trade_stpayterm");
            record["mcs_buid"] = "BU-1018";
            record["mcs_subid"] = "SUB-001";
            record["mcs_countrycode"] = "CN";
            record["mcs_typeid"] = "03";
            record["mcs_buyergrade"] = new Microsoft.Xrm.Sdk.OptionSetValueCollection(
                new[] { new Microsoft.Xrm.Sdk.OptionSetValue(100000003) }); // C
            record["mcs_downpay"] = 0.3m;
            record["mcs_payterm"] = 30;
            record["mcs_payfreq"] = 30;

            // 禅道 #1151：提交审批共享 Plugin 要求记录必须有事业部 Lookup，否则 0->1 被拦截
            var buQuery = new QueryExpression("mcs_bu")
            {
                ColumnSet = new ColumnSet("mcs_name"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("mcs_code", ConditionOperator.Equal, "BU-1018") }
                }
            };
            var buResult = service.RetrieveMultiple(buQuery);
            if (buResult.Entities.Count > 0)
            {
                record["mcs_businessunit"] = buResult.Entities[0].ToEntityReference();
            }

            var recordId = service.Create(record);
            Console.WriteLine($"  ✅ 已创建记录: {recordId}");

            // 2026-08-14 Bug #1834：取消审批功能，新增记录默认生效，不再执行申请/审批
            // // 申请 (0->1)
            // var applyUpdate = new Entity("mcs_trade_stpayterm", recordId);
            // applyUpdate["mcs_status"] = new OptionSetValue(1);
            // service.Update(applyUpdate);
            // Console.WriteLine("  ✅ 申请成功 (0->1)");
            //
            // // 审批 (1->2)
            // var approveUpdate = new Entity("mcs_trade_stpayterm", recordId);
            // approveUpdate["mcs_status"] = new OptionSetValue(2);
            // service.Update(approveUpdate);
            // Console.WriteLine("  ✅ 审批成功 (1->2)，记录已生效");
            Console.WriteLine("  ⏸️ 审批流程已取消（Bug #1834），记录创建后即为生效态");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ 创建失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 测试成交条件样板库提交审批按事业部共享（禅道 #1151）
    /// 场景：创建记录 -> 提交(0->1) -> 验证 POA 共享给事业部 BU 团队 -> 重复提交幂等 -> 无事业部拦截
    /// </summary>
    /// <summary>
    /// 查询 POA 表，输出记录共享给指定 Principal（用户/团队）的权限掩码（禅道 #1764 验证用，通用只读）
    /// </summary>
    static void CheckRecordShare(ServiceClient service, Guid recordId, Guid principalId)
    {
        var sharing = new RecordSharingService(service);
        bool has = sharing.HasShare(recordId, principalId, out int mask);
        Console.WriteLine(has
            ? $"✅ 已共享: record={recordId}, principal={principalId}, mask={mask}（1=Read,2=Write,3=Read+Write）"
            : $"❌ 未共享: record={recordId}, principal={principalId}（POA 无记录）");
    }

    // 2026-08-14 Bug #1834：取消审批功能，审批共享测试已停用
    static void TestTradeStPayTermShare(ServiceClient service)
    {
        Console.WriteLine(">>> 测试成交条件样板库提交审批共享（禅道 #1151）");
        Console.WriteLine("  ⏸️ 审批流程已取消（Bug #1834），本测试跳过");
        // var sharing = new RecordSharingService(service);
        // Guid recordId = Guid.Empty;
        // Guid noBuRecordId = Guid.Empty;
        // int passed = 0, failed = 0;
        //
        // try
        // {
        //     // 1. 查找带 BU 团队的事业部（BU-1017 工车海外营销公司）
        //     var buQuery = new QueryExpression("mcs_bu")
        //     {
        //         ColumnSet = new ColumnSet("mcs_name", "mcs_buteamid"),
        //         Criteria = new FilterExpression
        //         {
        //             Conditions = { new ConditionExpression("mcs_code", ConditionOperator.Equal, "BU-1017") }
        //         }
        //     };
        //     var buResult = service.RetrieveMultiple(buQuery);
        //     if (buResult.Entities.Count == 0)
        //     {
        //         Console.WriteLine("  ❌ 未找到事业部 BU-1017");
        //         return;
        //     }
        //     var bu = buResult.Entities[0];
        //     var teamRef = bu.GetAttributeValue<EntityReference>("mcs_buteamid");
        //     if (teamRef == null)
        //     {
        //         Console.WriteLine("  ❌ 事业部 BU-1017 未维护 BU 团队");
        //         return;
        //     }
        //     Console.WriteLine($"  测试事业部: {bu.GetAttributeValue<string>("mcs_name")}，BU团队: {teamRef.Name}");
        //
        //     // 2. 创建带事业部的测试记录（mcs_buid 用唯一测试编码避开重复校验）
        //     var record = new Entity("mcs_trade_stpayterm");
        //     record["mcs_businessunit"] = bu.ToEntityReference();
        //     record["mcs_buid"] = "BU-TEST-SHARE";
        //     record["mcs_buname"] = "共享测试";
        //     record["mcs_downpay"] = 0.3m;
        //     record["mcs_payterm"] = 30;
        //     record["mcs_payfreq"] = 30;
        //     recordId = service.Create(record);
        //     Console.WriteLine($"  ✅ 已创建测试记录: {recordId}");
        //
        //     // 3. 提交 0->1
        //     service.Update(new Entity("mcs_trade_stpayterm", recordId) { ["mcs_status"] = new OptionSetValue(1) });
        //     Console.WriteLine("  ✅ 提交成功 (0->1)");
        //
        //     // 4. 验证 POA 共享（Read=1 + Write=2）
        //     if (sharing.HasShare(recordId, teamRef.Id, out int mask) && (mask & 1) == 1 && (mask & 2) == 2)
        //     {
        //         Console.WriteLine($"  ✅ 共享验证通过（AccessRightsMask={mask}）");
        //         passed++;
        //     }
        //     else
        //     {
        //         Console.WriteLine($"  ❌ 共享验证失败（mask={mask}）");
        //         failed++;
        //     }
        //
        //     // 5. 幂等验证：1->0->1 重复提交不报错，共享仍在
        //     service.Update(new Entity("mcs_trade_stpayterm", recordId) { ["mcs_status"] = new OptionSetValue(0) });
        //     service.Update(new Entity("mcs_trade_stpayterm", recordId) { ["mcs_status"] = new OptionSetValue(1) });
        //     if (sharing.HasShare(recordId, teamRef.Id, out int mask2) && (mask2 & 1) == 1 && (mask2 & 2) == 2)
        //     {
        //         Console.WriteLine("  ✅ 重复提交幂等验证通过");
        //         passed++;
        //     }
        //     else
        //     {
        //         Console.WriteLine("  ❌ 重复提交后共享异常");
        //         failed++;
        //     }
        //
        //     // 6. 无事业部拦截验证（mcs_buid 换唯一编码避开重复校验）
        //     var record2 = new Entity("mcs_trade_stpayterm");
        //     record2["mcs_buid"] = "BU-TEST-SHARE2";
        //     record2["mcs_buname"] = "共享测试-无事业部";
        //     noBuRecordId = service.Create(record2);
        //     try
        //     {
        //         service.Update(new Entity("mcs_trade_stpayterm", noBuRecordId) { ["mcs_status"] = new OptionSetValue(1) });
        //         Console.WriteLine("  ❌ 无事业部提交未被拦截（异常）");
        //         failed++;
        //     }
        //     catch (Exception ex)
        //     {
        //         Console.WriteLine($"  ✅ 无事业部提交已被拦截: {ex.Message}");
        //         passed++;
        //     }
        //
        //     Console.WriteLine($">>> 测试结果: 通过 {passed}，失败 {failed}");
        // }
        // finally
        // {
        //     // 清理测试数据
        //     foreach (var id in new[] { recordId, noBuRecordId })
        //     {
        //         if (id != Guid.Empty)
        //         {
        //             try { service.Delete("mcs_trade_stpayterm", id); } catch { }
        //         }
        //     }
        //     Console.WriteLine("  🧹 测试数据已清理");
        // }
    }

    /// <summary>
    /// 存量待审批（mcs_status=1）记录补共享给各自事业部的 BU 团队（禅道 #1151）
    /// </summary>
    /// <summary>
    /// 禅道 #1151 环境配套：为事业部创建/复用 BuTeam（<事业部名>-BuTeam），
    /// 挂到 mcs_bu.mcs_buteamid，并把指定用户加入团队成员。
    /// 参照既有 BuTeam 配置：teamtype=0（Owner），administratorid=AdminTest1 T。
    /// </summary>
    static void SetupBuTeam(ServiceClient service, string buRecordIdText, string userDomainName)
    {
        Console.WriteLine(">>> 事业部 BU 团队配置（禅道 #1151 环境配套）");

        if (!Guid.TryParse(buRecordIdText, out var buId))
        {
            Console.WriteLine("  ❌ mcs_bu 记录 ID 格式不正确");
            return;
        }

        // 1. 读取事业部
        var bu = service.Retrieve("mcs_bu", buId, new ColumnSet("mcs_name", "mcs_buteamid"));
        var buName = bu.GetAttributeValue<string>("mcs_name");
        Console.WriteLine($"  事业部: {buName} ({buId})");

        // 2. 查找/创建 BuTeam
        var teamName = buName + "-BuTeam";
        var teamQuery = new QueryExpression("team")
        {
            ColumnSet = new ColumnSet("name"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("name", ConditionOperator.Equal, teamName) }
            }
        };
        var teamResult = service.RetrieveMultiple(teamQuery);
        Guid teamId;
        if (teamResult.Entities.Count > 0)
        {
            teamId = teamResult.Entities[0].Id;
            Console.WriteLine($"  ⏭️ BuTeam 已存在: {teamName} ({teamId})");
        }
        else
        {
            var team = new Entity("team");
            team["name"] = teamName;
            team["teamtype"] = new OptionSetValue(0); // Owner
            // 参照其他 BuTeam：管理员统一为 AdminTest1 T
            team["administratorid"] = new EntityReference("systemuser", new Guid("27538266-6707-ee11-8f6e-000d3a08d197"));
            // 挂到同名 D365 业务部门（若存在）
            var d365BuQuery = new QueryExpression("businessunit")
            {
                ColumnSet = new ColumnSet("name"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("name", ConditionOperator.Equal, buName) }
                }
            };
            var d365Bu = service.RetrieveMultiple(d365BuQuery);
            if (d365Bu.Entities.Count > 0)
            {
                team["businessunitid"] = d365Bu.Entities[0].ToEntityReference();
            }
            teamId = service.Create(team);
            Console.WriteLine($"  ✅ 已创建 BuTeam: {teamName} ({teamId})");
        }

        // 3. 挂到 mcs_bu.mcs_buteamid（幂等）
        var currentTeam = bu.GetAttributeValue<EntityReference>("mcs_buteamid");
        if (currentTeam != null && currentTeam.Id == teamId)
        {
            Console.WriteLine("  ⏭️ mcs_bu.mcs_buteamid 已是该团队，跳过");
        }
        else
        {
            var buUpdate = new Entity("mcs_bu", buId);
            buUpdate["mcs_buteamid"] = new EntityReference("team", teamId);
            service.Update(buUpdate);
            Console.WriteLine($"  ✅ 已维护 mcs_bu.mcs_buteamid = {teamName}");
        }

        // 4. 把用户加入团队（幂等：已在团队则跳过）
        var userQuery = new QueryExpression("systemuser")
        {
            ColumnSet = new ColumnSet("fullname"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("domainname", ConditionOperator.Equal, userDomainName) }
            }
        };
        var userResult = service.RetrieveMultiple(userQuery);
        if (userResult.Entities.Count == 0)
        {
            Console.WriteLine($"  ❌ 未找到用户: {userDomainName}");
            return;
        }
        var user = userResult.Entities[0];
        Console.WriteLine($"  用户: {user.GetAttributeValue<string>("fullname")} ({user.Id})");

        var membershipQuery = new QueryExpression("teammembership")
        {
            ColumnSet = new ColumnSet("teammembershipid"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("teamid", ConditionOperator.Equal, teamId),
                    new ConditionExpression("systemuserid", ConditionOperator.Equal, user.Id)
                }
            }
        };
        if (service.RetrieveMultiple(membershipQuery).Entities.Count > 0)
        {
            Console.WriteLine("  ⏭️ 用户已是团队成员，跳过");
        }
        else
        {
            service.Associate("team", teamId, new Relationship("teammembership_association"),
                new EntityReferenceCollection { new EntityReference("systemuser", user.Id) });
            Console.WriteLine("  ✅ 已把用户加入团队");
        }

        Console.WriteLine(">>> 完成");
    }

    // 2026-08-14 Bug #1834：取消审批功能，存量待审批记录补共享已停用
    static void ShareTradeStPayTermPending(ServiceClient service)
    {
        Console.WriteLine(">>> 存量待审批记录补共享（禅道 #1151）");
        Console.WriteLine("  ⏸️ 审批流程已取消（Bug #1834），无需补共享");
        // var sharing = new RecordSharingService(service);
        // var filter = new FilterExpression
        // {
        //     Conditions = { new ConditionExpression("mcs_status", ConditionOperator.Equal, 1) }
        // };
        //
        // var (success, skipped, details) = sharing.ShareRecordsToBuTeam("mcs_trade_stpayterm", "mcs_businessunit", filter);
        // foreach (var d in details)
        // {
        //     Console.WriteLine(d);
        // }
        // Console.WriteLine($">>> 完成: 成功 {success}，跳过 {skipped}");
    }

    static void RemoveTradeStPayTermGradeFields(ServiceClient service, EntityManager manager)
    {
        const string entityName = "mcs_trade_stpayterm";
        const string buyerGradeField = "mcs_buyergrade";
        const string creditGradeField = "mcs_creditgrade";

        Console.WriteLine($">>> 移除并删除 {entityName} 的 {buyerGradeField}/{creditGradeField} 字段");

        bool FieldExists(string fieldName)
        {
            try
            {
                var req = new RetrieveAttributeRequest
                {
                    EntityLogicalName = entityName,
                    LogicalName = fieldName,
                    RetrieveAsIfPublished = true
                };
                service.Execute(req);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // 1. 从视图移除字段
        Console.WriteLine($">>> 步骤 1/4: 从所有视图移除字段");
        manager.RemoveFieldFromViews(entityName, buyerGradeField);
        manager.RemoveFieldFromViews(entityName, creditGradeField);

        // 2. 从表单移除字段
        Console.WriteLine($">>> 步骤 2/4: 从主窗体移除字段");
        manager.RemoveFieldsFromForm(entityName, buyerGradeField, creditGradeField);

        // 3. 发布实体（使移除生效）
        Console.WriteLine($">>> 步骤 3/4: 发布实体 {entityName}");
        manager.PublishEntity(entityName);

        // 4. 删除老字段
        Console.WriteLine($">>> 步骤 4/4: 删除字段");
        if (FieldExists(buyerGradeField))
        {
            manager.DeleteField(entityName, buyerGradeField);
        }
        else
        {
            Console.WriteLine($"  ⊘ {buyerGradeField} 已不存在，跳过删除");
        }
        if (FieldExists(creditGradeField))
        {
            manager.DeleteField(entityName, creditGradeField);
        }
        else
        {
            Console.WriteLine($"  ⊘ {creditGradeField} 已不存在，跳过删除");
        }

        Console.WriteLine($">>> 字段已移除并删除，现在可以手动新建多选选项集字段。");
    }

    static void FixTradeStPayTermForm(EntityManager manager)
    {
        const string entityName = "mcs_trade_stpayterm";
        Console.WriteLine($">>> 修复 {entityName} 主窗体字段...");

        // 先添加非 Lookup 文本/数字/选项集字段（UpdateMainForm 使用文本 classid，对这些字段有效）
        var textAndNumberFields = new Dictionary<string, string>
        {
            { "mcs_trade_stpaytermname", "标准条件编码" },
            { "mcs_status", "生效状态" },
            { "mcs_buid", "事业部编码" },
            { "mcs_buname", "事业部名称" },
            { "mcs_subid", "子公司编码" },
            { "mcs_subname", "子公司名称" },
            { "mcs_countrycode", "国家代码" },
            { "mcs_countryname", "国家名称" },
            { "mcs_typeid", "产品分类编码" },
            { "mcs_typename", "产品分类名称" },
            { "mcs_downpay", "首付款比例" },
            { "mcs_payterm", "账期（天）" },
            { "mcs_payfreq", "付款频次（天）" }
        };
        manager.UpdateMainForm(entityName, textAndNumberFields);

        // Lookup 字段和多选选项集字段需要在 D365 UI 中手动调整，
        // 因为 UpdateMainForm 使用统一 classid，对复杂控件不适用。
        Console.WriteLine(">>> 提示：以下字段请通过 D365 窗体设计器手动检查/调整：");
        Console.WriteLine("  - mcs_businessunit (事业部)");
        Console.WriteLine("  - mcs_subsidiary (大区/子公司)");
        Console.WriteLine("  - mcs_nation (国家)");
        Console.WriteLine("  - mcs_trade_pttype (成交条件产品分类)");
        Console.WriteLine("  - mcs_buyergrade (客户分类，多选选项集)");
        Console.WriteLine("  - mcs_creditgrade (客户等级，多选选项集)");
    }

    static void RebuildTradeStPayTermGradeFields(ServiceClient service, EntityManager manager)
    {
        const string entityName = "mcs_trade_stpayterm";
        const string buyerGradeField = "mcs_buyergrade";
        const string creditGradeField = "mcs_creditgrade";
        string backupPath = $"/tmp/mcs_trade_stpayterm_grade_backup_{DateTime.Now:yyyyMMddHHmmss}.json";

        Console.WriteLine($">>> 重建 {entityName} 的客户分类/客户等级字段为多选选项集");

        bool FieldExists(string fieldName)
        {
            try
            {
                var req = new RetrieveAttributeRequest
                {
                    EntityLogicalName = entityName,
                    LogicalName = fieldName,
                    RetrieveAsIfPublished = true
                };
                service.Execute(req);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // 1. 备份现有数据
        Console.WriteLine($">>> 步骤 1/9: 备份现有 {buyerGradeField}/{creditGradeField} 数据");
        var backupRecords = new List<Dictionary<string, object>>();
        var query = new QueryExpression(entityName)
        {
            ColumnSet = new ColumnSet("mcs_trade_stpaytermname", buyerGradeField, creditGradeField),
            PageInfo = new PagingInfo { PageNumber = 1, PagingCookie = null, Count = 5000 }
        };
        while (true)
        {
            var result = service.RetrieveMultiple(query);
            foreach (var r in result.Entities)
            {
                var item = new Dictionary<string, object>
                {
                    ["id"] = r.Id,
                    ["mcs_trade_stpaytermname"] = r.GetAttributeValue<string>("mcs_trade_stpaytermname") ?? ""
                };

                var oldBuyerGrade = r.GetAttributeValue<string>(buyerGradeField);
                if (!string.IsNullOrEmpty(oldBuyerGrade))
                    item[buyerGradeField] = oldBuyerGrade;

                var oldCreditGrade = r.GetAttributeValue<OptionSetValue>(creditGradeField);
                if (oldCreditGrade != null)
                    item[creditGradeField] = oldCreditGrade.Value;

                backupRecords.Add(item);
            }
            if (result.MoreRecords)
            {
                query.PageInfo.PageNumber++;
                query.PageInfo.PagingCookie = result.PagingCookie;
            }
            else break;
        }
        System.IO.File.WriteAllText(backupPath, System.Text.Json.JsonSerializer.Serialize(backupRecords, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"  ✅ 已备份 {backupRecords.Count} 条记录到 {backupPath}");

        // 2. 从视图移除字段
        Console.WriteLine($">>> 步骤 2/9: 从所有视图移除字段");
        manager.RemoveFieldFromViews(entityName, buyerGradeField);
        manager.RemoveFieldFromViews(entityName, creditGradeField);

        // 3. 从表单移除字段
        Console.WriteLine($">>> 步骤 3/9: 从主窗体移除字段");
        manager.RemoveFieldsFromForm(entityName, buyerGradeField, creditGradeField);

        // 4. 发布实体（使移除生效）
        Console.WriteLine($">>> 步骤 4/9: 发布实体 {entityName}");
        manager.PublishEntity(entityName);

        // 5. 删除老字段
        Console.WriteLine($">>> 步骤 5/9: 删除老字段");
        if (FieldExists(buyerGradeField))
        {
            manager.DeleteField(entityName, buyerGradeField);
        }
        else
        {
            Console.WriteLine($"  ⊘ {buyerGradeField} 已不存在，跳过删除");
        }
        if (FieldExists(creditGradeField))
        {
            manager.DeleteField(entityName, creditGradeField);
        }
        else
        {
            Console.WriteLine($"  ⊘ {creditGradeField} 已不存在，跳过删除");
        }

        // 6. 新建多选选项集字段
        Console.WriteLine($">>> 步骤 6/9: 新建多选选项集字段");
        var buyerGradeOptions = new Dictionary<string, int>
        {
            { "S", 100000000 }, { "A", 100000001 }, { "B", 100000002 },
            { "C", 100000003 }, { "I", 100000004 },
            { "D1", 100000005 }, { "D2", 100000006 }, { "D3", 100000007 },
            { "D4", 100000008 }, { "D5", 100000009 }
        };
        if (!FieldExists(buyerGradeField))
        {
            manager.CreateMultiSelectPicklistField(entityName, buyerGradeField, "客户分类",
                "客户分类，多选。S/A/B/C/I 对应直销/个人客户，D1-D5 对应经销商分级", buyerGradeOptions);
        }
        else
        {
            Console.WriteLine($"  ⊘ {buyerGradeField} 已存在，跳过创建");
        }

        var creditGradeOptions = new Dictionary<string, int>
        {
            { "A0", 100000000 }, { "A1", 100000001 }, { "A2", 100000002 },
            { "A3", 100000003 }, { "A4", 100000004 }
        };
        if (!FieldExists(creditGradeField))
        {
            manager.CreateMultiSelectPicklistField(entityName, creditGradeField, "客户等级",
                "客户等级 A0-A4，与客户主数据 mcs_creditgrade 保持一致", creditGradeOptions);
        }
        else
        {
            Console.WriteLine($"  ⊘ {creditGradeField} 已存在，跳过创建");
        }

        // 7. 添加到视图
        Console.WriteLine($">>> 步骤 7/9: 添加字段到默认视图");
        manager.UpdateDefaultView(entityName, new Dictionary<string, string>
        {
            { buyerGradeField, "客户分类" },
            { creditGradeField, "客户等级" }
        });

        // 8. 添加到表单
        Console.WriteLine($">>> 步骤 8/9: 添加字段到主窗体");
        manager.UpdateMainForm(entityName, new Dictionary<string, string>
        {
            { buyerGradeField, "客户分类" },
            { creditGradeField, "客户等级" }
        });

        // 9. 最终发布
        Console.WriteLine($">>> 步骤 9/9: 最终发布实体 {entityName}");
        manager.PublishEntity(entityName);

        Console.WriteLine($">>> 重建完成。备份文件: {backupPath}");
    }

    static void QueryTradeStPayTermSamples(ServiceClient service, int topCount)
    {
        Console.WriteLine($">>> 查询成交条件样板库样本数据 (Top {topCount})");

        var query = new QueryExpression("mcs_trade_stpayterm")
        {
            ColumnSet = new ColumnSet(
                "mcs_trade_stpaytermname", "mcs_buid", "mcs_buname", "mcs_subid", "mcs_subname",
                "mcs_countries", "mcs_trade_type",
                "mcs_countrycode", "mcs_countryname", "mcs_typeid", "mcs_typename",
                "mcs_buyergrade", "mcs_status"),
            TopCount = topCount
        };

        var records = service.RetrieveMultiple(query).Entities;
        Console.WriteLine($"  找到 {records.Count} 条记录:");
        foreach (var r in records)
        {
            Console.WriteLine($"    编码={r.GetAttributeValue<string>("mcs_trade_stpaytermname")}, " +
                              $"BU={r.GetAttributeValue<string>("mcs_buid")}, " +
                              $"Sub={r.GetAttributeValue<string>("mcs_subid")}, " +
                              $"Countries={r.GetAttributeValue<string>("mcs_countries")}, " +
                              $"TradeType={r.GetAttributeValue<string>("mcs_trade_type")}, " +
                              $"CountryCode={r.GetAttributeValue<string>("mcs_countrycode")}, " +
                              $"CountryName={r.GetAttributeValue<string>("mcs_countryname")}, " +
                              $"TypeId={r.GetAttributeValue<string>("mcs_typeid")}, " +
                              $"TypeName={r.GetAttributeValue<string>("mcs_typename")}");
        }

        Console.WriteLine($">>> 查询产品线-产品分类映射样本 (Top {topCount})");
        var groupQuery = new QueryExpression("mcs_trade_ptgrouptype")
        {
            ColumnSet = new ColumnSet("mcs_groupid", "mcs_groupname", "mcs_typeid"),
            TopCount = topCount
        };
        var groupRecords = service.RetrieveMultiple(groupQuery).Entities;
        Console.WriteLine($"  找到 {groupRecords.Count} 条映射:");
        foreach (var r in groupRecords)
        {
            Console.WriteLine($"    GroupId={r.GetAttributeValue<string>("mcs_groupid")}, " +
                              $"GroupName={r.GetAttributeValue<string>("mcs_groupname")}, " +
                              $"TypeId={r.GetAttributeValue<string>("mcs_typeid")}");
        }

        Console.WriteLine($">>> 查询客户主数据样本 (Top {topCount})");
        var customerQuery = new QueryExpression("mcs_customermasterdata")
        {
            ColumnSet = new ColumnSet("mcs_accountnumber", "mcs_accountcategory", "mcs_accountlevel", "mcs_dealerrank"),
            TopCount = topCount
        };
        var customerRecords = service.RetrieveMultiple(customerQuery).Entities;
        Console.WriteLine($"  找到 {customerRecords.Count} 条客户主数据:");
        foreach (var r in customerRecords)
        {
            Console.WriteLine($"    AccountNumber={r.GetAttributeValue<string>("mcs_accountnumber")}, " +
                              $"Category={r.GetAttributeValue<OptionSetValue>("mcs_accountcategory")?.Value}, " +
                              $"Level={r.GetAttributeValue<OptionSetValue>("mcs_accountlevel")?.Value}, " +
                              $"DealerRank={r.GetAttributeValue<OptionSetValue>("mcs_dealerrank")?.Value}");
        }
    }

    static void TestTradeStPayTermApi(ServiceClient service, string buId, string subId, string countryCode, string prdGroupId, string buyerCode)
    {
        Console.WriteLine($">>> 测试 Custom API: mcs_QueryTradeStPayTerm");
        Console.WriteLine($"    入参: buId={buId}, subId={subId}, countryCode={countryCode}, prdGroupId={prdGroupId}, buyerCode={buyerCode}");

        try
        {
            var request = new OrganizationRequest("mcs_QueryTradeStPayTerm");
            request["mcs_buid"] = buId;
            request["mcs_subid"] = subId;
            request["mcs_countrycode"] = countryCode;
            request["mcs_prdgroupid"] = prdGroupId;
            request["mcs_buyercode"] = buyerCode;

            var response = service.Execute(request);
            var status = response["status"]?.ToString() ?? "?";
            var message = response["message"]?.ToString() ?? "";
            var records = response["records"]?.ToString() ?? "[]";

            Console.WriteLine($"  ✅ 调用成功");
            Console.WriteLine($"     Status: {status}");
            Console.WriteLine($"     Message: {message}");
            Console.WriteLine($"     Records: {records}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ 调用失败: {ex.Message}");
            if (ex.InnerException != null)
                Console.WriteLine($"     Inner: {ex.InnerException.Message}");
        }
    }

    /// <summary>
    /// 成交条件基线库数据导入（2026-09-04 生产初始化）：逐行创建 mcs_trade_stpayterm。
    /// Lookup（事业部/大区子公司）已在 JSON 中预解析为生产 GUID；产品分类名称交给服务端「名称→GUID」解析插件；
    /// 客户分类多选直接传选项值；生效状态留空由插件默认 2（生效）。
    /// </summary>
    static void ImportTradeStPayTerm(ServiceClient service, string jsonPath, bool dryRun, int pilot)
    {
        var json = System.IO.File.ReadAllText(jsonPath);
        var rows = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, System.Text.Json.JsonElement>>>(json);
        int total = pilot > 0 ? Math.Min(pilot, rows.Count) : rows.Count;
        Console.WriteLine($">>> 导入成交条件基线库：共 {rows.Count} 行，本次执行 {total} 行{(dryRun ? "（dry-run 只打印不创建）" : "")}");

        int ok = 0, fail = 0;
        for (int i = 0; i < total; i++)
        {
            var r = rows[i];
            string name = r["name"].GetString();
            string subName = r["subName"].GetString();
            try
            {
                if (dryRun)
                {
                    var grades = r["buyerGrades"].EnumerateArray().Select(g => g.GetInt32());
                    Console.WriteLine($"  [DRY] 行{r["row"].GetInt32()} {name} | {subName} | {r["typeName"].GetString()} | 分类[{string.Join(";", grades)}] | 首付{r["downpay"].GetDecimal()} 账期{r["payterm"].GetInt32()} 频次{r["payfreq"].GetInt32()}");
                    ok++;
                    continue;
                }

                var entity = new Entity("mcs_trade_stpayterm");
                entity["mcs_trade_stpaytermname"] = name;
                entity["mcs_businessunit"] = new EntityReference("mcs_bu", Guid.Parse(r["buId"].GetString()));
                entity["mcs_buname"] = r["buName"].GetString();
                entity["mcs_buid"] = r["buCode"].GetString();
                entity["mcs_subsidiary"] = new EntityReference("mcs_region", Guid.Parse(r["subId"].GetString()));
                entity["mcs_subname"] = subName;
                entity["mcs_subid"] = r["subCode"].GetString();
                entity["mcs_typename"] = r["typeName"].GetString();
                // 可选：国家名称（服务端「名称→GUID」解析插件回填 mcs_countries/mcs_countrycode）
                if (r.TryGetValue("countryName", out var cn) && cn.ValueKind == System.Text.Json.JsonValueKind.String && !string.IsNullOrWhiteSpace(cn.GetString()))
                {
                    entity["mcs_countryname"] = cn.GetString();
                }
                var gradeCollection = new OptionSetValueCollection();
                foreach (var g in r["buyerGrades"].EnumerateArray())
                {
                    gradeCollection.Add(new OptionSetValue(g.GetInt32()));
                }
                entity["mcs_buyergrade"] = gradeCollection;
                entity["mcs_downpay"] = r["downpay"].GetDecimal();
                entity["mcs_payterm"] = r["payterm"].GetInt32();
                entity["mcs_payfreq"] = r["payfreq"].GetInt32();

                var id = service.Create(entity);
                ok++;
                Console.WriteLine($"  ✓ 行{r["row"].GetInt32()} {name} ({subName}/{r["typeName"].GetString()}) -> {id}");
            }
            catch (Exception ex)
            {
                fail++;
                var msg = ex.Message.Length > 200 ? ex.Message.Substring(0, 200) : ex.Message;
                Console.WriteLine($"  ✗ 行{r["row"].GetInt32()} {name} ({subName}/{r["typeName"].GetString()}): {msg}");
            }
        }
        Console.WriteLine($">>> 完成：成功 {ok}，失败 {fail}");
        if (fail > 0)
        {
            Environment.ExitCode = 1;
        }
    }

    static void QueryRecords(ServiceClient service, string entityName, string fieldList, int topCount, string equalityFilter = null)
    {
        Console.WriteLine($">>> 查询 {entityName}（Top {topCount}）: {fieldList}" + (equalityFilter != null ? $"，过滤: {equalityFilter}" : ""));

        var fields = fieldList.Split(',').Select(f => f.Trim()).Where(f => !string.IsNullOrEmpty(f)).ToArray();
        var query = new QueryExpression(entityName)
        {
            ColumnSet = new ColumnSet(fields),
            TopCount = topCount
        };
        if (equalityFilter != null)
        {
            var kv = equalityFilter.Split(new[] { '=' }, 2);
            object val = kv[1];
            if (Guid.TryParse(kv[1], out var g)) val = g;
            else if (int.TryParse(kv[1], out var i) && !(kv[1].Length > 1 && kv[1].StartsWith("0"))) val = i; // 前导零的数字保持字符串（如 SAP 编码 0200001384）
            query.Criteria.AddCondition(kv[0], ConditionOperator.Equal, val);
        }
        query.AddOrder("createdon", OrderType.Descending);

        var result = service.RetrieveMultiple(query);
        Console.WriteLine($"  返回 {result.Entities.Count} 条记录");
        foreach (var entity in result.Entities)
        {
            var parts = fields.Select(f =>
            {
                if (!entity.Attributes.Contains(f) || entity[f] == null)
                    return $"{f}=(空)";
                var v = entity[f];
                if (v is EntityReference er)
                    return $"{f}={er.Name}({er.Id})";
                if (v is OptionSetValue osv)
                    return $"{f}={osv.Value}";
                if (v is Money m)
                    return $"{f}={m.Value}";
                return $"{f}={v}";
            });
            Console.WriteLine($"  - {entity.Id}: {string.Join(", ", parts)}");
        }
    }

    // 通用创建记录（后台测试用）：JSON 键支持类型后缀 #int/#decimal/#bool/#optionset/#optionsetcollection/#lookup
    static void CreateRecordFromJson(ServiceClient service, string entityName, string jsonOrFile)
    {
        var entity = BuildEntityFromJson(service, entityName, jsonOrFile);
        if (entity == null) return;

        try
        {
            var id = service.Create(entity);
            Console.WriteLine($"  ✅ 已创建 {entityName} ({id})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ 创建被拦截/失败: {ex.Message}");
        }
    }

    static void UpdateRecordFromJson(ServiceClient service, string entityName, Guid id, string jsonOrFile)
    {
        var entity = BuildEntityFromJson(service, entityName, jsonOrFile);
        if (entity == null) return;
        entity.Id = id;

        try
        {
            service.Update(entity);
            Console.WriteLine($"  ✅ 已更新 {entityName} ({id})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ 更新被拦截/失败: {ex.Message}");
        }
    }

    static Entity BuildEntityFromJson(ServiceClient service, string entityName, string jsonOrFile)
    {
        var json = jsonOrFile.StartsWith("@") ? File.ReadAllText(jsonOrFile.Substring(1)) : jsonOrFile;
        var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(json);
        if (dict == null || dict.Count == 0)
        {
            Console.WriteLine("  ❌ JSON 为空或解析失败");
            return null;
        }

        var entity = new Entity(entityName);
        foreach (var kv in dict)
        {
            var key = kv.Key;
            var val = kv.Value;
            string field = key, type = "string";
            var hashIdx = key.IndexOf('#');
            if (hashIdx > 0)
            {
                field = key.Substring(0, hashIdx);
                type = key.Substring(hashIdx + 1).ToLowerInvariant();
            }

            switch (type)
            {
                case "int":
                    entity[field] = val.GetInt32();
                    break;
                case "decimal":
                    entity[field] = val.ValueKind == System.Text.Json.JsonValueKind.Null ? null : val.GetDecimal();
                    break;
                case "money":
                    entity[field] = new Money(val.GetDecimal());
                    break;
                case "bool":
                    entity[field] = val.GetBoolean();
                    break;
                case "optionset":
                    entity[field] = val.ValueKind == System.Text.Json.JsonValueKind.Null ? null : new OptionSetValue(val.GetInt32());
                    break;
                case "optionsetcollection":
                    var osc = new OptionSetValueCollection();
                    if (val.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var item in val.EnumerateArray()) osc.Add(new OptionSetValue(item.GetInt32()));
                    }
                    else
                    {
                        foreach (var part in (val.GetString() ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
                            osc.Add(new OptionSetValue(int.Parse(part.Trim())));
                    }
                    entity[field] = osc;
                    break;
                case "lookup":
                    if (val.ValueKind == System.Text.Json.JsonValueKind.Null)
                    {
                        entity[field] = null;
                        break;
                    }
                    var parts = (val.GetString() ?? "").Split(':');
                    if (parts.Length != 2 || !Guid.TryParse(parts[1], out var lookupGuid))
                    {
                        Console.WriteLine($"  ❌ lookup 值格式错误（应为 logicalName:guid）: {field} = {val.GetString()}");
                        return null;
                    }
                    entity[field] = new EntityReference(parts[0], lookupGuid);
                    break;
                default:
                    entity[field] = val.ValueKind == System.Text.Json.JsonValueKind.Null ? null : val.GetString();
                    break;
            }
        }

        return entity;
    }

    // 删除字段前的强制提醒与确认（方案A试点）：返回 true 才允许删除
    static bool ConfirmDeleteField(string entityName, string fieldName, bool force)
    {
        Console.WriteLine();
        Console.WriteLine("⚠️⚠️ 警告：删除字段可能会影响生产环境发布版本！ ⚠️⚠️");
        Console.WriteLine($"  目标字段: {entityName}.{fieldName}");
        Console.WriteLine("  影响说明:");
        Console.WriteLine("    - 若该字段已随历史版本发布到 UAT/生产，删除后同名重建（尤其类型不同），");
        Console.WriteLine("      会导致托管 Solution 导入失败（80041A06），需客户手动在生产删字段才能发版。");
        Console.WriteLine("  建议替代方案（优先考虑）:");
        Console.WriteLine("    1. 新增字段（新架构名，如 " + fieldName + "2），业务切换到新字段；");
        Console.WriteLine("    2. 旧字段显示名加「（废弃）」，从表单/视图移除，数据保留。");
        if (force)
        {
            Console.WriteLine("  已指定 --force，跳过确认直接删除。");
            return true;
        }
        Console.WriteLine();
        Console.Write($"  确认仍要删除 {entityName}.{fieldName} 吗？请输入 YES 继续，其他任意输入取消: ");
        var input = Console.ReadLine();
        if (string.Equals(input?.Trim(), "YES", StringComparison.Ordinal))
        {
            Console.WriteLine("  已确认，执行删除...");
            return true;
        }
        Console.WriteLine("  ❌ 已取消删除操作。");
        return false;
    }

    // 端到端测试：成交条件创建时「名称 → GUID」解析（TradeStPayTermValidationPlugin）
    // 场景：TC1 多值名称解析 / TC2 未知名称阻断 / TC3 直传 GUID 不覆盖 / TC4 中文分隔符 / TC5 NA 通配
    static void TestTradeStPayTermResolve(ServiceClient service)
    {
        const string TestBu = "TESTBU-RESOLVE";
        var createdIds = new List<Guid>();
        int pass = 0, fail = 0;
        int caseNo = 0;

        Action<string, bool, string, string, Action<Entity>> runCase = (caseName, expectSuccess, countryName, typeName, verify) =>
        {
            caseNo++;
            string buid = TestBu + "-" + caseNo; // 每个用例独立事业部，避免空维度通配导致自碰撞
            Console.WriteLine($"\n=== {caseName} ===");
            try
            {
                var entity = new Entity("mcs_trade_stpayterm");
                entity["mcs_buid"] = buid;
                if (countryName != null) entity["mcs_countryname"] = countryName;
                if (typeName != null) entity["mcs_typename"] = typeName;
                var id = service.Create(entity);
                createdIds.Add(id);
                var saved = service.Retrieve("mcs_trade_stpayterm", id,
                    new ColumnSet("mcs_countries", "mcs_countrycode", "mcs_trade_type", "mcs_typeid", "mcs_trade_stpaytermname"));
                Console.WriteLine($"  创建成功: {saved.GetAttributeValue<string>("mcs_trade_stpaytermname")}");
                Console.WriteLine($"  mcs_countries   = {saved.GetAttributeValue<string>("mcs_countries") ?? "(空)"}");
                Console.WriteLine($"  mcs_countrycode = {saved.GetAttributeValue<string>("mcs_countrycode") ?? "(空)"}");
                Console.WriteLine($"  mcs_trade_type  = {saved.GetAttributeValue<string>("mcs_trade_type") ?? "(空)"}");
                Console.WriteLine($"  mcs_typeid      = {saved.GetAttributeValue<string>("mcs_typeid") ?? "(空)"}");
                if (!expectSuccess)
                {
                    Console.WriteLine("  ❌ 预期应失败但创建成功了");
                    fail++;
                    return;
                }
                verify?.Invoke(saved);
                pass++;
            }
            catch (Exception ex)
            {
                var msg = ex.Message;
                Console.WriteLine($"  创建被阻断: {(msg.Length > 200 ? msg.Substring(0, 200) + "..." : msg)}");
                if (!expectSuccess && msg.Contains("无法识别"))
                {
                    Console.WriteLine("  ✅ 符合预期（未知名称被阻断）");
                    pass++;
                }
                else
                {
                    Console.WriteLine("  ❌ 不符合预期");
                    fail++;
                }
            }
        };

        // TC1: 多值名称解析（英文逗号）
        runCase("TC1 国家=南非,科特迪瓦 + 产品分类=泵车（英文逗号多值）", true, "南非,科特迪瓦", "泵车", saved =>
        {
            var countries = saved.GetAttributeValue<string>("mcs_countries") ?? "";
            var codes = saved.GetAttributeValue<string>("mcs_countrycode") ?? "";
            var tradeType = saved.GetAttributeValue<string>("mcs_trade_type") ?? "";
            var typeId = saved.GetAttributeValue<string>("mcs_typeid") ?? "";
            if (countries.Split(',').Length == 2 && codes == "1,KT" && tradeType.Split(',').Length == 1 && typeId == "03")
                Console.WriteLine("  ✅ 解析结果正确（2 国家 GUID + 编码 1,KT + 1 产品分类 GUID + 03）");
            else
                throw new Exception("解析结果与预期不符");
        });

        // TC2: 未知名称阻断
        runCase("TC2 国家=不存在的国家XYZ（应阻断）", false, "不存在的国家XYZ", null, null);

        // TC3: 直传 GUID 不覆盖
        Console.WriteLine("\n=== TC3 直传 mcs_countries GUID（应不覆盖） ===");
        try
        {
            var entity = new Entity("mcs_trade_stpayterm");
            entity["mcs_buid"] = TestBu + "-TC3";
            entity["mcs_countries"] = "ec0106f9-5784-ef11-ac20-000d3a08066b";
            entity["mcs_countryname"] = "南非";
            var id = service.Create(entity);
            createdIds.Add(id);
            var saved = service.Retrieve("mcs_trade_stpayterm", id, new ColumnSet("mcs_countries"));
            var countries = saved.GetAttributeValue<string>("mcs_countries") ?? "";
            Console.WriteLine($"  mcs_countries = {countries}");
            if (countries.Equals("ec0106f9-5784-ef11-ac20-000d3a08066b", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("  ✅ 直传 GUID 未被覆盖");
                pass++;
            }
            else
            {
                Console.WriteLine("  ❌ 直传 GUID 被改动");
                fail++;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ 异常: {ex.Message}");
            fail++;
        }

        // TC4: 中文逗号/顿号分隔符
        runCase("TC4 国家=南非，科特迪瓦（中文逗号）", true, "南非，科特迪瓦", null, saved =>
        {
            var countries = saved.GetAttributeValue<string>("mcs_countries") ?? "";
            if (countries.Split(',').Length == 2)
                Console.WriteLine("  ✅ 中文分隔符解析正确");
            else
                throw new Exception("中文分隔符解析结果与预期不符");
        });

        // TC5: NA 通配（不解析、不报错）
        runCase("TC5 国家=NA（通配，应创建成功且 GUID 为空）", true, "NA", null, saved =>
        {
            var countries = saved.GetAttributeValue<string>("mcs_countries");
            if (string.IsNullOrEmpty(countries))
                Console.WriteLine("  ✅ NA 按通配处理，GUID 字段为空");
            else
                throw new Exception("NA 不应解析出 GUID");
        });

        // 清理测试数据
        Console.WriteLine($"\n=== 清理 {createdIds.Count} 条测试记录 ===");
        foreach (var id in createdIds)
        {
            try { service.Delete("mcs_trade_stpayterm", id); }
            catch (Exception ex) { Console.WriteLine($"  清理失败 {id}: {ex.Message}"); }
        }
        Console.WriteLine($"\n>>> 测试结果: {pass} 通过, {fail} 失败");
    }

    /// <summary>
    /// 必填兜底 + 空值重复校验语义测试（2026-07-27，禅道#1282 衍生修复）。
    /// 前置条件：DEV1 已注册临时 Assembly SanyD365.Plugins.TradeStPayTerm 及 Validation Create/Update Step。
    /// 测试期间会临时停用主 Assembly SanyD365.D365Extension.Sales 的 ValidationPlugin Create/Update Step（旧通配语义会干扰断言），结束后自动恢复。
    /// </summary>
    static void TestTradeStPayTermRequired(ServiceClient service, bool mainAssemblyMode = false)
    {
        Console.WriteLine($"=== 成交条件样板库：必填兑底 + 空值重复校验语义测试{(mainAssemblyMode ? "（主 Assembly 回归模式）" : "（临时 Assembly 模式）")} ===");

        // 0. 选一个没有任何存量样板库记录的事业部，避免与真实数据砲撞
        var buQuery = new QueryExpression("mcs_bu")
        {
            ColumnSet = new ColumnSet("mcs_code", "mcs_name"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("mcs_code", ConditionOperator.NotNull) }
            },
            PageInfo = new PagingInfo { Count = 500, PageNumber = 1 }
        };
        var bus = service.RetrieveMultiple(buQuery).Entities;
        Entity chosenBu = null;
        foreach (var candidate in bus)
        {
            var code = candidate.GetAttributeValue<string>("mcs_code");
            var existQuery = new QueryExpression("mcs_trade_stpayterm")
            {
                ColumnSet = new ColumnSet(false),
                Criteria = new FilterExpression
                {
                    Filters =
                    {
                        new FilterExpression(LogicalOperator.Or)
                        {
                            Conditions =
                            {
                                new ConditionExpression("mcs_businessunit", ConditionOperator.Equal, candidate.Id),
                                new ConditionExpression("mcs_buid", ConditionOperator.Equal, code)
                            }
                        }
                    }
                },
                PageInfo = new PagingInfo { Count = 1, PageNumber = 1, ReturnTotalRecordCount = true }
            };
            var exist = service.RetrieveMultiple(existQuery);
            if (exist.TotalRecordCount == 0)
            {
                chosenBu = candidate;
                break;
            }
        }
        if (chosenBu == null)
        {
            Console.WriteLine("❌ 所有事业部都有存量记录，无法安全测试（需人工指定空事业部）");
            return;
        }
        var buRef = chosenBu.ToEntityReference();
        Console.WriteLine($"测试事业部: {chosenBu.GetAttributeValue<string>("mcs_name")} ({chosenBu.GetAttributeValue<string>("mcs_code")})，存量记录 0 条");

        // 1. 停用主 Assembly 的 ValidationPlugin Create/Update Step（旧通配语义会干扰 T7/T9 断言）
        var mainStepIds = new List<Guid>();
        var stepQuery = new QueryExpression("sdkmessageprocessingstep")
        {
            ColumnSet = new ColumnSet("sdkmessageprocessingstepid", "statecode"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("stage", ConditionOperator.Equal, 20),
                    new ConditionExpression("plugintypeid", ConditionOperator.NotNull)
                }
            },
            LinkEntities =
            {
                new LinkEntity("sdkmessageprocessingstep", "plugintype", "plugintypeid", "plugintypeid", JoinOperator.Inner)
                {
                    LinkCriteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("typename", ConditionOperator.Like, "%TradeStPayTermValidationPlugin")
                        }
                    },
                    LinkEntities =
                    {
                        new LinkEntity("plugintype", "pluginassembly", "pluginassemblyid", "pluginassemblyid", JoinOperator.Inner)
                        {
                            LinkCriteria = new FilterExpression
                            {
                                Conditions =
                                {
                                    new ConditionExpression("name", ConditionOperator.Equal, "SanyD365.D365Extension.Sales")
                                }
                            }
                        }
                    }
                }
            }
        };
        foreach (var step in service.RetrieveMultiple(stepQuery).Entities)
        {
            if (step.GetAttributeValue<OptionSetValue>("statecode")?.Value == 0)
            {
                mainStepIds.Add(step.Id);
            }
        }
        if (mainAssemblyMode)
        {
            // 主 Assembly 回归模式：被测对象就是主 Assembly Step，不能停用
            Console.WriteLine($"主 Assembly Validation Step: {mainStepIds.Count} 个保持启用（回归被测对象）");
            mainStepIds.Clear();
        }
        else
        {
            Console.WriteLine($"主 Assembly Validation Step: {mainStepIds.Count} 个已临时停用（测试后自动恢复）");
            foreach (var stepId in mainStepIds)
            {
                service.Execute(new Microsoft.Crm.Sdk.Messages.SetStateRequest
                {
                    EntityMoniker = new EntityReference("sdkmessageprocessingstep", stepId),
                    State = new OptionSetValue(1),
                    Status = new OptionSetValue(-1)
                });
            }
        }

        var createdIds = new List<Guid>();
        int pass = 0, fail = 0;

        Func<Entity> newBase = () =>
        {
            var e = new Entity("mcs_trade_stpayterm");
            e["mcs_businessunit"] = buRef;
            e["mcs_buyergrade"] = new OptionSetValueCollection(new[] { new OptionSetValue(100000009) }); // D5
            e["mcs_downpay"] = 0.3m;
            e["mcs_payterm"] = 30;
            e["mcs_payfreq"] = 30;
            return e;
        };

        // expectBlock=null 预期成功；否则预期被拦截且错误信息包含该片段
        Action<string, Func<Guid>, string> runCase = (name, action, expectBlock) =>
        {
            Console.WriteLine($"\n=== {name} ===");
            try
            {
                var id = action();
                if (expectBlock == null)
                {
                    Console.WriteLine($"  ✅ 符合预期（创建成功 {(id != Guid.Empty ? id.ToString() : "")}）");
                    pass++;
                }
                else
                {
                    Console.WriteLine("  ❌ 预期应被拦截但操作成功了");
                    fail++;
                }
            }
            catch (Exception ex)
            {
                var msg = ex.Message;
                Console.WriteLine($"  被拦截: {(msg.Length > 150 ? msg.Substring(0, 150) + "..." : msg)}");
                if (expectBlock != null && msg.Contains(expectBlock))
                {
                    Console.WriteLine($"  ✅ 符合预期（包含「{expectBlock}」）");
                    pass++;
                }
                else
                {
                    Console.WriteLine($"  ❌ 不符合预期（期望包含「{expectBlock ?? "成功"}」）");
                    fail++;
                }
            }
        };

        Guid baseId = Guid.Empty;
        try
        {
            // TC1: 基线记录（必填齐全，其余维度全空）→ 应创建成功
            runCase("TC1 基线记录（必填齐全+其余维度空）应创建成功", () =>
            {
                baseId = service.Create(newBase());
                createdIds.Add(baseId);
                return baseId;
            }, null);

            // TC2-TC6: 必填缺失逐项拦截
            runCase("TC2 缺客户分类代码应拦截", () =>
            {
                var e = newBase(); e.Attributes.Remove("mcs_buyergrade");
                return service.Create(e);
            }, "客户分类代码为必填项");

            runCase("TC3 缺事业部应拦截", () =>
            {
                var e = newBase(); e.Attributes.Remove("mcs_businessunit");
                return service.Create(e);
            }, "事业部为必填项");

            runCase("TC4 缺首付款比例应拦截", () =>
            {
                var e = newBase(); e.Attributes.Remove("mcs_downpay");
                return service.Create(e);
            }, "首付款比例为必填项");

            runCase("TC5 缺账期应拦截", () =>
            {
                var e = newBase(); e.Attributes.Remove("mcs_payterm");
                return service.Create(e);
            }, "账期（天）为必填项");

            runCase("TC6 缺付款频次数应拦截", () =>
            {
                var e = newBase(); e.Attributes.Remove("mcs_payfreq");
                return service.Create(e);
            }, "付款频次（天）为必填项");

            // TC7: 与基线全维度相同（含空）→ 重复拦截
            runCase("TC7 与基线全维度相同（含空值）应判重复", () => service.Create(newBase()), "存在有重复记录");

            // TC8: 子公司非空 vs 基线空 → 不重复，创建成功
            runCase("TC8 子公司=TESTSUB-REQ（空 vs 非空）应不判重复", () =>
            {
                var e = newBase(); e["mcs_subid"] = "TESTSUB-REQ";
                var id = service.Create(e);
                createdIds.Add(id);
                return id;
            }, null);

            // TC9: 再建一条与 TC8 完全相同 → 重复拦截（非空精确匹配）
            runCase("TC9 与 TC8 完全相同（子公司非空）应判重复", () =>
            {
                var e = newBase(); e["mcs_subid"] = "TESTSUB-REQ";
                return service.Create(e);
            }, "存在有重复记录");

            // TC10: 客户等级 A0 vs 基线空 → 不重复，创建成功
            runCase("TC10 客户等级=A0（空 vs 非空）应不判重复", () =>
            {
                var e = newBase(); e["mcs_creditgrade"] = new OptionSetValue(100000000);
                var id = service.Create(e);
                createdIds.Add(id);
                return id;
            }, null);

            // TC11: 子公司=NA 应归一为空，与基线判重复
            runCase("TC11 子公司=NA（归一为空）应与基线判重复", () =>
            {
                var e = newBase(); e["mcs_subid"] = "NA";
                return service.Create(e);
            }, "存在有重复记录");

            // TC12: Update 仅改首付款比例 → 合并状态必填校验不误伤
            if (baseId != Guid.Empty)
            {
                runCase("TC12 Update 仅改首付款比例应成功（不误伤）", () =>
                {
                    var e = new Entity("mcs_trade_stpayterm", baseId);
                    e["mcs_downpay"] = 0.4m;
                    service.Update(e);
                    return baseId;
                }, null);

                // TC13: Update 清空客户分类 → 拦截
                runCase("TC13 Update 清空客户分类代码应拦截", () =>
                {
                    var e = new Entity("mcs_trade_stpayterm", baseId);
                    e["mcs_buyergrade"] = new OptionSetValueCollection();
                    service.Update(e);
                    return baseId;
                }, "客户分类代码为必填项");
            }
        }
        finally
        {
            // 恢复主 Assembly Steps
            foreach (var stepId in mainStepIds)
            {
                try
                {
                    service.Execute(new Microsoft.Crm.Sdk.Messages.SetStateRequest
                    {
                        EntityMoniker = new EntityReference("sdkmessageprocessingstep", stepId),
                        State = new OptionSetValue(0),
                        Status = new OptionSetValue(1)
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ⚠️ 恢复主 Assembly Step {stepId} 失败: {ex.Message}（需人工检查！）");
                }
            }
            if (mainStepIds.Count > 0) Console.WriteLine($"\n主 Assembly Validation Step 已恢复启用 ({mainStepIds.Count} 个)");

            // 清理测试记录
            Console.WriteLine($"\n=== 清理 {createdIds.Count} 条测试记录 ===");
            foreach (var id in createdIds)
            {
                try { service.Delete("mcs_trade_stpayterm", id); }
                catch (Exception ex) { Console.WriteLine($"  清理失败 {id}: {ex.Message}"); }
            }
        }

        Console.WriteLine($"\n>>> 测试结果: {pass} 通过, {fail} 失败");
    }

    // 注册临时 Assembly 的 TradeStPayTermValidationPlugin Create/Update PreOp Step
    // Type 的 name 加 _Temp 后缀避开与主 Assembly 同类名的备用键冲突（错误 2601）
    static void RegisterTradeStPayTermTempValidation(ServiceClient service)
    {
        const string assemblyName = "SanyD365.Plugins.TradeStPayTerm";
        const string typeName = "SanyD365.Plugins.TradeStPayTerm.TradeStPayTermValidationPlugin";

        // 1. 查找临时 Assembly
        var asmQuery = new QueryExpression("pluginassembly")
        {
            ColumnSet = new ColumnSet("pluginassemblyid"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("name", ConditionOperator.Equal, assemblyName) }
            }
        };
        var asmResult = service.RetrieveMultiple(asmQuery);
        if (asmResult.Entities.Count == 0)
        {
            Console.WriteLine($"  ❌ 未找到临时 Assembly: {assemblyName}（请先用 register-plugin 注册 DLL）");
            return;
        }
        var assemblyId = asmResult.Entities[0].Id;
        Console.WriteLine($"  ✓ 临时 Assembly: {assemblyName} ({assemblyId})");

        // 2. 查找/创建 Plugin Type（name 带 _Temp 后缀）
        var typeQuery = new QueryExpression("plugintype")
        {
            ColumnSet = new ColumnSet("plugintypeid"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("typename", ConditionOperator.Equal, typeName),
                    new ConditionExpression("pluginassemblyid", ConditionOperator.Equal, assemblyId)
                }
            }
        };
        var typeResult = service.RetrieveMultiple(typeQuery);
        Guid typeId;
        if (typeResult.Entities.Count > 0)
        {
            typeId = typeResult.Entities[0].Id;
            Console.WriteLine($"  ✓ Plugin Type 已存在 ({typeId})");
        }
        else
        {
            var pluginType = new Entity("plugintype");
            pluginType["pluginassemblyid"] = new EntityReference("pluginassembly", assemblyId);
            pluginType["typename"] = typeName;
            pluginType["friendlyname"] = "TradeStPayTermValidationPlugin_Temp";
            pluginType["name"] = "TradeStPayTermValidationPlugin_Temp";
            typeId = service.Create(pluginType);
            Console.WriteLine($"  ✓ Plugin Type 已创建 ({typeId})，name=TradeStPayTermValidationPlugin_Temp");
        }

        // 3. 注册 Create/Update PreOperation Steps（复用公共方法，幂等）
        var pluginService = new D365ToolCommon.Plugin.PluginRegistrationService(service);
        pluginService.RegisterOrUpdateSteps(typeId, new[]
        {
            new D365ToolCommon.Plugin.Models.StepConfig { MessageName = "Create", PrimaryEntity = "mcs_trade_stpayterm", Stage = 20, Mode = 0 },
            new D365ToolCommon.Plugin.Models.StepConfig { MessageName = "Update", PrimaryEntity = "mcs_trade_stpayterm", Stage = 20, Mode = 0 }
        });
        Console.WriteLine("  ✅ 临时 Validation Steps 注册完成");
    }

    // 只读诊断：列出导入失败的 Solution 及其日志中的错误明细（含字段类型冲突 80041A06）
    static void ListFailedImports(ServiceClient service, int topCount, DateTime? since = null)
    {
        Console.WriteLine($">>> 查询导入失败的 Solution（progress < 100，Top {topCount}{(since.HasValue ? $"，createdon >= {since:yyyy-MM-dd}" : "")}）");
        var query = new QueryExpression("importjob")
        {
            ColumnSet = new ColumnSet("solutionname", "startedon", "completedon", "progress", "operationcontext", "data"),
            TopCount = topCount
        };
        query.Criteria.AddCondition("progress", ConditionOperator.LessThan, 100.0);
        if (since.HasValue)
            query.Criteria.AddCondition("createdon", ConditionOperator.GreaterEqual, since.Value.ToUniversalTime());
        query.AddOrder("createdon", OrderType.Descending);

        var result = service.RetrieveMultiple(query);
        Console.WriteLine($"  返回 {result.Entities.Count} 条失败记录\n");
        foreach (var job in result.Entities)
        {
            var name = job.GetAttributeValue<string>("solutionname");
            var started = job.GetAttributeValue<DateTime?>("startedon");
            var progress = job.GetAttributeValue<double?>("progress");
            Console.WriteLine($"=== {name} | {started:yyyy-MM-dd HH:mm} | progress={progress:0.##} | id={job.Id}");
            var data = job.GetAttributeValue<string>("data");
            if (string.IsNullOrEmpty(data))
            {
                Console.WriteLine("  (无日志数据)");
                continue;
            }
            try
            {
                var doc = System.Xml.Linq.XDocument.Parse(data);
                var root = doc.Root;
                if (root != null)
                {
                    var errCode = (string)root.Attribute("errorcode");
                    var status = (string)root.Attribute("status");
                    if (!string.IsNullOrEmpty(errCode)) Console.WriteLine($"  错误码: {errCode}");
                    if (!string.IsNullOrEmpty(status)) Console.WriteLine($"  摘要: {(status.Length > 500 ? status.Substring(0, 500) + "..." : status)}");
                }
                // 全文扫描字段类型冲突（80041A06: Attribute xxx is a Xxx, but a Yyy type was specified）
                var attrMatches = System.Text.RegularExpressions.Regex.Matches(data, @"Attribute\s+(\w+)\s+is a\s+([^,\.]+),\s+but a\s+(\w+)\s+type was specified");
                var seenAttrs = new HashSet<string>();
                foreach (System.Text.RegularExpressions.Match m in attrMatches)
                {
                    var key = $"{m.Groups[1].Value}|{m.Groups[2].Value}|{m.Groups[3].Value}";
                    if (seenAttrs.Add(key))
                        Console.WriteLine($"  ⚠️ 字段类型冲突: {m.Groups[1].Value}（环境中={m.Groups[2].Value}，包中={m.Groups[3].Value}）");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  (日志解析失败: {ex.Message})");
            }
        }
    }

    /// <summary>
    /// 只读诊断：查指定 sitemap 组件出现在哪些 Solution 的 solutioncomponent 中。
    /// 出现在 Active Solution = 存在非托管 Active 层（覆盖托管基底）；用于排查导入后 sitemap 未生效。
    /// </summary>
    static void QuerySitemapLayers(ServiceClient service, Guid sitemapId)
    {
        var query = new QueryExpression("solutioncomponent")
        {
            ColumnSet = new ColumnSet("solutionid", "componenttype"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("objectid", ConditionOperator.Equal, sitemapId),
                    new ConditionExpression("componenttype", ConditionOperator.Equal, 62)
                }
            }
        };
        var comps = RetrieveAllPages(service, query);
        Console.WriteLine($"=== sitemap {sitemapId} 的 solutioncomponent 分布（{comps.Count} 条）===");
        var solCache = new Dictionary<Guid, string>();
        foreach (var c in comps)
        {
            var sid = c.GetAttributeValue<EntityReference>("solutionid")?.Id ?? Guid.Empty;
            if (!solCache.TryGetValue(sid, out var sname))
            {
                try { sname = service.Retrieve("solution", sid, new ColumnSet("friendlyname", "uniquename", "ismanaged")).GetAttributeValue<string>("friendlyname"); }
                catch { sname = "(无法读取)"; }
                solCache[sid] = sname;
            }
            Console.WriteLine($"  type={c.GetAttributeValue<OptionSetValue>("componenttype")?.Value}  solution={sname}  ({sid})");
        }
        if (comps.Count == 0) Console.WriteLine("  (任何 Solution 都没有该 sitemap 组件——说明导入未挂载)");
    }

    /// <summary>
    /// 只读诊断：按 Solution 名查最近一次导入日志（importjob.data），列出组件级 warning/error 明细。
    /// 用于排查「导入成功但某组件没生效」（如 AppModuleSiteMap 被跳过）。
    /// </summary>
    static void QueryImportLog(ServiceClient service, string solutionNameKeyword)
    {
        var query = new QueryExpression("importjob")
        {
            ColumnSet = new ColumnSet("solutionname", "startedon", "completedon", "progress", "operationcontext", "data"),
            TopCount = 1
        };
        query.Criteria.AddCondition("solutionname", ConditionOperator.Like, $"%{solutionNameKeyword}%");
        query.AddOrder("createdon", OrderType.Descending);
        var result = service.RetrieveMultiple(query);
        if (result.Entities.Count == 0)
        {
            Console.WriteLine($"  未找到 Solution 名含 '{solutionNameKeyword}' 的导入记录");
            return;
        }
        var job = result.Entities[0];
        Console.WriteLine($"=== {job.GetAttributeValue<string>("solutionname")} | started={job.GetAttributeValue<DateTime?>("startedon"):yyyy-MM-dd HH:mm} | progress={job.GetAttributeValue<double?>("progress"):0.##} | context={job.GetAttributeValue<string>("operationcontext")}");
        var data = job.GetAttributeValue<string>("data");
        if (string.IsNullOrEmpty(data)) { Console.WriteLine("  (无日志数据)"); return; }
        // 调试：输出 XML 根结构与全部标签名统计
        try
        {
            var doc = System.Xml.Linq.XDocument.Parse(data);
            Console.WriteLine("  根节点: " + doc.Root?.Name);
            var tagStat = doc.Descendants().GroupBy(e => e.Name.LocalName).OrderByDescending(g => g.Count()).Take(10);
            foreach (var g in tagStat) Console.WriteLine($"    <{g.Key}> × {g.Count()}");
            // 打印 result 节点的原始形态（前 6 条）
            foreach (var r in doc.Descendants().Where(e => e.Name.LocalName == "result").Take(6))
                Console.WriteLine("  result 样例: " + r.ToString().Substring(0, Math.Min(260, r.ToString().Length)));
            // 非 success 的 result 全部打印
            foreach (var r in doc.Descendants().Where(e => e.Name.LocalName == "result"))
            {
                var rv = r.Attributes().FirstOrDefault(a => a.Name.LocalName == "result")?.Value ?? "";
                if (!string.Equals(rv, "success", StringComparison.OrdinalIgnoreCase))
                    Console.WriteLine($"  ⚠️ 非 success: {r}");
            }
            // sitemap / appmodule 相关节点
            var smNodes = doc.Descendants().Where(e => e.Name.LocalName.ToLowerInvariant().Contains("sitemap") || e.Name.LocalName.ToLowerInvariant().Contains("appmodule")).ToList();
            Console.WriteLine($"  sitemap/appmodule 相关节点数: {smNodes.Count}");
            foreach (var n in smNodes.Take(8))
                Console.WriteLine("    " + n.ToString().Substring(0, Math.Min(300, n.ToString().Length)));
        }
        catch (Exception ex) { Console.WriteLine("  (XML 解析失败: " + ex.Message + ")，前 500 字符: " + data.Substring(0, Math.Min(500, data.Length))); }
        // importjob.data 为 XML：<solutionization> 下逐组件 <genericresult result="success|failure|warning" ...>，错误/告警在 <errorinfo><description>
        var results = System.Text.RegularExpressions.Regex.Matches(data,
            @"<(?<tag>\w*result)\s+(?<attrs>[^>]*)>(?<body>.*?)</\k<tag>>",
            System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        int warn = 0, fail = 0, total = 0;
        foreach (System.Text.RegularExpressions.Match m in results)
        {
            var attrs = m.Groups["attrs"].Value;
            var body = m.Groups["body"].Value;
            var resAttr = System.Text.RegularExpressions.Regex.Match(attrs, "result=\"(?<r>\\w+)\"");
            var r = resAttr.Success ? resAttr.Groups["r"].Value.ToLowerInvariant() : "";
            total++;
            if (r != "warning" && r != "failure") continue;
            var nameAttr = System.Text.RegularExpressions.Regex.Match(attrs, "name=\"(?<n>[^\"]*)\"");
            var desc = System.Text.RegularExpressions.Regex.Match(body, @"<description>(?<d>.*?)</description>", System.Text.RegularExpressions.RegexOptions.Singleline);
            var d = desc.Success ? System.Net.WebUtility.HtmlDecode(desc.Groups["d"].Value.Trim()) : "";
            if (d.Length > 300) d = d.Substring(0, 300) + "...";
            Console.WriteLine($"  [{(r == "failure" ? "❌失败" : "⚠️告警")}] {nameAttr.Groups["n"].Value}: {d}");
            if (r == "failure") fail++; else warn++;
        }
        Console.WriteLine($"  组件结果共 {total} 条：⚠️告警 {warn}，❌失败 {fail}");
    }

    static void TestFcaQuotaApi(ServiceClient service, string accountCode, string useBalance, string proccess, string adjust,
        string contractCode, string orderCode)
    {
        Console.WriteLine($">>> 测试 Custom API: mcs_AdjustFcaQuotaBalance");
        Console.WriteLine($"    入参: accountCode={accountCode}, useBalance={useBalance}, proccess={proccess}, adjust={adjust}, contractCode={contractCode}, orderCode={orderCode}");

        try
        {
            var request = new OrganizationRequest("mcs_AdjustFcaQuotaBalance");
            request["mcs_accountid"] = accountCode;
            request["mcs_usebalance"] = decimal.Parse(useBalance);
            request["mcs_proccess"] = proccess;
            request["mcs_adjust"] = adjust;
            if (!string.IsNullOrWhiteSpace(contractCode))
                request["mcs_contractid"] = contractCode;
            if (!string.IsNullOrWhiteSpace(orderCode))
                request["mcs_orderid"] = orderCode;

            var response = service.Execute(request);

            Console.WriteLine($"  ✅ 调用成功");
            Console.WriteLine($"     是否调整成功(mcs_usedflag): {response["mcs_usedflag"]}");
            Console.WriteLine($"     实际调整金额(mcs_usedbalance): {response["mcs_usedbalance"]}");
            Console.WriteLine($"     调整后余额(mcs_sellerbalance): {response["mcs_sellerbalance"]}");
            Console.WriteLine($"     台账编号(mcs_recordid): {response["mcs_recordid"]}");
            Console.WriteLine($"     失败原因(mcs_failreason): {response["mcs_failreason"]}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ 调用失败: {ex.Message}");
            if (ex.InnerException != null)
                Console.WriteLine($"     Inner: {ex.InnerException.Message}");
        }
    }

    static void TestRecordCreditDetailApi(ServiceClient service, string accountCode, string useBalanceUSD, string useBalanceCNY,
        string proccess, string adjust, string creditType, string contractCode, string orderCode,
        string deliveryNo, string settleId, string settleNo)
    {
        Console.WriteLine($">>> 测试 Custom API: mcs_recordCreditDetail");
        Console.WriteLine($"    入参: accountCode={accountCode}, usebalanceUSD={useBalanceUSD}, usebalanceCNY={useBalanceCNY}, proccess={proccess}, adjust={adjust}, creditType={creditType}, contractCode={contractCode}, orderCode={orderCode}, deliveryNo={deliveryNo}, settleId={settleId}, settleNo={settleNo}");

        try
        {
            var request = new OrganizationRequest("mcs_recordCreditDetail");
            request["mcs_accountid"] = accountCode;
            request["mcs_usebalanceUSD"] = decimal.Parse(useBalanceUSD);
            request["mcs_usebalanceCNY"] = decimal.Parse(useBalanceCNY);
            request["mcs_proccess"] = proccess;
            request["mcs_adjust"] = adjust;
            if (!string.IsNullOrWhiteSpace(creditType))
                request["mcs_creditType"] = creditType;
            if (!string.IsNullOrWhiteSpace(contractCode))
                request["mcs_contractid"] = contractCode;
            if (!string.IsNullOrWhiteSpace(orderCode))
                request["mcs_orderid"] = orderCode;
            if (!string.IsNullOrWhiteSpace(deliveryNo))
                request["mcs_deliveryordid"] = deliveryNo;
            if (!string.IsNullOrWhiteSpace(settleId))
                request["mcs_settle_id"] = settleId;
            if (!string.IsNullOrWhiteSpace(settleNo))
                request["mcs_settle_no"] = settleNo;

            var response = service.Execute(request);

            Console.WriteLine($"  ✅ 调用成功");
            Console.WriteLine($"     是否调整成功(mcs_usedflag): {response["mcs_usedflag"]}");
            Console.WriteLine($"     实际调整金额(mcs_usedbalance): {response["mcs_usedbalance"]}");
            Console.WriteLine($"     调整后余额(mcs_sellerbalance): {response["mcs_sellerbalance"]}");
            Console.WriteLine($"     台账编号(mcs_recordid): {response["mcs_recordid"]}");
            Console.WriteLine($"     失败原因(mcs_failreason): {response["mcs_failreason"]}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ 调用失败: {ex.Message}");
            if (ex.InnerException != null)
                Console.WriteLine($"     Inner: {ex.InnerException.Message}");
        }
    }

    static void TestQueryCreditBalanceApi(ServiceClient service, string accountCode, string contractCode)
    {
        Console.WriteLine($">>> 测试 Custom API: mcs_queryCreditBalance");
        Console.WriteLine($"    入参: accountCode={accountCode}, contractCode={contractCode}");

        try
        {
            var request = new OrganizationRequest("mcs_queryCreditBalance");
            request["mcs_accountid"] = accountCode;
            if (!string.IsNullOrWhiteSpace(contractCode))
                request["mcs_contractid"] = contractCode;

            var response = service.Execute(request);

            Console.WriteLine($"  ✅ 调用成功");
            string[] decimalKeys = { "mcs_credit_limit_usd", "mcs_credit_limit_cny",
                "mcs_sinosure_limit_usd", "mcs_sinosure_limit_cny", "mcs_sinosure_balance_usd", "mcs_sinosure_balance_cny",
                "mcs_sinosure_uplift_limit_usd", "mcs_sinosure_uplift_limit_cny", "mcs_sinosure_netused_usd", "mcs_sinosure_netused_cny",
                "mcs_sinosure_uplift_balance_usd", "mcs_sinosure_uplift_balance_cny",
                "mcs_factory_limit_usd", "mcs_factory_limit_cny", "mcs_factory_balance_usd", "mcs_factory_balance_cny",
                "mcs_risk_exposure_usd", "mcs_risk_exposure_cny", "mcs_signing_occupy_usd", "mcs_signing_occupy_cny" };
            foreach (var key in decimalKeys)
            {
                Console.WriteLine($"     {key}: {response[key]}");
            }
            Console.WriteLine($"     mcs_usedflag: {response["mcs_usedflag"]}");
            Console.WriteLine($"     mcs_failreason: {response["mcs_failreason"]}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ 调用失败: {ex.Message}");
            if (ex.InnerException != null)
                Console.WriteLine($"     Inner: {ex.InnerException.Message}");
        }
    }

    static void TestCofaceSearchApi(ServiceClient service, string companyName, string countryCode)
    {
        Console.WriteLine($">>> 测试 Custom API: mcs_CofaceSearchCompany");
        Console.WriteLine($"    入参: CompanyName={companyName}, CountryCode={countryCode}");

        try
        {
            var request = new OrganizationRequest("mcs_CofaceSearchCompany");
            request["CompanyName"] = companyName;
            request["CountryCode"] = countryCode;

            var response = service.Execute(request);
            var resultJson = response["ResultJson"]?.ToString() ?? "";

            Console.WriteLine($"  ✅ 调用成功");
            Console.WriteLine($"     ResultJson: {resultJson}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ 调用失败: {ex.Message}");
            if (ex.InnerException != null)
                Console.WriteLine($"     Inner: {ex.InnerException.Message}");
        }
    }

    static void TestCofacePlaceOrderApi(ServiceClient service, string creditRecordId)
    {
        Console.WriteLine($">>> 测试 Custom API: mcs_CofacePlaceOrder");
        Console.WriteLine($"    入参: CreditRecordId={creditRecordId}");

        try
        {
            var request = new OrganizationRequest("mcs_CofacePlaceOrder");
            request["CreditRecordId"] = creditRecordId;

            var response = service.Execute(request);
            var resultJson = response["ResultJson"]?.ToString() ?? "";

            Console.WriteLine($"  ✅ 调用成功");
            Console.WriteLine($"     ResultJson: {resultJson}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ 调用失败: {ex.Message}");
            if (ex.InnerException != null)
                Console.WriteLine($"     Inner: {ex.InnerException.Message}");
        }
    }

    static void TestRiskExposureApi(ServiceClient service, string type, string buyerCode, string riskAmount, string signedAmount, string contractCode)
    {
        Console.WriteLine($">>> 测试 Custom API: mcs_CalcContractRiskExposure");
        Console.WriteLine($"    入参: type={type}, buyerCode={buyerCode}, riskAmount={riskAmount}, signedAmount={signedAmount}, contractCode={contractCode}");

        try
        {
            var request = new OrganizationRequest("mcs_CalcContractRiskExposure");
            request["mcs_type"] = type;
            request["mcs_buyercode"] = buyerCode;
            request["mcs_risk_amount"] = decimal.Parse(riskAmount);
            request["mcs_signed_amount"] = decimal.Parse(signedAmount);
            if (!string.IsNullOrWhiteSpace(contractCode))
            {
                request["mcs_contractid"] = contractCode;
            }

            var response = service.Execute(request);
            var resultBuyerCode = response["mcs_buyercode"]?.ToString() ?? "";
            var resultExposure = response["mcs_risk_exposure"] as decimal? ?? 0m;

            Console.WriteLine($"  ✅ 调用成功");
            Console.WriteLine($"     mcs_buyercode: {resultBuyerCode}");
            Console.WriteLine($"     mcs_risk_exposure: {resultExposure:F2}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ 调用失败: {ex.Message}");
            if (ex.InnerException != null)
                Console.WriteLine($"     Inner: {ex.InnerException.Message}");
        }
    }

    static void CheckSolutionCustomApis(ServiceClient service, string solutionName)
    {
        Console.WriteLine($">>> 检查解决方案中的 Custom API: {solutionName}");

        var solutionQuery = new QueryExpression("solution")
        {
            ColumnSet = new ColumnSet("solutionid"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("uniquename", ConditionOperator.Equal, solutionName) }
            }
        };
        var solutionResult = service.RetrieveMultiple(solutionQuery).Entities.FirstOrDefault();
        if (solutionResult == null)
        {
            Console.WriteLine($"  ❌ 未找到解决方案: {solutionName}");
            return;
        }
        Guid solutionId = solutionResult.Id;

        // 先列出该解决方案下所有不常见的 componenttype
        var typeQuery = new QueryExpression("solutioncomponent")
        {
            ColumnSet = new ColumnSet("componenttype"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("solutionid", ConditionOperator.Equal, solutionId) }
            }
        };
        var allComponents = service.RetrieveMultiple(typeQuery).Entities;
        var typeGroups = allComponents
            .Select(c => c.GetAttributeValue<OptionSetValue>("componenttype")?.Value ?? -1)
            .Where(v => v > 10000)
            .GroupBy(v => v)
            .OrderBy(g => g.Key)
            .ToList();
        Console.WriteLine($"  解决方案中 componenttype > 10000 的组件类型:");
        foreach (var g in typeGroups)
        {
            Console.WriteLine($"    Type {g.Key}: {g.Count()} 个");
        }

        // 尝试查找 Custom API 记录本身关联的 solutionid
        var apiQuery = new QueryExpression("customapi")
        {
            ColumnSet = new ColumnSet("customapiid", "uniquename", "name", "displayname", "solutionid"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("uniquename", ConditionOperator.Equal, "mcs_QueryTradeStPayTerm") }
            }
        };
        var apis = service.RetrieveMultiple(apiQuery).Entities;
        Console.WriteLine($"  找到 {apis.Count} 个 mcs_QueryTradeStPayTerm:");
        foreach (var api in apis)
        {
            var apiSolutionId = api.GetAttributeValue<Guid>("solutionid");
            Console.WriteLine($"    ID={api.Id}, SolutionId={apiSolutionId}, Match={apiSolutionId == solutionId}");

            // 查询该 Custom API 是否在目标解决方案的 solutioncomponent 中
            var componentQuery2 = new QueryExpression("solutioncomponent")
            {
                ColumnSet = new ColumnSet("solutioncomponentid", "componenttype", "objectid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("solutionid", ConditionOperator.Equal, solutionId),
                        new ConditionExpression("objectid", ConditionOperator.Equal, api.Id)
                    }
                }
            };
            var matchedComponents = service.RetrieveMultiple(componentQuery2).Entities;
            Console.WriteLine($"    -> Custom API 在 {solutionName} 中命中 {matchedComponents.Count} 条");
            foreach (var mc in matchedComponents)
            {
                var ctype = mc.GetAttributeValue<OptionSetValue>("componenttype")?.Value ?? -1;
                Console.WriteLine($"       ComponentType={ctype}, ID={mc.Id}");
            }

            // 查询该 Custom API 的参数和响应属性
            var paramQuery = new QueryExpression("customapirequestparameter")
            {
                ColumnSet = new ColumnSet("customapirequestparameterid", "uniquename"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("customapiid", ConditionOperator.Equal, api.Id) }
                }
            };
            var params_ = service.RetrieveMultiple(paramQuery).Entities;
            Console.WriteLine($"    -> 请求参数: {params_.Count} 个");
            foreach (var p in params_)
            {
                var pid = p.Id;
                var pCompQuery = new QueryExpression("solutioncomponent")
                {
                    ColumnSet = new ColumnSet("solutioncomponentid", "componenttype"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("solutionid", ConditionOperator.Equal, solutionId),
                            new ConditionExpression("objectid", ConditionOperator.Equal, pid)
                        }
                    }
                };
                var pComps = service.RetrieveMultiple(pCompQuery).Entities;
                Console.WriteLine($"       {p.GetAttributeValue<string>("uniquename")}: 在 {solutionName} 中命中 {pComps.Count} 条");
            }

            var propQuery = new QueryExpression("customapiresponseproperty")
            {
                ColumnSet = new ColumnSet("customapiresponsepropertyid", "uniquename"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("customapiid", ConditionOperator.Equal, api.Id) }
                }
            };
            var props = service.RetrieveMultiple(propQuery).Entities;
            Console.WriteLine($"    -> 响应属性: {props.Count} 个");
            foreach (var p in props)
            {
                var pid = p.Id;
                var pCompQuery = new QueryExpression("solutioncomponent")
                {
                    ColumnSet = new ColumnSet("solutioncomponentid", "componenttype"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("solutionid", ConditionOperator.Equal, solutionId),
                            new ConditionExpression("objectid", ConditionOperator.Equal, pid)
                        }
                    }
                };
                var pComps = service.RetrieveMultiple(pCompQuery).Entities;
                Console.WriteLine($"       {p.GetAttributeValue<string>("uniquename")}: 在 {solutionName} 中命中 {pComps.Count} 条");
            }
        }

        // 验证 componenttype 10023/10024/10025 是否对应 Custom API 相关记录
        var verifyQuery = new QueryExpression("solutioncomponent")
        {
            ColumnSet = new ColumnSet("objectid", "componenttype"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("solutionid", ConditionOperator.Equal, solutionId),
                    new ConditionExpression("componenttype", ConditionOperator.In, new[] { 10023, 10024, 10025 })
                }
            },
            TopCount = 5
        };
        var verifyComponents = service.RetrieveMultiple(verifyQuery).Entities;
        Console.WriteLine("  验证 componenttype 样本:");
        var sampleIds = new List<Guid>();
        foreach (var c in verifyComponents)
        {
            var objId = c.GetAttributeValue<Guid>("objectid");
            var ctype = c.GetAttributeValue<OptionSetValue>("componenttype")?.Value ?? -1;
            Console.WriteLine($"    ObjectId={objId}, ComponentType={ctype}");
            sampleIds.Add(objId);
        }

        // 分别在 customapi / customapirequestparameter / customapiresponseproperty 中查找
        foreach (var entityName in new[] { "customapi", "customapirequestparameter", "customapiresponseproperty" })
        {
            var q = new QueryExpression(entityName)
            {
                ColumnSet = new ColumnSet(entityName + "id"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression(entityName + "id", ConditionOperator.In, sampleIds.ToArray()) }
                }
            };
            var count = service.RetrieveMultiple(q).Entities.Count;
            Console.WriteLine($"    -> 在 {entityName} 中命中 {count} 条");
        }
    }

    static void CheckSolutionWebResources(ServiceClient service, string solutionName, string? prefix)
    {
        Console.WriteLine($">>> 检查解决方案中的 WebResource: {solutionName}");

        // 1. 查解决方案 ID
        var solutionQuery = new QueryExpression("solution")
        {
            ColumnSet = new ColumnSet("solutionid", "friendlyname"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("uniquename", ConditionOperator.Equal, solutionName) }
            }
        };
        var solutionResult = service.RetrieveMultiple(solutionQuery).Entities.FirstOrDefault();
        if (solutionResult == null)
        {
            Console.WriteLine($"  ❌ 未找到解决方案: {solutionName}");
            return;
        }
        Guid solutionId = solutionResult.Id;
        Console.WriteLine($"  解决方案: {solutionResult.GetAttributeValue<string>("friendlyname")} ({solutionId})");

        // 2. 查该解决方案下的 WebResource 组件（componenttype = 61）
        var componentQuery = new QueryExpression("solutioncomponent")
        {
            ColumnSet = new ColumnSet("objectid"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("solutionid", ConditionOperator.Equal, solutionId),
                    new ConditionExpression("componenttype", ConditionOperator.Equal, 61)
                }
            }
        };
        var components = service.RetrieveMultiple(componentQuery).Entities;
        if (components.Count == 0)
        {
            Console.WriteLine($"  该解决方案中没有 WebResource 组件");
            return;
        }

        var webResourceIds = components.Select(c => c.GetAttributeValue<Guid>("objectid")).ToList();
        Console.WriteLine($"  找到 {webResourceIds.Count} 个 WebResource 组件");

        // 3. 查 WebResource 详细信息
        var wrQuery = new QueryExpression("webresource")
        {
            ColumnSet = new ColumnSet("webresourceid", "name", "displayname", "webresourcetype"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("webresourceid", ConditionOperator.In, webResourceIds.ToArray()) }
            }
        };
        var webResources = service.RetrieveMultiple(wrQuery).Entities;

        // 4. 按前缀过滤并输出
        var filtered = webResources
            .Where(w => prefix == null || (w.GetAttributeValue<string>("name") ?? "").StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(w => w.GetAttributeValue<string>("name"))
            .ToList();

        Console.WriteLine($"  符合前缀过滤 '{prefix ?? "(无)"}' 的 WebResource: {filtered.Count} 个");
        foreach (var wr in filtered)
        {
            var name = wr.GetAttributeValue<string>("name") ?? "";
            var displayName = wr.GetAttributeValue<string>("displayname") ?? "";
            var typeCode = wr.GetAttributeValue<OptionSetValue>("webresourcetype")?.Value ?? -1;
            Console.WriteLine($"    - {name} (Type={typeCode}, Display={displayName})");
        }
    }

    static void CheckWebResourceLayer(ServiceClient service, string webResourceName)
    {
        Console.WriteLine($">>> 检查 WebResource 的 Solution 归属: {webResourceName}");

        // 1. 查询 WebResource
        var wrQuery = new QueryExpression("webresource")
        {
            ColumnSet = new ColumnSet("webresourceid", "name", "ismanaged", "modifiedon"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("name", ConditionOperator.Equal, webResourceName) }
            }
        };
        var wrResult = service.RetrieveMultiple(wrQuery).Entities.FirstOrDefault();
        if (wrResult == null)
        {
            Console.WriteLine($"  ❌ 未找到 WebResource: {webResourceName}");
            return;
        }
        var wrId = wrResult.Id;
        var isManaged = wrResult.GetAttributeValue<bool?>("ismanaged") ?? false;
        var modifiedOn = wrResult.GetAttributeValue<DateTime?>("modifiedon");
        Console.WriteLine($"  WebResource ID: {wrId}");
        Console.WriteLine($"  是否托管 (ismanaged): {isManaged}");
        Console.WriteLine($"  最近修改时间: {(modifiedOn.HasValue ? modifiedOn.Value.ToString("yyyy-MM-dd HH:mm:ss") : "N/A")}");
        Console.WriteLine($"  注意：无法通过 Organization Service 准确判断 Active Layer，请以 D365 Maker Portal 的【解决方案层】界面为准。");

        // 2. 查询该 WebResource 属于哪些 Solution
        var solutionComponentQuery = new QueryExpression("solutioncomponent")
        {
            ColumnSet = new ColumnSet("solutionid"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("componenttype", ConditionOperator.Equal, 61),
                    new ConditionExpression("objectid", ConditionOperator.Equal, wrId)
                }
            }
        };
        var allComponents = service.RetrieveMultiple(solutionComponentQuery).Entities;
        var solutionIds = allComponents
            .Select(c => c.GetAttributeValue<Microsoft.Xrm.Sdk.EntityReference>("solutionid")?.Id ?? Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        Console.WriteLine($"  该 WebResource 出现在 {solutionIds.Count} 个 Solution 中:");

        foreach (var sid in solutionIds)
        {
            var solutionQuery = new QueryExpression("solution")
            {
                ColumnSet = new ColumnSet("uniquename", "friendlyname", "ismanaged"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("solutionid", ConditionOperator.Equal, sid) }
                }
            };
            var solution = service.RetrieveMultiple(solutionQuery).Entities.FirstOrDefault();
            if (solution != null)
            {
                var uniqueName = solution.GetAttributeValue<string>("uniquename") ?? "";
                var friendlyName = solution.GetAttributeValue<string>("friendlyname") ?? "";
                var solutionManaged = solution.GetAttributeValue<bool?>("ismanaged") ?? false;
                Console.WriteLine($"     - {uniqueName} ({friendlyName}) [托管: {solutionManaged}]");
            }
        }
    }

    /// <summary>
    /// 判断指定属性名是否为 Active Layer 中常见无意义元数据字段。
    /// </summary>
    static bool IsExcludedActiveLayerProperty(string propName)
    {
        return propName.Equals("displaymask", StringComparison.OrdinalIgnoreCase) ||
               propName.Equals("createdon", StringComparison.OrdinalIgnoreCase) ||
               propName.Equals("modifiedon", StringComparison.OrdinalIgnoreCase) ||
               propName.Equals("attributetypeid", StringComparison.OrdinalIgnoreCase) ||
               propName.Equals("attributelogicaltypeid", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 通过官方 msdyn_componentlayer 虚拟实体检测 WebResource 是否存在 Active Layer 自定义。
    /// 参考：PowerDataOps Test-XrmComponentCustomization / Microsoft Docs msdyn_componentlayer。
    /// </summary>
    static void CheckWebResourceActiveLayer(ServiceClient service, string webResourceName)
    {
        Console.WriteLine($">>> 检测 WebResource 的 Active Layer: {webResourceName}");

        // 1. 查询 WebResource
        var wrQuery = new QueryExpression("webresource")
        {
            ColumnSet = new ColumnSet("webresourceid", "name", "ismanaged"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("name", ConditionOperator.Equal, webResourceName) }
            }
        };
        var wrResult = service.RetrieveMultiple(wrQuery).Entities.FirstOrDefault();
        if (wrResult == null)
        {
            Console.WriteLine($"  ❌ 未找到 WebResource: {webResourceName}");
            return;
        }
        var wrId = wrResult.Id;
        Console.WriteLine($"  WebResource ID: {wrId}");

        // 2. 查询 msdyn_componentlayer 虚拟实体中的 Active Layer
        // 官方文档：https://learn.microsoft.com/power-apps/developer/data-platform/reference/entities/msdyn_componentlayer
        var layerQuery = new QueryExpression("msdyn_componentlayer")
        {
            ColumnSet = new ColumnSet(
                "msdyn_componentlayerid",
                "msdyn_name",
                "msdyn_solutionname",
                "msdyn_solutioncomponentname",
                "msdyn_componentid",
                "msdyn_order",
                "msdyn_publishername",
                "msdyn_changes"
            ),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("msdyn_solutionname", ConditionOperator.Equal, "Active"),
                    new ConditionExpression("msdyn_solutioncomponentname", ConditionOperator.Equal, "WebResource"),
                    new ConditionExpression("msdyn_componentid", ConditionOperator.Equal, wrId.ToString())
                }
            }
        };

        var layers = service.RetrieveMultiple(layerQuery).Entities;
        if (layers.Count == 0)
        {
            Console.WriteLine($"  ✅ 未找到 Active Layer：该 WebResource 在 Active Solution 中无自定义层。");
            return;
        }

        Console.WriteLine($"  找到 {layers.Count} 条 Active Layer 记录:");
        foreach (var layer in layers)
        {
            var layerId = layer.Id;
            var layerName = layer.GetAttributeValue<string>("msdyn_name") ?? "";
            var solutionName = layer.GetAttributeValue<string>("msdyn_solutionname") ?? "";
            var componentName = layer.GetAttributeValue<string>("msdyn_solutioncomponentname") ?? "";
            // msdyn_componentid 官方文档标注为 String，但实际可能返回 Guid 或 String
            var componentId = layer.GetAttributeValue<object>("msdyn_componentid")?.ToString() ?? "";
            var order = layer.GetAttributeValue<int?>("msdyn_order") ?? -1;
            var publisher = layer.GetAttributeValue<string>("msdyn_publishername") ?? "";
            var changesJson = layer.GetAttributeValue<string>("msdyn_changes") ?? "";

            Console.WriteLine($"    Layer ID: {layerId}");
            Console.WriteLine($"    Name: {layerName}");
            Console.WriteLine($"    Solution: {solutionName}");
            Console.WriteLine($"    Component: {componentName} ({componentId})");
            Console.WriteLine($"    Order: {order}");
            Console.WriteLine($"    Publisher: {publisher}");

            if (string.IsNullOrWhiteSpace(changesJson))
            {
                Console.WriteLine($"    Changes: (空)");
                continue;
            }

            Console.WriteLine($"    Raw Changes: {changesJson.Substring(0, Math.Min(500, changesJson.Length))}...");

            // 3. 解析 changes JSON，提取有实际意义的变更属性
            try
            {
                using var doc = JsonDocument.Parse(changesJson);
                var changedProperties = new List<string>();
                if (doc.RootElement.TryGetProperty("Attributes", out var attributes))
                {
                    if (attributes.ValueKind == JsonValueKind.Array)
                    {
                        // msdyn_changes 中 Attributes 可能是 [{"Key":"...","Value":"..."}, ...] 数组
                        foreach (var item in attributes.EnumerateArray())
                        {
                            if (item.TryGetProperty("Key", out var keyProp) && keyProp.ValueKind == JsonValueKind.String)
                            {
                                var propName = keyProp.GetString() ?? "";
                                if (IsExcludedActiveLayerProperty(propName)) continue;
                                changedProperties.Add(propName);
                            }
                        }
                    }
                    else if (attributes.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in attributes.EnumerateObject())
                        {
                            var propName = prop.Name;
                            if (IsExcludedActiveLayerProperty(propName)) continue;
                            changedProperties.Add(propName);
                        }
                    }
                }
                else if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        changedProperties.Add(prop.Name);
                    }
                }

                if (changedProperties.Count == 0)
                {
                    Console.WriteLine($"    ✅ 无有效 Active Layer 变更（仅包含时间戳/元数据字段）");
                }
                else
                {
                    Console.WriteLine($"    ⚠️ 检测到 {changedProperties.Count} 个有效 Active Layer 变更属性:");
                    foreach (var p in changedProperties)
                    {
                        Console.WriteLine($"      - {p}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    ⚠️ 无法解析 Changes JSON: {ex.Message}");
            }
        }
    }

    static void TestRetrieveSolutionMetadata(ServiceClient service, string webResourceName)
    {
        Console.WriteLine($">>> 测试 RetrieveSolutionMetadataForComponent: {webResourceName}");
        var wrQuery = new QueryExpression("webresource")
        {
            ColumnSet = new ColumnSet("webresourceid"),
            Criteria = new FilterExpression { Conditions = { new ConditionExpression("name", ConditionOperator.Equal, webResourceName) } }
        };
        var wr = service.RetrieveMultiple(wrQuery).Entities.FirstOrDefault();
        if (wr == null) { Console.WriteLine("  未找到 WebResource"); return; }
        var wrId = wr.Id;
        Console.WriteLine($"  WebResource ID: {wrId}");

        try
        {
            var req = new OrganizationRequest("RetrieveSolutionMetadataForComponent");
            req["SolutionComponentName"] = "WebResource";
            req["ColumnNames"] = new string[] { "webresourceid" };
            req["ColumnValues"] = new string[] { wrId.ToString() };
            var resp = service.Execute(req);
            Console.WriteLine("  Response keys:");
            foreach (var key in resp.Results.Keys)
            {
                Console.WriteLine($"    {key}: {resp.Results[key]?.GetType().Name}");
            }
            if (resp.Results.Contains("EntityCollection") && resp["EntityCollection"] is EntityCollection ec)
            {
                Console.WriteLine($"  找到 {ec.Entities.Count} 条 Solution 记录:");
                foreach (var e in ec.Entities)
                {
                    Console.WriteLine("  ---");
                    foreach (var attr in e.Attributes.OrderBy(a => a.Key))
                    {
                        var value = attr.Value;
                        if (value is EntityReference er) value = $"ER({er.LogicalName},{er.Id})";
                        Console.WriteLine($"    {attr.Key}: {value}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ERROR: {ex.Message}");
            if (ex.InnerException != null) Console.WriteLine($"  INNER: {ex.InnerException.Message}");
        }
    }

    static void CheckSolutionVersion(ServiceClient service, string solutionName)
    {
        Console.WriteLine($">>> 查询 Solution 版本信息: {solutionName}");

        var query = new QueryExpression("solution")
        {
            ColumnSet = new ColumnSet("solutionid", "uniquename", "friendlyname", "version", "ismanaged", "installedon", "modifiedon"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("uniquename", ConditionOperator.Equal, solutionName) }
            }
        };
        var result = service.RetrieveMultiple(query).Entities;
        if (result.Count == 0)
        {
            Console.WriteLine($"  ❌ 未找到 Solution: {solutionName}");
            return;
        }

        foreach (var s in result)
        {
            var uniqueName = s.GetAttributeValue<string>("uniquename") ?? "";
            var friendlyName = s.GetAttributeValue<string>("friendlyname") ?? "";
            var version = s.GetAttributeValue<string>("version") ?? "";
            var isManaged = s.GetAttributeValue<bool?>("ismanaged") ?? false;
            var installedOn = s.GetAttributeValue<DateTime?>("installedon");
            var modifiedOn = s.GetAttributeValue<DateTime?>("modifiedon");

            Console.WriteLine($"  Solution: {uniqueName} ({friendlyName})");
            Console.WriteLine($"    版本号: {version}");
            Console.WriteLine($"    是否托管: {isManaged}");
            Console.WriteLine($"    安装时间: {(installedOn.HasValue ? installedOn.Value.ToString("yyyy-MM-dd HH:mm:ss") : "N/A")}");
            Console.WriteLine($"    修改时间: {(modifiedOn.HasValue ? modifiedOn.Value.ToString("yyyy-MM-dd HH:mm:ss") : "N/A")}");
        }
    }

    /// <summary>
    /// 通用发版 Solution 归属只读检查：按清单 JSON 分组核对组件是否在应属 Solution 中。
    /// Solution 归属固定映射（开发手册 4.4）：
    ///   实体/字段/App Action → 清单 entitySolution；WebResource → McsWebResource；
    ///   Plugin（Assembly/Type/Step）→ McsPlugin；Custom API → McsCustomAPI。
    /// 全程只做 RetrieveMultiple / RetrieveEntityRequest 只读查询，零写操作。
    /// </summary>
    static void CheckRelease(ServiceClient service, string manifestPath, bool withFields)
    {
        if (!File.Exists(manifestPath))
        {
            Console.WriteLine($"  ❌ 清单文件不存在: {manifestPath}");
            return;
        }

        ReleaseManifest? manifest;
        try
        {
            var json = File.ReadAllText(manifestPath);
            manifest = JsonSerializer.Deserialize<ReleaseManifest>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ 清单 JSON 解析失败: {ex.Message}");
            return;
        }
        if (manifest == null)
        {
            Console.WriteLine("  ❌ 清单内容为空或格式不正确");
            return;
        }

        Console.WriteLine($"═══ 发版自检报告：{manifest.Name ?? "(未命名)"} ═══");
        if (withFields) Console.WriteLine("（--with-fields：实体 mcs_ 自定义字段一并核对）");

        int ok = 0, missing = 0, warn = 0;
        var solutionIdCache = new Dictionary<string, Guid?>(StringComparer.OrdinalIgnoreCase);

        // 解析 Solution ID（带缓存，同一 Solution 只查一次）；查不到输出 ⚠️ 并返回 null
        Guid? GetSolutionId(string solutionName)
        {
            if (!solutionIdCache.TryGetValue(solutionName, out var id))
            {
                id = ResolveSolutionId(service, solutionName);
                solutionIdCache[solutionName] = id;
                if (id == null)
                {
                    warn++;
                    Console.WriteLine($"  ⚠️ Solution {solutionName} 在环境中不存在，跳过该组");
                }
            }
            return id;
        }

        var entitySolution = manifest.EntitySolution;
        bool entitySolutionDeclared = !string.IsNullOrWhiteSpace(entitySolution);

        // ── 实体 → entitySolution（--with-fields 时逐字段核对）──
        if (manifest.Entities?.Count > 0)
        {
            Console.WriteLine($"【实体 → {entitySolution ?? "(未声明)"}】");
            if (!entitySolutionDeclared)
            {
                warn++;
                Console.WriteLine("  ⚠️ 清单未声明 entitySolution，跳过实体组");
            }
            else
            {
                var sid = GetSolutionId(entitySolution!);
                if (sid != null)
                {
                    foreach (var entityName in manifest.Entities)
                    {
                        var entityId = FindComponentIdByName(service, "entity", "name", entityName);
                        if (entityId == null)
                        {
                            warn++;
                            Console.WriteLine($"  ⚠️ {entityName} —— 环境中不存在该实体");
                            continue;
                        }
                        // rootcomponentbehavior：0=含全部子组件（字段等隐式随包），1=不含子组件，2=仅外壳
                        var rcb = GetEntityRootComponentBehavior(service, sid.Value, entityId.Value);
                        if (rcb == null)
                        {
                            missing++;
                            Console.WriteLine($"  ❌ {entityName} —— 不在 {entitySolution} 中，请手动添加");
                            continue;
                        }
                        if (!withFields)
                        {
                            ok++;
                            Console.WriteLine($"  ✅ {entityName}");
                            continue;
                        }
                        if (rcb.Value == 0)
                        {
                            ok++;
                            Console.WriteLine($"  ✅ {entityName}（含全部子组件）");
                            continue;
                        }

                        // 逐字段核对：全部 mcs_ 自定义字段（componenttype=2 的 objectid 即字段 MetadataId）
                        var fields = GetCustomFields(service, entityName);
                        var missingFields = fields.Where(f => !IsComponentInSolution(service, sid.Value, f.MetadataId)).ToList();
                        if (missingFields.Count == 0)
                        {
                            ok++;
                            Console.WriteLine($"  ✅ {entityName}（{fields.Count} 个 mcs_ 自定义字段均在包内）");
                        }
                        else
                        {
                            Console.WriteLine($"     {entityName} —— 实体在包内，但 {missingFields.Count} 个自定义字段未入包：");
                            foreach (var f in missingFields)
                            {
                                missing++;
                                Console.WriteLine($"  ❌ {entityName}.{f.LogicalName} —— 字段不在 {entitySolution} 中，请手动添加");
                            }
                        }
                    }
                }
            }
        }

        // ── WebResource → McsWebResource ──
        if (manifest.WebResources?.Count > 0)
        {
            Console.WriteLine("【WebResource → McsWebResource】");
            var sid = GetSolutionId("McsWebResource");
            if (sid != null)
            {
                foreach (var wrName in manifest.WebResources)
                {
                    var wrId = FindComponentIdByName(service, "webresource", "name", wrName);
                    if (wrId == null)
                    {
                        warn++;
                        Console.WriteLine($"  ⚠️ {wrName} —— 环境中不存在该 WebResource");
                    }
                    else if (IsComponentInSolution(service, sid.Value, wrId.Value))
                    {
                        ok++;
                        Console.WriteLine($"  ✅ {wrName}");
                    }
                    else
                    {
                        missing++;
                        Console.WriteLine($"  ❌ {wrName} —— 不在 McsWebResource 中，请手动添加");
                    }
                }
            }
        }

        // ── Plugin → McsPlugin（Assembly / Type / Step 三层核对）──
        if (manifest.PluginTypes?.Count > 0)
        {
            Console.WriteLine("【Plugin → McsPlugin】");
            var sid = GetSolutionId("McsPlugin");
            if (sid != null)
            {
                var reportedAssemblies = new HashSet<Guid>();
                foreach (var keyword in manifest.PluginTypes)
                {
                    var typeQuery = new QueryExpression("plugintype")
                    {
                        ColumnSet = new ColumnSet("typename", "pluginassemblyid"),
                        Criteria = new FilterExpression
                        {
                            Conditions = { new ConditionExpression("typename", ConditionOperator.Like, $"%{keyword}%") }
                        }
                    };
                    var types = service.RetrieveMultiple(typeQuery).Entities;
                    if (types.Count == 0)
                    {
                        warn++;
                        Console.WriteLine($"  ⚠️ {keyword} —— 环境中不存在该 PluginType");
                        continue;
                    }

                    foreach (var t in types)
                    {
                        var typeName = t.GetAttributeValue<string>("typename") ?? keyword;

                        // a. 所属 Assembly（同名只报一次）
                        var asmRef = t.GetAttributeValue<EntityReference>("pluginassemblyid");
                        if (asmRef != null && reportedAssemblies.Add(asmRef.Id))
                        {
                            var asmName = asmRef.Name ?? asmRef.Id.ToString();
                            if (IsComponentInSolution(service, sid.Value, asmRef.Id))
                            {
                                ok++;
                                Console.WriteLine($"  ✅ {asmName}（Assembly）");
                            }
                            else
                            {
                                missing++;
                                Console.WriteLine($"  ❌ {asmName}（Assembly）—— 不在 McsPlugin 中，请手动添加");
                            }
                        }

                        // b. Type 本身（McsPlugin 惯例：Assembly+Step 入包即可，Type 不单独入包，仅作备注）
                        bool typeIn = IsComponentInSolution(service, sid.Value, t.Id);

                        // c. 全部 Step
                        var steps = GetPluginSteps(service, t.Id);
                        var missingSteps = steps.Where(s => !IsComponentInSolution(service, sid.Value, s.Id)).ToList();

                        if (missingSteps.Count == 0)
                        {
                            ok++;
                            Console.WriteLine($"  ✅ {typeName}（{steps.Count} 个 Step 均在包内）");
                            if (!typeIn)
                            {
                                Console.WriteLine($"     ℹ️ Type 未单独入包（McsPlugin 惯例：Assembly+Step 即可）");
                            }
                        }
                        else
                        {
                            if (!typeIn)
                            {
                                Console.WriteLine($"     ℹ️ {typeName} Type 未单独入包（McsPlugin 惯例：Assembly+Step 即可）");
                            }
                            foreach (var step in missingSteps)
                            {
                                missing++;
                                var stepName = step.GetAttributeValue<string>("name") ?? "";
                                var messageName = step.GetAttributeValue<EntityReference>("sdkmessageid")?.Name ?? "";
                                var stepEntity = step.GetAttributeValue<EntityReference>("sdkmessagefilterid")?.Name ?? "";
                                Console.WriteLine($"  ❌ {typeName} —— Step「{stepName}」({step.Id}) [{messageName}/{stepEntity}] 不在 McsPlugin 中，请手动添加");
                            }
                        }
                    }
                }
            }
        }

        // ── Custom API → McsCustomAPI（本体 + 请求参数 + 响应属性）──
        if (manifest.CustomApis?.Count > 0)
        {
            Console.WriteLine("【Custom API → McsCustomAPI】");
            var sid = GetSolutionId("McsCustomAPI");
            if (sid != null)
            {
                foreach (var apiName in manifest.CustomApis)
                {
                    var apiId = FindComponentIdByName(service, "customapi", "uniquename", apiName);
                    if (apiId == null)
                    {
                        warn++;
                        Console.WriteLine($"  ⚠️ {apiName} —— 环境中不存在该 Custom API");
                        continue;
                    }

                    bool bodyIn = IsComponentInSolution(service, sid.Value, apiId.Value);
                    var missingChildren = new List<string>();
                    int paramCount = 0, propCount = 0;
                    foreach (var (childTable, childLabel) in new[]
                    {
                        ("customapirequestparameter", "请求参数"),
                        ("customapiresponseproperty", "响应属性")
                    })
                    {
                        var childQuery = new QueryExpression(childTable)
                        {
                            ColumnSet = new ColumnSet("name"),
                            Criteria = new FilterExpression
                            {
                                Conditions = { new ConditionExpression("customapiid", ConditionOperator.Equal, apiId.Value) }
                            }
                        };
                        var children = service.RetrieveMultiple(childQuery).Entities;
                        if (childLabel == "请求参数") paramCount = children.Count; else propCount = children.Count;
                        foreach (var c in children)
                        {
                            if (!IsComponentInSolution(service, sid.Value, c.Id))
                            {
                                missingChildren.Add($"{childLabel}「{c.GetAttributeValue<string>("name") ?? c.Id.ToString()}」");
                            }
                        }
                    }

                    if (bodyIn && missingChildren.Count == 0)
                    {
                        ok++;
                        Console.WriteLine($"  ✅ {apiName}（含 {paramCount} 个请求参数、{propCount} 个响应属性）");
                    }
                    else
                    {
                        if (!bodyIn)
                        {
                            missing++;
                            Console.WriteLine($"  ❌ {apiName} —— 不在 McsCustomAPI 中，请手动添加");
                        }
                        foreach (var child in missingChildren)
                        {
                            missing++;
                            Console.WriteLine($"  ❌ {apiName} —— {child} 不在 McsCustomAPI 中，请手动添加");
                        }
                    }
                }
            }
        }

        // ── App Action → entitySolution ──
        if (manifest.AppActions?.Count > 0)
        {
            Console.WriteLine($"【App Action → {entitySolution ?? "(未声明)"}】");
            if (!entitySolutionDeclared)
            {
                warn++;
                Console.WriteLine("  ⚠️ 清单未声明 entitySolution，跳过 App Action 组");
            }
            else
            {
                var sid = GetSolutionId(entitySolution!);
                if (sid != null)
                {
                    foreach (var actionName in manifest.AppActions)
                    {
                        var actionId = FindComponentIdByName(service, "appaction", "uniquename", actionName);
                        if (actionId == null)
                        {
                            warn++;
                            Console.WriteLine($"  ⚠️ {actionName} —— 环境中不存在该 App Action");
                        }
                        else if (IsComponentInSolution(service, sid.Value, actionId.Value))
                        {
                            ok++;
                            Console.WriteLine($"  ✅ {actionName}");
                        }
                        else
                        {
                            missing++;
                            Console.WriteLine($"  ❌ {actionName} —— 不在 {entitySolution} 中，请手动添加");
                        }
                    }
                }
            }
        }

        // ── App Action 自动发现（兜底）：按清单实体反查环境中全部按钮，不依赖清单手写 appActions ──
        // 背景：App Action 不是实体子组件，加实体进 Solution 不会自动带入，清单漏写即漏发。
        if (manifest.Entities?.Count > 0)
        {
            Console.WriteLine($"【App Action 自动发现 → {entitySolution ?? "(未声明)"}】");
            if (!entitySolutionDeclared)
            {
                warn++;
                Console.WriteLine("  ⚠️ 清单未声明 entitySolution，跳过 App Action 自动发现");
            }
            else
            {
                var sid = GetSolutionId(entitySolution!);
                if (sid != null)
                {
                    // 已在 appActions 组核对过的跳过，避免重复报告
                    var declared = new HashSet<string>(manifest.AppActions ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
                    var reported = new HashSet<Guid>();
                    // 全量收集（含清单已声明的，供重复副本比对；报告时跳过已声明项）
                    var discoveredList = new List<(Guid Id, string UniqueName, int? StateCode, string? JsFunction, string Entity, bool InSolution, bool Declared)>();
                    foreach (var entityName in manifest.Entities)
                    {
                        foreach (var action in GetAppActionsForEntity(service, entityName))
                        {
                            if (!reported.Add(action.Id)) continue;
                            discoveredList.Add((action.Id, action.UniqueName, action.StateCode, action.JsFunction, entityName,
                                IsComponentInSolution(service, sid.Value, action.Id), declared.Contains(action.UniqueName)));
                        }
                    }
                    int discovered = 0;
                    foreach (var a in discoveredList)
                    {
                        if (a.Declared) continue; // 上方 appActions 组已核对
                        discovered++;
                        if (a.StateCode != 0)
                        {
                            Console.WriteLine($"  ℹ️ {a.UniqueName}（{a.Entity}）—— 已停用，跳过核对");
                            continue;
                        }
                        if (a.InSolution)
                        {
                            ok++;
                            Console.WriteLine($"  ✅ {a.UniqueName}（{a.Entity}，自动发现）");
                            continue;
                        }
                        if (a.JsFunction != null && discoveredList.Any(b => b.Id != a.Id && b.Entity == a.Entity && b.JsFunction == a.JsFunction && b.InSolution))
                        {
                            Console.WriteLine($"  ℹ️ {a.UniqueName}（{a.Entity}）—— 与包内按钮 JS 函数相同（{a.JsFunction}），视为重复副本，跳过");
                            continue;
                        }
                        missing++;
                        Console.WriteLine($"  ❌ {a.UniqueName}（{a.Entity}，自动发现）—— 不在 {entitySolution} 中，请手动添加");
                    }
                    if (discovered == 0)
                    {
                        Console.WriteLine("  ℹ️ 清单实体关联的按钮均已在清单 appActions 中核对，无遗漏");
                    }
                }
            }
        }

        Console.WriteLine($"═══ 汇总：✅ {ok} / ❌ {missing} / ⚠️ {warn} ═══");
        if (missing > 0)
        {
            Console.WriteLine($"═══ 有 {missing} 项缺失 —— 补齐后请重跑 check-release ═══");
        }
        else if (warn > 0)
        {
            Console.WriteLine($"═══ 无缺失，但有 {warn} 项警告，请确认清单 ═══");
        }
        else
        {
            Console.WriteLine("═══ 全部通过，可以等待发布 ═══");
        }
    }

    /// <summary>
    /// 按 uniquename 查询 Solution ID；查不到返回 null。
    /// </summary>
    static Guid? ResolveSolutionId(ServiceClient service, string solutionUniqueName)
    {
        var query = new QueryExpression("solution")
        {
            ColumnSet = new ColumnSet("solutionid"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("uniquename", ConditionOperator.Equal, solutionUniqueName) }
            }
        };
        return service.RetrieveMultiple(query).Entities.FirstOrDefault()?.Id;
    }

    /// <summary>
    /// 按名称字段精确查询组件 ID；查不到返回 null。
    /// </summary>
    static Guid? FindComponentIdByName(ServiceClient service, string tableName, string nameField, string name)
    {
        var query = new QueryExpression(tableName)
        {
            ColumnSet = new ColumnSet(tableName + "id"),
            TopCount = 1,
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression(nameField, ConditionOperator.Equal, name) }
            }
        };
        return service.RetrieveMultiple(query).Entities.FirstOrDefault()?.Id;
    }

    /// <summary>
    /// 按 contextentity 反查指定实体关联的全部 App Action（现代命令栏按钮，只读）。
    /// App Action 不是实体子组件，不会随实体自动带入 Solution，需单独核对。
    /// </summary>
    static List<(Guid Id, string UniqueName, int? StateCode, string? JsFunction)> GetAppActionsForEntity(ServiceClient service, string entityName)
    {
        var entityId = FindComponentIdByName(service, "entity", "name", entityName);
        if (entityId == null) return new List<(Guid Id, string UniqueName, int? StateCode, string? JsFunction)>();
        var query = new QueryExpression("appaction")
        {
            ColumnSet = new ColumnSet("uniquename", "statecode", "onclickeventjavascriptfunctionname"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("contextentity", ConditionOperator.Equal, entityId.Value) }
            }
        };
        return service.RetrieveMultiple(query).Entities
            .Select(e => (e.Id, e.GetAttributeValue<string>("uniquename") ?? e.Id.ToString(), e.GetAttributeValue<OptionSetValue>("statecode")?.Value, e.GetAttributeValue<string>("onclickeventjavascriptfunctionname")))
            .ToList();
    }

    /// <summary>
    /// 查实体在目标 Solution 中的 solutioncomponent 记录的 rootcomponentbehavior；
    /// 不在包内返回 null。0=Include Subcomponents（子组件隐式随包），1=不含子组件，2=仅外壳。
    /// </summary>
    static int? GetEntityRootComponentBehavior(ServiceClient service, Guid solutionId, Guid entityId)
    {
        var query = new QueryExpression("solutioncomponent")
        {
            ColumnSet = new ColumnSet("rootcomponentbehavior"),
            TopCount = 1,
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("solutionid", ConditionOperator.Equal, solutionId),
                    new ConditionExpression("objectid", ConditionOperator.Equal, entityId)
                }
            }
        };
        var record = service.RetrieveMultiple(query).Entities.FirstOrDefault();
        return record?.GetAttributeValue<OptionSetValue>("rootcomponentbehavior")?.Value;
    }

    /// <summary>
    /// 判断指定组件是否已在目标 Solution 中（solutionid + objectid 过滤，objectid 全局唯一，天然覆盖所有组件类型）。
    /// </summary>
    static bool IsComponentInSolution(ServiceClient service, Guid solutionId, Guid objectId)
    {
        var query = new QueryExpression("solutioncomponent")
        {
            ColumnSet = new ColumnSet("solutioncomponentid"),
            TopCount = 1,
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("solutionid", ConditionOperator.Equal, solutionId),
                    new ConditionExpression("objectid", ConditionOperator.Equal, objectId)
                }
            }
        };
        return service.RetrieveMultiple(query).Entities.Count > 0;
    }

    /// <summary>
    /// 取实体全部 mcs_ 开头自定义字段（排除主键）的 MetadataId 与逻辑名；实体不存在返回空列表。
    /// 只读：RetrieveEntityRequest（EntityFilters.Attributes）。
    /// </summary>
    static List<(Guid MetadataId, string LogicalName)> GetCustomFields(ServiceClient service, string entityLogicalName)
    {
        try
        {
            var response = (RetrieveEntityResponse)service.Execute(new RetrieveEntityRequest
            {
                LogicalName = entityLogicalName,
                EntityFilters = EntityFilters.Attributes,
                RetrieveAsIfPublished = false
            });
            var primaryId = response.EntityMetadata.PrimaryIdAttribute ?? "";
            return response.EntityMetadata.Attributes
                .Where(a => a.LogicalName.StartsWith("mcs_", StringComparison.OrdinalIgnoreCase))
                .Where(a => !a.LogicalName.Equals(primaryId, StringComparison.OrdinalIgnoreCase))
                // 排除 lookup/选项集伴生的 *name 虚拟属性（AttributeOf != null 表示依附于其他属性，非独立字段）
                .Where(a => a.AttributeOf == null)
                .Where(a => a.MetadataId.HasValue)
                .Select(a => (a.MetadataId!.Value, a.LogicalName))
                .ToList();
        }
        catch
        {
            return new List<(Guid, string)>();
        }
    }

    /// <summary>
    /// 查询指定 PluginType 的全部 Step（含名称、Message、实体信息，用于缺失报告）。
    /// </summary>
    static List<Microsoft.Xrm.Sdk.Entity> GetPluginSteps(ServiceClient service, Guid pluginTypeId)
    {
        var query = new QueryExpression("sdkmessageprocessingstep")
        {
            ColumnSet = new ColumnSet("sdkmessageprocessingstepid", "name", "sdkmessageid", "sdkmessagefilterid"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("plugintypeid", ConditionOperator.Equal, pluginTypeId) }
            }
        };
        return service.RetrieveMultiple(query).Entities.ToList();
    }

    /// <summary>
    /// 列出工作流/BPF 流程定义（只读）。category：0=工作流 1=对话 2=业务规则 3=操作 4=业务流程流(BPF) 5=现代化流。
    /// </summary>
    static void ListWorkflows(ServiceClient service, string? keyword)
    {
        var query = new QueryExpression("workflow")
        {
            ColumnSet = new ColumnSet("name", "category", "primaryentity", "statecode"),
            Criteria = new FilterExpression()
        };
        if (!string.IsNullOrWhiteSpace(keyword))
            query.Criteria.Conditions.Add(new ConditionExpression("name", ConditionOperator.Like, $"%{keyword}%"));
        var flows = RetrieveAllPages(service, query);
        Console.WriteLine($"=== 工作流/BPF 清单（{flows.Count} 个）===");
        foreach (var f in flows.OrderBy(f => f.GetAttributeValue<OptionSetValue>("category")?.Value).ThenBy(f => f.GetAttributeValue<string>("name")))
        {
            var category = f.GetAttributeValue<OptionSetValue>("category")?.Value ?? -1;
            var categoryLabel = category switch { 0 => "工作流", 1 => "对话", 2 => "业务规则", 3 => "操作", 4 => "BPF", 5 => "现代化流", _ => $"类别{category}" };
            var state = f.GetAttributeValue<OptionSetValue>("statecode")?.Value == 1 ? "启用" : "停用";
            Console.WriteLine($"  [{categoryLabel}] {f.GetAttributeValue<string>("name")}  实体={f.GetAttributeValue<string>("primaryentity")}  {state}  {f.Id}");
        }
    }

    /// <summary>
    /// 只读诊断：指定用户对某 BPF 流程条的可见性。
    /// 原理：经典 D365 中 BPF 进度条可见性 = 用户安全角色对「BPF 流程实体」（IsBPFEntity，如 mcs_credit）的 Read 权限；
    /// 记录能打开但进度条不显示的典型根因 = 角色配了业务实体（如 mcs_credit_record）的 Read，漏配 BPF 流程实体的 Read。
    /// </summary>
    static void CheckBpfAccess(ServiceClient service, string domainName, string bpfKeyword)
    {
        Console.WriteLine($"=== BPF 进度条可见性诊断（只读）：用户={domainName}，BPF 关键字={bpfKeyword} ===");

        // 1. 查用户
        var userQuery = new QueryExpression("systemuser")
        {
            ColumnSet = new ColumnSet("fullname", "domainname", "businessunitid", "isdisabled"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("domainname", ConditionOperator.Equal, domainName) }
            }
        };
        var users = service.RetrieveMultiple(userQuery).Entities;
        if (users.Count == 0)
        {
            Console.WriteLine($"  ❌ 未找到 domainname={domainName} 的用户");
            return;
        }
        var user = users[0];
        Console.WriteLine($"用户: {user.GetAttributeValue<string>("fullname")}  id={user.Id}  部门={user.GetAttributeValue<EntityReference>("businessunitid")?.Name}  禁用={user.GetAttributeValue<bool>("isdisabled")}");

        // 2. 直接分配角色（systemuserroles）
        var directRoleQuery = new QueryExpression("role") { ColumnSet = new ColumnSet("name", "businessunitid") };
        var linkUserRoles = directRoleQuery.AddLink("systemuserroles", "roleid", "roleid");
        linkUserRoles.LinkCriteria.AddCondition("systemuserid", ConditionOperator.Equal, user.Id);
        var directRoles = service.RetrieveMultiple(directRoleQuery).Entities;

        // 3. 团队继承角色（teammembership → teamroles）
        var teamRoleQuery = new QueryExpression("role") { ColumnSet = new ColumnSet("name", "businessunitid") };
        var linkTeamRoles = teamRoleQuery.AddLink("teamroles", "roleid", "roleid");
        linkTeamRoles.EntityAlias = "teamroles";
        var linkMembership = linkTeamRoles.AddLink("teammembership", "teamid", "teamid");
        linkMembership.LinkCriteria.AddCondition("systemuserid", ConditionOperator.Equal, user.Id);
        var teamRoles = service.RetrieveMultiple(teamRoleQuery).Entities;

        var allRoles = directRoles.Select(r => (Role: r, Source: "直接"))
            .Concat(teamRoles.Select(r => (Role: r, Source: "团队")))
            .GroupBy(x => x.Role.Id)
            .Select(g => g.First())
            .ToList();
        Console.WriteLine($"\n安全角色（{allRoles.Count} 个，直接 {directRoles.Count} + 团队 {teamRoles.Count}，已去重）:");
        foreach (var x in allRoles)
            Console.WriteLine($"  [{x.Source}] {x.Role.GetAttributeValue<string>("name")}  (BU: {x.Role.GetAttributeValue<EntityReference>("businessunitid")?.Name})  id={x.Role.Id}");

        // 4. 查 BPF 定义
        var bpfQuery = new QueryExpression("workflow")
        {
            ColumnSet = new ColumnSet("name", "uniquename", "statecode", "primaryentity"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("category", ConditionOperator.Equal, 4),
                    new ConditionExpression("name", ConditionOperator.Like, $"%{bpfKeyword}%")
                }
            }
        };
        var bpfs = service.RetrieveMultiple(bpfQuery).Entities;
        Console.WriteLine($"\nBPF 定义（名称含「{bpfKeyword}」）:");
        foreach (var b in bpfs)
        {
            var st = b.GetAttributeValue<OptionSetValue>("statecode")?.Value == 1 ? "启用" : "停用";
            Console.WriteLine($"  {b.GetAttributeValue<string>("name")}  uniquename={b.GetAttributeValue<string>("uniquename")}  主实体={b.GetAttributeValue<string>("primaryentity")}  {st}  id={b.Id}");
        }
        if (bpfs.Count == 0) Console.WriteLine("  ❌ 未找到匹配的 BPF");

        // 5. 查 BPF 流程实体（IsBPFEntity），锁定目标 BPF 的流程实体
        var metaReq = new RetrieveAllEntitiesRequest { EntityFilters = EntityFilters.Entity, RetrieveAsIfPublished = true };
        var metaResp = (RetrieveAllEntitiesResponse)service.Execute(metaReq);
        var bpfEntityNames = metaResp.EntityMetadata
            .Where(e => e.IsBPFEntity == true)
            .Select(e => e.LogicalName)
            .ToList();
        Console.WriteLine($"\n环境中 BPF 流程实体（IsBPFEntity）共 {bpfEntityNames.Count} 个:");
        foreach (var n in bpfEntityNames.OrderBy(x => x)) Console.WriteLine($"  {n}");

        // 目标实体集合：BPF 主实体（业务实体）+ 目标 BPF 的流程实体
        var targetEntities = new List<string>();
        foreach (var b in bpfs)
        {
            var primary = b.GetAttributeValue<string>("primaryentity");
            if (!string.IsNullOrEmpty(primary) && primary != "none" && !targetEntities.Contains(primary))
                targetEntities.Add(primary);
            // BPF 流程实体逻辑名通常与 workflow uniquename 一致
            var uniq = b.GetAttributeValue<string>("uniquename");
            if (!string.IsNullOrEmpty(uniq) && bpfEntityNames.Contains(uniq) && !targetEntities.Contains(uniq))
                targetEntities.Add(uniq);
        }
        Console.WriteLine($"\n待核对权限的目标实体: {string.Join(", ", targetEntities)}");

        // 6. 用平台 API RetrieveUserPrivileges 汇总用户全部权限（含直接+团队角色，管理员内置角色也准确）
        var privResp = (RetrieveUserPrivilegesResponse)service.Execute(new RetrieveUserPrivilegesRequest { UserId = user.Id });
        // privilegeid → name 映射（privilege 表无 createdon，直接全量拉）
        var privNameMap = new Dictionary<Guid, string>();
        var allPrivQuery = new QueryExpression("privilege") { ColumnSet = new ColumnSet("name") };
        foreach (var p in RetrieveAllPages(service, allPrivQuery))
            privNameMap[p.Id] = p.GetAttributeValue<string>("name") ?? "";
        var userDepth = new Dictionary<string, int>(); // privilege name -> 最大深度
        foreach (var rp in privResp.RolePrivileges)
        {
            if (!privNameMap.TryGetValue(rp.PrivilegeId, out var pname)) continue;
            var depth = (int)rp.Depth;
            if (!userDepth.ContainsKey(pname) || userDepth[pname] < depth) userDepth[pname] = depth;
        }
        Console.WriteLine($"\n用户权限总数={privResp.RolePrivileges.Length}（RetrieveUserPrivileges，含直接+团队继承）");
        Console.WriteLine("\n=== 目标实体 Read 权限（PrivilegeDepth: 0=本人 1=本部门 2=本部门及子部门 3=组织）===");
        foreach (var e in targetEntities)
        {
            // privilege 名精确匹配 + 模糊匹配容错（前缀/大小写差异）
            var exact = "prvRead" + e;
            var found = userDepth.TryGetValue(exact, out var dExact);
            var hit = found
                ? (Found: true, Name: exact, Depth: dExact)
                : userDepth.Where(kv => kv.Key.EndsWith(e, StringComparison.OrdinalIgnoreCase) && kv.Key.StartsWith("prvRead", StringComparison.OrdinalIgnoreCase))
                    .Select(kv => (Found: true, Name: kv.Key, Depth: kv.Value))
                    .OrderByDescending(t => t.Depth).FirstOrDefault();
            if (hit.Found)
                Console.WriteLine($"  {e}: ✅ 有 Read 权限，深度={hit.Depth}（{DepthLabel(hit.Depth)}，privilege: {hit.Name}）");
            else
                Console.WriteLine($"  {e}: ❌ 无 Read 权限（未找到 {exact}）");
        }
        // 参考样本：输出用户所有含 credit 的 Read 权限
        var creditSamples = userDepth.Where(kv => kv.Key.Contains("redit")).OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}=深度{kv.Value}").ToList();
        Console.WriteLine($"\n（参考）用户全部含 'credit' 的权限: {(creditSamples.Count > 0 ? string.Join(" | ", creditSamples) : "无")}");

        Console.WriteLine("\n=== 结论 ===");
        Console.WriteLine("  判定规则：业务实体(mcs_credit_record)有 Read 但 BPF 流程实体(mcs_credit)无 Read ⇒ 能打开记录、看不到进度条（权限根因确认）");
        Console.WriteLine("  若两者均无 Read ⇒ 连记录本身也无权限，需先补业务实体权限");
    }

    static string DepthLabel(int depth) => depth switch
    {
        0 => "本人",
        1 => "本部门",
        2 => "本部门及子部门",
        3 => "组织",
        _ => $"未知({depth})"
    };

    #region 角色权限工具（SecurityRoleService 薄命令入口，2026-08-21 新增）

    /// <summary>权限操作输出顺序（AppendTo 必须先于 Append 参与名称解析）。</summary>
    static readonly string[] PrivOpsOrdered = { "AppendTo", "Create", "Delete", "Append", "Assign", "Share", "Write", "Read" };
    static readonly string[] PrivOpsDisplay = { "Read", "Write", "Create", "Delete", "Append", "AppendTo", "Assign", "Share" };

    /// <summary>解析 privilege 名为 (实体, 操作)；无法解析（misc 权限）返回 (null, null)。</summary>
    static (string? Entity, string? Op) SplitPrivilegeName(string privName)
    {
        if (!privName.StartsWith("prv")) return (null, null);
        var rest = privName.Substring(3);
        foreach (var op in PrivOpsOrdered)
            if (rest.StartsWith(op) && rest.Length > op.Length)
                return (rest.Substring(op.Length), op);
        return (null, null);
    }

    /// <summary>打印指定实体的 8 类权限深度明细。</summary>
    static void PrintEntityPrivilegeDetail(IDictionary<string, PrivilegeDepth> depths, string entity)
    {
        var ci = new Dictionary<string, PrivilegeDepth>(depths, StringComparer.OrdinalIgnoreCase);
        Console.WriteLine($"  实体: {entity}");
        foreach (var op in PrivOpsDisplay)
        {
            if (ci.TryGetValue("prv" + op + entity, out var d))
                Console.WriteLine($"    {op,-9}: {(int)d}（{SecurityRoleService.DepthLabel((int)d)}）");
            else
                Console.WriteLine($"    {op,-9}: 无");
        }
    }

    /// <summary>按实体分组打印权限紧凑表（每实体一行：R/W/C/D/AP/AT/AS/S + 深度）。</summary>
    static void PrintPrivilegeGroupedTable(IDictionary<string, PrivilegeDepth> depths)
    {
        var entityOps = new SortedDictionary<string, Dictionary<string, PrivilegeDepth>>(StringComparer.OrdinalIgnoreCase);
        var misc = new SortedDictionary<string, PrivilegeDepth>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in depths)
        {
            var (entity, op) = SplitPrivilegeName(kv.Key);
            if (entity == null || op == null) { misc[kv.Key] = kv.Value; continue; }
            if (!entityOps.TryGetValue(entity, out var ops)) { ops = new Dictionary<string, PrivilegeDepth>(); entityOps[entity] = ops; }
            ops[op] = kv.Value;
        }

        var opShort = new Dictionary<string, string>
        {
            ["Read"] = "R", ["Write"] = "W", ["Create"] = "C", ["Delete"] = "D",
            ["Append"] = "AP", ["AppendTo"] = "AT", ["Assign"] = "AS", ["Share"] = "S"
        };
        Console.WriteLine($"  【实体权限】（深度: 本人/本部门/本部门及子部门/组织；- = 无）共 {entityOps.Count} 个实体");
        foreach (var (entity, ops) in entityOps)
        {
            var cells = PrivOpsDisplay.Select(op =>
                ops.TryGetValue(op, out var d) ? $"{opShort[op]}:{SecurityRoleService.DepthLabel((int)d)}" : $"{opShort[op]}:-");
            Console.WriteLine($"    {entity,-42} {string.Join(" ", cells)}");
        }
        if (misc.Count > 0)
        {
            Console.WriteLine($"  【杂项权限】共 {misc.Count} 个");
            foreach (var (name, d) in misc)
                Console.WriteLine($"    {name,-52} {SecurityRoleService.DepthLabel((int)d)}");
        }
    }

    /// <summary>只读：查角色权限明细。</summary>
    static void ListRolePrivileges(ServiceClient service, string roleKeyword, string? entityFilter)
    {
        Console.WriteLine($"=== 角色权限查询（只读）：关键字={roleKeyword}，实体过滤={entityFilter ?? "（无）"} ===");
        var srs = new SecurityRoleService(service);
        var roles = srs.FindRoles(roleKeyword, rootOnly: true);
        if (roles.Count == 0)
        {
            Console.WriteLine($"  ❌ 未找到名称含 '{roleKeyword}' 的安全角色");
            return;
        }
        Console.WriteLine($"找到 {roles.Count} 条匹配角色");

        // 逐角色取权限（每副本一次 API 调用，多副本时稍慢）
        var roleDepths = roles.Select(r => (Role: r, Depths: srs.GetRolePrivilegeDepths(r.Id))).ToList();

        // 多 BU 副本权限一致时合并输出，避免 92 个副本刷屏
        var first = roleDepths[0].Depths;
        var allSame = roleDepths.All(x =>
            x.Depths.Count == first.Count &&
            x.Depths.All(kv => first.TryGetValue(kv.Key, out var d) && d == kv.Value));
        if (roleDepths.Count > 1 && allSame)
        {
            var name = roleDepths[0].Role.GetAttributeValue<string>("name");
            Console.WriteLine($"\n--- 角色: {name}  共 {roleDepths.Count} 个 BU 副本，权限完全一致，合并输出 ---");
            Console.WriteLine($"  权限总数: {first.Count}");
            if (!string.IsNullOrEmpty(entityFilter))
                PrintEntityPrivilegeDetail(first, entityFilter);
            else
                PrintPrivilegeGroupedTable(first);
            return;
        }

        foreach (var (role, depths) in roleDepths)
        {
            var name = role.GetAttributeValue<string>("name");
            var bu = role.GetAttributeValue<EntityReference>("businessunitid");
            Console.WriteLine($"\n--- 角色: {name}  (BU: {bu?.Name})  id={role.Id} ---");
            Console.WriteLine($"  权限总数: {depths.Count}");
            if (!string.IsNullOrEmpty(entityFilter))
                PrintEntityPrivilegeDetail(depths, entityFilter);
            else
                PrintPrivilegeGroupedTable(depths);
        }
    }

    /// <summary>只读：查用户有效权限（角色清单 + RetrieveUserPrivileges 汇总）。</summary>
    static void QueryUserPermissions(ServiceClient service, string domainName, string? entityFilter)
    {
        Console.WriteLine($"=== 用户有效权限查询（只读）：用户={domainName}，实体过滤={entityFilter ?? "（无）"} ===");
        var srs = new SecurityRoleService(service);
        var user = srs.FindUser(domainName);
        if (user == null)
        {
            Console.WriteLine($"  ❌ 未找到 domainname={domainName} 的用户");
            return;
        }
        Console.WriteLine($"用户: {user.GetAttributeValue<string>("fullname")}  id={user.Id}  部门={user.GetAttributeValue<EntityReference>("businessunitid")?.Name}  禁用={user.GetAttributeValue<bool>("isdisabled")}");

        var roles = srs.GetUserRoles(user.Id);
        Console.WriteLine($"\n安全角色（{roles.Count} 个，已去重）:");
        foreach (var (role, source) in roles)
            Console.WriteLine($"  [{source}] {role.GetAttributeValue<string>("name")}  (BU: {role.GetAttributeValue<EntityReference>("businessunitid")?.Name})  id={role.Id}");

        var privs = srs.GetUserEffectivePrivileges(user.Id);
        Console.WriteLine($"\n有效权限总数={privs.Count}（RetrieveUserPrivileges，含直接+团队继承，同名取最大深度）");
        var depths = privs.ToDictionary(kv => kv.Key, kv => (PrivilegeDepth)kv.Value);
        if (!string.IsNullOrEmpty(entityFilter))
            PrintEntityPrivilegeDetail(depths, entityFilter);
        else
            PrintPrivilegeGroupedTable(depths);
    }

    /// <summary>写：设置角色权限（需用户明确授权）。</summary>
    static void SetRolePrivilegeCommand(ServiceClient service, string roleKeyword, string entityName, string operation, string depthText)
    {
        Console.WriteLine($"=== 设置角色权限（写）=== ");
        Console.WriteLine($"目标环境: {D365ConnectionFactory.ResolveUrl()}");

        // 1. 参数校验（entityName=misc 时为杂项权限：operation 参数传杂项权限名，如 DocumentGeneration）
        var isMisc = string.Equals(entityName, "misc", StringComparison.OrdinalIgnoreCase);
        string privilegeName;
        if (isMisc)
        {
            privilegeName = "prv" + operation;
        }
        else
        {
            if (!SecurityRoleService.OperationMap.TryGetValue(operation, out var op))
            {
                Console.WriteLine($"  ❌ 权限类型 '{operation}' 不合法，支持: {string.Join("|", SecurityRoleService.OperationMap.Keys)}");
                return;
            }
            privilegeName = "prv" + op + entityName;
        }
        PrivilegeDepth? depth;
        if (string.Equals(depthText, "none", StringComparison.OrdinalIgnoreCase)) depth = null;
        else if (SecurityRoleService.DepthMap.TryGetValue(depthText, out var d)) depth = d;
        else if (isMisc && string.Equals(depthText, "-", StringComparison.OrdinalIgnoreCase)) depth = PrivilegeDepth.Global; // 杂项权限无深度概念，统一给 Global
        else
        {
            Console.WriteLine($"  ❌ 深度 '{depthText}' 不合法，支持: none|user|bu|childbu|org{(isMisc ? "|-(杂项默认org)" : "")}");
            return;
        }

        // 2. 角色定位（多匹配一律拒绝，防误批量改 BU 副本）
        var srs = new SecurityRoleService(service);
        var roles = srs.FindRoles(roleKeyword, rootOnly: true);
        if (roles.Count == 0) { Console.WriteLine($"  ❌ 未找到名称含 '{roleKeyword}' 的角色"); return; }
        if (roles.Count > 1)
        {
            Console.WriteLine($"  ❌ 关键字匹配到 {roles.Count} 条角色，为避免误改请用更精确的名称:");
            foreach (var r in roles)
                Console.WriteLine($"     {r.GetAttributeValue<string>("name")}  (BU: {r.GetAttributeValue<EntityReference>("businessunitid")?.Name})  id={r.Id}");
            return;
        }
        var role = roles[0];
        var roleName = role.GetAttributeValue<string>("name");

        // 3. 系统内置角色保护
        if (string.Equals(roleName, "System Administrator", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(roleName, "System Customizer", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"  ❌ 禁止修改系统内置角色【{roleName}】的权限");
            return;
        }

        // 4. 执行
        var before = srs.GetRolePrivilegeDepths(role.Id);
        var hadBefore = before.TryGetValue(privilegeName, out var beforeDepth);
        Console.WriteLine($"角色: {roleName}  (BU: {role.GetAttributeValue<EntityReference>("businessunitid")?.Name})  id={role.Id}");
        Console.WriteLine($"权限: {privilegeName}  变更前: {(hadBefore ? $"{(int)beforeDepth}（{SecurityRoleService.DepthLabel((int)beforeDepth)}）" : "无")}  →  目标: {(depth == null ? "无（移除）" : $"{(int)depth.Value}（{SecurityRoleService.DepthLabel((int)depth.Value)}）")}");

        var changed = srs.SetRolePrivilege(role.Id, privilegeName, depth);
        if (!changed)
        {
            Console.WriteLine("  ⏭️ 已是目标状态，未改动（幂等）");
            return;
        }

        // 5. 回读确认
        var after = srs.GetRolePrivilegeDepths(role.Id);
        var hasAfter = after.TryGetValue(privilegeName, out var afterDepth);
        Console.WriteLine($"  ✅ 已变更，回读确认: {(hasAfter ? $"{(int)afterDepth}（{SecurityRoleService.DepthLabel((int)afterDepth)}）" : "无")}");
    }

    /// <summary>写：给用户挂/摘角色（需用户明确授权）。</summary>
    static void AssignOrRemoveRoleCommand(ServiceClient service, string domainName, string roleName, bool isAssign)
    {
        Console.WriteLine($"=== {(isAssign ? "分配" : "移除")}角色（写）===");
        Console.WriteLine($"目标环境: {D365ConnectionFactory.ResolveUrl()}");

        var srs = new SecurityRoleService(service);
        var user = srs.FindUser(domainName);
        if (user == null) { Console.WriteLine($"  ❌ 未找到 domainname={domainName} 的用户"); return; }
        Console.WriteLine($"用户: {user.GetAttributeValue<string>("fullname")}  id={user.Id}");

        // 角色定位：优先精确名匹配，多匹配拒绝
        var candidates = srs.FindRoles(roleName, rootOnly: true);
        var exact = candidates.Where(r => string.Equals(r.GetAttributeValue<string>("name"), roleName, StringComparison.OrdinalIgnoreCase)).ToList();
        List<Entity> pool = exact.Count > 0 ? exact : candidates;
        if (pool.Count == 0) { Console.WriteLine($"  ❌ 未找到名称含 '{roleName}' 的角色"); return; }
        if (pool.Count > 1)
        {
            Console.WriteLine($"  ❌ 匹配到 {pool.Count} 条角色，为避免误操作请用更精确的名称:");
            foreach (var r in pool)
                Console.WriteLine($"     {r.GetAttributeValue<string>("name")}  (BU: {r.GetAttributeValue<EntityReference>("businessunitid")?.Name})  id={r.Id}");
            return;
        }
        var role = pool[0];
        Console.WriteLine($"角色: {role.GetAttributeValue<string>("name")}  (BU: {role.GetAttributeValue<EntityReference>("businessunitid")?.Name})  id={role.Id}");

        // 防误摘自己
        var whoAmI = (WhoAmIResponse)service.Execute(new WhoAmIRequest());
        if (whoAmI.UserId == user.Id)
            Console.WriteLine("  ⚠️ 警告：目标用户是当前连接账号本身，请确认操作意图！");

        var changed = isAssign ? srs.AssignRole(user.Id, role.Id) : srs.RemoveRole(user.Id, role.Id);
        if (!changed)
        {
            Console.WriteLine($"  ⏭️ {(isAssign ? "用户已分配该角色" : "用户本就没有该角色")}，未改动（幂等）");
            return;
        }
        var nowHas = srs.UserHasRole(user.Id, role.Id);
        Console.WriteLine($"  ✅ 已{(isAssign ? "分配" : "移除")}，回读确认: 用户{(nowHas ? "已持有" : "不再持有")}该角色");
    }

    #endregion

    /// <summary>
    /// 列出指定 App（appmodule）包含的 appmodulecomponent，可选按实体逻辑名前缀过滤。只读。
    /// </summary>
    static void QueryAppComponents(ServiceClient service, string appNameKeyword, string? entityPrefix)
    {
        var appQuery = new QueryExpression("appmodule")
        {
            ColumnSet = new ColumnSet("name", "uniquename", "ismanaged", "appmoduleidunique"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("name", ConditionOperator.Like, $"%{appNameKeyword}%") }
            }
        };
        var apps = RetrieveAllPages(service, appQuery);
        if (apps.Count == 0) { Console.WriteLine($"  ❌ 未找到名称含 {appNameKeyword} 的 App"); return; }

        // appmodulecomponenttype 选项集常见值（用于可读显示）
        var typeNames = new Dictionary<int, string>
        {
            { 1, "实体" }, { 26, "仪表板" }, { 29, "业务流程" }, { 35, "视图" },
            { 59, "表单" }, { 60, "表单(SystemForm)" }, { 62, "SiteMap" }, { 300, "CanvasApp" }
        };

        foreach (var app in apps)
        {
            var appUnique = app.GetAttributeValue<Guid?>("appmoduleidunique");
            Console.WriteLine($"=== App: {app.GetAttributeValue<string>("name")}  appmoduleid={app.Id}  unique={appUnique} ===");
            if (appUnique == null) continue;
            var compQuery = new QueryExpression("appmodulecomponent")
            {
                ColumnSet = new ColumnSet("objectid", "componenttype", "isdefault"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("appmoduleidunique", ConditionOperator.Equal, appUnique.Value) }
                }
            };
            var comps = RetrieveAllPages(service, compQuery);
            // 解析实体逻辑名
            var entityIds = comps.Where(c => c.GetAttributeValue<OptionSetValue>("componenttype")?.Value == 1)
                .Select(c => c.Attributes.TryGetValue("objectid", out var ov) ? ov switch { Guid g => g, EntityReference er => er.Id, _ => Guid.Empty } : Guid.Empty)
                .Where(id => id != Guid.Empty).Distinct().ToList();
            var logicalNames = new Dictionary<Guid, string>();
            if (entityIds.Count > 0)
            {
                var metaQuery = new QueryExpression("entity")
                {
                    ColumnSet = new ColumnSet("logicalname"),
                    Criteria = new FilterExpression
                    {
                        Conditions = { new ConditionExpression("entityid", ConditionOperator.In, entityIds.Cast<object>().ToArray()) }
                    }
                };
                try
                {
                    foreach (var e in RetrieveAllPages(service, metaQuery))
                        logicalNames[e.Id] = e.GetAttributeValue<string>("logicalname") ?? "";
                }
                catch (Exception ex) { Console.WriteLine($"  ⚠️ 实体名解析失败: {ex.Message}"); }
            }
            int shown = 0;
            foreach (var c in comps.OrderBy(c => c.GetAttributeValue<OptionSetValue>("componenttype")?.Value ?? -1))
            {
                var ct = c.GetAttributeValue<OptionSetValue>("componenttype")?.Value ?? -1;
                var objId = c.Attributes.TryGetValue("objectid", out var ov) ? ov switch { Guid g => g, EntityReference er => er.Id, _ => Guid.Empty } : Guid.Empty;
                var name = ct == 1 && logicalNames.TryGetValue(objId, out var ln) ? ln : "";
                if (!string.IsNullOrWhiteSpace(entityPrefix) && (ct != 1 || !name.StartsWith(entityPrefix, StringComparison.OrdinalIgnoreCase))) continue;
                shown++;
                Console.WriteLine($"  [{(typeNames.TryGetValue(ct, out var tn) ? tn : $"type{ct}")}] {name}  {objId}");
            }
            Console.WriteLine($"  （共 {comps.Count} 个组件，显示 {shown} 个）");
        }
    }

    /// <summary>
    /// 查询 SiteMap 记录、App（appmodule）与 SiteMap 的对应关系、以及单个 SiteMap 的解决方案分层。只读。
    /// </summary>
    static void QuerySitemaps(ServiceClient service, string? sitemapIdOrName)
    {
        // 1. 查询 SiteMap 记录
        var query = new QueryExpression("sitemap")
        {
            ColumnSet = new ColumnSet("sitemapname", "isappaware", "ismanaged", "modifiedon", "sitemapxml"),
            Criteria = new FilterExpression()
        };
        if (!string.IsNullOrWhiteSpace(sitemapIdOrName) && Guid.TryParse(sitemapIdOrName, out var sid))
            query.Criteria.Conditions.Add(new ConditionExpression("sitemapid", ConditionOperator.Equal, sid));
        else if (!string.IsNullOrWhiteSpace(sitemapIdOrName))
            query.Criteria.Conditions.Add(new ConditionExpression("sitemapname", ConditionOperator.Like, $"%{sitemapIdOrName}%"));
        var maps = RetrieveAllPages(service, query);
        Console.WriteLine($"=== SiteMap 记录清单（{maps.Count} 个）===");
        foreach (var m in maps.OrderByDescending(m => m.GetAttributeValue<DateTime?>("modifiedon")))
        {
            var xml = m.GetAttributeValue<string>("sitemapxml") ?? "";
            var appAware = m.GetAttributeValue<bool?>("isappaware") == true ? "App专用" : "默认";
            var managed = m.GetAttributeValue<bool?>("ismanaged") == true ? "托管" : "非托管";
            Console.WriteLine($"  {m.Id}  [{appAware}/{managed}]  {m.GetAttributeValue<string>("sitemapname")}  modifiedon={m.GetAttributeValue<DateTime?>("modifiedon"):yyyy-MM-dd HH:mm:ss}  xml长度={xml.Length}");
            if (maps.Count == 1)
            {
                var path = $"/tmp/sitemap_{m.Id}.xml";
                File.WriteAllText(path, xml);
                Console.WriteLine($"  ✅ XML 已导出: {path}");
            }
        }

        // 2. 查询 App 与 SiteMap 对应关系
        var appQuery = new QueryExpression("appmodule")
        {
            ColumnSet = new ColumnSet("name", "uniquename", "ismanaged", "appmoduleidunique")
        };
        var apps = RetrieveAllPages(service, appQuery);
        // appmodule↔sitemap 的关联存储在 appmodulecomponent（objectid = sitemapid）
        var sitemapNameMap = RetrieveAllPages(service, new QueryExpression("sitemap")
        {
            ColumnSet = new ColumnSet("sitemapname")
        }).ToDictionary(m => m.Id, m => m.GetAttributeValue<string>("sitemapname") ?? "");
        var appToSitemap = new Dictionary<Guid, Guid>();
        var amcAll = new QueryExpression("appmodulecomponent") { ColumnSet = new ColumnSet("objectid", "appmoduleidunique") };
        foreach (var c in RetrieveAllPages(service, amcAll))
        {
            Guid? objId = c.Attributes.TryGetValue("objectid", out var o) ? o switch { Guid g => g, EntityReference er => er.Id, _ => null } : null;
            Guid? appUnique = c.Attributes.TryGetValue("appmoduleidunique", out var u) ? u switch { Guid g => g, EntityReference er => er.Id, _ => null } : null;
            if (objId != null && appUnique != null && sitemapNameMap.ContainsKey(objId.Value) && !appToSitemap.ContainsKey(appUnique.Value))
                appToSitemap[appUnique.Value] = objId.Value;
        }
        Console.WriteLine($"\n=== App（appmodule）与 SiteMap 对应关系（{apps.Count} 个）===");
        foreach (var a in apps.OrderBy(a => a.GetAttributeValue<string>("name")))
        {
            var managed = a.GetAttributeValue<bool?>("ismanaged") == true ? "托管" : "非托管";
            var appUnique = a.GetAttributeValue<Guid?>("appmoduleidunique");
            var sm = appUnique != null && appToSitemap.TryGetValue(appUnique.Value, out var smId) ? $"{smId}  {(sitemapNameMap.TryGetValue(smId, out var n) ? n : "")}" : "(无专用SiteMap)";
            Console.WriteLine($"  [{managed}] {a.GetAttributeValue<string>("name")}  sitemap={sm}  appmoduleid={a.Id}");
        }

        // 3. 单个 SiteMap 时，检查解决方案分层（排查非托管 Active 层覆盖托管导入的问题）
        if (maps.Count == 1)
        {
            Console.WriteLine($"\n=== SiteMap {maps[0].Id} 解决方案分层 ===");
            try
            {
                var req = new OrganizationRequest("RetrieveSolutionLayers");
                req["SolutionName"] = "Active";
                req["ComponentType"] = 62;
                req["ComponentId"] = maps[0].Id;
                var resp = service.Execute(req);
                foreach (var kv in resp.Results)
                {
                    if (kv.Value is EntityCollection ec)
                    {
                        foreach (var layer in ec.Entities)
                        {
                            var parts = layer.Attributes.Select(a => $"{a.Key}={(a.Value is OptionSetValue osv ? osv.Value : a.Value)}");
                            Console.WriteLine($"  {string.Join("  ", parts)}");
                        }
                        if (ec.Entities.Count == 0) Console.WriteLine("  (空)");
                    }
                    else
                    {
                        Console.WriteLine($"  {kv.Key} = {kv.Value}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠️ 分层查询失败: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 列出 Solution 全部组件（按类型分组：类型 + 名称 + ObjectId），只读。
    /// </summary>
    static void ListSolutionComponentsCmd(ServiceClient service, string solutionUniqueName)
    {
        if (ResolveSolutionId(service, solutionUniqueName) == null)
        {
            Console.WriteLine($"  ❌ Solution {solutionUniqueName} 在环境中不存在");
            return;
        }
        var compSvc = new D365ToolCommon.Solution.SolutionComponentService(service);
        var components = compSvc.ListSolutionComponents(solutionUniqueName);
        Console.WriteLine($"═══ Solution 组件清单：{solutionUniqueName}（{components.Count} 个）═══");

        // componenttype 的格式化标签（解决硬编码表覆盖不到的类型，如 SiteMap/Connector 等）
        var sid = ResolveSolutionId(service, solutionUniqueName)!.Value;
        var typeLabelMap = new Dictionary<(int, Guid), string>();
        var scQuery = new QueryExpression("solutioncomponent")
        {
            ColumnSet = new ColumnSet("componenttype", "objectid"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("solutionid", ConditionOperator.Equal, sid) }
            }
        };
        foreach (var e in RetrieveAllPages(service, scQuery))
        {
            var oid = e.GetAttributeValue<Guid>("objectid");
            if (oid != Guid.Empty && e.FormattedValues.TryGetValue("componenttype", out var label) && !string.IsNullOrWhiteSpace(label))
                typeLabelMap[(ReadComponentType(e, "componenttype"), oid)] = label;
        }

        var names = ResolveDependencyNames(service, components.Select(c => (c.ComponentType, c.ObjectId)).ToHashSet());
        foreach (var group in components.GroupBy(c => c.ComponentType).OrderBy(g => g.Key))
        {
            var groupLabel = group.Select(c => typeLabelMap.GetValueOrDefault((c.ComponentType, c.ObjectId))).FirstOrDefault(l => !string.IsNullOrWhiteSpace(l))
                ?? ComponentTypeLabel(group.Key);
            Console.WriteLine($"【{groupLabel} ({group.Key})】({group.Count()} 个)");
            foreach (var c in group.OrderBy(c => names.GetValueOrDefault(c.ObjectId) ?? "", StringComparer.OrdinalIgnoreCase))
            {
                var name = names.GetValueOrDefault(c.ObjectId) ?? "(无法解析)";
                Console.WriteLine($"  {name}  {c.ObjectId}");
            }
        }
    }

    /// <summary>
    /// WebResource 发版排查（只读）：以当前环境为基准，核对清单 Solution 内全部 WebResource
    /// 在目标环境是否存在、内容是否一致、是否被非托管 Active 层遮挡。
    /// </summary>
    static async Task CheckWebResourceRelease(ServiceClient baselineService, string solutionUniqueName, string targetUrl)
    {
        if (ResolveSolutionId(baselineService, solutionUniqueName) == null)
        {
            Console.WriteLine($"  ❌ Solution {solutionUniqueName} 在基准环境中不存在");
            return;
        }

        // 1. 清单 Solution 取 WebResource 组件（componenttype 61），解析名称
        var compSvc = new D365ToolCommon.Solution.SolutionComponentService(baselineService);
        var components = compSvc.ListSolutionComponents(solutionUniqueName);
        var wrIds = components.Where(c => c.ComponentType == 61).Select(c => c.ObjectId).ToList();
        if (wrIds.Count == 0)
        {
            Console.WriteLine($"  Solution {solutionUniqueName} 中无 WebResource 组件");
            return;
        }
        var wrQuery = new QueryExpression("webresource")
        {
            ColumnSet = new ColumnSet("name"),
            Criteria = new FilterExpression()
        };
        wrQuery.Criteria.Conditions.Add(new ConditionExpression("webresourceid", ConditionOperator.In, wrIds.Cast<object>().ToArray()));
        var names = baselineService.RetrieveMultiple(wrQuery).Entities
            .Select(e => e.GetAttributeValue<string>("name"))
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
        Console.WriteLine($"═══ WebResource 发版排查：{solutionUniqueName}（{names.Count} 个）═══");
        Console.WriteLine($"基准环境: {GetDefaultUrl()}");
        Console.WriteLine($"目标环境: {targetUrl}");

        // 2. 连接目标环境并逐个检查
        using var targetService = await D365ConnectionFactory.CreateAsync(targetUrl);
        var checker = new WebResourceReleaseCheckService(baselineService, targetService);
        var results = checker.Check(names!);

        // 3. 输出明细
        int okCount = 0, blockedCount = 0, diffCount = 0, missingCount = 0, warnCount = 0;
        foreach (var r in results)
        {
            string conclusion;
            if (!r.ExistsInTarget) { conclusion = "❌ 目标环境不存在"; missingCount++; }
            else if (!r.ExistsInBaseline) { conclusion = "⚠️ 基准环境不存在（目标有）"; warnCount++; }
            else if (r.ActiveLayerOverridesContent)
            {
                conclusion = "🔴 被非托管Active层遮挡（需在目标环境删除Active层）";
                blockedCount++;
            }
            else if (r.ContentMatches == false) { conclusion = "❌ 内容不一致（未遮挡，可能未导入最新包）"; diffCount++; }
            else if (r.ActiveLayerChanges.Count > 0)
            {
                conclusion = $"🟡 内容一致，但有残留Active层({string.Join(",", r.ActiveLayerChanges)})，建议清理";
                warnCount++;
                okCount++;
            }
            else { conclusion = "✅ 一致"; okCount++; }
            Console.WriteLine($"  {r.Name}  基准:{r.BaselineSize}B/{r.BaselineMd5}  目标:{r.TargetSize}B/{r.TargetMd5}  {conclusion}");
        }
        Console.WriteLine($"═══ 汇总：共 {results.Count} 个 | ✅正常 {okCount} | 🔴被遮挡 {blockedCount} | ❌不一致 {diffCount} | 缺失 {missingCount} | 🟡建议清理 {warnCount} ═══");
        if (blockedCount > 0)
        {
            Console.WriteLine("修复：目标环境 Maker Portal → 找到该 WebResource → 查看解决方案层(See solution layers) → 选中 Active 层 → Remove active customization，删完硬刷新即可，无需重新发布");
        }
    }

    /// <summary>
    /// 以源 Solution 为真相源，核对其组件是否已分布到固定发版包（只读）。
    /// 映射（开发手册 4.4）：WebResource→McsWebResource；OptionSet→McsOptionSet；普通工作流→McsAutomate；角色→role_XX（跳过）；
    /// Custom API 本体/参数/响应（10023-25）及其实现 Plugin Assembly/Step→McsCustomAPI；其余 Plugin Assembly+Step→McsPlugin（Type 惯例不入包）；
    /// 实体/字段/表单/视图/BPF/App Action 等→实体包（每次发版新建，由 entitySolution 参数指定；不传则跳过该组核对；BPF 按 workflow.category=4 识别）。
    /// </summary>
    static void CheckSolutionCoverage(ServiceClient service, string sourceSolution, string? entitySolution)
    {
        var compSvc = new D365ToolCommon.Solution.SolutionComponentService(service);

        // 1. 源 Solution 组件
        if (ResolveSolutionId(service, sourceSolution) == null)
        {
            Console.WriteLine($"  ❌ Solution {sourceSolution} 在环境中不存在");
            return;
        }
        var components = compSvc.ListSolutionComponents(sourceSolution);
        Console.WriteLine($"═══ Solution 分布核对：{sourceSolution}（{components.Count} 个组件）═══");

        // 2. 目标包组件 ObjectId 集合（惰性加载，同一包只查一次）
        var targetCache = new Dictionary<string, HashSet<Guid>>(StringComparer.OrdinalIgnoreCase);
        HashSet<Guid> GetTargetSet(string targetSolution)
        {
            if (!targetCache.TryGetValue(targetSolution, out var set))
            {
                set = compSvc.ListSolutionComponents(targetSolution)
                    .Select(c => c.ObjectId).ToHashSet();
                targetCache[targetSolution] = set;
            }
            return set;
        }

        // 3. Custom API 实现归属判断：customapi.plugintypeid 指向实现它的 Plugin Type
        //    是 Custom API 实现的 Assembly/Step → McsCustomAPI，其余 → McsPlugin（开发手册 4.4）
        var customApiImplTypeIds = new HashSet<Guid>();
        if (components.Any(c => c.ComponentType is 91 or 92))
        {
            var apiQuery = new QueryExpression("customapi")
            {
                ColumnSet = new ColumnSet("plugintypeid")
            };
            foreach (var api in RetrieveAllPages(service, apiQuery))
            {
                var typeRef = api.GetAttributeValue<EntityReference>("plugintypeid");
                if (typeRef != null) customApiImplTypeIds.Add(typeRef.Id);
            }
        }

        // Step(92)：eventhandler 指向的 Type 是否 Custom API 实现
        var stepIsCustomApiImpl = new Dictionary<Guid, bool>();
        foreach (var chunk in Chunk(components.Where(c => c.ComponentType == 92).Select(c => c.ObjectId).ToList(), 200))
        {
            if (chunk.Count == 0) continue;
            var stepQuery = new QueryExpression("sdkmessageprocessingstep")
            {
                ColumnSet = new ColumnSet("eventhandler"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("sdkmessageprocessingstepid", ConditionOperator.In, chunk.Cast<object>().ToArray()) }
                }
            };
            foreach (var s in service.RetrieveMultiple(stepQuery).Entities)
            {
                var handler = s.GetAttributeValue<EntityReference>("eventhandler");
                stepIsCustomApiImpl[s.Id] = handler != null && customApiImplTypeIds.Contains(handler.Id);
            }
        }

        // Assembly(91)：其 Step 多数为 Custom API 实现时归 McsCustomAPI（无 Step 的 Assembly 归 McsPlugin）；
        // 少数非实现 Step 输出提醒，不影响归属
        var assemblyIsCustomApiImpl = new Dictionary<Guid, bool>();
        foreach (var asmId in components.Where(c => c.ComponentType == 91).Select(c => c.ObjectId))
        {
            var stepQuery = new QueryExpression("sdkmessageprocessingstep")
            {
                ColumnSet = new ColumnSet("eventhandler"),
                LinkEntities =
                {
                    new LinkEntity("sdkmessageprocessingstep", "plugintype", "eventhandler", "plugintypeid", JoinOperator.Inner)
                    {
                        EntityAlias = "eventhandler",
                        LinkCriteria = new FilterExpression
                        {
                            Conditions = { new ConditionExpression("pluginassemblyid", ConditionOperator.Equal, asmId) }
                        }
                    }
                }
            };
            var steps = RetrieveAllPages(service, stepQuery);
            int matched = steps.Count(s => customApiImplTypeIds.Contains(s.GetAttributeValue<EntityReference>("eventhandler")?.Id ?? Guid.Empty));
            if (matched > 0 && matched < steps.Count)
                Console.WriteLine($"  ℹ️ Assembly {asmId.ToString()[..8]}：混合型 Assembly，{steps.Count} 个 Step 中 {steps.Count - matched} 个非 Custom API 实现，按多数决归类");
            assemblyIsCustomApiImpl[asmId] = steps.Count > 0 && matched * 2 > steps.Count;
        }

        // 3.1 工作流(29)细分：BPF（category=4）归实体包，普通工作流归 McsAutomate
        var bpfIds = new HashSet<Guid>();
        foreach (var chunk in Chunk(components.Where(c => c.ComponentType == 29).Select(c => c.ObjectId).ToList(), 200))
        {
            if (chunk.Count == 0) continue;
            var wfQuery = new QueryExpression("workflow")
            {
                ColumnSet = new ColumnSet("category"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("workflowid", ConditionOperator.In, chunk.Cast<object>().ToArray()) }
                }
            };
            foreach (var wf in service.RetrieveMultiple(wfQuery).Entities)
            {
                if (wf.GetAttributeValue<OptionSetValue>("category")?.Value == 4)
                    bpfIds.Add(wf.Id);
            }
        }

        // 4. 按发版包映射分组（实体包组：传了 entitySolution 则核对，未传则跳过）
        var entityGroupKey = !string.IsNullOrWhiteSpace(entitySolution)
            ? entitySolution!
            : "(实体包未指定，跳过核对)";
        var groups = new Dictionary<string, List<(int Type, Guid Id)>>(StringComparer.OrdinalIgnoreCase)
        {
            ["McsWebResource"] = new(),
            ["McsOptionSet"] = new(),
            ["McsAutomate"] = new(),
            ["McsPlugin"] = new(),
            ["McsCustomAPI"] = new(),
            ["(Plugin Type 惯例不入包)"] = new(),
            ["(role_XX 每版本一包，跳过核对)"] = new(),
            [entityGroupKey] = new(),
        };
        foreach (var c in components)
        {
            var target = c.ComponentType switch
            {
                61 => "McsWebResource",
                9 => "McsOptionSet",
                29 => bpfIds.Contains(c.ObjectId) ? entityGroupKey : "McsAutomate",
                20 => "(role_XX 每版本一包，跳过核对)",
                91 => assemblyIsCustomApiImpl.GetValueOrDefault(c.ObjectId) ? "McsCustomAPI" : "McsPlugin",
                92 => stepIsCustomApiImpl.GetValueOrDefault(c.ObjectId) ? "McsCustomAPI" : "McsPlugin",
                90 => "(Plugin Type 惯例不入包)",
                10023 or 10024 or 10025 => "McsCustomAPI",
                _ => entityGroupKey,
            };
            groups[target].Add(c);
        }
        if (!string.IsNullOrWhiteSpace(entitySolution) && ResolveSolutionId(service, entitySolution!) == null)
        {
            Console.WriteLine($"  ❌ 实体包 Solution {entitySolution} 在环境中不存在");
            return;
        }

        // 5. 批量解析显示名
        var names = ResolveDependencyNames(service, components.Select(c => (c.ComponentType, c.ObjectId)).ToHashSet());
        string NameOf((int Type, Guid Id) c) =>
            names.GetValueOrDefault(c.Id) ?? $"(无法解析) type={c.Type} id={c.Id.ToString()[..8]}";

        // 6. 分组核对输出
        int ok = 0, missing = 0, info = 0;
        foreach (var (target, items) in groups)
        {
            if (items.Count == 0) continue;
            Console.WriteLine($"【→ {target}】({items.Count} 个)");
            bool isCheckedPackage = target.StartsWith("Mcs") || target == entitySolution;
            HashSet<Guid>? targetSet = isCheckedPackage ? GetTargetSet(target) : null;
            foreach (var item in items.OrderBy(i => NameOf(i), StringComparer.OrdinalIgnoreCase))
            {
                if (!isCheckedPackage)
                {
                    info++;
                    Console.WriteLine($"  ℹ️ {NameOf(item)}");
                }
                else if (targetSet!.Contains(item.Id))
                {
                    ok++;
                    Console.WriteLine($"  ✅ {NameOf(item)}");
                }
                else
                {
                    missing++;
                    Console.WriteLine($"  ❌ {NameOf(item)} —— 不在 {target} 中，请手动添加");
                }
            }
        }

        Console.WriteLine($"═══ 汇总：✅ {ok} / ❌ {missing} / ℹ️ {info} ═══");
        if (missing > 0)
            Console.WriteLine($"═══ 有 {missing} 项未分布到固定发版包 —— 补齐后请重跑 check-solution-coverage ═══");
    }

    /// <summary>
    /// 模拟 D365 Solution 导出时的「缺少必需组件」依赖检查（只读）。
    /// 所有传入 Solution 视为本次一起发布的并集：依赖项在并集内即视为已随包。
    /// 查询策略：solutioncomponent 分页拉取发布集合 → dependency 表按 dependentcomponentobjectid 分块 In 查询（每块 200）。
    /// </summary>
    /// <summary>
    /// 跨包依赖检查（2026-08-20 新增，#1641 事故防线）：
    /// 检查 Solution 内每个 Step 的实现 PluginType/Assembly 是否同包；不同包则导入目标环境时可能报缺少依赖项。
    /// 用法：dotnet run check-step-assembly [Solution唯一名]（默认 McsPlugin，只读）
    /// </summary>
    static void CheckStepAssemblyCoverage(ServiceClient service, string solutionName)
    {
        Console.WriteLine($"═══ 跨包依赖检查：[{solutionName}] 内 Step 的实现类/程序集是否同包 ═══");

        var sid = ResolveSolutionId(service, solutionName);
        if (sid == null)
        {
            Console.WriteLine($"  ❌ Solution {solutionName} 在环境中不存在");
            return;
        }

        // 1. 包内组件：91=Assembly 92=Step（PluginType 随 Assembly 隐式携带，无独立组件行）
        var asmIds = new HashSet<Guid>();
        var stepIds = new List<Guid>();
        var compQuery = new QueryExpression("solutioncomponent")
        {
            ColumnSet = new ColumnSet("componenttype", "objectid"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("solutionid", ConditionOperator.Equal, sid.Value),
                    new ConditionExpression("componenttype", ConditionOperator.In, new object[] { 91, 92 })
                }
            }
        };
        foreach (var c in RetrieveAllPages(service, compQuery))
        {
            var ct = c.GetAttributeValue<OptionSetValue>("componenttype").Value;
            var oid = c.GetAttributeValue<Guid>("objectid");
            if (ct == 91) asmIds.Add(oid);
            else stepIds.Add(oid);
        }
        Console.WriteLine($"  包内组件：Assembly {asmIds.Count} / Step {stepIds.Count}（PluginType 随 Assembly 隐式携带，无独立组件行）");

        // 2. 分批取 Step 的 plugintypeid
        var stepTypeIds = new HashSet<Guid>();
        var stepList = new List<(string Name, Guid TypeId)>();
        for (int i = 0; i < stepIds.Count; i += 200)
        {
            var batch = stepIds.Skip(i).Take(200).Cast<object>().ToArray();
            var stepQuery = new QueryExpression("sdkmessageprocessingstep")
            {
                ColumnSet = new ColumnSet("name", "plugintypeid"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("sdkmessageprocessingstepid", ConditionOperator.In, batch) }
                }
            };
            foreach (var s in RetrieveAllPages(service, stepQuery))
            {
                var pt = s.GetAttributeValue<EntityReference>("plugintypeid");
                if (pt == null) continue;
                stepList.Add((s.GetAttributeValue<string>("name"), pt.Id));
                stepTypeIds.Add(pt.Id);
            }
        }

        // 3. 取这些类的所属 Assembly，判定：类所在 Assembly 不在包内 = 跨包依赖
        var typeAsm = new Dictionary<Guid, (string TypeName, string AsmName, Guid AsmId)>();
        foreach (var chunk in stepTypeIds.Chunk(200))
        {
            var ptQuery = new QueryExpression("plugintype")
            {
                ColumnSet = new ColumnSet("typename", "assemblyname", "pluginassemblyid"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("plugintypeid", ConditionOperator.In, chunk.Cast<object>().ToArray()) }
                }
            };
            foreach (var t in RetrieveAllPages(service, ptQuery))
            {
                var asmRef = t.GetAttributeValue<EntityReference>("pluginassemblyid");
                typeAsm[t.Id] = (t.GetAttributeValue<string>("typename"), t.GetAttributeValue<string>("assemblyname"), asmRef?.Id ?? Guid.Empty);
            }
        }

        var violations = stepList
            .Where(s => typeAsm.TryGetValue(s.TypeId, out var info) && !asmIds.Contains(info.AsmId))
            .Where(s => !(typeAsm[s.TypeId].AsmName ?? string.Empty).StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase)) // 第一方程序集各环境内置，恒存在
            .Select(s => (s.Name, typeAsm[s.TypeId]))
            .ToList();

        if (violations.Count == 0)
        {
            Console.WriteLine("  ✅ 全部 Step 的实现类所在 Assembly 均同包，无跨包依赖");
            return;
        }

        Console.WriteLine($"  🚨 发现 {violations.Count} 条跨包 Step（实现类所在 Assembly 不在本包，目标环境缺该 Assembly 时导入将报缺少依赖项）：");
        foreach (var v in violations)
            Console.WriteLine($"     - {v.Name}\n       实现类: {v.Item2.TypeName}（Assembly: {v.Item2.AsmName}）");
        Console.WriteLine("  处理建议：实现类程序集随本批同发（注意顺序），或将 Step 逻辑拆到本包程序集的独立类（推荐，见上线核对清单 2.2）");
    }

    static void CheckSolutionDependencies(ServiceClient service, string[] solutionNames)
    {
        Console.WriteLine("═══ 发版依赖检查（模拟导出）═══");

        // 1. 解析 Solution ID
        var solutionIds = new List<Guid>();
        var validNames = new List<string>();
        foreach (var name in solutionNames)
        {
            var sid = ResolveSolutionId(service, name);
            if (sid == null)
            {
                Console.WriteLine($"  ⚠️ Solution {name} 在环境中不存在，跳过");
                continue;
            }
            solutionIds.Add(sid.Value);
            validNames.Add(name);
        }
        if (solutionIds.Count == 0)
        {
            Console.WriteLine("  ❌ 没有可用的 Solution，退出");
            return;
        }
        var publishSolutionIds = solutionIds.ToHashSet();

        // 2. 拉取全部 solutioncomponent（分页），构建发布集合
        var publishSet = new HashSet<(int Type, Guid Id)>();
        foreach (var sid in solutionIds)
        {
            var compQuery = new QueryExpression("solutioncomponent")
            {
                ColumnSet = new ColumnSet("componenttype", "objectid"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("solutionid", ConditionOperator.Equal, sid) }
                }
            };
            foreach (var c in RetrieveAllPages(service, compQuery))
            {
                var objectId = c.GetAttributeValue<Guid>("objectid");
                if (objectId != Guid.Empty)
                    publishSet.Add((ReadComponentType(c, "componenttype"), objectId));
            }
        }
        Console.WriteLine($"发布集合：{string.Join(", ", validNames)}（共 {publishSet.Count} 个组件）");

        // 3. 查 dependency 表：dependentcomponentobjectid 分块 In（每块 200），并行提速
        var missing = new System.Collections.Concurrent.ConcurrentDictionary<(int Type, Guid Id), byte>();
        var publishIds = publishSet.Select(p => p.Id).Distinct().ToArray();
        int depScanned = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Parallel.ForEach(Chunk(publishIds, 200).ToList(), new ParallelOptions { MaxDegreeOfParallelism = 8 }, chunk =>
        {
            var depQuery = new QueryExpression("dependency")
            {
                ColumnSet = new ColumnSet("dependentcomponentobjectid", "dependentcomponenttype", "requiredcomponentobjectid", "requiredcomponenttype"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("dependentcomponentobjectid", ConditionOperator.In, chunk.Cast<object>().ToArray()) }
                }
            };
            using var client = service.Clone();
            foreach (var d in RetrieveAllPages(client, depQuery))
            {
                Interlocked.Increment(ref depScanned);
                var reqId = d.GetAttributeValue<Guid>("requiredcomponentobjectid");
                var reqType = ReadComponentType(d, "requiredcomponenttype");
                if (reqId == Guid.Empty) continue;
                // 自依赖剔除
                if (reqId == d.GetAttributeValue<Guid>("dependentcomponentobjectid") &&
                    reqType == ReadComponentType(d, "dependentcomponenttype")) continue;
                // 已在发布集合内剔除
                if (publishSet.Contains((reqType, reqId))) continue;
                missing.TryAdd((reqType, reqId), 0);
            }
        });
        Console.WriteLine($"（扫描 dependency 记录 {depScanned} 条，耗时 {sw.Elapsed.TotalSeconds:F0}s）");

        if (missing.Count == 0)
        {
            Console.WriteLine("═══ 汇总：缺失 0 项 —— 依赖完整，可以导出发布 ═══");
            return;
        }

        // 4. 解析缺失依赖显示名
        var missingSet = missing.Keys.ToHashSet();
        Console.WriteLine($"缺失依赖 {missingSet.Count} 项，解析显示名中…");
        var names = ResolveDependencyNames(service, missingSet);
        Console.WriteLine($"显示名解析完成（累计 {sw.Elapsed.TotalSeconds:F0}s），查询其他 Solution 归属中…");

        // 5. 批量查缺失依赖还在哪些其他 Solution 中（仅 isvisible=true，排除发布集合）
        var otherSolutions = FindOtherSolutionsOfComponents(service, missingSet.Select(m => m.Id).ToList(), publishSolutionIds);
        Console.WriteLine();

        // 6. 分组输出 + 建议规则（组内按 ❌→ℹ️→⏭️ 排序，保证可操作项不被截断隐藏）
        Console.WriteLine("缺失依赖（被包内组件依赖但未随包）：");
        int errCount = 0, infoCount = 0, skipCount = 0;
        foreach (var group in missingSet.GroupBy(m => m.Type).OrderBy(g => g.Key))
        {
            Console.WriteLine($"【{ComponentTypeLabel(group.Key)} ({group.Key})】");
            var lines = new List<(int Priority, string Name, string Text)>();
            foreach (var item in group)
            {
                var displayName = names.GetValueOrDefault(item.Id) ?? $"(无法解析) {item.Id.ToString()[..8]}";
                var others = otherSolutions.GetValueOrDefault(item.Id) ?? new List<string>();
                bool isOurs = displayName.StartsWith("mcs_", StringComparison.OrdinalIgnoreCase);
                if (isOurs && others.Count == 0)
                {
                    errCount++;
                    lines.Add((0, displayName, $"  ❌ {displayName} —— 我方组件，不在任何 Solution，建议加入对应 Solution"));
                }
                else if (isOurs)
                {
                    infoCount++;
                    lines.Add((1, displayName, $"  ℹ️ {displayName} —— 已在 {string.Join(", ", others)}，随该包发布即可（注意发版顺序）"));
                }
                else
                {
                    skipCount++;
                    lines.Add((2, displayName, $"  ⏭️ {displayName} —— 系统组件，通常可忽略"));
                }
            }
            foreach (var line in lines.OrderBy(l => l.Priority).ThenBy(l => l.Name, StringComparer.OrdinalIgnoreCase).Take(30))
                Console.WriteLine(line.Text);
            if (lines.Count > 30)
                Console.WriteLine($"  …共 {lines.Count} 条");
        }

        Console.WriteLine($"═══ 汇总：缺失 {missingSet.Count} 项 —— ❌ 必须处理 {errCount} 项 / ℹ️ 注意 {infoCount} 项 / ⏭️ 可忽略 {skipCount} 项 ═══");
    }

    /// <summary>
    /// 按发版清单将组件查重后加入指定 Solution（写操作，需用户明确授权）。
    /// 规则：实体/字段含全部子组件；Plugin 只加 Step 不加 Type 本体；
    /// Custom API 连带请求参数/响应属性；AppAction 的 componenttype 从现有 solutioncomponent 记录反查。
    /// </summary>
    static void AddManifestToSolution(ServiceClient service, string manifestPath, string solutionUniqueName)
    {
        if (!File.Exists(manifestPath))
        {
            Console.WriteLine($"  ❌ 清单文件不存在: {manifestPath}");
            return;
        }
        ReleaseManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<ReleaseManifest>(File.ReadAllText(manifestPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ 清单 JSON 解析失败: {ex.Message}");
            return;
        }
        if (manifest == null)
        {
            Console.WriteLine("  ❌ 清单内容为空或格式不正确");
            return;
        }

        var solutionId = ResolveSolutionId(service, solutionUniqueName);
        if (solutionId == null)
        {
            Console.WriteLine($"  ❌ Solution 不存在: {solutionUniqueName}");
            return;
        }
        Console.WriteLine($"═══ 将清单组件加入 Solution：{solutionUniqueName} ═══");
        Console.WriteLine($"清单: {manifest.Name ?? "(未命名)"}");

        var solutionService = new D365ToolCommon.Solution.SolutionComponentService(service);
        int added = 0, skipped = 0, notFound = 0;

        // 查重 → 缺失才添加（公共库内部也有去重，这里先查一次以便输出 ⊘）
        void TryAdd(string label, Guid componentId, int componentType)
        {
            if (IsComponentInSolution(service, solutionId.Value, componentId))
            {
                skipped++;
                Console.WriteLine($"  ⊘ {label} —— 已存在跳过");
                return;
            }
            Console.WriteLine($"  → {label}");
            solutionService.AddComponentToSolution(componentId, componentType, solutionUniqueName);
            added++;
        }

        // ── 实体（componenttype=1，AddSolutionComponentRequest 未设 DoNotIncludeSubcomponents → 含全部子组件）──
        if (manifest.Entities?.Count > 0)
        {
            Console.WriteLine("【实体（含全部子组件）】");
            foreach (var entityName in manifest.Entities)
            {
                var entityId = FindComponentIdByName(service, "entity", "name", entityName);
                if (entityId == null)
                {
                    notFound++;
                    Console.WriteLine($"  ⚠️ {entityName} —— 未找到");
                    continue;
                }
                TryAdd($"{entityName}（实体，含全部子组件）", entityId.Value, 1);
            }
        }

        // ── WebResource（61）──
        if (manifest.WebResources?.Count > 0)
        {
            Console.WriteLine("【WebResource】");
            foreach (var wrName in manifest.WebResources)
            {
                var wrId = FindComponentIdByName(service, "webresource", "name", wrName);
                if (wrId == null)
                {
                    notFound++;
                    Console.WriteLine($"  ⚠️ {wrName} —— 未找到");
                    continue;
                }
                TryAdd($"{wrName}（WebResource）", wrId.Value, 61);
            }
        }

        // ── Plugin：候选名后缀匹配 plugintype（限定两个 Assembly），只加 Step（92）不加 Type 本体 ──
        if (manifest.PluginTypes?.Count > 0)
        {
            Console.WriteLine("【Plugin Step】");
            var allowedAssemblies = new[] { "SanyD365.D365Extension.Sales", "SanyD365.D365ExtensionApi.Sales" }
                .Select(n => FindComponentIdByName(service, "pluginassembly", "name", n))
                .Where(id => id != null).Select(id => id!.Value).ToList();
            var matchedTypeIds = new HashSet<Guid>();
            var unmatchedCandidates = new List<string>();

            foreach (var candidate in manifest.PluginTypes)
            {
                var typeQuery = new QueryExpression("plugintype")
                {
                    ColumnSet = new ColumnSet("typename", "pluginassemblyid"),
                    Criteria = new FilterExpression
                    {
                        Conditions = { new ConditionExpression("typename", ConditionOperator.EndsWith, candidate) }
                    }
                };
                var types = service.RetrieveMultiple(typeQuery).Entities
                    .Where(t => allowedAssemblies.Count == 0 ||
                                allowedAssemblies.Contains(t.GetAttributeValue<EntityReference>("pluginassemblyid")?.Id ?? Guid.Empty))
                    .ToList();
                if (types.Count == 0)
                {
                    notFound++;
                    unmatchedCandidates.Add(candidate);
                    Console.WriteLine($"  ⚠️ {candidate} —— 未匹配到 PluginType（限定 Assembly: {string.Join("/", allowedAssemblies.Count > 0 ? allowedAssemblies.Select(a => a.ToString()) : new[] { "(未解析)" })}）");
                    continue;
                }
                foreach (var t in types)
                {
                    var typeName = t.GetAttributeValue<string>("typename") ?? candidate;
                    if (!matchedTypeIds.Add(t.Id))
                    {
                        Console.WriteLine($"  {candidate} → {typeName}（重复匹配，已去重）");
                        continue;
                    }
                    Console.WriteLine($"  {candidate} → {typeName}");
                    var steps = GetPluginSteps(service, t.Id);
                    if (steps.Count == 0)
                    {
                        Console.WriteLine($"     ⚠️ 该 Type 无任何 Step");
                    }
                    foreach (var step in steps)
                    {
                        var stepName = step.GetAttributeValue<string>("name") ?? step.Id.ToString();
                        TryAdd($"Step「{stepName}」({step.Id.ToString()[..8]})（componenttype=92）", step.Id, 92);
                    }
                }
            }
            if (unmatchedCandidates.Count > 0)
            {
                Console.WriteLine($"  ⚠️ 未匹配候选名清单：{string.Join(", ", unmatchedCandidates)}");
            }
        }

        // ── Custom API：本体（10023）+ 请求参数（10024）+ 响应属性（10025）──
        if (manifest.CustomApis?.Count > 0)
        {
            Console.WriteLine("【Custom API】");
            foreach (var apiName in manifest.CustomApis)
            {
                var apiId = FindComponentIdByName(service, "customapi", "uniquename", apiName);
                if (apiId == null)
                {
                    notFound++;
                    Console.WriteLine($"  ⚠️ {apiName} —— 未找到");
                    continue;
                }
                TryAdd($"{apiName}（Custom API 本体，10023）", apiId.Value, 10023);
                foreach (var (childTable, childType, childLabel) in new[]
                {
                    ("customapirequestparameter", 10024, "请求参数"),
                    ("customapiresponseproperty", 10025, "响应属性")
                })
                {
                    var childQuery = new QueryExpression(childTable)
                    {
                        ColumnSet = new ColumnSet("name"),
                        Criteria = new FilterExpression
                        {
                            Conditions = { new ConditionExpression("customapiid", ConditionOperator.Equal, apiId.Value) }
                        }
                    };
                    foreach (var c in service.RetrieveMultiple(childQuery).Entities)
                    {
                        var childName = c.GetAttributeValue<string>("name") ?? c.Id.ToString();
                        TryAdd($"{apiName}.{childName}（{childLabel}，{childType}）", c.Id, childType);
                    }
                }
            }
        }

        // ── App Action：componenttype 从现有 solutioncomponent 记录反查，不硬编码 ──
        if (manifest.AppActions?.Count > 0)
        {
            Console.WriteLine("【App Action】");
            foreach (var actionName in manifest.AppActions)
            {
                var actionId = FindComponentIdByName(service, "appaction", "uniquename", actionName);
                if (actionId == null)
                {
                    notFound++;
                    Console.WriteLine($"  ⚠️ {actionName} —— 未找到");
                    continue;
                }
                // 反查 componenttype：该 appaction 已在其他 Solution 的现有记录
                var compQuery = new QueryExpression("solutioncomponent")
                {
                    ColumnSet = new ColumnSet("componenttype"),
                    TopCount = 1,
                    Criteria = new FilterExpression
                    {
                        Conditions = { new ConditionExpression("objectid", ConditionOperator.Equal, actionId.Value) }
                    }
                };
                var existing = service.RetrieveMultiple(compQuery).Entities.FirstOrDefault();
                if (existing == null)
                {
                    notFound++;
                    Console.WriteLine($"  ⚠️ {actionName} —— 全环境无 solutioncomponent 记录，无法确定 componenttype，跳过（请先加入任一 Solution 后再试）");
                    continue;
                }
                var actionType = ReadComponentType(existing, "componenttype");
                TryAdd($"{actionName}（App Action，componenttype={actionType}）", actionId.Value, actionType);
            }
        }

        Console.WriteLine($"═══ 汇总：✅ 新加入 {added} / ⊘ 已存在 {skipped} / ⚠️ 未找到 {notFound} ═══");
    }

    /// <summary>
    /// 分页拉取查询全部结果（PageInfo Cookie 分页，每页 5000）。
    /// </summary>
    static List<Microsoft.Xrm.Sdk.Entity> RetrieveAllPages(ServiceClient service, QueryExpression query)
    {
        var all = new List<Microsoft.Xrm.Sdk.Entity>();
        query.PageInfo = new PagingInfo { Count = 5000, PageNumber = 1 };
        while (true)
        {
            var result = service.RetrieveMultiple(query);
            all.AddRange(result.Entities);
            if (!result.MoreRecords) break;
            query.PageInfo.PageNumber++;
            query.PageInfo.PagingCookie = result.PagingCookie;
        }
        return all;
    }

    /// <summary>
    /// 读取 componenttype 类属性（兼容 OptionSetValue / int 两种存储形式）。
    /// </summary>
    static int ReadComponentType(Microsoft.Xrm.Sdk.Entity entity, string attributeName)
    {
        if (!entity.Attributes.TryGetValue(attributeName, out var value) || value == null) return -1;
        if (value is OptionSetValue osv) return osv.Value;
        if (value is int i) return i;
        if (value is AliasedValue av) return ReadComponentType(new Microsoft.Xrm.Sdk.Entity { [attributeName] = av.Value }, attributeName);
        return -1;
    }

    /// <summary>
    /// 数组分块。
    /// </summary>
    static IEnumerable<List<T>> Chunk<T>(IReadOnlyList<T> source, int size)
    {
        for (var i = 0; i < source.Count; i += size)
            yield return source.Skip(i).Take(size).ToList();
    }

    /// <summary>
    /// componenttype → 中文分组标签。
    /// </summary>
    static string ComponentTypeLabel(int componentType) => componentType switch
    {
        1 => "Entity",
        2 => "Attribute",
        9 => "OptionSet",
        10 => "EntityRelationship",
        24 => "SystemForm",
        26 => "SavedQuery",
        29 => "Workflow",
        60 => "SystemForm",
        61 => "WebResource",
        90 => "PluginType",
        91 => "PluginAssembly",
        92 => "PluginStep",
        10023 => "CustomAPI",
        10024 => "CustomAPIRequestParameter",
        10025 => "CustomAPIResponseProperty",
        _ => $"ComponentType{componentType}"
    };

    /// <summary>
    /// 批量解析缺失依赖的显示名。按 componenttype 分组查对应表；Attribute(2) 用 RetrieveAttributeRequest；
    /// 未知类型尝试 appaction 表；单项失败优雅降级（不返回键即视为无法解析）。
    /// </summary>
    static Dictionary<Guid, string> ResolveDependencyNames(ServiceClient service, HashSet<(int Type, Guid Id)> missing)
    {
        var names = new Dictionary<Guid, string>();

        // componenttype → (表名, 名称字段, 主键字段)（主键不符合 "表名+id" 规律的需显式指定，如 systemform→formid）
        var tableMap = new Dictionary<int, (string Table, string NameField, string PrimaryKey)>
        {
            [1] = ("entity", "name", "entityid"),
            [9] = ("optionset", "name", "optionsetid"),
            [10] = ("entityrelationship", "schemaname", "entityrelationshipid"),
            [24] = ("systemform", "name", "formid"),
            [26] = ("savedquery", "name", "savedqueryid"),
            [29] = ("workflow", "name", "workflowid"),
            [60] = ("systemform", "name", "formid"),
            [61] = ("webresource", "name", "webresourceid"),
            [90] = ("plugintype", "typename", "plugintypeid"),
            [91] = ("pluginassembly", "name", "pluginassemblyid"),
            [92] = ("sdkmessageprocessingstep", "name", "sdkmessageprocessingstepid"),
            [10023] = ("customapi", "uniquename", "customapiid"),
            [10024] = ("customapirequestparameter", "name", "customapirequestparameterid"),
            [10025] = ("customapiresponseproperty", "name", "customapiresponsepropertyid")
        };

        foreach (var group in missing.GroupBy(m => m.Type))
        {
            if (group.Key == 2)
            {
                // Attribute：RetrieveAttributeRequest（MetadataId）只能逐个调，并行提速（Clone 保证线程安全）
                var attrItems = group.ToList();
                var resolved = new System.Collections.Concurrent.ConcurrentDictionary<Guid, string>();
                var doneCount = 0;
                Parallel.ForEach(attrItems, new ParallelOptions { MaxDegreeOfParallelism = 16 }, item =>
                {
                    try
                    {
                        using var client = service.Clone();
                        var resp = (RetrieveAttributeResponse)client.Execute(new RetrieveAttributeRequest { MetadataId = item.Id });
                        var attr = resp.AttributeMetadata;
                        resolved[item.Id] = $"{attr.EntityLogicalName}.{attr.LogicalName}";
                    }
                    catch { /* 无法解析，降级 */ }
                    var done = Interlocked.Increment(ref doneCount);
                    if (done % 100 == 0 || done == attrItems.Count)
                        Console.WriteLine($"  Attribute 名称解析进度: {done}/{attrItems.Count}");
                });
                foreach (var kv in resolved) names[kv.Key] = kv.Value;
                continue;
            }

            if (!tableMap.TryGetValue(group.Key, out var map)) continue; // 未知类型后面统一走 appaction 兑底

            foreach (var chunk in Chunk(group.Select(g => g.Id).ToList(), 200))
            {
                List<Microsoft.Xrm.Sdk.Entity> records;
                try
                {
                    var query = new QueryExpression(map.Table)
                    {
                        ColumnSet = new ColumnSet(map.NameField),
                        Criteria = new FilterExpression
                        {
                            Conditions = { new ConditionExpression(map.PrimaryKey, ConditionOperator.In, chunk.Cast<object>().ToArray()) }
                        }
                    };
                    records = service.RetrieveMultiple(query).Entities.ToList();
                }
                catch (Exception ex) { Console.WriteLine($"  （诊断）名称解析查询失败 type={group.Key} table={map.Table}: {ex.Message}"); continue; }
                foreach (var r in records)
                    names[r.Id] = r.GetAttributeValue<string>(map.NameField) ?? r.Id.ToString();
            }
        }

        // 未解析的（未知类型或查询失败）：尝试 appaction 表
        var unresolved = missing.Where(m => !names.ContainsKey(m.Id)).Select(m => m.Id).ToList();
        foreach (var chunk in Chunk(unresolved, 200))
        {
            try
            {
                var query = new QueryExpression("appaction")
                {
                    ColumnSet = new ColumnSet("uniquename"),
                    Criteria = new FilterExpression
                    {
                        Conditions = { new ConditionExpression("appactionid", ConditionOperator.In, chunk.Cast<object>().ToArray()) }
                    }
                };
                foreach (var r in service.RetrieveMultiple(query).Entities)
                    names[r.Id] = r.GetAttributeValue<string>("uniquename") ?? r.Id.ToString();
            }
            catch { /* 忽略 */ }
        }

        return names;
    }

    /// <summary>
    /// 批量查组件还在哪些其他 Solution 中（仅 isvisible=true，排除发布集合内 Solution）。
    /// 返回 objectId → Solution uniquename 列表。
    /// </summary>
    static Dictionary<Guid, List<string>> FindOtherSolutionsOfComponents(ServiceClient service, List<Guid> objectIds, HashSet<Guid> publishSolutionIds)
    {
        var result = new Dictionary<Guid, List<string>>();
        var involvedSolutionIds = new HashSet<Guid>();
        var objectToSolutions = new Dictionary<Guid, List<Guid>>();

        foreach (var chunk in Chunk(objectIds, 200))
        {
            var query = new QueryExpression("solutioncomponent")
            {
                ColumnSet = new ColumnSet("objectid", "solutionid"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("objectid", ConditionOperator.In, chunk.Cast<object>().ToArray()) }
                }
            };
            foreach (var c in RetrieveAllPages(service, query))
            {
                var objectId = c.GetAttributeValue<Guid>("objectid");
                var sid = c.GetAttributeValue<EntityReference>("solutionid")?.Id ?? Guid.Empty;
                if (objectId == Guid.Empty || sid == Guid.Empty || publishSolutionIds.Contains(sid)) continue;
                if (!objectToSolutions.TryGetValue(objectId, out var list))
                    objectToSolutions[objectId] = list = new List<Guid>();
                if (!list.Contains(sid)) list.Add(sid);
                involvedSolutionIds.Add(sid);
            }
        }

        if (involvedSolutionIds.Count == 0) return result;

        // 批量取 Solution uniquename（仅 isvisible=true）
        var solutionNames = new Dictionary<Guid, string>();
        foreach (var chunk in Chunk(involvedSolutionIds.ToList(), 200))
        {
            var query = new QueryExpression("solution")
            {
                ColumnSet = new ColumnSet("uniquename"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("solutionid", ConditionOperator.In, chunk.Cast<object>().ToArray()),
                        new ConditionExpression("isvisible", ConditionOperator.Equal, true)
                    }
                }
            };
            foreach (var s in service.RetrieveMultiple(query).Entities)
                solutionNames[s.Id] = s.GetAttributeValue<string>("uniquename") ?? s.Id.ToString();
        }

        foreach (var (objectId, sids) in objectToSolutions)
        {
            var names = sids.Where(solutionNames.ContainsKey).Select(s => solutionNames[s]).ToList();
            if (names.Count > 0) result[objectId] = names;
        }
        return result;
    }

    static void ListPluginAssemblies(ServiceClient service, string? prefix)
    {
        Console.WriteLine($">>> 列出 Plugin Assembly{(prefix != null ? " (前缀: " + prefix + ")" : "")}");

        var query = new QueryExpression("pluginassembly")
        {
            ColumnSet = new ColumnSet("pluginassemblyid", "name", "culture", "version", "publickeytoken"),
            Criteria = prefix != null
                ? new FilterExpression { Conditions = { new ConditionExpression("name", ConditionOperator.BeginsWith, prefix) } }
                : new FilterExpression()
        };
        query.AddOrder("name", OrderType.Ascending);

        var assemblies = service.RetrieveMultiple(query).Entities;
        Console.WriteLine($"  找到 {assemblies.Count} 个 Plugin Assembly");

        foreach (var asm in assemblies)
        {
            var asmId = asm.Id;
            var name = asm.GetAttributeValue<string>("name") ?? "";
            var version = asm.GetAttributeValue<string>("version") ?? "";
            var publicKeyToken = asm.GetAttributeValue<string>("publickeytoken") ?? "";

            // 查询所属 Solution
            var compQuery = new QueryExpression("solutioncomponent")
            {
                ColumnSet = new ColumnSet("solutionid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("componenttype", ConditionOperator.Equal, 91),
                        new ConditionExpression("objectid", ConditionOperator.Equal, asmId)
                    }
                }
            };
            var compResult = service.RetrieveMultiple(compQuery).Entities;
            var solutionNames = new List<string>();
            foreach (var comp in compResult)
            {
                var sid = comp.GetAttributeValue<Microsoft.Xrm.Sdk.EntityReference>("solutionid")?.Id ?? Guid.Empty;
                if (sid == Guid.Empty) continue;
                var s = service.Retrieve("solution", sid, new ColumnSet("uniquename", "friendlyname", "ismanaged"));
                if (s != null)
                {
                    var sName = s.GetAttributeValue<string>("uniquename") ?? "";
                    var sManaged = s.GetAttributeValue<bool?>("ismanaged") ?? false;
                    solutionNames.Add($"{sName}({(sManaged ? "托管" : "非托管")})");
                }
            }

            Console.WriteLine($"  - {name} [v{version}] [token={publicKeyToken}]");
            Console.WriteLine($"    Solutions: {(solutionNames.Count > 0 ? string.Join(", ", solutionNames) : "未找到")}");
        }
    }

    static void QueryEntitySolutions(ServiceClient service, string entityName)
    {
        Console.WriteLine($">>> 查询实体所属 Solution: {entityName}");

        var entityQuery = new QueryExpression("entity")
        {
            ColumnSet = new ColumnSet("entityid", "name"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("name", ConditionOperator.Equal, entityName) }
            }
        };
        var entities = service.RetrieveMultiple(entityQuery).Entities;
        if (entities.Count == 0)
        {
            Console.WriteLine($"  ❌ 未找到实体: {entityName}");
            return;
        }
        var entityId = entities[0].Id;

        var compQuery = new QueryExpression("solutioncomponent")
        {
            ColumnSet = new ColumnSet("solutionid"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("componenttype", ConditionOperator.Equal, 1),
                    new ConditionExpression("objectid", ConditionOperator.Equal, entityId)
                }
            }
        };
        var compResult = service.RetrieveMultiple(compQuery).Entities;
        Console.WriteLine($"  找到 {compResult.Count} 个 Solution 关联");

        foreach (var comp in compResult)
        {
            var sid = comp.GetAttributeValue<Microsoft.Xrm.Sdk.EntityReference>("solutionid")?.Id ?? Guid.Empty;
            if (sid == Guid.Empty) continue;
            var s = service.Retrieve("solution", sid, new ColumnSet("uniquename", "friendlyname", "ismanaged"));
            if (s != null)
            {
                var sName = s.GetAttributeValue<string>("uniquename") ?? "";
                var sFriendly = s.GetAttributeValue<string>("friendlyname") ?? "";
                var sManaged = s.GetAttributeValue<bool?>("ismanaged") ?? false;
                Console.WriteLine($"    - {sName} ({sFriendly}) [{(sManaged ? "托管" : "非托管")}]");
            }
        }
    }

    /// <summary>
    /// 查询指定 Custom API 及其实现 Plugin 所在的 Solution
    /// </summary>
    static void QueryCustomApiSolution(ServiceClient service, string uniqueName)
    {
        Console.WriteLine($"\n=== 查询 Custom API 所在 Solution: {uniqueName} ===\n");

        // 1. 先用 uniquename 精确查 Custom API；未命中则再用 name/displayname 模糊查
        var apiQuery = new QueryExpression("customapi")
        {
            ColumnSet = new ColumnSet("customapiid", "name", "uniquename", "plugintypeid"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("uniquename", ConditionOperator.Equal, uniqueName) }
            }
        };
        var apiResult = service.RetrieveMultiple(apiQuery);
        if (apiResult.Entities.Count == 0)
        {
            var fuzzyQuery = new QueryExpression("customapi")
            {
                ColumnSet = new ColumnSet("customapiid", "name", "uniquename", "plugintypeid"),
                Criteria = new FilterExpression(LogicalOperator.Or)
                {
                    Conditions =
                    {
                        new ConditionExpression("name", ConditionOperator.Like, $"%{uniqueName}%"),
                        new ConditionExpression("displayname", ConditionOperator.Like, $"%{uniqueName}%"),
                        new ConditionExpression("uniquename", ConditionOperator.Like, $"%{uniqueName}%")
                    }
                }
            };
            apiResult = service.RetrieveMultiple(fuzzyQuery);
        }

        if (apiResult.Entities.Count == 0)
        {
            Console.WriteLine($"❌ 未在 customapi 表中找到: {uniqueName}");
            Console.WriteLine("   正在检查是否以 SdkMessage / Plugin Step 形式存在...\n");

            // 额外诊断：按 Message 名反查 Plugin Steps
            var msgQuery = new QueryExpression("sdkmessage")
            {
                ColumnSet = new ColumnSet("sdkmessageid", "name"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("name", ConditionOperator.Equal, uniqueName) }
                }
            };
            var msgResult = service.RetrieveMultiple(msgQuery);
            if (msgResult.Entities.Count == 0)
            {
                Console.WriteLine($"   SdkMessage 表中也没有 name='{uniqueName}' 的记录");
                Console.WriteLine($"   结论：DEV1 中不存在名为 {uniqueName} 的 Custom API 或 Plugin Step");
                return;
            }

            foreach (var msg in msgResult.Entities)
            {
                var msgId = msg.GetAttributeValue<Guid>("sdkmessageid");
                var msgName = msg.GetAttributeValue<string>("name");
                Console.WriteLine($"   找到 SdkMessage: {msgName} ({msgId})");

                var stepQuery = new QueryExpression("sdkmessageprocessingstep")
                {
                    ColumnSet = new ColumnSet("sdkmessageprocessingstepid", "name", "plugintypeid", "stage", "mode"),
                    Criteria = new FilterExpression
                    {
                        Conditions = { new ConditionExpression("sdkmessageid", ConditionOperator.Equal, msgId) }
                    }
                };
                var steps = service.RetrieveMultiple(stepQuery).Entities;
                if (steps.Count == 0)
                {
                    Console.WriteLine("   该 Message 下没有注册 Plugin Step");
                }
                else
                {
                    Console.WriteLine($"   关联 Plugin Steps ({steps.Count}):");
                    foreach (var step in steps)
                    {
                        var stepId = step.GetAttributeValue<Guid>("sdkmessageprocessingstepid");
                        var stepName = step.GetAttributeValue<string>("name") ?? "(no name)";
                        var ptRef = step.GetAttributeValue<EntityReference>("plugintypeid");
                        var stage = step.GetAttributeValue<OptionSetValue>("stage")?.Value ?? -1;
                        var mode = step.GetAttributeValue<OptionSetValue>("mode")?.Value ?? -1;
                        Console.WriteLine($"     - {stepName} ({stepId})");
                        Console.WriteLine($"       Plugin Type: {ptRef?.Name ?? "N/A"} ({ptRef?.Id.ToString() ?? "N/A"})");
                        Console.WriteLine($"       Stage: {stage}, Mode: {mode}");

                        // 继续查询该 Step 的实现 Plugin 所在 Solution
                        if (ptRef != null && ptRef.Id != Guid.Empty)
                        {
                            QueryAndPrintSolution(service, ptRef.Id, "Plugin Type", fallbackToAssembly: true);
                        }
                    }
                }
            }
            return;
        }

        foreach (var api in apiResult.Entities)
        {
            var apiId = api.GetAttributeValue<Guid>("customapiid");
            var actualUniqueName = api.GetAttributeValue<string>("uniquename") ?? uniqueName;
            var apiName = api.GetAttributeValue<string>("name");
            var pluginTypeRef = api.GetAttributeValue<EntityReference>("plugintypeid");
            var pluginTypeId = pluginTypeRef?.Id ?? Guid.Empty;
            var pluginTypeName = pluginTypeRef?.Name ?? "N/A";

            Console.WriteLine($"Custom API: {actualUniqueName}");
            if (!string.IsNullOrEmpty(apiName) && apiName != actualUniqueName)
                Console.WriteLine($"  Name: {apiName}");
            Console.WriteLine($"  ID: {apiId}");
            Console.WriteLine($"  实现 Plugin Type: {pluginTypeName} ({pluginTypeId})");

            // 2. 查 Custom API 所在的 Solution
            QueryAndPrintSolution(service, apiId, "Custom API");

            // 3. 查实现 Plugin Type 所在的 Solution（如 Plugin Type 本身未作为 component，则回退到其所属 Plugin Assembly）
            if (pluginTypeId != Guid.Empty)
            {
                QueryAndPrintSolution(service, pluginTypeId, "Plugin Type", fallbackToAssembly: true);
            }

            Console.WriteLine();
        }
    }

    static void QueryAndPrintSolution(ServiceClient service, Guid objectId, string componentType, bool fallbackToAssembly = false)
    {
        var compQuery = new QueryExpression("solutioncomponent")
        {
            ColumnSet = new ColumnSet("solutionid", "componenttype"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("objectid", ConditionOperator.Equal, objectId) }
            }
        };
        var comps = service.RetrieveMultiple(compQuery).Entities;

        // Plugin Type 通常不会作为独立 solutioncomponent，回退查询所属 Plugin Assembly
        if (comps.Count == 0 && fallbackToAssembly && componentType == "Plugin Type")
        {
            var ptQuery = new QueryExpression("plugintype")
            {
                ColumnSet = new ColumnSet("pluginassemblyid"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("plugintypeid", ConditionOperator.Equal, objectId) }
                }
            };
            var ptResult = service.RetrieveMultiple(ptQuery).Entities;
            if (ptResult.Count > 0)
            {
                var assemblyRef = ptResult[0].GetAttributeValue<EntityReference>("pluginassemblyid");
                var assemblyId = assemblyRef?.Id ?? Guid.Empty;
                var assemblyName = assemblyRef?.Name ?? "Unknown";
                if (assemblyId != Guid.Empty)
                {
                    Console.WriteLine($"  Plugin Type 未作为独立组件，回退查询所属 Plugin Assembly: {assemblyName} ({assemblyId})");
                    QueryAndPrintSolution(service, assemblyId, "Plugin Assembly");
                    return;
                }
            }
        }

        if (comps.Count == 0)
        {
            Console.WriteLine($"  {componentType}: 未在任何 Solution 中找到");
            return;
        }

        foreach (var comp in comps)
        {
            var solutionId = comp.GetAttributeValue<EntityReference>("solutionid")?.Id ?? Guid.Empty;
            var solutionQuery = new QueryExpression("solution")
            {
                ColumnSet = new ColumnSet("friendlyname", "uniquename"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("solutionid", ConditionOperator.Equal, solutionId) }
                }
            };
            var solutionResult = service.RetrieveMultiple(solutionQuery);
            var solutionName = solutionResult.Entities.Count > 0
                ? $"{solutionResult.Entities[0].GetAttributeValue<string>("friendlyname")} ({solutionResult.Entities[0].GetAttributeValue<string>("uniquename")})"
                : $"Unknown ({solutionId})";
            Console.WriteLine($"  {componentType}: 在 Solution {solutionName} 中");
        }
    }

    /// <summary>
    /// 查询系统自动编号配置 ms_numbergenerateconfiguration
    /// </summary>
    static void ListNumberGenerateConfigurations(ServiceClient service, string? entityName)
    {
        var query = new QueryExpression("ms_numbergenerateconfiguration")
        {
            ColumnSet = new ColumnSet(
                "ms_numbergenerateconfigurationid",
                "ms_name",
                "ms_attributename",
                "ms_prefixtemplate",
                "ms_numbertemplate",
                "ms_serialnolength",
                "ms_serialnostart",
                "ms_useserialnoservice",
                "statecode",
                "createdon"
            ),
            Orders = { new OrderExpression("ms_name", OrderType.Ascending) }
        };

        if (!string.IsNullOrWhiteSpace(entityName))
        {
            query.Criteria.AddCondition("ms_name", ConditionOperator.Equal, entityName);
        }

        var result = service.RetrieveMultiple(query);

        if (result.Entities.Count == 0)
        {
            Console.WriteLine($"未找到 {(string.IsNullOrWhiteSpace(entityName) ? "任何" : $"{entityName} 的")}自动编号配置");
            return;
        }

        Console.WriteLine($"共找到 {result.Entities.Count} 条配置:\n");
        foreach (var e in result.Entities)
        {
            Console.WriteLine($"实体: {e.GetAttributeValue<string>("ms_name")}");
            Console.WriteLine($"  配置ID: {e.Id}");
            Console.WriteLine($"  属性名: {e.GetAttributeValue<string>("ms_attributename")}");
            Console.WriteLine($"  前缀模板: {e.GetAttributeValue<string>("ms_prefixtemplate")}");
            Console.WriteLine($"  数字模板: {e.GetAttributeValue<string>("ms_numbertemplate")}");
            Console.WriteLine($"  序列号长度: {e.GetAttributeValue<int>("ms_serialnolength")}");
            Console.WriteLine($"  序号开始: {e.GetAttributeValue<int>("ms_serialnostart")}");
            Console.WriteLine($"  使用序列号服务: {e.GetAttributeValue<bool>("ms_useserialnoservice")}");
            Console.WriteLine($"  状态: {(e.GetAttributeValue<OptionSetValue>("statecode")?.Value == 0 ? "启用" : "停用")}");
            Console.WriteLine($"  创建时间: {e.GetAttributeValue<DateTime>("createdon")}");
            Console.WriteLine();
        }
    }

    /// <summary>
    /// 新增系统自动编号配置 ms_numbergenerateconfiguration
    /// 创建前会检查同一实体+字段是否已有启用配置，避免重复
    /// </summary>
    static void CreateNumberGenerateConfiguration(ServiceClient service, string entityName, string attributeName, string prefixTemplate, string numberTemplate, int serialNoLength, int serialNoStart, bool useSerialNoService = true)
    {
        // 先检查是否已存在启用配置
        var checkQuery = new QueryExpression("ms_numbergenerateconfiguration")
        {
            ColumnSet = new ColumnSet("ms_numbergenerateconfigurationid", "ms_name", "ms_attributename"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("ms_name", ConditionOperator.Equal, entityName),
                    new ConditionExpression("ms_attributename", ConditionOperator.Equal, attributeName),
                    new ConditionExpression("statecode", ConditionOperator.Equal, 0)
                }
            }
        };

        var existing = service.RetrieveMultiple(checkQuery);
        if (existing.Entities.Count > 0)
        {
            Console.WriteLine($"⚠️ 已存在 {entityName}.{attributeName} 的启用自动编号配置，不会重复创建");
            return;
        }

        var entity = new Entity("ms_numbergenerateconfiguration");
        entity["ms_name"] = entityName;
        entity["ms_attributename"] = attributeName;
        entity["ms_prefixtemplate"] = prefixTemplate;
        entity["ms_numbertemplate"] = numberTemplate;
        entity["ms_serialnolength"] = serialNoLength;
        entity["ms_serialnostart"] = serialNoStart;
        entity["ms_useserialnoservice"] = useSerialNoService;

        var id = service.Create(entity);
        Console.WriteLine($"✅ 已创建自动编号配置: {id}");
        Console.WriteLine($"   实体: {entityName}");
        Console.WriteLine($"   属性: {attributeName}");
        Console.WriteLine($"   前缀模板: {prefixTemplate}");
        Console.WriteLine($"   数字模板: {numberTemplate}");
        Console.WriteLine($"   序列号长度: {serialNoLength}");
        Console.WriteLine($"   序号开始: {serialNoStart}");
        Console.WriteLine($"   使用序列号服务: {useSerialNoService}");
    }

    /// <summary>
    /// 为指定实体注册通用自动编号 Plugin Step
    /// 使用 DEV1 中已存在的 MSLibrary.D365.Common.Plugins.EntityValidateCreateForGenerateNumber Plugin Type
    /// 不更新任何 Assembly 内容，仅新增 sdkmessageprocessingstep 记录
    /// </summary>
    static void RegisterNumberGenerateStep(ServiceClient service, string entityName)
    {
        const string className = "MSLibrary.D365.Common.Plugins.EntityValidateCreateForGenerateNumber";
        const string messageName = "Create";
        const int stage = 10; // PreValidation
        const int mode = 0;   // Synchronous

        // 1. 查找已存在的 Plugin Type（优先使用 SanyD365.D365Extension Assembly 中的）
        var ptQuery = new QueryExpression("plugintype")
        {
            ColumnSet = new ColumnSet("plugintypeid", "pluginassemblyid", "name"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("typename", ConditionOperator.Equal, className)
                }
            }
        };

        var ptResults = service.RetrieveMultiple(ptQuery).Entities;
        if (ptResults.Count == 0)
        {
            Console.WriteLine($"❌ 找不到 Plugin Type: {className}");
            return;
        }

        // 优先使用 SanyD365.D365Extension Assembly 中的 Plugin Type（与 mcs_partwarningmessage 一致）
        var targetPt = ptResults.FirstOrDefault(pt =>
        {
            var assemblyRef = pt.GetAttributeValue<EntityReference>("pluginassemblyid");
            return assemblyRef?.Name == "SanyD365.D365Extension";
        }) ?? ptResults.First();

        var pluginTypeId = targetPt.Id;
        var assemblyName = targetPt.GetAttributeValue<EntityReference>("pluginassemblyid")?.Name ?? "Unknown";
        Console.WriteLine($"✅ 使用 Plugin Type: {pluginTypeId} (Assembly: {assemblyName})");

        // 2. 查找 Create 消息
        var msgQuery = new QueryExpression("sdkmessage")
        {
            ColumnSet = new ColumnSet("sdkmessageid"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("name", ConditionOperator.Equal, messageName) }
            }
        };
        var msgResults = service.RetrieveMultiple(msgQuery).Entities;
        if (msgResults.Count == 0)
        {
            Console.WriteLine($"❌ 找不到消息: {messageName}");
            return;
        }
        var messageId = msgResults[0].Id;

        // 3. 查找消息过滤器
        var filterQuery = new QueryExpression("sdkmessagefilter")
        {
            ColumnSet = new ColumnSet("sdkmessagefilterid"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("sdkmessageid", ConditionOperator.Equal, messageId),
                    new ConditionExpression("primaryobjecttypecode", ConditionOperator.Equal, entityName)
                }
            }
        };
        var filterResults = service.RetrieveMultiple(filterQuery).Entities;
        if (filterResults.Count == 0)
        {
            Console.WriteLine($"❌ 找不到消息过滤器: {messageName} of {entityName}");
            return;
        }
        var filterId = filterResults[0].Id;

        // 4. 检查 Step 是否已存在
        var stepQuery = new QueryExpression("sdkmessageprocessingstep")
        {
            ColumnSet = new ColumnSet("sdkmessageprocessingstepid"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("plugintypeid", ConditionOperator.Equal, pluginTypeId),
                    new ConditionExpression("sdkmessagefilterid", ConditionOperator.Equal, filterId),
                    new ConditionExpression("stage", ConditionOperator.Equal, stage)
                }
            }
        };
        var existingSteps = service.RetrieveMultiple(stepQuery).Entities;
        if (existingSteps.Count > 0)
        {
            Console.WriteLine($"⚠️ Step 已存在: {existingSteps[0].Id}");
            return;
        }

        // 5. 创建 Step
        var step = new Entity("sdkmessageprocessingstep");
        step["plugintypeid"] = new EntityReference("plugintype", pluginTypeId);
        step["sdkmessageid"] = new EntityReference("sdkmessage", messageId);
        step["sdkmessagefilterid"] = new EntityReference("sdkmessagefilter", filterId);
        step["name"] = $"{className.Split('.').Last()}: {messageName} of {entityName}";
        step["stage"] = new OptionSetValue(stage);
        step["mode"] = new OptionSetValue(mode);
        step["rank"] = 1;
        step["supporteddeployment"] = new OptionSetValue(0);

        var stepId = service.Create(step);
        Console.WriteLine($"✅ 已注册自动编号 Step: {stepId}");
        Console.WriteLine($"   Plugin: {className}");
        Console.WriteLine($"   实体: {entityName}");
        Console.WriteLine($"   消息: {messageName}");
        Console.WriteLine($"   阶段: PreValidation");
        Console.WriteLine($"   模式: Synchronous");
    }

    /// <summary>
    /// 测试自动编号配置：创建一条测试记录，查看生成的编号，然后删除
    /// 当前仅支持 mcs_fca_mdlversion，其他实体可类似扩展
    /// </summary>
    static void TestNumberGenerateConfiguration(ServiceClient service, string entityName, string? attributeName, bool keepRecord = false)
    {
        var attr = attributeName ?? GetDefaultNumberAttribute(entityName);
        if (string.IsNullOrEmpty(attr))
        {
            Console.WriteLine($"❌ 未找到 {entityName} 的默认编号字段，请显式指定属性名");
            return;
        }

        Entity testEntity;
        try
        {
            testEntity = BuildTestEntity(service, entityName);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 构建测试记录失败: {ex.Message}");
            return;
        }

        try
        {
            var id = service.Create(testEntity);
            Console.WriteLine($"✅ 测试记录已创建: {id}");

            var created = service.Retrieve(entityName, id, new ColumnSet(attr));
            var generatedValue = created.GetAttributeValue<string>(attr);
            Console.WriteLine($"   生成编号: {generatedValue}");

            if (keepRecord)
            {
                Console.WriteLine($"✅ --keep 已指定，测试记录保留: {id}");
            }
            else
            {
                service.Delete(entityName, id);
                Console.WriteLine($"✅ 测试记录已删除");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 测试失败: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"   内部错误: {ex.InnerException.Message}");
            }
        }
    }

    static string? GetDefaultNumberAttribute(string entityName)
    {
        return entityName.ToLower() switch
        {
            "mcs_fca_mdlversion" => "mcs_versionid",
            "mcs_fca_proc" => "mcs_doid",
            "mcs_fca_quotaapp" => "mcs_grantid",
            "mcs_fca_records" => "mcs_recordid",
            "mcs_fsm_resource" => "mcs_fsm_resource_no",
            _ => null
        };
    }

    static Entity BuildTestEntity(ServiceClient service, string entityName)
    {
        var entity = new Entity(entityName);

        switch (entityName.ToLower())
        {
            case "mcs_fca_mdlversion":
                entity["mcs_isactive"] = new OptionSetValue(1);
                entity["mcs_modeldesc"] = "自动编号测试记录";
                entity["mcs_validfrom"] = DateTime.UtcNow.Date;
                entity["mcs_validend"] = new DateTime(9999, 12, 31);
                break;

            case "mcs_fca_proc":
                // mcs_accountid 实际指向 mcs_customermasterdata，查找一条可用记录
                const string customerEntity = "mcs_customermasterdata";
                var customerQuery = new QueryExpression(customerEntity)
                {
                    ColumnSet = new ColumnSet("mcs_customermasterdataid", "mcs_name"),
                    Orders = { new OrderExpression("createdon", OrderType.Descending) },
                    TopCount = 1
                };
                var customers = service.RetrieveMultiple(customerQuery).Entities;
                if (customers.Count == 0)
                {
                    throw new InvalidOperationException($"环境中没有 {customerEntity} 记录，无法创建 mcs_fca_proc 测试记录");
                }
                var customer = customers[0];
                entity["mcs_accountid"] = new EntityReference(customerEntity, customer.Id);
                entity["mcs_custname"] = $"测试客户-{customer.GetAttributeValue<string>("mcs_name") ?? customer.Id.ToString().Substring(0, 8)}";
                entity["mcs_modeldesc"] = "自动编号测试记录";
                entity["mcs_status"] = new OptionSetValue(1); // 假设 1 为有效状态
                break;

            case "mcs_fca_records":
                // 台账：mcs_accountid 指向 mcs_customermasterdata，取一条可用客户
                var ledgerCustomers = service.RetrieveMultiple(new QueryExpression("mcs_customermasterdata")
                {
                    ColumnSet = new ColumnSet("mcs_customermasterdataid", "mcs_name", "mcs_sapnumber"),
                    Orders = { new OrderExpression("createdon", OrderType.Descending) },
                    TopCount = 1
                }).Entities;
                if (ledgerCustomers.Count == 0)
                {
                    throw new InvalidOperationException("环境中没有 mcs_customermasterdata 记录，无法创建 mcs_fca_records 测试记录");
                }
                var ledgerCustomer = ledgerCustomers[0];
                entity["mcs_accountid"] = new EntityReference("mcs_customermasterdata", ledgerCustomer.Id);
                entity["mcs_custname"] = ledgerCustomer.GetAttributeValue<string>("mcs_sapnumber")
                    ?? ledgerCustomer.GetAttributeValue<string>("mcs_name");
                entity["mcs_proccess"] = new OptionSetValue(1); // 环节：厂端授信模型计算
                entity["mcs_adjust"] = new OptionSetValue(1);   // 调整类型：初始化
                entity["mcs_asisbalance"] = new Money(0m);
                entity["mcs_adjustamt"] = new Money(0m);
                entity["mcs_tobebalance"] = new Money(0m);
                break;

            case "mcs_fca_quotaapp":
                // 客户编码
                const string quotaCustomerEntity = "mcs_customermasterdata";
                var quotaCustomerQuery = new QueryExpression(quotaCustomerEntity)
                {
                    ColumnSet = new ColumnSet("mcs_customermasterdataid", "mcs_name"),
                    Orders = { new OrderExpression("createdon", OrderType.Descending) },
                    TopCount = 1
                };
                var quotaCustomers = service.RetrieveMultiple(quotaCustomerQuery).Entities;
                if (quotaCustomers.Count == 0)
                {
                    throw new InvalidOperationException($"环境中没有 {quotaCustomerEntity} 记录，无法创建 mcs_fca_quotaapp 测试记录");
                }
                var quotaCustomer = quotaCustomers[0];
                entity["mcs_accountid"] = new EntityReference(quotaCustomerEntity, quotaCustomer.Id);

                // 产品类型（mcs_typename），动态查询其 Lookup 目标实体
                var typeAttrRequest = new RetrieveAttributeRequest
                {
                    EntityLogicalName = "mcs_fca_quotaapp",
                    LogicalName = "mcs_typename",
                    RetrieveAsIfPublished = true
                };
                var typeAttrResponse = (RetrieveAttributeResponse)service.Execute(typeAttrRequest);
                var typeTargets = (typeAttrResponse.AttributeMetadata as LookupAttributeMetadata)?.Targets
                    ?? throw new InvalidOperationException("mcs_typename 不是 Lookup 字段");
                var typeEntityName = typeTargets.FirstOrDefault()
                    ?? throw new InvalidOperationException("mcs_typename 没有目标实体");

                var typeQuery = new QueryExpression(typeEntityName)
                {
                    ColumnSet = new ColumnSet($"{typeEntityName}id"),
                    TopCount = 1
                };
                var typeResults = service.RetrieveMultiple(typeQuery).Entities;
                if (typeResults.Count == 0)
                {
                    throw new InvalidOperationException($"环境中没有 {typeEntityName} 记录，无法创建 mcs_fca_quotaapp 测试记录");
                }
                entity["mcs_typename"] = new EntityReference(typeEntityName, typeResults[0].Id);

                entity["mcs_contractamt"] = new Money(0m);
                entity["mcs_payterm"] = 30;
                entity["mcs_quotabalance"] = new Money(0m);
                entity["mcs_quotasum"] = new Money(0m);
                entity["mcs_remark"] = "自动编号测试记录";
                entity["mcs_sellerbalance"] = new Money(0m);
                entity["mcs_sellergrant"] = new Money(0m);
                entity["mcs_tobebalance"] = new Money(0m);
                entity["mcs_tobegrant"] = new Money(0m);
                break;

            case "mcs_fsm_resource":
                // 融资资源管理：必填 Lookup 取国家/州省/城市主数据各一条
                var fsmCountry = service.RetrieveMultiple(new QueryExpression("mcs_country")
                {
                    ColumnSet = new ColumnSet("mcs_countryid"),
                    TopCount = 1
                }).Entities.FirstOrDefault()
                    ?? throw new InvalidOperationException("环境中没有 mcs_country 记录，无法创建 mcs_fsm_resource 测试记录");
                var fsmState = service.RetrieveMultiple(new QueryExpression("mcs_state")
                {
                    ColumnSet = new ColumnSet("mcs_stateid"),
                    TopCount = 1
                }).Entities.FirstOrDefault()
                    ?? throw new InvalidOperationException("环境中没有 mcs_state 记录，无法创建 mcs_fsm_resource 测试记录");
                var fsmCity = service.RetrieveMultiple(new QueryExpression("mcs_city")
                {
                    ColumnSet = new ColumnSet("mcs_cityid"),
                    TopCount = 1
                }).Entities.FirstOrDefault()
                    ?? throw new InvalidOperationException("环境中没有 mcs_city 记录，无法创建 mcs_fsm_resource 测试记录");
                // 编号字段留空，由通用自动编号插件生成
                entity["mcs_fsm_institution_type"] = new OptionSetValue(1);
                entity["mcs_fsm_institution_code"] = "AUTOTEST";
                entity["mcs_fsm_institution_name"] = "自动编号测试机构";
                entity["mcs_fsm_institution_country"] = new EntityReference("mcs_country", fsmCountry.Id);
                entity["mcs_fsm_institution_province"] = new EntityReference("mcs_state", fsmState.Id);
                entity["mcs_fsm_institution_city"] = new EntityReference("mcs_city", fsmCity.Id);
                entity["mcs_fsm_institution_address"] = "自动编号测试地址";
                entity["mcs_fsm_institution_contact"] = "测试联系人";
                entity["mcs_fsm_institution_contact_position"] = "测试职位";
                entity["mcs_fsm_institution_contact_tel"] = "123456789";
                entity["mcs_fsm_institution_contact_email"] = "test@test.com";
                entity["mcs_fsm_institution_desc"] = "自动编号测试记录";
                entity["mcs_fsm_institution_products"] = new OptionSetValueCollection { new OptionSetValue(1) };
                entity["mcs_fsm_institution_product_remark"] = "自动编号测试备注";
                entity["mcs_fsm_status"] = true;
                entity["mcs_fsm_rl_status"] = false;
                break;

            default:
                throw new NotSupportedException($"暂不支持为 {entityName} 自动构建测试记录");
        }

        return entity;
    }

    /// <summary>
    /// 删除指定实体+字段的自动编号配置
    /// </summary>
    static void DeleteNumberGenerateConfiguration(ServiceClient service, string entityName, string attributeName)
    {
        var query = new QueryExpression("ms_numbergenerateconfiguration")
        {
            ColumnSet = new ColumnSet("ms_numbergenerateconfigurationid"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("ms_name", ConditionOperator.Equal, entityName),
                    new ConditionExpression("ms_attributename", ConditionOperator.Equal, attributeName)
                }
            }
        };

        var result = service.RetrieveMultiple(query);
        if (result.Entities.Count == 0)
        {
            Console.WriteLine($"未找到 {entityName}.{attributeName} 的自动编号配置");
            return;
        }

        foreach (var e in result.Entities)
        {
            service.Delete("ms_numbergenerateconfiguration", e.Id);
            Console.WriteLine($"✅ 已删除自动编号配置: {e.Id} ({entityName}.{attributeName})");
        }
    }

    /// <summary>
    /// 测试厂端授信模型计算生效启用 Plugin
    /// 创建测试 proc 记录（状态=2），再更新为状态=3，验证 quota/records 是否生成
    /// </summary>
    static void TestFcaProcActivation(ServiceClient service)
    {
        Console.WriteLine("=== 测试 mcs_fca_proc 生效启用 Plugin ===");

        const string customerEntity = "mcs_customermasterdata";
        var customerQuery = new QueryExpression(customerEntity)
        {
            ColumnSet = new ColumnSet("mcs_customermasterdataid", "mcs_name"),
            Orders = { new OrderExpression("createdon", OrderType.Descending) },
            TopCount = 1
        };
        var customers = service.RetrieveMultiple(customerQuery).Entities;
        if (customers.Count == 0)
        {
            Console.WriteLine($"❌ 环境中没有 {customerEntity} 记录，无法测试");
            return;
        }

        var customer = customers[0];
        var customerRef = new EntityReference(customerEntity, customer.Id);
        string customerName = customer.GetAttributeValue<string>("mcs_name") ?? $"测试客户-{customer.Id.ToString().Substring(0, 8)}";
        Console.WriteLine($"使用客户: {customerName} ({customer.Id})");

        Guid procId = Guid.Empty;
        Guid? quotaIdToClean = null;
        Guid? recordIdToClean = null;

        try
        {
            // 1. 创建模型计算记录（状态=2）
            var proc = new Entity("mcs_fca_proc");
            proc["mcs_accountid"] = customerRef;
            proc["mcs_custname"] = customerName;
            proc["mcs_status"] = new OptionSetValue(2); // 模型计算
            proc["mcs_initigrant"] = new Money(12345.67m);
            proc["mcs_modelgrant"] = new Money(15000m);
            proc["mcs_modeldesc"] = "Plugin 生效启用测试";
            procId = service.Create(proc);
            Console.WriteLine($"✅ 已创建测试 proc 记录: {procId}");

            // 2. 更新状态为 3（生效启用），触发 Plugin
            var update = new Entity("mcs_fca_proc", procId);
            update["mcs_status"] = new OptionSetValue(3); // 生效启用
            service.Update(update);
            Console.WriteLine($"✅ 已更新 proc 状态为生效启用");

            // 3. 验证 mcs_fca_quota
            var quotaQuery = new QueryExpression("mcs_fca_quota")
            {
                ColumnSet = new ColumnSet("mcs_fca_quotaid", "mcs_sellergrant", "mcs_sellerbalance", "mcs_isactive", "mcs_doid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_accountid", ConditionOperator.Equal, customer.Id)
                    }
                }
            };
            var quotaResult = service.RetrieveMultiple(quotaQuery);
            if (quotaResult.Entities.Count == 0)
            {
                Console.WriteLine("❌ 未找到生成的 mcs_fca_quota 额度记录");
                return;
            }

            var quota = quotaResult.Entities[0];
            quotaIdToClean = quota.Id;
            var quotaGrant = quota.GetAttributeValue<Money>("mcs_sellergrant");
            var quotaBalance = quota.GetAttributeValue<Money>("mcs_sellerbalance");
            var quotaActive = quota.GetAttributeValue<OptionSetValue>("mcs_isactive")?.Value;
            Console.WriteLine($"✅ 额度记录: {quota.Id}");
            Console.WriteLine($"   厂端授信额度USD: {quotaGrant?.Value}");
            Console.WriteLine($"   厂端授信余额USD: {quotaBalance?.Value}");
            Console.WriteLine($"   是否生效: {quotaActive}");

            // 4. 验证 mcs_fca_records
            var recordQuery = new QueryExpression("mcs_fca_records")
            {
                ColumnSet = new ColumnSet("mcs_fca_recordsid", "mcs_recordid", "mcs_sellergrant", "mcs_tobebalance", "mcs_adjustamt", "mcs_proccess", "mcs_adjust"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_accountid", ConditionOperator.Equal, customer.Id)
                    }
                },
                Orders = { new OrderExpression("createdon", OrderType.Descending) },
                TopCount = 1
            };
            var recordResult = service.RetrieveMultiple(recordQuery);
            if (recordResult.Entities.Count == 0)
            {
                Console.WriteLine("❌ 未找到生成的 mcs_fca_records 台账记录");
                return;
            }

            var record = recordResult.Entities[0];
            recordIdToClean = record.Id;
            var recordNo = record.GetAttributeValue<string>("mcs_recordid");
            var recordGrant = record.GetAttributeValue<Money>("mcs_sellergrant");
            var recordToBe = record.GetAttributeValue<Money>("mcs_tobebalance");
            var recordAdjust = record.GetAttributeValue<Money>("mcs_adjustamt");
            var recordProcess = record.GetAttributeValue<OptionSetValue>("mcs_proccess")?.Value;
            var recordAdjustType = record.GetAttributeValue<OptionSetValue>("mcs_adjust")?.Value;
            Console.WriteLine($"✅ 台账记录: {record.Id}");
            Console.WriteLine($"   台账编号: {recordNo}");
            Console.WriteLine($"   授信限额USD: {recordGrant?.Value}");
            Console.WriteLine($"   调整后授信余额USD: {recordToBe?.Value}");
            Console.WriteLine($"   调整金额USD: {recordAdjust?.Value}");
            Console.WriteLine($"   流程环节: {recordProcess}, 调整动作: {recordAdjustType}");

            Console.WriteLine("\n🎉 Plugin 生效启用测试通过");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 测试失败: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"   内部错误: {ex.InnerException.Message}");
            }
        }
        finally
        {
            // 清理测试数据
            if (recordIdToClean.HasValue)
            {
                try { service.Delete("mcs_fca_records", recordIdToClean.Value); Console.WriteLine($"✅ 已删除测试台账记录: {recordIdToClean.Value}"); } catch { }
            }
            if (quotaIdToClean.HasValue)
            {
                try { service.Delete("mcs_fca_quota", quotaIdToClean.Value); Console.WriteLine($"✅ 已删除测试额度记录: {quotaIdToClean.Value}"); } catch { }
            }
            if (procId != Guid.Empty)
            {
                try { service.Delete("mcs_fca_proc", procId); Console.WriteLine($"✅ 已删除测试 proc 记录: {procId}"); } catch { }
            }
        }
    }

    /// <summary>
    /// 发送 D365 小铃铛（In-App Notification）测试通知。Bug #1654 预研：
    /// 调用绑定到 systemuser 的 SendAppNotification Action，验证通知中心可达性与样式。
    /// </summary>
    static void TestAppNotification(IOrganizationService service, string userDomain, string? title)
    {
        // 1. 查找目标用户
        var qe = new QueryExpression("systemuser")
        {
            TopCount = 1,
            ColumnSet = new ColumnSet("fullname", "domainname", "internalemailaddress"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("domainname", ConditionOperator.Equal, userDomain)
                }
            }
        };
        var users = service.RetrieveMultiple(qe);
        if (users.Entities.Count == 0)
        {
            Console.WriteLine($"未找到用户: {userDomain}");
            return;
        }
        var user = users.Entities[0];
        Console.WriteLine($"目标用户: {user.GetAttributeValue<string>("fullname")} (systemuserid={user.Id})");

        // 2. 调用 SendAppNotification（Recipient = 接收人，非绑定 Action）
        string notifTitle = string.IsNullOrWhiteSpace(title) ? "融资落实提醒（测试）" : title;
        string body = "这是一条测试通知：您负责的融资记录已进入【融资落实】阶段，请及时跟进处理。";

        var request = new OrganizationRequest("SendAppNotification")
        {
            ["Recipient"] = user.ToEntityReference(),
            ["Title"] = notifTitle,
            ["Body"] = body,
            ["IconType"] = new OptionSetValue(100000000),   // Info
            ["ToastType"] = new OptionSetValue(200000000),  // Timed（自动消失）
            ["Expiry"] = 86400                              // 保留 1 天（秒，必填）
        };
        try
        {
            var response = service.Execute(request);
            Console.WriteLine("✅ SendAppNotification 调用成功");
            foreach (var p in response.Results)
            {
                Console.WriteLine($"  返回参数: {p.Key} = {p.Value}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ SendAppNotification 调用失败: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"  内部异常: {ex.InnerException.Message}");
            }
        }

        // 3. 第三次：本环境不支持 Data 参数（操作按钮不可用），改为在正文中放记录链接验证可点击性
        string recordUrl = "https://dev1.crm5.dynamics.com/main.aspx?pagetype=apps";
        try
        {
            var fsmQe = new QueryExpression("mcs_fsm_data")
            {
                TopCount = 1,
                ColumnSet = new ColumnSet("mcs_fsm_no"),
                Orders = { new OrderExpression("createdon", OrderType.Descending) }
            };
            var fsmRecords = service.RetrieveMultiple(fsmQe);
            if (fsmRecords.Entities.Count > 0)
            {
                var fsm = fsmRecords.Entities[0];
                recordUrl = $"https://dev1.crm5.dynamics.com/main.aspx?pagetype=entityrecord&etn=mcs_fsm_data&id={fsm.Id}";
                Console.WriteLine($"链接目标记录: {fsm.GetAttributeValue<string>("mcs_fsm_no")} ({fsm.Id})");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ 查询融资记录失败，链接用兜底地址: {ex.Message}");
        }

        var request3 = new OrganizationRequest("SendAppNotification")
        {
            ["Recipient"] = user.ToEntityReference(),
            ["Title"] = notifTitle.Replace("（测试）", "（带链接测试）"),
            ["Body"] = $"{body}\n记录链接：{recordUrl}",
            ["IconType"] = new OptionSetValue(100000001),   // Success
            ["ToastType"] = new OptionSetValue(200000000),
            ["Expiry"] = 86400
        };
        try
        {
            service.Execute(request3);
            Console.WriteLine("✅ SendAppNotification（正文带链接）调用成功");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ 正文带链接的通知调用失败: {ex.Message}");
        }
        Console.WriteLine("请在 DEV1 右上角小铃铛中查看通知样式，重点看正文链接是否可点击");
    }
}

public class TestQuotaHelper
{
    public static void SeedQuotaData(ServiceClient service, string accountName)
    {
        Console.WriteLine($"=== 为 {accountName} 创建测试额度数据 ===");
        // 1. 查找 Account
        var accountQuery = new QueryExpression("account")
        {
            ColumnSet = new ColumnSet("name", "mcs_customermasterdata"),
            Criteria = new FilterExpression { Conditions = { new ConditionExpression("name", ConditionOperator.Equal, accountName) } },
            TopCount = 1
        };
        var accountResult = service.RetrieveMultiple(accountQuery);
        if (accountResult.Entities.Count == 0)
        {
            Console.WriteLine($"❌ 找不到 Account: {accountName}");
            return;
        }
        var account = accountResult.Entities[0];
        var masterRef = account.GetAttributeValue<EntityReference>("mcs_customermasterdata");
        if (masterRef == null)
        {
            Console.WriteLine($"❌ Account {accountName} 没有关联客户主数据");
            return;
        }
        var masterId = masterRef.Id;
        var master = service.Retrieve("mcs_customermasterdata", masterId, new ColumnSet("mcs_sinosurecode"));
        var existingCode = master.GetAttributeValue<string>("mcs_sinosurecode");
        var sinosureCode = existingCode ?? "TESTBUYER001";
        if (string.IsNullOrEmpty(existingCode))
        {
            var updateMaster = new Entity("mcs_customermasterdata", masterId);
            updateMaster["mcs_sinosurecode"] = sinosureCode;
            service.Update(updateMaster);
            Console.WriteLine($"📝 客户主数据 sinosurecode 为空，已设置为: {sinosureCode}");
        }
        Console.WriteLine($"Account: {account.Id}, CustomerMaster: {masterId}, SinosureCode: {sinosureCode}");

        // 2. 清理该客户主数据下已有的测试厂端授信记录
        var existingQuotaQuery = new QueryExpression("mcs_fca_quota")
        {
            ColumnSet = new ColumnSet("mcs_fca_quotaid"),
            Criteria = new FilterExpression { Conditions = { new ConditionExpression("mcs_accountid", ConditionOperator.Equal, masterId) } }
        };
        foreach (var existing in service.RetrieveMultiple(existingQuotaQuery).Entities)
        {
            service.Delete("mcs_fca_quota", existing.Id);
            Console.WriteLine($"🧹 清理已有 mcs_fca_quota: {existing.Id}");
        }

        // 4. 创建 mcs_fca_quota 测试记录
        var factoryQuota = new Entity("mcs_fca_quota");
        factoryQuota["mcs_accountid"] = new EntityReference("mcs_customermasterdata", masterId);
        factoryQuota["mcs_sellergrant"] = new Money(1000000m);
        factoryQuota["mcs_sellerbalance"] = new Money(750000m);
        var factoryQuotaId = service.Create(factoryQuota);
        Console.WriteLine($"✅ 创建 mcs_fca_quota: {factoryQuotaId}, grant=1,000,000 USD, balance=750,000 USD");

        // 5. 清理该 buyerno 下已有的测试中信保限额记录
        var existingApprovedQuery = new QueryExpression("mcs_approvedquota")
        {
            ColumnSet = new ColumnSet("mcs_approvedquotaid"),
            Criteria = new FilterExpression { Conditions = { new ConditionExpression("mcs_buyerno", ConditionOperator.Equal, sinosureCode) } }
        };
        foreach (var existing in service.RetrieveMultiple(existingApprovedQuery).Entities)
        {
            service.Delete("mcs_approvedquota", existing.Id);
            Console.WriteLine($"🧹 清理已有 mcs_approvedquota: {existing.Id}");
        }

        // 6. 创建 mcs_approvedquota 测试记录
        var approvedQuota = new Entity("mcs_approvedquota");
        approvedQuota["mcs_buyerno"] = sinosureCode;
        approvedQuota["mcs_quotasum"] = 500000m;
        approvedQuota["mcs_buyerchnname"] = accountName;
        approvedQuota["mcs_buyerengname"] = $"Test Buyer {accountName}";
        var approvedQuotaId = service.Create(approvedQuota);
        Console.WriteLine($"✅ 创建 mcs_approvedquota: {approvedQuotaId}, buyerno={sinosureCode}, quotasum=500,000 USD");

        Console.WriteLine("\n测试数据已创建。请在 D365 Account 表单中打开该客户的信用画像页签，刷新后查看额度显示。");
        Console.WriteLine("创建的记录 ID（如需手动清理）：");
        Console.WriteLine($"  mcs_fca_quota: {factoryQuotaId}");
        Console.WriteLine($"  mcs_approvedquota: {approvedQuotaId}");
    }

    public static void CopyLatestTagsToApprovedRecord(ServiceClient service, string accountName)
    {
        Console.WriteLine($"=== 为 {accountName} 复制标签到审批通过记录 ===");
        var accountQuery = new QueryExpression("account")
        {
            ColumnSet = new ColumnSet("name"),
            Criteria = new FilterExpression { Conditions = { new ConditionExpression("name", ConditionOperator.Equal, accountName) } },
            TopCount = 1
        };
        var accountResult = service.RetrieveMultiple(accountQuery);
        if (accountResult.Entities.Count == 0)
        {
            Console.WriteLine($"❌ 找不到 Account: {accountName}");
            return;
        }
        var accountId = accountResult.Entities[0].Id;

        // 1. 找所有审批通过且有效的记录
        var approvedQuery = new QueryExpression("mcs_credit_record")
        {
            ColumnSet = new ColumnSet("mcs_scoreid"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("mcs_accountid", ConditionOperator.Equal, accountId),
                    new ConditionExpression("mcs_status", ConditionOperator.Equal, 15),
                    new ConditionExpression("mcs_active", ConditionOperator.Equal, true)
                }
            },
            Orders = { new OrderExpression("modifiedon", OrderType.Descending) }
        };
        var approvedRecords = service.RetrieveMultiple(approvedQuery);
        if (approvedRecords.Entities.Count == 0)
        {
            Console.WriteLine("❌ 没有激活的审批通过记录，无法复制标签");
            return;
        }
        Console.WriteLine($"找到 {approvedRecords.Entities.Count} 条激活的审批通过记录");

        // 2. 找最新一条评估记录（任意状态）作为标签来源
        var sourceQuery = new QueryExpression("mcs_credit_record")
        {
            ColumnSet = new ColumnSet("mcs_scoreid"),
            Criteria = new FilterExpression { Conditions = { new ConditionExpression("mcs_accountid", ConditionOperator.Equal, accountId) } },
            Orders = { new OrderExpression("modifiedon", OrderType.Descending) },
            TopCount = 1
        };
        var sourceResult = service.RetrieveMultiple(sourceQuery);
        if (sourceResult.Entities.Count == 0)
        {
            Console.WriteLine("❌ 没有来源评估记录");
            return;
        }
        var sourceRecord = sourceResult.Entities[0];
        Console.WriteLine($"来源评估记录: {sourceRecord.GetAttributeValue<string>("mcs_scoreid")} ({sourceRecord.Id})");

        // 3. 查询来源记录下的标签
        var tagQuery = new QueryExpression("mcs_customer_tag")
        {
            ColumnSet = new ColumnSet(
                "mcs_credit_item", "mcs_credititem_value", "mcs_group", "mcs_itemname",
                "mcs_itemdesc", "mcs_itemtxtvalue1", "mcs_itemtxtvalue2",
                "mcs_itemvalue1", "mcs_itemvalue2", "mcs_scorevalue", "mcs_isscore", "mcs_scoreid"),
            Criteria = new FilterExpression { Conditions = { new ConditionExpression("mcs_credit_record", ConditionOperator.Equal, sourceRecord.Id) } }
        };
        var tags = service.RetrieveMultiple(tagQuery);
        Console.WriteLine($"来源标签数: {tags.Entities.Count}");
        if (tags.Entities.Count == 0)
        {
            Console.WriteLine("⚠️ 来源记录没有标签，无需复制");
            return;
        }

        // 4/5. 为每条审批通过记录复制标签
        var totalCopied = 0;
        foreach (var approvedRecord in approvedRecords.Entities)
        {
            Console.WriteLine($"\n目标审批通过记录: {approvedRecord.GetAttributeValue<string>("mcs_scoreid")} ({approvedRecord.Id})");

            // 清理该记录下已有的标签，避免重复
            var existingTagQuery = new QueryExpression("mcs_customer_tag")
            {
                ColumnSet = new ColumnSet("mcs_customer_tagid"),
                Criteria = new FilterExpression { Conditions = { new ConditionExpression("mcs_credit_record", ConditionOperator.Equal, approvedRecord.Id) } }
            };
            foreach (var existing in service.RetrieveMultiple(existingTagQuery).Entities)
            {
                service.Delete("mcs_customer_tag", existing.Id);
                Console.WriteLine($"🧹 清理目标记录旧标签: {existing.Id}");
            }

            // 复制标签
            foreach (var tag in tags.Entities)
            {
                var newTag = new Entity("mcs_customer_tag");
                newTag["mcs_accountid"] = new EntityReference("account", accountId);
                newTag["mcs_credit_record"] = new EntityReference("mcs_credit_record", approvedRecord.Id);
                newTag["mcs_scoreid"] = approvedRecord.GetAttributeValue<string>("mcs_scoreid");

                var creditItem = tag.GetAttributeValue<EntityReference>("mcs_credit_item");
                if (creditItem != null) newTag["mcs_credit_item"] = creditItem;

                var creditItemValue = tag.GetAttributeValue<EntityReference>("mcs_credititem_value");
                if (creditItemValue != null) newTag["mcs_credititem_value"] = creditItemValue;

                if (tag.Contains("mcs_group")) newTag["mcs_group"] = tag.GetAttributeValue<OptionSetValue>("mcs_group");
                if (tag.Contains("mcs_itemname")) newTag["mcs_itemname"] = tag.GetAttributeValue<string>("mcs_itemname");
                if (tag.Contains("mcs_itemdesc")) newTag["mcs_itemdesc"] = tag.GetAttributeValue<string>("mcs_itemdesc");
                if (tag.Contains("mcs_itemtxtvalue1")) newTag["mcs_itemtxtvalue1"] = tag.GetAttributeValue<string>("mcs_itemtxtvalue1");
                if (tag.Contains("mcs_itemtxtvalue2")) newTag["mcs_itemtxtvalue2"] = tag.GetAttributeValue<string>("mcs_itemtxtvalue2");
                if (tag.Contains("mcs_itemvalue1")) newTag["mcs_itemvalue1"] = tag.GetAttributeValue<object>("mcs_itemvalue1");
                if (tag.Contains("mcs_itemvalue2")) newTag["mcs_itemvalue2"] = tag.GetAttributeValue<object>("mcs_itemvalue2");
                if (tag.Contains("mcs_scorevalue")) newTag["mcs_scorevalue"] = tag.GetAttributeValue<int>("mcs_scorevalue");
                if (tag.Contains("mcs_isscore")) newTag["mcs_isscore"] = tag.GetAttributeValue<bool>("mcs_isscore");

                var newTagId = service.Create(newTag);
                Console.WriteLine($"✅ 复制标签: {tag.GetAttributeValue<string>("mcs_itemname")} -> {newTagId}");
                totalCopied++;
            }
        }
        Console.WriteLine($"\n完成，共复制 {totalCopied} 条标签。刷新页面后标签和飞轮图应正常显示。");
    }

    public static void ActivateApprovedRecords(ServiceClient service, string accountName)
    {
        Console.WriteLine($"=== 激活 {accountName} 的审批通过记录 ===");
        var accountQuery = new QueryExpression("account")
        {
            ColumnSet = new ColumnSet("name"),
            Criteria = new FilterExpression { Conditions = { new ConditionExpression("name", ConditionOperator.Equal, accountName) } },
            TopCount = 1
        };
        var accountResult = service.RetrieveMultiple(accountQuery);
        if (accountResult.Entities.Count == 0)
        {
            Console.WriteLine($"❌ 找不到 Account: {accountName}");
            return;
        }
        var accountId = accountResult.Entities[0].Id;

        var recordQuery = new QueryExpression("mcs_credit_record")
        {
            ColumnSet = new ColumnSet("mcs_scoreid", "mcs_status", "mcs_active"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("mcs_accountid", ConditionOperator.Equal, accountId),
                    new ConditionExpression("mcs_status", ConditionOperator.Equal, 15)
                }
            }
        };
        var records = service.RetrieveMultiple(recordQuery);
        Console.WriteLine($"找到 {records.Entities.Count} 条审批通过记录");
        foreach (var r in records.Entities)
        {
            var active = r.GetAttributeValue<bool?>("mcs_active");
            if (active != true)
            {
                var update = new Entity("mcs_credit_record", r.Id);
                update["mcs_active"] = true;
                service.Update(update);
                Console.WriteLine($"✅ 已激活: {r.GetAttributeValue<string>("mcs_scoreid")} ({r.Id})");
            }
            else
            {
                Console.WriteLine($"⏭️ 已是激活状态: {r.GetAttributeValue<string>("mcs_scoreid")} ({r.Id})");
            }
        }
        Console.WriteLine("完成。刷新页面后，信用画像会按最新激活的审批通过记录过滤标签。");
    }

    public static void DiagnoseProfileData(ServiceClient service, string accountName)
    {
        Console.WriteLine($"=== 诊断 {accountName} 信用画像数据 ===");
        // 1. 查找 Account
        var accountQuery = new QueryExpression("account")
        {
            ColumnSet = new ColumnSet("name", "mcs_customermasterdata"),
            Criteria = new FilterExpression { Conditions = { new ConditionExpression("name", ConditionOperator.Equal, accountName) } },
            TopCount = 1
        };
        var accountResult = service.RetrieveMultiple(accountQuery);
        if (accountResult.Entities.Count == 0)
        {
            Console.WriteLine($"❌ 找不到 Account: {accountName}");
            return;
        }
        var account = accountResult.Entities[0];
        var accountId = account.Id;
        var masterRef = account.GetAttributeValue<EntityReference>("mcs_customermasterdata");
        Console.WriteLine($"Account: {accountId}, name={account.GetAttributeValue<string>("name")}");
        if (masterRef != null)
        {
            var master = service.Retrieve("mcs_customermasterdata", masterRef.Id, new ColumnSet("mcs_creditscore", "mcs_creditgrade", "mcs_creditvalid"));
            Console.WriteLine($"  客户主数据上反写的信用分/等级/有效: score={master.GetAttributeValue<decimal?>("mcs_creditscore")}, grade={master.GetAttributeValue<OptionSetValue>("mcs_creditgrade")?.Value}, valid={master.GetAttributeValue<bool?>("mcs_creditvalid")}");
        }
        else
        {
            Console.WriteLine("  Account 未关联客户主数据");
        }

        // 2. 查询所有信用评估记录（按 modifiedon 倒序）
        var recordQuery = new QueryExpression("mcs_credit_record")
        {
            ColumnSet = new ColumnSet("mcs_scoreid", "mcs_status", "mcs_active", "mcs_creditscore", "mcs_approvedate", "modifiedon"),
            Criteria = new FilterExpression { Conditions = { new ConditionExpression("mcs_accountid", ConditionOperator.Equal, accountId) } },
            Orders = { new OrderExpression("modifiedon", OrderType.Descending) },
            TopCount = 10
        };
        var records = service.RetrieveMultiple(recordQuery);
        Console.WriteLine($"\n信用评估记录数（最近10条）: {records.Entities.Count}");
        Entity? latestApprovedRecord = null;
        Entity? latestActiveRecord = null;
        foreach (var r in records.Entities)
        {
            var status = r.GetAttributeValue<OptionSetValue>("mcs_status")?.Value;
            var active = r.GetAttributeValue<bool?>("mcs_active");
            var score = r.GetAttributeValue<decimal?>("mcs_creditscore");
            var apprDate = r.GetAttributeValue<DateTime?>("mcs_approvedate");
            Console.WriteLine($"  id={r.Id}, scoreid={r.GetAttributeValue<string>("mcs_scoreid")}, status={status}, active={active}, score={score}, approvedate={apprDate}, modifiedon={r.GetAttributeValue<DateTime?>("modifiedon")}");
            if (latestActiveRecord == null && active == true) latestActiveRecord = r;
            if (latestApprovedRecord == null && status == 15) latestApprovedRecord = r;
        }

        // 3. 列出 mcs_customer_tag 所有字段，查找与信用评估记录关联的字段
        try
        {
            var emdReq = new RetrieveEntityRequest { EntityFilters = EntityFilters.Attributes, LogicalName = "mcs_customer_tag" };
            var emdResp = (RetrieveEntityResponse)service.Execute(emdReq);
            Console.WriteLine("\nmcs_customer_tag 字段（重点看关联/编号字段）:");
            foreach (var attr in emdResp.EntityMetadata.Attributes.OrderBy(a => a.LogicalName))
            {
                if (attr.LogicalName.Contains("score") || attr.LogicalName.Contains("record") || attr.LogicalName.Contains("account") || attr.LogicalName.Contains("customer") || attr.LogicalName.Contains("credit"))
                {
                    Console.WriteLine($"  {attr.LogicalName} ({attr.AttributeType})");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n列出 mcs_customer_tag 字段失败: {ex.Message}");
        }

        // 4. 查询该 account 下的 customer_tag（最多 20 条）
        var tagQuery = new QueryExpression("mcs_customer_tag")
        {
            ColumnSet = new ColumnSet("mcs_credit_record", "mcs_group", "mcs_itemname", "mcs_itemvalue1", "mcs_scorevalue", "modifiedon"),
            Criteria = new FilterExpression { Conditions = { new ConditionExpression("mcs_accountid", ConditionOperator.Equal, accountId) } },
            Orders = { new OrderExpression("modifiedon", OrderType.Descending) },
            TopCount = 20
        };
        var tags = service.RetrieveMultiple(tagQuery);
        Console.WriteLine($"\nCustomerTag 记录数（最近20条）: {tags.Entities.Count}");
        foreach (var t in tags.Entities)
        {
            var recRef = t.GetAttributeValue<EntityReference>("mcs_credit_record");
            Console.WriteLine($"  id={t.Id}, group={t.GetAttributeValue<OptionSetValue>("mcs_group")?.Value}, item={t.GetAttributeValue<string>("mcs_itemname")}, value={t.GetAttributeValue<object>("mcs_itemvalue1")}, scorevalue={t.GetAttributeValue<object>("mcs_scorevalue")}, recordid={(recRef == null ? "null" : $"{recRef.Id}")}, modifiedon={t.GetAttributeValue<DateTime?>("modifiedon")}");
        }

        // 5. 单独查询审批通过记录（status=15）
        var approvedQuery = new QueryExpression("mcs_credit_record")
        {
            ColumnSet = new ColumnSet("mcs_scoreid", "mcs_status", "mcs_active", "mcs_creditscore", "mcs_approvedate", "modifiedon"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("mcs_accountid", ConditionOperator.Equal, accountId),
                    new ConditionExpression("mcs_status", ConditionOperator.Equal, 15)
                }
            },
            Orders = { new OrderExpression("modifiedon", OrderType.Descending) },
            TopCount = 5
        };
        var approvedRecords = service.RetrieveMultiple(approvedQuery);
        Console.WriteLine($"\n审批通过记录数（status=15，最近5条）: {approvedRecords.Entities.Count}");
        foreach (var r in approvedRecords.Entities)
        {
            Console.WriteLine($"  id={r.Id}, scoreid={r.GetAttributeValue<string>("mcs_scoreid")}, status={r.GetAttributeValue<OptionSetValue>("mcs_status")?.Value}, active={r.GetAttributeValue<bool?>("mcs_active")}, score={r.GetAttributeValue<decimal?>("mcs_creditscore")}, approvedate={r.GetAttributeValue<DateTime?>("mcs_approvedate")}, modifiedon={r.GetAttributeValue<DateTime?>("modifiedon")}");
        }
        if (approvedRecords.Entities.Count > 0)
        {
            latestApprovedRecord = approvedRecords.Entities[0];
        }

        // 6. 判断风险
        Console.WriteLine("\n=== 诊断结论 ===");
        if (latestApprovedRecord == null)
        {
            Console.WriteLine("⚠️ 该 Account 没有审批通过（status=15）的信用评估记录。");
        }
        else
        {
            Console.WriteLine($"✅ 最新审批通过记录: {latestApprovedRecord.Id}, score={latestApprovedRecord.GetAttributeValue<decimal?>("mcs_creditscore")}, active={latestApprovedRecord.GetAttributeValue<bool?>("mcs_active")}, modifiedon={latestApprovedRecord.GetAttributeValue<DateTime?>("modifiedon")}");
        }
        if (latestActiveRecord != null && latestActiveRecord.GetAttributeValue<OptionSetValue>("mcs_status")?.Value != 15)
        {
            Console.WriteLine($"⚠️ 当前 fetchLatestCreditRecord 取到的是最新 active 记录（{latestActiveRecord.Id}, status={latestActiveRecord.GetAttributeValue<OptionSetValue>("mcs_status")?.Value}），但不是审批通过记录。");
        }
        foreach (var r in approvedRecords.Entities)
        {
            var linkedTags = tags.Entities.Where(t => t.GetAttributeValue<EntityReference>("mcs_credit_record")?.Id == r.Id).ToList();
            Console.WriteLine($"  审批通过记录 {r.GetAttributeValue<string>("mcs_scoreid")} ({r.Id}) 关联的标签数: {linkedTags.Count}");
        }
        Console.WriteLine("建议：fetchLatestCreditRecord 增加 status=15 过滤；fetchCustomerTags 按最新审批通过记录的 mcs_credit_record 过滤。");
    }

    public static void TestQuotaData(ServiceClient service)
    {
        Console.WriteLine("=== 测试额度数据 ===");
        // 1. 查询有数据的 mcs_fca_quota 样本
        var q1 = new QueryExpression("mcs_fca_quota") { ColumnSet = new ColumnSet("mcs_accountid", "mcs_sellergrant", "mcs_sellerbalance"), TopCount = 5 };
        var r1 = service.RetrieveMultiple(q1);
        Console.WriteLine($"mcs_fca_quota 样本数: {r1.Entities.Count}");
        foreach (var e in r1.Entities)
        {
            var acc = e.GetAttributeValue<EntityReference>("mcs_accountid");
            Console.WriteLine($"  quota id={e.Id}, accountid={acc?.Id} ({acc?.LogicalName}), grant={e.GetAttributeValue<Money>("mcs_sellergrant")?.Value}, balance={e.GetAttributeValue<Money>("mcs_sellerbalance")?.Value}");
        }

        // 2. 查询 mcs_approvedquota 样本
        var q2 = new QueryExpression("mcs_approvedquota") { ColumnSet = new ColumnSet("mcs_buyerno", "mcs_quotasum"), TopCount = 5 };
        var r2 = service.RetrieveMultiple(q2);
        Console.WriteLine($"mcs_approvedquota 样本数: {r2.Entities.Count}");
        foreach (var e in r2.Entities)
        {
            Console.WriteLine($"  approvedquota id={e.Id}, buyerno={e.GetAttributeValue<string>("mcs_buyerno")}, quotasum={e.GetAttributeValue<decimal?>("mcs_quotasum")}");
        }

        // 3. 查询有 customermaster 的 account
        var q3 = new QueryExpression("account") { ColumnSet = new ColumnSet("name", "mcs_customermasterdata"), TopCount = 5 };
        q3.Criteria.AddCondition("mcs_customermasterdata", ConditionOperator.NotNull);
        var r3 = service.RetrieveMultiple(q3);
        Console.WriteLine($"有 customermaster 的 account 数: {r3.Entities.Count}");
        foreach (var e in r3.Entities)
        {
            var master = e.GetAttributeValue<EntityReference>("mcs_customermasterdata");
            Console.WriteLine($"  account id={e.Id}, name={e.GetAttributeValue<string>("name")}, master={master?.Id}");
        }

        // 4. 查询 mcs_fca_quotaapp 样本
        var q4 = new QueryExpression("mcs_fca_quotaapp") { ColumnSet = new ColumnSet("mcs_accountid", "mcs_sellergrant", "mcs_sellerbalance", "mcs_quotasum", "mcs_quotabalance"), TopCount = 5 };
        var r4 = service.RetrieveMultiple(q4);
        Console.WriteLine($"mcs_fca_quotaapp 样本数: {r4.Entities.Count}");
        foreach (var e in r4.Entities)
        {
            var acc = e.GetAttributeValue<EntityReference>("mcs_accountid");
            Console.WriteLine($"  quotaapp id={e.Id}, accountid={acc?.Id} ({acc?.LogicalName}), sellergrant={e.GetAttributeValue<Money>("mcs_sellergrant")?.Value}, sellerbalance={e.GetAttributeValue<Money>("mcs_sellerbalance")?.Value}, quotasum={e.GetAttributeValue<Money>("mcs_quotasum")?.Value}, quotabalance={e.GetAttributeValue<Money>("mcs_quotabalance")?.Value}");
        }

        // 5. 查询 account 上的融资额度字段
        var q5 = new QueryExpression("account") { ColumnSet = new ColumnSet("name", "mcs_internalfinancingcreditlimit", "mcs_suppliercreditlimit", "mcs_sinosurecreditlimit"), TopCount = 5 };
        var r5 = service.RetrieveMultiple(q5);
        Console.WriteLine($"account 融资额度字段样本数: {r5.Entities.Count}");
        foreach (var e in r5.Entities)
        {
            Console.WriteLine($"  account id={e.Id}, name={e.GetAttributeValue<string>("name")}, internal={e.GetAttributeValue<Money>("mcs_internalfinancingcreditlimit")?.Value}, supplier={e.GetAttributeValue<Money>("mcs_suppliercreditlimit")?.Value}, sinosure={e.GetAttributeValue<Money>("mcs_sinosurecreditlimit")?.Value}");
        }

        // 6. 查询 customermaster 上的 sinosurecode
        if (r3.Entities.Count > 0)
        {
            var masterId = r3.Entities[0].GetAttributeValue<EntityReference>("mcs_customermasterdata")?.Id;
            if (masterId.HasValue)
            {
                var master = service.Retrieve("mcs_customermasterdata", masterId.Value, new ColumnSet("mcs_sinosurecode"));
                Console.WriteLine($"customermaster {masterId} 的 sinosurecode={master.GetAttributeValue<string>("mcs_sinosurecode")}");
            }
        }

        // 7. 查询 mcs_fca_quota.mcs_accountid 的目标实体
        try
        {
            var attrReq = new RetrieveAttributeRequest { EntityLogicalName = "mcs_fca_quota", LogicalName = "mcs_accountid" };
            var attrResp = (RetrieveAttributeResponse)service.Execute(attrReq);
            if (attrResp.AttributeMetadata is LookupAttributeMetadata lookupAttr)
            {
                Console.WriteLine($"mcs_fca_quota.mcs_accountid targets={string.Join(",", lookupAttr.Targets)}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"查询 mcs_fca_quota.mcs_accountid 属性失败: {ex.Message}");
        }

        // 8. 查询 mcs_approvedquota 关系字段
        try
        {
            var attrReq = new RetrieveAttributeRequest { EntityLogicalName = "mcs_approvedquota", LogicalName = "mcs_lc_credit_limit_applicationid" };
            var attrResp = (RetrieveAttributeResponse)service.Execute(attrReq);
            if (attrResp.AttributeMetadata is LookupAttributeMetadata lookupAttr)
            {
                Console.WriteLine($"mcs_approvedquota.mcs_lc_credit_limit_applicationid targets={string.Join(",", lookupAttr.Targets)}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"查询 mcs_approvedquota 属性失败: {ex.Message}");
        }

        // 9. 列出 mcs_approvedquota 所有字段名（前50个）
        try
        {
            var emdReq = new RetrieveEntityRequest { EntityFilters = EntityFilters.Attributes, LogicalName = "mcs_approvedquota" };
            var emdResp = (RetrieveEntityResponse)service.Execute(emdReq);
            Console.WriteLine("mcs_approvedquota 字段:");
            foreach (var attr in emdResp.EntityMetadata.Attributes.OrderBy(a => a.LogicalName).Take(80))
            {
                Console.WriteLine($"  {attr.LogicalName} ({attr.AttributeType})");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"列出 mcs_approvedquota 字段失败: {ex.Message}");
        }

        // 10. 列出 mcs_lc_credit_limit_application 所有字段名
        try
        {
            var emdReq = new RetrieveEntityRequest { EntityFilters = EntityFilters.Attributes, LogicalName = "mcs_lc_credit_limit_application" };
            var emdResp = (RetrieveEntityResponse)service.Execute(emdReq);
            Console.WriteLine("mcs_lc_credit_limit_application 字段:");
            foreach (var attr in emdResp.EntityMetadata.Attributes.OrderBy(a => a.LogicalName).Take(80))
            {
                Console.WriteLine($"  {attr.LogicalName} ({attr.AttributeType})");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"列出 mcs_lc_credit_limit_application 字段失败: {ex.Message}");
        }

        // 11. mcs_lc_credit_limit_application.mcs_applicant target
        try
        {
            var attrReq = new RetrieveAttributeRequest { EntityLogicalName = "mcs_lc_credit_limit_application", LogicalName = "mcs_applicant" };
            var attrResp = (RetrieveAttributeResponse)service.Execute(attrReq);
            if (attrResp.AttributeMetadata is LookupAttributeMetadata lookupAttr)
            {
                Console.WriteLine($"mcs_lc_credit_limit_application.mcs_applicant targets={string.Join(",", lookupAttr.Targets)}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"查询 mcs_applicant 属性失败: {ex.Message}");
        }

        // 12. mcs_lc_credit_limit_application 记录样本（ applicant / buyerno / quotasum ）
        try
        {
            var qe = new QueryExpression("mcs_lc_credit_limit_application")
            {
                TopCount = 5,
                ColumnSet = new ColumnSet("mcs_applicant", "mcs_buyerquotano", "mcs_quotasum", "mcs_quotasumapply", "mcs_corpserialno")
            };
            var ec = service.RetrieveMultiple(qe);
            Console.WriteLine($"mcs_lc_credit_limit_application 样本数: {ec.Entities.Count}");
            foreach (var e in ec.Entities)
            {
                var applicant = e.GetAttributeValue<EntityReference>("mcs_applicant");
                Console.WriteLine($"  id={e.Id}, applicant={(applicant == null ? "null" : $"{applicant.LogicalName}:{applicant.Id}")}, buyerquotano={e.GetAttributeValue<string>("mcs_buyerquotano")}, quotasum={e.GetAttributeValue<decimal?>("mcs_quotasum")}, quotasumapply={e.GetAttributeValue<decimal?>("mcs_quotasumapply")}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"查询 mcs_lc_credit_limit_application 失败: {ex.Message}");
        }
    }
}

/// <summary>
/// check-release 通用发版清单。Solution 归属固定映射（开发手册 4.4）：
/// 实体/字段/App Action → entitySolution；WebResource → McsWebResource；
/// Plugin → McsPlugin；Custom API → McsCustomAPI。缺失的组跳过。
/// </summary>
public class ReleaseManifest
{
    public string? Name { get; set; }
    public string? EntitySolution { get; set; }
    public List<string>? Entities { get; set; }
    public List<string>? WebResources { get; set; }
    public List<string>? PluginTypes { get; set; }
    public List<string>? CustomApis { get; set; }
    public List<string>? AppActions { get; set; }
}

public class CreditItemValueImportRecord
{
    public string CreditItemCode { get; set; } = string.Empty;
    public string ListValue { get; set; } = string.Empty;
    public string ListName { get; set; } = string.Empty;
    public string CofaceValue { get; set; } = string.Empty;
}

public class ScoringCardImportRecord
{
    public int CategoryId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public int DataType { get; set; }
    public decimal? Min { get; set; }
    public decimal? Max { get; set; }
    public string? ListValue { get; set; }
    public int Weight { get; set; }
    public string? RawCriteria { get; set; }
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
