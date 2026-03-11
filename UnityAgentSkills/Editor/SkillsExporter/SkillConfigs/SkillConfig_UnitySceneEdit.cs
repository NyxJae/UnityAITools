namespace UnityAgentSkills.SkillsExporter
{
    /// <summary>
    /// unity-scene-edit 技能配置.
    /// </summary>
    public static class SkillConfig_UnitySceneEdit
    {
        /// <summary>
        /// 技能名称.
        /// </summary>
        public const string SkillName = "unity-scene-edit";

        /// <summary>
        /// 技能描述.
        /// </summary>
        public const string SkillDescription = "编辑 Unity 场景. 触发关键词:Unity:场景编辑,Unity scene edit,编辑场景";

        /// <summary>
        /// SKILL.md 的完整内容.
        /// </summary>
        public const string SkillMarkdown = @"---
name: unity-scene-edit
description: 编辑 Unity 场景. 触发关键词:Unity:场景编辑,Unity scene edit,编辑场景
---

# Unity Scene Edit

## Instructions

### Context

本技能用于编辑 Unity 已加载场景中的对象与组件,包括改名,创建,删除,移动,排序,组件增删改,Transform/UI 布局修改,以及批量事务编辑.

使用建议:
- 先使用 `unity-scene-view` 查看并确认 `sceneName`,`objectPath`,`siblingIndex`,`componentIndex`,再执行编辑.
- 所有编辑命令都必须显式传入 `sceneName`,系统不会猜测目标场景.
- 所有编辑命令仅允许在 Edit Mode 执行.Play Mode 调用会返回 `error.code=ONLY_ALLOWED_IN_EDIT_MODE`,且 `error.detail` 包含 `OnlyAllowedInEditMode`.

### Steps

**工具脚本**: `<Scripts Directory>/execute_unity_command.py`

**最简单的调用方式** - 直接命令行传参(推荐):

> 💡 可使用 `python` 或 `uv run` 执行,推荐 `uv run`.注意,请将 JSON 参数写在一行内.
> 💡 脚本最好加引号包裹,避免路径解析问题.

**通用输入规则**:
- `batchId` 必填,批次唯一标识.
- `timeout` 可选,默认 30000.
- `commands` 必填,命令数组.
- 下面 11 个编辑命令都要求顶层 `params.sceneName` 必填.
- 编辑前请先查看,不要手写猜测 `objectPath`.

**命令清单**:
1. `scene.setGameObjectProperties`: 修改 GameObject 基础属性.
2. `scene.renameGameObject`: 修改对象名称.
3. `scene.createGameObject`: 创建对象.
4. `scene.deleteGameObject`: 删除对象.
5. `scene.moveOrCopyGameObject`: 在同一场景内移动或复制对象.
6. `scene.setSiblingIndex`: 调整同级排序.
7. `scene.addComponent`: 添加组件.
8. `scene.setComponentProperties`: 修改组件属性.
9. `scene.deleteComponent`: 删除组件.
10. `scene.setTransform`: 修改 Transform 的 local 字段.
11. `scene.setRectTransform`: 修改 RectTransform 布局字段.
12. `scene.batchEdit`: 同场景批量事务编辑.

### Key command notes

**1) scene.setGameObjectProperties**
- 必填: `sceneName`,`objectPath`,`properties`.
- 可选: `siblingIndex`.
- 常用 `properties`: `name`,`tag`,`layer`,`isActive`,`isStatic`,`hideFlags`.

**2) scene.renameGameObject**
- 必填: `sceneName`,`objectPath`,`newName`.
- 可选: `siblingIndex`.
- 返回 `oldName`,`newName`,`oldPath`,`newPath`.

**3) scene.createGameObject**
- 必填: `sceneName`,`name`.
- 可选: `parentPath`,`parentSiblingIndex`,`insertSiblingIndex`,`initialProperties`.
- 返回新对象路径与实际插入序号.

**4) scene.deleteGameObject**
- 必填: `sceneName`,`objectPath`.
- 可选: `siblingIndex`.
- 级联删除子物体.

**5) scene.moveOrCopyGameObject**
- 必填: `sceneName`,`objectPath`,`parentPath`.
- 可选: `siblingIndex`,`parentSiblingIndex`,`targetSiblingIndex`,`isCopy`.
- 仅允许在同一个 `sceneName` 内移动或复制,不支持跨场景改父节点.

**6) scene.setSiblingIndex**
- 必填: `sceneName`,`objectPath`,`newSiblingIndex`.
- 可选: `siblingIndex`.
- 仅调整同一父节点下的排序.

