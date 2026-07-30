using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using D365ToolCommon.Solution;

namespace D365ToolCommon.WebResource
{
    /// <summary>
    /// 单个 WebResource 的发版排查结果（只读）。
    /// </summary>
    public record WebResourceReleaseCheckResult(
        string Name,
        bool ExistsInBaseline,
        bool ExistsInTarget,
        int BaselineSize,
        int TargetSize,
        string BaselineMd5,
        string TargetMd5,
        /// <summary>内容是否一致；null = 任一侧缺失无法比较</summary>
        bool? ContentMatches,
        /// <summary>目标环境 Active 层有效变更属性（已排除元数据噪声）</summary>
        IReadOnlyList<string> ActiveLayerChanges,
        /// <summary>Active 层是否覆盖 content（true = 发布内容被遮挡，运行时走旧版）</summary>
        bool ActiveLayerOverridesContent);

    /// <summary>
    /// WebResource 发版排查服务（只读）：以基准环境内容为标准，对目标环境检查：
    /// ① 是否存在；② 运行时内容是否与基准一致；③ 是否被非托管 Active 层遮挡（覆盖 content）。
    ///
    /// 背景：托管 Solution 导入目标环境后，若组件在目标环境存在非托管 Active 层且覆盖 content，
    /// 运行时永远以顶层（Active）为准，新发布内容不生效
    /// （2026-07-27 实例：UAT mcs_fca_quotaapp.js 被旧版 Active 层遮挡，审批锁定不生效）。
    /// 修复方式：在目标环境 Maker Portal 删除该组件的 Active 层（Remove active customization）。
    /// </summary>
    public class WebResourceReleaseCheckService
    {
        private readonly ServiceClient _baseline;
        private readonly ServiceClient _target;
        private readonly SolutionComponentService _targetSolutionSvc;

        /// <param name="baseline">基准环境（清单/内容真相源，通常是 DEV1）</param>
        /// <param name="target">目标环境（通常是 UAT）</param>
        public WebResourceReleaseCheckService(ServiceClient baseline, ServiceClient target)
        {
            _baseline = baseline ?? throw new ArgumentNullException(nameof(baseline));
            _target = target ?? throw new ArgumentNullException(nameof(target));
            _targetSolutionSvc = new SolutionComponentService(target);
        }

        /// <summary>
        /// 逐个检查指定名称的 WebResource。
        /// </summary>
        public List<WebResourceReleaseCheckResult> Check(IReadOnlyList<string> names)
        {
            var results = new List<WebResourceReleaseCheckResult>();
            foreach (var name in names)
            {
                results.Add(CheckOne(name));
            }
            return results;
        }

        private WebResourceReleaseCheckResult CheckOne(string name)
        {
            // 1. 基准环境内容
            var baselineWr = QueryByName(_baseline, name);
            var baselineBytes = GetContent(baselineWr);

            // 2. 目标环境内容（webresource 实体返回的是顶层生效内容）
            var targetWr = QueryByName(_target, name);
            var targetBytes = GetContent(targetWr);

            // 3. 目标环境 Active 层
            var layerChanges = new List<string>();
            if (targetWr != null)
            {
                try
                {
                    layerChanges = _targetSolutionSvc.CheckActiveLayer("WebResource", targetWr.Id);
                }
                catch (Exception ex)
                {
                    layerChanges.Add($"(Active层查询失败: {ex.Message})");
                }
            }

            bool? matches = (baselineBytes == null || targetBytes == null)
                ? null
                : Convert.ToBase64String(baselineBytes) == Convert.ToBase64String(targetBytes);

            return new WebResourceReleaseCheckResult(
                Name: name,
                ExistsInBaseline: baselineWr != null,
                ExistsInTarget: targetWr != null,
                BaselineSize: baselineBytes?.Length ?? 0,
                TargetSize: targetBytes?.Length ?? 0,
                BaselineMd5: Md5Short(baselineBytes),
                TargetMd5: Md5Short(targetBytes),
                ContentMatches: matches,
                ActiveLayerChanges: layerChanges,
                ActiveLayerOverridesContent: layerChanges.Any(p => p.Equals("content", StringComparison.OrdinalIgnoreCase)));
        }

        private static Entity? QueryByName(ServiceClient service, string name)
        {
            var query = new QueryExpression("webresource")
            {
                ColumnSet = new ColumnSet("webresourceid", "name", "content"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("name", ConditionOperator.Equal, name) }
                }
            };
            return service.RetrieveMultiple(query).Entities.FirstOrDefault();
        }

        private static byte[]? GetContent(Entity? wr)
        {
            var content = wr?.GetAttributeValue<string>("content");
            return string.IsNullOrEmpty(content) ? null : Convert.FromBase64String(content);
        }

        private static string Md5Short(byte[]? bytes)
        {
            if (bytes == null) return "-";
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(bytes);
            var sb = new StringBuilder(8);
            for (int i = 0; i < 4; i++) sb.Append(hash[i].ToString("x2"));
            return sb.ToString();
        }
    }
}
