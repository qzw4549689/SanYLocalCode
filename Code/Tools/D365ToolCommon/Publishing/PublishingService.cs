using Microsoft.Crm.Sdk.Messages;
using Microsoft.PowerPlatform.Dataverse.Client;

namespace D365ToolCommon.Publishing
{
    /// <summary>
    /// 发布实体等元数据变更的通用服务。
    /// WebResource 发布请统一使用 <see cref="WebResource.WebResourceService.PublishWebResources"/>。
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
        /// <param name="entityNames">实体逻辑名列表</param>
        /// <param name="maxRetries">最大重试次数</param>
        public void PublishEntities(int maxRetries, params string[] entityNames)
        {
            if (entityNames.Length == 0) return;

            var entitiesXml = string.Join("", entityNames.Select(n => $"<entity>{n}</entity>"));
            var parameterXml = $"<importexportxml><entities>{entitiesXml}</entities><nodes/><securityroles/><settings/><workflows/></importexportxml>";

            var request = new PublishXmlRequest { ParameterXml = parameterXml };

            for (int i = 1; i <= maxRetries; i++)
            {
                try
                {
                    _service.Execute(request);
                    return;
                }
                catch (Exception ex) when (i < maxRetries)
                {
                    Console.WriteLine($"  ⚠️ 第 {i} 次发布实体失败: {ex.Message}，2秒后重试...");
                    Thread.Sleep(2000);
                }
            }
        }

        /// <summary>
        /// 发布指定实体（默认重试 3 次）。
        /// </summary>
        public void PublishEntities(params string[] entityNames)
        {
            PublishEntities(3, entityNames);
        }

        /// <summary>
        /// 发布所有元数据变更。
        /// ⚠️ 警告：AI 禁止调用此方法。全局 PublishAll 会阻塞整个 D365 环境，必须由用户手动执行。
        /// 如需发布，请使用 PublishEntities 等指定范围的发布方法。
        /// </summary>
        public void PublishAll()
        {
            var request = new PublishAllXmlRequest();
            _service.Execute(request);
        }
    }
}
