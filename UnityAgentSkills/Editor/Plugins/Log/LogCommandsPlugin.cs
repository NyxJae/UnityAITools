using UnityAgentSkills.Core;

namespace UnityAgentSkills.Plugins.Log
{
    /// <summary>
    /// 日志命令插件.
    /// log.query命令已在CoreCommandsLoader中以priority 0注册,此处不再重复注册.
    /// 此类作为代码规范的参考实现,展示正确的插件写法.
    /// 
    /// 注意: 此插件类会被反射扫描加载,但RegisterHandlers为空实现,避免重复注册覆盖元能力层优先级.
    /// </summary>
    [CommandPlugin("Log", Priority = 10)]
    public class LogCommandsPlugin : ICommandPlugin
    {
        /// <summary>
        /// 插件名称.
        /// </summary>
        public string Name => "Log";

        /// <summary>
        /// 注册命令处理器.
        /// 空实现以避免覆盖元能力层的priority 0注册.
        /// </summary>
        /// <param name="registry">命令注册表.</param>
        public void RegisterHandlers(CommandHandlerRegistry registry)
        {
            // log.query已在CoreCommandsLoader中注册,不再重复注册
        }

        /// <summary>
        /// 插件初始化.
        /// </summary>
        public void Initialize()
        {
        }

        /// <summary>
        /// 插件清理.
        /// </summary>
        public void Shutdown()
        {
        }
    }
}
