# 需求文档 - Unity 技能导出插件

## 1. 项目现状与核心目标

### 1.1 现状

项目中已有完整的 Json 命令监听处理系统(插件代码位置不在本文档约束范围内).该插件使用 `FileSystemWatcher` 监听 UnityAgentSkills 命令数据目录下的三个子文件夹:

- `pending/` - 输入队列,外部工具写入命令 JSON 的位置
- `results/` - 输出结果,最多保留最近 20 条结果
- `done/` - 归档备份,处理完的命令文件移动到这里

其中,UnityAgentSkills 命令数据目录固定为: `Assets/UnityAgentSkills/Commands/`.

**插件化架构**: 采用反射插件系统，支持通过 `ICommandPlugin` 接口扩展命令。

系统提供的命令类型包括：

- `log.query` - 日志查询(内置命令,priority 0)
- `log.screenshot` - 日志截图(内置命令,priority 0)
- `prefab.queryHierarchy` - 预制体层级查询(Prefab 插件,priority 20)
- `prefab.queryComponents` - 预制体组件查询（Prefab 插件，Priority 20）
- `prefab.setGameObjectProperties` - GameObject 属性修改（Prefab 插件，Priority 20）
- `prefab.setComponentProperties` - 组件属性修改（Prefab 插件，Priority 20）

详见 `01_整体与框架需求.md` 第 6 节"模块化/可扩展性要求"。

同时，项目中已手动创建了部分技能文件夹在 `.snow/skills/` 目录下(示例)：

- `unity-log/` - 包含 SKILL.md 说明文档
- `unity-prefab-view/` - 包含 SKILL.md 说明文档
- `unity-prefab-edit/` - Unity 预制体编辑技能(低频使用,编辑前先查看)
- `xlsx-viewer/` - 作为参考，展示了技能文件夹的标准结构（包含 SKILL.md 和 scripts/子文件夹）

### 1.2 核心目标

开发一个 Unity Editor 用户交互插件，主要有两个目的：

**目的 1：方便生成和管理 AI 工具所需的技能**

- 提供友好的用户界面，让用户可以一键导出配置好的技能
- 每个技能以独立文件夹形式导出，包含完整的说明文档和通用 Python 脚本
- 便于后续用户更新技能内容

**目的 2：提供 Python 脚本，简化和规范化 UnityAgentSkills 插件的使用**

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
- [ ] 导出时,固定在导出路径下的 `skills/` 子目录中生成技能文件夹(若 `skills/` 不存在则自动创建).
- [ ] 如果存在同名技能文件夹，直接覆盖
- [ ] 每个导出的技能文件夹包含：SKILL.md 文件 + scripts/子文件夹 + Python 脚本

**技能配置管理：**

