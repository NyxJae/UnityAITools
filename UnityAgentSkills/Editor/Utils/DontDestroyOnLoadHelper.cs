using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityAgentSkills.Utils
{
    /// <summary>
    /// DontDestroyOnLoad 场景的访问辅助.
    /// Play 模式下通过临时 GameObject 获取该隐藏场景的引用和 root objects.
    /// </summary>
    internal static class DontDestroyOnLoadHelper
    {
        /// <summary>
        /// DontDestroyOnLoad 场景在结果中使用的固定名称.
        /// </summary>
        public const string SceneName = "DontDestroyOnLoad";

        /// <summary>
        /// 当前是否处于 Play 模式(仅 Play 模式下 DontDestroyOnLoad 场景才存在).
        /// </summary>
        public static bool IsAvailable
        {
            get { return EditorApplication.isPlaying; }
        }

        /// <summary>
        /// 获取 DontDestroyOnLoad 场景的 root GameObjects.
        /// 通过创建临时 GO 标记为 DontDestroyOnLoad 来获取该隐藏场景的引用.
        /// 仅在 Play 模式下可用, 非 Play 模式返回空数组.
        /// </summary>
        public static GameObject[] GetRootGameObjects()
        {
            if (!EditorApplication.isPlaying)
            {
                return new GameObject[0];
            }

            // DontDestroyOnLoad 场景无法通过 SceneManager 枚举,
            // 需要借助临时 GO 的 scene 属性间接获取引用.
            GameObject temp = new GameObject("__AgentSkills_DDOLProbe__");
            Object.DontDestroyOnLoad(temp);

            UnityEngine.SceneManagement.Scene ddolScene = temp.scene;
            Object.DestroyImmediate(temp);

            if (!ddolScene.IsValid() || !ddolScene.isLoaded)
            {
                return new GameObject[0];
            }

            // Unity 对象销毁后可能残留为 null 引用,做防御性过滤.
            GameObject[] allRoots = ddolScene.GetRootGameObjects();
            List<GameObject> validRoots = new List<GameObject>(allRoots.Length);
            foreach (GameObject go in allRoots)
            {
                if (go != null)
                {
                    validRoots.Add(go);
                }
            }
            return validRoots.ToArray();
        }
    }
}
