using System;
using System.Collections.Generic;

namespace AgentCommands.Core
{
    /// <summary>
    /// 插件加载结果,包含成功/失败信息和框架可用性状态.
    /// </summary>
    public class PluginLoadResult
    {
        /// <summary>
        /// 框架是否可用(核心命令是否成功加载).
        /// 如果为false,表示log.query等核心命令加载失败,框架处于不可用状态.
        /// </summary>
        public bool IsFrameworkFunctional { get; set; }

        /// <summary>
        /// 核心命令是否加载成功.
        /// 如果为false,表示CoreCommandsLoader加载失败.
        /// </summary>
        public bool CoreCommandsLoaded { get; set; }

        /// <summary>
        /// 是否发生严重失败(核心命令加载失败).
        /// 严重失败时,插件加载流程会中断,框架进入不可用状态.
        /// </summary>
        public bool CriticalFailure { get; set; }

        /// <summary>
        /// 成功加载的插件名称列表.
        /// </summary>
        public List<string> SuccessfulPlugins { get; set; } = new List<string>();

        /// <summary>
        /// 加载失败的插件信息.
        /// Key: 插件类型名称, Value: 失败原因(异常消息).
        /// </summary>
        public Dictionary<string, string> FailedPlugins { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// 每个插件注册的命令清单.
        /// Key: 插件名称, Value: 该插件注册的命令类型列表.
        /// </summary>
        public Dictionary<string, List<string>> PluginCommands { get; set; } = new Dictionary<string, List<string>>();

        /// <summary>
        /// 每个插件的优先级.
        /// Key: 插件名称, Value: 插件优先级.
        /// </summary>
        public Dictionary<string, int> PluginPriorities { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// 加载开始时间.
        /// </summary>
        public DateTime LoadStartTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 加载结束时间.
        /// </summary>
        public DateTime LoadEndTime { get; set; }

        /// <summary>
        /// 加载耗时(毫秒).
        /// </summary>
        public double LoadDurationMs => (LoadEndTime - LoadStartTime).TotalMilliseconds;

        /// <summary>
        /// 获取框架状态的简要描述.
        /// </summary>
        public string GetStatusSummary()
        {
            if (CriticalFailure)
            {
                return $"CRITICAL: 核心命令加载失败,框架不可用";
            }

            if (!IsFrameworkFunctional)
            {
                return $"WARNING: 框架不可用,核心命令未正确注册";
            }

            int totalPlugins = SuccessfulPlugins.Count + FailedPlugins.Count;
            return $"OK: 框架可用, 成功加载{SuccessfulPlugins.Count}/{totalPlugins}个插件";
        }
    }
}
