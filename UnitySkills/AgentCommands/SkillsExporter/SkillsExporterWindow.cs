using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AgentCommands.SkillsExporter
{
    /// <summary>
    /// Unity技能导出器主窗口.
    /// </summary>
    public class SkillsExporterWindow : EditorWindow
    {
        /// <summary>
        /// 导出路径.
        /// </summary>
        private string _exportPath = string.Empty;

        /// <summary>
        /// 技能选中状态字典.
        /// </summary>
        private Dictionary<string, bool> _skillSelections = new Dictionary<string, bool>();

        /// <summary>
        /// 可用技能列表.
        /// </summary>
        private List<SkillConfig> _availableSkills = new List<SkillConfig>();

        /// <summary>
        /// 滚动位置.
        /// </summary>
        private Vector2 _scrollPosition;

        private void OnEnable()
        {
            _exportPath = EditorPrefs.GetString(GetEditorPrefsKey(), string.Empty);

            _availableSkills = new List<SkillConfig>(SkillConfigsRegistry.GetAllSkills());

            _skillSelections.Clear();
            foreach (var skill in _availableSkills)
            {
                _skillSelections[skill.Name] = true;
            }
        }

        private void OnGUI()
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

        /// <summary>
        /// 绘制导出路径选择区域.
        /// </summary>
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

        /// <summary>
        /// 绘制全选/取消全选区域.
        /// </summary>
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

        /// <summary>
        /// 绘制技能列表.
        /// </summary>
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

        /// <summary>
        /// 绘制导出按钮.
        /// </summary>
        private void DrawExportButton()
        {
            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(_exportPath));
            if (GUILayout.Button("导出选中的技能", GUILayout.Height(30)))
            {
                ExportSelectedSkills();
            }
            EditorGUI.EndDisabledGroup();
        }

        /// <summary>
        /// 导出选中的技能.
        /// </summary>
        private void ExportSelectedSkills()
        {
            if (string.IsNullOrEmpty(_exportPath))
            {
                EditorUtility.DisplayDialog("错误", "请先选择导出路径", "确定");
                return;
            }

            // 检查导出路径是否存在,不存在则直接创建
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

            string agentCommandsDir = Path.GetFullPath(Path.Combine(Application.dataPath, "AgentCommands"));
            // Python使用原始字符串r''，无需转义反斜杠，跨平台兼容
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

            // 不必打开
            // EditorUtility.RevealInFinder(skillsDir);
        }

        /// <summary>
        /// 导出单个技能.
        /// </summary>
        private void ExportSkill(SkillConfig skill, string skillsDir, string pythonScript)
        {
            // 创建技能文件夹,如果已存在则先删除整个目录
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

            Debug.Log($"已导出技能: {skill.Name} -> {skillDir}");
        }

        /// <summary>
        /// 获取EditorPrefs的key,基于项目路径.
        /// </summary>
        private string GetEditorPrefsKey()
        {
            string projectPath = System.IO.Directory.GetParent(Application.dataPath).FullName;
            return $"UnitySkillsExporter.ExportPath.{projectPath}";
        }
    }
}
