using AgentCommands.Core;

namespace AgentCommands.Core
{
    /// <summary>
    /// 命令插件接口,所有命令插件必须实现此接口.
    /// 插件通过反射自动发现并注册到CommandHandlerRegistry中.
    /// </summary>
    public interface ICommandPlugin
    {
        /// <summary>
        /// 插件名称,用于日志和状态显示.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 注册命令处理器到注册表.
        /// 此方法在插件加载时被调用,应在此方法中调用 registry.Register() 注册所有命令.
        /// </summary>
        /// <param name="registry">命令注册表实例.</param>
        void RegisterHandlers(CommandHandlerRegistry registry);

        /// <summary>
        /// 插件初始化,在RegisterHandlers之后调用.
        /// 可用于初始化插件所需的资源或状态.
        /// 如果不需要初始化,可留空.
        /// </summary>
        void Initialize();

        /// <summary>
        /// 插件清理,在域重载或Unity退出时调用.
        /// 可用于释放插件占用的资源.
        /// 如果不需要清理,可留空.
        /// </summary>
        void Shutdown();
    }
}
