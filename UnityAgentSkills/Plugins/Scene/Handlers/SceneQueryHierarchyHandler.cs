using System;
using System.Collections.Generic;
using UnityAgentSkills.Core;
using UnityAgentSkills.Utils;
using UnityAgentSkills.Utils.JsonBuilders;
using LitJson2_utf;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityAgentSkills.Plugins.Scene.Handlers
{
    /// <summary>
    /// scene.queryHierarchy 命令处理器.
    /// 查询当前已加载的所有场景的 GameObject 层级.
    /// 支持两种模式: 默认树结构模式和 nameContains 过滤模式.
    /// 在 Play 模式和编辑模式都可执行.
    /// </summary>
    internal static class SceneQueryHierarchyHandler
    {
        /// <summary>
        /// 支持的命令类型.
        /// </summary>
        public const string CommandType = "scene.queryHierarchy";

        /// <summary>
        /// 执行场景层级查询命令.
        /// </summary>
        /// <param name="rawParams">命令参数 json.</param>
        /// <returns>结果 json.</returns>
        public static JsonData Execute(JsonData rawParams)
        {
            CommandParams parameters = new CommandParams(rawParams);

            bool includeInactive = parameters.GetBool("includeInactive", true);
            int maxDepth = parameters.GetInt("maxDepth", -1);
            string nameContains = parameters.GetString("nameContains", null);
            int maxMatches = parameters.GetInt("maxMatches", 50);

            if (maxDepth < -1)
            {
                throw new ArgumentException(
                    UnityAgentSkillCommandErrorCodes.InvalidFields + ": maxDepth must be >= -1");
            }

            if (maxMatches <= 0)
            {
                throw new ArgumentException(
                    UnityAgentSkillCommandErrorCodes.InvalidFields + ": maxMatches must be > 0");
            }

            bool isFilterMode = !string.IsNullOrEmpty(nameContains) && !string.IsNullOrEmpty(nameContains.Trim());

            JsonData result = JsonResultBuilder.CreateObject();

            JsonData scenesArray = JsonResultBuilder.CreateArray();
            int loadedSceneCount = 0;

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                UnityEngine.SceneManagement.Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                {
                    continue;
                }

                GameObject[] rootObjects = scene.GetRootGameObjects();
                loadedSceneCount++;
                if (isFilterMode)
                {
                    scenesArray.Add(BuildSceneFilterResult(scene.name, scene.path, rootObjects, includeInactive, maxDepth, nameContains, maxMatches));
                }
                else
                {
                    scenesArray.Add(BuildSceneHierarchyResult(scene.name, scene.path, rootObjects, includeInactive, maxDepth));
                }
            }

            // Play 模式下追加 DontDestroyOnLoad 场景.
            if (DontDestroyOnLoadHelper.IsAvailable)
            {
                GameObject[] ddolRoots = DontDestroyOnLoadHelper.GetRootGameObjects();
                if (ddolRoots.Length > 0)
                {
                    loadedSceneCount++;
                    if (isFilterMode)
                    {
                        scenesArray.Add(BuildSceneFilterResult(DontDestroyOnLoadHelper.SceneName, "", ddolRoots, includeInactive, maxDepth, nameContains, maxMatches));
                    }
                    else
                    {
                        scenesArray.Add(BuildSceneHierarchyResult(DontDestroyOnLoadHelper.SceneName, "", ddolRoots, includeInactive, maxDepth));
                    }
                }
            }

            result["loadedSceneCount"] = loadedSceneCount;
            result["scenes"] = scenesArray;

            return result;
        }

        /// <summary>
        /// 构建单个场景的层级树结果(默认模式).
        /// </summary>
        private static JsonData BuildSceneHierarchyResult(
            string sceneName,
            string scenePath,
            GameObject[] rootObjects,
            bool includeInactive,
            int maxDepth)
        {
            JsonData sceneJson = JsonResultBuilder.CreateObject();
            sceneJson["sceneName"] = sceneName;
            sceneJson["scenePath"] = scenePath ?? "";

            // 统计总 GameObject 数并构建层级.
            int totalGameObjects = 0;
            JsonData hierarchyArray = JsonResultBuilder.CreateArray();

            foreach (GameObject root in rootObjects)
            {
                if (!includeInactive && !root.activeSelf)
                {
                    continue;
                }

                HierarchyNode node = HierarchyTraverser.Traverse(root, includeInactive, maxDepth);
                if (node != null)
                {
                    totalGameObjects += CountNodes(node);
                    hierarchyArray.Add(HierarchyJsonBuilder.BuildGameObjectNode(node));
                }
            }

            sceneJson["totalGameObjects"] = totalGameObjects;
            sceneJson["hierarchy"] = hierarchyArray;

            return sceneJson;
        }

        /// <summary>
        /// 构建单个场景的过滤匹配结果(nameContains 模式).
        /// </summary>
        private static JsonData BuildSceneFilterResult(
            string sceneName,
            string scenePath,
            GameObject[] rootObjects,
            bool includeInactive,
            int maxDepth,
            string nameContains,
            int maxMatches)
        {
            JsonData sceneJson = JsonResultBuilder.CreateObject();
            sceneJson["sceneName"] = sceneName;
            sceneJson["scenePath"] = scenePath ?? "";

            // 跨多个 root 汇总 matches.
            int sceneTotalMatches = 0;
            List<HierarchyNode> sceneMatches = new List<HierarchyNode>();

            foreach (GameObject root in rootObjects)
            {
                if (!includeInactive && !root.activeSelf)
                {
                    continue;
                }

                int rootTotalMatches;
                var rootMatches = HierarchyTraverser.TraverseAndFilter(
                    root, includeInactive, maxDepth, nameContains, maxMatches - sceneMatches.Count, out rootTotalMatches);

                sceneTotalMatches += rootTotalMatches;
                sceneMatches.AddRange(rootMatches);
            }

            int matchedCount = sceneMatches.Count;
            sceneJson["totalMatches"] = sceneTotalMatches;
            sceneJson["matchedCount"] = matchedCount;
            sceneJson["truncated"] = sceneTotalMatches > matchedCount;
            sceneJson["matches"] = BuildMatchesArray(sceneMatches);

            return sceneJson;
        }

        /// <summary>
        /// 构建 matches 数组 json.
        /// </summary>
        private static JsonData BuildMatchesArray(List<HierarchyNode> matches)
        {
            JsonData arr = JsonResultBuilder.CreateArray();
            if (matches == null)
            {
                return arr;
            }

            foreach (var node in matches)
            {
                JsonData json = JsonResultBuilder.CreateObject();
                json["name"] = node.name ?? "";
                json["instanceID"] = node.instanceID;
                json["path"] = node.path ?? "";
                json["siblingIndex"] = node.siblingIndex;
                json["depth"] = node.depth;
                json["isActive"] = node.isActive;
                arr.Add(json);
            }

            return arr;
        }

        /// <summary>
        /// 递归计算节点总数.
        /// </summary>
        private static int CountNodes(HierarchyNode node)
        {
            if (node == null)
            {
                return 0;
            }

            int count = 1;
            foreach (var child in node.children)
            {
                count += CountNodes(child);
            }
            return count;
        }
    }
}
