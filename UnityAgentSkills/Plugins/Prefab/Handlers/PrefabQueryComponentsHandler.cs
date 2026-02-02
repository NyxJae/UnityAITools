using System;
using UnityAgentSkills.Core;
using UnityAgentSkills.Utils;
using UnityAgentSkills.Utils.JsonBuilders;
using LitJson2_utf;
using UnityEngine;

namespace UnityAgentSkills.Plugins.Prefab.Handlers
{
    /// <summary>
    /// prefab.queryComponents命令处理器.
    /// </summary>
    internal static class PrefabQueryComponentsHandler
    {
        /// <summary>
        /// 支持的命令类型.
        /// </summary>
        public const string CommandType = "prefab.queryComponents";

        /// <summary>
        /// 执行预制体组件查询命令.
        /// </summary>
        /// <param name="rawParams">命令参数json.</param>
        /// <returns>结果json.</returns>
        public static JsonData Execute(JsonData rawParams)
        {
            CommandParams parameters = new CommandParams(rawParams);

            string prefabPath = parameters.GetString("prefabPath", null);
            string objectPath = parameters.GetString("objectPath", null);
            int siblingIndex = parameters.GetInt("siblingIndex", 0);

            // 参数验证
            if (string.IsNullOrEmpty(prefabPath))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": prefabPath is required");
            }
            if (string.IsNullOrEmpty(objectPath))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": objectPath is required");
            }

            // 加载预制体
            GameObject prefab = PrefabLoader.LoadPrefab(prefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException("Prefab not found at path: " + prefabPath);
            }

            // 定位目标GameObject
            GameObject target = GameObjectPathFinder.FindByPath(prefab, objectPath, siblingIndex);
            if (target == null)
            {
                throw new InvalidOperationException("GameObject not found at path: " + objectPath + " (siblingIndex=" + siblingIndex + ")");
            }

            // 获取组件过滤参数
            string[] componentFilter = null;
            try
            {
                if (parameters.Has("componentFilter"))
                {
                    JsonData componentFilterData = parameters.GetData()["componentFilter"];
                    // 检查是否为 null
                    if (componentFilterData == null)
                    {
                        componentFilter = null;
                    }
                    else if (componentFilterData.IsArray)
                    {
                        JsonData filterArray = componentFilterData;
                        componentFilter = new string[filterArray.Count];
                        for (int i = 0; i < filterArray.Count; i++)
                        {
                            componentFilter[i] = filterArray[i].ToString();
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[PrefabQueryComponentsHandler] Error getting componentFilter: {ex.Message}\n{ex.StackTrace}");
            }
            bool includeBuiltin = parameters.GetBool("includeBuiltin", false);
            bool includePrivateFields = parameters.GetBool("includePrivateFields", false);

            // 读取组件
            var components = ComponentPropertyReader.ReadComponents(
                target, componentFilter, includeBuiltin, includePrivateFields);

            // 构建结果(使用ComponentJsonBuilder)
            return ComponentJsonBuilder.BuildComponentsResult(objectPath, target, components);
        }
    }
}
