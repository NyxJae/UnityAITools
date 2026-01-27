using System;
using System.Collections.Generic;
using LitJson2_utf;

namespace AgentCommands.Core
{
    /// <summary>
    /// 命令结果的状态常量.
    /// </summary>
    internal static class AgentCommandStatuses
    {
        /// <summary>
        /// 处理中状态.
        /// </summary>
        public const string Processing = "processing";

        /// <summary>
        /// 成功状态.
        /// </summary>
        public const string Success = "success";

        /// <summary>
        /// 失败状态.
        /// </summary>
        public const string Error = "error";
    }

    /// <summary>
    /// 命令错误码常量.
    /// </summary>
    internal static class AgentCommandErrorCodes
    {
        /// <summary>
        /// 命令json不可解析.
        /// </summary>
        public const string InvalidJson = "INVALID_JSON";

        /// <summary>
        /// 命令字段缺失或非法.
        /// </summary>
        public const string InvalidFields = "INVALID_FIELDS";

        /// <summary>
        /// 未知命令类型.
        /// </summary>
        public const string UnknownType = "UNKNOWN_TYPE";

        /// <summary>
        /// 正则表达式非法.
        /// </summary>
        public const string InvalidRegex = "INVALID_REGEX";

        /// <summary>
        /// 执行时异常.
        /// </summary>
        public const string RuntimeError = "RUNTIME_ERROR";

        /// <summary>
        /// 命令执行超时.
        /// </summary>
        public const string Timeout = "TIMEOUT";

        /// <summary>
        /// 批次超时,命令未执行.
        /// </summary>
        public const string Skipped = "SKIPPED";
    }

    /// <summary>
    /// pending命令的解析结果.
    /// </summary>
    internal sealed class PendingCommand
    {
        /// <summary>
        /// 命令id,对应文件名.
        /// </summary>
        public string id;

        /// <summary>
        /// 命令类型.
        /// </summary>
        public string type;

        /// <summary>
        /// 命令参数的json数据.
        /// </summary>
        public JsonData @params;

        /// <summary>
        /// 命令文件时间戳.
        /// </summary>
        public DateTime fileTime;
    }

    /// <summary>
    /// 命令执行错误信息.
    /// </summary>
    internal sealed class CommandError
    {
        /// <summary>
        /// 错误码.
        /// </summary>
        public string code;

        /// <summary>
        /// 错误消息.
        /// </summary>
        public string message;

        /// <summary>
        /// 错误详情.
        /// </summary>
        public string detail;
    }

    /// <summary>
    /// 命令执行结果.
    /// </summary>
    internal sealed class CommandResult
    {
        /// <summary>
        /// 命令id.
        /// </summary>
        public string id;

        /// <summary>
        /// 命令类型.
        /// </summary>
        public string type;

        /// <summary>
        /// 状态.
        /// </summary>
        public string status;

        /// <summary>
        /// 开始时间.
        /// </summary>
        public string startedAt;

        /// <summary>
        /// 完成时间.
        /// </summary>
        public string finishedAt;

        /// <summary>
        /// 错误信息,仅在error状态下有值.
        /// </summary>
        public CommandError error;

        /// <summary>
        /// 业务结果,仅在success状态下有值.
        /// </summary>
        public JsonData result;

        /// <summary>
        /// 转换为结果json对象.
        /// </summary>
        /// <returns>可序列化的json数据.</returns>
        public JsonData ToJsonData()
        {
            JsonData jd = new JsonData();
            jd["id"] = id ?? "";
            jd["type"] = type ?? "";
            jd["status"] = status ?? "";
            jd["startedAt"] = startedAt ?? "";

            if (status == AgentCommandStatuses.Success || status == AgentCommandStatuses.Error)
            {
                jd["finishedAt"] = finishedAt ?? "";
            }

            if (status == AgentCommandStatuses.Error)
            {
                JsonData ejd = new JsonData();
                ejd["code"] = error != null ? (error.code ?? "") : "";
                ejd["message"] = error != null ? (error.message ?? "") : "";
                if (error != null && !string.IsNullOrEmpty(error.detail))
                {
                    ejd["detail"] = error.detail;
                }
                jd["error"] = ejd;
            }

            if (status == AgentCommandStatuses.Success)
            {
                jd["result"] = result ?? new JsonData();
            }

            return jd;
        }
    }

    /// <summary>
    /// 命令结果的状态常量(扩展,用于批量命令).
    /// </summary>
    internal static class BatchStatuses
    {
        /// <summary>
        /// 批次正在执行中.
        /// </summary>
        public const string Processing = "processing";

        /// <summary>
        /// 批次执行完成(包括全部成功或部分成功).
        /// </summary>
        public const string Completed = "completed";
    }

    /// <summary>
    /// 单个命令的输入定义(批量模式).
    /// </summary>
    internal sealed class BatchCommand
    {
        /// <summary>
        /// 命令唯一标识,由调用方提供.
        /// </summary>
        public string id;

        /// <summary>
        /// 命令类型,例如 "log.query".
        /// </summary>
        public string type;

