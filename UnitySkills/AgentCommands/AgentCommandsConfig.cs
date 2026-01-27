using System;
using System.IO;
using UnityEngine;

namespace AgentCommands
{
    /// <summary>
    /// AgentCommands插件配置与路径常量.
    /// </summary>
    internal static class AgentCommandsConfig
    {
        /// <summary>
        /// 数据目录相对Assets的路径.
        /// </summary>
        internal const string DataDirRelativeToAssets = "AgentCommands";

        /// <summary>
        /// results中最多保留的最终结果数量.
        /// </summary>
        internal const int MaxResults = 20;

        /// <summary>
        /// 默认批次级别超时(毫秒).
        /// </summary>
        internal const int DefaultBatchTimeoutMs = 30000;

        /// <summary>
        /// 读取pending文件失败时的重试间隔(毫秒).
        /// </summary>
        internal static readonly int[] ReadRetryDelaysMs = { 1000, 2000, 4000 };

        /// <summary>
        /// 定时扫描pending目录的间隔(秒).
        /// </summary>
        internal const double PendingRescanIntervalSeconds = 1.0;

        /// <summary>
        /// Unity工程Assets目录的绝对路径.
        /// </summary>
        internal static string AssetsAbsolutePath
        {
            get { return Application.dataPath; }
        }

        /// <summary>
        /// 插件数据目录的绝对路径.
        /// </summary>
        internal static string DataDirAbsolutePath
        {
            get { return Path.Combine(AssetsAbsolutePath, DataDirRelativeToAssets); }
        }

        /// <summary>
        /// pending目录的绝对路径.
        /// </summary>
        internal static string PendingDirAbsolutePath
        {
            get { return Path.Combine(DataDirAbsolutePath, "pending"); }
        }

        /// <summary>
        /// results目录的绝对路径.
        /// </summary>
        internal static string ResultsDirAbsolutePath
        {
            get { return Path.Combine(DataDirAbsolutePath, "results"); }
        }

        /// <summary>
        /// done目录的绝对路径.
        /// </summary>
        internal static string DoneDirAbsolutePath
        {
            get { return Path.Combine(DataDirAbsolutePath, "done"); }
        }

        /// <summary>
        /// 将时间戳格式化为协议使用的字符串.
        /// </summary>
        /// <param name="dt">时间.</param>
        /// <returns>格式化后的时间字符串.</returns>
        internal static string FormatTimestamp(DateTime dt)
        {
            return dt.ToString("yyyy-MM-dd HH:mm:ss.fff");
        }
    }
}
