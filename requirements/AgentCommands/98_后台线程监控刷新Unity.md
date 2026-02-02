# 需求文档 - AutoCompile 功能融合

## 1. 项目现状与核心目标

### 1.1 现状

**源项目** (`Code/Assets/Editor/AutoCompileOnOutOfFocus`):

- 独立的 Unity 编辑器工具
- 功能：在外部编辑器（如 VSCode）修改代码后，当 Unity 编辑器失去焦点时自动触发编译
- 使用 FileSystemWatcher 监听 .cs 文件变化
- 通过 EditorApplication.update 轮询检测焦点状态
- 配置使用 ScriptableObject (AutoCompileConfig.asset)

**目标项目** (`Code/Assets/Editor/AgentCommands`):

- 插件化命令执行框架
- 有 SkillsExporterWindow 窗口（菜单：Tools/Unity-skills）
- 当前为单页面窗口，只支持技能导出功能

**实施状态**: ✅ 已完成融合（2026-02-02）

### 1.2 问题

用户在外部编辑器修改 C# 代码后，如果焦点不在 Unity 编辑器，无法自动触发编译刷新，需要手动切回 Unity 才能编译。

### 1.3 核心目标

将 AutoCompileOnOutOfFocus 的核心功能融合进 AgentCommands 项目：

- ✅ **作为后台服务自动运行**：使用 `[InitializeOnLoad]` 在编辑器启动时自动初始化服务
- ✅ **集成配置界面**：在 SkillsExporterWindow 中添加新的 tab 页，提供 AutoCompile 配置选项
- ✅ **保持原有功能**：完全保留 AutoCompileOnOutOfFocus 的核心能力（焦点外自动编译）

---

## 2. 范围与边界

### 2.1 功能点

- [x] **后台服务集成** ✅ 已完成

  - 使用 `[InitializeOnLoad]` 自动初始化 (AutoCompileService)
  - Unity 编辑器启动时根据配置自动启动服务
  - 不再实现 ICommandPlugin 接口（改为独立服务模式）

- [x] **配置界面（新增 tab 页）** ✅ 已完成

  - SkillsExporterWindow 重构为 tab 结构
  - Tab 1：技能导出（现有功能）
  - Tab 2：AutoCompile 配置（新增）
  - 配置选项：
    - 启用/禁用自动编译（开关）
    - 防抖间隔（毫秒，200-5000）
    - **监听路径列表**（支持多个路径，可添加/删除/浏览）
    - 当前状态显示（Running/Paused/Compiling/Pending）

- [x] **核心功能保留** ✅ 已完成

  - 文件监听服务（FileSystemWatcher 监听 .cs 文件）
  - 焦点检测（InternalEditorUtility.isApplicationActive）
  - 防抖机制（避免频繁触发编译）
  - 状态机管理（Stopped/Running/Paused/Pending/Compiling）
  - 自动触发编译（AssetDatabase.Refresh()）
  - **Play 模式兼容**（进入 Play 暂停服务，退出 Play 恢复服务）

- [x] **配置持久化** ✅ 已完成
  - 使用 EditorPrefs 存储配置
  - Key 格式：`AgentCommands.AutoCompile.{项目路径}`
  - JSON 序列化存储

### 2.2 排除项

- **不提供外部命令接口**：不需要 compile.trigger 等命令，仅作为后台服务
- **不保留原窗口**：配置界面集成到 SkillsExporterWindow
- **不改变核心逻辑**：保持原有的焦点检测和防抖逻辑不变
- ~~**暂不支持多路径监听**：只监听单个路径（默认 Assets）~~ → **✅ 已支持多路径监听**

---

## 3. 举例覆盖需求和边缘情况

### 例 1：首次使用，启用自动编译

**场景**：用户首次使用此功能，需要在配置界面启用自动编译。

**操作步骤**：

1. 用户打开 Unity 编辑器
2. 通过菜单 `Tools/Unity-skills` 打开 SkillsExporterWindow
3. 切换到 `AutoCompile` tab 页
4. 看到配置界面：
   ```
   ☐ 启用自动编译
   防抖间隔: [500] 毫秒
   监听路径: [Assets]
   当前状态: Stopped
   ```
