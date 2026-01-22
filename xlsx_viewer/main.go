package main

import (
	"errors"
	"fmt"
	"os"
	"path/filepath"
	"regexp"
	"strconv"
	"strings"

	"github.com/xuri/excelize/v2"
)

const (
	defaultMaxCols = 50
	defaultMaxRows = 50
	defaultLimit   = 10
)

type operation int

const (
	opNone operation = iota
	opSize
	opRows
	opCols
	opSearchCol
	opSearchRow
)

type options struct {
	path     string
	op       operation
	rowsRaw  string
	colsRaw  string
	maxCols  int
	maxRows  int
	mode     string
	limit    int
	searchIx string
	keyword  string
	showHelp bool
}

func main() {
	opts, err := parseArgs(os.Args[1:])
	if err != nil {
		exitWithUsageError(err.Error())
	}

	if opts.showHelp {
		printHelp()
		return
	}

	if opts.path == "" {
		exitWithUsageError("必须指定文件路径 (--path 参数)")
	}

	if opts.op == opNone {
		exitWithUsageError("必须指定操作类型 (--size, --rows, --cols, --search-col, --search-row 之一)")
	}

	if err := validatePath(opts.path); err != nil {
		exitWithError(err.Error())
	}

	file, sheetName, err := openWorkbook(opts.path)
	if err != nil {
		exitWithError(err.Error())
	}
	defer func() {
		_ = file.Close()
	}()

	rows, cols, err := sheetSize(file, sheetName)
	if err != nil {
		exitWithError(err.Error())
	}

	switch opts.op {
	case opSize:
		printSize(rows, cols)
	case opRows:
		handleRows(file, sheetName, rows, cols, opts)
	case opCols:
		handleCols(file, sheetName, rows, cols, opts)
	case opSearchCol:
		handleSearchColumn(file, sheetName, rows, cols, opts)
	case opSearchRow:
		handleSearchRow(file, sheetName, rows, cols, opts)
	default:
		exitWithUsageError("未知的操作类型")
	}
}

func parseArgs(args []string) (options, error) {
	opts := options{
		maxCols: defaultMaxCols,
		maxRows: defaultMaxRows,
		mode:    "fuzzy",
		limit:   defaultLimit,
	}

	if len(args) == 0 {
		return opts, nil
	}

	var i int
	for i < len(args) {
		arg := args[i]
		switch arg {
		case "--help":
			opts.showHelp = true
			return opts, nil
		case "--path":
			value, next, err := nextValue(args, i)
			if err != nil {
				return opts, err
			}
			opts.path = value
			i = next
		case "--size":
			if err := setOperation(&opts, opSize); err != nil {
				return opts, err
			}
			i++
		case "--rows":
			if err := setOperation(&opts, opRows); err != nil {
				return opts, err
			}
			value, next := readOptionalRange(args, i, true)
			opts.rowsRaw = value
			i = next
		case "--cols":
			if err := setOperation(&opts, opCols); err != nil {
				return opts, err
			}
			value, next := readOptionalRange(args, i, false)
			opts.colsRaw = value
			i = next
		case "--max-cols":
			value, next, err := nextValue(args, i)
			if err != nil {
				return opts, err
			}
			parsed, err := parsePositiveInt(value, "--max-cols")
			if err != nil {
				return opts, err
			}
			opts.maxCols = parsed
			i = next
		case "--max-rows":
			value, next, err := nextValue(args, i)
			if err != nil {
				return opts, err
			}
			parsed, err := parsePositiveInt(value, "--max-rows")
			if err != nil {
				return opts, err
			}
			opts.maxRows = parsed
			i = next
		case "--mode":
			value, next, err := nextValue(args, i)
			if err != nil {
				return opts, err
			}
			value = strings.ToLower(value)
			switch value {
			case "fuzzy", "exact", "regex":
				opts.mode = value
			default:
				return opts, fmt.Errorf("--mode 只能是 fuzzy, exact, regex")
			}
			i = next
		case "--limit":
			value, next, err := nextValue(args, i)
			if err != nil {
				return opts, err
			}
			parsed, err := parsePositiveInt(value, "--limit")
			if err != nil {
				return opts, err
			}
			opts.limit = parsed
			i = next
		case "--search-col":
			if err := setOperation(&opts, opSearchCol); err != nil {
				return opts, err
			}
			value, next, err := nextValue(args, i)
			if err != nil {
				return opts, err
			}
			keyword, next2, err := nextValue(args, next-1)
			if err != nil {
				return opts, errors.New("--search-col 需要指定关键词")
			}
			opts.searchIx = value
			opts.keyword = keyword
			i = next2
		case "--search-row":
			if err := setOperation(&opts, opSearchRow); err != nil {
				return opts, err
			}
			value, next, err := nextValue(args, i)
			if err != nil {
				return opts, err
			}
			keyword, next2, err := nextValue(args, next-1)
			if err != nil {
				return opts, errors.New("--search-row 需要指定关键词")
			}
			opts.searchIx = value
			opts.keyword = keyword
			i = next2
		default:
			return opts, fmt.Errorf("未知参数: %s", arg)
		}
	}

	return opts, nil
}

