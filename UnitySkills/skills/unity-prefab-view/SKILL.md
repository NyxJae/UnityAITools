---
name: unity-prefab-view
description: 查看 Unity 预制体信息. 触发关键词:Unity:预制体,Unity prefab,查看预制体
---

# Unity Prefab View

## Instructions

### Context

本技能用于查看 Unity 预制体信息,包括层级结构和组件属性

### Steps

1. 确认目录

   - 输入目录: `Assets/AgentCommands/pending/`
   - 输出目录: `Assets/AgentCommands/results/`
   - 归档目录: `Assets/AgentCommands/done/`
     注意 MUST 只寻找以上目录,若找不到,则说明用户未安装必要的插件,需要友好提醒用户安装.MUSTNOT 创建以上目录!!!

2. 生成命令文件

   使用批量命令格式,即使只执行一个命令也必须使用批量格式

   - 文件名: `{batchId}.json`,batchId 建议 16-32 字符,仅 `[a-zA-Z0-9_-]`
   - 写入 `pending/` 后,一般瞬间完成,可根据 batchId 推测出结果文件路径,结果文件名也会是`{batchId}.json`

### 命令 1: prefab.queryHierarchy (查询层级结构)

单命令示例:
batch_prefab_hierarchy_001.json

```json
{
  "batchId": "batch_prefab_hierarchy_001",
  "timeout": 30000,
  "commands": [
    {
      "id": "cmd_001",
      "type": "prefab.queryHierarchy",
      "params": {
        "prefabPath": "Assets/Resources/Prefabs/DialogMain.prefab",
        "includeInactive": true,
        "maxDepth": -1
      }
    }
  ]
}
```

params 说明:

- `prefabPath` 必填,预制体绝对路径(必须以 "Assets/" 开头),例如 "Assets/Prefabs/DialogMain.prefab"
- 路径分隔符自动适配,支持 Windows 反斜杠(\) 和 macOS/Linux 正斜杠(/)
- `includeInactive` 可选,是否包含禁用的 GameObject,默认 true
- `maxDepth` 可选,最大遍历深度,-1 表示无限,默认 -1

输出示例:

```json
{
  "batchId": "batch_prefab_hierarchy_001",
  "status": "completed",
  "results": [
    {
      "id": "cmd_001",
      "type": "prefab.queryHierarchy",
      "status": "success",
      "result": {
        "prefabPath": "Assets/Resources/Prefabs/DialogMain.prefab",
        "rootName": "DialogMain",
        "totalGameObjects": 15,
        "hierarchy": [
          {
            "name": "DialogMain",
            "instanceID": 123456789,
            "path": "DialogMain",
            "depth": 0,
            "isActive": true,
            "children": [
              {
                "name": "Panel_Content",
                "instanceID": 234567890,
                "path": "DialogMain/Panel_Content",
                "depth": 1,
                "isActive": true,
                "children": []
              }
            ]
          }
        ]
      }
    }
  ]
}
```

### 命令 2: prefab.queryComponents (查询组件信息)

单命令示例:
batch_prefab_components_001.json

```json
{
  "batchId": "batch_prefab_components_001",
  "timeout": 30000,
  "commands": [
    {
      "id": "cmd_001",
      "type": "prefab.queryComponents",
      "params": {
        "prefabPath": "Assets/Resources/Prefabs/DialogMain.prefab",
        "objectPath": "DialogMain/Panel_Content/K3Button_Confirm",
        "componentFilter": ["K3Button"],
        "includeBuiltin": false,
        "includePrivateFields": false
      }
    }
  ]
}
```

params 说明:

- `prefabPath` 必填,预制体绝对路径(必须以 "Assets/" 开头),例如 "Assets/Prefabs/DialogMain.prefab"
- 路径分隔符自动适配,支持 Windows 反斜杠(\) 和 macOS/Linux 正斜杠(/)
- `objectPath` 必填,GameObject 层级路径(从 prefab.queryHierarchy 返回的 path 字段获取)
- `componentFilter` 可选,组件类型过滤,null 表示全部
- `includeBuiltin` 可选,是否包含 Unity 内置组件(RectTransform, Transform 等),默认 false
- `includePrivateFields` 可选,是否包含私有字段(带 SerializeField 标记的),默认 false

输出示例:

```json
{
  "batchId": "batch_prefab_components_001",
  "status": "completed",
  "results": [
    {
      "id": "cmd_001",
      "type": "prefab.queryComponents",
      "status": "success",
      "result": {
        "objectPath": "DialogMain/Panel_Content/K3Button_Confirm",
        "instanceID": 345678901,
        "totalComponents": 1,
        "components": [
          {
            "type": "K3Button",
            "instanceID": 789012345,
            "scriptPath": "Assets/Scripts/HotUpdate/K3Engine/Component/K3Button.cs",
            "properties": {
              "interactable": true,
              "transitionType": 0
            }
          }
        ]
      }
    }
  ]
}
```
3. 直接读取结果
   你可以推测出结果文件路径,可直接尝试读取,一般能直接拿到结果,不行就再读一次试试.不必用`ls`,`sleep`等命令

### Notes

- results 仅保留最近 20 条最终结果,建议及时读取
- objectPath 必须从 prefab.queryHierarchy 的结果中获取,确保路径正确
- prefab.queryComponents 中 properties 支持嵌套结构(数组、对象等)
- 批量命令采用串行执行,严格按输入顺序
- 批量命令支持部分成功模式,单个命令失败不影响后续执行
- instanceID 用于会话中快速定位,跨会话请使用 path

### 流程

1. 先用 prefab.queryHierarchy 获取预制体的完整层级结构
2. 根据层级结构找到感兴趣的 GameObject 及其 path
3. 用 prefab.queryComponents 查询该 GameObject 的组件信息
4. 如需查看多个 GameObject,可在批量命令中组合使用两个命令
