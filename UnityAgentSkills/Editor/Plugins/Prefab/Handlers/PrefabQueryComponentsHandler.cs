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
        /// 读取 string[] 过滤参数,不填/null/空数组/全空词项时返回 null 表示不过滤.
        /// </summary>
        private static string[] ParseStringArray(JsonData rawParams, string key)
        {
            if (rawParams == null || !rawParams.IsObject || !rawParams.ContainsKey(key))
            {
                return null;
            }

            JsonData value = rawParams[key];
            if (value == null || !value.IsArray)
            {
                return null;
            }

            System.Collections.Generic.List<string> results = new System.Collections.Generic.List<string>();
            for (int i = 0; i < value.Count; i++)
            {
                JsonData item = value[i];
                string text = item == null ? null : item.ToString();
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                results.Add(text.Trim());
            }

            return results.Count > 0 ? results.ToArray() : null;
        }

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

            // 获取过滤参数.这里显式从 rawParams 读取 string[]，避免依赖隐式解析入口导致过滤失效。
            string[] componentFilter = null;
            string[] propertyFilter = null;
            try
            {
                JsonData paramsData = parameters.GetData();
                componentFilter = ParseStringArray(paramsData, "componentFilter");
                propertyFilter = ParseStringArray(paramsData, "propertyFilter");
            }
            catch (Exception ex)
            {
                Debug.LogError("[PrefabQueryComponentsHandler] Error getting filter params: " + ex.Message + "\n" + ex.StackTrace);
                throw;
            }

            // 读取组件
            var components = ComponentPropertyReader.ReadComponents(
                target, componentFilter, propertyFilter);

            // 构建结果(使用ComponentJsonBuilder)
            return ComponentJsonBuilder.BuildComponentsResult(objectPath, target, components);
        }
    }
}
