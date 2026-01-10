# Unity Prefab AI 查看器

一个专为 AI 设计的 Unity Prefab 文件查看工具,以 JSON 格式输出 Prefab 结构与组件信息,方便 AI 或非专业人员快速理解和分析 Unity 预制体。

## 项目简介

本项目提供了一个 Node.js 脚本 `prefab_viewer.js`,用于读取 Unity Prefab(YAML 格式)文件并以标准化 JSON 格式输出。工具专注于查看功能,不修改任何文件,适用于 AI 辅助开发、代码分析、自动化测试等场景。

### 核心特性

- 📦 **纯 JSON 输出**: stdout 仅输出 JSON,便于 AI 解析
- 🔍 **多种查询模式**: 支持树状结构、元数据、组件列表、组件详情四种输出模式
- 🎯 **智能脚本识别**: 自动通过 GUID 反查 MonoBehaviour 脚本名称
- ⚠️ **缺失脚本检测**: 自动标记 Missing Script 组件
- 📊 **数据标准化**: 统一的数据格式,便于 AI 理解
- 🛠️ **工具兼容**: 优先使用 ripgrep(rg),自动回退到 grep

## 功能特性

### 1. 树状层级结构 (`--tree`)

输出从 Prefab 根节点开始的 GameObject 树,每个节点包含:

- `name`: GameObject 名称
- `id`: GameObject 的 fileID
- `children`: 子节点数组

**示例输出**:

```json
{
  "name": "itembox",
  "id": "160547937799403005",
  "children": [
    {
      "name": "Background",
      "id": "183714419140684073",
      "children": []
    }
  ]
}
```

### 2. 根节点元数据 (`--root-meta`)

输出根节点 GameObject 的有意义元数据,包括:

- `m_Layer`: 层级
- `m_TagString`: 标签
- `m_Name`: 名称
- `m_IsActive`: 是否激活
- `m_NavMeshLayer`: 导航网格层级
- `m_StaticEditorFlags`: 静态编辑器标志

**示例输出**:

```json
{
  "m_Name": { "type": "string", "raw": "itembox" },
  "m_Layer": 0,
  "m_TagString": { "type": "string", "raw": "Untagged" },
  "m_IsActive": 1
}
```

### 3. GameObject 组件列表 (`--components-of`)

输出指定 GameObject 的组件列表,每个组件包含:

- `id`: 组件的 fileID
- `type`: 组件类型(如 RectTransform, MonoBehaviour)
- `script`: MonoBehaviour 脚本名(仅 MonoBehaviour 组件)

**参数格式**: `<gameobjectFileID>[,<gameobjectFileID>...]`

**示例输出**:

```json
{
  "160547937799403005": [
    {
      "id": "3467262767273149046",
      "type": "RectTransform"
    },
    {
      "id": "1744541728560894454",
      "type": "MonoBehaviour",
      "script": "K3Panel.cs"
    }
  ]
}
```

### 4. 组件详情 (`--component`)

输出指定组件的所有参数 key/value,数据经过标准化处理。

**参数格式**: `<componentFileID>[,<componentFileID>...]`

**示例输出**:

```json
{
  "m_Enabled": 1,
  "m_Script": {
    "type": "ref",
    "raw": "{fileID: 11500000, guid: 3383921b82e57b7439e7d76d6d21d9de, type: 3}"
  }
}
```

## 安装配置

### 环境要求

- **Node.js**: 版本 12.0 或更高
- **操作系统**: Windows/Linux/macOS
- **工具依赖**: ripgrep(rg) 或 grep(至少需要其一)

### 配置 ASSETS_PATH

在使用脚本前,必须在脚本顶部配置 Unity 项目的 Assets 文件夹绝对路径:

```javascript
// 用户必须配置此路径
const ASSETS_PATH = "F:\\UnityProject\\RXJH\\RXJH_307_mini\\Code\\Assets";
```

**配置说明**:

- 路径必须指向 Unity 项目的 `Assets` 文件夹
- 用于递归搜索 .cs.meta 文件,通过 GUID 反查 MonoBehaviour 脚本名
- 路径分隔符可使用单斜杠 `/` (跨平台) 或双反斜杠 `\\` (Windows)

### 工具依赖

脚本会自动检测可用工具:

1. **优先使用**: ripgrep(rg) - 更快的搜索速度
2. **回退**: grep - 如果 rg 不可用

**安装 ripgrep**:

```bash
# Windows (使用 Scoop)
scoop install ripgrep

# Windows (使用 Chocolatey)
choco install ripgrep

# Linux
sudo apt install ripgrep  # Ubuntu/Debian
sudo yum install ripgrep  # CentOS/RHEL

# macOS
brew install ripgrep
```

