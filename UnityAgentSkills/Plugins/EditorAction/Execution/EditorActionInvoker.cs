using System;
using System.Globalization;
using System.Reflection;
using UnityAgentSkills.Core;
using UnityAgentSkills.Plugins.EditorAction.Catalog;
using LitJson2_utf;

namespace UnityAgentSkills.Plugins.EditorAction.Execution
{
    /// <summary>
    /// EditorAction 执行器.
    /// 负责 actionArgs 绑定,方法执行与返回值封装.
    /// </summary>
    internal static class EditorActionInvoker
    {
        /// <summary>
        /// 执行动作.
        /// </summary>
        /// <param name="descriptor">动作描述.</param>
        /// <param name="actionArgs">动作参数.</param>
        /// <returns>执行结果.</returns>
        public static JsonData Invoke(EditorActionDescriptor descriptor, JsonData actionArgs)
        {
            if (descriptor == null || descriptor.Method == null)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": invalid action descriptor");
            }

            object[] args = BindArguments(descriptor, actionArgs);
            object returnValue = descriptor.Method.Invoke(null, args);

            JsonData result = new JsonData();
            result["actionId"] = descriptor.ActionId;
            result["declaringType"] = descriptor.Method.DeclaringType != null ? descriptor.Method.DeclaringType.FullName : string.Empty;
            result["methodName"] = descriptor.Method.Name;
            result["invoked"] = true;
            result["returnValue"] = ConvertReturnValue(returnValue);
            return result;
        }

        private static object[] BindArguments(EditorActionDescriptor descriptor, JsonData actionArgs)
        {
            ParameterInfo[] parameters = descriptor.Parameters ?? Array.Empty<ParameterInfo>();
            if (parameters.Length == 0)
            {
                return Array.Empty<object>();
            }

            bool hasArgsObject = actionArgs != null && actionArgs.IsObject;
            object[] bound = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterInfo parameter = parameters[i];
                string key = parameter.Name;

                if (hasArgsObject && actionArgs.ContainsKey(key))
                {
                    bound[i] = ConvertArgument(actionArgs[key], parameter.ParameterType, key);
                    continue;
                }

                if (parameter.HasDefaultValue)
                {
                    bound[i] = parameter.DefaultValue;
                    continue;
                }

                if (IsNullable(parameter.ParameterType))
                {
                    bound[i] = null;
                    continue;
                }

                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": missing actionArgs field: " + key);
            }

            return bound;
        }

        private static object ConvertArgument(JsonData value, Type targetType, string fieldName)
        {
            Type underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
            bool allowNull = IsNullable(targetType);

            if (value == null)
            {
                if (allowNull)
                {
                    return null;
                }

                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": field " + fieldName + " cannot be null");
            }

            if (underlying == typeof(JsonData))
            {
                return value;
            }

            if (underlying == typeof(string))
            {
                return value.IsString ? (string)value : value.ToJson();
            }

            if (underlying == typeof(int))
            {
                try
                {
                    if (value.IsInt) return (int)value;
                    if (value.IsLong) return checked((int)(long)value);
                    if (value.IsDouble) return checked((int)(double)value);
                    if (value.IsString && int.TryParse((string)value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i)) return i;
                }
                catch (OverflowException)
                {
                    throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": field " + fieldName + " is out of range");
                }
            }

            if (underlying == typeof(long))
            {
                try
                {
                    if (value.IsLong) return (long)value;
                    if (value.IsInt) return (long)(int)value;
                    if (value.IsDouble) return checked((long)(double)value);
                    if (value.IsString && long.TryParse((string)value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l)) return l;
                }
                catch (OverflowException)
                {
                    throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": field " + fieldName + " is out of range");
                }
            }

            if (underlying == typeof(double))
            {
                if (value.IsDouble) return (double)value;
                if (value.IsInt) return (double)(int)value;
                if (value.IsLong) return (double)(long)value;
                if (value.IsString && double.TryParse((string)value, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) return d;
            }

            if (underlying == typeof(float))
            {
                if (value.IsDouble) return (float)(double)value;
                if (value.IsInt) return (float)(int)value;
                if (value.IsLong) return (float)(long)value;
                if (value.IsString && float.TryParse((string)value, NumberStyles.Float, CultureInfo.InvariantCulture, out float f)) return f;
            }

            if (underlying == typeof(bool))
            {
                if (value.IsBoolean) return (bool)value;
                if (value.IsString && bool.TryParse((string)value, out bool b)) return b;
            }

            if (underlying.IsEnum)
            {
                if (value.IsString)
                {
                    string enumName = (string)value;
                    if (Enum.IsDefined(underlying, enumName))
                    {
                        return Enum.Parse(underlying, enumName);
                    }
                }

                if (value.IsInt)
                {
                    return Enum.ToObject(underlying, (int)value);
                }
            }

            throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": unsupported or invalid field: " + fieldName);
        }

        private static bool IsNullable(Type type)
        {
            if (type == null)
            {
                return false;
            }

            return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
        }

        private static JsonData ConvertReturnValue(object value)
        {
            if (value == null)
            {
                return new JsonData();
            }

            if (value is JsonData jsonData)
            {
                return jsonData;
            }

            if (value is string str)
            {
                return new JsonData(str);
            }

            if (value is bool boolValue)
            {
                return new JsonData(boolValue);
            }

            if (value is int intValue)
            {
                return new JsonData(intValue);
            }

            if (value is long longValue)
            {
                return new JsonData(longValue);
            }

            if (value is float floatValue)
            {
                return new JsonData((double)floatValue);
            }

            if (value is double doubleValue)
            {
                return new JsonData(doubleValue);
            }

            return new JsonData(value.ToString());
        }
    }
}
