using Microsoft.Xrm.Sdk;
using System.Collections.Generic;
using System.Linq;

namespace D365ToolCommon.Metadata
{
    /// <summary>
    /// D365 标签辅助类，统一封装多语言 Label 创建。
    /// </summary>
    public static class LabelHelper
    {
        /// <summary>默认支持的语言代码：英文(1033) + 简体中文(2052)</summary>
        public static readonly int[] DefaultLanguageCodes = { 1033, 2052 };

        /// <summary>
        /// 创建多语言 Label（默认 1033 + 2052）
        /// </summary>
        public static Label Create(string text)
        {
            return Create(text, DefaultLanguageCodes);
        }

        /// <summary>
        /// 创建指定语言列表的 Label
        /// </summary>
        public static Label Create(string text, params int[] languageCodes)
        {
            if (languageCodes == null || languageCodes.Length == 0)
            {
                languageCodes = DefaultLanguageCodes;
            }

            var localizedLabels = languageCodes
                .Distinct()
                .Select(lc => new LocalizedLabel(text, lc))
                .ToArray();

            if (localizedLabels.Length == 0)
            {
                return new Label();
            }

            var userLabel = localizedLabels[0];
            var otherLabels = localizedLabels.Length > 1
                ? localizedLabels.Skip(1).ToArray()
                : System.Array.Empty<LocalizedLabel>();

            return new Label(userLabel, otherLabels);
        }

        /// <summary>
        /// 创建中英文双语 Label（2052 简体中文 + 1033 英文）
        /// 注意：D365 组织基础语言为中文，Label 第一个 LocalizedLabel 需与基础语言一致
        /// </summary>
        public static Label Create(string zhCN, string enUS)
        {
            var localizedLabels = new List<LocalizedLabel>();

            if (!string.IsNullOrWhiteSpace(zhCN))
            {
                localizedLabels.Add(new LocalizedLabel(zhCN, 2052));
            }

            if (!string.IsNullOrWhiteSpace(enUS))
            {
                localizedLabels.Add(new LocalizedLabel(enUS, 1033));
            }

            if (localizedLabels.Count == 0)
            {
                return new Label();
            }

            var userLabel = localizedLabels[0];
            var otherLabels = localizedLabels.Count > 1
                ? localizedLabels.Skip(1).ToArray()
                : System.Array.Empty<LocalizedLabel>();

            return new Label(userLabel, otherLabels);
        }

        /// <summary>
        /// 从语言代码字典创建多语言 Label
        /// </summary>
        public static Label Create(Dictionary<int, string> translations)
        {
            if (translations == null || translations.Count == 0)
            {
                return new Label();
            }

            var localizedLabels = translations
                .Where(t => !string.IsNullOrWhiteSpace(t.Value))
                .Select(t => new LocalizedLabel(t.Value, t.Key))
                .ToArray();

            if (localizedLabels.Length == 0)
            {
                return new Label();
            }

            var userLabel = localizedLabels[0];
            var otherLabels = localizedLabels.Length > 1
                ? localizedLabels.Skip(1).ToArray()
                : System.Array.Empty<LocalizedLabel>();

            return new Label(userLabel, otherLabels);
        }

        /// <summary>
        /// 创建单语言 Label
        /// </summary>
        public static Label CreateSingle(string text, int languageCode)
        {
            return new Label(text, languageCode);
        }
    }
}
