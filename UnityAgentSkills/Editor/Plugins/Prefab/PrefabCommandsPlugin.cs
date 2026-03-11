using UnityAgentSkills.Core;
using UnityAgentSkills.Plugins.Prefab.Handlers;

namespace UnityAgentSkills.Plugins.Prefab
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
            registry.Register(PrefabQueryHierarchyHandler.CommandType, PrefabQueryHierarchyHandler.Execute);
            registry.Register(PrefabQueryComponentsHandler.CommandType, PrefabQueryComponentsHandler.Execute);
            registry.Register(PrefabSetGameObjectPropertiesHandler.CommandType, PrefabSetGameObjectPropertiesHandler.Execute);
            registry.Register(PrefabDeleteGameObjectHandler.CommandType, PrefabDeleteGameObjectHandler.Execute);
            registry.Register(PrefabMoveOrCopyGameObjectHandler.CommandType, PrefabMoveOrCopyGameObjectHandler.Execute);
            registry.Register(PrefabCreateGameObjectHandler.CommandType, PrefabCreateGameObjectHandler.Execute);
            registry.Register(PrefabAddComponentHandler.CommandType, PrefabAddComponentHandler.Execute);
            registry.Register(PrefabSetComponentPropertiesHandler.CommandType, PrefabSetComponentPropertiesHandler.Execute);
            registry.Register(PrefabDeleteComponentHandler.CommandType, PrefabDeleteComponentHandler.Execute);

            registry.Register(PrefabRenameGameObjectHandler.CommandType, PrefabRenameGameObjectHandler.Execute);
            registry.Register(PrefabSetSiblingIndexHandler.CommandType, PrefabSetSiblingIndexHandler.Execute);
            registry.Register(PrefabSetTransformHandler.CommandType, PrefabSetTransformHandler.Execute);
            registry.Register(PrefabSetRectTransformHandler.CommandType, PrefabSetRectTransformHandler.Execute);
            registry.Register(PrefabBatchEditHandler.CommandType, PrefabBatchEditHandler.Execute);
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
