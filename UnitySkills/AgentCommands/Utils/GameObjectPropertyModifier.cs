using UnityEngine;
using UnityEditor;
using LitJson2_utf;
using System;
using System.Collections.Generic;

namespace AgentCommands.Utils
{
    /// <summary>
    /// GameObject属性修改工具类.
    /// 负责修改GameObject的8种可修改属性并保存预制体.
    /// </summary>
    internal static class GameObjectPropertyModifier
    {
        /// <summary>
        /// GameObject支持的6种可修改属性.
        /// 注意: navMeshLayer和icon因Unity API限制不支持修改,故不提供.
        /// </summary>
        private static readonly HashSet<string> SupportedProperties = new HashSet<string>
        {
            "name", "tag", "layer", "isActive", "isStatic", "hideFlags"
        };

        /// <summary>
        /// 属性修改记录.
        /// </summary>
        public class PropertyChange
        {
            public string name { get; set; }
            public object oldValue { get; set; }
            public object newValue { get; set; }
        }

        /// <summary>
        /// 修改GameObject属性.
        /// </summary>
        /// <param name="target">目标GameObject.</param>
        /// <param name="properties">要修改的属性对象.</param>
        /// <param name="modifiedProperties">输出参数: 实际修改的属性列表.</param>
        /// <returns>当前所有属性的值.</returns>
        public static JsonData ModifyProperties(GameObject target, JsonData properties, out List<PropertyChange> modifiedProperties)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            modifiedProperties = new List<PropertyChange>();

            // 验证properties不为空
            if (properties == null || !properties.IsObject || properties.Count == 0)
            {
                throw new ArgumentException("properties对象不能为空, 至少需要指定一个要修改的属性");
            }

            // 记录修改前的属性值(用于比较)
            Dictionary<string, object> oldValues = GetCurrentProperties(target);

            // 使用Undo记录修改
            Undo.RecordObject(target, "Modify GameObject Properties");

            // 遍历并修改属性
            foreach (string propName in properties.Keys)
            {
                if (!SupportedProperties.Contains(propName))
                {
                    throw new ArgumentException($"GameObject不支持属性: {propName}");
                }

                JsonData propValue = properties[propName];
                object oldValue = oldValues[propName];
                object newValue = null;

                try
                {
                    switch (propName)
                    {
                        case "name":
                            newValue = ModifyName(target, propValue);
                            break;
                        case "tag":
                            newValue = ModifyTag(target, propValue);
                            break;
                        case "layer":
                            newValue = ModifyLayer(target, propValue);
                            break;
                        case "isActive":
                            newValue = ModifyIsActive(target, propValue);
                            break;
                        case "isStatic":
                            newValue = ModifyIsStatic(target, propValue);
                            break;
                        case "hideFlags":
                            newValue = ModifyHideFlags(target, propValue);
                            break;
                    }

                    // 检查值是否实际发生变化
                    if (!Equals(oldValue, newValue))
                    {
                        modifiedProperties.Add(new PropertyChange
                        {
                            name = propName,
                            oldValue = oldValue,
                            newValue = newValue
                        });
                    }
                }
                catch (Exception ex)
                {
                    throw new ArgumentException($"修改属性{propName}失败: {ex.Message}", ex);
                }
            }

            // 标记场景已修改
            EditorUtility.SetDirty(target);

            // 返回当前所有属性值
            return GetCurrentPropertiesAsJson(target);
        }

        /// <summary>
        /// 修改name属性.
        /// </summary>
        private static object ModifyName(GameObject target, JsonData value)
        {
            if (value == null || !value.IsString)
            {
                throw new ArgumentException("name属性值必须是字符串");
            }
            string newName = (string)value;
            target.name = newName;
            return newName;
        }

        /// <summary>
        /// 修改tag属性.
        /// </summary>
        private static object ModifyTag(GameObject target, JsonData value)
        {
            if (value == null || !value.IsString)
            {
                throw new ArgumentException("tag属性值必须是字符串");
            }
            string newTag = (string)value;

            // Unity会自动创建不存在的tag, Untagged是默认tag
            // 这里不做额外验证,允许设置任何tag值
            target.tag = newTag;
            return newTag;
        }

