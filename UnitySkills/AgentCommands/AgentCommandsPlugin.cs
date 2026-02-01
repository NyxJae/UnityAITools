using System;
using System.Collections.Generic;
using System.IO;
using AgentCommands.Core;
using UnityEditor;
using UnityEngine;

namespace AgentCommands
{
    /// <summary>
    /// AgentCommands编辑器插件入口.
    /// </summary>
    [InitializeOnLoad]
    internal static class AgentCommandsPlugin
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
        static AgentCommandsPlugin()
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

            _nextRescanTime = EditorApplication.timeSinceStartup + AgentCommandsConfig.PendingRescanIntervalSeconds;
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
                    Debug.LogError("[AgentCommands] CRITICAL: 核心命令加载失败,框架不可用!");
                }
                else if (!result.IsFrameworkFunctional)
                {
                    Debug.LogWarning("[AgentCommands] WARNING: 框架不可用,核心命令未正确注册");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AgentCommands] 插件加载过程中发生异常: {ex}");
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
            Directory.CreateDirectory(AgentCommandsConfig.DataDirAbsolutePath);
            Directory.CreateDirectory(AgentCommandsConfig.PendingDirAbsolutePath);
            Directory.CreateDirectory(AgentCommandsConfig.ResultsDirAbsolutePath);
            Directory.CreateDirectory(AgentCommandsConfig.DoneDirAbsolutePath);
        }

        /// <summary>
        /// 启动pending目录的文件监听.
        /// </summary>
        private static void RegisterWatcher()
        {
            _watcher = new FileSystemWatcher
            {
                Path = AgentCommandsConfig.PendingDirAbsolutePath,
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
            if (!Directory.Exists(AgentCommandsConfig.PendingDirAbsolutePath)) return;

            string[] files = Directory.GetFiles(AgentCommandsConfig.PendingDirAbsolutePath, "*.json", SearchOption.TopDirectoryOnly);
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
        /// Editor循环处理命令队列.
        /// </summary>
        private static void OnEditorUpdate()
        {
            // 双保险:定时扫描 pending.
            double now = EditorApplication.timeSinceStartup;
            if (now >= _nextRescanTime)
            {
                _nextRescanTime = now + AgentCommandsConfig.PendingRescanIntervalSeconds;
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

            ProcessPendingFile(item);
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
            finally
            {
                _isProcessing = false;
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

            // 开始执行批次
            ProcessBatchCommands(batchCmd, item);
        }

        /// <summary>
        /// 判断是否需要重试读取pending文件.
        /// </summary>
        /// <param name="item">队列项.</param>
        /// <returns>是否继续重试.</returns>
        private static bool ShouldRetryRead(PendingFileItem item)
        {
            return item.attempt < AgentCommandsConfig.ReadRetryDelaysMs.Length;
        }

        /// <summary>
        /// 重新排队等待读取重试.
        /// </summary>
        /// <param name="item">队列项.</param>
        private static void Reschedule(PendingFileItem item)
        {
            int delayMs = AgentCommandsConfig.ReadRetryDelaysMs[item.attempt];
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



        /// <summary>
        /// 执行批量命令,支持超时控制和部分成功模式.
        /// </summary>
        /// <param name="batchCmd">批量命令对象.</param>
        /// <param name="item">队列项.</param>
        private static void ProcessBatchCommands(BatchPendingCommand batchCmd, PendingFileItem item)
        {
            // 使用BatchCommandExecutor执行批量命令
            BatchResult batchResult = BatchCommandExecutor.Execute(batchCmd, result =>
            {
                // processing状态更新回调
                BatchResultWriter.WriteProcessing(result);
            });

            // 批次完成,写入最终结果并归档
            BatchResultWriter.WriteSuccessAndArchive(batchResult, item.fullPath);
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
            string pendingPath = string.IsNullOrEmpty(id) ? null : Path.Combine(AgentCommandsConfig.PendingDirAbsolutePath, id + ".json");
            BatchResultWriter.WriteErrorAndArchive(batchId, pendingPath, startedAt, code, message, detail);
        }
}
}