using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityAgentSkills.Core;

namespace UnityAgentSkills.AutoCompile
{
    /// <summary>
    /// AutoCompile 配置提供者,负责从 EditorPrefs 读写配置.
    /// </summary>
    internal static class AutoCompileConfigProvider
    {
        /// <summary>
        /// 获取 EditorPrefs 存储键.
        /// 格式: UnityAgentSkills.AutoCompile.{项目路径}
        /// </summary>
        private static string GetEditorPrefsKey()
        {
            string projectPath = Directory.GetParent(Application.dataPath).FullName;
            return $"UnityAgentSkills.AutoCompile.{projectPath}";
        }

        /// <summary>
        /// 从 EditorPrefs 加载配置.
        /// </summary>
        /// <returns>配置对象,如果不存在则返回默认配置.</returns>
        public static AutoCompileConfig LoadConfig()
        {
            string key = GetEditorPrefsKey();
            string json = EditorPrefs.GetString(key, string.Empty);

            if (string.IsNullOrEmpty(json))
            {
                // 返回默认配置
                return new AutoCompileConfig();
            }

            try
            {
                return JsonUtility.FromJson<AutoCompileConfig>(json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AutoCompile] 加载配置失败: {ex.Message}, 使用默认配置");
                return new AutoCompileConfig();
            }
        }

        /// <summary>
        /// 保存配置到 EditorPrefs.
        /// </summary>
        /// <param name="config">要保存的配置对象.</param>
        public static void SaveConfig(AutoCompileConfig config)
        {
            if (config == null)
            {
                Debug.LogWarning("[AutoCompile] 尝试保存空配置");
                return;
            }

            string key = GetEditorPrefsKey();
            string json = JsonUtility.ToJson(config, true);
            EditorPrefs.SetString(key, json);
        }

        /// <summary>
        /// 验证配置是否有效.
        /// </summary>
        /// <param name="config">要验证的配置.</param>
        /// <param name="errorMessage">输出错误信息.</param>
        /// <returns>配置是否有效.</returns>
        public static bool ValidateConfig(AutoCompileConfig config, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (config == null)
            {
                errorMessage = "配置对象为空";
                return false;
            }

            // 验证防抖间隔范围
            if (config.DebounceInterval < 200 || config.DebounceInterval > 5000)
            {
                errorMessage = $"防抖间隔必须在 200-5000 毫秒之间,当前值: {config.DebounceInterval}";
                return false;
            }

            // 验证监听路径列表
            if (config.WatchPaths == null || config.WatchPaths.Count == 0)
            {
                errorMessage = "监听路径列表不能为空";
                return false;
            }

            // 检查是否有至少一个有效路径
            int validPathCount = 0;
            foreach (var watchPath in config.WatchPaths)
            {
                if (string.IsNullOrWhiteSpace(watchPath))
                {
                    continue;
                }

                string fullPath = PathUtils.GetFullPath(watchPath);
                if (Directory.Exists(fullPath))
                {
                    validPathCount++;
                }
            }

            if (validPathCount == 0)
            {
                errorMessage = "没有有效的监听路径,请至少添加一个存在的目录";
                return false;
            }

            return true;
        }
    }
}
