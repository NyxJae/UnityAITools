# 需求文档

## 1. 项目现状与核心目标

本文档描述 AgentCommands 插件的首期命令 `log.query`(查看日志)的功能需求.

整体框架需求与协议(目录约定,批量命令输入输出协议,文件流转,恢复机制,模块化扩展等)详见:

- `requirements/AgentCommands/01_整体与框架需求.md`

本文档核心目标:

- 让外部工具通过写入批量命令 `pending/{batchId}.json` 的方式,向 Unity Editor 发起"查看日志"的请求.
- 支持按最近 n 条,按等级过滤,以及 2 种关键词匹配模式(模糊/正则).
- 支持通过参数决定返回结果是否包含堆栈信息.
- 返回的日志条目按时间从旧到新排序,并且每条包含 `time` 时间戳.
- 支持在批量命令中执行多个日志查询,并返回所有结果.

## 2. 范围与边界

**功能点简述(本期必须):**

- [ ] 支持命令类型 `type = "log.query"`.
- [ ] `params.n` 控制返回"最近 n 条日志".
- [ ] 可选按日志等级过滤.
- [ ] 可选按关键词过滤,支持 `Fuzzy`,`Regex` 两种匹配模式.
- [ ] 可选 `includeStack` 控制是否返回堆栈字段.
- [ ] 结果中 `items` 按时间从旧到新排序.
- [ ] 每条日志必须包含 `time`(时间戳).

**排除项(本期明确不做):**

- 不查询 Unity Editor 启动前的历史日志文件(只查本次启动后实时捕获的日志缓存).
- 不做“跨文件的复杂全文检索”或“结构化日志分析”.

## 3. 输入协议(仅针对 log.query)

外部工具写入批量命令: `Assets/AgentCommands/pending/{batchId}.json`.

批量命令的完整格式详见 `01_整体与框架需求.md`,本节仅描述 `log.query` 命令的 `params` 部分.

批量命令中,`commands` 数组可以包含一个或多个 `log.query` 命令:

```json
{
  "batchId": "batch_log_001",
  "timeout": 30000,
  "commands": [
    {
      "id": "cmd_001",
      "type": "log.query",
      "params": {
        "n": 50,
        "level": "Error",
        "includeStack": true
      }
    },
    {
      "id": "cmd_002",
      "type": "log.query",
      "params": {
        "n": 100,
        "keyword": "timeout",
        "matchMode": "Fuzzy"
      }
    }
  ]
}
```

**命令对象字段**:

- `id`: 必填.命令唯一标识,由调用方提供,用于在结果中匹配每个命令.
- `type`: 必须为 `log.query`.
- `params`: 必填.命令参数对象,定义见下文.

### 3.1 params 字段定义

`type = "log.query"` 时,`params` 支持:

- `n`: number,最近 n 条日志(例如 50).必填.
- `level`: string,日志等级过滤,可选.允许值:
  - `Log`
  - `Warning`
  - `Error`
- `keyword`: string,关键词,可选.为空表示不按关键词过滤.
- `matchMode`: string,关键词匹配模式,可选.允许值:
  - `Fuzzy`: 模糊匹配(不区分大小写,按包含关系命中),默认值.
  - `Regex`: 正则匹配(以 `keyword` 作为正则表达式).
- `includeStack`: boolean,是否包含堆栈信息,可选.默认 false.

### 3.2 过滤与匹配规则说明

- 当未提供 `level` 时,默认不过滤等级.
- 当未提供 `keyword` 或为空时,默认不过滤关键词.
- 当 `matchMode` 未提供时:
  - 如果提供了 `keyword`,默认按 `Fuzzy`.
  - 如果未提供 `keyword`,则不进行关键词过滤.
- 当 `includeStack=false` 时,返回的每条日志不包含 `stack` 字段(字段缺失).

## 4. 输出协议(仅针对 log.query)

批量命令的输出协议详见 `01_整体与框架需求.md`,本节仅描述 `log.query` 命令执行成功时的 `result` 结构.

当批量命令执行完成(`status=completed`)时,`results` 数组中每个 `type="log.query"` 的命令在 `status=success` 时,其 `result` 结构如下:

- `items`: array,日志条目列表,按时间从旧到新排序(旧的在上,新的在下).
  - 每条包含:
    - `time`: string,记录时间戳(以插件捕获时间为准).
    - `level`: string,同输入 level 枚举.
    - `message`: string,日志正文.
    - `stack`: string,可选,异常/错误堆栈; 仅当 `params.includeStack=true` 时返回.
- `totalCaptured`: number,当前插件内存中已缓存的日志总数.
- `returned`: number,本次返回条数.

