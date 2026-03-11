using UnityAgentSkills.Core;
using UnityAgentSkills.Plugins.EditorControl.Handlers;

namespace UnityAgentSkills.Plugins.EditorControl
{
    /// <summary>
    /// EditorControl 命令插件.
    /// 提供显式 `editor.*` 控制命令注册入口.
    /// </summary>
    [CommandPlugin("EditorControl", Priority = 34)]
    public class EditorControlCommandsPlugin : ICommandPlugin
    {
        /// <summary>
        /// 插件名称.
        /// </summary>
        public string Name => "EditorControl";

        /// <summary>
        /// 注册命令处理器.
        /// </summary>
        /// <param name="registry">命令注册表.</param>
        public void RegisterHandlers(CommandHandlerRegistry registry)
        {
            registry.Register(EditorGetStateHandler.CommandType, EditorGetStateHandler.Execute);
            registry.Register(EditorGetContextHandler.CommandType, EditorGetContextHandler.Execute);
            registry.Register(EditorGetSelectionHandler.CommandType, EditorGetSelectionHandler.Execute);
            registry.Register(EditorGetTagsHandler.CommandType, EditorGetTagsHandler.Execute);
            registry.Register(EditorGetLayersHandler.CommandType, EditorGetLayersHandler.Execute);
            registry.Register(EditorSelectHandler.CommandType, EditorSelectHandler.Execute);
            registry.Register(EditorUndoHandler.CommandType, EditorUndoHandler.Execute);
            registry.Register(EditorRedoHandler.CommandType, EditorRedoHandler.Execute);
            registry.Register(EditorExecuteMenuHandler.CommandType, EditorExecuteMenuHandler.Execute);
            registry.Register(EditorSetPauseOnErrorHandler.CommandType, EditorSetPauseOnErrorHandler.Execute);
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
