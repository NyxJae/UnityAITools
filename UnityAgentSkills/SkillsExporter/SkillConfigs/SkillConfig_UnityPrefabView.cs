namespace UnityAgentSkills.SkillsExporter
{
    /// <summary>
    /// unity-prefab-view技能配置.
    /// </summary>
    public static class SkillConfig_UnityPrefabView
    {
        /// <summary>
        /// 技能名称.
        /// </summary>
        public const string SkillName = "unity-prefab-view";

        /// <summary>
        /// 技能描述.
        /// </summary>
        public const string SkillDescription = "查看 Unity 预制体信息. 触发关键词:Unity:预制体,Unity prefab,查看预制体";

        /// <summary>
        /// SKILL.md的完整内容.
        /// </summary>
        public const string SkillMarkdown = @"---
name: unity-prefab-view
description: 查看 Unity 预制体信息. 触发关键词:Unity:预制体,Unity prefab,查看预制体
---

# Unity Prefab View

## Instructions

### Context

本技能用于查看 Unity 预制体信息,包括层级结构和组件属性

### Steps

**工具脚本**: `<Scripts Directory>/execute_unity_command.py`

**最简单的调用方式** - 直接命令行传参(推荐):

> 💡 可使用 `python` 或 `uv run` 执行,推荐 `uv run`.注意,以防命令行对多行字符串处理异常,请将JSON参数写在一行内.
> 💡 脚本最好加引号包裹,避免路径解析问题.

**命令 1: prefab.queryHierarchy (查询层级结构)**

单命令示例 (uv run):

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_prefab_hierarchy_001"",""timeout"":30000,""commands"":[{""id"":""cmd_001"",""type"":""prefab.queryHierarchy"",""params"":{""prefabPath"":""Assets/Resources/Prefabs/DialogMain.prefab"",""includeInactive"":true,""maxDepth"":-1}}]}'
```

**命令参数说明**:

- `batchId` 必填,批次唯一标识(建议 16-32 字符,仅 `[a-zA-Z0-9_-]`)
- `timeout` 可选,超时时间(毫秒),默认 30000
- `commands` 必填,命令数组,每个元素包含:
  - `id` 必填,命令唯一标识
  - `type` 必填,命令类型: `""prefab.queryHierarchy""` 或 `""prefab.queryComponents""`
  - `params` 必填,命令参数

**prefab.queryHierarchy 参数说明**:

- `prefabPath` 必填,Unity 工程内 Assets 相对路径(必须以 ""Assets/"" 开头),例如 ""Assets/Prefabs/DialogMain.prefab""
- 路径分隔符自动适配,支持 Windows 反斜杠(\\) 和 macOS/Linux 正斜杠(/)
- `includeInactive` 可选,是否包含禁用的 GameObject,默认 true
- `maxDepth` 可选,最大遍历深度,-1 表示无限,默认 -1
- `nameContains` 可选,名称模糊过滤(contains).当传入时,将不返回 `hierarchy`,改为返回扁平列表 `matches[]`(IgnoreCase,对入参 Trim)
- `maxMatches` 可选,当传入 nameContains 时生效,最多返回的 matches 条数,默认 50.超过时将被截断,并通过 `totalMatches`,`matchedCount`,`truncated` 体现

**prefab.queryHierarchy 返回结果示例**:

```json
{
  ""batchId"": ""batch_prefab_hierarchy_001"",
  ""status"": ""completed"",
  ""results"": [
    {
      ""id"": ""cmd_001"",
      ""type"": ""prefab.queryHierarchy"",
      ""status"": ""success"",
      ""result"": {
        ""prefabPath"": ""Assets/Resources/Prefabs/DialogMain.prefab"",
        ""rootName"": ""DialogMain"",
        ""totalGameObjects"": 15,
        ""hierarchy"": [
          {
            ""name"": ""DialogMain"",
            ""instanceID"": 123456789,
            ""path"": ""DialogMain"",
            ""siblingIndex"": 0,
            ""depth"": 0,
            ""isActive"": true,
            ""children"": [
              {
                ""name"": ""Panel_Content"",
                ""instanceID"": 234567890,
                ""path"": ""DialogMain/Panel_Content"",
                ""siblingIndex"": 0,
                ""depth"": 1,
                ""isActive"": true,
                ""children"": []
              }
            ]
          }
        ]
      }
    }
  ]
}
```

**prefab.queryHierarchy nameContains 模式示例**:

当 `params` 中传入 `nameContains` 时,将返回 `matches[]`(扁平列表),不再返回 `hierarchy`.

```json
{
  ""batchId"": ""batch_prefab_hierarchy_search_001"",
  ""status"": ""completed"",
  ""results"": [
    {
      ""id"": ""cmd_001"",
      ""type"": ""prefab.queryHierarchy"",
      ""status"": ""success"",
      ""result"": {
        ""prefabPath"": ""Assets/Resources/Prefabs/DialogMain.prefab"",
        ""rootName"": ""DialogMain"",
        ""totalMatches"": 2,
        ""matchedCount"": 2,
        ""truncated"": false,
        ""matches"": [
          {
            ""name"": ""K3Button_Confirm"",
            ""instanceID"": 345678901,
            ""path"": ""DialogMain/Panel_Content/K3Button_Confirm"",
            ""depth"": 2,
            ""siblingIndex"": 0,
            ""isActive"": true
          },
          {
            ""name"": ""Button_Close"",
            ""instanceID"": 987654321,
            ""path"": ""DialogMain/Panel_Content/Button_Close"",
            ""depth"": 2,
            ""siblingIndex"": 1,
            ""isActive"": true
          }
        ]
      }
    }
  ]
}
```

**命令 2: prefab.queryComponents (查询组件信息)**

单命令示例 (uv run):

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_prefab_components_001"",""timeout"":30000,""commands"":[{""id"":""cmd_001"",""type"":""prefab.queryComponents"",""params"":{""prefabPath"":""Assets/Resources/Prefabs/DialogMain.prefab"",""objectPath"":""DialogMain/Panel_Content/K3Button_Confirm"",""componentFilter"":[""transform"",""k3""],""includePrivateFields"":false}}]}'
```

**prefab.queryComponents 参数说明**:

- `prefabPath` 必填,Unity 工程内 Assets 相对路径(必须以 ""Assets/"" 开头),例如 ""Assets/Prefabs/DialogMain.prefab""
- 路径分隔符自动适配,支持 Windows 反斜杠(\) 和 macOS/Linux 正斜杠(/)
- `objectPath` 必填,GameObject 层级路径(从 prefab.queryHierarchy 返回的 path 字段获取)
- `siblingIndex` 可选,同名对象序号(从 0 开始),用于定位同路径下的同名对象,默认 0
- `componentFilter` 可选,组件名称模糊过滤(contains + IgnoreCase + OR),`null`/空数组/空字符串表示不过滤,返回所有组件(含内置组件)
- `includePrivateFields` 可选,是否包含私有字段(带 SerializeField 标记的),默认 false

**prefab.queryComponents 返回结果示例**:

```json
{
  ""batchId"": ""batch_prefab_components_001"",
  ""status"": ""completed"",
  ""results"": [
    {
      ""id"": ""cmd_001"",
      ""type"": ""prefab.queryComponents"",
      ""status"": ""success"",
      ""result"": {
        ""objectPath"": ""DialogMain/Panel_Content/K3Button_Confirm"",
        ""instanceID"": 345678901,
        ""totalComponents"": 1,
        ""components"": [
          {
            ""type"": ""K3Button"",
            ""instanceID"": 789012345,
            ""scriptPath"": ""Assets/Scripts/HotUpdate/K3Engine/Component/K3Button.cs"",
            ""properties"": {
              ""interactable"": true,
              ""transitionType"": 0
            }
          }
        ]
      }
    }
  ]
}
```

**命令 3: prefab.createGameObject (创建子物体)**

关键参数:
- `prefabPath`,`name` 必填.
- `parentPath` 可选,不传时默认 prefab 根节点.
- `parentSiblingIndex` 默认 0,用于同名父节点定位.
- `insertSiblingIndex` 默认 -1,-1 表示插入到末尾.
- `initialProperties` 可选,支持 `name`,`tag`,`layer`,`isActive`,`isStatic`,`hideFlags`.

关键返回:
- `createdObjectPath`,`createdSiblingIndex`,`insertSiblingIndexApplied`,`instanceID`,`saved`.

**命令 4: prefab.addComponent (添加组件)**

关键参数:
- `prefabPath`,`objectPath`,`componentType` 必填.
- `siblingIndex` 默认 0.
- `initialProperties` 可选,用于组件初始属性写入.

关键返回:
- `componentType`,`componentIndex`,`componentInstanceID`,`instanceID`,`saved`.

**命令 5: prefab.setComponentProperties (修改组件属性)**

关键参数:
- `prefabPath`,`objectPath`,`componentType`,`properties` 必填.
- `siblingIndex` 默认 0.
- `componentIndex` 默认 0,按同类型组件计数.
- `properties` 支持基础类型,数组,嵌套对象,以及 `$ref` 引用协议.

`$ref` 支持:
- `kind=prefabGameObject`
- `kind=prefabComponent`
- `kind=asset`
- 也支持 `null` 置空引用.

关键返回:
- `componentType`,`componentIndex`,`componentInstanceID`,`modifiedProperties`,`saved`.

**命令 6: prefab.deleteComponent (删除组件)**

关键参数:
- `prefabPath`,`objectPath`,`componentType` 必填.
- `siblingIndex` 默认 0.
- `componentIndex` 默认 0.

关键返回:
- `deletedComponentType`,`deletedComponentIndex`,`deletedComponentInstanceID`,`saved`.

**新增错误码(节选)**:
- `PREFAB_NOT_FOUND`,`GAMEOBJECT_NOT_FOUND`,`COMPONENT_TYPE_NOT_FOUND`,`AMBIGUOUS_COMPONENT_TYPE`,`COMPONENT_NOT_FOUND`
- `COMPONENT_ALREADY_EXISTS`,`CANNOT_DELETE_REQUIRED_COMPONENT`,`PROPERTY_NOT_FOUND`,`INVALID_PROPERTY_PATH`,`TYPE_MISMATCH`
- `REFERENCE_TARGET_NOT_FOUND`,`REFERENCE_TARGET_TYPE_MISMATCH`,`ASSET_NOT_FOUND`,`ASSET_TYPE_MISMATCH`,`EMPTY_PROPERTIES`

**批量命令示例** (组合两个命令):

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_prefab_full_001"",""timeout"":30000,""commands"":[{""id"":""cmd_hierarchy"",""type"":""prefab.queryHierarchy"",""params"":{""prefabPath"":""Assets/Resources/Prefabs/DialogMain.prefab""}},{""id"":""cmd_components"",""type"":""prefab.queryComponents"",""params"":{""prefabPath"":""Assets/Resources/Prefabs/DialogMain.prefab"",""objectPath"":""DialogMain/Panel_Content/K3Button_Confirm"",""componentFilter"":[""K3Button""]}}]}'
```

**代码调用说明**:

- 如需在脚本中调用,可使用 `python` 或 `uv run` 执行 `<Scripts Directory>/execute_unity_command.py`.
- `uv run` 更现代,建议优先使用.

### Notes

- 命令行方式无需创建任何文件,直接在终端执行即可
- objectPath 必须从 prefab.queryHierarchy 的结果中获取,确保路径正确
- `siblingIndex` 用于定位同一路径下的同名 GameObject,从 prefab.queryHierarchy 返回的 siblingIndex 字段获取
- prefab.queryComponents 中 properties 支持嵌套结构(数组、对象等)
- 批量命令采用串行执行,严格按输入顺序
- 批量命令支持部分成功模式,单个命令失败不影响后续执行
- instanceID 用于会话中快速定位,跨会话请使用 path
- `status` 可能为 `processing`/`completed`/`error`

### 流程

1. 先用 prefab.queryHierarchy 获取预制体的完整层级结构
2. 根据层级结构找到感兴趣的 GameObject 及其 path 和 siblingIndex
3. 用 prefab.queryComponents 查询该 GameObject 的组件信息(如果存在同名对象,需传入 siblingIndex)
4. 如需查看多个 GameObject,可在批量命令中组合使用两个命令

**处理同名对象示例**:

如果预制体中存在多个同名 GameObject (如两个 K3Button_Confirm):
1. 从 prefab.queryHierarchy 返回结果中获取目标对象的 siblingIndex
2. 调用 prefab.queryComponents 时传入 siblingIndex 参数精确定位

```bash
# 查询第二个 K3Button_Confirm (siblingIndex=1)
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""x"",""commands"":[{""type"":""prefab.queryComponents"",""params"":{""prefabPath"":""Assets/.../DialogMain.prefab"",""objectPath"":""DialogMain/Panel_Content/K3Button_Confirm"",""siblingIndex"":1}}]}'
```
";
    }
}
