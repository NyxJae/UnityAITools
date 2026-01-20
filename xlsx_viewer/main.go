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

// Exit codes
const (
	SUCCESS       = 0
	GENERAL_ERROR = 1
	PARAM_ERROR   = 2
)

// Default configurations
const (
	MAX_COLS           = 50
	MAX_ROWS           = 50
	SEARCH_MODE        = "fuzzy"
	SEARCH_LIMIT       = 10
	DEFAULT_ROWS_START = 1
	DEFAULT_ROWS_END   = 3
	DEFAULT_COLS_START = 1
	DEFAULT_COLS_END   = 3
)

// Args represents command line arguments
type Args struct {
	Path      string
	Operation string
	Params    map[string]interface{}
}

// Response represents the result structure
type Response struct {
	Success      bool          `json:"success"`
	Operation    string        `json:"operation"`
	FilePath     string        `json:"filePath"`
	SearchParams *SearchParams `json:"searchParams,omitempty"`
	Result       interface{}   `json:"result"`
}

// SearchParams represents search parameters
type SearchParams struct {
	ColIndex int    `json:"colIndex,omitempty"`
	RowIndex int    `json:"rowIndex,omitempty"`
	Keyword  string `json:"keyword"`
	Mode     string `json:"mode"`
	Limit    int    `json:"limit"`
}

// Range represents a row or column range
type Range struct {
	Start int `json:"start"`
	End   int `json:"end"`
}

// RowsResult represents rows query result
type RowsResult struct {
	RowRange Range          `json:"rowRange"`
	MaxCols  int            `json:"maxCols"`
	Data     [][]interface{} `json:"data"`
	Warning  string         `json:"warning,omitempty"`
}

// ColumnData represents a single column data
type ColumnData struct {
	ColIndex int           `json:"colIndex"`
	Values   []interface{} `json:"values"`
}

// ColsResult represents columns query result
type ColsResult struct {
	ColRange Range       `json:"colRange"`
	MaxRows  int         `json:"maxRows"`
	Data     []ColumnData `json:"data"`
	Warning  string      `json:"warning,omitempty"`
}

// SearchResult represents search result
type SearchResult struct {
	Matches int           `json:"matches"`
	Data    []interface{} `json:"data"` // Can be [][]interface{} or []RowSearchItem
	Warning string        `json:"warning,omitempty"`
}

// RowSearchItem represents a row search item
type RowSearchItem struct {
	ColIndex int         `json:"colIndex"`
	Value    interface{} `json:"value"`
}
// showHelp displays help information
func showHelp() {
	fmt.Println(`
XLSX 查看器 - 便捷查看和搜索 Excel 文件

用法:
  ./xlsx_viewer --path <xlsx文件路径> <操作类型> [参数]

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
  ./xlsx_viewer --path data.xlsx --size
  ./xlsx_viewer --path data.xlsx --rows 1 5 --max-cols 20
  ./xlsx_viewer --path data.xlsx --rows 10
  ./xlsx_viewer --path data.xlsx --cols 1 3 --max-rows 100
  ./xlsx_viewer --path data.xlsx --search-col 2 "测试" --mode exact --limit 5
  ./xlsx_viewer --path data.xlsx --search-row 1 "error" --mode regex --limit 20
`)
	os.Exit(SUCCESS)
}

// errorExit outputs error message and exits with given code
func errorExit(message string, code int) {
	fmt.Fprintf(os.Stderr, "错误: %s\n", message)
	os.Exit(code)
}

