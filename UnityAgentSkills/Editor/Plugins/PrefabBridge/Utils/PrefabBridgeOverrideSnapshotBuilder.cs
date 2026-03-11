using System;
using System.Collections.Generic;
using LitJson2_utf;
using UnityAgentSkills.Core;
using UnityAgentSkills.Plugins.Scene.Handlers;
using UnityAgentSkills.Utils;
using UnityAgentSkills.Utils.JsonBuilders;
using UnityEditor;
using UnityEngine;

namespace UnityAgentSkills.Plugins.PrefabBridge.Utils
{
    /// <summary>
    /// PrefabBridge override 快照构建器.
    /// 统一生成 getOverrides 与 selected apply/revert 共享的 override 视图.
    /// </summary>
    internal static class PrefabBridgeOverrideSnapshotBuilder
    {
        /// <summary>
        /// override 快照项.
        /// </summary>
        internal sealed class OverrideEntry
        {
            public string OverrideId;
            public string OverrideKind;
            public string TargetObjectPath;
            public int TargetSiblingIndex;
            public string ComponentType;
            public int ComponentIndex;
            public string PropertyPath;
            public JsonData Payload;
        }

        /// <summary>
        /// override 快照结果.
        /// </summary>
        internal sealed class OverrideSnapshot
        {
            public readonly List<OverrideEntry> Entries = new List<OverrideEntry>();
            public readonly Dictionary<string, OverrideEntry> ById = new Dictionary<string, OverrideEntry>(StringComparer.Ordinal);
            public JsonData ModifiedProperties = JsonResultBuilder.CreateArray();
            public JsonData AddedComponents = JsonResultBuilder.CreateArray();
            public JsonData RemovedComponents = JsonResultBuilder.CreateArray();
            public JsonData AddedGameObjects = JsonResultBuilder.CreateArray();
            public JsonData RemovedGameObjects = JsonResultBuilder.CreateArray();
        }

        /// <summary>
        /// 构建指定 instance root 的 override 快照.
        /// </summary>
        public static OverrideSnapshot Build(GameObject instanceRoot)
        {
            var snapshot = new OverrideSnapshot();
            if (instanceRoot == null)
            {
                return snapshot;
            }

            BuildModifiedPropertyEntries(snapshot, instanceRoot);
            BuildAddedComponentEntries(snapshot, instanceRoot);
            BuildRemovedComponentEntries(snapshot, instanceRoot);
            BuildAddedGameObjectEntries(snapshot, instanceRoot);
            BuildRemovedGameObjectEntries(snapshot, instanceRoot);
            return snapshot;
        }

        private static void BuildModifiedPropertyEntries(OverrideSnapshot snapshot, GameObject instanceRoot)
        {
            List<UnityEditor.SceneManagement.ObjectOverride> objectOverrides = PrefabUtility.GetObjectOverrides(instanceRoot, false);
            foreach (UnityEditor.SceneManagement.ObjectOverride item in objectOverrides)
            {
                if (item == null || item.instanceObject == null)
                {
                    continue;
                }

                Component component = item.instanceObject as Component;
                GameObject targetGameObject = component != null ? component.gameObject : item.instanceObject as GameObject;
                if (targetGameObject == null)
                {
                    continue;
                }

                SerializedObject so = new SerializedObject(item.instanceObject);
                SerializedProperty iterator = so.GetIterator();
                if (!iterator.NextVisible(true))
                {
                    continue;
                }

                do
                {
                    if (!iterator.prefabOverride)
                    {
                        continue;
                    }

                    string targetPath = GameObjectPathFinder.GetPath(targetGameObject);
                    string componentType = component != null ? component.GetType().FullName : "UnityEngine.GameObject";
                    int componentIndex = component != null ? SceneEditHandlerUtils.GetComponentIndex(targetGameObject, component) : 0;
                    string overrideId = PrefabBridgeCommon.BuildOverrideId("modifiedProperty", targetPath, componentType, componentIndex, iterator.propertyPath);

                    JsonData row = JsonResultBuilder.CreateObject();
                    row["overrideId"] = overrideId;
                    row["overrideKind"] = "modifiedProperty";
                    row["targetObjectPath"] = targetPath;
                    row["targetSiblingIndex"] = GameObjectPathFinder.GetSameNameSiblingIndex(targetGameObject);
                    row["componentType"] = componentType;
                    row["componentIndex"] = componentIndex;
                    row["propertyPath"] = iterator.propertyPath;
                    row["instanceValue"] = ConvertSerializedPropertyValue(iterator);
                    row["prefabValue"] = JsonResultBuilder.CreateObject();
                    snapshot.ModifiedProperties.Add(row);
                    AddEntry(snapshot, row, componentIndex);
                }
                while (iterator.NextVisible(false));
            }
        }

