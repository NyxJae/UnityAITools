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
    /// prefab.setTransform命令处理器.
    /// </summary>
    internal static class PrefabSetTransformHandler
    {
        /// <summary>
        /// 支持的命令类型.
        /// </summary>
        public const string CommandType = "prefab.setTransform";

        /// <summary>
        /// 执行Transform写入命令(仅写Local字段).
        /// </summary>
        public static JsonData Execute(JsonData @params)
        {
            CommandParams parameters = new CommandParams(@params);

            string prefabPath = PrefabComponentHandlerUtils.NormalizePrefabPath(parameters.GetString("prefabPath", null));
            PrefabComponentHandlerUtils.ValidatePrefabPathOrThrow(prefabPath);

            string objectPath = parameters.GetString("objectPath", null);
            int siblingIndex = parameters.GetInt("siblingIndex", 0);

            if (siblingIndex < 0)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": siblingIndex must be >= 0");
            }

            bool hasLocalPosition = TryReadVector3(parameters, "localPosition", out Vector3 localPosition);
            bool hasLocalRotationEuler = TryReadVector3(parameters, "localRotationEuler", out Vector3 localRotationEuler);
            bool hasLocalScale = TryReadVector3(parameters, "localScale", out Vector3 localScale);

            if (!hasLocalPosition && !hasLocalRotationEuler && !hasLocalScale)
            {
                throw new ArgumentException(
                    UnityAgentSkillCommandErrorCodes.InvalidFields
                    + ": 必须至少提供一个字段: localPosition/localRotationEuler/localScale");
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
                Transform transform = target.transform;

                Vector3 oldLocalPosition = transform.localPosition;
                Vector3 oldLocalRotationEuler = transform.localEulerAngles;
                Vector3 oldLocalScale = transform.localScale;

                Undo.RecordObject(transform, "Set Prefab Transform");

                if (hasLocalPosition)
                {
                    transform.localPosition = localPosition;
                }

                if (hasLocalRotationEuler)
                {
                    transform.localEulerAngles = localRotationEuler;
                }

                if (hasLocalScale)
                {
                    transform.localScale = localScale;
                }

                JsonData modifiedFields = JsonResultBuilder.CreateArray();
                JsonData result = JsonResultBuilder.CreateObject();

                if (hasLocalPosition && transform.localPosition != oldLocalPosition)
                {
                    modifiedFields.Add("localPosition");
                    result["localPosition"] = BuildVector3Json(transform.localPosition);
                }

                if (hasLocalRotationEuler && transform.localEulerAngles != oldLocalRotationEuler)
                {
                    modifiedFields.Add("localRotationEuler");
                    result["localRotationEuler"] = BuildVector3Json(transform.localEulerAngles);
                }

                if (hasLocalScale && transform.localScale != oldLocalScale)
                {
                    modifiedFields.Add("localScale");
                    result["localScale"] = BuildVector3Json(transform.localScale);
                }

                bool saved = PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);

                result["prefabPath"] = prefabPath;
                result["objectPath"] = objectPath;
                result["siblingIndex"] = siblingIndex;
                result["instanceID"] = target.GetInstanceID();
                result["modifiedFields"] = modifiedFields;
                result["saved"] = saved;

                return result;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static bool TryReadVector3(CommandParams parameters, string key, out Vector3 value)
        {
            value = Vector3.zero;
            if (parameters == null || string.IsNullOrEmpty(key) || !parameters.Has(key))
            {
                return false;
            }

            JsonData data = parameters.GetData()[key];
            if (data == null || data.GetJsonType() == JsonType.None)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.TypeMismatch + ": " + key + " must be an object");
            }

            value = ReadVector3(data, key);
            return true;
        }

        private static Vector3 ReadVector3(JsonData value, string originalPath)
        {
            if (value == null || !value.IsObject)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.TypeMismatch + ": 期望object类型: " + originalPath);
            }

            float x = ReadObjectFloat(value, "x", originalPath);
            float y = ReadObjectFloat(value, "y", originalPath);
            float z = ReadObjectFloat(value, "z", originalPath);
            return new Vector3(x, y, z);
        }

        private static float ReadObjectFloat(JsonData parent, string key, string originalPath)
        {
            if (parent == null || !parent.IsObject || string.IsNullOrEmpty(key))
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.TypeMismatch + ": 期望object类型: " + originalPath);
            }

            if (!parent.ContainsKey(key))
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.TypeMismatch + ": 缺少字段" + key + ": " + originalPath);
            }

            JsonData v = parent[key];
            if (v.IsDouble) return (float)(double)v;
            if (v.IsInt) return (int)v;
            if (v.IsLong) return (long)v;
            if (v.IsString && float.TryParse(v.ToString(), out float parsed)) return parsed;

            throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.TypeMismatch + ": 期望float类型: " + originalPath + "." + key);
        }

        private static JsonData BuildVector3Json(Vector3 value)
        {
            JsonData json = JsonResultBuilder.CreateObject();
            json["x"] = value.x;
            json["y"] = value.y;
            json["z"] = value.z;
            return json;
        }
    }
}
