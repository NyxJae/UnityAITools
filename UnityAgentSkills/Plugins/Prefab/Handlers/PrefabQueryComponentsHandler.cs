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
            string normalizedPrefabPath = PrefabComponentHandlerUtils.NormalizePrefabPath(prefabPath);
            PrefabComponentHandlerUtils.ValidatePrefabPathOrThrow(normalizedPrefabPath);
            string objectPath = parameters.GetString("objectPath", null);
            int siblingIndex = parameters.GetInt("siblingIndex", 0);

            // 加载预制体
            GameObject prefab = PrefabLoader.LoadPrefab(normalizedPrefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.PrefabNotFound + ": 预制体文件不存在: " + normalizedPrefabPath);
            }

            // 定位目标GameObject
            GameObject target = PrefabComponentHandlerUtils.FindGameObjectOrThrow(prefab, objectPath, siblingIndex, "objectPath");

            // 获取组件过滤参数
            string[] componentFilter = null;
            try
            {
                if (parameters.Has("componentFilter"))
                {
                    JsonData componentFilterData = parameters.GetData()["componentFilter"];
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
            catch (Exception ex)
            {
                Debug.LogError("[PrefabQueryComponentsHandler] Error getting componentFilter: " + ex.Message + "\n" + ex.StackTrace);
            }

            bool includePrivateFields = parameters.GetBool("includePrivateFields", false);

            // 读取组件
            var components = ComponentPropertyReader.ReadComponents(
                target, componentFilter, includePrivateFields);

            // 构建结果(使用ComponentJsonBuilder)
            return ComponentJsonBuilder.BuildComponentsResult(objectPath, target, components);
        }
    }
}
