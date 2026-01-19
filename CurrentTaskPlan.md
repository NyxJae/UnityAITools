# Go版本XLSX查看器 - 开发计划书

## 项目概述

**目标**: 将现有的JavaScript版本XLSX查看器转换为Go语言版本,保持功能一致,提供单文件可执行程序,支持跨平台部署。

**参考实现**: `E:/Project/UnityAITools/xlsx_viewer/xlsx_viewer.js` (已验证功能正常)

**目标文件**: `E:/Project/UnityAITools/xlsx_viewer/xlsx_viewer.go`

**技术栈**: Go 1.16+ + github.com/xuri/excelize/v2

---

## 一、项目架构设计

### 1.1 单文件架构

采用单文件 `xlsx_viewer.go`,通过包内函数组织代码:

```
xlsx_viewer.go
├── 包导入和常量定义
│   ├── 退出码常量
│   └── 默认配置常量
├── 数据结构定义
│   ├── 命令行参数结构体
│   └── 响应结果结构体
├── 工具函数区
│   ├── showHelp(): 显示帮助信息
│   └── errorExit(): 统一错误处理
├── 命令行参数解析
│   └── parseArgs(): 参数解析和验证
├── XLSX文件处理
│   ├── readExcelFile(): 读取Excel文件
│   └── getSheetSize(): 获取行列数
├── 数据查询功能
│   ├── getRowsData(): 获取行范围数据
│   ├── getColsData(): 获取列范围数据
│   ├── searchColumn(): 列搜索
│   └── searchRow(): 行搜索
└── 主程序入口
    └── main(): 主流程控制
```

### 1.2 设计原则

- **模块化**: 每个函数职责单一,高内聚低耦合
- **可维护**: 代码清晰,注释完整
- **单文件**: 所有代码在一个.go文件中,便于编译和分发
- **跨平台**: 使用标准库和稳定的第三方库,支持Windows/Linux/macOS

---

## 二、核心模块详细设计

### 2.1 包导入和常量定义

```go
package main

import (
	"encoding/json"
	"fmt"
	"os"
	"regexp"
	"strconv"
	"strings"

	"github.com/xuri/excelize/v2"
)

// 退出码定义
const (
	ExitSuccess    = 0 // 成功
	ExitGeneralErr = 1 // 一般错误
	ExitParamErr   = 2 // 参数错误
)

// 默认配置
const (
	DefaultMaxCols      = 50
	DefaultMaxRows      = 50
	DefaultSearchMode   = "fuzzy"
	DefaultSearchLimit  = 10
	DefaultRowsStart    = 1
	DefaultRowsEnd      = 3
	DefaultColsStart    = 1
	DefaultColsEnd      = 3
)
```

### 2.2 数据结构定义

```go
// 命令行参数结构体
type CommandLineArgs struct {
	Path      string              // 文件路径
	Operation string              // 操作类型: size, rows, cols, search-col, search-row
	Params    map[string]interface{} // 操作参数
}

// 行列范围结构体
type Range struct {
	Start int `json:"start"`
	End   int `json:"end"`
}

// 行数据结果
type RowsResult struct {
	RowRange Range        `json:"rowRange"`
	MaxCols  int          `json:"maxCols"`
	Data     [][]interface{} `json:"data"`
	Warning  string       `json:"warning,omitempty"`
}

// 列数据结构
type ColumnData struct {
	ColIndex int           `json:"colIndex"`
	Values   []interface{} `json:"values"`
}

// 列数据结果
type ColsResult struct {
	ColRange Range        `json:"colRange"`
	MaxRows  int          `json:"maxRows"`
	Data     []ColumnData `json:"data"`
	Warning  string       `json:"warning,omitempty"`
}

// 搜索参数
type SearchParams struct {
	ColIndex int    `json:"colIndex,omitempty"`
	RowIndex int    `json:"rowIndex,omitempty"`
	Keyword  string `json:"keyword"`
	Mode     string `json:"mode"`
	Limit    int    `json:"limit"`
}

// 搜索结果
type SearchResult struct {
	Matches int              `json:"matches"`
	Data    []interface{}    `json:"data"` // 可能是[][]interface{}或[]map[string]interface{}
	Warning string           `json:"warning,omitempty"`
}

// 行搜索单项结果
type RowSearchItem struct {
	ColIndex int         `json:"colIndex"`
	Value    interface{} `json:"value"`
}

// 统一响应结构
type Response struct {
	Success      bool          `json:"success"`
	Operation    string        `json:"operation"`
	FilePath     string        `json:"filePath"`
	SearchParams *SearchParams `json:"searchParams,omitempty"`
	Result       interface{}   `json:"result"`
}
```

