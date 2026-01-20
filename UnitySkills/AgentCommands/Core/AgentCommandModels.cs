using System;
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
}
