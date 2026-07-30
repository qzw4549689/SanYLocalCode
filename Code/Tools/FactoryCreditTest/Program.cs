using D365ToolCommon.Connection;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FactoryCreditTest
{
    class Program
    {
        static readonly string ServiceUrl = Environment.GetEnvironmentVariable("D365_URL") ?? "https://dev1.crm5.dynamics.com";

        static async Task Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("用法:");
                Console.WriteLine("  dotnet run setup     - 创建测试基础数据（模型版本、参数配置）");
                Console.WriteLine("  dotnet run create-customer - 创建测试客户主数据及对应 Account");
                Console.WriteLine("  dotnet run prepare <客户主数据ID> - 为客户准备信用标签和在外货款测试数据");
                Console.WriteLine("  dotnet run calc <客户主数据ID>    - 创建 mcs_fca_proc 并触发模型计算");
                Console.WriteLine("  dotnet run check <mcs_fca_proc ID> - 查询计算结果");
                Console.WriteLine("  dotnet run set-version-active <版本编码> <0|1> - 切换模型版本生效标志（测试用）");
                return;
            }

            using var serviceClient = await D365ConnectionFactory.CreateAsync(ServiceUrl);
            var service = (IOrganizationService)serviceClient;

            switch (args[0].ToLowerInvariant())
            {
                case "set-version-active":
                    if (args.Length < 3) { Console.WriteLine("用法: dotnet run set-version-active <版本编码> <0|1>"); return; }
                    SetVersionActive(service, args[1], args[2] == "1");
                    break;
                case "setup":
                    SetupTestData(service);
                    break;
                case "create-customer":
                    var (masterDataId, accountId) = CreateTestCustomer(service);
                    Console.WriteLine($"客户主数据ID: {masterDataId}");
                    Console.WriteLine($"Account ID: {accountId}");
                    break;
                case "prepare":
                    if (args.Length < 2) { Console.WriteLine("缺少客户主数据ID"); return; }
                    PrepareCustomerData(service, Guid.Parse(args[1]));
                    break;
                case "calc":
                    if (args.Length < 2) { Console.WriteLine("缺少客户主数据ID"); return; }
                    TriggerCalculation(service, Guid.Parse(args[1]));
                    break;
                case "check":
                    if (args.Length < 2) { Console.WriteLine("缺少 mcs_fca_proc ID"); return; }
                    CheckResult(service, Guid.Parse(args[1]));
                    break;
                case "activate":
                    if (args.Length < 2) { Console.WriteLine("缺少 mcs_fca_proc ID"); return; }
                    ActivateProc(service, Guid.Parse(args[1]));
                    break;
                case "set-sapnumber":
                    if (args.Length < 3) { Console.WriteLine("用法: dotnet run set-sapnumber <accountId> <sapNumber>"); return; }
                    SetAccountSapNumber(service, Guid.Parse(args[1]), args[2]);
                    break;
                case "list-quota":
                    if (args.Length < 2) { Console.WriteLine("用法: dotnet run list-quota <accountId>"); return; }
                    ListQuotaRecords(service, Guid.Parse(args[1]));
                    break;
                case "show-account":
                    if (args.Length < 2) { Console.WriteLine("用法: dotnet run show-account <accountId>"); return; }
                    ShowAccount(service, Guid.Parse(args[1]));
                    break;
                case "set-quota-custname":
                    if (args.Length < 3) { Console.WriteLine("用法: dotnet run set-quota-custname <quotaId> <custName>"); return; }
                    SetQuotaCustName(service, Guid.Parse(args[1]), args[2]);
                    break;
                case "test-overdue":
                    if (args.Length < 4) { Console.WriteLine("用法: dotnet run test-overdue <masterDataId> <逾期金额> <在外货款余额>"); return; }
                    TestOverdueScenario(service, Guid.Parse(args[1]), decimal.Parse(args[2]), decimal.Parse(args[3]));
                    break;
                case "test-fallback":
                    if (args.Length < 2) { Console.WriteLine("缺少客户主数据ID"); return; }
                    TestFallbackScenario(service, Guid.Parse(args[1]));
                    break;
                case "test-normal":
                    if (args.Length < 2) { Console.WriteLine("缺少客户主数据ID"); return; }
                    TestNormal.Run(service, Guid.Parse(args[1]));
                    break;
                case "test-blacklist":
                    if (args.Length < 2) { Console.WriteLine("缺少客户主数据ID"); return; }
                    TestBlacklist.Run(service, Guid.Parse(args[1]));
                    break;
                case "query-bpf":
                    QueryBpf(service);
                    break;
                case "link-config-to-version":
                    LinkConfigToVersion(service);
                    break;
                case "check-tags":
                    if (args.Length < 2) { Console.WriteLine("缺少客户主数据ID"); return; }
                    CheckCustomerTags(service, Guid.Parse(args[1]));
                    break;
                case "reset-reject-by-name":
                    if (args.Length < 2) { Console.WriteLine("缺少客户名称"); return; }
                    ResetRejectByName(service, args[1]);
                    break;
                case "set-creditscore-by-name":
                    if (args.Length < 2) { Console.WriteLine("用法: dotnet run set-creditscore-by-name <客户名称> [信用分，省略则清空]"); return; }
                    decimal? score = null;
                    if (args.Length >= 3 && !string.IsNullOrWhiteSpace(args[2]))
                    {
                        score = decimal.Parse(args[2]);
                    }
                    SetCreditScoreByName(service, args[1], score);
                    break;
                case "set-creditvalid-by-name":
                    if (args.Length < 3) { Console.WriteLine("用法: dotnet run set-creditvalid-by-name <客户名称> <true|false>"); return; }
                    SetCreditValidByName(service, args[1], bool.Parse(args[2]));
                    break;
                case "query-audit":
                    if (args.Length < 2) { Console.WriteLine("用法: dotnet run query-audit <记录ID>"); return; }
                    QueryAudit(service, Guid.Parse(args[1]));
                    break;
                case "monthly-sum-setup":
                    if (args.Length < 2) { Console.WriteLine("用法: dotnet run monthly-sum-setup <客户名称>"); return; }
                    TestMonthlySum.Setup(service, args[1]);
                    break;
                case "monthly-sum-cleanup":
                    if (args.Length < 3) { Console.WriteLine("用法: dotnet run monthly-sum-cleanup <客户名称> <mcs_fca_proc ID，多个用分号分隔>"); return; }
                    TestMonthlySum.Cleanup(service, args[1], args[2]);
                    break;
                case "check-overdue-fields":
                    if (args.Length < 2) { Console.WriteLine("用法: dotnet run check-overdue-fields <客户名称 或 ALL>"); return; }
                    CheckOverdueFields(service, args[1]);
                    break;
                default:
                    Console.WriteLine("未知命令");
                    break;
            }
        }

        static (Guid masterDataId, Guid accountId) CreateTestCustomer(IOrganizationService service)
        {
            Console.WriteLine("=== 创建测试客户主数据 ===");
            string name = $"厂端授信测试客户_{DateTime.UtcNow:yyyyMMddHHmmss}";

            Entity customer = new Entity("mcs_customermasterdata");
            customer["mcs_name"] = name;
            customer["mcs_accounttype"] = new OptionSetValue(2); // 公司客户
            customer["mcs_kacategory"] = new OptionSetValue(1); // S级
            customer["mcs_creditgrade"] = new OptionSetValue(100000002); // A2
            customer["mcs_creditscore"] = 85m;
            customer["mcs_creditvalid"] = true;
            customer["mcs_blacklist"] = false;
            Guid masterDataId = service.Create(customer);
            Console.WriteLine($"创建客户主数据: {masterDataId}");

            // 使用一个现有 Account 并关联到新建的客户主数据
            Guid? accountIdNullable = FindAccount.FirstAvailable(service);
            if (!accountIdNullable.HasValue)
            {
                throw new InvalidOperationException("DEV1 中没有可用 Account，无法创建测试数据");
            }
            Guid accountId = accountIdNullable.Value;

            Entity accountUpdate = new Entity("account", accountId);
            accountUpdate["mcs_customermasterdata"] = new EntityReference("mcs_customermasterdata", masterDataId);
            service.Update(accountUpdate);
            Console.WriteLine($"关联 Account: {accountId}");

            return (masterDataId, accountId);
        }

        static void SetupTestData(IOrganizationService service)
        {
            Console.WriteLine("=== 创建厂端授信模拟基础数据 ===");

            // 1. 创建/更新模型版本 V20260830（与截图对齐）
            string versionIdText = "V20260830";
            Guid versionId = EnsureModelVersion(service, versionIdText,
                "按客户分类(S/A/B/C/I和经销商分级)、客户等级(A0-A4)维度配置系数1-3和取三个因子最大或最小。三个因子计算公式：1.财务能力=净资产 x 系数1；2.历史回款能力=近12个月客户回款月均值 x 系数2；3.历史基准额度=按客户分类(S/A/B/C/I和经销商分级)维度人工维护值 x 系数3。",
                new DateTime(2026, 6, 12),
                new DateTime(2099, 12, 31));
            Console.WriteLine($"模型版本: {versionIdText} ({versionId})");

            // 2. 场景一：具体客户等级，配置系数1-3、聚合方法（参考截图 S/A0-A4）
            var scenarioOneConfigs = new[]
            {
                new { BuyerGrade = 1, CreditGrade = 1, Adjust1 = 1.1m, Adjust2 = 1.1m, Adjust3 = 1.1m, AggFunc = 1, Benchmark = 900000m }, // S/A0
                new { BuyerGrade = 1, CreditGrade = 2, Adjust1 = 1.0m, Adjust2 = 1.0m, Adjust3 = 0.9m, AggFunc = 1, Benchmark = 800000m }, // S/A1
                new { BuyerGrade = 1, CreditGrade = 3, Adjust1 = 0.9m, Adjust2 = 0.8m, Adjust3 = 0.8m, AggFunc = 1, Benchmark = 700000m }, // S/A2
                new { BuyerGrade = 1, CreditGrade = 4, Adjust1 = 0.8m, Adjust2 = 0.7m, Adjust3 = 0.7m, AggFunc = 2, Benchmark = 600000m }, // S/A3
                new { BuyerGrade = 1, CreditGrade = 5, Adjust1 = 0.7m, Adjust2 = 0.6m, Adjust3 = 0.6m, AggFunc = 2, Benchmark = 500000m }, // S/A4
                new { BuyerGrade = 2, CreditGrade = 1, Adjust1 = 1.1m, Adjust2 = 1.1m, Adjust3 = 1.1m, AggFunc = 1, Benchmark = 700000m }, // A/A0
                new { BuyerGrade = 2, CreditGrade = 2, Adjust1 = 1.0m, Adjust2 = 1.0m, Adjust3 = 0.9m, AggFunc = 1, Benchmark = 650000m }, // A/A1
                new { BuyerGrade = 2, CreditGrade = 3, Adjust1 = 0.9m, Adjust2 = 0.8m, Adjust3 = 0.8m, AggFunc = 1, Benchmark = 600000m }, // A/A2
            };

            foreach (var cfg in scenarioOneConfigs)
            {
                Guid configId = EnsureModelConfig(service, cfg.BuyerGrade, cfg.CreditGrade,
                    cfg.Adjust1, cfg.Adjust2, cfg.Adjust3, cfg.AggFunc, cfg.Benchmark);
                Console.WriteLine($"场景一 模型参数({GetBuyerGradeLabel(cfg.BuyerGrade)}/{GetCreditGradeLabel(cfg.CreditGrade)}): {configId}");
            }

            // 3. 场景二 + 场景三：客户等级=ALL，同时配置历史基准额度和兜底系数
            //    由于 mcs_fca_mdlconfig 按【客户分类+客户等级】唯一，ALL 等级用同一条记录同时承载：
            //    - 场景二：历史基准额度（>1000）
            //    - 场景三：客户等级为空时的系数1-3、聚合方法兜底默认值
            Guid allConfigId = EnsureModelConfig(service, 1, 6,
                adjust1: 1.0m, adjust2: 1.0m, adjust3: 1.0m, aggFunc: 1, benchmark: 1000000m);
            Console.WriteLine($"场景二/三 ALL 兜底配置(S/ALL): {allConfigId}");

            Console.WriteLine("厂端授信模拟基础数据创建完成");
        }

        /// <summary>
        /// 切换模型版本生效标志（测试用，验证版本前置校验）
        /// </summary>
        static void SetVersionActive(IOrganizationService service, string versionIdText, bool isActive)
        {
            QueryExpression query = new QueryExpression("mcs_fca_mdlversion")
            {
                ColumnSet = new ColumnSet("mcs_fca_mdlversionid"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("mcs_versionid", ConditionOperator.Equal, versionIdText) }
                },
                TopCount = 1
            };
            EntityCollection result = service.RetrieveMultiple(query);
            if (result.Entities.Count == 0) { Console.WriteLine($"未找到模型版本: {versionIdText}"); return; }

            Entity update = new Entity("mcs_fca_mdlversion", result.Entities[0].Id);
            update["mcs_isactive"] = new OptionSetValue(isActive ? 1 : 0);
            service.Update(update);
            Console.WriteLine($"模型版本 {versionIdText} 是否生效已设为: {(isActive ? "是" : "否")}");
        }

        static Guid EnsureModelVersion(IOrganizationService service, string versionIdText, string description, DateTime validFrom, DateTime validEnd)
        {
            QueryExpression query = new QueryExpression("mcs_fca_mdlversion")
            {
                ColumnSet = new ColumnSet("mcs_fca_mdlversionid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_versionid", ConditionOperator.Equal, versionIdText)
                    }
                },
                TopCount = 1
            };

            EntityCollection result = service.RetrieveMultiple(query);
            if (result.Entities.Count > 0)
            {
                Guid existingId = result.Entities[0].Id;
                Entity update = new Entity("mcs_fca_mdlversion", existingId);
                update["mcs_isactive"] = new OptionSetValue(1);
                update["mcs_modeldesc"] = description;
                update["mcs_validfrom"] = validFrom;
                update["mcs_validend"] = validEnd;
                service.Update(update);
                return existingId;
            }

            Entity version = new Entity("mcs_fca_mdlversion");
            version["mcs_versionid"] = versionIdText;
            version["mcs_isactive"] = new OptionSetValue(1);
            version["mcs_modeldesc"] = description;
            version["mcs_validfrom"] = validFrom;
            version["mcs_validend"] = validEnd;
            return service.Create(version);
        }

        static Guid EnsureModelConfig(IOrganizationService service, int buyerGrade, int creditGrade,
            decimal adjust1, decimal adjust2, decimal adjust3, int aggFunc, decimal benchmark)
        {
            QueryExpression query = new QueryExpression("mcs_fca_mdlconfig")
            {
                ColumnSet = new ColumnSet("mcs_fca_mdlconfigid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_buyergrade", ConditionOperator.Equal, buyerGrade),
                        new ConditionExpression("mcs_creditgrade", ConditionOperator.Equal, creditGrade)
                    }
                },
                TopCount = 1
            };

            EntityCollection result = service.RetrieveMultiple(query);
            if (result.Entities.Count > 0)
            {
                Guid existingId = result.Entities[0].Id;
                Entity update = new Entity("mcs_fca_mdlconfig", existingId);
                update["mcs_adjust1"] = adjust1;
                update["mcs_adjust2"] = adjust2;
                update["mcs_adjust3"] = adjust3;
                update["mcs_aggfunc"] = new OptionSetValue(aggFunc);
                update["mcs_countryname"] = benchmark;
                service.Update(update);
                return existingId;
            }

            Entity config = new Entity("mcs_fca_mdlconfig");
            config["mcs_buyergrade"] = new OptionSetValue(buyerGrade);
            config["mcs_creditgrade"] = new OptionSetValue(creditGrade);
            config["mcs_adjust1"] = adjust1;
            config["mcs_adjust2"] = adjust2;
            config["mcs_adjust3"] = adjust3;
            config["mcs_aggfunc"] = new OptionSetValue(aggFunc);
            config["mcs_countryname"] = benchmark;
            return service.Create(config);
        }

        static string GetBuyerGradeLabel(int value)
        {
            switch (value)
            {
                case 1: return "S";
                case 2: return "A";
                case 3: return "B";
                case 4: return "C";
                case 5: return "I";
                case 6: return "D1";
                case 7: return "D2";
                case 8: return "D3";
                case 9: return "D4";
                case 10: return "D5";
                default: return value.ToString();
            }
        }

        static string GetCreditGradeLabel(int value)
        {
            switch (value)
            {
                case 1: return "A0";
                case 2: return "A1";
                case 3: return "A2";
                case 4: return "A3";
                case 5: return "A4";
                case 6: return "ALL";
                default: return value.ToString();
            }
        }

        static void PrepareCustomerData(IOrganizationService service, Guid masterDataId)
        {
            Console.WriteLine($"=== 为客户 {masterDataId} 准备测试数据 ===");

            // 查找关联 Account
            Guid? accountId = FindAccount.ByMasterDataId(service, masterDataId);
            if (!accountId.HasValue)
            {
                Console.WriteLine("未找到关联 Account，请先运行 create-customer");
                return;
            }

            // 更新客户主数据
            Entity updateCustomer = new Entity("mcs_customermasterdata", masterDataId);
            updateCustomer["mcs_accounttype"] = new OptionSetValue(2); // 公司客户
            updateCustomer["mcs_kacategory"] = new OptionSetValue(1); // S级
            updateCustomer["mcs_creditgrade"] = new OptionSetValue(100000002); // A2
            updateCustomer["mcs_creditscore"] = 85m;
            updateCustomer["mcs_creditvalid"] = true;
            updateCustomer["mcs_blacklist"] = false;
            service.Update(updateCustomer);
            Console.WriteLine("更新客户主数据为 S级/A2/信用分85/有效");

            // 清理该 Account 下已有的 NetAssets 标签，避免旧数据干扰计算
            var existingTagQuery = new QueryExpression("mcs_customer_tag")
            {
                ColumnSet = new ColumnSet("mcs_customer_tagid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_accountid", ConditionOperator.Equal, accountId.Value),
                        new ConditionExpression("mcs_itemcode", ConditionOperator.Equal, "NetAssets")
                    }
                }
            };
            var existingTags = service.RetrieveMultiple(existingTagQuery);
            foreach (var old in existingTags.Entities)
            {
                service.Delete("mcs_customer_tag", old.Id);
                Console.WriteLine($"删除旧 NetAssets 标签: {old.Id}");
            }

            // 创建客户信用标签 NetAssets
            Entity tag = new Entity("mcs_customer_tag");
            tag["mcs_accountid"] = new EntityReference("account", accountId.Value);
            tag["mcs_itemcode"] = "NetAssets";
            tag["mcs_itemintvalue2"] = 2000000m;
            tag["mcs_active"] = true;
            Guid tagId = service.Create(tag);
            Console.WriteLine($"创建 NetAssets 标签: {tagId}");

            // 清理该 Account 下已有的在外货款记录，避免旧逾期数据影响计算
            var existingOutstandingQuery = new QueryExpression("mcs_outstanding")
            {
                ColumnSet = new ColumnSet("mcs_outstandingid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_account", ConditionOperator.Equal, accountId.Value)
                    }
                }
            };
            var existingOutstandings = service.RetrieveMultiple(existingOutstandingQuery);
            foreach (var old in existingOutstandings.Entities)
            {
                service.Delete("mcs_outstanding", old.Id);
                Console.WriteLine($"删除旧在外货款记录: {old.Id}");
            }

            // 创建月度在外货款记录（逾期天数<180，逾期金额为0，确保不触发逾期调整）
            Entity outstanding = new Entity("mcs_outstanding");
            outstanding["mcs_account"] = new EntityReference("account", accountId.Value);
            outstanding["mcs_name"] = $"OUT{DateTime.UtcNow:yyyyMMddHHmmss}";
            outstanding["mcs_paymentcollectioncurrentmonthrmb"] = 1200000m;
            outstanding["mcs_overdurationdays"] = 30;
            outstanding["mcs_newoverdueamountrmb"] = 0m;
            outstanding["mcs_newremainingamountrmb"] = 500000m;
            Guid outstandingId = service.Create(outstanding);
            Console.WriteLine($"创建月度在外货款记录: {outstandingId}");
        }

        internal static void TriggerCalculation(IOrganizationService service, Guid masterDataId)
        {
            Console.WriteLine($"=== 创建 mcs_fca_proc 并触发模型计算 ===");

            Entity customer = service.Retrieve("mcs_customermasterdata", masterDataId, new ColumnSet("mcs_name"));
            string custName = customer.GetAttributeValue<string>("mcs_name") ?? "测试客户";

            Entity proc = new Entity("mcs_fca_proc");
            proc["mcs_accountid"] = new EntityReference("mcs_customermasterdata", masterDataId);
            proc["mcs_custname"] = custName;
            proc["mcs_status"] = new OptionSetValue(1); // 授信校验
            Guid procId = service.Create(proc);
            Console.WriteLine($"创建 mcs_fca_proc: {procId}");

            // 更新状态到 2 触发计算
            Entity update = new Entity("mcs_fca_proc", procId);
            update["mcs_status"] = new OptionSetValue(2); // 模型计算
            service.Update(update);
            Console.WriteLine("已触发模型计算（状态 1→2）");

            CheckResult(service, procId);
        }

        internal static void ActivateProc(IOrganizationService service, Guid procId)
        {
            Console.WriteLine($"=== 生效启用 mcs_fca_proc: {procId} ===");

            // 更新状态到 3 触发生效启用
            Entity update = new Entity("mcs_fca_proc", procId);
            update["mcs_status"] = new OptionSetValue(3); // 生效启用
            service.Update(update);
            Console.WriteLine("已触发生效启用（状态 2→3）");

            // 查询结果
            CheckResult(service, procId);

            // 查询生成的额度记录
            var proc = service.Retrieve("mcs_fca_proc", procId, new ColumnSet("mcs_accountid", "mcs_doid"));
            var accountRef = proc.GetAttributeValue<EntityReference>("mcs_accountid");
            if (accountRef != null)
            {
                QueryExpression quotaQuery = new QueryExpression("mcs_fca_quota")
                {
                    ColumnSet = new ColumnSet("mcs_accountid", "mcs_custname", "mcs_doid", "mcs_fca_procid", "mcs_sellergrant", "mcs_sellerbalance"),
                    Criteria = new FilterExpression
                    {
                        Conditions = { new ConditionExpression("mcs_accountid", ConditionOperator.Equal, accountRef.Id) }
                    },
                    TopCount = 1,
                    Orders = { new OrderExpression("createdon", OrderType.Descending) }
                };
                var quotaResult = service.RetrieveMultiple(quotaQuery);
                if (quotaResult.Entities.Count > 0)
                {
                    var quota = quotaResult.Entities[0];
                    Console.WriteLine($"\n=== 额度记录: {quota.Id} ===");
                    Console.WriteLine($"  客户编码(mcs_custname): {quota.GetAttributeValue<string>("mcs_custname")}");
                    Console.WriteLine($"  文本序列号(mcs_doid): {quota.GetAttributeValue<string>("mcs_doid")}");
                    var procIdRef = quota.GetAttributeValue<EntityReference>("mcs_fca_procid");
                    Console.WriteLine($"  查找序列号(mcs_fca_procid): {(procIdRef != null ? procIdRef.Id.ToString() : "(空)")}");
                    Console.WriteLine($"  卖方额度: {quota.GetAttributeValue<Money>("mcs_sellergrant")?.Value}");
                    Console.WriteLine($"  可用余额: {quota.GetAttributeValue<Money>("mcs_sellerbalance")?.Value}");
                }

                QueryExpression recordQuery = new QueryExpression("mcs_fca_records")
                {
                    ColumnSet = new ColumnSet("mcs_accountid", "mcs_custname", "mcs_sellergrant", "mcs_tobebalance", "mcs_adjustamt"),
                    Criteria = new FilterExpression
                    {
                        Conditions = { new ConditionExpression("mcs_accountid", ConditionOperator.Equal, accountRef.Id) }
                    },
                    TopCount = 1,
                    Orders = { new OrderExpression("createdon", OrderType.Descending) }
                };
                var recordResult = service.RetrieveMultiple(recordQuery);
                if (recordResult.Entities.Count > 0)
                {
                    var record = recordResult.Entities[0];
                    Console.WriteLine($"\n=== 台账记录: {record.Id} ===");
                    Console.WriteLine($"  客户编码(mcs_custname): {record.GetAttributeValue<string>("mcs_custname")}");
                    Console.WriteLine($"  授信限额: {record.GetAttributeValue<Money>("mcs_sellergrant")?.Value}");
                    Console.WriteLine($"  调整后余额: {record.GetAttributeValue<Money>("mcs_tobebalance")?.Value}");
                }
            }
        }

        static void SetAccountSapNumber(IOrganizationService service, Guid accountId, string sapNumber)
        {
            Entity account = new Entity("account", accountId);
            account["mcs_sapnumber"] = sapNumber;
            service.Update(account);
            Console.WriteLine($"已设置 Account {accountId} 的 SAP 客户编号为: {sapNumber}");
        }

        static void SetQuotaCustName(IOrganizationService service, Guid quotaId, string custName)
        {
            Entity quota = new Entity("mcs_fca_quota", quotaId);
            quota["mcs_custname"] = custName;
            service.Update(quota);
            Console.WriteLine($"已更新额度记录 {quotaId} 的客户编码为: {custName}");
        }

        static void ShowAccount(IOrganizationService service, Guid accountId)
        {
            Console.WriteLine($"=== 查询 Account: {accountId} ===");
            var account = service.Retrieve("account", accountId, new ColumnSet("name", "accountnumber", "mcs_sapnumber", "mcs_customermasterdata"));
            Console.WriteLine($"  name={account.GetAttributeValue<string>("name")}");
            Console.WriteLine($"  accountnumber={account.GetAttributeValue<string>("accountnumber")}");
            Console.WriteLine($"  mcs_sapnumber={account.GetAttributeValue<string>("mcs_sapnumber")}");
            var masterRef = account.GetAttributeValue<EntityReference>("mcs_customermasterdata");
            Console.WriteLine($"  mcs_customermasterdata={(masterRef != null ? masterRef.Id.ToString() : "(空)")}");
        }

        static void ListQuotaRecords(IOrganizationService service, Guid accountId)
        {
            Console.WriteLine($"=== 查询客户 {accountId} 的额度记录 ===");
            QueryExpression query = new QueryExpression("mcs_fca_quota")
            {
                ColumnSet = new ColumnSet("mcs_fca_quotaid", "mcs_accountid", "mcs_custname", "mcs_doid", "mcs_fca_procid", "mcs_sellergrant", "mcs_sellerbalance", "createdon"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("mcs_accountid", ConditionOperator.Equal, accountId) }
                },
                Orders = { new OrderExpression("createdon", OrderType.Descending) }
            };
            var result = service.RetrieveMultiple(query);
            Console.WriteLine($"找到 {result.Entities.Count} 条额度记录");
            foreach (var q in result.Entities)
            {
                var procRef = q.GetAttributeValue<EntityReference>("mcs_fca_procid");
                Console.WriteLine($"  ID={q.Id}, createdon={q.GetAttributeValue<DateTime>("createdon")}, custname={q.GetAttributeValue<string>("mcs_custname")}, doid={q.GetAttributeValue<string>("mcs_doid")}, procid={(procRef != null ? procRef.Id.ToString() : "(空)")}, grant={q.GetAttributeValue<Money>("mcs_sellergrant")?.Value}, balance={q.GetAttributeValue<Money>("mcs_sellerbalance")?.Value}");
            }
        }

        static void TestFallbackScenario(IOrganizationService service, Guid masterDataId)
        {
            Console.WriteLine($"=== 测试历史基准额度兜底场景 {masterDataId} ===");

            Entity update = new Entity("mcs_customermasterdata", masterDataId);
            update["mcs_creditscore"] = 0m;
            update["mcs_creditvalid"] = false;
            service.Update(update);
            Console.WriteLine("已设置客户信用分=0，信用评估有效=false");

            TriggerCalculation(service, masterDataId);
        }

        static void TestOverdueScenario(IOrganizationService service, Guid masterDataId, decimal overdueAmount, decimal outstandingBalance)
        {
            Console.WriteLine($"=== 测试逾期调整场景 {masterDataId} ===");
            Console.WriteLine($"逾期金额={overdueAmount}, 在外货款余额={outstandingBalance}, 比例={(outstandingBalance == 0 ? 0 : overdueAmount / outstandingBalance):P}");

            Guid? accountId = FindAccount.ByMasterDataId(service, masterDataId);
            if (!accountId.HasValue)
            {
                Console.WriteLine("未找到关联 Account");
                return;
            }

            // 关闭黑名单，避免场景4与场景3同时触发，确保只验证场景3
            Entity customerUpdate = new Entity("mcs_customermasterdata", masterDataId);
            customerUpdate["mcs_blacklist"] = false;
            service.Update(customerUpdate);
            Console.WriteLine("已重置客户黑名单=false");

            ResetOutstanding(service, accountId.Value, 200, overdueAmount, outstandingBalance);

            TriggerCalculation(service, masterDataId);
        }

        internal static void ResetOutstanding(IOrganizationService service, Guid accountId, int days, decimal overdueAmount, decimal outstandingBalance)
        {
            QueryExpression query = new QueryExpression("mcs_outstanding")
            {
                ColumnSet = new ColumnSet("mcs_outstandingid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_account", ConditionOperator.Equal, accountId)
                    }
                },
                TopCount = 1
            };

            EntityCollection result = service.RetrieveMultiple(query);
            if (result.Entities.Count > 0)
            {
                Entity update = new Entity("mcs_outstanding", result.Entities[0].Id);
                update["mcs_overdurationdays"] = days;
                update["mcs_newoverdueamountrmb"] = overdueAmount;
                update["mcs_newremainingamountrmb"] = outstandingBalance;
                service.Update(update);
                Console.WriteLine($"更新在外货款记录: outstandingId={result.Entities[0].Id}, 逾期天数={days}, 逾期金额={overdueAmount}, 在外货款余额={outstandingBalance}");
            }
            else
            {
                Entity outstanding = new Entity("mcs_outstanding");
                outstanding["mcs_account"] = new EntityReference("account", accountId);
                outstanding["mcs_name"] = $"OUT{DateTime.UtcNow:yyyyMMddHHmmss}";
                outstanding["mcs_paymentcollectioncurrentmonthrmb"] = 1200000m;
                outstanding["mcs_overdurationdays"] = days;
                outstanding["mcs_newoverdueamountrmb"] = overdueAmount;
                outstanding["mcs_newremainingamountrmb"] = outstandingBalance;
                Guid outstandingId = service.Create(outstanding);
                Console.WriteLine($"新建在外货款记录: outstandingId={outstandingId}");
            }
        }

        static void QueryCustomerFields(IOrganizationService service, string customerName)
        {
            Console.WriteLine($"=== 查询客户主数据编码字段: {customerName} ===");

            QueryExpression query = new QueryExpression("mcs_customermasterdata")
            {
                ColumnSet = new ColumnSet(
                    "mcs_name",
                    "mcs_accountnumber",
                    "mcs_erpcustomercode",
                    "mcs_registrationno",
                    "mcs_previousmasterrecordnumber",
                    "mcs_branchcode",
                    "mcs_cofaceid",
                    "mcs_countrycode",
                    "mcs_phone"
                ),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_name", ConditionOperator.Equal, customerName)
                    }
                },
                TopCount = 5
            };

            EntityCollection result = service.RetrieveMultiple(query);
            Console.WriteLine($"找到 {result.Entities.Count} 条记录");
            foreach (Entity e in result.Entities)
            {
                Console.WriteLine($"ID: {e.Id}");
                Console.WriteLine($"  mcs_name (客户名): {e.GetAttributeValue<string>("mcs_name")}");
                Console.WriteLine($"  mcs_accountnumber (流水号/Reference No): {e.GetAttributeValue<string>("mcs_accountnumber")}");
                Console.WriteLine($"  mcs_erpcustomercode (ERP客户代码): {e.GetAttributeValue<string>("mcs_erpcustomercode")}");
                Console.WriteLine($"  mcs_registrationno (注册号): {e.GetAttributeValue<string>("mcs_registrationno")}");
                Console.WriteLine($"  mcs_previousmasterrecordnumber (上一主记录编号): {e.GetAttributeValue<string>("mcs_previousmasterrecordnumber")}");
                Console.WriteLine($"  mcs_branchcode (网点编码): {e.GetAttributeValue<string>("mcs_branchcode")}");
                Console.WriteLine($"  mcs_cofaceid (科法斯客户代码): {e.GetAttributeValue<string>("mcs_cofaceid")}");
                Console.WriteLine($"  mcs_countrycode (国家码): {e.GetAttributeValue<string>("mcs_countrycode")}");

                // 列出所有非空字段（帮助定位客户编码字段）
                Console.WriteLine("  --- 所有非空字段 ---");
                foreach (var attr in e.Attributes)
                {
                    string valueStr = attr.Value?.ToString() ?? "";
                    if (!string.IsNullOrWhiteSpace(valueStr) && attr.Key != "mcs_name")
                    {
                        Console.WriteLine($"    {attr.Key}: {valueStr}");
                    }
                }

                // 查询关联 account 的客户编码
                QueryExpression accountQuery = new QueryExpression("account")
                {
                    ColumnSet = new ColumnSet(true),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("mcs_customermasterdata", ConditionOperator.Equal, e.Id)
                        }
                    },
                    TopCount = 3
                };
                EntityCollection accounts = service.RetrieveMultiple(accountQuery);
                Console.WriteLine($"  关联 Account 数量: {accounts.Entities.Count}");
                foreach (Entity acc in accounts.Entities)
                {
                    Console.WriteLine($"    Account: {acc.GetAttributeValue<string>("name")}, accountnumber: {acc.GetAttributeValue<string>("accountnumber")}");
                    Console.WriteLine("    --- Account 所有非空字段 ---");
                    foreach (var attr in acc.Attributes)
                    {
                        string valueStr = attr.Value?.ToString() ?? "";
                        if (!string.IsNullOrWhiteSpace(valueStr) && attr.Key != "name")
                        {
                            Console.WriteLine($"      {attr.Key}: {valueStr}");
                        }
                    }

                    // 查找值为 BMW0001 或 0210000677 的字段
                    Console.WriteLine("    --- 疑似客户编号字段 ---");
                    string[] candidates = { "BMW0001", "0210000677", "0210000680" };
                    foreach (var attr in acc.Attributes)
                    {
                        string valueStr = attr.Value?.ToString() ?? "";
                        foreach (var candidate in candidates)
                        {
                            if (valueStr.IndexOf(candidate, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                Console.WriteLine($"      >>> {attr.Key}: {valueStr}");
                            }
                        }
                    }
                }
            }
        }

        static void QueryBpf(IOrganizationService service)
        {
            Console.WriteLine("=== 查询厂端授信 BPF ===");

            QueryExpression query = new QueryExpression("workflow")
            {
                ColumnSet = new ColumnSet("name", "workflowid", "type", "category", "statecode", "primaryentity"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("category", ConditionOperator.Equal, 4),
                        new ConditionExpression("name", ConditionOperator.Like, "%厂端授信%")
                    }
                }
            };

            EntityCollection result = service.RetrieveMultiple(query);
            Console.WriteLine($"找到 {result.Entities.Count} 个 BPF");
            foreach (Entity e in result.Entities)
            {
                Console.WriteLine($"BPF: {e.GetAttributeValue<string>("name")}, ID: {e.Id}, PrimaryEntity: {e.GetAttributeValue<string>("primaryentity")}");

                // 查询 BPF 阶段
                QueryExpression stageQuery = new QueryExpression("processstage")
                {
                    ColumnSet = new ColumnSet("processstageid", "stagename", "stagecategory"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("processid", ConditionOperator.Equal, e.Id)
                        }
                    },
                    Orders =
                    {
                        new OrderExpression("stagecategory", OrderType.Ascending)
                    }
                };

                EntityCollection stages = service.RetrieveMultiple(stageQuery);
                foreach (Entity stage in stages.Entities)
                {
                    Console.WriteLine($"  Stage: {stage.GetAttributeValue<string>("stagename")}, ID: {stage.Id}, Category: {stage.GetAttributeValue<OptionSetValue>("stagecategory")?.Value}");
                }
            }
        }

        static void CheckCustomerTags(IOrganizationService service, Guid masterDataId)
        {
            Console.WriteLine($"=== 查询客户 {masterDataId} 的信用标签 ===");
            Guid? accountId = FindAccount.ByMasterDataId(service, masterDataId);
            if (!accountId.HasValue)
            {
                Console.WriteLine("未找到关联 Account");
                return;
            }
            Console.WriteLine($"Account ID: {accountId.Value}");

            var query = new QueryExpression("mcs_customer_tag")
            {
                ColumnSet = new ColumnSet("mcs_customer_tagid", "mcs_itemcode", "mcs_itemintvalue2", "mcs_active", "mcs_accountid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_accountid", ConditionOperator.Equal, accountId.Value)
                    }
                }
            };
            var result = service.RetrieveMultiple(query);
            Console.WriteLine($"找到 {result.Entities.Count} 条标签记录");
            foreach (var e in result.Entities)
            {
                Console.WriteLine($"  ID={e.Id}, itemcode={e.GetAttributeValue<string>("mcs_itemcode")}, value={e.GetAttributeValue<decimal>("mcs_itemintvalue2")}, active={e.GetAttributeValue<bool>("mcs_active")}");
            }
        }

        static void LinkConfigToVersion(IOrganizationService service)
        {
            Console.WriteLine("=== 将现有授信参数配置关联到生效模型版本 ===");

            // 1. 查找生效的模型版本（默认取 V20260830，若不存在则取第一个生效版本）
            QueryExpression versionQuery = new QueryExpression("mcs_fca_mdlversion")
            {
                ColumnSet = new ColumnSet("mcs_fca_mdlversionid", "mcs_versionid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_isactive", ConditionOperator.Equal, 1)
                    }
                },
                Orders =
                {
                    new OrderExpression("createdon", OrderType.Descending)
                },
                TopCount = 1
            };

            EntityCollection versionResult = service.RetrieveMultiple(versionQuery);
            if (versionResult.Entities.Count == 0)
            {
                Console.WriteLine("❌ 未找到生效的模型版本，请先创建模型版本。");
                return;
            }

            Entity version = versionResult.Entities[0];
            Guid versionId = version.Id;
            string versionText = version.GetAttributeValue<string>("mcs_versionid") ?? versionId.ToString();
            Console.WriteLine($"目标模型版本: {versionText} ({versionId})");

            // 2. 查询尚未关联版本的参数配置记录
            QueryExpression configQuery = new QueryExpression("mcs_fca_mdlconfig")
            {
                ColumnSet = new ColumnSet("mcs_argid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_fca_mdlversionid", ConditionOperator.Null)
                    }
                }
            };

            EntityCollection configResult = service.RetrieveMultiple(configQuery);
            Console.WriteLine($"找到 {configResult.Entities.Count} 条未关联版本的参数配置记录");

            // 3. 批量更新
            int updatedCount = 0;
            foreach (Entity config in configResult.Entities)
            {
                Entity update = new Entity("mcs_fca_mdlconfig", config.Id);
                update["mcs_fca_mdlversionid"] = new EntityReference("mcs_fca_mdlversion", versionId);
                service.Update(update);
                updatedCount++;
                Console.WriteLine($"  ✓ 已更新: {config.Id}");
            }

            Console.WriteLine($"完成，共更新 {updatedCount} 条记录。");
        }

        static void SetCreditScoreByName(IOrganizationService service, string customerName, decimal? score)
        {
            Console.WriteLine($"=== 设置客户信用分: {customerName} => {(score.HasValue ? score.Value.ToString() : "null(清空)")} ===");

            QueryExpression query = new QueryExpression("mcs_customermasterdata")
            {
                ColumnSet = new ColumnSet("mcs_customermasterdataid", "mcs_name", "mcs_creditscore"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_name", ConditionOperator.Equal, customerName)
                    }
                },
                TopCount = 1
            };

            EntityCollection result = service.RetrieveMultiple(query);
            if (result.Entities.Count == 0)
            {
                Console.WriteLine("❌ 未找到客户");
                return;
            }

            var customer = result.Entities[0];
            Guid masterDataId = customer.Id;
            var originalScore = customer.GetAttributeValue<decimal?>("mcs_creditscore");
            Console.WriteLine($"找到客户: {customer.GetAttributeValue<string>("mcs_name")} ({masterDataId})");
            Console.WriteLine($"  原信用分: {(originalScore.HasValue ? originalScore.Value.ToString() : "null")}");

            Entity updateCustomer = new Entity("mcs_customermasterdata", masterDataId);
            if (score.HasValue)
            {
                updateCustomer["mcs_creditscore"] = score.Value;
            }
            else
            {
                updateCustomer["mcs_creditscore"] = null;
            }
            service.Update(updateCustomer);
            Console.WriteLine($"✓ 已设置信用分={(score.HasValue ? score.Value.ToString() : "null")}");
        }

        static void SetCreditValidByName(IOrganizationService service, string customerName, bool creditValid)
        {
            Console.WriteLine($"=== 设置客户信用评估有效状态: {customerName} => {creditValid} ===");

            QueryExpression query = new QueryExpression("mcs_customermasterdata")
            {
                ColumnSet = new ColumnSet("mcs_customermasterdataid", "mcs_name", "mcs_creditvalid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_name", ConditionOperator.Equal, customerName)
                    }
                },
                TopCount = 1
            };

            EntityCollection result = service.RetrieveMultiple(query);
            if (result.Entities.Count == 0)
            {
                Console.WriteLine("❌ 未找到客户");
                return;
            }

            var customer = result.Entities[0];
            Guid masterDataId = customer.Id;
            Console.WriteLine($"找到客户: {customer.GetAttributeValue<string>("mcs_name")} ({masterDataId})");
            Console.WriteLine($"  原信用评估有效状态: {customer.GetAttributeValue<bool>("mcs_creditvalid")}");

            Entity updateCustomer = new Entity("mcs_customermasterdata", masterDataId);
            updateCustomer["mcs_creditvalid"] = creditValid;
            service.Update(updateCustomer);
            Console.WriteLine($"✓ 已设置 mcs_creditvalid={creditValid}");
        }

        static void CheckOverdueFields(IOrganizationService service, string customerName)
        {
            Console.WriteLine($"=== 核对该客户逾期字段币种: {customerName} ===");

            QueryExpression query = new QueryExpression("mcs_outstanding")
            {
                ColumnSet = new ColumnSet(
                    "mcs_newoverdueamount", "mcs_newoverdueamount_base", "mcs_newoverdueamountrmb",
                    "mcs_newremainingamount", "mcs_newremainingamount_base", "mcs_newremainingamountrmb",
                    "mcs_isocurrencycode", "mcs_createon", "createdon"),
                TopCount = 5,
                Orders = { new OrderExpression("mcs_newoverdueamountrmb", OrderType.Descending) }
            };

            if (!string.Equals(customerName, "ALL", StringComparison.OrdinalIgnoreCase))
            {
                // 1. 找 Account
                QueryExpression accountQuery = new QueryExpression("account")
                {
                    ColumnSet = new ColumnSet("accountid", "name"),
                    Criteria = new FilterExpression
                    {
                        Conditions = { new ConditionExpression("name", ConditionOperator.Equal, customerName) }
                    },
                    TopCount = 1
                };
                var accountResult = service.RetrieveMultiple(accountQuery);
                if (accountResult.Entities.Count == 0)
                {
                    Console.WriteLine("❌ 未找到 Account");
                    return;
                }
                var accountId = accountResult.Entities[0].Id;
                Console.WriteLine($"Account: {accountId}");
                query.Criteria.Conditions.Add(new ConditionExpression("mcs_account", ConditionOperator.Equal, accountId));
            }
            else
            {
                query.Criteria.Conditions.Add(new ConditionExpression("mcs_newoverdueamountrmb", ConditionOperator.GreaterThan, 0m));
            }

            var result = service.RetrieveMultiple(query);
            Console.WriteLine($"找到 {result.Entities.Count} 条有逾期金额的记录");
            foreach (var e in result.Entities)
            {
                Console.WriteLine("---");
                Console.WriteLine($"货币代码: {e.GetAttributeValue<string>("mcs_isocurrencycode")}");
                Console.WriteLine($"逾期金额 transactional: {e.GetAttributeValue<Money>("mcs_newoverdueamount")?.Value:N2}");
                Console.WriteLine($"逾期金额 base: {e.GetAttributeValue<Money>("mcs_newoverdueamount_base")?.Value:N2}");
                Console.WriteLine($"逾期金额 rmb: {e.GetAttributeValue<decimal?>("mcs_newoverdueamountrmb"):N2}");
                Console.WriteLine($"在外货款 transactional: {e.GetAttributeValue<Money>("mcs_newremainingamount")?.Value:N2}");
                Console.WriteLine($"在外货款 base: {e.GetAttributeValue<Money>("mcs_newremainingamount_base")?.Value:N2}");
                Console.WriteLine($"在外货款 rmb: {e.GetAttributeValue<decimal?>("mcs_newremainingamountrmb"):N2}");
            }
        }

        static void ResetRejectByName(IOrganizationService service, string customerName)
        {
            Console.WriteLine($"=== 按名称重置客户不予授信数据: {customerName} ===");

            // 1. 查找客户主数据
            QueryExpression query = new QueryExpression("mcs_customermasterdata")
            {
                ColumnSet = new ColumnSet("mcs_customermasterdataid", "mcs_name", "mcs_blacklist", "mcs_creditgrant"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_name", ConditionOperator.Equal, customerName)
                    }
                },
                TopCount = 1
            };

            EntityCollection result = service.RetrieveMultiple(query);
            if (result.Entities.Count == 0)
            {
                Console.WriteLine("❌ 未找到客户");
                return;
            }

            var customer = result.Entities[0];
            Guid masterDataId = customer.Id;
            Console.WriteLine($"找到客户: {customer.GetAttributeValue<string>("mcs_name")} ({masterDataId})");
            Console.WriteLine($"  当前黑名单: {customer.GetAttributeValue<bool>("mcs_blacklist")}");
            Console.WriteLine($"  当前不予授信: {customer.GetAttributeValue<bool>("mcs_creditgrant")}");

            // 2. 更新客户主数据
            Entity updateCustomer = new Entity("mcs_customermasterdata", masterDataId);
            updateCustomer["mcs_blacklist"] = false;
            updateCustomer["mcs_creditgrant"] = false;
            service.Update(updateCustomer);
            Console.WriteLine("✓ 已重置黑名单=false, 不予授信=false");

            // 3. 查找关联 Account
            Guid? accountId = FindAccount.ByMasterDataId(service, masterDataId);
            if (!accountId.HasValue)
            {
                Console.WriteLine("⚠ 未找到关联 Account，跳过在外货款重置");
                return;
            }

            // 4. 重置在外货款为非逾期状态
            ResetOutstanding(service, accountId.Value, 30, 0m, 500000m);
            Console.WriteLine("✓ 已重置在外货款为非逾期状态");

            // 5. 清理该客户下未生效启用的 mcs_fca_proc 中已自动勾选的不予授信场景，避免旧记录继续阻塞测试
            var procQuery = new QueryExpression("mcs_fca_proc")
            {
                ColumnSet = new ColumnSet("mcs_fca_procid", "mcs_creditreject"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_accountid", ConditionOperator.Equal, masterDataId),
                        new ConditionExpression("mcs_status", ConditionOperator.NotEqual, 3)
                    }
                }
            };

            var procResult = service.RetrieveMultiple(procQuery);
            foreach (var proc in procResult.Entities)
            {
                var rejectOptions = proc.GetAttributeValue<OptionSetValueCollection>("mcs_creditreject");
                if (rejectOptions != null && rejectOptions.Count > 0)
                {
                    Entity updateProc = new Entity("mcs_fca_proc", proc.Id);
                    updateProc["mcs_creditreject"] = new OptionSetValueCollection();
                    service.Update(updateProc);
                    Console.WriteLine($"✓ 已清理 mcs_fca_proc {proc.Id} 的不予授信场景");
                }
            }
        }

        static void QueryAudit(IOrganizationService service, Guid recordId)
        {
            Console.WriteLine($"=== 查询记录审计历史: {recordId} ===");
            var query = new QueryExpression("audit")
            {
                ColumnSet = new ColumnSet("createdon", "operation", "attributemask", "changedata", "action"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("objectid", ConditionOperator.Equal, recordId) }
                },
                Orders = { new OrderExpression("createdon", OrderType.Ascending) }
            };
            var result = service.RetrieveMultiple(query);
            Console.WriteLine($"找到 {result.Entities.Count} 条审计记录");
            foreach (var a in result.Entities)
            {
                Console.WriteLine("---");
                Console.WriteLine($"时间: {a.GetAttributeValue<DateTime?>("createdon")}, 操作: {a.GetAttributeValue<OptionSetValue>("operation")?.Value}, Action: {a.GetAttributeValue<OptionSetValue>("action")?.Value}");
                Console.WriteLine($"字段掩码: {a.GetAttributeValue<string>("attributemask")}");
                string changeData = a.GetAttributeValue<string>("changedata");
                if (!string.IsNullOrEmpty(changeData))
                {
                    try
                    {
                        byte[] bytes = Convert.FromBase64String(changeData);
                        Console.WriteLine($"变更数据: {System.Text.Encoding.UTF8.GetString(bytes)}");
                    }
                    catch
                    {
                        Console.WriteLine($"变更数据(原始): {changeData}");
                    }
                }
            }
        }

        static void CheckResult(IOrganizationService service, Guid procId)
        {
            Console.WriteLine($"=== 查询计算结果 {procId} ===");

            Entity proc = service.Retrieve("mcs_fca_proc", procId,
                new ColumnSet("mcs_modelgrant", "mcs_initigrant", "mcs_versionid", "mcs_modeldesc", "mcs_doproc", "mcs_status", "mcs_creditreject", "mcs_accountid"));

            Console.WriteLine($"状态: {proc.GetAttributeValue<OptionSetValue>("mcs_status")?.Value}");
            Console.WriteLine($"模型计算额度USD: {proc.GetAttributeValue<Money>("mcs_modelgrant")?.Value:N2}");
            Console.WriteLine($"调整模型额度USD: {proc.GetAttributeValue<Money>("mcs_initigrant")?.Value:N2}");
            var versionRef = proc.GetAttributeValue<EntityReference>("mcs_versionid");
            Console.WriteLine($"模型版本ID: {versionRef?.Id}");
            Console.WriteLine($"模型描述: {proc.GetAttributeValue<string>("mcs_modeldesc")}");

            var rejectOptions = proc.GetAttributeValue<OptionSetValueCollection>("mcs_creditreject");
            if (rejectOptions != null && rejectOptions.Count > 0)
            {
                Console.WriteLine($"不予授信场景: [{string.Join(",", rejectOptions.Select(o => o.Value))}]");
            }
            else
            {
                Console.WriteLine("不予授信场景: (空)");
            }

            var masterRef = proc.GetAttributeValue<EntityReference>("mcs_accountid");
            if (masterRef != null)
            {
                Entity master = service.Retrieve("mcs_customermasterdata", masterRef.Id,
                    new ColumnSet("mcs_creditgrant", "mcs_blacklist"));
                Console.WriteLine($"客户主数据 不予授信标志(mcs_creditgrant): {master.GetAttributeValue<bool>("mcs_creditgrant")}");
                Console.WriteLine($"客户主数据 黑名单(mcs_blacklist): {master.GetAttributeValue<bool>("mcs_blacklist")}");
            }

            Console.WriteLine($"计算日志:\n{proc.GetAttributeValue<string>("mcs_doproc")}");
        }
    }
}
