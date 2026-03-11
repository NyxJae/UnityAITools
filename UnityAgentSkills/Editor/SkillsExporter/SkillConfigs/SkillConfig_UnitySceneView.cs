namespace UnityAgentSkills.SkillsExporter
{
    /// <summary>
    /// unity-scene-view 技能配置.
    /// </summary>
    public static class SkillConfig_UnitySceneView
    {
        /// <summary>
        /// 技能名称.
        /// </summary>
        public const string SkillName = "unity-scene-view";

        /// <summary>
        /// 技能描述.
        /// </summary>
        public const string SkillDescription = "查看 Unity 场景信息. 触发关键词:Unity:场景,Unity scene,查看场景";

        /// <summary>
        /// SKILL.md 的完整内容.
        /// </summary>
        public const string SkillMarkdown = @"---
name: unity-scene-view
description: 查看 Unity 场景信息. 触发关键词:Unity:场景,Unity scene,查看场景
---

# Unity Scene View

## Instructions

### Context

本技能用于打开 Unity 场景和查看场景内对象信息,包括层级结构,搜索定位结果和组件属性.

推荐把 `scene.queryHierarchy` 同时当作“查看层级”和“搜索定位”入口使用:
- 默认模式返回完整 `hierarchy`,适合整体浏览.
- 当你想快速找对象时,优先在现有 `queryHierarchy` 中使用搜索参数,而不是先导出整棵树再手工筛选.
- `nameContains` 使用 `string[]`,支持单元素数组和多关键词 OR 搜索.
- 搜索结果优先服务后续 `scene.queryComponents` 和编辑定位,因此会直接回显 `path`,`siblingIndex`,`sceneName`,`scenePath`.

### Steps

**工具脚本**: `<Scripts Directory>/execute_unity_command.py`

**最简单的调用方式** - 直接命令行传参(推荐):

> 💡 使用 `uv run` 执行.注意,以防命令行对多行字符串处理异常,请将JSON参数写在一行内.
> 💡 脚本最好加引号包裹,避免路径解析问题.

**命令 1: scene.open (打开场景)**

> ⚠️ 仅在编辑模式可执行. Play 模式调用会返回错误.

单命令示例 (uv run):

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_scene_open_001"",""timeout"":30000,""commands"":[{""id"":""cmd_001"",""type"":""scene.open"",""params"":{""scenePath"":""Assets/Scenes/Main.unity""}}]}'
```

**scene.open 参数说明**:

- `scenePath` 必填,Unity 工程内 Assets 相对路径(必须以 ""Assets/"" 开头),例如 ""Assets/Scenes/Main.unity""
- `openMode` 可选,打开模式: ""Single""(默认,替换当前场景) 或 ""Additive""(叠加加载)

**scene.open 返回结果示例**:

```json
{
  ""batchId"": ""batch_scene_open_001"",
  ""status"": ""completed"",
  ""results"": [
    {
      ""id"": ""cmd_001"",
      ""type"": ""scene.open"",
      ""status"": ""success"",
      ""result"": {
        ""scenePath"": ""Assets/Scenes/Main.unity"",
        ""sceneName"": ""Main""
      }
    }
  ]
}
```

**命令 2: scene.queryHierarchy (查询场景层级结构)**

单命令示例 (uv run):

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_scene_hierarchy_001"",""timeout"":30000,""commands"":[{""id"":""cmd_001"",""type"":""scene.queryHierarchy"",""params"":{""includeInactive"":true,""maxDepth"":-1}}]}'
```

**scene.queryHierarchy 参数说明**:

- `includeInactive` 可选,是否包含禁用的 GameObject,默认 true
- `maxDepth` 可选,最大遍历深度,-1 表示无限,默认 -1
- `nameContains` 可选,类型为 `string[]`.每个词项都会做 Trim,按 contains + IgnoreCase 匹配,多项 OR.当传入有效搜索条件时,将不返回 `hierarchy`,改为返回扁平列表 `matches[]`
- `maxResults` 可选,当传入 `nameContains` 等有效搜索条件时生效,最多返回的 `matches` 条数,默认 50.超过时将被截断,并通过 `totalMatches`,`matchedCount`,`truncated` 体现

**搜索体验说明**:

- `nameContains` 支持单元素数组和多关键词 OR.
- 空数组或全空词项视为未传.

**scene.queryHierarchy 返回结果示例(默认模式)**:

```json
{
  ""batchId"": ""batch_scene_hierarchy_001"",
  ""status"": ""completed"",
  ""results"": [
    {
      ""id"": ""cmd_001"",
      ""type"": ""scene.queryHierarchy"",
      ""status"": ""success"",
      ""result"": {
        ""loadedSceneCount"": 1,
        ""scenes"": [
          {
            ""sceneName"": ""Main"",
            ""scenePath"": ""Assets/Scenes/Main.unity"",
            ""totalGameObjects"": 15,
            ""hierarchy"": [
              {
                ""name"": ""MainCamera"",
                ""instanceID"": 123456789,
                ""path"": ""MainCamera"",
                ""siblingIndex"": 0,
                ""depth"": 0,
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

**scene.queryHierarchy 搜索模式示例**:

当 `params` 中传入有效的 `nameContains` 搜索条件时,将返回 `matches[]`(扁平列表),不再返回 `hierarchy`.结果按场景分组.

```json
{
  ""batchId"": ""batch_scene_search_001"",
  ""status"": ""completed"",
  ""results"": [
    {
      ""id"": ""cmd_001"",
      ""type"": ""scene.queryHierarchy"",
      ""status"": ""success"",
      ""result"": {
        ""loadedSceneCount"": 1,
        ""scenes"": [
          {
            ""sceneName"": ""Main"",
            ""scenePath"": ""Assets/Scenes/Main.unity"",
            ""totalMatches"": 2,
            ""matchedCount"": 2,
            ""truncated"": false,
            ""matches"": [
              {
                ""name"": ""UIButton_Confirm"",
                ""instanceID"": 345678901,
                ""path"": ""Main/Panel_Content/UIButton_Confirm"",
                ""siblingIndex"": 0,
                ""depth"": 2,
                ""isActive"": true,
                ""sceneName"": ""Main"",
                ""scenePath"": ""Assets/Scenes/Main.unity""
              }
            ]
          }
        ]
      }
    }
  ]
}
```

**多关键词 OR 示例**:

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_scene_search_keywords_001"",""timeout"":30000,""commands"":[{""id"":""cmd_search"",""type"":""scene.queryHierarchy"",""params"":{""nameContains"":[""dropdown"",""language""],""maxResults"":10}}]}'
```

这个结果可直接拿 `sceneName + path + siblingIndex` 去调用 `scene.queryComponents`,也可继续用于后续编辑定位.

**命令 3: scene.queryComponents (查询组件信息)**

单命令示例 (uv run):

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_scene_components_001"",""timeout"":30000,""commands"":[{""id"":""cmd_001"",""type"":""scene.queryComponents"",""params"":{""sceneName"":""Main"",""objectPath"":""Main/Panel_Content/UIButton_Confirm"",""componentFilter"":[""transform"",""ui""],""propertyFilter"":[""color"",""transition""
]}}]}'
```

**scene.queryComponents 参数说明**:

- `sceneName` 必填,目标场景名称(大小写敏感,与 Unity Scene.name 对应)
- `objectPath` 必填,GameObject 层级路径(从 scene.queryHierarchy 返回的 path 字段获取)
- `siblingIndex` 可选,同名对象序号(从 0 开始),用于定位同路径下的同名对象,默认 0
- `componentFilter` 可选,组件名称模糊过滤(contains + IgnoreCase + OR),`null`/空数组/空字符串表示不过滤,返回所有组件(含内置组件)
- `propertyFilter` 可选,字段名称模糊过滤(contains + IgnoreCase + OR),`null`/空数组/空字符串表示不过滤,返回该组件全部字段

**scene.queryComponents 返回结果示例**:

```json
{
  ""batchId"": ""batch_scene_components_001"",
  ""status"": ""completed"",
  ""results"": [
    {
      ""id"": ""cmd_001"",
      ""type"": ""scene.queryComponents"",
      ""status"": ""success"",
      ""result"": {
        ""sceneName"": ""Main"",
        ""objectPath"": ""Main/Panel_Content/UIButton_Confirm"",
        ""instanceID"": 345678901,
        ""totalComponents"": 1,
        ""components"": [
          {
            ""type"": ""UIButton"",
            ""instanceID"": 789012345,
            ""scriptPath"": ""Assets/Scripts/HotUpdate/UIEngine/Component/UIButton.cs"",
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

**批量命令示例** (先打开场景,再查层级):

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_scene_full_001"",""timeout"":30000,""commands"":[{""id"":""cmd_open"",""type"":""scene.open"",""params"":{""scenePath"":""Assets/Scenes/Main.unity""}},{""id"":""cmd_hierarchy"",""type"":""scene.queryHierarchy"",""params"":{""maxDepth"":2}}]}'
```

### Notes

- scene.open 仅在编辑模式可用,Play 模式下调用会返回错误 ONLY_ALLOWED_IN_EDIT_MODE
- scene.queryHierarchy 和 scene.queryComponents 在 Play 模式和编辑模式都可执行
- 查询范围为当前已加载的所有场景,结果按场景分组
- objectPath 必须从 scene.queryHierarchy 的结果中获取,确保路径正确
- `siblingIndex` 用于定位同一路径下的同名 GameObject,从 scene.queryHierarchy 返回的 siblingIndex 字段获取
- scene.queryComponents 中 properties 支持嵌套结构(数组、对象等)
- 批量命令采用串行执行,严格按输入顺序
- 批量命令支持部分成功模式,单个命令失败不影响后续执行

### 流程

1. 先用 scene.open 打开目标场景(如已打开可跳过此步)
2. 用 scene.queryHierarchy 获取场景的完整层级结构
3. 根据层级结构找到感兴趣的 GameObject 及其 path,siblingIndex 和所属 sceneName
4. 用 scene.queryComponents 查询该 GameObject 的组件信息(需传入 sceneName)
5. 如需查看多个 GameObject,可在批量命令中组合使用多个命令

**处理同名对象示例**:

如果场景中存在多个同名 GameObject (如两个 UIButton_Confirm):
1. 从 scene.queryHierarchy 返回结果中获取目标对象的 siblingIndex
2. 调用 scene.queryComponents 时传入 siblingIndex 参数精确定位

```bash
# 查询第二个 UIButton_Confirm (siblingIndex=1)
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""x"",""commands"":[{""type"":""scene.queryComponents"",""params"":{""sceneName"":""Main"",""objectPath"":""Main/Panel_Content/UIButton_Confirm"",""siblingIndex"":1}}]}'
```
";
    }
}
