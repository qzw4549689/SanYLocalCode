using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace DeployTool
{
    /// <summary>
    /// 探测BPP框架如何获取domainaccount
    /// </summary>
    public class ProbeDomainAccount
    {
        public static void Probe(ServiceClient service)
        {
            Console.WriteLine(">>> 探测domainaccount相关配置...");
            Guid userId = new Guid("6ad92d8f-ab60-f111-a826-000d3aa333b3");

            try
            {
                // 1. 查询systemuser的所有字段（包括空值字段）
                Console.WriteLine("  1. systemuser所有字符串字段:");
                var user = service.Retrieve("systemuser", userId, new ColumnSet(true));
                foreach (var attr in user.Attributes)
                {
                    if (attr.Value is string strVal && !string.IsNullOrEmpty(strVal))
                    {
                        Console.WriteLine($"    {attr.Key} = {strVal}");
                    }
                }

                // 2. 查询是否有domainaccount字段
                Console.WriteLine("  2. 检查systemuser是否有domainaccount字段...");
                try
                {
                    var req = new RetrieveAttributeRequest
                    {
                        EntityLogicalName = "systemuser",
                        LogicalName = "domainaccount"
                    };
                    var resp = (RetrieveAttributeResponse)service.Execute(req);
                    Console.WriteLine($"    domainaccount字段存在: {resp.AttributeMetadata.LogicalName}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"    domainaccount字段不存在: {ex.Message}");
                }

                // 3. 查询所有含domain的自定义实体
                Console.WriteLine("  3. 查找含domain的自定义实体...");
                var entityQuery = new QueryExpression("entity")
                {
                    ColumnSet = new ColumnSet("logicalname", "objecttypecode"),
                    Criteria = new FilterExpression
                    {
                        Conditions = { new ConditionExpression("logicalname", ConditionOperator.Like, "%domain%") }
                    }
                };
                // 注意：entity是元数据实体，可能无法直接查询
                // 改用 RetrieveAllEntities
                try
                {
                    var allEntitiesReq = new RetrieveAllEntitiesRequest
                    {
                        EntityFilters = EntityFilters.Entity,
                        RetrieveAsIfPublished = false
                    };
                    var allEntitiesResp = (RetrieveAllEntitiesResponse)service.Execute(allEntitiesReq);
                    int count = 0;
                    foreach (var emd in allEntitiesResp.EntityMetadata)
                    {
                        if (emd.LogicalName.Contains("domain") || emd.LogicalName.Contains("bpp") || emd.LogicalName.Contains("accountmap"))
                        {
                            Console.WriteLine($"    实体: {emd.LogicalName}");
                            count++;
                        }
                    }
                    if (count == 0) Console.WriteLine("    未找到相关实体");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"    查询失败: {ex.Message}");
                }

                // 4. 尝试直接查看BPP Start Plugin的源码（反编译Assembly bytes）
                Console.WriteLine("  4. 尝试下载BPP Assembly源码...");
                try
                {
                    var assemblyQuery = new QueryExpression("pluginassembly")
                    {
                        ColumnSet = new ColumnSet("pluginassemblyid", "name", "content"),
                        Criteria = new FilterExpression
                        {
                            Conditions = { new ConditionExpression("name", ConditionOperator.Equal, "SanyD365.D365ExtensionApi") }
                        }
                    };
                    var assemblies = service.RetrieveMultiple(assemblyQuery);
                    if (assemblies.Entities.Count > 0)
                    {
                        var assembly = assemblies.Entities[0];
                        string content = assembly.GetAttributeValue<string>("content");
                        if (!string.IsNullOrEmpty(content))
                        {
                            var bytes = Convert.FromBase64String(content);
                            Console.WriteLine($"    Assembly大小: {bytes.Length} bytes");
                            // 保存到文件供分析
                            var path = "/tmp/bpp_assembly.dll";
                            System.IO.File.WriteAllBytes(path, bytes);
                            Console.WriteLine($"    已保存到: {path}");
                            
                            // 尝试用strings命令查找domainaccount相关字符串
                            try
                            {
                                var psi = new System.Diagnostics.ProcessStartInfo("strings", $"-n 10 {path}")
                                {
                                    RedirectStandardOutput = true,
                                    UseShellExecute = false
                                };
                                var proc = System.Diagnostics.Process.Start(psi);
                                string output = proc.StandardOutput.ReadToEnd();
                                proc.WaitForExit();
                                
                                var lines = output.Split('\n');
                                foreach (var line in lines)
                                {
                                    if (line.Contains("domainaccount") || line.Contains("DomainAccount") ||
                                        line.Contains("domainname") || line.Contains("GetDomain"))
                                    {
                                        Console.WriteLine($"    [源码线索] {line.Trim()}");
                                    }
                                }
                            }
                            catch (Exception ex2)
                            {
                                Console.WriteLine($"    strings命令失败: {ex2.Message}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"    下载失败: {ex.Message}");
                }

                // 5. 查询BPP配置表（如果有的话）
                Console.WriteLine("  5. 查找BPP用户映射配置...");
                var configQuery = new QueryExpression("environmentvariabledefinition")
                {
                    ColumnSet = new ColumnSet("schemaname", "defaultvalue"),
                    Criteria = new FilterExpression
                    {
                        Conditions = { new ConditionExpression("schemaname", ConditionOperator.Like, "%bpp%") }
                    }
                };
                try
                {
                    var configs = service.RetrieveMultiple(configQuery);
                    foreach (var c in configs.Entities)
                    {
                        Console.WriteLine($"    环境变量: {c.GetAttributeValue<string>("schemaname")} = {c.GetAttributeValue<string>("defaultvalue")}");
                    }
                }
                catch { }

                Console.WriteLine("  探测完成。");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  探测失败: {ex.Message}");
            }
        }
    }
}
