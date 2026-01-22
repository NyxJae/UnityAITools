---
name: xlsx-viewer
description: Excel (.xlsx) 文件查询和分析工具。使用场景：查看配置数据、分析 Excel 文件内容、搜索特定表格内容、提取表格数据。触发关键词：查看xlsx、搜索excel、查询配置、xlsx查看、表格搜索、配置数据、找找配置
---

# XLSX Viewer - Excel 文件查询工具

## Instructions

### Context

这个技能帮助你快速查询和分析 Excel (.xlsx) 文件内容，特别适用于游戏配置数据的查看和搜索。工具以 CSV 格式输出数据，便于解析和处理。

**工具位置**：`<Absolute Path>/scripts/xlsx_viewer.exe`

### Usage

```bash
<Absolute Path>/scripts/xlsx_viewer.exe --path <xlsx文件路径> <操作类型> [参数]
```

### Parameters

必填参数:

- `--path <文件路径>`: 指定 xlsx 文件的绝对路径

操作类型(必选其一):

- `--size`: 显示文件行列数
- `--rows [x] [y]`: 显示第 x 到第 y 行(默认 1-3 行), 可选 `--max-cols m` 限制每行最多 m 列(默认 50)
- `--cols [x] [y]`: 显示第 x 到第 y 列(默认 1-3 列), 可选 `--max-rows m` 限制每列最多 m 行(默认 50)
- `--search-col <列索引> <关键词>`: 在指定列搜索关键词
- `--search-row <行索引> <关键词>`: 在指定行搜索关键词

搜索参数(用于 --search-col 和 --search-row):

- `--mode <模式>`: 搜索模式,可选 fuzzy(默认,模糊), exact(精确), regex(正则)
- `--limit <数量>`: 返回最多条数(默认 10)

其他:

- `--help`: 显示帮助信息

### Output

- 输出为 CSV 格式
- 行列索引从 1 开始

### Examples

```bash
# 查看行列数
<Absolute Path>/scripts/xlsx_viewer.exe --path data.xlsx --size

# 查看前5行,限制20列
<Absolute Path>/scripts/xlsx_viewer.exe --path data.xlsx --rows 1 5 --max-cols 20

# 查看第10行
<Absolute Path>/scripts/xlsx_viewer.exe --path data.xlsx --rows 10

# 查看前3列,限制100行
<Absolute Path>/scripts/xlsx_viewer.exe --path data.xlsx --cols 1 3 --max-rows 100

# 在第2列搜索"测试",精确匹配,最多5条
<Absolute Path>/scripts/xlsx_viewer.exe --path data.xlsx --search-col 2 "测试" --mode exact --limit 5

# 在第1行搜索"error",正则匹配,最多20条
<Absolute Path>/scripts/xlsx_viewer.exe --path data.xlsx --search-row 1 "error" --mode regex --limit 20
```
