using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace UnityAgentSkills.Internal
{
    /// <summary>
    /// pending 文件队列.
    /// 负责: 去重,排序,以及严格保序的重试调度(Insert(0)).
    /// </summary>
    internal sealed class PendingQueue
    {
        internal sealed class PendingItem
        {
            internal string fullPath;
            internal string id;
            internal int attempt;
            internal double nextAttemptEditorTime;
            internal DateTime fileTime;
        }

        private readonly List<PendingItem> _queue = new List<PendingItem>(64);
        private readonly HashSet<string> _knownPending = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public int Count
        {
            get
            {
                lock (_queue) return _queue.Count;
            }
        }

        /// <summary>
        /// 清空内存中的 pending 队列和去重索引.
        /// </summary>
        public void Clear()
        {
            lock (_queue)
            {
                _queue.Clear();
                _knownPending.Clear();
            }
        }

        public void EnqueueAllPendingFiles(string pendingDirAbsolutePath)
        {
            if (!Directory.Exists(pendingDirAbsolutePath)) return;

            string[] files = Directory.GetFiles(pendingDirAbsolutePath, "*.json", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++)
            {
                TryEnqueuePendingFile(files[i]);
            }

            SortQueue();
        }

        public void TryEnqueuePendingFile(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath)) return;
            if (!File.Exists(fullPath)) return;

            lock (_queue)
            {
                if (_knownPending.Contains(fullPath)) return;

                string id = Path.GetFileNameWithoutExtension(fullPath);
                if (string.IsNullOrEmpty(id)) return;

                PendingItem item = new PendingItem
                {
                    fullPath = fullPath,
                    id = id,
                    attempt = 0,
                    nextAttemptEditorTime = 0,
                    fileTime = GetFileTimeForSort(fullPath)
                };

                _queue.Add(item);
                _knownPending.Add(fullPath);

                SortQueue_NoLock();
            }
        }

        public bool TryDequeueReady(double editorNow, out PendingItem item)
        {
            item = null;

            lock (_queue)
            {
                if (_queue.Count <= 0) return false;

                if (editorNow < _queue[0].nextAttemptEditorTime) return false;

                item = _queue[0];
                _queue.RemoveAt(0);
                _knownPending.Remove(item.fullPath);
                return true;
            }
        }

        public void RescheduleToFront(PendingItem item, int delayMs)
        {
            if (item == null) return;

            item.attempt++;
            item.nextAttemptEditorTime = EditorApplication.timeSinceStartup + (delayMs / 1000.0);

            // 严格保持处理顺序: 最早的文件重试等待期间不允许后续插队.
            lock (_queue)
            {
                _queue.Insert(0, item);
                _knownPending.Add(item.fullPath);
            }
        }

        private void SortQueue()
        {
            lock (_queue)
            {
                SortQueue_NoLock();
            }
        }

        private void SortQueue_NoLock()
        {
            _queue.Sort((a, b) =>
            {
                int cmp = a.fileTime.CompareTo(b.fileTime);
                if (cmp != 0) return cmp;
                return string.Compare(a.id, b.id, StringComparison.Ordinal);
            });
        }

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
    }
}