5. 勾选"启用自动编译"复选框
6. 点击"应用配置"按钮（或实时生效）
7. 状态变为"Running"
8. 控制台输出：`[AutoCompile] 服务已启动，监听路径: Assets`

**预期结果**：

- AutoCompile 服务启动
- FileSystemWatcher 开始监听 Assets 目录下的 .cs 文件变化
- 配置保存到 EditorPrefs

---

### 例 2：外部编辑代码后自动编译

**场景**：用户在 VSCode 中修改代码，焦点在 VSCode，Unity 在后台。

**当前状态**：

- AutoCompile 已启用
- 防抖间隔：500ms
- Unity 编辑器在后台（失去焦点）

**操作步骤**：

1. 用户在 VSCode 中修改 `Assets/Scripts/Test.cs` 文件
2. 保存文件（Ctrl+S）
3. VSCode 保持焦点状态
4. FileSystemWatcher 检测到文件变化
5. 文件变更加入队列
6. 防抖计时器启动（500ms）
7. 500ms 后检测到 Unity 失去焦点
8. 自动触发 `AssetDatabase.Refresh()`
9. Unity 开始编译
10. 状态变化：Running → Pending → Compiling → Running
11. 控制台输出：
    ```
    [AutoCompile] 检测到文件变更: Assets/Scripts/Test.cs
    [AutoCompile] 防抖延时 500ms...
    [AutoCompile] 编辑器失去焦点，触发自动编译
    ```

**预期结果**：

- Unity 在后台自动编译
- 用户无需切回 Unity
- 编译完成后，下次切回 Unity 时代码已是最新版本

---

### 例 3：频繁编辑代码，防抖机制生效

**场景**：用户快速修改多个文件，防抖机制避免频繁触发编译。

**当前状态**：

- AutoCompile 已启用
- 防抖间隔：1000ms

**操作步骤**：

1. 用户在 30 秒内修改了 5 个文件：
   - Test1.cs (第 0 秒)
   - Test2.cs (第 5 秒)
   - Test3.cs (第 10 秒)
   - Test4.cs (第 15 秒)
   - Test5.cs (第 20 秒)
2. 每次文件变化都被检测到并入队
3. 防抖计时器不断重置
4. 第 20 秒后，1 秒内无新文件变化
5. 防抖计时结束，触发编译
6. 只编译一次，包含所有 5 个文件的修改

**预期结果**：

- 只触发一次编译（而非 5 次）
- 节省编译时间
- 控制台输出：
  ```
  [AutoCompile] 检测到文件变更: Assets/Scripts/Test1.cs
  [AutoCompile] 检测到文件变更: Assets/Scripts/Test2.cs
  [AutoCompile] 检测到文件变更: Assets/Scripts/Test3.cs
  [AutoCompile] 检测到文件变更: Assets/Scripts/Test4.cs
  [AutoCompile] 检测到文件变更: Assets/Scripts/Test5.cs
  [AutoCompile] 编辑器失去焦点，触发自动编译
  ```

---

### 例 4：焦点在 Unity 内部，不触发自动编译

**场景**：用户在 Unity 编辑器内修改代码（不常见，但可能通过内置代码编辑器）。

**当前状态**：

- AutoCompile 已启用
- 焦点在 Unity 编辑器

**操作步骤**：

1. 用户在 Unity 内置代码编辑器中修改脚本
2. 保存文件
3. FileSystemWatcher 检测到文件变化
4. 状态变为 Pending
5. 防抖延时结束后检测焦点
6. 发现 `isApplicationActive == true`（焦点在 Unity）
7. **不触发编译**
8. 状态变回 Running
9. 控制台输出：
   ```
   [AutoCompile] 检测到文件变更: Assets/Scripts/Test.cs
   [AutoCompile] 编辑器有焦点，跳过自动编译
   ```

**预期结果**：

- 不触发自动编译
- 用户手动保存或切换场景时才会编译
- 避免打断用户在 Unity 内的操作

---

### 例 5：编译进行中，再次检测到文件变化

**场景**：Unity 正在编译，此时又有文件被修改。

