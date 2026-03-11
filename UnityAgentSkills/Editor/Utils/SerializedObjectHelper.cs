using UnityEngine;
using UnityEditor;
using LitJson2_utf;
using System;
using System.Collections.Generic;
using UnityAgentSkills.Utils.Serialization;

namespace UnityAgentSkills.Utils
{
    /// <summary>
    /// SerializedObject操作工具,统一处理属性读取和类型转换.
    /// 现为入口协调器,内部调用各专职模块:
    /// - SerializedFieldFilter: 字段过滤与缓存
    /// - PropertyPathParser: 属性路径解析
    /// - SerializedPropertyConverter: 属性类型转换
    /// - SerializedJsonTreeBuilder: JSON树构建
    /// </summary>
    internal static class SerializedObjectHelper
    {
        /// <summary>
        /// 获取对象的所有可序列化属性.
        /// </summary>
        /// <param name="obj">目标对象.</param>
        /// <param name="includePrivate">是否包含私有字段.</param>
        /// <returns>属性字典.</returns>
        public static JsonData GetSerializedProperties(UnityEngine.Object obj, bool includePrivate = false)
        {
            if (obj == null)
            {
                return new JsonData();
            }

            JsonData result = new JsonData();
            result.SetJsonType(JsonType.Object);
            using (SerializedObject serializedObj = new SerializedObject(obj))
            {
                SerializedProperty prop = serializedObj.GetIterator();

                // 如果需要过滤私有字段, 建立字段映射.
                // 为什么这样做: queryComponents 的主数据源本来就是 SerializedObject,其中大量可见字段
                // 都是 private + [SerializeField] 或 Unity 内建已序列化字段.如果这里按 private 一刀切,
                // `m_Colors`,`m_Transition` 这类常见字段会被误删,导致 propertyFilter 看起来失效.
                Dictionary<string, System.Reflection.FieldInfo> fieldMap = null;
                if (!includePrivate)
                {
                    fieldMap = SerializedFieldFilter.GetFieldMap(obj.GetType());
                }

                // 跳过根属性, 使用深度优先遍历包含容器子属性, 避免 m_FontData 之类容器只留下空壳对象.
                if (prop.Next(true))
                {
                    int propIndex = 0;
                    string skipChildPathPrefix = null;

                    do
                    {
                        try
                        {
                            propIndex++;

                            if (!string.IsNullOrEmpty(skipChildPathPrefix) &&
                                prop.propertyPath.StartsWith(skipChildPathPrefix, StringComparison.Ordinal))
                            {
                                continue;
                            }

                            // 跳过脚本字段
                            if (prop.propertyPath == "m_Script")
                            {
                                continue;
                            }

                            // 仅在明确定位到“私有且未标记 SerializeField”的脚本字段时才过滤.
                            // Unity 内建组件的已序列化私有字段常无法稳定通过反射拿到同名 FieldInfo,
                            // 这类字段应保留,否则 propertyFilter 会错误裁成空对象.
                            if (!includePrivate && fieldMap != null)
                            {
                                string baseFieldName = SerializedFieldFilter.ExtractBaseFieldName(prop.propertyPath);
                                if (fieldMap.TryGetValue(baseFieldName, out System.Reflection.FieldInfo field) &&
                                    field.IsPrivate &&
                                    !SerializedFieldFilter.IsSerializeField(field))
                                {
                                    continue;
                                }
                            }

                            if (prop.propertyType == SerializedPropertyType.Generic && prop.isArray)
                            {
                                // 数组/列表容器本身由 Array.size 与 data[i] 子路径驱动建树, 这里不应先写入对象壳, 否则空集合会被误固定为 {}.
                                skipChildPathPrefix = null;
                                continue;
                            }

                            JsonData value = SerializedPropertyConverter.ConvertSerializedProperty(prop);
                            SerializedJsonTreeBuilder.InsertPropertyValue(
                                result, 
                                PropertyPathParser.ParsePropertyPath(prop.propertyPath), 
                                value);
// 字符串和 AnimationCurve 都可能继续暴露内部子路径, 但对外协议应只保留单一结构化模板.
                            skipChildPathPrefix =
                                prop.propertyType == SerializedPropertyType.String ||
                                prop.propertyType == SerializedPropertyType.AnimationCurve
                                    ? prop.propertyPath + "."
                                    : null;
                        }
                        catch (System.Exception ex)
                        {
                            string propertyPathStr = "unknown";
                            try
                            {
                                propertyPathStr = prop.propertyPath;
                            }
                            catch { }

                            Debug.LogError($"[SerializedObjectHelper] Error processing property {propIndex}: {ex.Message}\nPropertyPath: {propertyPathStr}\nStack: {ex.StackTrace}");
                        }
                    }
                    while (prop.Next(true));
                }
            }

            return result;
        }
    }
}
