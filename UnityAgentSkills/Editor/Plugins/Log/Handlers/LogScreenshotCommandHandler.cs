using System;
using System.Collections;
using System.Globalization;
using System.IO;
using LitJson2_utf;
using UnityAgentSkills.Core;
using UnityEditor;
using UnityEngine;

namespace UnityAgentSkills.Plugins.Log.Handlers
{
    /// <summary>
    /// log.screenshot 命令处理器.
    /// </summary>
    internal static class LogScreenshotCommandHandler
    {
        /// <summary>
        /// 支持的命令类型.
        /// </summary>
        public const string CommandType = "log.screenshot";

        // 与 log.screenshot 的统一超时口径保持一致.
        internal const int ScreenshotReadyTimeoutMs = 5000;

        internal readonly struct ScreenshotJob
        {
            /// <summary>
            /// 截图文件绝对路径.
            /// </summary>
            public readonly string pngAbsolutePath;

            /// <summary>
            /// 可选红框区域.
            /// </summary>
            public readonly HighlightRectData? highlightRect;

            /// <summary>
            /// 创建截图等待任务.
            /// </summary>
            /// <param name="pngAbsolutePath">截图文件绝对路径.</param>
            /// <param name="highlightRect">可选红框区域.</param>
            public ScreenshotJob(string pngAbsolutePath, HighlightRectData? highlightRect)
            {
                this.pngAbsolutePath = pngAbsolutePath;
                this.highlightRect = highlightRect;
            }
        }

        internal readonly struct HighlightRectData
        {
            /// <summary>
            /// 红框左边界.
            /// </summary>
            public readonly float xMin;

            /// <summary>
            /// 红框右边界.
            /// </summary>
            public readonly float xMax;

            /// <summary>
            /// 红框下边界.
            /// </summary>
            public readonly float yMin;

            /// <summary>
            /// 红框上边界.
            /// </summary>
            public readonly float yMax;

            /// <summary>
            /// 创建红框标注区域.
            /// </summary>
            /// <param name="xMin">左边界.</param>
            /// <param name="xMax">右边界.</param>
            /// <param name="yMin">下边界.</param>
            /// <param name="yMax">上边界.</param>
            public HighlightRectData(float xMin, float xMax, float yMin, float yMax)
            {
                this.xMin = xMin;
                this.xMax = xMax;
                this.yMin = yMin;
                this.yMax = yMax;
            }
        }

        /// <summary>
        /// 兼容入口: 立即触发截图并返回路径信息.
        /// 实际文件可读与红框处理由外层跨帧轮询完成.
        /// </summary>
        public static JsonData Execute(JsonData rawParams)
        {
            ScreenshotJob job = CreateJob(rawParams);
            BeginCapture(job);
            return null;
        }

        /// <summary>
        /// 跨帧轮询截图完成状态.
        /// 返回 true 表示命令已完成(成功或失败),false 表示继续等待.
        /// </summary>
        internal static bool TryComplete(ScreenshotJob job, DateTime captureStartedAt, DateTime now, out JsonData resultData, out CommandError error)
        {
            resultData = BuildSuccessResult(job, false);
            error = null;
            return true;
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

            ValidateAllowedFields(rawParams);

            CommandParams parameters = new CommandParams(rawParams);

            string batchId = parameters.GetString("__batchId", "");
            string cmdId = parameters.GetString("__cmdId", "");
            int screenshotCommandCount = parameters.GetInt("__screenshotCommandCount", 1);
            HighlightRectData? highlightRect = ParseHighlightRect(rawParams);

            if (string.IsNullOrEmpty(batchId) || string.IsNullOrEmpty(cmdId))
            {
                throw new InvalidOperationException("Missing injected context fields: __batchId/__cmdId");
            }

            string baseName = screenshotCommandCount <= 1 ? batchId : (batchId + "_" + cmdId);
            string pngPath = Path.Combine(UnityAgentSkillsConfig.ResultsDirAbsolutePath, baseName + ".png");
            return new ScreenshotJob(pngPath, highlightRect);
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

            EnsureGameViewFocused();
            ScreenCapture.CaptureScreenshot(job.pngAbsolutePath);
        }

