using Microsoft.Crm.Sdk.Messages;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System.Text;

namespace D365ToolCommon.WebResource
{
    /// <summary>
    /// WebResource 查询/更新/创建/发布通用服务。
    /// </summary>
    public class WebResourceService
    {
        private readonly ServiceClient _service;

        public WebResourceService(ServiceClient service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        /// <summary>
        /// 根据名称查询 WebResource。
        /// </summary>
        public Entity? QueryByName(string name)
        {
            var query = new QueryExpression("webresource")
            {
                ColumnSet = new ColumnSet("webresourceid", "name", "content", "webresourcetype"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("name", ConditionOperator.Equal, name) }
                }
            };
            return _service.RetrieveMultiple(query).Entities.FirstOrDefault();
        }

        /// <summary>
        /// 根据名称前缀查询 WebResource 列表（只读）。
        /// </summary>
        public List<Entity> ListByPrefix(string prefix)
        {
            var query = new QueryExpression("webresource")
            {
                ColumnSet = new ColumnSet("webresourceid", "name", "displayname", "webresourcetype", "ismanaged"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("name", ConditionOperator.BeginsWith, prefix) }
                },
                Orders = { new OrderExpression("name", OrderType.Ascending) }
            };
            return _service.RetrieveMultiple(query).Entities.ToList();
        }

        /// <summary>
        /// 更新 WebResource 内容（从字符串）。
        /// </summary>
        public void UpdateContent(string name, string content)
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            UpdateContent(name, bytes);
        }

        /// <summary>
        /// 更新 WebResource 内容（从字节数组）。
        /// </summary>
        public void UpdateContent(string name, byte[] content)
        {
            var webResource = QueryByName(name);
            if (webResource == null)
                throw new InvalidOperationException($"未找到 WebResource: {name}");

            webResource["content"] = Convert.ToBase64String(content);
            _service.Update(webResource);
        }

        /// <summary>
        /// 从文件更新 WebResource。
        /// </summary>
        public void UpdateFromFile(string name, string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"文件不存在: {filePath}");

            var content = File.ReadAllText(filePath);
            UpdateContent(name, content);
        }

        /// <summary>
        /// 批量从文件更新 WebResource。
        /// </summary>
        public void UpdateFromFiles(IEnumerable<(string Name, string FilePath)> resources)
        {
            foreach (var (name, path) in resources)
            {
                UpdateFromFile(name, path);
            }
        }

        /// <summary>
        /// 创建 WebResource。
        /// </summary>
        public Guid Create(string name, string displayName, int type, string content)
        {
            var webResource = new Entity("webresource");
            webResource["name"] = name;
            webResource["displayname"] = displayName;
            webResource["webresourcetype"] = new OptionSetValue(type);
            webResource["content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(content));
            return _service.Create(webResource);
        }

        /// <summary>
        /// 发布指定的 WebResource。
        /// 实现说明：D365 PublishXmlRequest 对 WebResource 使用 GUID 比使用名称更稳定，
        /// 因此本方法先按名称查询 webresourceid，再用 GUID 构造发布请求。
        /// </summary>
        /// <param name="names">WebResource 名称列表</param>
        /// <param name="maxRetries">最大重试次数</param>
        public void PublishWebResources(int maxRetries, params string[] names)
        {
            if (names.Length == 0) return;

            // 查询所有 WebResource ID
            var resourceIds = new List<string>();
            foreach (var name in names)
            {
                var webResource = QueryByName(name);
                if (webResource == null)
                    throw new InvalidOperationException($"未找到 WebResource: {name}");

                resourceIds.Add(webResource.Id.ToString("B").ToLowerInvariant());
            }

            var resourcesXml = string.Join("", resourceIds.Select(id => $"<webresource>{id}</webresource>"));
            var parameterXml = $"<importexportxml><webresources>{resourcesXml}</webresources><nodes/><securityroles/><settings/><workflows/></importexportxml>";

            var request = new PublishXmlRequest { ParameterXml = parameterXml };

            // 带重试执行
            for (int i = 1; i <= maxRetries; i++)
            {
                try
                {
                    _service.Execute(request);
                    return;
                }
                catch (Exception ex) when (i < maxRetries)
                {
                    Console.WriteLine($"  ⚠️ 第 {i} 次发布 WebResource 失败: {ex.Message}，2秒后重试...");
                    Thread.Sleep(2000);
                }
            }
        }

        /// <summary>
        /// 发布指定的 WebResource（默认重试 3 次）。
        /// </summary>
        public void PublishWebResources(params string[] names)
        {
            PublishWebResources(3, names);
        }
    }
}
