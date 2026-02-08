# 需求文档

## 1. 项目现状与核心目标

本文档描述 UnityAgentSkills 系统的日志命令需求(log.query,log.screenshot).

### 1.1 特殊说明

- `log.query` 与 `log.screenshot` 均为内置命令(priority 0),必须在 `CoreCommandsLoader` 中硬编码注册,不通过反射插件加载.
- 批量命令框架的通用输入/输出协议,目录约定(pending/results/done),文件流转与恢复机制等,详见:
  - `requirements/UnityAgentSkills/01_整体与框架需求.md`

### 1.2 核心目标

- 外部工具通过写入 `Assets/UnityAgentSkills/pending/{batchId}.json` 向 Unity Editor 发起日志相关能力请求.
- 提供两类能力:
  - `log.query`: 查询 Unity 编辑器在本次启动期间捕获的日志.
  - `log.screenshot`: 对当前 Game 视图截图,并返回截图产物路径.

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
- [ ] 输出位置: `Assets/UnityAgentSkills/results/`.
- [ ] 仅支持 single(单张截图).
- [ ] 兼容 Unity Editor 的 Edit 模式与 Play 模式.
  - 若当前未打开/未聚焦 Game 视图,命令需要自动尝试打开或聚焦 Game 视图后截图.
  - 若仍无法生成有效截图文件,按失败处理.
- [ ] success 语义: `status=success` 表示截图文件已真实落盘且可读.
  - 重要: 必须通过 Editor 异步机制等待截图落盘,不得用 `Thread.Sleep`/忙等循环阻塞 Unity Editor 主进程.
  - 允许 results 从 `processing` 延迟一段时间后再变为 `completed`.
  - 超时: 从开始执行到文件可读的最长等待时间为 5s,超时则该命令 `status=error`,`error.code=TIMEOUT`.

### 2.2 与 results 清理联动(必须)

- [ ] 当框架清理旧的 `results/{batchId}.json` 时,必须同时精准删除该 results JSON 中记录的截图产物.
- [ ] 删除必须有安全边界: 只能删除 `Assets/UnityAgentSkills/results/` 目录下的文件/文件夹.
- [ ] 删除失败需要忽略并继续,不得影响 results 清理流程.

### 2.3 排除项(明确不做)

- 不查询 Unity Editor 启动前的历史日志文件(只查本次启动后实时捕获的日志缓存).
- 不做跨文件的复杂全文检索或结构化日志分析.
- `log.screenshot` 不提供指定分辨率,指定相机,或裁剪区域等高级参数(本期固定截图 Game 视图).

## 3. 输入协议

外部工具写入批量命令: `Assets/UnityAgentSkills/pending/{batchId}.json`.

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
- `level`: string,日志等级过滤,可选.允许值:
  - `Log`
  - `Warning`
  - `Error`
- `keyword`: string,关键词,可选.为空表示不按关键词过滤.
- `matchMode`: string,关键词匹配模式,可选.允许值:
  - `Fuzzy`: 模糊匹配(不区分大小写,按包含关系命中),默认值.
  - `Regex`: 正则匹配(以 `keyword` 作为正则表达式).
- `includeStack`: boolean,是否包含堆栈信息,可选.默认 false.

补充规则:

- 当未提供 `level` 时,默认不过滤等级.
- 当未提供 `keyword` 或为空时,默认不过滤关键词.
- 当 `matchMode` 未提供时:
  - 如果提供了 `keyword`,默认按 `Fuzzy`.
  - 如果未提供 `keyword`,则不进行关键词过滤.
- 当 `includeStack=false` 时,返回的每条日志不包含 `stack` 字段.

### 3.4 log.screenshot 参数(params)

当前版本仅支持单张截图,因此 `params` 为空对象即可.

| 参数名 | 类型 | 必填 | 默认值 | 说明 |
| ------ | ---: | ---: | -----: | ---- |
| (无)   |      |      |        |      |

参数规则:

- 调用方应传 `{}`.
- 如传入本命令未定义的字段,按框架对"未知字段"的统一策略处理(不在本命令内单独定义兼容逻辑).

## 4. 输出协议

批量命令的通用输出结构详见 `01_整体与框架需求.md`.本节只描述两个命令在成功时的 `result` 结构.

### 4.1 log.query 成功结果(result)

