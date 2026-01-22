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
- `--rows <行范围>`：获取指定行，支持多种格式：
  - 单行：`--rows 5`（获取第 5 行）
  - 多行：`--rows 1,3,5`（获取第 1, 3, 5 行）
  - 连续范围：`--rows 1-5`（获取第 1-5 行）
  - 组合格式：`--rows 1-3,5,7-10`（获取第 1-3 行，第 5 行，第 7-10 行）
  - 默认：`--rows`（等效于 `--rows 1-3`，获取第 1-3 行）
- `--cols <列范围>`：获取指定列，支持多种格式：
  - Excel 列标号（推荐）：`--cols A`（获取第 A 列）、`--cols A,C,E`（获取第 A, C, E 列）、`--cols A-E`（获取第 A-E 列）、`--cols A-C,E,G-J`（组合格式）
  - 数字索引（兼容）：`--cols 1`（获取第 1 列）、`--cols 1,3,5`（获取第 1, 3, 5 列）、`--cols 1-5`（获取第 1-5 列）、`--cols 1-3,5,7-10`（组合格式）
  - 默认：`--cols`（等效于 `--cols A-C` 或 `--cols 1-3`，获取第 A-C 列）
  - **Excel 列标号规则**：A=1, B=2, ..., Z=26, AA=27, AB=28, ..., AZ=52, BA=53, ...
- `--search-col <列索引> <关键词>`：在指定列搜索，支持 Excel 列标号（A, B, C...）或数字索引（1, 2, 3...）
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

- 使用 **CSV 格式**输出，便于人类阅读和查看
- 第一行为列标号（空白单元格 + A, B, C, D...）
- 第一列为行号（1, 2, 3, 4...）
- 数据区域使用逗号分隔，内容包含逗号时用双引号包裹
- 错误信息输出到 `console.error`，使用 `process.exit(1)` 退出

**CSV 输出格式详解**：

对于获取行数据的操作（`--rows`，`--search-col`），输出格式为：

```
,A,B,C,D,E,F,... ← 第一行：空白单元格 + Excel 列标号（A, B, C...）
1,数据1,数据2,数据3,数据4,数据5,数据6,... ← 第一列：行号（1, 2, 3...）
2,数据1,数据2,数据3,数据4,数据5,数据6,...
3,数据1,数据2,数据3,数据4,数据5,数据6,...
...
```

对于获取列数据的操作（`--cols`，`--search-row`），输出格式为：

```
,A,B,C ← 第一行：空白单元格 + Excel 列标号（A, B, C）
1,数据1,数据2,数据3 ← 第一列：行号（1, 2, 3...）
2,数据1,数据2,数据3
3,数据1,数据2,数据3
...
```

**Excel 列标号规则**：

- 1-26：A, B, C, ..., Z
- 27-52：AA, AB, AC, ..., AZ
- 53-78：BA, BB, BC, ..., BZ
- 以此类推，类似 Excel 的列标号系统

**特殊字符处理**：

- 如果单元格内容包含逗号（,），则用双引号包裹整个单元格内容
- 如果单元格内容包含双引号（"），则用两个双引号（""）转义
- 示例：`包含,逗号的内容` → `"包含,逗号的内容"`
- 示例：`包含"引号"的内容` → `"包含""引号""的内容"`

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

```
Rows:150,Cols:20
```

**说明**：返回文件的实际使用行数和列数，不包括空行空列。

#### 例 2：获取第 1-3 行（默认）

```bash
xlsx_viewer --path F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx --rows
```

**期望输出**：

```
,A,B,C,D,E,...
1,ID,Name,Description,Duration,EffectType,...
2,1001,攻击提升,攻击力提升20%,10,attack,...
3,1002,防御提升,防御力提升15%,10,defense,...
```

**说明**：返回第 1-3 行数据，每行最多 50 列。第一行为列标号，第一列为行号。

