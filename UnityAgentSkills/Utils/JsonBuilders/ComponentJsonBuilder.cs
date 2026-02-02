using System.Collections.Generic;
using UnityEngine;
using UnityAgentSkills.Utils.JsonBuilders;
using LitJson2_utf;

namespace UnityAgentSkills.Utils.JsonBuilders
{
    /// <summary>
    /// 组件信息JSON构建器,负责将ComponentInfo列表转换为JSON格式.
    /// 从PrefabQueryComponentsHandler抽取组件JSON构建逻辑.
    /// </summary>
    internal static class ComponentJsonBuilder
    {
        /// <summary>
        /// 构建完整的组件查询结果JSON.
        /// </summary>
        /// <param name="objectPath">GameObject路径.</param>
        /// <param name="obj">目标GameObject.</param>
        /// <param name="components">组件信息列表.</param>
        /// <returns>组件查询结果JSON.</returns>
        public static JsonData BuildComponentsResult(string objectPath, GameObject obj, List<ComponentInfo> components)
        {
            JsonData result = JsonResultBuilder.CreateObject();

            result["objectPath"] = objectPath ?? "";
            result["instanceID"] = obj != null ? obj.GetInstanceID() : 0;
            result["totalComponents"] = components != null ? components.Count : 0;

            JsonData componentsJson = JsonResultBuilder.CreateArray();
            if (components != null)
            {
                foreach (var comp in components)
                {
                    componentsJson.Add(BuildComponentInfo(comp));
                }
            }
            result["components"] = componentsJson;

            return result;
        }

        /// <summary>
        /// 构建单个组件的JSON表示.
        /// </summary>
        /// <param name="info">组件信息.</param>
        /// <returns>组件的JSON表示.</returns>
        public static JsonData BuildComponentInfo(ComponentInfo info)
        {
            JsonData compJson = JsonResultBuilder.CreateObject();

            compJson["type"] = info.type ?? "";
            compJson["instanceID"] = info.instanceID;

            // 仅在有值时添加scriptPath字段
            if (!string.IsNullOrEmpty(info.scriptPath))
            {
                compJson["scriptPath"] = info.scriptPath;
            }

            compJson["properties"] = info.properties ?? JsonResultBuilder.CreateObject();

            return compJson;
        }
    }
}
