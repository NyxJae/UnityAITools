using System;
using System.Collections.Generic;
using System.IO;
using LitJson2_utf;
using UnityAgentSkills.Core;
using UnityEditor;
using UnityEngine;

namespace UnityAgentSkills
{
    /// <summary>
    /// UnityAgentSkills编辑器插件入口.
    /// </summary>
    [InitializeOnLoad]
    internal static class UnityAgentSkillsPlugin
    {
        private static readonly UnityAgentSkills.Internal.PendingQueue _pendingQueue = new UnityAgentSkills.Internal.PendingQueue();
        private static UnityAgentSkills.Internal.PendingWatcher _pendingWatcher;

        /// <summary>
        /// 是否正在处理命令.
        /// </summary>
        private static bool _isProcessing;

        /// <summary>
        /// 下次全量扫描的编辑器时间.
        /// </summary>
        private static double _nextRescanTime;

        /// <summary>
        /// 是否已挂载关闭钩子.
        /// </summary>
        private static bool _shutdownHooked;

        /// <summary>
        /// SessionState键: 标记当前Unity会话是否已执行过pending初始化清理.
        /// </summary>
        private const string PendingPurgeSessionKey = "UnityAgentSkills.PendingPurgedOnSessionStartup";

        /// <summary>
        /// 静态构造函数,编辑器加载时自动初始化插件.
        /// </summary>
        static UnityAgentSkillsPlugin()
        {
            Initialize();
        }

        /// <summary>
        /// 初始化监听器与缓存.
        /// </summary>
        private static void Initialize()
        {
            SetupShutdownHooks();

            // 域重载时确保回调不重复注册.
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;

            StopPendingWatcher();
            ResetRuntimeState();

            EnsureDirectories();
            if (ShouldPurgePendingOnSessionStartup())
            {
                int purgedCount = PurgePendingCommandFiles();
                if (purgedCount > 0)
                {
                    Debug.Log($"[UnityAgentSkills] 会话首次初始化已清理pending命令文件: {purgedCount}");
                }
            }

            LogCache.Initialize();

            // 每次初始化前清空注册表,避免热重启后残留旧处理器.
            CommandHandlerRegistry.Instance.Clear();

            // 加载所有命令插件
            LoadCommandPlugins();

            StartPendingWatcher();
            EnqueueAllPendingFiles();

            _nextRescanTime = EditorApplication.timeSinceStartup + UnityAgentSkillsConfig.PendingRescanIntervalSeconds;
        }

        /// <summary>
        /// 判断是否应在当前Unity会话启动阶段清理pending目录.
        /// </summary>
        private static bool ShouldPurgePendingOnSessionStartup()
        {
            if (SessionState.GetBool(PendingPurgeSessionKey, false))
            {
                return false;
            }

            SessionState.SetBool(PendingPurgeSessionKey, true);
            return true;
        }

        /// <summary>
        /// 加载所有命令插件.
        /// </summary>
        private static void LoadCommandPlugins()
        {
            try
            {
                var result = CommandPluginLoader.LoadAllPlugins();

                if (result.CriticalFailure)
                {
                    Debug.LogError("[UnityAgentSkills] CRITICAL: 核心命令加载失败,框架不可用!");
                }
                else if (!result.IsFrameworkFunctional)
                {
                    Debug.LogWarning("[UnityAgentSkills] WARNING: 框架不可用,核心命令未正确注册");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnityAgentSkills] 插件加载过程中发生异常: {ex}");
            }
        }

        /// <summary>
        /// 注册域重载与退出时的释放钩子.
        /// </summary>
        private static void SetupShutdownHooks()
        {
            if (_shutdownHooked) return;

            AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;

            EditorApplication.quitting -= Shutdown;
            EditorApplication.quitting += Shutdown;

            _shutdownHooked = true;
        }

        /// <summary>
        /// 释放回调与文件监听.
        /// </summary>
        private static void Shutdown()
        {
            EditorApplication.update -= OnEditorUpdate;
            StopPendingWatcher();
            ResetRuntimeState();
            LogCache.Shutdown();
            CommandPluginLoader.ShutdownAllPlugins();
        }

        /// <summary>
        /// 重启命令服务.
        /// </summary>
        /// <param name="purgePendingFirst">是否在重启前清理pending目录.</param>
        /// <returns>清理的pending文件数量.</returns>
        internal static int RestartCommandService(bool purgePendingFirst)
        {
            StopPendingWatcher();
            ResetRuntimeState();

            int purgedCount = 0;
            if (purgePendingFirst)
            {
                EnsureDirectories();
                purgedCount = PurgePendingCommandFiles();
            }

            LogCache.Initialize();
            CommandHandlerRegistry.Instance.Clear();
            CommandPluginLoader.ShutdownAllPlugins();
            LoadCommandPlugins();

            StartPendingWatcher();
            EnqueueAllPendingFiles();
            _nextRescanTime = EditorApplication.timeSinceStartup + UnityAgentSkillsConfig.PendingRescanIntervalSeconds;

            return purgedCount;
        }

        /// <summary>
        /// 重置运行时状态,避免历史会话在重启后继续推进.
        /// </summary>
        private static void ResetRuntimeState()
        {
            _activeSession = null;
            _isProcessing = false;
            _pendingQueue.Clear();
        }

