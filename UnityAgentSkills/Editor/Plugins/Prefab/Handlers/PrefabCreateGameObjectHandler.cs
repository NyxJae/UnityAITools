using System;
using System.Collections.Generic;
using UnityAgentSkills.Core;
using UnityAgentSkills.Utils;
using UnityAgentSkills.Utils.JsonBuilders;
using LitJson2_utf;
using UnityEditor;
using UnityEngine;

namespace UnityAgentSkills.Plugins.Prefab.Handlers
{
    /// <summary>
    /// prefab.createGameObject命令处理器.
    /// </summary>
    internal static class PrefabCreateGameObjectHandler
    {
        /// <summary>
        /// 支持的命令类型.
        /// </summary>
        public const string CommandType = "prefab.createGameObject";

        /// <summary>
        /// 执行创建GameObject命令.
        /// </summary>
        public static JsonData Execute(JsonData rawParams)
        {
            CommandParams parameters = new CommandParams(rawParams);

            string prefabPath = PrefabComponentHandlerUtils.NormalizePrefabPath(parameters.GetString("prefabPath", null));
            string parentPath = parameters.GetString("parentPath", null);
            int parentSiblingIndex = parameters.GetInt("parentSiblingIndex", 0);
            string name = parameters.GetString("name", null);
            int insertSiblingIndex = parameters.GetInt("insertSiblingIndex", -1);

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": name is required");
            }
            if (parentSiblingIndex < 0)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": parentSiblingIndex must be >= 0");
            }
            if (insertSiblingIndex < -1)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": insertSiblingIndex must be >= -1");
            }

            GameObject prefabRoot = PrefabComponentHandlerUtils.LoadPrefabContentsOrThrow(prefabPath);
            try
            {
                GameObject parent = ResolveParentOrThrow(prefabRoot, parentPath, parentSiblingIndex);

                var createdObject = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(createdObject, "Create Prefab GameObject");
                createdObject.transform.SetParent(parent.transform, false);

                int appliedInsertSiblingIndex = ApplyInsertSiblingIndex(createdObject.transform, parent.transform, insertSiblingIndex);
                ApplyInitialPropertiesIfNeeded(parameters, createdObject);

                string createdObjectPath = GameObjectPathFinder.GetPath(createdObject);
                int createdSiblingIndex = GameObjectPathFinder.GetSameNameSiblingIndex(createdObject);
                int instanceID = createdObject.GetInstanceID();

                bool saved = PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);

                JsonData result = JsonResultBuilder.CreateObject();
                result["prefabPath"] = prefabPath;
                result["createdObjectPath"] = createdObjectPath;
                result["createdSiblingIndex"] = createdSiblingIndex;
                result["insertSiblingIndexApplied"] = appliedInsertSiblingIndex;
                result["instanceID"] = instanceID;
                result["saved"] = saved;
                return result;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static GameObject ResolveParentOrThrow(GameObject prefabRoot, string parentPath, int parentSiblingIndex)
        {
            if (string.IsNullOrWhiteSpace(parentPath))
            {
                if (parentSiblingIndex != 0)
                {
                    throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": parentSiblingIndex must be 0 when parentPath is omitted");
                }

                return prefabRoot;
            }

            if (prefabRoot != null && string.Equals(parentPath, prefabRoot.name, StringComparison.Ordinal) && parentSiblingIndex != 0)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": parentSiblingIndex must be 0 when parentPath points to prefab root");
            }

            GameObject parent = PrefabComponentHandlerUtils.FindGameObjectOrThrow(prefabRoot, parentPath, parentSiblingIndex, "parentPath");
            return parent;
        }

        private static int ApplyInsertSiblingIndex(Transform child, Transform parent, int insertSiblingIndex)
        {
            if (child == null || parent == null)
            {
                return 0;
            }

            if (insertSiblingIndex >= 0 && insertSiblingIndex < parent.childCount)
            {
                child.SetSiblingIndex(insertSiblingIndex);
            }
            else
            {
                child.SetAsLastSibling();
            }

            return child.GetSiblingIndex();
        }

        private static void ApplyInitialPropertiesIfNeeded(CommandParams parameters, GameObject target)
        {
            if (!parameters.Has("initialProperties"))
            {
                return;
            }

            JsonData initialProperties = parameters.GetData()["initialProperties"];
            if (initialProperties == null || initialProperties.GetJsonType() == JsonType.None)
            {
                return;
            }

            if (!initialProperties.IsObject)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": initialProperties must be an object");
            }

            if (initialProperties.Count == 0)
            {
                return;
            }

            try
            {
                List<GameObjectPropertyModifier.PropertyChange> changes;
                GameObjectPropertyModifier.ModifyProperties(target, initialProperties, out changes);
            }
            catch (Exception ex)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": initialProperties invalid, " + ex.Message);
            }
        }
    }
}
