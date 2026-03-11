using LitJson2_utf;

namespace UnityAgentSkills.Utils.JsonBuilders
{
    /// <summary>
    /// 通用JSON结果构建工具,提供轻量级的JSON对象/数组创建方法.
    /// 减少重复的SetJsonType调用,简化JSON构建代码.
    /// </summary>
    internal static class JsonResultBuilder
    {
        /// <summary>
        /// 创建JsonData对象并设置为Object类型.
        /// </summary>
        /// <returns>Object类型的JsonData实例.</returns>
        public static JsonData CreateObject()
        {
            JsonData obj = new JsonData();
            obj.SetJsonType(JsonType.Object);
            return obj;
        }

        /// <summary>
        /// 创建JsonData数组并设置为Array类型.
        /// </summary>
        /// <returns>Array类型的JsonData实例.</returns>
        public static JsonData CreateArray()
        {
            JsonData arr = new JsonData();
            arr.SetJsonType(JsonType.Array);
            return arr;
        }

        /// <summary>
        /// 仅在值非空时添加键值对到JsonData对象.
        /// 避免在JSON中添加无意义的空值字段.
        /// </summary>
        /// <param name="obj">目标JsonData对象.</param>
        /// <param name="key">键名.</param>
        /// <param name="value">值.</param>
        public static void AddIfNotEmpty(JsonData obj, string key, object value)
        {
            if (obj == null || !obj.IsObject || string.IsNullOrEmpty(key))
            {
                return;
            }

            // 判断值是否为空
            if (value == null)
            {
                return;
            }

            // 字符串类型判断是否为空
            if (value is string str && string.IsNullOrEmpty(str))
            {
                return;
            }

            // 如果value已经是JsonData,直接使用;否则包装为JsonData
            if (value is JsonData jsonData)
            {
                obj[key] = jsonData;
            }
            else
            {
                obj[key] = new JsonData(value);
            }
        }

        /// <summary>
        /// 仅在值非空时添加键值对到JsonData对象(JsonData版本).
        /// </summary>
        /// <param name="obj">目标JsonData对象.</param>
        /// <param name="key">键名.</param>
        /// <param name="value">JsonData值.</param>
        public static void AddIfNotEmpty(JsonData obj, string key, JsonData value)
        {
            if (obj == null || !obj.IsObject || string.IsNullOrEmpty(key))
            {
                return;
            }

            if (value == null)
            {
                return;
            }

            obj[key] = value;
        }
    }
}
