using System;
using System.IO;
using LitJson2_utf;
using UnityAgentSkills.Core;
using UnityEditor;
using UnityEngine;

namespace UnityAgentSkills.Plugins.Log.Handlers
{
    /// <summary>
    /// log.screenshot 命令处理器.
    /// 只负责生成截图计划与触发截图,不在此处阻塞等待落盘.
    /// </summary>
    internal static class LogScreenshotCommandHandler
    {
        /// <summary>
        /// 支持的命令类型.
        /// </summary>
        public const string CommandType = "log.screenshot";

        // 需求约束: 从触发截图到文件可读的最长等待时间 5s.
        internal const int ScreenshotReadyTimeoutMs = 5000;

        internal readonly struct ScreenshotJob
        {
            public readonly string pngAbsolutePath;

            public ScreenshotJob(string pngAbsolutePath)
            {
                this.pngAbsolutePath = pngAbsolutePath;
            }
        }

        /// <summary>
        /// 旧同步接口(保留,但不推荐在 Editor Update 主循环中直接调用).
        /// 当前框架会通过异步状态机在外层等待文件可读后再写入 success.
        /// </summary>
        public static JsonData Execute(JsonData rawParams)
        {
            ScreenshotJob job = CreateJob(rawParams);
            BeginCapture(job);

            // 注意: 此处不等待落盘,仅返回产物路径.
            // 最终 success=文件可读 的语义由外层批次执行状态机保证.
            return BuildSuccessResult(job);
        }

        /// <summary>
        /// 校验参数并计算截图输出路径.
        /// </summary>
        internal static ScreenshotJob CreateJob(JsonData rawParams)
        {
            if (rawParams == null || !rawParams.IsObject)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": params must be an object");
            }

            CommandParams parameters = new CommandParams(rawParams);

            // 上下文参数(由执行器注入,不属于对外协议字段)
            string batchId = parameters.GetString("__batchId", "");
            string cmdId = parameters.GetString("__cmdId", "");
            int screenshotCommandCount = parameters.GetInt("__screenshotCommandCount", 1);

            if (string.IsNullOrEmpty(batchId) || string.IsNullOrEmpty(cmdId))
            {
                // 这里属于框架内部错误,用 runtime error 走异常.
                throw new InvalidOperationException("Missing injected context fields: __batchId/__cmdId");
            }

            // 命名规则: 同一 batch 仅 1 条截图时用 batchId,否则 batchId_cmdId.
            string baseName = screenshotCommandCount <= 1 ? batchId : (batchId + "_" + cmdId);
            string pngPath = Path.Combine(UnityAgentSkillsConfig.ResultsDirAbsolutePath, baseName + ".png");
            return new ScreenshotJob(pngPath);
        }

        /// <summary>
        /// 触发截图写文件(写入发生在后续帧).
        /// </summary>
        internal static void BeginCapture(ScreenshotJob job)
        {
            if (string.IsNullOrEmpty(job.pngAbsolutePath))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": missing png path");
            }

            // 覆盖规则: 若已存在,先删再写,避免读取到旧文件.
            try
            {
                if (File.Exists(job.pngAbsolutePath))
                {
                    File.Delete(job.pngAbsolutePath);
                }
            }
            catch
            {
                // 忽略删除失败,让后续写入尝试覆盖.
            }

            EnsureGameViewFocused();

            // Edit 模式可用: ScreenCapture.CaptureScreenshot.
            // 注意: Unity 会在后续帧落盘,因此需要外层非阻塞等待文件可读.
            ScreenCapture.CaptureScreenshot(job.pngAbsolutePath);
        }

        internal static JsonData BuildSuccessResult(ScreenshotJob job)
        {
            JsonData result = new JsonData();
            result["mode"] = "single";
            result["imageAbsolutePath"] = job.pngAbsolutePath;
            return result;
        }

        internal static bool IsFileReadablePng(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
                using (var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    return fs.Length > 0;
                }
            }
            catch
            {
                return false;
            }
        }

        private static void EnsureGameViewFocused()
        {
            try
            {
                Type gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
                if (gameViewType == null)
                {
                    throw new InvalidOperationException("UnityEditor.GameView type not found");
                }

                EditorWindow gameView = EditorWindow.GetWindow(gameViewType);
                if (gameView == null)
                {
                    throw new InvalidOperationException("Failed to open GameView");
                }

                gameView.Show();
                gameView.Focus();

                // 触发重绘,提高截屏稳定性.
                gameView.Repaint();
                EditorApplication.QueuePlayerLoopUpdate();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to focus GameView: " + ex.Message);
            }
        }
    }
}
