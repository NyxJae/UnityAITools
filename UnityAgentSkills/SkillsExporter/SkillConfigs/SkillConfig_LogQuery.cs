namespace UnityAgentSkills.SkillsExporter
{
    /// <summary>
    /// unity-log-query技能配置.
    /// </summary>
    public static class SkillConfig_LogQuery
    {
        /// <summary>
        /// 技能名称.
        /// </summary>
        public const string SkillName = "unity-log-query";

        /// <summary>
        /// 技能描述.
        /// </summary>
        public const string SkillDescription = "查询 Unity 编辑器日志. 触发关键词:Unity:日志,Unity log";

        /// <summary>
        /// SKILL.md的完整内容.
        /// </summary>
        public const string SkillMarkdown = @"---
name: unity-log-query
description: 查询 Unity 编辑器日志. 触发关键词:Unity:日志,Unity log
---

# Unity Log Query

## Instructions

### Context

本技能用于查询 Unity 编辑器日志,支持按等级、关键词过滤,并包含堆栈信息.

### Steps

**工具脚本**: `<Scripts Directory>/execute_unity_command.py`

**最简单的调用方式** - 直接命令行传参(推荐):

> 💡 使用 `python` 或 `uv run` 执行.注意,以防命令行对多行字符串处理异常,请将JSON参数写在一行内.
> 💡 脚本最好加引号包裹,避免路径解析问题.

**单命令示例** (python):

```bash
python ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_log_001"",""timeout"":30000,""commands"":[{""id"":""cmd_001"",""type"":""log.query"",""params"":{""n"":50,""level"":""Error"",""keyword"":""LoginFailed"",""includeStack"":true}}]}'
```

**多命令示例** (uv run):

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_logs_001"",""timeout"":30000,""commands"":[{""id"":""cmd_error"",""type"":""log.query"",""params"":{""n"":50,""level"":""Error""}},{""id"":""cmd_warning"",""type"":""log.query"",""params"":{""n"":100,""level"":""Warning""}}]}'
```

**命令参数说明**:

- `batchId` 必填,批次唯一标识(建议 16-32 字符,仅 `[a-zA-Z0-9_-]`)
- `timeout` 可选,超时时间(毫秒),默认 30000
- `commands` 必填,命令数组,每个元素包含:
  - `id` 必填,命令唯一标识
  - `type` 必填,命令类型,固定为 `""log.query""`
  - `params` 必填,查询参数:
    - `n` 必填,返回最近 n 条日志
    - `level` 可选,日志等级: `Log`/`Warning`/`Error`
    - `keyword` 可选,关键词过滤
    - `matchMode` 可选,匹配模式: `Fuzzy`(默认)/`Regex`
    - `includeStack` 可选,是否包含堆栈,默认 `false`

**Python代码调用** (备选方式):

```python
from scripts.execute_unity_command import execute_command
result = execute_command({""batchId"":""x"",""commands"":[{""type"":""log.query"",""params"":{""n"":50,""level"":""Error""}}]})
```

**返回结果示例**:

```json
{
  ""batchId"": ""batch_logs_001"",
  ""status"": ""completed"",
  ""results"": [
    {
      ""id"": ""cmd_error"",
      ""type"": ""log.query"",
      ""status"": ""success"",
      ""result"": {
        ""items"": [
          {
            ""time"": ""2026-01-20T07:53:00Z"",
            ""level"": ""Error"",
            ""message"": ""Login failed"",
            ""stack"": ""UnityEngine.Debug:LogError(...)""
          }
        ],
        ""totalCaptured"": 150,
        ""returned"": 10
      }
    }
  ],
  ""successCount"": 1,
  ""failedCount"": 0
}
```

### Notes

- 命令行方式无需创建任何文件,直接在终端执行即可
- 批量命令采用串行执行,严格按输入顺序
- 批量命令支持部分成功模式,单个命令失败不影响后续执行
- 正则非法会返回 error,不会崩溃插件
- 命令超时优先级高于批次超时
- `status` 可能为 `processing`/`completed`/`error`
- `error.message` 为中文错误提示,可直接展示

### 推荐工作流程
0. 之前已经加好了日志等代码
1. 先让用户触发一次目标日志(或给出可复现步骤)
2. 汇总查询参数:日志等级、关键词、是否需要堆栈
3. 直接在命令行执行 Python 脚本,传入 JSON 参数
4. 查看返回的 JSON 结果,分析日志内容
5. 必要时引导用户再次触发或调整参数
";
    }
}
