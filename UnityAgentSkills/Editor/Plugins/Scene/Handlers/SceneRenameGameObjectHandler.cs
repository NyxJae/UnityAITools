using System;
using LitJson2_utf;
using UnityAgentSkills.Core;
using UnityAgentSkills.Utils;
using UnityAgentSkills.Utils.JsonBuilders;
using UnityEditor;
using UnityEngine;

namespace UnityAgentSkills.Plugins.Scene.Handlers
{
    /// <summary>
    /// scene.renameGameObject命令处理器.
    /// </summary>
    internal static class SceneRenameGameObjectHandler
    {
        /// <summary>
        /// 支持的命令类型.
        /// </summary>
        public const string CommandType = "scene.renameGameObject";

        /// <summary>
        /// 执行GameObject改名命令.
        /// </summary>
        public static JsonData Execute(JsonData rawParams)
        {
            CommandParams parameters = new CommandParams(rawParams);
            string sceneName = parameters.GetString("sceneName", null);
            string objectPath = parameters.GetString("objectPath", null);
            int siblingIndex = parameters.GetInt("siblingIndex", 0);
            string newName = parameters.GetString("newName", null);

            if (siblingIndex < 0)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": siblingIndex must be >= 0");
            }

            if (string.IsNullOrWhiteSpace(newName))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": newName is required");
            }

            SceneEditSession session = new SceneEditSession(sceneName);
            GameObject target = session.FindGameObjectOrThrow(objectPath, siblingIndex, "objectPath");
            string oldName = target.name;
            string oldPath = GameObjectPathFinder.GetPath(target);
            Undo.RecordObject(target, "Rename Scene GameObject");
            target.name = newName;
            string newPath = GameObjectPathFinder.GetPath(target);
            session.Save();

            JsonData result = JsonResultBuilder.CreateObject();
            result["sceneName"] = session.SceneName;
            result["objectPath"] = objectPath;
            result["siblingIndex"] = siblingIndex;
            result["instanceID"] = target.GetInstanceID();
            result["oldName"] = oldName;
            result["newName"] = newName;
            result["oldPath"] = oldPath;
            result["newPath"] = newPath;
            result["saved"] = true;
            return result;
        }
    }
}
