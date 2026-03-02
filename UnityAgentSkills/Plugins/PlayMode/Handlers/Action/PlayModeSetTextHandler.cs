using System;
using UnityAgentSkills.Core;
using UnityAgentSkills.Plugins.PlayMode.Utils;
using LitJson2_utf;
using UnityEngine;
using UnityEngine.UI;

namespace UnityAgentSkills.Plugins.PlayMode.Handlers
{
    /// <summary>
    /// playmode.setText 命令处理器.
    /// </summary>
    internal static class PlayModeSetTextHandler
    {
        /// <summary>
        /// 命令类型.
        /// </summary>
        public const string CommandType = "playmode.setText";

        /// <summary>
        /// 设置输入框文本.
        /// </summary>
        public static JsonData Execute(JsonData rawParams)
        {
            CommandParams parameters = new CommandParams(rawParams);
            string targetPath = parameters.GetString("targetPath", null);
            int siblingIndex = parameters.GetInt("siblingIndex", 0);
            string text = parameters.GetString("text", string.Empty);
            bool submit = parameters.GetBool("submit", false);

            PlayModeSession.EnsureActiveForCommand();

            GameObject target = PlayModeUIQueryUtils.FindTarget(targetPath, siblingIndex);
            if (!PlayModeUIQueryUtils.IsVisible(target))
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.ElementNotVisible + ": Element is not visible");
            }

            InputField inputField = target.GetComponent<InputField>();
            if (inputField == null)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.UnsupportedElementType + ": Target is not an InputField");
            }

            if (!PlayModeUIQueryUtils.IsInteractable(target))
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.ElementNotInteractable + ": Target element is not interactable");
            }

            inputField.text = text;
            inputField.ForceLabelUpdate();
            if (submit)
            {
                inputField.onEndEdit?.Invoke(text);
            }

            JsonData resolvedTarget = PlayModeUIQueryUtils.BuildResolvedTarget(target);

            JsonData result = new JsonData();
            result["actionType"] = "setText";
            result["targetPath"] = targetPath;
            result["siblingIndex"] = siblingIndex;
            result["text"] = text;
            result["submitted"] = submit;
            result["selectorSource"] = "path";
            result["actionAccepted"] = true;
            result["resolvedTarget"] = resolvedTarget;
            return result;
        }
    }
}
