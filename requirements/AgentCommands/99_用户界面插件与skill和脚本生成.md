# 需求文档 - Unity 技能导出插件

## 1. 项目现状与核心目标

### 1.1 现状

项目中已有完整的 Jason 命令监听处理系统，核心插件位于 `Code/Assets/Editor/AgentCommands/`，该插件使用 `FileSystemWatcher` 监听三个子文件夹：

- `pending/` - 输入队列，外部工具写入命令 JSON 的位置
- `results/` - 输出结果，最多保留最近 20 条结果
- `done/` - 归档备份，处理完的命令文件移动到这里

目前已实现的命令类型包括：

- `log.query` - 日志查询
- `prefab.queryHierarchy` - 预制体层级查询
- `prefab.queryComponents` - 预制体组件查询

同时，项目中已手动创建了两个技能文件夹在 `.snow/skills/` 目录下：

- `unity-log-query/` - 包含 SKILL.md 说明文档
- `unity-prefab-view/` - 包含 SKILL.md 说明文档
- `xlsx-viewer/` - 作为参考，展示了技能文件夹的标准结构（包含 SKILL.md 和 scripts/子文件夹）

### 1.2 核心目标

开发一个 Unity Editor 用户交互插件，主要有两个目的：

**目的 1：方便生成和管理 AI 工具所需的技能**

- 提供友好的用户界面，让用户可以一键导出配置好的技能
- 每个技能以独立文件夹形式导出，包含完整的说明文档和通用 Python 脚本
- 便于后续用户更新技能内容

**目的 2：提供 Python 脚本，简化和规范化 Unity AgentCommands 插件的使用**

- 不需要手动去寻找命令 JSON 文件发布的文件夹
- 不需要手动去结果文件夹中查找结果文件是否生成
- Python 脚本自动处理输入和输出流程

## 2. 范围与边界

### 2.1 功能点简述

**主功能点：**

- [ ] 在 Unity Editor 顶部菜单栏添加菜单项 `Tools/Unity-skills`
- [ ] 点击菜单项打开独立的弹窗界面
- [ ] 提供导出路径选择功能（通过 EditorPrefs 保存，按 Unity 项目为单位存储）
- [ ] 显示所有可用技能的列表，支持勾选选择
- [ ] 提供全选/取消全选按钮
- [ ] 提供导出选中技能按钮
- [ ] 导出时，如果指定文件夹下有 skills 子文件夹，则在 skills 文件夹中生成技能文件夹；如果没有则创建 skills 文件夹
- [ ] 如果存在同名技能文件夹，直接覆盖
- [ ] 每个导出的技能文件夹包含：SKILL.md 文件 + scripts/子文件夹 + Python 脚本

**技能配置管理：**

- [ ] 每个技能一个独立的 C#配置文件，位于 `Assets/Editor/AgentCommands/SkillConfigs/` 目录
- [ ] 配置文件只包含 SKILL.md 的完整字符串内容
- [ ] 代码中有集中配置的地方，统一管理所有技能配置
- [ ] 新增技能时，只需新建一个 C#配置文件并在集中配置中添加几行代码

**Python 脚本功能：**

- [ ] 接收输入 JSON 并保存到 pending 目录
- [ ] 使用 batchId 作为 JSON 文件名（简单正则提取或直接从 JSON 读取）
- [ ] 轮询 results 目录获取结果文件
- [ ] 根据 batchId 和生成时间筛选结果（生成时间与当前时间小于 3 秒的才认定为新结果）
- [ ] 读取并返回结果 JSON
- [ ] 使用占位符 `{AGENT_COMMANDS_DATA_DIR}` 表示 AgentCommands 目录，生成时替换为实际路径
- [ ] 超时处理：如果 30 秒内没有找到符合条件的结果，抛出 TimeoutError 异常
- [ ] 不对输入输出 JSON 做任何校验（除了 batchId 缺失的情况），Unity 插件内部已做完整处理
- [ ] 支持直接以命令行参数形式使用（推荐），JSON 字符串作为命令行参数传入
- [ ] 同时支持在 Python 代码中导入并调用 execute_command 函数

**排除项：**

- 不需要支持技能 SKILL.md 的可视化编辑（直接修改 C#配置文件）
- 不需要支持 Python 脚本的复杂参数校验（只提取 batchId 即可）
- 不需要支持批量技能的增量导出（同名直接覆盖）

### 2.2 技术约束

