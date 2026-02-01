using AgentCommands.Core;
using AgentCommands.Plugins.K3Prefab.Handlers;

namespace AgentCommands.Plugins.K3Prefab
{
    /// <summary>
    /// K3预制体命令插件.
    /// 包含所有k3prefab.*命令(项目专用能力).
    /// 通过反射自动发现并加载.
    /// 此插件是完全可移除的,删除K3Prefab文件夹后不影响项目编译.
    /// </summary>
    [CommandPlugin("K3Prefab", Priority = 100)]
    public class K3PrefabPlugin : ICommandPlugin
    {
        /// <summary>
        /// 插件名称.
        /// </summary>
        public string Name => "K3Prefab";

        /// <summary>
        /// 注册命令处理器.
        /// </summary>
        /// <param name="registry">命令注册表.</param>
        public void RegisterHandlers(CommandHandlerRegistry registry)
        {
            registry.Register("k3prefab.queryByK3Id", K3PrefabQueryByK3IdHandler.Execute);
            registry.Register("k3prefab.setComponentProperties", K3PrefabSetComponentPropertiesHandler.Execute);
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