func setOperation(opts *options, op operation) error {
	if opts.op != opNone {
		return errors.New("只能指定一个操作类型 (--size, --rows, --cols, --search-col, --search-row 之一)")
	}
	opts.op = op
	return nil
}

func nextValue(args []string, i int) (string, int, error) {
	if i+1 >= len(args) {
		return "", i + 1, fmt.Errorf("%s 需要参数", args[i])
	}
	return args[i+1], i + 2, nil
}

func optionalValue(args []string, i int) (string, int) {
	if i+1 >= len(args) || strings.HasPrefix(args[i+1], "--") {
		return "", i + 1
	}
	return args[i+1], i + 2
}

func readOptionalRange(args []string, i int, numeric bool) (string, int) {
	first, next := optionalValue(args, i)
	if first == "" {
		return "", next
	}
	if strings.ContainsAny(first, ",-") {
		return first, next
	}
	if next >= len(args) || strings.HasPrefix(args[next], "--") {
		return first, next
	}
	second := args[next]
	if numeric {
		if _, err := strconv.Atoi(second); err != nil {
			return first, next
		}
	} else {
		if _, ok := parseColumnIndex(second); !ok {
			return first, next
		}
	}
	return first + "-" + second, next + 1
}

func parsePositiveInt(value, flag string) (int, error) {
	parsed, err := strconv.Atoi(value)
	if err != nil || parsed <= 0 {
		return 0, fmt.Errorf("%s 需要正整数", flag)
	}
	return parsed, nil
}

func validatePath(path string) error {
	info, err := os.Stat(path)
	if err != nil {
		if os.IsNotExist(err) {
			return fmt.Errorf("文件不存在: %s", path)
		}
		return fmt.Errorf("无法访问文件: %s", path)
	}
	if info.IsDir() {
		return fmt.Errorf("路径是目录: %s", path)
	}
	if strings.ToLower(filepath.Ext(path)) != ".xlsx" {
		return fmt.Errorf("不是有效的 xlsx 文件: %s", path)
	}
	return nil
}

func openWorkbook(path string) (*excelize.File, string, error) {
	file, err := excelize.OpenFile(path)
	if err != nil {
		return nil, "", fmt.Errorf("无法打开文件: %s", path)
	}
	sheets := file.GetSheetList()
	if len(sheets) == 0 {
		return file, "", errors.New("文件中没有可用的 sheet")
	}
	return file, sheets[0], nil
}

func sheetSize(file *excelize.File, sheet string) (int, int, error) {
	rows, err := file.GetRows(sheet)
	if err != nil {
		return 0, 0, err
	}
	maxCols := 0
	lastRow := 0
	for i, row := range rows {
		rowHasData := false
		for _, cell := range row {
			if cell != "" {
				rowHasData = true
			}
		}
		if rowHasData {
			lastRow = i + 1
			if len(row) > maxCols {
				maxCols = len(row)
			}
		}
	}
	return lastRow, maxCols, nil
}

