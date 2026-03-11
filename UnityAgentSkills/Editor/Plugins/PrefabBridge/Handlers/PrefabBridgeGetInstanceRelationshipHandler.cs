using LitJson2_utf;
using UnityAgentSkills.Plugins.PrefabBridge.Utils;
using UnityAgentSkills.Utils.JsonBuilders;
using UnityAgentSkills.Utils;

namespace UnityAgentSkills.Plugins.PrefabBridge.Handlers
{
    /// <summary>
    /// prefabBridge.getInstanceRelationship 命令处理器.
    /// </summary>
    internal static class PrefabBridgeGetInstanceRelationshipHandler
    {
        /// <summary>
        /// 支持的命令类型.
        /// </summary>
        public const string CommandType = "prefabBridge.getInstanceRelationship";

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
            result["instanceKind"] = context.InstanceKind;
            result["sourcePrefabPath"] = context.SourcePrefabPath;
            result["variantBasePrefabPath"] = string.IsNullOrEmpty(context.VariantBasePrefabPath) ? null : context.VariantBasePrefabPath;
            result["outerInstanceRootPath"] = context.IsPrefabInstance ? GameObjectPathFinder.GetPath(context.InstanceRoot) : null;
            result["relationshipSummary"] = PrefabBridgeCommon.BuildRelationshipSummary(context);
            return result;
        }
    }
}
