using System;
using System.Collections.Concurrent;
using System.IO;

namespace UnityAgentSkills.AutoCompile
{
    /// <summary>
    /// 使用 FileSystemWatcher 监视指定目录中的 .cs 文件变更.
    /// 此服务在后台线程上运行,并通过线程安全的队列与主线程通信.
    /// </summary>
    public class FileMonitorService : IDisposable
    {
        private readonly FileSystemWatcher _watcher;
        private readonly ConcurrentQueue<string> _changedFilesQueue;

        /// <summary>
        /// 初始化 FileMonitorService 类的新实例.
        /// </summary>
        /// <param name="path">要监视的目录的绝对路径.</param>
        /// <param name="queue">用于推送文件变更事件的线程安全队列.</param>
        public FileMonitorService(string path, ConcurrentQueue<string> queue)
        {
            _changedFilesQueue = queue;
            _watcher = new FileSystemWatcher(path)
            {
                Filter = "*.cs",
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                IncludeSubdirectories = true,
            };

            _watcher.Changed += OnFileChanged;
            _watcher.Created += OnFileChanged;
            _watcher.Deleted += OnFileChanged;
            _watcher.Renamed += OnFileRenamed;
        }

        /// <summary>
        /// 开始监视文件变更.
        /// </summary>
        public void Start() => _watcher.EnableRaisingEvents = true;

        /// <summary>
        /// 停止监视文件变更.
        /// </summary>
        public void Stop() => _watcher.EnableRaisingEvents = false;

        /// <summary>
        /// 释放 FileMonitorService 使用的所有资源.
        /// </summary>
        public void Dispose()
        {
            _watcher?.Dispose();
        }

        /// <summary>
        /// 处理文件被更改、创建或删除的事件.
        /// </summary>
        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            // 只将事件加入队列,由主线程处理
            _changedFilesQueue.Enqueue(e.FullPath);
        }

        /// <summary>
        /// 处理文件被重命名的事件.
        /// </summary>
        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            // 将旧路径和新路径都加入队列,以正确处理变更
            _changedFilesQueue.Enqueue(e.OldFullPath);
            _changedFilesQueue.Enqueue(e.FullPath);
        }
    }
}
