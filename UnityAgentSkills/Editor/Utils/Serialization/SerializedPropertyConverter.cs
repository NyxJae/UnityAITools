using UnityEngine;
using UnityEditor;
using LitJson2_utf;

namespace UnityAgentSkills.Utils.Serialization
{
    /// <summary>
    /// SerializedProperty类型转换器, 将Unity序列化属性转换为JsonData.
    /// </summary>
    internal static class SerializedPropertyConverter
    {
        /// <summary>
        /// 将SerializedProperty转换为JsonData.
        /// </summary>
        /// <param name="prop">要转换的属性.</param>
        /// <returns>转换后的JsonData对象.</returns>
        public static JsonData ConvertSerializedProperty(SerializedProperty prop)
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
                    string stringValue = prop.stringValue ?? string.Empty;
                    return CreateStringValue(stringValue);
                case SerializedPropertyType.ObjectReference:
                    return CreateObjectReferenceValue(prop);
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
                case SerializedPropertyType.Vector4:
                    JsonData v4 = new JsonData();
                    v4.SetJsonType(JsonType.Object);
                    v4["x"] = prop.vector4Value.x;
                    v4["y"] = prop.vector4Value.y;
                    v4["z"] = prop.vector4Value.z;
                    v4["w"] = prop.vector4Value.w;
                    return v4;
                case SerializedPropertyType.Vector2Int:
                    JsonData v2Int = new JsonData();
                    v2Int.SetJsonType(JsonType.Object);
                    v2Int["x"] = prop.vector2IntValue.x;
                    v2Int["y"] = prop.vector2IntValue.y;
                    return v2Int;
                case SerializedPropertyType.Vector3Int:
                    JsonData v3Int = new JsonData();
                    v3Int.SetJsonType(JsonType.Object);
                    v3Int["x"] = prop.vector3IntValue.x;
                    v3Int["y"] = prop.vector3IntValue.y;
                    v3Int["z"] = prop.vector3IntValue.z;
                    return v3Int;
                case SerializedPropertyType.Color:
                    JsonData color = new JsonData();
                    color.SetJsonType(JsonType.Object);
                    color["r"] = prop.colorValue.r;
                    color["g"] = prop.colorValue.g;
                    color["b"] = prop.colorValue.b;
                    color["a"] = prop.colorValue.a;
                    return color;
                case SerializedPropertyType.LayerMask:
                    JsonData layerMask = new JsonData();
                    layerMask.SetJsonType(JsonType.Object);
                    layerMask["value"] = prop.intValue;
                    return layerMask;
                case SerializedPropertyType.Rect:
                    JsonData rect = new JsonData();
                    rect.SetJsonType(JsonType.Object);
                    rect["x"] = prop.rectValue.x;
                    rect["y"] = prop.rectValue.y;
                    rect["width"] = prop.rectValue.width;
                    rect["height"] = prop.rectValue.height;
                    return rect;
                case SerializedPropertyType.RectInt:
                    JsonData rectInt = new JsonData();
                    rectInt.SetJsonType(JsonType.Object);
                    rectInt["x"] = prop.rectIntValue.x;
                    rectInt["y"] = prop.rectIntValue.y;
                    rectInt["width"] = prop.rectIntValue.width;
                    rectInt["height"] = prop.rectIntValue.height;
                    return rectInt;
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
                case SerializedPropertyType.BoundsInt:
                    JsonData boundsInt = new JsonData();
                    boundsInt.SetJsonType(JsonType.Object);
                    JsonData centerInt = new JsonData();
                    centerInt.SetJsonType(JsonType.Object);
                    centerInt["x"] = prop.boundsIntValue.position.x;
                    centerInt["y"] = prop.boundsIntValue.position.y;
                    centerInt["z"] = prop.boundsIntValue.position.z;
                    boundsInt["position"] = centerInt;

