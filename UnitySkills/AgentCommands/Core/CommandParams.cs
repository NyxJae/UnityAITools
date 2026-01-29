using System;
using LitJson2_utf;

namespace AgentCommands.Core
{
    /// <summary>
    /// 统一读取命令params字段的工具类.
    /// </summary>
    internal sealed class CommandParams
    {
        /// <summary>
        /// 原始参数数据对象.
        /// </summary>
        private readonly JsonData _data;

        /// <summary>
        /// 创建参数读取器.
        /// </summary>
        /// <param name="data">原始params对象.</param>
        public CommandParams(JsonData data)
        {
            _data = data;
        }

        /// <summary>
        /// 判断参数是否存在.
        /// </summary>
        /// <param name="key">字段名.</param>
        /// <returns>是否存在.</returns>
        public bool Has(string key)
        {
            return _data != null && _data.IsObject && _data.ContainsKey(key);
        }

        /// <summary>
        /// 读取整型参数,不存在或类型不合法时抛异常.
        /// </summary>
        /// <param name="key">字段名.</param>
        /// <returns>整型值.</returns>
        public int GetInt(string key)
        {
            if (!Has(key))
            {
                throw new ArgumentException(AgentCommandErrorCodes.InvalidFields + ": Missing int param: " + key);
            }

            JsonData v = _data[key];
            if (v.IsInt) return (int)v;
            if (v.IsLong) return (int)(long)v;
            if (v.IsDouble) return (int)(double)v;
            if (v.IsString && int.TryParse((string)v, out int parsed)) return parsed;

            throw new ArgumentException(AgentCommandErrorCodes.InvalidFields + ": Invalid int param: " + key);
        }

        /// <summary>
        /// 读取整型参数,缺失则返回默认值.
        /// </summary>
        /// <param name="key">字段名.</param>
        /// <param name="defaultValue">默认值.</param>
        /// <returns>整型值.</returns>
        public int GetInt(string key, int defaultValue)
        {
            if (!Has(key)) return defaultValue;

            JsonData v = _data[key];
            if (v.IsInt) return (int)v;
            if (v.IsLong) return (int)(long)v;
            if (v.IsDouble) return (int)(double)v;
            if (v.IsString && int.TryParse((string)v, out int parsed)) return parsed;

            return defaultValue;
        }

        /// <summary>
        /// 读取布尔参数,缺失则返回默认值.
        /// </summary>
        /// <param name="key">字段名.</param>
        /// <param name="defaultValue">默认值.</param>
        /// <returns>布尔值.</returns>
        public bool GetBool(string key, bool defaultValue)
        {
            if (!Has(key)) return defaultValue;

            JsonData v = _data[key];
            if (v.IsBoolean) return (bool)v;
            if (v.IsString && bool.TryParse((string)v, out bool parsed)) return parsed;

            return defaultValue;
        }

        /// <summary>
        /// 读取字符串参数,缺失则返回默认值.
        /// </summary>
        /// <param name="key">字段名.</param>
        /// <param name="defaultValue">默认值.</param>
        /// <returns>字符串值.</returns>
        public string GetString(string key, string defaultValue)
        {
            if (!Has(key)) return defaultValue;

            JsonData v = _data[key];
            if (v.IsString) return (string)v;

            return v.ToString();
        }

        /// <summary>
        /// 获取原始数据对象.
        /// </summary>
        /// <returns>原始JsonData对象.</returns>
        public JsonData GetData()
        {
            return _data;
        }
    }
}
