# XLSX 查看器 - AI 友好 Excel 查询工具

一个专为 AI 设计的 Excel (.xlsx) 文件查询工具,以 JSON 格式输出数据,方便 AI 或编程助手快速读取和分析 Excel 表格数据。

## 📋 目录

- [核心特性](#核心特性)
- [快速开始](#快速开始)
- [详细使用](#详细使用)
- [AI 工具配置](#ai-工具配置)
- [输出格式说明](#输出格式说明)
- [参数说明](#参数说明)
- [错误处理](#错误处理)
- [使用技巧](#使用技巧)
- [故障排查](#故障排查)

## ✨ 核心特性

- 📦 **纯 JSON 输出**: stdout 仅输出 JSON,便于 AI 解析
- 🔍 **多种查询模式**: 支持行列范围查询、关键词搜索
- 🎯 **智能索引**: 行列索引从 1 开始,符合人类习惯
- ⚡ **高性能**: 使用 Go 语言编写,处理大文件快速高效
- 🚫 **只读模式**: 不修改任何文件,安全可靠
- 🎨 **智能压缩**: JSON 输出自动压缩,保持可读性

## 🚀 快速开始

### 前置要求

- **Go 编译器**: 版本 1.16 或更高(如需要重新编译)
- **操作系统**: Windows/Linux/macOS

### 基本使用

```bash
# 查看文件行列数
./xlsx_viewer.exe --path "data.xlsx" --size

# 查看前 3 行数据
./xlsx_viewer.exe --path "data.xlsx" --rows 1 3

# 在第 3 列搜索关键词
./xlsx_viewer.exe --path "data.xlsx" --search-col 3 "测试"
```

## 📖 详细使用

### 1. 获取文件尺寸 (`--size`)

返回 Excel 文件的行列数信息。

**命令格式**:
```bash
./xlsx_viewer.exe --path <文件路径> --size
```

**示例输出**:
```json
{
  "success": true,
  "operation": "size",
  "filePath": "F:/UnityProject/data.xlsx",
  "result": {
    "rows": 1276,
    "cols": 45
  }
}
```

**AI 使用场景**:
- 快速了解 Excel 文件的大小
- 在执行其他查询前检查索引范围
- 验证文件是否成功读取

---

### 2. 查询行数据 (`--rows`)

返回指定范围内的行数据。

**命令格式**:
```bash
./xlsx_viewer.exe --path <文件路径> --rows [起始行] [结束行] [--max-cols <列数>]
```

**参数说明**:
- `[起始行] [结束行]`: 可选,默认 1-3 行
- `--max-cols <列数>`: 可选,限制每行最多显示的列数,默认 50

**示例 1**: 查看前 5 行
```bash
./xlsx_viewer.exe --path "data.xlsx" --rows 1 5
```

**示例输出**:
```json
{
  "success": true,
  "operation": "rows",
  "filePath": "F:/UnityProject/data.xlsx",
  "result": {
    "rowRange": {
      "start": 1,
      "end": 5
    },
    "maxCols": 50,
    "data": [
      [ "BUFFID", "deleted", "BUFF名字", "BUFF类型", "BUFF等级" ],
      [ "10001", "1", "天魔加成", "0", "1" ],
      [ "10002", "0", "神兽祝福", "1", "5" ]
    ]
  }
}
```

**示例 2**: 查看第 10 行,限制最多 10 列
```bash
./xlsx_viewer.exe --path "data.xlsx" --rows 10 10 --max-cols 10
```

**AI 使用场景**:
- 快速浏览表头结构
- 查看特定行的数据
- 分析数据模式和格式

---

### 3. 查询列数据 (`--cols`)

返回指定范围内的列数据。

**命令格式**:
```bash
./xlsx_viewer.exe --path <文件路径> --cols [起始列] [结束列] [--max-rows <行数>]
```

**参数说明**:
- `[起始列] [结束列]`: 可选,默认 1-3 列
- `--max-rows <行数>`: 可选,限制每列最多显示的行数,默认 50

**示例 1**: 查看第 1-3 列,限制最多 10 行
```bash
./xlsx_viewer.exe --path "data.xlsx" --cols 1 3 --max-rows 10
```

**示例输出**:
```json
{
  "success": true,
  "operation": "cols",
  "filePath": "F:/UnityProject/data.xlsx",
  "result": {
    "colRange": {
      "start": 1,
      "end": 3
    },
    "maxRows": 10,
    "data": [
      {
        "colIndex": 1,
        "values": [ "BUFFID", "10001", "10002", "10003", "10004" ]
      },
      {
        "colIndex": 2,
        "values": [ "deleted", "1", "0", "0", "1" ]
      },
      {
        "colIndex": 3,
        "values": [ "BUFF名字", "天魔加成", "神兽祝福", "狂暴", "闪避" ]
      }
    ]
  }
}
```

**示例 2**: 查看第 5 列的全部数据
```bash
./xlsx_viewer.exe --path "data.xlsx" --cols 5 5 --max-rows 1000
```

**AI 使用场景**:
- 分析特定列的数据分布
- 查找某一列的唯一值
- 理解列与列之间的关系

---

### 4. 列搜索 (`--search-col`)

在指定列中搜索关键词,返回匹配的完整行数据。

**命令格式**:
```bash
./xlsx_viewer.exe --path <文件路径> --search-col <列索引> <关键词> [--mode <模式>] [--limit <数量>]
```

**参数说明**:
- `<列索引>`: 要搜索的列索引(从 1 开始)
- `<关键词>`: 搜索关键词
- `--mode <模式>`: 搜索模式,可选值:
  - `fuzzy`: 模糊匹配(默认),包含关键词即可
  - `exact`: 精确匹配,完全相等
  - `regex`: 正则表达式匹配
- `--limit <数量>`: 返回最多条数,默认 10

**示例 1**: 在第 3 列模糊搜索"天魔"
```bash
./xlsx_viewer.exe --path "data.xlsx" --search-col 3 "天魔"
```

**示例输出**:
```json
{
  "success": true,
  "operation": "search-col",
  "filePath": "F:/UnityProject/data.xlsx",
  "searchParams": {
    "colIndex": 3,
    "keyword": "天魔",
    "mode": "fuzzy",
    "limit": 10
  },
  "result": {
    "matches": 2,
    "data": [
      [ "10001", "1", "天魔加成", "0", "1", "..." ],
      [ "10015", "0", "天魔之怒", "1", "3", "..." ]
    ]
  }
}
```

**示例 2**: 在第 2 列精确搜索"0",最多返回 20 条
```bash
./xlsx_viewer.exe --path "data.xlsx" --search-col 2 "0" --mode exact --limit 20
```

**示例 3**: 使用正则表达式搜索
```bash
./xlsx_viewer.exe --path "data.xlsx" --search-col 3 ".*加成.*" --mode regex --limit 5
```

**AI 使用场景**:
- 查找特定配置项
- 搜索包含特定关键词的记录
- 使用正则表达式进行复杂匹配

---

### 5. 行搜索 (`--search-row`)

在指定行中搜索关键词,返回匹配的单元格位置和值。

**命令格式**:
```bash
./xlsx_viewer.exe --path <文件路径> --search-row <行索引> <关键词> [--mode <模式>] [--limit <数量>]
```

**参数说明**:
- `<行索引>`: 要搜索的行索引(从 1 开始)
- `<关键词>`: 搜索关键词
- `--mode <模式>`: 搜索模式(同列搜索)
- `--limit <数量>`: 返回最多条数,默认 10

**示例 1**: 在第 1 行搜索包含"BUFF"的列
```bash
./xlsx_viewer.exe --path "data.xlsx" --search-row 1 "BUFF"
```

**示例输出**:
```json
{
  "success": true,
  "operation": "search-row",
  "filePath": "F:/UnityProject/data.xlsx",
  "searchParams": {
    "rowIndex": 1,
    "keyword": "BUFF",
    "mode": "fuzzy",
    "limit": 10
  },
  "result": {
    "matches": 5,
    "data": [
      { "colIndex": 1, "value": "BUFFID" },
      { "colIndex": 3, "value": "BUFF名字" },
      { "colIndex": 4, "value": "BUFF类型" },
      { "colIndex": 5, "value": "BUFF等级" },
      { "colIndex": 6, "value": "BUFF持续时间" }
    ]
  }
}
```

**示例 2**: 在第 100 行精确搜索特定值
```bash
./xlsx_viewer.exe --path "data.xlsx" --search-row 100 "10001" --mode exact
```

**AI 使用场景**:
- 查找表头中的特定字段名
- 在单行中定位数据
- 分析行内数据的分布

---

### 6. 获取帮助 (`--help` / `-h`)

显示工具使用帮助信息。

```bash
./xlsx_viewer.exe --help
# 或
./xlsx_viewer.exe -h
```

## 🤖 AI 工具配置

### Cursor AI 配置

将以下内容添加到项目根目录的 `.cursorrules` 文件:

```text
# XLSX 查询工具

当需要查看或分析 Excel (.xlsx) 文件时:

1. 基本流程:
   - 先使用 --size 查看文件大小
   - 使用 --rows 查看前几行了解结构
   - 根据需要使用查询功能获取数据

2. 常用命令:
   - 查看大小: ./xlsx_viewer.exe --path "文件路径" --size
   - 查看表头: ./xlsx_viewer.exe --path "文件路径" --rows 1 3 --max-cols 20
   - 列搜索: ./xlsx_viewer.exe --path "文件路径" --search-col 3 "关键词" --limit 10
   - 行搜索: ./xlsx_viewer.exe --path "文件路径" --search-row 1 "字段名"

3. 注意事项:
   - 行列索引从 1 开始
   - 搜索支持 fuzzy/exact/regex 三种模式
   - 使用 --limit 限制返回数量,避免输出过长
   - JSON 输出已经过智能压缩,易于阅读
```

### Windsurf / AGENTS.md 配置

```markdown
## XLSX 文件查询

### 工作流程

1. 获取文件大小: `xlsx_viewer.exe --path "文件.xlsx" --size`
2. 查看表头结构: `xlsx_viewer.exe --path "文件.xlsx" --rows 1 5 --max-cols 20`
3. 根据需要执行查询:
   - 行数据: `--rows`
   - 列数据: `--cols`
   - 列搜索: `--search-col`
   - 行搜索: `--search-row`

### 常用命令

- 获取尺寸: `xlsx_viewer.exe --path "data.xlsx" --size`
- 查看表头: `xlsx_viewer.exe --path "data.xlsx" --rows 1 5 --max-cols 20`
- 列搜索: `xlsx_viewer.exe --path "data.xlsx" --search-col 3 "关键词" --limit 10`
- 行搜索: `xlsx_viewer.exe --path "data.xlsx" --search-row 1 "字段名"`

### 重要提示

- 行列索引从 1 开始
- 搜索支持三种模式: fuzzy(默认), exact, regex
- 使用 --limit 参数控制返回数量
- 输出格式为标准 JSON,AI 可直接解析
```

### Claude / ChatGPT 等工具

```markdown
我有一个 Excel 文件查询工具,可以帮助你分析 xlsx 文件。

工具位置:
E:/Project/UnityAITools/xlsx_viewer/xlsx_viewer.exe

常用命令:
- 获取大小: ./xlsx_viewer.exe --path "文件.xlsx" --size
- 查看表头: ./xlsx_viewer.exe --path "文件.xlsx" --rows 1 5 --max-cols 20
- 列搜索: ./xlsx_viewer.exe --path "文件.xlsx" --search-col 3 "关键词" --limit 10
- 行搜索: ./xlsx_viewer.exe --path "文件.xlsx" --search-row 1 "字段名"

参数说明:
- 行列索引从 1 开始
- 搜索模式: fuzzy(模糊), exact(精确), regex(正则)
- 使用 --limit 限制返回数量
- --max-cols 和 --max-rows 用于限制输出范围
```

## 📊 输出格式说明

### 统一响应结构

所有查询都返回统一的 JSON 响应结构:

```json
{
  "success": true/false,
  "operation": "操作类型",
  "filePath": "文件路径",
  "searchParams": {
    "colIndex": 列索引,
    "rowIndex": 行索引,
    "keyword": "关键词",
    "mode": "搜索模式",
    "limit": 限制数量
  },
  "result": {
    // 具体数据,根据操作类型不同
  },
  "errorMessage": "错误信息(仅失败时)"
}
```

### 数据类型说明

| 数据类型 | 说明 | 示例 |
|---------|------|------|
| `string` | 字符串 | `"天魔加成"` |
| `number` | 数字 | `10001`, `1`, `0` |
| `null` | 空值 | `null` |

### 智能压缩说明

工具会对 JSON 输出进行智能压缩:
- **二维数组**: 子数组换行,子数组内部压缩为单行
- **对象数组**: 保持格式化,便于阅读

**示例**:

```json
// 二维数组(查询结果)
"data": [
  [ "BUFFID", "deleted", "BUFF名字" ],
  [ "10001", "1", "天魔加成" ]
]

// 对象数组(列数据)
"data": [
  {
    "colIndex": 1,
    "values": [ "BUFFID", "10001", "10002" ]
  },
  {
    "colIndex": 2,
    "values": [ "deleted", "1", "0" ]
  }
]
```

## 📝 参数说明

### 必填参数

| 参数 | 说明 | 示例 |
|------|------|------|
| `--path <文件路径>` | xlsx 文件的绝对路径或相对路径 | `--path "data.xlsx"` |

### 操作类型(必选其一)

| 参数 | 说明 | 示例 |
|------|------|------|
| `--size` | 获取文件行列数 | `--size` |
| `--rows [x] [y]` | 查询第 x 到第 y 行(默认 1-3) | `--rows 1 5` |
| `--cols [x] [y]` | 查询第 x 到第 y 列(默认 1-3) | `--cols 1 3` |
| `--search-col <列> <关键词>` | 在指定列搜索 | `--search-col 3 "测试"` |
| `--search-row <行> <关键词>` | 在指定行搜索 | `--search-row 1 "BUFF"` |

### 可选参数

| 参数 | 说明 | 默认值 | 示例 |
|------|------|--------|------|
| `--max-cols <数量>` | 行查询时限制每行最多列数 | 50 | `--max-cols 20` |
| `--max-rows <数量>` | 列查询时限制每列最多行数 | 50 | `--max-rows 100` |
| `--mode <模式>` | 搜索模式: fuzzy/exact/regex | fuzzy | `--mode exact` |
| `--limit <数量>` | 搜索时最多返回条数 | 10 | `--limit 20` |
| `--help` / `-h` | 显示帮助信息 | - | `--help` |

## ⚠️ 错误处理

### 退出码说明

| 退出码 | 说明 | 常见场景 |
|--------|------|----------|
| 0 | 成功 | 正常执行完成 |
| 1 | 一般错误 | 文件不存在、正则错误等 |
| 2 | 参数错误 | 缺少必填参数、参数格式错误 |

### 常见错误场景

#### 1. 文件不存在

```bash
./xlsx_viewer.exe --path "notexist.xlsx" --size
```

**stderr 输出**:
```
错误: 文件不存在: notexist.xlsx
```

**退出码**: 1

#### 2. 未指定文件路径

```bash
./xlsx_viewer.exe --size
```

**stderr 输出**:
```
错误: 必须指定文件路径 (--path 参数)
```

**退出码**: 2

#### 3. 未指定操作类型

```bash
./xlsx_viewer.exe --path "data.xlsx"
```

**stderr 输出**:
```
错误: 必须指定操作类型 (--size, --rows, --cols, --search-col, --search-row 之一)
```

**退出码**: 2

#### 4. 正则表达式语法错误

```bash
./xlsx_viewer.exe --path "data.xlsx" --search-col 3 "[invalid(" --mode regex
```

**stderr 输出**:
```
错误: 正则表达式语法错误: error parsing regexp: missing closing ]: `[invalid(`
```

**退出码**: 1

#### 5. 索引超出范围(警告,不中断)

当查询范围超出实际数据范围时,会在返回结果中包含 `warning` 字段:

```json
{
  "success": true,
  "operation": "rows",
  "result": {
    "rowRange": { "start": 1, "end": 2000 },
    "maxCols": 50,
    "data": [ ... ],
    "warning": "查询的行范围超出了实际数据的行数(1276),已自动调整"
  }
}
```

## 💡 使用技巧

### 典型工作流程

**场景 1: 快速了解 Excel 文件结构**

```bash
# 1. 先获取文件大小
./xlsx_viewer.exe --path "data.xlsx" --size

# 输出: {"rows": 1276, "cols": 45}

# 2. 查看前几行了解表头结构
./xlsx_viewer.exe --path "data.xlsx" --rows 1 3 --max-cols 20
```

**场景 2: 查找特定配置项**

```bash
# 1. 先确认列名(在第 1 行搜索)
./xlsx_viewer.exe --path "data.xlsx" --search-row 1 "BUFFID"

# 输出: 知道 BUFFID 在第 1 列

# 2. 搜索特定 ID
./xlsx_viewer.exe --path "data.xlsx" --search-col 1 "10001" --mode exact
```

**场景 3: 分析数据分布**

```bash
# 查看某一列的前 100 行数据
./xlsx_viewer.exe --path "data.xlsx" --cols 3 3 --max-rows 100
```

### 搜索模式选择建议

| 场景 | 推荐模式 | 示例 |
|------|----------|------|
| 查找包含特定关键词 | fuzzy | `--mode fuzzy` (默认) |
| 精确匹配特定值 | exact | `--mode exact` |
| 复杂模式匹配 | regex | `--mode regex` |
| 查找数字 ID | exact | `--mode exact` |
| 查找文本描述 | fuzzy | `--mode fuzzy` |

### 性能优化建议

1. **使用 `--limit` 控制返回数量**:
   ```bash
   # 限制返回 10 条,避免输出过长
   ./xlsx_viewer.exe --path "data.xlsx" --search-col 3 "关键词" --limit 10
   ```

2. **使用 `--max-cols` 和 `--max-rows` 限制范围**:
   ```bash
   # 只查看前 10 列
   ./xlsx_viewer.exe --path "data.xlsx" --rows 1 5 --max-cols 10
   ```

3. **优先使用 `fuzzy` 模式**(性能最好):
   ```bash
   # 模糊匹配速度最快
   ./xlsx_viewer.exe --path "data.xlsx" --search-col 3 "关键词"
   ```

### 调试技巧

**查看完整的命令帮助**:
```bash
./xlsx_viewer.exe --help
```

**检查文件是否可访问**:
```bash
# Windows
dir data.xlsx

# Linux/Mac
ls -la data.xlsx
```

**验证查询结果**:
```bash
# 查看前 1 行(表头)
./xlsx_viewer.exe --path "data.xlsx" --rows 1 1

# 查看最后 1 行
./xlsx_viewer.exe --path "data.xlsx" --rows 1276 1276
```

## 🔧 故障排查

### 问题 1: 文件读取失败

**症状**: 显示"文件不存在"错误

**解决方案**:
1. 确认文件路径正确
2. 检查文件扩展名是否为 `.xlsx`
3. 确认文件未被其他程序占用
4. 尝试使用绝对路径

```bash
# 使用绝对路径
./xlsx_viewer.exe --path "F:/Project/data.xlsx" --size
```

### 问题 2: 查询结果为空

**症状**: `data` 字段为空数组或 `matches: 0`

**可能原因**:
1. 搜索关键词不存在
2. 搜索模式不匹配
3. 索引超出范围

**解决方案**:
- 使用 `fuzzy` 模式尝试更宽泛的匹配
- 检查关键词拼写
- 先用 `--rows` 或 `--cols` 查看数据内容

```bash
# 先查看数据内容
./xlsx_viewer.exe --path "data.xlsx" --rows 1 5

# 再进行搜索
./xlsx_viewer.exe --path "data.xlsx" --search-col 3 "关键词" --mode fuzzy
```

### 问题 3: 搜索模式不工作

**症状**: 搜索结果不符合预期

**解决方案**:
1. 确认 `--mode` 参数正确
2. `fuzzy`: 包含关键词即可
3. `exact`: 必须完全相等
4. `regex`: 正则表达式语法

```bash
# 模糊匹配(默认)
./xlsx_viewer.exe --path "data.xlsx" --search-col 3 "天魔"

# 精确匹配
./xlsx_viewer.exe --path "data.xlsx" --search-col 3 "天魔加成" --mode exact

# 正则表达式
./xlsx_viewer.exe --path "data.xlsx" --search-col 3 ".*加成.*" --mode regex
```

### 问题 4: 输出 JSON 格式错误

**症状**: AI 工具无法解析 JSON 输出

**可能原因**:
1. stderr 中包含额外信息
2. 编码问题

**解决方案**:
- 只读取 stdout 的输出
- stderr 输出错误信息,stdout 输出有效的 JSON
- 确保使用 UTF-8 编码

### 问题 5: Windows 路径问题

**症状**: 路径包含空格或特殊字符导致错误

**解决方案**:
使用引号包裹路径:

```bash
# 正确
./xlsx_viewer.exe --path "F:/Project/My Folder/data.xlsx" --size

# 错误
./xlsx_viewer.exe --path F:/Project/My Folder/data.xlsx --size
```

### 问题 6: 性能问题

**症状**: 大文件查询很慢

**解决方案**:
1. 使用 `--limit` 限制返回数量
2. 使用 `--max-cols` 和 `--max-rows` 减少数据量
3. 优先使用 `fuzzy` 搜索模式
4. 避免查询过大范围

```bash
# 优化查询
./xlsx_viewer.exe --path "large_data.xlsx" --search-col 3 "关键词" --limit 10 --mode fuzzy
```

## 📁 文件说明

- **main.go**: Go 源代码文件
- **xlsx_viewer.exe**: 编译后的可执行文件
- **go.mod**: Go 依赖配置文件

### 重新编译

如果需要修改源代码并重新编译:

```bash
# 进入工具目录
cd xlsx_viewer

# 编译
go build -o xlsx_viewer.exe main.go

# 运行
./xlsx_viewer.exe --path "data.xlsx" --size
```

## 🎯 开始使用

1. **下载或编译工具**:
   - 使用预编译的 `xlsx_viewer.exe`,或
   - 使用 `go build` 从源代码编译

2. **测试工具**:
   ```bash
   ./xlsx_viewer.exe --help
   ```

3. **配置 AI 工具**:
   - 将"AI 工具配置"部分的内容添加到你的 AI 编程助手配置文件

4. **开始查询**:
   ```bash
   ./xlsx_viewer.exe --path "data.xlsx" --size
   ```

## ⚠️ 重要提示

- **行列索引从 1 开始**(不是 0)
- **搜索默认不区分大小写**(fuzzy 和 exact 模式)
- **正则模式区分大小写**,取决于正则表达式本身
- **索引超出范围会显示警告**,但不会中断程序
- **JSON 输出已经过智能压缩**,便于 AI 解析
- **stdout 只输出 JSON**,错误信息输出到 stderr
- **工具是只读的**,不会修改任何文件
- **大文件查询建议使用 `--limit`** 避免输出过长

## 📊 完整示例

### 示例 1: 完整的配置项查询流程

```bash
# 步骤 1: 获取文件大小
./xlsx_viewer.exe --path "F:/UnityProject/data.xlsx" --size

# 输出: {"success": true, "result": {"rows": 1276, "cols": 45}}

# 步骤 2: 查看表头结构
./xlsx_viewer.exe --path "F:/UnityProject/data.xlsx" --rows 1 1 --max-cols 20

# 输出: 了解列名和列的对应关系

# 步骤 3: 搜索特定配置项
./xlsx_viewer.exe --path "F:/UnityProject/data.xlsx" --search-col 1 "10001" --mode exact

# 输出: 获取 ID 为 10001 的完整行数据
```

### 示例 2: 分析数据分布

```bash
# 查看 BUFF类型 列(第 4 列)的数据
./xlsx_viewer.exe --path "F:/UnityProject/data.xlsx" --cols 4 4 --max-rows 50

# 输出: 获取前 50 行的第 4 列数据,了解类型分布
```

### 示例 3: 多关键词搜索

```bash
# 在第 3 列搜索包含"加成"的记录
./xlsx_viewer.exe --path "F:/UnityProject/data.xlsx" --search-col 3 "加成" --limit 5

# 输出: 获取前 5 条匹配记录
```

---

**提示**: 将"AI 工具配置"部分复制到你的 AI 编程助手配置文件中,即可快速启用 XLSX 查询功能!
