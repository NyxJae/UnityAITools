namespace UnityAgentSkills.SkillsExporter
{
    /// <summary>
    /// unity-k3-prefab技能配置.
    /// </summary>
    public static class SkillConfig_K3Prefab
    {
        /// <summary>
        /// 技能名称.
        /// </summary>
        public const string SkillName = "unity-k3-prefab";

        /// <summary>
        /// 技能描述.
        /// </summary>
        public const string SkillDescription = "K3框架预制体查询与编辑工具. 触发关键词:Unity:K3预制体,Unity:K3 prefab,Unity:K3UI";

        /// <summary>
        /// SKILL.md的完整内容.
        /// </summary>
        public const string SkillMarkdown = @"---
name: unity-k3-prefab
description: K3框架预制体查询与编辑工具. 触发关键词:Unity:K3预制体,Unity:K3 prefab,Unity:K3UI
---

# Unity K3 Prefab Editor

## Instructions

### Context

本技能用于查询和编辑 K3 框架的 UI 预制体，支持通过 K3ID 查询组件、修改 K3 组件属性、修改 GameObject 属性。

### Steps

**工具脚本**: `<Scripts Directory>/execute_unity_command.py`

**最简单的调用方式** - 直接命令行传参(推荐):

> 💡 使用 `python` 或 `uv run` 执行.注意,以防命令行对多行字符串处理异常,请将JSON参数写在一行内.
> 💡 脚本最好加引号包裹,避免路径解析问题.

---

## 命令 1: k3prefab.queryByK3Id (通过K3ID查询组件)

通过 K3ID 快速查询 K3 框架组件，无需知道 GameObject 路径。

**单命令示例**:

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_k3_query_001"",""timeout"":30000,""commands"":[{""id"":""cmd_001"",""type"":""k3prefab.queryByK3Id"",""params"":{""prefabPath"":""Assets/ResourcesAB/UIPrefabs/DialogMain.prefab"",""k3Id"":6}}]}'
```

**参数说明**:

- `prefabPath` 必填，预制体绝对路径(必须以 ""Assets/"" 开头)
- `k3Id` 必填，K3 组件的 ID (uint 类型，与 Lua 代码中使用的 ID 一致)
- `componentFilter` 可选，组件类型过滤数组，如 `[""K3Button""]`，null 表示返回所有类型

**返回结果示例**:

```json
{
  ""batchId"": ""batch_k3_query_001"",
  ""status"": ""completed"",
  ""results"": [
    {
      ""id"": ""cmd_001"",
      ""type"": ""k3prefab.queryByK3Id"",
      ""status"": ""success"",
      ""result"": {
        ""prefabPath"": ""Assets/ResourcesAB/UIPrefabs/DialogMain.prefab"",
        ""k3Id"": 6,
        ""totalMatches"": 1,
        ""components"": [
          {
            ""index"": 0,
            ""gameObjectPath"": ""DialogMain/Panel_Content/K3Button_Confirm"",
            ""containerPath"": ""DialogMain"",
            ""containerType"": ""K3Dialog"",
            ""gameObjectProperties"": {
              ""name"": ""K3Button_Confirm"",
              ""tag"": ""Untagged"",
              ""layer"": 5,
              ""isActive"": true
            },
            ""k3Component"": {
              ""type"": ""K3Button"",
              ""instanceID"": 345678901,
              ""properties"": {
                ""interactable"": true,
                ""alpha"": 1.0,
                ""ID"": 6
              }
            }
          }
        ]
      }
    }
  ]
}
```

---

## 命令 2: k3prefab.setComponentProperties (修改K3组件属性)

通过 K3ID 精确修改 K3 组件的属性，支持乐观锁（验证旧值后才修改）。

**单命令示例**:

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_k3_modify_001"",""timeout"":30000,""commands"":[{""id"":""cmd_001"",""type"":""k3prefab.setComponentProperties"",""params"":{""prefabPath"":""Assets/ResourcesAB/UIPrefabs/DialogMain.prefab"",""k3Id"":6,""index"":0,""modifications"":[{""property"":""alpha"",""oldValue"":1.0,""newValue"":0.5},{""property"":""interactable"",""oldValue"":true,""newValue"":false}]}}]}'
```

**参数说明**:

- `prefabPath` 必填，预制体绝对路径
- `k3Id` 必填，K3 组件的 ID
- `index` 可选，同 K3ID 中的索引（用于精确定位），默认为 0
- `modifications` 必填，修改请求数组，每个元素包含:
  - `property` 属性名称 (如 ""alpha""、""interactable""、""text"" 等)
  - `oldValue` 期望的旧值（用于验证）
  - `newValue` 要修改的新值

**返回结果示例**:

```json
{
  ""batchId"": ""batch_k3_modify_001"",
  ""status"": ""completed"",
  ""results"": [
    {
      ""id"": ""cmd_001"",
      ""type"": ""k3prefab.setComponentProperties"",
      ""status"": ""success"",
      ""result"": {
        ""prefabPath"": ""Assets/ResourcesAB/UIPrefabs/DialogMain.prefab"",
        ""k3Id"": 6,
        ""index"": 0,
        ""gameObjectPath"": ""DialogMain/Panel_Content/K3Button_Confirm"",
        ""componentType"": ""K3Button"",
        ""modifications"": [
          {
            ""property"": ""alpha"",
            ""oldValue"": 1.0,
            ""currentValue"": 1.0,
            ""newValue"": 0.5,
            ""status"": ""success"",
            ""message"": ""属性修改成功""
          }
        ],
        ""currentProperties"": {
          ""interactable"": false,
          ""alpha"": 0.5,
          ""ID"": 6
        },
        ""saved"": true,
        ""summary"": {
          ""total"": 2,
          ""success"": 2,
          ""skipped"": 0,
          ""failed"": 0
        }
      }
    }
  ]
}
```

---

## 命令 3: prefab.setGameObjectProperties (修改GameObject属性)

修改预制体中指定 GameObject 的属性 (name, tag, layer, isActive 等)。

**单命令示例**:

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""_batch_goprops_001"",""timeout"":30000,""commands"":[{""id"":""cmd_001"",""type"":""prefab.setGameObjectProperties"",""params"":{""prefabPath"":""Assets/ResourcesAB/UIPrefabs/DialogMain.prefab"",""objectPath"":""DialogMain/Panel_Content/K3Button_Confirm"",""properties"":{""name"":""K3Button_Confirm_New"",""layer"":5}}]}'
```

**参数说明**:

- `prefabPath` 必填，预制体绝对路径
- `objectPath` 必填，GameObject 层级路径 (从 k3prefab.queryByK3Id 返回的 gameObjectPath 获取)
- `siblingIndex` 可选，同名对象索引，默认为 0
- `properties` 必填，要修改的属性对象，支持的字段:
  - `name` (string) 对象名称
  - `tag` (string) 标签
  - `layer` (int) 层级 (0-31)
  - `isActive` (bool) 激活状态
  - `isStatic` (bool) 静态标记
  - `hideFlags` (int) 隐藏标志

**返回结果示例**:

```json
{
  ""batchId"": ""batch_goprops_001"",
  ""status"": ""completed"",
  ""results"": [
    {
      ""id"": ""cmd_001"",
      ""type"": ""prefab.setGameObjectProperties"",
      ""status"": ""success"",
      ""result"": {
        ""prefabPath"": ""Assets/ResourcesAB/UIPrefabs/DialogMain.prefab"",
        ""objectPath"": ""DialogMain/Panel_Content/K3Button_Confirm"",
        ""instanceID"": 345678901,
        ""modifiedProperties"": [
          {
            ""name"": ""name"",
            ""oldValue"": ""K3Button_Confirm"",
            ""newValue"": ""K3Button_Confirm_New""
          }
        ],
        ""currentProperties"": {
          ""name"": ""K3Button_Confirm_New"",
          ""tag"": ""Untagged"",
          ""layer"": 5,
          ""isActive"": true
        },
        ""saved"": true
      }
    }
  ]
}
```

---

## 命令 3: k3prefab.createComponent (创建K3UI组件)

在预制体中创建新的 K3UI 组件（K3Button、K3Label、K3Image 等），自动分配 K3ID 并设置初始属性。

**单命令示例** - 创建一个按钮:

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_create_001"",""timeout"":30000,""commands"":[{""id"":""cmd_create_button"",""type"":""k3prefab.createComponent"",""params"":{""prefabPath"":""Assets/ResourcesAB/UIPrefabs/DialogMain.prefab"",""parentPath"":""DialogMain/Panel_Content"",""componentType"":""K3Button"",""initialProperties"":{""interactable"":true,""alpha"":1.0}}}]}'
```

**参数说明**:

- `prefabPath` 必填，预制体绝对路径(必须以 ""Assets/"" 开头)
- `parentPath` 必填，父节点路径(必须是实现 IK3Container 的节点，如 K3Dialog、K3Panel)
- `componentType` 必填，K3UI 组件类型名(支持: K3Button, K3Label, K3Image, K3Edit, K3CheckBox, K3Panel, K3ListView 等 20+ 种类型)
- `initialProperties` 可选，组件初始属性对象，不同组件类型支持不同的属性

**支持的组件类型**:

K3Button, K3Label, K3Image, K3Edit, K3CheckBox, K3LinkLabel, K3Panel, K3ListView, K3Dialog, K3Dialog2, K3TabButton, K3Tab, K3ProgressBar, K3RadarChart, K3HeadIcon, K3Itembox, K3LabelButton, K3Animation, K3SliderBar, K3Movie, K3NumImage, K3JoyStick, K3Magicbox, K3ExpandListView, K3ExpandListPanel, K3InsightImage

**返回结果示例**:

```json
{
  ""batchId"": ""batch_create_001"",
  ""status"": ""completed"",
  ""results"": [
    {
      ""id"": ""cmd_create_button"",
      ""type"": ""k3prefab.createComponent"",
      ""status"": ""success"",
      ""result"": {
        ""prefabPath"": ""Assets/ResourcesAB/UIPrefabs/DialogMain.prefab"",
        ""parentPath"": ""DialogMain/Panel_Content"",
        ""componentType"": ""K3Button"",
        ""gameObject"": {
          ""name"": ""K3Button_6"",
          ""path"": ""DialogMain/Panel_Content/K3Button_6"",
          ""instanceID"": 123456789
        },
        ""k3Component"": {
          ""type"": ""K3Button"",
          ""properties"": {
            ""ID"": 6,
            ""parentID"": 3,
            ""interactable"": true,
            ""alpha"": 1.0
          }
        },
        ""saved"": true
      }
    }
  ]
}
```

**批量创建示例** - 创建多个组件:

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_create_multiple"",""timeout"":30000,""commands"":[{""id"":""cmd_create_button"",""type"":""k3prefab.createComponent"",""params"":{""prefabPath"":""Assets/ResourcesAB/UIPrefabs/DialogMain.prefab"",""parentPath"":""DialogMain/Panel_Content"",""componentType"":""K3Button""}},{""id"":""cmd_create_label"",""type"":""k3prefab.createComponent"",""params"":{""prefabPath"":""Assets/ResourcesAB/UIPrefabs/DialogMain.prefab"",""parentPath"":""DialogMain/Panel_Content"",""componentType"":""K3Label"",""initialProperties"":{""text"":""确认"",""fontSize"":22}}}]}'
```

**创建带初始属性的标签**:

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_create_label"",""timeout"":30000,""commands"":[{""id"":""cmd_create_label"",""type"":""k3prefab.createComponent"",""params"":{""prefabPath"":""Assets/ResourcesAB/UIPrefabs/DialogMain.prefab"",""parentPath"":""DialogMain/Panel_Content"",""componentType"":""K3Label"",""initialProperties"":{""text"":""Hello World"",""fontSize"":22,""color"":""NormalBai""}}}]}'
```

