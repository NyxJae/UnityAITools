using System;
using UnityAgentSkills.Core;
using UnityAgentSkills.Utils;
using UnityAgentSkills.Utils.JsonBuilders;
using LitJson2_utf;
using UnityEditor;
using UnityEngine;

namespace UnityAgentSkills.Plugins.Scene.Handlers
{
    /// <summary>
    /// scene.deleteComponent 命令处理器.
    /// </summary>
    internal static class SceneDeleteComponentHandler
    {
        /// <summary>
        /// 支持的命令类型.
        /// </summary>
        public const string CommandType = "scene.deleteComponent";

        /// <summary>
        /// 执行删除组件命令.
        /// </summary>
        public static JsonData Execute(JsonData rawParams)
        {
            CommandParams parameters = new CommandParams(rawParams);
            SceneEditSession session = new SceneEditSession(parameters.GetString("sceneName", null));
            string objectPath = parameters.GetString("objectPath", null);
            int siblingIndex = parameters.GetInt("siblingIndex", 0);
            string componentTypeName = parameters.GetString("componentType", null);
            int componentIndex = parameters.GetInt("componentIndex", 0);

            GameObject target = session.FindGameObjectOrThrow(objectPath, siblingIndex);
            Type componentType = SceneEditSession.ResolveComponentTypeOrThrow(componentTypeName);
            Component component = session.FindComponentOrThrow(target, componentType, componentIndex);

            if (component is Transform)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.CannotDeleteRequiredComponent + ": 不能删除Transform/RectTransform组件");
            }

            int deletedComponentInstanceID = component.GetInstanceID();
            string deletedComponentType = component.GetType().FullName;
            Undo.DestroyObjectImmediate(component);
            session.Save();

            JsonData result = JsonResultBuilder.CreateObject();
            result["sceneName"] = session.SceneName;
            result["objectPath"] = objectPath;
            result["instanceID"] = target.GetInstanceID();
            result["deletedComponentType"] = deletedComponentType;
            result["deletedComponentIndex"] = componentIndex;
            result["deletedComponentInstanceID"] = deletedComponentInstanceID;
            result["saved"] = true;
            return result;
        }
    }
}