当 `type="log.query"` 的命令 `status=success` 时,`result` 结构:

- `items`: array,日志条目列表,按时间从旧到新排序.
  - 每条包含:
    - `time`: string,记录时间戳.
    - `level`: string,同输入 level 枚举.
    - `message`: string,日志正文.
    - `stack`: string,可选,异常/错误堆栈;仅当 `params.includeStack=true` 时返回.
- `totalCaptured`: number,当前插件内存中已缓存的日志总数.
- `returned`: number,本次返回条数.

### 4.2 log.screenshot 成功结果(result)

`log.screenshot` 成功时,命令状态 `status=success` 的含义为: 图片文件已真实落盘且可读.

非阻塞约束(必须):

- 等待截图落盘的过程不得阻塞 Unity Editor 主进程(不得使用 `Thread.Sleep` 或忙等循环占用主线程).
- 允许批次 results 先写入 `processing`,并在截图文件可读后再写入最终 `completed` 结果.
- 超时: 若从该命令开始执行起 5s 内仍无法得到可读的 png 文件,则该命令必须以 `status=error`,`error.code=TIMEOUT` 结束.

- 返回:
  - `mode`: 固定为 `single`.
  - `imageAbsolutePath`: 图片绝对路径.

路径格式约定:

- 返回值使用当前操作系统的绝对路径.
  - Windows 示例: `F:\UnityProject\...\Assets\UnityAgentSkills\results\batch_xxx.png`.
  - macOS 示例: `/Users/name/.../Assets/UnityAgentSkills/results/batch_xxx.png`.

### 4.3 错误的典型情况

- log.query 正则非法:

  - `status=error`
  - `error.code=INVALID_REGEX`

- log.screenshot 参数错误:
  - 例如必填字段缺失,或字段类型不符合协议(以框架通用字段校验为准).
  - `status=error`
  - `error.code=INVALID_FIELDS`

## 5. log.screenshot 输出路径与命名规则

### 5.1 输出根目录

- 输出根目录固定为 UnityAgentSkills 的 results 目录:
  - `Assets/UnityAgentSkills/results/`

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
- 删除失败要忽略并继续,不能影响 results 清理流程.
- 只允许删除 `Assets/UnityAgentSkills/results/` 目录下的文件/文件夹(安全边界).

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

### 例 2: log.query Regex 错误不影响后续命令

- pending 输入:

```json
{
  "batchId": "batch_regex_error_001",
  "commands": [
    {
      "id": "cmd_invalid",
      "type": "log.query",
      "params": { "n": 100, "keyword": "[invalid", "matchMode": "Regex" }
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
  - `imageAbsolutePath` 指向的 png 文件存在且非 0 字节.

### 例 4: 一个 batch 多条 log.screenshot 的命名冲突避免

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

## 8. 验收清单(可执行)

**批量命令框架相关**:

- [ ] 往 `pending/` 放入 1 个合法批量命令,json 会被处理,`results/` 先出现 processing 再变 completed.
- [ ] 批量命令的批次级别统计字段正确: `totalCommands`,`successCount`,`failedCount`.
- [ ] 部分成功模式: 某个命令失败时,后续命令继续执行.

**log.query 功能相关**:

- [ ] `params.n=10` 时,`result.returned` 小于等于 10.
- [ ] `params.level="Error"` 时,只返回 Error.
- [ ] `matchMode="Regex"` 且正则非法时,返回 `status=error`,`error.code=INVALID_REGEX`.
- [ ] 返回的 `items` 永远按时间从旧到新排序,且每条包含 `time` 字段.

**log.screenshot 功能相关**:

- [ ] 能通过写入 pending JSON 触发截图,并在 `Assets/UnityAgentSkills/results/` 下看到对应 png 文件.
- [ ] `status=success` 时: 对应 png 文件存在且非 0 字节.
  - [ ] 且整个等待过程不阻塞编辑器主进程.
  - [ ] 且从开始执行到完成的总耗时 <= 5s(超时应返回 `status=error`,`error.code=TIMEOUT`).
- [ ] 截图内容必须包含 UI(例如有弹窗时,截图里应看到弹窗).
- [ ] 在 Edit 模式下也能截图成功(不要求进入 Play).
- [ ] 触发 results 清理(超过框架配置的最大保留数)后,被清理的 batch 对应截图文件会同步删除.
