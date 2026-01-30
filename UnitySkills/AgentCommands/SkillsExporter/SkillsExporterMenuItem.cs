using UnityEditor;

namespace AgentCommands.SkillsExporter
{
    /// <summary>
    /// Unity技能导出器菜单项.
    /// </summary>
    public static class SkillsExporterMenuItem
    {
        private const string MenuPath = "Tools/Unity-skills";

        static SkillsExporterMenuItem()
        {
            // 静态构造函数,确保编辑器加载时初始化
        }

        [MenuItem(MenuPath)]
        private static void OpenExporterWindow()
        {
            SkillsExporterWindow window = EditorWindow.GetWindow<SkillsExporterWindow>(false, "Unity Skills Exporter", true);
            window.Show();
        }
    }
}
