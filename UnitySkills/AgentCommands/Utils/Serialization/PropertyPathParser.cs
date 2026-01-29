using System;
using System.Collections.Generic;
using LitJson2_utf;

namespace AgentCommands.Utils.Serialization
{
    /// <summary>
    /// Unity序列化属性路径解析器.
    /// </summary>
    internal static class PropertyPathParser
    {
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
    }
}
