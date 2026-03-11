using UnityEngine;
using UnityEditor;
using LitJson2_utf;
using System;
using System.Collections.Generic;

namespace UnityAgentSkills.Utils
{
    /// <summary>
    /// 组件信息.
    /// </summary>
    internal class ComponentInfo
    {
        public string type;
        public int instanceID;
        public int componentIndex;
        public string scriptPath;
        public JsonData properties;

        public ComponentInfo()
        {
            properties = new JsonData();
            properties.SetJsonType(JsonType.Object);
        }
    }

    /// <summary>
    /// 组件属性读取工具,支持类型过滤和属性过滤.
    /// </summary>
    internal static class ComponentPropertyReader
    {
        /// <summary>
        /// 读取GameObject上的所有组件信息.
        /// </summary>
        /// <param name="gameObject">目标GameObject.</param>
        /// <param name="componentFilter">组件类型过滤,null/空数组/全空白词项表示不过滤.支持 contains + IgnoreCase + OR.</param>
        /// <param name="propertyFilter">属性名过滤,null/空数组/全空白词项表示不过滤.支持 contains + IgnoreCase + OR.</param>
        /// <param name="includePrivateFields">是否包含私有字段.</param>
        /// <returns>组件信息列表.</returns>
        public static List<ComponentInfo> ReadComponents(
            GameObject gameObject,
            string[] componentFilter = null,
            string[] propertyFilter = null,
            bool includePrivateFields = false)
        {
            List<ComponentInfo> result = new List<ComponentInfo>();
            if (gameObject == null)
            {
                Debug.LogWarning("[ComponentPropertyReader] GameObject is null");
                return result;
            }

            string[] normalizedComponentFilter = NormalizeFilter(componentFilter);
            string[] normalizedPropertyFilter = NormalizeFilter(propertyFilter);
            Component[] components = gameObject.GetComponents<Component>();
            Dictionary<Type, int> componentTypeIndices = new Dictionary<Type, int>();

            foreach (Component comp in components)
            {
                try
                {
                    Type componentType = comp.GetType();
                    int componentIndex = 0;
                    if (componentTypeIndices.TryGetValue(componentType, out int existingIndex))
                    {
                        componentIndex = existingIndex;
                    }
                    componentTypeIndices[componentType] = componentIndex + 1;

                    if (!MatchesFilter(componentType.Name, normalizedComponentFilter))
                    {
                        continue;
                    }

                    ComponentInfo info = new ComponentInfo
                    {
                        type = componentType.Name,
                        instanceID = comp.GetInstanceID(),
                        componentIndex = componentIndex
                    };

                    // 如果是MonoBehaviour,获取脚本路径
                    if (comp is MonoBehaviour monoBehaviour && monoBehaviour != null)
                    {
                        MonoScript script = MonoScript.FromMonoBehaviour(monoBehaviour);
                        if (script != null)
                        {
                            string scriptPath = AssetDatabase.GetAssetPath(script);
                            if (!string.IsNullOrEmpty(scriptPath))
                            {
                                info.scriptPath = scriptPath;
                            }
                        }
                    }

                    JsonData allProperties = SerializedObjectHelper.GetSerializedProperties(comp, includePrivateFields);
                    info.properties = FilterProperties(allProperties, normalizedPropertyFilter);

                    result.Add(info);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[ComponentPropertyReader] Error processing component {comp?.GetType().Name ?? "null"}: {ex.Message}\nStack: {ex.StackTrace}");
                }
            }

            return result;
        }

        /// <summary>
        /// 规范化过滤词项,空结果返回 null 表示不过滤.
        /// </summary>
        /// <param name="filters">原始过滤词项.</param>
        /// <returns>规范化后的过滤词项.</returns>
        private static string[] NormalizeFilter(string[] filters)
        {
            if (filters == null || filters.Length == 0)
            {
                return null;
            }

            List<string> normalized = new List<string>();
            foreach (string filter in filters)
            {
                if (!string.IsNullOrWhiteSpace(filter))
                {
                    normalized.Add(filter.Trim());
                }
            }

            return normalized.Count > 0 ? normalized.ToArray() : null;
        }