### 2.3 工具函数区

#### 2.3.1 showHelp()

```go
func showHelp() {
	helpText := `
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
`
	fmt.Println(helpText)
	os.Exit(ExitSuccess)
}
```

#### 2.3.2 errorExit()

```go
func errorExit(message string, code int) {
	fmt.Fprintf(os.Stderr, "错误: %s\n", message)
	os.Exit(code)
}
```

### 2.4 命令行参数解析

#### 2.4.1 parseArgs()

```go
func parseArgs(args []string) *CommandLineArgs {
	result := &CommandLineArgs{
		Params: make(map[string]interface{}),
	}

	i := 0
	for i < len(args) {
		arg := args[i]

		switch arg {
		case "--help":
			showHelp()
		case "--path":
			if i+1 >= len(args) {
				errorExit("--path 需要指定文件路径", ExitParamErr)
			}
			result.Path = args[i+1]
			i++
		case "--size":
			if result.Operation != "" {
				errorExit("只能指定一个操作类型 (--size, --rows, --cols, --search-col, --search-row 之一)", ExitParamErr)
			}
			result.Operation = "size"
		case "--rows":
			if result.Operation != "" {
				errorExit("只能指定一个操作类型 (--size, --rows, --cols, --search-col, --search-row 之一)", ExitParamErr)
			}
			result.Operation = "rows"
			result.Params["rowStart"] = DefaultRowsStart
			result.Params["rowEnd"] = DefaultRowsEnd

			// 解析行范围参数
			if i+1 < len(args) && !strings.HasPrefix(args[i+1], "--") {
				num1, err := strconv.Atoi(args[i+1])
				if err == nil && num1 > 0 {
					result.Params["rowStart"] = 1
					result.Params["rowEnd"] = num1
					i++

					if i+1 < len(args) && !strings.HasPrefix(args[i+1], "--") {
						num2, err := strconv.Atoi(args[i+1])
						if err == nil && num2 > 0 {
							result.Params["rowStart"] = num1
							result.Params["rowEnd"] = num2
							i++
						}
					}
				}
			}
		case "--cols":
			if result.Operation != "" {
				errorExit("只能指定一个操作类型 (--size, --rows, --cols, --search-col, --search-row 之一)", ExitParamErr)
			}
			result.Operation = "cols"
			result.Params["colStart"] = DefaultColsStart
			result.Params["colEnd"] = DefaultColsEnd

			// 解析列范围参数
			if i+1 < len(args) && !strings.HasPrefix(args[i+1], "--") {
				num1, err := strconv.Atoi(args[i+1])
				if err == nil && num1 > 0 {
					result.Params["colStart"] = 1
					result.Params["colEnd"] = num1
					i++

					if i+1 < len(args) && !strings.HasPrefix(args[i+1], "--") {
						num2, err := strconv.Atoi(args[i+1])
						if err == nil && num2 > 0 {
							result.Params["colStart"] = num1
							result.Params["colEnd"] = num2
							i++
						}
					}
				}
			}
		case "--search-col":
			if result.Operation != "" {
				errorExit("只能指定一个操作类型 (--size, --rows, --cols, --search-col, --search-row 之一)", ExitParamErr)
			}
			result.Operation = "search-col"

			if i+1 >= len(args) {
				errorExit("--search-col 需要指定列索引", ExitParamErr)
			}
			colIndex, err := strconv.Atoi(args[i+1])
			if err != nil || colIndex < 1 {
				errorExit("--search-col 需要指定有效的列索引(大于0的整数)", ExitParamErr)
			}
			result.Params["colIndex"] = colIndex
			i++

			if i+1 >= len(args) || strings.HasPrefix(args[i+1], "--") {
				errorExit("--search-col 需要指定关键词", ExitParamErr)
			}
			result.Params["keyword"] = args[i+1]
			i++
		case "--search-row":
			if result.Operation != "" {
				errorExit("只能指定一个操作类型 (--size, --rows, --cols, --search-col, --search-row 之一)", ExitParamErr)
			}
			result.Operation = "search-row"

			if i+1 >= len(args) {
				errorExit("--search-row 需要指定行索引", ExitParamErr)
			}
			rowIndex, err := strconv.Atoi(args[i+1])
			if err != nil || rowIndex < 1 {
				errorExit("--search-row 需要指定有效的行索引(大于0的整数)", ExitParamErr)
			}
			result.Params["rowIndex"] = rowIndex
			i++

			if i+1 >= len(args) || strings.HasPrefix(args[i+1], "--") {
				errorExit("--search-row 需要指定关键词", ExitParamErr)
			}
			result.Params["keyword"] = args[i+1]
			i++
		case "--max-cols":
			if i+1 >= len(args) {
				errorExit("--max-cols 需要指定数值", ExitParamErr)
			}
			maxCols, err := strconv.Atoi(args[i+1])
			if err != nil || maxCols < 1 {
				errorExit("--max-cols 需要指定大于0的整数", ExitParamErr)
			}
			result.Params["maxCols"] = maxCols
			i++
		case "--max-rows":
			if i+1 >= len(args) {
				errorExit("--max-rows 需要指定数值", ExitParamErr)
			}
			maxRows, err := strconv.Atoi(args[i+1])
			if err != nil || maxRows < 1 {
				errorExit("--max-rows 需要指定大于0的整数", ExitParamErr)
			}
			result.Params["maxRows"] = maxRows
			i++
		case "--mode":
			if i+1 >= len(args) {
				errorExit("--mode 需要指定模式(fuzzy/exact/regex)", ExitParamErr)
			}
			mode := args[i+1]
			if mode != "fuzzy" && mode != "exact" && mode != "regex" {
				errorExit("--mode 只能是 fuzzy、exact 或 regex 之一", ExitParamErr)
			}
			result.Params["mode"] = mode
			i++
		case "--limit":
			if i+1 >= len(args) {
				errorExit("--limit 需要指定数量", ExitParamErr)
			}
			limit, err := strconv.Atoi(args[i+1])
			if err != nil || limit < 1 {
				errorExit("--limit 需要指定大于0的整数", ExitParamErr)
			}
			result.Params["limit"] = limit
			i++
		default:
			errorExit(fmt.Sprintf("未知参数: %s", arg), ExitParamErr)
		}

		i++
	}

	// 设置默认值
	if result.Operation == "rows" && result.Params["maxCols"] == nil {
		result.Params["maxCols"] = DefaultMaxCols
	}
	if result.Operation == "cols" && result.Params["maxRows"] == nil {
		result.Params["maxRows"] = DefaultMaxRows
	}
	if result.Operation == "search-col" || result.Operation == "search-row" {
		if result.Params["mode"] == nil {
			result.Params["mode"] = DefaultSearchMode
		}
		if result.Params["limit"] == nil {
			result.Params["limit"] = DefaultSearchLimit
		}
	}

	return result
}
```

