using System;
using System.Collections.Generic;
using LitJson2_utf;

namespace UnityAgentSkills.Plugins.PlayMode.Utils
{
    /// <summary>
    /// Play Mode 命令参数解析工具.
    /// </summary>
    internal static class PlayModeParamUtils
    {
        /// <summary>
        /// 读取可选浮点参数.
        /// </summary>
        public static float GetFloat(JsonData rawParams, string key, float defaultValue)
        {
            if (rawParams == null || !rawParams.IsObject || !rawParams.ContainsKey(key))
            {
                return defaultValue;
            }

            JsonData value = rawParams[key];
            if (value.IsDouble)
            {
                return (float)(double)value;
            }

            if (value.IsInt)
            {
                return (int)value;
            }

            if (value.IsLong)
            {
                return (long)value;
            }

            if (value.IsString && float.TryParse(value.ToString(), out float parsed))
            {
                return parsed;
            }

            return defaultValue;
        }

        /// <summary>
        /// 读取 componentFilter 数组.
        /// </summary>
        public static string[] ParseComponentFilter(JsonData rawParams)
        {
            return ParseStringArray(rawParams, "componentFilter");
        }

        /// <summary>
        /// 读取指定字段的字符串数组,并过滤 Trim 后的空词项.
        /// 非数组输入一律视为未提供,用于阻断旧单字符串主入口继续存活.
        /// </summary>
        public static string[] ParseStringArray(JsonData rawParams, string key)
        {
            if (rawParams == null || !rawParams.IsObject || !rawParams.ContainsKey(key))
            {
                return null;
            }

            JsonData value = rawParams[key];
            if (value == null || !value.IsArray)
            {
                return null;
            }

            List<string> items = new List<string>();
            for (int i = 0; i < value.Count; i++)
            {
                AddNormalizedItem(items, value[i]);
            }

            return items.Count == 0 ? null : items.ToArray();
        }

        /// <summary>
        /// 判断目标文本是否命中任一 contains 词项.
        /// </summary>
        public static bool MatchesAnyContains(string target, string[] filters)
        {
            if (filters == null || filters.Length == 0)
            {
                return true;
            }

            string safeTarget = target ?? string.Empty;
            for (int i = 0; i < filters.Length; i++)
            {
                string filter = filters[i];
                if (string.IsNullOrEmpty(filter))
                {
                    continue;
                }

                if (safeTarget.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddNormalizedItem(List<string> items, JsonData value)
        {
            string text = value == null ? string.Empty : value.ToString();
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            items.Add(text.Trim());
        }
    }
}
