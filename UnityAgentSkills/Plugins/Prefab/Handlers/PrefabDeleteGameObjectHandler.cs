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
            CommandParams parameters = new CommandParams(rawParams);

            string prefabPath = parameters.GetString("prefabPath", null);
            string normalizedPrefabPath = PrefabComponentHandlerUtils.NormalizePrefabPath(prefabPath);
            PrefabComponentHandlerUtils.ValidatePrefabPathOrThrow(normalizedPrefabPath);
            string objectPath = parameters.GetString("objectPath", null);
            int siblingIndex = parameters.GetInt("siblingIndex", 0);

            // 需要在 Prefab 编辑场景中加载,才能安全修改并保存到资源文件.
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(normalizedPrefabPath);
            if (prefabRoot == null)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.PrefabNotFound + ": 预制体文件不存在: " + normalizedPrefabPath);
            }

            try
            {
                GameObject target = PrefabComponentHandlerUtils.FindGameObjectOrThrow(prefabRoot, objectPath, siblingIndex, "objectPath");

                // 根节点是预制体载体,不允许删除.
                if (target == prefabRoot)
                {
                    throw new InvalidOperationException("CANNOT_DELETE_ROOT: 不能删除预制体根节点: " + objectPath);
                }

                string deletedObjectPath = GameObjectPathFinder.GetPath(target);
                int deletedInstanceID = target.GetInstanceID();
                int totalDeletedCount = CountGameObjects(target, true); // 包含所有子物体

                // 通过 Undo 删除,保留编辑器回退链路.
                Undo.DestroyObjectImmediate(target);

                bool saved = PrefabUtility.SaveAsPrefabAsset(prefabRoot, normalizedPrefabPath);

                JsonData result = JsonResultBuilder.CreateObject();
                result["prefabPath"] = normalizedPrefabPath;
                result["deletedObjectPath"] = deletedObjectPath;
                result["deletedInstanceID"] = deletedInstanceID;
                result["deletedObjectCount"] = 1;
                result["totalDeletedCount"] = totalDeletedCount;
                result["saved"] = saved;

                return result;
            }
            finally
            {
                // 无论成功或失败都卸载编辑场景,避免残留临时对象.
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
