# 需求文档 - Play Mode UI 交互

## 1. 项目现状与核心目标

### 1.1 用户需求简述

用户希望在 Unity Play Mode 下实现类似 Web 自动化测试的能力:

1. 通过命令启动 Play Mode
2. 等待指定时间后截图并返回当前场景中所有可交互的 UI 元素列表
3. 通过命令触发按钮点击或在输入框中填写内容
4. 再次等待并截图,形成完整的自动化测试工作流

当前项目已有 UnityAgentSkills 命令框架,支持通过 JSON 文件向 Unity Editor 发送命令.需要在此基础上扩展 Play Mode 下的 UI 自动化能力.

### 1.2 技术可行性

Unity 原生提供完整的 UI 事件触发 API:

- `ExecuteEvents.Execute<IPointerClickHandler>()` - 程序化触发点击事件
- `Selectable.allSelectablesArray` - 获取所有可交互 UI 元素
- `InputField.text = "xxx"` - 设置输入框内容
- `EditorApplication.EnterPlaymode()` - 启动 Play Mode

基于以上 API,可以实现通用的 UI 交互能力,不依赖特定 UI 框架.

### 1.3 核心目标

- 提供 Play Mode 会话管理能力:启动,停止,状态检测
- 提供 UI 元素查询能力:获取当前场景中所有可交互元素
- 提供 UI 交互能力:点击按钮,设置输入框文本
- 提供等待与截图能力:延迟执行并捕获 Game 视图画面
- 支持命令链式执行:多次交互,多次截图的工作流

## 2. 范围与边界

### 2.1 功能范围(必须)

**playmode.start**:

- [ ] 支持命令类型 `type = "playmode.start"`
- [ ] 可选 `entryScene` 参数,指定进入 Play Mode 时加载的场景
- [ ] 返回 Play Mode 启动结果及当前状态

**playmode.stop**:

- [ ] 支持命令类型 `type = "playmode.stop"`
- [ ] 退出 Play Mode 并清理会话状态

**playmode.queryUI**:

- [ ] 支持命令类型 `type = "playmode.queryUI"`
- [ ] 支持筛选参数: `nameContains`,`textContains`,`componentFilter`,`visibleOnly`,`interactableOnly`,`screenRect`,`maxResults`
- [ ] 返回当前场景中的 UI 元素列表(可按筛选参数裁剪)
- [ ] 每个元素包含:对象路径,组件类型,屏幕坐标,当前文本等

筛选参数说明:

- `nameContains`: 可选,按对象名 contains 匹配(IgnoreCase)
- `textContains`: 可选,按文本 contains 匹配(IgnoreCase)
- `componentFilter`: 可选,组件名 contains + OR 匹配
- `visibleOnly`: 可选,默认 false
- `interactableOnly`: 可选,默认 false
- `screenRect`: 可选,格式 `{xMin,xMax,yMin,yMax}`
- `maxResults`: 可选,默认 200,超过时返回 `truncated=true`

**playmode.waitFor**:

- [ ] 支持命令类型 `type = "playmode.waitFor"`
- [ ] 必填 `waitSeconds`,表示最长等待时长,`>0`
- [ ] 可选等待条件: `nameContains`,`textContains`,两者是 OR 关系,任一命中即结束等待
- [ ] `nameContains` 和 `textContains` 可都不传,表示纯等待到时长上限
- [ ] 若先命中目标,返回 `waitOutcome="matched"`;若先到时,返回 `waitOutcome="timeoutReached"` 且命令状态仍为 `success`
- [ ] 命中目标时返回 `matchedBy` 与 `matchedElement(path,siblingIndex,elementId,name,text)`

**playmode.click**:

- [ ] 支持命令类型 `type = "playmode.click"`
- [ ] 支持通过对象路径定位点击目标
- [ ] 使用 `ExecuteEvents.Execute<IPointerClickHandler>` 触发点击
- [ ] 支持 `siblingIndex` 参数用于定位同名对象(与 prefab.queryComponents 保持一致)
- [ ] `targetPath` 和 `siblingIndex` 配合使用,不传 `siblingIndex` 时默认为 0
- [ ] 返回点击执行结果

