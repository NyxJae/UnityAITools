using UnityEngine;
using UnityEditor;
using LitJson2_utf;
using System;
using System.Collections.Generic;
using AgentCommands.Utils.Serialization;

namespace AgentCommands.Utils
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

                // 如果需要过滤私有字段, 建立字段映射
                Dictionary<string, System.Reflection.FieldInfo> fieldMap = null;
                if (!includePrivate)
                {
                    fieldMap = SerializedFieldFilter.GetFieldMap(obj.GetType());
                }

                // 跳过根属性, 使用Next()以包含[HideInInspector]字段
                if (prop.Next(true))
                {
                    do
                    {
                        // 跳过脚本字段
                        if (prop.propertyPath == "m_Script")
                        {
                            continue;
                        }

                        // 过滤私有字段
                        if (!includePrivate && fieldMap != null)
                        {
                            string baseFieldName = SerializedFieldFilter.ExtractBaseFieldName(prop.propertyPath);
                            if (fieldMap.TryGetValue(baseFieldName, out System.Reflection.FieldInfo field) && 
                                field.IsPrivate)
                            {
                                continue;
                            }
                        }

                        JsonData value = SerializedPropertyConverter.ConvertSerializedProperty(prop);
                        SerializedJsonTreeBuilder.InsertPropertyValue(
                            result, 
                            PropertyPathParser.ParsePropertyPath(prop.propertyPath), 
                            value);
                    }
                    while (prop.Next(false));
                }
            }

            return result;
        }
    }
}
