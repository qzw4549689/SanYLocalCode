using Microsoft.Crm.Sdk.Messages;
using Microsoft.PowerPlatform.Dataverse.Client;

namespace D365ToolCommon.Publishing
{
    /// <summary>
    /// 发布实体、WebResource 等元数据变更的通用服务。
    /// </summary>
    public class PublishingService
    {
        private readonly ServiceClient _service;

        public PublishingService(ServiceClient service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        /// <summary>
        /// 发布指定实体。
        /// </summary>
        public void PublishEntities(params string[] entityNames)
        {
            if (entityNames.Length == 0) return;

            var entitiesXml = string.Join("", entityNames.Select(n => $"<entity>{n}</entity>"));
            var parameterXml = $"<importexportxml><entities>{entitiesXml}</entities><nodes/><securityroles/><settings/><workflows/></importexportxml>";

            var request = new PublishXmlRequest { ParameterXml = parameterXml };
            _service.Execute(request);
        }

        /// <summary>
        /// 发布指定 WebResource。
        /// </summary>
        public void PublishWebResources(params string[] webResourceNames)
        {
            if (webResourceNames.Length == 0) return;

            var resourcesXml = string.Join("", webResourceNames.Select(n => $"<webresource>{n}</webresource>"));
            var parameterXml = $"<importexportxml><webresources>{resourcesXml}</webresources><nodes/><securityroles/><settings/><workflows/></importexportxml>";

            var request = new PublishXmlRequest { ParameterXml = parameterXml };
            _service.Execute(request);
        }

        /// <summary>
        /// 发布所有元数据变更。
        /// </summary>
        public void PublishAll()
        {
            var request = new PublishAllXmlRequest();
            _service.Execute(request);
        }
    }
}