- 使用 `[InitializeOnLoad]` 特性实现编辑器启动时自动运行
- 使用 `EditorPrefs` 保存导出路径配置（按 Unity 项目为单位）
- Python 脚本使用 `time.time()` 计算生成时间差
- Python 脚本使用 `glob` 模式匹配文件名
- Python 脚本轮询间隔建议为 0.5 秒

## 3. 界面设计

### 3.1 布局结构

```
┌─────────────────────────────────────────────────┐
│  Unity Skills Exporter                          │
├─────────────────────────────────────────────────┤
│  导出路径: C:/Users/xxx/skills  [修改按钮]      │
├─────────────────────────────────────────────────┤
│  [☑] 全选  [ ] 取消全选                         │
├─────────────────────────────────────────────────┤
│  ☑ unity-log-query      Unity日志查询技能        │
│  ☑ unity-prefab-view    Unity预制体查看技能       │
│  ☐ [其他技能...]                              │
├─────────────────────────────────────────────────┤
│            [  导出选中技能  ]                   │
└─────────────────────────────────────────────────┘
```

### 3.2 交互流程

1. 用户点击 `Tools/Unity-skills` 菜单项
2. 弹窗显示，自动加载上次保存的导出路径
3. 用户可以点击「修改按钮」选择新的导出路径
4. 用户通过勾选框选择要导出的技能
5. 用户可以点击「全选」或「取消全选」快速操作
6. 用户点击「导出选中技能」按钮
7. 插件检查导出路径，自动创建 skills 文件夹（如果不存在）
8. 对每个选中的技能，生成对应的技能文件夹
9. 如果已存在同名技能文件夹，直接覆盖
10. 显示导出完成提示

## 4. 技术实现要点

### 4.1 技能配置文件结构

每个技能对应一个 C#配置文件，例如 `SkillConfig_LogQuery.cs`。

**SKILL.md 内容来源说明**：开发者需要从现有 `.snow/skills/` 目录下读取对应的 SKILL.md 文件内容，将其完整内容复制并赋值到 `SkillMarkdown` 字符串常量中。例如，对于 `unity-log-query` 技能，需要读取 `F:/UnityProject/SL/SL_402/.snow/skills/unity-log-query/SKILL.md` 文件内容。

```csharp
// Assets/Editor/AgentCommands/SkillConfigs/SkillConfig_LogQuery.cs
public static class SkillConfig_LogQuery
{
    public const string SkillName = "unity-log-query";
    public const string SkillDescription = "Unity日志查询技能";

    // SKILL.md的完整内容
    // 开发者需要从 .snow/skills/unity-log-query/SKILL.md 文件中读取并复制完整内容
    public const string SkillMarkdown = @"---
name: unity-log-query
description: 查询 Unity 编辑器日志. 触发关键词:Unity:日志,Unity log
---

# Unity Log Query

... (完整的SKILL.md内容，从现有文件中复制)
";
}
```

### 4.2 集中配置管理

在 `SkillConfigsRegistry.cs` 中统一管理所有技能：

```csharp
// Assets/Editor/AgentCommands/SkillConfigs/SkillConfigsRegistry.cs
public static class SkillConfigsRegistry
{
    private static readonly Dictionary<string, SkillConfig> AllSkills = new Dictionary<string, SkillConfig>
    {
        { SkillConfig_LogQuery.SkillName, new SkillConfig
            {
                Name = SkillConfig_LogQuery.SkillName,
                Description = SkillConfig_LogQuery.SkillDescription,
                Markdown = SkillConfig_LogQuery.SkillMarkdown
            }
        },
        { SkillConfig_PrefabView.SkillName, new SkillConfig
            {
                Name = SkillConfig_PrefabView.SkillName,
                Description = SkillConfig_PrefabView.SkillDescription,
                Markdown = SkillConfig_PrefabView.SkillMarkdown
            }
        }
        // 新增技能时，在这里添加配置
    };

    public static IEnumerable<SkillConfig> GetAllSkills()
    {
        return AllSkills.Values;
    }
}
```

### 4.3 Python 脚本模板

所有技能使用相同的 Python 脚本模板，位于集中配置的字符串常量中：

