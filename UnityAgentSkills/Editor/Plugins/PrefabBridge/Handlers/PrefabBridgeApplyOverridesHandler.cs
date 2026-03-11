using System;
using System.Collections.Generic;
using LitJson2_utf;
using UnityAgentSkills.Core;
using UnityAgentSkills.Plugins.PrefabBridge.Utils;
using UnityAgentSkills.Utils;
using UnityAgentSkills.Utils.JsonBuilders;
using UnityEditor;
using UnityEngine;

namespace UnityAgentSkills.Plugins.PrefabBridge.Handlers
{
    /// <summary>
    /// prefabBridge.applyOverrides 命令处理器.
    /// 仅对统一 snapshot 中已建模且支持的 override 执行 apply,未知或不支持项明确失败.
    /// </summary>
    internal static class PrefabBridgeApplyOverridesHandler
    {
        /// <summary>
        /// 支持的命令类型.
        /// </summary>
        public const string CommandType = "prefabBridge.applyOverrides";

        /// <summary>
        /// 执行命令.
        /// </summary>
        /// <param name="rawParams">原始参数.</param>
        /// <returns>apply 结果.</returns>
        public static JsonData Execute(JsonData rawParams)
        {
            CommandParams parameters = new CommandParams(rawParams);
            string sceneName = parameters.GetString("sceneName", null);
            string objectPath = parameters.GetString("objectPath", null);
            int siblingIndex = parameters.GetInt("siblingIndex", 0);
            string applyMode = parameters.GetString("applyMode", "all");

            var context = PrefabBridgeCommon.ResolvePrefabInstanceContextOrThrow(sceneName, objectPath, siblingIndex);
            if (!context.IsPrefabInstance)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": 该对象不是 prefab instance,无法 apply overrides");
            }

