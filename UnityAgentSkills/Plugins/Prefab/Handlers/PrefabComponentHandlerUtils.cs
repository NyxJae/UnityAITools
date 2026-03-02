using System;
using System.Collections.Generic;
using System.Linq;
using UnityAgentSkills.Core;
using UnityAgentSkills.Utils;
using UnityEditor;
using UnityEngine;

namespace UnityAgentSkills.Plugins.Prefab.Handlers
{
    /// <summary>
    /// Prefab组件编辑命令共享工具.
    /// </summary>
    internal static class PrefabComponentHandlerUtils
    {
        /// <summary>
        /// 规范化prefabPath路径分隔符.
        /// </summary>
        public static string NormalizePrefabPath(string prefabPath)
        {
            if (string.IsNullOrWhiteSpace(prefabPath))
            {
                return prefabPath;
            }

            return prefabPath.Replace('\\', '/');
        }

        /// <summary>
        /// 校验prefabPath协议.
        /// </summary>
        public static void ValidatePrefabPathOrThrow(string prefabPath)
        {
            string normalizedPrefabPath = NormalizePrefabPath(prefabPath);
            if (string.IsNullOrWhiteSpace(normalizedPrefabPath))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": prefabPath is required");
            }

            if (!normalizedPrefabPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": prefabPath must start with Assets/");
            }

            if (!normalizedPrefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": prefabPath must end with .prefab");
            }
        }

        /// <summary>
        /// 加载Prefab编辑内容.
        /// </summary>
        public static GameObject LoadPrefabContentsOrThrow(string prefabPath)
        {
            string normalizedPrefabPath = NormalizePrefabPath(prefabPath);
            ValidatePrefabPathOrThrow(normalizedPrefabPath);
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(normalizedPrefabPath);
            if (prefabRoot == null)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.PrefabNotFound + ": 预制体不存在: " + normalizedPrefabPath);
            }

            return prefabRoot;
        }

        /// <summary>
        /// 路径定位GameObject.
        /// </summary>
        public static GameObject FindGameObjectOrThrow(GameObject prefabRoot, string objectPath, int siblingIndex, string pathFieldName)
        {
            if (string.IsNullOrWhiteSpace(objectPath))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": " + pathFieldName + " is required");
            }

            if (siblingIndex < 0)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": siblingIndex must be >= 0");
            }

            GameObject target = GameObjectPathFinder.FindByPath(prefabRoot, objectPath, siblingIndex);
            if (target == null)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.GameObjectNotFound + ": GameObject不存在: " + objectPath + " (siblingIndex=" + siblingIndex + ")");
            }

            return target;
        }

        /// <summary>
        /// 解析组件类型(优先全名,再短名).
        /// </summary>
        public static Type ResolveComponentTypeOrThrow(string componentType)
        {
            return ResolveTypeOrThrow(
                componentType,
                type => typeof(Component).IsAssignableFrom(type) && !type.IsAbstract,
                UnityAgentSkillCommandErrorCodes.ComponentTypeNotFound,
                UnityAgentSkillCommandErrorCodes.AmbiguousComponentType);
        }

        /// <summary>
        /// 解析assetType.
        /// </summary>
        public static Type ResolveAssetTypeOrThrow(string assetType)
        {
            return ResolveTypeOrThrow(
                assetType,
                type => typeof(UnityEngine.Object).IsAssignableFrom(type) && !type.IsAbstract,
                UnityAgentSkillCommandErrorCodes.AssetTypeMismatch,
                UnityAgentSkillCommandErrorCodes.AssetTypeMismatch);
        }

        /// <summary>
        /// 在目标对象上定位组件.
        /// </summary>
        public static Component FindComponentOrThrow(GameObject target, Type componentType, int componentIndex)
        {
            if (componentIndex < 0)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": componentIndex must be >= 0");
            }

            Component[] components = target.GetComponents(componentType);
            if (components == null || components.Length == 0 || componentIndex >= components.Length)
            {
                throw new InvalidOperationException(UnityAgentSkillCommandErrorCodes.ComponentNotFound + ": 组件不存在: " + componentType.FullName + " (componentIndex=" + componentIndex + ")");
            }

            return components[componentIndex];
        }

        /// <summary>
        /// 计算组件在同类型集合内的索引.
        /// </summary>
        public static int GetComponentIndex(GameObject target, Component component)
        {
            if (target == null || component == null)
            {
                return 0;
            }

            Component[] components = target.GetComponents(component.GetType());
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == component)
                {
                    return i;
                }
            }

            return 0;
        }

        private static Type ResolveTypeOrThrow(string typeName, Func<Type, bool> validator, string notFoundCode, string ambiguousCode)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": componentType is required");
            }

            Type fullType = FindTypeByFullName(typeName.Trim(), validator);
            if (fullType != null)
            {
                return fullType;
            }

            Type[] shortMatches = FindTypesByShortName(typeName.Trim(), validator);
            if (shortMatches.Length == 1)
            {
                return shortMatches[0];
            }

            if (shortMatches.Length > 1)
            {
                throw new InvalidOperationException(ambiguousCode + ": 类型名存在歧义: " + typeName);
            }

            throw new InvalidOperationException(notFoundCode + ": 类型不存在: " + typeName);
        }

        private static Type FindTypeByFullName(string fullName, Func<Type, bool> validator)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null && validator(type))
                {
                    return type;
                }
            }

            return null;
        }

        private static Type[] FindTypesByShortName(string shortName, Func<Type, bool> validator)
        {
            var matches = new List<Type>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch
                {
                    continue;
                }

                matches.AddRange(types.Where(type => type != null && type.Name == shortName && validator(type)));
            }

            return matches.Distinct().ToArray();
        }
    }
}