**playmode.clickAt**:

- [ ] 支持命令类型 `type = "playmode.clickAt"`
- [ ] 通过屏幕坐标直接点击,不依赖对象路径
- [ ] 在指定坐标执行射线检测,触发击中的 UI 元素的点击事件
- [ ] 适用场景:动态生成 UI 路径不固定,或需要点击特定屏幕位置

**playmode.setText**:

- [ ] 支持命令类型 `type = "playmode.setText"`
- [ ] 支持通过对象路径指定目标输入框
- [ ] 支持 `siblingIndex` 参数用于定位同名对象(可选,默认为 0)
- [ ] 设置文本内容
- [ ] 可选 `submit` 参数,为 true 时触发 `onEndEdit` 事件(默认为 false)

**playmode.scroll**:

- [ ] 支持命令类型 `type = "playmode.scroll"`
- [ ] 模拟鼠标滚轮滚动,用于滚动列表、ScrollView 等
- [ ] 支持指定滚动量(正数向上,负数向下)
- [ ] 可选指定鼠标位置(某些 UI 需要鼠标悬停在特定区域才响应滚动)

**log.screenshot(跨技能验证能力)**:

- [ ] PlayMode 文档中不再定义独立截图命令,截图统一使用 `unity-log` 技能的 `log.screenshot`
- [ ] `playmode.waitAndScreenshot` 已删除且不向后兼容
- [ ] `log.screenshot` 仅支持可选 `highlightRect`(红框标注),不再支持 `waitSeconds`
- [ ] `highlightRect` 格式为 `{xMin,xMax,yMin,yMax}`,越界自动裁剪到图像边界
- [ ] 输出为全图 PNG,不输出裁剪图,并在全图中标注红框

### 2.2 会话状态管理(必须)

- [ ] 维护 Play Mode 会话状态:`Idle`,`Starting`,`Active`,`Stopping`,`Stopped`
- [ ] 检测 Play Mode 异常退出(崩溃或用户手动停止)
- [ ] 会话异常时返回明确错误码

### 2.3 排除项(明确不做)

- 本期不实现角色模拟输入(键盘,摇杆等)
- 本期不实现拖拽,滑动等复杂手势
- 本期不实现指定分辨率截图(固定使用 Game 视图当前分辨率)
- 本期不实现跨场景自动化(每次从 Entry 场景开始)

## 3. 举例覆盖需求和边缘情况

### 例 1: 完整的新手引导自动化测试

**场景**: 验证新手引导流程是否正常

**步骤**:

1. 启动 Play Mode 进入登录场景
2. 使用 `playmode.waitFor` 等待登录相关 UI 出现(或等待到超时)
3. 使用 `log.screenshot` 截图确认当前画面
4. 使用 `playmode.queryUI`(含 `screenRect`)获取目标区域内可交互 UI
5. 点击"开始游戏"按钮(假设路径为 `LoginDialog/StartButton`)
6. 使用 `playmode.waitFor` 等待创建角色 UI 出现(或等待到超时)
7. 使用 `log.screenshot` 再次截图确认
8. 在名称输入框(路径 `CreateRoleDialog/NameInput`)输入"TestPlayer"
9. 点击"确认创建"按钮
10. 再次执行 `playmode.queryUI` 校验关键 UI 是否出现
11. 使用 `playmode.waitFor` 等待主界面按钮出现(或等待到超时)
12. 使用 `log.screenshot` 截图留档
13. 停止 Play Mode

**输入示例**:

