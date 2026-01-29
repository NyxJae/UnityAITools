using System;
using AgentCommands.Core;
using AgentCommands.Utils;
using LitJson2_utf;
using UnityEngine;

namespace AgentCommands.Handlers
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
            CommandParams p = new CommandParams(rawParams);

            string prefabPath = p.GetString("prefabPath", null);
            
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

            bool includeInactive = p.GetBool("includeInactive", true);
            int maxDepth = p.GetInt("maxDepth");

            // 遍历层级树
            HierarchyNode hierarchy = HierarchyTraverser.Traverse(prefab, includeInactive, maxDepth);

            // 统计GameObject总数
            int totalGameObjects = CountGameObjects(hierarchy);

            // 构建结果
            JsonData result = new JsonData();
            result.SetJsonType(JsonType.Object);
            
            result["prefabPath"] = prefabPath ?? "";
            result["rootName"] = prefab.name ?? "";
            result["totalGameObjects"] = totalGameObjects;
            result["hierarchy"] = BuildHierarchyJson(hierarchy);

            return result;
        }

        /// <summary>
        /// 统计层级树中的GameObject总数.
        /// </summary>
        private static int CountGameObjects(HierarchyNode node)
        {
            if (node == null)
            {
                return 0;
            }

            int count = 1;
            foreach (var child in node.children)
            {
                count += CountGameObjects(child);
            }
            return count;
        }

        /// <summary>
        /// 构建层级树JSON数据.
        /// </summary>
        private static JsonData BuildHierarchyJson(HierarchyNode node)
        {
            JsonData json = new JsonData();
            json.SetJsonType(JsonType.Object);

            json["name"] = node.name ?? "";
            json["instanceID"] = node.instanceID;
            json["path"] = node.path ?? "";
            json["depth"] = node.depth;
            json["isActive"] = node.isActive;

            JsonData children = new JsonData();
            children.SetJsonType(JsonType.Array);
            foreach (var child in node.children)
            {
                children.Add(BuildHierarchyJson(child));
            }
            json["children"] = children;

            return json;
        }
    }
}
