using System;
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
            if (rawParams == null || !rawParams.IsObject || !rawParams.ContainsKey("componentFilter"))
            {
                return null;
            }

            JsonData filter = rawParams["componentFilter"];
            if (filter == null)
            {
                return null;
            }

            if (filter.IsArray)
            {
                string[] items = new string[filter.Count];
                for (int i = 0; i < filter.Count; i++)
                {
                    items[i] = filter[i] == null ? string.Empty : filter[i].ToString();
                }

                return items;
            }

            if (filter.IsString)
            {
                return new[] { filter.ToString() };
            }

            return null;
        }
    }
}