### 2.5 XLSX文件处理

#### 2.5.1 readExcelFile()

```go
func readExcelFile(filePath string) (*excelize.File, error) {
	// 检查文件是否存在
	if _, err := os.Stat(filePath); os.IsNotExist(err) {
		return nil, fmt.Errorf("文件不存在: %s", filePath)
	}

	// 打开Excel文件
	f, err := excelize.OpenFile(filePath)
	if err != nil {
		return nil, fmt.Errorf("读取 Excel 文件失败: %v", err)
	}

	return f, nil
}

// 获取sheet数据为二维数组
func getSheetData(f *excelize.File) ([][]interface{}, error) {
	// 获取第一个sheet名称
	sheets := f.GetSheetList()
	if len(sheets) == 0 {
		return [][]interface{}{}, nil
	}
	sheetName := sheets[0]

	// 获取所有行
	rows, err := f.GetRows(sheetName)
	if err != nil {
		return nil, err
	}

	// 转换为interface{}类型
	var result [][]interface{}
	for _, row := range rows {
		var rowInterface []interface{}
		for _, cell := range row {
			rowInterface = append(rowInterface, cell)
		}
		result = append(result, rowInterface)
	}

	// 过滤空行
	var filtered [][]interface{}
	for _, row := range result {
		if len(row) > 0 {
			filtered = append(filtered, row)
		}
	}

	return filtered, nil
}
```

#### 2.5.2 getSheetSize()

```go
func getSheetSize(sheetData [][]interface{}) (int, int) {
	rows := len(sheetData)
	cols := 0
	if rows > 0 {
		cols = len(sheetData[0])
	}
	return rows, cols
}
```

