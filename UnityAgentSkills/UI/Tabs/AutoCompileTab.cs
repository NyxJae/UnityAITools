using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityAgentSkills.AutoCompile;

namespace UnityAgentSkills.UI.Tabs
{
    /// <summary>
    /// AutoCompile配置tab页面.
    /// </summary>
    public class AutoCompileTab : UI.Components.ITabContent
    {
        private Vector2 _pathListScroll;
        private AutoCompileConfig _cachedConfig;

        /// <summary>
        /// Tab显示名称.
        /// </summary>
        public string TabName => "AutoCompile";

        /// <summary>
        /// 绘制AutoCompile配置界面.
        /// </summary>
        public void OnGUI()
        {
            GUILayout.Label("AutoCompile 配置", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // 使用缓存的配置,避免每帧从EditorPrefs读取
            if (_cachedConfig == null)
            {
                _cachedConfig = AutoCompileConfigProvider.LoadConfig();
            }

            EditorGUI.BeginChangeCheck();

            _cachedConfig.IsEnabled = EditorGUILayout.Toggle("启用自动编译", _cachedConfig.IsEnabled);
            EditorGUILayout.Space();

            _cachedConfig.DebounceInterval = EditorGUILayout.IntSlider(
                new GUIContent("防抖间隔 (毫秒)", "文件变更后等待多久再触发编译,避免频繁编译"),
                _cachedConfig.DebounceInterval,
                200,
                5000
            );
            EditorGUILayout.LabelField($"当前值: {_cachedConfig.DebounceInterval} 毫秒", EditorStyles.miniLabel);
            EditorGUILayout.Space();

            DrawWatchPathsSection(_cachedConfig);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox($"当前状态: {AutoCompileController.CurrentStatus}", MessageType.Info);

            if (!AutoCompileConfigProvider.ValidateConfig(_cachedConfig, out string errorMessage))
            {
                EditorGUILayout.HelpBox($"配置错误: {errorMessage}", MessageType.Error);
            }

            EditorGUILayout.Space();

            DrawActionButtons(_cachedConfig);

            if (EditorGUI.EndChangeCheck())
            {
                if (AutoCompileConfigProvider.ValidateConfig(_cachedConfig, out string error))
                {
                    ApplyConfig(_cachedConfig);
                }
            }
        }

        private void DrawWatchPathsSection(AutoCompileConfig config)
        {
            EditorGUILayout.LabelField("监听路径列表", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("添加要监听的文件夹路径,支持任意绝对路径或相对路径", MessageType.Info);

            _pathListScroll = EditorGUILayout.BeginScrollView(_pathListScroll, GUILayout.Height(100));
            
            List<int> indicesToRemove = new List<int>();
            string pathToBrowse = null;
            int browseIndex = -1;

            for (int i = 0; i < config.WatchPaths.Count; i++)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                config.WatchPaths[i] = EditorGUILayout.TextField(config.WatchPaths[i]);

                if (GUILayout.Button("浏览", GUILayout.Width(50)))
                {
                    pathToBrowse = config.WatchPaths[i];
                    browseIndex = i;
                }

                if (GUILayout.Button("删除", GUILayout.Width(50)))
                {
                    indicesToRemove.Add(i);
                }

                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndScrollView();

            for (int i = indicesToRemove.Count - 1; i >= 0; i--)
            {
                config.WatchPaths.RemoveAt(indicesToRemove[i]);
            }

            if (browseIndex >= 0 && browseIndex < config.WatchPaths.Count)
            {
                string fullPath = string.IsNullOrEmpty(pathToBrowse) ? 
                    Path.GetFullPath(Path.Combine(Application.dataPath, "..")) : 
                    GetFullPath(pathToBrowse);
                string selectedPath = EditorUtility.OpenFolderPanel("选择监听路径", fullPath, string.Empty);
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    config.WatchPaths[browseIndex] = selectedPath;
                }
            }

            if (GUILayout.Button("添加路径"))
            {
                config.WatchPaths.Add(string.Empty);
            }
        }

        private void DrawActionButtons(AutoCompileConfig config)
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginDisabledGroup(!AutoCompileConfigProvider.ValidateConfig(config, out _));
            if (GUILayout.Button("应用配置", GUILayout.Height(30)))
            {
                ApplyConfig(config);
            }
            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button("恢复默认", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("确认", "确定要恢复默认配置吗?", "确定", "取消"))
                {
                    config = new AutoCompileConfig();
                    ApplyConfig(config);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void ApplyConfig(AutoCompileConfig config)
        {
            if (config == null)
            {
                Debug.LogWarning("[AutoCompileTab] 尝试应用空配置");
                return;
            }

            AutoCompileConfigProvider.SaveConfig(config);
            AutoCompileService.Restart();
        }

        private string GetFullPath(string relativePath)
        {
            return UnityAgentSkills.Core.PathUtils.GetFullPath(relativePath);
        }
    }
}
