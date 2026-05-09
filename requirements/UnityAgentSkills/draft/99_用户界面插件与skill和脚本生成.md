# 需求文档 — 技能导出（unity-agent-skills）

## 1. 核心目标

SkillsExporter 将 UnityAgentSkills 的所有命令能力导出为**一个统一的 Hermes 技能包 `unity-agent-skills`**，包含：

- 主技能文档（通用协议说明 + log 命令 + 能力目录）
- 8 个按需加载的能力 reference 文档
- 一份共用 Python 胶水脚本

## 2. 导出产物结构

```
unity-agent-skills/
├── SKILL.md                              # 主技能文档
│   ├── 通用协议说明（批量格式、状态机、错误码体系，以 log 命令为示例）
│   ├── log.query / log.screenshot / log.refresh（完整参数 + 输入输出示例 + 工作原理 + 典型工作流）
│   └── 能力目录（名称 + 命令类型清单 + reference 路径指针）
├── scripts/
│   └── execute_unity_command.py          # 共用 Python 胶水脚本
└── references/
    ├── prefab-view.md                    # 预制体查看
    ├── prefab-edit.md                    # 预制体编辑
    ├── scene-view.md                     # 场景查看
    ├── scene-edit.md                     # 场景编辑
    ├── playmode.md                       # PlayMode UI 自动化
    ├── prefab-bridge.md                  # 预制体桥接（实例化/Override/Unpack）
    ├── editor-action.md                  # 编辑器动作
    └── editor-control.md                 # 编辑器控制
```

### 2.1 主 SKILL.md 内容要求

**通用协议层**（以 log 命令为示例载体）：

- 批量命令输入格式（`batchId`、`commands[]`、`timeout`）
- 结果输出格式（`status: processing → completed/error`、`results[]`、统计字段）
- 错误码体系说明
- pending → results → done 文件流转机制
- 原子写与轮询等待原理

**log 命令**（完整独立章节）：

- `log.query`：参数表（`n`、`level`、`keyword`、`matchMode`、`includeStack`）、输入示例、输出结构、典型场景
- `log.screenshot`：参数表（`highlightRect`）、输出结构（`imageAbsolutePath`）、非阻塞异步等待说明、典型场景
- `log.refresh`：参数表（`{}`）、输出结构（`summary`、`refreshTriggered`、`compilationOccurred`）、编译等待闭环说明、典型场景

**能力目录**（索引表）：

每个能力一行：能力名称 + 包含的命令 type 列表 + reference 路径指针。

示例格式：

```
| 能力 | 命令 | 文档 |
|------|------|------|
| 预制体查看 | prefab.queryHierarchy, prefab.queryComponents | [references/prefab-view.md](references/prefab-view.md) |
| 预制体编辑 | prefab.setGameObjectProperties, prefab.renameGameObject, ... | [references/prefab-edit.md](references/prefab-edit.md) |
```

**篇幅约束**：主 SKILL.md 须控制在 8–15K 字符。通用协议和 log 命令文档的书写应精简，协议说明以 log 命令为示例一次性覆盖，log 三个命令共用一个协议示例段而非各自重复展开。

### 2.2 reference 文档要求

每个 reference **只写输入侧**，输出格式和示例由主 SKILL.md 通用协议层统一覆盖：

- 该能力的使用前提与依赖（如编辑前必须先查看）
- 所含命令的 `params` 参数表（核心）
- 精简的输入示例（1-2 个典型即可，不展开）
- 常见错误码（仅列出该能力特有的）
- **末尾**：指向相关 reference 的路径指针

**不写**输出结构、输出示例、通用协议说明——这些主 SKILL.md 已覆盖，reference 只补差异部分，节省 token。

reference 内容来源于对应的 done/ 需求文档：

| reference | 需求来源 |
|-----------|---------|
| prefab-view.md | `03_预制体查看.md` |
| prefab-edit.md | `04_预制体编辑.md` |
| scene-view.md | `05_场景查看.md` |
| scene-edit.md | `06_场景编辑.md` |
| playmode.md | `06_PlayMode_UI交互.md` |
| prefab-bridge.md | `04_预制体编辑.md` §3.6 桥接回写约定 |
| editor-action.md | `07_运行编辑器命令.md` |
| editor-control.md | `08_编辑器控制能力.md` |

### 2.3 Python 脚本要求

- 文件名固定为 `execute_unity_command.py`
- 存放 `{AGENT_COMMANDS_DATA_DIR}` 占位符，导出时替换为 Unity 项目的 `Commands/` 目录绝对路径
- 核心行为：
  1. 接收 JSON 字符串或字典，提取 `batchId`
  2. 以原子写（先写 `.tmp` 再 `os.replace`）将 JSON 写入 `pending/{batchId}.json`
  3. 以 0.5s 间隔轮询 `results/{batchId}.json`，通过文件修改时间过滤旧结果（新鲜度 ≤ 5s）
  4. 发现 `status=completed` 或 `status=error` 时返回结果，超时（30s）则抛出 `TimeoutError`
  5. 结果超过 8000 字符时自动截断 stdout 并提示完整结果文件路径

## 3. SkillsExporter UI

SkillsExporter 窗口与 AutoCompile 共享同一个 `SkillsExporterWindow`（`Tools/Unity-skills`），采用 tab 结构：

```
┌─────────────────────────────────────────────────────────┐
│ [技能导出] [AutoCompile]                                │
├─────────────────────────────────────────────────────────┤
│  导出路径: <ExportDir>                        [修改]     │
│                                                         │
│              [  导出 unity-agent-skills  ]              │
└─────────────────────────────────────────────────────────┘
```

