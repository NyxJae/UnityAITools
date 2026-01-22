using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AgentCommands.Core;
using AgentCommands.Handlers;
using LitJson2;
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
            // 运行游戏期间仍要处理命令.
            // if (Application.isPlaying)
            // {
            //     return;
            // }

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

            RegisterWatcher();
            EnqueueAllPendingFiles();

            _nextRescanTime = EditorApplication.timeSinceStartup + AgentCommandsConfig.PendingRescanIntervalSeconds;
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
            // 运行游戏期间仍要处理命令.
            // if (Application.isPlaying) return;

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
        /// 读取并处理单个pending命令文件.
        /// </summary>
        /// <param name="item">队列项.</param>
        private static void ProcessPendingFile(PendingFileItem item)
        {
            _isProcessing = true;

            try
            {
                EnsureDirectories();

                PendingCommand cmd;
                try
                {
                    cmd = ReadPendingCommand(item);
                }
                catch (Exception ex)
                {
                    if (ShouldRetryRead(item))
                    {
                        Reschedule(item);
                        return;
                    }

                    WriteErrorAndArchive(item.id, null, null, AgentCommandErrorCodes.InvalidJson, "命令文件不是合法的 json,请检查写入是否完整", "异常详情: " + ex.Message);
                    return;
                }

                if (cmd == null || string.IsNullOrEmpty(cmd.type) || cmd.@params == null)
                {
                    WriteErrorAndArchive(item.id, cmd != null ? cmd.type : null, null, AgentCommandErrorCodes.InvalidFields, "命令文件缺少必要字段 type 或 params", null);
                    return;
                }

                string startedAt = AgentCommandsConfig.FormatTimestamp(DateTime.Now);
                WriteProcessing(item.id, cmd.type, startedAt);

                try
                {
                    JsonData resultData = ExecuteCommand(cmd);
                    WriteSuccessAndArchive(item.id, cmd.type, startedAt, resultData);
                }
                catch (Exception ex)
                {
                    string code;
                    string message;

                    if (ex is InvalidOperationException && ex.Message != null && ex.Message.StartsWith(AgentCommandErrorCodes.InvalidRegex + ":"))
                    {
                        code = AgentCommandErrorCodes.InvalidRegex;
                        message = "正则表达式非法,请检查 keyword";
                    }
                    else if (ex is NotSupportedException)
                    {
                        code = AgentCommandErrorCodes.UnknownType;
                        message = "未知命令类型: " + cmd.type;
                    }
                    else
                    {
                        code = AgentCommandErrorCodes.RuntimeError;
                        message = "命令执行发生异常";
                    }

                    WriteErrorAndArchive(item.id, cmd.type, startedAt, code, message, "异常详情: " + ex.Message);
                }
            }
            finally
            {
                _isProcessing = false;
            }
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
        /// 读取pending命令并解析为结构体.
        /// </summary>
        /// <param name="item">队列项.</param>
        /// <returns>解析后的命令对象.</returns>
        private static PendingCommand ReadPendingCommand(PendingFileItem item)
        {
            string json = File.ReadAllText(item.fullPath);
            JsonData root = JsonMapper.ToObject(json);

            string type = null;
            JsonData p = null;

            if (root != null && root.IsObject)
            {
                if (root.ContainsKey("type")) type = root["type"].ToString();
                if (root.ContainsKey("params")) p = root["params"];
            }

            return new PendingCommand
            {
                id = item.id,
                type = type,
                @params = p,
                fileTime = item.fileTime
            };
        }

        /// <summary>
        /// 根据命令类型分发执行.
        /// </summary>
        /// <param name="cmd">解析后的命令.</param>
        /// <returns>命令结果数据.</returns>
        private static JsonData ExecuteCommand(PendingCommand cmd)
        {
            if (cmd.type == LogQueryCommandHandler.CommandType)
            {
                return LogQueryCommandHandler.Execute(cmd.@params);
            }

            throw new NotSupportedException(AgentCommandErrorCodes.UnknownType + ": " + cmd.type);
        }

        /// <summary>
        /// 写入processing状态的结果文件.
        /// </summary>
        /// <param name="id">命令id.</param>
        /// <param name="type">命令类型.</param>
        /// <param name="startedAt">开始时间.</param>
        private static void WriteProcessing(string id, string type, string startedAt)
        {
            CommandResult result = new CommandResult
            {
                id = id,
                type = type,
                status = AgentCommandStatuses.Processing,
                startedAt = startedAt,
            };

            WriteResultAtomically(id, result);
        }

        /// <summary>
        /// 写入成功结果并归档命令文件.
        /// </summary>
        /// <param name="id">命令id.</param>
        /// <param name="type">命令类型.</param>
        /// <param name="startedAt">开始时间.</param>
        /// <param name="resultData">结果数据.</param>
        private static void WriteSuccessAndArchive(string id, string type, string startedAt, JsonData resultData)
        {
            CommandResult result = new CommandResult
            {
                id = id,
                type = type,
                status = AgentCommandStatuses.Success,
                startedAt = startedAt,
                finishedAt = AgentCommandsConfig.FormatTimestamp(DateTime.Now),
                result = resultData
            };

            WriteResultAtomically(id, result);
            CleanupOldFinalResults();
            ArchivePending(id);
        }

        /// <summary>
        /// 写入错误结果并归档命令文件.
        /// </summary>
        /// <param name="id">命令id.</param>
        /// <param name="type">命令类型.</param>
        /// <param name="startedAt">开始时间.</param>
        /// <param name="code">错误码.</param>
        /// <param name="message">错误消息.</param>
        /// <param name="detail">错误详情.</param>
        private static void WriteErrorAndArchive(string id, string type, string startedAt, string code, string message, string detail)
        {
            string started = startedAt;
            if (string.IsNullOrEmpty(started))
            {
                started = AgentCommandsConfig.FormatTimestamp(DateTime.Now);
            }

            CommandResult result = new CommandResult
            {
                id = id,
                type = type ?? "",
                status = AgentCommandStatuses.Error,
                startedAt = started,
                finishedAt = AgentCommandsConfig.FormatTimestamp(DateTime.Now),
                error = new CommandError
                {
                    code = code,
                    message = message,
                    detail = detail
                }
            };

            WriteResultAtomically(id, result);
            CleanupOldFinalResults();
            ArchivePending(id);
        }

        /// <summary>
        /// 原子写入results文件.
        /// </summary>
        /// <param name="id">命令id.</param>
        /// <param name="result">结果对象.</param>
        private static void WriteResultAtomically(string id, CommandResult result)
        {
            string destPath = Path.Combine(AgentCommandsConfig.ResultsDirAbsolutePath, id + ".json");
            string tmpPath = destPath + ".tmp";

            StringBuilder sb = new StringBuilder();
            JsonWriter writer = new JsonWriter(new StringWriter(sb));
            writer.EscapeUnicode = false;
            JsonMapper.ToJson(result.ToJsonData(), writer);
            string json = sb.ToString();
            File.WriteAllText(tmpPath, json);

            try
            {
                if (File.Exists(destPath))
                {
                    // 优先使用 Replace,避免短暂的结果文件缺失.
                    string backupPath = destPath + ".bak";
                    if (File.Exists(backupPath)) File.Delete(backupPath);
                    File.Replace(tmpPath, destPath, backupPath);
                    if (File.Exists(backupPath)) File.Delete(backupPath);
                }
                else
                {
                    File.Move(tmpPath, destPath);
                }
            }
            catch
            {
                // 兜底处理.
                if (File.Exists(destPath)) File.Delete(destPath);
                if (File.Exists(tmpPath)) File.Move(tmpPath, destPath);
            }

            if (File.Exists(tmpPath)) File.Delete(tmpPath);
            string backupLeft = destPath + ".bak";
            if (File.Exists(backupLeft)) File.Delete(backupLeft);
        }

        /// <summary>
        /// 将pending命令文件移动到done目录.
        /// </summary>
        /// <param name="id">命令id.</param>
        private static void ArchivePending(string id)
        {
            string pendingPath = Path.Combine(AgentCommandsConfig.PendingDirAbsolutePath, id + ".json");
            if (!File.Exists(pendingPath)) return;

            string donePath = Path.Combine(AgentCommandsConfig.DoneDirAbsolutePath, id + ".json");

            if (File.Exists(donePath)) File.Delete(donePath);
            File.Move(pendingPath, donePath);
        }

        /// <summary>
        /// 清理多余的最终结果文件.
        /// </summary>
        private static void CleanupOldFinalResults()
        {
            if (!Directory.Exists(AgentCommandsConfig.ResultsDirAbsolutePath)) return;

            string[] files = Directory.GetFiles(AgentCommandsConfig.ResultsDirAbsolutePath, "*.json", SearchOption.TopDirectoryOnly);
            List<string> finals = new List<string>();

            foreach (var f in files)
            {
                try
                {
                    string text = File.ReadAllText(f);
                    if (string.IsNullOrEmpty(text)) continue;

                    // 先做轻量判断,避免不必要的解析.
                    if (text.Contains("\"status\":\"processing\""))
                    {
                        continue;
                    }

                    JsonData jd = JsonMapper.ToObject(text);
                    if (jd != null && jd.IsObject && jd.ContainsKey("status"))
                    {
                        string status = jd["status"].ToString();
                        if (status == AgentCommandStatuses.Success || status == AgentCommandStatuses.Error)
                        {
                            finals.Add(f);
                        }
                    }
                }
                catch
                {
                    // 忽略解析失败的结果文件.
                }
            }

            if (finals.Count <= AgentCommandsConfig.MaxResults) return;

            finals.Sort((a, b) =>
            {
                DateTime ta = GetFileTimeForSort(a);
                DateTime tb = GetFileTimeForSort(b);
                int cmp = ta.CompareTo(tb);
                if (cmp != 0) return cmp;
                return string.Compare(Path.GetFileName(a), Path.GetFileName(b), StringComparison.Ordinal);
            });

            int toDelete = finals.Count - AgentCommandsConfig.MaxResults;
            for (int i = 0; i < toDelete; i++)
            {
                string resultPath = finals[i];
                try
                {
                    File.Delete(resultPath);
                }
                catch
                {
                    // 忽略删除失败的文件.
                }

                string id = Path.GetFileNameWithoutExtension(resultPath);
                string donePath = Path.Combine(AgentCommandsConfig.DoneDirAbsolutePath, id + ".json");
                try
                {
                    if (File.Exists(donePath)) File.Delete(donePath);
                }
                catch
                {
                    // 忽略删除失败的归档文件.
                }
            }
        }
    }
}