```json
{
  "batchId": "test_newbie_001",
  "commands": [
    { "id": "1", "type": "playmode.start" },
    {
      "id": "2",
      "type": "playmode.waitFor",
      "params": {
        "waitSeconds": 3,
        "nameContains": "loginpanel",
        "textContains": "请输入"
      }
    },
    {
      "id": "3",
      "type": "log.screenshot",
      "params": {
        "highlightRect": { "xMin": 420, "xMax": 1500, "yMin": 200, "yMax": 900 }
      }
    },
    {
      "id": "4",
      "type": "playmode.click",
      "params": { "targetPath": "LoginDialog/StartButton" }
    },
    {
      "id": "5",
      "type": "playmode.waitFor",
      "params": {
        "waitSeconds": 2,
        "nameContains": "CreateRoleDialog",
        "textContains": "请输入"
      }
    },
    {
      "id": "6",
      "type": "playmode.setText",
      "params": {
        "targetPath": "CreateRoleDialog/NameInput",
        "text": "TestPlayer"
      }
    },
    {
      "id": "7",
      "type": "playmode.click",
      "params": { "targetPath": "CreateRoleDialog/ConfirmButton" }
    },
    {
      "id": "8",
      "type": "playmode.queryUI",
      "params": {
        "nameContains": "Confirm",
        "interactableOnly": true,
        "maxResults": 30
      }
    },
    {
      "id": "9",
      "type": "playmode.waitFor",
      "params": {
        "waitSeconds": 5,
        "nameContains": "MainDialog"
      }
    },
    {
      "id": "10",
      "type": "log.screenshot",
      "params": {}
    },
    { "id": "11", "type": "playmode.stop" }
  ]
}
```

**预期输出**:

```json
{
  "batchId": "test_newbie_001",
  "results": [
    {"id": "1", "status": "success", "result": {"sessionState": "Starting"}},
    {"id": "2", "status": "success", "result": {"data": {"waitOutcome": "matched", "elapsedMs": 1200, "matchedBy": "textContains", "matchedElement": {"path": "LoginDialog/loginpanel/account", "siblingIndex": 0, "elementId": "LoginDialog/loginpanel/account#0", "name": "account", "text": "请输入...."}}}},
    {"id": "3", "status": "success", "result": {"mode": "single", "imageAbsolutePath": ".../results/test_newbie_001_3.png", "highlightApplied": true}},
    {"id": "4", "status": "success"},
    {"id": "5", "status": "success", "result": {"data": {"waitOutcome": "timeoutReached", "elapsedMs": 2000}}},
    {"id": "6", "status": "success"},
    {"id": "7", "status": "success"},
    {"id": "8", "status": "success", "result": {"data": {"total": 6, "returned": 6, "truncated": false, "uiElements": [...]}}},
    {"id": "9", "status": "success", "result": {"data": {"waitOutcome": "matched", "elapsedMs": 3400, "matchedBy": "nameContains", "matchedElement": {"path": "MainDialog", "siblingIndex": 0, "elementId": "MainDialog#0", "name": "MainDialog", "text": ""}}}},
    {"id": "10", "status": "success", "result": {"mode": "single", "imageAbsolutePath": ".../results/test_newbie_001_10.png", "highlightApplied": false}},
    {"id": "11", "status": "success", "result": {"sessionState": "Stopped"}}
  ]
}
```

### 例 2: 用户手动停止 Play Mode

**场景**: 测试执行过程中用户手动点击 Unity 的 Play 按钮停止

**输入**: 同例 1

**预期行为**:

- 当前执行的命令返回 `status = "error"`
- `error.code = "PLAYMODE_INTERRUPTED"`
- `error.message = "Play Mode was manually stopped by user"`
- 后续命令跳过执行或全部返回错误

### 例 3: 点击不可交互元素

**场景**: 尝试点击一个 `interactable = false` 的按钮

**输入**:

```json
{
  "batchId": "test_error_001",
  "commands": [
    { "id": "1", "type": "playmode.start" },
    {
      "id": "2",
      "type": "playmode.queryUI",
      "params": {
        "nameContains": "GrayedButton",
        "interactableOnly": false,
        "maxResults": 20
      }
    },
    {
      "id": "3",
      "type": "playmode.click",
      "params": { "targetPath": "MainDialog/GrayedButton" }
    }
  ]
}
```

