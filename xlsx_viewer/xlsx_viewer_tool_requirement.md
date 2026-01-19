# 需求文档：XLSX 查看器工具

## 1. 核心目标

**核心目标**：
开发一个 Go 语言编写的单文件可执行程序，用于便捷查看和搜索 xlsx 文件，提供行列数查询、表头查看、行列搜索等功能，帮助开发者快速了解和查找 Excel 数据。

**部署特性**：

- 编译为单个可执行文件，无需安装任何依赖或环境
- 可直接复制到任何目录独立运行
- 跨平台支持（Windows、Linux、macOS）

## 2. 范围与边界

### 功能点

- [x] **文件路径参数**：必填参数 `--path`，指定 xlsx 文件的绝对路径
- [x] **获取行列数**：`--size` 参数，返回 xlsx 文件第一个 sheet 的实际使用行数和列数（不包括空行空列）
- [x] **获取 x-y 行（表头）**：`--rows [x] [y]` 参数，返回第 x 到第 y 行数据，默认 x=1, y=3。可选 `--max-cols m` 参数，限制每行最多返回 m 列，默认 m=50
- [x] **获取 x-y 列（表头）**：`--cols [x] [y]` 参数，返回第 x 到第 y 列数据，默认 x=1, y=3。可选 `--max-rows m` 参数，限制每列最多返回 m 行，默认 m=50
- [x] **列搜索**：`--search-col <列索引> <关键词>` 参数，在指定列搜索关键词，返回所有符合条件行的完整数据。可选 `--mode <搜索模式>` 和 `--limit <数量>` 参数
- [x] **行搜索**：`--search-row <行索引> <关键词>` 参数，在指定行搜索关键词，返回所有符合条件列的完整数据。可选 `--mode <搜索模式>` 和 `--limit <数量>` 参数

### 排除项

- 不支持多 sheet 处理（只处理第一个 sheet）
- 不支持修改 xlsx 文件（只读）
- 不支持写入 xlsx 文件
- 不支持批量处理多个文件
- 不支持复杂的合并单元格处理
- 不支持公式计算和图表解析

## 3. 详细需求说明

### 3.1 命令行参数设计

**参数风格**：全 `--` 参数风格，所有参数都必须使用 `--` 前缀

**通用参数**：

- `--path <文件路径>`：必填，指定 xlsx 文件的绝对路径
- `--help`：可选，显示帮助信息

**操作类型参数（必选其一）**：

- `--size`：获取行列数
- `--rows [x] [y]`：获取第 x 到第 y 行，x 和 y 可选，默认 x=1, y=3
- `--cols [x] [y]`：获取第 x 到第 y 列，x 和 y 可选，默认 x=1, y=3
- `--search-col <列索引> <关键词>`：在指定列搜索
- `--search-row <行索引> <关键词>`：在指定行搜索

**可选参数**：

- `--max-cols <m>`：配合 `--rows` 使用，限制每行最多返回 m 列，默认 50
- `--max-rows <m>`：配合 `--cols` 使用，限制每列最多返回 m 行，默认 50
- `--mode <模式>`：配合搜索功能使用，可选值 `fuzzy`（默认）、`exact`、`regex`
- `--limit <数量>`：配合搜索功能使用，返回最多条数，默认 10

### 3.2 行列索引规则

- 行列索引从 **1 开始**（如第 1 行、第 1 列）
- 符合 Excel 表格的显示习惯，便于非技术人员理解

### 3.3 输出格式

- 使用 **JSON 格式**输出，便于程序解析
- 使用 `JSON.stringify(result, null, 2)` 美化输出，便于人类阅读
- 错误信息输出到 `console.error`，使用 `process.exit(1)` 退出

### 3.4 数据范围定义

- **行列数统计**：统计整个 sheet 的实际使用行列数（即有数据的最后一行/列，不包括空行空列）
- **Sheet 处理**：只处理第一个 sheet（不处理多 sheet）

