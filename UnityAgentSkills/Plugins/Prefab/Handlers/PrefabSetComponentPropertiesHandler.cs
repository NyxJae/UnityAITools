using System;
using System.Collections.Generic;
using UnityAgentSkills.Core;
using UnityAgentSkills.Utils;
using UnityAgentSkills.Utils.JsonBuilders;
using LitJson2_utf;
using UnityEditor;
using UnityEngine;

namespace UnityAgentSkills.Plugins.Prefab.Handlers
{
    /// <summary>
    /// prefab.setComponentProperties命令处理器.
    /// </summary>
    internal static class PrefabSetComponentPropertiesHandler
    {
        /// <summary>
        /// 支持的命令类型.
        /// </summary>
        public const string CommandType = "prefab.setComponentProperties";

        /// <summary>
        /// 执行组件属性修改命令.
        /// </summary>
        public static JsonData Execute(JsonData rawParams)
        {
            CommandParams parameters = new CommandParams(rawParams);

            string prefabPath = PrefabComponentHandlerUtils.NormalizePrefabPath(parameters.GetString("prefabPath", null));
            string objectPath = parameters.GetString("objectPath", null);
            int siblingIndex = parameters.GetInt("siblingIndex", 0);
            string componentTypeName = parameters.GetString("componentType", null);
            int componentIndex = parameters.GetInt("componentIndex", 0);

            if (!parameters.Has("properties"))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": properties is required");
            }

            JsonData properties = parameters.GetData()["properties"];
            if (properties == null || properties.GetJsonType() == JsonType.None)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.TypeMismatch + ": properties必须是object类型");
            }
            if (!properties.IsObject)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.TypeMismatch + ": properties必须是object类型");
            }

            GameObject prefabRoot = PrefabComponentHandlerUtils.LoadPrefabContentsOrThrow(prefabPath);
            try
            {
                GameObject target = PrefabComponentHandlerUtils.FindGameObjectOrThrow(prefabRoot, objectPath, siblingIndex, "objectPath");
                Type componentType = PrefabComponentHandlerUtils.ResolveComponentTypeOrThrow(componentTypeName);
                Component component = PrefabComponentHandlerUtils.FindComponentOrThrow(target, componentType, componentIndex);
                Debug.Log("[PrefabSetComponentPropertiesHandler] 开始写入组件属性, objectPath=" + objectPath + ", componentType=" + component.GetType().FullName + ", componentIndex=" + componentIndex + ", propertyCount=" + properties.Count);
                List<PrefabComponentPropertyWriter.PropertyWriteResult> modifiedProperties =
                    PrefabComponentPropertyWriter.ApplyProperties(component, prefabRoot, properties);
                Debug.Log("[PrefabSetComponentPropertiesHandler] 组件属性写入完成, modifiedCount=" + modifiedProperties.Count);


                bool saved = PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);

                JsonData result = JsonResultBuilder.CreateObject();
                result["prefabPath"] = prefabPath;
                result["objectPath"] = objectPath;
                result["instanceID"] = target.GetInstanceID();
                result["componentType"] = component.GetType().FullName;
                result["componentIndex"] = componentIndex;
                result["componentInstanceID"] = component.GetInstanceID();
                result["saved"] = saved;
                result["modifiedProperties"] = BuildModifiedProperties(modifiedProperties);
                return result;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static JsonData BuildModifiedProperties(List<PrefabComponentPropertyWriter.PropertyWriteResult> modifiedProperties)
        {
            JsonData array = JsonResultBuilder.CreateArray();
            if (modifiedProperties == null)
            {
                return array;
            }

            foreach (PrefabComponentPropertyWriter.PropertyWriteResult property in modifiedProperties)
            {
                JsonData item = JsonResultBuilder.CreateObject();
                item["name"] = property.name ?? string.Empty;
                item["oldValue"] = property.oldValue ?? new JsonData();
                item["newValue"] = property.newValue ?? new JsonData();
                array.Add(item);
            }

            return array;
        }
    }
}
