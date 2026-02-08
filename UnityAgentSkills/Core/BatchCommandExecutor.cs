using System;
using System.Collections.Generic;
using LitJson2_utf;
using UnityEngine;

namespace UnityAgentSkills.Core
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
            int batchTimeout = batchCmd.timeout ?? UnityAgentSkillsConfig.DefaultBatchTimeoutMs;
            DateTime batchStartTime = DateTime.Now;
            string batchStartedAt = UnityAgentSkillsConfig.FormatTimestamp(batchStartTime);

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

            // 预计算本批次 screenshot 命令数量,用于命名冲突规则(baseName).
            int screenshotCommandCount = 0;
            for (int i = 0; i < batchCmd.commands.Count; i++)
            {
                if (string.Equals(batchCmd.commands[i].type, "log.screenshot", StringComparison.OrdinalIgnoreCase))
                {
                    screenshotCommandCount++;
                }
            }

            // 串行执行每个命令
            for (int i = 0; i < batchCmd.commands.Count; i++)
            {
                BatchCommand cmd = batchCmd.commands[i];
                BatchCommandResult cmdResult = new BatchCommandResult
                {
                    id = cmd.id,
                    type = cmd.type,
                    status = UnityAgentSkillCommandStatuses.Error,
                    startedAt = UnityAgentSkillsConfig.FormatTimestamp(DateTime.Now),
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
                    batchResult.finishedAt = UnityAgentSkillsConfig.FormatTimestamp(DateTime.Now);
                    break;
                }

                // 执行单个命令
                DateTime cmdStartTime = DateTime.Now;
                int cmdTimeout = cmd.timeout ?? batchTimeout;

                try
                {
                    // 执行命令
                    JsonData effectiveParams = cmd.@params;
                    if (string.Equals(cmd.type, "log.screenshot", StringComparison.OrdinalIgnoreCase))
                    {
                        effectiveParams = InjectScreenshotContext(effectiveParams, batchId, cmd.id, screenshotCommandCount);
                    }

                    JsonData resultData = CommandHandlerRegistry.Instance.Execute(cmd.type, effectiveParams);

                    // 检查命令是否超时(即使成功也要检查)
                    TimeSpan cmdElapsed = DateTime.Now - cmdStartTime;
                    if (cmdElapsed.TotalMilliseconds > cmdTimeout)
                    {
                        cmdResult.status = UnityAgentSkillCommandStatuses.Error;
                        cmdResult.finishedAt = UnityAgentSkillsConfig.FormatTimestamp(DateTime.Now);
                        cmdResult.error = BuildTimeoutError(cmdElapsed, cmdTimeout);
                        batchResult.failedCount++;
                    }
                    else
                    {
                        cmdResult.status = UnityAgentSkillCommandStatuses.Success;
                        cmdResult.finishedAt = UnityAgentSkillsConfig.FormatTimestamp(DateTime.Now);
                        cmdResult.result = resultData;
                        batchResult.successCount++;
                    }
                }
                catch (Exception ex)
                {
                    // 命令失败
                    cmdResult.finishedAt = UnityAgentSkillsConfig.FormatTimestamp(DateTime.Now);

                    // 判断是否为超时
                    TimeSpan cmdElapsed = DateTime.Now - cmdStartTime;
                    if (cmdElapsed.TotalMilliseconds > cmdTimeout)
                    {
                        cmdResult.error = BuildTimeoutError(cmdElapsed, cmdTimeout);
                    }
                    // 处理业务侧主动抛出的 TIMEOUT(例如 log.screenshot 等待文件可读超时)
                    else if (ex is ArgumentException && ex.Message != null && ex.Message.StartsWith(UnityAgentSkillCommandErrorCodes.Timeout + ":"))
                    {
                        cmdResult.error = CommandErrorFactory.CreateTimeoutError((int)cmdElapsed.TotalMilliseconds, cmdTimeout);
                    }
                    // 处理参数校验异常(ArgumentException)
                    else if (ex is ArgumentException && ex.Message != null && ex.Message.StartsWith(UnityAgentSkillCommandErrorCodes.InvalidFields + ":"))
                    {
                        // 提取冒号后的详细信息作为detail
                        string detail = ex.Message.Substring(UnityAgentSkillCommandErrorCodes.InvalidFields.Length + 1).Trim();
                        cmdResult.error = CommandErrorFactory.CreateInvalidFieldsError(detail);
                    }
                    // 处理正则表达式异常
                    else if (ex is InvalidOperationException && ex.Message != null && ex.Message.StartsWith(UnityAgentSkillCommandErrorCodes.InvalidRegex + ":"))
                    {
                        // 提取冒号后的详细信息作为detail,去除前缀
                        string detail = ex.Message.Substring(UnityAgentSkillCommandErrorCodes.InvalidRegex.Length + 1).Trim();
                        cmdResult.error = CommandErrorFactory.CreateInvalidRegexError(detail);
                    }
                    // 处理预制体未找到异常
                    else if (ex is InvalidOperationException && ex.Message != null && ex.Message.StartsWith("Prefab not found at path: "))
                    {
                        string path = ex.Message.Substring("Prefab not found at path: ".Length);
                        cmdResult.error = CommandErrorFactory.CreatePrefabNotFoundError(path);
                    }
                    // 处理GameObject未找到异常
                    else if (ex is InvalidOperationException && ex.Message != null && ex.Message.StartsWith("GameObject not found at path: "))
                    {
                        string path = ex.Message.Substring("GameObject not found at path: ".Length);
                        cmdResult.error = CommandErrorFactory.CreateGameObjectNotFoundError(path);
                    }
                    // 处理未知命令类型异常
                    else if (ex is NotSupportedException)
                    {
                        cmdResult.error = CommandErrorFactory.CreateUnknownCommandError(cmd.type, ex.Message);
                    }
                    // 其他运行时错误
                    else
                    {
                        cmdResult.error = CommandErrorFactory.CreateRuntimeError("异常详情: " + ex.Message);
                    }
                    batchResult.failedCount++;
                }

                batchResult.results.Add(cmdResult);

                // 更新批次状态(用于外部监控)
                batchResult.finishedAt = UnityAgentSkillsConfig.FormatTimestamp(DateTime.Now);
                onProcessingUpdate?.Invoke(batchResult);
            }

            // 批次完成
            batchResult.status = BatchStatuses.Completed;
            batchResult.finishedAt = UnityAgentSkillsConfig.FormatTimestamp(DateTime.Now);

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
                status = UnityAgentSkillCommandStatuses.Error,
                startedAt = UnityAgentSkillsConfig.FormatTimestamp(DateTime.Now),
                finishedAt = UnityAgentSkillsConfig.FormatTimestamp(DateTime.Now),
                error = CommandErrorFactory.CreateSkippedError(batchTimeout)
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
            return CommandErrorFactory.CreateTimeoutError((int)elapsed.TotalMilliseconds, timeout);
        }

        /// <summary>
        /// 为 log.screenshot 注入执行上下文(不属于对外协议字段).
        /// </summary>
        private static JsonData InjectScreenshotContext(JsonData rawParams, string batchId, string cmdId, int screenshotCommandCount)
        {
            JsonData data = new JsonData();

            // 深拷贝原始 params,避免注入字段污染输入对象(包括嵌套引用共享).
            if (rawParams != null && rawParams.IsObject)
            {
                try
                {
                    data = JsonMapper.ToObject(rawParams.ToJson());
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[UnityAgentSkills] Deep clone screenshot params failed, fallback to shallow copy. " + ex.Message);

                    // Deep clone 失败时,至少保留顶层业务字段,避免静默丢参导致行为漂移.
                    data = new JsonData();
                    try
                    {
                        foreach (System.Collections.DictionaryEntry entry in (System.Collections.IDictionary)rawParams)
                        {
                            string key = entry.Key as string;
                            if (string.IsNullOrEmpty(key)) continue;
                            data[key] = (JsonData)entry.Value;
                        }
                    }
                    catch (Exception ex2)
                    {
                        Debug.LogWarning("[UnityAgentSkills] Shallow copy screenshot params failed, fallback to empty object. " + ex2.Message);
                        data = new JsonData();
                    }
                }
            }

            data["__batchId"] = batchId ?? "";
            data["__cmdId"] = cmdId ?? "";
            data["__screenshotCommandCount"] = screenshotCommandCount;
            return data;
        }
    }
}