        /// <summary>
        /// 清理pending目录下尚未处理的命令文件.
        /// </summary>
        /// <returns>已删除的文件数量.</returns>
        private static int PurgePendingCommandFiles()
        {
            string pendingDir = UnityAgentSkillsConfig.PendingDirAbsolutePath;
            if (!Directory.Exists(pendingDir))
            {
                return 0;
            }

            string[] files = Directory.GetFiles(pendingDir, "*.json", SearchOption.TopDirectoryOnly);
            int deletedCount = 0;
            for (int i = 0; i < files.Length; i++)
            {
                string filePath = files[i];
                try
                {
                    File.Delete(filePath);
                    deletedCount++;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[UnityAgentSkills] 清理pending文件失败: {filePath}. {ex.Message}");
                }
            }

            return deletedCount;
        }

        /// <summary>
        /// 确保命令数据目录存在.
        /// </summary>
        private static void EnsureDirectories()
        {
            Directory.CreateDirectory(UnityAgentSkillsConfig.DataDirAbsolutePath);
            Directory.CreateDirectory(UnityAgentSkillsConfig.PendingDirAbsolutePath);
            Directory.CreateDirectory(UnityAgentSkillsConfig.ResultsDirAbsolutePath);
            Directory.CreateDirectory(UnityAgentSkillsConfig.DoneDirAbsolutePath);
        }

        private static void StartPendingWatcher()
        {
            if (_pendingWatcher == null) _pendingWatcher = new UnityAgentSkills.Internal.PendingWatcher();

            _pendingWatcher.Start(UnityAgentSkillsConfig.PendingDirAbsolutePath, fullPath =>
            {
                // watcher 回调可能来自非主线程,不得触碰 Unity API.
                _pendingQueue.TryEnqueuePendingFile(fullPath);
            });
        }

        private static void StopPendingWatcher()
        {
            if (_pendingWatcher == null) return;

            try
            {
                _pendingWatcher.Stop();
            }
            catch
            {
                // watcher 释放阶段允许失败,避免关闭流程被异常打断.
            }

            _pendingWatcher = null;
        }

        /// <summary>
        /// 扫描pending目录并加入队列.
        /// </summary>
        private static void EnqueueAllPendingFiles()
        {
            _pendingQueue.EnqueueAllPendingFiles(UnityAgentSkillsConfig.PendingDirAbsolutePath);
        }

        /// <summary>
        /// 当前正在执行的批次会话(跨帧推进,避免阻塞 Editor 主线程).
        /// </summary>
        private static BatchExecutionSession _activeSession;

        /// <summary>
        /// Editor循环处理命令队列.
        /// </summary>
        private static void OnEditorUpdate()
        {
            // 1) 优先推进当前会话(非阻塞).
            if (_activeSession != null)
            {
                try
                {
                    _activeSession.Tick();
                }
                catch (Exception ex)
                {
                    // 防止 Tick 未捕获异常导致每帧报错并卡死在活跃会话.
                    try
                    {
                        _activeSession.FailFatal(ex);
                    }
                    catch
                    {
                        // 忽略二次失败.
                    }

                    _activeSession = null;
                    _isProcessing = false;
                }

                if (_activeSession != null && _activeSession.IsDone)
                {
                    _activeSession = null;
                    _isProcessing = false;
                }
                return;
            }

            // 2) 双保险:定时扫描 pending.
            double now = EditorApplication.timeSinceStartup;
            if (now >= _nextRescanTime)
            {
                _nextRescanTime = now + UnityAgentSkillsConfig.PendingRescanIntervalSeconds;
                EnqueueAllPendingFiles();
            }

            if (_isProcessing) return;

            UnityAgentSkills.Internal.PendingQueue.PendingItem item;
            if (!_pendingQueue.TryDequeueReady(now, out item)) return;

            // 开始新的批次会话.
            ProcessPendingFile(item);
        }

        /// <summary>
        /// 批次跨帧执行会话.
        /// 目的: 支持 log.screenshot 等需要等待落盘但不能阻塞主线程的命令.
        /// </summary>
        private sealed class BatchExecutionSession
        {
            private readonly UnityAgentSkills.Internal.PendingQueue.PendingItem _item;
            private readonly BatchPendingCommand _batchCmd;
            private readonly BatchResult _batchResult;
            private readonly DateTime _batchStartTime;
            private readonly int _batchTimeoutMs;

            public string BatchId => _batchCmd != null ? (_batchCmd.batchId ?? "") : "";

            private int _cmdIndex;
            private DateTime _currentCmdStartTime;
            private int _currentCmdTimeoutMs;
            private bool _waitingScreenshot;
            private UnityAgentSkills.Plugins.Log.Handlers.LogScreenshotCommandHandler.ScreenshotJob _screenshotJob;
            private DateTime _screenshotCaptureStartedAt;
            private bool _waitingPlayModeWaitFor;
            private UnityAgentSkills.Plugins.PlayMode.Handlers.PlayModeWaitForHandler.WaitForJob _waitForJob;

            public bool IsDone { get; private set; }

