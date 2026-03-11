# 需求文档

## 1. 项目现状与核心目标

本文档描述 UnityAgentSkills 系统的日志命令需求(log.query,log.screenshot,log.refresh).

### 1.1 特殊说明

- `log.query`,`log.screenshot`,`log.refresh` 均为内置命令(priority 0),必须在 `CoreCommandsLoader` 中硬编码注册,不通过反射插件加载.
- `log.refresh` 是手动触发一次 Unity 刷新并等待刷新/编译闭环结束的 `log.*` 命令,不属于通用 `editor.*` 或后台自动编译服务.
- 批量命令框架的通用输入/输出协议,目录约定(pending/results/done),文件流转与恢复机制等,详见:
  - `01_整体与框架需求.md`

### 1.2 核心目标

- 外部工具通过写入 `Assets/UnityAgentSkills/Commands/pending/{batchId}.json` 向 Unity Editor 发起日志相关能力请求.
- 提供三类能力:
  - `log.query`: 查询 Unity 编辑器在本次启动期间捕获的日志.
  - `log.screenshot`: 对当前 Game 视图截图,并返回截图产物路径.
  - `log.refresh`: 手动触发一次 Unity 刷新,并等待 Unity 回到非刷新,非编译状态后再返回结果.

## 2. 范围与边界

### 2.1 功能范围(必须)

**log.query**:

- [ ] 支持命令类型 `type = "log.query"`.
- [ ] `params.n` 控制返回最近 n 条日志.
- [ ] 可选按日志等级过滤.
- [ ] 可选按关键词过滤,支持 `Fuzzy`,`Regex` 两种匹配模式.
- [ ] 可选 `includeStack` 控制是否返回堆栈字段.
- [ ] 结果中 `items` 按时间从旧到新排序.
- [ ] 每条日志必须包含 `time`(时间戳).

**log.screenshot**:

- [ ] 支持命令类型 `type = "log.screenshot"`.
- [ ] 命令性质: 内置命令(priority 0),硬编码注册在 `CoreCommandsLoader`,不通过反射插件加载.
- [ ] 截图对象: 当前 Game 视图画面,默认包含所有 UI.
- [ ] 输出格式: 固定 PNG(`.png`).
- [ ] 输出位置: `Assets/UnityAgentSkills/Commands/results/`.
- [ ] 仅支持 single(单张截图).
- [ ] 兼容 Unity Editor 的 Edit 模式与 Play 模式.
  - 若当前未打开/未聚焦 Game 视图,命令需要自动尝试打开或聚焦 Game 视图后截图.
  - 若仍无法生成有效截图文件,按失败处理.
- [ ] success 语义: `status=success` 表示截图文件已真实落盘且可读.
  - 重要: 必须通过 Editor 异步机制等待截图落盘,不得用 `Thread.Sleep`/忙等循环阻塞 Unity Editor 主进程.
  - 允许 results 从 `processing` 延迟一段时间后再变为 `completed`.
  - 超时: 从开始执行到文件可读的最长等待时间为 5s,超时则该命令 `status=error`,`error.code=TIMEOUT`.

**log.refresh**:

- [ ] 支持命令类型 `type = "log.refresh"`.
- [ ] 命令性质: 内置命令(priority 0),硬编码注册在 `CoreCommandsLoader`,不通过反射插件加载.
- [ ] 该命令用于手动触发一次 Unity 刷新,典型动作为 `AssetDatabase.Refresh()`.
- [ ] 命令发起后,系统需要等待直到 Unity 不再处于"刷新中/编译中"状态,才可将该命令视为成功完成.
- [ ] 若刷新后没有发生脚本编译,只要 Unity 最终已回到非刷新,非编译状态,仍可成功.
- [ ] 超时规则沿用 batch 的整体 `timeout`,不为该命令单独新增专属超时字段.
- [ ] 结果语义必须让非专业人士也能看懂,至少应能区分"已完成刷新且期间发生过编译"和"已完成刷新但本次未触发编译".
- [ ] 即使调用时 Unity 已经处于刷新中或编译中,仍必须再次显式触发刷新,然后统一等待到 Unity 空闲.
- [ ] `log.refresh` 属于 `unity-log` 技能导出范围,但不应被设计成通用 `editor.*` 或后台自动编译服务能力.