```python
import json
import os
import sys
import glob
import time

# 占位符，生成时会被替换为实际路径，例如：F:/UnityProject/SL/SL_402/Code/Assets/AgentCommands
AGENT_COMMANDS_DATA_DIR = "{AGENT_COMMANDS_DATA_DIR}"

PENDING_DIR = os.path.join(AGENT_COMMANDS_DATA_DIR, "pending")
RESULTS_DIR = os.path.join(AGENT_COMMANDS_DATA_DIR, "results")
DONE_DIR = os.path.join(AGENT_COMMANDS_DATA_DIR, "done")

TIMEOUT = 30 # 超时时间（秒）
POLL_INTERVAL = 0.5 # 轮询间隔（秒）
MAX_RESULT_AGE = 3 # 结果文件最大年龄（秒）

def execute_command(input_json):
    """
    执行命令并返回结果

    Args:
        input_json: 输入JSON字符串或字典，必须包含batchId字段

    Returns:
        结果JSON字典

    Raises:
        TimeoutError: 超时未找到结果文件
        ValueError: batchId缺失
    """
    # 解析输入
    if isinstance(input_json, str):
        data = json.loads(input_json)
    else:
        data = input_json

    # 提取batchId（简单提取，不做其他校验）
    batch_id = data.get("batchId")
    if not batch_id:
        raise ValueError("Missing required field: batchId")

    # 写入pending目录
    pending_file = os.path.join(PENDING_DIR, f"{batch_id}.json")
    with open(pending_file, 'w', encoding='utf-8') as f:
        json.dump(data, f, ensure_ascii=False, indent=2)

    # 轮询results目录
    start_time = time.time()
    while time.time() - start_time < TIMEOUT:
        # 查找结果文件
        pattern = os.path.join(RESULTS_DIR, f"{batch_id}.json")
        result_files = glob.glob(pattern)

        for result_file in result_files:
            # 检查文件生成时间
            file_time = os.path.getmtime(result_file)
            if time.time() - file_time <= MAX_RESULT_AGE:
                # 读取结果
                with open(result_file, 'r', encoding='utf-8') as f:
                    result = json.load(f)

                # 检查状态
                status = result.get("status")
                if status in ["completed", "error"]:
                    return result

        # 等待后再次轮询
        time.sleep(POLL_INTERVAL)

    # 超时处理
    raise TimeoutError(f"Timeout after {TIMEOUT} seconds. No result found for batchId: {batch_id}")

# 命令行入口
if __name__ == "__main__":
    # 检查命令行参数
    if len(sys.argv) > 1:
        # 从命令行参数获取JSON字符串（推荐方式）
        # 示例: python execute_unity_command.py '{"batchId":"batch_001","commands":[...]}'
        input_json_str = " ".join(sys.argv[1:])
    else:
        # 示例用法（当没有参数时）
        example_input = {
            "batchId": "batch_log_001",
            "timeout": 30000,
            "commands": [{
                "id": "cmd_001",
                "type": "log.query",
                "params": {
                    "n": 50,
                    "level": "Error"
                }
            }]
        }
        print("Usage: python execute_unity_command.py '<JSON_STRING>'")
        print("Example:")
        example_json = json.dumps(example_input, ensure_ascii=False)
        print(f"  python execute_unity_command.py '{example_json}'")
        sys.exit(1)

    try:
        # 执行命令
        result = execute_command(input_json_str)

        # 输出结果（JSON格式，便于解析）
        print(json.dumps(result, indent=2, ensure_ascii=False))

    except TimeoutError as e:
        print(f"Error: {e}", file=sys.stderr)
        sys.exit(1)
    except ValueError as e:
        print(f"Input error: {e}", file=sys.stderr)
        sys.exit(1)
    except Exception as e:
        print(f"Unexpected error: {e}", file=sys.stderr)
        sys.exit(1)

# 在Python代码中导入并调用（推荐用于复杂场景）：
# from execute_unity_command import execute_command
# result = execute_command({"batchId":"batch_001","commands":[...]})
```

### 4.4 路径替换逻辑

生成 Python 脚本时，需要将占位符 `{AGENT_COMMANDS_DATA_DIR}` 替换为实际的 AgentCommands 目录路径：

```csharp
string agentCommandsDir = "F:/UnityProject/SL/SL_402/Code/Assets/AgentCommands";
string pythonScriptContent = SkillConfigsRegistry.PythonScriptTemplate;
string replacedContent = pythonScriptContent.Replace("{AGENT_COMMANDS_DATA_DIR}", agentCommandsDir);
```

## 5. 举例覆盖需求和边缘情况