        private static void BuildAddedComponentEntries(OverrideSnapshot snapshot, GameObject instanceRoot)
        {
            List<UnityEditor.SceneManagement.AddedComponent> addedComponentList = PrefabUtility.GetAddedComponents(instanceRoot);
            foreach (UnityEditor.SceneManagement.AddedComponent item in addedComponentList)
            {
                if (item == null || item.instanceComponent == null)
                {
                    continue;
                }

                Component component = item.instanceComponent;
                GameObject targetGameObject = component.gameObject;
                string targetPath = GameObjectPathFinder.GetPath(targetGameObject);
                int componentIndex = SceneEditHandlerUtils.GetComponentIndex(targetGameObject, component);
                string overrideId = PrefabBridgeCommon.BuildOverrideId("addedComponent", targetPath, component.GetType().FullName, componentIndex, string.Empty);

                JsonData row = JsonResultBuilder.CreateObject();
                row["overrideId"] = overrideId;
                row["overrideKind"] = "addedComponent";
                row["targetObjectPath"] = targetPath;
                row["targetSiblingIndex"] = GameObjectPathFinder.GetSameNameSiblingIndex(targetGameObject);
                row["componentType"] = component.GetType().FullName;
                row["componentIndex"] = componentIndex;
                snapshot.AddedComponents.Add(row);
                AddEntry(snapshot, row, componentIndex);
            }
        }

        private static void BuildRemovedComponentEntries(OverrideSnapshot snapshot, GameObject instanceRoot)
        {
            try
            {
                List<UnityEditor.SceneManagement.RemovedComponent> removedComponentList = PrefabUtility.GetRemovedComponents(instanceRoot);
                foreach (UnityEditor.SceneManagement.RemovedComponent item in removedComponentList)
                {
                    if (item == null || item.assetComponent == null || item.containingInstanceGameObject == null)
                    {
                        continue;
                    }

                    GameObject targetGameObject = item.containingInstanceGameObject;
                    string targetPath = GameObjectPathFinder.GetPath(targetGameObject);
                    string componentType = item.assetComponent.GetType().FullName;
                    int componentIndex = 0;
                    string overrideId = PrefabBridgeCommon.BuildOverrideId("removedComponent", targetPath, componentType, componentIndex, string.Empty);

                    JsonData row = JsonResultBuilder.CreateObject();
                    row["overrideId"] = overrideId;
                    row["overrideKind"] = "removedComponent";
                    row["targetObjectPath"] = targetPath;
                    row["targetSiblingIndex"] = GameObjectPathFinder.GetSameNameSiblingIndex(targetGameObject);
                    row["componentType"] = componentType;
                    row["componentIndex"] = componentIndex;
                    snapshot.RemovedComponents.Add(row);
                    AddEntry(snapshot, row, componentIndex);
                }
            }
            catch
            {
                // 某些 Unity 版本在 removed override 查询上会抛编辑器内部异常,这里降级为空结果以保持快照构建稳定.
            }
        }

        private static void BuildAddedGameObjectEntries(OverrideSnapshot snapshot, GameObject instanceRoot)
        {
            List<UnityEditor.SceneManagement.AddedGameObject> addedGameObjectList = PrefabUtility.GetAddedGameObjects(instanceRoot);
            foreach (UnityEditor.SceneManagement.AddedGameObject item in addedGameObjectList)
            {
                if (item == null || item.instanceGameObject == null)
                {
                    continue;
                }

                GameObject go = item.instanceGameObject;
                string targetPath = GameObjectPathFinder.GetPath(go);
                string overrideId = PrefabBridgeCommon.BuildOverrideId("addedGameObject", targetPath);

                JsonData row = JsonResultBuilder.CreateObject();
                row["overrideId"] = overrideId;
                row["overrideKind"] = "addedGameObject";
                row["targetObjectPath"] = targetPath;
                row["targetSiblingIndex"] = GameObjectPathFinder.GetSameNameSiblingIndex(go);
                snapshot.AddedGameObjects.Add(row);
                AddEntry(snapshot, row, 0);
            }
        }