## 使用方法

### 基本命令格式

```bash
node prefab_viewer.js <prefab路径> <输出类型> [参数]
```

### 参数说明

| 参数              | 说明                        | 必需               | 示例                                            |
| ----------------- | --------------------------- | ------------------ | ----------------------------------------------- |
| `<prefab路径>`    | Prefab 文件路径(相对或绝对) | 是                 | `example.prefab` 或 `D:/Project/example.prefab` |
| `--tree`          | 输出树状层级结构            | 输出类型(必选其一) | -                                               |
| `--root-meta`     | 输出根节点元数据            | 输出类型(必选其一) | -                                               |
| `--components-of` | 输出 GameObject 组件列表    | 输出类型(必选其一) | `--components-of 160547937799403005`            |
| `--component`     | 输出组件详情                | 输出类型(必选其一) | `--component 1744541728560894454`               |
| `--help` / `-h`   | 显示帮助信息                | 否                 | -                                               |

### 示例命令

#### 1. 查看树状结构

```bash
node prefab_viewer.js "Unity预制体AI友好查询/Dev_example/example1.prefab" --tree
```

#### 2. 查看根节点元数据

```bash
node prefab_viewer.js "Unity预制体AI友好查询/Dev_example/example1.prefab" --root-meta
```

#### 3. 查询单个 GameObject 的组件列表

```bash
node prefab_viewer.js "Unity预制体AI友好查询/Dev_example/example1.prefab" --components-of 160547937799403005
```

#### 4. 批量查询多个 GameObject 的组件列表

```bash
node prefab_viewer.js "Unity预制体AI友好查询/Dev_example/example1.prefab" --components-of 160547937799403005,183714419140684073
```

#### 5. 查询单个组件详情

```bash
node prefab_viewer.js "Unity预制体AI友好查询/Dev_example/example1.prefab" --component 1744541728560894454
```

#### 6. 批量查询多个组件详情

```bash
node prefab_viewer.js "Unity预制体AI友好查询/Dev_example/example1.prefab" --component 1744541728560894454,3467262767273149046
```

## 输出格式说明

### 数据标准化规则

工具会将 Unity YAML 中的值转换为标准化 JSON 格式:

| 原始类型 | YAML 示例                                            | JSON 输出                                                         |
| -------- | ---------------------------------------------------- | ----------------------------------------------------------------- |
| 数字     | `m_Layer: 0`                                         | `0`                                                               |
| 布尔     | `m_IsActive: 1`                                      | `1`                                                               |
| null     | `m_Material: {fileID: 0}`                            | `null`                                                            |
| 字符串   | `m_Name: itembox`                                    | `{"type":"string","raw":"itembox"}`                               |
| 对象     | `m_LocalPosition: {x: 0, y: 0, z: 0}`                | `{"type":"object","raw":"{x: 0, y: 0, z: 0}"}`                    |
| 引用     | `m_Script: {fileID: 11500000, guid: 9d..., type: 3}` | `{"type":"ref","raw":"{fileID: 11500000, guid: 9d..., type: 3}"}` |

### 各输出类型的格式示例

#### --tree 输出格式

```json
{
  "name": "RootObject",
  "id": "160547937799403005",
  "children": [
    {
      "name": "ChildObject",
      "id": "183714419140684073",
      "children": []
    }
  ]
}
```

#### --root-meta 输出格式

```json
{
  "m_Name": { "type": "string", "raw": "RootObject" },
  "m_Layer": 0,
  "m_TagString": { "type": "string", "raw": "Untagged" },
  "m_IsActive": 1,
  "m_NavMeshLayer": 0,
  "m_StaticEditorFlags": 0
}
```

#### --components-of 输出格式

**单个 GameObject**:

```json
{
  "160547937799403005": [
    { "id": "3467262767273149046", "type": "RectTransform" },
    {
      "id": "1744541728560894454",
      "type": "MonoBehaviour",
      "script": "K3Panel.cs"
    }
  ]
}
```

**多个 GameObject**:

```json
{
  "160547937799403005": [
    { "id": "3467262767273149046", "type": "RectTransform" }
  ],
  "183714419140684073": [
    { "id": "2222222222222222222", "type": "RectTransform" }
  ]
}
```

#### --component 输出格式

**单个组件**:

```json
{
  "m_Enabled": 1,
  "m_Script": {
    "type": "ref",
    "raw": "{fileID: 11500000, guid: 3383921b82e57b7439e7d76d6d21d9de, type: 3}"
  },
  "m_Name": { "type": "string", "raw": "K3Panel" }
}
```