---

## 命令 4: prefab.deleteGameObject (删除GameObject)

删除预制体中指定的 GameObject（级联删除所有子物体），支持通过 objectPath 和 siblingIndex 精确定位。

**单命令示例** - 删除一个按钮:

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_delete_001"",""timeout"":30000,""commands"":[{""id"":""cmd_delete"",""type"":""prefab.deleteGameObject"",""params"":{""prefabPath"":""Assets/ResourcesAB/UIPrefabs/DialogMain.prefab"",""objectPath"":""DialogMain/Panel_Content/K3Button_Confirm"",""siblingIndex"":0}}]}'
```

**参数说明**:

- `prefabPath` 必填，预制体绝对路径(必须以 ""Assets/"" 开头)
- `objectPath` 必填，GameObject 层级路径
- `siblingIndex` 可选，同名对象索引，默认为 0

**返回结果示例**:

```json
{
  ""batchId"": ""batch_delete_001"",
  ""status"": ""completed"",
  ""results"": [
    {
      ""id"": ""cmd_delete"",
      ""type"": ""prefab.deleteGameObject"",
      ""status"": ""success"",
      ""result"": {
        ""prefabPath"": ""Assets/ResourcesAB/UIPrefabs/DialogMain.prefab"",
        ""deletedObjectPath"": ""DialogMain/Panel_Content/K3Button_Confirm"",
        ""deletedInstanceID"": 345678901,
        ""deletedObjectCount"": 1,
        ""totalDeletedCount"": 5,
        ""saved"": true
      }
    }
  ]
}
```

**返回字段说明**:

- `deletedObjectPath`: 被删除对象的完整路径
- `deletedInstanceID`: 被删除对象的 Unity 实例ID
- `deletedObjectCount`: 直接删除的对象数量（始终为 1）
- `totalDeletedCount`: 删除的对象总数（包含所有子物体）
- `saved`: 预制体是否成功保存到磁盘

**批量删除示例**:

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_delete_multiple"",""timeout"":30000,""commands"":[{""id"":""cmd_del1"",""type"":""prefab.deleteGameObject"",""params"":{""prefabPath"":""Assets/ResourcesAB/UIPrefabs/DialogMain.prefab"",""objectPath"":""DialogMain/Panel_Content/K3Button_Confirm""}},{""id"":""cmd_del2"",""type"":""prefab.deleteGameObject"",""params"":{""prefabPath"":""Assets/ResourcesAB/UIPrefabs/DialogMain.prefab"",""objectPath"":""DialogMain/Panel_Content/K3Button_Cancel""}}]}'
```

