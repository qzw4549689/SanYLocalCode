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
        /// </summary>
        public void PublishWebResources(params string[] names)
        {
            if (names.Length == 0) return;

            var webResourcesXml = string.Join("", names.Select(n => $"<webresource>{n}</webresource>"));
            var parameterXml = $"<importexportxml><webresources>{webResourcesXml}</webresources><nodes/><securityroles/><settings/><workflows/></importexportxml>";

            var request = new PublishXmlRequest { ParameterXml = parameterXml };
            _service.Execute(request);
        }

        /// <summary>
        /// 发布指定的实体。
        /// </summary>
        public void PublishEntities(params string[] entityNames)
        {
            if (entityNames.Length == 0) return;

            var entitiesXml = string.Join("", entityNames.Select(n => $"<entity>{n}</entity>"));
            var parameterXml = $"<importexportxml><entities>{entitiesXml}</entities><nodes/><securityroles/><settings/><workflows/></importexportxml>";

            var request = new PublishXmlRequest { ParameterXml = parameterXml };
            _service.Execute(request);
        }
    }
}
