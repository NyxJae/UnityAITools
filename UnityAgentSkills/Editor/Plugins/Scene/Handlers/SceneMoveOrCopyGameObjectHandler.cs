using System;
using LitJson2_utf;
using UnityAgentSkills.Core;
using UnityAgentSkills.Utils;
using UnityAgentSkills.Utils.JsonBuilders;
using UnityEditor;
using UnityEngine;

namespace UnityAgentSkills.Plugins.Scene.Handlers
{
    /// <summary>
    /// scene.moveOrCopyGameObject命令处理器.
    /// </summary>
    internal static class SceneMoveOrCopyGameObjectHandler
    {
        /// <summary>
        /// 支持的命令类型.
        /// </summary>
        public const string CommandType = "scene.moveOrCopyGameObject";

        /// <summary>
        /// 执行场景内GameObject移动或复制命令.
        /// </summary>
        public static JsonData Execute(JsonData rawParams)
        {
            CommandParams parameters = new CommandParams(rawParams);
            string sceneName = parameters.GetString("sceneName", null);
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

            SceneEditSession session = new SceneEditSession(sceneName);
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

            string newPath = GameObjectPathFinder.GetPath(sourceGO);
            string newParentPath = GameObjectPathFinder.GetPath(targetParent);
            session.Save();

            JsonData result = JsonResultBuilder.CreateObject();
            result["sceneName"] = session.SceneName;
            result["sourcePath"] = sourcePath;
            result["oldPath"] = oldPath;
            result["newPath"] = newPath;
            result["oldParentPath"] = oldParentPath;
            result["newParentPath"] = newParentPath;
            result["oldSiblingIndex"] = oldSiblingIndex;
            result["newSiblingIndexApplied"] = newSiblingIndex;
            result["operationType"] = "move";
            result["worldPositionPreserved"] = true;
            result["operatedInstanceID"] = sourceInstanceID;
            result["saved"] = true;
            return result;
        }

        private static JsonData ExecuteCopy(SceneEditSession session, GameObject sourceGO, GameObject targetParent, int targetSiblingIndex)
        {
            Vector3 sourceWorldPosition = sourceGO.transform.position;
            Quaternion sourceWorldRotation = sourceGO.transform.rotation;
            Vector3 sourceWorldScale = sourceGO.transform.lossyScale;
            string sourcePath = GameObjectPathFinder.GetPath(sourceGO);
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

            string copiedPath = GameObjectPathFinder.GetPath(copiedGO);
            int copiedInstanceID = copiedGO.GetInstanceID();
            session.Save();

            JsonData result = JsonResultBuilder.CreateObject();
            result["sceneName"] = session.SceneName;
            result["sourcePath"] = sourcePath;
            result["originalPath"] = sourcePath;
            result["copiedPath"] = copiedPath;
            result["oldParentPath"] = sourceGO.transform.parent != null ? GameObjectPathFinder.GetPath(sourceGO.transform.parent.gameObject) : string.Empty;
            result["newParentPath"] = targetParentPath;
            result["sourceSiblingIndex"] = sourceSiblingIndex;
            result["targetSiblingIndex"] = copiedSiblingIndex;
            result["operationType"] = "copy";
            result["worldPositionPreserved"] = true;
            result["originalInstanceID"] = sourceInstanceID;
            result["copiedInstanceID"] = copiedInstanceID;
            result["saved"] = true;
            return result;
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
    }
}
