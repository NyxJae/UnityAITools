using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace UnityAgentSkills.Plugins.EditorAction.Catalog
{
    /// <summary>
    /// EditorAction 动作目录.
    /// 负责扫描并维护 actionId 到方法描述的只读索引.
    /// </summary>
    internal static class EditorActionCatalog
    {
        private const string LogPrefix = "[UnityAgentSkills][EditorActionCatalog]";

        private static readonly object SyncRoot = new object();

        private static Dictionary<string, EditorActionDescriptor> _catalog =
            new Dictionary<string, EditorActionDescriptor>(StringComparer.Ordinal);

        /// <summary>
        /// 初始化动作目录.
        /// </summary>
        public static void Initialize()
        {
            Rebuild();
        }

        /// <summary>
        /// 清空目录缓存.
        /// </summary>
        public static void Clear()
        {
            lock (SyncRoot)
            {
                _catalog = new Dictionary<string, EditorActionDescriptor>(StringComparer.Ordinal);
            }
        }

        /// <summary>
        /// 重新扫描并重建动作目录.
        /// </summary>
        public static void Rebuild()
        {
            var next = new Dictionary<string, EditorActionDescriptor>(StringComparer.Ordinal);

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!IsEditorCandidateAssembly(assembly))
                {
                    continue;
                }

                foreach (Type type in SafeGetTypes(assembly))
                {
                    if (type == null || !type.IsClass)
                    {
                        continue;
                    }

                    MethodInfo[] methods;
                    try
                    {
                        methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static);
                    }
                    catch
                    {
                        continue;
                    }

                    for (int i = 0; i < methods.Length; i++)
                    {
                        MethodInfo method = methods[i];
                        if (!IsExposedMethod(method))
                        {
                            continue;
                        }

                        string actionId = BuildActionId(type, method);
                        if (string.IsNullOrEmpty(actionId))
                        {
                            continue;
                        }

                        if (next.ContainsKey(actionId))
                        {
                            Debug.LogWarning($"{LogPrefix} Duplicate actionId skipped: {actionId}");
                            continue;
                        }

                        next[actionId] = new EditorActionDescriptor
                        {
                            ActionId = actionId,
                            Method = method,
                            Parameters = method.GetParameters()
                        };
                    }
                }
            }

            lock (SyncRoot)
            {
                _catalog = next;
            }

            Debug.Log($"{LogPrefix} Catalog rebuilt, action count: {_catalog.Count}");
        }

        /// <summary>
        /// 查询动作描述.
        /// </summary>
        /// <param name="actionId">动作标识.</param>
        /// <param name="descriptor">动作描述.</param>
        /// <returns>是否命中.</returns>
        public static bool TryGet(string actionId, out EditorActionDescriptor descriptor)
        {
            descriptor = null;
            if (string.IsNullOrEmpty(actionId))
            {
                return false;
            }

            lock (SyncRoot)
            {
                return _catalog.TryGetValue(actionId, out descriptor);
            }
        }

        /// <summary>
        /// 返回当前动作标识列表快照.
        /// </summary>
        /// <returns>actionId 列表.</returns>
        public static string[] GetActionIds()
        {
            lock (SyncRoot)
            {
                return _catalog.Keys.OrderBy(static k => k, StringComparer.Ordinal).ToArray();
            }
        }

        private static bool IsEditorCandidateAssembly(Assembly assembly)
        {
            if (assembly == null || assembly.IsDynamic)
            {
                return false;
            }

            string name = assembly.GetName().Name;
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            if (name.StartsWith("Unity", StringComparison.Ordinal) ||
                name.StartsWith("System", StringComparison.Ordinal) ||
                name.StartsWith("Microsoft", StringComparison.Ordinal) ||
                name.StartsWith("mscorlib", StringComparison.Ordinal) ||
                name.StartsWith("netstandard", StringComparison.Ordinal))
            {
                return false;
            }

            return name.IndexOf("Editor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("UnityAgentSkills", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(static t => t != null);
            }
            catch
            {
                return Array.Empty<Type>();
            }
        }

        private static bool IsExposedMethod(MethodInfo method)
        {
            if (method == null)
            {
                return false;
            }

            if (!method.IsPublic || !method.IsStatic)
            {
                return false;
            }

            if (method.IsAbstract || method.ContainsGenericParameters || method.IsSpecialName)
            {
                return false;
            }

            return true;
        }

        private static string BuildActionId(Type type, MethodInfo method)
        {
            string typeName = type?.FullName;
            if (string.IsNullOrEmpty(typeName))
            {
                return null;
            }

            typeName = typeName.Replace('+', '.');
            return string.Concat(typeName, ".", method.Name);
        }
    }
}
