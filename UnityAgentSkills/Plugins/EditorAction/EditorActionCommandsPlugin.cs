using UnityAgentSkills.Core;
using UnityAgentSkills.Plugins.EditorAction.Catalog;
using UnityAgentSkills.Plugins.EditorAction.Handlers;

namespace UnityAgentSkills.Plugins.EditorAction
{
    /// <summary>
    /// EditorAction 命令插件.
    /// 提供 editor.runAction 命令注册与动作目录初始化.
    /// </summary>
    [CommandPlugin("EditorAction", Priority = 35)]
    public class EditorActionCommandsPlugin : ICommandPlugin
    {
        /// <summary>
        /// 插件名称.
        /// </summary>
        public string Name => "EditorAction";

        /// <summary>
        /// 注册命令处理器.
        /// </summary>
        /// <param name="registry">命令注册表.</param>
        public void RegisterHandlers(CommandHandlerRegistry registry)
        {
            registry.Register(EditorRunActionHandler.CommandType, EditorRunActionHandler.Execute);
        }

        /// <summary>
        /// 插件初始化.
        /// </summary>
        public void Initialize()
        {
            EditorActionCatalog.Initialize();
        }

        /// <summary>
        /// 插件清理.
        /// </summary>
        public void Shutdown()
        {
            EditorActionCatalog.Clear();
        }
    }
}
