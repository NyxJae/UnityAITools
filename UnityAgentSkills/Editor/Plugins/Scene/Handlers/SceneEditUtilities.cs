using System;
using System.Collections.Generic;
using System.Linq;
using UnityAgentSkills.Core;
using UnityEngine;

namespace UnityAgentSkills.Plugins.Scene.Handlers
{
    /// <summary>
    /// 场景编辑共享工具.
    /// 统一提供组件类型解析和组件定位能力.
    /// </summary>
    internal static class SceneEditUtilities
    {
        /// <summary>
        /// 解析组件类型,优先全名,再短名.
        /// </summary>
        /// <param name="componentType">组件类型名.</param>
        /// <returns>解析后的组件类型.</returns>
        public static Type ResolveComponentTypeOrThrow(string componentType)
        {
            return ResolveTypeOrThrow(
                componentType,
                type => typeof(Component).IsAssignableFrom(type) && !type.IsAbstract,
                UnityAgentSkillCommandErrorCodes.ComponentTypeNotFound,
                UnityAgentSkillCommandErrorCodes.AmbiguousComponentType);
        }

        /// <summary>
        /// 在目标对象上定位组件.
        /// </summary>
        /// <param name="target">目标对象.</param>
        /// <param name="componentType">组件类型.</param>
        /// <param name="componentIndex">同类型组件索引.</param>
        /// <returns>目标组件.</returns>
        public static Component FindComponentOrThrow(GameObject target, Type componentType, int componentIndex)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (componentType == null)
            {
                throw new ArgumentNullException(nameof(componentType));
            }

            if (componentIndex < 0)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": componentIndex must be >= 0");
            }

            Component[] components = target.GetComponents(componentType);
            if (components == null || components.Length == 0 || componentIndex >= components.Length)
            {
                throw new InvalidOperationException(
                    UnityAgentSkillCommandErrorCodes.ComponentNotFound + ": 组件不存在: " + componentType.FullName + " (componentIndex=" + componentIndex + ")");
            }

            return components[componentIndex];
        }

        /// <summary>
        /// 计算组件在同类型集合内的索引.
        /// </summary>
        /// <param name="target">目标对象.</param>
        /// <param name="component">目标组件.</param>
        /// <returns>同类型组件索引.</returns>
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

            string trimmedTypeName = typeName.Trim();
            Type fullType = FindTypeByFullName(trimmedTypeName, validator);
            if (fullType != null)
            {
                return fullType;
            }

            Type[] shortMatches = FindTypesByShortName(trimmedTypeName, validator);
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
