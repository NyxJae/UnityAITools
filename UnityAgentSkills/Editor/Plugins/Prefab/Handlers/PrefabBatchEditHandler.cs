using System;
using System.Collections.Generic;
using LitJson2_utf;
using UnityAgentSkills.Core;
using UnityAgentSkills.Utils;
using UnityAgentSkills.Utils.JsonBuilders;
using UnityEditor;
using UnityEngine;

namespace UnityAgentSkills.Plugins.Prefab.Handlers
{
    /// <summary>
    /// prefab.batchEdit 命令处理器.
    /// 严格事务: 单次加载 PrefabContents,串行执行子操作,仅当全部成功时保存一次.
    /// </summary>
    internal static class PrefabBatchEditHandler
    {
        /// <summary>
        /// 支持的命令类型.
        /// </summary>
        public const string CommandType = "prefab.batchEdit";

        private const string ModeStopOnError = "stopOnError";
        private const string ModeContinueOnError = "continueOnError";

        private sealed class OperationInput
        {
            public string id;
            public string type;
            public JsonData @params;
        }

        /// <summary>
        /// 执行 batchEdit.
        /// </summary>
        public static JsonData Execute(JsonData rawParams)
        {
            CommandParams parameters = new CommandParams(rawParams);

            string prefabPath = PrefabComponentHandlerUtils.NormalizePrefabPath(parameters.GetString("prefabPath", null));
            PrefabComponentHandlerUtils.ValidatePrefabPathOrThrow(prefabPath);

            bool stopOnError = ParseModeOrThrow(parameters.GetString("mode", ModeStopOnError));

            if (!parameters.Has("operations"))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": operations is required");
            }

