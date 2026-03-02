using System;
using UnityAgentSkills.Core;
using UnityAgentSkills.Utils;
using UnityAgentSkills.Utils.JsonBuilders;
using LitJson2_utf;
using UnityEngine;

namespace UnityAgentSkills.Plugins.Prefab.Handlers
{
    /// <summary>
    /// prefab.queryHierarchy命令处理器.
    /// </summary>
    internal static class PrefabQueryHierarchyHandler
    {
        /// <summary>
        /// 支持的命令类型.
        /// </summary>
        public const string CommandType = "prefab.queryHierarchy";

        /// <summary>
        /// 执行预制体层级查询命令.
        /// </summary>
        /// <param name="rawParams">命令参数json.</param>
        /// <returns>结果json.</returns>
        public static JsonData Execute(JsonData rawParams)
        {
            CommandParams parameters = new CommandParams(rawParams);

            string prefabPath = parameters.GetString("prefabPath", null);
            string normalizedPrefabPath = PrefabComponentHandlerUtils.NormalizePrefabPath(prefabPath);
            PrefabComponentHandlerUtils.ValidatePrefabPathOrThrow(normalizedPrefabPath);

            // 加载预制体
            GameObject prefab = PrefabLoader.LoadPrefab(normalizedPrefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.PrefabNotFound + ": 预制体文件不存在: " + normalizedPrefabPath);
            }

            bool includeInactive = parameters.GetBool("includeInactive", true);
            int maxDepth = parameters.GetInt("maxDepth", -1);

            string nameContains = parameters.GetString("nameContains", null);
            int maxMatches = parameters.GetInt("maxMatches", 50);

            // nameContains 模式: 返回扁平 matches 列表(不返回 hierarchy)
            if (!string.IsNullOrEmpty(nameContains) && !string.IsNullOrEmpty(nameContains.Trim()))
            {
                int totalMatches;
                var matches = HierarchyTraverser.TraverseAndFilter(
                    prefab,
                    includeInactive,
                    maxDepth,
                    nameContains,
                    maxMatches,
                    out totalMatches);

                return HierarchyJsonBuilder.BuildMatchesResult(normalizedPrefabPath, prefab, matches, totalMatches);
            }

            // 默认模式: 遍历层级树
            HierarchyNode hierarchy = HierarchyTraverser.Traverse(prefab, includeInactive, maxDepth);

            // 构建结果(使用HierarchyJsonBuilder)
            return HierarchyJsonBuilder.BuildHierarchyResult(normalizedPrefabPath, prefab, hierarchy);
        }
    }
}