### 3.5 搜索结果返回

- 返回 **整行/整列的数据**（完整的数据内容）
- 不需要高亮标注或特殊标识

## 4. 举例覆盖需求和边缘情况

### 4.1 基础用法示例

#### 例 1：获取行列数

```bash
xlsx_viewer --path F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx --size
```

**期望输出**：

```json
{
  "success": true,
  "operation": "size",
  "filePath": "F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx",
  "result": {
    "rows": 150,
    "cols": 20
  }
}
```

**说明**：返回文件的实际使用行数和列数，不包括空行空列。

#### 例 2：获取第 1-3 行（默认）

```bash
xlsx_viewer --path F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx --rows
```

**期望输出**：

```json
{
  "success": true,
  "operation": "rows",
  "filePath": "F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx",
  "result": {
    "rowRange": {
      "start": 1,
      "end": 3
    },
    "maxCols": 50,
    "data": [
      ["ID", "Name", "Description", "Duration", "EffectType", ...],
      ["1001", "攻击提升", "攻击力提升20%", 10, "attack", ...],
      ["1002", "防御提升", "防御力提升15%", 10, "defense", ...]
    ]
  }
}
```

**说明**：返回第 1-3 行数据，每行最多 50 列。

#### 例 3：获取第 1-5 行，每行最多 20 列

```bash
xlsx_viewer --path F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx --rows 1 5 --max-cols 20
```

```bash
xlsx_viewer --path F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx --rows 5 --max-cols 20
```

**期望输出**：

```json
{
  "success": true,
  "operation": "rows",
  "filePath": "F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx",
  "result": {
    "rowRange": {
      "start": 1,
      "end": 5
    },
    "maxCols": 20,
    "data": [
      ["ID", "Name", "Description", ...],
      ["1001", "攻击提升", "攻击力提升20%", ...],
      ["1002", "防御提升", "防御力提升15%", ...],
      ["1003", "速度提升", "速度提升10%", ...],
      ["1004", "生命提升", "生命值提升30%", ...]
    ]
  }
}
```

**说明**：返回第 1-5 行数据，每行最多 20 列。只指定一个参数时，表示从第 1 行到第 n 行。

#### 例 4：获取第 5-10 行

```bash
xlsx_viewer --path F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx --rows 5 10
```

**期望输出**：

```json
{
  "success": true,
  "operation": "rows",
  "filePath": "F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx",
  "result": {
    "rowRange": {
      "start": 5,
      "end": 10
    },
    "maxCols": 50,
    "data": [
      ["1005", "超级攻击", "攻击力提升50%", ...],
      ["1006", "超级防御", "防御力提升40%", ...],
      ["1007", "超级速度", "速度提升30%", ...],
      ["1008", "超级生命", "生命值提升50%", ...],
      ["1009", "暴击提升", "暴击率提升15%", ...],
      ["1010", "闪避提升", "闪避率提升12%", ...]
    ]
  }
}
```

**说明**：返回第 5-10 行数据，常用于查看中间的数据段。

注意 : -- rows 5 5 是 只返回第 5 行数据

#### 例 5：获取第 1-3 列（默认）

```bash
xlsx_viewer --path F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx --cols
```

**期望输出**：

```json
{
  "success": true,
  "operation": "cols",
  "filePath": "F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx",
  "result": {
    "colRange": {
      "start": 1,
      "end": 3
    },
    "maxRows": 50,
    "data": [
      {
        "colIndex": 1,
        "values": ["ID", "1001", "1002", "1003", ...]
      },
      {
        "colIndex": 2,
        "values": ["Name", "攻击提升", "防御提升", "速度提升", ...]
      },
      {
        "colIndex": 3,
        "values": ["Description", "攻击力提升20%", "防御力提升15%", "速度提升10%", ...]
      }
    ]
  }
}
```

**说明**：返回第 1-3 列数据，每列最多 50 行，数据以列的方式组织。

