using System;
using AgentCommands.Core;
using AgentCommands.Utils;
using LitJson2_utf;
using UnityEngine;

namespace AgentCommands.Handlers
{
    /// <summary>
    /// prefab.queryComponents命令处理器.
    /// </summary>
    internal static class PrefabQueryComponentsHandler
    {
        /// <summary>
        /// 支持的命令类型.
        /// </summary>
        public const string CommandType = "prefab.queryComponents";

        /// <summary>
        /// 执行预制体组件查询命令.
        /// </summary>
        /// <param name="rawParams">命令参数json.</param>
        /// <returns>结果json.</returns>
        public static JsonData Execute(JsonData rawParams)
        {
            CommandParams p = new CommandParams(rawParams);

            string prefabPath = p.GetString("prefabPath", null);
            string objectPath = p.GetString("objectPath", null);
            
            // 参数验证
            if (string.IsNullOrEmpty(prefabPath))
            {
                throw new ArgumentException(AgentCommandErrorCodes.InvalidFields + ": prefabPath is required");
            }
            if (string.IsNullOrEmpty(objectPath))
            {
                throw new ArgumentException(AgentCommandErrorCodes.InvalidFields + ": objectPath is required");
            }

            // 加载预制体
            GameObject prefab = PrefabLoader.LoadPrefab(prefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException("Prefab not found at path: " + prefabPath);
            }

            // 定位目标GameObject
            GameObject target = GameObjectPathFinder.FindByPath(prefab, objectPath);
            if (target == null)
            {
                throw new InvalidOperationException("GameObject not found at path: " + objectPath);
            }

            // 获取组件过滤参数
            string[] componentFilter = null;
            if (p.Has("componentFilter") && p.GetData()["componentFilter"].IsArray)
            {
                JsonData filterArray = p.GetData()["componentFilter"];
                componentFilter = new string[filterArray.Count];
                for (int i = 0; i < filterArray.Count; i++)
                {
                    componentFilter[i] = filterArray[i].ToString();
                }
            }

            bool includeBuiltin = p.GetBool("includeBuiltin", false);
            bool includePrivateFields = p.GetBool("includePrivateFields", false);

            // 读取组件
            var components = ComponentPropertyReader.ReadComponents(
                target, componentFilter, includeBuiltin, includePrivateFields);

            // 构建结果
            JsonData result = new JsonData();
            result.SetJsonType(JsonType.Object);
            
            result["objectPath"] = objectPath;
            result["instanceID"] = target.GetInstanceID();
            result["totalComponents"] = components.Count;
            
            JsonData componentsJson = new JsonData();
            componentsJson.SetJsonType(JsonType.Array);
            foreach (var comp in components)
            {
                JsonData compJson = new JsonData();
                compJson.SetJsonType(JsonType.Object);
                
                compJson["type"] = comp.type ?? "";
                compJson["instanceID"] = comp.instanceID;
                
                if (!string.IsNullOrEmpty(comp.scriptPath))
                {
                    compJson["scriptPath"] = comp.scriptPath;
                }
                
                compJson["properties"] = comp.properties;
                componentsJson.Add(compJson);
            }
            result["components"] = componentsJson;

            return result;
        }
    }
}
