---
name: unity-log-query
description: 查询 Unity 编辑器日志. 触发关键词:Unity:日志,Unity log
---

# Unity Log Query

## Instructions

### Context

本技能用于查看 Unity 编辑器日志

### Steps

1. 确认目录

   - 输入目录: `Assets/AgentCommands/pending/`
   - 输出目录: `Assets/AgentCommands/results/`
   - 归档目录: `Assets/AgentCommands/done/`
     注意 MUST 只寻找以上目录,若找不到,则说明用户未安装必要的插件,需要友好提醒用户安装.MUSTNOT 创建以上目录!!!

2. 生成命令文件

   使用批量命令格式,即使只执行一个命令也必须使用批量格式

   - 文件名: `{batchId}.json`,batchId 建议 16-32 字符,仅 `[a-zA-Z0-9_-]`
   - 写入 `pending/` 后,一般瞬间完成,可根据 batchId 推测出结果文件路径,结果文件名也会是`{batchId}.json`

   单命令示例:
  batch_log_001.json
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
           "keyword": "LoginFailed",
           "includeStack": true
         }
       }
     ]
   }
   ```

   多命令示例:
  batch_logs_001.json
   ```json
   {
     "batchId": "batch_logs_001",
     "timeout": 30000,
     "commands": [
       {
         "id": "cmd_error",
         "type": "log.query",
         "params": { "n": 50, "level": "Error" }
       },
       {
         "id": "cmd_warning",
         "type": "log.query",
         "params": { "n": 100, "level": "Warning" }
       }
     ]
   }
   ```

   字段说明:

   - `batchId` 必填,批次唯一标识
   - `timeout` 可选,批次级别超时(毫秒),默认 30000
   - `commands` 必填,命令数组,每个元素是一个命令
   - `id` 必填,命令唯一标识
   - `type` 必填,命令类型,固定为 "log.query"
   - `params` 必填,命令参数
   - `timeout` (命令级别) 可选,命令超时(优先级高于批次)

   params 说明:

   - `n` 必填,返回最近 n 条日志
   - `level` 可选,Log/Warning/Error
   - `keyword` 可选,关键词
   - `matchMode` 可选,Fuzzy(默认)/Regex
   - `includeStack` 可选,默认 false

3. 直接读取结果
   你可以推测出结果文件路径,可直接尝试读取,一般能直接拿到结果,不行就再读一次试试.不用`ls`,`sleep`等命令

   - 结果路径: `results/{batchId}.json`
   - `status` 可能为 `processing` | `completed` | `error`
   - `error.message` 为中文错误提示,可直接展示

   结果示例:
  batch_logs_001.json
   ```json
   {
     "batchId": "batch_logs_001",
     "status": "completed",
     "startedAt": "2026-01-20T07:54:00Z",
     "finishedAt": "2026-01-20T07:54:02Z",
     "results": [
       {
         "id": "cmd_error",
         "type": "log.query",
         "status": "success",
         "startedAt": "2026-01-20T07:54:00Z",
         "finishedAt": "2026-01-20T07:54:01Z",
         "result": {
           "items": [
             {
               "time": "2026-01-20T07:53:00Z",
               "level": "Error",
               "message": "Login failed",
               "stack": "UnityEngine.Debug:LogError(...)"
             }
           ],
           "totalCaptured": 150,
           "returned": 10
         }
       }
     ],
     "totalCommands": 1,
     "successCount": 1,
     "failedCount": 0
   }
   ```

### Notes

- results 仅保留最近 20 条最终结果(success/error),建议及时读取.
- 正则非法会返回 error,不会崩溃插件.
- 批量命令采用串行执行,严格按输入顺序.
- 批量命令支持部分成功模式,单个命令失败不影响后续执行.
- 批次超时会导致未执行命令标记为 SKIPPED 错误.
- 命令超时优先级高于批次超时.

### 流程

1. 先让用户触发一次目标日志(或给出可复现步骤).
2. 汇总触发路径,日志等级,关键词,是否需要堆栈.
3. 生成 pending/{batchId}.json 并写入.
4. 直接查看 results/{batchId}.json,直到 status 为 completed 或 error.
5. 分析结果,必要时引导用户再次触发或调整参数.
