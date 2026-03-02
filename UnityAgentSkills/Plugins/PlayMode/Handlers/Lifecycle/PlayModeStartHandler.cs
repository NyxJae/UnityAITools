using System;
using UnityAgentSkills.Core;
using LitJson2_utf;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace UnityAgentSkills.Plugins.PlayMode.Handlers
{
    /// <summary>
    /// playmode.start 命令处理器.
    /// </summary>
    internal static class PlayModeStartHandler
    {
        /// <summary>
        /// 命令类型.
        /// </summary>
        public const string CommandType = "playmode.start";

        /// <summary>
        /// 执行启动 Play Mode.
        /// </summary>
        public static JsonData Execute(JsonData rawParams)
        {
            CommandParams parameters = new CommandParams(rawParams);
            string entryScene = parameters.GetString("entryScene", null);

            if (!string.IsNullOrEmpty(entryScene))
            {
                if (!entryScene.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                    !entryScene.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": entryScene must start with Assets/ and end with .unity");
                }

                SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(entryScene);
                if (sceneAsset == null)
                {
                    throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.PlayModeStartFailed + ": Scene file not found");
                }

                EditorSceneManager.OpenScene(entryScene, OpenSceneMode.Single);
            }

            PlayModeSession.Start();

            JsonData result = new JsonData();
            result["sessionState"] = PlayModeSession.State.ToString();
            if (!string.IsNullOrEmpty(entryScene))
            {
                result["entryScene"] = entryScene;
            }

            return result;
        }
    }
}