        /// <summary>
        /// 命令参数,具体内容由命令类型决定.
        /// </summary>
        public JsonData @params;

        /// <summary>
        /// 命令级别超时时间(毫秒),优先级高于批次级别,默认 null(使用批次级别值).
        /// </summary>
        public int? timeout;
    }

    /// <summary>
    /// 批量命令的解析结果.
    /// </summary>
    internal sealed class BatchPendingCommand
    {
        /// <summary>
        /// 批次唯一标识,用作文件名 {batchId}.json.
        /// </summary>
        public string batchId;

        /// <summary>
        /// 批次级别超时时间(毫秒),默认 30000(30 秒).
        /// </summary>
        public int? timeout;

        /// <summary>
        /// 命令数组,每个元素是一个完整的命令对象.
        /// </summary>
        public List<BatchCommand> commands;

        /// <summary>
        /// 命令文件时间戳.
        /// </summary>
        public DateTime fileTime;
    }

    /// <summary>
    /// 批量命令中单个命令的执行结果.
    /// </summary>
    internal sealed class BatchCommandResult
    {
        /// <summary>
        /// 命令唯一标识(与输入一致).
        /// </summary>
        public string id;

        /// <summary>
        /// 命令类型(与输入一致).
        /// </summary>
        public string type;

        /// <summary>
        /// 命令状态,值: success/error.
        /// </summary>
        public string status;

        /// <summary>
        /// 命令开始执行时间戳.
        /// </summary>
        public string startedAt;

        /// <summary>
        /// 命令完成执行时间戳.
        /// </summary>
        public string finishedAt;

        /// <summary>
        /// 命令执行成功时的结果数据,内容由命令类型决定.
        /// </summary>
        public JsonData result;

        /// <summary>
        /// 命令执行失败时的错误信息.
        /// </summary>
        public CommandError error;

        /// <summary>
        /// 转换为结果json对象.
        /// </summary>
        /// <returns>可序列化的json数据.</returns>
        public JsonData ToJsonData()
        {
            JsonData jd = new JsonData();
            jd["id"] = id ?? "";
            jd["type"] = type ?? "";
            jd["status"] = status ?? "";
            jd["startedAt"] = startedAt ?? "";
            jd["finishedAt"] = finishedAt ?? "";

            if (status == AgentCommandStatuses.Error)
            {
                JsonData ejd = new JsonData();
                ejd["code"] = error != null ? (error.code ?? "") : "";
                ejd["message"] = error != null ? (error.message ?? "") : "";
                if (error != null && !string.IsNullOrEmpty(error.detail))
                {
                    ejd["detail"] = error.detail;
                }
                jd["error"] = ejd;
            }

            if (status == AgentCommandStatuses.Success)
            {
                jd["result"] = result ?? new JsonData();
            }

            return jd;
        }
    }

    /// <summary>
    /// 批量命令的执行结果.
    /// </summary>
    internal sealed class BatchResult
    {
        /// <summary>
        /// 批次唯一标识(与输入一致).
        /// </summary>
        public string batchId;

        /// <summary>
        /// 批次状态,值: processing/completed/error.
        /// </summary>
        public string status;

        /// <summary>
        /// 批次开始执行时间戳.
        /// </summary>
        public string startedAt;

        /// <summary>
        /// 批次完成执行时间戳.
        /// </summary>
        public string finishedAt;

        /// <summary>
        /// 命令结果数组,顺序与输入 commands 顺序一致.
        /// </summary>
        public List<BatchCommandResult> results;

        /// <summary>
        /// 总命令数.
        /// </summary>
        public int totalCommands;

        /// <summary>
        /// 成功执行的命令数.
        /// </summary>
        public int successCount;

        /// <summary>
        /// 失败的命令数.
        /// </summary>
        public int failedCount;

        /// <summary>
        /// 批次级别错误信息(仅 status=error 时有值).
        /// </summary>
        public CommandError error;

        /// <summary>
        /// 转换为结果json对象.
        /// </summary>
        /// <returns>可序列化的json数据.</returns>
        public JsonData ToJsonData()
        {
            JsonData jd = new JsonData();
            jd["batchId"] = batchId ?? "";
            jd["status"] = status ?? "";
            jd["startedAt"] = startedAt ?? "";

            if (status == BatchStatuses.Completed || status == AgentCommandStatuses.Error)
            {
                jd["finishedAt"] = finishedAt ?? "";
            }

            JsonData resultsJson = new JsonData();
            foreach (var result in results ?? new List<BatchCommandResult>())
            {
                resultsJson.Add(result.ToJsonData());
            }
            jd["results"] = resultsJson;

            jd["totalCommands"] = totalCommands;
            jd["successCount"] = successCount;
            jd["failedCount"] = failedCount;

            if (status == AgentCommandStatuses.Error && error != null)
            {
                JsonData ejd = new JsonData();
                ejd["code"] = error.code ?? "";
                ejd["message"] = error.message ?? "";
                if (!string.IsNullOrEmpty(error.detail))
                {
                    ejd["detail"] = error.detail;
                }
                jd["error"] = ejd;
            }

            return jd;
        }
    }
}