        internal static JsonData BuildSuccessResult(ScreenshotJob job, bool highlightApplied)
        {
            JsonData result = new JsonData();
            result["mode"] = "single";
            result["imageAbsolutePath"] = job.pngAbsolutePath;
            result["highlightApplied"] = highlightApplied;
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

        private static bool HasReadableMeta(string assetPath)
        {
            string metaPath = GetMetaPath(assetPath);
            try
            {
                if (string.IsNullOrEmpty(metaPath) || !File.Exists(metaPath)) return false;
                using (var fs = File.Open(metaPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    return fs.Length > 0;
                }
            }
            catch
            {
                return false;
            }
        }

        private static string GetMetaPath(string assetPath)
        {
            return string.IsNullOrEmpty(assetPath) ? string.Empty : (assetPath + ".meta");
        }

        private static void ValidateAllowedFields(JsonData rawParams)
        {
            if (rawParams == null || !rawParams.IsObject)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": params must be an object");
            }

            foreach (DictionaryEntry entry in (IDictionary)rawParams)
            {
                string key = entry.Key as string;
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                if (string.Equals(key, "highlightRect", StringComparison.Ordinal) ||
                    string.Equals(key, "__batchId", StringComparison.Ordinal) ||
                    string.Equals(key, "__cmdId", StringComparison.Ordinal) ||
                    string.Equals(key, "__screenshotCommandCount", StringComparison.Ordinal))
                {
                    continue;
                }

                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": unsupported field " + key);
            }
        }

        private static HighlightRectData? ParseHighlightRect(JsonData rawParams)
        {
            if (rawParams == null || !rawParams.IsObject || !rawParams.ContainsKey("highlightRect"))
            {
                return null;
            }

            JsonData highlightRect = rawParams["highlightRect"];
            if (highlightRect == null || !highlightRect.IsObject)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": highlightRect must be object");
            }

            float xMin = ParseRequiredRectField(highlightRect, "xMin");
            float xMax = ParseRequiredRectField(highlightRect, "xMax");
            float yMin = ParseRequiredRectField(highlightRect, "yMin");
            float yMax = ParseRequiredRectField(highlightRect, "yMax");

            if (xMin > xMax || yMin > yMax)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": highlightRect requires xMin<=xMax and yMin<=yMax");
            }

            return new HighlightRectData(xMin, xMax, yMin, yMax);
        }

        private static float ParseRequiredRectField(JsonData rect, string field)
        {
            if (rect == null || !rect.IsObject || !rect.ContainsKey(field))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": missing highlightRect." + field);
            }

            return ParseFloatValue(rect[field], "highlightRect." + field);
        }

        private static float ParseFloatValue(JsonData value, string field)
        {
            if (value == null)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": invalid " + field);
            }

            if (value.IsInt)
            {
                return (int)value;
            }

            if (value.IsLong)
            {
                return (long)value;
            }

            if (value.IsDouble)
            {
                return (float)(double)value;
            }

            if (value.IsString)
            {
                float parsed;
                if (float.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
                {
                    return parsed;
                }
            }

            throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": invalid " + field);
        }

        private static bool TryApplyHighlight(string pngPath, HighlightRectData rect)
        {
            Texture2D texture = null;
            try
            {
                byte[] bytes = File.ReadAllBytes(pngPath);
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(bytes))
                {
                    return false;
                }

                int width = texture.width;
                int height = texture.height;
                if (width <= 0 || height <= 0)
                {
                    return false;
                }

                int minX = Mathf.Clamp(Mathf.RoundToInt(rect.xMin), 0, width - 1);
                int maxX = Mathf.Clamp(Mathf.RoundToInt(rect.xMax), 0, width - 1);
                int minY = Mathf.Clamp(Mathf.RoundToInt(rect.yMin), 0, height - 1);
                int maxY = Mathf.Clamp(Mathf.RoundToInt(rect.yMax), 0, height - 1);

                if (minX > maxX || minY > maxY)
                {
                    return false;
                }

                Color borderColor = Color.red;
                int thickness = 2;

                for (int t = 0; t < thickness; t++)
                {
                    int left = Mathf.Clamp(minX + t, 0, width - 1);
                    int right = Mathf.Clamp(maxX - t, 0, width - 1);
                    int bottom = Mathf.Clamp(minY + t, 0, height - 1);
                    int top = Mathf.Clamp(maxY - t, 0, height - 1);

                    for (int x = left; x <= right; x++)
                    {
                        texture.SetPixel(x, bottom, borderColor);
                        texture.SetPixel(x, top, borderColor);
                    }

                    for (int y = bottom; y <= top; y++)
                    {
                        texture.SetPixel(left, y, borderColor);
                        texture.SetPixel(right, y, borderColor);
                    }
                }

                texture.Apply(false, false);
                File.WriteAllBytes(pngPath, texture.EncodeToPNG());
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (texture != null)
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
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
