using LitJson2_utf;
using UnityAgentSkills.Core;
using UnityAgentSkills.Utils;
using UnityAgentSkills.Utils.JsonBuilders;
using UnityEditor;
using UnityEngine;

namespace UnityAgentSkills.Plugins.Scene.Handlers
{
    /// <summary>
    /// scene.deleteGameObject命令处理器.
    /// </summary>
    internal static class SceneDeleteGameObjectHandler
    {
        /// <summary>
        /// 支持的命令类型.
        /// </summary>
        public const string CommandType = "scene.deleteGameObject";

        /// <summary>
        /// 执行场景内GameObject删除命令.
        /// </summary>
        public static JsonData Execute(JsonData rawParams)
        {
            CommandParams parameters = new CommandParams(rawParams);
            string sceneName = parameters.GetString("sceneName", null);
            string objectPath = parameters.GetString("objectPath", null);
            int siblingIndex = parameters.GetInt("siblingIndex", 0);

            SceneEditSession session = new SceneEditSession(sceneName);
            GameObject target = session.FindGameObjectOrThrow(objectPath, siblingIndex, "objectPath");
            string deletedObjectPath = GameObjectPathFinder.GetPath(target);
            int deletedInstanceID = target.GetInstanceID();
            int totalDeletedCount = SceneEditCommon.CountGameObjects(target, true);
            Undo.DestroyObjectImmediate(target);
            session.Save();

            JsonData result = JsonResultBuilder.CreateObject();
            result["sceneName"] = session.SceneName;
            result["deletedObjectPath"] = deletedObjectPath;
            result["deletedInstanceID"] = deletedInstanceID;
            result["deletedObjectCount"] = 1;
            result["totalDeletedCount"] = totalDeletedCount;
            result["saved"] = true;
            return result;
        }
    }
}
