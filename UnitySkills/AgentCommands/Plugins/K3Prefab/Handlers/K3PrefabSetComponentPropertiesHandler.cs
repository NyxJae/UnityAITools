using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using LitJson2_utf;
using AgentCommands.Core;
using AgentCommands.Utils;
using AgentCommands.Plugins.K3Prefab.Utils;
using AgentCommands.Plugins.K3Prefab.Models;

namespace AgentCommands.Plugins.K3Prefab.Handlers
{
    /// <summary>
    /// K3预制体组件属性修改命令处理器
    /// 命令类型: k3prefab.setComponentProperties
    /// </summary>
    internal static class K3PrefabSetComponentPropertiesHandler
    {
        public const string CommandType = "k3prefab.setComponentProperties";

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

            int index = parameters.GetInt("index", 0);
            if (index < 0)
            {
                throw new ArgumentException(AgentCommandErrorCodes.InvalidFields + ": index must be >= 0");
            }

            // 解析modifications数组
            if (!parameters.Has("modifications"))
            {
                throw new ArgumentException("EMPTY_MODIFICATIONS: modifications参数缺失");
            }

            JsonData modificationsJson = parameters.GetData()["modifications"];
            if (modificationsJson == null || !modificationsJson.IsArray || modificationsJson.Count == 0)
            {
                throw new ArgumentException("EMPTY_MODIFICATIONS: modifications数组不能为空");
            }

            var modifications = ParseModifications(modificationsJson);

            // 3. 加载预制体
            GameObject prefab = PrefabLoader.LoadPrefab(prefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException($"PREFAB_NOT_FOUND: 预制体文件不存在: {prefabPath}");
            }

            // 4. 查找K3组件
            var allMatches = K3ComponentFinder.FindComponentsByK3Id(prefab, k3Id);

            if (allMatches.Count == 0)
            {
                throw new InvalidOperationException($"K3ID_NOT_FOUND: 未找到K3ID为{k3Id}的组件");
            }

            if (index >= allMatches.Count)
            {
                throw new IndexOutOfRangeException($"INDEX_OUT_OF_RANGE: 索引超出范围，K3ID {k3Id}只有{allMatches.Count}个匹配项（索引0-{allMatches.Count - 1}），但请求了索引{index}");
            }

            var targetMatch = allMatches[index];

            // 5. 修改属性
            var modificationResults = K3ComponentPropertyModifier.ModifyProperties(targetMatch, modifications);

            // 6. 保存预制体（如果有属性被成功修改）
            bool saved = false;
            if (modificationResults.Any(r => r.status == "success"))
            {
                PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
                saved = true;
            }

            // 7. 构建结果
            JsonData result = new JsonData();
            result.SetJsonType(JsonType.Object);

            result["prefabPath"] = prefabPath;
            result["k3Id"] = (JsonData)k3Id;
            result["index"] = (JsonData)index;
            result["gameObjectPath"] = K3ComponentPropertyReader.ReadGameObjectPath(targetMatch.gameObject);
            result["componentType"] = targetMatch.component.GetType().Name;

            // 构建modifications结果数组
            JsonData modificationsArray = new JsonData();
            modificationsArray.SetJsonType(JsonType.Array);

            foreach (var modResult in modificationResults)
            {
                JsonData modJson = new JsonData();
                modJson.SetJsonType(JsonType.Object);

                modJson["property"] = modResult.property;
                modJson["oldValue"] = ConvertToJsonData(modResult.oldValue);
                modJson["expectedValue"] = ConvertToJsonData(modResult.expectedValue);
                modJson["currentValue"] = ConvertToJsonData(modResult.currentValue);
                modJson["newValue"] = ConvertToJsonData(modResult.newValue);
                modJson["status"] = modResult.status;
                modJson["message"] = modResult.message ?? string.Empty;

                modificationsArray.Add(modJson);
            }

            result["modifications"] = modificationsArray;

            // 修改后的属性
            result["currentProperties"] = K3ComponentPropertyReader.ReadK3ComponentProperties(targetMatch, true);

            result["saved"] = (JsonData)saved;

            // 统计信息
            JsonData summary = new JsonData();
            summary.SetJsonType(JsonType.Object);
            summary["total"] = (JsonData)modificationResults.Count;
            summary["success"] = (JsonData)modificationResults.Count(r => r.status == "success");
            summary["skipped"] = (JsonData)modificationResults.Count(r => r.status == "skipped");
            summary["failed"] = (JsonData)modificationResults.Count(r => r.status == "failed");

            result["summary"] = summary;

            return result;
        }

        /// <summary>
        /// 解析modifications数组
        /// </summary>
        private static List<K3PropertyModification> ParseModifications(JsonData modificationsJson)
        {
            var modifications = new List<K3PropertyModification>();

            for (int i = 0; i < modificationsJson.Count; i++)
            {
                JsonData modJson = modificationsJson[i];

                string property = modJson["property"]?.ToString() ?? string.Empty;
                if (string.IsNullOrEmpty(property))
                {
                    throw new ArgumentException($"modifications[{i}]: property is required");
                }

                object oldValue = ParseJsonValue(modJson["oldValue"]);
                object newValue = ParseJsonValue(modJson["newValue"]);

                modifications.Add(new K3PropertyModification
                {
                    property = property,
                    oldValue = oldValue,
                    newValue = newValue
                });
            }

            return modifications;
        }

        /// <summary>
        /// 解析JsonData值为C#对象
        /// </summary>
        private static object ParseJsonValue(JsonData jsonData)
        {
            if (jsonData == null)
            {
                return null;
            }

            switch (jsonData.GetJsonType())
            {
                case JsonType.String:
                    return jsonData.ToString();
                case JsonType.Int:
                    return (int)jsonData;
                case JsonType.Long:
                    return (long)jsonData;
                case JsonType.Double:
                    return (double)jsonData;
                case JsonType.Boolean:
                    return (bool)jsonData;
                default:
                    return jsonData.ToString();
            }
        }

        /// <summary>
        /// 将C#对象转换为JsonData
        /// </summary>
        private static JsonData ConvertToJsonData(object value)
        {
            if (value == null)
            {
                return new JsonData();
            }

            if (value is string str)
            {
                return new JsonData(str);
            }
            if (value is int i)
            {
                return new JsonData(i);
            }
            if (value is uint u)
            {
                return new JsonData((int)u);
            }
            if (value is double d)
            {
                return new JsonData(d);
            }
            if (value is float f)
            {
                return new JsonData((double)f);
            }
            if (value is bool b)
            {
                return new JsonData(b);
            }

            return new JsonData(value.ToString());
        }
    }
}
