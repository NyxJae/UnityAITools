using System;

namespace UnityAgentSkills.Core
{
    /// <summary>
    /// 命令插件标记特性.
    /// 用于标记哪些类是命令插件,便于反射扫描和自动发现.
    /// 插件加载器会扫描所有带此特性的类并按优先级顺序加载.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class CommandPluginAttribute : Attribute
    {
        /// <summary>
        /// 插件名称.
        /// 应与ICommandPlugin.Name返回值一致.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 加载优先级.
        /// 数值越小越先加载,默认值为100.
        /// 
        /// 优先级分层建议:
        /// - 0-9: 元能力层(log.query, system.status等核心命令)
        /// - 10-99: 官方插件层(prefab.*, asset.*等Unity通用能力)
        /// - 100+: 扩展插件层(k3prefab.*等项目专用能力)
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// 创建命令插件标记特性.
        /// </summary>
        /// <param name="name">插件名称.</param>
        /// <param name="priority">加载优先级,数值越小越先加载,默认100.</param>
        public CommandPluginAttribute(string name, int priority = 100)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("插件名称不能为空", nameof(name));
            }

            Name = name;
            Priority = priority;
        }
    }
}