**错误处理**:

- `GameObject not found at path`: GameObject 路径不存在
- `CANNOT_DELETE_ROOT`: 不能删除预制体根节点
- 删除操作使用 Unity Undo 系统，可在编辑器中按 Ctrl+Z 撤销

**注意事项**:

- 删除操作会级联删除所有子物体
- 不能删除预制体的根节点（会抛出错误）
- 批量删除时，建议按照""从叶子节点到父节点""的顺序删除
- 删除前可使用 `k3prefab.queryByK3Id` 查询确认对象信息

---

## 命令 5: prefab.moveOrCopyGameObject (移动或复制GameObject)

移动或复制 GameObject 到新的父节点，自动保持世界坐标不变。通过 `isCopy` 参数控制操作类型。

### 移动操作示例

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_move_001"",""timeout"":30000,""commands"":[{""id"":""cmd_move"",""type"":""prefab.moveOrCopyGameObject"",""params"":{""prefabPath"":""Assets/ResourcesAB/UIPrefabs/DialogMain.prefab"",""sourcePath"":""DialogMain/Panel_Content/K3Button_Confirm"",""sourceSiblingIndex"":0,""targetParentPath"":""DialogMain/Panel_Other"",""targetSiblingIndex"":0,""isCopy"":false}}]}'
```

### 复制操作示例

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_copy_001"",""timeout"":30000,""commands"":[{""id"":""cmd_copy"",""type"":""prefab.moveOrCopyGameObject"",""params"":{""prefabPath"":""Assets/ResourcesAB/UIPrefabs/DialogMain.prefab"",""sourcePath"":""DialogMain/Panel_Content/K3Button_Confirm"",""sourceSiblingIndex"":0,""targetParentPath"":""DialogMain/Panel_Other"",""targetSiblingIndex"":0,""isCopy"":true}}]}'
```

