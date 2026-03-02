using UnityAgentSkills.Core;
using UnityAgentSkills.Plugins.Scene.Handlers;

namespace UnityAgentSkills.Plugins.Scene
{
    /// <summary>
    /// 场景命令插件.
    /// 提供场景打开与场景内对象查询功能.
    /// </summary>
    [CommandPlugin("Scene", Priority = 25)]
    public class SceneCommandsPlugin : ICommandPlugin
    {
        /// <summary>
        /// 插件名称.
        /// </summary>
        public string Name => "Scene";

        /// <summary>
        /// 注册命令处理器.
        /// </summary>
        /// <param name="registry">命令注册表.</param>
        public void RegisterHandlers(CommandHandlerRegistry registry)
        {
            registry.Register(SceneOpenHandler.CommandType, SceneOpenHandler.Execute);
            registry.Register(SceneQueryHierarchyHandler.CommandType, SceneQueryHierarchyHandler.Execute);
            registry.Register(SceneQueryComponentsHandler.CommandType, SceneQueryComponentsHandler.Execute);
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
