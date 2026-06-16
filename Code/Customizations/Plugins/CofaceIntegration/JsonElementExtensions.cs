using System;
using System.Globalization;
using System.Text.Json;

namespace SanyD365.Plugins.CofaceIntegration
{
    /// <summary>
    /// JsonElement 扩展方法
    /// 兼容 Coface 接口中数值字段可能返回为 Number 或 String 的情况
    /// </summary>
    public static class JsonElementExtensions
    {
        /// <summary>
        /// 安全获取 decimal 值：支持 JSON Number 和 String 两种格式
        /// </summary>
        public static decimal? GetDecimalSafe(this JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Number)
            {
                if (element.TryGetDecimal(out decimal value))
                    return value;
                return null;
            }

            if (element.ValueKind == JsonValueKind.String)
            {
                string str = element.GetString();
                if (string.IsNullOrWhiteSpace(str))
                    return null;

                if (decimal.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal value))
                    return value;
            }

            return null;
        }
    }
}