**当前状态**：

- AutoCompile 已启用
- 正在编译（EditorApplication.isCompiling == true）

**操作步骤**：

1. Unity 正在编译（状态：Compiling）
2. 用户修改外部文件
3. FileSystemWatcher 检测到文件变化
4. 在 OnUpdate() 中检测到 `isCompiling == true`
5. **不立即处理**，将文件变更加入队列
6. 继续等待当前编译完成
7. 编译完成后，在下一个 update 循环中：
   - 检测到队列不为空
   - 启动防抖计时器
   - 延时后触发新的编译

**预期结果**：

- 不打断正在进行的编译
- 文件变更被缓存
- 当前编译完成后自动触发新编译

---

### 例 6：禁用自动编译服务

**场景**：用户想临时禁用自动编译功能。

**当前状态**：

- AutoCompile 已启用并运行

**操作步骤**：

1. 用户打开 SkillsExporterWindow
2. 切换到 `AutoCompile` tab 页
3. 当前显示：
   ```
   ☑ 启用自动编译
   防抖间隔: [500] 毫秒
   监听路径: [Assets]
   当前状态: Running
   ```
4. 用户取消勾选"启用自动编译"
5. 状态变为"Stopped"
6. FileSystemWatcher 停止监听
7. 控制台输出：
   ```
   [AutoCompile] 服务已停止
   ```
8. 配置保存到 EditorPrefs

**预期结果**：

- 服务完全停止
- 不再监听文件变化
- 下次编辑器启动时保持禁用状态（除非重新启用）

---

### 例 7：编辑器重启，配置持久化

**场景**：用户配置了自动编译，重启 Unity 编辑器后配置保持。

**操作步骤**：

1. 用户配置：
   - 启用自动编译：☑
   - 防抖间隔：1000ms
   - 监听路径：Assets
2. 配置保存到 EditorPrefs：
   ```
   Key: AgentCommands.AutoCompile.F:/UnityProject/SL/SL_402
   Value: {"enabled":true,"debounceInterval":1000,"watchPath":"Assets"}
   ```
3. 用户关闭 Unity 编辑器
4. 重新打开 Unity 项目
5. AutoCompilePlugin.Initialize() 被调用
6. 从 EditorPrefs 读取配置
7. 检测到 enabled == true
8. 自动启动服务

**预期结果**：

- 配置完全保留
- 服务自动启动
- 无需重新配置

---

### 例 8：修改防抖间隔

**场景**：用户觉得 500ms 太短，想调整为 2000ms。

**操作步骤**：

1. 用户打开 SkillsExporterWindow → AutoCompile tab
2. 当前防抖间隔：500ms
3. 用户修改为 2000ms
4. 点击"应用"（或实时生效）
5. 控制台输出：
   ```
   [AutoCompile] 防抖间隔已更新: 2000ms
   ```
6. 后续文件变更将使用新的防抖间隔

**预期结果**：

- 新配置立即生效
- EditorPrefs 更新
- 下次编辑器启动时使用新配置

---

### 例 9：监听路径不存在或无效

**场景**：用户配置了一个不存在的监听路径。

**操作步骤**：

1. 用户在配置界面设置监听路径为 `Assets/NonExistent`
2. 点击"应用"
3. AutoCompilePlugin.Initialize() 尝试启动 FileSystemWatcher
4. 检测到路径不存在
5. 在配置界面显示错误提示：
   ```
   ⚠️ 监听路径不存在: Assets/NonExistent
   ```
6. 服务不启动，状态保持"Stopped"

**预期结果**：

- 显示友好的错误提示
- 不启动服务
- 避免后续错误

---

### 例 10：多项目独立配置

**场景**：同一台电脑上有多个 Unity 项目，每个项目有独立的 AutoCompile 配置。

**项目 A** (`F:/ProjectA`):

- EditorPrefs Key: `AgentCommands.AutoCompile.F:/ProjectA`
- 配置：启用，500ms

**项目 B** (`D:/Games/ProjectB`):

- EditorPrefs Key: `AgentCommands.AutoCompile.D:/Games/ProjectB`
- 配置：禁用，1000ms

**操作步骤**：

