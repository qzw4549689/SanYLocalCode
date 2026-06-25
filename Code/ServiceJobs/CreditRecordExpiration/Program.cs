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

            // 默认预览模式；传入 --execute 才实际更新
            var previewMode = args.Length == 0 || args[0] != "--execute";

            if (previewMode)
            {
                Console.WriteLine("当前为预览模式，不会修改数据。如需实际执行，请追加 --execute 参数。");
                Console.WriteLine();

                var count = await service.PreviewCountAsync();
                Console.WriteLine($"发现 {count} 条待失效记录");
            }
            else
            {
                var count = await service.ExpireAsync();
                Console.WriteLine($"成功失效 {count} 条记录");
            }
        }
    }
}