        /// <summary>
        /// 修改layer属性.
        /// </summary>
        private static object ModifyLayer(GameObject target, JsonData value)
        {
            if (value == null)
            {
                throw new ArgumentException("layer属性值不能为null");
            }

            int newLayer;
            if (value.IsInt)
            {
                newLayer = (int)value;
            }
            else if (value.IsString)
            {
                // 尝试通过layer名称获取layer值
                string layerName = (string)value;
                newLayer = LayerMask.NameToLayer(layerName);
                if (newLayer == -1)
                {
                    throw new ArgumentException($"layer名称无效: {layerName}");
                }
            }
            else
            {
                throw new ArgumentException("layer属性值必须是整数或字符串");
            }

            // 验证layer范围(0-31)
            if (newLayer < 0 || newLayer > 31)
            {
                throw new ArgumentException($"layer属性值必须是0-31之间的整数, 当前值: {newLayer}");
            }

            target.layer = newLayer;
            return newLayer;
        }

        /// <summary>
        /// 修改isActive属性.
        /// </summary>
        private static object ModifyIsActive(GameObject target, JsonData value)
        {
            if (value == null || !value.IsBoolean)
            {
                throw new ArgumentException("isActive属性值必须是布尔值");
            }
            bool newIsActive = (bool)value;
            target.SetActive(newIsActive);
            return newIsActive;
        }

        /// <summary>
        /// 修改isStatic属性.
        /// </summary>
        private static object ModifyIsStatic(GameObject target, JsonData value)
        {
            if (value == null || !value.IsBoolean)
            {
                throw new ArgumentException("isStatic属性值必须是布尔值");
            }
            bool newIsStatic = (bool)value;
            target.isStatic = newIsStatic;
            return newIsStatic;
        }

        /// <summary>
        /// 修改hideFlags属性.
        /// </summary>
        private static object ModifyHideFlags(GameObject target, JsonData value)
        {
            if (value == null)
            {
                throw new ArgumentException("hideFlags属性值不能为null");
            }

            int newHideFlags;
            if (value.IsInt)
            {
                newHideFlags = (int)value;
            }
            else
            {
                throw new ArgumentException("hideFlags属性值必须是整数(HideFlags枚举值)");
            }

            target.hideFlags = (HideFlags)newHideFlags;
            return newHideFlags;
        }

        /// <summary>
        /// 获取GameObject当前所有属性值.
        /// </summary>
        private static Dictionary<string, object> GetCurrentProperties(GameObject target)
        {
            return new Dictionary<string, object>
            {
                { "name", target.name },
                { "tag", target.tag },
                { "layer", target.layer },
                { "isActive", target.activeSelf },
                { "isStatic", target.isStatic },
                { "hideFlags", (int)target.hideFlags }
            };
        }

        /// <summary>
        /// 获取GameObject当前所有属性值(JsonData格式).
        /// </summary>
        private static JsonData GetCurrentPropertiesAsJson(GameObject target)
        {
            JsonData result = new JsonData();
            result.SetJsonType(JsonType.Object);

            result["name"] = target.name;
            result["tag"] = target.tag;
            result["layer"] = target.layer;
            result["isActive"] = target.activeSelf;
            result["isStatic"] = target.isStatic;
            result["hideFlags"] = (int)target.hideFlags;

            return result;
        }

        /// <summary>
        /// 保存预制体到磁盘.
        /// </summary>
        /// <param name="prefabInstance">预制体实例.</param>
        /// <param name="prefabPath">预制体路径.</param>
        /// <returns>是否保存成功.</returns>
        public static bool SavePrefab(GameObject prefabInstance, string prefabPath)
        {
            if (prefabInstance == null)
            {
                return false;
            }

            try
            {
                // 保存预制体
                bool success = PrefabUtility.SaveAsPrefabAsset(prefabInstance, prefabPath, out bool savedSuccessfully);

                if (savedSuccessfully)
                {
                    // 刷新资产数据库
                    AssetDatabase.Refresh();
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameObjectPropertyModifier] 保存预制体失败: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }
    }
}