#### 例 6：获取第 3-5 列，每列最多 100 行

```bash
xlsx_viewer --path F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx --cols 3 5 --max-rows 100
```

**期望输出**：

```json
{
  "success": true,
  "operation": "cols",
  "filePath": "F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx",
  "result": {
    "colRange": {
      "start": 3,
      "end": 5
    },
    "maxRows": 100,
    "data": [
      {
        "colIndex": 3,
        "values": ["Description", "攻击力提升20%", "防御力提升15%", "速度提升10%", ...]
      },
      {
        "colIndex": 4,
        "values": ["Duration", 10, 10, 10, ...]
      },
      {
        "colIndex": 5,
        "values": ["EffectType", "attack", "defense", "speed", ...]
      }
    ]
  }
}
```

**说明**：返回第 3-5 列数据，每列最多 100 行。

### 4.2 搜索功能示例

#### 例 7：在第 2 列模糊搜索关键词"攻击"（默认模式）

```bash
xlsx_viewer --path F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx --search-col 2 "攻击"
```

**期望输出**：

```json
{
  "success": true,
  "operation": "search-col",
  "filePath": "F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx",
  "searchParams": {
    "colIndex": 2,
    "keyword": "攻击",
    "mode": "fuzzy",
    "limit": 10
  },
  "result": {
    "matches": 2,
    "data": [
      ["1001", "攻击提升", "攻击力提升20%", 10, "attack", ...],
      ["1005", "超级攻击", "攻击力提升50%", 20, "attack", ...]
    ]
  }
}
```

**说明**：返回第 2 列中包含"攻击"的所有行，最多返回 10 条，每行返回完整数据。

#### 例 8：在第 2 列精确搜索"攻击提升"

```bash
xlsx_viewer --path F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx --search-col 2 "攻击提升" --mode exact --limit 5
```

**期望输出**：

```json
{
  "success": true,
  "operation": "search-col",
  "filePath": "F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx",
  "searchParams": {
    "colIndex": 2,
    "keyword": "攻击提升",
    "mode": "exact",
    "limit": 5
  },
  "result": {
    "matches": 1,
    "data": [
      ["1001", "攻击提升", "攻击力提升20%", 10, "attack", ...]
    ]
  }
}
```

#### 例 9：在第 2 列使用正则表达式搜索

```bash
xlsx_viewer --path F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx --search-col 2 "攻击.*%" --mode regex --limit 10
```

**期望输出**：

```json
{
  "success": true,
  "operation": "search-col",
  "filePath": "F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx",
  "searchParams": {
    "colIndex": 2,
    "keyword": "攻击.*%",
    "mode": "regex",
    "limit": 10
  },
  "result": {
    "matches": 2,
    "data": [
      ["1001", "攻击提升", "攻击力提升20%", 10, "attack", ...],
      ["1005", "超级攻击", "攻击力提升50%", 20, "attack", ...]
    ]
  }
}
```

#### 例 10：在第 1 行搜索关键词"ID"

```bash
xlsx_viewer --path F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx --search-row 1 "ID"
```

**期望输出**：

```json
{
  "success": true,
  "operation": "search-row",
  "filePath": "F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx",
  "searchParams": {
    "rowIndex": 1,
    "keyword": "ID",
    "mode": "fuzzy",
    "limit": 10
  },
  "result": {
    "matches": 1,
    "data": [
      {
        "colIndex": 1,
        "value": "ID"
      }
    ]
  }
}
```

**说明**：返回第 1 行中包含"ID"的所有列，最多返回 10 条，每列返回完整数据。

#### 例 11：在第 3 行模糊搜索"%"，返回最多 20 条

```bash
xlsx_viewer --path F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx --search-row 3 "%" --mode fuzzy --limit 20
```

**期望输出**：

