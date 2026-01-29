using AgentCommands.Core;

namespace AgentCommands.Core
{
    /// <summary>
    /// 命令错误工厂,统一创建CommandError对象.
    /// 将错误消息提取到常量中,便于维护和国际化.
    /// </summary>
    internal static class CommandErrorFactory
    {
        /// <summary>
        /// 错误消息常量 - 正则表达式错误.
        /// </summary>
        private const string MessageInvalidRegex = "正则表达式非法,请检查 keyword";

        /// <summary>
        /// 错误消息常量 - 未知命令类型.
        /// </summary>
        private const string MessageUnknownType = "未知命令类型";

        /// <summary>
        /// 错误消息常量 - 参数错误.
        /// </summary>
        private const string MessageInvalidFields = "命令字段缺失或非法";

        /// <summary>
        /// 错误消息常量 - 预制体未找到.
        /// </summary>
        private const string MessagePrefabNotFound = "预制体未找到";

        /// <summary>
        /// 错误消息常量 - GameObject未找到.
        /// </summary>
        private const string MessageGameObjectNotFound = "GameObject未找到";

        /// <summary>
        /// 错误消息常量 - 运行时错误.
        /// </summary>
        private const string MessageRuntimeError = "命令执行发生异常";

        /// <summary>
        /// 错误消息常量 - 命令超时.
        /// </summary>
        private const string MessageTimeout = "命令执行超时";

        /// <summary>
        /// 错误消息常量 - 批次超时跳过.
        /// </summary>
        private const string MessageSkipped = "批次超时,命令未执行";

        /// <summary>
        /// 创建正则表达式错误.
        /// </summary>
        /// <param name="detail">错误详情.</param>
        /// <returns>CommandError对象.</returns>
        public static CommandError CreateInvalidRegexError(string detail)
        {
            return new CommandError
            {
                code = AgentCommandErrorCodes.InvalidRegex,
                message = MessageInvalidRegex,
                detail = detail
            };
        }

        /// <summary>
        /// 创建未知命令类型错误.
        /// </summary>
        /// <param name="commandType">命令类型.</param>
        /// <param name="detail">错误详情.</param>
        /// <returns>CommandError对象.</returns>
        public static CommandError CreateUnknownCommandError(string commandType, string detail)
        {
            return new CommandError
            {
                code = AgentCommandErrorCodes.UnknownType,
                message = MessageUnknownType + ": " + (commandType ?? ""),
                detail = detail
            };
        }

        /// <summary>
        /// 创建参数错误.
        /// </summary>
        /// <param name="detail">错误详情.</param>
        /// <returns>CommandError对象.</returns>
        public static CommandError CreateInvalidFieldsError(string detail)
        {
            return new CommandError
            {
                code = AgentCommandErrorCodes.InvalidFields,
                message = MessageInvalidFields,
                detail = detail
            };
        }

        /// <summary>
        /// 创建预制体未找到错误.
        /// </summary>
        /// <param name="prefabPath">预制体路径.</param>
        /// <returns>CommandError对象.</returns>
        public static CommandError CreatePrefabNotFoundError(string prefabPath)
        {
            return new CommandError
            {
                code = AgentCommandErrorCodes.RuntimeError,
                message = MessagePrefabNotFound,
                detail = "Prefab not found at path: " + (prefabPath ?? "")
            };
        }

        /// <summary>
        /// 创建GameObject未找到错误.
        /// </summary>
        /// <param name="objectPath">GameObject路径.</param>
        /// <returns>CommandError对象.</returns>
        public static CommandError CreateGameObjectNotFoundError(string objectPath)
        {
            return new CommandError
            {
                code = AgentCommandErrorCodes.RuntimeError,
                message = MessageGameObjectNotFound,
                detail = "GameObject not found at path: " + (objectPath ?? "")
            };
        }

        /// <summary>
        /// 创建运行时错误.
        /// </summary>
        /// <param name="detail">错误详情.</param>
        /// <returns>CommandError对象.</returns>
        public static CommandError CreateRuntimeError(string detail)
        {
            return new CommandError
            {
                code = AgentCommandErrorCodes.RuntimeError,
                message = MessageRuntimeError,
                detail = detail
            };
        }

        /// <summary>
        /// 创建命令超时错误.
        /// </summary>
        /// <param name="elapsedMs">已用时间(毫秒).</param>
        /// <param name="timeoutMs">超时限制(毫秒).</param>
        /// <returns>CommandError对象.</returns>
        public static CommandError CreateTimeoutError(int elapsedMs, int timeoutMs)
        {
            return new CommandError
            {
                code = AgentCommandErrorCodes.Timeout,
                message = MessageTimeout,
                detail = "命令执行时间 " + elapsedMs + "ms 超过限制 " + timeoutMs + "ms"
            };
        }

        /// <summary>
        /// 创建批次超时跳过错误.
        /// </summary>
        /// <param name="batchTimeoutMs">批次超时时间(毫秒).</param>
        /// <returns>CommandError对象.</returns>
        public static CommandError CreateSkippedError(int batchTimeoutMs)
        {
            return new CommandError
            {
                code = AgentCommandErrorCodes.Skipped,
                message = MessageSkipped,
                detail = "批次执行时间超过限制 " + batchTimeoutMs + "ms"
            };
        }
    }
}
