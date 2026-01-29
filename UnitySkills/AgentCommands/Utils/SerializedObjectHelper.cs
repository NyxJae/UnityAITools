using UnityEngine;
using UnityEditor;
using LitJson2_utf;
using System;
using System.Collections.Generic;

namespace AgentCommands.Utils
{
    /// <summary>
    /// 属性路径token类型.
    /// </summary>
    internal enum PathTokenType
    {
        Field,      // 普通字段, 例如 "m_LocalPosition"
        ArraySize,  // 数组大小, 例如 "array.size"
        ArrayIndex  // 数组索引, 例如 "array.data[0]" 中的 0
    }

    /// <summary>
    /// 属性路径token, 表示propertyPath的一个解析单元.
    /// </summary>
    internal struct PathToken
    {
        public PathTokenType Type;  // Token类型
        public string FieldName;    // 字段名(对于字段token)
        public int ArrayIndex;       // 数组索引(对于数组索引token)

        /// <summary>
        /// 创建字段token.
        /// </summary>
        public static PathToken CreateField(string fieldName)
        {
            return new PathToken
            {
                Type = PathTokenType.Field,
                FieldName = fieldName,
                ArrayIndex = -1
            };
        }

        /// <summary>
        /// 创建数组大小token.
        /// </summary>
        public static PathToken CreateArraySize(string fieldName)
        {
            return new PathToken
            {
                Type = PathTokenType.ArraySize,
                FieldName = fieldName,
                ArrayIndex = -1
            };
        }

        /// <summary>
        /// 创建数组索引token.
        /// </summary>
        public static PathToken CreateArrayIndex(string fieldName, int index)
        {
            return new PathToken
            {
                Type = PathTokenType.ArrayIndex,
                FieldName = fieldName,
                ArrayIndex = index
            };
        }

        public override string ToString()
        {
            switch (Type)
            {
                case PathTokenType.Field:
                    return $"Field({FieldName})";
                case PathTokenType.ArraySize:
                    return $"ArraySize({FieldName})";
                case PathTokenType.ArrayIndex:
                    return $"ArrayIndex({FieldName}[{ArrayIndex}])";
                default:
                    return "Unknown";
            }
        }
    }

