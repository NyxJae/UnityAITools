using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using AgentCommands.Core;
using LitJson2_utf;

namespace AgentCommands.Plugins.Log.Handlers
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
        /// 执行日志查询命令.
        /// </summary>
        /// <param name="rawParams">命令参数json.</param>
        /// <returns>结果json.</returns>
        public static JsonData Execute(JsonData rawParams)
        {
            CommandParams parameters = new CommandParams(rawParams);

            int n = parameters.GetInt("n");
            string level = parameters.GetString("level", null);
            string keyword = parameters.GetString("keyword", null);
            string matchMode = parameters.GetString("matchMode", null);
            bool includeStack = parameters.GetBool("includeStack", false);

            if (!string.IsNullOrEmpty(keyword) && string.IsNullOrEmpty(matchMode))
            {
                matchMode = "Fuzzy";
            }

            if (matchMode == "Regex" && !string.IsNullOrEmpty(keyword))
            {
                try
                {
                    _ = new Regex(keyword);
                }
                catch (ArgumentException ex)
                {
                    throw new InvalidOperationException(AgentCommandErrorCodes.InvalidRegex + ":" + ex.Message);
                }
            }

            List<LogEntry> items = LogCache.Query(n, level, keyword, matchMode, includeStack);

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
