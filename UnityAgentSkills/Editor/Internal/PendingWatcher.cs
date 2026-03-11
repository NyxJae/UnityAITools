using System;
using System.IO;

namespace UnityAgentSkills.Internal
{
    /// <summary>
    /// pending 目录的 FileSystemWatcher 封装.
    /// 约束: 事件回调可能来自非主线程,回调中不得触碰 Unity API.
    /// </summary>
    internal sealed class PendingWatcher : IDisposable
    {
        private FileSystemWatcher _watcher;

        public void Start(string pendingDirAbsolutePath, Action<string> onPendingFileChanged)
        {
            Stop();

            if (string.IsNullOrEmpty(pendingDirAbsolutePath)) return;

            try
            {
                // watcher 要求目录存在,这里显式确保创建成功.
                Directory.CreateDirectory(pendingDirAbsolutePath);
            }
            catch
            {
                // 目录不可用时无法启用 watcher,直接返回,由定时 rescan 兜底.
                return;
            }

            _watcher = new FileSystemWatcher
            {
                Path = pendingDirAbsolutePath,
                Filter = "*.json",
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime
            };

            _watcher.Created += (_, e) => onPendingFileChanged?.Invoke(e.FullPath);
            _watcher.Changed += (_, e) => onPendingFileChanged?.Invoke(e.FullPath);
            _watcher.Renamed += (_, e) => onPendingFileChanged?.Invoke(e.FullPath);
            _watcher.EnableRaisingEvents = true;
        }

        public void Stop()
        {
            if (_watcher == null) return;

            try
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
            }
            catch
            {
                // watcher 释放阶段允许失败,避免关闭流程被异常打断.
            }

            _watcher = null;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
