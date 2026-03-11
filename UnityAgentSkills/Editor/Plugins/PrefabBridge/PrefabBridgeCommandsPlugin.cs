using UnityAgentSkills.Core;
using UnityAgentSkills.Plugins.PrefabBridge.Handlers;

namespace UnityAgentSkills.Plugins.PrefabBridge
{
    /// <summary>
    /// PrefabBridge 命令插件.
    /// 负责注册 prefab asset 与 scene prefab instance 之间的桥接命令.
    /// </summary>
    [CommandPlugin("PrefabBridge", Priority = 27)]
    public class PrefabBridgeCommandsPlugin : ICommandPlugin
    {
        /// <summary>
        /// 插件名称.
        /// </summary>
        public string Name => "PrefabBridge";

        /// <summary>
        /// 注册命令处理器.
        /// </summary>
        /// <param name="registry">命令注册表.</param>
        public void RegisterHandlers(CommandHandlerRegistry registry)
        {
            registry.Register(PrefabBridgeInstantiateInSceneHandler.CommandType, PrefabBridgeInstantiateInSceneHandler.Execute);
            registry.Register(PrefabBridgeGetInstanceSourceHandler.CommandType, PrefabBridgeGetInstanceSourceHandler.Execute);
            registry.Register(PrefabBridgeGetInstanceRelationshipHandler.CommandType, PrefabBridgeGetInstanceRelationshipHandler.Execute);
            registry.Register(PrefabBridgeGetOverridesHandler.CommandType, PrefabBridgeGetOverridesHandler.Execute);
            registry.Register(PrefabBridgeApplyOverridesHandler.CommandType, PrefabBridgeApplyOverridesHandler.Execute);
            registry.Register(PrefabBridgeRevertOverridesHandler.CommandType, PrefabBridgeRevertOverridesHandler.Execute);
            registry.Register(PrefabBridgeUnpackInstanceHandler.CommandType, PrefabBridgeUnpackInstanceHandler.Execute);
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
