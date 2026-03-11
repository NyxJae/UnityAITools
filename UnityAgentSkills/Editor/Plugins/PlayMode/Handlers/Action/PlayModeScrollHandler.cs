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
    /// playmode.scroll 命令处理器.
    /// </summary>
    internal static class PlayModeScrollHandler
    {
        /// <summary>
        /// 命令类型.
        /// </summary>
        public const string CommandType = "playmode.scroll";

        /// <summary>
        /// 执行滚轮滚动.
        /// </summary>
        public static JsonData Execute(JsonData rawParams)
        {
            if (rawParams == null || !rawParams.IsObject)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": params must be object");
            }

            PlayModeSession.EnsureActiveForCommand();

            float scrollDelta = PlayModeParamUtils.GetFloat(rawParams, "scrollDelta", 0f);
            if (Math.Abs(scrollDelta) <= float.Epsilon)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": scrollDelta is required and cannot be 0");
            }

            float x = PlayModeParamUtils.GetFloat(rawParams, "x", Screen.width * 0.5f);
            float y = PlayModeParamUtils.GetFloat(rawParams, "y", Screen.height * 0.5f);

            List<RaycastResult> rayResults = PlayModeUIQueryUtils.RaycastAt(x, y);
            if (rayResults.Count == 0)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.NoElementAtPosition + ": No UI element at position");
            }

            PointerEventData pointerEventData = new PointerEventData(EventSystem.current)
            {
                position = new Vector2(x, y),
                scrollDelta = new Vector2(0f, scrollDelta)
            };

            bool handled = false;
            GameObject resolvedTargetObject = null;
            for (int i = 0; i < rayResults.Count; i++)
            {
                GameObject target = rayResults[i].gameObject;
                if (target == null)
                {
                    continue;
                }

                bool executed = ExecuteEvents.ExecuteHierarchy<IScrollHandler>(target, pointerEventData, ExecuteEvents.scrollHandler);
                if (executed)
                {
                    handled = true;
                    resolvedTargetObject = target;
                    break;
                }
            }

            if (!handled)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.NoElementAtPosition + ": No scroll handler at position");
            }

            JsonData resolvedTarget = PlayModeUIQueryUtils.BuildResolvedTarget(resolvedTargetObject);

            JsonData result = new JsonData();
            result["actionType"] = "scroll";
            result["x"] = x;
            result["y"] = y;
            result["scrollDelta"] = scrollDelta;
            result["handled"] = true;
            result["selectorSource"] = "coordinates";
            result["actionAccepted"] = true;
            result["resolvedTarget"] = resolvedTarget;
            return result;
        }
    }
}