**预期输出**:

```json
{
  "results": [
    {"id": "1", "status": "success"},
    {"id": "2", "status": "success", "result": {"data": {"uiElements": [{"path": "MainDialog/GrayedButton", "interactable": false, ...}]}}},
    {
      "id": "3",
      "status": "error",
      "error": {
        "code": "ELEMENT_NOT_INTERACTABLE",
        "message": "Target element is not interactable",
        "detail": "MainDialog/GrayedButton.interactable = false"
      }
    }
  ]
}
```

### 例 4: 指定场景启动

**场景**: 直接进入某个测试场景,跳过登录流程

**输入**:

```json
{
  "batchId": "test_shop_001",
  "commands": [
    {
      "id": "1",
      "type": "playmode.start",
      "params": { "entryScene": "Assets/Scenes/Test/ShopTestScene.unity" }
    },
    {
      "id": "2",
      "type": "log.screenshot",
      "params": {}
    }
  ]
}
```

### 例 5: stop/start 串行重启 Play Mode

**场景**: 在同一批次中先执行 `playmode.stop`,再执行 `playmode.start`,最后截图.

**输入**:

```json
{
  "batchId": "batch_pm_lottery_fix_002",
  "timeout": 180000,
  "commands": [
    {
      "id": "cmd_stop",
      "type": "playmode.stop",
      "params": {}
    },
    {
      "id": "cmd_start",
      "type": "playmode.start",
      "params": {}
    },
    {
      "id": "cmd_query_verify",
      "type": "playmode.queryUI",
      "params": {
        "interactableOnly": true,
        "maxResults": 120
      }
    },
    {
      "id": "cmd_shot",
      "type": "log.screenshot",
      "params": {}
    }
  ]
}
```

**预期行为**:

- 该调用方式必须被允许,用于重置运行态后再继续自动化流程.
- 不依赖初始状态,先执行 `playmode.stop`,再执行 `playmode.start`.
- 若初始不在 Play Mode,`stop` 可返回 `PLAYMODE_NOT_ACTIVE`,但后续 `start` 仍可继续执行.
- 批次执行过程不应出现非预期的反复开关 Play Mode 行为.

## 4. 输入协议

### 4.1 坐标系定义

**坐标系**: 屏幕坐标系,以 Game 视图左上角为原点 (0,0),X 轴向右,Y 轴向下.

| 参数 | 说明        | 有效范围          | 示例                             |
| ---- | ----------- | ----------------- | -------------------------------- |
| `x`  | 屏幕 X 坐标 | 0 ~ GameView 宽度 | 960 (1920x1080 分辨率的水平中点) |
| `y`  | 屏幕 Y 坐标 | 0 ~ GameView 高度 | 540 (1920x1080 分辨率的垂直中点) |

**重要说明**:

- 坐标值基于 Game 视图当前分辨率,非归一化坐标
- 不考虑 DPI Scaling,直接使用像素值
- 多显示器环境下,坐标相对于 Game 视图窗口,非整个桌面
- 点击/查询时,若坐标超出 Game 视图范围,返回 `INVALID_COORDINATES` 错误

**示例**:

- 示例 A: 1920x1080 分辨率下点击屏幕中心.

```json
{ "type": "playmode.clickAt", "params": { "x": 960, "y": 540 } }
```

- 示例 B: 点击左上角.

```json
{ "type": "playmode.clickAt", "params": { "x": 0, "y": 0 } }
```

### 4.2 命令参数详解

**playmode.start**:

可选参数 `entryScene` 用于指定启动场景.

```json
{
  "type": "playmode.start",
  "params": {
    "entryScene": "Assets/Scenes/GameEntry.unity"
  }
}
```

**playmode.queryUI**:

