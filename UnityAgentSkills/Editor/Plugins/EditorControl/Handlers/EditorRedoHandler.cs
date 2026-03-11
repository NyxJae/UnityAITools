using LitJson2_utf;
using UnityAgentSkills.Core;
using UnityAgentSkills.Plugins.EditorControl.Services;

namespace UnityAgentSkills.Plugins.EditorControl.Handlers
{
    /// <summary>
    /// editor.redo 命令处理器.
    /// </summary>
    internal static class EditorRedoHandler
    {
        /// <summary>
        /// 命令类型.
        /// </summary>
        public const string CommandType = "editor.redo";

        /// <summary>
        /// 执行命令.
        /// </summary>
        public static JsonData Execute(JsonData rawParams)
        {
            CommandParams parameters = new CommandParams(rawParams);
            return EditorControlService.RedoSteps(parameters.GetInt("steps"));
        }
    }
}
