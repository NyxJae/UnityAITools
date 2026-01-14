# Unity 日志 AI 友好查询工具

让 AI 编程助手轻松读取 Unity 日志，提升调试效率。

## 📋 目录

- [架构说明](#架构说明)
- [快速开始](#快速开始)
- [详细使用](#详细使用)
  - [启动日志服务](#启动日志服务)
  - [配置 Unity 客户端](#配置-unity-客户端)
  - [查询日志](#查询日志)
- [AI 工具配置](#ai-工具配置)
- [查询参数说明](#查询参数说明)
- [故障排查](#故障排查)

## 🏗️ 架构说明

本工具采用客户端-服务端架构：

```
Unity编辑器 (UnityLogClient.cs)
       ↓ TCP连接
日志服务端 (unity_log_server.js) ← 查询工具 (query_unity_logs.js)
```

- **Unity 端**：作为客户端，实时向服务端发送日志
- **服务端**：接收并缓存日志（最多 200 条），提供查询接口
- **查询工具**：连接服务端，获取日志数据

## 🚀 快速开始

### 第一步：复制文件到项目根目录（推荐）

将日志服务和查询工具复制到你的 Unity 项目根目录：

```bash
# 复制到项目根目录
cp unity日志AI友好查询/unity_log_server.js ./
cp unity日志AI友好查询/query_unity_logs.js ./
```

这样可以在项目根目录直接启动服务，方便统一管理。

### 第二步：启动日志服务

在项目根目录启动日志服务端（只需启动一次）：

```bash
# 启动日志服务（保持终端运行）
node unity_log_server.js
```

服务启动后会显示：

```
========================================
  Unity Log Server Started
  Port: 6800
  Max Log Cache: 200
========================================

[Server 2026-01-14T15:00:00.000Z] Waiting for connections...
```

### 第三步：配置 Unity 客户端

1. 将 `UnityLogClient.cs` 复制到 Unity 项目的 `Assets/Editor/` 文件夹
2. 打开 Unity 编辑器，插件会自动启动并连接到日志服务

Unity Console 会显示：

```
[UnityLogClient] Connected to server as YourProjectName
```

### 第四步：查询日志

```bash
# 查询最近 20 条日志
node query_unity_logs.js --count 20

# 查询最近 5 分钟的日志
node query_unity_logs.js --minutes 5

# 查询包含 "error" 的日志
node query_unity_logs.js --fuzzy "error"
```

## 📖 详细使用

### 复制文件到项目根目录（推荐）

```bash
# 复制日志服务和查询工具到项目根目录
cp unity日志AI友好查询/unity_log_server.js ./
cp unity日志AI友好查询/query_unity_logs.js ./
```

这样可以在项目根目录统一管理，方便后续使用。

### 启动日志服务

**方法一：直接运行（推荐调试时使用）**

```bash
node unity_log_server.js
```

保持终端运行，你会实时看到所有 Unity 日志输出（带颜色和客户端标识）。

**方法二：后台运行（推荐长期使用）**

```bash
# Windows
start /B node unity_log_server.js

# Linux/Mac
nohup node unity_log_server.js > /dev/null 2>&1 &
```

### 配置 Unity 客户端

将 `UnityLogClient.cs` 复制到你的 Unity 项目：

```bash
# 复制到 Unity 项目
cp unity日志AI友好查询/UnityLogClient.cs YourUnityProject/Assets/Editor/
```

**特性：**

- ✅ 随 Unity 编辑器自动启动
- ✅ 自动重连（每 3 秒尝试一次）
- ✅ 检测 Console Clear 操作并同步清空
- ✅ 支持多个 Unity 项目同时接入
- ✅ 使用项目名作为客户端标识

**Unity 菜单：**

- `Tools > UnityLogClient > Reconnect` - 手动重连
- `Tools > UnityLogClient > Status` - 查看连接状态

### 查询日志

#### 基础查询

```bash
# 查询最近 20 条
node query_unity_logs.js --count 20

# 查询最近 5 分钟的日志
node query_unity_logs.js --minutes 5

# 查询最近 1 小时的日志
node query_unity_logs.js --minutes 60
```

#### 关键词查询

```bash
# 模糊匹配（包含关键词）
node query_unity_logs.js --fuzzy "error"

# 精确匹配（完全相等）
node query_unity_logs.js --keyword "NullReferenceException"

# 正则表达式匹配
node query_unity_logs.js --regex "Error.*player"
```

#### 按客户端筛选

```bash
# 只查询特定 Unity 项目的日志
node query_unity_logs.js --count 20 --client "MyProject"

# 多个项目同时接入时很有用
node query_unity_logs.js --count 20 --client "ProjectA"
```

#### 组合查询

```bash
# 最近 50 条包含 "player" 的日志
node query_unity_logs.js --count 50 --fuzzy "player"

# 最近 10 分钟的 Error 类型日志
node query_unity_logs.js --minutes 10 --fuzzy "Error"

# 特定项目最近 20 条日志
node query_unity_logs.js --count 20 --client "GameProject"
```

## 🤖 AI 工具配置

### Cursor AI 配置

将以下内容添加到项目根目录的 `.cursorrules` 文件：

```text
# Unity 日志查询工具

当调试 Unity 项目时：

1. 确保日志服务已启动：node unity_log_server.js
  注意!此服务端让用户自己手动启动!!!,用户没启动就友好提醒用户启动
2. 如果需要查询日志，先提示用户重现问题
3. 根据情况选择查询命令：
   - 最近错误：node query_unity_logs.js --count 20 --fuzzy "Error"
   - 最近日志：node query_unity_logs.js --count 50
   - 时间范围：node query_unity_logs.js --minutes 5
4. 如有多个 Unity 项目，使用 --client 筛选
```

### Windsurf / AGENTS.md 配置

```markdown
## Unity 日志查询

### 工作流程

1. 确认日志服务已运行（UnityLogServer）
2. 提示用户在 Unity 中重现问题
3. 执行查询命令获取日志
4. 分析日志并给出解决方案

### 常用命令

- 基础查询：`node query_unity_logs.js --count 20`
- 时间范围：`node query_unity_logs.js --minutes 5`
- 搜索错误：`node query_unity_logs.js --fuzzy "Error"`
- 特定项目：`node query_unity_logs.js --count 20 --client "ProjectName"`

### 重要提示

- 查询前必须先让用户触发日志操作（运行游戏、点击按钮等）
- 如遇连接失败，检查日志服务是否启动
- 最多缓存 200 条日志，太久之前的日志会被清理
```

### Claude / ChatGPT 等工具

```markdown
我有一个 Unity 日志查询工具，可以帮助调试 Unity 项目。

工具位置：

- 服务端：unity 日志 AI 友好查询/unity_log_server.js
- 查询工具：unity 日志 AI 友好查询/query_unity_logs.js

使用流程：

1. 启动日志服务（只需启动一次）
2. 配置 Unity 客户端（UnityLogClient.cs 放到 Assets/Editor/）
3. 提示用户在 Unity 中重现问题
4. 使用查询命令获取日志

常用查询：

- node query_unity_logs.js --count 20（最近 20 条）
- node query_unity_logs.js --minutes 5（最近 5 分钟）
- node query_unity_logs.js --fuzzy "error"（搜索错误）
```

## 📊 查询参数说明

| 参数        | 说明                     | 范围  | 示例                      |
| ----------- | ------------------------ | ----- | ------------------------- |
| `--count`   | 查询最近 n 条日志        | 1-200 | `--count 20`              |
| `--minutes` | 查询最近 n 分钟的日志    | 1-60  | `--minutes 5`             |
| `--keyword` | 精确匹配关键词           | -     | `--keyword "Error"`       |
| `--fuzzy`   | 模糊匹配关键词（包含）   | -     | `--fuzzy "error"`         |
| `--regex`   | 正则表达式匹配           | -     | `--regex "Error.*player"` |
| `--client`  | 按客户端筛选（模糊匹配） | -     | `--client "MyProject"`    |

**组合规则：**

- `--count` 和 `--minutes` 不能同时使用
- `--keyword`、`--fuzzy`、`--regex` 不能同时使用
- 可以组合：数量/时间 + 关键词匹配 + 客户端筛选
- `--client` 可以与任何参数组合使用

**示例：**

- ✅ `--count 20 --fuzzy "error" --client "ProjectA"`
- ✅ `--minutes 5 --regex "Error.*player"`
- ❌ `--count 20 --minutes 5`（冲突）
- ❌ `--fuzzy "error" --regex "Error"`（冲突）

## 🔧 故障排查

### 问题 1：Unity 无法连接到日志服务

**症状：**
Unity Console 显示 `[UnityLogClient] Connection lost, will reconnect...`

**解决方案：**

1. 确认日志服务已启动：`node unity_log_server.js`
2. 检查端口 6800 是否被占用
3. 确认 `UnityLogClient.cs` 在 `Assets/Editor/` 目录
4. 在 Unity 中使用 `Tools > UnityLogClient > Status` 查看连接状态

### 问题 2：查询连接失败

**症状：**
查询时显示 `❌ 错误: 连接错误: ECONNREFUSED`

**解决方案：**

1. 确认日志服务正在运行
2. 检查端口 6800 是否可访问
3. 尝试重启日志服务

### 问题 3：查询不到日志

**症状：**
查询显示 `没有找到匹配的日志`

**可能原因：**

1. 查询前未触发日志（未运行游戏、未点击按钮等）
2. 关键词拼写错误
3. 日志已被清理（最多保留 200 条）
4. 多项目时未指定正确的 `--client`

**解决方案：**

- 提示用户先在 Unity 中重现问题
- 使用更宽泛的查询条件（如 `--count 50`）
- 检查 `--client` 参数是否正确

### 问题 4：日志服务端口被占用

**症状：**
启动服务时显示 `Port 6800 is already in use`

**解决方案：**

1. 查找占用端口的进程并关闭：

   ```bash
   # Windows
   netstat -ano | findstr :6800
   taskkill /PID <进程ID> /F

   # Linux/Mac
   lsof -i :6800
   kill -9 <进程ID>
   ```

2. 或修改 `unity_log_server.js` 中的 `PORT` 常量

## 💡 使用技巧

### 调试流程示例

**场景：用户报告游戏点击按钮后报错**

```
AI: 请先在 Unity 中点击该按钮重现错误，然后告诉我。

用户: 已重现。

AI: [执行查询]
node query_unity_logs.js --count 20 --fuzzy "Error"

[查询结果分析]
根据日志显示，错误发生在 ButtonController.cs 的第 45 行，
原因是对象为空引用。建议检查按钮的引用是否正确设置...
```

### 多项目同时开发

当同时开发多个 Unity 项目时，每个项目会自动注册为独立客户端：

```bash
# 查询所有项目的日志
node query_unity_logs.js --count 50

# 只查询 ProjectA 的日志
node query_unity_logs.js --count 20 --client "ProjectA"

# 只查询 ProjectB 的错误日志
node query_unity_logs.js --minutes 10 --fuzzy "Error" --client "ProjectB"
```

日志服务端会实时显示所有项目的日志，带客户端前缀标识：

```
[ProjectA] [Error]     [2026-01-14T15:00:00.000Z] NullReferenceException
[ProjectB] [Warning]  [2026-01-14T15:00:01.000Z] Asset not found
```

### Console 清空同步

当你在 Unity Console 中点击 "Clear" 清空日志时，服务端会自动同步清空该客户端的日志缓存，保持数据一致性。

## 📁 文件说明

- **UnityLogClient.cs** - Unity 端日志客户端，自动捕获并发送日志
- **unity_log_server.js** - Node.js 日志服务端，接收、缓存、提供查询
- **query_unity_logs.js** - Node.js 查询工具，从服务端获取日志

**推荐使用方式：** 将这三个文件复制到你的 Unity 项目根目录，方便统一管理：

- `unity_log_server.js` - 日志服务，放在项目根目录启动
- `query_unity_logs.js` - 查询工具，放在项目根目录直接调用
- `UnityLogClient.cs` - Unity 插件，放在 `Assets/Editor/` 目录

## 🎯 开始使用

1. **复制文件**：
   - 将 `unity_log_server.js` 复制到项目根目录
   - 将 `query_unity_logs.js` 复制到项目根目录
   - 将 `UnityLogClient.cs` 复制到 `Assets/Editor/`
2. **启动服务**：`node unity_log_server.js`
3. **打开 Unity**：插件自动连接
4. **配置 AI 工具**：添加上述配置到你的 AI 工具
5. **开始调试**：让 AI 帮你分析日志！

## ⚠️ 重要提示

- **日志服务必须先启动**，Unity 客户端才能连接
- 日志最多缓存 200 条，定期清空可释放内存
- 多个项目会自动区分，查询时可按项目筛选
- Console 清空操作会同步到服务端
- 建议将查询工具路径加入 AI 工具配置，方便快速调用

---

**提示：** 将"AI 工具配置"部分复制到你的 AI 编程助手配置文件中，即可快速启用日志查询功能！
