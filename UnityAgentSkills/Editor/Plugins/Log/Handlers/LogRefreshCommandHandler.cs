using System;
using System.Collections;
using LitJson2_utf;
using UnityAgentSkills.Core;
using UnityEditor;

namespace UnityAgentSkills.Plugins.Log.Handlers
{
    /// <summary>
    /// log.refresh 命令处理器.
    /// </summary>
    internal static class LogRefreshCommandHandler
    {
        /// <summary>
        /// 支持的命令类型.
        /// </summary>
        public const string CommandType = "log.refresh";

        /// <summary>
        /// 刷新等待任务.
        /// </summary>
        internal readonly struct RefreshJob
        {
            /// <summary>
            /// 本次任务是否已显式触发刷新.
            /// </summary>
            public readonly bool refreshTriggered;

            /// <summary>
            /// 创建任务时 Unity 是否已处于编译中.
            /// </summary>
            public readonly bool compilingObservedAtStart;

            public RefreshJob(bool refreshTriggered, bool compilingObservedAtStart)
            {
                this.refreshTriggered = refreshTriggered;
                this.compilingObservedAtStart = compilingObservedAtStart;
            }
        }

        /// <summary>
        /// 同步入口仅用于参数校验与立即触发刷新.
        /// 完整成功语义依赖批处理层的跨帧等待闭环,因此这里不直接返回伪成功结果.
        /// </summary>
        public static JsonData Execute(JsonData rawParams)
        {
            RefreshJob job = CreateJob(rawParams);
            BeginRefresh(job);
            return null;
        }

        /// <summary>
        /// 校验参数并创建刷新任务.
        /// </summary>
        internal static RefreshJob CreateJob(JsonData rawParams)
        {
            ValidateAllowedFields(rawParams);
            return new RefreshJob(refreshTriggered: true, compilingObservedAtStart: EditorApplication.isCompiling);
        }

        /// <summary>
        /// 显式触发一次 Unity 刷新.
        /// </summary>
        internal static void BeginRefresh(RefreshJob job)
        {
            if (!job.refreshTriggered)
            {
                throw new InvalidOperationException("Refresh job is not initialized.");
            }

            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 跨帧轮询刷新是否完成.
        /// 返回 true 表示命令已完成(成功或失败),false 表示继续等待.
        /// </summary>
        internal static bool TryComplete(RefreshJob job, DateTime now, DateTime commandStartedAt, int timeoutMs, bool compilationOccurred, out JsonData resultData, out CommandError error)
        {
            resultData = null;
            error = null;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                int elapsedMs = (int)(now - commandStartedAt).TotalMilliseconds;
                if (elapsedMs > timeoutMs)
                {
                    error = new CommandError
                    {
                        code = UnityAgentSkillCommandErrorCodes.Timeout,
                        message = "等待 Unity 刷新完成超时",
                        detail = "已触发 Unity 刷新,但在 batch timeout 内未等到 Unity 完成刷新或编译. 已等待 " + elapsedMs + "ms,限制 " + timeoutMs + "ms"
                    };
                    return true;
                }

                return false;
            }

            resultData = BuildSuccessResult(job, compilationOccurred);
            return true;
        }

        /// <summary>
        /// 构建成功结果.
        /// </summary>
        internal static JsonData BuildSuccessResult(RefreshJob job, bool compilationOccurred)
        {
            JsonData result = new JsonData();
            result["summary"] = compilationOccurred
                ? "已完成 Unity 刷新,并且这次过程中发生了编译."
                : "已完成 Unity 刷新,这次没有触发编译.";
            result["refreshTriggered"] = job.refreshTriggered;
            result["compilationOccurred"] = compilationOccurred;
            return result;
        }

        /// <summary>
        /// 校验只允许空对象参数.
        /// </summary>
        private static void ValidateAllowedFields(JsonData rawParams)
        {
            if (rawParams == null || !rawParams.IsObject)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": params must be an object");
            }

            foreach (DictionaryEntry entry in (IDictionary)rawParams)
            {
                string key = entry.Key as string;
                if (!string.IsNullOrEmpty(key))
                {
                    throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": unsupported field: " + key);
                }
            }
        }
    }
}
