using System;
using System.Collections.Generic;
using UnityEngine;
using K3Engine.Component.Interfaces;
using K3Engine.Component;
using LitJson2_utf;
using UnityAgentSkills.Utils;
using UnityAgentSkills.Plugins.K3Prefab.Models;

namespace UnityAgentSkills.Plugins.K3Prefab.Utils
{
    /// <summary>
    /// K3组件属性修改工具类
    /// 负责修改K3组件的属性，支持旧值验证（乐观锁）
    /// </summary>
    public static class K3ComponentPropertyModifier
    {
        /// <summary>
        /// 属性修改结果
        /// </summary>
        public class PropertyModificationResult
        {
            public string property { get; set; }
            public object oldValue { get; set; }
            public object expectedValue { get; set; }
            public object currentValue { get; set; }
            public object newValue { get; set; }
            public string status { get; set; }  // "success", "skipped", "failed"
            public string message { get; set; }
        }

        /// <summary>
        /// 批量修改K3组件属性
        /// </summary>
        /// <param name="match">K3组件匹配结果</param>
        /// <param name="modifications">修改请求列表</param>
        /// <returns>每个属性的修改结果</returns>
        public static List<PropertyModificationResult> ModifyProperties(
            K3ComponentMatch match,
            List<K3Prefab.Models.K3PropertyModification> modifications)
        {
            var results = new List<PropertyModificationResult>();

            if (match == null || match.component == null)
            {
                return results;
            }

            var component = match.component;

            foreach (var modification in modifications)
            {
                var result = new PropertyModificationResult
                {
                    property = modification.property,
                    oldValue = modification.oldValue,
                    newValue = modification.newValue
                };

                try
                {
                    // 读取当前属性值
                    object currentValue = GetPropertyValue(component, modification.property);
                    result.currentValue = currentValue;
                    result.expectedValue = modification.oldValue;

                    // 验证旧值
                    if (!CompareValues(currentValue, modification.oldValue))
                    {
                        result.status = "skipped";
                        result.message = $"旧值不匹配，期望{modification.oldValue}，实际{currentValue}，跳过修改";
                        results.Add(result);
                        continue;
                    }

                    // 设置新值
                    bool setSuccess = SetPropertyValue(component, modification.property, modification.newValue);

                    if (setSuccess)
                    {
                        result.status = "success";
                        result.message = "属性修改成功";
                    }
                    else
                    {
                        result.status = "failed";
                        result.message = $"属性{modification.property}设置失败";
                    }
                }
                catch (Exception ex)
                {
                    result.status = "failed";
                    result.message = $"修改属性{modification.property}时出错: {ex.Message}";
                }

                results.Add(result);
            }

            return results;
        }

        /// <summary>
        /// 获取K3组件的属性值
        /// </summary>
        private static object GetPropertyValue(IK3Component component, string propertyName)
        {
            switch (propertyName)
            {
                case "ID":
                    return component.ID;

                case "alpha":
                    return component.alpha;

                case "atlasName":
                    return component.atlasName ?? string.Empty;

                case "picName":
                    return component.picName ?? string.Empty;

                case "x":
                    return component.x;

                case "y":
                    return component.y;

                case "interactable":
                    // K3Button特有属性 - 需要转换为Unity Button组件
                    if (component is Component unityComp && unityComp is K3Button button)
                    {
                        return button.interactable;
                    }
                    break;

                case "text":
                    // K3Label特有属性 - 需要转换为Unity Text组件
                    if (component is Component unityComp2 && unityComp2 is K3Label label)
                    {
                        return label.text;
                    }
                    break;

                default:
                    // 尝试从K3Property获取
                    if (component.property != null)
                    {
                        var property = component.property;
                        switch (propertyName)
                        {
                            case "parentID":
                                return property.parentID;
                            case "maxID":
                                return property.maxID;
                        }
                    }
                    break;
            }

            throw new ArgumentException($"属性{propertyName}不存在或不支持读取");
        }

        /// <summary>
        /// 设置K3组件的属性值
        /// </summary>
        private static bool SetPropertyValue(IK3Component component, string propertyName, object value)
        {
            try
            {
                switch (propertyName)
                {
                    case "alpha":
                        component.alpha = Convert.ToSingle(value);
                        return true;

                    case "atlasName":
                        component.atlasName = value?.ToString() ?? string.Empty;
                        return true;

                    case "picName":
                        component.picName = value?.ToString() ?? string.Empty;
                        return true;

                    case "x":
                        component.x = Convert.ToSingle(value);
                        return true;

                    case "y":
                        component.y = Convert.ToSingle(value);
                        return true;

                    case "interactable":
                        if (component is Component unityComp && unityComp is K3Button button)
                        {
                            button.interactable = Convert.ToBoolean(value);
                            return true;
                        }
                        return false;

                    case "text":
                        if (component is Component unityComp2 && unityComp2 is K3Label label)
                        {
                            label.text = value?.ToString() ?? string.Empty;
                            return true;
                        }
                        return false;

                    default:
                        // 尝试设置K3Property的属性
                        if (component.property != null)
                        {
                            var property = component.property;
                            switch (propertyName)
                            {
                                case "parentID":
                                    property.parentID = Convert.ToUInt32(value);
                                    return true;
                                case "maxID":
                                    property.maxID = Convert.ToUInt32(value);
                                    return true;
                            }
                        }
                        break;
                }

                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 比较两个值是否相等
        /// </summary>
        private static bool CompareValues(object value1, object value2)
        {
            if (value1 == null && value2 == null) return true;
            if (value1 == null || value2 == null) return false;

            // 类型相同直接比较
            if (value1.GetType() == value2.GetType())
            {
                return value1.Equals(value2);
            }

            // 类型不同时，尝试转换后比较
            try
            {
                if (value1 is IConvertible && value2 is IConvertible)
                {
                    return Convert.ToDouble(value1) == Convert.ToDouble(value2);
                }
            }
            catch
            {
                // 转换失败，返回false
            }

            return false;
        }
    }
}
