using System;
using System.Collections.Generic;
using LitJson2_utf;
using UnityAgentSkills.Core;
using UnityAgentSkills.Utils;
using UnityAgentSkills.Utils.JsonBuilders;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityAgentSkills.Plugins.Scene.Handlers
{
    /// <summary>
    /// scene.batchEdit 的子操作执行器.
    /// 在同一个场景编辑会话内执行子操作,禁止在子操作内保存.
    /// </summary>
    internal static class SceneBatchEditOperationExecutor
    {
        /// <summary>
        /// 在指定场景会话上执行一个子操作.
        /// </summary>
        /// <param name="session">场景编辑会话.</param>
        /// <param name="operationType">子操作类型,例如 scene.renameGameObject.</param>
        /// <param name="operationParams">子操作参数对象,必须省略 sceneName.</param>
        /// <returns>子操作结果,不得包含 saved 字段.</returns>
        public static JsonData Execute(SceneEditSession session, string operationType, JsonData operationParams)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (string.IsNullOrWhiteSpace(operationType))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": operation type is required");
            }

            if (operationParams == null || operationParams.GetJsonType() == JsonType.None || !operationParams.IsObject)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": operation params must be an object");
            }

            switch (operationType)
            {
                case SceneSetGameObjectPropertiesHandler.CommandType:
                    return ExecuteSetGameObjectProperties(session, operationParams);
                case SceneRenameGameObjectHandler.CommandType:
                    return ExecuteRenameGameObject(session, operationParams);
                case SceneCreateGameObjectHandler.CommandType:
                    return ExecuteCreateGameObject(session, operationParams);
                case SceneDeleteGameObjectHandler.CommandType:
                    return ExecuteDeleteGameObject(session, operationParams);
                case SceneMoveOrCopyGameObjectHandler.CommandType:
                    return ExecuteMoveOrCopyGameObject(session, operationParams);
                case SceneSetSiblingIndexHandler.CommandType:
                    return ExecuteSetSiblingIndex(session, operationParams);
                case SceneAddComponentHandler.CommandType:
                    return ExecuteAddComponent(session, operationParams);
                case SceneSetComponentPropertiesHandler.CommandType:
                    return ExecuteSetComponentProperties(session, operationParams);
                case SceneDeleteComponentHandler.CommandType:
                    return ExecuteDeleteComponent(session, operationParams);
                case SceneSetTransformHandler.CommandType:
                    return ExecuteSetTransform(session, operationParams);
                case SceneSetRectTransformHandler.CommandType:
                    return ExecuteSetRectTransform(session, operationParams);
                default:
                    throw new NotSupportedException(operationType);
            }
        }

        private static JsonData ExecuteSetGameObjectProperties(SceneEditSession session, JsonData opParams)
        {
            CommandParams parameters = new CommandParams(opParams);
            string objectPath = parameters.GetString("objectPath", null);
            int siblingIndex = parameters.GetInt("siblingIndex", 0);

            if (!parameters.Has("properties"))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": properties is required");
            }

            JsonData properties = parameters.GetData()["properties"];
            if (properties == null || !properties.IsObject || properties.Count == 0)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": properties对象不能为空, 至少需要指定一个要修改的属性");
            }

            GameObject target = session.FindGameObjectOrThrow(objectPath, siblingIndex, "objectPath");
            List<GameObjectPropertyModifier.PropertyChange> modifiedProperties;
            JsonData currentProperties = GameObjectPropertyModifier.ModifyProperties(target, properties, out modifiedProperties);

            JsonData result = JsonResultBuilder.CreateObject();
            result["sceneName"] = session.SceneName;
            result["objectPath"] = objectPath;
            result["siblingIndex"] = siblingIndex;
            result["instanceID"] = target.GetInstanceID();
            result["currentProperties"] = currentProperties;
            result["modifiedProperties"] = BuildGameObjectModifiedProperties(modifiedProperties);
            return result;
        }

        private static JsonData ExecuteRenameGameObject(SceneEditSession session, JsonData opParams)
        {
            CommandParams parameters = new CommandParams(opParams);
            string objectPath = parameters.GetString("objectPath", null);
            int siblingIndex = parameters.GetInt("siblingIndex", 0);
            string newName = parameters.GetString("newName", null);

            if (siblingIndex < 0)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": siblingIndex must be >= 0");
            }

            if (string.IsNullOrWhiteSpace(newName))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": newName is required");
            }

            GameObject target = session.FindGameObjectOrThrow(objectPath, siblingIndex, "objectPath");
            string oldName = target.name;
            string oldPath = GameObjectPathFinder.GetPath(target);
            Undo.RecordObject(target, "Rename Scene GameObject");
            target.name = newName;
            string newPath = GameObjectPathFinder.GetPath(target);

            JsonData result = JsonResultBuilder.CreateObject();
            result["sceneName"] = session.SceneName;
            result["objectPath"] = objectPath;
            result["siblingIndex"] = siblingIndex;
            result["instanceID"] = target.GetInstanceID();
            result["oldName"] = oldName;
            result["newName"] = newName;
            result["oldPath"] = oldPath;
            result["newPath"] = newPath;
            return result;
        }

        private static JsonData ExecuteCreateGameObject(SceneEditSession session, JsonData opParams)
        {
            CommandParams parameters = new CommandParams(opParams);
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

            GameObject parent = ResolveParentOrThrow(session, parentPath, parentSiblingIndex);
            GameObject createdObject = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(createdObject, "Create Scene GameObject");
            if (parent != null)
            {
                createdObject.transform.SetParent(parent.transform, false);
            }
            else
            {
                SceneManager.MoveGameObjectToScene(createdObject, session.TargetScene);
            }

            int appliedInsertSiblingIndex = SceneEditCommon.ApplySiblingIndex(createdObject.transform, parent != null ? parent.transform : null, insertSiblingIndex);
            ApplyInitialPropertiesIfNeeded(parameters, createdObject);
            string createdObjectPath = GameObjectPathFinder.GetPath(createdObject);
            int createdSiblingIndex = GameObjectPathFinder.GetSameNameSiblingIndex(createdObject);

            JsonData result = JsonResultBuilder.CreateObject();
            result["sceneName"] = session.SceneName;
            result["createdObjectPath"] = createdObjectPath;
            result["createdSiblingIndex"] = createdSiblingIndex;
            result["insertSiblingIndexApplied"] = appliedInsertSiblingIndex;
            result["instanceID"] = createdObject.GetInstanceID();
            return result;
        }

        private static JsonData ExecuteDeleteGameObject(SceneEditSession session, JsonData opParams)
        {
            CommandParams parameters = new CommandParams(opParams);
            string objectPath = parameters.GetString("objectPath", null);
            int siblingIndex = parameters.GetInt("siblingIndex", 0);

            GameObject target = session.FindGameObjectOrThrow(objectPath, siblingIndex, "objectPath");
            string deletedObjectPath = GameObjectPathFinder.GetPath(target);
            int deletedInstanceID = target.GetInstanceID();
            int totalDeletedCount = SceneEditCommon.CountGameObjects(target, true);
            Undo.DestroyObjectImmediate(target);

            JsonData result = JsonResultBuilder.CreateObject();
            result["sceneName"] = session.SceneName;
            result["deletedObjectPath"] = deletedObjectPath;
            result["deletedInstanceID"] = deletedInstanceID;
            result["deletedObjectCount"] = 1;
            result["totalDeletedCount"] = totalDeletedCount;
            return result;
        }

        private static JsonData ExecuteMoveOrCopyGameObject(SceneEditSession session, JsonData opParams)
        {
            CommandParams parameters = new CommandParams(opParams);
            string objectPath = parameters.GetString("objectPath", null);
            int siblingIndex = parameters.GetInt("siblingIndex", 0);
            string parentPath = parameters.GetString("parentPath", null);
            int parentSiblingIndex = parameters.GetInt("parentSiblingIndex", 0);
            int targetSiblingIndex = parameters.GetInt("targetSiblingIndex", -1);
            bool isCopy = parameters.GetBool("isCopy", false);

            if (string.IsNullOrEmpty(objectPath))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": objectPath is required");
            }
            if (string.IsNullOrEmpty(parentPath))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": parentPath is required");
            }
            if (siblingIndex < 0)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": siblingIndex must be >= 0");
            }
            if (parentSiblingIndex < 0)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": parentSiblingIndex must be >= 0");
            }

            GameObject sourceGO = session.FindGameObjectOrThrow(objectPath, siblingIndex, "objectPath");
            GameObject targetParent = session.FindGameObjectOrThrow(parentPath, parentSiblingIndex, "parentPath");
            Transform sourceParent = sourceGO.transform.parent;

            if (!isCopy)
            {
                if (targetParent == sourceGO || IsChildOf(targetParent.transform, sourceGO.transform))
                {
                    throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": 不能将物体移动到其自身或其子节点下: " + objectPath);
                }
            }
            else if (targetParent == sourceParent?.gameObject)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": 复制目标父节点不能与原父节点相同");
            }

            return isCopy
                ? ExecuteCopy(session, sourceGO, targetParent, targetSiblingIndex)
                : ExecuteMove(session, sourceGO, targetParent, targetSiblingIndex, objectPath);
        }

        private static JsonData ExecuteMove(SceneEditSession session, GameObject sourceGO, GameObject targetParent, int targetSiblingIndex, string sourcePath)
        {
            Vector3 oldWorldPosition = sourceGO.transform.position;
            Quaternion oldWorldRotation = sourceGO.transform.rotation;
            Vector3 oldWorldScale = sourceGO.transform.lossyScale;
            string oldPath = GameObjectPathFinder.GetPath(sourceGO);
            string oldParentPath = sourceGO.transform.parent != null ? GameObjectPathFinder.GetPath(sourceGO.transform.parent.gameObject) : string.Empty;
            int oldSiblingIndex = sourceGO.transform.GetSiblingIndex();
            int sourceInstanceID = sourceGO.GetInstanceID();
            Undo.SetTransformParent(sourceGO.transform, targetParent.transform, "Move Scene GameObject");
            int newSiblingIndex = SceneEditCommon.ApplySiblingIndex(sourceGO.transform, targetParent.transform, targetSiblingIndex);
            sourceGO.transform.position = oldWorldPosition;
            sourceGO.transform.rotation = oldWorldRotation;
            Transform currentParent = sourceGO.transform.parent;
            if (currentParent != null)
            {
                Vector3 parentScale = currentParent.lossyScale;
                sourceGO.transform.localScale = new Vector3(
                    parentScale.x != 0 ? oldWorldScale.x / parentScale.x : sourceGO.transform.localScale.x,
                    parentScale.y != 0 ? oldWorldScale.y / parentScale.y : sourceGO.transform.localScale.y,
                    parentScale.z != 0 ? oldWorldScale.z / parentScale.z : sourceGO.transform.localScale.z);
            }

            JsonData result = JsonResultBuilder.CreateObject();
            result["sceneName"] = session.SceneName;
            result["sourcePath"] = sourcePath;
            result["oldPath"] = oldPath;
            result["newPath"] = GameObjectPathFinder.GetPath(sourceGO);
            result["oldParentPath"] = oldParentPath;
            result["newParentPath"] = GameObjectPathFinder.GetPath(targetParent);
            result["oldSiblingIndex"] = oldSiblingIndex;
            result["newSiblingIndexApplied"] = newSiblingIndex;
            result["operationType"] = "move";
            result["worldPositionPreserved"] = true;
            result["operatedInstanceID"] = sourceInstanceID;
            return result;
        }

        private static JsonData ExecuteCopy(SceneEditSession session, GameObject sourceGO, GameObject targetParent, int targetSiblingIndex)
        {
            Vector3 sourceWorldPosition = sourceGO.transform.position;
            Quaternion sourceWorldRotation = sourceGO.transform.rotation;
            Vector3 sourceWorldScale = sourceGO.transform.lossyScale;
            string sourcePath = GameObjectPathFinder.GetPath(sourceGO);
            string oldParentPath = sourceGO.transform.parent != null ? GameObjectPathFinder.GetPath(sourceGO.transform.parent.gameObject) : string.Empty;
            string targetParentPath = GameObjectPathFinder.GetPath(targetParent);
            int sourceInstanceID = sourceGO.GetInstanceID();
            int sourceSiblingIndex = sourceGO.transform.GetSiblingIndex();
            GameObject copiedGO = (GameObject)UnityEngine.Object.Instantiate(sourceGO);
            Undo.RegisterCreatedObjectUndo(copiedGO, "Copy Scene GameObject");
            copiedGO.transform.SetParent(targetParent.transform);
            int copiedSiblingIndex = SceneEditCommon.ApplySiblingIndex(copiedGO.transform, targetParent.transform, targetSiblingIndex);
            copiedGO.transform.position = sourceWorldPosition;
            copiedGO.transform.rotation = sourceWorldRotation;
            Transform currentParent = copiedGO.transform.parent;
            if (currentParent != null)
            {
                Vector3 parentScale = currentParent.lossyScale;
                copiedGO.transform.localScale = new Vector3(
                    parentScale.x != 0 ? sourceWorldScale.x / parentScale.x : sourceGO.transform.localScale.x,
                    parentScale.y != 0 ? sourceWorldScale.y / parentScale.y : sourceGO.transform.localScale.y,
                    parentScale.z != 0 ? sourceWorldScale.z / parentScale.z : sourceGO.transform.localScale.z);
            }

            JsonData result = JsonResultBuilder.CreateObject();
            result["sceneName"] = session.SceneName;
            result["sourcePath"] = sourcePath;
            result["originalPath"] = sourcePath;
            result["copiedPath"] = GameObjectPathFinder.GetPath(copiedGO);
            result["oldParentPath"] = oldParentPath;
            result["newParentPath"] = targetParentPath;
            result["sourceSiblingIndex"] = sourceSiblingIndex;
            result["targetSiblingIndex"] = copiedSiblingIndex;
            result["operationType"] = "copy";
            result["worldPositionPreserved"] = true;
            result["originalInstanceID"] = sourceInstanceID;
            result["copiedInstanceID"] = copiedGO.GetInstanceID();
            return result;
        }

        private static JsonData ExecuteSetSiblingIndex(SceneEditSession session, JsonData opParams)
        {
            CommandParams parameters = new CommandParams(opParams);
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

            GameObject target = session.FindGameObjectOrThrow(objectPath, siblingIndex, "objectPath");
            Transform transform = target.transform;
            int oldSiblingIndex = transform.GetSiblingIndex();
            Transform parent = transform.parent;
            int maxSiblingIndex = parent == null ? session.TargetScene.rootCount - 1 : parent.childCount - 1;
            int newSiblingIndexApplied = newSiblingIndexRequested > maxSiblingIndex ? maxSiblingIndex : newSiblingIndexRequested;
            Undo.RecordObject(transform, "Set Scene Sibling Index");
            transform.SetSiblingIndex(newSiblingIndexApplied);

            JsonData result = JsonResultBuilder.CreateObject();
            result["sceneName"] = session.SceneName;
            result["objectPath"] = objectPath;
            result["siblingIndex"] = siblingIndex;
            result["instanceID"] = target.GetInstanceID();
            result["oldSiblingIndex"] = oldSiblingIndex;
            result["newSiblingIndexRequested"] = newSiblingIndexRequested;
            result["newSiblingIndexApplied"] = newSiblingIndexApplied;
            return result;
        }

        private static JsonData ExecuteAddComponent(SceneEditSession session, JsonData opParams)
        {
            CommandParams parameters = new CommandParams(opParams);
            string objectPath = parameters.GetString("objectPath", null);
            int siblingIndex = parameters.GetInt("siblingIndex", 0);
            string componentTypeName = parameters.GetString("componentType", null);

            GameObject target = session.FindGameObjectOrThrow(objectPath, siblingIndex);
            Type componentType = SceneEditSession.ResolveComponentTypeOrThrow(componentTypeName);

            if (Attribute.IsDefined(componentType, typeof(DisallowMultipleComponent), true) && target.GetComponent(componentType) != null)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.ComponentAlreadyExists + ": 组件已存在且不允许重复添加: " + componentType.FullName);
            }

            Component added = Undo.AddComponent(target, componentType);
            if (added == null)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.TypeMismatch + ": 添加组件失败,组件可能与目标对象不兼容: " + componentType.FullName);
            }

            if (parameters.Has("initialProperties"))
            {
                JsonData initialProperties = parameters.GetData()["initialProperties"];
                if (initialProperties != null && initialProperties.GetJsonType() != JsonType.None)
                {
                    if (!initialProperties.IsObject)
                    {
                        throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": initialProperties must be an object");
                    }

                    if (initialProperties.Count > 0)
                    {
                        Prefab.Handlers.PrefabComponentPropertyWriter.ApplyProperties(added, null, initialProperties);
                    }
                }
            }

            JsonData result = JsonResultBuilder.CreateObject();
            result["sceneName"] = session.SceneName;
            result["objectPath"] = objectPath;
            result["instanceID"] = target.GetInstanceID();
            result["componentType"] = added.GetType().FullName;
            result["componentIndex"] = SceneEditUtilities.GetComponentIndex(target, added);
            result["componentInstanceID"] = added.GetInstanceID();
            return result;
        }

        private static JsonData ExecuteSetComponentProperties(SceneEditSession session, JsonData opParams)
        {
            CommandParams parameters = new CommandParams(opParams);
            string objectPath = parameters.GetString("objectPath", null);
            int siblingIndex = parameters.GetInt("siblingIndex", 0);
            string componentTypeName = parameters.GetString("componentType", null);
            int componentIndex = parameters.GetInt("componentIndex", 0);

            if (!parameters.Has("properties"))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": properties is required");
            }

            JsonData properties = parameters.GetData()["properties"];
            if (properties == null || properties.GetJsonType() == JsonType.None || !properties.IsObject)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.TypeMismatch + ": properties必须是object类型");
            }

            GameObject target = session.FindGameObjectOrThrow(objectPath, siblingIndex);
            Type componentType = SceneEditSession.ResolveComponentTypeOrThrow(componentTypeName);
            Component component = session.FindComponentOrThrow(target, componentType, componentIndex);
            List<Prefab.Handlers.PrefabComponentPropertyWriter.PropertyWriteResult> modifiedProperties =
                Prefab.Handlers.PrefabComponentPropertyWriter.ApplyProperties(component, null, properties);

            JsonData result = JsonResultBuilder.CreateObject();
            result["sceneName"] = session.SceneName;
            result["objectPath"] = objectPath;
            result["instanceID"] = target.GetInstanceID();
            result["componentType"] = component.GetType().FullName;
            result["componentIndex"] = componentIndex;
            result["componentInstanceID"] = component.GetInstanceID();
            result["modifiedProperties"] = BuildComponentModifiedProperties(modifiedProperties);
            return result;
        }

        private static JsonData ExecuteDeleteComponent(SceneEditSession session, JsonData opParams)
        {
            CommandParams parameters = new CommandParams(opParams);
            string objectPath = parameters.GetString("objectPath", null);
            int siblingIndex = parameters.GetInt("siblingIndex", 0);
            string componentTypeName = parameters.GetString("componentType", null);
            int componentIndex = parameters.GetInt("componentIndex", 0);

            GameObject target = session.FindGameObjectOrThrow(objectPath, siblingIndex);
            Type componentType = SceneEditSession.ResolveComponentTypeOrThrow(componentTypeName);
            Component component = session.FindComponentOrThrow(target, componentType, componentIndex);

            if (component is Transform)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.CannotDeleteRequiredComponent + ": 不能删除Transform/RectTransform组件");
            }

            int deletedComponentInstanceID = component.GetInstanceID();
            string deletedComponentType = component.GetType().FullName;
            Undo.DestroyObjectImmediate(component);

            JsonData result = JsonResultBuilder.CreateObject();
            result["sceneName"] = session.SceneName;
            result["objectPath"] = objectPath;
            result["instanceID"] = target.GetInstanceID();
            result["deletedComponentType"] = deletedComponentType;
            result["deletedComponentIndex"] = componentIndex;
            result["deletedComponentInstanceID"] = deletedComponentInstanceID;
            return result;
        }

        private static JsonData ExecuteSetTransform(SceneEditSession session, JsonData opParams)
        {
            CommandParams parameters = new CommandParams(opParams);
            string objectPath = parameters.GetString("objectPath", null);
            int siblingIndex = parameters.GetInt("siblingIndex", 0);

            bool hasLocalPosition = TryReadVector3(parameters, "localPosition", out Vector3 localPosition);
            bool hasLocalRotationEuler = TryReadVector3(parameters, "localRotationEuler", out Vector3 localRotationEuler);
            bool hasLocalScale = TryReadVector3(parameters, "localScale", out Vector3 localScale);
            if (!hasLocalPosition && !hasLocalRotationEuler && !hasLocalScale)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": 必须至少提供一个字段: localPosition/localRotationEuler/localScale");
            }

            GameObject target = session.FindGameObjectOrThrow(objectPath, siblingIndex);
            Transform transform = target.transform;
            Vector3 oldLocalPosition = transform.localPosition;
            Vector3 oldLocalRotationEuler = transform.localEulerAngles;
            Vector3 oldLocalScale = transform.localScale;

            Undo.RecordObject(transform, "Set Scene Transform");
            if (hasLocalPosition) transform.localPosition = localPosition;
            if (hasLocalRotationEuler) transform.localEulerAngles = localRotationEuler;
            if (hasLocalScale) transform.localScale = localScale;

            JsonData modifiedFields = JsonResultBuilder.CreateArray();
            JsonData result = JsonResultBuilder.CreateObject();
            if (hasLocalPosition && transform.localPosition != oldLocalPosition)
            {
                modifiedFields.Add("localPosition");
                result["localPosition"] = BuildVector3Json(transform.localPosition);
            }
            if (hasLocalRotationEuler && transform.localEulerAngles != oldLocalRotationEuler)
            {
                modifiedFields.Add("localRotationEuler");
                result["localRotationEuler"] = BuildVector3Json(transform.localEulerAngles);
            }
            if (hasLocalScale && transform.localScale != oldLocalScale)
            {
                modifiedFields.Add("localScale");
                result["localScale"] = BuildVector3Json(transform.localScale);
            }

            result["sceneName"] = session.SceneName;
            result["objectPath"] = objectPath;
            result["siblingIndex"] = siblingIndex;
            result["instanceID"] = target.GetInstanceID();
            result["modifiedFields"] = modifiedFields;
            return result;
        }

        private static JsonData ExecuteSetRectTransform(SceneEditSession session, JsonData opParams)
        {
            CommandParams parameters = new CommandParams(opParams);
            string objectPath = parameters.GetString("objectPath", null);
            int siblingIndex = parameters.GetInt("siblingIndex", 0);

            bool hasAnchorMin = TryReadVector2(parameters, "anchorMin", out Vector2 anchorMin);
            bool hasAnchorMax = TryReadVector2(parameters, "anchorMax", out Vector2 anchorMax);
            bool hasPivot = TryReadVector2(parameters, "pivot", out Vector2 pivot);
            bool hasAnchoredPosition = TryReadVector2(parameters, "anchoredPosition", out Vector2 anchoredPosition);
            bool hasSizeDelta = TryReadVector2(parameters, "sizeDelta", out Vector2 sizeDelta);
            bool hasOffsetMin = TryReadVector2(parameters, "offsetMin", out Vector2 offsetMin);
            bool hasOffsetMax = TryReadVector2(parameters, "offsetMax", out Vector2 offsetMax);
            if (!hasAnchorMin && !hasAnchorMax && !hasPivot && !hasAnchoredPosition && !hasSizeDelta && !hasOffsetMin && !hasOffsetMax)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": 必须至少提供一个字段: anchorMin/anchorMax/pivot/anchoredPosition/sizeDelta/offsetMin/offsetMax");
            }

            GameObject target = session.FindGameObjectOrThrow(objectPath, siblingIndex);
            RectTransform rectTransform = target.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.ComponentNotFound + ": RectTransform不存在: " + objectPath);
            }

            Vector2 mergedAnchorMin = hasAnchorMin ? anchorMin : rectTransform.anchorMin;
            Vector2 mergedAnchorMax = hasAnchorMax ? anchorMax : rectTransform.anchorMax;
            ValidateAnchorsOrThrow(mergedAnchorMin, mergedAnchorMax, objectPath);

            Vector2 oldAnchorMin = rectTransform.anchorMin;
            Vector2 oldAnchorMax = rectTransform.anchorMax;
            Vector2 oldPivot = rectTransform.pivot;
            Vector2 oldAnchoredPosition = rectTransform.anchoredPosition;
            Vector2 oldSizeDelta = rectTransform.sizeDelta;
            Vector2 oldOffsetMin = rectTransform.offsetMin;
            Vector2 oldOffsetMax = rectTransform.offsetMax;

            Undo.RecordObject(rectTransform, "Set Scene RectTransform");
            if (hasAnchorMin) rectTransform.anchorMin = anchorMin;
            if (hasAnchorMax) rectTransform.anchorMax = anchorMax;
            if (hasPivot) rectTransform.pivot = pivot;
            if (hasAnchoredPosition) rectTransform.anchoredPosition = anchoredPosition;
            if (hasSizeDelta) rectTransform.sizeDelta = sizeDelta;
            if (hasOffsetMin) rectTransform.offsetMin = offsetMin;
            if (hasOffsetMax) rectTransform.offsetMax = offsetMax;

            JsonData modifiedFields = JsonResultBuilder.CreateArray();
            JsonData result = JsonResultBuilder.CreateObject();
            if (hasAnchorMin && rectTransform.anchorMin != oldAnchorMin)
            {
                modifiedFields.Add("anchorMin");
                result["anchorMin"] = BuildVector2Json(rectTransform.anchorMin);
            }
            if (hasAnchorMax && rectTransform.anchorMax != oldAnchorMax)
            {
                modifiedFields.Add("anchorMax");
                result["anchorMax"] = BuildVector2Json(rectTransform.anchorMax);
            }
            if (hasPivot && rectTransform.pivot != oldPivot)
            {
                modifiedFields.Add("pivot");
                result["pivot"] = BuildVector2Json(rectTransform.pivot);
            }
            if (hasAnchoredPosition && rectTransform.anchoredPosition != oldAnchoredPosition)
            {
                modifiedFields.Add("anchoredPosition");
                result["anchoredPosition"] = BuildVector2Json(rectTransform.anchoredPosition);
            }
            if (hasSizeDelta && rectTransform.sizeDelta != oldSizeDelta)
            {
                modifiedFields.Add("sizeDelta");
                result["sizeDelta"] = BuildVector2Json(rectTransform.sizeDelta);
            }
            if (hasOffsetMin && rectTransform.offsetMin != oldOffsetMin)
            {
                modifiedFields.Add("offsetMin");
                result["offsetMin"] = BuildVector2Json(rectTransform.offsetMin);
            }
            if (hasOffsetMax && rectTransform.offsetMax != oldOffsetMax)
            {
                modifiedFields.Add("offsetMax");
                result["offsetMax"] = BuildVector2Json(rectTransform.offsetMax);
            }

            result["sceneName"] = session.SceneName;
            result["objectPath"] = objectPath;
            result["siblingIndex"] = siblingIndex;
            result["instanceID"] = target.GetInstanceID();
            result["modifiedFields"] = modifiedFields;
            return result;
        }

        private static GameObject ResolveParentOrThrow(SceneEditSession session, string parentPath, int parentSiblingIndex)
        {
            if (string.IsNullOrWhiteSpace(parentPath))
            {
                if (parentSiblingIndex != 0)
                {
                    throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": parentSiblingIndex must be 0 when parentPath is omitted");
                }

                return null;
            }

            return session.FindGameObjectOrThrow(parentPath, parentSiblingIndex, "parentPath");
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

        private static bool IsChildOf(Transform target, Transform source)
        {
            if (target == null || source == null)
            {
                return false;
            }

            Transform current = target;
            while (current != null)
            {
                if (current == source)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static JsonData BuildGameObjectModifiedProperties(List<GameObjectPropertyModifier.PropertyChange> modifiedProperties)
        {
            JsonData modifiedPropsArray = JsonResultBuilder.CreateArray();
            foreach (GameObjectPropertyModifier.PropertyChange change in modifiedProperties)
            {
                JsonData changeObj = JsonResultBuilder.CreateObject();
                changeObj["name"] = change.name;
                changeObj["oldValue"] = SceneEditCommon.ConvertJsonValue(change.oldValue);
                changeObj["newValue"] = SceneEditCommon.ConvertJsonValue(change.newValue);
                modifiedPropsArray.Add(changeObj);
            }

            return modifiedPropsArray;
        }

        private static JsonData BuildComponentModifiedProperties(List<Prefab.Handlers.PrefabComponentPropertyWriter.PropertyWriteResult> modifiedProperties)
        {
            JsonData array = JsonResultBuilder.CreateArray();
            if (modifiedProperties == null)
            {
                return array;
            }

            foreach (Prefab.Handlers.PrefabComponentPropertyWriter.PropertyWriteResult property in modifiedProperties)
            {
                JsonData item = JsonResultBuilder.CreateObject();
                item["name"] = property.name ?? string.Empty;
                item["oldValue"] = property.oldValue ?? new JsonData();
                item["newValue"] = property.newValue ?? new JsonData();
                array.Add(item);
            }

            return array;
        }

        private static bool TryReadVector3(CommandParams parameters, string key, out Vector3 value)
        {
            value = Vector3.zero;
            if (parameters == null || string.IsNullOrEmpty(key) || !parameters.Has(key))
            {
                return false;
            }

            JsonData data = parameters.GetData()[key];
            if (data == null || data.GetJsonType() == JsonType.None)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.TypeMismatch + ": " + key + " must be an object");
            }

            value = ReadVector3(data, key);
            return true;
        }

        private static Vector3 ReadVector3(JsonData value, string originalPath)
        {
            if (value == null || !value.IsObject)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.TypeMismatch + ": 期望object类型: " + originalPath);
            }

            float x = ReadObjectFloat(value, "x", originalPath);
            float y = ReadObjectFloat(value, "y", originalPath);
            float z = ReadObjectFloat(value, "z", originalPath);
            return new Vector3(x, y, z);
        }

        private static JsonData BuildVector3Json(Vector3 value)
        {
            JsonData json = JsonResultBuilder.CreateObject();
            json["x"] = value.x;
            json["y"] = value.y;
            json["z"] = value.z;
            return json;
        }

        private static bool TryReadVector2(CommandParams parameters, string key, out Vector2 value)
        {
            value = Vector2.zero;
            if (parameters == null || string.IsNullOrEmpty(key) || !parameters.Has(key))
            {
                return false;
            }

            JsonData data = parameters.GetData()[key];
            if (data == null || data.GetJsonType() == JsonType.None)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.TypeMismatch + ": " + key + " must be an object");
            }

            value = ReadVector2(data, key);
            return true;
        }

        private static Vector2 ReadVector2(JsonData value, string originalPath)
        {
            if (value == null || !value.IsObject)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.TypeMismatch + ": 期望object类型: " + originalPath);
            }

            float x = ReadObjectFloat(value, "x", originalPath);
            float y = ReadObjectFloat(value, "y", originalPath);
            return new Vector2(x, y);
        }

        private static JsonData BuildVector2Json(Vector2 value)
        {
            JsonData json = JsonResultBuilder.CreateObject();
            json["x"] = value.x;
            json["y"] = value.y;
            return json;
        }

        private static float ReadObjectFloat(JsonData parent, string key, string originalPath)
        {
            if (parent == null || !parent.IsObject || string.IsNullOrEmpty(key))
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.TypeMismatch + ": 期望object类型: " + originalPath);
            }

            if (!parent.ContainsKey(key))
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.TypeMismatch + ": 缺少字段" + key + ": " + originalPath);
            }

            JsonData v = parent[key];
            if (v.IsDouble) return (float)(double)v;
            if (v.IsInt) return (int)v;
            if (v.IsLong) return (long)v;
            if (v.IsString && float.TryParse(v.ToString(), out float parsed)) return parsed;
            throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.TypeMismatch + ": 期望float类型: " + originalPath + "." + key);
        }

        private static void ValidateAnchorsOrThrow(Vector2 anchorMin, Vector2 anchorMax, string objectPath)
        {
            if (anchorMin.x > anchorMax.x || anchorMin.y > anchorMax.y)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": anchorMin must be <= anchorMax for both x and y, objectPath=" + objectPath);
            }
        }
    }
}
