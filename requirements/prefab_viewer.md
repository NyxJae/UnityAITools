# 需求文档

## 1. 项目现状与核心目标

本项目需要一个 JS 脚本,专注于读取 Unity Prefab(YAML 格式)并以 JSON 输出,方便 AI 或非专业人员快速查看结构与组件信息.脚本定位于 `Unity预制体AI友好查询/prefab_viewer.js`,命令行参数模式使用.输入为 .prefab 相对路径或绝对路径.输出只允许一种类型,stdout 仅输出 JSON,错误写 stderr 并返回非 0 退出码.

## 2. 范围与边界

**功能点简述**:

- [ ] 读取 .prefab 路径,支持相对路径与绝对路径
- [ ] 树状层级输出: --tree,从根节点开始,每个节点仅包含 name + id(fileID) + children
- [ ] 根节点元数据输出: --root-meta,包含有意义字段(例如 m_Layer,m_TagString,m_Name,m_IsActive,m_NavMeshLayer,m_StaticEditorFlags)
- [ ] 组件列表输出: --components-of <gameobjectFileID>[,<gameobjectFileID>...],返回指定 GameObject 的组件列表(组件 id + 组件类型 + MonoBehaviour 脚本名)
- [ ] 组件详情输出: --component <componentFileID>[,<componentFileID>...],返回指定组件的所有参数 key/value
- [ ] MonoBehaviour 脚本名解析: 使用 m_Script.guid 在脚本配置的 Assets 绝对路径下递归查找对应的 .cs.meta 文件
- [ ] Missing Script 处理: 若脚本缺失,标记 $status: "MissingScript"
- [ ] 数据标准化: 基于值类型判断,数字/布尔/null 直接输出,字符串/对象/引用封装
- [ ] 组件类型识别: 使用 Unity 类型 ID 映射表(如 224=RectTransform, 114=MonoBehaviour),映射不到时使用 "GameObject" 作为托底
- [ ] 工具依赖: 优先使用 rg,不可用时回退到 grep

**排除项**:

- 不支持在一次调用中混合多种输出(例如同时 --tree 与 --root-meta)
- 不输出非 JSON 文本到 stdout
- 不修改 Prefab 或项目文件(只读查看)

## 3. 输入输出规则与示例

**输入规则**:

- gameobject guid 指 Prefab YAML 内的 fileID(例如 &160547937799403005)
- 组件 id 指组件块的 fileID(例如 RectTransform 或 MonoBehaviour 的 &xxxx)
- 参数支持逗号分隔的批量输入

**数据标准化规则**:

- **判断标准**: 基于值类型

  - 数字类型: `m_Layer: 0` → `\"m_Layer\": 0`
  - 布尔类型: `m_IsActive: 1` → `\"m_IsActive\": 1`
  - null 值: `m_Material: {fileID: 0}` → `\"m_Material\": null`
  - 字符串类型: `m_Name: itembox` → `{\"type\":\"string\",\"raw\":\"itembox\"}`
  - 对象类型: `m_LocalPosition: {x: 0, y: 0, z: 0}` → `{\"type\":\"object\",\"raw\":\"{x: 0, y: 0, z: 0}\"}`
  - 引用类型: `m_Script: {fileID: 11500000, guid: 9d..., type: 3}` → `{\"type\":\"ref\",\"raw\":\"{fileID: 11500000, guid: 9d..., type: 3}\"}`
    **输出示例(仅示意,字段可省略具体值)**:

- **例 1: --tree**
  期望输出 JSON 顶层即树对象,例如:
  `{"name":"itembox","id":"160547937799403005","children":[...]}`
- **例 2: --root-meta**
  期望输出 JSON 顶层为根节点元数据对象,例如:
  `{"m_Name":"itembox","m_Layer":0,"m_TagString":"Untagged"}`
- **例 3: --components-of 160547937799403005**
  期望输出 JSON 顶层为对象,以 gameobjectId 为 key,例如:
  `{"160547937799403005":[{"id":"3467262767273149046","type":"RectTransform"},{"id":"1744541728560894454","type":"MonoBehaviour","script":"K3Panel.cs"}]}`

