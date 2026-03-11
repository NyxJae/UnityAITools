using System;
using System.Collections.Generic;
using UnityAgentSkills.Core;

namespace UnityAgentSkills.Plugins.EditorControl.Security
{
    /// <summary>
    /// editor 控制命令的高风险拦截策略.
    /// 当前仅收口菜单执行黑名单,避免把 menu 语义混入 EditorAction 的 actionId 黑名单.
    /// </summary>
    internal static class EditorControlSafetyPolicy
    {
        private static readonly HashSet<string> BlockedMenuPaths = new HashSet<string>(StringComparer.Ordinal)
        {
            "Assets/Delete",
            "Edit/Delete",
            "Assets/Remove Unused Assets"
        };

        /// <summary>
        /// 判断菜单路径是否命中高风险黑名单.
        /// </summary>
        /// <param name="menuPath">菜单路径.</param>
        /// <param name="reason">拦截原因.</param>
        /// <returns>是否应拦截.</returns>
        public static bool IsBlockedMenu(string menuPath, out string reason)
        {
            reason = null;
            if (string.IsNullOrWhiteSpace(menuPath))
            {
                return false;
            }

            if (!BlockedMenuPaths.Contains(menuPath))
            {
                return false;
            }

            reason = UnityAgentSkillCommandErrorCodes.ForbiddenEditorAction + ": 命中高风险菜单,已拒绝执行: " + menuPath;
            return true;
        }
    }
}
