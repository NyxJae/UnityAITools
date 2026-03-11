namespace UnityAgentSkills.SkillsExporter
{
    /// <summary>
    /// unity-log技能配置.
    /// </summary>
    public static class SkillConfig_UnityLog
    {
        /// <summary>
        /// skillId(也是导出目录名).
        /// </summary>
        public const string SkillName = "unity-log";

        /// <summary>
        /// 技能描述.
        /// </summary>
        public const string SkillDescription = "查询 Unity 编辑器日志,截图 Game 视图,并手动触发 Unity 刷新等待完成. 触发关键词:Unity:日志,Unity log,Unity:截图,Unity screenshot,Unity:刷新,Unity refresh";

        /// <summary>
        /// SKILL.md的完整内容.
        /// </summary>
        public const string SkillMarkdown = @"---
name: unity-log
description: 查询 Unity 编辑器日志,截图 Game 视图,并手动触发 Unity 刷新等待完成. 触发关键词:Unity:日志,Unity log,Unity:截图,Unity screenshot,Unity:刷新,Unity refresh
---

# Unity Log

## Instructions

### Context

本技能用于与 Unity Editor 通信,提供三类能力:

1) `log.query`: 查询 Unity 编辑器日志,支持按等级、关键词过滤,可选堆栈信息.
2) `log.screenshot`: 截图当前 Game 视图(包含 UI),并把截图落盘到 `Assets/UnityAgentSkills/Commands/results/`.
3) `log.refresh`: 手动触发一次 Unity 刷新,并等待到 Unity 不再处于刷新或编译状态.

### Steps

**工具脚本**: `<Scripts Directory>/execute_unity_command.py`

> 💡 请使用 `uv run` 执行(本机不保证存在全局 python).注意,以防命令行对多行字符串处理异常,请将JSON参数写在一行内.
> 💡 脚本最好加引号包裹,避免路径解析问题.

---

## 命令 1: log.query (查询日志)

### 输入参数(params)

| 参数名 | 类型 | 必填 | 默认值 | 说明 |
| --- | --- | --- | --- | --- |
| `n` | number | 是 | - | 返回最近 n 条日志. |
| `level` | string | 否 | - | 最佳实践: 不传表示不过滤等级.可选值: `Log`,`Warning`,`Error`.未识别值按不过滤处理. |
| `keyword` | string[] | 否 | - | 日志正文关键词过滤.空数组或全空词项表示不过滤.单个词也使用单元素数组. |
| `matchMode` | string | 否 | `Fuzzy` | 关键词匹配模式. `Fuzzy` 为 contains + IgnoreCase + OR, `Regex` 仅允许 1 个关键词元素. |
| `includeStack` | boolean | 否 | `false` | 是否在每条日志中包含 `stack` 字段. |

**单命令示例** (uv run):

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_log_001"",""timeout"":30000,""commands"":[{""id"":""cmd_001"",""type"":""log.query"",""params"":{""n"":50,""level"":""Error"",""keyword"":[""LoginFailed""],""includeStack"":true}}]}'
```

**多命令示例** (uv run):

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_logs_001"",""timeout"":30000,""commands"":[{""id"":""cmd_error"",""type"":""log.query"",""params"":{""n"":50,""level"":""Error""}},{""id"":""cmd_warning"",""type"":""log.query"",""params"":{""n"":100,""level"":""Warning""}}]}'
```

---

## 命令 2: log.screenshot (截图 Game 视图)

### 输入参数(params)

| 参数名 | 类型 | 必填 | 默认值 | 说明 |
| --- | --- | --- | --- | --- |
| `highlightRect` | object | 否 | - | 可选红框标注,格式 `{xMin,xMax,yMin,yMax}`. |

示例:

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_screenshot_001"",""timeout"":60000,""commands"":[{""id"":""cmd_001"",""type"":""log.screenshot"",""params"":{}}]}'
```

---

## 命令 3: log.refresh (刷新 Unity 并等待完成)

### 输入参数(params)

| 参数名 | 类型 | 必填 | 默认值 | 说明 |
| --- | --- | --- | --- | --- |
| - | - | - | - | `params` 允许直接传空对象 `{}`. 不支持额外字段,也不单独提供命令级 timeout. |

示例:

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_refresh_001"",""timeout"":30000,""commands"":[{""id"":""cmd_refresh"",""type"":""log.refresh"",""params"":{}}]}'
```

与日志联动示例:

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_refresh_then_logs_001"",""timeout"":30000,""commands"":[{""id"":""cmd_refresh"",""type"":""log.refresh"",""params"":{}},{""id"":""cmd_logs"",""type"":""log.query"",""params"":{""n"":50,""level"":""Error""}}]}'
```

成功结果至少会返回:

- `summary`: 中文自然语言说明,例如""已完成 Unity 刷新,并且这次过程中发生了编译."".
- `refreshTriggered`: 固定表示本次命令已显式触发刷新.
- `compilationOccurred`: 表示本次等待期间是否观察到编译发生.

---

### Notes

- `log.screenshot` 仅截图 Game 视图,且落盘完成后才会返回 `success`.
- `log.screenshot` 仅支持可选 `highlightRect`.
- `log.refresh` 会显式触发一次刷新,并等待到 Unity 不再处于刷新或编译状态.
- 批量命令采用串行执行,严格按输入顺序.
";
    }
}