**多个组件**:

```json
{
  "1744541728560894454": {
    "m_Enabled": 1,
    "m_Script": { "type": "ref", "raw": "{fileID: 11500000, guid: ...}" }
  },
  "3467262767273149046": {
    "m_AnchorMin": { "type": "object", "raw": "{x: 0, y: 0}" },
    "m_AnchorMax": { "type": "object", "raw": "{x: 1, y: 1}" }
  }
}
```

### Missing Script 处理

当 MonoBehaviour 的脚本在项目中找不到时,会标记为 MissingScript:

**组件列表中的输出**:

```json
{
  "id": "1744541728560894454",
  "type": "MonoBehaviour",
  "script": {
    "$status": "MissingScript",
    "guid": "3383921b82e57b7439e7d76d6d21d9de"
  }
}
```

**组件详情中的输出**:

```json
{
  "m_Enabled": 1,
  "m_Script": {
    "type": "ref",
    "raw": "{fileID: 11500000, guid: 3383921b82e57b7439e7d76d6d21d9de, type: 3}"
  }
}
```

## 错误处理

### 退出码说明

| 退出码 | 说明     | 常见场景                                           |
| ------ | -------- | -------------------------------------------------- |
| 0      | 成功     | 正常执行完成                                       |
| 1      | 通用错误 | 文件不存在、组件 ID 不存在、解析失败               |
| 2      | 参数错误 | 未指定输出类型、同时指定多个输出类型、参数格式错误 |
| 3      | 系统错误 | 文件系统错误、权限不足、磁盘空间不足               |

### 常见错误场景

#### 1. 未指定输出类型

```bash
node prefab_viewer.js example.prefab
```

**错误信息** (stderr):

```
Error: must specify one output type (--tree, --root-meta, --components-of, or --component)
```

**退出码**: 2

#### 2. 同时指定多个输出类型

```bash
node prefab_viewer.js example.prefab --tree --root-meta
```

**错误信息** (stderr):

```
Error: only one output type is allowed
```

**退出码**: 2

#### 3. Prefab 文件不存在

```bash
node prefab_viewer.js notexist.prefab --tree
```

**错误信息** (stderr):

```
Error: file not found: E:\Project\UnityAITools\notexist.prefab
```

**退出码**: 1

#### 4. 组件 ID 不存在

```bash
node prefab_viewer.js example.prefab --component 999999999999999999
```

**错误信息** (stderr):

```
Error: component(s) not found: 999999999999999999
```

**退出码**: 1

#### 5. 缺少必需参数

```bash
node prefab_viewer.js example.prefab --components-of
```

**错误信息** (stderr):

```
Error: --components-of or --component requires IDs
```

**退出码**: 2

#### 6. GameObject ID 不存在

```bash
node prefab_viewer.js example.prefab --components-of 999999999999999999
```

**输出**:

```json
{
  "999999999999999999": []
}
```

**说明**: GameObject ID 不存在时,返回空数组,不会报错。

## 注意事项

### ASSETS_PATH 必须配置

- **必须**在脚本顶部配置 `ASSETS_PATH` 为 Unity 项目的 Assets 文件夹绝对路径
- 未配置或配置错误会导致 MonoBehaviour 脚本名反查失败,所有脚本都会被标记为 MissingScript
- 配置示例:
  ```javascript
  const ASSETS_PATH = "F:\\UnityProject\\RXJH\\RXJH_307_mini\\Code\\Assets";
  ```

### 一次只能使用一种输出类型

- `--tree`, `--root-meta`, `--components-of`, `--component` 四种参数只能使用其中一个
- 不能在同一次调用中混合使用多种输出类型
- 如果同时指定多个,脚本会报错并返回退出码 2

### stdout 仅输出 JSON

- stdout 只会输出有效的 JSON 数据
- 所有错误信息都输出到 stderr
- 错误发生时,stdout 保持为空,stderr 包含错误信息,返回非 0 退出码

### ID 参数格式

- `--components-of` 和 `--component` 的参数支持逗号分隔的多个 ID
- ID 之间不能有空格,或使用逗号后加空格均可(会自动 trim)
- 示例: `160547937799403005,183714419140684073` 或 `160547937799403005, 183714419140684073`

### 路径格式

- 支持相对路径和绝对路径
- Windows 路径分隔符可以使用 `/` 或 `\\`
- 建议使用引号包裹路径,避免特殊字符问题:
  ```bash
  node prefab_viewer.js "Unity预制体AI友好查询/Dev_example/example1.prefab" --tree
  ```

