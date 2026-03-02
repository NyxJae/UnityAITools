using System;
using UnityAgentSkills.Core;
using UnityAgentSkills.Plugins.PlayMode.Utils;
using LitJson2_utf;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UnityAgentSkills.Plugins.PlayMode.Handlers
{
    /// <summary>
    /// playmode.click 命令处理器.
    /// </summary>
    internal static class PlayModeClickHandler
    {
        /// <summary>
        /// 命令类型.
        /// </summary>
        public const string CommandType = "playmode.click";

        /// <summary>
        /// 执行路径点击.
        /// </summary>
        public static JsonData Execute(JsonData rawParams)
        {
            CommandParams parameters = new CommandParams(rawParams);
            string targetPath = parameters.GetString("targetPath", null);
            int siblingIndex = parameters.GetInt("siblingIndex", 0);

            PlayModeSession.EnsureActiveForCommand();

            GameObject target = PlayModeUIQueryUtils.FindTarget(targetPath, siblingIndex);
            if (!PlayModeUIQueryUtils.IsVisible(target))
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.ElementNotVisible + ": Element is not visible");
            }

            if (!PlayModeUIQueryUtils.IsInteractable(target))
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.ElementNotInteractable + ": Target element is not interactable");
            }

            if (EventSystem.current == null)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.RuntimeError + ": EventSystem.current is null");
            }

            PointerEventData pointerEventData = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left
            };
            ExecuteEvents.Execute<IPointerClickHandler>(target, pointerEventData, ExecuteEvents.pointerClickHandler);

            JsonData resolvedTarget = PlayModeUIQueryUtils.BuildResolvedTarget(target);

            JsonData result = new JsonData();
            result["actionType"] = "click";
            result["targetPath"] = targetPath;
            result["siblingIndex"] = siblingIndex;
            result["clicked"] = true;
            result["selectorSource"] = "path";
            result["actionAccepted"] = true;
            result["resolvedTarget"] = resolvedTarget;
            return result;
        }
    }
}
