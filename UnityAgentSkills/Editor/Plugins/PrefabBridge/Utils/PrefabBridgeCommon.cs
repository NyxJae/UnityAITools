using System;
using System.Collections.Generic;
using LitJson2_utf;
using UnityAgentSkills.Core;
using UnityAgentSkills.Plugins.Prefab.Handlers;
using UnityAgentSkills.Plugins.Scene.Handlers;
using UnityAgentSkills.Utils;
using UnityAgentSkills.Utils.JsonBuilders;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityAgentSkills.Plugins.PrefabBridge.Utils
{
    /// <summary>
    /// PrefabBridge 共享工具.
    /// </summary>
    internal static class PrefabBridgeCommon
    {
        /// <summary>
        /// prefab instance 上下文.
        /// </summary>
        internal sealed class PrefabInstanceContext
        {
            /// <summary>
            /// 目标场景.
            /// </summary>
            public UnityEngine.SceneManagement.Scene Scene;

            /// <summary>
            /// 目标对象.
            /// </summary>
            public GameObject Target;

            /// <summary>
            /// 最近 prefab instance root.
            /// </summary>
            public GameObject InstanceRoot;

            /// <summary>
            /// 直接来源对象.
            /// </summary>
            public UnityEngine.Object SourceObject;

            /// <summary>
            /// 来源 prefab 路径.
            /// </summary>
            public string SourcePrefabPath;

            /// <summary>
            /// variant 的 base prefab 路径.
            /// </summary>
            public string VariantBasePrefabPath;

            /// <summary>
            /// 是否为 prefab instance.
            /// </summary>
            public bool IsPrefabInstance;

            /// <summary>
            /// regular,variant,nested,notPrefab.
            /// </summary>
            public string InstanceKind;
        }

        /// <summary>
        /// 校验编辑模式.
        /// </summary>
        public static void EnsureEditModeOrThrow()
        {
            SceneEditHandlerUtils.EnsureEditModeOrThrow();
        }

        /// <summary>
        /// 规范化并校验 prefab 路径.
        /// </summary>
        public static string NormalizeAndValidatePrefabPathOrThrow(string prefabPath)
        {
            string normalizedPrefabPath = PrefabComponentHandlerUtils.NormalizePrefabPath(prefabPath);
            PrefabComponentHandlerUtils.ValidatePrefabPathOrThrow(normalizedPrefabPath);
            return normalizedPrefabPath;
        }

        /// <summary>
        /// 查找场景对象.
        /// </summary>
        public static GameObject FindSceneObjectOrThrow(string sceneName, string objectPath, int siblingIndex, string pathFieldName = "objectPath")
        {
            UnityEngine.SceneManagement.Scene scene = SceneEditHandlerUtils.ResolveLoadedSceneOrThrow(sceneName);
            return SceneEditHandlerUtils.FindGameObjectOrThrow(scene, objectPath, siblingIndex, pathFieldName);
        }

        /// <summary>
        /// 解析 prefab instance 上下文.
        /// </summary>
        public static PrefabInstanceContext ResolvePrefabInstanceContextOrThrow(string sceneName, string objectPath, int siblingIndex)
        {
            EnsureEditModeOrThrow();
            UnityEngine.SceneManagement.Scene scene = SceneEditHandlerUtils.ResolveLoadedSceneOrThrow(sceneName);
            GameObject target = SceneEditHandlerUtils.FindGameObjectOrThrow(scene, objectPath, siblingIndex, "objectPath");
            GameObject nearestRoot = PrefabUtility.GetNearestPrefabInstanceRoot(target);

            var context = new PrefabInstanceContext
            {
                Scene = scene,
                Target = target,
                InstanceRoot = nearestRoot,
                IsPrefabInstance = nearestRoot != null
            };

            if (!context.IsPrefabInstance)
            {
                context.InstanceKind = "notPrefab";
                context.SourcePrefabPath = string.Empty;
                context.VariantBasePrefabPath = string.Empty;
                return context;
            }

            UnityEngine.Object source = PrefabUtility.GetCorrespondingObjectFromSource(nearestRoot);
            context.SourceObject = source;
            context.SourcePrefabPath = source != null ? AssetDatabase.GetAssetPath(source) ?? string.Empty : string.Empty;
            context.InstanceKind = DetermineInstanceKind(target, nearestRoot, source);
            context.VariantBasePrefabPath = GetVariantBasePrefabPath(source);
            return context;
        }

        /// <summary>
        /// 保存场景.
        /// </summary>
        public static bool SaveScene(UnityEngine.SceneManagement.Scene scene)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            return EditorSceneManager.SaveScene(scene);
        }

        /// <summary>
        /// 填充对象定位结果.
        /// </summary>
        public static void FillCommonObjectIdentity(JsonData result, string sceneName, GameObject target)
        {
            result["sceneName"] = sceneName;
            result["objectPath"] = GameObjectPathFinder.GetPath(target);
            result["siblingIndex"] = GameObjectPathFinder.GetSameNameSiblingIndex(target);
            result["instanceID"] = target.GetInstanceID();
        }

        /// <summary>
        /// 构建 Vector3 Json.
        /// </summary>
        public static JsonData BuildVector3Json(Vector3 value)
        {
            return SceneEditHandlerUtils.BuildVector3Json(value);
        }

        /// <summary>
        /// 构建 Euler Json.
        /// </summary>
        public static JsonData BuildEulerJson(Quaternion value)
        {
            return BuildVector3Json(value.eulerAngles);
        }

        /// <summary>
        /// 构建 RectTransform Json.
        /// </summary>
        public static JsonData BuildRectTransformJson(RectTransform rectTransform)
        {
            JsonData json = JsonResultBuilder.CreateObject();
            json["anchorMin"] = SceneEditHandlerUtils.BuildVector2Json(rectTransform.anchorMin);
            json["anchorMax"] = SceneEditHandlerUtils.BuildVector2Json(rectTransform.anchorMax);
            json["anchoredPosition"] = SceneEditHandlerUtils.BuildVector2Json(rectTransform.anchoredPosition);
            json["sizeDelta"] = SceneEditHandlerUtils.BuildVector2Json(rectTransform.sizeDelta);
            return json;
        }

        /// <summary>
        /// 构建关系摘要.
        /// </summary>
        public static string BuildRelationshipSummary(PrefabInstanceContext context)
        {
            if (context == null || !context.IsPrefabInstance)
            {
                return "该对象不是 prefab instance,而是普通场景对象.";
            }

            if (context.InstanceKind == "variant")
            {
                return string.IsNullOrEmpty(context.VariantBasePrefabPath)
                    ? "该对象来源于一个 prefab variant 实例."
                    : "该对象来源于 prefab variant,其基础 prefab 为: " + context.VariantBasePrefabPath;
            }

            if (context.InstanceKind == "nested")
            {
                return "该对象位于一个 nested prefab 实例内部,最近的实例根节点为: " + GameObjectPathFinder.GetPath(context.InstanceRoot);
            }

            return "该对象来源于 prefab: " + context.SourcePrefabPath;
        }

        /// <summary>
        /// 生成 overrideId.
        /// </summary>
        public static string BuildOverrideId(string overrideKind, string targetObjectPath, string componentType = null, int? componentIndex = null, string propertyPath = null)
        {
            return string.Join("|", new[]
            {
                overrideKind ?? string.Empty,
                targetObjectPath ?? string.Empty,
                componentType ?? string.Empty,
                componentIndex.HasValue ? componentIndex.Value.ToString() : string.Empty,
                propertyPath ?? string.Empty
            });
        }

        /// <summary>
        /// 读取 overrideIds.
        /// </summary>
        public static HashSet<string> ReadOverrideIdsOrThrow(JsonData rawParams, string fieldName)
        {
            if (rawParams == null || !rawParams.IsObject || !rawParams.ContainsKey(fieldName))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": " + fieldName + " is required");
            }

            JsonData rawIds = rawParams[fieldName];
            if (rawIds == null || !rawIds.IsArray || rawIds.Count == 0)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": " + fieldName + " must be a non-empty array");
            }

            var result = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < rawIds.Count; i++)
            {
                string id = rawIds[i] == null ? null : rawIds[i].ToString();
                if (string.IsNullOrWhiteSpace(id))
                {
                    throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": " + fieldName + " contains empty item");
                }

                result.Add(id.Trim());
            }

            return result;
        }

        private static string DetermineInstanceKind(GameObject target, GameObject nearestRoot, UnityEngine.Object source)
        {
            if (nearestRoot == null || source == null)
            {
                return "notPrefab";
            }

            PrefabAssetType assetType = PrefabUtility.GetPrefabAssetType(nearestRoot);
            if (assetType == PrefabAssetType.Variant)
            {
                return "variant";
            }

            return target != nearestRoot ? "nested" : "regular";
        }

        private static string GetVariantBasePrefabPath(UnityEngine.Object source)
        {
            if (source == null)
            {
                return string.Empty;
            }

            GameObject sourceGameObject = source as GameObject;
            if (sourceGameObject == null)
            {
                return string.Empty;
            }

            GameObject baseObject = PrefabUtility.GetCorrespondingObjectFromSource(sourceGameObject);
            if (baseObject == null || baseObject == sourceGameObject)
            {
                return string.Empty;
            }

            return AssetDatabase.GetAssetPath(baseObject) ?? string.Empty;
        }
    }
}
