using System;
using System.Collections.Generic;
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

            GameObject prefab = PrefabLoader.LoadPrefab(normalizedPrefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.PrefabNotFound + ": 预制体文件不存在: " + normalizedPrefabPath);
            }
            bool includeInactive = parameters.GetBool("includeInactive", true);
            int maxDepth = parameters.GetInt("maxDepth", -1);
            string[] nameContains = ReadNameContains(parameters);
            int maxResults = parameters.GetInt("maxResults", HierarchySearchSemantics.DefaultMaxMatches);
            HierarchySearchQuery searchQuery = HierarchySearchSemantics.BuildOrDefault(null, nameContains, maxResults);

            if (searchQuery != null)
            {
                int totalMatches;
                List<HierarchyNode> matches = HierarchyTraverser.TraverseAndFilter(
                    prefab,
                    includeInactive,
                    maxDepth,
                    searchQuery,
                    out totalMatches);

                return HierarchyJsonBuilder.BuildMatchesResult(normalizedPrefabPath, prefab, matches, totalMatches);
            }

            HierarchyNode hierarchy = HierarchyTraverser.Traverse(prefab, includeInactive, maxDepth);
            return HierarchyJsonBuilder.BuildHierarchyResult(normalizedPrefabPath, prefab, hierarchy);
        }

        /// <summary>
        /// 读取 nameContains 数组参数.
        /// </summary>
        /// <param name="parameters">命令参数.</param>
        /// <returns>规范化后的关键词数组,无有效词项时返回 null.</returns>
        private static string[] ReadNameContains(CommandParams parameters)
        {
            if (parameters == null || !parameters.Has("nameContains"))
            {
                return null;
            }

            JsonData data = parameters.GetData()["nameContains"];
            if (data == null || data.GetJsonType() == JsonType.None || !data.IsArray)
            {
                throw new ArgumentException(
                    UnityAgentSkillCommandErrorCodes.InvalidFields + ": nameContains must be an array of strings");
            }

            List<string> keywords = new List<string>();
            for (int i = 0; i < data.Count; i++)
            {
                if (data[i] == null || data[i].GetJsonType() != JsonType.String)
                {
                    throw new ArgumentException(
                        UnityAgentSkillCommandErrorCodes.InvalidFields + ": nameContains must be an array of strings");
                }

                string keyword = ((string)data[i]).Trim();
                if (!string.IsNullOrEmpty(keyword))
                {
                    keywords.Add(keyword);
                }
            }

            return keywords.Count > 0 ? keywords.ToArray() : null;
        }
    }
}
