using System;
using UnityEngine;
using UnityEditor;
using K3Engine.Component.Interfaces;
using LitJson2_utf;
using AgentCommands.Utils;
using AgentCommands.Utils.JsonBuilders;
using AgentCommands.K3Prefab.Models;

namespace AgentCommands.K3Prefab.Utils
{
    /// <summary>
    /// K3组件属性读取工具类
    /// 负责读取K3组件的IK3Component接口属性和Unity序列化属性
    /// </summary>
    public static class K3ComponentPropertyReader
    {
        /// <summary>
        /// 读取K3组件的所有属性
        /// 包括IK3Component接口属性和Unity序列化属性
        /// </summary>
        /// <param name="match">K3组件匹配结果</param>
        /// <param name="includePrivateFields">是否包含私有字段</param>
        /// <returns>属性的JSON表示</returns>
        public static JsonData ReadK3ComponentProperties(K3ComponentMatch match, bool includePrivateFields = false)
        {
            if (match == null || match.component == null)
            {
                return new JsonData();
            }

            JsonData result = new JsonData();
            result.SetJsonType(JsonType.Object);

            var component = match.component;

            // 读取K3Property.ID（避免触发getter，直接从序列化数据读取）
            uint componentId = 0;
            if (component is Component unityComp)
            {
                var so = new SerializedObject(unityComp);
                var idProp = so.FindProperty("m_property");
                if (idProp != null)
                {
                    var propertyObj = idProp.objectReferenceValue;
                    if (propertyObj != null)
                    {
                        var propertySo = new SerializedObject(propertyObj);
                        var realIdProp = propertySo.FindProperty("m_ID");
                        if (realIdProp != null)
                        {
                            componentId = (uint)realIdProp.intValue;
                        }
                    }
                }
            }

            // 读取IK3Component接口属性（避免访问可能触发验证的getter）
            result["ID"] = (JsonData)componentId;
            result["alpha"] = (JsonData)component.alpha;
            result["atlasName"] = component.atlasName ?? string.Empty;
            result["picName"] = component.picName ?? string.Empty;
            result["IsVisible"] = (JsonData)component.IsVisible;
            result["ComponentName"] = component.ComponentName ?? string.Empty;

            // 读取位置信息
            result["x"] = (JsonData)component.x;
            result["y"] = (JsonData)component.y;

            // 读取K3Property对象的其他属性
            if (component.property != null)
            {
                var property = component.property;
                result["parentID"] = (JsonData)property.parentID;
                result["maxID"] = (JsonData)property.maxID;
            }

            // 读取Unity序列化属性（包括K3组件特有的私有字段）
            try
            {
                // 如果component是Unity Component，读取其序列化属性
                if (component is Component unityComponent)
                {
                    JsonData serializedProps = SerializedObjectHelper.GetSerializedProperties(
                        unityComponent, includePrivateFields);

                    // 合并序列化属性到结果中（避免覆盖已读取的IK3Component属性）
                    foreach (string key in serializedProps.Keys)
                    {
                        if (!result.Keys.Contains(key))
                        {
                            result[key] = serializedProps[key];
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"读取K3组件序列化属性时出错: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 读取GameObject属性
        /// </summary>
        /// <param name="gameObject">目标GameObject</param>
        /// <returns>GameObject属性的JSON表示</returns>
        public static JsonData ReadGameObjectProperties(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return new JsonData();
            }

            JsonData result = new JsonData();
            result.SetJsonType(JsonType.Object);

            result["name"] = gameObject.name ?? string.Empty;
            result["tag"] = gameObject.tag ?? string.Empty;
            result["layer"] = (JsonData)gameObject.layer;
            result["isActive"] = (JsonData)gameObject.activeSelf;
            result["isStatic"] = (JsonData)gameObject.isStatic;
            result["hideFlags"] = (JsonData)(int)gameObject.hideFlags;

            return result;
        }

        /// <summary>
        /// 读取GameObject路径
        /// </summary>
        /// <param name="gameObject">目标GameObject</param>
        /// <returns>从预制体根节点开始的路径</returns>
        public static string ReadGameObjectPath(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return string.Empty;
            }

            return GameObjectPathFinder.GetPath(gameObject);
        }

        /// <summary>
        /// 读取容器GameObject路径
        /// </summary>
        /// <param name="container">容器GameObject</param>
        /// <returns>容器路径</returns>
        public static string ReadContainerPath(GameObject container)
        {
            if (container == null)
            {
                return string.Empty;
            }

            return GameObjectPathFinder.GetPath(container);
        }
    }
}
