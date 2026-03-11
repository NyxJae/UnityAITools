# UnityAgentSkills 需求文档

- `01_整体与框架需求.md`: 框架整体目标,目录约定,输入/输出协议,文件流转,恢复机制,扩展要求,以及框架验收清单,并包含 json 命令文件与其 `.meta` 的联动归档/清理规则.
- `02_日志功能需求_log.md`: 日志命令 `log.query`(查询日志),`log.screenshot`(截图)与 `log.refresh`(刷新并等待完成)的需求与验收口径,并包含截图产物与其 `.meta` 的联动清理规则.
- `03_预制体查看.md`: 预制体查看功能 `prefab.view` 的需求与验收口径.
- `04_预制体编辑.md`: `unity-prefab-edit` 对应的 `prefab.*` 编辑类命令最终需求与验收口径,并包含桥接层中与 prefab 回写相关的 `prefabBridge.*` 需求.
- `05_场景查看.md`: 场景查看功能 `scene.open`,`scene.queryHierarchy`,`scene.queryComponents` 的需求与验收口径.
- `06_PlayMode_UI交互.md`: PlayMode UI 自动化交互能力的需求与验收口径.
- `06_场景编辑.md`: 场景编辑功能 `scene.*` 编辑类命令的最终需求与验收口径,并包含桥接层中与 scene 落位,识别,还原,解包相关的 `prefabBridge.*` 需求.
- `98_后台线程监控刷新Unity.md`: 后台自动编译与刷新监控服务的需求边界,用于区分手动 `log.refresh` 与后台自动能力.
- `99_用户界面插件与skill和脚本生成.md`: Unity 技能导出插件的需求与约束,包含用户界面设计与 Python 脚本生成规范.

## 文档说明

- 需求文档按工作流与能力边界组织.
- 常用主流程优先单独成文.
- 原本独立存在的查看体验增强与组件属性展开增强内容,已整理并入 `03_预制体查看.md` 与 `05_场景查看.md`.
- 保持插件依赖少,便于安装到各个 Unity 项目.