### 4.2 error 的典型情况

- 正则非法:
  - `status=error`
  - `error.code=INVALID_REGEX`
  - `error.message` 为对非专业人士可读的说明(例如“正则表达式非法,请检查 keyword”).

## 5. 举例覆盖需求和边缘情况

**例 1: 单个命令查询(批量格式)**

- pending 输入:

```json
{
  "batchId": "batch_log_001",
  "commands": [
    {
      "id": "cmd_001",
      "type": "log.query",
      "params": { "n": 50 }
    }
  ]
}
```

- 预期 results:
  - `status=completed`.
  - `results[0]`: `status=success`, `result.items` 至多 50 条.
  - `result.items` 按时间从旧到新排序.

**例 2: 批量查询不同等级日志**

- pending 输入:

```json
{
  "batchId": "batch_multi_level_001",
  "commands": [
    {
      "id": "cmd_error_50",
      "type": "log.query",
      "params": {
        "n": 50,
        "level": "Error",
        "includeStack": true
      }
    },
    {
      "id": "cmd_warning_100",
      "type": "log.query",
      "params": {
        "n": 100,
        "level": "Warning",
        "includeStack": false
      }
    },
    {
      "id": "cmd_log_200",
      "type": "log.query",
      "params": {
        "n": 200,
        "level": "Log"
      }
    }
  ]
}
```

- 预期 results:
  - `results` 数组包含 3 个元素,顺序与输入一致.
  - `results[0]`: Error 日志,包含 stack.
  - `results[1]`: Warning 日志,不包含 stack.
  - `results[2]`: Log 日志.
  - `totalCommands = 3`, `successCount = 3`, `failedCount = 0`.

**例 3: 批量查询不同关键词(多种匹配模式)**

- pending 输入:

```json
{
  "batchId": "batch_keyword_001",
  "commands": [
    {
      "id": "cmd_timeout",
      "type": "log.query",
      "params": {
        "n": 50,
        "keyword": "timeout",
        "matchMode": "Fuzzy"
      }
    },
    {
      "id": "cmd_ui_init",
      "type": "log.query",
      "params": {
        "n": 100,
        "keyword": "^UI_.*_Init$",
        "matchMode": "Regex"
      }
    }
  ]
}
```

- 预期:
  - `results[0]`: 模糊匹配包含 "timeout" 的日志(不区分大小写).
  - `results[1]`: 正则匹配满足 `^UI_.*_Init$` 的日志.

**例 4: 正则匹配及错误处理**

- pending 输入:

```json
{
  "batchId": "batch_regex_error_001",
  "commands": [
    {
      "id": "cmd_valid",
      "type": "log.query",
      "params": {
        "n": 200,
        "keyword": "^UI_.*_Init$",
        "matchMode": "Regex"
      }
    },
    {
      "id": "cmd_invalid",
      "type": "log.query",
      "params": {
        "n": 100,
        "keyword": "[invalid",
        "matchMode": "Regex"
      }
    }
  ]
}
```

- 预期:
  - `results[0]`: 成功,返回正则匹配的日志.
  - `results[1]`: 失败,`status=error`, `error.code=INVALID_REGEX`.
  - `totalCommands = 2`, `successCount = 1`, `failedCount = 1`.

## 6. 验收清单(可执行)

**批量命令框架相关**:

- [ ] 往 `pending/` 放入 1 个合法批量命令,json 会被处理,`results/` 先出现 processing 再变 completed.
- [ ] 批量命令中的 `log.query` 命令执行成功后,`results` 数组中对应元素的 `status=success`.
- [ ] 批量命令的批次级别统计字段正确: `totalCommands`,`successCount`,`failedCount`.
- [ ] 部分成功模式: 某个 `log.query` 命令失败时(如正则非法),后续命令继续执行.

**log.query 功能相关**:

- [ ] `params.n=10` 时,`result.returned` 小于等于 10.
- [ ] `params.level="Error"` 时,只返回 Error(包含原 Exception 归入 Error).
- [ ] `params.keyword="timeout",matchMode="Fuzzy"` 时,大小写不同也能命中(默认 Fuzzy).
- [ ] `params.matchMode="Regex"` 且正则非法时,返回 `status=error`,`error.code=INVALID_REGEX`.
- [ ] `params.includeStack=false` 时,每条日志不包含 `stack` 字段; `params.includeStack=true` 时,每条日志包含 `stack` 字段(允许为空字符串,但字段必须存在).
- [ ] 返回的 `items` 永远按时间从旧到新排序,且每条包含 `time` 字段.
- [ ] 批量命令中包含多个 `log.query` 时,每个命令的结果按输入顺序返回,顺序一致.
