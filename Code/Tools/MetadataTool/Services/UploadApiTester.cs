using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using System.Text;
using Newtonsoft.Json.Linq;

namespace D365MetadataTool;

/// <summary>
/// 测试 DEV1 中与上传相关的 Custom API，评估 Plugin 复用可行性
/// </summary>
public class UploadApiTester
{
    private readonly ServiceClient _service;

    public UploadApiTester(ServiceClient service)
    {
        _service = service;
    }

    /// <summary>
    /// 测试指定文件通过不同 Custom API 上传
    /// </summary>
    public void TestUploadApi(string filePath, string accountId, string apiType = "all")
    {
        Console.WriteLine($"\n=== 测试上传 Custom API ===");
        Console.WriteLine($"环境: {_service.ConnectedOrgUriActual}");
        Console.WriteLine($"文件: {filePath}");
        Console.WriteLine($"客户: {accountId}");
        Console.WriteLine($"测试类型: {apiType}");
        Console.WriteLine();

        if (!File.Exists(filePath))
        {
            Console.WriteLine($"❌ 文件不存在: {filePath}");
            return;
        }

        byte[] fileBytes = File.ReadAllBytes(filePath);
        string fileName = Path.GetFileName(filePath);
        string base64Content = Convert.ToBase64String(fileBytes);
        string contentType = GetContentType(filePath);

        Console.WriteLine($"文件大小: {fileBytes.Length} bytes ({fileBytes.Length / 1024.0:F2} KB)");
        Console.WriteLine($"Base64 长度: {base64Content.Length}");
        Console.WriteLine();

        QueryUploadFileTypeMapping();

        // 先创建一个 mcs_customer_file 记录作为上传目标
        Console.WriteLine("1. 创建测试 mcs_customer_file 记录...");
        var fileRecord = new Entity("mcs_customer_file");
        fileRecord["mcs_accountid"] = new EntityReference("account", Guid.Parse(accountId));
        fileRecord["mcs_filename"] = fileName;
        fileRecord["mcs_filetype"] = new OptionSetValue(2); // 客户资信报告
        fileRecord["mcs_filedate"] = DateTime.UtcNow;
        var fileRecordId = _service.Create(fileRecord);
        Console.WriteLine($"✅ 创建记录成功: {fileRecordId}");
        Console.WriteLine();

        try
        {
            if (apiType == "all" || apiType == "mcp")
            {
                TestMcpUploadFile(fileRecordId, fileName, contentType, base64Content);
            }

            if (apiType == "all" || apiType == "mcs")
            {
                TestMcsUploadFlow(accountId, fileRecordId, fileName, fileBytes);
                TestMcsUploadFlowOnCreditRecord(accountId, fileName, fileBytes);
            }
        }
        finally
        {
            Console.WriteLine();
            Console.WriteLine($"清理测试记录: {fileRecordId}");
            try
            {
                _service.Delete("mcs_customer_file", fileRecordId);
                Console.WriteLine("✅ 已清理");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 清理失败: {ex.Message}");
            }
        }
    }

