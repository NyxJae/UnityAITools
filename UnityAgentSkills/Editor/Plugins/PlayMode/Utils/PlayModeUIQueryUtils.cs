using System;
using System.Collections.Generic;
using System.Globalization;
using UnityAgentSkills.Core;
using UnityAgentSkills.Utils;
using LitJson2_utf;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnityAgentSkills.Plugins.PlayMode.Utils
{
    /// <summary>
    /// Play Mode UI 查询与交互工具.
    /// </summary>
    internal static class PlayModeUIQueryUtils
    {
        /// <summary>
        /// 查询当前场景中的 UI 元素.
        /// </summary>
        public static JsonData QueryAll(string[] nameContains, string[] textContains, string[] componentFilter, bool visibleOnly, bool interactableOnly, int maxResults, JsonData screenRect)
        {
            PlayModeSession.EnsureActiveForCommand();

            if (maxResults <= 0)
            {
                maxResults = 200;
            }

            ScreenRectFilter rectFilter = ParseScreenRect(screenRect);

            JsonData uiElements = new JsonData();
            uiElements.SetJsonType(JsonType.Array);

            int total = 0;
            Selectable[] selectables = Selectable.allSelectablesArray;
            for (int i = 0; i < selectables.Length; i++)
            {
                Selectable selectable = selectables[i];
                if (selectable == null || selectable.gameObject == null)
                {
                    continue;
                }

                GameObject target = selectable.gameObject;
                if (!MatchesComponentFilter(selectable, componentFilter))
                {
                    continue;
                }

                if (!PlayModeParamUtils.MatchesAnyContains(target.name, nameContains))
                {
                    continue;
                }

                string resolvedText = ResolveText(target);
                if (!PlayModeParamUtils.MatchesAnyContains(resolvedText, textContains))
                {
                    continue;
                }

                Vector3 screenPosition = GetScreenPosition(target);
                if (rectFilter != null && !rectFilter.Contains(screenPosition.x, screenPosition.y))
                {
                    continue;
                }

                bool visible = IsVisible(target);
                bool interactable = IsInteractable(target);
                if (visibleOnly && !visible)
                {
                    continue;
                }

                if (interactableOnly && !interactable)
                {
                    continue;
                }

                total++;
                if (uiElements.Count >= maxResults)
                {
                    continue;
                }

                JsonData element = BuildElement(target, selectable.GetType().Name, visible, interactable, resolvedText, screenPosition);
                uiElements.Add(element);
            }

            JsonData result = new JsonData();
            result["total"] = total;
            result["returned"] = uiElements.Count;
            result["truncated"] = total > uiElements.Count;
            result["uiElements"] = uiElements;
            return result;
        }

        /// <summary>
        /// 根据路径和 siblingIndex 定位对象.
        /// </summary>
        public static GameObject FindTarget(string targetPath, int siblingIndex)
        {
            if (string.IsNullOrEmpty(targetPath))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": targetPath is required");
            }

            if (siblingIndex < 0)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": siblingIndex must be >= 0");
            }

            // 优先在 Selectable 全局列表中按 path+siblingIndex 定位,与 queryUI 的返回域保持一致.
            GameObject fromSelectables = FindTargetFromSelectables(targetPath, siblingIndex);
            if (fromSelectables != null)
            {
                return fromSelectables;
            }

            // 兜底: 遍历所有已加载场景,兼容非 Selectable 目标.
            int sceneCount = UnityEngine.SceneManagement.SceneManager.sceneCount;
            for (int sceneIndex = 0; sceneIndex < sceneCount; sceneIndex++)
            {
                UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    continue;
                }

                GameObject[] roots = scene.GetRootGameObjects();
                foreach (var root in roots)
                {
                    GameObject found = GameObjectPathFinder.FindByPath(root, targetPath, siblingIndex);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }

            throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.ElementNotFound + ": Element not found at path: " + targetPath + " (siblingIndex=" + siblingIndex + ")");
        }

        /// <summary>
        /// 从 Selectable 全局列表中通过 path+siblingIndex 定位目标.
        /// </summary>
        private static GameObject FindTargetFromSelectables(string targetPath, int siblingIndex)
        {
            Selectable[] selectables = Selectable.allSelectablesArray;
            for (int i = 0; i < selectables.Length; i++)
            {
                Selectable selectable = selectables[i];
                if (selectable == null || selectable.gameObject == null)
                {
                    continue;
                }

                GameObject candidate = selectable.gameObject;
                if (!string.Equals(GameObjectPathFinder.GetPath(candidate), targetPath, StringComparison.Ordinal))
                {
                    continue;
                }

                if (GameObjectPathFinder.GetSameNameSiblingIndex(candidate) == siblingIndex)
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// 在指定坐标执行 UI 射线检测.
        /// </summary>
        public static List<RaycastResult> RaycastAt(float x, float y)
        {
            if (EventSystem.current == null)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.RuntimeError + ": EventSystem.current is null");
            }

            PointerEventData pointer = new PointerEventData(EventSystem.current)
            {
                position = new Vector2(x, y)
            };
            List<RaycastResult> rayResults = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, rayResults);
            return rayResults;
        }

        /// <summary>
        /// 校验坐标是否在当前 Game 视图范围内.
        /// </summary>
        public static void EnsureCoordinatesInBounds(float x, float y)
        {
            if (float.IsNaN(x) || float.IsNaN(y) || float.IsInfinity(x) || float.IsInfinity(y))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidCoordinates + ": Coordinates out of bounds");
            }

            if (x < 0f || y < 0f || x > Screen.width || y > Screen.height)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidCoordinates + ": Coordinates out of bounds");
            }
        }

        /// <summary>
        /// 检查对象是否可见.
        /// </summary>
        public static bool IsVisible(GameObject target)
        {
            if (target == null || !target.activeInHierarchy)
            {
                return false;
            }

            CanvasGroup[] groups = target.GetComponentsInParent<CanvasGroup>(true);
            for (int i = 0; i < groups.Length; i++)
            {
                CanvasGroup group = groups[i];
                if (group == null) continue;
                if (group.alpha <= 0.01f) return false;
                if (!group.blocksRaycasts) return false;
            }

            return true;
        }

        /// <summary>
        /// 检查对象是否可交互.
        /// </summary>
        public static bool IsInteractable(GameObject target)
        {
            if (!IsVisible(target))
            {
                return false;
            }

            CanvasGroup[] groups = target.GetComponentsInParent<CanvasGroup>(true);
            for (int i = 0; i < groups.Length; i++)
            {
                CanvasGroup group = groups[i];
                if (group == null) continue;
                if (!group.interactable) return false;
            }

            Selectable selectable = target.GetComponent<Selectable>();
            if (selectable != null)
            {
                return selectable.IsInteractable();
            }

            InputField inputField = target.GetComponent<InputField>();
            if (inputField != null)
            {
                return inputField.interactable;
            }

            return true;
        }

        /// <summary>
        /// 获取对象当前可见文本.
        /// </summary>
        public static string ResolveText(GameObject target)
        {
            if (target == null)
            {
                return string.Empty;
            }

            Text text = target.GetComponentInChildren<Text>(true);
            if (text != null)
            {
                return text.text ?? string.Empty;
            }

            InputField inputField = target.GetComponent<InputField>();
            if (inputField != null)
            {
                return inputField.text ?? string.Empty;
            }

            return string.Empty;
        }

        private static JsonData BuildElement(GameObject target, string typeName, bool visible, bool interactable, string resolvedText, Vector3 screenPosition)
        {
            JsonData element = new JsonData();
            string path = GameObjectPathFinder.GetPath(target);
            int siblingIndex = GameObjectPathFinder.GetSameNameSiblingIndex(target);

            element["name"] = target.name;
            element["path"] = path;
            element["siblingIndex"] = siblingIndex;
            element["elementId"] = BuildElementId(path, siblingIndex);
            element["type"] = typeName;
            element["visible"] = visible;
            element["interactable"] = interactable;

            JsonData screenData = new JsonData();
            screenData["x"] = (int)screenPosition.x;
            screenData["y"] = (int)screenPosition.y;
            element["screenPosition"] = screenData;
            element["text"] = resolvedText ?? string.Empty;
            return element;
        }

        private static ScreenRectFilter ParseScreenRect(JsonData screenRect)
        {
            if (screenRect == null)
            {
                return null;
            }

            if (!screenRect.IsObject)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": screenRect must be an object");
            }

            float xMin = ParseRequiredFloat(screenRect, "xMin");
            float xMax = ParseRequiredFloat(screenRect, "xMax");
            float yMin = ParseRequiredFloat(screenRect, "yMin");
            float yMax = ParseRequiredFloat(screenRect, "yMax");

            if (xMin > xMax || yMin > yMax)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": screenRect requires xMin<=xMax and yMin<=yMax");
            }

            return new ScreenRectFilter(xMin, xMax, yMin, yMax);
        }

        private static float ParseRequiredFloat(JsonData data, string key)
        {
            if (data == null || !data.IsObject || !data.ContainsKey(key))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": missing screenRect." + key);
            }

            JsonData value = data[key];
            if (value == null)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": invalid screenRect." + key);
            }

            if (value.IsInt)
            {
                return (int)value;
            }

            if (value.IsLong)
            {
                return (long)value;
            }

            if (value.IsDouble)
            {
                return (float)(double)value;
            }

            if (value.IsString)
            {
                float parsed;
                if (float.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
                {
                    return parsed;
                }
            }

            throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": invalid screenRect." + key);
        }

        private sealed class ScreenRectFilter
        {
            private readonly float _xMin;
            private readonly float _xMax;
            private readonly float _yMin;
            private readonly float _yMax;

            public ScreenRectFilter(float xMin, float xMax, float yMin, float yMax)
            {
                _xMin = xMin;
                _xMax = xMax;
                _yMin = yMin;
                _yMax = yMax;
            }

            public bool Contains(float x, float y)
            {
                return x >= _xMin && x <= _xMax && y >= _yMin && y <= _yMax;
            }
        }

        /// <summary>
        /// 构造标准化目标定位信息.
        /// </summary>
        public static JsonData BuildResolvedTarget(GameObject target)
        {
            JsonData resolvedTarget = new JsonData();
            string path = GameObjectPathFinder.GetPath(target);
            int siblingIndex = GameObjectPathFinder.GetSameNameSiblingIndex(target);
            resolvedTarget["path"] = path;
            resolvedTarget["siblingIndex"] = siblingIndex;
            resolvedTarget["elementId"] = BuildElementId(path, siblingIndex);
            return resolvedTarget;
        }

        /// <summary>
        /// 获取同名节点中的 siblingIndex.
        /// </summary>
        public static int GetSameNameSiblingIndex(GameObject target)
        {
            return GameObjectPathFinder.GetSameNameSiblingIndex(target);
        }

        private static Vector3 GetScreenPosition(GameObject target)
        {
            RectTransform rect = target.GetComponent<RectTransform>();
            if (rect == null)
            {
                return Vector3.zero;
            }

            Vector3 worldPoint = rect.TransformPoint(rect.rect.center);
            Camera camera = Camera.main;
            if (camera == null)
            {
                return RectTransformUtility.WorldToScreenPoint(null, worldPoint);
            }

            return RectTransformUtility.WorldToScreenPoint(camera, worldPoint);
        }

        private static bool MatchesComponentFilter(Selectable selectable, string[] filter)
        {
            if (selectable == null)
            {
                return false;
            }

            return MatchesTypeFilter(selectable.GetType().Name, filter);
        }

        private static bool MatchesTypeFilter(string typeName, string[] filter)
        {
            if (filter == null || filter.Length == 0)
            {
                return true;
            }

            foreach (var item in filter)
            {
                if (string.IsNullOrWhiteSpace(item))
                {
                    return true;
                }

                if (typeName.IndexOf(item, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static string BuildElementId(string path, int siblingIndex)
        {
            return (path ?? string.Empty) + "#" + siblingIndex;
        }

        private static string ResolvePrimaryType(GameObject target)
        {
            Selectable selectable = target.GetComponent<Selectable>();
            if (selectable != null)
            {
                return selectable.GetType().Name;
            }

            Graphic graphic = target.GetComponent<Graphic>();
            if (graphic != null)
            {
                return graphic.GetType().Name;
            }

            return target.GetType().Name;
        }
    }
}
