using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using LitJson2_utf;
using UnityAgentSkills.Core;
using UnityAgentSkills.Utils;
using K3Engine.Component.Interfaces;
using K3Editor;

namespace UnityAgentSkills.Plugins.K3Prefab.Handlers
{
    /// <summary>
    /// K3预制体组件创建命令处理器
    /// 命令类型: k3prefab.createComponent
    /// </summary>
    internal static class K3PrefabCreateComponentHandler
    {
        public const string CommandType = "k3prefab.createComponent";

        // 支持的组件类型与创建方法映射
        private static readonly Dictionary<string, Func<GameObject>> ComponentCreators = new Dictionary<string, Func<GameObject>>
        {
            { "K3Label", () => K3Engine.Component.K3DefaultControls.CreateK3Label() },
            { "K3Button", () => K3Engine.Component.K3DefaultControls.CreateK3Button() },
            { "K3Panel", () => K3Engine.Component.K3DefaultControls.CreateK3panel() },
            { "K3Image", () => K3Engine.Component.K3DefaultControls.CreateK3Image() },
            { "K3Edit", () => K3Engine.Component.K3DefaultControls.CreateK3Edit() },
            { "K3CheckBox", () => K3Engine.Component.K3DefaultControls.CreateK3CheckBox() },
            { "K3LinkLabel", () => K3Engine.Component.K3DefaultControls.CreateK3LinkLabel() },
            { "K3ListView", () => K3Engine.Component.K3DefaultControls.CreateK3ListView() },
            { "K3TabButton", () => K3Engine.Component.K3DefaultControls.CreateK3TabButton(K3Engine.Component.K3DefaultControls.K3COMP_NAME_TABBUTTON, null, 0, 125f) },
            { "K3Tab", () => K3Engine.Component.K3DefaultControls.CreateK3Tab() },
            { "K3ProgressBar", () => K3Engine.Component.K3DefaultControls.CreateK3ProgressBar() },
            { "K3RadarChart", () => K3Engine.Component.K3DefaultControls.CreateK3RadarChart() },
            { "K3HeadIcon", () => K3Engine.Component.K3DefaultControls.CreateK3HeadIcon() },
            { "K3Itembox", () => K3Engine.Component.K3DefaultControls.CreateK3Itembox() },
            { "K3LabelButton", () => K3Engine.Component.K3DefaultControls.CreateK3LabelButton() },
            { "K3Animation", () => K3Engine.Component.K3DefaultControls.CreateK3Animation() },
            { "K3SliderBar", () => K3Engine.Component.K3DefaultControls.CreateK3SliderBar() },
            { "K3Movie", () => K3Engine.Component.K3DefaultControls.CreateK3Movie() },
            { "K3NumImage", () => K3Engine.Component.K3DefaultControls.CreateK3NumImage() },
            { "K3JoyStick", () => K3Engine.Component.K3DefaultControls.CreateK3JoyStick() },
            { "K3Magicbox", () => K3Engine.Component.K3DefaultControls.CreateK3Magicbox() },
            { "K3ExpandListView", () => K3Engine.Component.K3DefaultControls.CreateK3ExpandListView() },
            { "K3ExpandListPanel", () => K3Engine.Component.K3DefaultControls.CreateK3ExpanelListPanel() },
            { "K3InsightImage", () => K3Engine.Component.K3DefaultControls.CreateK3InsightImage() }
        };

        public static JsonData Execute(JsonData rawParams)
        {
            // 1. 参数解析
            CommandParams parameters = new CommandParams(rawParams);

            // 2. 参数读取和验证
            string prefabPath = parameters.GetString("prefabPath", null);
            if (string.IsNullOrEmpty(prefabPath))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": prefabPath is required");
            }

