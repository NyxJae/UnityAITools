namespace UnityAgentSkills.SkillsExporter
{
    /// <summary>
    /// unity-k3-prefab技能配置.
    /// </summary>
    public static class SkillConfig_UnityK3Prefab
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
description: K3框架预制体查询与编辑工具. 触发关键词:Unity,K3预制体,K3 prefab,K3UI,UI
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

- `prefabPath` 必填，Unity 工程内 Assets 相对路径(必须以 ""Assets/"" 开头)
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

- `prefabPath` 必填，Unity 工程内 Assets 相对路径(必须以 ""Assets/"" 开头)
- `k3Id` 必填，K3 组件的 ID
- `index` 可选，同 K3ID 中的索引（用于精确定位），默认为 0
- `modifications` 必填，修改请求数组，每个元素包含:
  - `property` 属性名称 (如 ""alpha""、""interactable""、""text"" 等)
  - `oldValue` 期望的旧值（用于验证）
  - `newValue` 要修改的新值

**关键返回字段说明**:

- `modifications[]`: 每个属性的修改结果,包含 `property`,`oldValue`,`currentValue`,`newValue`,`status`(success/skipped/failed),`message`
- `currentProperties`: 修改后的当前属性快照
- `saved`: 预制体是否成功保存
- `summary`: 修改统计(`total`,`success`,`skipped`,`failed`)
- 乐观锁: 每个属性独立验证 oldValue,不匹配则 status=skipped

---

## 命令 3: prefab.setGameObjectProperties (修改GameObject属性)

修改预制体中指定 GameObject 的属性 (name, tag, layer, isActive 等)。

**单命令示例**:

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""_batch_goprops_001"",""timeout"":30000,""commands"":[{""id"":""cmd_001"",""type"":""prefab.setGameObjectProperties"",""params"":{""prefabPath"":""Assets/ResourcesAB/UIPrefabs/DialogMain.prefab"",""objectPath"":""DialogMain/Panel_Content/K3Button_Confirm"",""properties"":{""name"":""K3Button_Confirm_New"",""layer"":5}}]}'
```

**参数说明**:

- `prefabPath` 必填，Unity 工程内 Assets 相对路径(必须以 ""Assets/"" 开头)
- `objectPath` 必填，GameObject 层级路径 (从 k3prefab.queryByK3Id 返回的 gameObjectPath 获取)
- `siblingIndex` 可选，同名对象序号(从 0 开始)，默认为 0
- `properties` 必填，要修改的属性对象，支持的字段:
  - `name` (string) 对象名称
  - `tag` (string) 标签
  - `layer` (int) 层级 (0-31)
  - `isActive` (bool) 激活状态

  - `hideFlags` (int) 隐藏标志

**关键返回字段说明**:

- `modifiedProperties[]`: 每个属性的修改详情,包含 `name`,`oldValue`,`newValue`
- `currentProperties`: 修改后的 GameObject 属性快照
- `saved`: 预制体是否成功保存

---

## 命令 4: k3prefab.createComponent (创建K3UI组件)

在预制体中创建新的 K3UI 组件（K3Button、K3Label、K3Image 等），自动分配 K3ID 并设置初始属性。

**单命令示例** - 创建一个按钮:

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_create_001"",""timeout"":30000,""commands"":[{""id"":""cmd_create_button"",""type"":""k3prefab.createComponent"",""params"":{""prefabPath"":""Assets/ResourcesAB/UIPrefabs/DialogMain.prefab"",""parentPath"":""DialogMain/Panel_Content"",""componentType"":""K3Button"",""initialProperties"":{""interactable"":true,""alpha"":1.0}}}]}'
```

**参数说明**:

- `prefabPath` 必填，Unity 工程内 Assets 相对路径(必须以 ""Assets/"" 开头)
- `parentPath` 必填，父节点路径(必须是实现 IK3Container 的节点，如 K3Dialog、K3Panel)
- `componentType` 必填，K3UI 组件类型名(支持: K3Button, K3Label, K3Image, K3Edit, K3CheckBox, K3Panel, K3ListView 等 20+ 种类型)
- `initialProperties` 可选，组件初始属性对象，不同组件类型支持不同的属性

**支持的组件类型**:

K3Button, K3Label, K3Image, K3Edit, K3CheckBox, K3LinkLabel, K3Panel, K3ListView, K3Dialog, K3Dialog2, K3TabButton, K3Tab, K3ProgressBar, K3RadarChart, K3HeadIcon, K3Itembox, K3LabelButton, K3Animation, K3SliderBar, K3Movie, K3NumImage, K3JoyStick, K3Magicbox, K3ExpandListView, K3ExpandListPanel, K3InsightImage

**关键返回字段说明**:

- `gameObject`: 创建的 GameObject 信息(`name`,`path`,`instanceID`)
- `k3Component`: K3 组件信息(`type`,`properties` 含自动分配的 `ID`,`parentID`)
- `saved`: 预制体是否成功保存

---

## 命令 5: prefab.deleteGameObject (删除GameObject)

删除预制体中指定的 GameObject（级联删除所有子物体），支持通过 objectPath 和 siblingIndex 精确定位。

**单命令示例** - 删除一个按钮:

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_delete_001"",""timeout"":30000,""commands"":[{""id"":""cmd_delete"",""type"":""prefab.deleteGameObject"",""params"":{""prefabPath"":""Assets/ResourcesAB/UIPrefabs/DialogMain.prefab"",""objectPath"":""DialogMain/Panel_Content/K3Button_Confirm"",""siblingIndex"":0}}]}'
```

**参数说明**:

- `prefabPath` 必填，Unity 工程内 Assets 相对路径(必须以 ""Assets/"" 开头)
- `objectPath` 必填，GameObject 层级路径
- `siblingIndex` 可选，同名对象序号(从 0 开始)，默认为 0

**关键返回字段说明**:

- `deletedObjectPath`: 被删除对象的完整路径
- `deletedObjectCount`: 直接删除的对象数量(始终为 1)
- `totalDeletedCount`: 删除的对象总数(包含所有子物体)
- `saved`: 预制体是否成功保存
- 不能删除预制体根节点(会返回 `CANNOT_DELETE_ROOT` 错误)
- 批量删除建议按""从叶子节点到父节点""的顺序

---

## 命令 6: prefab.moveOrCopyGameObject (移动或复制GameObject)

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

- `prefabPath` 必填，Unity 工程内 Assets 相对路径(必须以 ""Assets/"" 开头)
- `sourcePath` 必填，源 GameObject 层级路径
- `sourceSiblingIndex` 可选，源对象同名索引，默认为 0
- `targetParentPath` 必填，目标父节点路径
- `targetSiblingIndex` 可选，在目标父节点子物体列表中的位置，-1 或未指定表示移动到末尾，默认为 -1
- `isCopy` 可选，true=复制操作，false=移动操作，默认为 false

**关键返回字段说明**:

移动操作(`isCopy`=false):
- `oldPath`/`newPath`: 移动前后的完整路径
- `operationType`: 固定为 ""move""
- `worldPositionPreserved`: 是否保持世界坐标

复制操作(`isCopy`=true):
- `originalPath`/`copiedPath`: 原对象与副本的完整路径
- `operationType`: 固定为 ""copy""
- `copiedInstanceID`: 新对象的 Unity 实例ID

通用:
- `saved`: 预制体是否成功保存
- 不能移动到自身或子节点下(`CANNOT_MOVE_TO_SELF_OR_CHILD`)
- 不能复制到原父节点下(`CANNOT_COPY_TO_SAME_PARENT`)

---

## 命令 7: prefab.queryHierarchy (查询预制体层级结构)

查询预制体的 GameObject 层级结构.支持两种返回模式: 默认返回完整层级树,传入 `nameContains` 时返回扁平匹配列表.

**单命令示例** - 查询完整层级:

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_hierarchy_001"",""timeout"":30000,""commands"":[{""id"":""cmd_001"",""type"":""prefab.queryHierarchy"",""params"":{""prefabPath"":""Assets/ResourcesAB/UIPrefabs/DialogMain.prefab"",""includeInactive"":true,""maxDepth"":-1}}]}'
```