### 2.2 与 results 清理联动(必须)

- [ ] 当框架清理旧的 `results/{batchId}.json` 时,必须同时精准删除该 results JSON 中记录的截图产物.
- [ ] 删除必须有安全边界: 只能删除 `Assets/UnityAgentSkills/Commands/results/` 目录下的文件/文件夹.
- [ ] 删除失败需要忽略并继续,不得影响 results 清理流程.

### 2.3 排除项(明确不做)

- 不查询 Unity Editor 启动前的历史日志文件(只查本次启动后实时捕获的日志缓存).
- 不做跨文件的复杂全文检索或结构化日志分析.
- `log.screenshot` 不提供指定分辨率,指定相机,或裁剪输出图等高级参数(本期固定截图 Game 视图).
- `log.screenshot` 仅支持可选 `highlightRect`,用于全图截图红框标注.

## 3. 输入协议

外部工具写入批量命令: `Assets/UnityAgentSkills/Commands/pending/{batchId}.json`.

批量命令的完整格式详见 `01_整体与框架需求.md`.本节只列出与日志能力相关的字段.

### 3.1 批量命令输入示例

```json
{
  "batchId": "batch_log_001",
  "timeout": 30000,
  "commands": [
    {
      "id": "cmd_001",
      "type": "log.query",
      "params": { "n": 50 }
    },
    {
      "id": "cmd_002",
      "type": "log.screenshot",
      "params": {}
    }
  ]
}
```

### 3.2 命令对象字段

- `id`: 必填.命令唯一标识,由调用方提供,用于在结果中匹配每个命令.
- `type`: 必填.命令类型.
- `params`: 必填.命令参数对象.

### 3.3 log.query 参数(params)

`type = "log.query"` 时,`params` 支持:

- `n`: number,最近 n 条日志.必填.
- `level`: any,日志等级过滤,可选.有效等级(对输入做 trim 后,不区分大小写识别,识别成功才过滤,否则视为未提供):
  - `Log`
  - `Warning`
  - `Error`
- `keyword`: string[],关键词,可选.空数组或仅包含空字符串时表示不按关键词过滤.
- `matchMode`: string,关键词匹配模式,可选.允许值:
  - `Fuzzy`: 模糊匹配(不区分大小写,按包含关系命中),默认值.
  - `Regex`: 正则匹配(以 `keyword[0]` 作为正则表达式).
- `includeStack`: boolean,是否包含堆栈信息,可选.默认 false.

补充规则:

- `level` 属于日志领域专用过滤字段,继续保留该命名,不与其他技能强行统一.
- 当未提供 `level` 时,默认不过滤等级.
- 当提供了 `level` 时,会先对其做 trim,然后再以"不区分大小写"的方式识别是否为 `Log`/`Warning`/`Error`.
  - 例如 `" error "` 会被识别为 `Error` 并过滤.
- 除上述 3 个等级以外的任何输入(包括空字符串/仅空格/null/非字符串类型),都按"未提供 level"处理,即不过滤等级.
- `keyword` 属于日志正文关键词搜索字段.在 `Fuzzy` 模式下,其理解方式与其他技能中的文本搜索一致: contains + IgnoreCase + OR.
- 当未提供 `keyword`,或 `keyword` 为空数组,或数组元素在 Trim 后全部为空时,默认不过滤关键词.
- 当 `matchMode = Fuzzy` 时,`keyword` 中多个词项按 contains + IgnoreCase + OR 处理.
- 当 `matchMode = Regex` 时,仅允许 `keyword` 数组中存在 1 个元素,并以该元素作为正则表达式.
- 当 `matchMode = Regex` 且 `keyword` 传入多个元素时,应返回参数错误,避免语义歧义.
- 当 `matchMode` 未提供时:
  - 如果提供了 `keyword`,默认按 `Fuzzy`.
  - 如果未提供 `keyword`,则不进行关键词过滤.
