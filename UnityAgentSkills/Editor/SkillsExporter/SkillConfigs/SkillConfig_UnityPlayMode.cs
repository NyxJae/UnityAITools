namespace UnityAgentSkills.SkillsExporter
{
    /// <summary>
    /// unity-playmode技能配置.
    /// </summary>
    public static class SkillConfig_UnityPlayMode
    {
        /// <summary>
        /// 技能名称.
        /// </summary>
        public const string SkillName = "unity-playmode";

        /// <summary>
        /// 技能描述.
        /// </summary>
        public const string SkillDescription = "Unity Play Mode UI自动化交互技能. 触发关键词:Unity:PlayMode,Unity playmode,UI自动化";

        /// <summary>
        /// SKILL.md的完整内容.
        /// </summary>
        public const string SkillMarkdown = @"---
name: unity-playmode
description: Unity Play Mode UI自动化交互技能. 触发关键词:Unity:PlayMode,Unity playmode,UI自动化
---

# Unity PlayMode UI Automation

## Instructions

### Context

本技能用于在 Unity Play Mode 下执行 UI 自动化操作,支持启动/停止 Play Mode,查询UI,等待目标出现,点击,输入文本,滚动.

### Steps

**工具脚本**: `<Scripts Directory>/execute_unity_command.py`

> 💡 请使用 `uv run` 执行(本机不保证存在全局 python).注意,以防命令行对多行字符串处理异常,请将JSON参数写在一行内.
> 💡 脚本最好加引号包裹,避免路径解析问题.

---

## 支持命令

- `playmode.start`
- `playmode.stop`
- `playmode.queryUI`(推荐查询入口)
- `playmode.waitFor`
- `playmode.click`
- `playmode.clickAt`
- `playmode.setText`
- `playmode.scroll`
- `log.screenshot`(推荐验证入口)

---

## 常用示例

### 1) 启动 Play Mode

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_playmode_start_001"",""timeout"":30000,""commands"":[{""id"":""cmd_start"",""type"":""playmode.start"",""params"":{}}]}'
```

### 2) 查询当前可交互 UI

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_playmode_query_001"",""timeout"":30000,""commands"":[{""id"":""cmd_query"",""type"":""playmode.queryUI"",""params"":{""componentFilter"":[""Button"",""InputField""]}}]}'
```

### 3) 点击和输入

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_playmode_action_001"",""timeout"":30000,""commands"":[{""id"":""cmd_click"",""type"":""playmode.click"",""params"":{""targetPath"":""DialogMain/ConfirmButton"",""siblingIndex"":0}},{""id"":""cmd_set_text"",""type"":""playmode.setText"",""params"":{""targetPath"":""DialogMain/NameInput"",""siblingIndex"":0,""text"":""PlayerName"",""submit"":true}}]}'
```

### 4) 坐标点击和推荐查询

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_playmode_point_001"",""timeout"":30000,""commands"":[{""id"":""cmd_click_at"",""type"":""playmode.clickAt"",""params"":{""x"":960,""y"":540}},{""id"":""cmd_query"",""type"":""playmode.queryUI"",""params"":{""nameContains"":[""Confirm""],""componentFilter"":[""Button""],""visibleOnly"":true,""interactableOnly"":true,""maxResults"":100}}]}'
```

### 5) 等待目标出现(waitFor)

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_playmode_wait_001"",""timeout"":30000,""commands"":[{""id"":""cmd_wait"",""type"":""playmode.waitFor"",""params"":{""waitSeconds"":3,""nameContains"":[""Confirm""
]}}]}'
```

### 6) 验证截图(可选红框)

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_playmode_verify_001"",""timeout"":60000,""commands"":[{""id"":""cmd_shot"",""type"":""log.screenshot"",""params"":{""highlightRect"":{""xMin"":420,""xMax"":980,""yMin"":180,""yMax"":560}}}]}'
```

---

### Notes

- `playmode.queryUI` 支持 `textContains` 和 `screenRect`,用于减少返回体积并降低坐标依赖.
- `playmode.waitFor` 要求 `waitSeconds>0`,可选 `nameContains` 或 `textContains`,先命中或先超时都会结束.
- `log.screenshot` 仅输出全图,返回 `mode`,`imageAbsolutePath`,`highlightApplied`.
- `log.screenshot` 仅支持可选 `highlightRect`.使用坐标前请先此命令确认预估的坐标是否正确.例如:先用此命令查看所标记范围是否有框住关系的 UI.然后再用验证后的坐标去`playmode.queryUI`.
- 使用 `targetPath` 定位对象时,可配合 `siblingIndex` 精确定位同名节点.
- `clickAt` 的坐标必须在 GameView 范围内,否则会返回 `INVALID_COORDINATES`.
- `queryUI` 返回字段统一为 `uiElements`,推荐使用 `path + siblingIndex` 做后续定位.
- 在未启动 Play Mode 时执行交互命令会返回 `PLAYMODE_NOT_ACTIVE`.
";
    }
}