            JsonData operations = parameters.GetData()["operations"];
            if (operations == null || operations.GetJsonType() == JsonType.None || !operations.IsArray)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": operations must be an array");
            }

            if (operations.Count == 0)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": operations array must not be empty");
            }

            GameObject prefabRoot = PrefabComponentHandlerUtils.LoadPrefabContentsOrThrow(prefabPath);
            try
            {
                JsonData operationResults = JsonResultBuilder.CreateArray();

                int successCount = 0;
                int failedCount = 0;
                bool hasAnyError = false;

                for (int i = 0; i < operations.Count; i++)
                {
                    JsonData op = operations[i];

                    if (hasAnyError && stopOnError)
                    {
                        operationResults.Add(BuildSkippedOperationResult(op));
                        failedCount++;
                        continue;
                    }

                    string opId = TryGetString(op, "id");
                    string opType = TryGetString(op, "type");

                    try
                    {
                        OperationInput input = ParseOperationOrThrow(op, i);

                        if (input.@params != null && input.@params.IsObject && input.@params.ContainsKey("prefabPath"))
                        {
                            throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": operations[" + i + "].params must not contain prefabPath");
                        }

                        JsonData opResult = ExecuteOperation(prefabRoot, prefabPath, input);

                        JsonData ok = JsonResultBuilder.CreateObject();
                        ok["id"] = input.id;
                        ok["type"] = input.type;
                        ok["status"] = UnityAgentSkillCommandStatuses.Success;
                        ok["result"] = opResult;
                        operationResults.Add(ok);

                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        hasAnyError = true;

                        JsonData err = JsonResultBuilder.CreateObject();
                        err["id"] = opId;
                        err["type"] = opType;
                        err["status"] = UnityAgentSkillCommandStatuses.Error;
                        err["error"] = BuildOperationError(ex, opType);
                        operationResults.Add(err);

                        failedCount++;
                    }
                }

                bool saved = false;
                if (!hasAnyError)
                {
                    saved = PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                }

                JsonData result = JsonResultBuilder.CreateObject();
                result["prefabPath"] = prefabPath;
                result["operationCount"] = operations.Count;
                result["successCount"] = successCount;
                result["failedCount"] = failedCount;
                result["mode"] = stopOnError ? ModeStopOnError : ModeContinueOnError;
                result["saved"] = saved;
                result["operationResults"] = operationResults;
                return result;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static bool ParseModeOrThrow(string mode)
        {
            if (string.IsNullOrWhiteSpace(mode) || string.Equals(mode, ModeStopOnError, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(mode, ModeContinueOnError, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": mode must be stopOnError or continueOnError");
        }

        private static OperationInput ParseOperationOrThrow(JsonData op, int index)
        {
            if (op == null || !op.IsObject)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": operations[" + index + "] must be an object");
            }

            string id = TryGetString(op, "id");
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": operations[" + index + "].id is required");
            }

            string type = TryGetString(op, "type");
            if (string.IsNullOrWhiteSpace(type))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": operations[" + index + "].type is required");
            }

            if (!op.ContainsKey("params"))
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": operations[" + index + "].params is required");
            }

            JsonData p = op["params"];
            if (p == null || p.GetJsonType() == JsonType.None || !p.IsObject)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": operations[" + index + "].params must be an object");
            }

            return new OperationInput
            {
                id = id,
                type = type,
                @params = p
            };
        }

        private static string TryGetString(JsonData obj, string key)
        {
            if (obj == null || string.IsNullOrEmpty(key) || !obj.IsObject || !obj.ContainsKey(key))
            {
                return string.Empty;
            }

            JsonData v = obj[key];
            return v == null || v.GetJsonType() == JsonType.None ? string.Empty : v.ToString();
        }

        private static JsonData BuildSkippedOperationResult(JsonData op)
        {
            string opId = TryGetString(op, "id");
            string opType = TryGetString(op, "type");

            JsonData error = JsonResultBuilder.CreateObject();
            error["code"] = UnityAgentSkillCommandErrorCodes.Skipped;
            error["message"] = "因前序操作失败而跳过";
            error["detail"] = "因前序操作失败而跳过";

            JsonData result = JsonResultBuilder.CreateObject();
            result["id"] = opId;
            result["type"] = opType;
            result["status"] = UnityAgentSkillCommandStatuses.Error;
            result["error"] = error;
            return result;
        }

        private static JsonData BuildOperationError(Exception ex, string operationType)
        {
            if (ex == null)
            {
                return BuildErrorObject(UnityAgentSkillCommandErrorCodes.RuntimeError, "命令执行发生异常", "异常详情: ");
            }

            if (ex is ArgumentException && ex.Message != null
                && ex.Message.StartsWith(UnityAgentSkillCommandErrorCodes.InvalidFields + ":", StringComparison.Ordinal))
            {
                string detail = ex.Message.Substring(UnityAgentSkillCommandErrorCodes.InvalidFields.Length + 1).Trim();
                string userMessage = string.IsNullOrWhiteSpace(detail) ? "命令字段缺失或非法" : detail;
                return BuildErrorObject(UnityAgentSkillCommandErrorCodes.InvalidFields, userMessage, detail);
            }

            if (TryParseCodePrefixedError(ex.Message, out string code, out string message))
            {
                return BuildErrorObject(code, message, ex.Message);
            }

            if (ex is NotSupportedException)
            {
                string safeType = operationType ?? string.Empty;
                return BuildErrorObject(UnityAgentSkillCommandErrorCodes.UnknownType, "未知命令类型: " + safeType, ex.Message);
            }

            return BuildErrorObject(UnityAgentSkillCommandErrorCodes.RuntimeError, "命令执行发生异常", "异常详情: " + ex.Message);
        }

        private static bool TryParseCodePrefixedError(string message, out string code, out string parsedMessage)
        {
            code = null;
            parsedMessage = null;

            if (string.IsNullOrEmpty(message))
            {
                return false;
            }

            int separatorIndex = message.IndexOf(':');
            if (separatorIndex <= 0)
            {
                return false;
            }

            string maybeCode = message.Substring(0, separatorIndex).Trim();
            if (string.IsNullOrEmpty(maybeCode) || !IsKnownOperationErrorCode(maybeCode))
            {
                return false;
            }

            string maybeMessage = message.Substring(separatorIndex + 1).TrimStart();

            code = maybeCode;
            parsedMessage = string.IsNullOrEmpty(maybeMessage) ? maybeCode : maybeMessage;
            return true;
        }

        private static bool IsKnownOperationErrorCode(string code)
        {
            switch (code)
            {
                case UnityAgentSkillCommandErrorCodes.InvalidFields:
                case UnityAgentSkillCommandErrorCodes.UnknownType:
                case UnityAgentSkillCommandErrorCodes.InvalidRegex:
                case UnityAgentSkillCommandErrorCodes.RuntimeError:
                case UnityAgentSkillCommandErrorCodes.Timeout:
                case UnityAgentSkillCommandErrorCodes.Skipped:
                case UnityAgentSkillCommandErrorCodes.OnlyAllowedInEditMode:
                case UnityAgentSkillCommandErrorCodes.PrefabNotFound:
                case UnityAgentSkillCommandErrorCodes.GameObjectNotFound:
                case UnityAgentSkillCommandErrorCodes.ComponentTypeNotFound:
                case UnityAgentSkillCommandErrorCodes.AmbiguousComponentType:
                case UnityAgentSkillCommandErrorCodes.ComponentNotFound:
                case UnityAgentSkillCommandErrorCodes.ComponentAlreadyExists:
                case UnityAgentSkillCommandErrorCodes.CannotDeleteRequiredComponent:
                case UnityAgentSkillCommandErrorCodes.PropertyNotFound:
                case UnityAgentSkillCommandErrorCodes.InvalidPropertyPath:
                case UnityAgentSkillCommandErrorCodes.TypeMismatch:
                case UnityAgentSkillCommandErrorCodes.ReferenceTargetNotFound:
                case UnityAgentSkillCommandErrorCodes.ReferenceTargetTypeMismatch:
                case UnityAgentSkillCommandErrorCodes.AssetNotFound:
                case UnityAgentSkillCommandErrorCodes.AssetTypeMismatch:
                case UnityAgentSkillCommandErrorCodes.EmptyProperties:
                case UnityAgentSkillCommandErrorCodes.EmptyModifications:
                case UnityAgentSkillCommandErrorCodes.IdNotFound:
                case UnityAgentSkillCommandErrorCodes.IndexOutOfRange:
                    return true;

                default:
                    return false;
            }
        }

        private static JsonData BuildErrorObject(string code, string message, string detail)
        {
            JsonData err = JsonResultBuilder.CreateObject();
            err["code"] = code ?? UnityAgentSkillCommandErrorCodes.RuntimeError;
            err["message"] = message ?? string.Empty;
            err["detail"] = detail ?? string.Empty;
            return err;
        }

        private static JsonData ExecuteOperation(GameObject prefabRoot, string prefabPath, OperationInput input)
        {
            if (input == null)
            {
                throw new ArgumentException(UnityAgentSkillCommandErrorCodes.InvalidFields + ": operation is required");
            }

            return PrefabBatchEditOperationExecutor.Execute(prefabRoot, prefabPath, input.type, input.@params);
        }

    }
}
