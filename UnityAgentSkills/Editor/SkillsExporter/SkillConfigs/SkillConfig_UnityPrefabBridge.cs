namespace UnityAgentSkills.SkillsExporter
{
    /// <summary>
    /// unity-prefab-bridge 技能配置.
    /// </summary>
    public static class SkillConfig_UnityPrefabBridge
    {
        /// <summary>
        /// 技能名称.
        /// </summary>
        public const string SkillName = "unity-prefab-bridge";

        /// <summary>
        /// 技能描述.
        /// </summary>
        public const string SkillDescription = "桥接 Unity 场景中的 prefab instance 与 prefab asset. 触发关键词:Unity:Prefab桥接,Unity prefab bridge,Prefab实例桥接";

        /// <summary>
        /// SKILL.md 的完整内容.
        /// </summary>
        public const string SkillMarkdown = @"---
name: unity-prefab-bridge
description: 桥接 Unity 场景中的 prefab instance 与 prefab asset. 触发关键词:Unity:Prefab桥接,Unity prefab bridge,Prefab实例桥接
---

# Unity Prefab Bridge

## Instructions

### Context

本技能用于处理 Unity 场景中的 prefab instance 与 prefab asset 之间的桥接关系,包括:
- 将 prefab 实例化到指定已加载场景.
- 查询场景对象是否为 prefab instance,以及它来源于哪个 prefab.
- 查询 prefab instance 与普通 prefab,variant,nested prefab 的关系.
- 查询 instance 相对来源 prefab 的 overrides.
- 将 overrides apply 回 prefab asset.
- 将 overrides revert 回 prefab 默认值.
- 对 prefab instance 执行 unpack.

### Steps

**工具脚本**: `<Scripts Directory>/execute_unity_command.py`

> 💡 推荐使用 `uv run`,并将 JSON 参数写在一行内.
> 💡 批量处理统一复用框架级 `batchId + commands[]`,本技能不提供专用 `*Batch` 命令.

### 通用输入规则

- `batchId` 必填.
- `timeout` 可选,默认 30000.
- `commands` 必填.
- 所有桥接命令仅允许在 Edit Mode 执行.
- 所有场景内对象定位统一使用 `sceneName + objectPath + siblingIndex`.
- 所有 prefab 资源统一使用 `prefabPath`,格式必须是 `Assets/.../*.prefab`.

### 命令清单

1. `prefabBridge.instantiateInScene`
2. `prefabBridge.getInstanceSource`
3. `prefabBridge.getInstanceRelationship`
4. `prefabBridge.getOverrides`
5. `prefabBridge.applyOverrides`
6. `prefabBridge.revertOverrides`
7. `prefabBridge.unpackInstance`

### Key notes

**1) prefabBridge.instantiateInScene**
- 必填: `sceneName`,`prefabPath`.
- 可选: `parentPath`,`parentSiblingIndex`,`insertSiblingIndex`,`name`,`localPosition`,`localRotation`,`localScale`,`anchorMin`,`anchorMax`,`anchoredPosition`,`sizeDelta`.
- 返回: `sceneName`,`prefabPath`,`instancePath`,`siblingIndex`,`isPrefabInstance`,`saved`.

**2) prefabBridge.getInstanceSource**
- 必填: `sceneName`,`objectPath`.
- 可选: `siblingIndex`.
- 返回对象是否是 prefab instance,来源 prefab 路径,以及 instance kind.

**3) prefabBridge.getInstanceRelationship**
- 必填: `sceneName`,`objectPath`.
- 可选: `siblingIndex`.
- 返回 `instanceKind`,`sourcePrefabPath`,`variantBasePrefabPath`,`outerInstanceRootPath`,`relationshipSummary`.

**4) prefabBridge.getOverrides**
- 必填: `sceneName`,`objectPath`.
- 可选: `siblingIndex`.
- 返回 `hasOverrides`,`overrideCount`,`modifiedProperties`,`addedComponents`,`removedComponents`,`addedGameObjects`,`removedGameObjects` 等结构.
- 每条 override 都带稳定 `overrideId`.

**5) prefabBridge.applyOverrides**
- 必填: `sceneName`,`objectPath`.
- 可选: `siblingIndex`,`applyMode`.
- `applyMode` 支持 `all|selected`.
- `selected` 时必须传 `overrideIds[]`.
- variant 实例默认且仅允许写回当前直接来源 variant.

**6) prefabBridge.revertOverrides**
- 必填: `sceneName`,`objectPath`.
- 可选: `siblingIndex`,`revertMode`.
- `revertMode` 支持 `all|selected`.
- `selected` 时必须传 `overrideIds[]`.
- 成功后自动保存目标 scene.

**7) prefabBridge.unpackInstance**
- 必填: `sceneName`,`objectPath`.
- 可选: `siblingIndex`,`unpackMode`.
- `unpackMode` 支持 `outermost|completely`.
- 成功后自动保存目标 scene.

### Example

```bash
uv run ""<Scripts Directory>/execute_unity_command.py"" '{""batchId"":""batch_prefab_bridge_001"",""timeout"":30000,""commands"":[{""id"":""cmd_instantiate"",""type"":""prefabBridge.instantiateInScene"",""params"":{""sceneName"":""Main"",""prefabPath"":""Assets/Resources/Prefabs/ButtonConfirm.prefab"",""parentPath"":""Canvas/Panel"",""parentSiblingIndex"":0}},{""id"":""cmd_source"",""type"":""prefabBridge.getInstanceSource"",""params"":{""sceneName"":""Main"",""objectPath"":""Canvas/Panel/ButtonConfirm"",""siblingIndex"":0}}]}'
```

### Notes

- 最稳妥的工作流是: 先 `instantiateInScene` 或先定位对象,再 `getInstanceSource/getInstanceRelationship/getOverrides`,最后才做 `apply/revert/unpack`.
- `applyOverrides(selected)` 与 `revertOverrides(selected)` 都依赖 `overrideIds[]`.
- 批量桥接请直接在一个批次里组合多个单命令,并查看框架级 `results[]`.
";
    }
}
