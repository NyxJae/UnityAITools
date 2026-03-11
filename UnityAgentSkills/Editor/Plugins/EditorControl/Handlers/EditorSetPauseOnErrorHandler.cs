using System;
using LitJson2_utf;
using UnityAgentSkills.Core;
using UnityAgentSkills.Plugins.EditorControl.Services;

namespace UnityAgentSkills.Plugins.EditorControl.Handlers
{
    /// <summary>
    /// editor.setPauseOnError 命令处理器.
    /// </summary>
    internal static class EditorSetPauseOnErrorHandler
    {
        /// <summary>
        /// 命令类型.
        /// </summary>
        public const string CommandType = "editor.setPauseOnError";

        /// <summary>
        /// 执行命令.
        /// </summary>
        public static JsonData Execute(JsonData rawParams)
        {
            CommandParams parameters = new CommandParams(rawParams);
            JsonData data = parameters.GetData();
            if (data == null || !data.IsObject || !data.ContainsKey("enabled") || !data["enabled"].IsBoolean)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": enabled is required and must be boolean");
            }

            return EditorControlService.SetPauseOnError((bool)data["enabled"]);
        }
    }
}
