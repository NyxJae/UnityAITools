using LitJson2_utf;
using UnityAgentSkills.Core;
using UnityAgentSkills.Plugins.PrefabBridge.Utils;
using UnityAgentSkills.Utils.JsonBuilders;

namespace UnityAgentSkills.Plugins.PrefabBridge.Handlers
{
    /// <summary>
    /// prefabBridge.getOverrides 命令处理器.
    /// 基于统一 snapshot 输出全部可见 override,避免查询与 apply/revert 范围漂移.
    /// </summary>
    internal static class PrefabBridgeGetOverridesHandler
    {
        /// <summary>
        /// 支持的命令类型.
        /// </summary>
        public const string CommandType = "prefabBridge.getOverrides";

        /// <summary>
        /// 执行命令.
        /// </summary>
        /// <param name="rawParams">原始参数.</param>
        /// <returns>override 查询结果.</returns>
        public static JsonData Execute(JsonData rawParams)
        {
            CommandParams parameters = new CommandParams(rawParams);
            string sceneName = parameters.GetString("sceneName", null);
            string objectPath = parameters.GetString("objectPath", null);
            int siblingIndex = parameters.GetInt("siblingIndex", 0);

            var context = PrefabBridgeCommon.ResolvePrefabInstanceContextOrThrow(sceneName, objectPath, siblingIndex);
            if (!context.IsPrefabInstance)
            {
                JsonData notPrefabResult = JsonResultBuilder.CreateObject();
                notPrefabResult["sceneName"] = sceneName;
                notPrefabResult["objectPath"] = objectPath;
                notPrefabResult["siblingIndex"] = siblingIndex;
                notPrefabResult["isPrefabInstance"] = false;
                notPrefabResult["hasOverrides"] = false;
                notPrefabResult["overrideCount"] = 0;
                return notPrefabResult;
            }

            PrefabBridgeOverrideSnapshotBuilder.OverrideSnapshot snapshot = PrefabBridgeOverrideSnapshotBuilder.Build(context.InstanceRoot);
            JsonData result = JsonResultBuilder.CreateObject();
            result["sceneName"] = sceneName;
            result["objectPath"] = objectPath;
            result["siblingIndex"] = siblingIndex;
            result["isPrefabInstance"] = true;
            result["hasOverrides"] = snapshot.Entries.Count > 0;
            result["overrideCount"] = snapshot.Entries.Count;
            result["modifiedProperties"] = snapshot.ModifiedProperties;
            result["addedComponents"] = snapshot.AddedComponents;
            result["removedComponents"] = snapshot.RemovedComponents;
            result["addedGameObjects"] = snapshot.AddedGameObjects;
            result["removedGameObjects"] = snapshot.RemovedGameObjects;
            return result;
        }
    }
}
