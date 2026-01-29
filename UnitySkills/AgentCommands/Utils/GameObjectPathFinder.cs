using UnityEngine;
using System;

namespace AgentCommands.Utils
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
        /// <returns>找到的GameObject,未找到返回null.</returns>
        public static GameObject FindByPath(GameObject root, string path)
        {
            if (root == null || string.IsNullOrEmpty(path))
            {
                return null;
            }

            // 如果路径就是根对象名
            if (path == root.name)
            {
                return root;
            }

            // 分割路径
            string[] parts = path.Split('/');
            if (parts.Length == 0)
            {
                return null;
            }

            // 验证根对象名
            if (parts[0] != root.name)
            {
                return null;
            }

            // 递归查找
            GameObject current = root;
            for (int i = 1; i < parts.Length; i++)
            {
                bool found = false;
                for (int j = 0; j < current.transform.childCount; j++)
                {
                    if (current.transform.GetChild(j).name == parts[i])
                    {
                        current = current.transform.GetChild(j).gameObject;
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    return null;
                }
            }

            return current;
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