**参数说明**:

- `prefabPath` 必填，预制体绝对路径(必须以 ""Assets/"" 开头)
- `sourcePath` 必填，源 GameObject 层级路径
- `sourceSiblingIndex` 可选，源对象同名索引，默认为 0
- `targetParentPath` 必填，目标父节点路径
- `targetSiblingIndex` 可选，在目标父节点子物体列表中的位置，-1 或未指定表示移动到末尾，默认为 -1
- `isCopy` 可选，true=复制操作，false=移动操作，默认为 false

**移动操作返回结果示例**:

```json
{
  ""batchId"": ""batch_move_001"",
  ""status"": ""completed"",
  ""results"": [
    {
      ""id"": ""cmd_move"",
      ""type"": ""prefab.moveOrCopyGameObject"",
      ""status"": ""success"",
      ""result"": {
        ""prefabPath"": ""Assets/ResourcesAB/UIPrefabs/DialogMain.prefab"",
        ""sourcePath"": ""DialogMain/Panel_Content/K3Button_Confirm"",
        ""oldPath"": ""DialogMain/Panel_Content/K3Button_Confirm"",
        ""newPath"": ""DialogMain/Panel_Other/K3Button_Confirm"",
        ""oldSiblingIndex"": 2,
        ""newSiblingIndex"": 0,
        ""operationType"": ""move"",
        ""worldPositionPreserved"": true,
        ""operatedInstanceID"": 345678901,
        ""saved"": true
      }
    }
  ]
}
```

