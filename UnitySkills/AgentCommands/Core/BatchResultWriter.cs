using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LitJson2_utf;

namespace AgentCommands.Core
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
                started = AgentCommandsConfig.FormatTimestamp(DateTime.Now);
            }

            BatchResult result = new BatchResult
            {
                batchId = batchId ?? "",
                status = AgentCommandStatuses.Error,
                startedAt = started,
                finishedAt = AgentCommandsConfig.FormatTimestamp(DateTime.Now),
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
            string destPath = Path.Combine(AgentCommandsConfig.ResultsDirAbsolutePath, result.batchId + ".json");
            string tmpPath = destPath + ".tmp";

            StringBuilder sb = new StringBuilder();
            JsonWriter writer = new JsonWriter(new StringWriter(sb));
            writer.EscapeUnicode = false;
            writer.PrettyPrint = AgentCommandsConfig.PrettyPrintJson;
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

            string donePath = Path.Combine(AgentCommandsConfig.DoneDirAbsolutePath, Path.GetFileName(pendingFilePath));

            if (File.Exists(donePath)) File.Delete(donePath);
            File.Move(pendingFilePath, donePath);
        }

        /// <summary>
        /// 清理多余的最终结果文件,仅保留最近的N条.
        /// </summary>
        private static void CleanupOldResults()
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

                    // 先做轻量判断,避免不必要的解析
                    if (text.Contains("\"status\":\"processing\""))
                    {
                        continue;
                    }

                    JsonData jd = JsonMapper.ToObject(text);
                    if (jd != null && jd.IsObject && jd.ContainsKey("status"))
                    {
                        string status = jd["status"].ToString();
                        // completed是最终状态,应该参与清理,避免results无上限增长
                        if (status == AgentCommandStatuses.Success || status == AgentCommandStatuses.Error || status == BatchStatuses.Completed)
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
                    // 忽略删除失败的文件
                }

                string id = Path.GetFileNameWithoutExtension(resultPath);
                string donePath = Path.Combine(AgentCommandsConfig.DoneDirAbsolutePath, id + ".json");
                try
                {
                    if (File.Exists(donePath)) File.Delete(donePath);
                }
                catch
                {
                    // 忽略删除失败的归档文件
                }
            }
        }

        /// <summary>
        /// 获取文件的修改时间用于排序.
        /// </summary>
        /// <param name="filePath">文件路径.</param>
        /// <returns>文件修改时间.</returns>
        private static DateTime GetFileTimeForSort(string filePath)
        {
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