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
    /// prefab.setRectTransform命令处理器.
    /// </summary>
    internal static class PrefabSetRectTransformHandler
    {
        /// <summary>
        /// 支持的命令类型.
        /// </summary>
        public const string CommandType = "prefab.setRectTransform";

        /// <summary>
        /// 执行RectTransform写入命令.
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

            bool hasAnchorMin = TryReadVector2(parameters, "anchorMin", out Vector2 anchorMin);
            bool hasAnchorMax = TryReadVector2(parameters, "anchorMax", out Vector2 anchorMax);
            bool hasPivot = TryReadVector2(parameters, "pivot", out Vector2 pivot);
            bool hasAnchoredPosition = TryReadVector2(parameters, "anchoredPosition", out Vector2 anchoredPosition);
            bool hasSizeDelta = TryReadVector2(parameters, "sizeDelta", out Vector2 sizeDelta);
            bool hasOffsetMin = TryReadVector2(parameters, "offsetMin", out Vector2 offsetMin);
            bool hasOffsetMax = TryReadVector2(parameters, "offsetMax", out Vector2 offsetMax);

            if (!hasAnchorMin
                && !hasAnchorMax
                && !hasPivot
                && !hasAnchoredPosition
                && !hasSizeDelta
                && !hasOffsetMin
                && !hasOffsetMax)
            {
                throw new ArgumentException(
                    UnityAgentSkillCommandErrorCodes.InvalidFields
                    + ": 必须至少提供一个字段: anchorMin/anchorMax/pivot/anchoredPosition/sizeDelta/offsetMin/offsetMax");
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
                RectTransform rectTransform = target.GetComponent<RectTransform>();
                if (rectTransform == null)
                {
                    throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.ComponentNotFound + ": RectTransform不存在: " + objectPath);
                }

                // anchor partial merge 规则: 允许仅传一侧,读取另一侧的现值后再校验.
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

                Undo.RecordObject(rectTransform, "Set Prefab RectTransform");

                if (hasAnchorMin)
                {
                    rectTransform.anchorMin = anchorMin;
                }

                if (hasAnchorMax)
                {
                    rectTransform.anchorMax = anchorMax;
                }

                if (hasPivot)
                {
                    rectTransform.pivot = pivot;
                }

                if (hasAnchoredPosition)
                {
                    rectTransform.anchoredPosition = anchoredPosition;
                }

                if (hasSizeDelta)
                {
                    rectTransform.sizeDelta = sizeDelta;
                }

                if (hasOffsetMin)
                {
                    rectTransform.offsetMin = offsetMin;
                }

                if (hasOffsetMax)
                {
                    rectTransform.offsetMax = offsetMax;
                }

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

        private static void ValidateAnchorsOrThrow(Vector2 anchorMin, Vector2 anchorMax, string objectPath)
        {
            if (anchorMin.x > anchorMax.x || anchorMin.y > anchorMax.y)
            {
                throw new ArgumentException(
                    UnityAgentSkillCommandErrorCodes.InvalidFields
                    + ": anchorMin must be <= anchorMax for both x and y"
                    + ", objectPath="
                    + objectPath);
            }
        }

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

        private static JsonData BuildVector2Json(Vector2 value)
        {
            JsonData json = JsonResultBuilder.CreateObject();
            json["x"] = value.x;
            json["y"] = value.y;
            return json;
        }
    }
}