- 除 `level` 的特殊例外外,当 `keyword` 传入了有效过滤值但最终没有任何日志命中时,应正常返回空结果,不是回退为不过滤.
- 当 `includeStack=false` 时,返回的每条日志不包含 `stack` 字段.

### 3.4 log.screenshot 参数(params)

当前版本仅支持单张全图截图.`params` 可为空对象,也可仅传红框标注参数.

| 参数名          |   类型 | 必填 | 默认值 | 说明                                      |
| --------------- | -----: | ---: | -----: | ----------------------------------------- |
| `highlightRect` | object |   否 |      - | 红框标注区域,格式 `{xMin,xMax,yMin,yMax}` |

参数规则:

- `params` 可传 `{}`.
- `highlightRect` 越界时自动裁剪到图像边界,不报错.
- `highlightRect` 无效(如 `xMin > xMax`)时,返回 `INVALID_FIELDS`.
- 如传入本命令未定义字段,按框架对"未知字段"的统一策略处理.

### 3.5 log.refresh 参数(params)

`type = "log.refresh"` 时:

- `params` 当前允许为空对象 `{}`.
- 不要求新增必填参数.
- 若未来需要扩展,也应优先保持默认调用简单,避免用户每次手动刷新都要理解过多专业参数.

## 4. 输出协议

批量命令的通用输出结构详见 `01_整体与框架需求.md`.本节只描述两个命令在成功时的 `result` 结构.

### 4.1 log.query 成功结果(result)

当 `type="log.query"` 的命令 `status=success` 时,`result` 结构:

- `items`: array,日志条目列表,按时间从旧到新排序.
  - 每条包含:
    - `time`: string,记录时间戳.
    - `level`: string,固定为 `Log`/`Warning`/`Error` 之一,表示该条日志本身的等级.
    - `message`: string,日志正文.
    - `stack`: string,可选,异常/错误堆栈;仅当 `params.includeStack=true` 时返回.
- `totalCaptured`: number,当前插件内存中已缓存的日志总数.
- `returned`: number,本次返回条数.

### 4.2 log.screenshot 成功结果(result)

`log.screenshot` 成功时,命令状态 `status=success` 的含义为: 图片文件已真实落盘且可读.

补充语义(必须):

- screenshot 创建阶段不检查对应 `.meta` 是否存在.
- screenshot 创建阶段仍以 png 文件真实落盘且可读作为 success 判定基线,但 `.meta` 不参与这一步成功判定.
- 即使后续发现截图 sidecar `.meta` 缺失,也不影响这次 `log.screenshot` 已返回的成功结果.

非阻塞约束(必须):

- 等待截图落盘的过程不得阻塞 Unity Editor 主进程(不得使用 `Thread.Sleep` 或忙等循环占用主线程).
- 允许批次 results 先写入 `processing`,并在截图文件可读后再写入最终 `completed` 结果.
- 超时: 若从该命令开始执行起 5s 内仍无法得到可读的 png 文件,则该命令必须以 `status=error`,`error.code=TIMEOUT` 结束.

- 返回:
  - `mode`: 固定为 `single`.
  - `imageAbsolutePath`: 图片绝对路径(全图).
  - `highlightApplied`: bool,是否应用了红框标注.

路径格式约定:

- 返回值使用当前操作系统的绝对路径.
  - Windows 示例: `F:\\UnityProject\\...\\Assets\\UnityAgentSkills\\Commands\\results\\batch_xxx.png`.
  - macOS 示例: `/Users/name/.../Assets/UnityAgentSkills/Commands/results/batch_xxx.png`.

### 4.3 log.refresh 成功结果(result)

当 `type="log.refresh"` 的命令 `status=success` 时,`result` 至少应包含:

- `summary`: 中文自然语言说明,直接表达这次刷新等待的结果.
- `refreshTriggered`: bool,表示本次命令已显式触发刷新.
- `compilationOccurred`: bool,表示本次等待期间是否观察到编译发生.

成功判定补充:

- 命令成功的最低标准是: 已发起本次刷新动作,且 Unity 已回到非刷新,非编译状态.
- 不要求必须真的发生脚本编译.
- 不要求证明所有后台任务都结束,只要求满足本命令关注的刷新/编译闭环.
- 即使调用时 Unity 原本已经在刷新或编译中,也仍应再次显式触发刷新,并继续等待到空闲.

成功结果示意:

```json
{
  "status": "success",
  "result": {
    "summary": "已完成 Unity 刷新,并且这次过程中发生了编译.",
    "refreshTriggered": true,
    "compilationOccurred": true
  }
}
```

另一种成功示意:

```json
{
  "status": "success",
  "result": {
    "summary": "已完成 Unity 刷新,这次没有触发编译.",
    "refreshTriggered": true,
    "compilationOccurred": false
  }
}
```

### 4.4 错误的典型情况

- log.query 正则非法:

  - `status=error`
  - `error.code=INVALID_REGEX`

- log.screenshot 参数错误:

  - 例如必填字段缺失,或字段类型不符合协议(以框架通用字段校验为准).
  - `status=error`
  - `error.code=INVALID_FIELDS`

- log.refresh 超时或刷新闭环未完成:
  - 已尝试触发刷新,但在 batch timeout 内未等到 Unity 结束刷新/编译.
  - `status=error`
  - 错误码可沿用框架超时语义,至少应能稳定区分为 timeout 类失败.
  - 错误说明文字应优先使用中文自然语言,让非专业人士直接理解.

## 5. log.screenshot 输出路径与命名规则

### 5.1 输出根目录

- 输出根目录固定为 UnityAgentSkills 的 results 目录:
  - `Assets/UnityAgentSkills/Commands/results/`

### 5.2 兼容一个 batch 多个 log.screenshot 的命名规则

同一个 batchId 允许出现多个 `log.screenshot` 命令.为避免文件名冲突,命名规则如下.

定义 baseName:

- 若该 batch 内只有 1 条 `log.screenshot` 命令,则 `baseName = {batchId}`.
- 若该 batch 内有多条 `log.screenshot` 命令,则每条命令 `baseName = {batchId}_{cmdId}`.

输出文件:

- 输出文件: `results/{baseName}.png`.

覆盖规则:

- 若目标 PNG 已存在,本次执行覆盖旧文件.

## 6. 与 results 清理的联动(必须)

当框架清理 `results/{batchId}.json`(及 done 归档)时,必须同时清理该 batch 生成的截图产物.

清理规则(精准删除,避免误删):

- 在删除 `results/{batchId}.json` 之前,需要先读取并解析该 JSON.
- 遍历 `results[]` 中 `type == "log.screenshot"` 且 `status == "success"` 的命令结果.
- 从 `result.imageAbsolutePath` 读取截图文件的绝对路径.
- 仅删除这个明确返回的路径(文件).
- 截图清理时,必须将 png 本体与同路径同文件名追加 `.meta` 得到的 sidecar 文件视为同一组成功条件.
- 若截图 png 存在但 `.meta` 不存在,则本次截图清理静默跳过,不输出 warning.
- 若 png 与 `.meta` 都存在,则必须成对删除; 任一删除失败都不得算清理成功,且不输出 warning.
- 只允许删除 `Assets/UnityAgentSkills/Commands/results/` 目录下的文件/文件夹(安全边界).
- 不要求补写,重建,或自动重新导入以恢复缺失的截图 `.meta`.

## 7. 举例覆盖需求和边缘情况

### 例 1: log.query 单个命令

- pending 输入:

```json
{
  "batchId": "batch_log_001",
  "commands": [{ "id": "cmd_001", "type": "log.query", "params": { "n": 50 } }]
}
```

- 预期:
  - `status=completed`.
  - `results[0].status=success`.
  - `result.items` 至多 50 条,且按时间从旧到新排序.

### 例 1.1: log.query level=未知值(按不过滤处理)

- pending 输入:

```json
{
  "batchId": "batch_log_unknown_level_001",
  "commands": [
    {
      "id": "cmd_001",
      "type": "log.query",
      "params": { "n": 50, "level": "All" }
    }
  ]
}
```