                    JsonData sizeInt = new JsonData();
                    sizeInt.SetJsonType(JsonType.Object);
                    sizeInt["x"] = prop.boundsIntValue.size.x;
                    sizeInt["y"] = prop.boundsIntValue.size.y;
                    sizeInt["z"] = prop.boundsIntValue.size.z;
                    boundsInt["size"] = sizeInt;
                    return boundsInt;
                case SerializedPropertyType.Quaternion:
                    JsonData quat = new JsonData();
                    quat.SetJsonType(JsonType.Object);
                    quat["x"] = prop.quaternionValue.x;
                    quat["y"] = prop.quaternionValue.y;
                    quat["z"] = prop.quaternionValue.z;
                    quat["w"] = prop.quaternionValue.w;
                    return quat;
                case SerializedPropertyType.AnimationCurve:
                    return CreateAnimationCurveValue(prop.animationCurveValue);
                case SerializedPropertyType.Gradient:
                    return CreateGradientValue(prop.gradientValue);
                case SerializedPropertyType.ArraySize:
                    return new JsonData(GetArraySizeValue(prop));
                case SerializedPropertyType.Enum:
                    JsonData enumData = new JsonData();
                    enumData.SetJsonType(JsonType.Object);
                    enumData["value"] = prop.enumValueIndex;
                    enumData["name"] = GetEnumName(prop);
                    enumData["displayName"] = prop.displayName ?? string.Empty;
                    return enumData;
                case SerializedPropertyType.Generic:
                    // Generic类型作为对象容器,不在节点直接写值,由子属性填充
                    JsonData genericContainer = new JsonData();
                    genericContainer.SetJsonType(JsonType.Object);
                    return genericContainer;
                case SerializedPropertyType.ManagedReference:
                    return CreateManagedReferenceValue(prop);
                default:
                    return CreateFallbackValue(prop);
            }
        }

        /// <summary>
        /// 为 ManagedReference 创建稳定且可审查的基础描述结构.
        /// </summary>
        /// <param name="prop">托管引用属性.</param>
        /// <returns>托管引用描述结构.</returns>
        private static JsonData CreateManagedReferenceValue(SerializedProperty prop)
        {
            JsonData managedReferenceData = new JsonData();
            managedReferenceData.SetJsonType(JsonType.Object);
            managedReferenceData["referenceKind"] = "ManagedReference";

            string fullTypename = prop.managedReferenceFullTypename ?? string.Empty;
            managedReferenceData["isNull"] = string.IsNullOrEmpty(fullTypename);
            managedReferenceData["managedReferenceFullTypename"] = fullTypename;
            managedReferenceData["managedReferenceId"] = prop.managedReferenceId;
            managedReferenceData["type"] = GetManagedReferenceTypeName(fullTypename);
            managedReferenceData["assembly"] = GetManagedReferenceAssemblyName(fullTypename);
            return managedReferenceData;
        }

        /// <summary>
        /// 为 AnimationCurve 创建稳定且可审查的结构化结果.
        /// </summary>
        /// <param name="curve">动画曲线.</param>
        /// <returns>动画曲线结构.</returns>
        private static JsonData CreateAnimationCurveValue(AnimationCurve curve)
        {
            JsonData curveData = new JsonData();
            curveData.SetJsonType(JsonType.Object);

            JsonData keys = new JsonData();
            keys.SetJsonType(JsonType.Array);

            Keyframe[] keyframes = curve != null ? curve.keys : null;
            if (keyframes != null)
            {
                for (int i = 0; i < keyframes.Length; i++)
                {
                    Keyframe keyframe = keyframes[i];
                    JsonData keyData = new JsonData();
                    keyData.SetJsonType(JsonType.Object);
                    keyData["time"] = keyframe.time;
                    keyData["value"] = keyframe.value;
                    keyData["inTangent"] = keyframe.inTangent;
                    keyData["outTangent"] = keyframe.outTangent;
                    keyData["inWeight"] = keyframe.inWeight;
                    keyData["outWeight"] = keyframe.outWeight;
                    keyData["weightedMode"] = keyframe.weightedMode.ToString();
                    keys.Add(keyData);
                }
            }

            curveData["keys"] = keys;
            curveData["preWrapMode"] = curve != null ? curve.preWrapMode.ToString() : WrapMode.Default.ToString();
            curveData["postWrapMode"] = curve != null ? curve.postWrapMode.ToString() : WrapMode.Default.ToString();
            return curveData;
        }

        /// <summary>
        /// 为 Gradient 创建稳定且可审查的结构化结果.
        /// </summary>
        /// <param name="gradient">渐变.</param>
        /// <returns>渐变结构.</returns>
        private static JsonData CreateGradientValue(Gradient gradient)
        {
            JsonData gradientData = new JsonData();
            gradientData.SetJsonType(JsonType.Object);

            JsonData colorKeys = new JsonData();
            colorKeys.SetJsonType(JsonType.Array);
            JsonData alphaKeys = new JsonData();
            alphaKeys.SetJsonType(JsonType.Array);

            GradientColorKey[] gradientColorKeys = gradient != null ? gradient.colorKeys : null;
            if (gradientColorKeys != null)
            {
                for (int i = 0; i < gradientColorKeys.Length; i++)
                {
                    GradientColorKey colorKey = gradientColorKeys[i];
                    JsonData colorKeyData = new JsonData();
                    colorKeyData.SetJsonType(JsonType.Object);
                    colorKeyData["time"] = colorKey.time;
                    JsonData color = new JsonData();
                    color.SetJsonType(JsonType.Object);
                    color["r"] = colorKey.color.r;
                    color["g"] = colorKey.color.g;
                    color["b"] = colorKey.color.b;
                    color["a"] = colorKey.color.a;
                    colorKeyData["color"] = color;
                    colorKeys.Add(colorKeyData);
                }
            }

            GradientAlphaKey[] gradientAlphaKeys = gradient != null ? gradient.alphaKeys : null;
            if (gradientAlphaKeys != null)
            {
                for (int i = 0; i < gradientAlphaKeys.Length; i++)
                {
                    GradientAlphaKey alphaKey = gradientAlphaKeys[i];
                    JsonData alphaKeyData = new JsonData();
                    alphaKeyData.SetJsonType(JsonType.Object);
                    alphaKeyData["time"] = alphaKey.time;
                    alphaKeyData["alpha"] = alphaKey.alpha;
                    alphaKeys.Add(alphaKeyData);
                }
            }

            gradientData["mode"] = gradient != null ? gradient.mode.ToString() : GradientMode.Blend.ToString();
            gradientData["colorKeys"] = colorKeys;
            gradientData["alphaKeys"] = alphaKeys;
            return gradientData;
        }

        /// <summary>
        /// 为对象引用创建稳定且可扩展的基础描述结构.
        /// </summary>
        /// <param name="prop">对象引用属性.</param>
        /// <returns>对象引用描述结构.</returns>
        private static JsonData CreateObjectReferenceValue(SerializedProperty prop)
        {
            JsonData referenceData = new JsonData();
            referenceData.SetJsonType(JsonType.Object);
            referenceData["referenceKind"] = "ObjectReference";

            Object referenceObject = prop.objectReferenceValue;
            if (referenceObject == null)
            {
                referenceData["isNone"] = true;
                referenceData["instanceID"] = 0;
                referenceData["type"] = string.Empty;
                referenceData["name"] = string.Empty;
                referenceData["assetPath"] = string.Empty;
                return referenceData;
            }

            referenceData["isNone"] = false;
            referenceData["instanceID"] = referenceObject.GetInstanceID();
            referenceData["type"] = referenceObject.GetType().Name;
            referenceData["name"] = referenceObject.name ?? string.Empty;
            referenceData["assetPath"] = AssetDatabase.GetAssetPath(referenceObject) ?? string.Empty;
            return referenceData;
        }

        /// <summary>
        /// 为暂未专门支持的属性类型创建稳定回退结构.
        /// </summary>
        /// <param name="prop">要转换的属性.</param>
        /// <returns>稳定且可审查的回退对象.</returns>
        private static JsonData CreateFallbackValue(SerializedProperty prop)
        {
            JsonData fallback = new JsonData();
            fallback.SetJsonType(JsonType.Object);
            fallback["propertyType"] = prop.propertyType.ToString();
            fallback["displayName"] = prop.displayName ?? string.Empty;
            fallback["propertyPath"] = prop.propertyPath ?? string.Empty;
            fallback["stringValue"] = GetFallbackStringValue(prop);
            return fallback;
        }

        /// <summary>
        /// 获取未专门支持类型的稳定字符串值,避免泄漏编辑器内部对象字符串.
        /// </summary>
        /// <param name="prop">要转换的属性.</param>
        /// <returns>稳定字符串值.</returns>
        private static string GetFallbackStringValue(SerializedProperty prop)
        {
            try
            {
                string displayValue = prop.boxedValue != null ? prop.boxedValue.ToString() : string.Empty;
                return displayValue ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// 从 Unity 的 managedReferenceFullTypename 中提取具体类型名.
        /// </summary>
        /// <param name="fullTypename">完整托管引用类型名.</param>
        /// <returns>具体类型名.</returns>
        private static string GetManagedReferenceTypeName(string fullTypename)
        {
            if (string.IsNullOrEmpty(fullTypename))
            {
                return string.Empty;
            }

            int spaceIndex = fullTypename.IndexOf(' ');
            string typePart = spaceIndex >= 0 && spaceIndex + 1 < fullTypename.Length
                ? fullTypename.Substring(spaceIndex + 1)
                : fullTypename;

            int lastDotIndex = typePart.LastIndexOf('.');
            return lastDotIndex >= 0 && lastDotIndex + 1 < typePart.Length
                ? typePart.Substring(lastDotIndex + 1)
                : typePart;
        }

        /// <summary>
        /// 从 Unity 的 managedReferenceFullTypename 中提取程序集名.
        /// </summary>
        /// <param name="fullTypename">完整托管引用类型名.</param>
        /// <returns>程序集名.</returns>
        private static string GetManagedReferenceAssemblyName(string fullTypename)
        {
            if (string.IsNullOrEmpty(fullTypename))
            {
                return string.Empty;
            }

            int spaceIndex = fullTypename.IndexOf(' ');
            return spaceIndex > 0
                ? fullTypename.Substring(0, spaceIndex)
                : string.Empty;
        }

        /// <summary>
        /// 显式创建字符串 JsonData, 避免空字符串在后续链路中丢失字符串类型.
        /// </summary>
        /// <param name="value">字符串值.</param>
        /// <returns>字符串 JsonData.</returns>
        private static JsonData CreateStringValue(string value)
        {
            JsonData data = new JsonData();
            ((IJsonWrapper)data).SetString(value ?? string.Empty);
            return data;
        }

        /// <summary>
        /// 安全读取 ArraySize 值,避免在 Unity 某些内部 size 属性上直接访问 arraySize 触发异常.
        /// </summary>
        /// <param name="prop">数组大小属性.</param>
        /// <returns>数组大小值.</returns>
        private static int GetArraySizeValue(SerializedProperty prop)
        {
            try
            {
                return prop.intValue;
            }
            catch
            {
                try
                {
                    return prop.arraySize;
                }
                catch
                {
                    return 0;
                }
            }
        }

        /// <summary>
        /// 获取枚举属性当前值对应的名称.
        /// </summary>
        /// <param name="prop">枚举属性.</param>
        /// <returns>枚举名称.</returns>
        private static string GetEnumName(SerializedProperty prop)
        {
            try
            {
                if (prop.enumNames != null && prop.enumValueIndex >= 0 && prop.enumValueIndex < prop.enumNames.Length)
                {
                    return prop.enumNames[prop.enumValueIndex] ?? string.Empty;
                }
            }
            catch
            {
            }

            return string.Empty;
        }
    }
}
