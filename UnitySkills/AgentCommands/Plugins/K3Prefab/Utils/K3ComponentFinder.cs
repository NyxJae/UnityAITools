using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using K3Engine.Component;
using K3Engine.Component.Interfaces;
using AgentCommands.Plugins.K3Prefab.Models;

namespace AgentCommands.Plugins.K3Prefab.Utils
{
    /// <summary>
    /// K3组件查找工具类
    /// 负责通过K3ID在预制体中查找K3组件
    /// 实现方式：直接遍历预制体中所有IK3Component组件，检查comp.ID == k3Id
    /// 容器识别：向上遍历Transform父节点查找K3DialogEx或K3Panel组件
    /// </summary>
    public static class K3ComponentFinder
    {
        /// <summary>
        /// 通过K3ID查找所有匹配的K3组件
        /// </summary>
        /// <param name="prefabRoot">预制体根GameObject</param>
        /// <param name="k3Id">K3组件ID</param>
        /// <returns>所有匹配的K3组件列表</returns>
        public static List<K3ComponentMatch> FindComponentsByK3Id(GameObject prefabRoot, uint k3Id)
        {
            var matches = new List<K3ComponentMatch>();
            int index = 0;

            // 直接遍历预制体中所有IK3Component组件，检查ID
            // 参考：FIndK3UIWithIDEditorWindow.cs 的实现方式
            IK3Component[] allComps = prefabRoot.GetComponentsInChildren<IK3Component>(true);
            foreach (IK3Component comp in allComps)
            {
                try
                {
                    // 直接访问comp.ID（参考FIndK3UIWithIDEditorWindow.cs的实现）
                    // 某些K3组件可能因序列化字段问题导致异常，需要捕获
                    uint compId = comp.ID;
                    
                    if (compId == k3Id)
                    {
                        GameObject compGameObject = GetGameObjectFromComponent(comp);
                        GameObject container = FindContainerGameObject(compGameObject, prefabRoot);
                        string containerType = DetermineContainerType(container);

                        matches.Add(new K3ComponentMatch
                        {
                            index = index++,
                            component = comp,
                            container = container,
                            gameObject = compGameObject,
                            containerType = containerType
                        });
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[K3ComponentFinder] 检查K3组件时出错 (K3ID={k3Id}, Type={comp.GetType().Name}): {ex.Message}");
                    // 跳过这个组件，继续检查下一个
                    continue;
                }
            }

            return matches;
        }

        /// <summary>
        /// 查找组件所属的容器GameObject（K3DialogEx或K3Panel）
        /// </summary>
        private static GameObject FindContainerGameObject(GameObject componentGameObject, GameObject prefabRoot)
        {
            if (componentGameObject == null) return null;

            Transform current = componentGameObject.transform;
            while (current != null && current != prefabRoot.transform)
            {
                // 检查当前GameObject是否为容器类型
                if (current.GetComponent<K3DialogEx>() != null || current.GetComponent<K3Panel>() != null)
                {
                    return current.gameObject;
                }
                current = current.parent;
            }

            // 如果没有找到容器，返回预制体根节点
            return prefabRoot;
        }

        /// <summary>
        /// 确定容器类型
        /// </summary>
        private static string DetermineContainerType(GameObject container)
        {
            if (container == null) return "Unknown";

            if (container.GetComponent<K3DialogEx>() != null) return "K3DialogEx";
            if (container.GetComponent<K3Panel>() != null) return "K3Panel";

            return "Unknown";
        }

        /// <summary>
        /// 通过K3ID查找所有匹配的K3组件，并按组件类型过滤
        /// </summary>
        /// <param name="prefabRoot">预制体根GameObject</param>
        /// <param name="k3Id">K3组件ID</param>
        /// <param name="componentTypeFilters">组件类型过滤器（如"K3Button", "K3Label"）</param>
        /// <returns>所有匹配且类型符合的K3组件列表</returns>
        public static List<K3ComponentMatch> FindComponentsByK3Id(GameObject prefabRoot, uint k3Id, string[] componentTypeFilters)
        {
            var allMatches = FindComponentsByK3Id(prefabRoot, k3Id);

            // 如果没有过滤器，返回所有匹配项
            if (componentTypeFilters == null || componentTypeFilters.Length == 0)
            {
                return allMatches;
            }

            // 按类型过滤
            var filteredMatches = allMatches.Where(match =>
            {
                string componentTypeName = match.component.GetType().Name;
                return componentTypeFilters.Contains(componentTypeName);
            }).ToList();

            return filteredMatches;
        }

        /// <summary>
        /// 通过K3ID和索引精确定位单个K3组件
        /// </summary>
        /// <param name="prefabRoot">预制体根GameObject</param>
        /// <param name="k3Id">K3组件ID</param>
        /// <param name="index">索引（从0开始）</param>
        /// <returns>匹配的K3组件，如果未找到或索引超出范围则返回null</returns>
        public static K3ComponentMatch FindComponentByK3IdAndIndex(GameObject prefabRoot, uint k3Id, int index)
        {
            var matches = FindComponentsByK3Id(prefabRoot, k3Id);

            if (index < 0 || index >= matches.Count)
            {
                return null;
            }

            return matches[index];
        }

        /// <summary>
        /// 从IK3Component获取对应的GameObject
        /// 优先使用Component.gameObject避免访问可能未赋值的recttransform
        /// </summary>
        private static GameObject GetGameObjectFromComponent(IK3Component component)
        {
            if (component == null) return null;

            // 优先将component转换为Component获取GameObject(避免访问recttransform getter)
            if (component is Component unityComponent)
            {
                return unityComponent.gameObject;
            }

            // 备用方案: 尝试通过recttransform获取GameObject
            try
            {
                if (component.recttransform != null)
                {
                    return component.recttransform.gameObject;
                }
            }
            catch (System.Exception)
            {
                // recttransform getter可能因字段未赋值而抛异常,忽略
            }

            return null;
        }
    }
}