### 例 1：首次使用，设置导出路径并导出所有技能

**场景**：用户第一次打开插件，导出路径未设置，导出目标文件夹不存在。

**操作步骤**：

1. 用户点击 `Tools/Unity-skills` 菜单项
2. 弹窗显示，导出路径显示为空或默认值
3. 用户点击「修改按钮」，选择路径 `C:/Users/xxx/.snow/skills`
4. 用户点击「全选」按钮，选中所有技能（共 2 个：unity-log-query, unity-prefab-view）
5. 用户点击「导出选中技能」按钮
6. 插件创建 `C:/Users/xxx/.snow/skills/skills/` 文件夹
7. 插件生成 `unity-log-query/` 文件夹，包含：
   - `SKILL.md` - 完整的技能说明文档
   - `scripts/execute_unity_command.py` - Python 脚本（已替换路径）
8. 插件生成 `unity-prefab-view/` 文件夹，包含：
   - `SKILL.md` - 完整的技能说明文档
   - `scripts/execute_unity_command.py` - Python 脚本（已替换路径）
9. 插件显示"导出成功：2 个技能"提示

**最终目录结构**：

```
C:/Users/xxx/.snow/skills/
└── skills/
    ├── unity-log-query/
    │   ├── SKILL.md
    │   └── scripts/
    │       └── execute_unity_command.py
    └── unity-prefab-view/
        ├── SKILL.md
        └── scripts/
            └── execute_unity_command.py
```

### 例 2：部分导出技能

**场景**：用户只想导出部分技能，不需要全部导出。

**操作步骤**：

1. 用户点击 `Tools/Unity-skills` 菜单项
2. 弹窗显示，导出路径已保存为 `C:/Users/xxx/.snow/skills`
3. 用户取消勾选 `unity-prefab-view`
4. 用户只保留 `unity-log-query` 的勾选
5. 用户点击「导出选中技能」按钮
6. 插件只生成 `unity-log-query/` 文件夹

### 例 3：覆盖已存在的技能文件夹

**场景**：目标文件夹下已存在同名技能文件夹，需要更新内容。

**当前状态**：

```
C:/Users/xxx/.snow/skills/
└── skills/
    └── unity-log-query/
        ├── SKILL.md (旧版本)
        └── scripts/
            └── execute_unity_command.py (旧版本)
```

**操作步骤**：

1. 用户选中 `unity-log-query` 技能
2. 用户点击「导出选中技能」按钮
3. 插件检测到 `C:/Users/xxx/.snow/skills/skills/unity-log-query/` 已存在
4. 插件直接覆盖该文件夹，不提示警告
5. 新的 `SKILL.md` 和 Python 脚本替换旧文件

### 例 4：修改导出路径

**场景**：用户想将技能导出到不同的位置。

**操作步骤**：

1. 用户点击 `Tools/Unity-skills` 菜单项
2. 当前导出路径显示为 `C:/Users/xxx/.snow/skills`
3. 用户点击「修改按钮」，选择新路径 `D:/Dev/snow-skills`
4. 用户选中所有技能并导出
5. 插件在 `D:/Dev/snow-skills/skills/` 下生成技能文件夹
6. 插件将新路径 `D:/Dev/snow-skills` 保存到 EditorPrefs
7. 下次打开插件时，自动加载新路径

### 例 5：Python 脚本命令行调用示例

**场景**：外部 AI 工具通过命令行直接调用 Python 脚本,无需创建额外的 Python 文件。

**命令行调用示例**：

```bash
# 方式1: 直接在命令行中传入JSON字符串
python C:/Users/xxx/.snow/skills/skills/unity-log-query/scripts/execute_unity_command.py '{"batchId":"batch_log_001","timeout":30000,"commands":[{"id":"cmd_001","type":"log.query","params":{"n":50,"level":"Error"}}]}'

# 方式2: 使用JSON变量
set JSON_INPUT='{"batchId":"batch_prefab_001","timeout":30000,"commands":[{"id":"cmd_001","type":"prefab.queryHierarchy","params":{"prefabPath":"Assets/Resources/Prefabs/DialogMain.prefab","includeInactive":true}}]}'
python C:/Users/xxx/.snow/skills/skills/unity-prefab-view/scripts/execute_unity_command.py %JSON_INPUT%
```

**返回结果**：