func handleRows(file *excelize.File, sheet string, totalRows, totalCols int, opts options) {
	rowIndexes, requestedMax, err := parseRowSelection(opts.rowsRaw, totalRows)
	if err != nil {
		exitWithUsageError(err.Error())
	}
	if requestedMax > totalRows {
		printWarning(fmt.Sprintf("请求%d行，但文件只有%d行", requestedMax, totalRows))
	}
	maxCols := opts.maxCols
	if totalCols < maxCols {
		maxCols = totalCols
	}
	data := make([][]string, 0, len(rowIndexes))
	for _, rowIdx := range rowIndexes {
		rowValues, err := readRow(file, sheet, rowIdx, maxCols)
		if err != nil {
			exitWithError(err.Error())
		}
		data = append(data, rowValues)
	}
	printRowData(data, rowIndexes, maxCols)
}

func handleCols(file *excelize.File, sheet string, totalRows, totalCols int, opts options) {
	colIndexes, requestedMax, err := parseColumnSelection(opts.colsRaw, totalCols)
	if err != nil {
		exitWithUsageError(err.Error())
	}
	if requestedMax > totalCols {
		printWarning(fmt.Sprintf("请求%d列，但文件只有%d列", requestedMax, totalCols))
	}
	maxRows := opts.maxRows
	if totalRows < maxRows {
		maxRows = totalRows
	}
	data := make([][]string, 0, len(colIndexes))
	for _, colIdx := range colIndexes {
		colValues, err := readColumn(file, sheet, colIdx, maxRows)
		if err != nil {
			exitWithError(err.Error())
		}
		data = append(data, colValues)
	}
	printColumnData(data, colIndexes, maxRows)
}

func handleSearchColumn(file *excelize.File, sheet string, totalRows, totalCols int, opts options) {
	colIdx, ok := parseColumnIndex(opts.searchIx)
	if !ok {
		exitWithUsageError("--search-col 需要列索引")
	}
	if colIdx < 1 || colIdx > totalCols {
		limitColumn := numberToColumn(totalCols)
		label := "列索引"
		if !isNumeric(opts.searchIx) {
			label = "列标号"
		}
		printWarning(fmt.Sprintf("%s %s 超出范围，文件只有 %d 列（%s 列）", label, strings.ToUpper(opts.searchIx), totalCols, limitColumn))
		printSearchResultHeader(0)
		return
	}
	if opts.mode == "regex" {
			if _, err := regexp.Compile(opts.keyword); err != nil {
				exitWithError(fmt.Sprintf("正则表达式语法错误: %s", err.Error()))
			}
		}

	matches := []int{}
	for row := 1; row <= totalRows; row++ {
		value, err := cellValue(file, sheet, row, colIdx)
		if err != nil {
			exitWithError(err.Error())
		}
		if matchValue(value, opts.keyword, opts.mode) {
			matches = append(matches, row)
			if len(matches) >= opts.limit {
				break
			}
		}
	}
	printSearchResultHeader(len(matches))
	if len(matches) == 0 {
		return
	}
	maxCols := opts.maxCols
	if totalCols < maxCols {
		maxCols = totalCols
	}
	data := make([][]string, 0, len(matches))
	for _, row := range matches {
		rowValues, err := readRow(file, sheet, row, maxCols)
		if err != nil {
			exitWithError(err.Error())
		}
		data = append(data, rowValues)
	}
	printRowData(data, matches, maxCols)
}

