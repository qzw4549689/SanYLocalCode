using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.IO;

namespace DeployTool
{
    public class DownloadBppSalesAssembly
    {
        public static void Download(ServiceClient service)
        {
            Console.WriteLine(">>> 下载BPP Sales Assembly...");
            
            try
            {
                var query = new QueryExpression("pluginassembly")
                {
                    ColumnSet = new ColumnSet("pluginassemblyid", "name", "content"),
                    Criteria = new FilterExpression
                    {
                        Conditions = 
                        { 
                            new ConditionExpression("name", ConditionOperator.Equal, "SanyD365.D365ExtensionApi.Sales")
                        }
                    }
                };
                
                var results = service.RetrieveMultiple(query);
                if (results.Entities.Count > 0)
                {
                    var assembly = results.Entities[0];
                    string content = assembly.GetAttributeValue<string>("content");
                    if (!string.IsNullOrEmpty(content))
                    {
                        var bytes = Convert.FromBase64String(content);
                        var path = "/tmp/bpp_sales_assembly.dll";
                        File.WriteAllBytes(path, bytes);
                        Console.WriteLine($"  ✅ Assembly已保存: {path} ({bytes.Length} bytes)");
                        
                        // 查找类型
                        Console.WriteLine("  查找BPP相关类型...");
                        var psi = new System.Diagnostics.ProcessStartInfo("monodis", $"--typedef {path}")
                        {
                            RedirectStandardOutput = true,
                            UseShellExecute = false
                        };
                        var proc = System.Diagnostics.Process.Start(psi);
                        string output = proc.StandardOutput.ReadToEnd();
                        proc.WaitForExit();
                        
                        foreach (var line in output.Split('\n'))
                        {
                            if (line.Contains("BppStart") || line.Contains("BPP") || line.Contains("DomainAccount"))
                            {
                                Console.WriteLine($"    {line.Trim()}");
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine("  ❌ 未找到Assembly");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  失败: {ex.Message}");
            }
        }
    }
}
