using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace UnityAgentSkills.Utils.Serialization
{
    /// <summary>
    /// 序列化字段过滤器, 提供反射字段映射和缓存功能.
    /// </summary>
    internal static class SerializedFieldFilter
    {
        /// <summary>
        /// 字段缓存, 以类型为键, 字段名为值的映射字典为值.
        /// </summary>
        private static readonly Dictionary<Type, Dictionary<string, FieldInfo>> _fieldCache = 
            new Dictionary<Type, Dictionary<string, FieldInfo>>();

        /// <summary>
        /// 获取类型的字段映射字典, 使用缓存优化性能.
        /// </summary>
        /// <param name="type">目标类型.</param>
        /// <returns>字段名到FieldInfo的映射字典.</returns>
        public static Dictionary<string, FieldInfo> GetFieldMap(Type type)
        {
            if (type == null)
            {
                return new Dictionary<string, FieldInfo>();
            }

            // 检查缓存
            if (_fieldCache.TryGetValue(type, out Dictionary<string, FieldInfo> cachedMap))
            {
                return cachedMap;
            }

            // 构建字段映射
            Dictionary<string, FieldInfo> fieldMap = new Dictionary<string, FieldInfo>();
            Type currentType = type;
            
            // 递归遍历所有基类的字段, 包括私有字段
            while (currentType != null)
            {
                foreach (FieldInfo field in currentType.GetFields(
                    BindingFlags.Instance | 
                    BindingFlags.Public | 
                    BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly))
                {
                    if (!fieldMap.ContainsKey(field.Name))
                    {
                        fieldMap[field.Name] = field;
                    }
                }
                currentType = currentType.BaseType;
            }

            // 缓存结果
            _fieldCache[type] = fieldMap;
            return fieldMap;
        }

        /// <summary>
        /// 从propertyPath中提取基础字段名.
        /// </summary>
        /// <param name="propertyPath">Unity序列化属性路径.</param>
        /// <returns>基础字段名.</returns>
        public static string ExtractBaseFieldName(string propertyPath)
        {
            if (string.IsNullOrEmpty(propertyPath))
            {
                return propertyPath;
            }

            // 移除数组索引和嵌套路径, 只保留第一个字段名
            // 例如: "m_LocalPosition" -> "m_LocalPosition"
            // "array.data[0]" -> "array"
            // "m_Children.Array.data[0]" -> "m_Children"
            
            int dotIndex = propertyPath.IndexOf('.');
            if (dotIndex > 0)
            {
                return propertyPath.Substring(0, dotIndex);
            }
            
            int bracketIndex = propertyPath.IndexOf('[');
            if (bracketIndex > 0)
            {
                return propertyPath.Substring(0, bracketIndex);
            }
            
            return propertyPath;
        }
    }
}