func handleSearchRow(file *excelize.File, sheet string, totalRows, totalCols int, opts options) {
	rowIdx, err := strconv.Atoi(opts.searchIx)
	if err != nil || rowIdx <= 0 {
		exitWithUsageError("--search-row 需要行索引")
	}
	if rowIdx > totalRows {
		printWarning(fmt.Sprintf("行索引 %d 超出范围，文件只有 %d 行", rowIdx, totalRows))
		printSearchResultHeader(0)
		return
	}
	if opts.mode == "regex" {
		if _, err := regexp.Compile(opts.keyword); err != nil {
			exitWithError(fmt.Sprintf("正则表达式语法错误: %s", err.Error()))
		}
	}

	matches := []int{}
	for col := 1; col <= totalCols; col++ {
		value, err := cellValue(file, sheet, rowIdx, col)
		if err != nil {
			exitWithError(err.Error())
		}
		if matchValue(value, opts.keyword, opts.mode) {
			matches = append(matches, col)
			if len(matches) >= opts.limit {
				break
			}
		}
	}
	printSearchResultHeader(len(matches))
	if len(matches) == 0 {
		return
	}
	maxRows := opts.maxRows
	if totalRows < maxRows {
		maxRows = totalRows
	}
	data := make([][]string, 0, len(matches))
	for _, col := range matches {
		colValues, err := readColumn(file, sheet, col, maxRows)
		if err != nil {
			exitWithError(err.Error())
		}
		data = append(data, colValues)
	}
	printColumnData(data, matches, maxRows)
}

func parseRowSelection(input string, totalRows int) ([]int, int, error) {
	if input == "" {
		return []int{1, 2, 3}, 3, nil
	}
	return parseNumberRange(input, totalRows)
}

func parseColumnSelection(input string, totalCols int) ([]int, int, error) {
	if input == "" {
		return []int{1, 2, 3}, 3, nil
	}
	return parseColumnRange(input, totalCols)
}

func parseNumberRange(input string, maxValue int) ([]int, int, error) {
	items := strings.Split(input, ",")
	values := []int{}
	maxRequested := 0
	seen := map[int]bool{}
	for _, item := range items {
		item = strings.TrimSpace(item)
		if item == "" {
			continue
		}
		if strings.Contains(item, "-") {
			parts := strings.SplitN(item, "-", 2)
			if len(parts) != 2 {
				return nil, 0, fmt.Errorf("无效范围: %s", item)
			}
			start, err := strconv.Atoi(strings.TrimSpace(parts[0]))
			if err != nil || start <= 0 {
				return nil, 0, fmt.Errorf("无效范围: %s", item)
			}
			end, err := strconv.Atoi(strings.TrimSpace(parts[1]))
			if err != nil || end <= 0 {
				return nil, 0, fmt.Errorf("无效范围: %s", item)
			}
			if start > end {
				start, end = end, start
			}
			if end > maxRequested {
				maxRequested = end
			}
			for v := start; v <= end; v++ {
				if !seen[v] {
					values = append(values, v)
					seen[v] = true
				}
			}
			continue
		}
		value, err := strconv.Atoi(item)
		if err != nil || value <= 0 {
			return nil, 0, fmt.Errorf("无效范围: %s", item)
		}
		if value > maxRequested {
			maxRequested = value
		}
		if !seen[value] {
			values = append(values, value)
			seen[value] = true
		}
	}
	if len(values) == 0 {
		return nil, 0, errors.New("未指定有效范围")
	}

	filtered := []int{}
	for _, v := range values {
		if v <= maxValue {
			filtered = append(filtered, v)
		}
	}
	if len(filtered) == 0 {
		filtered = append(filtered, values...)
	}
	return filtered, maxRequested, nil
}