```json
{
  "success": true,
  "operation": "search-row",
  "filePath": "F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx",
  "searchParams": {
    "rowIndex": 3,
    "keyword": "%",
    "mode": "fuzzy",
    "limit": 20
  },
  "result": {
    "matches": 3,
    "data": [
      {
        "colIndex": 3,
        "value": "攻击力提升20%"
      },
      {
        "colIndex": 4,
        "value": "防御力提升15%"
      },
      {
        "colIndex": 5,
        "value": "速度提升10%"
      }
    ]
  }
}
```

### 4.3 边缘情况处理

#### 例 12：文件不存在

```bash
xlsx_viewer --path F:/不存在的文件.xlsx --size
```

**期望输出**：

```
错误: 文件不存在: F:/不存在的文件.xlsx
```

**退出码**：1

#### 例 13：未指定操作类型

```bash
xlsx_viewer --path F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx
```

**期望输出**：

```
错误: 必须指定操作类型 (--size, --rows, --cols, --search-col, --search-row 之一)

使用 --help 查看帮助信息
```

**退出码**：2

#### 例 14：未指定文件路径

```bash
xlsx_viewer --size
```

**期望输出**：

```
错误: 必须指定文件路径 (--path 参数)

使用 --help 查看帮助信息
```

**退出码**：2

#### 例 15：指定了多个操作类型（互斥）

```bash
xlsx_viewer --path F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx --size --rows
```

**期望输出**：

```
错误: 只能指定一个操作类型 (--size, --rows, --cols, --search-col, --search-row 之一)
```

**退出码**：2

#### 例 16：搜索时缺少关键词

```bash
xlsx_viewer --path F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx --search-col 2
```

**期望输出**：

```
错误: --search-col 需要指定关键词

使用 --help 查看帮助信息
```

**退出码**：2

#### 例 17：列索引超出范围

```bash
xlsx_viewer --path F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx --search-col 999 "测试"
```

**期望输出**：

```json
{
  "success": true,
  "operation": "search-col",
  "filePath": "F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx",
  "searchParams": {
    "colIndex": 999,
    "keyword": "测试",
    "mode": "fuzzy",
    "limit": 10
  },
  "result": {
    "matches": 0,
    "data": [],
    "warning": "列索引 999 超出范围，文件只有 20 列"
  }
}
```

**说明**：不会报错，返回空结果并给出警告信息。

#### 例 18：正则表达式语法错误

```bash
xlsx_viewer --path F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx --search-col 2 "[无效正则" --mode regex
```

**期望输出**：

```
错误: 正则表达式语法错误: Invalid regular expression: /[无效正则/
```

**退出码**：1

#### 例 19：搜索无结果

```bash
xlsx_viewer --path F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx --search-col 2 "不存在的关键词"
```

**期望输出**：

```json
{
  "success": true,
  "operation": "search-col",
  "filePath": "F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx",
  "searchParams": {
    "colIndex": 2,
    "keyword": "不存在的关键词",
    "mode": "fuzzy",
    "limit": 10
  },
  "result": {
    "matches": 0,
    "data": []
  }
}
```

#### 例 20：请求行数超过文件实际行数

```bash
xlsx_viewer --path F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx --rows 1000
```

**期望输出**：

```json
{
  "success": true,
  "operation": "rows",
  "filePath": "F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx",
  "result": {
    "rowCount": 150,
    "maxCols": 50,
    "data": [
      /* 所有150行数据 */
    ],
    "warning": "请求1000行，但文件只有150行"
  }
}
```

**说明**：返回所有可用数据，不报错，给出警告信息。

#### 例 21：空文件或只有表头

```bash
xlsx_viewer --path F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/空文件.xlsx --size
```

**期望输出**：

```json
{
  "success": true,
  "operation": "size",
  "filePath": "F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/空文件.xlsx",
  "result": {
    "rows": 0,
    "cols": 0
  }
}
```

#### 例 20：显示帮助信息

```bash
xlsx_viewer --help
```

**期望输出**：