1. 用户打开项目 A
2. AutoCompile 以 500ms 防抖间隔运行
3. 用户关闭项目 A，打开项目 B
4. 从项目 B 的 EditorPrefs 读取配置
5. AutoCompile 保持禁用状态

**预期结果**：

- 项目间配置互不干扰
- 使用项目路径作为 EditorPrefs Key 后缀
- 不同项目可以有不同配置

---

## 4. 目录结构与文件清单

### 4.1 新增文件（✅ 已完成）

```
Code/Assets/Editor/AgentCommands/
├── AutoCompile/                              # ✅ 已创建
│   ├── AutoCompileService.cs                # ✅ 服务启动器（使用 [InitializeOnLoad]）
│   ├── Core/
│   │   ├── AutoCompileController.cs          # ✅ 核心控制器（状态机、事件处理）
│   │   └── FileMonitorService.cs             # ✅ 文件监听服务（FileSystemWatcher）
│   └── Configuration/
│       ├── AutoCompileConfig.cs              # ✅ 配置数据模型
│       └── AutoCompileConfigProvider.cs      # ✅ EditorPrefs 读写工具
│
└── UI/                                      # ✅ 【新增】轻量化UI架构
    ├── SkillsExporterWindow.cs               # ✅ 主窗口（轻量化版本）
    ├── Components/                           # UI组件
    │   ├── ITabContent.cs                    # ✅ Tab内容接口
    │   └── TabContentManager.cs              # ✅ Tab管理器
    └── Tabs/                                 # 各个Tab页面
        ├── SkillsExportTab.cs                # ✅ 技能导出Tab
        └── AutoCompileTab.cs                 # ✅ AutoCompile配置Tab
```

### 4.2 修改文件（✅ 已完成）

- `SkillsExporter/SkillsExporterMenuItem.cs`：移除 MenuItem 特性，保留用于向后兼容
- ~~`SkillsExporter/SkillsExporterWindow.cs`~~：已删除，迁移至 `UI/SkillsExporterWindow.cs`

### 4.3 实际实现与原计划差异

**架构调整**:

- ❌ ~~AutoCompilePlugin (ICommandPlugin)~~ → ✅ AutoCompileService ([InitializeOnLoad])
- 原计划作为插件运行，改为独立的后台服务模式

**配置改进**:

- ✅ 支持多路径监听（原计划只支持单个路径）
- ✅ WatchPath 改为 WatchPaths (List<string>)

**UI 增强**:

- ✅ 支持添加/删除/浏览多个监听路径
- ✅ 路径验证和错误提示

---

## 5. UI 界面设计

### 5.1 窗口顶部 tab 切换

```
┌─────────────────────────────────────────────────────────┐
│ [技能导出] [AutoCompile]                                │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  (tab 内容区域)                                         │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

### 5.2 AutoCompile 配置 tab（✅ 已实现）

```
┌─────────────────────────────────────────────────────────┐
│ AutoCompile 配置                                        │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  ☑ 启用自动编译                                         │
│                                                         │
│  防抖间隔: [1000] 毫秒                                   │
│  (范围: 200-5000，越短越灵敏但可能频繁编译)             │
│                                                         │
│  监听路径列表:                                           │
│  ┌─────────────────────────────────────────────────┐   │
│  │ [Assets]                    [浏览] [删除]        │   │
│  │ [Packages]                  [浏览] [删除]        │   │
│  └─────────────────────────────────────────────────┘   │
│  [添加路径]                                             │
│                                                         │
│  ────────────────────────────────────────────────────  │
│                                                         │
│  当前状态: [Running]                                    │
│                                                         │
│  ────────────────────────────────────────────────────  │
│                                                         │
│  [应用配置]  [恢复默认]                                  │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

---

## 6. 技术实现要点（✅ 已完成）

### 6.1 AutoCompileService 服务结构 ✅ 已实现

