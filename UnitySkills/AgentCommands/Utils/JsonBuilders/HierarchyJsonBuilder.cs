using UnityEngine;
using AgentCommands.Utils;
using AgentCommands.Utils.JsonBuilders;
using LitJson2_utf;

namespace AgentCommands.Utils.JsonBuilders
{
    /// <summary>
    /// 层级结构JSON构建器,负责将HierarchyNode树转换为JSON格式.
    /// 从PrefabQueryHierarchyHandler抽取层级JSON构建逻辑.
    /// </summary>
    internal static class HierarchyJsonBuilder
    {
        /// <summary>
        /// 构建完整的层级查询结果JSON.
        /// </summary>
        /// <param name="prefabPath">预制体路径.</param>
        /// <param name="prefab">预制体GameObject.</param>
        /// <param name="hierarchy">层级树根节点.</param>
        /// <returns>层级查询结果JSON.</returns>
        public static JsonData BuildHierarchyResult(string prefabPath, GameObject prefab, HierarchyNode hierarchy)
        {
            JsonData result = JsonResultBuilder.CreateObject();
            result["prefabPath"] = prefabPath ?? "";
            result["rootName"] = prefab != null ? prefab.name : "";
            result["totalGameObjects"] = CountGameObjects(hierarchy);
            result["hierarchy"] = BuildHierarchyArray(hierarchy);

            return result;
        }

        /// <summary>
        /// 构建层级数组JSON(将单根节点包装为数组格式).
        /// </summary>
        /// <param name="root">层级树根节点.</param>
        /// <returns>层级数组JSON.</returns>
        private static JsonData BuildHierarchyArray(HierarchyNode root)
        {
            JsonData arr = JsonResultBuilder.CreateArray();
            if (root != null)
            {
                arr.Add(BuildGameObjectNode(root));
            }
            return arr;
        }

        /// <summary>
        /// 构建单个GameObject的JSON表示.
        /// </summary>
        /// <param name="node">层级节点.</param>
        /// <returns>GameObject的JSON表示.</returns>
        public static JsonData BuildGameObjectNode(HierarchyNode node)
        {
            JsonData json = JsonResultBuilder.CreateObject();

            json["name"] = node.name ?? "";
            json["instanceID"] = node.instanceID;
            json["path"] = node.path ?? "";
            json["siblingIndex"] = node.siblingIndex;
            json["depth"] = node.depth;
            json["isActive"] = node.isActive;

            JsonData children = JsonResultBuilder.CreateArray();
            foreach (var child in node.children)
            {
                children.Add(BuildGameObjectNode(child));
            }
            json["children"] = children;

            return json;
        }

        /// <summary>
        /// 统计层级树中的GameObject总数.
        /// </summary>
        /// <param name="root">层级树根节点.</param>
        /// <returns>GameObject总数.</returns>
        private static int CountGameObjects(HierarchyNode root)
        {
            if (root == null)
            {
                return 0;
            }

            int count = 1;
            foreach (var child in root.children)
            {
                count += CountGameObjects(child);
            }
            return count;
        }
    }
}