            public BatchExecutionSession(UnityAgentSkills.Internal.PendingQueue.PendingItem item, BatchPendingCommand batchCmd)
            {
                _item = item;
                _batchCmd = batchCmd;

                _batchStartTime = DateTime.Now;
                _batchTimeoutMs = batchCmd.timeout ?? UnityAgentSkillsConfig.DefaultBatchTimeoutMs;

                _batchResult = new BatchResult
                {
                    batchId = batchCmd.batchId,
                    status = BatchStatuses.Processing,
                    startedAt = UnityAgentSkillsConfig.FormatTimestamp(_batchStartTime),
                    results = new List<BatchCommandResult>(),
                    totalCommands = batchCmd.commands.Count,
                    successCount = 0,
                    failedCount = 0
                };

                // 预填充 results 数组,保证 processing 阶段结构稳定.
                // 注意: 框架协议要求命令级 status 仅允许 success/error.
                // 因此 processing 阶段使用空字符串占位,在命令真正完成后再写 success/error.
                for (int i = 0; i < batchCmd.commands.Count; i++)
                {
                    BatchCommand cmd = batchCmd.commands[i];
                    _batchResult.results.Add(new BatchCommandResult
                    {
                        id = cmd.id,
                        type = cmd.type,
                        status = "",
                        startedAt = "",
                        finishedAt = ""
                    });
                }

                bool resumed = TryResumeFromProcessingResult();
                if (!resumed)
                {
                    // 首次写入 processing.
                    BatchResultWriter.WriteProcessing(_batchResult);
                }
            }