### 3.1 Tab 1：技能导出

- 显示保存的导出路径
- 「修改」按钮选择新路径（EditorPrefs 持久化，按项目区分）
- 「导出 unity-agent-skills」按钮 → 在 `<ExportDir>/skills/unity-agent-skills/` 生成完整技能包
- 若目标已存在，直接覆盖
- 不提供勾选选择，一次导出全部
- 导出完成后显示提示

### 3.2 Tab 2：AutoCompile

由 `98_后台线程监控刷新Unity.md` 定义，与技能导出 tab 并列，互不影响。

## 4. 实现约束

### 4.1 技能源文件外置

- 技能文档（`SKILL.md` + 8 个 `references/*.md`）和 Python 脚本以**原始文件形式**存放在 Unity Editor 代码资源目录下
- 目录路径：`UnityAgentSkills/Editor/SkillsExporter/SkillFiles/`
- 目录结构与导出产物结构完全一致
- 导出行为 = 遍历 `SkillFiles/` → 复制全部文件到目标 → 对 `.py` 文件做 `{AGENT_COMMANDS_DATA_DIR}` → 实际 `Commands/` 目录绝对路径的文本替换

### 4.2 注册机制

- 使用一个轻量类（替代旧 `SkillConfigsRegistry`），只存储 `SkillFiles/` 根目录路径
- 导出时遍历该目录，不逐文件注册
- 新增或删除 reference 只需在 `SkillFiles/references/` 下新增或删除 `.md` 文件，无需改 C# 代码

### 4.3 删除项

- 删除全部 `SkillConfig_*.cs`（9 个文件）
- 删除 `SkillConfigsRegistry.cs` 中的 markdown 嵌入逻辑
- 删除 UI 中的复选框、全选/取消全选按钮

### 4.4 保持不变

- `EditorPrefs` 导出路径存储 key 格式：`UnitySkillsExporter.ExportPath.<UnityProjectRoot>`
- 菜单项路径：`Tools/Unity-skills`
- `SkillsExporterWindow` 的 tab 结构（Tab 2 AutoCompile 不变）

## 5. Hermes 技能规范合规

导出的 `SKILL.md` 须满足 Hermes 技能规范：

- frontmatter：`name`（`unity-agent-skills`）、`description`（≤1024 字符，以 "Use when..." 开头）
- 主 SKILL.md 控制在 8–15K 字符
- 结构：Overview → When to Use → 主体内容 → Common Pitfalls → Verification Checklist
- 超 20K 的内容拆入 references（本需求已按此拆分）

## 6. 举例

### 例 1：首次导出

1. 用户点击 `Tools/Unity-skills` → 打开窗口，默认在「技能导出」tab
2. 导出路径为空，点击「修改」选择 `/home/user/hermes-skills`
3. 点击「导出」→ 生成 `/home/user/hermes-skills/skills/unity-agent-skills/`

### 例 2：更新导出

1. 目标路径已有旧版 `unity-agent-skills/`
2. 点击「导出」→ 直接覆盖全部文件
3. Python 脚本中的 `Commands/` 路径自动注入当前项目路径

### 例 3：新增 reference

1. 开发者在 `SkillFiles/references/` 下新增 `new-feature.md`
2. 在 `SkillFiles/SKILL.md` 的能力目录中加一行索引
3. 下次导出自动包含新 reference
4. 无需修改任何 C# 代码

## 7. 验收清单

- [ ] 「技能导出」tab 显示导出路径 + 修改按钮 + 一个导出按钮（无复选框）
- [ ] 点击导出后在目标路径生成 `skills/unity-agent-skills/`，结构与 §2 一致
- [ ] 主 SKILL.md 包含通用协议说明、log 三条命令的完整文档、能力目录索引表
- [ ] 主 SKILL.md 字符数在 8–15K 范围内
- [ ] 主 SKILL.md frontmatter 满足 Hermes 规范（name、description ≤1024 字符且以 "Use when..." 开头）
- [ ] 8 个 reference 各只写 params 参数表 + 精简输入示例 + 特有错误码 + 交叉引用指针，不重复输出格式
- [ ] `execute_unity_command.py` 中 `Commands/` 路径已替换为实际绝对路径
- [ ] `SkillFiles/` 目录结构与导出产物一致
- [ ] 新增/删除 reference 只需操作文件，不需改 C# 代码
- [ ] 旧 `SkillConfig_*.cs` 全部移除，项目编译无报错
- [ ] 按项目维度的 EditorPrefs 导出路径存储正常工作
- [ ] 同名覆盖导出无残留旧文件
- [ ] 切换到 AutoCompile tab 正常工作，与技能导出 tab 互不干扰

## 8. 与其他需求文档的关系

- `01_整体与框架需求.md` — 提供批量命令协议、错误处理与整体架构约束。本需求导出的主 SKILL.md 通用协议层以此为准
- `02_日志功能需求_log.md` — log 三条命令的完整规格。本需求 §2.1 log 命令章节以此为准，精简为技能文档格式
- `03_预制体查看.md` — prefab-view.md 内容来源
- `04_预制体编辑.md` — prefab-edit.md 和 prefab-bridge.md（§3.6）内容来源
- `05_场景查看.md` — scene-view.md 内容来源
- `06_场景编辑.md` — scene-edit.md 内容来源
- `06_PlayMode_UI交互.md` — playmode.md 内容来源
- `07_运行编辑器命令.md` — editor-action.md 内容来源
- `08_编辑器控制能力.md` — editor-control.md 内容来源
- `98_后台线程监控刷新Unity.md` — 与本需求共享 `SkillsExporterWindow`（Tab 2 AutoCompile 由此文档定义）