- **例 4: --component 1744541728560894454**
  期望输出 JSON 顶层为组件详情对象,例如:
  `{"m_Enabled":1,"m_Script":{type:"ref",raw:"{fileID: 11500000, guid: 9d..., type: 3}"}}`

## 4. 命令行参数规则

- **必须指定输出类型**: 必须提供 `--tree`, `--root-meta`, `--components-of`, 或 `--component` 中的一个,否则报错
- **参数冲突检查**: 不允许同时指定多个输出类型,否则报错
- **路径参数**: 必须提供有效的 .prefab 文件路径(相对路径或绝对路径)
- **Assets 路径配置(用户必须配置)**: 脚本顶部需要配置 Unity 项目的 Assets 绝对路径,用于查找 .meta 文件

**Assets 路径配置说明**:

用户需要在脚本顶部配置 Unity 项目的 Assets 文件夹绝对路径,例如:

```javascript
// 用户必须配置此路径
const ASSETS_PATH = "F:\\UnityProject\\RXJH\\RXJH_307_mini\\Code\\Assets";
```

脚本会在此路径下递归搜索 .cs.meta 文件,通过 guid 匹配 MonoBehaviour 的脚本名。
**错误输出格式**:

- 所有错误信息输出到 stderr
- stdout 保持为空
- 返回相应的退出码(见第 5 节)

## 5. 错误处理与退出码规范

采用标准 Unix 风格的退出码:

- **退出码 1**: 通用错误

  - Prefab 文件不存在或不可读
  - 找不到指定的 GameObject 或组件 ID
  - 脚本解析失败

- **退出码 2**: 参数错误

  - 未指定输出类型
  - 同时指定多个输出类型
  - 参数格式不正确
  - 缺少必需参数

- **退出码 3**: 系统错误

  - 文件系统错误
  - 权限不足
  - 磁盘空间不足

- **其他错误**: 统一使用退出码 1

## 6. 边缘情况与约束

- 组件含嵌套对象与引用混合: 按数据标准化规则分别封装

## 7. 例子覆盖边缘情况

- **例 5: Missing Script**
  当 m_Script.guid 在项目中找不到对应 .meta 时,在组件对象中标记:
  组件列表: `{"id":"1744541728560894454","type":"MonoBehaviour","script":{"$status":"MissingScript","guid":"9d..."}}`
  组件详情: `{"m_Enabled":1,"m_Script":{"type":"ref","raw":"{fileID: 11500000, guid: 9d..., type: 3}"}}`
- **例 6: 多 gameobject 批量查询组件列表**
  输入: `--components-of 160547937799403005,183714419140684073`
  输出顶层以 gameobjectId 为 key:
  `{"160547937799403005":[{"id":"..."}],"183714419140684073":[{"id":"..."}]}`

- **例 7: Missing Script 的组件列表与详情**
  组件列表中 MonoBehaviour 脚本缺失时:
  `{"id":"1744541728560894454","type":"MonoBehaviour","script":{"$status":"MissingScript","guid":"9d..."}}`
  组件详情中 m_Script 缺失时(统一按引用格式输出):
  `{"m_Script":{"type":"ref","raw":"{fileID: 11500000, guid: 9d..., type: 3}"},"m_Enabled":1}`

- **例 8: 非法参数与错误输出**
  输入: 同时传 `--tree --root-meta`
  stderr: `Error: only one output type is allowed`
  stdout: 空
  退出码: 2(参数错误)

- **例 9: 未指定输出类型**
  输入: `node prefab_viewer.js example.prefab`
  stderr: `Error: must specify one output type (--tree, --root-meta, --components-of, or --component)`
  stdout: 空
  退出码: 2(参数错误)

- **例 10: 文件不存在**
  输入: `node prefab_viewer.js notexist.prefab --tree`
  stderr: `Error: file not found: notexist.prefab`
  stdout: 空
  退出码: 1(通用错误)

- **例 11: 组件 ID 不存在**
  输入: `node prefab_viewer.js example.prefab --component 999999999999999999`
  stderr: `Error: component not found: 999999999999999999`
  stdout: 空
  退出码: 1(通用错误)

## 8. 测试素材与验证

- 使用 `Unity预制体AI友好查询/Dev_example/example1.prefab` 作为示例输入
- 脚本 meta guid 示例: `3383921b82e57b7439e7d76d6d21d9de`