// parseArgs parses command line arguments
func parseArgs(args []string) *Args {
	result := &Args{
		Path:      "",
		Operation: "",
		Params:    make(map[string]interface{}),
	}

	i := 0
	for i < len(args) {
		arg := args[i]

		if arg == "--help" || arg == "-h" {
			showHelp()
		} else if arg == "--path" {
			if i+1 >= len(args) {
				errorExit("--path 需要指定文件路径", PARAM_ERROR)
			}
			result.Path = args[i+1]
			i++
		} else if arg == "--size" {
			if result.Operation != "" {
				errorExit("只能指定一个操作类型 (--size, --rows, --cols, --search-col, --search-row 之一)", PARAM_ERROR)
			}
			result.Operation = "size"
		} else if arg == "--rows" {
			if result.Operation != "" {
				errorExit("只能指定一个操作类型 (--size, --rows, --cols, --search-col, --search-row 之一)", PARAM_ERROR)
			}
			result.Operation = "rows"
			result.Params["rowStart"] = DEFAULT_ROWS_START
			result.Params["rowEnd"] = DEFAULT_ROWS_END

			// Parse row range parameters
			if i+1 < len(args) && !strings.HasPrefix(args[i+1], "--") {
				num1, err := strconv.Atoi(args[i+1])
				if err == nil && num1 > 0 {
					result.Params["rowStart"] = 1
					result.Params["rowEnd"] = num1
					i++

					if i+1 < len(args) && !strings.HasPrefix(args[i+1], "--") {
						num2, err2 := strconv.Atoi(args[i+1])
						if err2 == nil && num2 > 0 {
							result.Params["rowStart"] = num1
							result.Params["rowEnd"] = num2
							i++
						}
					}
				}
			}
		} else if arg == "--cols" {
			if result.Operation != "" {
				errorExit("只能指定一个操作类型 (--size, --rows, --cols, --search-col, --search-row 之一)", PARAM_ERROR)
			}
			result.Operation = "cols"
			result.Params["colStart"] = DEFAULT_COLS_START
			result.Params["colEnd"] = DEFAULT_COLS_END

			// Parse column range parameters
			if i+1 < len(args) && !strings.HasPrefix(args[i+1], "--") {
				num1, err := strconv.Atoi(args[i+1])
				if err == nil && num1 > 0 {
					result.Params["colStart"] = 1
					result.Params["colEnd"] = num1
					i++

					if i+1 < len(args) && !strings.HasPrefix(args[i+1], "--") {
						num2, err2 := strconv.Atoi(args[i+1])
						if err2 == nil && num2 > 0 {
							result.Params["colStart"] = num1
							result.Params["colEnd"] = num2
							i++
						}
					}
				}
			}
		} else if arg == "--search-col" {
			if result.Operation != "" {
				errorExit("只能指定一个操作类型 (--size, --rows, --cols, --search-col, --search-row 之一)", PARAM_ERROR)
			}
			result.Operation = "search-col"

			if i+1 >= len(args) {
				errorExit("--search-col 需要指定列索引", PARAM_ERROR)
			}
			colIndex, err := strconv.Atoi(args[i+1])
			if err != nil || colIndex < 1 {
				errorExit("--search-col 需要指定有效的列索引(大于0的整数)", PARAM_ERROR)
			}
			result.Params["colIndex"] = colIndex
			i++

			if i+1 >= len(args) || strings.HasPrefix(args[i+1], "--") {
				errorExit("--search-col 需要指定关键词", PARAM_ERROR)
			}
			result.Params["keyword"] = args[i+1]
			i++
		} else if arg == "--search-row" {
			if result.Operation != "" {
				errorExit("只能指定一个操作类型 (--size, --rows, --cols, --search-col, --search-row 之一)", PARAM_ERROR)
			}
			result.Operation = "search-row"

			if i+1 >= len(args) {
				errorExit("--search-row 需要指定行索引", PARAM_ERROR)
			}
			rowIndex, err := strconv.Atoi(args[i+1])
			if err != nil || rowIndex < 1 {
				errorExit("--search-row 需要指定有效的行索引(大于0的整数)", PARAM_ERROR)
			}
			result.Params["rowIndex"] = rowIndex
			i++

			if i+1 >= len(args) || strings.HasPrefix(args[i+1], "--") {
				errorExit("--search-row 需要指定关键词", PARAM_ERROR)
			}
			result.Params["keyword"] = args[i+1]
			i++
		} else if arg == "--max-cols" {
			if i+1 >= len(args) {
				errorExit("--max-cols 需要指定数值", PARAM_ERROR)
			}
			maxCols, err := strconv.Atoi(args[i+1])
			if err != nil || maxCols < 1 {
				errorExit("--max-cols 需要指定大于0的整数", PARAM_ERROR)
			}
			result.Params["maxCols"] = maxCols
			i++
		} else if arg == "--max-rows" {
			if i+1 >= len(args) {
				errorExit("--max-rows 需要指定数值", PARAM_ERROR)
			}
			maxRows, err := strconv.Atoi(args[i+1])
			if err != nil || maxRows < 1 {
				errorExit("--max-rows 需要指定大于0的整数", PARAM_ERROR)
			}
			result.Params["maxRows"] = maxRows
			i++
		} else if arg == "--mode" {
			if i+1 >= len(args) {
				errorExit("--mode 需要指定模式(fuzzy/exact/regex)", PARAM_ERROR)
			}
			mode := args[i+1]
			if mode != "fuzzy" && mode != "exact" && mode != "regex" {
				errorExit("--mode 只能是 fuzzy、exact 或 regex 之一", PARAM_ERROR)
			}
			result.Params["mode"] = mode
			i++
		} else if arg == "--limit" {
			if i+1 >= len(args) {
				errorExit("--limit 需要指定数量", PARAM_ERROR)
			}
			limit, err := strconv.Atoi(args[i+1])
			if err != nil || limit < 1 {
				errorExit("--limit 需要指定大于0的整数", PARAM_ERROR)
			}
			result.Params["limit"] = limit
			i++
		} else {
			errorExit("未知参数: "+arg, PARAM_ERROR)
		}

		i++
	}

	// Set default values
	if result.Operation == "rows" {
		if _, ok := result.Params["maxCols"]; !ok {
			result.Params["maxCols"] = MAX_COLS
		}
	}
	if result.Operation == "cols" {
		if _, ok := result.Params["maxRows"]; !ok {
			result.Params["maxRows"] = MAX_ROWS
		}
	}
	if result.Operation == "search-col" || result.Operation == "search-row" {
		if _, ok := result.Params["mode"]; !ok {
			result.Params["mode"] = SEARCH_MODE
		}
		if _, ok := result.Params["limit"]; !ok {
			result.Params["limit"] = SEARCH_LIMIT
		}
	}

	return result
}


