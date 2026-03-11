using System;
using System.Collections.Generic;
using UnityAgentSkills.Core;
using UnityAgentSkills.Utils;
using UnityAgentSkills.Utils.Serialization;
using LitJson2_utf;
using UnityEditor;
using UnityEngine;

namespace UnityAgentSkills.Plugins.Prefab.Handlers
{
    /// <summary>
    /// Prefab组件属性写入工具.
    /// 负责路径标准化,类型转换和引用解析.
    /// </summary>
    internal static class PrefabComponentPropertyWriter
    {
        /// <summary>
        /// 属性写入结果.
        /// </summary>
        internal sealed class PropertyWriteResult
        {
            /// <summary>
            /// 属性名(使用调用方原始key).
            /// </summary>
            public string name;

            /// <summary>
            /// 修改前值.
            /// </summary>
            public JsonData oldValue;

            /// <summary>
            /// 修改后值.
            /// </summary>
            public JsonData newValue;
        }

        /// <summary>
        /// 应用属性写入请求.
        /// </summary>
        public static List<PropertyWriteResult> ApplyProperties(Component component, GameObject prefabRoot, JsonData properties)
        {
            if (component == null)
            {
                throw new ArgumentNullException(nameof(component));
            }

            if (properties == null || !properties.IsObject || properties.Count == 0)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.EmptyProperties + ": properties对象不能为空");
            }

            var results = new List<PropertyWriteResult>();

            using (var serializedObject = new SerializedObject(component))
            {
                serializedObject.Update();

                foreach (string originalPath in properties.Keys)
                {
                    JsonData inputValue = properties[originalPath];
                    string canonicalPath = NormalizePropertyPathOrThrow(originalPath);
                    Debug.Log("[PrefabComponentPropertyWriter] property path normalize, original=" + originalPath + ", canonical=" + canonicalPath);

                    SerializedProperty property = serializedObject.FindProperty(canonicalPath);
                    if (property == null)
                    {
                        throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.PropertyNotFound + ": 属性不存在: " + originalPath);
                    }

                    JsonData oldValue = SerializedPropertyConverter.ConvertSerializedProperty(property);
                    ApplyValueToProperty(property, inputValue, prefabRoot, originalPath);
                    JsonData newValue = SerializedPropertyConverter.ConvertSerializedProperty(property);

                    if (!JsonEquals(oldValue, newValue))
                    {
                        results.Add(new PropertyWriteResult
                        {
                            name = originalPath,
                            oldValue = oldValue,
                            newValue = newValue
                        });
                    }
                }

