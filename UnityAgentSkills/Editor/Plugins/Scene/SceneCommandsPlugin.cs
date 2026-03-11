using UnityAgentSkills.Core;
using UnityAgentSkills.Plugins.Scene.Handlers;

namespace UnityAgentSkills.Plugins.Scene
{
    /// <summary>
    /// 场景命令插件.
    /// 提供场景打开,查询与编辑功能.
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
            registry.Register(SceneSetGameObjectPropertiesHandler.CommandType, SceneSetGameObjectPropertiesHandler.Execute);
            registry.Register(SceneRenameGameObjectHandler.CommandType, SceneRenameGameObjectHandler.Execute);
            registry.Register(SceneCreateGameObjectHandler.CommandType, SceneCreateGameObjectHandler.Execute);
            registry.Register(SceneDeleteGameObjectHandler.CommandType, SceneDeleteGameObjectHandler.Execute);
            registry.Register(SceneMoveOrCopyGameObjectHandler.CommandType, SceneMoveOrCopyGameObjectHandler.Execute);
            registry.Register(SceneSetSiblingIndexHandler.CommandType, SceneSetSiblingIndexHandler.Execute);
            registry.Register(SceneAddComponentHandler.CommandType, SceneAddComponentHandler.Execute);
            registry.Register(SceneSetComponentPropertiesHandler.CommandType, SceneSetComponentPropertiesHandler.Execute);
            registry.Register(SceneDeleteComponentHandler.CommandType, SceneDeleteComponentHandler.Execute);
            registry.Register(SceneSetTransformHandler.CommandType, SceneSetTransformHandler.Execute);
            registry.Register(SceneSetRectTransformHandler.CommandType, SceneSetRectTransformHandler.Execute);
            registry.Register(SceneBatchEditHandler.CommandType, SceneBatchEditHandler.Execute);
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