### 2.6 数据查询功能

#### 2.6.1 getRowsData()

```go
func getRowsData(sheetData [][]interface{}, startRow, endRow, maxCols int) *RowsResult {
	result := &RowsResult{
		RowRange: Range{Start: startRow, End: endRow},
		MaxCols:  maxCols,
		Data:     [][]interface{}{},
	}

	totalRows := len(sheetData)

	// 调整行范围
	if startRow > totalRows {
		result.Warning = fmt.Sprintf("请求行范围 %d-%d 超出范围，文件只有 %d 行", startRow, endRow, totalRows)
		return result
	}

	if endRow > totalRows {
		result.Warning = fmt.Sprintf("请求 %d 行，但文件只有 %d 行", endRow, totalRows)
		endRow = totalRows
	}

	// 转换为0-based索引
	startIdx := startRow - 1
	endIdx := endRow - 1

	for i := startIdx; i <= endIdx && i < totalRows; i++ {
		row := sheetData[i]
		if maxCols < len(row) {
			row = row[:maxCols]
		}
		result.Data = append(result.Data, row)
	}

	return result
}
```

#### 2.6.2 getColsData()

```go
func getColsData(sheetData [][]interface{}, startCol, endCol, maxRows int) *ColsResult {
	result := &ColsResult{
		ColRange: Range{Start: startCol, End: endCol},
		MaxRows:  maxRows,
		Data:     []ColumnData{},
	}

	totalCols := 0
	if len(sheetData) > 0 {
		totalCols = len(sheetData[0])
	}

	// 调整列范围
	if startCol > totalCols {
		result.Warning = fmt.Sprintf("请求列范围 %d-%d 超出范围，文件只有 %d 列", startCol, endCol, totalCols)
		return result
	}

	if endCol > totalCols {
		result.Warning = fmt.Sprintf("请求 %d 列，但文件只有 %d 列", endCol, totalCols)
		endCol = totalCols
	}

	// 转换为0-based索引
	startIdx := startCol - 1
	endIdx := endCol - 1

	for colIdx := startIdx; colIdx <= endIdx; colIdx++ {
		colData := ColumnData{
			ColIndex: colIdx + 1,
			Values:   []interface{}{},
		}

		totalRows := len(sheetData)
		rowsToProcess := totalRows
		if maxRows < totalRows {
			rowsToProcess = maxRows
		}

		for rowIdx := 0; rowIdx < rowsToProcess; rowIdx++ {
			row := sheetData[rowIdx]
			var value interface{}
			if colIdx < len(row) {
				value = row[colIdx]
			} else {
				value = nil
			}
			colData.Values = append(colData.Values, value)
		}

		result.Data = append(result.Data, colData)
	}

	return result
}
```

#### 2.6.3 searchColumn()

```go
func searchColumn(sheetData [][]interface{}, colIndex int, keyword, mode string, limit int) *SearchResult {
	result := &SearchResult{
		Data: []interface{}{},
	}

	totalCols := 0
	if len(sheetData) > 0 {
		totalCols = len(sheetData[0])
	}

	// 检查列索引是否超出范围
	if colIndex > totalCols {
		result.Warning = fmt.Sprintf("列索引 %d 超出范围，文件只有 %d 列", colIndex, totalCols)
		return result
	}

	// 转换为0-based索引
	colIdx := colIndex - 1
	var regex *regexp.Regexp
	var err error

	// 准备正则表达式
	if mode == "regex" {
		regex, err = regexp.Compile(keyword)
		if err != nil {
			errorExit(fmt.Sprintf("正则表达式语法错误: %v", err), ExitGeneralErr)
		}
	}

	// 遍历所有行进行搜索
	for i := 0; i < len(sheetData); i++ {
		row := sheetData[i]

		var cellValue interface{}
		if colIdx < len(row) {
			cellValue = row[colIdx]
		}

		cellStr := strings.ToLower(fmt.Sprintf("%v", cellValue))
		keywordLower := strings.ToLower(keyword)

		matched := false
		switch mode {
		case "fuzzy":
			matched = strings.Contains(cellStr, keywordLower)
		case "exact":
			matched = cellStr == keywordLower
		case "regex":
			matched = regex.MatchString(fmt.Sprintf("%v", cellValue))
		}

		if matched {
			result.Data = append(result.Data, row)
			result.Matches++

			if result.Matches >= limit {
				break
			}
		}
	}

	return result
}
```

