using System;
using UnityAgentSkills.Core;
using UnityAgentSkills.Utils;
using UnityAgentSkills.Utils.JsonBuilders;
using LitJson2_utf;
using UnityEditor;
using UnityEngine;

namespace UnityAgentSkills.Plugins.Prefab.Handlers
{
    /// <summary>
    /// prefab.deleteComponent命令处理器.
    /// </summary>
    internal static class PrefabDeleteComponentHandler
    {
        /// <summary>
        /// 支持的命令类型.
        /// </summary>
        public const string CommandType = "prefab.deleteComponent";

        /// <summary>
        /// 执行删除组件命令.
        /// </summary>
        public static JsonData Execute(JsonData rawParams)
        {
            CommandParams parameters = new CommandParams(rawParams);

            string prefabPath = PrefabComponentHandlerUtils.NormalizePrefabPath(parameters.GetString("prefabPath", null));
            string objectPath = parameters.GetString("objectPath", null);
            int siblingIndex = parameters.GetInt("siblingIndex", 0);
            string componentTypeName = parameters.GetString("componentType", null);
            int componentIndex = parameters.GetInt("componentIndex", 0);

            GameObject prefabRoot = PrefabComponentHandlerUtils.LoadPrefabContentsOrThrow(prefabPath);
            try
            {
                GameObject target = PrefabComponentHandlerUtils.FindGameObjectOrThrow(prefabRoot, objectPath, siblingIndex, "objectPath");
                Type componentType = PrefabComponentHandlerUtils.ResolveComponentTypeOrThrow(componentTypeName);
                Component component = PrefabComponentHandlerUtils.FindComponentOrThrow(target, componentType, componentIndex);

                if (component is Transform)
                {
                    throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.CannotDeleteRequiredComponent + ": 不能删除Transform/RectTransform组件");
                }

                int deletedComponentInstanceID = component.GetInstanceID();
                string deletedComponentType = component.GetType().FullName;

                Undo.DestroyObjectImmediate(component);
                bool saved = PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);

                JsonData result = JsonResultBuilder.CreateObject();
                result["prefabPath"] = prefabPath;
                result["objectPath"] = objectPath;
                result["instanceID"] = target.GetInstanceID();
                result["deletedComponentType"] = deletedComponentType;
                result["deletedComponentIndex"] = componentIndex;
                result["deletedComponentInstanceID"] = deletedComponentInstanceID;
                result["saved"] = saved;
                return result;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
    }
}