    /// <summary>
    /// SerializedObject操作工具,统一处理属性读取和类型转换.
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
                    fieldMap = new Dictionary<string, System.Reflection.FieldInfo>();
                    Type currentType = obj.GetType();
                    // 递归遍历所有基类的字段, 包括私有字段
                    while (currentType != null)
                    {
                        foreach (System.Reflection.FieldInfo field in currentType.GetFields(
                            System.Reflection.BindingFlags.Instance | 
                            System.Reflection.BindingFlags.Public | 
                            System.Reflection.BindingFlags.NonPublic |
                            System.Reflection.BindingFlags.DeclaredOnly))
                        {
                            if (!fieldMap.ContainsKey(field.Name))
                            {
                                fieldMap[field.Name] = field;
                            }
                        }
                        currentType = currentType.BaseType;
                    }
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
                            string baseFieldName = ExtractBaseFieldName(prop.propertyPath);
                            if (fieldMap.TryGetValue(baseFieldName, out System.Reflection.FieldInfo field) && 
                                field.IsPrivate)
                            {
                                continue;
                            }
                        }

                        JsonData value = ConvertSerializedProperty(prop);
                        InsertPropertyValue(result, ParsePropertyPath(prop.propertyPath), value);
                    }
                    while (prop.Next(false));
                }
            }

            return result;
        }

        /// <summary>
        /// 从propertyPath中提取基础字段名.
        /// </summary>
        /// <param name="propertyPath">Unity序列化属性路径.</param>
        /// <returns>基础字段名.</returns>
        private static string ExtractBaseFieldName(string propertyPath)
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

        /// <summary>
        /// 解析Unity序列化属性路径为token列表.
        /// 支持格式:
        /// - "fieldName" -> [Field(fieldName)]
        /// - "array.size" -> [ArraySize(array)]
        /// - "array.data[index]" -> [Field(array), ArrayIndex(array, index)]
        /// - "parent.array.data[0].field" -> [Field(parent), ArrayIndex(parent.array, 0), Field(field)]
        /// </summary>
        /// <param name="propertyPath">Unity序列化属性路径.</param>
        /// <returns>Token列表, 解析失败返回空列表.</returns>
        public static List<PathToken> ParsePropertyPath(string propertyPath)
        {
            List<PathToken> tokens = new List<PathToken>();

            // 处理空路径
            if (string.IsNullOrEmpty(propertyPath))
            {
                return tokens;
            }

            // 分割路径为片段
            string[] segments = propertyPath.Split('.');

            foreach (string segment in segments)
            {
                if (string.IsNullOrEmpty(segment))
                {
                    continue;
                }

                // 处理 ".size" 格式 (数组大小)
                if (segment == "size")
                {
                    if (tokens.Count > 0)
                    {
                        int lastIndex = tokens.Count - 1;
                        PathToken lastToken = tokens[lastIndex];
                        if (lastToken.Type == PathTokenType.Field)
                        {
                            // 将上一个字段token转换为数组大小token
                            tokens[lastIndex] = PathToken.CreateArraySize(lastToken.FieldName);
                        }
                    }
                    continue;
                }

                // 处理 ".data[index]" 格式 (数组元素)
                if (segment.StartsWith("data["))
                {
                    if (tokens.Count > 0)
                    {
                        int lastIndex = tokens.Count - 1;
                        PathToken lastToken = tokens[lastIndex];

                        if (lastToken.Type == PathTokenType.Field)
                        {
                            // 解析索引: "data[0]" -> 0
                            string indexStr = segment.Substring(5, segment.Length - 6); // 移除 "data[" 和 "]"
                            if (int.TryParse(indexStr, out int index))
                            {
                                // 将上一个字段token转换为数组索引token
                                tokens[lastIndex] = PathToken.CreateArrayIndex(lastToken.FieldName, index);
                            }
                        }
                    }
                    continue;
                }

                // 跳过 "Array" 段, 因为 .size 和 .data[index] 会在后续段被处理
                if (segment == "Array")
                {
                    continue;
                }

                // 处理普通字段
                // 检查是否是直接数组访问, 例如 "array[0]"
                int bracketIndex = segment.IndexOf('[');
                if (bracketIndex > 0)
                {
                    string fieldName = segment.Substring(0, bracketIndex);
                    string indexStr = segment.Substring(bracketIndex + 1, segment.Length - bracketIndex - 2); // 移除 "[" 和 "]"
                    if (int.TryParse(indexStr, out int index))
                    {
                        tokens.Add(PathToken.CreateArrayIndex(fieldName, index));
                        continue;
                    }
                }

                // 普通字段
                tokens.Add(PathToken.CreateField(segment));
            }

            return tokens;
        }

        /// <summary>
        /// 将SerializedProperty转换为JsonData.
        /// </summary>
        private static JsonData ConvertSerializedProperty(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return new JsonData(prop.intValue);
                case SerializedPropertyType.Float:
                    return new JsonData(prop.floatValue);
                case SerializedPropertyType.Boolean:
                    return new JsonData(prop.boolValue);
                case SerializedPropertyType.String:
                    return new JsonData(prop.stringValue ?? "");
                case SerializedPropertyType.ObjectReference:
                    if (prop.objectReferenceValue != null)
                    {
                        JsonData refData = new JsonData();
                        refData.SetJsonType(JsonType.Object);
                        refData["instanceID"] = prop.objectReferenceValue.GetInstanceID();
                        refData["type"] = prop.objectReferenceValue.GetType().Name;
                        return refData;
                    }
                    // ObjectReference为null时, 返回明确的Object类型JsonData(表示无引用)
                    JsonData nullRef = new JsonData();
                    nullRef.SetJsonType(JsonType.Object);
                    nullRef["isNone"] = true;
                    return nullRef;
                case SerializedPropertyType.Vector2:
                    JsonData v2 = new JsonData();
                    v2.SetJsonType(JsonType.Object);
                    v2["x"] = prop.vector2Value.x;
                    v2["y"] = prop.vector2Value.y;
                    return v2;
                case SerializedPropertyType.Vector3:
                    JsonData v3 = new JsonData();
                    v3.SetJsonType(JsonType.Object);
                    v3["x"] = prop.vector3Value.x;
                    v3["y"] = prop.vector3Value.y;
                    v3["z"] = prop.vector3Value.z;
                    return v3;
                case SerializedPropertyType.Color:
                    JsonData color = new JsonData();
                    color.SetJsonType(JsonType.Object);
                    color["r"] = prop.colorValue.r;
                    color["g"] = prop.colorValue.g;
                    color["b"] = prop.colorValue.b;
                    color["a"] = prop.colorValue.a;
                    return color;
                case SerializedPropertyType.Rect:
                    JsonData rect = new JsonData();
                    rect.SetJsonType(JsonType.Object);
                    rect["x"] = prop.rectValue.x;
                    rect["y"] = prop.rectValue.y;
                    rect["width"] = prop.rectValue.width;
                    rect["height"] = prop.rectValue.height;
                    return rect;
                case SerializedPropertyType.Bounds:
                    JsonData bounds = new JsonData();
                    bounds.SetJsonType(JsonType.Object);
                    JsonData center = new JsonData();
                    center.SetJsonType(JsonType.Array);
                    center.Add(prop.boundsValue.center.x);
                    center.Add(prop.boundsValue.center.y);
                    center.Add(prop.boundsValue.center.z);
                    bounds["center"] = center;
                    
                    JsonData size = new JsonData();
                    size.SetJsonType(JsonType.Array);
                    size.Add(prop.boundsValue.size.x);
                    size.Add(prop.boundsValue.size.y);
                    size.Add(prop.boundsValue.size.z);
                    bounds["size"] = size;
                    return bounds;
                case SerializedPropertyType.Quaternion:
                    JsonData quat = new JsonData();
                    quat.SetJsonType(JsonType.Object);
                    quat["x"] = prop.quaternionValue.x;
                    quat["y"] = prop.quaternionValue.y;
                    quat["z"] = prop.quaternionValue.z;
                    quat["w"] = prop.quaternionValue.w;
                    return quat;
                case SerializedPropertyType.ArraySize:
                    return new JsonData(prop.arraySize);
                case SerializedPropertyType.Generic:
                    // Generic类型作为对象容器,不在节点直接写值,由子属性填充
                    JsonData genericContainer = new JsonData();
                    genericContainer.SetJsonType(JsonType.Object);
                    return genericContainer;
                case SerializedPropertyType.ManagedReference:
                    // ManagedReference类型作为对象容器,记录类型信息,由子属性填充
                    JsonData managedRefContainer = new JsonData();
                    managedRefContainer.SetJsonType(JsonType.Object);
                    managedRefContainer["managedReferenceFullTypename"] = prop.managedReferenceFullTypename ?? "";
                    managedRefContainer["managedReferenceId"] = prop.managedReferenceId;
                    return managedRefContainer;
                default:
                    return new JsonData(prop.ToString());
            }
        }

        /// <summary>
        /// 将属性值插入到JsonData树中.
        /// 根据token列表构建嵌套结构, 支持对象节点创建/复用、数组扩容与占位null.
        /// </summary>
        /// <param name="root">根JsonData对象.</param>
        /// <param name="tokens">属性路径token列表.</param>
        /// <param name="value">要插入的属性值.</param>
        public static void InsertPropertyValue(JsonData root, List<PathToken> tokens, JsonData value)
        {
            if (root == null || tokens == null || tokens.Count == 0)
            {
                return;
            }

            // 只有一个token, 直接设置
            if (tokens.Count == 1)
            {
                SetFinalValue(root, tokens[0], value);
                return;
            }

            JsonData currentNode = root;

            // 遍历token列表, 构建路径(处理前N-1个token)
            for (int i = 0; i < tokens.Count - 1; i++)
            {
                PathToken token = tokens[i];
                
                // 获取下一个token用于确定节点类型
                PathToken? nextToken = null;
                if (i + 2 < tokens.Count)
                {
                    nextToken = tokens[i + 2];
                }

                // 确保路径存在并继续
                currentNode = TraversePath(currentNode, token, nextToken);
                if (currentNode == null)
                {
                    // 路径构建失败
                    return;
                }
            }

            // 设置最后一个token的值到currentNode
            SetFinalValue(currentNode, tokens[tokens.Count - 1], value);
        }

        /// <summary>
        /// 沿路径遍历, 确保中间节点存在.
        /// </summary>
        /// <param name="parent">父节点.</param>
        /// <param name="token">当前token.</param>
        /// <param name="nextToken">下一个token, 用于确定需要创建的节点类型.</param>
        /// <returns>下一个节点, 失败返回null.</returns>
        private static JsonData TraversePath(JsonData parent, PathToken token, PathToken? nextToken = null)
        {
            switch (token.Type)
            {
                case PathTokenType.Field:
                    // 字段token, 确保对象节点存在
                    // 如果节点已存在,直接返回;否则创建新节点
                    if (parent.IsObject && parent.ContainsKey(token.FieldName))
                    {
                        JsonData existingNode = parent[token.FieldName];
                        // 如果已存在节点不是对象,需要重建
                        if (!existingNode.IsObject)
                        {
                            JsonData objNode = new JsonData();
                            objNode.SetJsonType(JsonType.Object);
                            parent[token.FieldName] = objNode;
                            return objNode;
                        }
                        return existingNode;
                    }
                    return GetOrCreateObjectNode(parent, token.FieldName);

                case PathTokenType.ArrayIndex:
                    // 数组索引token, 确保数组节点存在且有足够容量
                    JsonData array = GetOrCreateArrayNode(parent, token.FieldName);
                    if (array == null)
                    {
                        return null;
                    }

                    // 确保数组容量足够
                    EnsureArrayCapacity(array, token.ArrayIndex);

                    // 获取或创建数组元素
                    if (array.IsArray && token.ArrayIndex < array.Count)
                    {
                        JsonData element = array[token.ArrayIndex];
                        
                        // 如果元素为null且存在下一个token, 根据下一个token类型创建节点
                        if (element == null && nextToken.HasValue)
                        {
                            if (nextToken.Value.Type == PathTokenType.Field)
                            {
                                // 下一个是字段, 创建对象
                                JsonData objNode = new JsonData();
                                objNode.SetJsonType(JsonType.Object);
                                array[token.ArrayIndex] = objNode;
                                return objNode;
                            }
                            else if (nextToken.Value.Type == PathTokenType.ArrayIndex)
                            {
                                // 下一个是数组索引, 创建数组
                                JsonData arrNode = new JsonData();
                                arrNode.SetJsonType(JsonType.Array);
                                array[token.ArrayIndex] = arrNode;
                                return arrNode;
                            }
                        }
                        
                        return element;
                    }

                    return null;

                case PathTokenType.ArraySize:
                    // 数组大小token, 不应该出现在中间路径中
                    // Array.size通常是叶子节点
                    return null;

                default:
                    return null;
            }
        }

        /// <summary>
        /// 设置最终值.
        /// </summary>
        /// <param name="parent">父节点.</param>
        /// <param name="token">最后一个token.</param>
        /// <param name="value">要设置的值.</param>
        private static void SetFinalValue(JsonData parent, PathToken token, JsonData value)
        {
            switch (token.Type)
            {
                case PathTokenType.Field:
                    // 字段token, 直接设置
                    if (parent.IsObject)
                    {
                        parent[token.FieldName] = value;
                    }
                    break;

                case PathTokenType.ArrayIndex:
                    // 数组索引token, 设置数组元素
                    JsonData array = GetOrCreateArrayNode(parent, token.FieldName);
                    if (array != null && array.IsArray)
                    {
                        EnsureArrayCapacity(array, token.ArrayIndex);
                        if (token.ArrayIndex < array.Count)
                        {
                            array[token.ArrayIndex] = value;
                        }
                    }
                    break;

                case PathTokenType.ArraySize:
                    // 数组大小token, 实际调整数组长度
                    if (parent.IsObject)
                    {
                        // 尝试解析数组大小值
                        int arraySize = 0;
                        if (value.IsInt)
                        {
                            arraySize = (int)value;
                        }
                        else if (value.IsString)
                        {
                            int.TryParse(value.ToString(), out arraySize);
                        }

                        // 获取或创建数组节点
                        JsonData targetArray = GetOrCreateArrayNode(parent, token.FieldName);
                        if (targetArray != null && targetArray.IsArray)
                        {
                            // 确保数组容量足够
                            EnsureArrayCapacity(targetArray, arraySize - 1);
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// 获取或创建对象节点.
        /// </summary>
        /// <param name="parent">父节点.</param>
        /// <param name="fieldName">字段名.</param>
        /// <returns>对象节点.</returns>
        private static JsonData GetOrCreateObjectNode(JsonData parent, string fieldName)
        {
            if (!parent.IsObject)
            {
                // 父节点不是对象, 无法创建字段
                return null;
            }

            // 如果字段已存在且是对象, 直接返回
            if (parent.ContainsKey(fieldName))
            {
                JsonData existingNode = parent[fieldName];
                if (existingNode.IsObject)
                {
                    return existingNode;
                }

                // 字段存在但不是对象, 需要转换为对象
                JsonData objectNode = new JsonData();
                objectNode.SetJsonType(JsonType.Object);
                parent[fieldName] = objectNode;
                return objectNode;
            }

            // 字段不存在, 创建新对象
            JsonData newObject = new JsonData();
            newObject.SetJsonType(JsonType.Object);
            parent[fieldName] = newObject;
            return newObject;
        }

        /// <summary>
        /// 获取或创建数组节点.
        /// </summary>
        /// <param name="parent">父节点.</param>
        /// <param name="fieldName">字段名.</param>
        /// <returns>数组节点.</returns>
        private static JsonData GetOrCreateArrayNode(JsonData parent, string fieldName)
        {
            if (!parent.IsObject)
            {
                // 父节点不是对象, 无法创建字段
                return null;
            }

            // 如果字段已存在且是数组, 直接返回
            if (parent.ContainsKey(fieldName))
            {
                JsonData existingNode = parent[fieldName];
                if (existingNode.IsArray)
                {
                    return existingNode;
                }

                // 字段存在但不是数组, 需要转换为数组
                JsonData arrayNode = new JsonData();
                arrayNode.SetJsonType(JsonType.Array);
                parent[fieldName] = arrayNode;
                return arrayNode;
            }

            // 字段不存在, 创建新数组
            JsonData newArray = new JsonData();
            newArray.SetJsonType(JsonType.Array);
            parent[fieldName] = newArray;
            return newArray;
        }

        /// <summary>
        /// 确保数组容量足够, 不足时用null填充.
        /// </summary>
        /// <param name="array">数组节点.</param>
        /// <param name="requiredIndex">需要的索引.</param>
        private static void EnsureArrayCapacity(JsonData array, int requiredIndex)
        {
            if (!array.IsArray)
            {
                return;
            }

            // 如果当前容量不足, 用null填充
            while (array.Count <= requiredIndex)
            {
                array.Add(null);
            }
        }
    }
}
