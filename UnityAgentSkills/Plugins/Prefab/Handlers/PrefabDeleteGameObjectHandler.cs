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
    /// prefab.deleteGameObject命令处理器.
    /// 删除预制体中的指定GameObject(级联删除所有子物体).
    /// </summary>
    internal static class PrefabDeleteGameObjectHandler
    {
        /// <summary>
        /// 支持的命令类型.
        /// </summary>
        public const string CommandType = "prefab.deleteGameObject";

        /// <summary>
        /// 执行预制体GameObject删除命令.
        /// </summary>
        /// <param name="rawParams">命令参数json.</param>
        /// <returns>结果json.</returns>
        public static JsonData Execute(JsonData rawParams)
        {
            // 1. 参数解析
            CommandParams parameters = new CommandParams(rawParams);

            string prefabPath = parameters.GetString("prefabPath", null);
            string objectPath = parameters.GetString("objectPath", null);
            int siblingIndex = parameters.GetInt("siblingIndex", 0);

            // 2. 参数验证
            if (string.IsNullOrEmpty(prefabPath))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": prefabPath is required");
            }
            if (string.IsNullOrEmpty(objectPath))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": objectPath is required");
            }

            // 3. 加载预制体到编辑场景（使用 PrefabUtility.LoadPrefabContents 以支持预制体编辑）
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabRoot == null)
            {
                throw new InvalidOperationException("Prefab not found at path: " + prefabPath);
            }

            try
            {
                // 4. 定位目标GameObject
                GameObject target = GameObjectPathFinder.FindByPath(prefabRoot, objectPath, siblingIndex);
                if (target == null)
                {
                    throw new InvalidOperationException("GameObject not found at path: " + objectPath + " (siblingIndex=" + siblingIndex + ")");
                }

                // 5. 验证不是根节点
                if (target == prefabRoot)
                {
                    throw new InvalidOperationException("CANNOT_DELETE_ROOT: 不能删除预制体根节点: " + objectPath);
                }

                // 6. 记录删除信息
                string deletedObjectPath = GameObjectPathFinder.GetPath(target);
                int deletedInstanceID = target.GetInstanceID();
                int totalDeletedCount = CountGameObjects(target, true); // 包含所有子物体

                // 7. 使用Undo系统删除GameObject
                Undo.DestroyObjectImmediate(target);

                // 8. 保存预制体
                bool saved = PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);

                // 9. 构建结果
                JsonData result = JsonResultBuilder.CreateObject();
                result["prefabPath"] = prefabPath;
                result["deletedObjectPath"] = deletedObjectPath;
                result["deletedInstanceID"] = deletedInstanceID;
                result["deletedObjectCount"] = 1;
                result["totalDeletedCount"] = totalDeletedCount;
                result["saved"] = saved;

                return result;
            }
            finally
            {
                // 10. 无论成功或失败，都要卸载预制体编辑场景
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        /// <summary>
        /// 统计GameObject及其子物体的总数.
        /// </summary>
        /// <param name="root">根GameObject.</param>
        /// <param name="includeSelf">是否包含自身.</param>
        /// <returns>GameObject总数.</returns>
        private static int CountGameObjects(GameObject root, bool includeSelf)
        {
            if (root == null)
            {
                return 0;
            }

            int count = includeSelf ? 1 : 0;
            
            foreach (Transform child in root.transform)
            {
                count += CountGameObjects(child.gameObject, true);
            }

            return count;
        }
    }
}
