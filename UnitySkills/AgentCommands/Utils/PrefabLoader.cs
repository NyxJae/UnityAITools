using UnityEngine;
using UnityEditor;

namespace AgentCommands.Utils
{
    /// <summary>
    /// 预制体加载工具类,提供统一的预制体加载接口.
    /// </summary>
    internal static class PrefabLoader
    {
        /// <summary>
        /// 加载预制体GameObject.
        /// </summary>
        /// <param name="prefabPath">预制体相对Assets的路径.</param>
        /// <returns>加载的GameObject,如果加载失败返回null.</returns>
        public static GameObject LoadPrefab(string prefabPath)
        {
            if (string.IsNullOrEmpty(prefabPath))
            {
                return null;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            return prefab;
        }

        /// <summary>
        /// 检查预制体路径是否有效.
        /// </summary>
        /// <param name="prefabPath">预制体路径.</param>
        /// <returns>路径是否有效.</returns>
        public static bool IsValidPrefabPath(string prefabPath)
        {
            if (string.IsNullOrEmpty(prefabPath))
            {
                return false;
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null;
        }
    }
}