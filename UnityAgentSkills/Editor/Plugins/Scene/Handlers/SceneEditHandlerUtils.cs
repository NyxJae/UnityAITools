using System;
using UnityAgentSkills.Core;
using UnityAgentSkills.Utils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using LitJson2_utf;

namespace UnityAgentSkills.Plugins.Scene.Handlers
{
    /// <summary>
    /// Scene 编辑命令共享工具.
    /// </summary>
    internal static class SceneEditHandlerUtils
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
                    + ": 当前为 Play 模式,仅编辑模式可执行场景编辑命令 (OnlyAllowedInEditMode)"
                );
            }
        }

        /// <summary>
        /// 解析已加载场景.
        /// </summary>
        public static UnityEngine.SceneManagement.Scene ResolveLoadedSceneOrThrow(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": sceneName is required");
            }

            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (scene.isLoaded && scene.name == sceneName)
                {
                    return scene;
                }
            }

            throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": sceneName not found or not loaded: " + sceneName);
        }

        /// <summary>
        /// 在指定场景中定位对象.
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

            string[] pathParts = objectPath.Split('/');
            if (pathParts.Length == 0 || string.IsNullOrWhiteSpace(pathParts[0]))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": objectPath is invalid");
            }

            GameObject[] rootObjects = scene.GetRootGameObjects();
            string rootName = pathParts[0];
            bool isRootOnly = pathParts.Length == 1;
            GameObject targetRoot = null;
            int sameNameRootIndex = 0;

            if (isRootOnly)
            {
                foreach (GameObject root in rootObjects)
                {
                    if (root.name != rootName)
                    {
                        continue;
                    }

                    if (sameNameRootIndex == siblingIndex)
                    {
                        targetRoot = root;
                        break;
                    }

                    sameNameRootIndex++;
                }
            }
            else
            {
                foreach (GameObject root in rootObjects)
                {
                    if (root.name == rootName)
                    {
                        targetRoot = root;
                        break;
                    }
                }
            }

            if (targetRoot == null)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": root GameObject '" + rootName + "' not found in scene: " + scene.name);
            }

            GameObject target = isRootOnly ? targetRoot : GameObjectPathFinder.FindByPath(targetRoot, objectPath, siblingIndex);
            if (target == null)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.GameObjectNotFound + ": GameObject不存在: " + objectPath + " (siblingIndex=" + siblingIndex + ")");
            }

            return target;
        }

        /// <summary>
        /// 解析组件类型.
        /// </summary>
        public static Type ResolveComponentTypeOrThrow(string componentType)
        {
            return Prefab.Handlers.PrefabComponentHandlerUtils.ResolveComponentTypeOrThrow(componentType);
        }

        /// <summary>
        /// 定位组件.
        /// </summary>
        public static Component FindComponentOrThrow(GameObject target, Type componentType, int componentIndex)
        {
            return Prefab.Handlers.PrefabComponentHandlerUtils.FindComponentOrThrow(target, componentType, componentIndex);
        }

        /// <summary>
        /// 计算组件索引.
        /// </summary>
        public static int GetComponentIndex(GameObject target, Component component)
        {
            return Prefab.Handlers.PrefabComponentHandlerUtils.GetComponentIndex(target, component);
        }

        /// <summary>
        /// 构建 Vector2 Json.
        /// </summary>
        public static JsonData BuildVector2Json(Vector2 value)
        {
            JsonData json = Utils.JsonBuilders.JsonResultBuilder.CreateObject();
            json["x"] = value.x;
            json["y"] = value.y;
            return json;
        }

        /// <summary>
        /// 构建 Vector3 Json.
        /// </summary>
        public static JsonData BuildVector3Json(Vector3 value)
        {
            JsonData json = Utils.JsonBuilders.JsonResultBuilder.CreateObject();
            json["x"] = value.x;
            json["y"] = value.y;
            json["z"] = value.z;
            return json;
        }

        /// <summary>
        /// 将场景标记为脏并保存.
        /// </summary>
        public static bool SaveScene(UnityEngine.SceneManagement.Scene scene)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            return EditorSceneManager.SaveScene(scene);
        }
    }
}
