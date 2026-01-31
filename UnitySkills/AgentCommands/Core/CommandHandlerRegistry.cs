using System;
using System.Collections.Generic;
using AgentCommands.Handlers;
using AgentCommands.K3Prefab.Handlers;
using LitJson2_utf;

namespace AgentCommands.Core
{
    /// <summary>
    /// 命令处理器注册表,统一管理所有命令类型的处理器.
    /// </summary>
    internal static class CommandHandlerRegistry
    {
        /// <summary>
        /// 命令类型到处理器的映射表.
        /// </summary>
        private static readonly Dictionary<string, Func<JsonData, JsonData>> _handlers;

        /// <summary>
        /// 静态构造函数,注册所有命令处理器.
        /// </summary>
        static CommandHandlerRegistry()
        {
            _handlers = new Dictionary<string, Func<JsonData, JsonData>>(StringComparer.OrdinalIgnoreCase)
            {
                { LogQueryCommandHandler.CommandType, LogQueryCommandHandler.Execute },
                { PrefabQueryHierarchyHandler.CommandType, PrefabQueryHierarchyHandler.Execute },
                { PrefabQueryComponentsHandler.CommandType, PrefabQueryComponentsHandler.Execute },
                { PrefabSetGameObjectPropertiesHandler.CommandType, PrefabSetGameObjectPropertiesHandler.Execute },
                { K3PrefabQueryByK3IdHandler.CommandType, K3PrefabQueryByK3IdHandler.Execute },
                { K3PrefabSetComponentPropertiesHandler.CommandType, K3PrefabSetComponentPropertiesHandler.Execute }
            };
        }

        /// <summary>
        /// 注册一个新的命令处理器.
        /// </summary>
        /// <param name="commandType">命令类型标识.</param>
        /// <param name="handler">命令处理函数.</param>
        public static void Register(string commandType, Func<JsonData, JsonData> handler)
        {
            if (string.IsNullOrEmpty(commandType))
            {
                throw new ArgumentException("命令类型不能为空", nameof(commandType));
            }
            if (handler == null)
            {
                throw new ArgumentException("处理器不能为null", nameof(handler));
            }

            _handlers[commandType] = handler;
        }

        /// <summary>
        /// 根据命令类型获取对应的处理器并执行.
        /// </summary>
        /// <param name="commandType">命令类型.</param>
        /// <param name="parameters">命令参数.</param>
        /// <returns>执行结果.</returns>
        /// <exception cref="NotSupportedException">当命令类型未注册时抛出.</exception>
        public static JsonData Execute(string commandType, JsonData parameters)
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
        public static bool IsRegistered(string commandType)
        {
            return _handlers.ContainsKey(commandType);
        }
    }
}