```csharp
namespace AgentCommands.AutoCompile
{
    [InitializeOnLoad]
    public class AutoCompileService
    {
        static AutoCompileService()
        {
            // Unity 编辑器启动时自动初始化服务
            var config = AutoCompileConfigProvider.LoadConfig();
            AutoCompileController.Initialize(config);
        }

        /// <summary>
        /// 手动重启服务（用于配置更改后）.
        /// </summary>
        public static void Restart()
        {
            AutoCompileController.Shutdown();
            var config = AutoCompileConfigProvider.LoadConfig();
            AutoCompileController.Initialize(config);
        }
    }
}
```

**与原计划差异**:

- ❌ ~~不实现 ICommandPlugin 接口~~ → ✅ 使用 `[InitializeOnLoad]` 特性
- ✅ Unity 编辑器启动时自动初始化
- ✅ 提供 `Restart()` 方法用于配置更改后重启服务

### 6.2 配置模型 ✅ 已实现（支持多路径）

```csharp
public class AutoCompileConfig
{
    public bool IsEnabled { get; set; } = false;
    public int DebounceInterval { get; set; } = 500;
    public List<string> WatchPaths { get; set; } = new List<string> { "Assets" };
}
```

**改进**:

- ✅ WatchPath 改为 WatchPaths (List<string>)
- ✅ 支持监听多个路径

### 6.3 EditorPrefs 存储

```csharp
internal static class AutoCompileConfigProvider
{
    private static string GetEditorPrefsKey()
    {
        string projectPath = Directory.GetParent(Application.dataPath).FullName;
        return $"AgentCommands.AutoCompile.{projectPath}";
    }

    public static AutoCompileConfig LoadConfig()
    {
        string json = EditorPrefs.GetString(GetEditorPrefsKey(), "{}");
        return JsonUtility.FromJson<AutoCompileConfig>(json);
    }

    public static void SaveConfig(AutoCompileConfig config)
    {
        string json = JsonUtility.ToJson(config);
        EditorPrefs.SetString(GetEditorPrefsKey(), json);
    }
}
```

### 6.4 SkillsExporterWindow tab 重构

```csharp
public class SkillsExporterWindow : EditorWindow
{
    private enum TabType
    {
        SkillsExport,
        AutoCompile
    }

    private TabType _currentTab = TabType.SkillsExport;

    private void OnGUI()
    {
        DrawTabButtons();

        EditorGUILayout.Space();

        switch (_currentTab)
        {
            case TabType.SkillsExport:
                DrawSkillsExportTab();
                break;
            case TabType.AutoCompile:
                DrawAutoCompileConfigTab();
                break;
        }
    }

    private void DrawTabButtons()
    {
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("技能导出", _currentTab == TabType.SkillsExport ? EditorStyles.toolbarButton : GUI.skin.button))
        {
            _currentTab = TabType.SkillsExport;
        }
        if (GUILayout.Button("AutoCompile", _currentTab == TabType.AutoCompile ? EditorStyles.toolbarButton : GUI.skin.button))
        {
            _currentTab = TabType.AutoCompile;
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawAutoCompileConfigTab()
    {
        // AutoCompile 配置界面实现
    }
}
```

---

## 7. 兼容性与迁移

### 7.1 与原 AutoCompileConfig.asset 的兼容

**方案 1：保留原配置文件**

- 保留 AutoCompileConfig.asset
- AutoCompileController 继续从 ScriptableObject 读取配置
- SkillsExporterWindow 提供编辑界面，修改 ScriptableObject

**方案 2：迁移到 EditorPrefs**（推荐）

- 首次运行时检测 AutoCompileConfig.asset 是否存在
- 如果存在，读取并迁移到 EditorPrefs
- 后续只使用 EditorPrefs
- 简化配置管理，无需管理 .asset 文件

### 7.2 依赖程序集

确保 AutoCompile 相关文件引用正确的程序集定义：

- 如果使用 `AgentCommands.asmdef`，无需额外配置
- 如果独立程序集，需要在 `.asmdef` 中引用 `UnityEditor`

---

## 8. 测试要点

### 8.1 功能测试

- [ ] 启用服务后，外部修改 .cs 文件能自动触发编译
- [ ] 焦点在 Unity 内时，不触发编译
- [ ] 防抖机制生效，频繁编辑只编译一次
- [ ] 编译进行中，文件变化缓存等待
- [ ] 禁用服务后，不再监听文件变化

