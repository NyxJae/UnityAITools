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
    /// scene.setTransform 命令处理器.
    /// </summary>
    internal static class SceneSetTransformHandler
    {
        /// <summary>
        /// 支持的命令类型.
        /// </summary>
        public const string CommandType = "scene.setTransform";

        /// <summary>
        /// 执行 Transform 写入命令.
        /// </summary>
        public static JsonData Execute(JsonData rawParams)
        {
            CommandParams parameters = new CommandParams(rawParams);
            SceneEditSession session = new SceneEditSession(parameters.GetString("sceneName", null));
            string objectPath = parameters.GetString("objectPath", null);
            int siblingIndex = parameters.GetInt("siblingIndex", 0);

            bool hasLocalPosition = TryReadVector3(parameters, "localPosition", out Vector3 localPosition);
            bool hasLocalRotationEuler = TryReadVector3(parameters, "localRotationEuler", out Vector3 localRotationEuler);
            bool hasLocalScale = TryReadVector3(parameters, "localScale", out Vector3 localScale);
            if (!hasLocalPosition && !hasLocalRotationEuler && !hasLocalScale)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": 必须至少提供一个字段: localPosition/localRotationEuler/localScale");
            }

            GameObject target = session.FindGameObjectOrThrow(objectPath, siblingIndex);
            Transform transform = target.transform;
            Vector3 oldLocalPosition = transform.localPosition;
            Vector3 oldLocalRotationEuler = transform.localEulerAngles;
            Vector3 oldLocalScale = transform.localScale;

            Undo.RecordObject(transform, "Set Scene Transform");
            if (hasLocalPosition) transform.localPosition = localPosition;
            if (hasLocalRotationEuler) transform.localEulerAngles = localRotationEuler;
            if (hasLocalScale) transform.localScale = localScale;
            session.Save();

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

            result["sceneName"] = session.SceneName;
            result["objectPath"] = objectPath;
            result["siblingIndex"] = siblingIndex;
            result["instanceID"] = target.GetInstanceID();
            result["modifiedFields"] = modifiedFields;
            result["saved"] = true;
            return result;
        }

        /// <summary>
        /// 尝试读取 Vector3 参数.
        /// </summary>
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

        /// <summary>
        /// 读取 Vector3.
        /// </summary>
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

        /// <summary>
        /// 读取对象中的 float 字段.
        /// </summary>
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

        /// <summary>
        /// 构建 Vector3 Json.
        /// </summary>
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
