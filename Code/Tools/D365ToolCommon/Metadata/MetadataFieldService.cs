using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using D365ToolCommon.Connection;

namespace D365ToolCommon.Metadata
{
    /// <summary>
    /// 实体字段通用服务：检查、创建、删除。
    /// </summary>
    public class MetadataFieldService
    {
        private readonly ServiceClient _service;

        public MetadataFieldService(ServiceClient service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        /// <summary>
        /// 检查字段是否存在。
        /// </summary>
        public bool FieldExists(string entityName, string fieldLogicalName)
        {
            try
            {
                var request = new RetrieveAttributeRequest
                {
                    EntityLogicalName = entityName,
                    LogicalName = fieldLogicalName.ToLower()
                };
                _service.Execute(request);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 批量检查字段是否存在，返回存在/不存在的字段列表。
        /// </summary>
        public (List<string> exists, List<string> missing) CheckFieldsExist(string entityName, params string[] fieldLogicalNames)
        {
            var request = new RetrieveEntityRequest
            {
                EntityFilters = EntityFilters.Attributes,
                LogicalName = entityName
            };

            var response = (RetrieveEntityResponse)_service.Execute(request);
            var attributes = response.EntityMetadata.Attributes;

            var exists = new List<string>();
            var missing = new List<string>();

            foreach (var field in fieldLogicalNames)
            {
                var attr = attributes.FirstOrDefault(a => a.LogicalName == field.ToLower());
                if (attr != null)
                    exists.Add(field);
                else
                    missing.Add(field);
            }

            return (exists, missing);
        }

        /// <summary>
        /// 创建字符串字段（如果不存在）。
        /// </summary>
        public bool CreateStringFieldIfNotExists(string entityName, string schemaName, string displayName, string description, int maxLength = 100, bool required = false)
        {
            return CreateStringFieldIfNotExists(entityName, schemaName, displayName, description, maxLength, required, "", "");
        }

        /// <summary>
        /// 创建字符串字段（如果不存在），支持中英文显示名。
        /// </summary>
        public bool CreateStringFieldIfNotExists(string entityName, string schemaName, string displayName, string description, int maxLength, bool required, string displayNameZh, string displayNameEn, string format = "")
        {
            var logicalName = schemaName.ToLower();
            if (FieldExists(entityName, logicalName))
            {
                Console.WriteLine($"  ⬜ 字段已存在: {schemaName}");
                return false;
            }

            var displayLabel = string.IsNullOrWhiteSpace(displayNameZh) || string.IsNullOrWhiteSpace(displayNameEn)
                ? LabelHelper.Create(displayName)
                : LabelHelper.Create(displayNameZh, displayNameEn);

            var stringAttr = new StringAttributeMetadata
            {
                SchemaName = schemaName,
                LogicalName = logicalName,
                DisplayName = displayLabel,
                RequiredLevel = new AttributeRequiredLevelManagedProperty(required ? AttributeRequiredLevel.ApplicationRequired : AttributeRequiredLevel.None),
                Description = LabelHelper.Create(description),
                MaxLength = maxLength
            };
            // 可选格式（如 Url，审批链接类字段），不指定则默认 Text
            if (!string.IsNullOrWhiteSpace(format) && Enum.TryParse<StringFormat>(format, ignoreCase: true, out var stringFormat))
            {
                stringAttr.FormatName = new StringFormatName { Value = stringFormat.ToString() };
            }

            var request = new CreateAttributeRequest
            {
                EntityName = entityName,
                Attribute = stringAttr
            };

            _service.Execute(request);
            Console.WriteLine($"  ✅ 字段已创建: {schemaName} ({displayName})");
            return true;
        }

        /// <summary>
        /// 创建整数字段（如果不存在）。
        /// </summary>
        public bool CreateIntegerFieldIfNotExists(string entityName, string schemaName, string displayName, string description, int minValue = 0, int maxValue = int.MaxValue)
        {
            var logicalName = schemaName.ToLower();
            if (FieldExists(entityName, logicalName))
            {
                Console.WriteLine($"  ⬜ 字段已存在: {schemaName}");
                return false;
            }

            var request = new CreateAttributeRequest
            {
                EntityName = entityName,
                Attribute = new IntegerAttributeMetadata
                {
                    SchemaName = schemaName,
                    LogicalName = logicalName,
                    DisplayName = LabelHelper.Create(displayName),
                    RequiredLevel = new AttributeRequiredLevelManagedProperty(AttributeRequiredLevel.None),
                    Description = LabelHelper.Create(description),
                    Format = IntegerFormat.None,
                    MinValue = minValue,
                    MaxValue = maxValue
                }
            };

            _service.Execute(request);
            Console.WriteLine($"  ✅ 字段已创建: {schemaName} ({displayName})");
            return true;
        }

        /// <summary>
        /// 创建布尔字段（如果不存在）。
        /// </summary>
        public bool CreateBooleanFieldIfNotExists(string entityName, string schemaName, string displayName, string description, bool defaultValue = false)
        {
            var logicalName = schemaName.ToLower();
            if (FieldExists(entityName, logicalName))
            {
                Console.WriteLine($"  ⬜ 字段已存在: {schemaName}");
                return false;
            }

            var request = new CreateAttributeRequest
            {
                EntityName = entityName,
                Attribute = new BooleanAttributeMetadata
                {
                    SchemaName = schemaName,
                    LogicalName = logicalName,
                    DisplayName = LabelHelper.Create(displayName),
                    RequiredLevel = new AttributeRequiredLevelManagedProperty(AttributeRequiredLevel.None),
                    Description = LabelHelper.Create(description),
                    OptionSet = new BooleanOptionSetMetadata(
                        new OptionMetadata(LabelHelper.Create("是"), 1),
                        new OptionMetadata(LabelHelper.Create("否"), 0))
                }
            };

            _service.Execute(request);
            Console.WriteLine($"  ✅ 字段已创建: {schemaName} ({displayName})");
            return true;
        }

        /// <summary>
        /// 删除字段。
        /// </summary>
        public void DeleteField(string entityName, string fieldLogicalName)
        {
            var request = new DeleteAttributeRequest
            {
                EntityLogicalName = entityName,
                LogicalName = fieldLogicalName.ToLower()
            };
            _service.Execute(request);
        }

        /// <summary>
        /// 设置字段的多语言显示名称。
        /// 通过 Web API PUT 直接更新 AttributeMetadata.DisplayName 的 LocalizedLabels，
        /// 用于解决 SDK UpdateAttributeRequest 无法正确更新非基础语言本地标签的问题。
        /// </summary>
        public async Task SetFieldDisplayNameAsync(string entityName, string fieldLogicalName, string zhCN, string enUS, string? url = null)
        {
            url ??= D365ConnectionFactory.DefaultUrl.TrimEnd('/');
            Console.WriteLine($"设置字段多语言显示名称: {entityName}.{fieldLogicalName} -> zhCN={zhCN}, enUS={enUS}");

            var logicalName = fieldLogicalName.ToLower();

            // 查询现有字段类型，构造正确的 @odata.type
            var retrieveRequest = new RetrieveAttributeRequest
            {
                EntityLogicalName = entityName,
                LogicalName = logicalName,
                RetrieveAsIfPublished = true
            };
            var retrieveResponse = (RetrieveAttributeResponse)_service.Execute(retrieveRequest);
            var existing = retrieveResponse.AttributeMetadata;

            if (!existing.AttributeType.HasValue)
            {
                throw new InvalidOperationException($"无法获取字段 {entityName}.{logicalName} 的类型信息");
            }

            var typeName = existing.AttributeType.Value switch
            {
                AttributeTypeCode.Memo => "Microsoft.Dynamics.CRM.MemoAttributeMetadata",
                AttributeTypeCode.DateTime => "Microsoft.Dynamics.CRM.DateTimeAttributeMetadata",
                AttributeTypeCode.String => "Microsoft.Dynamics.CRM.StringAttributeMetadata",
                AttributeTypeCode.Picklist => "Microsoft.Dynamics.CRM.PicklistAttributeMetadata",
                AttributeTypeCode.Integer => "Microsoft.Dynamics.CRM.IntegerAttributeMetadata",
                AttributeTypeCode.Decimal => "Microsoft.Dynamics.CRM.DecimalAttributeMetadata",
                AttributeTypeCode.Money => "Microsoft.Dynamics.CRM.MoneyAttributeMetadata",
                AttributeTypeCode.Boolean => "Microsoft.Dynamics.CRM.BooleanAttributeMetadata",
                AttributeTypeCode.Lookup => "Microsoft.Dynamics.CRM.LookupAttributeMetadata",
                AttributeTypeCode.Uniqueidentifier => "Microsoft.Dynamics.CRM.AttributeMetadata",
                _ => "Microsoft.Dynamics.CRM.AttributeMetadata"
            };

            var labelsJson = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(zhCN))
            {
                labelsJson.Append($"{{\"Label\":\"{EscapeJson(zhCN)}\",\"LanguageCode\":2052}}");
            }
            if (!string.IsNullOrWhiteSpace(enUS))
            {
                if (labelsJson.Length > 0) labelsJson.Append(",");
                labelsJson.Append($"{{\"Label\":\"{EscapeJson(enUS)}\",\"LanguageCode\":1033}}");
            }

            if (labelsJson.Length == 0)
            {
                Console.WriteLine("  ⬜ 未提供任何标签，跳过");
                return;
            }

            var putUrl = $"{url}/api/data/v9.2/EntityDefinitions(LogicalName='{entityName}')/Attributes(LogicalName='{logicalName}')";
            var body = $"{{\"@odata.type\":\"{typeName}\",\"LogicalName\":\"{logicalName}\",\"DisplayName\":{{\"LocalizedLabels\":[{labelsJson}]}}}}";

            var token = await D365ConnectionFactory.GetAccessTokenAsync(url);
            if (string.IsNullOrEmpty(token))
            {
                throw new InvalidOperationException("无法获取 access token，无法通过 Web API 更新字段显示名称");
            }

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
            client.DefaultRequestHeaders.Add("OData-Version", "4.0");

            var content = new StringContent(body, Encoding.UTF8, "application/json");
            var response = await client.PutAsync(putUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("  ✓ 字段多语言显示名称设置成功");
            }
            else
            {
                Console.WriteLine($"  ⚠️ Web API 更新失败: {(int)response.StatusCode} {response.ReasonPhrase}");
                if (!string.IsNullOrWhiteSpace(responseBody))
                {
                    Console.WriteLine($"     {responseBody.Trim()}");
                }
                response.EnsureSuccessStatusCode();
            }
        }

        private static string EscapeJson(string text)
        {
            return text.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        /// <summary>
        /// 设置实体的多语言显示名称（中文 2052 + 英文 1033）。
        /// 通过 Web API PUT 直接更新 EntityMetadata.DisplayName / DisplayCollectionName 的 LocalizedLabels，
        /// 用于解决 SDK UpdateEntityRequest 在 Device Code Flow 下无法正确更新非基础语言本地标签的问题。
        /// </summary>
        public async Task SetEntityDisplayNameAsync(string entityLogicalName, string zhCN, string enUS, string? url = null)
        {
            url ??= D365ConnectionFactory.DefaultUrl.TrimEnd('/');
            Console.WriteLine($"设置实体多语言显示名称: {entityLogicalName} -> zhCN={zhCN}, enUS={enUS}");

            var logicalName = entityLogicalName.ToLower();
            var token = await D365ConnectionFactory.GetAccessTokenAsync(url);

            var payload = $@"{{
  ""@odata.type"": ""Microsoft.Dynamics.CRM.EntityMetadata"",
  ""LogicalName"": ""{EscapeJson(logicalName)}"",
  ""DisplayName"": {{
    ""LocalizedLabels"": [
      {{""Label"": ""{EscapeJson(zhCN)}"", ""LanguageCode"": 2052}},
      {{""Label"": ""{EscapeJson(enUS)}"", ""LanguageCode"": 1033}}
    ]
  }},
  ""DisplayCollectionName"": {{
    ""LocalizedLabels"": [
      {{""Label"": ""{EscapeJson(zhCN + "列表")}"", ""LanguageCode"": 2052}},
      {{""Label"": ""{EscapeJson(enUS + " List")}"", ""LanguageCode"": 1033}}
    ]
  }}
}}";

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
            client.DefaultRequestHeaders.Add("OData-Version", "4.0");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var requestUrl = $"{url}/api/data/v9.2/EntityDefinitions(LogicalName='{logicalName}')";
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var response = await client.PutAsync(requestUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("  ✓ 实体多语言显示名称设置成功");
            }
            else
            {
                Console.WriteLine($"  ⚠️ Web API 更新失败: {(int)response.StatusCode} {response.ReasonPhrase}");
                if (!string.IsNullOrWhiteSpace(responseBody))
                {
                    Console.WriteLine($"     {responseBody.Trim()}");
                }
                response.EnsureSuccessStatusCode();
            }
        }

        /// <summary>
        /// 更新字段的 RequiredLevel（业务必需/可选）。
        /// 优先尝试 SDK UpdateAttributeRequest；若不持久化，则通过 Web API PUT 兜底。
        /// 适用于 Device Code Flow 等 SDK 对 RequiredLevel 更新不生效的场景。
        /// </summary>
        public async Task UpdateRequiredLevelAsync(string entityName, string fieldLogicalName, bool required, string? url = null)
        {
            url ??= D365ConnectionFactory.DefaultUrl.TrimEnd('/');
            var logicalName = fieldLogicalName.ToLower();

            // 先查询现有字段类型
            var retrieveRequest = new RetrieveAttributeRequest
            {
                EntityLogicalName = entityName,
                LogicalName = logicalName,
                RetrieveAsIfPublished = true
            };
            var retrieveResponse = (RetrieveAttributeResponse)_service.Execute(retrieveRequest);
            var existing = retrieveResponse.AttributeMetadata;

            if (!existing.AttributeType.HasValue)
            {
                throw new InvalidOperationException($"无法获取字段 {entityName}.{logicalName} 的类型信息");
            }

            // 尝试 1：SDK UpdateAttributeRequest
            try
            {
                AttributeMetadata attribute = existing.AttributeType.Value switch
                {
                    AttributeTypeCode.Memo => new MemoAttributeMetadata { LogicalName = logicalName },
                    AttributeTypeCode.DateTime => new DateTimeAttributeMetadata { LogicalName = logicalName },
                    AttributeTypeCode.String => new StringAttributeMetadata { LogicalName = logicalName },
                    AttributeTypeCode.Picklist => new PicklistAttributeMetadata { LogicalName = logicalName },
                    AttributeTypeCode.Integer => new IntegerAttributeMetadata { LogicalName = logicalName },
                    AttributeTypeCode.Decimal => new DecimalAttributeMetadata { LogicalName = logicalName },
                    AttributeTypeCode.Money => new MoneyAttributeMetadata { LogicalName = logicalName },
                    AttributeTypeCode.Boolean => new BooleanAttributeMetadata { LogicalName = logicalName },
                    AttributeTypeCode.Lookup => new LookupAttributeMetadata { LogicalName = logicalName },
                    AttributeTypeCode.Uniqueidentifier => new AttributeMetadata { LogicalName = logicalName },
                    _ => new AttributeMetadata { LogicalName = logicalName }
                };

                attribute.RequiredLevel = new AttributeRequiredLevelManagedProperty(
                    required ? AttributeRequiredLevel.ApplicationRequired : AttributeRequiredLevel.None)
                {
                    CanBeChanged = true
                };

                var request = new UpdateAttributeRequest
                {
                    EntityName = entityName,
                    Attribute = attribute,
                    MergeLabels = false
                };
                _service.Execute(request);
                Console.WriteLine("  ✓ SDK 更新字段必填性成功");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠️ SDK 方式失败: {ex.Message}，尝试 Web API...");
            }

            // 尝试 2：Web API PUT（SDK UpdateAttributeRequest 在某些认证方式下会"成功"但不持久化，因此始终兜底执行）
            await UpdateRequiredLevelViaWebApiAsync(entityName, logicalName, required, existing.AttributeType.Value, url);
        }

        private async Task UpdateRequiredLevelViaWebApiAsync(string entityName, string fieldLogicalName, bool required, AttributeTypeCode attributeType, string url)
        {
            var typeName = attributeType switch
            {
                AttributeTypeCode.Memo => "Microsoft.Dynamics.CRM.MemoAttributeMetadata",
                AttributeTypeCode.DateTime => "Microsoft.Dynamics.CRM.DateTimeAttributeMetadata",
                AttributeTypeCode.String => "Microsoft.Dynamics.CRM.StringAttributeMetadata",
                AttributeTypeCode.Picklist => "Microsoft.Dynamics.CRM.PicklistAttributeMetadata",
                AttributeTypeCode.Integer => "Microsoft.Dynamics.CRM.IntegerAttributeMetadata",
                AttributeTypeCode.Decimal => "Microsoft.Dynamics.CRM.DecimalAttributeMetadata",
                AttributeTypeCode.Money => "Microsoft.Dynamics.CRM.MoneyAttributeMetadata",
                AttributeTypeCode.Boolean => "Microsoft.Dynamics.CRM.BooleanAttributeMetadata",
                AttributeTypeCode.Lookup => "Microsoft.Dynamics.CRM.LookupAttributeMetadata",
                AttributeTypeCode.Uniqueidentifier => "Microsoft.Dynamics.CRM.AttributeMetadata",
                _ => "Microsoft.Dynamics.CRM.AttributeMetadata"
            };

            var putUrl = $"{url}/api/data/v9.2/EntityDefinitions(LogicalName='{entityName}')/Attributes(LogicalName='{fieldLogicalName}')";
            var body = $"{{\"@odata.type\":\"{typeName}\",\"LogicalName\":\"{fieldLogicalName}\",\"RequiredLevel\":{{\"Value\":\"{(required ? "ApplicationRequired" : "None")}\",\"CanBeChanged\":true}}}}";

            var token = await D365ConnectionFactory.GetAccessTokenAsync(url);
            if (string.IsNullOrEmpty(token))
            {
                throw new InvalidOperationException("无法获取 access token，无法通过 Web API 更新字段必填性");
            }

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
            client.DefaultRequestHeaders.Add("OData-Version", "4.0");

            var content = new StringContent(body, Encoding.UTF8, "application/json");
            var response = await client.PutAsync(putUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("  ✓ Web API 更新字段必填性成功");
            }
            else
            {
                Console.WriteLine($"  ⚠️ Web API 更新失败: {(int)response.StatusCode} {response.ReasonPhrase}");
                if (!string.IsNullOrWhiteSpace(responseBody))
                {
                    Console.WriteLine($"     {responseBody.Trim()}");
                }
                response.EnsureSuccessStatusCode();
            }
        }

        /// <summary>
        /// 设置选项集字段的默认值。
        /// 优先尝试 SDK UpdateAttributeRequest；若不持久化，则通过 Web API PUT 兜底。
        /// </summary>
        public async Task SetPicklistDefaultValueAsync(string entityName, string fieldLogicalName, int defaultValue, string? url = null)
        {
            url ??= D365ConnectionFactory.DefaultUrl.TrimEnd('/');
            var logicalName = fieldLogicalName.ToLower();

            // 查询现有字段类型
            var retrieveRequest = new RetrieveAttributeRequest
            {
                EntityLogicalName = entityName,
                LogicalName = logicalName,
                RetrieveAsIfPublished = true
            };
            var retrieveResponse = (RetrieveAttributeResponse)_service.Execute(retrieveRequest);
            var existing = retrieveResponse.AttributeMetadata;

            if (existing.AttributeType != AttributeTypeCode.Picklist)
            {
                throw new InvalidOperationException($"字段 {entityName}.{logicalName} 不是选项集类型，当前类型为 {existing.AttributeType}");
            }

            // 尝试 1：SDK UpdateAttributeRequest
            try
            {
                var attribute = new PicklistAttributeMetadata
                {
                    LogicalName = logicalName,
                    DefaultFormValue = defaultValue
                };

                var request = new UpdateAttributeRequest
                {
                    EntityName = entityName,
                    Attribute = attribute,
                    MergeLabels = false
                };
                _service.Execute(request);
                Console.WriteLine("  ✓ SDK 设置字段默认值成功");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠️ SDK 方式失败: {ex.Message}，尝试 Web API...");
            }

            // 尝试 2：Web API PUT
            var putUrl = $"{url}/api/data/v9.2/EntityDefinitions(LogicalName='{entityName}')/Attributes(LogicalName='{logicalName}')";
            var body = $"{{\"@odata.type\":\"Microsoft.Dynamics.CRM.PicklistAttributeMetadata\",\"LogicalName\":\"{logicalName}\",\"DefaultFormValue\":{defaultValue}}}";

            var token = await D365ConnectionFactory.GetAccessTokenAsync(url);
            if (string.IsNullOrEmpty(token))
            {
                throw new InvalidOperationException("无法获取 access token，无法通过 Web API 设置字段默认值");
            }

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
            client.DefaultRequestHeaders.Add("OData-Version", "4.0");

            var content = new StringContent(body, Encoding.UTF8, "application/json");
            var response = await client.PutAsync(putUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("  ✓ Web API 设置字段默认值成功");
            }
            else
            {
                Console.WriteLine($"  ⚠️ Web API 设置失败: {(int)response.StatusCode} {response.ReasonPhrase}");
                if (!string.IsNullOrWhiteSpace(responseBody))
                {
                    Console.WriteLine($"     {responseBody.Trim()}");
                }
                response.EnsureSuccessStatusCode();
            }
        }

        /// <summary>
        /// 设置布尔字段的默认值（2026-07-25 新增，融资管理 can_initiated/can_project 默认「是」）。
        /// 优先 SDK UpdateAttributeRequest，失败时回退 Web API PUT。
        /// </summary>
        public async Task SetBooleanDefaultValueAsync(string entityName, string fieldLogicalName, bool defaultValue, string? url = null)
        {
            url ??= D365ConnectionFactory.DefaultUrl.TrimEnd('/');
            var logicalName = fieldLogicalName.ToLower();

            // 查询现有字段类型
            var retrieveRequest = new RetrieveAttributeRequest
            {
                EntityLogicalName = entityName,
                LogicalName = logicalName,
                RetrieveAsIfPublished = true
            };
            var retrieveResponse = (RetrieveAttributeResponse)_service.Execute(retrieveRequest);
            var existing = retrieveResponse.AttributeMetadata;

            if (existing.AttributeType != AttributeTypeCode.Boolean)
            {
                throw new InvalidOperationException($"字段 {entityName}.{logicalName} 不是布尔类型，当前类型为 {existing.AttributeType}");
            }

            var defaultValueJson = defaultValue ? "true" : "false";

            // 尝试 1：SDK UpdateAttributeRequest
            try
            {
                var attribute = new BooleanAttributeMetadata
                {
                    LogicalName = logicalName,
                    DefaultValue = defaultValue
                };

                var request = new UpdateAttributeRequest
                {
                    EntityName = entityName,
                    Attribute = attribute,
                    MergeLabels = false
                };
                _service.Execute(request);
                Console.WriteLine("  ✓ SDK 设置字段默认值成功");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠️ SDK 方式失败: {ex.Message}，尝试 Web API...");
            }

            // 尝试 2：Web API PUT
            var putUrl = $"{url}/api/data/v9.2/EntityDefinitions(LogicalName='{entityName}')/Attributes(LogicalName='{logicalName}')";
            var body = $"{{\"@odata.type\":\"Microsoft.Dynamics.CRM.BooleanAttributeMetadata\",\"LogicalName\":\"{logicalName}\",\"DefaultValue\":{defaultValueJson}}}";

            var token = await D365ConnectionFactory.GetAccessTokenAsync(url);
            if (string.IsNullOrEmpty(token))
            {
                throw new InvalidOperationException("无法获取 access token，无法通过 Web API 设置字段默认值");
            }

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
            client.DefaultRequestHeaders.Add("OData-Version", "4.0");

            var content = new StringContent(body, Encoding.UTF8, "application/json");
            var response = await client.PutAsync(putUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("  ✓ Web API 设置字段默认值成功");
            }
            else
            {
                Console.WriteLine($"  ⚠️ Web API 设置失败: {(int)response.StatusCode} {response.ReasonPhrase}");
                if (!string.IsNullOrWhiteSpace(responseBody))
                {
                    Console.WriteLine($"     {responseBody.Trim()}");
                }
                response.EnsureSuccessStatusCode();
            }
        }

        /// <summary>
        /// 更新字段的描述（Description）。
        /// </summary>
        public void UpdateDescription(string entityName, string fieldLogicalName, string description)
        {
            var logicalName = fieldLogicalName.ToLower();

            // 查询现有字段类型
            var retrieveRequest = new RetrieveAttributeRequest
            {
                EntityLogicalName = entityName,
                LogicalName = logicalName,
                RetrieveAsIfPublished = true
            };
            var retrieveResponse = (RetrieveAttributeResponse)_service.Execute(retrieveRequest);
            var existing = retrieveResponse.AttributeMetadata;

            if (!existing.AttributeType.HasValue)
            {
                throw new InvalidOperationException($"无法获取字段 {entityName}.{logicalName} 的类型信息");
            }

            AttributeMetadata attribute = existing.AttributeType.Value switch
            {
                AttributeTypeCode.Memo => new MemoAttributeMetadata { LogicalName = logicalName },
                AttributeTypeCode.DateTime => new DateTimeAttributeMetadata { LogicalName = logicalName },
                AttributeTypeCode.String => new StringAttributeMetadata { LogicalName = logicalName },
                AttributeTypeCode.Picklist => new PicklistAttributeMetadata { LogicalName = logicalName },
                AttributeTypeCode.Integer => new IntegerAttributeMetadata { LogicalName = logicalName },
                AttributeTypeCode.Decimal => new DecimalAttributeMetadata { LogicalName = logicalName },
                AttributeTypeCode.Money => new MoneyAttributeMetadata { LogicalName = logicalName },
                AttributeTypeCode.Boolean => new BooleanAttributeMetadata { LogicalName = logicalName },
                AttributeTypeCode.Lookup => new LookupAttributeMetadata { LogicalName = logicalName },
                AttributeTypeCode.Uniqueidentifier => new AttributeMetadata { LogicalName = logicalName },
                _ => new AttributeMetadata { LogicalName = logicalName }
            };

            attribute.Description = LabelHelper.Create(description);

            var request = new UpdateAttributeRequest
            {
                EntityName = entityName,
                Attribute = attribute,
                MergeLabels = false
            };
            _service.Execute(request);
            Console.WriteLine($"  ✓ 字段 {entityName}.{logicalName} 描述已更新");
        }

        /// <summary>
        /// 更新 Money 字段的最小值/最大值范围。
        /// 保留现有标签（MergeLabels=true），仅修改取值范围。
        /// </summary>
        public void UpdateMoneyRange(string entityName, string fieldLogicalName, decimal minValue, decimal maxValue)
        {
            var logicalName = fieldLogicalName.ToLower();

            var retrieveRequest = new RetrieveAttributeRequest
            {
                EntityLogicalName = entityName,
                LogicalName = logicalName,
                RetrieveAsIfPublished = true
            };
            var retrieveResponse = (RetrieveAttributeResponse)_service.Execute(retrieveRequest);

            if (!(retrieveResponse.AttributeMetadata is MoneyAttributeMetadata money))
            {
                throw new InvalidOperationException($"字段 {entityName}.{logicalName} 不是 Money 类型");
            }

            money.MinValue = (double)minValue;
            money.MaxValue = (double)maxValue;

            var request = new UpdateAttributeRequest
            {
                EntityName = entityName,
                Attribute = money,
                MergeLabels = true
            };
            _service.Execute(request);
            Console.WriteLine($"  ✓ 字段 {entityName}.{logicalName} 取值范围已更新为 [{minValue}, {maxValue}]");
        }

        /// <summary>
        /// 更新 Decimal 字段的最小值/最大值范围。
        /// 保留现有标签（MergeLabels=true），仅修改取值范围。
        /// </summary>
        public void UpdateDecimalRange(string entityName, string fieldLogicalName, decimal minValue, decimal maxValue)
        {
            var logicalName = fieldLogicalName.ToLower();

            var retrieveRequest = new RetrieveAttributeRequest
            {
                EntityLogicalName = entityName,
                LogicalName = logicalName,
                RetrieveAsIfPublished = true
            };
            var retrieveResponse = (RetrieveAttributeResponse)_service.Execute(retrieveRequest);

            if (!(retrieveResponse.AttributeMetadata is DecimalAttributeMetadata decimalAttr))
            {
                throw new InvalidOperationException($"字段 {entityName}.{logicalName} 不是 Decimal 类型");
            }

            decimalAttr.MinValue = minValue;
            decimalAttr.MaxValue = maxValue;

            var request = new UpdateAttributeRequest
            {
                EntityName = entityName,
                Attribute = decimalAttr,
                MergeLabels = true
            };
            _service.Execute(request);
            Console.WriteLine($"  ✓ 字段 {entityName}.{logicalName} 取值范围已更新为 [{minValue}, {maxValue}]");
        }

        /// <summary>
        /// 更新 String 字段的最大长度（如编码规则扩容）。
        /// 保留现有标签（MergeLabels=true），仅修改 MaxLength。
        /// </summary>
        public void UpdateStringMaxLength(string entityName, string fieldLogicalName, int maxLength)
        {
            var logicalName = fieldLogicalName.ToLower();

            var retrieveRequest = new RetrieveAttributeRequest
            {
                EntityLogicalName = entityName,
                LogicalName = logicalName,
                RetrieveAsIfPublished = true
            };
            var retrieveResponse = (RetrieveAttributeResponse)_service.Execute(retrieveRequest);

            if (!(retrieveResponse.AttributeMetadata is StringAttributeMetadata stringAttr))
            {
                throw new InvalidOperationException($"字段 {entityName}.{logicalName} 不是 String 类型");
            }

            stringAttr.MaxLength = maxLength;

            var request = new UpdateAttributeRequest
            {
                EntityName = entityName,
                Attribute = stringAttr,
                MergeLabels = true
            };
            _service.Execute(request);
            Console.WriteLine($"  ✓ 字段 {entityName}.{logicalName} 最大长度已更新为 {maxLength}");
        }

        /// <summary>
        /// 更新 String 字段的格式（如 Text 改 Url，使字段在表单渲染为可点击超链接）。
        /// 原地更新格式，字段与历史数据不受影响。保留现有标签（MergeLabels=true），仅修改 FormatName。
        /// </summary>
        public void UpdateStringFormat(string entityName, string fieldLogicalName, StringFormat format)
        {
            var logicalName = fieldLogicalName.ToLower();

            var retrieveRequest = new RetrieveAttributeRequest
            {
                EntityLogicalName = entityName,
                LogicalName = logicalName,
                RetrieveAsIfPublished = true
            };
            var retrieveResponse = (RetrieveAttributeResponse)_service.Execute(retrieveRequest);

            if (!(retrieveResponse.AttributeMetadata is StringAttributeMetadata stringAttr))
            {
                throw new InvalidOperationException($"字段 {entityName}.{logicalName} 不是 String 类型");
            }

            if (stringAttr.Format == format)
            {
                Console.WriteLine($"  - 字段 {entityName}.{logicalName} 格式已是 {format}，无需更新");
                return;
            }

            // 注意：必须设置 FormatName 才会序列化进 UpdateAttributeRequest；只设置 Format 属性不会生效（已实测）
            stringAttr.FormatName = new StringFormatName { Value = format.ToString() };

            var request = new UpdateAttributeRequest
            {
                EntityName = entityName,
                Attribute = stringAttr,
                MergeLabels = true
            };
            _service.Execute(request);
            Console.WriteLine($"  ✓ 字段 {entityName}.{logicalName} 格式已更新为 {format}");
        }

        /// <summary>
        /// 设置数值（Decimal）字段的默认值。
        /// 当前 SDK 版本 DecimalAttributeMetadata 不包含 DefaultValue 属性，直接通过 Web API PUT 设置。
        /// </summary>
        public async Task SetDecimalDefaultValueAsync(string entityName, string fieldLogicalName, decimal defaultValue, string? url = null)
        {
            url ??= D365ConnectionFactory.DefaultUrl.TrimEnd('/');
            var logicalName = fieldLogicalName.ToLower();

            // 查询现有字段类型
            var retrieveRequest = new RetrieveAttributeRequest
            {
                EntityLogicalName = entityName,
                LogicalName = logicalName,
                RetrieveAsIfPublished = true
            };
            var retrieveResponse = (RetrieveAttributeResponse)_service.Execute(retrieveRequest);
            var existing = retrieveResponse.AttributeMetadata;

            if (existing.AttributeType != AttributeTypeCode.Decimal)
            {
                throw new InvalidOperationException($"字段 {entityName}.{logicalName} 不是 Decimal 类型，当前类型为 {existing.AttributeType}");
            }

            // 通过 Web API PUT 设置 DefaultValue
            var putUrl = $"{url}/api/data/v9.2/EntityDefinitions(LogicalName='{entityName}')/Attributes(LogicalName='{logicalName}')";
            var body = $"{{\"@odata.type\":\"Microsoft.Dynamics.CRM.DecimalAttributeMetadata\",\"LogicalName\":\"{logicalName}\",\"DefaultValue\":{defaultValue}}}";

            var token = await D365ConnectionFactory.GetAccessTokenAsync(url);
            if (string.IsNullOrEmpty(token))
            {
                throw new InvalidOperationException("无法获取 access token，无法通过 Web API 设置 Decimal 默认值");
            }

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
            client.DefaultRequestHeaders.Add("OData-Version", "4.0");

            var content = new StringContent(body, Encoding.UTF8, "application/json");
            var response = await client.PutAsync(putUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("  ✓ Web API 设置 Decimal 默认值成功");
            }
            else
            {
                Console.WriteLine($"  ⚠️ Web API 设置失败: {(int)response.StatusCode} {response.ReasonPhrase}");
                if (!string.IsNullOrWhiteSpace(responseBody))
                {
                    Console.WriteLine($"     {responseBody.Trim()}");
                }
                response.EnsureSuccessStatusCode();
            }
        }

        /// <summary>
        /// 更新选项集字段的选项标签（保留其他语言标签，仅更新指定语言）。
        /// </summary>
        public void UpdateOptionSetLabels(string entityName, string fieldLogicalName, Dictionary<int, string> labels, int languageCode = 2052)
        {
            var request = new RetrieveAttributeRequest
            {
                EntityLogicalName = entityName,
                LogicalName = fieldLogicalName.ToLower()
            };
            var response = (RetrieveAttributeResponse)_service.Execute(request);

            if (!(response.AttributeMetadata is EnumAttributeMetadata enumAttr))
            {
                throw new InvalidOperationException($"字段 {fieldLogicalName} 不是选项集类型");
            }

            string optionSetName = enumAttr.OptionSet.Name;
            Console.WriteLine($"更新选项集 {optionSetName} 的标签...");

            foreach (var option in enumAttr.OptionSet.Options)
            {
                int value = option.Value ?? 0;
                if (!labels.ContainsKey(value))
                {
                    continue;
                }

                string newText = labels[value];
                var existingLabels = option.Label?.LocalizedLabels?.ToList() ?? new List<LocalizedLabel>();
                var labelDict = new Dictionary<int, string>();
                foreach (var lbl in existingLabels)
                {
                    if (lbl.LanguageCode != languageCode)
                    {
                        labelDict[lbl.LanguageCode] = lbl.Label;
                    }
                }
                labelDict[languageCode] = newText;

                var updateRequest = new UpdateOptionValueRequest
                {
                    EntityLogicalName = entityName,
                    AttributeLogicalName = fieldLogicalName.ToLower(),
                    Value = value,
                    Label = LabelHelper.Create(labelDict)
                };
                _service.Execute(updateRequest);
                Console.WriteLine($"  ✓ 选项 {value} 的 {languageCode} 标签更新为: {newText}");
            }
        }
    }
}
