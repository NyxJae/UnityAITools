using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityAgentSkills.Core;
using LitJson2_utf;

namespace UnityAgentSkills.Plugins.Log.Handlers
{
    /// <summary>
    /// log.query命令处理器.
    /// </summary>
    internal static class LogQueryCommandHandler
    {
        /// <summary>
        /// 支持的命令类型.
        /// </summary>
        public const string CommandType = "log.query";

        /// <summary>
        /// 解析并规范化 keyword 数组.
        /// </summary>
        /// <param name="rawParams">原始参数.</param>
        /// <returns>规范化后的关键词数组,若为空则返回 null.</returns>
        private static string[] ParseKeywords(JsonData rawParams)
        {
            if (rawParams == null || !rawParams.IsObject || !rawParams.ContainsKey("keyword"))
            {
                return null;
            }

            JsonData keywordData = rawParams["keyword"];
            if (keywordData == null || !keywordData.IsArray)
            {
                return null;
            }

            List<string> keywords = new List<string>();
            for (int i = 0; i < keywordData.Count; i++)
            {
                string keyword = keywordData[i] == null ? null : keywordData[i].ToString();
                if (string.IsNullOrWhiteSpace(keyword))
                {
                    continue;
                }

                keywords.Add(keyword.Trim());
            }

            return keywords.Count == 0 ? null : keywords.ToArray();
        }

        /// <summary>
        /// 执行日志查询命令.
        /// </summary>
        /// <param name="rawParams">命令参数json.</param>
        /// <returns>结果json.</returns>
        public static JsonData Execute(JsonData rawParams)
        {
            CommandParams parameters = new CommandParams(rawParams);

            int n = parameters.GetInt("n");
            string level = parameters.GetString("level", null);
            string[] keywords = ParseKeywords(rawParams);
            string matchMode = parameters.GetString("matchMode", null);
            bool includeStack = parameters.GetBool("includeStack", false);

            if (keywords != null && string.IsNullOrEmpty(matchMode))
            {
                matchMode = "Fuzzy";
            }

            if (string.Equals(matchMode, "Regex", StringComparison.Ordinal))
            {
                if (keywords != null && keywords.Length != 1)
                {
                    throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": keyword must contain exactly one regex pattern when matchMode=Regex");
                }

                if (keywords != null)
                {
                    try
                    {
                        _ = new Regex(keywords[0]);
                    }
                    catch (ArgumentException ex)
                    {
                        throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.InvalidRegex + ":" + ex.Message);
                    }
                }
            }

            List<LogEntry> items = LogCache.Query(n, level, keywords, matchMode, includeStack);

            JsonData result = new JsonData();
            JsonData arr = new JsonData();
            arr.SetJsonType(JsonType.Array);

            foreach (var e in items)
            {
                JsonData item = new JsonData();
                item["time"] = e.time ?? "";
                item["level"] = e.level ?? "";
                item["message"] = e.message ?? "";
                if (includeStack)
                {
                    item["stack"] = e.stack ?? "";
                }

                arr.Add(item);
            }

            result["items"] = arr;
            result["totalCaptured"] = LogCache.Count;
            result["returned"] = items.Count;
            return result;
        }
    }
}
