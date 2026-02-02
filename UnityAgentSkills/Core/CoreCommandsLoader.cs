using UnityAgentSkills.Plugins.Log.Handlers;

namespace UnityAgentSkills.Core
{
    /// <summary>
    /// 核心命令加载器.
    /// 硬编码注册内置命令(log.query).
    /// 不使用反射,确保内置命令永不失败.
    /// </summary>
    public static class CoreCommandsLoader
    {
        /// <summary>
        /// 注册所有核心命令.
        /// 此方法通过硬编码调用,不依赖反射,确保内置命令永远可用.
        /// </summary>
        /// <param name="registry">命令注册表实例.</param>
        public static void RegisterCoreCommands(CommandHandlerRegistry registry)
        {
            // 优先级0: 内置命令,最高优先级
            // log.query 是唯一内置命令,不使用反射加载

            registry.Register("log.query", LogQueryCommandHandler.Execute, priority: 0);
        }
    }
}