```json
{
  "batchId": "batch_log_001",
  "status": "completed",
  "startedAt": "2026-01-30T02:15:00Z",
  "finishedAt": "2026-01-30T02:15:02Z",
  "results": [
    {
      "id": "cmd_001",
      "type": "log.query",
      "status": "success",
      "result": {
        "items": [...],
        "totalCaptured": 150,
        "returned": 50
      }
    }
  ],
  "totalCommands": 1,
  "successCount": 1,
  "failedCount": 0
}
```

**Python 代码中导入调用（复杂场景推荐）**：

```python
from scripts.execute_unity_command import execute_command
import json

# 准备输入
input_data = {
 "batchId": "batch_prefab_001",
 "timeout": 30000,
 "commands": [{
 "id": "cmd_001",
 "type": "prefab.queryHierarchy",
 "params": {
 "prefabPath": "Assets/Resources/Prefabs/DialogMain.prefab",
 "includeInactive": True,
 "maxDepth": -1
 }
 }]
}

try:
 # 执行命令
 result = execute_command(input_data)

 # 检查结果状态
 if result["status"] == "completed":
 print("Command executed successfully")
 for cmd_result in result["results"]:
 if cmd_result["status"] == "success":
 print(f"Hierarchy has {cmd_result['result']['totalGameObjects']} objects")
 else:
 print(f"Command {cmd_result['id']} failed")
 else:
 print(f"Batch failed: {result.get('error', {}).get('message', 'Unknown error')}")

except TimeoutError as e:
 print(f"Timeout: {e}")
except ValueError as e:
 print(f"Invalid input: {e}")
```

### 例 6：新增技能配置

**场景**：开发者需要新增一个名为 `unity-config-view` 的技能。

**操作步骤**：

1. 在 `Assets/Editor/AgentCommands/SkillConfigs/` 目录下新建 `SkillConfig_ConfigView.cs`
2. 编写配置文件内容，包含 SkillName, SkillDescription, SkillMarkdown
3. 在 `SkillConfigsRegistry.cs` 的 `AllSkills` 字典中添加配置：
   ```csharp
   { SkillConfig_ConfigView.SkillName, new SkillConfig
       {
           Name = SkillConfig_ConfigView.SkillName,
           Description = SkillConfig_ConfigView.SkillDescription,
           Markdown = SkillConfig_ConfigView.SkillMarkdown
       }
   }
   ```
4. 重新打开 Unity 编辑器，插件会自动加载新技能
5. 用户可以在导出界面中看到 `unity-config-view` 技能并导出

### 例 7：轮询等待结果的时间筛选

**场景**：Python 脚本需要正确识别新生成的结果文件，避免读取旧的同名文件。

**说明**：

- 假设 results 目录下已有一个旧的 `batch_log_001.json` 文件，生成时间为 10 分钟前
- 用户发送新的命令，batchId 也是 `batch_log_001`
- Python 脚本会将新命令写入 `pending/batch_log_001.json`
- Unity 插件处理后，生成新的结果文件到 `results/batch_log_001.json`
- 由于同名文件被覆盖，文件的修改时间更新为当前时间
- Python 脚本检测到文件修改时间与当前时间差小于 3 秒，认定为新结果
- 读取并返回结果

**关键代码**：

```python
file_time = os.path.getmtime(result_file)  # 获取文件修改时间
if time.time() - file_time <= MAX_RESULT_AGE:  # MAX_RESULT_AGE = 3
    # 认定为新生成的结果
    with open(result_file, 'r', encoding='utf-8') as f:
        result = json.load(f)
    return result
```

### 例 8：超时处理场景

**场景**：Unity 插件未正常运行或处理时间过长，导致 30 秒内未生成结果。

**操作流程**：

1. 用户 Python 脚本执行 `execute_command(input_data)`
2. 脚本将命令写入 `pending/batch_log_001.json`
3. 脚本开始轮询 `results/batch_log_001.json`
4. 30 秒过去了，一直没有找到生成时间小于 3 秒的结果文件
5. 脚本抛出 `TimeoutError` 异常
6. 用户捕获异常，得到错误信息：`Timeout after 30 seconds. No result found for batchId: batch_log_001`
7. 用户检查 Unity 插件是否正常运行，检查 pending 目录文件是否被处理

### 例 9：EditorPrefs 按项目存储

**场景**：用户在两个不同的 Unity 项目中分别设置了不同的导出路径。

**Project A**（路径：`F:/UnityProject/SL/SL_402`）：

