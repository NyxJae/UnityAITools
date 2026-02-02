using System;
using UnityEngine;
using UnityEditor;
using K3Engine.Component.Interfaces;
using LitJson2_utf;
using UnityAgentSkills.Utils;
using UnityAgentSkills.Utils.JsonBuilders;
using UnityAgentSkills.Plugins.K3Prefab.Models;

namespace UnityAgentSkills.Plugins.K3Prefab.Utils
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

            // 读取K3Property.ID - 直接使用组件接口，避免触发序列化系统的 pptr 读取错误
            uint componentId = 0;
            try
            {
                // 优先使用 IK3Component 接口的 property 属性（不会触发序列化错误）
                if (component.property != null)
                {
                    componentId = component.property.ID;
                }
            }
            catch (System.Exception)
            {
                // 如果接口访问失败，回退到序列化读取（可能会触发 pptr 错误）
                if (component is Component unityComp)
                {
                    try
                    {
                        var so = new SerializedObject(unityComp);
                        var idProp = so.FindProperty("m_property");
                        if (idProp != null)
                        {
                            // 这里可能会触发 pptr 错误，但我们已经在最外层 try-catch 中
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
                    catch (System.Exception ex)
                    {
                        // 序列化读取也失败，使用默认值
                        string path = match.gameObject != null ? GameObjectPathFinder.GetPath(match.gameObject) : "Unknown";
                        Debug.LogWarning($"[K3ComponentPropertyReader] 组件 {path} 的 K3ID 无法读取: {ex.Message}。将使用默认 ID 值。");
                    }
                }
            }

            // 读取IK3Component接口属性（添加异常保护避免字段未赋值时失败）
            result["ID"] = (JsonData)componentId;
            
            // 使用try-catch保护属性读取,避免因字段未赋值导致整个组件数据丢失
            try { result["alpha"] = (JsonData)component.alpha; } 
            catch (System.Exception) { result["alpha"] = (JsonData)1.0f; }
            
            try { result["x"] = (JsonData)component.x; } 
            catch (System.Exception) { result["x"] = (JsonData)0f; }
            
            try { result["y"] = (JsonData)component.y; } 
            catch (System.Exception) { result["y"] = (JsonData)0f; }
            
            try { result["IsVisible"] = (JsonData)component.IsVisible; } 
            catch (System.Exception) { result["IsVisible"] = (JsonData)true; }
            
            result["atlasName"] = component.atlasName ?? string.Empty;
            result["picName"] = component.picName ?? string.Empty;
            result["ComponentName"] = component.ComponentName ?? string.Empty;

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
