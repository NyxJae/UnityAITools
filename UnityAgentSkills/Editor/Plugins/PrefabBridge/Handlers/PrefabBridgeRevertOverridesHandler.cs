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
    /// prefabBridge.revertOverrides 命令处理器.
    /// 仅对统一 snapshot 中已建模且支持的 override 执行 revert,未知或不支持项明确失败.
    /// </summary>
    internal static class PrefabBridgeRevertOverridesHandler
    {
        /// <summary>
        /// 支持的命令类型.
        /// </summary>
        public const string CommandType = "prefabBridge.revertOverrides";

        /// <summary>
        /// 执行命令.
        /// </summary>
        /// <param name="rawParams">原始参数.</param>
        /// <returns>revert 结果.</returns>
        public static JsonData Execute(JsonData rawParams)
        {
            CommandParams parameters = new CommandParams(rawParams);
            string sceneName = parameters.GetString("sceneName", null);
            string objectPath = parameters.GetString("objectPath", null);
            int siblingIndex = parameters.GetInt("siblingIndex", 0);
            string revertMode = parameters.GetString("revertMode", "all");

            var context = PrefabBridgeCommon.ResolvePrefabInstanceContextOrThrow(sceneName, objectPath, siblingIndex);
            if (!context.IsPrefabInstance)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": 该对象不是 prefab instance,无法 revert overrides");
            }

            PrefabBridgeOverrideSnapshotBuilder.OverrideSnapshot snapshot = PrefabBridgeOverrideSnapshotBuilder.Build(context.InstanceRoot);
            bool hasOverridesBefore = snapshot.Entries.Count > 0;
            HashSet<string> selectedIds = ReadSelectedIds(rawParams, revertMode, snapshot, "revert");
            JsonData revertedIds = JsonResultBuilder.CreateArray();

            foreach (PrefabBridgeOverrideSnapshotBuilder.OverrideEntry entry in snapshot.Entries)
            {
                if (selectedIds != null && !selectedIds.Contains(entry.OverrideId))
                {
                    continue;
                }

                RevertEntryOrThrow(entry, context.InstanceRoot);
                revertedIds.Add(entry.OverrideId);
            }

            bool saved = PrefabBridgeCommon.SaveScene(context.Scene);
            PrefabBridgeOverrideSnapshotBuilder.OverrideSnapshot afterSnapshot = PrefabBridgeOverrideSnapshotBuilder.Build(context.InstanceRoot);
            JsonData remainingOverrides = JsonResultBuilder.CreateObject();
            remainingOverrides["hasOverrides"] = afterSnapshot.Entries.Count > 0;
            remainingOverrides["overrideCount"] = afterSnapshot.Entries.Count;
            remainingOverrides["modifiedProperties"] = afterSnapshot.ModifiedProperties;
            remainingOverrides["addedComponents"] = afterSnapshot.AddedComponents;
            remainingOverrides["removedComponents"] = afterSnapshot.RemovedComponents;
            remainingOverrides["addedGameObjects"] = afterSnapshot.AddedGameObjects;
            remainingOverrides["removedGameObjects"] = afterSnapshot.RemovedGameObjects;

            JsonData result = JsonResultBuilder.CreateObject();
            result["sceneName"] = sceneName;
            result["objectPath"] = objectPath;
            result["siblingIndex"] = siblingIndex;
            result["revertedOverrideIds"] = revertedIds;
            result["hasOverridesBefore"] = hasOverridesBefore;
            result["hasOverridesAfter"] = afterSnapshot.Entries.Count > 0;
            result["remainingOverrides"] = remainingOverrides;
            result["saved"] = saved;
            return result;
        }

        private static HashSet<string> ReadSelectedIds(JsonData rawParams, string revertMode, PrefabBridgeOverrideSnapshotBuilder.OverrideSnapshot snapshot, string operationName)
        {
            if (revertMode == "all")
            {
                return null;
            }

            if (revertMode != "selected")
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": revertMode must be all or selected");
            }

            HashSet<string> selectedIds = PrefabBridgeCommon.ReadOverrideIdsOrThrow(rawParams, "overrideIds");
            ValidateSelectedOverrideIdsOrThrow(selectedIds, snapshot, operationName);
            return selectedIds;
        }

        private static void ValidateSelectedOverrideIdsOrThrow(HashSet<string> selectedIds, PrefabBridgeOverrideSnapshotBuilder.OverrideSnapshot snapshot, string operationName)
        {
            foreach (string id in selectedIds)
            {
                PrefabBridgeOverrideSnapshotBuilder.OverrideEntry entry;
                if (!snapshot.ById.TryGetValue(id, out entry))
                {
                    throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": overrideIds contains unknown id for " + operationName + ": " + id);
                }

                EnsureSupportedOverrideKindOrThrow(entry.OverrideKind, operationName, id);
            }
        }

        private static void RevertEntryOrThrow(PrefabBridgeOverrideSnapshotBuilder.OverrideEntry entry, GameObject instanceRoot)
        {
            EnsureSupportedOverrideKindOrThrow(entry.OverrideKind, "revert", entry.OverrideId);

            switch (entry.OverrideKind)
            {
                case "modifiedProperty":
                    SerializedProperty property = FindPropertyOverrideOrThrow(entry, instanceRoot);
                    PrefabUtility.RevertPropertyOverride(property, InteractionMode.AutomatedAction);
                    return;
                case "addedComponent":
                    Component addedComponent = FindInstanceComponentOrThrow(entry, instanceRoot);
                    PrefabUtility.RevertAddedComponent(addedComponent, InteractionMode.AutomatedAction);
                    return;
                case "addedGameObject":
                    GameObject addedGameObject = FindTargetGameObjectOrThrow(entry, instanceRoot);
                    PrefabUtility.RevertAddedGameObject(addedGameObject, InteractionMode.AutomatedAction);
                    return;
                default:
                    throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": revert does not support override kind: " + entry.OverrideKind + ", overrideId=" + entry.OverrideId);
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
