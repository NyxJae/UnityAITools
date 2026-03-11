using System;
using LitJson2_utf;
using UnityAgentSkills.Core;
using UnityAgentSkills.Plugins.EditorControl.Services;

namespace UnityAgentSkills.Plugins.EditorControl.Handlers
{
    /// <summary>
    /// editor.select 命令处理器.
    /// </summary>
    internal static class EditorSelectHandler
    {
        /// <summary>
        /// 命令类型.
        /// </summary>
        public const string CommandType = "editor.select";

        /// <summary>
        /// 执行命令.
        /// </summary>
        public static JsonData Execute(JsonData rawParams)
        {
            CommandParams parameters = new CommandParams(rawParams);
            string targetKind = parameters.GetString("targetKind", null);
            if (string.IsNullOrWhiteSpace(targetKind))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": targetKind is required");
            }

            if (string.Equals(targetKind, "sceneGameObject", StringComparison.Ordinal))
            {
                return EditorControlService.SelectSceneGameObject(
                    parameters.GetString("sceneName", null),
                    parameters.GetString("objectPath", null),
                    parameters.GetInt("siblingIndex"));
            }

            if (string.Equals(targetKind, "projectAsset", StringComparison.Ordinal))
            {
                return EditorControlService.SelectProjectAsset(parameters.GetString("assetPath", null));
            }

            throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": targetKind must be sceneGameObject or projectAsset");
        }
    }
}