支持可选筛选参数 `nameContains`,`textContains`,`componentFilter`,`visibleOnly`,`interactableOnly`,`screenRect`,`maxResults`.

```json
{
  "type": "playmode.queryUI",
  "params": {
    "nameContains": "Confirm",
    "componentFilter": ["Button", "InputField"],
    "visibleOnly": true,
    "interactableOnly": true,
    "maxResults": 100
  }
}
```

**playmode.click**:

可选参数 `siblingIndex` 不传时默认 `0`.

```json
{
  "type": "playmode.click",
  "params": {
    "targetPath": "DialogMain/ConfirmButton",
    "siblingIndex": 0
  }
}
```

**playmode.clickAt**:

参数 `x`,`y` 为屏幕坐标.

```json
{
  "type": "playmode.clickAt",
  "params": {
    "x": 960,
    "y": 540
  }
}
```

**playmode.setText**:

```json
{
  "type": "playmode.setText",
  "params": {
    "targetPath": "DialogMain/NameInput",
    "siblingIndex": 0,
    "text": "PlayerName",
    "submit": true
  }
}
```

**playmode.scroll**:

示例为向上滚动列表.

```json
{
  "type": "playmode.scroll",
  "params": {
    "scrollDelta": 120,
    "x": 960,
    "y": 540
  }
}
```

**log.screenshot(由 unity-log 技能提供)**:

用于截图验证当前画面,支持可选红框标注区域.

```json
{
  "type": "log.screenshot",
  "params": {
    "highlightRect": {
      "xMin": 400,
      "xMax": 1520,
      "yMin": 180,
      "yMax": 920
    }
  }
}
```

### 4.3 目标定位方式

点击和设置文本命令使用对象路径定位,支持 `siblingIndex` 参数处理同名对象,与 `prefab.queryComponents` 保持一致.

**定位规则**:

1. **基本路径定位**(`targetPath`): 使用 GameObject 的层级路径,如 `DialogMain/Panel_Content/ConfirmButton`
2. **同名对象定位**(`siblingIndex`): 当路径下有多个同名对象时,使用 `siblingIndex` 指定第几个(从 0 开始)

**示例**:

- 示例 A: 点击第一个 `ConfirmButton`,`siblingIndex` 省略时默认 `0`.

```json
{
  "type": "playmode.click",
  "params": {
    "targetPath": "DialogMain/Panel_Content/ConfirmButton"
  }
}
```

- 示例 B: 点击第二个同名 `ConfirmButton`.

```json
{
  "type": "playmode.click",
  "params": {
    "targetPath": "DialogMain/Panel_Content/ConfirmButton",
    "siblingIndex": 1
  }
}
```

**与 queryUI 的关联**:

`playmode.queryUI` 返回的每个元素包含 `siblingIndex` 字段,可直接用于后续点击命令.

- 步骤 1: 查询 UI.

```json
{
  "id": "query",
  "type": "playmode.queryUI"
}
```

- 步骤 2: 使用返回的 `path + siblingIndex` 点击.

```json
{
  "id": "click",
  "type": "playmode.click",
  "params": {
    "targetPath": "Dialog/ConfirmButton",
    "siblingIndex": 1
  }
}
```

### 5.1 UI 元素结构

说明: `playmode.*` 命令统一采用 `result.meta/data/diagnostics` 外层结构.查询结果固定读取 `result.data.uiElements`.

