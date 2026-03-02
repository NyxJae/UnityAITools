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
        public const string SkillDescription = "查询 Unity 编辑器日志与截图 Game 视图. 触发关键词:Unity:日志,Unity log,Unity:截图,Unity screenshot";

        /// <summary>
        /// SKILL.md的完整内容.
        /// </summary>
        public const string SkillMarkdown = @"---
name: unity-log
description: 查询 Unity 编辑器日志与截图 Game 视图. 触发关键词:Unity:日志,Unity log,Unity:截图,Unity screenshot
---

# Unity Log

## Instructions

### Context

本技能用于与 Unity Editor 通信,提供两类能力:

1) `log.query`: 查询 Unity 编辑器日志,支持按等级、关键词过滤,可选堆栈信息.
2) `log.screenshot`: 截图当前 Game 视图(包含 UI),并把截图落盘到 `Assets/UnityAgentSkills/results/`.

### Steps

**工具脚本**: `<Scripts Directory>/execute_unity_command.py`

> 💡 请使用 `uv run` 执行(本机不保证存在全局 python).注意,以防命令行对多行字符串处理异常,请将JSON参数写在一行内.
> 💡 脚本最好加引号包裹,避免路径解析问题.

---

## 命令 1: log.query (查询日志)

**单命令示例** (uv run):

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_log_001"",""timeout"":30000,""commands"":[{""id"":""cmd_001"",""type"":""log.query"",""params"":{""n"":50,""level"":""Error"",""keyword"":""LoginFailed"",""includeStack"":true}}]}'
```

**多命令示例** (uv run):

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_logs_001"",""timeout"":30000,""commands"":[{""id"":""cmd_error"",""type"":""log.query"",""params"":{""n"":50,""level"":""Error""}},{""id"":""cmd_warning"",""type"":""log.query"",""params"":{""n"":100,""level"":""Warning""}}]}'
```

---

## 命令 2: log.screenshot (截图 Game 视图)

示例:

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_screenshot_001"",""timeout"":60000,""commands"":[{""id"":""cmd_001"",""type"":""log.screenshot"",""params"":{}}]}'
```

---

### Notes

- `log.screenshot` 仅截图 Game 视图,且落盘完成后才会返回 `success`.
- `log.screenshot` 仅支持可选 `highlightRect`.
- 批量命令采用串行执行,严格按输入顺序.
";
    }
}
