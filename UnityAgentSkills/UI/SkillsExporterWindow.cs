using UnityEditor;
using UnityEngine;
using UnityAgentSkills.UI.Components;
using UnityAgentSkills.UI.Tabs;

namespace UnityAgentSkills.UI
{
    /// <summary>
    /// Unity技能导出器主窗口 - 轻量化架构版本.
    /// </summary>
    public class SkillsExporterWindow : EditorWindow
    {
        private TabContentManager _tabManager;

        /// <summary>
        /// 显示Unity技能导出器窗口.
        /// </summary>
        [MenuItem("Tools/Unity-skills")]
        public static void ShowWindow()
        {
            var window = GetWindow<SkillsExporterWindow>();
            window.titleContent = new GUIContent("Unity Skills");
            window.minSize = new Vector2(500, 400);
        }

        private void OnEnable()
        {
            InitializeTabManager();
        }

        private void InitializeTabManager()
        {
            if (_tabManager == null)
            {
                _tabManager = new TabContentManager();
            }
            else
            {
                // 清空现有tabs并重新初始化
                _tabManager = new TabContentManager();
            }

            // 注册所有tabs
            var skillsExportTab = new SkillsExportTab();
            skillsExportTab.Initialize();
            _tabManager.RegisterTab(skillsExportTab);

            var autoCompileTab = new AutoCompileTab();
            _tabManager.RegisterTab(autoCompileTab);
        }

        private void OnGUI()
        {
            if (_tabManager == null)
            {
                InitializeTabManager();
            }

            _tabManager.DrawTabButtons();

            EditorGUILayout.Space();

            _tabManager.DrawCurrentTabContent();
        }
    }
}
