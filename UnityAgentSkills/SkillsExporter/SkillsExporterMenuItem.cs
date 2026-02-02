using UnityEditor;
using UnityAgentSkills.UI;

namespace UnityAgentSkills.SkillsExporter
{
    /// <summary>
    /// Unity技能导出器菜单项.
    /// 注意: 菜单功能已迁移到UI.SkillsExporterWindow,此文件保留用于向后兼容.
    /// MenuItem已移至UI.SkillsExporterWindow,避免重复注册.
    /// </summary>
    public static class SkillsExporterMenuItem
    {
        private const string MenuPath = "Tools/Unity-skills";

        static SkillsExporterMenuItem()
        {
            // 静态构造函数,确保编辑器加载时初始化
        }

        // MenuItem已移至UI.SkillsExporterWindow.ShowWindow()
        // 此处保留用于向后兼容的引用
        internal static void OpenExporterWindow()
        {
            SkillsExporterWindow.ShowWindow();
        }
    }
}