**nameContains 模式示例** - 按名称模糊搜索:

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_search_001"",""timeout"":30000,""commands"":[{""id"":""cmd_001"",""type"":""prefab.queryHierarchy"",""params"":{""prefabPath"":""Assets/ResourcesAB/UIPrefabs/DialogMain.prefab"",""nameContains"":""Button"",""maxMatches"":50,""includeInactive"":true,""maxDepth"":-1}}]}'
```

**参数说明**:

- `prefabPath` 必填,Unity 工程内 Assets 相对路径(必须以 ""Assets/"" 开头)
- `includeInactive` 可选,是否包含禁用的 GameObject,默认 true
- `maxDepth` 可选,最大遍历深度,-1 表示无限,默认 -1
- `nameContains` 可选,名称模糊过滤(contains,IgnoreCase,对入参 Trim).传入后不返回 `hierarchy`,改为返回扁平 `matches[]`
- `maxMatches` 可选,nameContains 模式下最多返回条数,默认 50

**返回模式说明**:

两种模式均返回 `prefabPath` 和 `rootName` 字段.

默认模式(不传 nameContains):
- `prefabPath`,`rootName`: 预制体路径与根节点名称
- `totalGameObjects`: 总节点数
- `hierarchy[]`: 嵌套树结构,每个节点含 `name`,`instanceID`,`path`,`siblingIndex`,`depth`,`isActive`,`children[]`

nameContains 模式(传入 nameContains):
- `prefabPath`,`rootName`: 预制体路径与根节点名称
- `totalMatches`: 实际命中总数(未截断)
- `matchedCount`: 本次返回条数(受 maxMatches 限制)
- `truncated`: 是否被截断(totalMatches > matchedCount)
- `matches[]`: 扁平列表,每个元素含 `name`,`instanceID`,`path`,`siblingIndex`,`depth`,`isActive`(不含 children)

---

## 命令 8: prefab.queryComponents (查询组件信息)

查询预制体中指定 GameObject 的组件详情,支持类型过滤.

**单命令示例**:

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_components_001"",""timeout"":30000,""commands"":[{""id"":""cmd_001"",""type"":""prefab.queryComponents"",""params"":{""prefabPath"":""Assets/ResourcesAB/UIPrefabs/DialogMain.prefab"",""objectPath"":""DialogMain/Panel_Content/K3Button_Confirm"",""componentFilter"":[""transform"",""k3""],""includePrivateFields"":false}}]}'
```

