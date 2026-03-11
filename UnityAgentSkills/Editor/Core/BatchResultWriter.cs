using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LitJson2_utf;
using UnityEditor;

namespace UnityAgentSkills.Core
{
    /// <summary>
    /// 批量结果写入器,负责结果文件的写入、归档和清理.
    /// </summary>
    internal static class BatchResultWriter
    {
        /// <summary>
        /// 写入批量命令的processing状态.
        /// </summary>
        /// <param name="result">批量结果对象.</param>
        public static void WriteProcessing(BatchResult result)
        {
            WriteResultAtomically(result);
        }

        /// <summary>
        /// 写入批量命令成功结果并归档.
        /// </summary>
        /// <param name="result">批量结果对象.</param>
        /// <param name="pendingFilePath">pending文件完整路径.</param>
        public static void WriteSuccessAndArchive(BatchResult result, string pendingFilePath)
        {
            WriteResultAtomically(result);
            CleanupOldResults();
            ArchivePending(pendingFilePath);
        }

        /// <summary>
        /// 写入批量命令错误结果并归档.
        /// </summary>
        /// <param name="batchId">批次标识.</param>
        /// <param name="pendingFilePath">pending文件完整路径.</param>
        /// <param name="startedAt">开始时间(可选).</param>
        /// <param name="code">错误码.</param>
        /// <param name="message">错误消息.</param>
        /// <param name="detail">错误详情.</param>
        public static void WriteErrorAndArchive(string batchId, string pendingFilePath, string startedAt, string code, string message, string detail)
        {
            string started = startedAt;
            if (string.IsNullOrEmpty(started))
            {
                started = UnityAgentSkillsConfig.FormatTimestamp(DateTime.Now);
            }

            BatchResult result = new BatchResult
            {
                batchId = batchId ?? "",
                status = UnityAgentSkillCommandStatuses.Error,
                startedAt = started,
                finishedAt = UnityAgentSkillsConfig.FormatTimestamp(DateTime.Now),
                results = new List<BatchCommandResult>(),
                totalCommands = 0,
                successCount = 0,
                failedCount = 0,
                error = new CommandError
                {
                    code = code,
                    message = message,
                    detail = detail
                }
            };

            WriteResultAtomically(result);
            CleanupOldResults();

            // 归档pending文件
            if (!string.IsNullOrEmpty(pendingFilePath) && File.Exists(pendingFilePath))
            {
                ArchivePending(pendingFilePath);
            }
        }