                serializedObject.ApplyModifiedProperties();
            }

            return results;
        }

        /// <summary>
        /// 将简写属性路径转换为Unity canonical属性路径.
        /// </summary>
        public static string NormalizePropertyPathOrThrow(string propertyPath)
        {
            if (string.IsNullOrWhiteSpace(propertyPath))
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.InvalidPropertyPath + ": 属性路径不能为空");
            }

            string[] segments = propertyPath.Split('.');
            var normalizedSegments = new List<string>();

            foreach (string segmentRaw in segments)
            {
                string segment = segmentRaw == null ? string.Empty : segmentRaw.Trim();
                if (string.IsNullOrEmpty(segment))
                {
                    throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.InvalidPropertyPath + ": 属性路径包含空片段: " + propertyPath);
                }

                if (segment == "Array")
                {
                    normalizedSegments.Add(segment);
                    continue;
                }

                if (segment.StartsWith("data[", StringComparison.Ordinal) && segment.EndsWith("]", StringComparison.Ordinal))
                {
                    string indexText = segment.Substring(5, segment.Length - 6);
                    if (!int.TryParse(indexText, out int index) || index < 0)
                    {
                        throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.InvalidPropertyPath + ": 数组索引非法: " + segment);
                    }

                    normalizedSegments.Add("data[" + index + "]");
                    continue;
                }

                int leftBracket = segment.IndexOf('[');
                int rightBracket = segment.IndexOf(']');
                if (leftBracket >= 0 || rightBracket >= 0)
                {
                    if (leftBracket <= 0 || rightBracket != segment.Length - 1 || rightBracket <= leftBracket + 1)
                    {
                        throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.InvalidPropertyPath + ": 属性路径格式非法: " + segment);
                    }

                    string fieldName = segment.Substring(0, leftBracket);
                    string indexText = segment.Substring(leftBracket + 1, rightBracket - leftBracket - 1);
                    if (!int.TryParse(indexText, out int index) || index < 0)
                    {
                        throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.InvalidPropertyPath + ": 数组索引非法: " + segment);
                    }

                    normalizedSegments.Add(fieldName);
                    normalizedSegments.Add("Array");
                    normalizedSegments.Add("data[" + index + "]");
                    continue;
                }

                normalizedSegments.Add(segment);
            }

            return string.Join(".", normalizedSegments);
        }

        private static void ApplyValueToProperty(SerializedProperty property, JsonData value, GameObject prefabRoot, string originalPath)
        {
            if (property == null)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.PropertyNotFound + ": 属性不存在: " + originalPath);
            }

            try
            {
                if (property.isArray && property.propertyType != SerializedPropertyType.String)
                {
                    ApplyArrayValue(property, value, prefabRoot, originalPath);
                    return;
                }

                switch (property.propertyType)
                {
                    case SerializedPropertyType.Integer:
                        property.intValue = ReadInt(value, originalPath);
                        break;
                    case SerializedPropertyType.Float:
                        property.floatValue = ReadFloat(value, originalPath);
                        break;
                    case SerializedPropertyType.Boolean:
                        property.boolValue = ReadBool(value, originalPath);
                        break;
                    case SerializedPropertyType.String:
                        property.stringValue = ReadString(value, originalPath);
                        break;
                    case SerializedPropertyType.Enum:
                        property.intValue = ReadInt(value, originalPath);
                        break;
                    case SerializedPropertyType.Color:
                        property.colorValue = ReadColor(value, originalPath);
                        break;
                    case SerializedPropertyType.Vector2:
                        property.vector2Value = ReadVector2(value, originalPath);
                        break;
                    case SerializedPropertyType.Vector3:
                        property.vector3Value = ReadVector3(value, originalPath);
                        break;
                    case SerializedPropertyType.Vector4:
                        property.vector4Value = ReadVector4(value, originalPath);
                        break;
                    case SerializedPropertyType.Rect:
                        property.rectValue = ReadRect(value, originalPath);
                        break;
                    case SerializedPropertyType.Bounds:
                        property.boundsValue = ReadBounds(value, originalPath);
                        break;
                    case SerializedPropertyType.Quaternion:
                        property.quaternionValue = ReadQuaternion(value, originalPath);
                        break;
                    case SerializedPropertyType.ObjectReference:
                        ApplyObjectReferenceValue(property, value, prefabRoot, originalPath);
                        break;
                    case SerializedPropertyType.Generic:
                    case SerializedPropertyType.ManagedReference:
                        ApplyGenericValue(property, value, prefabRoot, originalPath);
                        break;
                    default:
                        throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.TypeMismatch + ": 暂不支持的属性类型: " + property.propertyType + ", path=" + originalPath);
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.TypeMismatch + ": 属性赋值失败: " + originalPath + ", detail=" + ex.Message);
            }
        }

        private static void ApplyArrayValue(SerializedProperty property, JsonData value, GameObject prefabRoot, string originalPath)
        {
            if (IsNullJson(value))
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.TypeMismatch + ": 数组属性不能赋值null: " + originalPath);
            }

            if (!value.IsArray)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.TypeMismatch + ": 数组属性必须传数组值: " + originalPath);
            }

            property.arraySize = value.Count;
            for (int i = 0; i < value.Count; i++)
            {
                SerializedProperty element = property.GetArrayElementAtIndex(i);
                ApplyValueToProperty(element, value[i], prefabRoot, originalPath + "[" + i + "]");
            }
        }

        private static void ApplyGenericValue(SerializedProperty property, JsonData value, GameObject prefabRoot, string originalPath)
        {
            if (IsNullJson(value))
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.TypeMismatch + ": 对象属性不能赋值null: " + originalPath);
            }

            if (!value.IsObject)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.TypeMismatch + ": 对象属性必须传object值: " + originalPath);
            }

            foreach (string childKey in value.Keys)
            {
                string childPath = NormalizePropertyPathOrThrow(childKey);
                SerializedProperty childProperty = property.FindPropertyRelative(childPath);
                if (childProperty == null)
                {
                    throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.PropertyNotFound + ": 子属性不存在: " + originalPath + "." + childKey);
                }

                ApplyValueToProperty(childProperty, value[childKey], prefabRoot, originalPath + "." + childKey);
            }
        }

        private static void ApplyObjectReferenceValue(SerializedProperty property, JsonData value, GameObject prefabRoot, string originalPath)
        {
            if (IsNullJson(value))
            {
                property.objectReferenceValue = null;
                return;
            }

            string referenceKind = ReadReferenceKind(value);
            UnityEngine.Object referenceObject = ResolveReferenceObjectOrThrow(value, prefabRoot, originalPath);
            UnityEngine.Object oldReference = property.objectReferenceValue;
            property.objectReferenceValue = referenceObject;

            if (referenceObject != null && property.objectReferenceValue != referenceObject)
            {
                property.objectReferenceValue = oldReference;
                if (string.Equals(referenceKind, "asset", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.AssetTypeMismatch + ": 资源类型不匹配: " + originalPath);
                }

                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.ReferenceTargetTypeMismatch + ": 引用目标类型不匹配: " + originalPath);
            }
        }

        private static UnityEngine.Object ResolveReferenceObjectOrThrow(JsonData value, GameObject prefabRoot, string originalPath)
        {
            if (value == null || !value.IsObject || !value.ContainsKey("$ref"))
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.TypeMismatch + ": 引用属性必须传null或$ref对象: " + originalPath);
            }

            JsonData refData = value["$ref"];
            if (refData == null || !refData.IsObject)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.TypeMismatch + ": $ref必须是对象: " + originalPath);
            }

            string kind = ReadRequiredString(refData, "kind", originalPath + ".$ref.kind");
            Debug.Log("[PrefabComponentPropertyWriter] resolve reference, path=" + originalPath + ", kind=" + kind);
            switch (kind)
            {
                case "prefabGameObject":
                {
                    string targetPath = ReadRequiredString(refData, "objectPath", originalPath + ".$ref.objectPath");
                    int siblingIndex = ReadOptionalInt(refData, "siblingIndex", 0, originalPath + ".$ref.siblingIndex");
                    GameObject target = GameObjectPathFinder.FindByPath(prefabRoot, targetPath, siblingIndex);
                    if (target == null)
                    {
                        throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.ReferenceTargetNotFound + ": 引用GameObject不存在: " + targetPath + " (siblingIndex=" + siblingIndex + ")");
                    }

                    return target;
                }
                case "prefabComponent":
                {
                    string targetPath = ReadRequiredString(refData, "objectPath", originalPath + ".$ref.objectPath");
                    int siblingIndex = ReadOptionalInt(refData, "siblingIndex", 0, originalPath + ".$ref.siblingIndex");
                    string componentType = ReadRequiredString(refData, "componentType", originalPath + ".$ref.componentType");
                    int componentIndex = ReadOptionalInt(refData, "componentIndex", 0, originalPath + ".$ref.componentIndex");

                    GameObject target = GameObjectPathFinder.FindByPath(prefabRoot, targetPath, siblingIndex);
                    if (target == null)
                    {
                        throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.ReferenceTargetNotFound + ": 引用组件目标对象不存在: " + targetPath + " (siblingIndex=" + siblingIndex + ")");
                    }

                    Type type = PrefabComponentHandlerUtils.ResolveComponentTypeOrThrow(componentType);
                    Component[] matches = target.GetComponents(type);
                    if (matches == null || matches.Length == 0 || componentIndex < 0 || componentIndex >= matches.Length)
                    {
                        throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.ReferenceTargetNotFound + ": 引用组件不存在: " + componentType + " (componentIndex=" + componentIndex + ")");
                    }

                    return matches[componentIndex];
                }
                case "asset":
                {
                    string assetPath = ReadRequiredString(refData, "assetPath", originalPath + ".$ref.assetPath");
                    string normalizedAssetPath = PrefabComponentHandlerUtils.NormalizePrefabPath(assetPath);
                    string assetTypeName = ReadOptionalString(refData, "assetType", null);

                    UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(normalizedAssetPath);
                    if (asset == null)
                    {
                        throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.AssetNotFound + ": 资源不存在: " + normalizedAssetPath);
                    }

                    if (!string.IsNullOrWhiteSpace(assetTypeName))
                    {
                        Type assetType = PrefabComponentHandlerUtils.ResolveAssetTypeOrThrow(assetTypeName);
                        if (!assetType.IsAssignableFrom(asset.GetType()))
                        {
                            throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.AssetTypeMismatch + ": 资源类型不匹配, 期望=" + assetType.FullName + ", 实际=" + asset.GetType().FullName);
                        }
                    }

                    return asset;
                }
                default:
                    throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.TypeMismatch + ": $ref.kind 不支持: " + kind);
            }
        }

        private static bool IsNullJson(JsonData value)
        {
            return value == null || value.GetJsonType() == JsonType.None;
        }

        private static string ReadReferenceKind(JsonData value)
        {
            if (value == null || !value.IsObject || !value.ContainsKey("$ref"))
            {
                return string.Empty;
            }

            JsonData refData = value["$ref"];
            if (refData == null || !refData.IsObject || !refData.ContainsKey("kind"))
            {
                return string.Empty;
            }

            JsonData kind = refData["kind"];
            return kind != null ? kind.ToString() : string.Empty;
        }

        private static bool JsonEquals(JsonData left, JsonData right)
        {
            if (left == null && right == null)
            {
                return true;
            }

            if (left == null || right == null)
            {
                return false;
            }

            return string.Equals(left.ToJson(), right.ToJson(), StringComparison.Ordinal);
        }

        private static string ReadRequiredString(JsonData parent, string key, string fieldPath)
        {
            string value = ReadOptionalString(parent, key, null);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": 字段必填: " + fieldPath);
            }

            return value;
        }

        private static string ReadOptionalString(JsonData parent, string key, string defaultValue)
        {
            if (parent == null || !parent.IsObject || !parent.ContainsKey(key))
            {
                return defaultValue;
            }

            JsonData value = parent[key];
            if (IsNullJson(value))
            {
                return defaultValue;
            }

            if (value.IsString)
            {
                return value.ToString();
            }

            return value.ToString();
        }

        private static int ReadOptionalInt(JsonData parent, string key, int defaultValue, string fieldPath)
        {
            if (parent == null || !parent.IsObject || !parent.ContainsKey(key))
            {
                return defaultValue;
            }

            JsonData value = parent[key];
            if (value == null)
            {
                return defaultValue;
            }

            if (value.IsInt)
            {
                return (int)value;
            }
            if (value.IsLong)
            {
                return (int)(long)value;
            }
            if (value.IsDouble)
            {
                return (int)(double)value;
            }
            if (value.IsString && int.TryParse(value.ToString(), out int parsed))
            {
                return parsed;
            }

            throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.TypeMismatch + ": 整数字段类型错误: " + fieldPath);
        }

        private static int ReadInt(JsonData value, string originalPath)
        {
            if (value == null)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.TypeMismatch + ": 属性值不能为null: " + originalPath);
            }

            if (value.IsInt)
            {
                return (int)value;
            }
            if (value.IsLong)
            {
                return (int)(long)value;
            }
            if (value.IsDouble)
            {
                return (int)(double)value;
            }
            if (value.IsString && int.TryParse(value.ToString(), out int parsed))
            {
                return parsed;
            }

            throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.TypeMismatch + ": 期望int类型: " + originalPath);
        }

        private static float ReadFloat(JsonData value, string originalPath)
        {
            if (value == null)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.TypeMismatch + ": 属性值不能为null: " + originalPath);
            }

            if (value.IsDouble)
            {
                return (float)(double)value;
            }
            if (value.IsInt)
            {
                return (int)value;
            }
            if (value.IsLong)
            {
                return (long)value;
            }
            if (value.IsString && float.TryParse(value.ToString(), out float parsed))
            {
                return parsed;
            }

            throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.TypeMismatch + ": 期望float类型: " + originalPath);
        }

        private static bool ReadBool(JsonData value, string originalPath)
        {
            if (value == null)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.TypeMismatch + ": 属性值不能为null: " + originalPath);
            }

            if (value.IsBoolean)
            {
                return (bool)value;
            }
            if (value.IsString && bool.TryParse(value.ToString(), out bool parsed))
            {
                return parsed;
            }

            throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.TypeMismatch + ": 期望bool类型: " + originalPath);
        }

        private static string ReadString(JsonData value, string originalPath)
        {
            if (IsNullJson(value))
            {
                return string.Empty;
            }

            if (value.IsString)
            {
                return value.ToString();
            }

            throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.TypeMismatch + ": 期望string类型: " + originalPath);
        }

        private static Color ReadColor(JsonData value, string originalPath)
        {
            float r = ReadObjectFloat(value, "r", originalPath, true);
            float g = ReadObjectFloat(value, "g", originalPath, true);
            float b = ReadObjectFloat(value, "b", originalPath, true);
            float a = ReadObjectFloat(value, "a", originalPath, false, 1f);
            return new Color(r, g, b, a);
        }

        private static Vector2 ReadVector2(JsonData value, string originalPath)
        {
            float x = ReadObjectFloat(value, "x", originalPath, true);
            float y = ReadObjectFloat(value, "y", originalPath, true);
            return new Vector2(x, y);
        }

        private static Vector3 ReadVector3(JsonData value, string originalPath)
        {
            float x = ReadObjectFloat(value, "x", originalPath, true);
            float y = ReadObjectFloat(value, "y", originalPath, true);
            float z = ReadObjectFloat(value, "z", originalPath, true);
            return new Vector3(x, y, z);
        }

        private static Vector4 ReadVector4(JsonData value, string originalPath)
        {
            float x = ReadObjectFloat(value, "x", originalPath, true);
            float y = ReadObjectFloat(value, "y", originalPath, true);
            float z = ReadObjectFloat(value, "z", originalPath, true);
            float w = ReadObjectFloat(value, "w", originalPath, true);
            return new Vector4(x, y, z, w);
        }

        private static Rect ReadRect(JsonData value, string originalPath)
        {
            float x = ReadObjectFloat(value, "x", originalPath, true);
            float y = ReadObjectFloat(value, "y", originalPath, true);
            float width = ReadObjectFloat(value, "width", originalPath, true);
            float height = ReadObjectFloat(value, "height", originalPath, true);
            return new Rect(x, y, width, height);
        }

        private static Quaternion ReadQuaternion(JsonData value, string originalPath)
        {
            float x = ReadObjectFloat(value, "x", originalPath, true);
            float y = ReadObjectFloat(value, "y", originalPath, true);
            float z = ReadObjectFloat(value, "z", originalPath, true);
            float w = ReadObjectFloat(value, "w", originalPath, true);
            return new Quaternion(x, y, z, w);
        }

        private static Bounds ReadBounds(JsonData value, string originalPath)
        {
            if (value == null || !value.IsObject)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.TypeMismatch + ": 期望Bounds对象: " + originalPath);
            }

            if (!value.ContainsKey("center") || !value.ContainsKey("size"))
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.TypeMismatch + ": Bounds必须包含center和size: " + originalPath);
            }

            Vector3 center = ReadVector3(value["center"], originalPath + ".center");
            Vector3 size = ReadVector3(value["size"], originalPath + ".size");
            return new Bounds(center, size);
        }

        private static float ReadObjectFloat(JsonData parent, string key, string originalPath, bool required, float defaultValue = 0f)
        {
            if (parent == null || !parent.IsObject)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.TypeMismatch + ": 期望object类型: " + originalPath);
            }

            if (!parent.ContainsKey(key))
            {
                if (required)
                {
                    throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.TypeMismatch + ": 缺少字段" + key + ": " + originalPath);
                }

                return defaultValue;
            }

            return ReadFloat(parent[key], originalPath + "." + key);
        }
    }
}
