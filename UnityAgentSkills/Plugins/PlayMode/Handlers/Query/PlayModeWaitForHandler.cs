using System;
using System.Collections;
using LitJson2_utf;
using UnityAgentSkills.Core;
using UnityAgentSkills.Plugins.PlayMode.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace UnityAgentSkills.Plugins.PlayMode.Handlers
{
    /// <summary>
    /// playmode.waitFor 命令处理器.
    /// </summary>
    internal static class PlayModeWaitForHandler
    {
        /// <summary>
        /// 命令类型.
        /// </summary>
        public const string CommandType = "playmode.waitFor";

        internal readonly struct WaitForJob
        {
            /// <summary>
            /// 等待上限(秒),必须大于0.
            /// </summary>
            public readonly float waitSeconds;

            /// <summary>
            /// 名称包含匹配条件.
            /// </summary>
            public readonly string nameContains;

            /// <summary>
            /// 文本包含匹配条件.
            /// </summary>
            public readonly string textContains;

            /// <summary>
            /// 等待开始UTC时间.
            /// </summary>
            public readonly DateTime startedAtUtc;

            public WaitForJob(float waitSeconds, string nameContains, string textContains, DateTime startedAtUtc)
            {
                this.waitSeconds = waitSeconds;
                this.nameContains = nameContains;
                this.textContains = textContains;
                this.startedAtUtc = startedAtUtc;
            }
        }

        /// <summary>
        /// 兼容同步执行入口.
        /// </summary>
        public static JsonData Execute(JsonData rawParams)
        {
            PlayModeSession.EnsureActiveForCommand();
            WaitForJob job = CreateJob(rawParams, DateTime.UtcNow);
            JsonData result;
            if (TryComplete(job, DateTime.UtcNow, out result))
            {
                return result;
            }

            // 在批处理跨帧执行链路中会继续推进.同步路径下仅返回当前快照结果.
            return BuildTimeoutReachedResult(job, 0);
        }

        /// <summary>
        /// 创建 waitFor 等待任务.
        /// </summary>
        internal static WaitForJob CreateJob(JsonData rawParams, DateTime startedAtUtc)
        {
            if (rawParams == null || !rawParams.IsObject)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": params must be an object");
            }

            ValidateAllowedFields(rawParams);
            CommandParams parameters = new CommandParams(rawParams);
            float waitSeconds = ParseRequiredWaitSeconds(rawParams);
            string nameContains = NormalizeContains(parameters.GetString("nameContains", string.Empty));
            string textContains = NormalizeContains(parameters.GetString("textContains", string.Empty));
            return new WaitForJob(waitSeconds, nameContains, textContains, startedAtUtc);
        }

        /// <summary>
        /// 跨帧推进 waitFor 任务.
        /// </summary>
        internal static bool TryComplete(WaitForJob job, DateTime nowUtc, out JsonData result)
        {
            result = null;

            JsonData matchedElement;
            string matchedBy;
            if (TryFindMatchedElement(job.nameContains, job.textContains, out matchedElement, out matchedBy))
            {
                result = BuildMatchedResult(job, nowUtc, matchedBy, matchedElement);
                return true;
            }

            int elapsedMs = (int)(nowUtc - job.startedAtUtc).TotalMilliseconds;
            if (elapsedMs < (int)(job.waitSeconds * 1000f))
            {
                return false;
            }

            result = BuildTimeoutReachedResult(job, elapsedMs);
            return true;
        }

        private static void ValidateAllowedFields(JsonData rawParams)
        {
            foreach (DictionaryEntry entry in (IDictionary)rawParams)
            {
                string key = entry.Key as string;
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                if (string.Equals(key, "waitSeconds", StringComparison.Ordinal) ||
                    string.Equals(key, "nameContains", StringComparison.Ordinal) ||
                    string.Equals(key, "textContains", StringComparison.Ordinal))
                {
                    continue;
                }

                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": unsupported field " + key);
            }
        }

        private static float ParseRequiredWaitSeconds(JsonData rawParams)
        {
            if (rawParams == null || !rawParams.IsObject || !rawParams.ContainsKey("waitSeconds"))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": waitSeconds is required");
            }

            float waitSeconds = ParseFloatValue(rawParams["waitSeconds"], "waitSeconds");
            if (waitSeconds <= 0f)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": waitSeconds must be > 0");
            }

            return waitSeconds;
        }

        private static string NormalizeContains(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static JsonData BuildMatchedResult(WaitForJob job, DateTime nowUtc, string matchedBy, JsonData matchedElement)
        {
            JsonData matchedResult = new JsonData();
            matchedResult["waitOutcome"] = "matched";
            matchedResult["matchedBy"] = matchedBy;
            matchedResult["matchedElement"] = matchedElement;
            matchedResult["elapsedMs"] = (int)(nowUtc - job.startedAtUtc).TotalMilliseconds;
            matchedResult["waitSeconds"] = job.waitSeconds;
            return matchedResult;
        }

        private static JsonData BuildTimeoutReachedResult(WaitForJob job, int elapsedMs)
        {
            JsonData timeoutResult = new JsonData();
            timeoutResult["waitOutcome"] = "timeoutReached";
            timeoutResult["elapsedMs"] = elapsedMs;
            timeoutResult["waitSeconds"] = job.waitSeconds;
            return timeoutResult;
        }

        private static bool TryFindMatchedElement(string nameContains, string textContains, out JsonData matchedElement, out string matchedBy)
        {
            matchedElement = null;
            matchedBy = string.Empty;

            bool hasNameCondition = !string.IsNullOrEmpty(nameContains);
            bool hasTextCondition = !string.IsNullOrEmpty(textContains);
            if (!hasNameCondition && !hasTextCondition)
            {
                return false;
            }

            Selectable[] selectables = Selectable.allSelectablesArray;
            for (int i = 0; i < selectables.Length; i++)
            {
                Selectable selectable = selectables[i];
                if (selectable == null || selectable.gameObject == null)
                {
                    continue;
                }

                GameObject target = selectable.gameObject;
                if (!PlayModeUIQueryUtils.IsVisible(target))
                {
                    continue;
                }

                bool nameMatched = hasNameCondition && target.name.IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) >= 0;
                string resolvedText = hasTextCondition ? PlayModeUIQueryUtils.ResolveText(target) : string.Empty;
                bool textMatched = hasTextCondition && resolvedText.IndexOf(textContains, StringComparison.OrdinalIgnoreCase) >= 0;

                if (!nameMatched && !textMatched)
                {
                    continue;
                }

                JsonData resolvedTarget = PlayModeUIQueryUtils.BuildResolvedTarget(target);
                resolvedTarget["name"] = target.name;
                resolvedTarget["text"] = resolvedText ?? string.Empty;
                matchedElement = resolvedTarget;

                if (nameMatched && textMatched)
                {
                    matchedBy = "nameContains+textContains";
                }
                else if (nameMatched)
                {
                    matchedBy = "nameContains";
                }
                else
                {
                    matchedBy = "textContains";
                }

                return true;
            }

            return false;
        }

        private static float ParseFloatValue(JsonData value, string field)
        {
            if (value == null)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": invalid " + field);
            }

            if (value.IsInt)
            {
                return (int)value;
            }

            if (value.IsLong)
            {
                return (long)value;
            }

            if (value.IsDouble)
            {
                return (float)(double)value;
            }

            if (value.IsString && float.TryParse(value.ToString(), out float parsed))
            {
                return parsed;
            }

            throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": invalid " + field);
        }
    }
}