**复制操作返回结果示例**:

```json
{
  ""batchId"": ""batch_copy_001"",
  ""status"": ""completed"",
  ""results"": [
    {
      ""id"": ""cmd_copy"",
      ""type"": ""prefab.moveOrCopyGameObject"",
      ""status"": ""success"",
      ""result"": {
        ""prefabPath"": ""Assets/ResourcesAB/UIPrefabs/DialogMain.prefab"",
        ""sourcePath"": ""DialogMain/Panel_Content/K3Button_Confirm"",
        ""originalPath"": ""DialogMain/Panel_Content/K3Button_Confirm"",
        ""copiedPath"": ""DialogMain/Panel_Other/K3Button_Confirm"",
        ""sourceSiblingIndex"": 2,
        ""targetSiblingIndex"": 0,
        ""operationType"": ""copy"",
        ""worldPositionPreserved"": true,
        ""originalInstanceID"": 345678901,
        ""copiedInstanceID"": 345678902,
        ""saved"": true
      }
    }
  ]
}
```

**返回字段说明**:

移动操作:
- `oldPath`: 移动前的完整路径
- `newPath`: 移动后的完整路径
- `oldSiblingIndex`: 移动前的子物体索引
- `newSiblingIndex`: 移动后的子物体索引
- `operationType`: 操作类型，固定为 ""move""
- `operatedInstanceID`: 被移动对象的 Unity 实例ID

复制操作:
- `originalPath`: 原对象的完整路径
- `copiedPath`: 复制后新对象的完整路径
- `sourceSiblingIndex`: 原对象的子物体索引
- `targetSiblingIndex`: 新对象的子物体索引
- `operationType`: 操作类型，固定为 ""copy""
- `originalInstanceID`: 原对象的 Unity 实例ID
- `copiedInstanceID`: 新对象的 Unity 实例ID

