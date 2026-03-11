using System;
using LitJson2_utf;
using UnityAgentSkills.Core;
using UnityAgentSkills.Utils.JsonBuilders;
using UnityEditor;
using UnityEngine;

namespace UnityAgentSkills.Plugins.Scene.Handlers
{
    /// <summary>
    /// scene.setSiblingIndex命令处理器.
    /// </summary>
    internal static class SceneSetSiblingIndexHandler
    {
        /// <summary>
        /// 支持的命令类型.
        /// </summary>
        public const string CommandType = "scene.setSiblingIndex";

        /// <summary>
        /// 执行GameObject排序命令.
        /// </summary>
        public static JsonData Execute(JsonData rawParams)
        {
            CommandParams parameters = new CommandParams(rawParams);
            string sceneName = parameters.GetString("sceneName", null);
            string objectPath = parameters.GetString("objectPath", null);
            int siblingIndex = parameters.GetInt("siblingIndex", 0);
            int newSiblingIndexRequested = parameters.GetInt("newSiblingIndex");

            if (siblingIndex < 0)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": siblingIndex must be >= 0");
            }
            if (newSiblingIndexRequested < 0)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": newSiblingIndex must be >= 0");
            }

            SceneEditSession session = new SceneEditSession(sceneName);
            GameObject target = session.FindGameObjectOrThrow(objectPath, siblingIndex, "objectPath");
            Transform transform = target.transform;
            int oldSiblingIndex = transform.GetSiblingIndex();
            Transform parent = transform.parent;
            int maxSiblingIndex = parent == null ? session.TargetScene.rootCount - 1 : parent.childCount - 1;
            int newSiblingIndexApplied = newSiblingIndexRequested > maxSiblingIndex ? maxSiblingIndex : newSiblingIndexRequested;
            Undo.RecordObject(transform, "Set Scene Sibling Index");
            transform.SetSiblingIndex(newSiblingIndexApplied);
            session.Save();

            JsonData result = JsonResultBuilder.CreateObject();
            result["sceneName"] = session.SceneName;
            result["objectPath"] = objectPath;
            result["siblingIndex"] = siblingIndex;
            result["instanceID"] = target.GetInstanceID();
            result["oldSiblingIndex"] = oldSiblingIndex;
            result["newSiblingIndexRequested"] = newSiblingIndexRequested;
            result["newSiblingIndexApplied"] = newSiblingIndexApplied;
            result["saved"] = true;
            return result;
        }
    }
}