#### 例 3：获取第 1-5 行，每行最多 20 列

```bash
xlsx_viewer --path F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx --rows 1 5 --max-cols 20
```

```bash
xlsx_viewer --path F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx --rows 5 --max-cols 20
```

**期望输出**：

```
,A,B,C,D,...
1,ID,Name,Description,...
2,1001,攻击提升,攻击力提升20%,...
3,1002,防御提升,防御力提升15%,...
4,1003,速度提升,速度提升10%,...
5,1004,生命提升,生命值提升30%,...
```

**说明**：返回第 1-5 行数据，每行最多 20 列。只指定一个参数时，表示从第 1 行到第 n 行。

#### 例 4：获取第 5-10 行

```bash
xlsx_viewer --path F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx --rows 5 10
```

**期望输出**：

```
,A,B,C,D,...
5,1005,超级攻击,攻击力提升50%,...
6,1006,超级防御,防御力提升40%,...
7,1007,超级速度,速度提升30%,...
8,1008,超级生命,生命值提升50%,...
9,1009,暴击提升,暴击率提升15%,...
10,1010,闪避提升,闪避率提升12%,...
```

**说明**：返回第 5-10 行数据，常用于查看中间的数据段。

注意 : -- rows 5 5 是 只返回第 5 行数据

#### 例 5：获取第 1-3 列（默认）

```bash
xlsx_viewer --path F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx --cols
```

**期望输出**：

```
,A,B,C
1,ID,Name,Description
2,1001,攻击提升,攻击力提升20%
3,1002,防御提升,防御力提升15%
4,1003,速度提升,速度提升10%
...
```

**说明**：返回第 1-3 列数据，每列最多 50 行，数据以列的方式组织，转置显示。

#### 例 6：获取第 3-5 列，每列最多 100 行

```bash
xlsx_viewer --path F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx --cols 3 5 --max-rows 100
```

**期望输出**：

```
,C,D,E
1,Description,Duration,EffectType
2,攻击力提升20%,10,attack
3,防御力提升15%,10,defense
4,速度提升10%,10,speed
...
```

**说明**：返回第 3-5 列数据，每列最多 100 行，转置显示。

#### 例 6a：使用 Excel 列标号获取第 A 列

```bash
xlsx_viewer --path F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx --cols A
```

**期望输出**：

```
,A
1,ID
2,1001
3,1002
4,1003
...
```

**说明**：返回第 A 列数据，支持使用 Excel 列标号。

#### 例 6b：使用 Excel 列标号获取第 A, C, E 列

```bash
xlsx_viewer --path F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx --cols A,C,E
```

**期望输出**：

```
,A,C,E
1,ID,Description,EffectType
2,1001,攻击力提升20%,attack
3,1002,防御力提升15%,defense
4,1003,速度提升10%,speed
...
```

**说明**：支持使用逗号分隔多个 Excel 列标号。

#### 例 6c：使用 Excel 列标号获取第 A-C 列

```bash
xlsx_viewer --path F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx --cols A-C
```

**期望输出**：

```
,A,B,C
1,ID,Name,Description
2,1001,攻击提升,攻击力提升20%
3,1002,防御提升,防御力提升15%
4,1003,速度提升,速度提升10%
...
```

**说明**：支持使用连字符（-）表示连续范围。

#### 例 6d：使用 Excel 列标号的组合格式

```bash
xlsx_viewer --path F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx --cols A-C,E,G-J
```

**期望输出**：

```
,A,B,C,E,G,H,I,J
1,ID,Name,Description,EffectType,...,...,...,...
2,1001,攻击提升,攻击力提升20%,attack,...,...,...,...
3,1002,防御提升,防御力提升15%,defense,...,...,...,...
...
```

**说明**：支持组合格式，可以混合使用连续范围和单独的列。

#### 例 6e：获取第 2, 4, 6 行（多行指定）

```bash
xlsx_viewer --path F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx --rows 2,4,6
```