        /// <summary>
        /// 原子写入批量结果文件.
        /// </summary>
        /// <param name="result">批量结果对象.</param>
        private static void WriteResultAtomically(BatchResult result)
        {
            string destPath = Path.Combine(UnityAgentSkillsConfig.ResultsDirAbsolutePath, result.batchId + ".json");
            string tmpPath = destPath + ".tmp";

            StringBuilder sb = new StringBuilder();
            JsonWriter writer = new JsonWriter(new StringWriter(sb));
            writer.EscapeUnicode = false;
            writer.PrettyPrint = UnityAgentSkillsConfig.PrettyPrintJson;
            JsonMapper.ToJson(result.ToJsonData(), writer);
            string json = sb.ToString();
            File.WriteAllText(tmpPath, json);

            try
            {
                if (File.Exists(destPath))
                {
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
                if (File.Exists(destPath)) File.Delete(destPath);
                if (File.Exists(tmpPath)) File.Move(tmpPath, destPath);
            }

            if (File.Exists(tmpPath)) File.Delete(tmpPath);
            string backupLeft = destPath + ".bak";
            if (File.Exists(backupLeft)) File.Delete(backupLeft);
        }

        /// <summary>
        /// 归档pending文件到done目录.
        /// </summary>
        /// <param name="pendingFilePath">pending文件完整路径.</param>
        private static void ArchivePending(string pendingFilePath)
        {
            if (!File.Exists(pendingFilePath)) return;

            string donePath = Path.Combine(UnityAgentSkillsConfig.DoneDirAbsolutePath, Path.GetFileName(pendingFilePath));
            TryMoveFileWithMeta(pendingFilePath, donePath);
        }

        /// <summary>
        /// 清理多余的最终结果文件,仅保留最近的N条.
        /// </summary>
        private static void CleanupOldResults()
        {
            if (!Directory.Exists(UnityAgentSkillsConfig.ResultsDirAbsolutePath)) return;

            string[] files = Directory.GetFiles(UnityAgentSkillsConfig.ResultsDirAbsolutePath, "*.json", SearchOption.TopDirectoryOnly);
            List<string> finals = new List<string>();

            foreach (var f in files)
            {
                try
                {
                    string text = File.ReadAllText(f);
                    if (string.IsNullOrEmpty(text)) continue;

                    JsonData jd = JsonMapper.ToObject(text);
                    if (jd != null && jd.IsObject && jd.ContainsKey("status"))
                    {
                        string status = jd["status"].ToString();
                        // 仅依据顶层status判断最终态,避免命令级status="processing"导致误判.
                        if (status == UnityAgentSkillCommandStatuses.Success || status == UnityAgentSkillCommandStatuses.Error || status == BatchStatuses.Completed)
                        {
                            finals.Add(f);
                        }
                    }
                }
                catch
                {
                    // 忽略解析失败的结果文件
                }
            }

            if (finals.Count <= UnityAgentSkillsConfig.MaxResults) return;

            finals.Sort((a, b) =>
            {
                DateTime ta = GetFileTimeForSort(a);
                DateTime tb = GetFileTimeForSort(b);
                int cmp = ta.CompareTo(tb);
                if (cmp != 0) return cmp;
                return string.Compare(Path.GetFileName(a), Path.GetFileName(b), StringComparison.Ordinal);
            });

            int toDelete = finals.Count - UnityAgentSkillsConfig.MaxResults;
            for (int i = 0; i < toDelete; i++)
            {
                string resultPath = finals[i];

                // 先清理该 results JSON 关联的截图产物(若有).
                try
                {
                    CleanupScreenshotArtifactsFromResultJson(resultPath);
                }
                catch
                {
                    // 忽略清理截图失败,不得影响 results 清理流程.
                }

                bool resultDeleted = TryDeleteFileWithMeta(resultPath);
                if (!resultDeleted)
                {
                    continue;
                }

                string id = Path.GetFileNameWithoutExtension(resultPath);
                string donePath = Path.Combine(UnityAgentSkillsConfig.DoneDirAbsolutePath, id + ".json");
                TryDeleteFileWithMeta(donePath);
            }
        }

        /// <summary>
        /// 解析将要删除的 results JSON,并精准清理其中 log.screenshot 返回的产物路径.
        /// </summary>
        private static void CleanupScreenshotArtifactsFromResultJson(string resultsJsonPath)
        {
            if (string.IsNullOrEmpty(resultsJsonPath) || !File.Exists(resultsJsonPath)) return;

            string text;
            try
            {
                text = File.ReadAllText(resultsJsonPath);
            }
            catch
            {
                return;
            }

            if (string.IsNullOrEmpty(text)) return;

            JsonData root;
            try
            {
                root = JsonMapper.ToObject(text);
            }
            catch
            {
                return;
            }

            if (root == null || !root.IsObject || !root.ContainsKey("results")) return;
            JsonData results = root["results"];
            if (results == null || !results.IsArray) return;

            for (int i = 0; i < results.Count; i++)
            {
                JsonData cmd = results[i];
                if (cmd == null || !cmd.IsObject) continue;

                if (!cmd.ContainsKey("type")) continue;
                if (!cmd.ContainsKey("status") || cmd["status"].ToString() != UnityAgentSkillCommandStatuses.Success) continue;
                if (!cmd.ContainsKey("result")) continue;

                string type = cmd["type"].ToString();
                JsonData result = cmd["result"];
                if (result == null || !result.IsObject) continue;

                if (string.Equals(type, "log.screenshot", StringComparison.OrdinalIgnoreCase))
                {
                    if (result.ContainsKey("imageAbsolutePath"))
                    {
                        TryDeleteScreenshotPath(result["imageAbsolutePath"].ToString());
                    }

                    continue;
                }


            }
        }

        private static void TryDeleteScreenshotPath(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath)) return;

            // 安全边界: 仅允许删除 results 目录下路径.
            string resultsRoot = UnityAgentSkillsConfig.ResultsDirAbsolutePath;
            if (string.IsNullOrEmpty(resultsRoot)) return;

            string fullRoot;
            string fullTarget;
            try
            {
                fullRoot = Path.GetFullPath(resultsRoot);
                fullTarget = Path.GetFullPath(absolutePath);
            }
            catch
            {
                return;
            }

            if (!IsUnderDirectory(fullTarget, fullRoot)) return;

            if (Directory.Exists(fullTarget))
            {
                TryDeleteDirectoryWithMeta(fullTarget);
                return;
            }

            TryDeleteFileWithMeta(fullTarget);
        }

        private static bool TryMoveFileWithMeta(string sourcePath, string targetPath)
        {
            if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(targetPath)) return false;
            if (!File.Exists(sourcePath)) return false;

