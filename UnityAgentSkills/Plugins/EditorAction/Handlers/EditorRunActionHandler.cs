using System;
using System.Reflection;
using UnityAgentSkills.Core;
using UnityAgentSkills.Plugins.EditorAction.Catalog;
using UnityAgentSkills.Plugins.EditorAction.Execution;
using UnityAgentSkills.Plugins.EditorAction.Security;
using LitJson2_utf;

namespace UnityAgentSkills.Plugins.EditorAction.Handlers
{
    /// <summary>
    /// editor.runAction 命令处理器.
    /// </summary>
    internal static class EditorRunActionHandler
    {
        /// <summary>
        /// 命令类型.
        /// </summary>
        public const string CommandType = "editor.runAction";

        /// <summary>
        /// 执行 editor.runAction 命令.
        /// </summary>
        /// <param name="rawParams">命令参数.</param>
        /// <returns>执行结果.</returns>
        public static JsonData Execute(JsonData rawParams)
        {
            CommandParams parameters = new CommandParams(rawParams);
            string actionId = parameters.GetString("actionId", null);

            if (string.IsNullOrEmpty(actionId))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": actionId is required");
            }

            if (!IsFullQualifiedActionId(actionId))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": actionId must be Namespace.ClassName.MethodName");
            }

            if (EditorActionSafetyPolicy.IsBlocked(actionId, out string blockedReason))
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.RuntimeError + ": " + blockedReason + " (" + actionId + ")");
            }

            JsonData actionArgs = null;
            if (parameters.Has("actionArgs"))
            {
                JsonData data = parameters.GetData();
                actionArgs = data != null && data.IsObject ? data["actionArgs"] : null;
                if (actionArgs != null && !actionArgs.IsObject)
                {
                    throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": actionArgs must be object");
                }
            }

            if (!EditorActionCatalog.TryGet(actionId, out EditorActionDescriptor descriptor))
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.RuntimeError + ": actionId 不存在或不可调用: " + actionId);
            }

            try
            {
                return EditorActionInvoker.Invoke(descriptor, actionArgs);
            }
            catch (TargetInvocationException ex)
            {
                Exception inner = ex.InnerException ?? ex;
                if (inner is ArgumentException)
                {
                    throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": " + inner.Message);
                }

                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.RuntimeError + ": action invoke failed: " + inner.Message);
            }
        }

        /// <summary>
        /// 检查 actionId 是否为完整限定名格式.
        /// </summary>
        /// <param name="actionId">动作标识.</param>
        /// <returns>是否满足 Namespace.ClassName.MethodName.</returns>
        private static bool IsFullQualifiedActionId(string actionId)
        {
            if (string.IsNullOrEmpty(actionId))
            {
                return false;
            }

            int firstDot = actionId.IndexOf('.');
            int lastDot = actionId.LastIndexOf('.');
            if (firstDot <= 0 || lastDot <= firstDot || lastDot >= actionId.Length - 1)
            {
                return false;
            }

            return true;
        }
    }
}
