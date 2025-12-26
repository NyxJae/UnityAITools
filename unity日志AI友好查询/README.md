# Unity 日志 AI 友好查询工具

让 AI 编程助手轻松读取 Unity 日志，提升调试效率。

## 🎯 快速开始

### 1. 安装 Node.js（如未安装）

查询工具需要 Node.js 环境。如果尚未安装，请访问 nodejs.org 下载并安装。

**验证安装：**

```bash
node --version
```

### 2. 安装 Unity 插件

将 `UnityLogServer.cs` 放到 Unity 项目的 `Assets/Editor/` 文件夹中。

插件会随 Unity 编辑器自动启动，无需手动配置。

### 3. 添加查询脚本到项目根目录（推荐）

**建议将 `query_unity_logs.js` 复制到你的 Unity 项目根目录下**，这样调用更方便：

```bash
# 复制脚本到项目根目录
cp unity日志AI友好查询/query_unity_logs.js ./query_unity_logs.js
```

复制后，可以在项目根目录直接运行查询命令，无需输入长路径。

### 4. 查询日志（命令行）

```bash
# 查询最近 20 条日志
node query_unity_logs.js --count 20

# 查询最近 5 分钟的日志
node query_unity_logs.js --minutes 5

# 查询包含 "error" 的日志（模糊匹配）
node query_unity_logs.js --fuzzy "error"

# 查询包含 "Error" 的日志（精确匹配）
node query_unity_logs.js --keyword "Error"

# 使用正则表达式查询
node query_unity_logs.js --regex "Error.*player"
```

### 5. 查询日志（AI 助手）

将以下配置添加到你的 AI 编程助手的配置文件中（如 Cursor 的 `.cursorrules`、Windsurf 的 `AGENTS.md` 等）：

---

## 🤖 接入 AI 编程工具

### 场景 1：Unity 报错时，让 AI 直接查看日志

**配置示例（添加到 AGENTS.md 或类似文件）：**

```markdown
## Unity 日志查询

当 Unity 项目出现错误或异常时，使用以下命令获取日志：

### 基础查询

- 查询最近 20 条日志：`node ./query_unity_logs.js --count 20`
- 查询最近 5 分钟的日志：`node ./query_unity_logs.js --minutes 5`

### 关键词查询

- 模糊搜索错误：`node ./query_unity_logs.js --fuzzy "error"`
- 精确匹配：`node ./query_unity_logs.js --keyword "NullReferenceException"`

### 组合查询

- 最近 50 条包含 "player" 的日志：`node ./query_unity_logs.js --count 50 --fuzzy "player"`

### ⚠️ 重要提示

在查询日志前，必须先提示用户手动触发需要查看的日志操作（如运行游戏、点击按钮等），否则可能查询不到相关日志。

### 工作流程

1. 用户报告 Unity 错误
2. 提示用户："请先在 Unity 中重现错误，然后告诉我"
3. 用户重现错误后，执行日志查询命令
4. 根据日志内容分析和解决问题
```

### 场景 2：Cursor AI 配置

将以下内容添加到项目根目录的 `.cursorrules` 文件：

```text
# Unity 日志查询规则

当需要调试 Unity 项目时：

1. 如果用户报告错误或异常，先询问用户："请先在 Unity 中重现这个问题，然后我再查询日志"

2. 根据情况选择合适的查询命令：
   - 一般错误：node ./query_unity_logs.js --count 20
   - 最近的问题：node ./query_unity_logs.js --minutes 5
   - 特定错误：node ./query_unity_logs.js --fuzzy "错误关键词"

3. 查询命令位于：./query_unity_logs.js（已复制到项目根目录）

4. 日志最多保留 200 条，查询范围：1-200 条或 1-60 分钟
```

### 场景 3：其他 AI 工具

对于任何支持自定义指令的 AI 编程工具，添加类似的配置：

