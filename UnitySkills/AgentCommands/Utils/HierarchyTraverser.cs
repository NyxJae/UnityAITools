using UnityEngine;
using System;
using System.Collections.Generic;

namespace AgentCommands.Utils
{
    /// <summary>
    /// 层级遍历结果.
    /// </summary>
    internal class HierarchyNode
    {
        public string name;
        public int instanceID;
        public string path;
        public int depth;
        public bool isActive;
        public List<HierarchyNode> children;

        public HierarchyNode()
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
                depth = 0,
                isActive = root.activeSelf
            };

            // 如果根对象被禁用且不包含禁用对象,直接返回
            if (!includeInactive && !root.activeSelf)
            {
                return rootNode;
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
    }
}
