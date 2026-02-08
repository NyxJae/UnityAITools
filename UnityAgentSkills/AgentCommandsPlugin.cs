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
        /// <summary>
        /// pending文件排队项.
        /// </summary>
        private sealed class PendingFileItem
        {
            /// <summary>
            /// 文件完整路径.
            /// </summary>
            public string fullPath;

            /// <summary>
            /// 命令id.
            /// </summary>
            public string id;

            /// <summary>
            /// 当前重试次数.
            /// </summary>
            public int attempt;

            /// <summary>
            /// 下次可重试的编辑器时间.
            /// </summary>
            public double nextAttemptEditorTime;

            /// <summary>
            /// 文件时间戳.
            /// </summary>
            public DateTime fileTime;
        }

        /// <summary>
        /// 监听pending目录的文件监视器.
        /// </summary>
        private static FileSystemWatcher _watcher;

        /// <summary>
        /// 等待处理的pending队列.
        /// </summary>
        private static readonly List<PendingFileItem> _queue = new List<PendingFileItem>(64);

        /// <summary>
        /// 去重用的pending路径集合.
        /// </summary>
        private static readonly HashSet<string> _knownPending = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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

            DisposeWatcher();

            EnsureDirectories();
            LogCache.Initialize();

            // 加载所有命令插件
            LoadCommandPlugins();

            RegisterWatcher();
            EnqueueAllPendingFiles();

            _nextRescanTime = EditorApplication.timeSinceStartup + UnityAgentSkillsConfig.PendingRescanIntervalSeconds;
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
            DisposeWatcher();
            LogCache.Shutdown();
            CommandPluginLoader.ShutdownAllPlugins();
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

        /// <summary>
        /// 启动pending目录的文件监听.
        /// </summary>
        private static void RegisterWatcher()
        {
            _watcher = new FileSystemWatcher
            {
                Path = UnityAgentSkillsConfig.PendingDirAbsolutePath,
                Filter = "*.json",
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime
            };

            _watcher.Created += OnPendingChanged;
            _watcher.Changed += OnPendingChanged;
            _watcher.Renamed += OnPendingRenamed;
            _watcher.EnableRaisingEvents = true;
        }

        /// <summary>
        /// 释放文件监听器.
        /// </summary>
        private static void DisposeWatcher()
        {
            if (_watcher == null) return;

            try
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Created -= OnPendingChanged;
                _watcher.Changed -= OnPendingChanged;
                _watcher.Renamed -= OnPendingRenamed;
                _watcher.Dispose();
            }
            catch
            {
                // 忽略释放时异常.
            }

            _watcher = null;
        }

        /// <summary>
        /// pending文件被重命名时入队.
        /// </summary>
        private static void OnPendingRenamed(object sender, RenamedEventArgs e)
        {
            TryEnqueuePendingFile(e.FullPath);
        }

        /// <summary>
        /// pending文件新增或更新时入队.
        /// </summary>
        private static void OnPendingChanged(object sender, FileSystemEventArgs e)
        {
            TryEnqueuePendingFile(e.FullPath);
        }

        /// <summary>
        /// 扫描pending目录并加入队列.
        /// </summary>
        private static void EnqueueAllPendingFiles()
        {
            if (!Directory.Exists(UnityAgentSkillsConfig.PendingDirAbsolutePath)) return;

            string[] files = Directory.GetFiles(UnityAgentSkillsConfig.PendingDirAbsolutePath, "*.json", SearchOption.TopDirectoryOnly);
            foreach (var f in files)
            {
                TryEnqueuePendingFile(f);
            }

            SortQueue();
        }

        /// <summary>
        /// 将pending文件加入处理队列.
        /// </summary>
        /// <param name="fullPath">文件完整路径.</param>
        private static void TryEnqueuePendingFile(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath)) return;
            if (!File.Exists(fullPath)) return;

            lock (_queue)
            {
                if (_knownPending.Contains(fullPath)) return;

                string id = Path.GetFileNameWithoutExtension(fullPath);
                if (string.IsNullOrEmpty(id)) return;

                PendingFileItem item = new PendingFileItem
                {
                    fullPath = fullPath,
                    id = id,
                    attempt = 0,
                    nextAttemptEditorTime = 0,
                    fileTime = GetFileTimeForSort(fullPath)
                };

                _queue.Add(item);
                _knownPending.Add(fullPath);

                SortQueue();
            }
        }

        /// <summary>
        /// 按文件时间排序队列.
        /// </summary>
        private static void SortQueue()
        {
            _queue.Sort((a, b) =>
            {
                int cmp = a.fileTime.CompareTo(b.fileTime);
                if (cmp != 0) return cmp;
                return string.Compare(a.id, b.id, StringComparison.Ordinal);
            });
        }

        /// <summary>
        /// 获取文件用于排序的时间戳.
        /// </summary>
        /// <param name="path">文件路径.</param>
        /// <returns>排序时间.</returns>
        private static DateTime GetFileTimeForSort(string path)
        {
            try
            {
                return File.GetCreationTime(path);
            }
            catch
            {
                try
                {
                    return File.GetLastWriteTime(path);
                }
                catch
                {
                    return DateTime.Now;
                }
            }
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

            PendingFileItem item = null;
            lock (_queue)
            {
                if (_queue.Count > 0)
                {
                    if (now >= _queue[0].nextAttemptEditorTime)
                    {
                        item = _queue[0];
                        _queue.RemoveAt(0);
                        _knownPending.Remove(item.fullPath);
                    }
                }
            }

            if (item == null) return;

            // 开始新的批次会话.
            ProcessPendingFile(item);
        }

        /// <summary>
        /// 批次跨帧执行会话.
        /// 目的: 支持 log.screenshot 等需要等待落盘但不能阻塞主线程的命令.
        /// </summary>
        private sealed class BatchExecutionSession
        {
            private readonly PendingFileItem _item;
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

            public bool IsDone { get; private set; }

            public BatchExecutionSession(PendingFileItem item, BatchPendingCommand batchCmd)
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

                // 首次写入 processing.
                BatchResultWriter.WriteProcessing(_batchResult);
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

                // 若正在等待 screenshot 落盘,则只做轮询.
                if (_waitingScreenshot)
                {
                    int elapsedMs = (int)(DateTime.Now - _currentCmdStartTime).TotalMilliseconds;
                    if (UnityAgentSkills.Plugins.Log.Handlers.LogScreenshotCommandHandler.IsFileReadablePng(_screenshotJob.pngAbsolutePath))
                    {
                        cmdResult.status = UnityAgentSkillCommandStatuses.Success;
                        cmdResult.finishedAt = UnityAgentSkillsConfig.FormatTimestamp(DateTime.Now);
                        cmdResult.result = UnityAgentSkills.Plugins.Log.Handlers.LogScreenshotCommandHandler.BuildSuccessResult(_screenshotJob);
                        _batchResult.successCount++;
                        _waitingScreenshot = false;
                        _cmdIndex++;
                        BatchResultWriter.WriteProcessing(_batchResult);
                        return;
                    }

                    if (elapsedMs > _currentCmdTimeoutMs)
                    {
                        cmdResult.status = UnityAgentSkillCommandStatuses.Error;
                        cmdResult.finishedAt = UnityAgentSkillsConfig.FormatTimestamp(DateTime.Now);
                        cmdResult.error = CommandErrorFactory.CreateTimeoutError(elapsedMs, _currentCmdTimeoutMs);
                        _batchResult.failedCount++;
                        _waitingScreenshot = false;
                        _cmdIndex++;
                        BatchResultWriter.WriteProcessing(_batchResult);
                        return;
                    }

                    // 仍在等待,保持占位状态.
                    return;
                }

                // 进入新命令.
                if (string.IsNullOrEmpty(cmdResult.startedAt))
                {
                    cmdResult.startedAt = UnityAgentSkillsConfig.FormatTimestamp(DateTime.Now);
                    _currentCmdStartTime = DateTime.Now;
                    _currentCmdTimeoutMs = cmd.timeout ?? _batchTimeoutMs;
                }

                try
                {
                    // 特殊处理 log.screenshot: 触发截图,并进入等待态(非阻塞).
                    if (string.Equals(cmd.type, "log.screenshot", StringComparison.OrdinalIgnoreCase))
                    {
                        JsonData effectiveParams = InjectScreenshotContext(cmd.@params, _batchCmd.batchId, cmd.id, CountScreenshotCommands(_batchCmd));
                        _screenshotJob = UnityAgentSkills.Plugins.Log.Handlers.LogScreenshotCommandHandler.CreateJob(effectiveParams);
                        UnityAgentSkills.Plugins.Log.Handlers.LogScreenshotCommandHandler.BeginCapture(_screenshotJob);

                        // processing 阶段先写入 result 路径(不写 success,避免与"success=文件可读"语义冲突).
                        // 最终文件可读时再将 status 置为 success.
                        cmdResult.status = "";
                        cmdResult.result = UnityAgentSkills.Plugins.Log.Handlers.LogScreenshotCommandHandler.BuildSuccessResult(_screenshotJob);

                        _currentCmdTimeoutMs = Math.Min(_currentCmdTimeoutMs, UnityAgentSkills.Plugins.Log.Handlers.LogScreenshotCommandHandler.ScreenshotReadyTimeoutMs);
                        _waitingScreenshot = true;
                        BatchResultWriter.WriteProcessing(_batchResult);
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
                        _batchResult.failedCount++;
                    }
                    else
                    {
                        cmdResult.status = UnityAgentSkillCommandStatuses.Success;
                        cmdResult.finishedAt = UnityAgentSkillsConfig.FormatTimestamp(DateTime.Now);
                        cmdResult.result = resultData;
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
                    _batchResult.failedCount++;
                }

                _batchResult.finishedAt = UnityAgentSkillsConfig.FormatTimestamp(DateTime.Now);
                BatchResultWriter.WriteProcessing(_batchResult);
                _cmdIndex++;
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

                _waitingScreenshot = false;

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
            /// 为 log.screenshot 注入执行上下文(不属于对外协议字段).
            /// 复制自 BatchCommandExecutor,但放在这里以便跨帧会话使用.
            /// </summary>
            private static JsonData InjectScreenshotContext(JsonData rawParams, string batchId, string cmdId, int screenshotCommandCount)
            {
                JsonData data = new JsonData();

                // 深拷贝原始 params,避免注入字段污染输入对象.
                if (rawParams != null && rawParams.IsObject)
                {
                    try
                    {
                        data = JsonMapper.ToObject(rawParams.ToJson());
                    }
                    catch
                    {
                        data = new JsonData();
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
        private static void ProcessPendingFile(PendingFileItem item)
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
        private static void ProcessBatchCommand(PendingFileItem item)
        {
            BatchPendingCommand batchCmd;
            try
            {
                batchCmd = BatchCommandParser.ParseAndValidate(item.fullPath, item.fileTime);
            }
            catch (Exception ex)
            {
                // 仅对JSON解析失败等暂时性错误触发重试
                if (ex is ArgumentException && ShouldRetryRead(item))
                {
                    Reschedule(item);
                    return;
                }

                string[] parts = ex.Message.Split(new[] { ": " }, 2, StringSplitOptions.None);
                string code = parts[0];
                string message = ex.Message;
                string detail = parts.Length > 1 ? parts[1] : "";

                WriteBatchErrorAndArchive(item.id, null, null, code, message, detail);
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
        private static bool ShouldRetryRead(PendingFileItem item)
        {
            return item.attempt < UnityAgentSkillsConfig.ReadRetryDelaysMs.Length;
        }

        /// <summary>
        /// 重新排队等待读取重试.
        /// </summary>
        /// <param name="item">队列项.</param>
        private static void Reschedule(PendingFileItem item)
        {
            int delayMs = UnityAgentSkillsConfig.ReadRetryDelaysMs[item.attempt];
            item.attempt++;
            item.nextAttemptEditorTime = EditorApplication.timeSinceStartup + (delayMs / 1000.0);

            // 严格保持处理顺序:最早的文件重试等待期间不允许后续插队.
            lock (_queue)
            {
                _queue.Insert(0, item);
                _knownPending.Add(item.fullPath);
            }

            _isProcessing = false;
        }



        // NOTE: ProcessBatchCommands 已由 BatchExecutionSession 取代(跨帧非阻塞).保留历史实现会导致重复路径.
        // private static void ProcessBatchCommands(...) { ... }





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