            string parentPath = parameters.GetString("parentPath", null);
            if (string.IsNullOrEmpty(parentPath))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": parentPath is required");
            }

            string componentType = parameters.GetString("componentType", null);
            if (string.IsNullOrEmpty(componentType))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": componentType is required");
            }

            // 解析初始属性
            Dictionary<string, object> initialProperties = new Dictionary<string, object>();
            if (parameters.Has("initialProperties"))
            {
                JsonData propsJson = parameters.GetData()["initialProperties"];
                if (propsJson != null && propsJson.IsObject)
                {
                    foreach (string key in propsJson.Keys)
                    {
                        // 将 JsonData 转换为正确的类型
                        initialProperties[key] = ConvertJsonValue(propsJson[key]);
                    }
                }
            }

            // 3. 加载预制体到编辑场景（使用 PrefabUtility.LoadPrefabContents 以支持预制体编辑）
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabRoot == null)
            {
                CommandError error = CommandErrorFactory.CreatePrefabNotFoundError(prefabPath);
                throw new InvalidOperationException($"{error.message}: {error.detail}");
            }

            // 4. 查找父节点
            GameObject parentGO = GameObjectPathFinder.FindByPath(prefabRoot, parentPath);
            if (parentGO == null)
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
                CommandError error = CommandErrorFactory.CreateGameObjectNotFoundError(parentPath);
                throw new InvalidOperationException($"{error.message}: {error.detail}");
            }

            // 5. 验证父节点是容器
            IK3Container container = parentGO.GetComponent<IK3Container>();
            if (container == null)
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
                CommandError error = CommandErrorFactory.CreateRuntimeError(
                    $"INVALID_PARENT_CONTAINER: 父节点不是有效的 K3 容器，必须实现 IK3Container 接口（如 K3Dialog、K3Panel）"
                );
                throw new InvalidOperationException($"{error.message}: {error.detail}");
            }

            // 6. 验证组件类型支持
            if (!ComponentCreators.ContainsKey(componentType))
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
                CommandError error = CommandErrorFactory.CreateRuntimeError(
                    $"UNSUPPORTED_COMPONENT_TYPE: 不支持的组件类型: {componentType}，支持的类型包括: {string.Join(", ", ComponentCreators.Keys)}"
                );
                throw new InvalidOperationException($"{error.message}: {error.detail}");
            }

            // 7. 创建组件
            GameObject componentGO = CreateComponent(parentGO, componentType, initialProperties);

            // 8. 保存预制体
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);

            // 9. 构建返回结果（在卸载预制体之前，因为 componentGO 在编辑场景中）
            JsonData result = BuildResult(prefabPath, parentPath, componentType, componentGO);

            // 10. 卸载预制体编辑场景
            PrefabUtility.UnloadPrefabContents(prefabRoot);

            return result;
        }

        private static GameObject CreateComponent(GameObject parentGO, string componentType, Dictionary<string, object> initialProperties)
        {
            // 1. 创建 GameObject
            GameObject componentGO = ComponentCreators[componentType]();
            
            // 2. 设置父节点（使用 Undo.SetTransformParent 以支持预制体编辑和 Undo 系统）
            if (parentGO != null)
            {
                Undo.SetTransformParent(componentGO.transform, parentGO.transform, $"Create {componentType}");
            }

            // 3. 设置 K3Property
            IK3Container container = parentGO.GetComponent<IK3Container>();
            IK3Component k3Component = componentGO.GetComponent<IK3Component>();

            if (k3Component != null && container != null)
            {
                // 使用 K3EditorUtils.SetK3Property 方法
                K3EditorUtils.SetK3Property(parentGO, container.property, k3Component.property);
            }

            // 4. 命名
            if (k3Component != null)
            {
                componentGO.name = $"{componentType}_{k3Component.property.ID}";
            }

            // 5. 设置初始属性
            if (initialProperties != null && initialProperties.Count > 0)
            {
                SetInitialProperties(componentGO, componentType, initialProperties);
            }

            return componentGO;
        }

        /// <summary>
        /// 将 JsonData 值转换为对应的 C# 类型
        /// </summary>
        private static object ConvertJsonValue(JsonData jsonData)
        {
            if (jsonData == null)
            {
                return null;
            }

            // 根据 JsonData 的类型进行转换
            if (jsonData.IsString)
            {
                return (string)jsonData;
            }
            if (jsonData.IsInt)
            {
                return (int)jsonData;
            }
            if (jsonData.IsLong)
            {
                return (long)jsonData;
            }
            if (jsonData.IsBoolean)
            {
                return (bool)jsonData;
            }
            if (jsonData.IsDouble)
            {
                return (double)jsonData;
            }
            if (jsonData.IsArray || jsonData.IsObject)
            {
                // 对于复杂类型，返回 JsonData 本身
                return jsonData;
            }

            // 默认返回字符串表示
            return jsonData.ToString();
        }

        private static void SetInitialProperties(GameObject componentGO, string componentType, Dictionary<string, object> initialProperties)
        {
            // 获取 K3 组件
            IK3Component k3Component = componentGO.GetComponent<IK3Component>();
            if (k3Component == null)
            {
                return;
            }

            // 使用 SerializedObject 修改属性
            SerializedObject serializedObject = new SerializedObject(k3Component as UnityEngine.Object);

            foreach (var kvp in initialProperties)
            {
                string propertyName = kvp.Key;
                object value = kvp.Value;

                // 查找属性
                SerializedProperty property = serializedObject.FindProperty(propertyName);
                if (property == null)
                {
                    continue; // 属性不存在，跳过
                }

                // 根据类型设置值
                try
                {
                    if (value is int intValue)
                    {
                        property.intValue = intValue;
                    }
                    else if (value is float floatValue)
                    {
                        property.floatValue = floatValue;
                    }
                    else if (value is bool boolValue)
                    {
                        property.boolValue = boolValue;
                    }
                    else if (value is string stringValue)
                    {
                        property.stringValue = stringValue;
                    }
                    else if (value is double doubleValue)
                    {
                        property.doubleValue = doubleValue;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[K3PrefabCreateComponentHandler] 设置属性 {propertyName} 失败: {ex.Message}");
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static JsonData BuildResult(string prefabPath, string parentPath, string componentType, GameObject componentGO)
        {
            JsonData result = new JsonData();

            result["prefabPath"] = prefabPath;
            result["parentPath"] = parentPath;
            result["componentType"] = componentType;

            // GameObject 信息
            JsonData gameObjectJson = new JsonData();
            gameObjectJson["name"] = componentGO.name;
            gameObjectJson["path"] = GameObjectPathFinder.GetPath(componentGO);
            gameObjectJson["instanceID"] = componentGO.GetInstanceID();
            result["gameObject"] = gameObjectJson;

            // K3 组件信息
            IK3Component k3Component = componentGO.GetComponent<IK3Component>();
            if (k3Component != null)
            {
                JsonData k3ComponentJson = new JsonData();
                k3ComponentJson["type"] = componentType;

                // 读取 K3Property
                JsonData propertiesJson = new JsonData();
                propertiesJson["ID"] = (int)k3Component.property.ID;
                propertiesJson["parentID"] = (int)k3Component.property.parentID;

                // 读取其他属性
                SerializedObject serializedObject = new SerializedObject(k3Component as UnityEngine.Object);
                SerializedProperty iter = serializedObject.GetIterator();
                bool enterChildren = true;
                while (iter.Next(enterChildren))
                {
                    if (iter.name == "m_Script" || iter.name == "property")
                    {
                        enterChildren = false;
                        continue;
                    }

                    switch (iter.propertyType)
                    {
                        case SerializedPropertyType.Integer:
                            propertiesJson[iter.name] = iter.intValue;
                            break;
                        case SerializedPropertyType.Boolean:
                            propertiesJson[iter.name] = iter.boolValue;
                            break;
                        case SerializedPropertyType.Float:
                            propertiesJson[iter.name] = iter.floatValue;
                            break;
                        case SerializedPropertyType.String:
                            propertiesJson[iter.name] = iter.stringValue;
                            break;
                        case SerializedPropertyType.ObjectReference:
                            if (iter.objectReferenceValue != null)
                            {
                                propertiesJson[iter.name] = iter.objectReferenceValue.name;
                            }
                            break;
                    }

                    enterChildren = false;
                }

                k3ComponentJson["properties"] = propertiesJson;
                result["k3Component"] = k3ComponentJson;
            }

            result["saved"] = true;

            return result;
        }
    }
}