        /// <summary>
        /// 判断文本是否命中过滤词项.
        /// </summary>
        /// <param name="value">待匹配文本.</param>
        /// <param name="filters">过滤词项.</param>
        /// <returns>是否命中.</returns>
        private static bool MatchesFilter(string value, string[] filters)
        {
            if (filters == null)
            {
                return true;
            }

            foreach (string filter in filters)
            {
                if (value.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 按属性名过滤属性树,命中父字段时保留整棵子树.
        /// </summary>
        /// <param name="properties">原始属性树.</param>
        /// <param name="propertyFilter">规范化后的属性过滤词项.</param>
        /// <returns>过滤后的属性树.</returns>
        private static JsonData FilterProperties(JsonData properties, string[] propertyFilter)
        {
            if (propertyFilter == null)
            {
                return properties ?? CreateEmptyObject();
            }

            if (properties == null)
            {
                return CreateEmptyObject();
            }

            JsonData filtered = FilterJsonNode(properties, propertyFilter, isRoot: true);
            if (filtered == null || !filtered.IsObject)
            {
                return CreateEmptyObject();
            }

            return filtered;
        }

        /// <summary>
        /// 递归过滤 Json 节点.
        /// </summary>
        /// <param name="node">当前节点.</param>
        /// <param name="propertyFilter">属性过滤词项.</param>
        /// <param name="isRoot">是否为根节点.</param>
        /// <returns>过滤后的节点,未命中返回 null.</returns>
        private static JsonData FilterJsonNode(JsonData node, string[] propertyFilter, bool isRoot)
        {
            if (node == null)
            {
                return null;
            }

            if (node.IsObject)
            {
                JsonData filteredObject = CreateEmptyObject();
                bool hasMatchedChild = false;

                foreach (string key in node.Keys)
                {
                    JsonData child = node[key];
                    if (MatchesFilter(key, propertyFilter))
                    {
                        filteredObject[key] = child;
                        hasMatchedChild = true;
                        continue;
                    }

                    JsonData filteredChild = FilterJsonNode(child, propertyFilter, isRoot: false);
                    if (filteredChild != null)
                    {
                        // 为什么这样做: 当子字段命中时,需要保留当前父字段作为结构容器,
                        // 否则会出现子字段被命中但父字段名没有命中,结果树被裁成空对象的问题.
                        filteredObject[key] = filteredChild;
                        hasMatchedChild = true;
                    }
                }

                return hasMatchedChild || isRoot ? filteredObject : null;
            }

            if (node.IsArray)
            {
                JsonData filteredArray = CreateEmptyArray();
                bool hasMatchedElement = false;

                for (int i = 0; i < node.Count; i++)
                {
                    JsonData element = node[i];
                    JsonData filteredElement = FilterJsonNode(element, propertyFilter, isRoot: false);
                    if (filteredElement != null)
                    {
                        filteredArray.Add(filteredElement);
                        hasMatchedElement = true;
                    }
                }

                return hasMatchedElement ? filteredArray : null;
            }

            return null;
        }

        /// <summary>
        /// 创建空对象节点.
        /// </summary>
        /// <returns>空对象.</returns>
        private static JsonData CreateEmptyObject()
        {
            JsonData jsonData = new JsonData();
            jsonData.SetJsonType(JsonType.Object);
            return jsonData;
        }

        /// <summary>
        /// 创建空数组节点.
        /// </summary>
        /// <returns>空数组.</returns>
        private static JsonData CreateEmptyArray()
        {
            JsonData jsonData = new JsonData();
            jsonData.SetJsonType(JsonType.Array);
            return jsonData;
        }
    }
}