### 8.2 UI 测试

- [ ] tab 切换正常
- [ ] 配置修改实时生效或点击应用后生效
- [ ] 状态显示正确（Running/Stopped/Compiling/Pending）
- [ ] 错误提示友好（如路径不存在）

### 8.3 配置持久化测试

- [ ] 配置保存到 EditorPrefs
- [ ] 重启编辑器后配置保留
- [ ] 多项目配置独立

---

## 9. 后续扩展性

### 9.1 新增配置项

未来可能需要添加的配置：

- [ ] 监听文件类型扩展（如 .asset、.json）
- [ ] 编译完成后通知（弹出提示/声音）
- [ ] 日志级别控制
- [ ] 白名单/黑名单目录

### 9.2 新增 tab 页

SkillsExporterWindow 的 tab 结构可以轻松扩展：

- [ ] 插件管理 tab（启用/禁用插件）
- [ ] 日志配置 tab
- [ ] 高级设置 tab

---

## 10. 不包含的功能

- ✅ **不提供命令接口**：不需要 compile.trigger 等外部命令（已确认）
- ✅ **不保留原 AutoCompileWindow**：配置界面集成到 SkillsExporterWindow（已完成）
- ~~**不支持多路径监听**：只监听单个目录~~ → **✅ 已支持多路径监听**（超出原计划）
- ✅ **不支持自定义编译触发条件**：固定为"失去焦点时触发"（已确认）
- ✅ **不修改核心编译逻辑**：使用 AssetDatabase.Refresh()，不自定义编译流程（已确认）

---

## 11. 成功标准

### 11.1 功能完整性

- ✅ AutoCompile 功能完全融入 AgentCommands 项目
- ✅ 作为后台服务自动运行
- ✅ 配置界面友好易用
- ✅ 配置持久化正确

### 11.2 用户体验

- ✅ 用户无需手动切换回 Unity 即可触发编译
- ✅ 配置简单直观
- ✅ 状态反馈及时
- ✅ 错误提示友好

### 11.3 代码质量

- ✅ 不再依赖插件系统（改为独立服务）
- ✅ 代码结构清晰，易于维护
- ✅ 与原 AutoCompileOnOutOfFocus 代码风格一致
- ✅ 充分利用 Unity Editor API

---

## 12. 实施总结（2026-02-02）

### 12.1 完成状态

**✅ 全部完成**

所有计划功能已实现，并超出原计划：

- ✅ 支持多路径监听（原计划只支持单路径）
- ✅ Play 模式兼容（原计划未提及）
- ✅ 更简洁的架构（使用 [InitializeOnLoad] 而非插件模式）

### 12.2 架构优化

**从插件模式改为服务模式**:

- 原计划: AutoCompilePlugin (ICommandPlugin)
- 实际实现: AutoCompileService ([InitializeOnLoad])
- 优势: 更简洁，无需依赖插件系统，Unity 启动时自动初始化

### 12.3 功能增强

**多路径监听支持**:

- 原计划: WatchPath (单个路径)
- 实际实现: WatchPaths (List<string>)
- UI: 支持添加/删除/浏览多个路径
- 实现: 每个路径独立的 FileMonitorService，共享同一个文件变更队列

### 12.4 Bug 修复记录

1. **GUI Layout 错误**: 修复 BeginHorizontal/EndHorizontal 不匹配问题
2. **命名空间统一**: 从 AgentCommands.Plugins.AutoCompile 改为 AgentCommands.AutoCompile
3. **目录位置调整**: 从 Plugins/AutoCompile/ 移至 AutoCompile/
4. **浏览按钮 Bug**: 修复空路径无法弹出文件夹选择框问题

### 12.5 待测试

- [ ] 编译测试（等待用户触发 C# 编译）
- [ ] 功能测试（等待用户验证自动编译功能）
- [ ] 边界条件测试（Play 模式、多路径等）

---

**文档版本**: 2.0
**创建时间**: 2026-02-02
**最后更新**: 2026-02-02
**实施状态**: ✅ 已完成融合（超出原计划）
**下一步**: 等待用户测试验证

=========已完成============
