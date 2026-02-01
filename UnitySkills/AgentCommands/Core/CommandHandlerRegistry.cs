using System;
using System.Collections.Generic;
using System.Linq;
using LitJson2_utf;

namespace AgentCommands.Core
{
    /// <summary>
    /// 命令处理器注册表,统一管理所有命令类型的处理器.
    /// 采用插件化架构,通过Register方法动态注册命令处理器.
    /// 不再硬编码引用具体的Handler类,实现完全解耦.
    /// </summary>
    public class CommandHandlerRegistry
    {
        /// <summary>
        /// 单例实例.
        /// </summary>
        private static CommandHandlerRegistry _instance;

        /// <summary>
        /// 获取注册表单例实例.
        /// </summary>
        public static CommandHandlerRegistry Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new CommandHandlerRegistry();
                }
                return _instance;
            }
        }

        /// <summary>
        /// 命令类型到处理器的映射表.
        /// </summary>
        private readonly Dictionary<string, Func<JsonData, JsonData>> _handlers;

        /// <summary>
        /// 命令类型到优先级的映射表.
        /// </summary>
        private readonly Dictionary<string, int> _priorities;

        /// <summary>
        /// 私有构造函数,防止外部直接实例化.
        /// </summary>
        private CommandHandlerRegistry()
        {
            _handlers = new Dictionary<string, Func<JsonData, JsonData>>(StringComparer.OrdinalIgnoreCase);
            _priorities = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 注册一个新的命令处理器.
        /// </summary>
        /// <param name="commandType">命令类型标识.</param>
        /// <param name="handler">命令处理函数.</param>
        /// <param name="priority">优先级(数值越小优先级越高),默认100.</param>
        public void Register(string commandType, Func<JsonData, JsonData> handler, int priority = 100)
        {
            if (string.IsNullOrEmpty(commandType))
            {
                throw new ArgumentException("命令类型不能为空", nameof(commandType));
            }
            if (handler == null)
            {
                throw new ArgumentException("处理器不能为null", nameof(handler));
            }

            // 检查是否已注册该命令
            if (_handlers.TryGetValue(commandType, out var existingHandler))
            {
                int existingPriority = _priorities[commandType];
                
                // 只有新注册的优先级更高时才覆盖
                if (priority < existingPriority)
                {
                    _handlers[commandType] = handler;
                    _priorities[commandType] = priority;
                }
                // 否则保持原有注册
            }
            else
            {
                _handlers[commandType] = handler;
                _priorities[commandType] = priority;
            }
        }

        /// <summary>
        /// 根据命令类型获取对应的处理器并执行.
        /// </summary>
        /// <param name="commandType">命令类型.</param>
        /// <param name="parameters">命令参数.</param>
        /// <returns>执行结果.</returns>
        /// <exception cref="NotSupportedException">当命令类型未注册时抛出.</exception>
        public JsonData Execute(string commandType, JsonData parameters)
        {
            if (_handlers.TryGetValue(commandType, out Func<JsonData, JsonData> handler))
            {
                return handler(parameters);
            }

            throw new NotSupportedException(AgentCommandErrorCodes.UnknownType + ": " + commandType);
        }

        /// <summary>
        /// 检查命令类型是否已注册.
        /// </summary>
        /// <param name="commandType">命令类型.</param>
        /// <returns>是否已注册.</returns>
        public bool IsRegistered(string commandType)
        {
            return _handlers.ContainsKey(commandType);
        }

        /// <summary>
        /// 获取所有已注册的命令类型列表(按优先级排序).
        /// </summary>
        /// <returns>已注册的命令类型数组.</returns>
        public string[] GetRegisteredTypes()
        {
            return _handlers.Keys
                .OrderBy(k => _priorities.ContainsKey(k) ? _priorities[k] : 100)
                .ToArray();
        }

        /// <summary>
        /// 获取已注册命令的数量.
        /// </summary>
        public int RegisteredCount => _handlers.Count;

        /// <summary>
        /// 清除所有已注册的命令处理器.
        /// 用于测试或重置.
        /// </summary>
        public void Clear()
        {
            _handlers.Clear();
            _priorities.Clear();
        }
    }
}