**7) scene.addComponent**
- 必填: `sceneName`,`objectPath`,`componentType`.
- 可选: `siblingIndex`,`initialProperties`.
- 返回 `componentType`,`componentIndex`.

**8) scene.setComponentProperties**
- 必填: `sceneName`,`objectPath`,`componentType`,`properties`.
- 可选: `siblingIndex`,`componentIndex`.
- `properties` 支持基础类型,数组,嵌套对象,以及 `$ref` 引用协议.

**9) scene.deleteComponent**
- 必填: `sceneName`,`objectPath`,`componentType`.
- 可选: `siblingIndex`,`componentIndex`.

**10) scene.setTransform**
- 必填: `sceneName`,`objectPath`.
- 可选: `siblingIndex`,`localPosition`,`localRotationEuler`,`localScale`.
- 至少提供一个可写字段,否则返回 `INVALID_FIELDS`.
- 返回 `modifiedFields`.

**11) scene.setRectTransform**
- 必填: `sceneName`,`objectPath`.
- 可选: `siblingIndex`,`anchorMin`,`anchorMax`,`pivot`,`anchoredPosition`,`sizeDelta`,`offsetMin`,`offsetMax`.
- 目标必须有 `RectTransform`,否则返回 `COMPONENT_NOT_FOUND`.
- 至少提供一个可写字段,否则返回 `INVALID_FIELDS`.
- 返回 `modifiedFields`.

**12) scene.batchEdit**
- 必填: `sceneName`,`operations`.
- 可选: `mode`,支持 `stopOnError`(默认) 或 `continueOnError`.
- `operations[].params` 中禁止再次出现 `sceneName`,由顶层统一提供.
- 返回固定字段 `operationResults[]` 和整体 `saved`.
- 严格事务: 只要任一子操作失败,整体 `saved=false`,不得产生部分落盘.
- `stopOnError` 下,后续未执行子操作应返回 `error.code=SKIPPED`.

### Examples

**例 1: 先查看再改名**

先用 `unity-scene-view` 找到目标对象的 `sceneName`,`objectPath`,`siblingIndex`,再执行:

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_scene_rename_001"",""timeout"":30000,""commands"":[{""id"":""cmd_rename"",""type"":""scene.renameGameObject"",""params"":{""sceneName"":""Main"",""objectPath"":""Canvas/Panel/ConfirmButton"",""siblingIndex"":0,""newName"":""ConfirmButtonPrimary""}}]}'
```

**例 2: 给按钮补组件属性**

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_scene_component_001"",""timeout"":30000,""commands"":[{""id"":""cmd_component"",""type"":""scene.setComponentProperties"",""params"":{""sceneName"":""Main"",""objectPath"":""Canvas/Panel/ConfirmButtonPrimary"",""siblingIndex"":0,""componentType"":""UnityEngine.UI.Button"",""componentIndex"":0,""properties"":{""interactable"":true}}}]}'
```

**例 3: 批量编辑同一个场景**

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_scene_batch_edit_001"",""timeout"":30000,""commands"":[{""id"":""cmd_batch"",""type"":""scene.batchEdit"",""params"":{""sceneName"":""Main"",""mode"":""stopOnError"",""operations"":[{""id"":""op_create"",""type"":""scene.createGameObject"",""params"":{""name"":""HelpButton"",""parentPath"":""Canvas/Panel"",""parentSiblingIndex"":0}},{""id"":""op_layout"",""type"":""scene.setRectTransform"",""params"":{""objectPath"":""Canvas/Panel/HelpButton"",""siblingIndex"":0,""anchorMin"":{""x"":0.5,""y"":0.5},""anchorMax"":{""x"":0.5,""y"":0.5},""anchoredPosition"":{""x"":0.0,""y"":-120.0}}},{""id"":""op_add_button"",""type"":""scene.addComponent"",""params"":{""objectPath"":""Canvas/Panel/HelpButton"",""siblingIndex"":0,""componentType"":""UnityEngine.UI.Button""}}]}}]}'
```

### Notes

- 本技能面向非专业人士,最稳妥的流程永远是: 先查看,再编辑.
- 多场景同时加载时,`sceneName` 是强制定位字段,不可省略.
- 单命令成功后会自动保存对应场景.
- `scene.batchEdit` 内子操作结果统一放在 `operationResults[]` 中,子操作 result 不应自行声明最终 `saved`.
- 如果你不确定路径,先回到 `unity-scene-view` 重新查询.

";
    }
}