func parseColumnRange(input string, maxValue int) ([]int, int, error) {
	items := strings.Split(input, ",")
	values := []int{}
	maxRequested := 0
	seen := map[int]bool{}
	for _, item := range items {
		item = strings.TrimSpace(item)
		if item == "" {
			continue
		}
		if strings.Contains(item, "-") {
			parts := strings.SplitN(item, "-", 2)
			start, ok := parseColumnIndex(parts[0])
			if !ok {
				return nil, 0, fmt.Errorf("无效范围: %s", item)
			}
			end, ok := parseColumnIndex(parts[1])
			if !ok {
				return nil, 0, fmt.Errorf("无效范围: %s", item)
			}
			if start > end {
				start, end = end, start
			}
			if end > maxRequested {
				maxRequested = end
			}
			for v := start; v <= end; v++ {
				if !seen[v] {
					values = append(values, v)
					seen[v] = true
				}
			}
			continue
		}
		value, ok := parseColumnIndex(item)
		if !ok {
			return nil, 0, fmt.Errorf("无效范围: %s", item)
		}
		if value > maxRequested {
			maxRequested = value
		}
		if !seen[value] {
			values = append(values, value)
			seen[value] = true
		}
	}
	if len(values) == 0 {
		return nil, 0, errors.New("未指定有效范围")
	}

	filtered := []int{}
	for _, v := range values {
		if v <= maxValue {
			filtered = append(filtered, v)
		}
	}
	if len(filtered) == 0 {
		filtered = append(filtered, values...)
	}
	return filtered, maxRequested, nil
}

func parseColumnIndex(raw string) (int, bool) {
	raw = strings.TrimSpace(raw)
	if raw == "" {
		return 0, false
	}
	if isNumeric(raw) {
		value, err := strconv.Atoi(raw)
		if err != nil || value <= 0 {
			return 0, false
		}
		return value, true
	}
	value := 0
	for _, ch := range strings.ToUpper(raw) {
		if ch < 'A' || ch > 'Z' {
			return 0, false
		}
		value = value*26 + int(ch-'A'+1)
	}
	if value <= 0 {
		return 0, false
	}
	return value, true
}

func isNumeric(value string) bool {
	value = strings.TrimSpace(value)
	if value == "" {
		return false
	}
	for _, ch := range value {
		if ch < '0' || ch > '9' {
			return false
		}
	}
	return true
}

func numberToColumn(value int) string {
	if value <= 0 {
		return ""
	}
	result := ""
	for value > 0 {
		value--
		result = string(rune('A'+(value%26))) + result
		value /= 26
	}
	return result
}

func readRow(file *excelize.File, sheet string, rowIndex int, maxCols int) ([]string, error) {
	row := make([]string, maxCols)
	for col := 1; col <= maxCols; col++ {
		value, err := cellValue(file, sheet, rowIndex, col)
		if err != nil {
			return nil, err
		}
		row[col-1] = value
	}
	return row, nil
}

func readColumn(file *excelize.File, sheet string, colIndex int, maxRows int) ([]string, error) {
	col := make([]string, maxRows)
	for row := 1; row <= maxRows; row++ {
		value, err := cellValue(file, sheet, row, colIndex)
		if err != nil {
			return nil, err
		}
		col[row-1] = value
	}
	return col, nil
}

func cellValue(file *excelize.File, sheet string, row, col int) (string, error) {
	cell, err := excelize.CoordinatesToCellName(col, row)
	if err != nil {
		return "", err
	}
	value, err := file.GetCellValue(sheet, cell)
	if err != nil {
		return "", err
	}
	return value, nil
}

func matchValue(value, keyword, mode string) bool {
	switch mode {
	case "exact":
		return value == keyword
	case "regex":
		re, err := regexp.Compile(keyword)
		if err != nil {
			return false
		}
		return re.MatchString(value)
	default:
		return strings.Contains(value, keyword)
	}
}

func printSize(rows, cols int) {
	fmt.Printf("Rows:%d,Cols:%d\n", rows, cols)
}

func printRowData(rows [][]string, rowIndexes []int, maxCols int) {
	headers := make([]string, maxCols+1)
	headers[0] = ""
	for i := 1; i <= maxCols; i++ {
		headers[i] = numberToColumn(i)
	}
	printCSVRow(headers)
	for i, row := range rows {
		line := make([]string, 0, maxCols+1)
		line = append(line, strconv.Itoa(rowIndexes[i]))
		line = append(line, row...)
		printCSVRow(line)
	}
}