## 已知问题

### 1. parseKeyValuePairs 的缩进判断问题

在 `parseKeyValuePairs` 函数中,YAML 数组解析使用 `>=` 判断缩进,可能在嵌套数组结构中出现层级混乱问题。

- **影响范围**: 复杂的嵌套数组结构
- **当前状态**: 简单 Prefab 可以正常工作
- **建议**: 未来版本可能重构为使用 `==` 或固定缩进增量(如 `indent === currentArrayIndent + 2`)

### 2. 空数组 [] 处理问题

Unity YAML 中的空数组格式(如 `m_Children: []`)会被解析为字符串 `"[]"` 而非空数组。

- **影响范围**: 包含空数组的字段
- **当前状态**: 当前测试的 Prefab 未受影响
- **建议**: 未来版本将添加对空数组的正确解析支持

## 示例

### 完整示例: 查看 Prefab 树状结构

**命令**:

```bash
cd "E:\Project\UnityAITools"
node "Unity预制体AI友好查询/prefab_viewer.js" "Unity预制体AI友好查询/Dev_example/example1.prefab" --tree
```

**输出**:

```json
{
  "name": "itembox",
  "id": "160547937799403005",
  "children": []
}
```

### 完整示例: 查看 GameObject 组件列表(包含 Missing Script)

**命令**:

```bash
cd "E:\Project\UnityAITools"
node "Unity预制体AI友好查询/prefab_viewer.js" "Unity预制体AI友好查询/Dev_example/example1.prefab" --components-of 160547937799403005
```

**输出**:

```json
{
  "160547937799403005": [
    {
      "id": "3467262767273149046",
      "type": "RectTransform"
    },
    {
      "id": "1744541728560894454",
      "type": "MonoBehaviour",
      "script": "K3Panel.cs"
    }
  ]
}
```

### 完整示例: 查看组件详情

**命令**:

```bash
cd "E:\Project\UnityAITools"
node "Unity预制体AI友好查询/prefab_viewer.js" "Unity预制体AI友好查询/Dev_example/example1.prefab" --component 1744541728560894454
```

**输出**:

```json
{
  "m_Enabled": 1,
  "m_Script": {
    "type": "ref",
    "raw": "{fileID: 11500000, guid: 3383921b82e57b7439e7d76d6d21d9de, type: 3}"
  },
  "m_Name": {
    "type": "string",
    "raw": "K3Panel"
  }
}
```

### 错误处理示例

**命令**:

```bash
cd "E:\Project\UnityAITools"
node "Unity预制体AI友好查询/prefab_viewer.js" "notexist.prefab" --tree
```

**stderr 输出**:

```
Error: file not found: E:\Project\UnityAITools\notexist.prefab
```

**stdout 输出**:
(空)

**退出码**: 1

## 技术细节

### Unity 类型 ID 映射

脚本内置了常用 Unity 组件的类型 ID 映射表:

| 类型 ID | 组件名称       |
| ------- | -------------- |
| 1       | GameObject     |
| 4       | Transform      |
| 114     | MonoBehaviour  |
| 224     | RectTransform  |
| 100     | Camera         |
| 108     | Light          |
| 215     | Canvas         |
| 198     | ParticleSystem |
| ...     | ...            |

如果遇到未映射的类型 ID,会显示为 `Unknown(typeId)` 或回退到 `GameObject`。

### MonoBehaviour 脚本名反查机制

1. 从 MonoBehaviour 组件的 `m_Script` 字段提取 GUID
2. 使用 `rg` 或 `grep` 在 ASSETS_PATH 下递归搜索包含该 GUID 的 .meta 文件
3. 从匹配的文件路径中提取脚本名(去掉 .meta 后缀)
4. 如果找不到匹配文件,标记为 MissingScript

### GameObject ID 识别规则

- GameObject 的 ID 使用 Prefab YAML 中的 fileID(如 `160547937799403005`)
- 根节点识别: 查找 `m_Father` 为 `{fileID: 0}` 的 GameObject
- 如果找不到,使用第一个 GameObject 作为根节点

## 测试素材

项目包含测试素材,位于 `Unity预制体AI友好查询/Dev_example/` 目录:

- `example1.prefab`: 测试用 Prefab 文件
- `K3Panel.cs`: 示例 MonoBehaviour 脚本
- `K3Panel.cs.meta`: 脚本元文件(GUID: 3383921b82e57b7439e7d76d6d21d9de)

## 许可证

本项目仅供学习和参考使用。

## 贡献

欢迎提交 Issue 和 Pull Request 来改进这个工具。
