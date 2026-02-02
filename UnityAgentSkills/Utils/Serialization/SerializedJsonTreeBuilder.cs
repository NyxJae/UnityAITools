using System;
using LitJson2_utf;

namespace UnityAgentSkills.Utils.Serialization
{
    /// <summary>
    /// JsonData树构建器, 根据token列表构建嵌套JSON结构.
    /// </summary>
    internal static class SerializedJsonTreeBuilder
    {
        /// <summary>
        /// 将属性值插入到JsonData树中.
        /// 根据token列表构建嵌套结构, 支持对象节点创建/复用、数组扩容与占位null.
        /// </summary>
        /// <param name="root">根JsonData对象.</param>
        /// <param name="tokens">属性路径token列表.</param>
        /// <param name="value">要插入的属性值.</param>
        public static void InsertPropertyValue(JsonData root, System.Collections.Generic.List<PathToken> tokens, JsonData value)
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
