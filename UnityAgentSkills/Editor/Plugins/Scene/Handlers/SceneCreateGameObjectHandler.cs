using System;
using System.Collections.Generic;
using LitJson2_utf;
using UnityAgentSkills.Core;
using UnityAgentSkills.Utils;
using UnityAgentSkills.Utils.JsonBuilders;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityAgentSkills.Plugins.Scene.Handlers
{
    /// <summary>
    /// scene.createGameObject命令处理器.
    /// </summary>
    internal static class SceneCreateGameObjectHandler
    {
        /// <summary>
        /// 支持的命令类型.
        /// </summary>
        public const string CommandType = "scene.createGameObject";

        /// <summary>
        /// 执行创建GameObject命令.
        /// </summary>
        public static JsonData Execute(JsonData rawParams)
        {
            CommandParams parameters = new CommandParams(rawParams);
            string sceneName = parameters.GetString("sceneName", null);
            string parentPath = parameters.GetString("parentPath", null);
            int parentSiblingIndex = parameters.GetInt("parentSiblingIndex", 0);
            string name = parameters.GetString("name", null);
            int insertSiblingIndex = parameters.GetInt("insertSiblingIndex", -1);

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": name is required");
            }
            if (parentSiblingIndex < 0)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": parentSiblingIndex must be >= 0");
            }
            if (insertSiblingIndex < -1)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": insertSiblingIndex must be >= -1");
            }

            SceneEditSession session = new SceneEditSession(sceneName);
            GameObject parent = ResolveParentOrThrow(session, parentPath, parentSiblingIndex);
            GameObject createdObject = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(createdObject, "Create Scene GameObject");
            if (parent != null)
            {
                createdObject.transform.SetParent(parent.transform, false);
            }
            else
            {
                SceneManager.MoveGameObjectToScene(createdObject, session.TargetScene);
            }

            int appliedInsertSiblingIndex = SceneEditCommon.ApplySiblingIndex(createdObject.transform, parent != null ? parent.transform : null, insertSiblingIndex);
            ApplyInitialPropertiesIfNeeded(parameters, createdObject);
            string createdObjectPath = GameObjectPathFinder.GetPath(createdObject);
            int createdSiblingIndex = GameObjectPathFinder.GetSameNameSiblingIndex(createdObject);
            int instanceID = createdObject.GetInstanceID();
            session.Save();

            JsonData result = JsonResultBuilder.CreateObject();
            result["sceneName"] = session.SceneName;
            result["createdObjectPath"] = createdObjectPath;
            result["createdSiblingIndex"] = createdSiblingIndex;
            result["insertSiblingIndexApplied"] = appliedInsertSiblingIndex;
            result["instanceID"] = instanceID;
            result["saved"] = true;
            return result;
        }

        private static GameObject ResolveParentOrThrow(SceneEditSession session, string parentPath, int parentSiblingIndex)
        {
            if (string.IsNullOrWhiteSpace(parentPath))
            {
                if (parentSiblingIndex != 0)
                {
                    throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": parentSiblingIndex must be 0 when parentPath is omitted");
                }

                return null;
            }

            return session.FindGameObjectOrThrow(parentPath, parentSiblingIndex, "parentPath");
        }

        private static void ApplyInitialPropertiesIfNeeded(CommandParams parameters, GameObject target)
        {
            if (!parameters.Has("initialProperties"))
            {
                return;
            }

            JsonData initialProperties = parameters.GetData()["initialProperties"];
            if (initialProperties == null || initialProperties.GetJsonType() == JsonType.None)
            {
                return;
            }

            if (!initialProperties.IsObject)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": initialProperties must be an object");
            }

            if (initialProperties.Count == 0)
            {
                return;
            }

            try
            {
                List<GameObjectPropertyModifier.PropertyChange> changes;
                GameObjectPropertyModifier.ModifyProperties(target, initialProperties, out changes);
            }
            catch (Exception ex)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": initialProperties invalid, " + ex.Message);
            }
        }
    }
}
