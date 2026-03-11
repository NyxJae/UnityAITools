using System;
using UnityAgentSkills.Core;
using UnityAgentSkills.Utils;
using UnityAgentSkills.Utils.JsonBuilders;
using LitJson2_utf;
using UnityEditor;
using UnityEngine;

namespace UnityAgentSkills.Plugins.Prefab.Handlers
{
    /// <summary>
    /// prefab.renameGameObject命令处理器.
    /// </summary>
    internal static class PrefabRenameGameObjectHandler
    {
        /// <summary>
        /// 支持的命令类型.
        /// </summary>
        public const string CommandType = "prefab.renameGameObject";

        /// <summary>
        /// 执行GameObject改名命令.
        /// </summary>
        public static JsonData Execute(JsonData @params)
        {
            CommandParams parameters = new CommandParams(@params);

            string prefabPath = PrefabComponentHandlerUtils.NormalizePrefabPath(parameters.GetString("prefabPath", null));
            PrefabComponentHandlerUtils.ValidatePrefabPathOrThrow(prefabPath);

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

            GameObject prefabRoot = PrefabComponentHandlerUtils.LoadPrefabContentsOrThrow(prefabPath);
            try
            {
                if (string.Equals(objectPath, prefabRoot.name, StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        UnityAgentSkillCommandErrorCodes.InvalidFields
                        + ": 不允许编辑预制体根节点: objectPath="
                        + objectPath
                        + ",rootPath="
                        + prefabRoot.name
                        + ".请改为编辑其子节点,或先用 prefab.queryHierarchy 确认根节点路径.");
                }

                GameObject target = PrefabComponentHandlerUtils.FindGameObjectOrThrow(prefabRoot, objectPath, siblingIndex, "objectPath");

                string oldName = target.name;
                string oldPath = GameObjectPathFinder.GetPath(target);

                Undo.RecordObject(target, "Rename Prefab GameObject");
                target.name = newName;

                string newPath = GameObjectPathFinder.GetPath(target);

                bool saved = PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);

                JsonData result = JsonResultBuilder.CreateObject();
                result["prefabPath"] = prefabPath;
                result["objectPath"] = objectPath;
                result["siblingIndex"] = siblingIndex;
                result["instanceID"] = target.GetInstanceID();
                result["oldName"] = oldName;
                result["newName"] = newName;
                result["oldPath"] = oldPath;
                result["newPath"] = newPath;
                result["saved"] = saved;
                return result;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
    }
}
