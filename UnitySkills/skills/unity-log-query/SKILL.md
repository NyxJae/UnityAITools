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

2. 生成命令文件

   - 文件名: `{id}.json`,id 建议 16-32 字符,仅 `[a-zA-Z0-9_-]`
   - 写入 `pending/` 后,一般瞬间完成,可根据 id 推测出结果文件路径,直接去 results 查看结果,而不用等待或检查文件存在性(比如不用`sleep`,`ls`等命令)

   示例:

   ```json
   {
     "type": "log.query",
     "params": {
       "n": 50,
       "level": "Error",
       "keyword": "LoginFailed",
       "matchMode": "Strict",
       "includeStack": true
     }
   }
   ```

   params 说明:

   - `n` 必填,返回最近 n 条日志
   - `level` 可选,Log/Warning/Error
   - `keyword` 可选,关键词
   - `matchMode` 可选,Strict/Fuzzy/Regex
   - `includeStack` 可选,默认 false

3. 直接读取结果
  你可以推测出结果文件路径,可直接尝试读取,一般能直接拿到结果.不用`ls`等命令
   - 结果路径: `results/{id}.json`
   - `status` 可能为 `processing` | `success` | `error`
   - `error.message` 为中文错误提示,可直接展示

   示例:

   ```json
   {
     "id": "cmd_001",
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
       "returned": 1
     }
   }
   ```

### Notes

- results 仅保留最近 20 条最终结果(success/error),建议及时读取.
- 正则非法会返回 error,不会崩溃插件.

### 流程

1. 先让用户触发一次目标日志(或给出可复现步骤).
2. 汇总触发路径,日志等级,关键词,是否需要堆栈.
3. 生成 pending/{id}.json 并写入.
4. 直接查看 results/{id}.json,若没有则轮询,直到 status 为 success 或 error.
5. 分析结果,必要时引导用户再次触发或调整参数.
