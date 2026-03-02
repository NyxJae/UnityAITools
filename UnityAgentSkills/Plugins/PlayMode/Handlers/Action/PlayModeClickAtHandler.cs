using System;
using System.Collections.Generic;
using UnityAgentSkills.Core;
using UnityAgentSkills.Plugins.PlayMode.Utils;
using LitJson2_utf;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UnityAgentSkills.Plugins.PlayMode.Handlers
{
    /// <summary>
    /// playmode.clickAt 命令处理器.
    /// </summary>
    internal static class PlayModeClickAtHandler
    {
        /// <summary>
        /// 命令类型.
        /// </summary>
        public const string CommandType = "playmode.clickAt";

        /// <summary>
        /// 执行坐标点击.
        /// </summary>
        public static JsonData Execute(JsonData rawParams)
        {
            if (rawParams == null || !rawParams.IsObject)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": params must be object");
            }

            PlayModeSession.EnsureActiveForCommand();

            float x = PlayModeParamUtils.GetFloat(rawParams, "x", float.MinValue);
            float y = PlayModeParamUtils.GetFloat(rawParams, "y", float.MinValue);
            if (x == float.MinValue || y == float.MinValue)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": x and y are required");
            }

            PlayModeUIQueryUtils.EnsureCoordinatesInBounds(x, y);
            List<RaycastResult> rayResults = PlayModeUIQueryUtils.RaycastAt(x, y);
            if (rayResults.Count == 0)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.NoElementAtPosition + ": No UI element at position");
            }

            GameObject target = null;
            for (int i = 0; i < rayResults.Count; i++)
            {
                if (rayResults[i].gameObject == null)
                {
                    continue;
                }

                if (!PlayModeUIQueryUtils.IsVisible(rayResults[i].gameObject))
                {
                    continue;
                }

                target = rayResults[i].gameObject;
                break;
            }

            if (target == null)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.NoElementAtPosition + ": No UI element at position");
            }

            if (!PlayModeUIQueryUtils.IsInteractable(target))
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.ElementNotInteractable + ": Target element is not interactable");
            }

            PointerEventData pointerEventData = new PointerEventData(EventSystem.current)
            {
                position = new Vector2(x, y),
                button = PointerEventData.InputButton.Left
            };
            ExecuteEvents.Execute<IPointerClickHandler>(target, pointerEventData, ExecuteEvents.pointerClickHandler);

            JsonData resolvedTarget = PlayModeUIQueryUtils.BuildResolvedTarget(target);

            JsonData result = new JsonData();
            result["actionType"] = "clickAt";
            result["x"] = x;
            result["y"] = y;
            result["targetPath"] = UnityAgentSkills.Utils.GameObjectPathFinder.GetPath(target);
            result["siblingIndex"] = UnityAgentSkills.Utils.GameObjectPathFinder.GetSameNameSiblingIndex(target);
            result["clicked"] = true;
            result["selectorSource"] = "coordinates";
            result["actionAccepted"] = true;
            result["resolvedTarget"] = resolvedTarget;
            return result;
        }
    }
}
