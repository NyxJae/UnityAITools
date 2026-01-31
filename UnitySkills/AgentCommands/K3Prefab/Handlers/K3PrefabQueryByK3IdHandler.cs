using System;
using UnityEngine;
using K3Engine.Component.Interfaces;
using LitJson2_utf;
using AgentCommands.Core;
using AgentCommands.Utils;
using AgentCommands.K3Prefab.Utils;
using AgentCommands.K3Prefab.Models;

namespace AgentCommands.K3Prefab.Handlers
{
    /// <summary>
    /// K3预制体K3ID查询命令处理器
    /// 命令类型: k3prefab.queryByK3Id
    /// </summary>
    internal static class K3PrefabQueryByK3IdHandler
    {
        public const string CommandType = "k3prefab.queryByK3Id";

        public static JsonData Execute(JsonData rawParams)
        {
            // 1. 参数解析
            CommandParams parameters = new CommandParams(rawParams);

            // 2. 参数读取和验证
            string prefabPath = parameters.GetString("prefabPath", null);
            if (string.IsNullOrEmpty(prefabPath))
            {
                throw new ArgumentException(AgentCommandErrorCodes.InvalidFields + ": prefabPath is required");
            }

            uint k3Id = 0;
            if (parameters.Has("k3Id"))
            {
                int k3IdInt = parameters.GetInt("k3Id");
                if (k3IdInt < 0)
                {
                    throw new ArgumentException(AgentCommandErrorCodes.InvalidFields + ": k3Id must be non-negative");
                }
                k3Id = (uint)k3IdInt;
            }
            if (k3Id == 0)
            {
                throw new ArgumentException(AgentCommandErrorCodes.InvalidFields + ": k3Id must be greater than 0");
            }

            string[] componentFilter = null;
            if (parameters.Has("componentFilter") && parameters.GetData()["componentFilter"].IsArray)
            {
                var filterJson = parameters.GetData()["componentFilter"];
                componentFilter = new string[filterJson.Count];
                for (int i = 0; i < filterJson.Count; i++)
                {
                    componentFilter[i] = (string)filterJson[i];
                }
            }

            // 3. 加载预制体
            GameObject prefab = PrefabLoader.LoadPrefab(prefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException($"PREFAB_NOT_FOUND: 预制体文件不存在: {prefabPath}");
            }

            // 4. 查找K3组件
            var matches = K3ComponentFinder.FindComponentsByK3Id(prefab, k3Id, componentFilter);

            // 注意:如果componentFilter过滤后无匹配,返回totalMatches=0而不是抛错
            // 只有在整个预制体中找不到该K3ID时才抛错

            // 检查预制体中是否存在该K3ID(不过滤)
            var allMatches = K3ComponentFinder.FindComponentsByK3Id(prefab, k3Id, null);
            if (allMatches.Count == 0)
            {
                throw new InvalidOperationException($"K3ID_NOT_FOUND: 未找到K3ID为{k3Id}的组件");
            }

            // 5. 构建结果
            JsonData result = new JsonData();
            result.SetJsonType(JsonType.Object);

            result["prefabPath"] = prefabPath;
            result["k3Id"] = (JsonData)k3Id;
            result["totalMatches"] = (JsonData)matches.Count;

            // 构建components数组
            JsonData componentsArray = new JsonData();
            componentsArray.SetJsonType(JsonType.Array);

            foreach (var match in matches)
            {
                try
                {
                    JsonData componentData = BuildComponentData(match);
                    componentsArray.Add(componentData);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[K3PrefabQueryByK3Id] 构建组件数据时出错 (K3ID={k3Id}, index={match.index}): {ex.Message}");
                    // 跳过这个组件，继续处理下一个
                    continue;
                }
            }

            result["components"] = componentsArray;

            return result;
        }

        /// <summary>
        /// 构建单个K3组件的数据
        /// </summary>
        private static JsonData BuildComponentData(K3ComponentMatch match)
        {
            JsonData componentData = new JsonData();
            componentData.SetJsonType(JsonType.Object);

            // 基本信息
            componentData["index"] = (JsonData)match.index;
            componentData["gameObjectPath"] = K3ComponentPropertyReader.ReadGameObjectPath(match.gameObject);
            componentData["containerPath"] = K3ComponentPropertyReader.ReadContainerPath(match.container);
            componentData["containerType"] = match.containerType;

            // GameObject属性
            componentData["gameObjectProperties"] = K3ComponentPropertyReader.ReadGameObjectProperties(match.gameObject);

            // K3组件信息
            JsonData k3ComponentData = new JsonData();
            k3ComponentData.SetJsonType(JsonType.Object);

            k3ComponentData["type"] = match.component.GetType().Name;
            k3ComponentData["instanceID"] = (JsonData)match.gameObject.GetInstanceID();
            k3ComponentData["properties"] = K3ComponentPropertyReader.ReadK3ComponentProperties(match, true);

            componentData["k3Component"] = k3ComponentData;

            return componentData;
        }
    }
}
