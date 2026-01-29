using UnityEngine;
using UnityEditor;
using LitJson2_utf;

namespace AgentCommands.Utils.Serialization
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
    }
}