**批量移动示例**:

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_move_multiple"",""timeout"":30000,""commands"":[{""id"":""cmd_move1"",""type"":""prefab.moveOrCopyGameObject"",""params"":{""prefabPath"":""Assets/ResourcesAB/UIPrefabs/DialogMain.prefab"",""sourcePath"":""DialogMain/Panel_Content/K3Button_Confirm"",""targetParentPath"":""DialogMain/Panel_Other"",""targetSiblingIndex"":0,""isCopy"":false}},{""id"":""cmd_move2"",""type"":""prefab.moveOrCopyGameObject"",""params"":{""prefabPath"":""Assets/ResourcesAB/UIPrefabs/DialogMain.prefab"",""sourcePath"":""DialogMain/Panel_Content/K3Button_Cancel"",""targetParentPath"":""DialogMain/Panel_Other"",""targetSiblingIndex"":1,""isCopy"":false}}]}'
```

**错误处理**:

- `GameObject not found at path`: GameObject 路径不存在
- `CANNOT_MOVE_TO_SELF_OR_CHILD`: 不能将物体移动到其自身或其子节点下
- `CANNOT_COPY_TO_SAME_PARENT`: 不能将 GameObject 复制到其原父节点下
- 移动/复制操作使用 Unity Undo 系统，可在编辑器中按 Ctrl+Z 撤销

**注意事项**:

- 移动和复制操作都会自动保持世界坐标不变（位置、旋转、缩放）
- 当 `targetSiblingIndex` 超出范围时，对象会被放置在子物体列表末尾
- 移动操作会改变原对象的层级结构，复制操作会创建新对象
- 不能将对象移动到其自身或其子节点下（会抛出错误）
- 不能将对象复制到其原父节点下（会抛出错误）
- 批量操作时，删除和移动混合时，删除优先执行
- 操作前可使用 `k3prefab.queryByK3Id` 查询确认对象信息

---

## Python代码调用 (备选方式)

```python
from scripts.execute_unity_command import execute_command

# 查询 K3 组件
result = execute_command({
    ""batchId"": ""batch_k3_query_001"",
    ""commands"": [{
        ""type"": ""k3prefab.queryByK3Id"",
        ""params"": {
            ""prefabPath"": ""Assets/ResourcesAB/UIPrefabs/DialogMain.prefab"",
            ""k3Id"": 6
        }
    }]
})

# 修改 K3 组件属性
result = execute_command({
    ""batchId"": ""batch_k3_modify_001"",
    ""commands"": [{
        ""type"": ""k3prefab.setComponentProperties"",
        ""params"": {
            ""prefabPath"": ""Assets/ResourcesAB/UIPrefabs/DialogMain.prefab"",
            ""k3Id"": 6,
            ""modifications"": [
                {""property"": ""alpha"", ""oldValue"": 1.0, ""newValue"": 0.5}
            ]
        }
    }]
})

# 修改 GameObject 属性
result = execute_command({
    ""batchId"": ""batch_goprops_001"",
    ""commands"": [{
        ""type"": ""prefab.setGameObjectProperties"",
        ""params"": {
            ""prefabPath"": ""Assets/ResourcesAB/UIPrefabs/DialogMain.prefab"",
            ""objectPath"": ""DialogMain/Panel_Content/K3Button_Confirm"",
            ""properties"": {
                ""name"": ""K3Button_Confirm_New"",
                ""layer"": 5
            }
        }
    }]
})

# 删除 GameObject
result = execute_command({
    ""batchId"": ""batch_delete_001"",
    ""commands"": [{
        ""type"": ""prefab.deleteGameObject"",
        ""params"": {
            ""prefabPath"": ""Assets/ResourcesAB/UIPrefabs/DialogMain.prefab"",
            ""objectPath"": ""DialogMain/Panel_Content/K3Button_Confirm""
        }
    }]
})