            private bool TryResumeFromProcessingResult()
            {
                string resultPath = Path.Combine(UnityAgentSkillsConfig.ResultsDirAbsolutePath, (_batchCmd.batchId ?? string.Empty) + ".json");
                if (!File.Exists(resultPath))
                {
                    return false;
                }

                JsonData root;
                try
                {
                    root = JsonMapper.ToObject(File.ReadAllText(resultPath));
                }
                catch
                {
                    return false;
                }

                if (root == null || !root.IsObject || !root.ContainsKey("status") || !root.ContainsKey("results"))
                {
                    return false;
                }

                if (!string.Equals(root["status"].ToString(), BatchStatuses.Processing, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                JsonData persistedResults = root["results"];
                if (persistedResults == null || !persistedResults.IsArray)
                {
                    return false;
                }

                if (root.ContainsKey("startedAt"))
                {
                    _batchResult.startedAt = root["startedAt"].ToString();
                }

                _cmdIndex = 0;
                _batchResult.successCount = 0;
                _batchResult.failedCount = 0;

                int count = Math.Min(_batchResult.results.Count, persistedResults.Count);
                for (int i = 0; i < count; i++)
                {
                    JsonData persisted = persistedResults[i];
                    if (persisted == null || !persisted.IsObject || !persisted.ContainsKey("status"))
                    {
                        break;
                    }

                    BatchCommandResult current = _batchResult.results[i];
                    string persistedId = persisted.ContainsKey("id") ? persisted["id"].ToString() : string.Empty;
                    string persistedType = persisted.ContainsKey("type") ? persisted["type"].ToString() : string.Empty;
                    if (!string.Equals(persistedId, current.id ?? string.Empty, StringComparison.Ordinal) ||
                        !string.Equals(persistedType, current.type ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }

                    string status = persisted["status"].ToString();
                    if (!string.Equals(status, UnityAgentSkillCommandStatuses.Success, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(status, UnityAgentSkillCommandStatuses.Error, StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }

                    current.status = status;
                    if (persisted.ContainsKey("startedAt")) current.startedAt = persisted["startedAt"].ToString();
                    if (persisted.ContainsKey("finishedAt")) current.finishedAt = persisted["finishedAt"].ToString();
                    if (persisted.ContainsKey("result")) current.result = persisted["result"];
                    if (persisted.ContainsKey("error") && persisted["error"] != null && persisted["error"].IsObject)
                    {
                        JsonData err = persisted["error"];
                        current.error = new CommandError
                        {
                            code = err.ContainsKey("code") ? err["code"].ToString() : string.Empty,
                            message = err.ContainsKey("message") ? err["message"].ToString() : string.Empty,
                            detail = err.ContainsKey("detail") ? err["detail"].ToString() : string.Empty
                        };
                    }

                    if (string.Equals(status, UnityAgentSkillCommandStatuses.Success, StringComparison.OrdinalIgnoreCase))
                    {
                        _batchResult.successCount++;
                    }
                    else
                    {
                        _batchResult.failedCount++;
                    }

                    _cmdIndex = i + 1;
                }

                return _cmdIndex > 0;
            }

            public void Tick()
            {
                if (IsDone) return;

                // 批次超时检查.
                // 需求: 批次超时必须立即中止,并将当前及后续未执行命令标记为 SKIPPED.
                // 否则可能在 completed 结果中残留 status="" 的占位值,违反协议.
                if ((DateTime.Now - _batchStartTime).TotalMilliseconds > _batchTimeoutMs)
                {
                    FinishBatchAsTimeout();
                    return;
                }

                if (_cmdIndex >= _batchCmd.commands.Count)
                {
                    FinishBatchAsCompleted();
                    return;
                }

                BatchCommand cmd = _batchCmd.commands[_cmdIndex];
                BatchCommandResult cmdResult = _batchResult.results[_cmdIndex];

                // 进入新命令.
                if (string.IsNullOrEmpty(cmdResult.startedAt))
                {
                    cmdResult.startedAt = UnityAgentSkillsConfig.FormatTimestamp(DateTime.Now);
                    _currentCmdStartTime = DateTime.Now;
                    _currentCmdTimeoutMs = cmd.timeout ?? _batchTimeoutMs;
                }

                // playmode.stop 后紧接 playmode.start 时,需要等待 Editor 完成退回 EditMode.
                // 否则 start 会在同帧命中 isPlayingOrWillChangePlaymode,被误判为 already active.
                if (string.Equals(cmd.type, UnityAgentSkills.Plugins.PlayMode.Handlers.PlayModeStartHandler.CommandType, StringComparison.OrdinalIgnoreCase) &&
                    UnityAgentSkills.Plugins.PlayMode.PlayModeSession.IsStopTransitionInProgress())
                {
                    return;
                }

                try
                {
                    if (string.Equals(cmd.type, UnityAgentSkills.Plugins.PlayMode.Handlers.PlayModeWaitForHandler.CommandType, StringComparison.OrdinalIgnoreCase))
                    {
                        DateTime now = DateTime.Now;
                        DateTime nowUtc = DateTime.UtcNow;
                        if (!_waitingPlayModeWaitFor)
                        {
                            _waitForJob = UnityAgentSkills.Plugins.PlayMode.Handlers.PlayModeWaitForHandler.CreateJob(cmd.@params, nowUtc);
                            _waitingPlayModeWaitFor = true;
                        }

                        JsonData waitForResult;
                        bool waitForCompleted = UnityAgentSkills.Plugins.PlayMode.Handlers.PlayModeWaitForHandler.TryComplete(_waitForJob, nowUtc, out waitForResult);
                        if (!waitForCompleted)
                        {
                            BatchResultWriter.WriteProcessing(_batchResult);
                            return;
                        }

                        _waitingPlayModeWaitFor = false;
                        cmdResult.status = UnityAgentSkillCommandStatuses.Success;
                        cmdResult.finishedAt = UnityAgentSkillsConfig.FormatTimestamp(now);
                        cmdResult.result = BuildUnifiedResultPayload(cmd, cmdResult.status, waitForResult, null);
                        _batchResult.successCount++;
                        _batchResult.finishedAt = UnityAgentSkillsConfig.FormatTimestamp(now);
                        BatchResultWriter.WriteProcessing(_batchResult);
                        _cmdIndex++;
                        return;
                    }

                    if (string.Equals(cmd.type, "log.screenshot", StringComparison.OrdinalIgnoreCase))
                    {
                        DateTime now = DateTime.Now;
                        if (!_waitingScreenshot)
                        {
                            JsonData effectiveParams = InjectScreenshotContext(cmd.@params, _batchCmd.batchId, cmd.id, CountScreenshotCommands(_batchCmd));
                            _screenshotJob = UnityAgentSkills.Plugins.Log.Handlers.LogScreenshotCommandHandler.CreateJob(effectiveParams);
                            UnityAgentSkills.Plugins.Log.Handlers.LogScreenshotCommandHandler.BeginCapture(_screenshotJob);
                            _screenshotCaptureStartedAt = now;
                            _waitingScreenshot = true;
                            BatchResultWriter.WriteProcessing(_batchResult);
                            return;
                        }

                        JsonData screenshotResult;
                        CommandError screenshotError;
                        bool completed = UnityAgentSkills.Plugins.Log.Handlers.LogScreenshotCommandHandler.TryComplete(
                            _screenshotJob,
                            _screenshotCaptureStartedAt,
                            now,
                            out screenshotResult,
                            out screenshotError);

                        if (!completed)
                        {
                            BatchResultWriter.WriteProcessing(_batchResult);
                            return;
                        }

                        _waitingScreenshot = false;

                        if (screenshotError != null)
                        {
                            cmdResult.status = UnityAgentSkillCommandStatuses.Error;
                            cmdResult.finishedAt = UnityAgentSkillsConfig.FormatTimestamp(now);
                            cmdResult.error = screenshotError;
                            cmdResult.result = BuildUnifiedResultPayload(cmd, cmdResult.status, null, cmdResult.error);
                            _batchResult.failedCount++;
                        }
                        else
                        {
                            cmdResult.status = UnityAgentSkillCommandStatuses.Success;
                            cmdResult.finishedAt = UnityAgentSkillsConfig.FormatTimestamp(now);
                            cmdResult.result = BuildUnifiedResultPayload(cmd, cmdResult.status, screenshotResult, null);
                            _batchResult.successCount++;
                        }

                        _batchResult.finishedAt = UnityAgentSkillsConfig.FormatTimestamp(now);
                        BatchResultWriter.WriteProcessing(_batchResult);
                        _cmdIndex++;
                        return;
                    }

                    // 其他命令保持同步执行.
                    JsonData resultData = CommandHandlerRegistry.Instance.Execute(cmd.type, cmd.@params);

                    int elapsedMs = (int)(DateTime.Now - _currentCmdStartTime).TotalMilliseconds;
                    if (elapsedMs > _currentCmdTimeoutMs)
                    {
                        cmdResult.status = UnityAgentSkillCommandStatuses.Error;
                        cmdResult.finishedAt = UnityAgentSkillsConfig.FormatTimestamp(DateTime.Now);
                        cmdResult.error = CommandErrorFactory.CreateTimeoutError(elapsedMs, _currentCmdTimeoutMs);
                        cmdResult.result = BuildUnifiedResultPayload(cmd, cmdResult.status, null, cmdResult.error);
                        _batchResult.failedCount++;
                    }
                    else
                    {
                        cmdResult.status = UnityAgentSkillCommandStatuses.Success;
                        cmdResult.finishedAt = UnityAgentSkillsConfig.FormatTimestamp(DateTime.Now);
                        cmdResult.result = BuildUnifiedResultPayload(cmd, cmdResult.status, resultData, null);
                        _batchResult.successCount++;
                    }
                }
                catch (Exception ex)
                {
                    int elapsedMs = (int)(DateTime.Now - _currentCmdStartTime).TotalMilliseconds;

                    if (elapsedMs > _currentCmdTimeoutMs)
                    {
                        cmdResult.error = CommandErrorFactory.CreateTimeoutError(elapsedMs, _currentCmdTimeoutMs);
                    }
                    else if (ex is ArgumentException && ex.Message != null && ex.Message.StartsWith(UnityAgentSkillCommandErrorCodes.InvalidFields + ":"))
                    {
                        string detail = ex.Message.Substring(UnityAgentSkillCommandErrorCodes.InvalidFields.Length + 1).Trim();
                        cmdResult.error = CommandErrorFactory.CreateInvalidFieldsError(detail);
                    }
                    else if (TryCreateCodePrefixedError(ex, out CommandError prefixedError))
                    {
                        cmdResult.error = prefixedError;
                    }
                    else if (ex is NotSupportedException)
                    {
                        cmdResult.error = CommandErrorFactory.CreateUnknownCommandError(cmd.type, ex.Message);
                    }
                    else
                    {
                        cmdResult.error = CommandErrorFactory.CreateRuntimeError("异常详情: " + ex.Message);
                    }

                    cmdResult.status = UnityAgentSkillCommandStatuses.Error;
                    cmdResult.finishedAt = UnityAgentSkillsConfig.FormatTimestamp(DateTime.Now);
                    cmdResult.result = BuildUnifiedResultPayload(cmd, cmdResult.status, null, cmdResult.error);
                    _batchResult.failedCount++;
                }

                _batchResult.finishedAt = UnityAgentSkillsConfig.FormatTimestamp(DateTime.Now);
                BatchResultWriter.WriteProcessing(_batchResult);
                _cmdIndex++;
            }

            private static bool TryCreateCodePrefixedError(Exception ex, out CommandError error)
            {
                error = null;
                if (ex == null || string.IsNullOrEmpty(ex.Message)) return false;

                int separatorIndex = ex.Message.IndexOf(':');
                if (separatorIndex <= 0) return false;

                string code = ex.Message.Substring(0, separatorIndex).Trim();
                if (string.IsNullOrEmpty(code) || !IsKnownCommandErrorCode(code)) return false;

                string message = ex.Message.Substring(separatorIndex + 1).TrimStart();
                error = new CommandError
                {
                    code = code,
                    message = string.IsNullOrEmpty(message) ? code : message,
                    detail = ex.Message
                };
                return true;
            }

            private static bool IsKnownCommandErrorCode(string code)
            {
                switch (code)
                {
                    case UnityAgentSkillCommandErrorCodes.PlayModeNotActive:
                    case UnityAgentSkillCommandErrorCodes.PlayModeStartFailed:
                    case UnityAgentSkillCommandErrorCodes.PlayModeAlreadyActive:
                    case UnityAgentSkillCommandErrorCodes.PlayModeInterrupted:
                    case UnityAgentSkillCommandErrorCodes.ElementNotFound:
                    case UnityAgentSkillCommandErrorCodes.ElementNotVisible:
                    case UnityAgentSkillCommandErrorCodes.ElementNotInteractable:
                    case UnityAgentSkillCommandErrorCodes.InvalidTargetIndex:
                    case UnityAgentSkillCommandErrorCodes.NoElementAtPosition:
                    case UnityAgentSkillCommandErrorCodes.InvalidCoordinates:
                    case UnityAgentSkillCommandErrorCodes.UnsupportedElementType:
                    case UnityAgentSkillCommandErrorCodes.AmbiguousTarget:
                    case UnityAgentSkillCommandErrorCodes.GameViewNotAvailable:
                    case UnityAgentSkillCommandErrorCodes.ScreenshotFailed:
                    case UnityAgentSkillCommandErrorCodes.PrefabNotFound:
                    case UnityAgentSkillCommandErrorCodes.GameObjectNotFound:
                    case UnityAgentSkillCommandErrorCodes.ComponentTypeNotFound:
                    case UnityAgentSkillCommandErrorCodes.AmbiguousComponentType:
                    case UnityAgentSkillCommandErrorCodes.ComponentNotFound:
                    case UnityAgentSkillCommandErrorCodes.ComponentAlreadyExists:
                    case UnityAgentSkillCommandErrorCodes.CannotDeleteRequiredComponent:
                    case UnityAgentSkillCommandErrorCodes.PropertyNotFound:
                    case UnityAgentSkillCommandErrorCodes.InvalidPropertyPath:
                    case UnityAgentSkillCommandErrorCodes.TypeMismatch:
                    case UnityAgentSkillCommandErrorCodes.ReferenceTargetNotFound:
                    case UnityAgentSkillCommandErrorCodes.ReferenceTargetTypeMismatch:
                    case UnityAgentSkillCommandErrorCodes.AssetNotFound:
                    case UnityAgentSkillCommandErrorCodes.AssetTypeMismatch:
                    case UnityAgentSkillCommandErrorCodes.EmptyProperties:
                    case UnityAgentSkillCommandErrorCodes.EmptyModifications:
                    case UnityAgentSkillCommandErrorCodes.IdNotFound:
                    case UnityAgentSkillCommandErrorCodes.IndexOutOfRange:
                    case UnityAgentSkillCommandErrorCodes.InvalidRegex:
                    case UnityAgentSkillCommandErrorCodes.Timeout:
                    case UnityAgentSkillCommandErrorCodes.OnlyAllowedInEditMode:
                        return true;
                    default:
                        return false;
                }
            }

            private JsonData BuildUnifiedResultPayload(BatchCommand cmd, string commandStatus, JsonData data, CommandError error)
            {
                if (cmd == null || string.IsNullOrEmpty(cmd.type) || !cmd.type.StartsWith("playmode.", StringComparison.OrdinalIgnoreCase))
                {
                    return data;
                }

                if (data != null && data.IsObject && data.ContainsKey("meta") && data.ContainsKey("data"))
                {
                    return data;
                }

                JsonData payload = new JsonData();

                JsonData meta = new JsonData();
                meta["sessionId"] = _batchCmd != null ? (_batchCmd.batchId ?? string.Empty) : string.Empty;
                meta["sessionState"] = UnityAgentSkills.Plugins.PlayMode.PlayModeSession.State.ToString();
                meta["commandId"] = cmd.id ?? string.Empty;
                DateTime now = DateTime.Now;
                meta["timestamp"] = UnityAgentSkillsConfig.FormatTimestamp(now);
                meta["durationMs"] = (int)(now - _currentCmdStartTime).TotalMilliseconds;
                payload["meta"] = meta;

                JsonData safeData = data;
                if (safeData == null)
                {
                    safeData = new JsonData();
                    safeData.SetJsonType(JsonType.Object);
                }
                payload["data"] = safeData;

                JsonData diagnostics = BuildPlayModeDiagnostics(cmd.type, commandStatus, error, data);
                if (diagnostics != null)
                {
                    payload["diagnostics"] = diagnostics;
                }

                return payload;
            }

            private static JsonData BuildPlayModeDiagnostics(string commandType, string commandStatus, CommandError error, JsonData data)
            {
                if (error == null)
                {
                    return null;
                }

                JsonData diagnostics = new JsonData();
                diagnostics["stage"] = ResolveDiagnosticsStage(commandType, error.code);
                diagnostics["retryable"] = ResolveDiagnosticsRetryable(error.code);

                JsonData hints = new JsonData();
                hints.SetJsonType(JsonType.Array);
                string[] suggestions = ResolveRecoveryHints(error.code);
                for (int i = 0; i < suggestions.Length; i++)
                {
                    hints.Add(suggestions[i]);
                }

                diagnostics["recoveryHints"] = hints;
                if (string.Equals(error.code, UnityAgentSkillCommandErrorCodes.AmbiguousTarget, StringComparison.Ordinal))
                {
                    diagnostics["candidateCount"] = ResolveCandidateCount(data, error.detail);
                }

                return diagnostics;
            }

            private static string ResolveDiagnosticsStage(string commandType, string errorCode)
            {
                switch (errorCode)
                {
                    case UnityAgentSkillCommandErrorCodes.AmbiguousTarget:
                    case UnityAgentSkillCommandErrorCodes.ElementNotFound:
                        return "resolve";
                    case UnityAgentSkillCommandErrorCodes.ElementNotInteractable:
                    case UnityAgentSkillCommandErrorCodes.ElementNotVisible:
                    case UnityAgentSkillCommandErrorCodes.InvalidCoordinates:
                    case UnityAgentSkillCommandErrorCodes.NoElementAtPosition:
                    case UnityAgentSkillCommandErrorCodes.UnsupportedElementType:
                        return "act";
                    case UnityAgentSkillCommandErrorCodes.Timeout:
                    case UnityAgentSkillCommandErrorCodes.ScreenshotFailed:
                    case UnityAgentSkillCommandErrorCodes.PlayModeInterrupted:
                    case UnityAgentSkillCommandErrorCodes.GameViewNotAvailable:
                    case UnityAgentSkillCommandErrorCodes.PlayModeNotActive:
                    case UnityAgentSkillCommandErrorCodes.PlayModeStartFailed:
                    case UnityAgentSkillCommandErrorCodes.PlayModeAlreadyActive:
                        return "verify";
                    default:
                        return string.Equals(commandType, UnityAgentSkills.Plugins.PlayMode.Handlers.PlayModeQueryUIHandler.CommandType, StringComparison.OrdinalIgnoreCase) ? "query" : "act";
                }
            }

            private static bool ResolveDiagnosticsRetryable(string errorCode)
            {
                switch (errorCode)
                {
                    case UnityAgentSkillCommandErrorCodes.Timeout:
                    case UnityAgentSkillCommandErrorCodes.ScreenshotFailed:
                    case UnityAgentSkillCommandErrorCodes.PlayModeInterrupted:
                    case UnityAgentSkillCommandErrorCodes.GameViewNotAvailable:
                    case UnityAgentSkillCommandErrorCodes.PlayModeNotActive:
                    case UnityAgentSkillCommandErrorCodes.PlayModeStartFailed:
                    case UnityAgentSkillCommandErrorCodes.ElementNotInteractable:
                    case UnityAgentSkillCommandErrorCodes.AmbiguousTarget:
                        return true;
                    default:
                        return false;
                }
            }

            private static string[] ResolveRecoveryHints(string errorCode)
            {
                switch (errorCode)
                {
                    case UnityAgentSkillCommandErrorCodes.AmbiguousTarget:
                        return new[]
                        {
                            "补充 siblingIndex 以消除路径歧义",
                            "优先使用 queryUI 返回的 elementId 做动作命中"
                        };
                    case UnityAgentSkillCommandErrorCodes.ElementNotInteractable:
                        return new[]
                        {
                            "先使用 log.screenshot 观察界面状态后再重试动作",
                            "检查目标是否被遮罩或动画锁定"
                        };
                    case UnityAgentSkillCommandErrorCodes.InvalidCoordinates:
                        return new[]
                        {
                            "改用 queryUI 返回的 path+siblingIndex 定位",
                            "确认坐标位于当前 GameView 可见范围"
                        };
                    case UnityAgentSkillCommandErrorCodes.Timeout:
                        return new[]
                        {
                            "扩大 timeout 或 waitUntilElementTimeout",
                            "先缩小 queryUI 筛选范围后再执行验证"
                        };
                    case UnityAgentSkillCommandErrorCodes.PlayModeInterrupted:
                    case UnityAgentSkillCommandErrorCodes.PlayModeNotActive:
                        return new[]
                        {
                            "重新执行 playmode.start",
                            "按 query -> action -> verify 重新建立链路"
                        };
                    default:
                        return new[]
                        {
                            "先执行 queryUI 获取最新可交互目标",
                            "根据 diagnostics.stage 调整下一步命令"
                        };
                }
            }

            private static int ResolveCandidateCount(JsonData data, string detail)
            {
                if (data != null && data.IsObject && data.ContainsKey("candidateCount"))
                {
                    try
                    {
                        return (int)data["candidateCount"];
                    }
                    catch
                    {
                    }
                }

                if (!string.IsNullOrEmpty(detail))
                {
                    const string marker = "candidateCount=";
                    int idx = detail.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                    if (idx >= 0)
                    {
                        string value = detail.Substring(idx + marker.Length).Trim();
                        int end = value.IndexOfAny(new[] { ',', ';', ' ', ')' });
                        if (end > 0)
                        {
                            value = value.Substring(0, end);
                        }

                        int parsed;
                        if (int.TryParse(value, out parsed))
                        {
                            return parsed;
                        }
                    }
                }

                return 0;
            }

            private void FinishBatchAsCompleted()
            {
                _batchResult.status = BatchStatuses.Completed;
                _batchResult.finishedAt = UnityAgentSkillsConfig.FormatTimestamp(DateTime.Now);
                BatchResultWriter.WriteSuccessAndArchive(_batchResult, _item.fullPath);
                IsDone = true;
            }

            private void FinishBatchAsTimeout()
            {
                DateTime now = DateTime.Now;
                string ts = UnityAgentSkillsConfig.FormatTimestamp(now);

                // 当前命令及剩余命令统一标记为 SKIPPED.
                for (int i = _cmdIndex; i < _batchResult.results.Count; i++)
                {
                    BatchCommandResult r = _batchResult.results[i];
                    if (!string.IsNullOrEmpty(r.status)) continue;

                    if (string.IsNullOrEmpty(r.startedAt)) r.startedAt = ts;

                    // SKIPPED 表示未执行/未完成,避免残留成功payload(result)造成外部误用.
                    r.result = null;

                    r.status = UnityAgentSkillCommandStatuses.Error;
                    r.finishedAt = ts;
                    r.error = CommandErrorFactory.CreateSkippedError(_batchTimeoutMs);
                    _batchResult.failedCount++;
                }

                // 超时属于批次正常完成态(含部分失败),对外 batch status 仍为 completed.
                FinishBatchAsCompleted();
            }

            /// <summary>
            /// 会话级兜底失败处理: 将 batch 写入 error 并归档,避免卡死在 processing.
            /// </summary>
            public void FailFatal(Exception ex)
            {
                string message = ex != null ? ex.Message : "unknown";
                string detail = ex != null ? ex.ToString() : "";
                BatchResultWriter.WriteErrorAndArchive(BatchId, _item != null ? _item.fullPath : null, _batchResult != null ? _batchResult.startedAt : null, UnityAgentSkillCommandErrorCodes.RuntimeError, message, detail);
                IsDone = true;
            }

            private static int CountScreenshotCommands(BatchPendingCommand batchCmd)
            {
                int count = 0;
                for (int i = 0; i < batchCmd.commands.Count; i++)
                {
                    if (string.Equals(batchCmd.commands[i].type, "log.screenshot", StringComparison.OrdinalIgnoreCase)) count++;
                }
                return count;
            }

            /// <summary>
            /// 为截图相关命令注入执行上下文(不属于对外协议字段).
            /// 通过深拷贝保持原始 params 不变,避免跨帧流程污染调用方输入.
            /// </summary>
            private static JsonData InjectScreenshotContext(JsonData rawParams, string batchId, string cmdId, int screenshotCommandCount)
            {
                JsonData data = new JsonData();

                if (rawParams != null && rawParams.IsObject)
                {
                    try
                    {
                        data = JsonMapper.ToObject(rawParams.ToJson());
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[UnityAgentSkills] Deep clone screenshot params failed, fallback to shallow copy. " + ex.Message);

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

        /// <summary>
        /// 读取并处理pending命令文件(仅支持批量命令格式).
        /// </summary>
        /// <param name="item">队列项.</param>
        private static void ProcessPendingFile(UnityAgentSkills.Internal.PendingQueue.PendingItem item)
        {
            _isProcessing = true;

            try
            {
                EnsureDirectories();
                ProcessBatchCommand(item);
            }
            catch
            {
                // ProcessBatchCommand 已处理写入错误与重试,这里兜底确保不会卡住.
                _isProcessing = false;
                throw;
            }
        }

        /// <summary>
        /// 处理批量命令.
        /// </summary>
        /// <param name="item">队列项.</param>
        private static void ProcessBatchCommand(UnityAgentSkills.Internal.PendingQueue.PendingItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.fullPath))
            {
                _isProcessing = false;
                return;
            }

            // 队列项可能因为重复事件或历史残留成为“陈旧项”,此时文件已不存在,应直接忽略.
            if (!File.Exists(item.fullPath))
            {
                Debug.Log("[UnityAgentSkills] 跳过陈旧pending项,文件不存在: " + item.fullPath);
                _isProcessing = false;
                return;
            }

            BatchPendingCommand batchCmd;
            try
            {
                batchCmd = BatchCommandParser.ParseAndValidate(item.fullPath, item.fileTime);
            }
            catch (Exception ex)
            {
                // Parse阶段再次兜底: 文件若已被归档/删除,按陈旧项处理,避免误写INVALID_JSON.
                if (!File.Exists(item.fullPath))
                {
                    Debug.Log("[UnityAgentSkills] Parse阶段跳过陈旧pending项,文件不存在: " + item.fullPath);
                    _isProcessing = false;
                    return;
                }

                // 仅对JSON解析失败等暂时性错误触发重试
                if (ex is ArgumentException && ShouldRetryRead(item))
                {
                    Reschedule(item);
                    return;
                }

                string[] parts = ex.Message.Split(new[] { ": " }, 2, StringSplitOptions.None);
                string code = parts[0];
                string message = parts.Length > 1 ? parts[1] : ex.Message;
                string detail = ex.Message;

                WriteBatchErrorAndArchive(item.id, item.id, null, code, message, detail);
                return;
            }

            // 创建跨帧会话,在 EditorApplication.update 中推进.
            _activeSession = new BatchExecutionSession(item, batchCmd);
        }

        /// <summary>
        /// 判断是否需要重试读取pending文件.
        /// </summary>
        /// <param name="item">队列项.</param>
        /// <returns>是否继续重试.</returns>
        private static bool ShouldRetryRead(UnityAgentSkills.Internal.PendingQueue.PendingItem item)
        {
            return item.attempt < UnityAgentSkillsConfig.ReadRetryDelaysMs.Length;
        }

        /// <summary>
        /// 重新排队等待读取重试.
        /// </summary>
        /// <param name="item">队列项.</param>
        private static void Reschedule(UnityAgentSkills.Internal.PendingQueue.PendingItem item)
        {
            int delayMs = UnityAgentSkillsConfig.ReadRetryDelaysMs[item.attempt];
            _pendingQueue.RescheduleToFront(item, delayMs);
            _isProcessing = false;
        }

        /// <summary>
        /// 写入批量命令错误结果并归档.
        /// </summary>
        /// <param name="id">批次id.</param>
        /// <param name="batchId">批次标识.</param>
        /// <param name="startedAt">开始时间.</param>
        /// <param name="code">错误码.</param>
        /// <param name="message">错误消息.</param>
        /// <param name="detail">错误详情.</param>
        private static void WriteBatchErrorAndArchive(string id, string batchId, string startedAt, string code, string message, string detail)
        {
            string pendingPath = string.IsNullOrEmpty(id) ? null : Path.Combine(UnityAgentSkillsConfig.PendingDirAbsolutePath, id + ".json");
            BatchResultWriter.WriteErrorAndArchive(batchId, pendingPath, startedAt, code, message, detail);
        }
}
}