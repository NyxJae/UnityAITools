using System;
using UnityAgentSkills.Core;
using UnityAgentSkills.Utils;
using UnityAgentSkills.Utils.JsonBuilders;
using LitJson2_utf;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityAgentSkills.Plugins.Scene.Handlers
{
    /// <summary>
    /// scene.queryComponents 命令处理器.
    /// 查询指定场景内某个 GameObject 的组件详情.
    /// 在 Play 模式和编辑模式都可执行.
    /// </summary>
    internal static class SceneQueryComponentsHandler
    {
        /// <summary>
        /// 支持的命令类型.
        /// </summary>
        public const string CommandType = "scene.queryComponents";

        /// <summary>
        /// 读取 string[] 过滤参数,不填/null/空数组/全空词项时返回 null 表示不过滤.
        /// </summary>
        private static string[] ParseStringArray(JsonData rawParams, string key)
        {
            if (rawParams == null || !rawParams.IsObject || !rawParams.ContainsKey(key))
            {
                return null;
            }

            JsonData value = rawParams[key];
            if (value == null || !value.IsArray)
            {
                return null;
            }

            System.Collections.Generic.List<string> results = new System.Collections.Generic.List<string>();
            for (int i = 0; i < value.Count; i++)
            {
                JsonData item = value[i];
                string text = item == null ? null : item.ToString();
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                results.Add(text.Trim());
            }

            return results.Count > 0 ? results.ToArray() : null;
        }

        /// <summary>
        /// 执行场景组件查询命令.
        /// </summary>
        /// <param name="rawParams">命令参数 json.</param>
        /// <returns>结果 json.</returns>
        public static JsonData Execute(JsonData rawParams)
        {
            CommandParams parameters = new CommandParams(rawParams);

            string sceneName = parameters.GetString("sceneName", null);
            string objectPath = parameters.GetString("objectPath", null);
            int siblingIndex = parameters.GetInt("siblingIndex", 0);

            if (string.IsNullOrEmpty(sceneName))
            {
                throw new ArgumentException(
                    UnityAgentSkillCommandErrorCodes.InvalidFields + ": sceneName is required");
            }

            if (string.IsNullOrEmpty(objectPath))
            {
                throw new ArgumentException(
                    UnityAgentSkillCommandErrorCodes.InvalidFields + ": objectPath is required");
            }

            if (siblingIndex < 0)
            {
                throw new ArgumentException(
                    UnityAgentSkillCommandErrorCodes.InvalidFields + ": siblingIndex must be >= 0");
            }

            // sceneName 大小写敏感匹配,与 Unity Scene.name 属性对应.
            // DontDestroyOnLoad 场景需特殊处理(Play 模式下通过 Helper 获取).
            GameObject[] rootObjects = null;
            bool sceneFound = false;

            if (sceneName == DontDestroyOnLoadHelper.SceneName)
            {
                if (!DontDestroyOnLoadHelper.IsAvailable)
                {
                    throw new ArgumentException(
                        UnityAgentSkillCommandErrorCodes.InvalidFields + ": DontDestroyOnLoad scene is only available in Play mode");
                }
                rootObjects = DontDestroyOnLoadHelper.GetRootGameObjects();
                sceneFound = rootObjects.Length > 0;
            }
            else
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    UnityEngine.SceneManagement.Scene s = SceneManager.GetSceneAt(i);
                    if (s.isLoaded && s.name == sceneName)
                    {
                        rootObjects = s.GetRootGameObjects();
                        sceneFound = true;
                        break;
                    }
                }
            }

            if (!sceneFound)
            {
                throw new ArgumentException(
                    UnityAgentSkillCommandErrorCodes.InvalidFields + ": sceneName not found or not loaded: " + sceneName);
            }

            string[] pathParts = objectPath.Split('/');
            if (pathParts.Length == 0 || string.IsNullOrEmpty(pathParts[0]))
            {
                throw new ArgumentException(
                    UnityAgentSkillCommandErrorCodes.InvalidFields + ": objectPath is invalid");
            }

            string rootName = pathParts[0];
            bool isRootOnly = pathParts.Length == 1;

            // 在目标场景 root 列表中按同名计数定位 root,支持 siblingIndex 区分同名 root.
            GameObject targetRoot = null;
            int sameNameRootIndex = 0;

            if (isRootOnly)
            {
                // objectPath 仅一段,siblingIndex 作用在 root 层.
                foreach (GameObject root in rootObjects)
                {
                    if (root.name == rootName)
                    {
                        if (sameNameRootIndex == siblingIndex)
                        {
                            targetRoot = root;
                            break;
                        }
                        sameNameRootIndex++;
                    }
                }
            }
            else
            {
                // objectPath 多段,siblingIndex 作用在最后一段(由 GameObjectPathFinder 处理),root 取第一个匹配.
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
                throw new ArgumentException(
                    UnityAgentSkillCommandErrorCodes.InvalidFields + ": root GameObject '" + rootName + "' not found in scene: " + sceneName);
            }

            // isRootOnly 时目标即 targetRoot,否则由 PathFinder 向下定位.
            GameObject target;
            if (isRootOnly)
            {
                target = targetRoot;
            }
            else
            {
                target = GameObjectPathFinder.FindByPath(targetRoot, objectPath, siblingIndex);
            }
            if (target == null)
            {
                throw new InvalidOperationException(
                    "GameObject not found at path: " + objectPath + " (siblingIndex=" + siblingIndex + ")");
            }

            // 获取过滤参数.这里显式从 rawParams 读取 string[]，避免依赖隐式解析入口导致过滤失效。
            JsonData paramsData = parameters.GetData();
            string[] componentFilter = ParseStringArray(paramsData, "componentFilter");
            string[] propertyFilter = ParseStringArray(paramsData, "propertyFilter");

            var components = ComponentPropertyReader.ReadComponents(target, componentFilter, propertyFilter);

            JsonData result = ComponentJsonBuilder.BuildComponentsResult(objectPath, target, components);
            result["sceneName"] = sceneName;

            return result;
        }
    }
}
