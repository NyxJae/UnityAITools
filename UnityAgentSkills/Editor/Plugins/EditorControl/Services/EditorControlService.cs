using System;
using LitJson2_utf;
using UnityAgentSkills.Core;
using UnityAgentSkills.Utils.JsonBuilders;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityAgentSkills.Plugins.EditorControl.Services
{
    /// <summary>
    /// 收口 editor.* 命令所需的 Editor 状态读取,选择,撤销重做,菜单执行与全局配置读取能力.
    /// </summary>
    internal static class EditorControlService
    {
        /// <summary>
        /// 获取轻量编辑器状态快照.
        /// </summary>
        public static JsonData GetState()
        {
            JsonData result = JsonResultBuilder.CreateObject();
            result["isPlayMode"] = EditorApplication.isPlaying;
            result["isPlayingOrWillChangePlaymode"] = EditorApplication.isPlayingOrWillChangePlaymode;
            result["isPaused"] = EditorApplication.isPaused;
            result["isCompiling"] = EditorApplication.isCompiling;
            result["isUpdating"] = EditorApplication.isUpdating;
            result["activeScene"] = BuildSceneSummary(SceneManager.GetActiveScene());
            return result;
        }

        /// <summary>
        /// 获取编辑器上下文摘要.
        /// </summary>
        public static JsonData GetContext()
        {
            JsonData result = GetState();
            result["selection"] = BuildSelectionArray();
            result["selectionCount"] = Selection.objects != null ? Selection.objects.Length : 0;

            JsonData loadedScenes = JsonResultBuilder.CreateArray();
            int sceneCount = SceneManager.sceneCount;
            for (int i = 0; i < sceneCount; i++)
            {
                loadedScenes.Add(BuildSceneSummary(SceneManager.GetSceneAt(i)));
            }

            result["loadedScenes"] = loadedScenes;
            result["activeObjectSummary"] = BuildSelectionObject(Selection.activeObject);
            result["activeGameObjectSummary"] = BuildGameObjectSelection(Selection.activeGameObject);
            return result;
        }

        /// <summary>
        /// 获取当前选择摘要.
        /// </summary>
        public static JsonData GetSelection()
        {
            JsonData result = JsonResultBuilder.CreateObject();
            result["selectionCount"] = Selection.objects != null ? Selection.objects.Length : 0;
            result["items"] = BuildSelectionArray();
            result["activeObjectSummary"] = BuildSelectionObject(Selection.activeObject);
            return result;
        }

        /// <summary>
        /// 获取当前项目可用标签列表.
        /// </summary>
        public static JsonData GetTags()
        {
            JsonData result = JsonResultBuilder.CreateObject();
            JsonData tags = JsonResultBuilder.CreateArray();
            foreach (string tag in InternalEditorUtility.tags)
            {
                tags.Add(tag ?? string.Empty);
            }

            result["tags"] = tags;
            result["count"] = tags.Count;
            return result;
        }

        /// <summary>
        /// 获取当前项目 layer 槽位列表.
        /// </summary>
        public static JsonData GetLayers()
        {
            JsonData result = JsonResultBuilder.CreateObject();
            JsonData layers = JsonResultBuilder.CreateArray();
            for (int i = 0; i < 32; i++)
            {
                JsonData item = JsonResultBuilder.CreateObject();
                string name = LayerMask.LayerToName(i);
                item["index"] = i;
                item["name"] = string.IsNullOrEmpty(name) ? string.Empty : name;
                item["isNamed"] = !string.IsNullOrEmpty(name);
                layers.Add(item);
            }

            result["layers"] = layers;
            return result;
        }

        /// <summary>
        /// 选择场景对象.
        /// </summary>
        public static JsonData SelectSceneGameObject(string sceneName, string objectPath, int siblingIndex)
        {
            RequireEditMode("当前为 Play 模式,仅编辑模式可选择场景对象");

            if (string.IsNullOrWhiteSpace(sceneName))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": sceneName is required");
            }

            if (string.IsNullOrWhiteSpace(objectPath))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": objectPath is required");
            }

            if (siblingIndex < 0)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": siblingIndex must be >= 0");
            }

            UnityEngine.SceneManagement.Scene targetScene = FindLoadedScene(sceneName);
            string[] parts = objectPath.Split(new[] {'/'}, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": objectPath is invalid");
            }

            GameObject root = FindRootObject(targetScene, parts[0]);
            if (root == null)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": root GameObject not found in scene: " + parts[0]);
            }

            GameObject target = FindByPath(root, parts, siblingIndex);
            if (target == null)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.GameObjectNotFound + ": objectPath not found: " + objectPath + ", siblingIndex=" + siblingIndex + ", sceneName=" + sceneName);
            }

            Selection.activeGameObject = target;
            JsonData result = JsonResultBuilder.CreateObject();
            result["selected"] = true;
            result["selectionKind"] = "sceneGameObject";
            result["item"] = BuildGameObjectSelection(target);
            return result;
        }

        /// <summary>
        /// 选择 Project 资源.
        /// </summary>
        public static JsonData SelectProjectAsset(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": assetPath is required");
            }

            if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": assetPath must start with Assets/");
            }

            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (asset == null)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.AssetNotFound + ": 资源不存在: " + assetPath);
            }

            Selection.activeObject = asset;
            JsonData result = JsonResultBuilder.CreateObject();
            result["selected"] = true;
            result["selectionKind"] = "projectAsset";
            result["item"] = BuildSelectionObject(asset);
            return result;
        }

        /// <summary>
        /// 执行撤销.
        /// </summary>
        public static JsonData UndoSteps(int steps)
        {
            if (steps < 1)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": steps must be >= 1");
            }

            int executed = ExecuteMenuSteps("Edit/Undo", steps);
            JsonData result = JsonResultBuilder.CreateObject();
            result["requestedSteps"] = steps;
            result["executedSteps"] = executed;
            result["performed"] = executed > 0;
            result["summary"] = executed > 0 ? "已执行撤销" : "当前没有可撤销内容";
            return result;
        }

        /// <summary>
        /// 执行重做.
        /// </summary>
        public static JsonData RedoSteps(int steps)
        {
            if (steps < 1)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": steps must be >= 1");
            }

            int executed = ExecuteMenuSteps("Edit/Redo", steps);
            JsonData result = JsonResultBuilder.CreateObject();
            result["requestedSteps"] = steps;
            result["executedSteps"] = executed;
            result["performed"] = executed > 0;
            result["summary"] = executed > 0 ? "已执行重做" : "当前没有可重做内容";
            return result;
        }

        /// <summary>
        /// 执行菜单命令.
        /// </summary>
        public static JsonData ExecuteMenu(string menuPath)
        {
            if (string.IsNullOrWhiteSpace(menuPath))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": menuPath is required");
            }

            if (!MenuPathExists(menuPath))
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.MenuItemNotFound + ": 菜单路径不存在: " + menuPath);
            }

            bool executed = EditorApplication.ExecuteMenuItem(menuPath);
            if (!executed)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.MenuItemNotExecutable + ": 菜单存在但当前上下文不可执行: " + menuPath);
            }

            JsonData result = JsonResultBuilder.CreateObject();
            result["menuPath"] = menuPath;
            result["executed"] = true;
            return result;
        }

        /// <summary>
        /// 设置 Pause On Error 开关.
        /// </summary>
        public static JsonData SetPauseOnError(bool enabled)
        {
            bool before = EditorPrefs.GetBool("ErrorPause", false);
            EditorPrefs.SetBool("ErrorPause", enabled);
            bool after = EditorPrefs.GetBool("ErrorPause", false);

            JsonData result = JsonResultBuilder.CreateObject();
            result["before"] = before;
            result["after"] = after;
            result["changed"] = before != after;
            result["summary"] = before == after ? "Pause On Error 状态未变化" : "Pause On Error 状态已更新";
            return result;
        }

        private static void RequireEditMode(string detail)
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.OnlyAllowedInEditMode + ": " + detail + " (OnlyAllowedInEditMode)");
            }
        }

        private static UnityEngine.SceneManagement.Scene FindLoadedScene(string sceneName)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                UnityEngine.SceneManagement.Scene scene = SceneManager.GetSceneAt(i);
                if (scene.IsValid() && scene.isLoaded && string.Equals(scene.name, sceneName, StringComparison.Ordinal))
                {
                    return scene;
                }
            }

            throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": sceneName not found or not loaded: " + sceneName);
        }

        private static GameObject FindRootObject(UnityEngine.SceneManagement.Scene scene, string rootName)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            GameObject matched = null;
            int count = 0;
            for (int i = 0; i < roots.Length; i++)
            {
                if (!string.Equals(roots[i].name, rootName, StringComparison.Ordinal))
                {
                    continue;
                }

                matched = roots[i];
                count++;
            }

            if (count > 1)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.AmbiguousTarget + ": root GameObject 存在同名歧义: " + rootName + ", sceneName=" + scene.name);
            }

            return matched;
        }

        private static GameObject FindByPath(GameObject root, string[] parts, int siblingIndex)
        {
            if (parts.Length == 1)
            {
                return siblingIndex == 0 ? root : null;
            }

            Transform current = root.transform;
            for (int i = 1; i < parts.Length; i++)
            {
                string part = parts[i];
                Transform matched = null;
                int count = 0;
                int seenIndex = -1;
                for (int childIndex = 0; childIndex < current.childCount; childIndex++)
                {
                    Transform child = current.GetChild(childIndex);
                    if (!string.Equals(child.name, part, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    seenIndex++;
                    if (i == parts.Length - 1)
                    {
                        if (seenIndex == siblingIndex)
                        {
                            matched = child;
                        }
                        count++;
                    }
                    else
                    {
                        if (matched == null)
                        {
                            matched = child;
                        }
                        count++;
                    }
                }

                if (count == 0)
                {
                    return null;
                }

                if (i < parts.Length - 1 && count > 1)
                {
                    throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.AmbiguousTarget + ": 路径中间节点存在同名歧义: " + part + ", objectPath=" + string.Join("/", parts));
                }

                if (i == parts.Length - 1 && matched == null)
                {
                    return null;
                }

                current = matched;
            }

            return current != null ? current.gameObject : null;
        }

        private static JsonData BuildSelectionArray()
        {
            JsonData items = JsonResultBuilder.CreateArray();
            UnityEngine.Object[] objects = Selection.objects;
            if (objects == null)
            {
                return items;
            }

            for (int i = 0; i < objects.Length; i++)
            {
                items.Add(BuildSelectionObject(objects[i]));
            }

            return items;
        }

        private static JsonData BuildSelectionObject(UnityEngine.Object obj)
        {
            JsonData item = JsonResultBuilder.CreateObject();
            if (obj == null)
            {
                item["kind"] = "none";
                return item;
            }

            GameObject gameObject = obj as GameObject;
            if (gameObject != null)
            {
                return BuildGameObjectSelection(gameObject);
            }

            item["kind"] = "asset";
            item["name"] = obj.name ?? string.Empty;
            item["assetPath"] = AssetDatabase.GetAssetPath(obj) ?? string.Empty;
            item["typeName"] = obj.GetType().FullName ?? string.Empty;
            return item;
        }

        private static JsonData BuildGameObjectSelection(GameObject gameObject)
        {
            JsonData item = JsonResultBuilder.CreateObject();
            if (gameObject == null)
            {
                item["kind"] = "none";
                return item;
            }

            item["kind"] = "sceneGameObject";
            item["name"] = gameObject.name ?? string.Empty;
            item["path"] = BuildTransformPath(gameObject.transform);
            item["siblingIndex"] = CountSameNameSiblingsBefore(gameObject.transform);
            item["sceneName"] = gameObject.scene.name ?? string.Empty;
            item["activeSelf"] = gameObject.activeSelf;
            item["activeInHierarchy"] = gameObject.activeInHierarchy;
            return item;
        }

        private static JsonData BuildSceneSummary(UnityEngine.SceneManagement.Scene scene)
        {
            JsonData item = JsonResultBuilder.CreateObject();
            item["sceneName"] = scene.name ?? string.Empty;
            item["scenePath"] = scene.path ?? string.Empty;
            item["isLoaded"] = scene.isLoaded;
            item["isDirty"] = scene.isDirty;
            item["rootCount"] = scene.IsValid() && scene.isLoaded ? scene.rootCount : 0;
            return item;
        }

        private static string BuildTransformPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            string path = transform.name;
            Transform current = transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        private static int CountSameNameSiblingsBefore(Transform transform)
        {
            if (transform == null)
            {
                return 0;
            }

            Transform parent = transform.parent;
            if (parent == null)
            {
                return 0;
            }

            int siblingIndex = 0;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (!string.Equals(child.name, transform.name, StringComparison.Ordinal))
                {
                    continue;
                }

                if (child == transform)
                {
                    return siblingIndex;
                }

                siblingIndex++;
            }

            return 0;
        }

        private static int ExecuteMenuSteps(string menuPath, int steps)
        {
            int executed = 0;
            for (int i = 0; i < steps; i++)
            {
                if (!EditorApplication.ExecuteMenuItem(menuPath))
                {
                    break;
                }

                executed++;
            }

            return executed;
        }

        private static bool MenuPathExists(string menuPath)
        {
            if (string.IsNullOrWhiteSpace(menuPath))
            {
                return false;
            }

            System.Type unsupportedType = typeof(EditorApplication).Assembly.GetType("UnityEditor.Unsupported");
            if (unsupportedType == null)
            {
                return true;
            }

            System.Reflection.MethodInfo getSubmenusMethod = unsupportedType.GetMethod(
                "GetSubmenus",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                null,
                new[] { typeof(string) },
                null);
            if (getSubmenusMethod == null)
            {
                return true;
            }

            string parentPath = string.Empty;
            int lastSlash = menuPath.LastIndexOf('/');
            if (lastSlash > 0)
            {
                parentPath = menuPath.Substring(0, lastSlash);
            }

            try
            {
                string[] submenus = getSubmenusMethod.Invoke(null, new object[] { parentPath }) as string[];
                if (submenus == null)
                {
                    return true;
                }

                for (int i = 0; i < submenus.Length; i++)
                {
                    if (string.Equals(submenus[i], menuPath, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }

                return false;
            }
            catch
            {
                return true;
            }
        }
    }
}
