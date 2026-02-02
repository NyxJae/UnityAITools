using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using LitJson2_utf;

namespace UnityAgentSkills.Core
{
    /// <summary>
    /// 批量命令解析器,负责JSON解析和字段校验.
    /// </summary>
    internal static class BatchCommandParser
    {
        /// <summary>
        /// 解析批量命令文件并验证字段.
        /// </summary>
        /// <param name="filePath">文件完整路径.</param>
        /// <param name="fileTime">文件时间戳.</param>
        /// <returns>解析后的批量命令.</returns>
        /// <exception cref="ArgumentException">当字段验证失败时抛出.</exception>
        public static BatchPendingCommand Parse(string filePath, DateTime fileTime)
        {
            if (!File.Exists(filePath))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidJson + ": 文件不存在: " + filePath);
            }

            string json = File.ReadAllText(filePath);
            JsonData root;
            try
            {
                root = JsonMapper.ToObject(json);
            }
            catch (Exception ex)
            {
                // 统一包装为带错误码的异常,确保ProcessBatchCommand能正确解析错误码
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidJson + ": JSON解析失败 - " + ex.Message);
            }

            if (root == null || !root.IsObject)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidJson + ": 批量命令文件不是合法的json,请检查写入是否完整");
            }

            // 解析batchId
            string batchId = null;
            if (root.ContainsKey("batchId"))
            {
                batchId = root["batchId"].ToString();
            }

            // 解析timeout
            int? timeout = null;
            if (root.ContainsKey("timeout") && root["timeout"].IsInt)
            {
                timeout = (int)root["timeout"];
            }

            // 解析commands
            List<BatchCommand> commands = null;
            if (root.ContainsKey("commands") && root["commands"].IsArray)
            {
                JsonData commandsJson = root["commands"];
                commands = new List<BatchCommand>();
                foreach (JsonData cmdJson in commandsJson)
                {
                    if (cmdJson.IsObject)
                    {
                        BatchCommand cmd = new BatchCommand();

                        if (cmdJson.ContainsKey("id"))
                        {
                            cmd.id = cmdJson["id"].ToString();
                        }

                        if (cmdJson.ContainsKey("type"))
                        {
                            cmd.type = cmdJson["type"].ToString();
                        }

                        if (cmdJson.ContainsKey("params"))
                        {
                            cmd.@params = cmdJson["params"];
                        }

                        if (cmdJson.ContainsKey("timeout") && cmdJson["timeout"].IsInt)
                        {
                            cmd.timeout = (int)cmdJson["timeout"];
                        }

                        commands.Add(cmd);
                    }
                }
            }

            return new BatchPendingCommand
            {
                batchId = batchId,
                timeout = timeout,
                commands = commands,
                fileTime = fileTime
            };
        }

        /// <summary>
        /// 验证批量命令的必填字段.
        /// </summary>
        /// <param name="batchCmd">批量命令.</param>
        /// <exception cref="ArgumentException">当验证失败时抛出,包含错误码和详细信息.</exception>
        public static void Validate(BatchPendingCommand batchCmd)
        {
            if (batchCmd == null)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": 批量命令为null");
            }

            if (string.IsNullOrEmpty(batchCmd.batchId))
            {
                throw new ArgumentException(
                    UnityAgentSkillCommandErrorCodes.InvalidFields + ": 缺少必填字段: batchId - 输入 JSON 必须包含 batchId 字段");
            }

            // 校验batchId安全性,防止路径穿越或写入异常路径
            if (!Regex.IsMatch(batchCmd.batchId, @"^[a-zA-Z0-9_-]+$"))
            {
                throw new ArgumentException(
                    UnityAgentSkillCommandErrorCodes.InvalidFields + ": batchId包含非法字符,仅允许字母数字下划线和短横线 - batchId=" + batchCmd.batchId);
            }

            if (batchCmd.commands == null || batchCmd.commands.Count == 0)
            {
                throw new ArgumentException(
                    UnityAgentSkillCommandErrorCodes.InvalidFields + ": 命令数组不能为空 - commands 数组至少包含一个命令");
            }

            // 验证每个命令的必填字段
            for (int i = 0; i < batchCmd.commands.Count; i++)
            {
                BatchCommand cmd = batchCmd.commands[i];

                if (string.IsNullOrEmpty(cmd.id))
                {
                    throw new ArgumentException(
                        UnityAgentSkillCommandErrorCodes.InvalidFields + $": 命令缺少必填字段: id - 第 {i + 1} 个命令必须包含 id 字段");
                }

                if (string.IsNullOrEmpty(cmd.type))
                {
                    throw new ArgumentException(
                        UnityAgentSkillCommandErrorCodes.InvalidFields + $": 命令缺少必填字段: type - 命令 id={cmd.id} 必须包含 type 字段");
                }

                if (cmd.@params == null)
                {
                    throw new ArgumentException(
                        UnityAgentSkillCommandErrorCodes.InvalidFields + $": 命令缺少必填字段: params - 命令 id={cmd.id} 必须包含 params 字段");
                }
            }
        }

        /// <summary>
        /// 解析并验证批量命令文件(组合方法).
        /// </summary>
        /// <param name="filePath">文件完整路径.</param>
        /// <param name="fileTime">文件时间戳.</param>
        /// <returns>解析并验证后的批量命令.</returns>
        public static BatchPendingCommand ParseAndValidate(string filePath, DateTime fileTime)
        {
            BatchPendingCommand batchCmd = Parse(filePath, fileTime);
            Validate(batchCmd);
            return batchCmd;
        }
    }
}