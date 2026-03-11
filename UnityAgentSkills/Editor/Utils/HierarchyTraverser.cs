using UnityEngine;
using System;
using System.Collections.Generic;

namespace UnityAgentSkills.Utils
{
    /// <summary>
    /// 层级遍历结果.
    /// </summary>
    internal class HierarchyNode
    {
        internal string name;
        internal int instanceID;
        internal string path;
        internal int siblingIndex;
        internal int depth;
        internal bool isActive;
        internal string sceneName;
        internal string scenePath;
        internal List<HierarchyNode> children;

        internal HierarchyNode()
        {
            children = new List<HierarchyNode>();
        }
    }

    /// <summary>
    /// 层级遍历工具,支持深度控制和激活状态过滤.
    /// </summary>
    internal static class HierarchyTraverser
    {
        /// <summary>
        /// 遍历层级树.
        /// </summary>
        /// <param name="root">根GameObject.</param>
        /// <param name="includeInactive">是否包含禁用的GameObject.</param>
        /// <param name="maxDepth">最大深度,-1表示无限.</param>
        /// <returns>层级树结构.</returns>
        public static HierarchyNode Traverse(GameObject root, bool includeInactive = true, int maxDepth = -1)
        {
            if (root == null)
            {
                return null;
            }

            HierarchyNode rootNode = new HierarchyNode
            {
                name = root.name,
                instanceID = root.GetInstanceID(),
                path = GameObjectPathFinder.GetPath(root),
                siblingIndex = GameObjectPathFinder.GetSameNameSiblingIndex(root.transform),
                depth = 0,
                isActive = root.activeSelf
            };

            // 如果根对象被禁用且不包含禁用对象,直接过滤掉根节点及其子树
            if (!includeInactive && !root.activeSelf)
            {
                return null;
            }

            // 递归遍历子对象
            TraverseChildren(root.transform, rootNode, includeInactive, maxDepth, 0);

            return rootNode;
        }

        /// <summary>
        /// 递归遍历子对象.
        /// </summary>
        private static void TraverseChildren(Transform parent, HierarchyNode parentNode, 
            bool includeInactive, int maxDepth, int currentDepth)
        {
            // 检查深度限制
            if (maxDepth != -1 && currentDepth >= maxDepth)
            {
                return;
            }

            foreach (Transform child in parent)
            {
                // 检查激活状态
                if (!includeInactive && !child.gameObject.activeSelf)
                {
                    continue;
                }

                HierarchyNode childNode = new HierarchyNode
                {
                    name = child.name,
                    instanceID = child.gameObject.GetInstanceID(),
                    path = GameObjectPathFinder.GetPath(child.gameObject),
                    siblingIndex = GameObjectPathFinder.GetSameNameSiblingIndex(child),
                    depth = currentDepth + 1,
                    isActive = child.gameObject.activeSelf
                };

                parentNode.children.Add(childNode);

                // 递归遍历子对象的子对象
                TraverseChildren(child, childNode, includeInactive, maxDepth, currentDepth + 1);
            }
        }

        /// <summary>
        /// 将层级树转换为扁平列表(按深度优先顺序).
        /// </summary>
        public static List<HierarchyNode> Flatten(HierarchyNode root)
        {
            List<HierarchyNode> result = new List<HierarchyNode>();
            if (root == null)
            {
                return result;
            }

            FlattenRecursive(root, result);
            return result;
        }

        private static void FlattenRecursive(HierarchyNode node, List<HierarchyNode> result)
        {
            result.Add(node);
            foreach (var child in node.children)
            {
                FlattenRecursive(child, result);
            }
        }

        /// <summary>
        /// 遍历层级树并按名称过滤,返回扁平匹配列表.
        /// </summary>
        /// <param name="root">根GameObject.</param>
        /// <param name="nameContains">名称包含匹配模式(调用方需保证非空).</param>
        /// <param name="maxMatches">最多返回的匹配条数(>0).</param>
        /// <param name="totalMatches">总命中数(未截断).</param>
        /// <returns>匹配的扁平节点列表,返回前会做稳定排序与截断.</returns>
        public static List<HierarchyNode> TraverseAndFilter(
            GameObject root,
            bool includeInactive,
            int maxDepth,
            string nameContains,
            int maxMatches,
            out int totalMatches)
        {
            HierarchySearchQuery query = HierarchySearchSemantics.BuildOrDefault(nameContains, null, maxMatches);
            return TraverseAndFilter(root, includeInactive, maxDepth, query, out totalMatches);
        }

        /// <summary>
        /// 遍历层级树并按搜索查询过滤,返回扁平匹配列表.
        /// </summary>
        /// <param name="root">根GameObject.</param>
        /// <param name="includeInactive">是否包含禁用的GameObject.</param>
        /// <param name="maxDepth">最大深度,-1表示无限.0表示仅检查根节点.</param>
        /// <param name="searchQuery">搜索查询,支持多关键词 OR.</param>
        /// <param name="totalMatches">总命中数(未截断).</param>
        /// <returns>匹配的扁平节点列表,返回前会做稳定排序与截断.</returns>
        public static List<HierarchyNode> TraverseAndFilter(
            GameObject root,
            bool includeInactive,
            int maxDepth,
            HierarchySearchQuery searchQuery,
            out int totalMatches)
        {
            List<HierarchyNode> matches = new List<HierarchyNode>();
            totalMatches = 0;

            if (root == null)
            {
                return matches;
            }

            if (!includeInactive && !root.activeSelf)
            {
                return matches;
            }

            if (searchQuery == null || !searchQuery.HasSearch)
            {
                return matches;
            }

            TraverseAndFilterRecursive(root.transform, matches, includeInactive, maxDepth, 0, searchQuery, ref totalMatches);
            List<HierarchyNode> sortedMatches = HierarchySearchSemantics.SortMatches(matches);
            if (sortedMatches.Count > searchQuery.MaxMatches)
            {
                sortedMatches.RemoveRange(searchQuery.MaxMatches, sortedMatches.Count - searchQuery.MaxMatches);
            }

            return sortedMatches;
        }

        private static void TraverseAndFilterRecursive(
            Transform current,
            List<HierarchyNode> matches,
            bool includeInactive,
            int maxDepth,
            int currentDepth,
            HierarchySearchQuery searchQuery,
            ref int totalMatches)
        {
            if (!includeInactive && !current.gameObject.activeSelf)
            {
                return;
            }

            if (HierarchySearchSemantics.IsMatch(current.name, searchQuery))
            {
                totalMatches++;
                matches.Add(new HierarchyNode
                {
                    name = current.name,
                    instanceID = current.gameObject.GetInstanceID(),
                    path = GameObjectPathFinder.GetPath(current.gameObject),
                    siblingIndex = GameObjectPathFinder.GetSameNameSiblingIndex(current),
                    depth = currentDepth,
                    isActive = current.gameObject.activeSelf
                });
            }

            if (maxDepth != -1 && currentDepth >= maxDepth)
            {
                return;
            }

            foreach (Transform child in current)
            {
                TraverseAndFilterRecursive(child, matches, includeInactive, maxDepth, currentDepth + 1, searchQuery, ref totalMatches);
            }
        }
    }
}