- 设置导出路径：`C:/Work/SL/skills`
- EditorPrefs 存储：`UnitySkillsExporter.ExportPath_F:/UnityProject/SL/SL_402` = `C:/Work/SL/skills`

**Project B**（路径：`D:/Dev/NewProject`）：

- 设置导出路径：`D:/Dev/skills`
- EditorPrefs 存储：`UnitySkillsExporter.ExportPath_D:/Dev/NewProject` = `D:/Dev/skills`

**说明**：

- 使用 Unity 项目的完整路径作为 EditorPrefs 的 key 后缀
- 不同项目的配置互不干扰
- 同一台电脑上的不同开发者也有独立的 EditorPrefs

### 例 10：导出目标文件夹已有 skills 子文件夹

**场景**：用户选择的导出路径下已经存在 skills 文件夹，但不包含任何技能文件夹。

**当前状态**：

```
D:/Dev/snow-skills/
└── skills/  (空文件夹)
```

**操作步骤**：

1. 用户选择导出路径为 `D:/Dev/snow-skills`
2. 用户选中技能并导出
3. 插件检测到 `D:/Dev/snow-skills/skills/` 已存在
4. 插件直接在 `skills/` 下生成技能文件夹
5. 最终结构：
   ```
   D:/Dev/snow-skills/
   └── skills/
       ├── unity-log-query/
       │   ├── SKILL.md
       │   └── scripts/
       │       └── execute_unity_command.py
       └── unity-prefab-view/
           ├── SKILL.md
           └── scripts/
               └── execute_unity_command.py
   ```

### 例 11：导出目标文件夹不存在

**场景**：用户选择一个完全不存在的文件夹路径。

**操作步骤**：

1. 用户选择导出路径为 `D:/NonExistent/Path/skills`
2. 用户选中技能并导出
3. 插件检测到 `D:/NonExistent/Path/skills` 不存在
4. 插件自动创建 `D:/NonExistent/Path/skills/` 目录
5. 插件自动创建 `D:/NonExistent/Path/skills/skills/` 子目录
6. 插件在 `skills/` 下生成技能文件夹

## 6. 与现有系统的集成

### 6.1 目录结构

新增的插件文件结构：

```
Code/Assets/Editor/AgentCommands/
├── SkillsExporter/
│   ├── SkillsExporterWindow.cs        # 主窗口类
│   ├── SkillsExporterMenuItem.cs      # 菜单项
│   └── SkillConfigs/                  # 技能配置目录
│       ├── SkillConfigsRegistry.cs    # 集中配置
│       ├── SkillConfig_LogQuery.cs    # 日志查询技能配置
│       ├── SkillConfig_PrefabView.cs  # 预制体查看技能配置
│       └── PythonScriptTemplate.cs    # Python脚本模板常量
└── (现有的其他文件...)
```

### 6.2 EditorPrefs Key 命名规范

- 导出路径存储 Key 格式：`UnitySkillsExporter.ExportPath.<项目路径>`
- 示例：`UnitySkillsExporter.ExportPath.F:/UnityProject/SL/SL_402`

### 6.3 Python 脚本生成位置

所有技能的 Python 脚本都生成在技能文件夹的 `scripts/` 子目录下，文件名统一为 `execute_unity_command.py`。

## 7. 后续扩展性

### 7.1 新增技能的步骤

1. 在 `Assets/Editor/AgentCommands/SkillConfigs/` 下创建新的配置文件
2. 在配置文件中定义 SkillName, SkillDescription, SkillMarkdown
3. 在 `SkillConfigsRegistry.cs` 中注册新技能
4. 重启 Unity 编辑器或等待脚本重载
5. 技能自动出现在导出界面中

### 7.2 Python 脚本功能扩展

虽然目前需求要求 Python 脚本不做太多校验，但未来如果需要扩展功能（如添加参数校验、日志记录、错误重试等），只需要修改 `PythonScriptTemplate.cs` 中的模板内容即可。所有技能都会使用更新后的 Python 脚本。

## 8. 不包含的功能

- 不支持在 Unity Editor 界面中直接编辑 SKILL.md 内容（需要修改 C#配置文件）
- 不支持技能的批量导入或从外部文件夹读取配置
- 不支持 Python 脚本的复杂校验和错误处理（保持简单）
- 不支持技能之间的依赖关系管理
- 不支持技能版本控制（直接覆盖同名文件）

====================================已完成=============================
