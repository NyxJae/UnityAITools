using System;
using System.Collections.Generic;

namespace UnityAgentSkills.Plugins.EditorAction.Security
{
    /// <summary>
    /// EditorAction 安全策略.
    /// 提供高风险 actionId 黑名单拦截规则.
    /// </summary>
    internal static class EditorActionSafetyPolicy
    {
        private static readonly HashSet<string> BlockedActionIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "UnityEditor.AssetDatabase.DeleteAsset",
            "UnityEditor.FileUtil.DeleteFileOrDirectory",
            "Project.Tools.EditorOps.ClearDirectory",
            "Project.Tools.EditorOps.ForceOverwriteImport",
            "Project.Tools.EditorOps.OverwriteGlobalConfig"
        };

        /// <summary>
        /// 判断 actionId 是否命中高风险黑名单.
        /// </summary>
        /// <param name="actionId">动作标识.</param>
        /// <param name="reason">拦截原因.</param>
        /// <returns>是否应拦截.</returns>
        public static bool IsBlocked(string actionId, out string reason)
        {
            reason = null;
            if (string.IsNullOrEmpty(actionId))
            {
                return false;
            }

            if (!BlockedActionIds.Contains(actionId))
            {
                return false;
            }

            reason = "命中高风险排除项";
            return true;
        }
    }
}
