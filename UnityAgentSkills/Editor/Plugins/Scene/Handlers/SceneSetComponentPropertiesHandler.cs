using System;
using System.Collections.Generic;
using UnityAgentSkills.Core;
using UnityAgentSkills.Utils;
using UnityAgentSkills.Utils.JsonBuilders;
using LitJson2_utf;
using UnityEngine;

namespace UnityAgentSkills.Plugins.Scene.Handlers
{
    /// <summary>
    /// scene.setComponentProperties 命令处理器.
    /// </summary>
    internal static class SceneSetComponentPropertiesHandler
    {
        /// <summary>
        /// 支持的命令类型.
        /// </summary>
        public const string CommandType = "scene.setComponentProperties";

        /// <summary>
        /// 执行组件属性修改命令.
        /// </summary>
        public static JsonData Execute(JsonData rawParams)
        {
            CommandParams parameters = new CommandParams(rawParams);
            SceneEditSession session = new SceneEditSession(parameters.GetString("sceneName", null));
            string objectPath = parameters.GetString("objectPath", null);
            int siblingIndex = parameters.GetInt("siblingIndex", 0);
            string componentTypeName = parameters.GetString("componentType", null);
            int componentIndex = parameters.GetInt("componentIndex", 0);

            if (!parameters.Has("properties"))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": properties is required");
            }

            JsonData properties = parameters.GetData()["properties"];
            if (properties == null || properties.GetJsonType() == JsonType.None || !properties.IsObject)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.TypeMismatch + ": properties必须是object类型");
            }

            GameObject target = session.FindGameObjectOrThrow(objectPath, siblingIndex);
            Type componentType = SceneEditSession.ResolveComponentTypeOrThrow(componentTypeName);
            Component component = session.FindComponentOrThrow(target, componentType, componentIndex);
            List<Prefab.Handlers.PrefabComponentPropertyWriter.PropertyWriteResult> modifiedProperties =
                Prefab.Handlers.PrefabComponentPropertyWriter.ApplyProperties(component, null, properties);

            session.Save();

            JsonData result = JsonResultBuilder.CreateObject();
            result["sceneName"] = session.SceneName;
            result["objectPath"] = objectPath;
            result["instanceID"] = target.GetInstanceID();
            result["componentType"] = component.GetType().FullName;
            result["componentIndex"] = componentIndex;
            result["componentInstanceID"] = component.GetInstanceID();
            result["saved"] = true;
            result["modifiedProperties"] = BuildModifiedProperties(modifiedProperties);
            return result;
        }

        /// <summary>
        /// 构建变更属性结果.
        /// </summary>
        private static JsonData BuildModifiedProperties(List<Prefab.Handlers.PrefabComponentPropertyWriter.PropertyWriteResult> modifiedProperties)
        {
            JsonData array = JsonResultBuilder.CreateArray();
            if (modifiedProperties == null)
            {
                return array;
            }

            foreach (Prefab.Handlers.PrefabComponentPropertyWriter.PropertyWriteResult property in modifiedProperties)
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
