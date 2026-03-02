using UnityEngine;

namespace UnityAgentSkills.Utils
{
    /// <summary>
    /// GameObject路径定位工具,支持通过层级路径查找GameObject.
    /// </summary>
    internal static class GameObjectPathFinder
    {
        /// <summary>
        /// 根据路径查找GameObject.
        /// </summary>
        /// <param name="root">根GameObject.</param>
        /// <param name="path">路径字符串,如 "Root/Child/Grandchild".</param>
        /// <param name="siblingIndex">同名对象序号,用于定位同路径下的同名对象,默认为0(第一个).</param>
        /// <returns>找到的GameObject,未找到返回null.</returns>
        public static GameObject FindByPath(GameObject root, string path, int siblingIndex = 0)
        {
            if (root == null || string.IsNullOrEmpty(path))
            {
                return null;
            }

            // 根路径命中时仅允许 siblingIndex=0,避免忽略调用方传入的定位信息.
            if (path == root.name)
            {
                return siblingIndex == 0 ? root : null;
            }


            string[] parts = path.Split('/');
            if (parts.Length == 0)
            {
                return null;
            }

            if (parts[0] != root.name)
            {
                return null;
            }

            // 使用回溯匹配中间路径,避免中间层同名节点时错误选中第一条分支.
            return FindByPathRecursive(root.transform, parts, 1, siblingIndex);
        }

        /// <summary>
        /// 递归匹配路径段.
        /// 仅在最后一段应用 siblingIndex,中间段采用回溯寻找可达分支.
        /// </summary>
        private static GameObject FindByPathRecursive(Transform current, string[] parts, int depth, int siblingIndex)
        {
            if (current == null || parts == null || depth >= parts.Length)
            {
                return null;
            }

            bool isLastSegment = depth == parts.Length - 1;
            int currentSiblingIndex = 0;

            for (int i = 0; i < current.childCount; i++)
            {
                Transform child = current.GetChild(i);
                if (child == null || child.name != parts[depth])
                {
                    continue;
                }

                if (isLastSegment)
                {
                    if (currentSiblingIndex == siblingIndex)
                    {
                        return child.gameObject;
                    }

                    currentSiblingIndex++;
                    continue;
                }

                GameObject found = FindByPathRecursive(child, parts, depth + 1, siblingIndex);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        /// <summary>
        /// 获取同名节点中的 siblingIndex.
        /// </summary>
        public static int GetSameNameSiblingIndex(GameObject target)
        {
            return target == null ? 0 : GetSameNameSiblingIndex(target.transform);
        }

        /// <summary>
        /// 获取同名 Transform 节点中的 siblingIndex.
        /// </summary>
        public static int GetSameNameSiblingIndex(Transform target)
        {
            if (target == null)
            {
                return 0;
            }

            Transform parent = target.parent;
            if (parent == null)
            {
                // 根节点没有同级同名统计语义,保持为 0.
                return 0;
            }

            int index = 0;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child == null || child.name != target.name)
                {
                    continue;
                }

                if (child == target)
                {
                    return index;
                }

                index++;
            }

            return 0;
        }

        /// <summary>
        /// 获取GameObject的完整路径.
        /// </summary>
        /// <param name="go">目标GameObject.</param>
        /// <returns>完整路径,如 "Root/Child/Grandchild".</returns>
        public static string GetPath(GameObject go)
        {
            if (go == null)
            {
                return "";
            }

            string path = go.name;
            Transform current = go.transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }
    }
}
