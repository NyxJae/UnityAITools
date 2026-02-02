using System;
using UnityAgentSkills.Core;
using UnityAgentSkills.Utils;
using UnityAgentSkills.Utils.JsonBuilders;
using LitJson2_utf;
using UnityEngine;
using UnityEditor;

namespace UnityAgentSkills.Plugins.Prefab.Handlers
{
    /// <summary>
    /// prefab.moveOrCopyGameObject命令处理器.
    /// 移动或复制预制体中的GameObject到新的父节点.
    /// </summary>
    internal static class PrefabMoveOrCopyGameObjectHandler
    {
        /// <summary>
        /// 支持的命令类型.
        /// </summary>
        public const string CommandType = "prefab.moveOrCopyGameObject";

        /// <summary>
        /// 执行预制体GameObject移动或复制命令.
        /// </summary>
        /// <param name="rawParams">命令参数json.</param>
        /// <returns>结果json.</returns>
        public static JsonData Execute(JsonData rawParams)
        {
            // 1. 参数解析
            CommandParams parameters = new CommandParams(rawParams);

            string prefabPath = parameters.GetString("prefabPath", null);
            string sourcePath = parameters.GetString("sourcePath", null);
            int sourceSiblingIndex = parameters.GetInt("sourceSiblingIndex", 0);
            string targetParentPath = parameters.GetString("targetParentPath", null);
            int targetSiblingIndex = parameters.GetInt("targetSiblingIndex", -1);
            bool isCopy = parameters.GetBool("isCopy", false);

            // 2. 参数验证
            if (string.IsNullOrEmpty(prefabPath))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": prefabPath is required");
            }
            if (string.IsNullOrEmpty(sourcePath))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": sourcePath is required");
            }
            if (string.IsNullOrEmpty(targetParentPath))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": targetParentPath is required");
            }

            // 3. 加载预制体到编辑场景（使用 PrefabUtility.LoadPrefabContents 以支持预制体编辑）
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabRoot == null)
            {
                throw new InvalidOperationException("Prefab not found at path: " + prefabPath);
            }

            try
            {
                // 4. 定位源GameObject
                GameObject sourceGO = GameObjectPathFinder.FindByPath(prefabRoot, sourcePath, sourceSiblingIndex);
                if (sourceGO == null)
                {
                    throw new InvalidOperationException("GameObject not found at path: " + sourcePath + " (siblingIndex=" + sourceSiblingIndex + ")");
                }

                // 5. 定位目标父节点
                GameObject targetParent = GameObjectPathFinder.FindByPath(prefabRoot, targetParentPath);
                if (targetParent == null)
                {
                    throw new InvalidOperationException("GameObject not found at path: " + targetParentPath);
                }

                // 6. 验证移动/复制操作
                Transform sourceParent = sourceGO.transform.parent;

                // 移动操作: 验证不能移动到自身或子节点
                if (!isCopy)
                {
                    if (targetParent == sourceGO || IsChildOf(targetParent.transform, sourceGO.transform))
                    {
                        throw new InvalidOperationException("CANNOT_MOVE_TO_SELF_OR_CHILD: 不能将物体移动到其自身或其子节点下: " + sourcePath);
                    }
                }
                // 复制操作: 验证不能复制到原父节点
                else
                {
                    if (targetParent == sourceParent?.gameObject)
                    {
                        throw new InvalidOperationException("CANNOT_COPY_TO_SAME_PARENT: 不能将GameObject复制到其原父节点下");
                    }
                }

                // 7. 执行移动或复制操作
                JsonData result = isCopy 
                    ? ExecuteCopy(prefabRoot, prefabPath, sourceGO, targetParent, targetSiblingIndex)
                    : ExecuteMove(prefabRoot, prefabPath, sourceGO, targetParent, targetSiblingIndex, sourcePath);

                return result;
            }
            finally
            {
                // 8. 无论成功或失败，都要卸载预制体编辑场景
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        /// <summary>
        /// 执行移动操作.
        /// </summary>
        private static JsonData ExecuteMove(GameObject prefab, string prefabPath, GameObject sourceGO, GameObject targetParent, int targetSiblingIndex, string sourcePath)
        {
            // 记录移动前的世界坐标
            Vector3 oldWorldPosition = sourceGO.transform.position;
            Quaternion oldWorldRotation = sourceGO.transform.rotation;
            Vector3 oldWorldScale = sourceGO.transform.lossyScale;

            string oldPath = GameObjectPathFinder.GetPath(sourceGO);
            int oldSiblingIndex = sourceGO.transform.GetSiblingIndex();
            int sourceInstanceID = sourceGO.GetInstanceID();

            // 使用Undo系统修改父节点
            Undo.SetTransformParent(sourceGO.transform, targetParent.transform, "Move GameObject");

            // 设置新的子物体索引
            if (targetSiblingIndex >= 0 && targetSiblingIndex < targetParent.transform.childCount)
            {
                sourceGO.transform.SetSiblingIndex(targetSiblingIndex);
            }
            else
            {
                // 超出范围或未指定,移动到末尾
                sourceGO.transform.SetAsLastSibling();
            }

            // 恢复世界坐标
            sourceGO.transform.position = oldWorldPosition;
            sourceGO.transform.rotation = oldWorldRotation;
            
            // lossyScale 是只读的, 需要根据父物体的 scale 反向计算 localScale
            Transform currentParent = sourceGO.transform.parent;
            if (currentParent != null)
            {
                Vector3 parentScale = currentParent.lossyScale;
                // 防止除零,如果父节点scale为0则使用移动前的localScale
                sourceGO.transform.localScale = new Vector3(
                    parentScale.x != 0 ? oldWorldScale.x / parentScale.x : sourceGO.transform.localScale.x,
                    parentScale.y != 0 ? oldWorldScale.y / parentScale.y : sourceGO.transform.localScale.y,
                    parentScale.z != 0 ? oldWorldScale.z / parentScale.z : sourceGO.transform.localScale.z
                );
            }

            string newPath = GameObjectPathFinder.GetPath(sourceGO);
            int newSiblingIndex = sourceGO.transform.GetSiblingIndex();

            // 保存预制体
            bool saved = PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);

            // 构建结果
            JsonData result = JsonResultBuilder.CreateObject();
            result["prefabPath"] = prefabPath;
            result["sourcePath"] = sourcePath;
            result["oldPath"] = oldPath;
            result["newPath"] = newPath;
            result["oldSiblingIndex"] = oldSiblingIndex;
            result["newSiblingIndex"] = newSiblingIndex;
            result["operationType"] = "move";
            result["worldPositionPreserved"] = true;
            result["operatedInstanceID"] = sourceInstanceID;
            result["saved"] = saved;

            return result;
        }

        /// <summary>
        /// 执行复制操作.
        /// </summary>
        private static JsonData ExecuteCopy(GameObject prefab, string prefabPath, GameObject sourceGO, GameObject targetParent, int targetSiblingIndex)
        {
            // 记录原对象的世界坐标
            Vector3 sourceWorldPosition = sourceGO.transform.position;
            Quaternion sourceWorldRotation = sourceGO.transform.rotation;
            Vector3 sourceWorldScale = sourceGO.transform.lossyScale;

            string sourcePath = GameObjectPathFinder.GetPath(sourceGO);
            int sourceInstanceID = sourceGO.GetInstanceID();
            int sourceSiblingIndex = sourceGO.transform.GetSiblingIndex();

            // 复制GameObject
            GameObject copiedGO = (GameObject)UnityEngine.Object.Instantiate(sourceGO);
            
            // 使用Undo系统注册创建操作
            Undo.RegisterCreatedObjectUndo(copiedGO, "Copy GameObject");

            // 设置父节点
            copiedGO.transform.SetParent(targetParent.transform);

            // 设置子物体索引
            if (targetSiblingIndex >= 0 && targetSiblingIndex < targetParent.transform.childCount)
            {
                copiedGO.transform.SetSiblingIndex(targetSiblingIndex);
            }
            else
            {
                // 超出范围或未指定,放置到末尾
                copiedGO.transform.SetAsLastSibling();
            }

            // 恢复世界坐标
            copiedGO.transform.position = sourceWorldPosition;
            copiedGO.transform.rotation = sourceWorldRotation;
            
            // 根据父物体的 scale 反向计算 localScale
            Transform currentParent = copiedGO.transform.parent;
            if (currentParent != null)
            {
                Vector3 parentScale = currentParent.lossyScale;
                // 防止除零,如果父节点scale为0则使用原对象的localScale
                copiedGO.transform.localScale = new Vector3(
                    parentScale.x != 0 ? sourceWorldScale.x / parentScale.x : sourceGO.transform.localScale.x,
                    parentScale.y != 0 ? sourceWorldScale.y / parentScale.y : sourceGO.transform.localScale.y,
                    parentScale.z != 0 ? sourceWorldScale.z / parentScale.z : sourceGO.transform.localScale.z
                );
            }

            string copiedPath = GameObjectPathFinder.GetPath(copiedGO);
            int copiedInstanceID = copiedGO.GetInstanceID();
            int copiedSiblingIndex = copiedGO.transform.GetSiblingIndex();

            // 保存预制体
            bool saved = PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);

            // 构建结果
            JsonData result = JsonResultBuilder.CreateObject();
            result["prefabPath"] = prefabPath;
            result["sourcePath"] = sourcePath;
            result["originalPath"] = sourcePath;
            result["copiedPath"] = copiedPath;
            result["sourceSiblingIndex"] = sourceSiblingIndex;
            result["targetSiblingIndex"] = copiedSiblingIndex;
            result["operationType"] = "copy";
            result["worldPositionPreserved"] = true;
            result["originalInstanceID"] = sourceInstanceID;
            result["copiedInstanceID"] = copiedInstanceID;
            result["saved"] = saved;

            return result;
        }

        /// <summary>
        /// 检查target是否是source的子节点.
        /// </summary>
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
