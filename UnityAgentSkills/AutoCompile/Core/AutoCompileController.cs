using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityAgentSkills.Core;

namespace UnityAgentSkills.AutoCompile
{
    /// <summary>
    /// 管理文件监视服务的生命周期,处理事件,并与Unity编辑器交互.
    /// </summary>
    public static class AutoCompileController
    {
        /// <summary>
        /// 服务的当前状态.
        /// </summary>
        public enum Status
        {
            Stopped,
            Running,
            Paused,
            Pending, // 等待防抖间隔
            Compiling
        }

        /// <summary>
        /// 获取服务的当前状态.
        /// </summary>
        public static Status CurrentStatus { get; private set; } = Status.Stopped;

        /// <summary>
        /// 状态变更时触发的事件.
        /// </summary>
        public static event Action<Status> OnStatusChanged;

        private static AutoCompileConfig _config;
        private static List<FileMonitorService> _services = new List<FileMonitorService>();
        private static readonly ConcurrentQueue<string> _changedFilesQueue = new ConcurrentQueue<string>();
        private static double _debounceEndTime;
        private static bool _isInitialized = false;

        /// <summary>
        /// 初始化 AutoCompile 控制器.
        /// </summary>
        /// <param name="config">配置对象.</param>
        public static void Initialize(AutoCompileConfig config)
        {
            if (config == null)
            {
                Debug.LogWarning("[AutoCompile] 配置为空,服务将不会启动");
                SetStatus(Status.Stopped);
                return;
            }

            _config = config;

            // 防止重复注册
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            _isInitialized = true;

            if (_config.IsEnabled)
            {
                StartService();
            }
            else
            {
                SetStatus(Status.Stopped);
            }
        }

        /// <summary>
        /// 关闭 AutoCompile 控制器并释放资源.
        /// </summary>
        public static void Shutdown()
        {
            StopService();

            EditorApplication.update -= OnUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

            _config = null;
            _isInitialized = false;
            SetStatus(Status.Stopped);
        }

        /// <summary>
        /// 启动文件监视服务.
        /// </summary>
        public static void StartService()
        {
            if (_services.Count > 0) return;
            if (_config == null)
            {
                Debug.LogWarning("[AutoCompile] 配置为空,无法启动服务");
                return;
            }

            // 验证配置
            if (!AutoCompileConfigProvider.ValidateConfig(_config, out string errorMessage))
            {
                Debug.LogError($"[AutoCompile] 配置无效: {errorMessage}");
                SetStatus(Status.Stopped);
                return;
            }

            // 为每个监听路径创建服务
            foreach (var watchPath in _config.WatchPaths)
            {
                string fullPath = PathUtils.GetFullPath(watchPath);
                if (!Directory.Exists(fullPath))
                {
                    Debug.LogWarning($"[AutoCompile] 监听路径不存在,跳过: {watchPath} (完整路径: {fullPath})");
                    continue;
                }

                var service = new FileMonitorService(fullPath, _changedFilesQueue);
                service.Start();
                _services.Add(service);
            }

            if (_services.Count > 0)
            {
                SetStatus(Status.Running);
            }
            else
            {
                Debug.LogWarning("[AutoCompile] 没有有效的监听路径,服务未启动");
                SetStatus(Status.Stopped);
            }
        }

        /// <summary>
        /// 停止文件监视服务.
        /// </summary>
        public static void StopService()
        {
            if (_services.Count == 0) return;

            foreach (var service in _services)
            {
                service.Stop();
                service.Dispose();
            }
            _services.Clear();
            SetStatus(Status.Stopped);
        }

        /// <summary>
        /// 更新配置.
        /// </summary>
        /// <param name="newConfig">新的配置对象.</param>
        public static void UpdateConfig(AutoCompileConfig newConfig)
        {
            if (newConfig == null)
            {
                Debug.LogWarning("[AutoCompile] 新配置为空,忽略更新");
                return;
            }

            // 校验新配置
            if (!AutoCompileConfigProvider.ValidateConfig(newConfig, out string errorMessage))
            {
                Debug.LogError($"[AutoCompile] 新配置无效,忽略更新: {errorMessage}");
                return;
            }

            bool wasRunning = CurrentStatus == Status.Running || CurrentStatus == Status.Pending;

            // 如果服务正在运行,先停止
            if (wasRunning)
            {
                StopService();
            }

            _config = newConfig;

            // 如果新配置启用服务,则重新启动
            if (_config.IsEnabled)
            {
                StartService();
            }
            else
            {
                // 禁用时显式设置状态
                SetStatus(Status.Stopped);
            }
        }

        private static void OnUpdate()
        {
            if (_config == null || !_config.IsEnabled || _services.Count == 0 || CurrentStatus == Status.Paused)
            {
                return;
            }

            if (EditorApplication.isCompiling)
            {
                if (CurrentStatus != Status.Compiling)
                {
                    SetStatus(Status.Compiling);
                }
                return;
            }

            // 如果刚从编译状态恢复,重置状态
            if (CurrentStatus == Status.Compiling)
            {
                SetStatus(Status.Running);
            }

            // 从队列中处理文件变更事件,并启动或重置防抖计时器
            if (!_changedFilesQueue.IsEmpty)
            {
                // 清空队列
                while (_changedFilesQueue.TryDequeue(out string _))
                {
                    // 只需要清空队列,不需要记录具体文件
                }

                SetStatus(Status.Pending);
                _debounceEndTime = EditorApplication.timeSinceStartup + (_config.DebounceInterval / 1000.0);
            }

            // 如果处于待定状态并且防抖时间已过
            if (CurrentStatus == Status.Pending && EditorApplication.timeSinceStartup >= _debounceEndTime)
            {
                // 重置状态
                _debounceEndTime = 0;
                SetStatus(Status.Running);

                // 检查是否离焦且不在编译中
                if (!UnityEditorInternal.InternalEditorUtility.isApplicationActive && !EditorApplication.isCompiling)
                {
                    AssetDatabase.Refresh();
                    SetStatus(Status.Compiling);
                }
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (_config == null || !_config.IsEnabled) return;

            if (state == PlayModeStateChange.ExitingEditMode)
            {
                // 进入 Play 模式时，停止所有服务（无论当前状态）
                if (CurrentStatus == Status.Running || CurrentStatus == Status.Pending || CurrentStatus == Status.Compiling)
                {
                    foreach (var service in _services)
                    {
                        service.Stop();
                    }
                    SetStatus(Status.Paused);
                }
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                // 退出 Play 模式时，恢复服务（如果之前已暂停）
                if (CurrentStatus == Status.Paused)
                {
                    foreach (var service in _services)
                    {
                        service.Start();
                    }
                    SetStatus(Status.Running);
                }
            }
        }

        private static void SetStatus(Status newStatus)
        {
            if (CurrentStatus == newStatus) return;

            CurrentStatus = newStatus;
            OnStatusChanged?.Invoke(CurrentStatus);
        }
    }
}
