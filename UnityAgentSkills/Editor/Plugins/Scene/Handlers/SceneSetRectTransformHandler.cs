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
    /// scene.setRectTransform 命令处理器.
    /// </summary>
    internal static class SceneSetRectTransformHandler
    {
        /// <summary>
        /// 支持的命令类型.
        /// </summary>
        public const string CommandType = "scene.setRectTransform";

        /// <summary>
        /// 执行 RectTransform 写入命令.
        /// </summary>
        public static JsonData Execute(JsonData rawParams)
        {
            CommandParams parameters = new CommandParams(rawParams);
            SceneEditSession session = new SceneEditSession(parameters.GetString("sceneName", null));
            string objectPath = parameters.GetString("objectPath", null);
            int siblingIndex = parameters.GetInt("siblingIndex", 0);

            bool hasAnchorMin = TryReadVector2(parameters, "anchorMin", out Vector2 anchorMin);
            bool hasAnchorMax = TryReadVector2(parameters, "anchorMax", out Vector2 anchorMax);
            bool hasPivot = TryReadVector2(parameters, "pivot", out Vector2 pivot);
            bool hasAnchoredPosition = TryReadVector2(parameters, "anchoredPosition", out Vector2 anchoredPosition);
            bool hasSizeDelta = TryReadVector2(parameters, "sizeDelta", out Vector2 sizeDelta);
            bool hasOffsetMin = TryReadVector2(parameters, "offsetMin", out Vector2 offsetMin);
            bool hasOffsetMax = TryReadVector2(parameters, "offsetMax", out Vector2 offsetMax);
            if (!hasAnchorMin && !hasAnchorMax && !hasPivot && !hasAnchoredPosition && !hasSizeDelta && !hasOffsetMin && !hasOffsetMax)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": 必须至少提供一个字段: anchorMin/anchorMax/pivot/anchoredPosition/sizeDelta/offsetMin/offsetMax");
            }

            GameObject target = session.FindGameObjectOrThrow(objectPath, siblingIndex);
            RectTransform rectTransform = target.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.ComponentNotFound + ": RectTransform不存在: " + objectPath);
            }

            Vector2 mergedAnchorMin = hasAnchorMin ? anchorMin : rectTransform.anchorMin;
            Vector2 mergedAnchorMax = hasAnchorMax ? anchorMax : rectTransform.anchorMax;
            ValidateAnchorsOrThrow(mergedAnchorMin, mergedAnchorMax, objectPath);

            Vector2 oldAnchorMin = rectTransform.anchorMin;
            Vector2 oldAnchorMax = rectTransform.anchorMax;
            Vector2 oldPivot = rectTransform.pivot;
            Vector2 oldAnchoredPosition = rectTransform.anchoredPosition;
            Vector2 oldSizeDelta = rectTransform.sizeDelta;
            Vector2 oldOffsetMin = rectTransform.offsetMin;
            Vector2 oldOffsetMax = rectTransform.offsetMax;

            Undo.RecordObject(rectTransform, "Set Scene RectTransform");
            if (hasAnchorMin) rectTransform.anchorMin = anchorMin;
            if (hasAnchorMax) rectTransform.anchorMax = anchorMax;
            if (hasPivot) rectTransform.pivot = pivot;
            if (hasAnchoredPosition) rectTransform.anchoredPosition = anchoredPosition;
            if (hasSizeDelta) rectTransform.sizeDelta = sizeDelta;
            if (hasOffsetMin) rectTransform.offsetMin = offsetMin;
            if (hasOffsetMax) rectTransform.offsetMax = offsetMax;
            session.Save();

            JsonData modifiedFields = JsonResultBuilder.CreateArray();
            JsonData result = JsonResultBuilder.CreateObject();
            if (hasAnchorMin && rectTransform.anchorMin != oldAnchorMin)
            {
                modifiedFields.Add("anchorMin");
                result["anchorMin"] = BuildVector2Json(rectTransform.anchorMin);
            }
            if (hasAnchorMax && rectTransform.anchorMax != oldAnchorMax)
            {
                modifiedFields.Add("anchorMax");
                result["anchorMax"] = BuildVector2Json(rectTransform.anchorMax);
            }
            if (hasPivot && rectTransform.pivot != oldPivot)
            {
                modifiedFields.Add("pivot");
                result["pivot"] = BuildVector2Json(rectTransform.pivot);
            }
            if (hasAnchoredPosition && rectTransform.anchoredPosition != oldAnchoredPosition)
            {
                modifiedFields.Add("anchoredPosition");
                result["anchoredPosition"] = BuildVector2Json(rectTransform.anchoredPosition);
            }
            if (hasSizeDelta && rectTransform.sizeDelta != oldSizeDelta)
            {
                modifiedFields.Add("sizeDelta");
                result["sizeDelta"] = BuildVector2Json(rectTransform.sizeDelta);
            }
            if (hasOffsetMin && rectTransform.offsetMin != oldOffsetMin)
            {
                modifiedFields.Add("offsetMin");
                result["offsetMin"] = BuildVector2Json(rectTransform.offsetMin);
            }
            if (hasOffsetMax && rectTransform.offsetMax != oldOffsetMax)
            {
                modifiedFields.Add("offsetMax");
                result["offsetMax"] = BuildVector2Json(rectTransform.offsetMax);
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
        /// 校验 anchors 关系.
        /// </summary>
        private static void ValidateAnchorsOrThrow(Vector2 anchorMin, Vector2 anchorMax, string objectPath)
        {
            if (anchorMin.x > anchorMax.x || anchorMin.y > anchorMax.y)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": anchorMin must be <= anchorMax for both x and y, objectPath=" + objectPath);
            }
        }

        /// <summary>
        /// 尝试读取 Vector2 参数.
        /// </summary>
        private static bool TryReadVector2(CommandParams parameters, string key, out Vector2 value)
        {
            value = Vector2.zero;
            if (parameters == null || string.IsNullOrEmpty(key) || !parameters.Has(key))
            {
                return false;
            }

            JsonData data = parameters.GetData()[key];
            if (data == null || data.GetJsonType() == JsonType.None)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.TypeMismatch + ": " + key + " must be an object");
            }

            value = ReadVector2(data, key);
            return true;
        }

        /// <summary>
        /// 读取 Vector2.
        /// </summary>
        private static Vector2 ReadVector2(JsonData value, string originalPath)
        {
            if (value == null || !value.IsObject)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.TypeMismatch + ": 期望object类型: " + originalPath);
            }

            float x = ReadObjectFloat(value, "x", originalPath);
            float y = ReadObjectFloat(value, "y", originalPath);
            return new Vector2(x, y);
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
        /// 构建 Vector2 Json.
        /// </summary>
        private static JsonData BuildVector2Json(Vector2 value)
        {
            JsonData json = JsonResultBuilder.CreateObject();
            json["x"] = value.x;
            json["y"] = value.y;
            return json;
        }
    }
}
