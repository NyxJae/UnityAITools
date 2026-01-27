using System;
using System.Collections.Generic;
using AgentCommands.Core;
using LitJson2_utf;

namespace AgentCommands
{
    /// <summary>
    /// 批量命令执行器,负责串行执行、超时控制和结果构造.
    /// </summary>
    internal static class BatchCommandExecutor
    {
        /// <summary>
        /// 执行批量命令,返回完整的执行结果.
        /// </summary>
        /// <param name="batchCmd">批量命令.</param>
        /// <param name="onProcessingUpdate">processing状态更新回调(可选).</param>
        /// <returns>批量执行结果.</returns>
        public static BatchResult Execute(BatchPendingCommand batchCmd, Action<BatchResult> onProcessingUpdate = null)
        {
            string batchId = batchCmd.batchId;
            int batchTimeout = batchCmd.timeout ?? AgentCommandsConfig.DefaultBatchTimeoutMs;
            DateTime batchStartTime = DateTime.Now;
            string batchStartedAt = AgentCommandsConfig.FormatTimestamp(batchStartTime);

            // 初始化批量结果
            BatchResult batchResult = new BatchResult
            {
                batchId = batchId,
                status = BatchStatuses.Processing,
                startedAt = batchStartedAt,
                results = new List<BatchCommandResult>(),
                totalCommands = batchCmd.commands.Count,
                successCount = 0,
                failedCount = 0
            };

            // 触发processing状态更新
            onProcessingUpdate?.Invoke(batchResult);

            // 串行执行每个命令
            for (int i = 0; i < batchCmd.commands.Count; i++)
            {
                BatchCommand cmd = batchCmd.commands[i];
                BatchCommandResult cmdResult = new BatchCommandResult
                {
                    id = cmd.id,
                    type = cmd.type,
                    status = AgentCommandStatuses.Error,
                    startedAt = AgentCommandsConfig.FormatTimestamp(DateTime.Now),
                    finishedAt = ""
                };

                // 检查批次超时
                TimeSpan batchElapsed = DateTime.Now - batchStartTime;
                if (batchElapsed.TotalMilliseconds > batchTimeout)
                {
                    // 批次超时,标记当前和后续命令为SKIPPED
                    cmdResult = BuildSkippedResult(cmd, batchTimeout);
                    batchResult.results.Add(cmdResult);
                    batchResult.failedCount++;

                    // 标记剩余命令为SKIPPED
                    for (int j = i + 1; j < batchCmd.commands.Count; j++)
                    {
                        BatchCommand remainingCmd = batchCmd.commands[j];
                        BatchCommandResult skippedResult = BuildSkippedResult(remainingCmd, batchTimeout);
                        batchResult.results.Add(skippedResult);
                        batchResult.failedCount++;
                    }

                    // 批次结束,更新finishedAt
                    batchResult.status = BatchStatuses.Completed;
                    batchResult.finishedAt = AgentCommandsConfig.FormatTimestamp(DateTime.Now);
                    break;
                }

                // 执行单个命令
                DateTime cmdStartTime = DateTime.Now;
                int cmdTimeout = cmd.timeout ?? batchTimeout;

                try
                {
                    // 执行命令
                    JsonData resultData = CommandHandlerRegistry.Execute(cmd.type, cmd.@params);

                    // 检查命令是否超时(即使成功也要检查)
                    TimeSpan cmdElapsed = DateTime.Now - cmdStartTime;
                    if (cmdElapsed.TotalMilliseconds > cmdTimeout)
                    {
                        cmdResult.status = AgentCommandStatuses.Error;
                        cmdResult.finishedAt = AgentCommandsConfig.FormatTimestamp(DateTime.Now);
                        cmdResult.error = BuildTimeoutError(cmdElapsed, cmdTimeout);
                        batchResult.failedCount++;
                    }
                    else
                    {
                        cmdResult.status = AgentCommandStatuses.Success;
                        cmdResult.finishedAt = AgentCommandsConfig.FormatTimestamp(DateTime.Now);
                        cmdResult.result = resultData;
                        batchResult.successCount++;
                    }
                }
                catch (Exception ex)
                {
                    // 命令失败
                    cmdResult.finishedAt = AgentCommandsConfig.FormatTimestamp(DateTime.Now);

                    // 判断是否为超时
                    TimeSpan cmdElapsed = DateTime.Now - cmdStartTime;
                    if (cmdElapsed.TotalMilliseconds > cmdTimeout)
                    {
                        cmdResult.error = BuildTimeoutError(cmdElapsed, cmdTimeout);
                    }
                    else if (ex is InvalidOperationException && ex.Message != null && ex.Message.StartsWith(AgentCommandErrorCodes.InvalidRegex + ":"))
                    {
                        cmdResult.error = new CommandError
                        {
                            code = AgentCommandErrorCodes.InvalidRegex,
                            message = "正则表达式非法,请检查 keyword",
                            detail = ex.Message
                        };
                    }
                    else if (ex is NotSupportedException)
                    {
                        cmdResult.error = new CommandError
                        {
                            code = AgentCommandErrorCodes.UnknownType,
                            message = "未知命令类型: " + cmd.type,
                            detail = ex.Message
                        };
                    }
                    else
                    {
                        cmdResult.error = new CommandError
                        {
                            code = AgentCommandErrorCodes.RuntimeError,
                            message = "命令执行发生异常",
                            detail = "异常详情: " + ex.Message
                        };
                    }
                    batchResult.failedCount++;
                }

                batchResult.results.Add(cmdResult);

                // 更新批次状态(用于外部监控)
                batchResult.finishedAt = AgentCommandsConfig.FormatTimestamp(DateTime.Now);
                onProcessingUpdate?.Invoke(batchResult);
            }

            // 批次完成
            batchResult.status = BatchStatuses.Completed;
            batchResult.finishedAt = AgentCommandsConfig.FormatTimestamp(DateTime.Now);

            return batchResult;
        }

        /// <summary>
        /// 构造批次超时的SKIPPED结果.
        /// </summary>
        /// <param name="cmd">命令.</param>
        /// <param name="batchTimeout">批次超时时间(毫秒).</param>
        /// <returns>SKIPPED结果.</returns>
        private static BatchCommandResult BuildSkippedResult(BatchCommand cmd, int batchTimeout)
        {
            return new BatchCommandResult
            {
                id = cmd.id,
                type = cmd.type,
                status = AgentCommandStatuses.Error,
                startedAt = AgentCommandsConfig.FormatTimestamp(DateTime.Now),
                finishedAt = AgentCommandsConfig.FormatTimestamp(DateTime.Now),
                error = new CommandError
                {
                    code = AgentCommandErrorCodes.Skipped,
                    message = "批次超时,命令未执行",
                    detail = "批次执行时间超过限制 " + batchTimeout + "ms"
                }
            };
        }

        /// <summary>
        /// 构造命令超时错误.
        /// </summary>
        /// <param name="elapsed">已用时间.</param>
        /// <param name="timeout">超时限制(毫秒).</param>
        /// <returns>超时错误对象.</returns>
        private static CommandError BuildTimeoutError(TimeSpan elapsed, int timeout)
        {
            return new CommandError
            {
                code = AgentCommandErrorCodes.Timeout,
                message = "命令执行超时",
                detail = "命令执行时间 " + (int)elapsed.TotalMilliseconds + "ms 超过限制 " + timeout + "ms"
            };
        }
    }
}