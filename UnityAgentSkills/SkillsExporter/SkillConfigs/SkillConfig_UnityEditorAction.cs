namespace UnityAgentSkills.SkillsExporter
{
    /// <summary>
    /// unity-editor-action 技能配置.
    /// </summary>
    public static class SkillConfig_UnityEditorAction
    {
        /// <summary>
        /// 技能名称.
        /// </summary>
        public const string SkillName = "unity-editor-action";

        /// <summary>
        /// 技能描述.
        /// </summary>
        public const string SkillDescription = "运行 Unity 编辑器动作. 触发关键词:Unity:编辑器命令,Unity action,运行菜单命令";

        /// <summary>
        /// SKILL.md 的完整内容.
        /// </summary>
        public const string SkillMarkdown = @"---
name: unity-editor-action
description: 运行 Unity 编辑器动作. 触发关键词:Unity:编辑器命令,Unity action,运行菜单命令
---

# Unity Editor Action

## Instructions

### Context

本技能用于执行 `editor.runAction` 命令,触发 Unity 编辑器中的公开静态方法.
典型场景: 通过 `MenuItem` 对应的方法完成资源导出,工具刷新,编辑器批处理动作.

### Steps

**工具脚本**: `<Scripts Directory>/execute_unity_command.py`

> 💡 请使用 `uv run` 执行(本机不保证存在全局 python).注意,以防命令行对多行字符串处理异常,请将JSON参数写在一行内.
> 💡 脚本最好加引号包裹,避免路径解析问题.

---

## 命令: editor.runAction

`editor.runAction` 使用完整限定名 `actionId` 执行动作:

- `type` 固定为 `editor.runAction`.
- `params.actionId` 固定为 `Namespace.ClassName.MethodName`.
- `params.actionArgs` 可选,用于参数绑定.

**单命令示例** (uv run):
```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_editor_action_001"",""timeout"":60000,""commands"":[{""id"":""cmd_001"",""type"":""editor.runAction"",""params"":{""actionId"":""UnityEditor.AssetDatabase.SaveAssets""}}]}'
```

**带参数示例** (uv run):

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_editor_action_002"",""timeout"":60000,""commands"":[{""id"":""cmd_001"",""type"":""editor.runAction"",""params"":{""actionId"":""UnityAgentSkills.Core.CommandErrorFactory.CreateSkippedError"",""actionArgs"":{""batchTimeoutMs"":5000}}}]}'
```

**多命令示例** (uv run):

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_editor_action_003"",""timeout"":60000,""commands"":[{""id"":""cmd_001"",""type"":""editor.runAction"",""params"":{""actionId"":""UnityAgentSkills.UI.SkillsExporterWindow.RestartCommandServiceMenu""}},{""id"":""cmd_002"",""type"":""editor.runAction"",""params"":{""actionId"":""UnityEditor.AssetDatabase.Refresh""}}]}'
```

---

### Notes

- `actionId` 需要先在代码中定位到目标 `public static` 方法,常见入口是 `MenuItem` 标注的方法.
- 错误码语义:
  - `UNKNOWN_TYPE`: 命令类型未注册.
  - `INVALID_FIELDS`: 参数缺失,类型错误,或格式非法.
  - `RUNTIME_ERROR`: actionId 不存在,执行异常,或命中高风险黑名单.
- 批量命令采用串行执行,严格按输入顺序.
";
    }
}