```json
{
  "meta": {
    "sessionId": "test_newbie_001",
    "sessionState": "Active",
    "commandId": "cmd_query",
    "timestamp": "2026-03-02 10:00:00",
    "durationMs": 24
  },
  "data": {
    "total": 2,
    "returned": 2,
    "truncated": false,
    "uiElements": [
      {
        "name": "ConfirmButton",
        "path": "DialogMain/Panel_Content/ConfirmButton",
        "siblingIndex": 0,
        "elementId": "DialogMain/Panel_Content/ConfirmButton#0",
        "type": "Button",
        "visible": true,
        "interactable": true,
        "screenPosition": { "x": 960, "y": 540 },
        "text": "确认"
      },
      {
        "name": "NameInput",
        "path": "DialogMain/Panel_Content/NameInput",
        "siblingIndex": 1,
        "elementId": "DialogMain/Panel_Content/NameInput#1",
        "type": "InputField",
        "visible": true,
        "interactable": true,
        "screenPosition": { "x": 960, "y": 600 },
        "text": ""
      }
    ]
  },
  "diagnostics": {
    "stage": "query",
    "retryable": false,
    "recoveryHints": []
  }
}
```

### 5.2 错误码定义

| 错误码                     | 说明                | 场景                                |
| -------------------------- | ------------------- | ----------------------------------- |
| `PLAYMODE_NOT_ACTIVE`      | Play Mode 未启动    | 在未启动时调用需要 Play Mode 的命令 |
| `PLAYMODE_START_FAILED`    | 启动 Play Mode 失败 | 场景加载失败等                      |
| `PLAYMODE_INTERRUPTED`     | Play Mode 被中断    | 用户手动停止或崩溃                  |
| `ELEMENT_NOT_FOUND`        | 元素未找到          | targetPath 无效                     |
| `ELEMENT_NOT_INTERACTABLE` | 元素不可交互        | interactable = false                |
| `TIMEOUT`                  | 截图等待超时        | log.screenshot 等待或落盘超时       |
| `SCREENSHOT_FAILED`        | 截图失败            | log.screenshot 无法获取 Game 视图   |
| `NO_ELEMENT_AT_POSITION`   | 指定坐标无 UI 元素  | clickAt 时该位置没有 UI 元素        |
| `INVALID_COORDINATES`      | 坐标非法            | x/y 超出 Game 视图范围或格式错误    |
| `UNSUPPORTED_ELEMENT_TYPE` | 不支持的元素类型    | setText/scroll 目标组件类型不支持   |
| `AMBIGUOUS_TARGET`         | 目标不明确          | 选择器命中多个元素且未指定区分方式  |
| `GAMEVIEW_NOT_AVAILABLE`   | Game 视图不可用     | 编辑器状态下无法获取 Game 视图      |

### 5.3 UI 可交互判定标准

**visible (可见性)判定**:

| 条件                         | 判定逻辑                                                      | 说明                            |
| ---------------------------- | ------------------------------------------------------------- | ------------------------------- |
| `activeInHierarchy`          | `gameObject.activeInHierarchy == true`                        | GameObject 在层级中处于激活状态 |
| `CanvasGroup.alpha`          | `canvasGroup == null \|\| canvasGroup.alpha > 0.01f`          | 无 CanvasGroup 或透明度大于 1%  |
| `CanvasGroup.blocksRaycasts` | `canvasGroup == null \|\| canvasGroup.blocksRaycasts == true` | 无 CanvasGroup 或不阻挡射线     |

**interactable (可交互性)判定**:

| 组件类型                         | 判定属性                                                    | 说明                       |
| -------------------------------- | ----------------------------------------------------------- | -------------------------- |
| `Selectable` (Button, Toggle 等) | `selectable.interactable == true`                           | 组件自身 interactable 属性 |
| `InputField`                     | `inputField.interactable == true`                           | InputField 专用属性        |
| `CanvasGroup`                    | `canvasGroup == null \|\| canvasGroup.interactable == true` | CanvasGroup 层级控制       |

**综合判定规则**:

```
visible = activeInHierarchy
          && (CanvasGroup.alpha > 0.01f)
          && (CanvasGroup.blocksRaycasts == true)

interactable = visible
               && Selectable.interactable == true
               && (CanvasGroup.interactable == true)
```

**点击执行前检查**:

1. 目标元素必须存在 (`ELEMENT_NOT_FOUND`)
2. 目标元素必须可见 (`ELEMENT_NOT_VISIBLE`)
3. 目标元素必须可交互 (`ELEMENT_NOT_INTERACTABLE`)