- 预期:
  - `status=completed`.
  - `results[0].status=success`(未知 level 不会导致命令失败,只是不做 level 过滤).
  - 返回日志"不按 level 过滤",因此 `result.items[].level` 可能同时包含 `Log`,`Warning`,`Error`.
  - `result.items` 仍按时间从旧到新排序.

### 例 2: log.query Regex 错误不影响后续命令

- pending 输入:

```json
{
  "batchId": "batch_regex_error_001",
  "commands": [
    {
      "id": "cmd_invalid",
      "type": "log.query",
      "params": { "n": 100, "keyword": ["[invalid"], "matchMode": "Regex" }
    },
    {
      "id": "cmd_screenshot",
      "type": "log.screenshot",
      "params": {}
    }
  ]
}
```

- 预期:
  - 第 1 条命令: `status=error`, `error.code=INVALID_REGEX`.
  - 第 2 条命令仍会执行.

### 例 3: log.screenshot single(默认)

- pending 输入:

```json
{
  "batchId": "batch_shot_single_001",
  "commands": [{ "id": "cmd_001", "type": "log.screenshot", "params": {} }]
}
```

- 预期:
  - `results[0].status=success`.
  - `results[0].result.mode="single"`.
  - `results[0].result.highlightApplied=false`.
  - `imageAbsolutePath` 指向的 png 文件存在且非 0 字节.

### 例 4: log.screenshot 带区域红框

- pending 输入:

```json
{
  "batchId": "batch_shot_highlight_001",
  "commands": [
    {
      "id": "cmd_001",
      "type": "log.screenshot",
      "params": {
        "highlightRect": { "xMin": 420, "xMax": 1500, "yMin": 200, "yMax": 900 }
      }
    }
  ]
}
```

- 预期:
  - `results[0].status=success`.
  - `results[0].result.highlightApplied=true`.
  - 返回文件是全图 PNG,并带红框标注.
  - 不返回裁剪图路径.

### 例 5: 一个 batch 多条 log.screenshot 的命名冲突避免

- pending 输入:

```json
{
  "batchId": "batch_multi_shot_001",
  "commands": [
    { "id": "cmd_a", "type": "log.screenshot", "params": {} },
    { "id": "cmd_b", "type": "log.screenshot", "params": {} }
  ]
}
```

- 预期:
  - 两条命令都会成功.
  - 两张截图不会覆盖,输出文件名满足 baseName 规则.

### 例 6: 修改 C# 脚本后手动触发刷新并发生编译

- 输入场景:
  - 用户在外部编辑器修改 `Assets/Scripts/CardLogic.cs`.
  - 随后发送 `log.refresh`.
- 预期:
  - Unity 执行刷新,进入脚本编译.
  - 编译结束后,Unity 不再处于刷新中/编译中.
  - 该命令成功.
  - 对非专业人士的可读解释类似: `已完成 Unity 刷新,并且这次过程中发生了编译.`

### 例 7: 修改普通资源后手动触发刷新,但未发生脚本编译

- 输入场景:
  - 用户只修改了某个不触发脚本编译的资源文件.
  - 发送 `log.refresh`.
- 预期:
  - Unity 执行刷新,但没有进入脚本编译.
  - 稍后 Unity 回到空闲状态.
  - 该命令仍成功.
  - 对非专业人士的可读解释类似: `已完成 Unity 刷新,但这次没有发生脚本编译.`

### 例 8: Unity 长时间未结束编译,最终超时

- 输入场景:
  - 用户修改了多处脚本,导致 Unity 编译时间较长.
  - batch 的 `timeout` 设置较短.
  - 发送 `log.refresh` 后,直到 timeout 到达时,Unity 仍处于编译中.
- 预期:
  - 该命令失败.
  - 对非专业人士的可读解释类似: `已触发 Unity 刷新,但在本次等待时限内没有等到刷新/编译结束.`

### 例 9: 同一个 batch 中先刷新,再查日志