// readExcelFile reads and validates an Excel file
func readExcelFile(filePath string) (*excelize.File, error) {
	// Check if file exists
	if _, err := os.Stat(filePath); os.IsNotExist(err) {
		return nil, fmt.Errorf("文件不存在: %s", filePath)
	}

	// Open Excel file
	f, err := excelize.OpenFile(filePath)
	if err != nil {
		return nil, fmt.Errorf("读取 Excel 文件失败: %v", err)
	}

	return f, nil
}

// getSheetData gets all data from the first sheet
func getSheetData(f *excelize.File) ([][]interface{}, error) {
	// Get first sheet name
	sheets := f.GetSheetList()
	if len(sheets) == 0 {
		return [][]interface{}{}, nil
	}
	sheetName := sheets[0]

	// Get all rows
	rows, err := f.GetRows(sheetName)
	if err != nil {
		return nil, err
	}

	// Convert to interface{} type
	var result [][]interface{}
	for _, row := range rows {
		var rowInterface []interface{}
		for _, cell := range row {
			rowInterface = append(rowInterface, cell)
		}
		result = append(result, rowInterface)
	}

	// Filter empty rows
	var filtered [][]interface{}
	for _, row := range result {
		if len(row) > 0 {
			filtered = append(filtered, row)
		}
	}

	return filtered, nil
}

// getSheetSize gets the actual used rows and columns of the sheet
func getSheetSize(sheetData [][]interface{}) (int, int) {
	rows := len(sheetData)
	cols := 0
	if rows > 0 {
		cols = len(sheetData[0])
	}
	return rows, cols
}