```
XLSX 查看器 - 便捷查看和搜索 Excel 文件

用法:
  xlsx_viewer --path <xlsx文件路径> <操作类型> [参数]

必填参数:
  --path <文件路径>    指定 xlsx 文件的绝对路径

操作类型 (必选其一):
  --size                          显示文件行列数
  --rows [x] [y]                  显示第x到第y行(默认1-3行), 可选 --max-cols m 限制每行最多m列(默认50)
  --cols [x] [y]                  显示第x到第y列(默认1-3列), 可选 --max-rows m 限制每列最多m行(默认50)
  --search-col <列索引> <关键词>   在指定列搜索关键词
  --search-row <行索引> <关键词>   在指定行搜索关键词

搜索参数 (用于--search-col和--search-row):
  --mode <模式>        搜索模式: fuzzy(默认,模糊), exact(精确), regex(正则)
  --limit <数量>       返回最多条数(默认10)

其他:
  --help               显示此帮助信息

行列索引说明:
  行列索引从 1 开始(如第1行、第1列)

示例:
  xlsx_viewer --path data.xlsx --size
  xlsx_viewer --path data.xlsx --rows 1 5 --max-cols 20
  xlsx_viewer --path data.xlsx --rows 10
  xlsx_viewer --path data.xlsx --cols 1 3 --max-rows 100
  xlsx_viewer --path data.xlsx --search-col 2 "测试" --mode exact --limit 5
  xlsx_viewer --path data.xlsx --search-row 1 "error" --mode regex --limit 20
```

**退出码**：0

## 5. 构建和部署

**开发依赖**：

- Go 1.16 或更高版本
- `github.com/360EntSecGroup-Skylar/excelize/v2` 库（或类似 Go 的 xlsx 处理库）

**编译命令**：

```bash
# Windows 编译
go build -o xlsx_viewer.exe

# Linux 编译
GOOS=linux GOARCH=amd64 go build -o xlsx_viewer

# macOS 编译
GOOS=darwin GOARCH=amd64 go build -o xlsx_viewer
```

**使用说明**：

- 编译后得到单个可执行文件（xlsx_viewer 或 xlsx_viewer.exe）
- 无需安装任何依赖或运行时环境
- 可直接复制到任何目录独立使用
- 可以放入 PATH 环境变量目录，方便全局调用

## 6. 技术实现要点

1. **参数解析**：使用标准库 `os.Args` 或第三方库如 `github.com/spf13/cobra` 解析命令行参数，支持 `--option value` 格式
2. **错误处理**：统一的错误处理函数，使用不同的退出码（os.Exit(code)）
3. **文件验证**：使用 `os.Stat()` 检查文件存在性
4. **XLSX 读取**：使用 excelize 库读取文件，获取第一个 sheet
5. **行列统计**：使用 `xlsx.GetSheetName()` 和 `xlsx.GetRows()` 获取实际使用的行列范围
6. **搜索逻辑**：支持三种搜索模式（模糊、精确、正则），使用 Go 的 `strings` 和 `regexp` 包
7. **输出格式**：使用 `encoding/json` 包，`json.MarshalIndent()` 美化输出
8. **索引处理**：内部使用 0-based 索引，对外展示为 1-based 索引

## 7. 测试要求

使用 `F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库` 目录下的 xlsx 文件进行测试：

- `mydb_buff_tbl.xlsx` - 测试基本功能
- `mydb_item_base_tbl.xlsx` - 测试大数据量
- `mydb_effect_base_tbl.xlsx` - 测试搜索功能
- `mydb_monster_tbl.xlsx` - 测试边缘情况

**测试场景覆盖**：

1. 文件不存在
2. 未指定操作类型
3. 未指定文件路径
4. 指定多个操作类型
5. 搜索缺少关键词
6. 索引超出范围
7. 正则表达式错误
8. 搜索无结果
9. 请求行数超过实际
10. 空文件处理