            string applyTargetPrefabPath = context.SourcePrefabPath;
            if (string.IsNullOrWhiteSpace(applyTargetPrefabPath))
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.PrefabNotFound + ": 无法解析来源 prefab 路径");
            }

            PrefabBridgeOverrideSnapshotBuilder.OverrideSnapshot snapshot = PrefabBridgeOverrideSnapshotBuilder.Build(context.InstanceRoot);
            bool hasOverridesBefore = snapshot.Entries.Count > 0;
            HashSet<string> selectedIds = ReadSelectedIds(rawParams, applyMode, snapshot, "apply");
            JsonData appliedIds = JsonResultBuilder.CreateArray();

            foreach (PrefabBridgeOverrideSnapshotBuilder.OverrideEntry entry in snapshot.Entries)
            {
                if (selectedIds != null && !selectedIds.Contains(entry.OverrideId))
                {
                    continue;
                }

                ApplyEntryOrThrow(entry, applyTargetPrefabPath, context.InstanceRoot);
                appliedIds.Add(entry.OverrideId);
            }

            AssetDatabase.SaveAssets();
            PrefabBridgeOverrideSnapshotBuilder.OverrideSnapshot afterSnapshot = PrefabBridgeOverrideSnapshotBuilder.Build(context.InstanceRoot);
            JsonData result = JsonResultBuilder.CreateObject();
            result["sceneName"] = sceneName;
            result["objectPath"] = objectPath;
            result["siblingIndex"] = siblingIndex;
            result["applyTargetPrefabPath"] = applyTargetPrefabPath;
            result["appliedOverrideIds"] = appliedIds;
            result["hasOverridesBefore"] = hasOverridesBefore;
            result["hasOverridesAfter"] = afterSnapshot.Entries.Count > 0;
            return result;
        }

        private static HashSet<string> ReadSelectedIds(JsonData rawParams, string applyMode, PrefabBridgeOverrideSnapshotBuilder.OverrideSnapshot snapshot, string operationName)
        {
            if (applyMode == "all")
            {
                return null;
            }

            if (applyMode != "selected")
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": applyMode must be all or selected");
            }

            HashSet<string> selectedIds = PrefabBridgeCommon.ReadOverrideIdsOrThrow(rawParams, "overrideIds");
            ValidateSelectedOverrideIdsOrThrow(selectedIds, snapshot, operationName);
            return selectedIds;
        }

        private static void ValidateSelectedOverrideIdsOrThrow(HashSet<string> selectedIds, PrefabBridgeOverrideSnapshotBuilder.OverrideSnapshot snapshot, string operationName)
        {
            foreach (string id in selectedIds)
            {
                if (!snapshot.ById.TryGetValue(id, out PrefabBridgeOverrideSnapshotBuilder.OverrideEntry entry))
                {
                    throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": overrideIds contains unknown id for " + operationName + ": " + id);
                }

                EnsureSupportedOverrideKindOrThrow(entry.OverrideKind, operationName, id);
            }
        }

        private static void ApplyEntryOrThrow(PrefabBridgeOverrideSnapshotBuilder.OverrideEntry entry, string prefabPath, GameObject instanceRoot)
        {
            EnsureSupportedOverrideKindOrThrow(entry.OverrideKind, "apply", entry.OverrideId);

            switch (entry.OverrideKind)
            {
                case "modifiedProperty":
                    SerializedProperty property = FindPropertyOverrideOrThrow(entry, instanceRoot);
                    PrefabUtility.ApplyPropertyOverride(property, prefabPath, InteractionMode.AutomatedAction);
                    return;
                case "addedComponent":
                    Component addedComponent = FindInstanceComponentOrThrow(entry, instanceRoot);
                    PrefabUtility.ApplyAddedComponent(addedComponent, prefabPath, InteractionMode.AutomatedAction);
                    return;
                case "addedGameObject":
                    GameObject addedGameObject = FindTargetGameObjectOrThrow(entry, instanceRoot);
                    PrefabUtility.ApplyAddedGameObject(addedGameObject, prefabPath, InteractionMode.AutomatedAction);
                    return;
                default:
                    throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": apply does not support override kind: " + entry.OverrideKind + ", overrideId=" + entry.OverrideId);
            }
        }

        private static void EnsureSupportedOverrideKindOrThrow(string overrideKind, string operationName, string overrideId)
        {
            if (overrideKind == "modifiedProperty" || overrideKind == "addedComponent" || overrideKind == "addedGameObject")
            {
                return;
            }

            throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": unsupported override kind for " + operationName + ": " + overrideKind + ", overrideId=" + overrideId);
        }

        private static SerializedProperty FindPropertyOverrideOrThrow(PrefabBridgeOverrideSnapshotBuilder.OverrideEntry entry, GameObject instanceRoot)
        {
            UnityEngine.Object targetObject = FindTargetObjectOrThrow(entry, instanceRoot);
            SerializedObject serializedObject = new SerializedObject(targetObject);
            SerializedProperty property = serializedObject.FindProperty(entry.PropertyPath);
            if (property == null || !property.prefabOverride)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": override property not found or no longer overridden: " + entry.OverrideId);
            }

            return property;
        }

        private static UnityEngine.Object FindTargetObjectOrThrow(PrefabBridgeOverrideSnapshotBuilder.OverrideEntry entry, GameObject instanceRoot)
        {
            if (entry.ComponentType == "UnityEngine.GameObject")
            {
                return FindTargetGameObjectOrThrow(entry, instanceRoot);
            }

            return FindInstanceComponentOrThrow(entry, instanceRoot);
        }

        private static GameObject FindTargetGameObjectOrThrow(PrefabBridgeOverrideSnapshotBuilder.OverrideEntry entry, GameObject instanceRoot)
        {
            GameObject target = GameObjectPathFinder.FindByPath(instanceRoot, entry.TargetObjectPath, entry.TargetSiblingIndex);
            if (target == null)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": target object not found for overrideId=" + entry.OverrideId);
            }

            return target;
        }

        private static Component FindInstanceComponentOrThrow(PrefabBridgeOverrideSnapshotBuilder.OverrideEntry entry, GameObject instanceRoot)
        {
            GameObject target = FindTargetGameObjectOrThrow(entry, instanceRoot);
            Component[] components = target.GetComponents<Component>();
            int currentIndex = -1;
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null)
                {
                    continue;
                }

                string fullName = component.GetType().FullName;
                if (!string.Equals(fullName, entry.ComponentType, StringComparison.Ordinal))
                {
                    continue;
                }

                currentIndex++;
                if (currentIndex == entry.ComponentIndex)
                {
                    return component;
                }
            }

            throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": target component not found for overrideId=" + entry.OverrideId);
        }
    }
}
