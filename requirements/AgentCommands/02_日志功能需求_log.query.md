# 需求文档

## 1. 项目现状与核心目标

本文档描述 AgentCommands 插件的首期命令 `log.query`(查看日志)的功能需求.

整体框架需求与协议(目录约定,输入输出协议,文件流转,恢复机制,模块化扩展等)详见:

- `requirements/AgentCommands/01_整体与框架需求.md`

本文档核心目标:

- 让外部工具通过写入 `pending/{id}.json` 的方式,向 Unity Editor 发起“查看日志”的请求.
- 支持按最近 n 条,按等级过滤,以及 3 种关键词匹配模式(严格/模糊/正则).
- 支持通过参数决定返回结果是否包含堆栈信息.
- 返回的日志条目按时间从旧到新排序,并且每条包含 `time` 时间戳.

## 2. 范围与边界

**功能点简述(本期必须):**

- [ ] 支持命令类型 `type = "log.query"`.
- [ ] `params.n` 控制返回“最近 n 条日志”.
- [ ] 可选按日志等级过滤.
- [ ] 可选按关键词过滤,支持 `Strict`,`Fuzzy`,`Regex` 三种匹配模式.
- [ ] 可选 `includeStack` 控制是否返回堆栈字段.
- [ ] 结果中 `items` 按时间从旧到新排序.
- [ ] 每条日志必须包含 `time`(时间戳).

**排除项(本期明确不做):**

- 不查询 Unity Editor 启动前的历史日志文件(只查本次启动后实时捕获的日志缓存).
- 不做“跨文件的复杂全文检索”或“结构化日志分析”.

## 3. 输入协议(仅针对 log.query)

外部工具写入: `Assets/AgentCommands/pending/{id}.json`.

json 最小字段(本命令相关):

- `type`: 必须为 `log.query`.
- `params`: object.

### 3.1 params 字段定义

`type = "log.query"` 时,`params` 支持:

- `n`: number,最近 n 条日志(例如 50).必填.
- `level`: string,日志等级过滤,可选.允许值:
  - `Log`
  - `Warning`
  - `Error`
- `keyword`: string,关键词,可选.为空表示不按关键词过滤.
- `matchMode`: string,关键词匹配模式,可选.允许值:
  - `Strict`: 严格匹配(区分大小写,整段文本包含该关键词即命中).
  - `Fuzzy`: 模糊匹配(不区分大小写,按包含关系命中).
  - `Regex`: 正则匹配(以 `keyword` 作为正则表达式).
- `includeStack`: boolean,是否包含堆栈信息,可选.默认 false.

### 3.2 过滤与匹配规则说明

- 当未提供 `level` 时,默认不过滤等级.
- 当未提供 `keyword` 或为空时,默认不过滤关键词.
- 当 `matchMode` 未提供时:
  - 如果提供了 `keyword`,默认按 `Strict`.
  - 如果未提供 `keyword`,则不进行关键词过滤.
- 当 `includeStack=false` 时,返回的每条日志不包含 `stack` 字段(字段缺失).

## 4. 输出协议(仅针对 log.query)

### 4.1 success 时的 result 结构

当 `results/{id}.json` 为 `status=success` 时,`result` 建议结构:

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

**例 1: 查询最近 50 条日志,不做过滤**

- pending 输入:
  - `id`: `cmd_001`
  - `type`: `log.query`
  - `params`: `{ n: 50 }`
- 预期 results:
  - 先出现 `status=processing`.
  - 最终变为 `status=success`,`result.items` 至多 50 条.
  - `result.items` 按时间从旧到新排序.

**例 2: 仅查询 Error 等级最近 20 条(不含堆栈)**

- `params`: `{ n: 20, level: "Error", includeStack: false }`
- 预期:
  - 只返回 Error,不包含 Warning/Log.
  - 每条日志不返回 stack.

**例 3: 严格关键词匹配(包含堆栈)**

- `params`: `{ n: 100, keyword: "LoginFailed", matchMode: "Strict", includeStack: true }`
- 预期:
  - 每条日志包含 stack.
  - 只要 message 或 stack 中包含 `LoginFailed`(区分大小写)就命中.

**例 4: 模糊关键词匹配(不区分大小写)**

- `params`: `{ n: 100, keyword: "timeout", matchMode: "Fuzzy" }`
- 预期:
  - `Timeout`,`TIMEOUT`,`timeout` 都命中.

**例 5: 正则匹配**

- `params`: `{ n: 200, keyword: "^UI_.*_Init$", matchMode: "Regex" }`
- 预期:
  - 仅命中满足正则表达式的日志.
- 边缘:
  - 如果正则非法,则 `status=error`,`error.code=INVALID_REGEX`.

## 6. 验收清单(可执行)

- [ ] 往 `pending/` 放入 1 个合法 `log.query` 命令,json 会被处理,`results/` 先出现 processing 再变 success.
- [ ] `params.n=10` 时,`result.returned` 小于等于 10.
- [ ] `params.level="Error"` 时,只返回 Error(包含原 Exception 归入 Error).
- [ ] `params.keyword="LoginFailed",matchMode="Strict"` 时,只命中大小写完全匹配的包含项.
- [ ] `params.keyword="timeout",matchMode="Fuzzy"` 时,大小写不同也能命中.
- [ ] `params.matchMode="Regex"` 且正则非法时,返回 `status=error`,`error.code=INVALID_REGEX`.
- [ ] `params.includeStack=false` 时,每条日志不包含 `stack` 字段; `params.includeStack=true` 时,每条日志包含 `stack` 字段(允许为空字符串,但字段必须存在).
- [ ] 返回的 `items` 永远按时间从旧到新排序,且每条包含 `time` 字段.
