using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace DeployTool
{
    public class CheckReportJson
    {
        public static void Check(ServiceClient service)
        {
            Console.WriteLine(">>> 检查 Report JSON 中的注册资本数据...");

            var query = new QueryExpression("mcs_credit_record")
            {
                ColumnSet = new ColumnSet("mcs_reportjson", "mcs_rptorderid"),
                Orders = { new OrderExpression("modifiedon", OrderType.Descending) },
                TopCount = 1
            };

            var results = service.RetrieveMultiple(query);
            if (results.Entities.Count == 0)
            {
                Console.WriteLine("  未找到记录");
                return;
            }

            var record = results.Entities[0];
            var reportJson = record.GetAttributeValue<string>("mcs_reportjson") ?? "";
            var rptOrderId = record.GetAttributeValue<string>("mcs_rptorderid");

            Console.WriteLine($"  Report订单ID: {rptOrderId}");
            Console.WriteLine($"  Report JSON长度: {reportJson.Length}");

            if (string.IsNullOrEmpty(reportJson))
            {
                Console.WriteLine("  Report JSON 为空");
                return;
            }

            // 简单文本搜索关键字
            string[] keywords = { "capital", "shareCapital", "registeredCapital", "share_capital" };
            foreach (var kw in keywords)
            {
                if (reportJson.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Console.WriteLine($"  ✅ JSON 中包含 '{kw}'");
                }
            }

            // 显示包含 capital 的片段
            int idx = reportJson.IndexOf("capital", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                int start = Math.Max(0, idx - 50);
                int len = Math.Min(200, reportJson.Length - start);
                Console.WriteLine($"  片段: ...{reportJson.Substring(start, len)}...");
            }
        }
    }
}
