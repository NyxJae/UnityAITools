using LitJson2_utf;
using UnityAgentSkills.Plugins.EditorControl.Services;

namespace UnityAgentSkills.Plugins.EditorControl.Handlers
{
    /// <summary>
    /// editor.getContext 命令处理器.
    /// </summary>
    internal static class EditorGetContextHandler
    {
        /// <summary>
        /// 命令类型.
        /// </summary>
        public const string CommandType = "editor.getContext";

        /// <summary>
        /// 执行命令.
        /// </summary>
        public static JsonData Execute(JsonData rawParams)
        {
            return EditorControlService.GetContext();
        }
    }
}
