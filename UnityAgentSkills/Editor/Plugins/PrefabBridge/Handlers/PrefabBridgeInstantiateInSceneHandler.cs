using System;
using LitJson2_utf;
using UnityAgentSkills.Core;
using UnityAgentSkills.Plugins.PrefabBridge.Utils;
using UnityAgentSkills.Utils;
using UnityAgentSkills.Utils.JsonBuilders;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityAgentSkills.Plugins.PrefabBridge.Handlers
{
    /// <summary>
    /// prefabBridge.instantiateInScene 命令处理器.
    /// </summary>
    internal static class PrefabBridgeInstantiateInSceneHandler
    {
        /// <summary>
        /// 支持的命令类型.
        /// </summary>
        public const string CommandType = "prefabBridge.instantiateInScene";

        /// <summary>
        /// 执行实例化命令.
        /// </summary>
        public static JsonData Execute(JsonData rawParams)
        {
            CommandParams parameters = new CommandParams(rawParams);
            string sceneName = parameters.GetString("sceneName", null);
            string prefabPath = PrefabBridgeCommon.NormalizeAndValidatePrefabPathOrThrow(parameters.GetString("prefabPath", null));
            string parentPath = parameters.GetString("parentPath", null);
            int parentSiblingIndex = parameters.GetInt("parentSiblingIndex", 0);
            int insertSiblingIndex = parameters.GetInt("insertSiblingIndex", -1);
            string name = parameters.GetString("name", null);

            if (parentSiblingIndex < 0)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": parentSiblingIndex must be >= 0");
            }

            if (insertSiblingIndex < -1)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": insertSiblingIndex must be >= -1");
            }

            PrefabBridgeCommon.EnsureEditModeOrThrow();
            UnityEngine.SceneManagement.Scene scene = UnityAgentSkills.Plugins.Scene.Handlers.SceneEditHandlerUtils.ResolveLoadedSceneOrThrow(sceneName);
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabAsset == null)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.PrefabNotFound + ": 预制体文件不存在: " + prefabPath);
            }

            GameObject parent = null;
            if (!string.IsNullOrWhiteSpace(parentPath))
            {
                parent = PrefabBridgeCommon.FindSceneObjectOrThrow(sceneName, parentPath, parentSiblingIndex, "parentPath");
            }
            else if (parentSiblingIndex != 0)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": parentSiblingIndex must be 0 when parentPath is omitted");
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(prefabAsset, scene) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.RuntimeError + ": 无法将 prefab 实例化到场景: " + prefabPath);
            }

            Undo.RegisterCreatedObjectUndo(instance, "Instantiate Prefab In Scene");
            if (parent != null)
            {
                instance.transform.SetParent(parent.transform, false);
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                instance.name = name;
            }

            ApplyTransformIfNeeded(rawParams, instance.transform);
            ApplyRectTransformIfNeeded(rawParams, instance);

            if (insertSiblingIndex >= 0)
            {
                instance.transform.SetSiblingIndex(insertSiblingIndex);
            }

            bool saved = PrefabBridgeCommon.SaveScene(scene);
            JsonData result = JsonResultBuilder.CreateObject();
            result["sceneName"] = sceneName;
            result["prefabPath"] = prefabPath;
            result["instancePath"] = GameObjectPathFinder.GetPath(instance);
            result["siblingIndex"] = GameObjectPathFinder.GetSameNameSiblingIndex(instance);
            result["parentPath"] = parent != null ? GameObjectPathFinder.GetPath(parent) : string.Empty;
            result["isRootInScene"] = parent == null;
            result["isPrefabInstance"] = true;
            result["saved"] = saved;
            return result;
        }

        private static void ApplyTransformIfNeeded(JsonData rawParams, Transform transform)
        {
            if (rawParams == null || !rawParams.IsObject)
            {
                return;
            }

            if (rawParams.ContainsKey("localPosition"))
            {
                transform.localPosition = ReadVector3OrThrow(rawParams["localPosition"], "localPosition");
            }

            if (rawParams.ContainsKey("localRotation"))
            {
                Vector3 euler = ReadVector3OrThrow(rawParams["localRotation"], "localRotation");
                transform.localRotation = Quaternion.Euler(euler);
            }

            if (rawParams.ContainsKey("localScale"))
            {
                transform.localScale = ReadVector3OrThrow(rawParams["localScale"], "localScale");
            }
        }

        private static void ApplyRectTransformIfNeeded(JsonData rawParams, GameObject instance)
        {
            RectTransform rectTransform = instance.GetComponent<RectTransform>();
            if (rectTransform == null || rawParams == null || !rawParams.IsObject)
            {
                return;
            }

            if (rawParams.ContainsKey("anchorMin"))
            {
                rectTransform.anchorMin = ReadVector2OrThrow(rawParams["anchorMin"], "anchorMin");
            }

            if (rawParams.ContainsKey("anchorMax"))
            {
                rectTransform.anchorMax = ReadVector2OrThrow(rawParams["anchorMax"], "anchorMax");
            }

            if (rawParams.ContainsKey("anchoredPosition"))
            {
                rectTransform.anchoredPosition = ReadVector2OrThrow(rawParams["anchoredPosition"], "anchoredPosition");
            }

            if (rawParams.ContainsKey("sizeDelta"))
            {
                rectTransform.sizeDelta = ReadVector2OrThrow(rawParams["sizeDelta"], "sizeDelta");
            }
        }

        private static Vector3 ReadVector3OrThrow(JsonData value, string fieldName)
        {
            if (value == null || !value.IsObject || !value.ContainsKey("x") || !value.ContainsKey("y") || !value.ContainsKey("z"))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": " + fieldName + " must be an object with x,y,z");
            }

            return new Vector3(
                ReadFloatOrThrow(value["x"], fieldName + ".x"),
                ReadFloatOrThrow(value["y"], fieldName + ".y"),
                ReadFloatOrThrow(value["z"], fieldName + ".z"));
        }

        private static Vector2 ReadVector2OrThrow(JsonData value, string fieldName)
        {
            if (value == null || !value.IsObject || !value.ContainsKey("x") || !value.ContainsKey("y"))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": " + fieldName + " must be an object with x,y");
            }

            return new Vector2(
                ReadFloatOrThrow(value["x"], fieldName + ".x"),
                ReadFloatOrThrow(value["y"], fieldName + ".y"));
        }

        private static float ReadFloatOrThrow(JsonData value, string fieldName)
        {
            if (value == null)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": " + fieldName + " is required");
            }

            if (value.IsDouble)
            {
                return (float)(double)value;
            }

            if (value.IsInt)
            {
                return (int)value;
            }

            double parsed;
            if (double.TryParse(value.ToString(), out parsed))
            {
                return (float)parsed;
            }

            throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": invalid float field: " + fieldName);
        }
    }
}
