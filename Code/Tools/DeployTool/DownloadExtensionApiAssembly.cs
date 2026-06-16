using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.IO;
using System.Linq;

namespace DeployTool
{
    public class DownloadExtensionApiAssembly
    {
        public static void Download(ServiceClient service, string assemblyName)
        {
            Console.WriteLine($">>> 下载 Assembly: {assemblyName}...");

            try
            {
                var query = new QueryExpression("pluginassembly")
                {
                    ColumnSet = new ColumnSet("pluginassemblyid", "name", "content"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("name", ConditionOperator.Equal, assemblyName)
                        }
                    }
                };

                var results = service.RetrieveMultiple(query);
                if (results.Entities.Count == 0)
                {
                    Console.WriteLine($"  ❌ 未找到Assembly: {assemblyName}");
                    return;
                }

                var assembly = results.Entities[0];
                string content = assembly.GetAttributeValue<string>("content");
                if (string.IsNullOrEmpty(content))
                {
                    Console.WriteLine("  ❌ Assembly content为空");
                    return;
                }

                var bytes = Convert.FromBase64String(content);
                var path = $"/tmp/{assemblyName}.dll";
                File.WriteAllBytes(path, bytes);
                Console.WriteLine($"  ✅ Assembly已保存: {path} ({bytes.Length} bytes)");

                // 使用 monodis 列出类型
                var psi = new System.Diagnostics.ProcessStartInfo("monodis", $"--typedef \"{path}\"")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                };
                var proc = System.Diagnostics.Process.Start(psi);
                string output = proc!.StandardOutput.ReadToEnd();
                proc.WaitForExit();

                Console.WriteLine("  BPP相关类型:");
                foreach (var line in output.Split('\n'))
                {
                    if (line.Contains("Bpp", StringComparison.OrdinalIgnoreCase) ||
                        line.Contains("BPP", StringComparison.OrdinalIgnoreCase) ||
                        line.Contains("Handler", StringComparison.OrdinalIgnoreCase) ||
                        line.Contains("MQ", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"    {line.Trim()}");
                    }
                }

                // 反编译 BppStartApis
                Console.WriteLine("\n  >>> 反编译 SanyD365.D365ExtensionApi.Apis.BppStartApis...");
                var disPsi = new System.Diagnostics.ProcessStartInfo("monodis", $"--output=/tmp/BppStartApis.il \"{path}\"")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };
                var disProc = System.Diagnostics.Process.Start(disPsi);
                disProc!.WaitForExit();

                var ilPath = "/tmp/BppStartApis.il";
                if (File.Exists(ilPath))
                {
                    var ilText = File.ReadAllText(ilPath);
                    // 找到 BppStartApis 类的 IL 代码块
                    int classIdx = ilText.IndexOf(".class SanyD365.D365ExtensionApi.Apis.BppStartApis", StringComparison.Ordinal);
                    if (classIdx >= 0)
                    {
                        int nextClassIdx = ilText.IndexOf("\n.class ", classIdx + 1);
                        string classIl = nextClassIdx > 0
                            ? ilText.Substring(classIdx, nextClassIdx - classIdx)
                            : ilText.Substring(classIdx);
                        // 保存到文件
                        File.WriteAllText("/tmp/BppStartApis_class.il", classIl);
                        Console.WriteLine("  ✅ BppStartApis IL已保存: /tmp/BppStartApis_class.il");

                        // 打印包含 "mcs_credit_record" 或 "Queue" 或 "Message" 的片段
                        var lines = classIl.Split('\n');
                        Console.WriteLine("  关键字符串/调用片段:");
                        for (int i = 0; i < lines.Length; i++)
                        {
                            var l = lines[i];
                            if (l.Contains("mcs_credit_record") ||
                                l.Contains("Queue", StringComparison.OrdinalIgnoreCase) ||
                                l.Contains("Message", StringComparison.OrdinalIgnoreCase) ||
                                l.Contains("Send", StringComparison.OrdinalIgnoreCase) ||
                                l.Contains("Topic", StringComparison.OrdinalIgnoreCase) ||
                                l.Contains("BPP", StringComparison.OrdinalIgnoreCase))
                            {
                                int start = Math.Max(0, i - 2);
                                int end = Math.Min(lines.Length - 1, i + 2);
                                for (int j = start; j <= end; j++)
                                {
                                    Console.WriteLine($"    {lines[j].Trim()}");
                                }
                                Console.WriteLine();
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("  ⚠️ 未在IL中找到 BppStartApis 类");
                    }
                }
                else
                {
                    Console.WriteLine($"  ❌ monodis 反编译失败: {disProc.StandardError.ReadToEnd()}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  失败: {ex.Message}");
            }
        }
    }
}
