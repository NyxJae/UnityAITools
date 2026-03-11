using System;
using LitJson2_utf;
using UnityAgentSkills.Core;
using UnityAgentSkills.Plugins.EditorControl.Security;
using UnityAgentSkills.Plugins.EditorControl.Services;

namespace UnityAgentSkills.Plugins.EditorControl.Handlers
{
    /// <summary>
    /// editor.executeMenu 命令处理器.
    /// </summary>
    internal static class EditorExecuteMenuHandler
    {
        /// <summary>
        /// 命令类型.
        /// </summary>
        public const string CommandType = "editor.executeMenu";

        /// <summary>
        /// 执行命令.
        /// </summary>
        public static JsonData Execute(JsonData rawParams)
        {
            CommandParams parameters = new CommandParams(rawParams);
            string menuPath = parameters.GetString("menuPath", null);
            if (string.IsNullOrWhiteSpace(menuPath))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": menuPath is required");
            }

            if (EditorControlSafetyPolicy.IsBlockedMenu(menuPath, out string reason))
            {
                throw new InvalidOperationException(reason);
            }

            return EditorControlService.ExecuteMenu(menuPath);
        }
    }
}
