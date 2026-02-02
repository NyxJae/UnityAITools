using System;
using System.Collections.Generic;

namespace UnityAgentSkills.AutoCompile
{
    /// <summary>
    /// AutoCompile 服务配置模型.
    /// 使用 EditorPrefs 持久化存储.
    /// </summary>
    [Serializable]
    public class AutoCompileConfig
    {
        /// <summary>
        /// 启用或禁用自动编译服务.
        /// </summary>
        public bool IsEnabled = false;

        /// <summary>
        /// 防抖间隔(毫秒),范围 200-5000.
        /// </summary>
        public int DebounceInterval = 500;

        /// <summary>
        /// 监听路径列表,支持多个路径.
        /// </summary>
        public List<string> WatchPaths = new List<string> { "Assets" };
    }
}