    private void TestMcpUploadFile(Guid fileRecordId, string fileName, string contentType, string base64Content)
    {
        Console.WriteLine("2. 测试 McpUploadFile...");
        Console.WriteLine("   参数:");
        Console.WriteLine($"     tablename: mcs_customer_file");
        Console.WriteLine($"     recordId: {fileRecordId}");
        Console.WriteLine($"     fileAttributeName: mcs_filebyte");
        Console.WriteLine($"     fileName: {fileName}");
        Console.WriteLine($"     contentType: {contentType}");

        try
        {
            var request = new OrganizationRequest("McpUploadFile");
            request["tablename"] = "mcs_customer_file";
            request["recordId"] = fileRecordId.ToString();
            request["fileAttributeName"] = "mcs_filebyte";
            request["fileName"] = fileName;
            request["contentType"] = contentType;
            request["fileContent"] = base64Content;

            var response = _service.Execute(request);
            Console.WriteLine("✅ McpUploadFile 调用成功");
            DumpResponse(response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ McpUploadFile 调用失败: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"   Inner: {ex.InnerException.Message}");
            }
        }
        Console.WriteLine();
    }

    private void TestMcsUploadFlowOnCreditRecord(string accountId, string fileName, byte[] fileBytes)
    {
        Console.WriteLine("4. 测试 mcs_credit_record 作为附件所有者...");

        // 查询 account 下最新的 credit record
        Guid? creditRecordId = null;
        try
        {
            var query = new Microsoft.Xrm.Sdk.Query.QueryExpression("mcs_credit_record")
            {
                ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("mcs_credit_recordid", "mcs_scoreid"),
                Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression
                {
                    Conditions =
                    {
                        new Microsoft.Xrm.Sdk.Query.ConditionExpression("mcs_accountid", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, Guid.Parse(accountId))
                    }
                },
                Orders = { new Microsoft.Xrm.Sdk.Query.OrderExpression("createdon", Microsoft.Xrm.Sdk.Query.OrderType.Descending) },
                TopCount = 1
            };
            var result = _service.RetrieveMultiple(query);
            if (result.Entities.Count > 0)
            {
                creditRecordId = result.Entities[0].GetAttributeValue<Guid>("mcs_credit_recordid");
                var scoreId = result.Entities[0].GetAttributeValue<string>("mcs_scoreid");
                Console.WriteLine($"   找到 credit record: {creditRecordId} ({scoreId})");
            }
            else
            {
                Console.WriteLine("⚠️ 未找到关联的 credit record");
                return;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 查询 credit record 失败: {ex.Message}");
            return;
        }

        RunMcsUploadFlow("mcs_credit_record", creditRecordId.Value.ToString(), fileName, fileBytes, "002");
    }

    private void TestMcsUploadFlow(string accountId, Guid fileRecordId, string fileName, byte[] fileBytes)
    {
        Console.WriteLine("3. 测试 mcs_InitUploadFile / mcs_CommitUploadFile 流程 (account)...");
        // 用 000 类型避免与现有 002 冲突
        RunMcsUploadFlow("account", accountId, fileName, fileBytes, "000");
    }

    private void RunMcsUploadFlow(string entityName, string entityId, string fileName, byte[] fileBytes, string type)
    {
        Console.WriteLine($"   EntityName: {entityName}, EntityID: {entityId}, Type: {type}");

        // 1. Init
        string? uploadId = null;
        string? initResult = null;
        try
        {
            var initRequest = new OrganizationRequest("mcs_InitUploadFile");
            initRequest["EntityName"] = entityName;
            initRequest["EntityID"] = entityId;
            initRequest["FileName"] = fileName;
            initRequest["Type"] = type;

            var initResponse = _service.Execute(initRequest);
            Console.WriteLine("✅ mcs_InitUploadFile 调用成功");
            DumpResponse(initResponse);

            if (initResponse.Results.ContainsKey("Result"))
            {
                initResult = initResponse.Results["Result"]?.ToString();
                uploadId = ParseUploadIdFromResult(initResult);
                Console.WriteLine($"   解析到上传 ID: {uploadId}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ mcs_InitUploadFile 调用失败: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"   Inner: {ex.InnerException.Message}");
            }
            return;
        }

        if (string.IsNullOrEmpty(uploadId))
        {
            Console.WriteLine("⚠️ 未能获取上传 ID，跳过后续步骤");
            return;
        }

        // 2. Upload content to the FileUrl returned by Init
        string? uploadUrl = initResult?.Contains("FileUrl") == true ? ExtractFileUrl(initResult) : null;
        if (!string.IsNullOrEmpty(uploadUrl) && (uploadUrl.StartsWith("http://") || uploadUrl.StartsWith("https://")))
        {
            try
            {
                Console.WriteLine("   正在通过 HTTP 上传文件内容到 FileUrl...");
                Console.WriteLine($"   URL: {uploadUrl}");
                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    var content = new System.Net.Http.ByteArrayContent(fileBytes);
                    content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
                    content.Headers.Add("x-ms-blob-type", "AppendBlob");
                    // URL 通常已包含 comp=appendblock；若未包含再追加
                    var appendUrl = uploadUrl.Contains("comp=appendblock", StringComparison.OrdinalIgnoreCase)
                        ? uploadUrl
                        : (uploadUrl.Contains("?") ? $"{uploadUrl}&comp=appendblock" : $"{uploadUrl}?comp=appendblock");
                    var response = httpClient.PutAsync(appendUrl, content).Result;
                    var responseBody = response.Content.ReadAsStringAsync().Result;
                    Console.WriteLine($"   HTTP 状态: {response.StatusCode}");
                    if (!string.IsNullOrEmpty(responseBody))
                    {
                        Console.WriteLine($"   响应: {responseBody}");
                    }
                    response.EnsureSuccessStatusCode();
                    Console.WriteLine("✅ HTTP 上传成功");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ HTTP 上传失败: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"   Inner: {ex.InnerException.Message}");
                }
            }
        }
        else
        {
            Console.WriteLine("⚠️ 未能从 Init 结果中提取 FileUrl");
        }

        // 3. Commit
        try
        {
            var commitRequest = new OrganizationRequest("mcs_CommitUploadFile");
            commitRequest["EntityName"] = entityName;
            commitRequest["EntityID"] = entityId;
            commitRequest["ID"] = uploadId;

            var commitResponse = _service.Execute(commitRequest);
            Console.WriteLine("✅ mcs_CommitUploadFile 调用成功");
            DumpResponse(commitResponse);

            // 查询是否自动创建了 mcs_customer_file 记录
            QueryCreatedCustomerFile(entityName, entityId, uploadId);

            // 测试 mcs_GenerateUploadFileUrl 能否生成下载 URL
            TestGenerateUploadFileUrl(entityName, entityId, uploadId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ mcs_CommitUploadFile 调用失败: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"   Inner: {ex.InnerException.Message}");
            }
        }

        Console.WriteLine();
    }

    private void TestGenerateUploadFileUrl(string entityName, string entityId, string uploadId)
    {
        Console.WriteLine("   测试 mcs_GenerateUploadFileUrl 生成下载 URL...");
        try
        {
            var request = new OrganizationRequest("mcs_GenerateUploadFileUrl");
            request["EntityName"] = entityName;
            request["EntityID"] = entityId;
            request["ID"] = uploadId;

            var response = _service.Execute(request);
            Console.WriteLine("✅ mcs_GenerateUploadFileUrl 调用成功");
            DumpResponse(response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ mcs_GenerateUploadFileUrl 调用失败: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"   Inner: {ex.InnerException.Message}");
            }
        }
    }

    private void QueryCreatedCustomerFile(string entityName, string entityId, string uploadId)
    {
        Console.WriteLine("   查询上传后创建的 mcs_customer_file 记录...");
        try
        {
            var query = new Microsoft.Xrm.Sdk.Query.QueryExpression("mcs_customer_file")
            {
                ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet(
                    "mcs_customer_fileid", "mcs_filename", "mcs_filetype", "mcs_filedate",
                    "mcs_accountid", "mcs_credit_recordid", "mcs_api_fileid", "mcs_api_status"),
                Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression
                {
                    Conditions =
                    {
                        new Microsoft.Xrm.Sdk.Query.ConditionExpression("mcs_api_fileid", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, uploadId)
                    }
                }
            };
            var result = _service.RetrieveMultiple(query);
            if (result.Entities.Count == 0)
            {
                Console.WriteLine("   ⚠️ 未找到 mcs_customer_file 记录（API 可能未自动创建）");
                return;
            }
            foreach (var e in result.Entities)
            {
                Console.WriteLine($"   ✅ 找到记录: {e.GetAttributeValue<Guid>("mcs_customer_fileid")}");
                Console.WriteLine($"      filename: {e.GetAttributeValue<string>("mcs_filename")}");
                Console.WriteLine($"      filetype: {e.GetAttributeValue<OptionSetValue>("mcs_filetype")?.Value}");
                Console.WriteLine($"      filedate: {e.GetAttributeValue<DateTime>("mcs_filedate")}");
                Console.WriteLine($"      accountid: {e.GetAttributeValue<EntityReference>("mcs_accountid")?.Id}");
                Console.WriteLine($"      credit_recordid: {e.GetAttributeValue<EntityReference>("mcs_credit_recordid")?.Id}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ 查询失败: {ex.Message}");
        }
    }

    private void QueryUploadFileTypeMapping()
    {
        Console.WriteLine("查询 UploadFileTypeMapping 配置...");
        try
        {
            var query = new Microsoft.Xrm.Sdk.Query.QueryExpression("ms_systemconfiguration")
            {
                ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("ms_name", "ms_content"),
                Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression
                {
                    Conditions =
                    {
                        new Microsoft.Xrm.Sdk.Query.ConditionExpression("ms_name", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, "UploadFileTypeMapping")
                    }
                }
            };
            var result = _service.RetrieveMultiple(query);
            if (result.Entities.Count == 0)
            {
                Console.WriteLine("⚠️ 未找到 UploadFileTypeMapping 配置");
                return;
            }
            foreach (var e in result.Entities)
            {
                var name = e.GetAttributeValue<string>("ms_name");
                var value = e.GetAttributeValue<string>("ms_content");
                Console.WriteLine($"  {name}: {value}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 查询失败: {ex.Message}");
        }
        Console.WriteLine();
    }

    private static string? ParseUploadIdFromResult(string? result)
    {
        if (string.IsNullOrEmpty(result)) return null;

        // 尝试直接是 GUID
        if (Guid.TryParse(result, out _)) return result;

        // 尝试 JSON 解析
        try
        {
            var json = JObject.Parse(result);
            // 常见字段名
            foreach (var prop in new[] { "id", "ID", "Id", "fileId", "UploadId", "uploadId" })
            {
                if (json[prop] != null)
                {
                    return json[prop]!.ToString();
                }
            }
        }
        catch
        {
            // 不是 JSON
        }

        return result;
    }

    private static string? ExtractFileUrl(string? result)
    {
        if (string.IsNullOrEmpty(result)) return null;

        try
        {
            var json = JObject.Parse(result);
            if (json["FileUrl"] != null)
            {
                return json["FileUrl"]!.ToString();
            }
        }
        catch
        {
            // 不是 JSON
        }

        return null;
    }

    private static void DumpResponse(OrganizationResponse response)
    {
        foreach (var kvp in response.Results)
        {
            var value = kvp.Value?.ToString() ?? "(null)";
            if (value.Length > 500)
            {
                value = value.Substring(0, 500) + "...";
            }
            Console.WriteLine($"   {kvp.Key}: {value}");
        }
    }

    private static string GetContentType(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => "application/pdf",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".txt" => "text/plain",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            _ => "application/octet-stream"
        };
    }
}
