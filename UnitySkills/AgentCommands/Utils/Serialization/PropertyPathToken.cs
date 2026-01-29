using System;

namespace AgentCommands.Utils.Serialization
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
}
