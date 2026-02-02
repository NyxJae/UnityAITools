using System;
using System.Collections.Generic;
using UnityAgentSkills.Core;
using UnityAgentSkills.Utils;
using UnityAgentSkills.Utils.JsonBuilders;
using LitJson2_utf;
using UnityEngine;

namespace UnityAgentSkills.Plugins.Prefab.Handlers
{
    /// <summary>
    /// prefab.setGameObjectProperties命令处理器.
    /// 修改预制体中指定GameObject的属性.
    /// </summary>
    internal static class PrefabSetGameObjectPropertiesHandler
    {
        /// <summary>
        /// 支持的命令类型.
        /// </summary>
        public const string CommandType = "prefab.setGameObjectProperties";

        /// <summary>
        /// 执行预制体GameObject属性修改命令.
        /// </summary>
        /// <param name="rawParams">命令参数json.</param>
        /// <returns>结果json.</returns>
        public static JsonData Execute(JsonData rawParams)
        {
            CommandParams parameters = new CommandParams(rawParams);

            string prefabPath = parameters.GetString("prefabPath", null);
            string objectPath = parameters.GetString("objectPath", null);
            int siblingIndex = parameters.GetInt("siblingIndex", 0);

            // 参数验证
            if (string.IsNullOrEmpty(prefabPath))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": prefabPath is required");
            }
            if (string.IsNullOrEmpty(objectPath))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": objectPath is required");
            }

            // 获取properties参数
            if (!parameters.Has("properties"))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": properties is required");
            }

            JsonData properties = parameters.GetData()["properties"];
            if (properties == null || !properties.IsObject || properties.Count == 0)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": properties对象不能为空, 至少需要指定一个要修改的属性");
            }

            // 加载预制体
            GameObject prefab = PrefabLoader.LoadPrefab(prefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException("Prefab not found at path: " + prefabPath);
            }

            // 定位目标GameObject
            GameObject target = GameObjectPathFinder.FindByPath(prefab, objectPath, siblingIndex);
            if (target == null)
            {
                throw new InvalidOperationException("GameObject not found at path: " + objectPath + " (siblingIndex=" + siblingIndex + ")");
            }

            // 修改属性
            List<GameObjectPropertyModifier.PropertyChange> modifiedProperties;
            JsonData currentProperties = GameObjectPropertyModifier.ModifyProperties(target, properties, out modifiedProperties);

            // 保存预制体
            bool saved = GameObjectPropertyModifier.SavePrefab(prefab, prefabPath);

            // 构建结果
            JsonData result = JsonResultBuilder.CreateObject();
            result["prefabPath"] = prefabPath;
            result["objectPath"] = objectPath;
            result["instanceID"] = target.GetInstanceID();
            result["currentProperties"] = currentProperties;
            result["saved"] = saved;

            // 构建modifiedProperties数组
            JsonData modifiedPropsArray = JsonResultBuilder.CreateArray();
            foreach (var change in modifiedProperties)
            {
                JsonData changeObj = JsonResultBuilder.CreateObject();
                changeObj["name"] = change.name;
                changeObj["oldValue"] = ConvertJsonValue(change.oldValue);
                changeObj["newValue"] = ConvertJsonValue(change.newValue);
                modifiedPropsArray.Add(changeObj);
            }
            result["modifiedProperties"] = modifiedPropsArray;

            return result;
        }

        /// <summary>
        /// 将对象值转换为JsonData.
        /// </summary>
        private static JsonData ConvertJsonValue(object value)
        {
            if (value == null)
            {
                return new JsonData(); // null
            }

            if (value is string)
            {
                return new JsonData((string)value);
            }
            if (value is int)
            {
                return new JsonData((int)value);
            }
            if (value is bool)
            {
                return new JsonData((bool)value);
            }
            if (value is double || value is float)
            {
                return new JsonData((double)value);
            }
            if (value is long)
            {
                return new JsonData((long)value);
            }

            // 对于复杂类型(如Dictionary), 转换为JsonData对象
            if (value is Dictionary<string, int> dictInt)
            {
                JsonData obj = JsonResultBuilder.CreateObject();
                foreach (var kvp in dictInt)
                {
                    obj[kvp.Key] = new JsonData(kvp.Value);
                }
                return obj;
            }

            // 默认返回字符串表示
            return new JsonData(value.ToString());
        }
    }
}