- [ ] 每个技能对应一份独立的 SkillConfig(实现可用 C# 文件/ScriptableObject/其他方式,本需求不约束存放目录).
- [ ] 每个 SkillConfig 必须包含 SKILL.md 的完整字符串内容(用于导出).
- [ ] 必须有一个"集中注册表"统一管理所有技能配置,UI 列表以注册表为准.
- [ ] 新增技能时,只需新增一个 SkillConfig 并将其注册到注册表,无需修改 UI 主流程.

**Python 脚本功能：**

- [ ] 接收输入 JSON 并保存到 pending 目录
- [ ] 使用 batchId 作为 JSON 文件名（简单正则提取或直接从 JSON 读取）
- [ ] 轮询 results 目录获取结果文件
- [ ] 根据 batchId 和生成时间筛选结果（生成时间与当前时间小于 3 秒的才认定为新结果）
- [ ] 导出时,脚本中需要注入 UnityAgentSkills 数据目录的绝对路径

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
│  导出路径: <ExportDir>  [修改按钮]              │
├─────────────────────────────────────────────────┤
│  [☑] 全选  [ ] 取消全选                         │
├─────────────────────────────────────────────────┤
│  ☑ unity-log            Unity日志技能(含查询,截图与刷新)   │
│  ☑ unity-prefab-view    Unity预制体查看技能       │
│  ☐ unity-prefab-edit    Unity预制体编辑技能(少用,编辑前先查看)  │
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

### 4.1 SkillConfig 与注册表(必须)

- 必须存在 SkillConfig 的"概念",用于描述每个 skill 的导出信息.
- SkillConfig 至少包含:
  - `SkillName`: skillId,用于 UI 展示与导出目录名.
  - `SkillDescription`: UI 列表描述文案.
  - `SkillMarkdown`: 导出文档内容,会写入 `<exportDir>/skills/<skillId>/SKILL.md`.
- SkillConfig 的具体存储形式与代码目录结构不在本需求约束范围内.
- 必须存在一个"集中注册表"统一管理所有 SkillConfig,SkillsExporter UI 的列表与导出行为都以该注册表为准.

### 4.2 Skills 列表来源与当前内置项

SkillsExporter UI 中可选择并导出的 skills 列表由代码配置决定(以 `SkillConfigsRegistry.cs` 注册项及 UI 过滤逻辑为准),可随版本增减.

当前内置项至少包含:

- `unity-log`
- `unity-prefab-view`
- `unity-prefab-edit`

使用场景划分(面向非专业人士):

- `unity-prefab-view`: 预制体查看为主,日常高频使用,仅包含 prefab 查看类命令.
- `unity-prefab-edit`: 预制体编辑为主,低频使用,仅包含 prefab 编辑类命令.编辑前必须先用 `unity-prefab-view` 获取 `rootPath`/`objectPath` 等定位信息.

命令范围(必须,避免误用):

- `unity-prefab-view` 包含的命令:
  - `prefab.queryHierarchy`
  - `prefab.queryComponents`
- `unity-prefab-edit` 包含的命令(所有编辑命令,不包含任何查看命令):
  - `prefab.setGameObjectProperties`
  - `prefab.deleteGameObject`
  - `prefab.moveOrCopyGameObject`
  - `prefab.createGameObject`
  - `prefab.addComponent`
  - `prefab.setComponentProperties`
  - `prefab.deleteComponent`
  - `prefab.renameGameObject`
  - `prefab.setSiblingIndex`
  - `prefab.setTransform`
  - `prefab.setRectTransform`
  - `prefab.batchEdit`

不提供任何旧 skillId 的 alias/兼容入口.

### 4.3 `unity-log` skill 的对外约束(必须)

- 正式 skillId: `unity-log`.

`unity-log` 的导出文档(SkillMarkdown,最终会导出为 SKILL.md)必须满足:

1. 必须同时包含三条命令的说明:
   - `log.query`
   - `log.screenshot`
   - `log.refresh`
2. 描述中必须包含至少以下触发关键词(用于非技术同学或外部系统通过关键词触发):
   - `Unity:日志`
   - `Unity log`
   - `Unity:截图`
   - `Unity screenshot`
   - `Unity:刷新`
   - `Unity refresh`
3. 对 `log.screenshot` 的说明必须覆盖以下信息:
   - 截图对象: Game 视图,包含 UI.
   - 输出位置: `Assets/UnityAgentSkills/Commands/results/`.
   - 返回结果: 返回 `imageAbsolutePath`(单张截图).
   - 重要语义: `status=success` 表示截图文件已真实落盘且可读.
4. 对 `log.refresh` 的说明必须覆盖以下信息:
   - 它是 `unity-log` 中的内置 `log.*` 命令,不是后台自动编译服务,也不是通用 `editor.*` 命令.
   - 核心动作: 显式触发一次 Unity 刷新,并等待到 Unity 不再处于刷新或编译状态.
   - `params` 允许为空对象 `{}`,不新增命令级 timeout 字段.
   - 成功结果至少包含 `summary`,`refreshTriggered`,`compilationOccurred`.
   - `summary` 需优先使用中文自然语言,让非专业人士直接可读.
   - 即使调用时 Unity 已在刷新或编译中,仍应再次显式触发刷新并继续等待到空闲.

- `log.screenshot` 的等待落盘必须不阻塞 Unity Editor 主进程.
- 允许 `log.screenshot` 的 results 从 processing 延迟后再 completed(直到文件可读才写 success).
- `log.refresh` 的等待完成也必须采用不阻塞 Unity Editor 主线程的跨帧推进模型.

### 4.4 SkillConfig 内容来源与导出规则(必须)

- 每个 skill 对应一个 SkillConfig 文件,包含:
  - `SkillName`: skillId,用于 UI 展示与导出目录名.
  - `SkillDescription`: UI 列表的描述文案.
  - `SkillMarkdown`: 导出文档内容,会写入 `<exportDir>/skills/<skillId>/SKILL.md`.
- `SkillMarkdown` 的内容来源:
  - 开发者从 `.snow/skills/<skillId>/SKILL.md` 读取完整内容,复制到 `SkillMarkdown` 常量中.

### 4.5 Python 脚本模板(必须)

- 所有 skills 使用同一份 Python 脚本模板.
- 导出时,每个 skill 的脚本固定生成在:
  - `<exportDir>/skills/<skillId>/scripts/execute_unity_command.py`
- 脚本必须满足的对外行为:
  - 能接收一个批量命令 JSON(字符串或对象),并提取 `batchId`.
  - 将该 JSON 写入 `pending/<batchId>.json`.
  - 轮询 `results/<batchId>.json`,直到状态为 `completed` 或 `error`.
  - 超过超时时间仍未得到最终状态时,抛出超时错误.
  - 为避免读到旧结果,轮询时需要用“结果文件生成时间距现在不超过 N 秒”作为新鲜度判断.

### 4.6 路径注入逻辑

生成 Python 脚本时,需要将 UnityAgentSkills 数据目录的绝对路径注入到脚本中,用于拼接 pending/results/done 等子目录:

- 示例路径: `<UnityAgentSkillsCommandsAbsPath>`.

```text
UNITY_AGENT_SKILLS_DATA_DIR = "<UnityAgentSkillsCommandsAbsPath>"
PENDING_DIR = join(UNITY_AGENT_SKILLS_DATA_DIR, "pending")
RESULTS_DIR = join(UNITY_AGENT_SKILLS_DATA_DIR, "results")
DONE_DIR = join(UNITY_AGENT_SKILLS_DATA_DIR, "done")

# 说明: 本次规范中,UNITY_AGENT_SKILLS_DATA_DIR 指向 `.../Assets/UnityAgentSkills/Commands`
```

## 5. 举例覆盖需求和边缘情况

### 例 1：首次使用，设置导出路径并导出所有技能

**场景**：用户第一次打开插件，导出路径未设置，导出目标文件夹不存在。

**操作步骤**：

1. 用户点击 `Tools/Unity-skills` 菜单项
2. 弹窗显示，导出路径显示为空或默认值
3. 用户点击「修改按钮」，选择路径 `<ExportDir>`
4. 用户点击「全选」按钮，选中所有技能
5. 用户点击「导出选中技能」按钮
6. 插件创建 `<ExportDir>/skills/` 文件夹(若该文件夹已存在则复用)
7. 插件生成 `unity-log/` 文件夹，包含：
   - `SKILL.md` - 完整的技能说明文档
   - `scripts/execute_unity_command.py` - Python 脚本（已替换路径）
8. 插件生成 `unity-prefab-view/` 文件夹，包含：
   - `SKILL.md` - 完整的技能说明文档
   - `scripts/execute_unity_command.py` - Python 脚本（已替换路径）
9. 插件生成 `unity-prefab-edit/` 文件夹，包含：
   - `SKILL.md` - 完整的技能说明文档
   - `scripts/execute_unity_command.py` - Python 脚本（已替换路径）
10. 插件显示"导出成功：3 个技能"提示

**导出结果示例**:

- `<ExportDir>/skills/`
  - `<skillId>/`
    - `SKILL.md`
    - `scripts/execute_unity_command.py`

### 例 2：部分导出技能

**场景**：用户只想导出部分技能，不需要全部导出。

**操作步骤**：

1. 用户点击 `Tools/Unity-skills` 菜单项
2. 弹窗显示，导出路径已保存为 `<ExportDir>`
3. 用户取消勾选 `unity-prefab-edit`
4. 用户只保留 `unity-log` 和 `unity-prefab-view` 的勾选
5. 用户点击「导出选中技能」按钮
6. 插件只生成 `unity-log/` 与 `unity-prefab-view/` 文件夹

### 例 3：覆盖已存在的技能文件夹

**场景**：目标文件夹下已存在同名技能文件夹，需要更新内容。

**当前状态**：

```
<ExportDir>/
└── skills/
    └── unity-log/
        ├── SKILL.md (旧版本)
        └── scripts/
            └── execute_unity_command.py (旧版本)
```

**操作步骤**：

1. 用户选中 `unity-log` 技能
2. 用户点击「导出选中技能」按钮
3. 插件检测到 `<ExportDir>/skills/unity-log/` 已存在
4. 插件直接覆盖该文件夹，不提示警告
5. 新的 `SKILL.md` 和 Python 脚本替换旧文件

### 例 4：修改导出路径

**场景**：用户想将技能导出到不同的位置。

**操作步骤**：

1. 用户点击 `Tools/Unity-skills` 菜单项
2. 当前导出路径显示为 `<OldExportDir>`
3. 用户点击「修改按钮」，选择新路径 `<NewExportDir>`
4. 用户选中所有技能并导出
5. 插件在 `<NewExportDir>/skills/` 下生成技能文件夹
6. 插件将新路径 `<NewExportDir>` 保存到 EditorPrefs
7. 下次打开插件时，自动加载新路径

### 例 5：Python 脚本命令行调用示例

**场景**：外部 AI 工具通过命令行直接调用 Python 脚本,无需创建额外的 Python 文件。

**命令行调用示例**：

```bash
# 方式1(推荐): 使用 uv 运行(本机无全局 python)
uv run <ExportDir>/skills/<skillId>/scripts/execute_unity_command.py '{"batchId":"batch_log_001","timeout":30000,"commands":[{"id":"cmd_001","type":"log.query","params":{"n":50,"level":"Error"}}]}'

# 方式1(兼容): 直接用 python 运行(仅当你的机器已安装全局 python)
python <ExportDir>/skills/<skillId>/scripts/execute_unity_command.py '{"batchId":"batch_log_001","timeout":30000,"commands":[{"id":"cmd_001","type":"log.query","params":{"n":50,"level":"Error"}}]}'

# 方式2(Windows CMD): 使用 JSON 变量 + uv run
set JSON_INPUT={"batchId":"batch_prefab_001","timeout":30000,"commands":[{"id":"cmd_001","type":"prefab.queryHierarchy","params":{"prefabPath":"Assets/Path/To/Some.prefab","includeInactive":true}}]}
uv run <ExportDir>/skills/<skillId>/scripts/execute_unity_command.py "%JSON_INPUT%"

# 方式2(PowerShell): $env:JSON_INPUT='...'; uv run ... $env:JSON_INPUT
# 方式2(bash): JSON_INPUT='...'; uv run ... "$JSON_INPUT"
```

**返回结果**：

```json
{
  "batchId": "batch_log_001",
  "status": "completed",
  "startedAt": "YYYY-MM-DD HH:mm:ss.SSS",
  "finishedAt": "YYYY-MM-DD HH:mm:ss.SSS",
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

### 例 6：新增技能配置

**场景**：开发者需要新增一个名为 `unity-config-view` 的技能。

**操作步骤**：

1. 新增一份 `unity-config-view` 的 SkillConfig(具体文件/资源位置由实现决定).
2. 在该 SkillConfig 中填入 SkillName, SkillDescription, SkillMarkdown.
3. 将该 SkillConfig 注册到"集中注册表"(注册表的具体实现不在本需求约束范围内).

伪代码示意(非实现):

```
注册表.add(SkillConfig)
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

**Project A**（路径：`<UnityProjectRootA>`）：

- 设置导出路径：`<ExportDirA>`
- EditorPrefs 存储：`UnitySkillsExporter.ExportPath_<UnityProjectRootA>` = `<ExportDirA>`

**Project B**（路径：`<UnityProjectRootB>`）：

- 设置导出路径：`<ExportDirB>`
- EditorPrefs 存储：`UnitySkillsExporter.ExportPath_<UnityProjectRootB>` = `<ExportDirB>`

**说明**：

- 使用 Unity 项目的完整路径作为 EditorPrefs 的 key 后缀
- 不同项目的配置互不干扰
- 同一台电脑上的不同开发者也有独立的 EditorPrefs

### 例 10：导出目标文件夹已有 skills 子文件夹

**场景**：用户选择的导出路径下已经存在 skills 文件夹，但不包含任何技能文件夹。

**当前状态**：

```
<ExportDir>/
└── skills/  (空文件夹)
```

**操作步骤**：

1. 用户选择导出路径为 `<ExportDir>`
2. 用户选中 `unity-log` 和 `unity-prefab-view` 两个技能并导出
3. 插件检测到 `<ExportDir>/skills/` 已存在
4. 插件直接在 `skills/` 下生成技能文件夹
5. 导出后 `skills/` 下会出现相应的技能文件夹,每个技能文件夹至少包含 `SKILL.md` 与 `scripts/execute_unity_command.py`.

### 例 11：导出目标文件夹不存在

**场景**：用户选择一个完全不存在的文件夹路径。

**操作步骤**：

1. 用户选择导出路径为 `<ExportDir>`
2. 用户选中技能并导出
3. 插件检测到 `<ExportDir>` 不存在
4. 插件自动创建 `<ExportDir>/` 目录
5. 插件自动创建 `<ExportDir>/skills/` 子目录
6. 插件在 `skills/` 下生成技能文件夹

## 6. 与现有系统的集成

### 6.1 与现有系统的关系(不约束实现结构)

- 本需求只约束对外行为与数据协议,不约束插件代码如何拆分模块.
- 实现只需满足:
  - UI 能展示技能列表并执行导出.
  - 技能列表与导出内容来自某种"集中配置"(可以是注册表,资源索引,硬编码列表等).

### 6.2 EditorPrefs Key 命名规范

- 导出路径存储 Key 格式：`UnitySkillsExporter.ExportPath.<UnityProjectRoot>`
- 示例：`UnitySkillsExporter.ExportPath.<UnityProjectRoot>`

### 6.3 Python 脚本生成位置

所有技能的 Python 脚本都生成在技能文件夹的 `scripts/` 子目录下，文件名统一为 `execute_unity_command.py`。

## 7. 后续扩展性

### 7.1 新增技能的步骤

1. 新增一个新的 SkillConfig.
2. 在 SkillConfig 中定义 SkillName, SkillDescription, SkillMarkdown.
3. 将新 SkillConfig 注册到"集中注册表".
4. 重启 Unity 编辑器或等待脚本重载
5. 技能自动出现在导出界面中

### 7.2 Python 脚本功能扩展

虽然目前需求要求 Python 脚本不做太多校验,但未来如果需要扩展功能(如添加参数校验,日志记录,错误重试等),只需要修改 Python 脚本模板即可.所有技能都会使用更新后的 Python 脚本.

## 8. 不包含的功能

- 不支持在 Unity Editor 界面中直接编辑 SKILL.md 内容（需要修改 C#配置文件）
- 不支持技能的批量导入或从外部文件夹读取配置
- 不支持 Python 脚本的复杂校验和错误处理（保持简单）
- 不支持技能之间的依赖关系管理
- 不支持技能版本控制（直接覆盖同名文件）
