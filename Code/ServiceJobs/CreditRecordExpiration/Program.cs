using D365ToolCommon.Connection;
using Microsoft.Extensions.Logging;

namespace Peter.ServiceJobs.CreditRecordExpiration
{
    /// <summary>
    /// 本地测试入口。
    /// 运行前请设置环境变量 D365_URL（可选，默认 dev1）。
    /// 首次运行会走 Device Code Flow，需浏览器登录。
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<CreditRecordExpirationService>();

            Console.WriteLine("===== 信用评估记录过期处理 =====");

            var serviceClient = await D365ConnectionFactory.CreateAsync();
            if (!serviceClient.IsReady)
            {
                Console.WriteLine("❌ D365 连接失败");
                return;
            }

            Console.WriteLine($"✅ 已连接: {serviceClient.ConnectedOrgFriendlyName}");
            Console.WriteLine();

            var service = new CreditRecordExpirationService(serviceClient, logger);

            // 默认预览模式；传入 --execute 才实际更新；传入 --diagnose 查看诊断统计
            var executeMode = args.Length > 0 && args[0] == "--execute";
            var diagnoseMode = args.Length > 0 && args[0] == "--diagnose";
            var seedTestDataMode = args.Length > 1 && args[0] == "--seed-test-data";
            var analyzeScoringCardsMode = args.Length > 0 && args[0] == "--analyze-scoring-cards";

            if (diagnoseMode)
            {
                Console.WriteLine("当前为诊断模式，不会修改数据。");
                Console.WriteLine();

                var diagnose = await service.DiagnoseAsync();
                Console.WriteLine($"评估记录总数: {diagnose.TotalRecordCount}");
                Console.WriteLine($"mcs_active = true 的记录数: {diagnose.ActiveRecordCount}");
                Console.WriteLine($"mcs_active = true 且 approvedate 不为空的记录数: {diagnose.ActiveWithApproveDateCount}");
                Console.WriteLine($"过期日期阈值: {diagnose.ExpireBeforeDate:yyyy-MM-dd}");
                Console.WriteLine($"已过期记录数: {diagnose.ExpiredRecordCount}");
            }
            else if (analyzeScoringCardsMode)
            {
                Console.WriteLine("当前为评分卡配置分析模式，不会修改数据。");
                Console.WriteLine();

                await service.AnalyzeScoringCardsAsync();
            }
            else if (seedTestDataMode)
            {
                var scoreId = args[1];
                Console.WriteLine($"正在为 {scoreId} 创建测试数据（approvedate 设为 1 年前）...");

                var success = await service.SeedTestDataAsync(scoreId);
                if (success)
                {
                    Console.WriteLine($"✅ {scoreId} 已设置为过期测试数据");
                }
                else
                {
                    Console.WriteLine($"❌ 未找到 {scoreId}");
                }
            }
            else if (executeMode)
            {
                var result = await service.ExpireAsync();
                Console.WriteLine($"成功失效 {result.ExpiredRecordCount} 条评估记录");
                Console.WriteLine($"成功将 {result.ExpiredCustomerMasterDataCount} 个客户主数据标记为信用评估失效");
            }
            else
            {
                Console.WriteLine("当前为预览模式，不会修改数据。如需实际执行，请追加 --execute 参数。");
                Console.WriteLine();

                var preview = await service.PreviewAsync();
                Console.WriteLine($"发现 {preview.ExpiredRecordCount} 条待失效评估记录");
                Console.WriteLine($"涉及 {preview.AffectedAccountCount} 个客户");
                Console.WriteLine($"预计 {preview.AffectedCustomerMasterDataCount} 个客户主数据会被标记为信用评估失效");
            }
        }
    }
}