**期望输出**：

```
,A,B,C,D,E
2,1001,攻击提升,攻击力提升20%,10,attack
4,1003,速度提升,速度提升10%,10,speed
6,1005,超级攻击,攻击力提升50%,20,attack
```

**说明**：支持使用逗号分隔多个行号。

#### 例 6f：获取第 2, 4, 6 行，使用 Excel 列标号限制列

```bash
xlsx_viewer --path F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx --rows 2,4,6 --max-cols 3
```

**期望输出**：

```
,A,B,C
2,1001,攻击提升,攻击力提升20%
4,1003,速度提升,速度提升10%
6,1005,超级攻击,攻击力提升50%
```

**说明**：`--max-cols` 限制每行最多返回的列数（前 3 列）。

### 4.2 搜索功能示例

#### 例 7：在第 2 列模糊搜索关键词"攻击"（默认模式）

```bash
xlsx_viewer --path F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx --search-col 2 "攻击"
```

**期望输出**：

```
搜索结果: 找到 2 个匹配
,A,B,C,D,E,...
2,1001,攻击提升,攻击力提升20%,10,attack,...
6,1005,超级攻击,攻击力提升50%,20,attack,...
```

**说明**：返回第 2 列中包含"攻击"的所有行，最多返回 10 条，每行返回完整数据。

#### 例 8：在第 2 列精确搜索"攻击提升"

```bash
xlsx_viewer --path F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx --search-col 2 "攻击提升" --mode exact --limit 5
```

**期望输出**：

```
搜索结果: 找到 1 个匹配
,A,B,C,D,E,...
2,1001,攻击提升,攻击力提升20%,10,attack,...
```

#### 例 9：在第 2 列使用正则表达式搜索

```bash
xlsx_viewer --path F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx --search-col 2 "攻击.*%" --mode regex --limit 10
```

**期望输出**：

```
搜索结果: 找到 2 个匹配
,A,B,C,D,E,...
2,1001,攻击提升,攻击力提升20%,10,attack,...
6,1005,超级攻击,攻击力提升50%,20,attack,...
```

#### 例 10：在第 1 行搜索关键词"ID"

```bash
xlsx_viewer --path F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx --search-row 1 "ID"
```

**期望输出**：

```
搜索结果: 找到 1 个匹配
,A
1,ID
```

**说明**：返回第 1 行中包含"ID"的所有列，最多返回 10 条，每列返回完整数据。

#### 例 11：在第 3 行模糊搜索"%"，返回最多 20 条

```bash
xlsx_viewer --path F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx --search-row 3 "%" --mode fuzzy --limit 20
```

**期望输出**：

```
搜索结果: 找到 3 个匹配
,C,D,E
3,攻击力提升20%,防御力提升15%,速度提升10%
```

#### 例 12：使用 Excel 列标号在第 B 列搜索"攻击"

```bash
xlsx_viewer --path F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx --search-col B "攻击"
```

**期望输出**：

```
搜索结果: 找到 2 个匹配
,A,B,C,D,E
2,1001,攻击提升,攻击力提升20%,10,attack
6,1005,超级攻击,攻击力提升50%,20,attack
```

**说明**：支持使用 Excel 列标号进行列搜索。

#### 例 13：使用 Excel 列标号在第 B 列精确搜索"攻击提升"

```bash
xlsx_viewer --path F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx --search-col B "攻击提升" --mode exact
```

**期望输出**：

```
搜索结果: 找到 1 个匹配
,A,B,C,D,E
2,1001,攻击提升,攻击力提升20%,10,attack
```

### 4.3 边缘情况处理

#### 例 14：文件不存在

```bash
xlsx_viewer --path F:/不存在的文件.xlsx --size
```

**期望输出**：

```
错误: 文件不存在: F:/不存在的文件.xlsx
```

**退出码**：1

#### 例 15：未指定操作类型

