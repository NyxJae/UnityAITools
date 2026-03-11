using System;
using UnityEngine;
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
                if (i + 1 < tokens.Count)
                {
                    nextToken = tokens[i + 1];
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
                    if (ShouldSuppressGradientInternalField(parent, token.FieldName) ||
                        ShouldSuppressAnimationCurveInternalField(parent, token.FieldName))
                    {
                        return null;
                    }

                    return GetOrCreateContainerNode(parent, token.FieldName, GetExpectedContainerType(nextToken));

                case PathTokenType.ArrayIndex:
                    JsonData array = GetOrCreateArrayNode(parent, token.FieldName);
                    if (array == null)
                    {
                        return null;
                    }

                    EnsureArrayCapacity(array, token.ArrayIndex);
                    if (!array.IsArray || token.ArrayIndex >= array.Count)
                    {
                        return null;
                    }

                    return GetOrCreateArrayElementNode(array, token.ArrayIndex, GetExpectedContainerType(nextToken));

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
                        if (ShouldSuppressAnimationCurveInternalField(parent, token.FieldName) ||
                            ShouldSuppressGradientInternalField(parent, token.FieldName))
                        {
                            return;
                        }

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
        /// 判断当前对象节点是否已是 AnimationCurve 结构化模板, 若是则抑制后续底层 m_* 字段写入.
        /// </summary>
        /// <param name="parent">父节点.</param>
        /// <param name="fieldName">当前字段名.</param>
        /// <returns>是否应抑制写入.</returns>
        private static bool ShouldSuppressAnimationCurveInternalField(JsonData parent, string fieldName)
        {
            if (!parent.IsObject)
            {
                return false;
            }

            if (!parent.ContainsKey("keys"))
            {
                return false;
            }

            return fieldName == "m_Curve" ||
                   fieldName == "m_PreInfinity" ||
                   fieldName == "m_PostInfinity" ||
                   fieldName == "m_RotationOrder";
        }

        /// <summary>
        /// 判断当前对象节点是否已是 Gradient 结构化模板, 若是则抑制后续底层 key/ctime/atime/m_* 字段写入.
        /// </summary>
        /// <param name="parent">父节点.</param>
        /// <param name="fieldName">当前字段名.</param>
        /// <returns>是否应抑制写入.</returns>
        private static bool ShouldSuppressGradientInternalField(JsonData parent, string fieldName)
        {
            if (!parent.IsObject)
            {
                return false;
            }

            bool isGradientTemplate = parent.ContainsKey("mode") ||
                                      parent.ContainsKey("colorKeys") ||
                                      parent.ContainsKey("alphaKeys");
            if (!isGradientTemplate)
            {
                return false;
            }

            return fieldName.StartsWith("key", StringComparison.Ordinal) ||
                   fieldName.StartsWith("ctime", StringComparison.Ordinal) ||
                   fieldName.StartsWith("atime", StringComparison.Ordinal) ||
                   fieldName == "m_Mode" ||
                   fieldName == "m_ColorSpace" ||
                   fieldName == "m_NumColorKeys" ||
                   fieldName == "m_NumAlphaKeys";
        }

        /// <summary>
        /// 获取期望的容器类型.
        /// </summary>
        /// <param name="nextToken">下一个token.</param>
        /// <returns>期望容器类型.</returns>
        private static JsonType? GetExpectedContainerType(PathToken? nextToken)
        {
            if (!nextToken.HasValue)
            {
                return null;
            }

            switch (nextToken.Value.Type)
            {
                case PathTokenType.Field:
                    return JsonType.Object;
                case PathTokenType.ArrayIndex:
                    return JsonType.Array;
                default:
                    return null;
            }
        }

        /// <summary>
        /// 获取或创建字段容器节点. 如果已有叶子值与期望容器冲突, 则保留原值并停止继续下钻, 避免静默覆盖已有数据.
        /// </summary>
        /// <param name="parent">父节点.</param>
        /// <param name="fieldName">字段名.</param>
        /// <param name="expectedType">期望容器类型.</param>
        /// <returns>可继续下钻的容器节点, 无法安全下钻时返回null.</returns>
        private static JsonData GetOrCreateContainerNode(JsonData parent, string fieldName, JsonType? expectedType)
        {
            if (!parent.IsObject || !expectedType.HasValue)
            {
                return null;
            }

            if (parent.ContainsKey(fieldName))
            {
                JsonData existingNode = parent[fieldName];
                if (IsContainerType(existingNode, expectedType.Value))
                {
                    return existingNode;
                }

                // 保留已有值, 并打印冲突路径, 便于在 Unity Console 中排查为何无法继续下钻.
                Debug.LogWarning("[SerializedJsonTreeBuilder] Container conflict on field '" + fieldName + "', expected=" + expectedType.Value + ", existingValueType=" + DescribeJsonNode(existingNode));
                return null;
            }

            JsonData newNode = new JsonData();
            newNode.SetJsonType(expectedType.Value);
            parent[fieldName] = newNode;
            return newNode;
        }

        /// <summary>
        /// 获取或创建数组节点. 如果已有非数组值, 则保留原值并停止继续下钻.
        /// </summary>
        /// <param name="parent">父节点.</param>
        /// <param name="fieldName">字段名.</param>
        /// <returns>数组节点.</returns>
        private static JsonData GetOrCreateArrayNode(JsonData parent, string fieldName)
        {
            if (!parent.IsObject)
            {
                return null;
            }

            if (parent.ContainsKey(fieldName))
            {
                JsonData existingNode = parent[fieldName];
                if (existingNode.IsArray)
                {
                    return existingNode;
                }

                Debug.LogWarning("[SerializedJsonTreeBuilder] Array conflict on field '" + fieldName + "', existingValueType=" + DescribeJsonNode(existingNode));
                return null;
            }

            JsonData newArray = new JsonData();
            newArray.SetJsonType(JsonType.Array);
            parent[fieldName] = newArray;
            return newArray;
        }

        /// <summary>
        /// 获取或创建数组元素容器节点. 如果元素已有非期望容器值, 则保留原值并停止继续下钻.
        /// </summary>
        /// <param name="array">数组节点.</param>
        /// <param name="index">元素索引.</param>
        /// <param name="expectedType">期望容器类型.</param>
        /// <returns>元素容器节点.</returns>
        private static JsonData GetOrCreateArrayElementNode(JsonData array, int index, JsonType? expectedType)
        {
            if (!expectedType.HasValue)
            {
                return index < array.Count ? array[index] : null;
            }

            JsonData element = array[index];
            if (element == null)
            {
                JsonData newNode = new JsonData();
                newNode.SetJsonType(expectedType.Value);
                array[index] = newNode;
                return newNode;
            }

            if (IsContainerType(element, expectedType.Value))
            {
                return element;
            }

            Debug.LogWarning("[SerializedJsonTreeBuilder] Array element conflict at index=" + index + ", expected=" + expectedType.Value + ", existingValueType=" + DescribeJsonNode(element));
            return null;
        }

        /// <summary>
        /// 判断节点是否与期望容器类型一致.
        /// </summary>
        /// <param name="node">目标节点.</param>
        /// <param name="expectedType">期望类型.</param>
        /// <returns>是否一致.</returns>
        private static bool IsContainerType(JsonData node, JsonType expectedType)
        {
            if (node == null)
            {
                return false;
            }

            switch (expectedType)
            {
                case JsonType.Object:
                    return node.IsObject;
                case JsonType.Array:
                    return node.IsArray;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 描述当前 Json 节点类型, 便于日志排查容器冲突.
        /// </summary>
        /// <param name="node">目标节点.</param>
        /// <returns>节点类型描述.</returns>
        private static string DescribeJsonNode(JsonData node)
        {
            if (node == null)
            {
                return "null";
            }

            if (node.IsObject)
            {
                return "Object";
            }

            if (node.IsArray)
            {
                return "Array";
            }

            if (node.IsString)
            {
                return "String";
            }

            if (node.IsBoolean)
            {
                return "Boolean";
            }

            if (node.IsInt || node.IsLong || node.IsDouble)
            {
                return "Number";
            }

            return "Value";
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
