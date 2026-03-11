using System;
using System.Collections.Generic;
using LitJson2_utf;
using UnityAgentSkills.Core;
using UnityAgentSkills.Utils;
using UnityAgentSkills.Utils.JsonBuilders;
using UnityEngine;

namespace UnityAgentSkills.Plugins.Scene.Handlers
{
    /// <summary>
    /// scene.setGameObjectProperties命令处理器.
    /// </summary>
    internal static class SceneSetGameObjectPropertiesHandler
    {
        /// <summary>
        /// 支持的命令类型.
        /// </summary>
        public const string CommandType = "scene.setGameObjectProperties";

        /// <summary>
        /// 执行场景内GameObject属性修改命令.
        /// </summary>
        public static JsonData Execute(JsonData rawParams)
        {
            CommandParams parameters = new CommandParams(rawParams);
            string sceneName = parameters.GetString("sceneName", null);
            string objectPath = parameters.GetString("objectPath", null);
            int siblingIndex = parameters.GetInt("siblingIndex", 0);

            if (!parameters.Has("properties"))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": properties is required");
            }

            JsonData properties = parameters.GetData()["properties"];
            if (properties == null || !properties.IsObject || properties.Count == 0)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": properties对象不能为空, 至少需要指定一个要修改的属性");
            }

            SceneEditSession session = new SceneEditSession(sceneName);
            GameObject target = session.FindGameObjectOrThrow(objectPath, siblingIndex, "objectPath");
            List<GameObjectPropertyModifier.PropertyChange> modifiedProperties;
            JsonData currentProperties = GameObjectPropertyModifier.ModifyProperties(target, properties, out modifiedProperties);
            session.Save();

            JsonData result = JsonResultBuilder.CreateObject();
            result["sceneName"] = session.SceneName;
            result["objectPath"] = objectPath;
            result["siblingIndex"] = siblingIndex;
            result["instanceID"] = target.GetInstanceID();
            result["currentProperties"] = currentProperties;
            result["saved"] = true;

            JsonData modifiedPropsArray = JsonResultBuilder.CreateArray();
            foreach (GameObjectPropertyModifier.PropertyChange change in modifiedProperties)
            {
                JsonData changeObj = JsonResultBuilder.CreateObject();
                changeObj["name"] = change.name;
                changeObj["oldValue"] = SceneEditCommon.ConvertJsonValue(change.oldValue);
                changeObj["newValue"] = SceneEditCommon.ConvertJsonValue(change.newValue);
                modifiedPropsArray.Add(changeObj);
            }

            result["modifiedProperties"] = modifiedPropsArray;
            return result;
        }
    }
}