            string sourceAssetPath = TryConvertToAssetPath(sourcePath);
            string targetAssetPath = TryConvertToAssetPath(targetPath);
            if (!string.IsNullOrEmpty(sourceAssetPath) && !string.IsNullOrEmpty(targetAssetPath))
            {
                if (TryMoveAssetWithMeta(sourcePath, sourceAssetPath, targetPath, targetAssetPath))
                {
                    return true;
                }
            }

            string sourceMetaPath = GetMetaPath(sourcePath);
            if (!File.Exists(sourceMetaPath)) return false;

            string targetMetaPath = GetMetaPath(targetPath);
            try
            {
                string targetDirectory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(targetDirectory)) Directory.CreateDirectory(targetDirectory);

                if (File.Exists(targetPath)) File.Delete(targetPath);
                if (File.Exists(targetMetaPath)) File.Delete(targetMetaPath);

                File.Move(sourcePath, targetPath);
                File.Move(sourceMetaPath, targetMetaPath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryMoveAssetWithMeta(string sourcePath, string sourceAssetPath, string targetPath, string targetAssetPath)
        {
            string sourceMetaPath = GetMetaPath(sourcePath);
            if (!File.Exists(sourceMetaPath)) return false;

            TryImportKnownAsset(sourcePath, sourceAssetPath, sourceMetaPath);

            try
            {
                string targetDirectory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(targetDirectory)) Directory.CreateDirectory(targetDirectory);

                if (File.Exists(targetPath) || File.Exists(GetMetaPath(targetPath)))
                {
                    AssetDatabase.DeleteAsset(targetAssetPath);
                }

                string moveError = AssetDatabase.MoveAsset(sourceAssetPath, targetAssetPath);
                if (!string.IsNullOrEmpty(moveError)) return false;

                return File.Exists(targetPath) && File.Exists(GetMetaPath(targetPath));
            }
            catch
            {
                return false;
            }
        }

        private static void TryImportKnownAsset(string absolutePath, string assetPath, string metaPath)
        {
            if (string.IsNullOrEmpty(absolutePath) || string.IsNullOrEmpty(assetPath) || string.IsNullOrEmpty(metaPath)) return;
            if (!File.Exists(absolutePath) || !File.Exists(metaPath)) return;

            try
            {
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            }
            catch
            {
                // 维持静默失败语义,避免因导入失败打断批次闭环.
            }
        }

        private static string TryConvertToAssetPath(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath)) return string.Empty;

            string fullPath;
            string assetsRoot;
            try
            {
                fullPath = Path.GetFullPath(absolutePath);
                assetsRoot = Path.GetFullPath(UnityAgentSkillsConfig.AssetsAbsolutePath);
            }
            catch
            {
                return string.Empty;
            }

            if (!IsUnderDirectory(fullPath, assetsRoot)) return string.Empty;

            string relativePath = fullPath.Substring(assetsRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.IsNullOrEmpty(relativePath)
                ? "Assets"
                : ("Assets/" + relativePath.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/'));
        }

        private static bool TryDeleteFileWithMeta(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return false;

            string metaPath = GetMetaPath(filePath);
            if (!File.Exists(metaPath)) return false;

            try
            {
                File.Delete(filePath);
                File.Delete(metaPath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryDeleteDirectoryWithMeta(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath)) return false;

            string metaPath = GetMetaPath(directoryPath);
            if (!File.Exists(metaPath)) return false;

            try
            {
                Directory.Delete(directoryPath, true);
                File.Delete(metaPath);
                return true;
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

        private static bool IsUnderDirectory(string path, string directory)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(directory)) return false;

            string dir = directory;
            if (!dir.EndsWith(Path.DirectorySeparatorChar.ToString()) && !dir.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
            {
                dir += Path.DirectorySeparatorChar;
            }

            return path.StartsWith(dir, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 获取文件的排序时间,优先创建时间,失败时回退修改时间.
        /// </summary>
        /// <param name="filePath">文件路径.</param>
        /// <returns>用于排序的时间.</returns>
        private static DateTime GetFileTimeForSort(string filePath)
        {
            try
            {
                DateTime creationTime = File.GetCreationTime(filePath);
                if (creationTime != DateTime.MinValue)
                {
                    return creationTime;
                }
            }
            catch
            {
                // CreationTime 不可靠时回退 LastWriteTime.
            }

            try
            {
                return File.GetLastWriteTime(filePath);
            }
            catch
            {
                return DateTime.MinValue;
            }
        }
    }
}