func printColumnData(columns [][]string, colIndexes []int, maxRows int) {
	headers := make([]string, len(colIndexes)+1)
	headers[0] = ""
	for i, col := range colIndexes {
		headers[i+1] = numberToColumn(col)
	}
	printCSVRow(headers)
	for row := 1; row <= maxRows; row++ {
		line := make([]string, 0, len(colIndexes)+1)
		line = append(line, strconv.Itoa(row))
		for _, col := range columns {
			line = append(line, col[row-1])
		}
		printCSVRow(line)
	}
}

func printSearchResultHeader(count int) {
	fmt.Printf("搜索结果: 找到 %d 个匹配\n", count)
}

func printCSVRow(values []string) {
	escaped := make([]string, len(values))
	for i, value := range values {
		escaped[i] = escapeCSV(value)
	}
	fmt.Println(strings.Join(escaped, ","))
}

func escapeCSV(value string) string {
	if strings.Contains(value, "\"") {
		value = strings.ReplaceAll(value, "\"", "\"\"")
	}
	if strings.Contains(value, ",") || strings.Contains(value, "\"") || strings.Contains(value, "\n") {
		return "\"" + value + "\""
	}
	return value
}

func printWarning(msg string) {
	fmt.Fprintf(os.Stderr, "警告: %s\n", msg)
}

func exitWithError(msg string) {
	fmt.Fprintf(os.Stderr, "错误: %s\n", msg)
	os.Exit(1)
}

func exitWithUsageError(msg string) {
	fmt.Fprintf(os.Stderr, "错误: %s\n\n使用 --help 查看帮助信息\n", msg)
	os.Exit(2)
}

func printHelp() {
	fmt.Println("XLSX 查看器 - 便捷查看和搜索 Excel 文件")
	fmt.Println()
	fmt.Println("用法:")
	fmt.Println("  xlsx_viewer --path <xlsx文件路径> <操作类型> [参数]")
	fmt.Println()
	fmt.Println("必填参数:")
	fmt.Println("  --path <文件路径>    指定 xlsx 文件的绝对路径")
	fmt.Println()
	fmt.Println("操作类型 (必选其一):")
	fmt.Println("  --size                          显示文件行列数")
	fmt.Println("  --rows [x] [y]                  显示第x到第y行(默认1-3行), 可选 --max-cols m 限制每行最多m列(默认50)")
	fmt.Println("  --cols [x] [y]                  显示第x到第y列(默认1-3列), 可选 --max-rows m 限制每列最多m行(默认50)")
	fmt.Println("  --search-col <列索引> <关键词>   在指定列搜索关键词")
	fmt.Println("  --search-row <行索引> <关键词>   在指定行搜索关键词")
	fmt.Println()
	fmt.Println("搜索参数 (用于--search-col和--search-row):")
	fmt.Println("  --mode <模式>        搜索模式: fuzzy(默认,模糊), exact(精确), regex(正则)")
	fmt.Println("  --limit <数量>       返回最多条数(默认10)")
	fmt.Println()
	fmt.Println("其他:")
	fmt.Println("  --help               显示此帮助信息")
	fmt.Println()
	fmt.Println("行列索引说明:")
	fmt.Println("  行列索引从 1 开始(如第1行、第1列)")
	fmt.Println()
	fmt.Println("示例:")
	fmt.Println("  xlsx_viewer --path data.xlsx --size")
	fmt.Println("  xlsx_viewer --path data.xlsx --rows 1 5 --max-cols 20")
	fmt.Println("  xlsx_viewer --path data.xlsx --rows 10")
	fmt.Println("  xlsx_viewer --path data.xlsx --cols 1 3 --max-rows 100")
	fmt.Println("  xlsx_viewer --path data.xlsx --search-col 2 \"测试\" --mode exact --limit 5")
	fmt.Println("  xlsx_viewer --path data.xlsx --search-row 1 \"error\" --mode regex --limit 20")
}
