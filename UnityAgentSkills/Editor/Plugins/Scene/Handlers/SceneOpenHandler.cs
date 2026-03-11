using System;
using UnityAgentSkills.Core;
using UnityAgentSkills.Utils.JsonBuilders;
using LitJson2_utf;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace UnityAgentSkills.Plugins.Scene.Handlers
{
    /// <summary>
    /// scene.open 命令处理器.
    /// 仅允许在编辑模式执行,Play 模式时直接报错不自动退出.
    /// </summary>
    internal static class SceneOpenHandler
    {
        /// <summary>
        /// 支持的命令类型.
        /// </summary>
        public const string CommandType = "scene.open";

        /// <summary>
        /// 执行场景打开命令.
        /// </summary>
        /// <param name="rawParams">命令参数 json.</param>
        /// <returns>结果 json.</returns>
        public static JsonData Execute(JsonData rawParams)
        {
            // 需求约束: 避免在运行态自动退出 Play 模式破坏运行上下文.
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    UnityAgentSkillCommandErrorCodes.OnlyAllowedInEditMode
                    + ": 当前为 Play 模式,仅编辑模式可打开场景 (OnlyAllowedInEditMode)");
            }

            CommandParams parameters = new CommandParams(rawParams);

            string scenePath = parameters.GetString("scenePath", null);
            if (string.IsNullOrEmpty(scenePath))
            {
                throw new ArgumentException(
                    UnityAgentSkillCommandErrorCodes.InvalidFields + ": scenePath is required");
            }

            if (!scenePath.StartsWith("Assets/"))
            {
                throw new ArgumentException(
                    UnityAgentSkillCommandErrorCodes.InvalidFields + ": scenePath must start with 'Assets/'");
            }

            string openModeStr = parameters.GetString("openMode", "Single");
            OpenSceneMode openMode;
            if (string.Equals(openModeStr, "Single", StringComparison.OrdinalIgnoreCase))
            {
                openMode = OpenSceneMode.Single;
            }
            else if (string.Equals(openModeStr, "Additive", StringComparison.OrdinalIgnoreCase))
            {
                openMode = OpenSceneMode.Additive;
            }
            else
            {
                throw new ArgumentException(
                    UnityAgentSkillCommandErrorCodes.InvalidFields + ": openMode must be 'Single' or 'Additive'");
            }

            string assetPath = scenePath.Replace('\\', '/');
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(assetPath);
            if (sceneAsset == null)
            {
                throw new ArgumentException(
                    UnityAgentSkillCommandErrorCodes.InvalidFields + ": scenePath not found: " + assetPath);
            }

            var scene = EditorSceneManager.OpenScene(assetPath, openMode);

            JsonData result = JsonResultBuilder.CreateObject();
            result["scenePath"] = assetPath;
            result["sceneName"] = scene.name;

            return result;
        }
    }
}