```markdown
### Unity 日志查询工具

我有一个 Unity 日志查询工具，可以实时获取 Unity 编辑器的日志。

**工具位置：** `./query_unity_logs.js`（已复制到项目根目录）

**使用场景：**

- Unity 报错时，需要查看具体错误信息
- 调试时需要查看运行时日志
- 分析异常堆栈

**使用流程：**

1. 提示用户在 Unity 中重现问题
2. 执行查询命令获取日志
3. 分析日志内容并给出解决方案

**常用命令：**

- `node ./query_unity_logs.js --count 20` - 最近 20 条
- `node ./query_unity_logs.js --minutes 5` - 最近 5 分钟
- `node ./query_unity_logs.js --fuzzy "error"` - 搜索错误
```

---

## 📋 查询参数说明

| 参数        | 说明                  | 范围  | 示例                      |
| ----------- | --------------------- | ----- | ------------------------- |
| `--count`   | 查询最近 n 条日志     | 1-200 | `--count 20`              |
| `--minutes` | 查询最近 n 分钟的日志 | 1-60  | `--minutes 5`             |
| `--keyword` | 精确匹配关键词        | -     | `--keyword "Error"`       |
| `--fuzzy`   | 模糊匹配关键词        | -     | `--fuzzy "error"`         |
| `--regex`   | 正则表达式匹配        | -     | `--regex "Error.*player"` |

**组合规则：**

- `--count` 和 `--minutes` 不能同时使用
- `--keyword`、`--fuzzy`、`--regex` 不能同时使用
- 可以组合使用：数量/时间 + 关键词匹配

---

## 💡 使用技巧

### 调试流程示例

**用户：** "我的游戏点击播放按钮后报错了"

**AI 的正确响应：**

```
请先在 Unity 中点击播放按钮重现错误，然后我会查询日志帮你分析问题。
```

**用户重现后：**

```
node ./query_unity_logs.js --count 20
```

**分析日志后：**

```
根据日志显示，错误是 NullReferenceException，发生在 PlayerController.cs 的第 45 行...
```

### 高级查询

```bash
# 查找所有警告
node query_unity_logs.js --fuzzy "Warning"

# 查找特定脚本的错误
node query_unity_logs.js --regex "PlayerController.*Error"

# 最近 10 分钟的所有日志
node query_unity_logs.js --minutes 10
```

---

## ⚙️ 工作原理

- **Unity 端：** `UnityLogServer.cs` 随 Unity 编辑器启动，自动捕获日志并提供 TCP 服务
- **查询端：** `query_unity_logs.js` 通过 TCP 连接获取日志数据
- **日志容量：** 最多保留 200 条，超出后自动移除最早的日志
- **端口配置：** 默认端口 6800，如被占用会自动选择 6801-6999 之间的可用端口

---

## 🔧 故障排查

### 查询失败

如果查询时提示连接错误：

1. 确认 Unity 编辑器正在运行
2. 检查 `UnityLogServer.cs` 是否正确放置在 `Assets/Editor/` 目录
3. 查看 Unity Console 是否有 `[UnityLogServer]` 相关日志
4. 端口文件位置：`~/.unitylog_port.txt`（用户主目录）

### 查询不到日志

- 确保在查询前已经触发了日志（运行游戏、点击按钮等）
- 日志最多保留 200 条，时间太久之前的日志会被自动清理
- 检查查询的关键词是否正确

---

## 📄 文件说明

- `UnityLogServer.cs` - Unity 编辑器插件，自动捕获日志
- `query_unity_logs.js` - Node.js 查询工具

---

## 🎉 开始使用

1. 安装 Node.js（如未安装）
2. 将 `UnityLogServer.cs` 放到 Unity 项目的 `Assets/Editor/` 文件夹
3. 将 `query_unity_logs.js` 复制到你的 Unity 项目根目录
4. 打开 Unity 编辑器（插件会自动启动）
5. 在 AI 工具配置中添加上述配置
6. 开始让 AI 帮你调试 Unity 项目！

---

**提示：** 将此 README 的"接入 AI 编程工具"部分复制到你的 AI 工具配置文件中，即可快速启用。
