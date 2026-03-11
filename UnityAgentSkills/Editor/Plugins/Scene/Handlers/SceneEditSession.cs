using System;
using UnityAgentSkills.Core;
using UnityAgentSkills.Utils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityAgentSkills.Plugins.Scene.Handlers
{
    /// <summary>
    /// 场景编辑共享会话.
    /// 统一处理 Edit Mode 护栏,sceneName 解析,对象与组件定位,以及保存入口.
    /// </summary>
    internal sealed class SceneEditSession : IDisposable
    {
        private readonly UnityEngine.SceneManagement.Scene targetScene;
        private readonly string scenePath;
        private readonly bool wasSceneDirty;
        private bool isCommitted;
        private bool isDisposed;

        /// <summary>
        /// 初始化指定场景的编辑会话.
        /// </summary>
        /// <param name="sceneName">目标场景名.</param>
        public SceneEditSession(string sceneName)
        {
            EnsureEditModeOrThrow();
            SceneName = ValidateSceneNameOrThrow(sceneName);
            targetScene = ResolveLoadedSceneOrThrow(SceneName);
            scenePath = targetScene.path;
            wasSceneDirty = targetScene.isDirty;
        }

        /// <summary>
        /// 当前会话对应的场景名.
        /// </summary>
        public string SceneName { get; }

        /// <summary>
        /// 当前会话对应的已加载场景.
        /// </summary>
        public UnityEngine.SceneManagement.Scene TargetScene => targetScene;

        /// <summary>
        /// 校验当前是否允许执行场景编辑.
        /// </summary>
        public static void EnsureEditModeOrThrow()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    UnityAgentSkillCommandErrorCodes.OnlyAllowedInEditMode
                    + ": 当前为 Play 模式,仅编辑模式可编辑场景 (OnlyAllowedInEditMode)");
            }
        }

        /// <summary>
        /// 校验 sceneName 非空.
        /// </summary>
        /// <param name="sceneName">输入场景名.</param>
        /// <returns>原样返回合法场景名.</returns>
        public static string ValidateSceneNameOrThrow(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                throw new ArgumentException(
                    UnityAgentSkillCommandErrorCodes.InvalidFields + ": sceneName is required");
            }

            return sceneName;
        }

        /// <summary>
        /// 按已加载场景名解析目标场景.
        /// </summary>
        /// <param name="sceneName">目标场景名.</param>
        /// <returns>已加载场景.</returns>
        public static UnityEngine.SceneManagement.Scene ResolveLoadedSceneOrThrow(string sceneName)
        {
            string validatedSceneName = ValidateSceneNameOrThrow(sceneName);
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                UnityEngine.SceneManagement.Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && scene.name == validatedSceneName)
                {
                    return scene;
                }
            }

            throw new ArgumentException(
                UnityAgentSkillCommandErrorCodes.InvalidFields + ": sceneName not found or not loaded: " + validatedSceneName);
        }

        /// <summary>
        /// 通过 objectPath 和 siblingIndex 定位对象.
        /// </summary>
        /// <param name="objectPath">对象路径.</param>
        /// <param name="siblingIndex">同名序号.</param>
        /// <param name="pathFieldName">路径字段名.</param>
        /// <returns>目标对象.</returns>
        public GameObject FindGameObjectOrThrow(string objectPath, int siblingIndex, string pathFieldName = "objectPath")
        {
            if (string.IsNullOrWhiteSpace(objectPath))
            {
                throw new ArgumentException(
                    UnityAgentSkillCommandErrorCodes.InvalidFields + ": " + pathFieldName + " is required");
            }

            if (siblingIndex < 0)
            {
                throw new ArgumentException(
                    UnityAgentSkillCommandErrorCodes.InvalidFields + ": siblingIndex must be >= 0");
            }

            string[] pathParts = objectPath.Split('/');
            if (pathParts.Length == 0 || string.IsNullOrWhiteSpace(pathParts[0]))
            {
                throw new ArgumentException(
                    UnityAgentSkillCommandErrorCodes.InvalidFields + ": " + pathFieldName + " is invalid");
            }

            string rootName = pathParts[0];
            GameObject[] rootObjects = targetScene.GetRootGameObjects();
            GameObject targetRoot = null;
            int sameNameRootIndex = 0;

            foreach (GameObject root in rootObjects)
            {
                if (root == null || root.name != rootName)
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

            if (targetRoot == null)
            {
                throw new ArgumentException(
                    UnityAgentSkillCommandErrorCodes.InvalidFields + ": root GameObject '" + rootName + "' not found in scene: " + SceneName);
            }

            if (pathParts.Length == 1)
            {
                return targetRoot;
            }

            GameObject target = GameObjectPathFinder.FindByPath(targetRoot, objectPath, siblingIndex);
            if (target == null)
            {
                throw new InvalidOperationException(
                    UnityAgentSkillCommandErrorCodes.GameObjectNotFound + ": GameObject不存在: " + objectPath + " (siblingIndex=" + siblingIndex + ")");
            }

            return target;
        }

        /// <summary>
        /// 解析组件类型.
        /// </summary>
        /// <param name="componentType">组件类型名.</param>
        /// <returns>解析后的类型.</returns>
        public static Type ResolveComponentTypeOrThrow(string componentType)
        {
            return SceneEditUtilities.ResolveComponentTypeOrThrow(componentType);
        }

        /// <summary>
        /// 在目标对象上定位组件.
        /// </summary>
        /// <param name="target">目标对象.</param>
        /// <param name="componentType">组件类型.</param>
        /// <param name="componentIndex">同类型组件索引.</param>
        /// <returns>目标组件.</returns>
        public Component FindComponentOrThrow(GameObject target, Type componentType, int componentIndex)
        {
            return SceneEditUtilities.FindComponentOrThrow(target, componentType, componentIndex);
        }

        /// <summary>
        /// 标记并保存当前场景.
        /// </summary>
        public void Save()
        {
            EditorSceneManager.MarkSceneDirty(targetScene);
            EditorSceneManager.SaveScene(targetScene);
            isCommitted = true;
        }

        /// <summary>
        /// 回滚当前会话对场景的未提交修改.
        /// </summary>
        public void Rollback()
        {
            if (isCommitted || !targetScene.IsValid() || string.IsNullOrWhiteSpace(scenePath))
            {
                return;
            }

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (wasSceneDirty)
            {
                UnityEngine.SceneManagement.Scene reloadedScene = ResolveLoadedSceneOrThrow(SceneName);
                EditorSceneManager.MarkSceneDirty(reloadedScene);
            }
        }

        /// <summary>
        /// 释放会话,未提交时自动回滚.
        /// </summary>
        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            if (!isCommitted)
            {
                Rollback();
            }
        }
    }
}
