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
    /// scene.addComponent 命令处理器.
    /// </summary>
    internal static class SceneAddComponentHandler
    {
        /// <summary>
        /// 支持的命令类型.
        /// </summary>
        public const string CommandType = "scene.addComponent";

        /// <summary>
        /// 执行添加组件命令.
        /// </summary>
        public static JsonData Execute(JsonData rawParams)
        {
            CommandParams parameters = new CommandParams(rawParams);
            SceneEditSession session = new SceneEditSession(parameters.GetString("sceneName", null));
            string objectPath = parameters.GetString("objectPath", null);
            int siblingIndex = parameters.GetInt("siblingIndex", 0);
            string componentTypeName = parameters.GetString("componentType", null);

            GameObject target = session.FindGameObjectOrThrow(objectPath, siblingIndex);
            Type componentType = SceneEditSession.ResolveComponentTypeOrThrow(componentTypeName);

            if (Attribute.IsDefined(componentType, typeof(DisallowMultipleComponent), true) && target.GetComponent(componentType) != null)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.ComponentAlreadyExists + ": 组件已存在且不允许重复添加: " + componentType.FullName);
            }

            Component added = Undo.AddComponent(target, componentType);
            if (added == null)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.TypeMismatch + ": 添加组件失败,组件可能与目标对象不兼容: " + componentType.FullName);
            }

            if (parameters.Has("initialProperties"))
            {
                JsonData initialProperties = parameters.GetData()["initialProperties"];
                if (initialProperties != null && initialProperties.GetJsonType() != JsonType.None)
                {
                    if (!initialProperties.IsObject)
                    {
                        throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": initialProperties must be an object");
                    }

                    if (initialProperties.Count > 0)
                    {
                        Prefab.Handlers.PrefabComponentPropertyWriter.ApplyProperties(added, null, initialProperties);
                    }
                }
            }

            session.Save();

            JsonData result = JsonResultBuilder.CreateObject();
            result["sceneName"] = session.SceneName;
            result["objectPath"] = objectPath;
            result["instanceID"] = target.GetInstanceID();
            result["componentType"] = added.GetType().FullName;
            result["componentIndex"] = SceneEditUtilities.GetComponentIndex(target, added);
            result["componentInstanceID"] = added.GetInstanceID();
            result["saved"] = true;
            return result;
        }
    }
}