#### 2.6.4 searchRow()

```go
func searchRow(sheetData [][]interface{}, rowIndex int, keyword, mode string, limit int) *SearchResult {
	result := &SearchResult{
		Data: []interface{}{},
	}

	totalRows := len(sheetData)

	// 检查行索引是否超出范围
	if rowIndex > totalRows {
		result.Warning = fmt.Sprintf("行索引 %d 超出范围，文件只有 %d 行", rowIndex, totalRows)
		return result
	}

	// 转换为0-based索引
	rowIdx := rowIndex - 1
	row := sheetData[rowIdx]

	var regex *regexp.Regexp
	var err error

	// 准备正则表达式
	if mode == "regex" {
		regex, err = regexp.Compile(keyword)
		if err != nil {
			errorExit(fmt.Sprintf("正则表达式语法错误: %v", err), ExitGeneralErr)
		}
	}

	// 遍历所有列进行搜索
	for i := 0; i < len(row); i++ {
		cellValue := row[i]
		cellStr := strings.ToLower(fmt.Sprintf("%v", cellValue))
		keywordLower := strings.ToLower(keyword)

		matched := false
		switch mode {
		case "fuzzy":
			matched = strings.Contains(cellStr, keywordLower)
		case "exact":
			matched = cellStr == keywordLower
		case "regex":
			matched = regex.MatchString(fmt.Sprintf("%v", cellValue))
		}

		if matched {
			result.Data = append(result.Data, RowSearchItem{
				ColIndex: i + 1,
				Value:    cellValue,
			})
			result.Matches++

			if result.Matches >= limit {
				break
			}
		}
	}

	return result
}
```

### 2.7 主程序入口

```go
func main() {
	// 解析命令行参数
	args := os.Args[1:]
	parsed := parseArgs(args)

	// 验证必填参数
	if parsed.Path == "" {
		errorExit("必须指定文件路径 (--path 参数)", ExitParamErr)
	}

	if parsed.Operation == "" {
		errorExit("必须指定操作类型 (--size, --rows, --cols, --search-col, --search-row 之一)", ExitParamErr)
	}

	// 读取Excel文件
	f, err := readExcelFile(parsed.Path)
	if err != nil {
		errorExit(err.Error(), ExitGeneralErr)
	}
	defer f.Close()

	// 获取sheet数据
	sheetData, err := getSheetData(f)
	if err != nil {
		errorExit(err.Error(), ExitGeneralErr)
	}

	// 构建响应对象
	response := Response{
		Success:   true,
		Operation: parsed.Operation,
		FilePath:  parsed.Path,
	}

	// 根据操作类型执行相应功能
	switch parsed.Operation {
	case "size":
		rows, cols := getSheetSize(sheetData)
		response.Result = map[string]int{
			"rows": rows,
			"cols": cols,
		}

	case "rows":
		rowStart := parsed.Params["rowStart"].(int)
		rowEnd := parsed.Params["rowEnd"].(int)
		maxCols := parsed.Params["maxCols"].(int)
		response.Result = getRowsData(sheetData, rowStart, rowEnd, maxCols)

	case "cols":
		colStart := parsed.Params["colStart"].(int)
		colEnd := parsed.Params["colEnd"].(int)
		maxRows := parsed.Params["maxRows"].(int)
		response.Result = getColsData(sheetData, colStart, colEnd, maxRows)

	case "search-col":
		colIndex := parsed.Params["colIndex"].(int)
		keyword := parsed.Params["keyword"].(string)
		mode := parsed.Params["mode"].(string)
		limit := parsed.Params["limit"].(int)
		response.SearchParams = &SearchParams{
			ColIndex: colIndex,
			Keyword:  keyword,
			Mode:     mode,
			Limit:    limit,
		}
		response.Result = searchColumn(sheetData, colIndex, keyword, mode, limit)

	case "search-row":
		rowIndex := parsed.Params["rowIndex"].(int)
		keyword := parsed.Params["keyword"].(string)
		mode := parsed.Params["mode"].(string)
		limit := parsed.Params["limit"].(int)
		response.SearchParams = &SearchParams{
			RowIndex: rowIndex,
			Keyword:  keyword,
			Mode:     mode,
			Limit:    limit,
		}
		response.Result = searchRow(sheetData, rowIndex, keyword, mode, limit)

	default:
		errorExit("未知的操作类型", ExitParamErr)
	}

	// 输出JSON格式结果
	jsonData, err := json.MarshalIndent(response, "", "  ")
	if err != nil {
		errorExit(fmt.Sprintf("JSON序列化失败: %v", err), ExitGeneralErr)
	}

	fmt.Println(string(jsonData))
	os.Exit(ExitSuccess)
}
```

