using System;
using System.Collections.Generic;
using LitJson2_utf;
using UnityAgentSkills.Core;
using UnityAgentSkills.Utils;
using UnityAgentSkills.Utils.JsonBuilders;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityAgentSkills.Plugins.Scene.Handlers
{
    /// <summary>
    /// Scene 编辑命令共享工具.
    /// </summary>
    internal static class SceneEditCommon
    {
        /// <summary>
        /// 校验当前必须为编辑模式.
        /// </summary>
        public static void EnsureEditModeOrThrow()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    UnityAgentSkillCommandErrorCodes.OnlyAllowedInEditMode
                    + ": 当前为 Play 模式,仅编辑模式可修改场景 (OnlyAllowedInEditMode)");
            }
        }

        /// <summary>
        /// 获取已加载场景.
        /// </summary>
        public static UnityEngine.SceneManagement.Scene GetLoadedSceneOrThrow(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": sceneName is required");
            }

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                UnityEngine.SceneManagement.Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && scene.name == sceneName)
                {
                    return scene;
                }
            }

            throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": sceneName not found or not loaded: " + sceneName);
        }

        /// <summary>
        /// 按路径定位场景内对象.
        /// </summary>
        public static GameObject FindGameObjectOrThrow(UnityEngine.SceneManagement.Scene scene, string objectPath, int siblingIndex, string pathFieldName)
        {
            if (string.IsNullOrWhiteSpace(objectPath))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": " + pathFieldName + " is required");
            }

            if (siblingIndex < 0)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": siblingIndex must be >= 0");
            }

            GameObject[] roots = scene.GetRootGameObjects();
            string[] parts = objectPath.Split('/');
            if (parts.Length == 0 || string.IsNullOrWhiteSpace(parts[0]))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": " + pathFieldName + " is invalid");
            }

            GameObject root = null;
            foreach (GameObject candidate in roots)
            {
                if (candidate != null && candidate.name == parts[0])
                {
                    root = candidate;
                    break;
                }
            }

            if (root == null)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": root GameObject not found in scene: " + parts[0]);
            }

            GameObject target = GameObjectPathFinder.FindByPath(root, objectPath, siblingIndex);
            if (target == null)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.GameObjectNotFound + ": GameObject不存在: " + objectPath + " (siblingIndex=" + siblingIndex + ")");
            }

            if (target.scene.name != scene.name)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": object is not in sceneName: " + scene.name);
            }

            return target;
        }

        /// <summary>
        /// 解析创建父节点.
        /// </summary>
        public static GameObject ResolveParentOrThrow(UnityEngine.SceneManagement.Scene scene, string parentPath, int parentSiblingIndex)
        {
            if (string.IsNullOrWhiteSpace(parentPath))
            {
                if (parentSiblingIndex != 0)
                {
                    throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": parentSiblingIndex must be 0 when parentPath is omitted");
                }

                return null;
            }

            if (parentSiblingIndex < 0)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": parentSiblingIndex must be >= 0");
            }

            return FindGameObjectOrThrow(scene, parentPath, parentSiblingIndex, "parentPath");
        }

        /// <summary>
        /// 保存场景.
        /// </summary>
        public static bool SaveScene(UnityEngine.SceneManagement.Scene scene)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            return EditorSceneManager.SaveScene(scene);
        }

        /// <summary>
        /// 应用插入位置.
        /// </summary>
        public static int ApplySiblingIndex(Transform transform, Transform parent, int requestedIndex)
        {
            if (transform == null)
            {
                return 0;
            }

            if (parent == null)
            {
                if (requestedIndex <= 0)
                {
                    transform.SetAsFirstSibling();
                    return transform.GetSiblingIndex();
                }

                transform.SetAsLastSibling();
                return transform.GetSiblingIndex();
            }

            if (requestedIndex >= 0 && requestedIndex < parent.childCount)
            {
                transform.SetSiblingIndex(requestedIndex);
            }
            else
            {
                transform.SetAsLastSibling();
            }

            return transform.GetSiblingIndex();
        }

        /// <summary>
        /// 统计对象树数量.
        /// </summary>
        public static int CountGameObjects(GameObject root, bool includeSelf)
        {
            if (root == null)
            {
                return 0;
            }

            int count = includeSelf ? 1 : 0;
            foreach (Transform child in root.transform)
            {
                count += CountGameObjects(child.gameObject, true);
            }

            return count;
        }

        /// <summary>
        /// 将对象值转换为 JsonData.
        /// </summary>
        public static JsonData ConvertJsonValue(object value)
        {
            if (value == null)
            {
                return new JsonData();
            }

            if (value is string stringValue)
            {
                return new JsonData(stringValue);
            }

            if (value is int intValue)
            {
                return new JsonData(intValue);
            }

            if (value is bool boolValue)
            {
                return new JsonData(boolValue);
            }

            if (value is double doubleValue)
            {
                return new JsonData(doubleValue);
            }

            if (value is float floatValue)
            {
                return new JsonData((double)floatValue);
            }

            if (value is long longValue)
            {
                return new JsonData(longValue);
            }

            if (value is Dictionary<string, int> dictInt)
            {
                JsonData obj = JsonResultBuilder.CreateObject();
                foreach (KeyValuePair<string, int> pair in dictInt)
                {
                    obj[pair.Key] = new JsonData(pair.Value);
                }

                return obj;
            }

            return new JsonData(value.ToString());
        }
    }
}
