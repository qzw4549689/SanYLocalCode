using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using D365ToolCommon.Metadata;
using D365ToolCommon.Plugin;
using D365ToolCommon.WebResource;
using D365ToolCommon.Publishing;

namespace D365MetadataTool
{
    /// <summary>
    /// D365ToolCommon 共享库隔离测试。
    /// 使用独立测试实体 mcs_test_common，不跟项目业务实体/代码关联，测试完成后删除。
    /// </summary>
    public class TestCommonService
    {
        private readonly ServiceClient _service;
        private const string TestEntityName = "mcs_test_common";
        private const string TestEntityDisplayName = "D365ToolCommon Test Entity";
        private const string TestWebResourceName = "mcs_test_common.js";

        public TestCommonService(ServiceClient service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public void Run()
        {
            Console.WriteLine("\n=== D365ToolCommon 共享库隔离测试 ===");
            Console.WriteLine($"测试实体: {TestEntityName}");
            Console.WriteLine("注意：测试完成后会自动清理。\n");

            try
            {
                Cleanup();

                TestEntityAndFields();
                TestWebResource();
                TestPluginQuery();

                Console.WriteLine("\n✅ 全部测试通过，开始清理...");
            }
            finally
            {
                Cleanup();
            }

            Console.WriteLine("✅ 测试实体和数据已清理。");
        }

        /// <summary>
        /// 仅清理测试实体和 WebResource，不创建新数据。
        /// </summary>
        public void CleanupOnly()
        {
            Console.WriteLine("\n=== 清理 D365ToolCommon 测试实体 ===");
            Console.WriteLine($"目标: {TestEntityName}\n");
            Cleanup();
            Console.WriteLine("✅ 清理完成。");
        }

        private void TestEntityAndFields()
        {
            Console.WriteLine(">>> 1. 创建测试实体...");
            var entityId = CreateTestEntity();
            Console.WriteLine($"  ✅ 实体创建成功: {entityId}");

            Console.WriteLine(">>> 2. 使用 MetadataFieldService 创建测试字段...");
            var fieldService = new MetadataFieldService(_service);
            fieldService.CreateStringFieldIfNotExists(TestEntityName, "mcs_test_string", "测试字符串", "D365ToolCommon 测试用字符串字段", 100);
            fieldService.CreateIntegerFieldIfNotExists(TestEntityName, "mcs_test_integer", "测试整数", "D365ToolCommon 测试用整数字段", 0, 1000);
            fieldService.CreateBooleanFieldIfNotExists(TestEntityName, "mcs_test_boolean", "测试布尔", "D365ToolCommon 测试用布尔字段", false);

            Console.WriteLine(">>> 3. 验证字段存在性...");
            var (exists, missing) = fieldService.CheckFieldsExist(TestEntityName, "mcs_test_string", "mcs_test_integer", "mcs_test_boolean", "mcs_not_exists");
            Console.WriteLine($"  ✅ 存在字段: {string.Join(", ", exists)}");
            Console.WriteLine($"  ✅ 缺失字段: {string.Join(", ", missing)}");

            Console.WriteLine(">>> 4. 发布实体...");
            new PublishingService(_service).PublishEntities(TestEntityName);
            Console.WriteLine($"  ✅ 实体发布成功");
        }

        private void TestWebResource()
        {
            Console.WriteLine(">>> 5. 使用 WebResourceService 创建测试 WebResource...");
            var wrService = new WebResourceService(_service);
            var content = "// D365ToolCommon test web resource\nconsole.log('test');";
            var wrId = wrService.Create(TestWebResourceName, "D365ToolCommon Test JS", 3, content);
            Console.WriteLine($"  ✅ WebResource 创建成功: {wrId}");

            Console.WriteLine(">>> 6. 更新 WebResource 内容...");
            var newContent = "// D365ToolCommon test web resource updated\nconsole.log('updated');";
            wrService.UpdateContent(TestWebResourceName, newContent);
            Console.WriteLine($"  ✅ WebResource 更新成功");

            // 注：WebResource 发布操作在 DEV 繁忙时容易触发全局锁，此处不单独验证。
            // PublishingService 已通过第 4 步「发布实体」验证。
        }

        private void TestPluginQuery()
        {
            Console.WriteLine(">>> 8. 使用 PluginQueryService 查询 Plugin Assembly（只读）...");
            var queryService = new PluginQueryService(_service);
            var assemblies = queryService.QueryAssemblies("SanyD365").Take(5).ToList();
            Console.WriteLine($"  ✅ 查询到 {assemblies.Count} 个 Assembly");
            foreach (var asm in assemblies)
            {
                Console.WriteLine($"     - {asm.GetAttributeValue<string>("name")}");
            }
        }

        private Guid CreateTestEntity()
        {
            var request = new CreateEntityRequest
            {
                Entity = new EntityMetadata
                {
                    SchemaName = TestEntityName,
                    LogicalName = TestEntityName,
                    DisplayName = LabelHelper.Create(TestEntityDisplayName),
                    DisplayCollectionName = LabelHelper.Create(TestEntityDisplayName),
                    Description = LabelHelper.Create(TestEntityDisplayName),
                    OwnershipType = OwnershipTypes.UserOwned,
                    IsActivity = false,
                },
                PrimaryAttribute = new StringAttributeMetadata
                {
                    SchemaName = "mcs_test_name",
                    LogicalName = "mcs_test_name",
                    RequiredLevel = new AttributeRequiredLevelManagedProperty(AttributeRequiredLevel.None),
                    MaxLength = 100,
                    FormatName = StringFormatName.Text,
                    DisplayName = LabelHelper.Create("测试名称"),
                    Description = LabelHelper.Create("测试名称")
                }
            };

            return ExecuteWithRetry(() =>
            {
                var response = (CreateEntityResponse)_service.Execute(request);
                return response.EntityId;
            }, "创建实体");
        }

        private void Cleanup()
        {
            Console.WriteLine(">>> 清理测试数据...");

            // 1. 删除 WebResource
            ExecuteWithRetry(() =>
            {
                var wrService = new WebResourceService(_service);
                var wr = wrService.QueryByName(TestWebResourceName);
                if (wr != null)
                {
                    _service.Delete("webresource", wr.Id);
                    Console.WriteLine($"  ✅ 已删除 WebResource: {TestWebResourceName}");
                }
            }, "删除 WebResource", swallowError: true);

            // 2. 删除实体（会连带删除字段）
            ExecuteWithRetry(() =>
            {
                var fieldService = new MetadataFieldService(_service);
                if (fieldService.FieldExists(TestEntityName, "mcs_test_name"))
                {
                    var request = new DeleteEntityRequest { LogicalName = TestEntityName };
                    _service.Execute(request);
                    Console.WriteLine($"  ✅ 已删除实体: {TestEntityName}");
                }
            }, "删除实体", swallowError: true);
        }

        private T ExecuteWithRetry<T>(Func<T> action, string actionName, int maxRetries = 15, int delaySeconds = 10)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    return action();
                }
                catch (Exception ex) when (i < maxRetries - 1 && IsLockException(ex))
                {
                    Console.WriteLine($"  ⏳ {actionName} 被锁定，{delaySeconds} 秒后重试 ({i + 1}/{maxRetries})...");
                    Thread.Sleep(TimeSpan.FromSeconds(delaySeconds));
                }
            }
            throw new InvalidOperationException($"{actionName} 重试 {maxRetries} 次后仍然失败");
        }

        private void ExecuteWithRetry(Action action, string actionName, bool swallowError = false, int maxRetries = 15, int delaySeconds = 10)
        {
            try
            {
                ExecuteWithRetry(() => { action(); return true; }, actionName, maxRetries, delaySeconds);
            }
            catch (Exception ex)
            {
                if (swallowError)
                    Console.WriteLine($"  ⚠️ {actionName} 失败: {ex.Message}");
                else
                    throw;
            }
        }

        private static bool IsLockException(Exception ex)
        {
            var message = ex.Message ?? "";
            return message.Contains("CustomizationLockException", StringComparison.OrdinalIgnoreCase)
                || message.Contains("another", StringComparison.OrdinalIgnoreCase)
                || message.Contains("at this moment", StringComparison.OrdinalIgnoreCase);
        }
    }
}
