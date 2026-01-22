using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using AgentCommands;

namespace AgentCommands.Core
{
    /// <summary>
    /// 单条日志缓存数据.
    /// </summary>
    internal sealed class LogEntry
    {
        /// <summary>
        /// 日志时间戳.
        /// </summary>
        public string time;
        /// <summary>
        /// 日志等级.
        /// </summary>
        public string level;
        /// <summary>
        /// 日志内容.
        /// </summary>
        public string message;
        /// <summary>
        /// 日志堆栈.
        /// </summary>
        public string stack;
    }

    /// <summary>
    /// Unity日志缓存与查询工具.
    /// </summary>
    internal static class LogCache
    {
        /// <summary>
        /// 最大缓存日志数量.
        /// </summary>
        private const int MaxCached = 10000;

        /// <summary>
        /// 线程同步锁对象.
        /// </summary>
        private static readonly object _lock = new object();

        /// <summary>
        /// 日志缓存列表.
        /// </summary>
        private static readonly List<LogEntry> _logs = new List<LogEntry>(1024);

        /// <summary>
        /// 是否已初始化标志.
        /// </summary>
        private static bool _initialized;

        /// <summary>
        /// 当前缓存日志数量.
        /// </summary>
        public static int Count
        {
            get
            {
                lock (_lock)
                {
                    return _logs.Count;
                }
            }
        }

        /// <summary>
        /// 初始化日志回调监听.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized)
            {
                Application.logMessageReceived -= HandleLog;
            }

            Application.logMessageReceived += HandleLog;
            _initialized = true;
        }

        /// <summary>
        /// 释放日志回调监听.
        /// </summary>
        public static void Shutdown()
        {
            if (!_initialized) return;

            Application.logMessageReceived -= HandleLog;
            _initialized = false;
        }

        /// <summary>
        /// 处理Unity日志回调并缓存.
        /// </summary>
        /// <param name="logString">日志文本.</param>
        /// <param name="stackTrace">日志堆栈.</param>
        /// <param name="type">日志类型.</param>
        private static void HandleLog(string logString, string stackTrace, LogType type)
        {
            lock (_lock)
            {
                _logs.Add(new LogEntry
                {
                    time = AgentCommandsConfig.FormatTimestamp(DateTime.Now),
                    level = MapLevel(type),
                    message = logString ?? "",
                    stack = stackTrace ?? ""
                });

                if (_logs.Count > MaxCached)
                {
                    _logs.RemoveAt(0);
                }
            }
        }

        /// <summary>
        /// 将Unity日志类型映射为协议等级.
        /// </summary>
        /// <param name="type">Unity日志类型.</param>
        /// <returns>协议等级字符串.</returns>
        private static string MapLevel(LogType type)
        {
            switch (type)
            {
                case LogType.Warning:
                    return "Warning";
                case LogType.Error:
                case LogType.Exception:
                case LogType.Assert:
                    return "Error";
                default:
                    return "Log";
            }
        }

        /// <summary>
        /// 查询日志列表.
        /// </summary>
        /// <param name="n">返回最近n条.</param>
        /// <param name="level">日志等级过滤.</param>
        /// <param name="keyword">关键词过滤.</param>
        /// <param name="matchMode">匹配模式.</param>
        /// <param name="includeStack">是否包含堆栈.</param>
        /// <returns>符合条件的日志列表.</returns>
        public static List<LogEntry> Query(int n, string level, string keyword, string matchMode, bool includeStack)
        {
            if (n < 0) n = 0;

            lock (_lock)
            {
                IEnumerable<LogEntry> seq = _logs;

                if (!string.IsNullOrEmpty(level))
                {
                    seq = FilterByLevel(seq, level);
                }

                if (!string.IsNullOrEmpty(keyword))
                {
                    seq = FilterByKeyword(seq, keyword, matchMode ?? "Strict", includeStack);
                }

                List<LogEntry> filtered = new List<LogEntry>();
                foreach (var item in seq)
                {
                    filtered.Add(item);
                }

                // 返回最近 n 条,并保持旧->新顺序.
                if (n > 0 && filtered.Count > n)
                {
                    filtered.RemoveRange(0, filtered.Count - n);
                }

                return filtered;
            }
        }

        /// <summary>
        /// 按日志等级过滤.
        /// </summary>
        /// <param name="seq">日志序列.</param>
        /// <param name="level">等级.</param>
        /// <returns>过滤后的序列.</returns>
        private static IEnumerable<LogEntry> FilterByLevel(IEnumerable<LogEntry> seq, string level)
        {
            foreach (var e in seq)
            {
                if (e.level == level)
                {
                    yield return e;
                }
            }
        }

        /// <summary>
        /// 按关键词过滤.
        /// </summary>
        /// <param name="seq">日志序列.</param>
        /// <param name="keyword">关键词.</param>
        /// <param name="matchMode">匹配模式.</param>
        /// <param name="includeStack">是否包含堆栈.</param>
        /// <returns>过滤后的序列.</returns>
        private static IEnumerable<LogEntry> FilterByKeyword(IEnumerable<LogEntry> seq, string keyword, string matchMode, bool includeStack)
        {
            if (string.IsNullOrEmpty(keyword))
            {
                foreach (var e in seq) yield return e;
                yield break;
            }

            if (matchMode == "Fuzzy")
            {
                foreach (var e in seq)
                {
                    if (ContainsIgnoreCase(e.message, keyword) || (includeStack && ContainsIgnoreCase(e.stack, keyword)))
                    {
                        yield return e;
                    }
                }
                yield break;
            }

            if (matchMode == "Regex")
            {
                Regex regex = new Regex(keyword);
                foreach (var e in seq)
                {
                    if (regex.IsMatch(e.message) || (includeStack && regex.IsMatch(e.stack)))
                    {
                        yield return e;
                    }
                }
                yield break;
            }

            // 严格匹配(默认).
            foreach (var e in seq)
            {
                if ((e.message != null && e.message.Contains(keyword)) || (includeStack && e.stack != null && e.stack.Contains(keyword)))
                {
                    yield return e;
                }
            }
        }

        /// <summary>
        /// 大小写不敏感的包含判断.
        /// </summary>
        /// <param name="haystack">原文本.</param>
        /// <param name="needle">关键词.</param>
        /// <returns>是否包含.</returns>
        private static bool ContainsIgnoreCase(string haystack, string needle)
        {
            if (haystack == null) return false;
            return haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
