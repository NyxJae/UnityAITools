using System;
using System.Collections.Generic;
using System.IO;
using UnityAgentSkills.SkillsExporter;
using UnityEditor;
using UnityEngine;

namespace UnityAgentSkills.UI.Tabs
{
    /// <summary>
    /// 技能导出tab页面.
    /// </summary>
    public class SkillsExportTab : UI.Components.ITabContent
    {
        private string _exportPath = string.Empty;
        private Dictionary<string, bool> _skillSelections = new Dictionary<string, bool>();
        private List<SkillConfig> _availableSkills = new List<SkillConfig>();
        private Vector2 _scrollPosition;

        /// <summary>
        /// Tab显示名称.
        /// </summary>
        public string TabName => "技能导出";

        /// <summary>
        /// 初始化tab,加载技能配置.
        /// </summary>
        public void Initialize()
        {
            _exportPath = EditorPrefs.GetString(GetEditorPrefsKey(), string.Empty);
            _availableSkills = new List<SkillConfig>(SkillConfigsRegistry.GetAllSkills());
            
            _skillSelections.Clear();
            foreach (var skill in _availableSkills)
            {
                _skillSelections[skill.Name] = true;
            }
        }

        /// <summary>
        /// 绘制tab内容.
        /// </summary>
        public void OnGUI()
        {
            GUILayout.Label("Unity Skills Exporter", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            DrawExportPathSection();
            EditorGUILayout.Space();

            DrawSelectAllSection();
            EditorGUILayout.Space();

            DrawSkillsList();
            EditorGUILayout.Space();

            DrawExportButton();
        }

        private void DrawExportPathSection()
        {
            EditorGUILayout.LabelField("导出路径", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            _exportPath = EditorGUILayout.TextField(_exportPath);
            if (GUILayout.Button("浏览", GUILayout.Width(60)))
            {
                string selectedPath = EditorUtility.OpenFolderPanel("选择导出路径", _exportPath, string.Empty);
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    _exportPath = selectedPath;
                    EditorPrefs.SetString(GetEditorPrefsKey(), _exportPath);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox("请选择形如\"~/.claude/\"的路径", MessageType.Info);

            if (string.IsNullOrEmpty(_exportPath))
            {
                EditorGUILayout.HelpBox("请选择导出路径", MessageType.Warning);
            }
        }

        private void DrawSelectAllSection()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("全选"))
            {
                foreach (var skillName in _skillSelections.Keys)
                {
                    _skillSelections[skillName] = true;
                }
            }
            if (GUILayout.Button("取消全选"))
            {
                foreach (var skillName in _skillSelections.Keys)
                {
                    _skillSelections[skillName] = false;
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSkillsList()
        {
            EditorGUILayout.LabelField("可用技能", EditorStyles.boldLabel);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            foreach (var skill in _availableSkills)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                _skillSelections[skill.Name] = EditorGUILayout.ToggleLeft(skill.Name, _skillSelections[skill.Name], EditorStyles.boldLabel);
                EditorGUILayout.LabelField(skill.Description, EditorStyles.wordWrappedLabel);
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawExportButton()
        {
            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(_exportPath));
            if (GUILayout.Button("导出选中的技能", GUILayout.Height(30)))
            {
                ExportSelectedSkills();
            }
            EditorGUI.EndDisabledGroup();
        }

        private void ExportSelectedSkills()
        {
            if (string.IsNullOrEmpty(_exportPath))
            {
                EditorUtility.DisplayDialog("错误", "请先选择导出路径", "确定");
                return;
            }

            if (!Directory.Exists(_exportPath))
            {
                Directory.CreateDirectory(_exportPath);
            }

            string skillsDir = Path.Combine(_exportPath, "skills");
            if (!Directory.Exists(skillsDir))
            {
                Directory.CreateDirectory(skillsDir);
            }

            int exportedCount = 0;
            int skippedCount = 0;

            string pythonScriptTemplate = SkillConfigsRegistry.GetPythonScriptTemplate();
            string agentCommandsDir = Path.GetFullPath(Path.Combine(Application.dataPath, "UnityAgentSkills"));
            string pythonScript = pythonScriptTemplate.Replace("{AGENT_COMMANDS_DATA_DIR}", agentCommandsDir);

            foreach (var skill in _availableSkills)
            {
                if (!_skillSelections[skill.Name])
                {
                    skippedCount++;
                    continue;
                }

                try
                {
                    ExportSkill(skill, skillsDir, pythonScript);
                    exportedCount++;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"导出技能 {skill.Name} 失败: {ex.Message}");
                    EditorUtility.DisplayDialog("导出失败", $"技能 {skill.Name} 导出失败:\n{ex.Message}", "确定");
                }
            }

            string message = $"导出完成!\n成功: {exportedCount} 个\n跳过: {skippedCount} 个";
            EditorUtility.DisplayDialog("导出完成", message, "确定");
        }

        private void ExportSkill(SkillConfig skill, string skillsDir, string pythonScript)
        {
            string skillDir = Path.Combine(skillsDir, skill.Name);
            if (Directory.Exists(skillDir))
            {
                Directory.Delete(skillDir, recursive: true);
            }
            Directory.CreateDirectory(skillDir);

            string skillMarkdownPath = Path.Combine(skillDir, "SKILL.md");
            File.WriteAllText(skillMarkdownPath, skill.Markdown, System.Text.Encoding.UTF8);

            string scriptsDir = Path.Combine(skillDir, "scripts");
            if (!Directory.Exists(scriptsDir))
            {
                Directory.CreateDirectory(scriptsDir);
            }

            string pythonScriptPath = Path.Combine(scriptsDir, "execute_unity_command.py");
            File.WriteAllText(pythonScriptPath, pythonScript, System.Text.Encoding.UTF8);
        }

        private string GetEditorPrefsKey()
        {
            string projectPath = Directory.GetParent(Application.dataPath).FullName;
            return $"UnitySkillsExporter.ExportPath.{projectPath}";
        }
    }
}
