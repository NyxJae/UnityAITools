using UnityAgentSkills.Core;
using UnityAgentSkills.Plugins.PlayMode.Handlers;

namespace UnityAgentSkills.Plugins.PlayMode
{
    /// <summary>
    /// Play Mode 命令插件.
    /// </summary>
    [CommandPlugin("PlayMode", Priority = 30)]
    public class PlayModeCommandsPlugin : ICommandPlugin
    {
        /// <summary>
        /// 插件名称.
        /// </summary>
        public string Name => "PlayMode";

        /// <summary>
        /// 注册命令处理器.
        /// </summary>
        /// <param name="registry">命令注册表.</param>
        public void RegisterHandlers(CommandHandlerRegistry registry)
        {
            registry.Register("playmode.start", PlayModeStartHandler.Execute);
            registry.Register("playmode.stop", PlayModeStopHandler.Execute);
            registry.Register("playmode.queryUI", PlayModeQueryUIHandler.Execute);
            registry.Register("playmode.waitFor", PlayModeWaitForHandler.Execute);
            registry.Register("playmode.click", PlayModeClickHandler.Execute);
            registry.Register("playmode.clickAt", PlayModeClickAtHandler.Execute);
            registry.Register("playmode.setText", PlayModeSetTextHandler.Execute);
            registry.Register("playmode.scroll", PlayModeScrollHandler.Execute);
        }

        /// <summary>
        /// 插件初始化.
        /// </summary>
        public void Initialize()
        {
            PlayModeSession.Initialize();
        }

        /// <summary>
        /// 插件清理.
        /// </summary>
        public void Shutdown()
        {
        }
    }
}
