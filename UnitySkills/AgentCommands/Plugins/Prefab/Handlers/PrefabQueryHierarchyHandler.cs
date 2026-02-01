using System;
using AgentCommands.Core;
using AgentCommands.Utils;
using AgentCommands.Utils.JsonBuilders;
using LitJson2_utf;
using UnityEngine;

namespace AgentCommands.Plugins.Prefab.Handlers
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
            
            // 参数验证
            if (string.IsNullOrEmpty(prefabPath))
            {
                throw new ArgumentException(AgentCommandErrorCodes.InvalidFields + ": prefabPath is required");
            }

            // 加载预制体
            GameObject prefab = PrefabLoader.LoadPrefab(prefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException("Prefab not found at path: " + prefabPath);
            }

            bool includeInactive = parameters.GetBool("includeInactive", true);
            int maxDepth = parameters.GetInt("maxDepth", -1);

            // 遍历层级树
            HierarchyNode hierarchy = HierarchyTraverser.Traverse(prefab, includeInactive, maxDepth);

            // 构建结果(使用HierarchyJsonBuilder)
            return HierarchyJsonBuilder.BuildHierarchyResult(prefabPath, prefab, hierarchy);
        }
    }
}
