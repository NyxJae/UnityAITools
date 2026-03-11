namespace UnityAgentSkills.SkillsExporter
{
    /// <summary>
    /// unity-prefab-edit技能配置.
    /// </summary>
    public static class SkillConfig_UnityPrefabEdit
    {
        /// <summary>
        /// 技能名称.
        /// </summary>
        public const string SkillName = "unity-prefab-edit";

        /// <summary>
        /// 技能描述.
        /// </summary>
        public const string SkillDescription = "编辑 Unity 预制体. 触发关键词:Unity:预制体编辑,Unity prefab edit,编辑预制体";

        /// <summary>
        /// SKILL.md的完整内容.
        /// </summary>
        public const string SkillMarkdown = @"---
name: unity-prefab-edit
description: 编辑 Unity 预制体. 触发关键词:Unity:预制体编辑,Unity prefab edit,编辑预制体
---

# Unity Prefab Edit

## Instructions

### Context

本技能用于编辑 Unity 预制体(创建/删除/移动GameObject,修改组件与Transform,批量事务编辑等).

使用建议:
- 先使用 `unity-prefab-view` 技能确认目标对象路径,再使用本技能执行编辑.

### Steps

**工具脚本**: `<Scripts Directory>/execute_unity_command.py`

**最简单的调用方式** - 直接命令行传参(推荐):

> 💡 可使用 `python` 或 `uv run` 执行,推荐 `uv run`.注意,以防命令行对多行字符串处理异常,请将JSON参数写在一行内.
> 💡 脚本最好加引号包裹,避免路径解析问题.

**命令参数说明**:

- `batchId` 必填,批次唯一标识(建议 16-32 字符,仅 `[a-zA-Z0-9_-]`)
- `timeout` 可选,超时时间(毫秒),默认 30000
- `commands` 必填,命令数组,每个元素包含:
  - `id` 必填,命令唯一标识
  - `type` 必填,命令类型
  - `params` 必填,命令参数

**命令 1: prefab.setGameObjectProperties (修改 GameObject 属性)**

关键参数:
- `prefabPath`,`objectPath`,`properties` 必填.
- `siblingIndex` 默认 0,用于同名对象精确定位.

`properties` 常用字段:
- `name`,`tag`,`layer`,`isActive`,`isStatic`,`hideFlags`

关键返回:
- `modifiedProperties`,`instanceID`,`saved`.

**命令 2: prefab.deleteGameObject (删除 GameObject,级联删除子物体)**

关键参数:
- `prefabPath`,`objectPath` 必填.
- `siblingIndex` 默认 0.

关键返回:
- `deletedObjectPath`,`deletedSiblingIndex`,`instanceID`,`saved`.

**命令 3: prefab.moveOrCopyGameObject (移动或复制 GameObject)**

关键参数:
- `prefabPath`,`objectPath` 必填.
- `siblingIndex` 默认 0.
- `isCopy` 可选,默认 false.为 true 时复制.
- `newParentPath` 可选,不传表示保持原父节点.
- `newParentSiblingIndex` 默认 0.
- `targetSiblingIndex` 默认 -1,-1 表示移动/复制到末尾.

关键返回:
- `sourceObjectPath`,`sourceSiblingIndex`,`targetObjectPath`,`targetSiblingIndex`,`instanceID`,`saved`.

**命令 4: prefab.createGameObject (创建子物体)**

关键参数:
- `prefabPath`,`name` 必填.
- `parentPath` 可选,不传时默认 prefab 根节点.
- `parentSiblingIndex` 默认 0,用于同名父节点定位.
- `insertSiblingIndex` 默认 -1,-1 表示插入到末尾.
- `initialProperties` 可选,支持 `name`,`tag`,`layer`,`isActive`,`isStatic`,`hideFlags`.

关键返回:
- `createdObjectPath`,`createdSiblingIndex`,`insertSiblingIndexApplied`,`instanceID`,`saved`.

**命令 5: prefab.addComponent (添加组件)**

关键参数:
- `prefabPath`,`objectPath`,`componentType` 必填.
- `siblingIndex` 默认 0.
- `initialProperties` 可选,用于组件初始属性写入.

关键返回:
- `componentType`,`componentIndex`,`componentInstanceID`,`instanceID`,`saved`.

**命令 6: prefab.setComponentProperties (修改组件属性)**

关键参数:
- `prefabPath`,`objectPath`,`componentType`,`properties` 必填.
- `siblingIndex` 默认 0.
- `componentIndex` 默认 0,按同类型组件计数.
- `properties` 支持基础类型,数组,嵌套对象,以及 `$ref` 引用协议.

`$ref` 支持:
- `kind=prefabGameObject`
- `kind=prefabComponent`
- `kind=asset`
- 也支持 `null` 置空引用.

关键返回:
- `componentType`,`componentIndex`,`componentInstanceID`,`modifiedProperties`,`saved`.

**命令 7: prefab.deleteComponent (删除组件)**

关键参数:
- `prefabPath`,`objectPath`,`componentType` 必填.
- `siblingIndex` 默认 0.
- `componentIndex` 默认 0.

关键返回:
- `deletedComponentType`,`deletedComponentIndex`,`deletedComponentInstanceID`,`saved`.

**命令 8: prefab.renameGameObject (改名 GameObject)**

关键参数:
- `prefabPath`,`objectPath`,`newName` 必填.
- `siblingIndex` 默认 0,用于同名对象精确定位.

关键返回:
- `oldName`,`newName`,`oldPath`,`newPath`,`instanceID`,`saved`.

约束:
- 不允许编辑预制体根节点(rootPath).如果 `objectPath` 指向根节点,将返回 `status=error`,`error.code=INVALID_FIELDS`.

**命令 9: prefab.setSiblingIndex (调整同级排序)**

关键参数:
- `prefabPath`,`objectPath`,`newSiblingIndex` 必填.
- `siblingIndex` 默认 0,用于同名对象精确定位.

关键返回:
- `oldSiblingIndex`,`newSiblingIndexRequested`,`newSiblingIndexApplied`,`instanceID`,`saved`.

约束:
- 不允许编辑预制体根节点(rootPath).

**命令 10: prefab.setTransform (修改 Transform Local 值)**

关键参数:
- `prefabPath`,`objectPath` 必填.
- `siblingIndex` 默认 0,用于同名对象精确定位.
- 仅支持修改 Local 字段,未提供的字段保持不变:
  - `localPosition`:{`x`,`y`,`z`}
  - `localRotationEuler`:{`x`,`y`,`z`} (单位:度)
  - `localScale`:{`x`,`y`,`z`}

关键返回:
- `modifiedFields` 仅包含实际发生变更的字段.
- 当 `modifiedFields` 包含对应字段时,会回显修改后的 `localPosition`/`localRotationEuler`/`localScale`.
- `instanceID`,`saved`.

约束:
- 未提供任何可写字段时返回 `status=error`,`error.code=INVALID_FIELDS`.
- 不允许编辑预制体根节点(rootPath).

**命令 11: prefab.setRectTransform (修改 UI 布局,需要 RectTransform)**

关键参数:
- `prefabPath`,`objectPath` 必填.
- `siblingIndex` 默认 0,用于同名对象精确定位.
- 仅修改提供的字段,未提供的字段保持不变:
  - `anchorMin`:{`x`,`y`}
  - `anchorMax`:{`x`,`y`}
  - `pivot`:{`x`,`y`}
  - `anchoredPosition`:{`x`,`y`}
  - `sizeDelta`:{`x`,`y`}
  - `offsetMin`:{`x`,`y`}
  - `offsetMax`:{`x`,`y`}

关键返回:
- `modifiedFields` 仅包含实际发生变更的字段.
- 当 `modifiedFields` 包含对应字段时,会回显修改后的对应数值.
- `instanceID`,`saved`.

约束:
- 目标对象必须有 `RectTransform`,否则返回 `status=error`,`error.code=COMPONENT_NOT_FOUND`.
- 未提供任何可写字段时返回 `status=error`,`error.code=INVALID_FIELDS`.
- 不允许编辑预制体根节点(rootPath).

**命令 12: prefab.batchEdit (批量编辑,严格事务)**

关键参数:
- `prefabPath` 必填.
- `mode` 可选,`stopOnError`(默认)或 `continueOnError`.
- `operations` 必填,子操作数组.每项包含:
  - `id` 必填.
  - `type` 必填.
  - `params` 必填.注意:子操作的 `params` 内必须省略 `prefabPath`,只填写 `objectPath`,`siblingIndex` 等字段.

关键返回:
- `operationCount`,`successCount`,`failedCount`,`mode`.
- `saved`: 严格事务落盘标志.任一子操作失败则 `saved=false`,且必须保证 prefab asset 文件未发生变化(不产生部分落盘).
- `operationResults[]`: 子操作结果数组.每项至少包含 `id`,`type`,`status`(success|error),以及 `result` 或 `error`.

批量示例 (prefab.batchEdit):

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_prefab_batch_edit_001"",""timeout"":30000,""commands"":[{""id"":""cmd_batch_edit"",""type"":""prefab.batchEdit"",""params"":{""prefabPath"":""Assets/ResourcesAB/UIPrefabs/DialogMain.prefab"",""mode"":""stopOnError"",""operations"":[{""id"":""op_rename"",""type"":""prefab.renameGameObject"",""params"":{""objectPath"":""DialogMain/Panel_Content/UIButton_Confirm"",""siblingIndex"":0,""newName"":""UIButton_Confirm_New""}},{""id"":""op_layout"",""type"":""prefab.setRectTransform"",""params"":{""objectPath"":""DialogMain/Panel_Content/ConfirmButton"",""siblingIndex"":0,""anchorMin"":{""x"":0.5,""y"":0.5},""anchorMax"":{""x"":0.5,""y"":0.5}}}]}}]}'
```

说明:
- 在 `mode=stopOnError` 下,当某个子操作失败后,未执行的子操作将返回 `status=error` 且 `error.code=SKIPPED`.
- 在 `prefab.batchEdit` 上下文中,子操作 result 内不得包含 `saved` 字段,统一以 batchEdit 的 `saved` 作为最终落盘结果.

### Notes

- 命令行方式无需创建任何文件,直接在终端执行即可
- `prefab.batchEdit` 是严格事务: 任一子操作失败 => `saved=false` 且不落盘(不产生部分落盘)
- `prefab.batchEdit` 在 `mode=stopOnError` 下,失败后的未执行子操作返回 `status=error` 且 `error.code=SKIPPED`
- 新增编辑命令族(rootPath 保护)不允许编辑预制体根节点,命中返回 `error.code=INVALID_FIELDS`
- 框架级批量(commands[])的 command `status` 可能为 `processing`/`completed`/`error`,而 `prefab.batchEdit.operationResults[].status` 必须为 `success`/`error`

";
    }
}
