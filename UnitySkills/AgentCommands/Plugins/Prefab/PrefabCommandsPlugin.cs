using AgentCommands.Core;
using AgentCommands.Plugins.Prefab.Handlers;

namespace AgentCommands.Plugins.Prefab
{
    /// <summary>
    /// 预制体命令插件.
    /// 提供预制体查询和属性修改功能.
    /// </summary>
    [CommandPlugin("Prefab", Priority = 20)]
    public class PrefabCommandsPlugin : ICommandPlugin
    {
        /// <summary>
        /// 插件名称.
        /// </summary>
        public string Name => "Prefab";

        /// <summary>
        /// 注册命令处理器.
        /// </summary>
        /// <param name="registry">命令注册表.</param>
        public void RegisterHandlers(CommandHandlerRegistry registry)
        {
            registry.Register("prefab.queryHierarchy", PrefabQueryHierarchyHandler.Execute);
            registry.Register("prefab.queryComponents", PrefabQueryComponentsHandler.Execute);
            registry.Register("prefab.setGameObjectProperties", PrefabSetGameObjectPropertiesHandler.Execute);
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
