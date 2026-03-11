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
    /// prefab.setSiblingIndex命令处理器.
    /// </summary>
    internal static class PrefabSetSiblingIndexHandler
    {
        /// <summary>
        /// 支持的命令类型.
        /// </summary>
        public const string CommandType = "prefab.setSiblingIndex";

        /// <summary>
        /// 执行GameObject排序命令.
        /// </summary>
        public static JsonData Execute(JsonData @params)
        {
            CommandParams parameters = new CommandParams(@params);

            string prefabPath = PrefabComponentHandlerUtils.NormalizePrefabPath(parameters.GetString("prefabPath", null));
            PrefabComponentHandlerUtils.ValidatePrefabPathOrThrow(prefabPath);

            string objectPath = parameters.GetString("objectPath", null);
            int siblingIndex = parameters.GetInt("siblingIndex", 0);
            int newSiblingIndexRequested = parameters.GetInt("newSiblingIndex");

            if (siblingIndex < 0)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": siblingIndex must be >= 0");
            }

            if (newSiblingIndexRequested < 0)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": newSiblingIndex must be >= 0");
            }

            GameObject prefabRoot = PrefabComponentHandlerUtils.LoadPrefabContentsOrThrow(prefabPath);
            try
            {
                if (string.Equals(objectPath, prefabRoot.name, StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        UnityAgentSkillCommandErrorCodes.InvalidFields
                        + ": 不允许编辑预制体根节点: objectPath="
                        + objectPath
                        + ",rootPath="
                        + prefabRoot.name
                        + ".请改为编辑其子节点,或先用 prefab.queryHierarchy 确认根节点路径.");
                }

                GameObject target = PrefabComponentHandlerUtils.FindGameObjectOrThrow(prefabRoot, objectPath, siblingIndex, "objectPath");

                Transform transform = target.transform;
                int oldSiblingIndex = transform.GetSiblingIndex();
                int newSiblingIndexApplied = newSiblingIndexRequested;

                Transform parent = transform.parent;
                int maxSiblingIndex = parent == null ? 0 : parent.childCount - 1;
                if (newSiblingIndexApplied > maxSiblingIndex)
                {
                    newSiblingIndexApplied = maxSiblingIndex;
                }

                Undo.RecordObject(transform, "Set Prefab Sibling Index");
                transform.SetSiblingIndex(newSiblingIndexApplied);

                bool saved = PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);

                JsonData result = JsonResultBuilder.CreateObject();
                result["prefabPath"] = prefabPath;
                result["objectPath"] = objectPath;
                result["siblingIndex"] = siblingIndex;
                result["instanceID"] = target.GetInstanceID();
                result["oldSiblingIndex"] = oldSiblingIndex;
                result["newSiblingIndexRequested"] = newSiblingIndexRequested;
                result["newSiblingIndexApplied"] = newSiblingIndexApplied;
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