```bash
xlsx_viewer --path F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx
```

**期望输出**：

```
错误: 必须指定操作类型 (--size, --rows, --cols, --search-col, --search-row 之一)

使用 --help 查看帮助信息
```

**退出码**：2

#### 例 16：未指定文件路径

```bash
xlsx_viewer --size
```

**期望输出**：

```
错误: 必须指定文件路径 (--path 参数)

使用 --help 查看帮助信息
```

**退出码**：2

#### 例 17：指定了多个操作类型（互斥）

```bash
xlsx_viewer --path F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx --size --rows
```

**期望输出**：

```
错误: 只能指定一个操作类型 (--size, --rows, --cols, --search-col, --search-row 之一)
```

**退出码**：2

#### 例 18：搜索时缺少关键词

```bash
xlsx_viewer --path F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx --search-col 2
```

**期望输出**：

```
错误: --search-col 需要指定关键词

使用 --help 查看帮助信息
```

**退出码**：2

#### 例 19：列索引超出范围

```bash
xlsx_viewer --path F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx --search-col 999 "测试"
```

**期望输出**：

```
警告: 列索引 999 超出范围，文件只有 20 列
搜索结果: 找到 0 个匹配
```

**说明**：不会报错，返回空结果并给出警告信息。

#### 例 20：Excel 列标号超出范围

```bash
xlsx_viewer --path F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx --search-col ZZ "测试"
```

**期望输出**：

```
警告: 列标号 ZZ 超出范围，文件只有 20 列（T 列）
搜索结果: 找到 0 个匹配
```

**说明**：使用 Excel 列标号时，如果超出范围，给出友好的提示。

#### 例 21：正则表达式语法错误

```bash
xlsx_viewer --path F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx --search-col 2 "[无效正则" --mode regex
```

**期望输出**：

```
错误: 正则表达式语法错误: Invalid regular expression: /[无效正则/
```

**退出码**：1

#### 例 22：搜索无结果

```bash
xlsx_viewer --path F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx --search-col 2 "不存在的关键词"
```

**期望输出**：

```
搜索结果: 找到 0 个匹配
```

#### 例 23：请求行数超过文件实际行数

```bash
xlsx_viewer --path F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx --rows 1000
```

**期望输出**：

```
警告: 请求1000行，但文件只有150行
```

（随后是所有 150 行数据的 CSV 格式输出，第一行为列标号，第一列为行号）

**说明**：返回所有可用数据，不报错，给出警告信息。

#### 例 24：空文件或只有表头

```bash
xlsx_viewer --path F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/空文件.xlsx --size
```

**期望输出**：

```
Rows:0,Cols:0
```

#### 例 25：显示帮助信息

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
- `github.com/xuri/excelize/v2` 库（或类似 Go 的 xlsx 处理库）

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

## 6. 技术实现要点

1. **参数解析**：使用标准库 `os.Args` 或第三方库如 `github.com/spf13/cobra` 解析命令行参数，支持 `--option value` 格式
2. **错误处理**：统一的错误处理函数，使用不同的退出码（os.Exit(code)）
3. **文件验证**：使用 `os.Stat()` 检查文件存在性
4. **XLSX 读取**：使用 excelize 库读取文件，获取第一个 sheet
5. **行列统计**：使用 `xlsx.GetSheetName()` 和 `xlsx.GetRows()` 获取实际使用的行列范围
6. **搜索逻辑**：支持三种搜索模式（模糊、精确、正则），使用 Go 的 `strings` 和 `regexp` 包
7. **输出格式**：使用 CSV 格式输出，第一行为列标号（空白 + A, B, C...），第一列为行号（1, 2, 3...），内容包含逗号时用双引号包裹
8. **索引处理**：内部使用 0-based 索引，对外展示为 1-based 索引
9. **列标号转换**：实现数字索引到 Excel 列标的转换函数（如 1->A, 2->B, 27->AA）

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
