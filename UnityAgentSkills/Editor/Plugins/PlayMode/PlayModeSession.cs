using System;
using UnityEditor;

namespace UnityAgentSkills.Plugins.PlayMode
{
    /// <summary>
    /// Play Mode 会话状态定义.
    /// </summary>
    internal enum PlayModeSessionState
    {
        Idle,
        Starting,
        Active,
        Stopping,
        Stopped
    }

    /// <summary>
    /// Play Mode 会话管理器.
    /// 负责维护状态机并识别用户手动中断.
    /// </summary>
    internal static class PlayModeSession
    {
        private static bool _initialized;
        private static bool _requestedStop;
        private static PlayModeSessionState _state = PlayModeSessionState.Idle;

        /// <summary>
        /// 当前会话状态.
        /// </summary>
        public static PlayModeSessionState State => _state;

        /// <summary>
        /// 上次状态变化时间.
        /// </summary>
        public static DateTime LastStateChangedAt { get; private set; } = DateTime.Now;

        /// <summary>
        /// 是否发生了外部中断(例如用户手动停止 Play Mode).
        /// </summary>
        public static bool WasInterrupted { get; private set; }

        /// <summary>
        /// 初始化状态监听.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            _initialized = true;
            SyncStateFromEditor();
        }

        /// <summary>
        /// 启动 Play Mode 会话.
        /// </summary>
        public static void Start()
        {
            Initialize();

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(UnityAgentSkills.Core.UnityAgentSkillCommandErrorCodes.PlayModeAlreadyActive + ": Already in Play Mode");
            }

            WasInterrupted = false;
            _requestedStop = false;
            SetState(PlayModeSessionState.Starting);
            EditorApplication.isPlaying = true;
        }

        /// <summary>
        /// 停止 Play Mode 会话.
        /// </summary>
        public static void Stop()
        {
            Initialize();

            if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                _requestedStop = false;
                WasInterrupted = false;
                SetState(PlayModeSessionState.Stopped);
                return;
            }

            _requestedStop = true;
            SetState(PlayModeSessionState.Stopping);
            EditorApplication.isPlaying = false;
        }

        /// <summary>
        /// 是否处于 stop 后回到 Edit Mode 的过渡阶段.
        /// </summary>
        public static bool IsStopTransitionInProgress()
        {
            Initialize();
            return _requestedStop && !EditorApplication.isPlaying && EditorApplication.isPlayingOrWillChangePlaymode;
        }

        /// <summary>
        /// 要求命令执行前必须处于可操作会话.
        /// </summary>
        public static void EnsureActiveForCommand()
        {
            Initialize();

            if (EditorApplication.isPlaying)
            {
                return;
            }

            if (WasInterrupted)
            {
                throw new InvalidOperationException(UnityAgentSkills.Core.UnityAgentSkillCommandErrorCodes.PlayModeInterrupted + ": Play Mode was manually stopped by user");
            }

            throw new InvalidOperationException(UnityAgentSkills.Core.UnityAgentSkillCommandErrorCodes.PlayModeNotActive + ": Not in Play Mode");
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                    SetState(PlayModeSessionState.Starting);
                    break;
                case PlayModeStateChange.EnteredPlayMode:
                    WasInterrupted = false;
                    SetState(PlayModeSessionState.Active);
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    SetState(PlayModeSessionState.Stopping);
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    if (_requestedStop)
                    {
                        _requestedStop = false;
                        SetState(PlayModeSessionState.Stopped);
                    }
                    else
                    {
                        WasInterrupted = true;
                        SetState(PlayModeSessionState.Stopped);
                    }
                    break;
            }
        }

        private static void SyncStateFromEditor()
        {
            if (EditorApplication.isPlaying)
            {
                SetState(PlayModeSessionState.Active);
                return;
            }

            SetState(PlayModeSessionState.Idle);
        }

        private static void SetState(PlayModeSessionState state)
        {
            _state = state;
            LastStateChangedAt = DateTime.Now;
        }
    }
}
