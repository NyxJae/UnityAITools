namespace UnityAgentSkills.SkillsExporter
{
    /// <summary>
    /// unity-editor-control 技能配置.
    /// </summary>
    public static class SkillConfig_UnityEditorControl
    {
        /// <summary>
        /// 技能名称.
        /// </summary>
        public const string SkillName = "unity-editor-control";

        /// <summary>
        /// 技能描述.
        /// </summary>
        public const string SkillDescription = "控制 Unity 编辑器状态,选择,撤销重做与菜单执行. 触发关键词:Unity:编辑器控制,Unity editor control,选择对象,撤销重做";

        /// <summary>
        /// SKILL.md 的完整内容.
        /// </summary>
        public const string SkillMarkdown = @"---
name: unity-editor-control
description: 控制 Unity 编辑器状态,选择,撤销重做与菜单执行. 触发关键词:Unity:编辑器控制,Unity editor control,选择对象,撤销重做
---

# Unity Editor Control

## Instructions

### Context

本技能用于读取 Unity Editor 当前状态与上下文,选择对象或资源,执行菜单命令,执行撤销与重做,以及控制 Pause On Error 开关.

本技能与 `unity-editor-action` 的边界:
- `unity-editor-control` 负责显式 `editor.*` 控制命令.
- `unity-editor-action` 继续只负责 `editor.runAction`.
- 已知稳定菜单路径时,优先使用 `editor.executeMenu`,不要绕回 `editor.runAction`.

### Steps

**工具脚本**: `<Scripts Directory>/execute_unity_command.py`

> 💡 请使用 `uv run` 执行. 注意,请将 JSON 参数写在一行内.
> 💡 脚本最好加引号包裹,避免路径解析问题.

## 支持命令

- `editor.getState`
- `editor.getContext`
- `editor.select`
- `editor.getSelection`
- `editor.undo`
- `editor.redo`
- `editor.executeMenu`
- `editor.getTags`
- `editor.getLayers`
- `editor.setPauseOnError`

## 示例

### 1) 读取编辑器状态

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_editor_state_001"",""timeout"":30000,""commands"":[{""id"":""cmd_state"",""type"":""editor.getState"",""params"":{}}]}'
```

### 2) 选择场景对象并读取当前 selection

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_editor_select_001"",""timeout"":30000,""commands"":[{""id"":""cmd_select"",""type"":""editor.select"",""params"":{""targetKind"":""sceneGameObject"",""sceneName"":""Main"",""objectPath"":""Canvas/Panel/StartButton"",""siblingIndex"":0}},{""id"":""cmd_selection"",""type"":""editor.getSelection"",""params"":{}}]}'
```

### 3) 执行菜单

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_editor_menu_001"",""timeout"":30000,""commands"":[{""id"":""cmd_menu"",""type"":""editor.executeMenu"",""params"":{""menuPath"":""Tools/Card/Export Selected""}}]}'
```

### 4) 撤销与重做

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_editor_undo_redo_001"",""timeout"":30000,""commands"":[{""id"":""cmd_undo"",""type"":""editor.undo"",""params"":{""steps"":1}},{""id"":""cmd_redo"",""type"":""editor.redo"",""params"":{""steps"":1}}]}'
```

## Notes

- `editor.select` 首版仅支持单目标选择,目标类型为 `sceneGameObject` 或 `projectAsset`.
- `editor.undo` / `editor.redo` 的 `params.steps` 必须 >= 1.
- `editor.executeMenu` 命中高风险菜单时会被拒绝执行.
- 常见错误码:
  - `INVALID_FIELDS`: 参数缺失或格式错误.
  - `ONLY_ALLOWED_IN_EDIT_MODE`: 当前仅允许在编辑模式执行.
  - `ASSET_NOT_FOUND`: 资源不存在.
  - `AMBIGUOUS_TARGET`: 目标存在歧义,系统不能安全猜测.
  - `MENU_ITEM_NOT_FOUND`: 菜单路径不存在.
  - `MENU_ITEM_NOT_EXECUTABLE`: 菜单存在,但当前上下文下不可执行.
  - `FORBIDDEN_EDITOR_ACTION`: 命中高风险菜单,已被拦截.
";
    }
}
