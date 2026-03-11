using LitJson2_utf;
using UnityAgentSkills.Plugins.PrefabBridge.Utils;
using UnityAgentSkills.Utils.JsonBuilders;
using UnityAgentSkills.Utils;

namespace UnityAgentSkills.Plugins.PrefabBridge.Handlers
{
    /// <summary>
    /// prefabBridge.getInstanceSource 命令处理器.
    /// </summary>
    internal static class PrefabBridgeGetInstanceSourceHandler
    {
        /// <summary>
        /// 支持的命令类型.
        /// </summary>
        public const string CommandType = "prefabBridge.getInstanceSource";

        /// <summary>
        /// 执行命令.
        /// </summary>
        public static JsonData Execute(JsonData rawParams)
        {
            var parameters = new UnityAgentSkills.Core.CommandParams(rawParams);
            string sceneName = parameters.GetString("sceneName", null);
            string objectPath = parameters.GetString("objectPath", null);
            int siblingIndex = parameters.GetInt("siblingIndex", 0);

            PrefabBridgeCommon.PrefabInstanceContext context = PrefabBridgeCommon.ResolvePrefabInstanceContextOrThrow(sceneName, objectPath, siblingIndex);
            JsonData result = JsonResultBuilder.CreateObject();
            result["sceneName"] = sceneName;
            result["objectPath"] = objectPath;
            result["siblingIndex"] = siblingIndex;
            result["isPrefabInstance"] = context.IsPrefabInstance;
            result["sourcePrefabPath"] = context.IsPrefabInstance ? context.SourcePrefabPath : null;
            result["instanceRootPath"] = context.IsPrefabInstance ? GameObjectPathFinder.GetPath(context.InstanceRoot) : null;
            result["instanceKind"] = context.InstanceKind;
            return result;
        }
    }
}
