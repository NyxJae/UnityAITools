using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace AgentCommands.Core
{
    /// <summary>
    /// 命令插件加载器.
    /// 负责扫描程序集,自动发现并加载所有带[CommandPlugin]特性的命令插件.
    /// 采用异常隔离机制,单个插件失败不影响其他插件加载.
    /// </summary>
    public static class CommandPluginLoader
    {
        /// <summary>
        /// 插件加载日志前缀.
        /// </summary>
        private const string LOG_PREFIX = "[AgentCommands.PluginLoader]";

        /// <summary>
        /// 已加载的插件实例列表(用于Shutdown时调用).
        /// </summary>
        private static readonly List<ICommandPlugin> _loadedPlugins = new List<ICommandPlugin>();

        /// <summary>
        /// 插件加载结果(最新一次).
        /// </summary>
        private static PluginLoadResult _lastLoadResult;

        /// <summary>
        /// 获取最新一次的插件加载结果.
        /// </summary>
        public static PluginLoadResult LastLoadResult => _lastLoadResult;

        /// <summary>
        /// 加载所有命令插件.
        /// 此方法会先加载核心命令,然后扫描并加载所有插件.
        /// </summary>
        /// <returns>插件加载结果.</returns>
        public static PluginLoadResult LoadAllPlugins()
        {
            var result = new PluginLoadResult
            {
                LoadStartTime = DateTime.Now
            };

            try
            {
                // 步骤1: 先加载核心命令(硬编码,永不失败)
                LoadCoreCommands(result);

                // 如果核心命令加载失败,直接返回
                if (result.CriticalFailure)
                {
                    result.LoadEndTime = DateTime.Now;
                    _lastLoadResult = result;
                    return result;
                }

                // 步骤2: 扫描并加载所有插件
                LoadDiscoveredPlugins(result);

                // 步骤3: 检查框架可用性
                result.IsFrameworkFunctional = CommandHandlerRegistry.Instance.IsRegistered("log.query");

                result.LoadEndTime = DateTime.Now;
                _lastLoadResult = result;

                // 记录加载结果
                LogLoadResult(result);
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LOG_PREFIX} 插件加载过程中发生未捕获异常: {ex}");
                result.CriticalFailure = true;
                result.IsFrameworkFunctional = false;
                result.LoadEndTime = DateTime.Now;
                _lastLoadResult = result;
            }

            return result;
        }

        /// <summary>
        /// 加载核心命令(硬编码注册,不使用反射).
        /// </summary>
        /// <param name="result">插件加载结果.</param>
        private static void LoadCoreCommands(PluginLoadResult result)
        {
            try
            {
                CoreCommandsLoader.RegisterCoreCommands(CommandHandlerRegistry.Instance);
                result.CoreCommandsLoaded = true;
                Debug.Log($"{LOG_PREFIX} 核心命令加载成功");
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LOG_PREFIX} CRITICAL: 核心命令加载失败: {ex}");
                result.CriticalFailure = true;
                result.CoreCommandsLoaded = false;
                result.FailedPlugins.Add("CoreCommands", ex.Message);
            }
        }

        /// <summary>
        /// 扫描并加载所有发现的插件.
        /// </summary>
        /// <param name="result">插件加载结果.</param>
        private static void LoadDiscoveredPlugins(PluginLoadResult result)
        {
            // 扫描所有程序集中的插件类型
            var pluginTypes = ScanPluginTypes();

            if (pluginTypes.Count == 0)
            {
                Debug.LogWarning($"{LOG_PREFIX} 未发现任何插件类");
                return;
            }

            Debug.Log($"{LOG_PREFIX} 发现 {pluginTypes.Count} 个插件类");

            // 按优先级排序
            var sortedPlugins = pluginTypes
                .OrderBy(t => GetPluginPriority(t))
                .ToList();

            // 逐个加载插件
            foreach (var pluginType in sortedPlugins)
            {
                LoadSinglePlugin(pluginType, result);
            }
        }

        /// <summary>
        /// 扫描所有程序集,查找带[CommandPlugin]特性的类型.
        /// </summary>
        /// <returns>插件类型列表.</returns>
        private static List<Type> ScanPluginTypes()
        {
            var pluginTypes = new List<Type>();

            try
            {
                // 获取所有程序集
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();

                foreach (var assembly in assemblies)
                {
                    try
                    {
                        // 跳过系统程序集
                        if (IsSystemAssembly(assembly))
                        {
                            continue;
                        }

                        // 查找带[CommandPlugin]特性的类型
                        var types = assembly.GetTypes()
                            .Where(t => t.IsClass && !t.IsAbstract && t.GetCustomAttribute<CommandPluginAttribute>() != null);

                        pluginTypes.AddRange(types);
                    }
                    catch (Exception ex)
                    {
                        // 跳过无法加载的程序集
                        Debug.LogWarning($"{LOG_PREFIX} 扫描程序集 {assembly.GetName().Name} 时出错: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LOG_PREFIX} 扫描程序集时发生错误: {ex}");
            }

            return pluginTypes;
        }

        /// <summary>
        /// 判断是否为系统程序集.
        /// </summary>
        /// <param name="assembly">程序集.</param>
        /// <returns>是否为系统程序集.</returns>
        private static bool IsSystemAssembly(Assembly assembly)
        {
            var name = assembly.GetName().Name;

            // 跳过Unity系统程序集和.NET系统程序集
            return name.StartsWith("Unity") ||
                   name.StartsWith("UnityEngine") ||
                   name.StartsWith("UnityEditor") ||
                   name.StartsWith("System") ||
                   name.StartsWith("Microsoft") ||
                   name.StartsWith("mscorlib") ||
                   name.StartsWith("netstandard");
        }

        /// <summary>
        /// 获取插件的优先级.
        /// </summary>
        /// <param name="pluginType">插件类型.</param>
        /// <returns>优先级数值.</returns>
        private static int GetPluginPriority(Type pluginType)
        {
            var attribute = pluginType.GetCustomAttribute<CommandPluginAttribute>();
            return attribute?.Priority ?? 100;
        }

        /// <summary>
        /// 加载单个插件.
        /// </summary>
        /// <param name="pluginType">插件类型.</param>
        /// <param name="result">插件加载结果.</param>
        private static void LoadSinglePlugin(Type pluginType, PluginLoadResult result)
        {
            ICommandPlugin plugin = null;

            try
            {
                // 实例化插件
                plugin = Activator.CreateInstance(pluginType) as ICommandPlugin;

                if (plugin == null)
                {
                    result.FailedPlugins.Add(pluginType.Name, "无法实例化为ICommandPlugin接口");
                    Debug.LogWarning($"{LOG_PREFIX} 插件 {pluginType.Name} 实例化失败");
                    return;
                }

                // 获取插件优先级
                var attribute = pluginType.GetCustomAttribute<CommandPluginAttribute>();
                int priority = attribute?.Priority ?? 100;

                // 记录插件优先级
                result.PluginPriorities[plugin.Name] = priority;

                // 记录注册前的命令列表
                var commandsBefore = CommandHandlerRegistry.Instance.GetRegisteredTypes().ToHashSet();

                // 注册命令处理器
                plugin.RegisterHandlers(CommandHandlerRegistry.Instance);

                // 记录注册后的命令列表,计算差异
                var commandsAfter = CommandHandlerRegistry.Instance.GetRegisteredTypes();
                var newCommands = commandsAfter.Except(commandsBefore).ToList();

                // 记录插件注册的命令清单
                result.PluginCommands[plugin.Name] = newCommands;

                // 初始化插件
                plugin.Initialize();

                // 添加到已加载列表
                _loadedPlugins.Add(plugin);

                // 记录成功
                result.SuccessfulPlugins.Add(plugin.Name);
                
                // Log插件特殊说明
                if (plugin.Name == "Log")
                {
                    Debug.Log($"{LOG_PREFIX} 插件 {plugin.Name} 加载成功 (log.query命令已在CoreCommandsLoader中以priority 0注册)");
                }
                else
                {
                    Debug.Log($"{LOG_PREFIX} 插件 {plugin.Name} 加载成功, 注册了 {newCommands.Count} 个命令");
                }
            }
            catch (Exception ex)
            {
                // 记录失败,但不中断其他插件加载
                // 使用 plugin.Name 而不是 pluginType.Name,保持与 PluginCommands key 一致
                string pluginKey = plugin?.Name ?? pluginType.Name;
                result.FailedPlugins[pluginKey] = ex.Message;
                Debug.LogWarning($"{LOG_PREFIX} 插件 {pluginKey} 加载失败: {ex.Message}");

                // 如果插件已经部分加载,尝试清理
                try
                {
                    plugin?.Shutdown();
                }
                catch
                {
                    // 忽略清理时的异常
                }
            }
        }

        /// <summary>
        /// 记录插件加载结果.
        /// </summary>
        /// <param name="result">插件加载结果.</param>
        private static void LogLoadResult(PluginLoadResult result)
        {
            if (result.CriticalFailure)
            {
                Debug.LogError($"{LOG_PREFIX} {result.GetStatusSummary()}");
                return;
            }

            if (result.FailedPlugins.Count > 0)
            {
                Debug.LogWarning($"{LOG_PREFIX} {result.GetStatusSummary()}");
                foreach (var failed in result.FailedPlugins)
                {
                    Debug.LogWarning($"{LOG_PREFIX}   - {failed.Key}: {failed.Value}");
                }
            }
            else
            {
                Debug.Log($"{LOG_PREFIX} {result.GetStatusSummary()}");
            }

            Debug.Log($"{LOG_PREFIX} 加载耗时: {result.LoadDurationMs:F2}ms");
        }

        /// <summary>
        /// 关闭所有已加载的插件.
        /// 在域重载或Unity退出时调用.
        /// </summary>
        public static void ShutdownAllPlugins()
        {
            foreach (var plugin in _loadedPlugins)
            {
                try
                {
                    plugin.Shutdown();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"{LOG_PREFIX} 关闭插件 {plugin.Name} 时出错: {ex.Message}");
                }
            }

            _loadedPlugins.Clear();
        }
    }
}