# 移动 GameObject
result = execute_command({
    ""batchId"": ""batch_move_001"",
    ""commands"": [{
        ""type"": ""prefab.moveOrCopyGameObject"",
        ""params"": {
            ""prefabPath"": ""Assets/ResourcesAB/UIPrefabs/DialogMain.prefab"",
            ""sourcePath"": ""DialogMain/Panel_Content/K3Button_Confirm"",
            ""targetParentPath"": ""DialogMain/Panel_Other"",
            ""targetSiblingIndex"": 0,
            ""isCopy"": False
        }
    }]
})

# 复制 GameObject
result = execute_command({
    ""batchId"": ""batch_copy_001"",
    ""commands"": [{
        ""type"": ""prefab.moveOrCopyGameObject"",
        ""params"": {
            ""prefabPath"": ""Assets/ResourcesAB/UIPrefabs/DialogMain.prefab"",
            ""sourcePath"": ""DialogMain/Panel_Content/K3Button_Confirm"",
            ""targetParentPath"": ""DialogMain/Panel_Other"",
            ""targetSiblingIndex"": 0,
            ""isCopy"": True
        }
    }]
})
```

---

## Notes

### K3 框架核心概念

- **K3ID 唯一性范围**: K3ID 在 Dialog/Panel 级别唯一，不同容器中可以有相同的 K3ID
- **容器类型**: K3Dialog 和 K3PanelEx 是容器，维护 childrenDict 字典 (ID 到组件的映射)
- **组件类型**: K3Button、K3Label、K3Image、K3Edit、K3CheckBox、K3Slider 等

### 命令选择指南

- **k3prefab.queryByK3Id**: 当你知道 K3ID 时使用，返回该 K3ID 对应的所有组件
- **k3prefab.setComponentProperties**: 修改 K3 组件的特殊属性 (如 alpha、interactable、text 等)
- **k3prefab.createComponent**: 在预制体中创建新的 K3UI 组件，自动分配 K3ID
- **prefab.setGameObjectProperties**: 修改 GameObject 的通用属性 (如 name、tag、layer、isActive 等)
- **prefab.deleteGameObject**: 删除 GameObject 及其所有子物体
- **prefab.moveOrCopyGameObject**: 移动或复制 GameObject 到新的父节点

### 工作流建议

1. 先用 `k3prefab.queryByK3Id` 查询 K3ID，获取组件的完整信息
2. 从查询结果中获取当前属性值作为 `oldValue`，以及 `gameObjectPath`
3. 使用 `k3prefab.setComponentProperties` 修改 K3 组件属性
4. 使用 `prefab.setGameObjectProperties` 修改 GameObject 属性
5. 使用 `prefab.deleteGameObject` 删除不需要的 GameObject（会级联删除子物体）
6. 使用 `prefab.moveOrCopyGameObject` 移动或复制 GameObject 到新的父节点
7. 如需验证，再次调用 `k3prefab.queryByK3Id` 确认修改结果

### 错误处理

- **K3ID_NOT_FOUND**: 未找到指定 K3ID 的组件
- **INDEX_OUT_OF_RANGE**: 索引超出范围 (K3ID 匹配数量少于请求的索引)
- **旧值不匹配**: 当 oldValue 与实际值不符时，该属性会被跳过 (status=skipped)
- **部分成功模式**: 批量命令中单个命令失败不影响后续执行

### 乐观锁机制

`k3prefab.setComponentProperties` 使用乐观锁:
- 每个属性独立验证 oldValue
- 匹配则修改 (status=success)
- 不匹配则跳过 (status=skipped)
- 避免误修改，适合协作环境

### 路径适配

- 路径分隔符自动适配 Windows (\\) 和 macOS/Linux (/)
- prefabPath 必须以 ""Assets/"" 开头
- objectPath 从查询结果中获取，确保准确

### 状态说明

- `status` 可能的值: `processing` (处理中) / `completed` (已完成) / `error` (错误)
- 每个命令有独立的 `status`，批量命令支持部分成功
- `summary` 字段统计修改结果 (total/success/skipped/failed)
";
    }
}
