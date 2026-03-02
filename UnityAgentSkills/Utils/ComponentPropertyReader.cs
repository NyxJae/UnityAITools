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
        public string scriptPath;
        public JsonData properties;

        public ComponentInfo()
        {
            properties = new JsonData();
            properties.SetJsonType(JsonType.Object);
        }
    }

    /// <summary>
    /// 组件属性读取工具,支持类型过滤和属性详细程度控制.
    /// </summary>
    internal static class ComponentPropertyReader
    {
        /// <summary>
        /// 读取GameObject上的所有组件信息.
        /// </summary>
        /// <param name="gameObject">目标GameObject.</param>
        /// <param name="componentFilter">组件类型过滤,null/空数组/空字符串表示不过滤.支持模糊匹配(包含匹配,大小写不敏感).</param>
        /// <param name="includePrivateFields">是否包含私有字段.</param>
        /// <returns>组件信息列表.</returns>
        public static List<ComponentInfo> ReadComponents(GameObject gameObject,
            string[] componentFilter = null, bool includePrivateFields = false)
        {
            List<ComponentInfo> result = new List<ComponentInfo>();
            if (gameObject == null)
            {
                Debug.LogWarning("[ComponentPropertyReader] GameObject is null");
                return result;
            }

            Component[] components = gameObject.GetComponents<Component>();

            foreach (Component comp in components)
            {
                try
                {
                    // 检查类型过滤: OR 关系,包含匹配,大小写不敏感.
                    if (componentFilter != null && componentFilter.Length > 0)
                    {
                        string typeName = comp.GetType().Name;
                        bool matched = false;
                        foreach (string filter in componentFilter)
                        {
                            if (string.IsNullOrWhiteSpace(filter))
                            {
                                matched = true;
                                break;
                            }

                            if (typeName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                matched = true;
                                break;
                            }
                        }

                        if (!matched)
                        {
                            continue;
                        }
                    }

                    ComponentInfo info = new ComponentInfo
                    {
                        type = comp.GetType().Name,
                        instanceID = comp.GetInstanceID()
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

                    // 读取属性
                    info.properties = SerializedObjectHelper.GetSerializedProperties(comp, includePrivateFields);

                    result.Add(info);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[ComponentPropertyReader] Error processing component {comp?.GetType().Name ?? "null"}: {ex.Message}\nStack: {ex.StackTrace}");
                }
            }

            return result;
        }
    }
}