// getRowsData gets rows data in specified range
func getRowsData(sheetData [][]interface{}, startRow, endRow, maxCols int) *RowsResult {
	result := &RowsResult{
		RowRange: Range{Start: startRow, End: endRow},
		MaxCols:  maxCols,
		Data:     [][]interface{}{},
	}

	totalRows := len(sheetData)

	// Adjust row range
	if startRow > totalRows {
		result.Warning = fmt.Sprintf("请求行范围 %d-%d 超出范围，文件只有 %d 行", startRow, endRow, totalRows)
		return result
	}

	if endRow > totalRows {
		result.Warning = fmt.Sprintf("请求 %d 行，但文件只有 %d 行", endRow, totalRows)
		endRow = totalRows
	}

	// Convert to 0-based index
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

// getColsData gets columns data in specified range
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

	// Adjust column range
	if startCol > totalCols {
		result.Warning = fmt.Sprintf("请求列范围 %d-%d 超出范围，文件只有 %d 列", startCol, endCol, totalCols)
		return result
	}

	if endCol > totalCols {
		result.Warning = fmt.Sprintf("请求 %d 列，但文件只有 %d 列", endCol, totalCols)
		endCol = totalCols
	}

	// Convert to 0-based index
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

// searchColumn searches for keyword in specified column
func searchColumn(sheetData [][]interface{}, colIndex int, keyword, mode string, limit int) *SearchResult {
	result := &SearchResult{
		Data: []interface{}{},
	}

	totalCols := 0
	if len(sheetData) > 0 {
		totalCols = len(sheetData[0])
	}

	// Check if column index is out of range
	if colIndex > totalCols {
		result.Warning = fmt.Sprintf("列索引 %d 超出范围，文件只有 %d 列", colIndex, totalCols)
		return result
	}

	// Convert to 0-based index
	colIdx := colIndex - 1
	var regex *regexp.Regexp
	var err error

	// Prepare regex
	if mode == "regex" {
		regex, err = regexp.Compile(keyword)
		if err != nil {
			errorExit(fmt.Sprintf("正则表达式语法错误: %v", err), GENERAL_ERROR)
		}
	}

	// Iterate through all rows to search
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

// searchRow searches for keyword in specified row
func searchRow(sheetData [][]interface{}, rowIndex int, keyword, mode string, limit int) *SearchResult {
	result := &SearchResult{
		Data: []interface{}{},
	}

	totalRows := len(sheetData)

	// Check if row index is out of range
	if rowIndex > totalRows {
		result.Warning = fmt.Sprintf("行索引 %d 超出范围，文件只有 %d 行", rowIndex, totalRows)
		return result
	}

	// Convert to 0-based index
	rowIdx := rowIndex - 1
	row := sheetData[rowIdx]

	var regex *regexp.Regexp
	var err error

	// Prepare regex
	if mode == "regex" {
		regex, err = regexp.Compile(keyword)
		if err != nil {
			errorExit(fmt.Sprintf("正则表达式语法错误: %v", err), GENERAL_ERROR)
		}
	}

	// Iterate through all columns to search
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

func main() {
	// Parse command line arguments
	args := parseArgs(os.Args[1:])

	// Validate required parameters
	if args.Path == "" {
		errorExit("必须指定文件路径 (--path 参数)", PARAM_ERROR)
	}

	if args.Operation == "" {
		errorExit("必须指定操作类型 (--size, --rows, --cols, --search-col, --search-row 之一)", PARAM_ERROR)
	}

	// Read Excel file
	f, err := readExcelFile(args.Path)
	if err != nil {
		errorExit(err.Error(), GENERAL_ERROR)
	}
	defer f.Close()

	// Get sheet data
	sheetData, err := getSheetData(f)
	if err != nil {
		errorExit(err.Error(), GENERAL_ERROR)
	}

	// Build response object
	response := Response{
		Success:   true,
		Operation: args.Operation,
		FilePath:  args.Path,
	}

	// Execute corresponding function based on operation type
	switch args.Operation {
	case "size":
		rows, cols := getSheetSize(sheetData)
		response.Result = map[string]int{
			"rows": rows,
			"cols": cols,
		}

	case "rows":
		rowStart := args.Params["rowStart"].(int)
		rowEnd := args.Params["rowEnd"].(int)
		maxCols := args.Params["maxCols"].(int)
		response.Result = getRowsData(sheetData, rowStart, rowEnd, maxCols)

	case "cols":
		colStart := args.Params["colStart"].(int)
		colEnd := args.Params["colEnd"].(int)
		maxRows := args.Params["maxRows"].(int)
		response.Result = getColsData(sheetData, colStart, colEnd, maxRows)

	case "search-col":
		colIndex := args.Params["colIndex"].(int)
		keyword := args.Params["keyword"].(string)
		mode := args.Params["mode"].(string)
		limit := args.Params["limit"].(int)
		response.SearchParams = &SearchParams{
			ColIndex: colIndex,
			Keyword:  keyword,
			Mode:     mode,
			Limit:    limit,
		}
		response.Result = searchColumn(sheetData, colIndex, keyword, mode, limit)

	case "search-row":
		rowIndex := args.Params["rowIndex"].(int)
		keyword := args.Params["keyword"].(string)
		mode := args.Params["mode"].(string)
		limit := args.Params["limit"].(int)
		response.SearchParams = &SearchParams{
			RowIndex: rowIndex,
			Keyword:  keyword,
			Mode:     mode,
			Limit:    limit,
		}
		response.Result = searchRow(sheetData, rowIndex, keyword, mode, limit)

	default:
		errorExit("未知的操作类型", PARAM_ERROR)
	}

	// Output JSON format result
	jsonData, err := json.MarshalIndent(response, "", "  ")
	if err != nil {
		errorExit(fmt.Sprintf("JSON序列化失败: %v", err), GENERAL_ERROR)
	}

	// Compress data array: keep subarrays on separate lines, but compress content within each subarray
	// This only applies to 2D arrays ([[...], [...]]), not object arrays ([{...}, {...}])
	jsonStr := string(jsonData)
	
	// Find "data": [ and check if it's a 2D array
	dataPrefix := `"data": [`
	dataIdx := strings.Index(jsonStr, dataPrefix)
	if dataIdx != -1 {
		startIdx := dataIdx + len(dataPrefix)
		
		// Check if the first element is an array (2D array) or object (object array)
		// Skip whitespace to find the first non-space character
		firstCharIdx := startIdx
		for firstCharIdx < len(jsonStr) {
			c := jsonStr[firstCharIdx]
			if c != ' ' && c != '\n' && c != '\t' {
				break
			}
			firstCharIdx++
		}
		
		// Only compress if it's a 2D array (first element starts with '[')
		if firstCharIdx < len(jsonStr) && jsonStr[firstCharIdx] == '[' {
			// Find matching closing bracket for data array
			bracketCount := 0
			endIdx := startIdx
			for i := startIdx; i < len(jsonStr); i++ {
				if jsonStr[i] == '[' {
					bracketCount++
				} else if jsonStr[i] == ']' {
					if bracketCount == 0 {
						endIdx = i
						break
					}
					bracketCount--
				}
			}
			
			if endIdx > startIdx {
				// Parse and compress each subarray
				content := jsonStr[startIdx:endIdx]
				var compressedParts []string
				
				// Track brackets to identify subarrays
				depth := 0
				start := 0
				for i := 0; i < len(content); i++ {
					if content[i] == '[' {
						if depth == 0 {
							start = i
						}
						depth++
					} else if content[i] == ']' {
						depth--
						if depth == 0 {
							// Found a complete subarray, compress it
							subarray := content[start:i+1]
							// Remove newlines and tabs within this subarray
							subarray = strings.ReplaceAll(subarray, "\n", " ")
							subarray = strings.ReplaceAll(subarray, "\t", " ")
							subarray = regexp.MustCompile(`\s+`).ReplaceAllString(subarray, " ")
							// Trim and ensure proper spacing
							subarray = strings.TrimSpace(subarray)
							subarray = "[" + subarray[1:len(subarray)-1] + "]"
							compressedParts = append(compressedParts, subarray)
						}
					}
				}
				
				// Rebuild with newlines between subarrays
			newContent := "\n      " + strings.Join(compressedParts, ",\n      ")
			newContent += "\n    "
				
				// Rebuild JSON string
				jsonStr = jsonStr[:startIdx] + newContent + jsonStr[endIdx:]
			}
		}
	}

	fmt.Println(jsonStr)
	os.Exit(SUCCESS)
}