---

## 三、实现步骤和顺序

### 阶段 1: 项目初始化和环境配置 (TODO 1.1-1.4)

1. **创建Go模块目录结构**
   - 在 `E:/Project/UnityAITools/xlsx_viewer/` 目录下
   - 创建 `xlsx_viewer.go` 文件

2. **初始化go.mod文件**
   ```bash
   cd E:/Project/UnityAITools/xlsx_viewer
   go mod init xlsx_viewer
   ```

3. **安装excelize/v2依赖库**
   ```bash
   go get github.com/xuri/excelize/v2
   go mod tidy
   ```

4. **创建main.go主程序框架**
   - 实现基础的package main和空main函数
   - 添加必要的import语句

### 阶段 2: 核心数据结构设计 (TODO 2.1-2.4)

1. **定义退出码常量结构**
   - ExitSuccess = 0
   - ExitGeneralErr = 1
   - ExitParamErr = 2

2. **定义默认配置常量**
   - DefaultMaxCols = 50
   - DefaultMaxRows = 50
   - DefaultSearchMode = "fuzzy"
   - DefaultSearchLimit = 10

3. **定义命令行参数结构体**
   - CommandLineArgs 结构体
   - 包含 Path, Operation, Params 字段

4. **定义响应结果结构体**
   - Response, Range, RowsResult, ColsResult
   - SearchParams, SearchResult, RowSearchItem

### 阶段 3: 命令行参数解析模块 (TODO 3.1-3.4)

1. **实现showHelp帮助信息函数**
   - 输出完整的帮助文本
   - 包含所有参数说明和示例

2. **实现errorExit错误处理函数**
   - 输出错误信息到stderr
   - 使用指定的退出码退出

3. **实现parseArgs参数解析函数**
   - 解析所有命令行参数
   - 处理参数验证和默认值设置

4. **实现参数验证逻辑**
   - 验证必填参数
   - 验证参数值范围

### 阶段 4: XLSX文件读取模块 (TODO 4.1-4.3)

1. **实现readExcelFile文件读取函数**
   - 检查文件存在性
   - 使用excelize打开文件

2. **实现getSheetSize行列数统计函数**
   - 返回实际使用的行数和列数

3. **实现数据过滤空行逻辑**
   - 过滤掉空行
   - 返回过滤后的二维数组

### 阶段 5: 数据查询功能模块 (TODO 5.1-5.5)

1. **实现getRowsData获取行范围数据**
   - 处理行范围调整
   - 限制每行列数

2. **实现getColsData获取列范围数据**
   - 处理列范围调整
   - 限制每列行数

3. **实现searchColumn列搜索功能**
   - 支持三种搜索模式
   - 返回匹配的完整行

4. **实现searchRow行搜索功能**
   - 支持三种搜索模式
   - 返回匹配的列索引和值

5. **实现三种搜索模式(fuzzy/exact/regex)**
   - fuzzy: 模糊匹配(包含)
   - exact: 精确匹配
   - regex: 正则表达式匹配

### 阶段 6: 输出和错误处理模块 (TODO 6.1-6.3)

1. **实现JSON格式美化输出**
   - 使用json.MarshalIndent
   - 2空格缩进

2. **实现错误信息输出到stderr**
   - 统一使用errorExit函数
   - 错误信息输出到stderr

3. **实现主程序流程控制**
   - 参数解析 → 文件读取 → 数据处理 → 结果输出
   - 完整的错误处理

### 阶段 7: 跨平台编译和测试 (TODO 7.1-7.4)

1. **编译Windows版本测试**
   ```bash
   go build -o xlsx_viewer.exe
   ./xlsx_viewer.exe --help
   ./xlsx_viewer.exe --path "F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx" --size
   ```

2. **编译Linux版本测试**
   ```bash
   GOOS=linux GOARCH=amd64 go build -o xlsx_viewer_linux
   # 在Linux环境测试
   ```

3. **编译macOS版本测试**
   ```bash
   GOOS=darwin GOARCH=amd64 go build -o xlsx_viewer_macos
   # 在macOS环境测试
   ```