```json
{
  "batchId": "batch_refresh_then_query_001",
  "timeout": 30000,
  "commands": [
    {
      "id": "cmd_refresh",
      "type": "log.refresh",
      "params": {}
    },
    {
      "id": "cmd_logs",
      "type": "log.query",
      "params": { "n": 50, "level": "Error" }
    }
  ]
}
```

- 预期:
  - 调用方可以先主动等待 Unity 刷新闭环完成.
  - 再继续查询日志,看这次刷新/编译后是否产生新的错误日志.

### 例 10: 调用 `log.refresh` 时,Unity 已经处于刷新中或编译中

- 输入场景:
  - 用户发送 `log.refresh` 时,Unity 此刻可能已经因为别的文件变化而在刷新或编译.
- 预期:
  - 本命令仍然要求显式发起一次新的刷新动作.
  - 发起刷新后,继续统一等待,直到 Unity 回到非刷新,非编译状态.
  - 只有在这种前提下,才可把本次 `log.refresh` 视为成功完成.
  - 成功结果中的 `refreshTriggered` 仍应为 `true`.

## 8. 验收清单(可执行)

**批量命令框架相关**:

- [ ] 往 `pending/` 放入 1 个合法批量命令,json 会被处理,`results/` 先出现 processing 再变 completed.
- [ ] 批量命令的批次级别统计字段正确: `totalCommands`,`successCount`,`failedCount`.
- [ ] 部分成功模式: 某个命令失败时,后续命令继续执行.

**log.query 功能相关**:

- [ ] `params.n=10` 时,`result.returned` 小于等于 10.
- [ ] `params.level="Error"`(或等价的大小写/空白变体,如 `" error "`)时,只返回 Error.
- [ ] `params.level` 为未知值(不在 `Log`/`Warning`/`Error` 中)时,不按 level 过滤,返回所有等级.
- [ ] `params.keyword` 在 `Fuzzy` 模式下按 contains + IgnoreCase + OR 处理.
- [ ] `params.keyword` 传入有效值但没有日志命中时,正常返回空结果,不回退为不过滤.
- [ ] `matchMode="Regex"` 且正则非法时,返回 `status=error`,`error.code=INVALID_REGEX`.
- [ ] `matchMode="Regex"` 且 `keyword` 传入多个元素时,返回参数错误.
- [ ] 返回的 `items` 永远按时间从旧到新排序,且每条包含 `time` 字段.

**log.screenshot 功能相关**:

- [ ] 能通过写入 pending JSON 触发截图,并在 `Assets/UnityAgentSkills/Commands/results/` 下看到对应 png 文件.
- [ ] `status=success` 时: 对应 png 文件存在且非 0 字节.
  - [ ] 且整个等待过程不阻塞编辑器主进程.
  - [ ] 且从开始执行到完成的总耗时 <= 5s(超时应返回 `status=error`,`error.code=TIMEOUT`).
- [ ] 截图内容必须包含 UI(例如有弹窗时,截图里应看到弹窗).
- [ ] 在 Edit 模式下也能截图成功(不要求进入 Play).
- [ ] 触发 results 清理(超过框架配置的最大保留数)后,被清理的 batch 对应截图文件会同步删除.

**log.refresh 功能相关**:

- [ ] 能通过写入 pending JSON 触发 `log.refresh`,并在 batch timeout 内等待到 Unity 回到非刷新,非编译状态.
- [ ] 成功结果至少包含 `summary`,`refreshTriggered`,`compilationOccurred`.
- [ ] `refreshTriggered` 在成功场景下为 `true`,即使调用开始时 Unity 原本已经处于刷新或编译中.
- [ ] 修改会触发编译的脚本后调用 `log.refresh`,能够返回成功并体现 `compilationOccurred=true`.
- [ ] 修改不会触发编译的普通资源后调用 `log.refresh`,能够返回成功并体现 `compilationOccurred=false`.
- [ ] 若直到 batch timeout 到达时 Unity 仍未结束刷新/编译,则返回 timeout 类失败,并给出非专业人士可读说明.
- [ ] 整个等待过程不得阻塞 Unity Editor 主线程,应采用跨帧推进模型.
