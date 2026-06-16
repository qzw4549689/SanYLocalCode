using Microsoft.Xrm.Sdk;
using System.Collections.Generic;
using System.Linq;

namespace D365MetadataTool
{
    /// <summary>
    /// D365 标签辅助类
    /// 统一封装多语言 Label 创建，避免各处写法不一致导致编译或运行时问题
    /// </summary>
    public static class LabelHelper
    {
        /// <summary>
        /// 默认支持的语言代码：英文(1033) + 简体中文(2052)
        /// </summary>
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

            // Label 构造函数：第一个为当前用户语言标签，第二个为其他语言标签数组
            var userLabel = localizedLabels[0];
            var otherLabels = localizedLabels.Length > 1
                ? localizedLabels.Skip(1).ToArray()
                : System.Array.Empty<LocalizedLabel>();

            return new Label(userLabel, otherLabels);
        }

        /// <summary>
        /// 创建单语言 Label（仅用于明确只需要一种语言的场景）
        /// </summary>
        public static Label CreateSingle(string text, int languageCode)
        {
            return new Label(text, languageCode);
        }

    }
}