### 5.4 与 prefab.queryComponents 协议对齐

补充说明:

- `playmode.queryUI` 是唯一推荐的 UI 查询入口,结合筛选参数和 `targetPath/siblingIndex` 完成定位.

**字段对照表**:

| 字段名           | playmode.queryUI | prefab.queryComponents | 说明                     |
| ---------------- | ---------------- | ---------------------- | ------------------------ |
| `name`           | yes              | yes                    | GameObject 名称          |
| `path`           | yes              | yes                    | 层级路径                 |
| `siblingIndex`   | yes              | yes                    | 同名对象索引             |
| `type`           | yes              | yes                    | 组件类型名               |
| `components`     | no               | yes                    | 预制体侧返回所有组件列表 |
| `visible`        | yes              | no                     | 运行时专用:是否可见      |
| `interactable`   | yes              | no                     | 运行时专用:是否可交互    |
| `screenPosition` | yes              | no                     | 运行时专用:屏幕坐标      |
| `text`           | yes              | no                     | 运行时专用:当前文本内容  |

**过滤规则对齐**:

| 规则              | playmode.queryUI                     | prefab.queryComponents |
| ----------------- | ------------------------------------ | ---------------------- |
| `componentFilter` | contains + IgnoreCase + OR           | 相同                   |
| 未传过滤          | 返回所有 UI 元素(含可交互与不可交互) | 返回所有组件(含内置)   |
| 空数组 `[]`       | 返回所有 UI 元素(含可交互与不可交互) | 返回所有组件(含内置)   |

### 5.5 输入约束表

**playmode.start**:

| 字段         | 类型   | 必填 | 约束                                   | 非法值示例                   |
| ------------ | ------ | ---- | -------------------------------------- | ---------------------------- |
| `entryScene` | string | 否   | 必须以 `Assets/` 开头,以 `.unity` 结尾 | `GameEntry.unity` (缺少路径) |

**playmode.click / playmode.setText**:

| 字段           | 类型   | 必填 | 约束                   | 非法值示例   |
| -------------- | ------ | ---- | ---------------------- | ------------ |
| `targetPath`   | string | 是   | 非空,使用 `/` 分隔层级 | `""`, `null` |
| `siblingIndex` | int    | 否   | >= 0                   | `-1`, `1.5`  |

**playmode.clickAt**:

| 字段 | 类型   | 必填 | 约束                     | 非法值示例      |
| ---- | ------ | ---- | ------------------------ | --------------- |
| `x`  | number | 是   | >= 0 且 <= GameView 宽度 | `-10`, 超出范围 |
| `y`  | number | 是   | >= 0 且 <= GameView 高度 | `-10`, 超出范围 |

**playmode.queryUI**:

| 字段               | 类型     | 必填 | 约束                                                 | 非法值示例               |
| ------------------ | -------- | ---- | ---------------------------------------------------- | ------------------------ |
| `nameContains`     | string   | 否   | Trim 后可为空,空视为未传                             | -                        |
| `textContains`     | string   | 否   | Trim 后可为空,按文本 contains + IgnoreCase           | `123`(非字符串)          |
| `componentFilter`  | string[] | 否   | contains + IgnoreCase + OR                           | `[123]`                  |
| `visibleOnly`      | bool     | 否   | 默认 false                                           | `"true"`                 |
| `interactableOnly` | bool     | 否   | 默认 false                                           | `1`                      |
| `screenRect`       | object   | 否   | `{xMin,xMax,yMin,yMax}`,且 `xMin<=xMax`,`yMin<=yMax` | `{"xMin":100,"xMax":50}` |
| `maxResults`       | int      | 否   | > 0,默认 200                                         | `0`,`-1`,`1.5`           |

**playmode.waitFor**:

| 字段           | 类型   | 必填 | 约束          | 说明                                          |
| -------------- | ------ | ---- | ------------- | --------------------------------------------- |
| `waitSeconds`  | number | 是   | > 0           | 最长等待时长,先达到该时长则停止等待并成功返回 |
| `nameContains` | string | 否   | Trim 后可为空 | 对象名 contains + IgnoreCase                  |
| `textContains` | string | 否   | Trim 后可为空 | 文本 contains + IgnoreCase                    |

**约束处理**:

- `nameContains` 和 `textContains` 为 OR 关系,任一命中即停止
- 两者都不传时,允许纯等待到 `waitSeconds`
- 命中时返回 `waitOutcome="matched"`,`matchedBy`,`matchedElement`
- 到时返回 `waitOutcome="timeoutReached"`,命令状态仍为 `success`

**log.screenshot(由 unity-log 技能提供)**:

| 字段            | 类型   | 必填 | 约束                    | 说明             |
| --------------- | ------ | ---- | ----------------------- | ---------------- |
| `highlightRect` | object | 否   | `{xMin,xMax,yMin,yMax}` | 全图红框标注区域 |

**约束处理**:

- `highlightRect` 越界自动裁剪到图像边界,不报错
- `highlightRect` 无效(如 `xMin > xMax`) → 返回 `INVALID_FIELDS`

### 5.6 失败矩阵

| 命令             | 失败场景            | 错误码                     | 错误信息示例                   |
| ---------------- | ------------------- | -------------------------- | ------------------------------ |
| `start`          | 已在 Play Mode      | `PLAYMODE_ALREADY_ACTIVE`  | Already in Play Mode           |
| `start`          | 场景文件不存在      | `PLAYMODE_START_FAILED`    | Scene file not found           |
| `stop`           | 不在 Play Mode      | `PLAYMODE_NOT_ACTIVE`      | Not in Play Mode               |
| `queryUI`        | 不在 Play Mode      | `PLAYMODE_NOT_ACTIVE`      | Not in Play Mode               |
| `click`          | 路径不存在          | `ELEMENT_NOT_FOUND`        | Element not found at path      |
| `click`          | 元素不可见          | `ELEMENT_NOT_VISIBLE`      | Element is not visible         |
| `click`          | 元素不可交互        | `ELEMENT_NOT_INTERACTABLE` | Element.interactable = false   |
| `clickAt`        | 坐标越界            | `INVALID_COORDINATES`      | Coordinates out of bounds      |
| `clickAt`        | 该位置无元素        | `NO_ELEMENT_AT_POSITION`   | No UI element at (x,y)         |
| `queryUI`        | 结果被截断          | `truncated = true`         | Returned results are truncated |
| `setText`        | 目标不是 InputField | `UNSUPPORTED_ELEMENT_TYPE` | Target is not InputField       |
| `log.screenshot` | 截图处理超时        | `TIMEOUT`                  | Screenshot processing timeout  |
| `log.screenshot` | Game 视图不可用     | `GAMEVIEW_NOT_AVAILABLE`   | Game view not available        |
| `log.screenshot` | 截图失败            | `SCREENSHOT_FAILED`        | Failed to capture screenshot   |
| `playmode.*`     | Play Mode 被中断    | `PLAYMODE_INTERRUPTED`     | Play Mode was stopped          |

## 6. 确认事项

以下事项已确认:

### 6.1 命令异步执行模型(已确认)

**方案**: 复用现有 `log.screenshot` 的异步机制

**实现方式**:

- 命令启动后立即返回 `status: "processing"`
- 在 `EditorApplication.update` 中轮询等待
- 等待完成后更新结果为 `status: "completed"` + 截图路径

**外部工具使用流程**:

1. 发送命令到 pending
2. 轮询 results 文件直到状态变为 completed
3. 获取截图路径

### 6.2 待讨论

1. **场景加载等待**: 进入 Play Mode 后场景可能异步加载,是否需要内置场景加载完成检测?
