using LitJson2_utf;
using UnityAgentSkills.Core;
using UnityAgentSkills.Plugins.PrefabBridge.Utils;
using UnityAgentSkills.Utils.JsonBuilders;
using UnityEditor;

namespace UnityAgentSkills.Plugins.PrefabBridge.Handlers
{
    /// <summary>
    /// prefabBridge.unpackInstance 命令处理器.
    /// </summary>
    internal static class PrefabBridgeUnpackInstanceHandler
    {
        /// <summary>
        /// 支持的命令类型.
        /// </summary>
        public const string CommandType = "prefabBridge.unpackInstance";

        /// <summary>
        /// 执行命令.
        /// </summary>
        public static JsonData Execute(JsonData rawParams)
        {
            CommandParams parameters = new CommandParams(rawParams);
            string sceneName = parameters.GetString("sceneName", null);
            string objectPath = parameters.GetString("objectPath", null);
            int siblingIndex = parameters.GetInt("siblingIndex", 0);
            string unpackModeText = parameters.GetString("unpackMode", "outermost");

            var context = PrefabBridgeCommon.ResolvePrefabInstanceContextOrThrow(sceneName, objectPath, siblingIndex);
            if (!context.IsPrefabInstance)
            {
                throw new System.InvalidOperationException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": 该对象不是 prefab instance,无法 unpack");
            }

            PrefabUnpackMode mode;
            if (unpackModeText == "outermost")
            {
                mode = PrefabUnpackMode.OutermostRoot;
            }
            else if (unpackModeText == "completely")
            {
                mode = PrefabUnpackMode.Completely;
            }
            else
            {
                throw new System.ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": unpackMode must be outermost or completely");
            }

            PrefabUtility.UnpackPrefabInstance(context.InstanceRoot, mode, InteractionMode.AutomatedAction);
            bool saved = PrefabBridgeCommon.SaveScene(context.Scene);
            JsonData result = JsonResultBuilder.CreateObject();
            result["sceneName"] = sceneName;
            result["objectPath"] = objectPath;
            result["siblingIndex"] = siblingIndex;
            result["unpackMode"] = unpackModeText;
            result["isPrefabInstance"] = false;
            result["saved"] = saved;
            return result;
        }
    }
}