**参数说明**:

- `prefabPath` 必填,Unity 工程内 Assets 相对路径(必须以 ""Assets/"" 开头)
- `objectPath` 必填,GameObject 层级路径(从 prefab.queryHierarchy 返回的 path 字段获取)
- `siblingIndex` 可选,同名对象序号(从 0 开始),用于定位同路径下的同名对象,默认 0
- `componentFilter` 可选,组件名称模糊过滤(contains + IgnoreCase + OR),`null`/空数组/空字符串表示不过滤,返回所有组件(含内置组件)
- `includePrivateFields` 可选,是否包含私有字段(带 SerializeField 标记的),默认 false

**关键返回字段说明**:

- `objectPath`: 查询的 GameObject 路径
- `totalComponents`: 返回的组件数量
- `components[]`: 组件数组,每个元素含 `type`,`instanceID`,`scriptPath`,`properties`(属性键值对)

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

# 其他命令省略
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
- **prefab.queryHierarchy**: 查询预制体层级结构(默认返回树,传 nameContains 返回扁平匹配列表)
- **prefab.queryComponents**: 查询指定 GameObject 的组件详情
- **prefab.setGameObjectProperties**: 修改 GameObject 的通用属性 (如 name、tag、layer、isActive 等)
- **prefab.deleteGameObject**: 删除 GameObject 及其所有子物体
- **prefab.moveOrCopyGameObject**: 移动或复制 GameObject 到新的父节点

### 错误处理

- **INVALID_FIELDS**: 参数非法,例如 prefabPath 不是 Assets/.../*.prefab
- **PREFAB_NOT_FOUND**: 预制体文件不存在
- **EMPTY_MODIFICATIONS**: k3prefab.setComponentProperties 的 modifications 缺失或为空
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