4. **完整功能测试验证**
   - 测试所有操作类型
   - 测试边缘情况
   - 与JS版本对比结果

### 阶段 8: 文档和部署 (TODO 8.1-8.3)

1. **编写README使用文档**
   - 项目简介
   - 安装说明
   - 使用示例
   - 参数说明

2. **编写编译和部署说明**
   - 编译命令
   - 依赖说明
   - 跨平台编译

3. **清理临时文件并提交代码**
   - 清理编译产物
   - 提交到版本控制

---

## 四、重要注意事项

### 4.1 性能优化

- **文件读取**: 使用excelize.GetRows一次性读取所有数据,避免多次IO
- **内存管理**: 大文件时注意内存使用,必要时可以分批处理
- **搜索优化**: 达到limit数量后立即停止搜索

### 4.2 边缘情况处理

- **空文件**: 返回空的行列数
- **索引超出**: 返回警告信息,不报错
- **正则错误**: 捕获regexp.Compile错误并提示用户
- **类型转换**: 使用fmt.Sprintf安全转换为字符串

### 4.3 错误处理

- **文件不存在**: 返回退出码1
- **参数错误**: 返回退出码2
- **一般错误**: 返回退出码1
- **所有错误信息输出到stderr,stdout保持为空**

### 4.4 代码质量

- **注释完整**: 每个函数都有清晰的注释
- **命名规范**: 使用Go命名规范,语义清晰
- **错误处理**: 每个关键步骤都有错误处理
- **类型安全**: 使用interface{}处理Excel中的混合类型

### 4.5 跨平台兼容性

- **路径处理**: 使用filepath包处理路径分隔符
- **编码问题**: 确保使用UTF-8编码
- **行结束符**: excelize库自动处理不同平台的行结束符

---

## 五、测试计划

### 5.1 单元测试

1. 测试 --size 功能
2. 测试 --rows 功能
3. 测试 --cols 功能
4. 测试 --search-col 功能
5. 测试 --search-row 功能
6. 测试错误处理

### 5.2 集成测试

使用 `F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/` 目录下的xlsx文件进行测试:

```bash
# 测试 --size
./xlsx_viewer --path "F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx" --size

# 测试 --rows
./xlsx_viewer --path "F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx" --rows

# 测试 --cols
./xlsx_viewer --path "F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx" --cols

# 测试 --search-col
./xlsx_viewer --path "F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx" --search-col 3 "天魔" --limit 3

# 测试 --search-row
./xlsx_viewer --path "F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx" --search-row 1 "BUFF"

# 测试错误处理
./xlsx_viewer --path "F:/notexist.xlsx" --size
```

### 5.3 对比测试

与JS版本对比输出结果,确保功能一致:

```bash
# JS版本
node xlsx_viewer.js --path "F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx" --size

# Go版本
./xlsx_viewer --path "F:/UnityProject/RXJH/RXJH_307_mini/Client/数据库/mydb_buff_tbl.xlsx" --size
```

---

## 六、文档计划

### 6.1 README.md

包含以下内容:

1. 项目简介
2. 功能特性
3. 安装说明
4. 使用示例
5. 参数说明
6. 输出格式说明
7. 常见问题
8. 编译说明

### 6.2 编译说明

```bash
# 本地编译
go build -o xlsx_viewer

# Windows交叉编译
GOOS=windows GOARCH=amd64 go build -o xlsx_viewer.exe

# Linux交叉编译
GOOS=linux GOARCH=amd64 go build -o xlsx_viewer_linux

# macOS交叉编译
GOOS=darwin GOARCH=amd64 go build -o xlsx_viewer_macos

# 静态编译(可选)
CGO_ENABLED=0 GOOS=linux GOARCH=amd64 go build -ldflags="-s -w" -o xlsx_viewer_linux
```

---

## 七、总结

本计划书详细描述了Go版本XLSX查看器的实现方案,包括:

1. **文件结构**: 单文件架构,模块化设计
2. **核心模块**: 命令行解析、Excel读取、数据查询、搜索功能
3. **实现步骤**: 分8个阶段,34个子任务,逐步实现功能
4. **注意事项**: 性能优化、边缘情况处理、错误处理、代码质量、跨平台兼容性
5. **测试计划**: 单元测试、集成测试、对比测试
6. **文档计划**: README和编译说明

通过本计划,团队成员可以清晰地了解整个项目的实现方案,按照步骤有序地完成开发任务。