        private static void BuildRemovedGameObjectEntries(OverrideSnapshot snapshot, GameObject instanceRoot)
        {
            try
            {
                List<UnityEditor.SceneManagement.RemovedGameObject> removedGameObjectList = PrefabUtility.GetRemovedGameObjects(instanceRoot);
                foreach (UnityEditor.SceneManagement.RemovedGameObject item in removedGameObjectList)
                {
                    if (item == null || item.assetGameObject == null || item.parentOfRemovedGameObjectInInstance == null)
                    {
                        continue;
                    }

                    string parentPath = GameObjectPathFinder.GetPath(item.parentOfRemovedGameObjectInInstance);
                    string targetPath = string.IsNullOrEmpty(parentPath)
                        ? item.assetGameObject.name
                        : parentPath + "/" + item.assetGameObject.name;
                    string overrideId = PrefabBridgeCommon.BuildOverrideId("removedGameObject", targetPath);

                    JsonData row = JsonResultBuilder.CreateObject();
                    row["overrideId"] = overrideId;
                    row["overrideKind"] = "removedGameObject";
                    row["targetObjectPath"] = targetPath;
                    row["targetSiblingIndex"] = 0;
                    snapshot.RemovedGameObjects.Add(row);
                    AddEntry(snapshot, row, 0);
                }
            }
            catch
            {
                // 某些 Unity 版本在 removed override 查询上会抛编辑器内部异常,这里降级为空结果以保持快照构建稳定.
            }
        }

        private static void AddEntry(OverrideSnapshot snapshot, JsonData row, int componentIndex)
        {
            var entry = new OverrideEntry
            {
                OverrideId = row["overrideId"].ToString(),
                OverrideKind = row["overrideKind"].ToString(),
                TargetObjectPath = row["targetObjectPath"].ToString(),
                TargetSiblingIndex = row.ContainsKey("targetSiblingIndex") ? (int)row["targetSiblingIndex"] : 0,
                ComponentType = row.ContainsKey("componentType") ? row["componentType"].ToString() : string.Empty,
                ComponentIndex = componentIndex,
                PropertyPath = row.ContainsKey("propertyPath") ? row["propertyPath"].ToString() : string.Empty,
                Payload = row
            };
            snapshot.Entries.Add(entry);
            snapshot.ById[entry.OverrideId] = entry;
        }

        private static JsonData ConvertSerializedPropertyValue(SerializedProperty property)
        {
            JsonData result = JsonResultBuilder.CreateObject();
            result["propertyType"] = property.propertyType.ToString();

            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    result["value"] = property.intValue;
                    return result;
                case SerializedPropertyType.Boolean:
                    result["value"] = property.boolValue;
                    return result;
                case SerializedPropertyType.Float:
                    result["value"] = property.floatValue;
                    return result;
                case SerializedPropertyType.String:
                    result["value"] = property.stringValue ?? string.Empty;
                    return result;
                case SerializedPropertyType.Enum:
                    result["value"] = property.enumNames != null && property.enumValueIndex >= 0 && property.enumValueIndex < property.enumNames.Length
                        ? property.enumNames[property.enumValueIndex]
                        : property.enumValueIndex.ToString();
                    return result;
                case SerializedPropertyType.ObjectReference:
                    result["value"] = property.objectReferenceValue != null ? property.objectReferenceValue.name : string.Empty;
                    return result;
                case SerializedPropertyType.Vector2:
                    result["value"] = SceneEditHandlerUtils.BuildVector2Json(property.vector2Value);
                    return result;
                case SerializedPropertyType.Vector3:
                    result["value"] = SceneEditHandlerUtils.BuildVector3Json(property.vector3Value);
                    return result;
                case SerializedPropertyType.Color:
                    JsonData color = JsonResultBuilder.CreateObject();
                    color["r"] = property.colorValue.r;
                    color["g"] = property.colorValue.g;
                    color["b"] = property.colorValue.b;
                    color["a"] = property.colorValue.a;
                    result["value"] = color;
                    return result;
                default:
                    result["value"] = property.propertyPath;
                    return result;
            }
        }
    }
}
