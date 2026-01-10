# 提交与 PR

已安装 gh.提交 PR 时,为避免多行文本截断或特殊字符被转义,MUST 只能使用 --body-file 使用文件形式提交,git commit --file 提交也一样用文件!提交成功后清理该临时文件.
中文提交信息和 pr 信息

# 当前任务

<!-- 一个 JS 脚本用于 AI 查看 Unity Prefab(专注于查看),脚本位置在 Unity 预制体 AI 友好查询/prefab_viewer.js
输入 .prefab 的相对路径或绝对路径,命令行参数模式使用,输出为 JSON,stdout 仅输出 JSON,错误写 stderr 并返回非 0 退出码
一次只做一种输出,不支持在同一次调用里混合多种输出

功能与参数

1. 树状层级结构: --tree
   - 输出从 Prefab 根节点开始的 GameObject 树,每个节点仅包含 name + id(fileID) + children
2. 根节点元数据: --root-meta
   - 输出根节点 GameObject 的有意义元数据,例如 m_Layer,m_TagString,m_Name,m_IsActive,m_NavMeshLayer,m_StaticEditorFlags
3. GameObject 组件列表: --components-of <gameobjectFileID>[,<gameobjectFileID>...]
   - 输出指定 GameObject 的组件列表(仅组件 id + 组件类型 + MonoBehaviour 脚本名)
4. 组件详情: --component <componentFileID>[,<componentFileID>...]
   - 输出指定组件的所有参数 key/value

关键规则

- GameObject 的 guid 使用 Prefab YAML 中的 fileID
- MonoBehaviour 通过 m_Script.guid 在 .meta 中反查脚本名(xxx.cs)
- 如果脚本缺失,标记 $status: "MissingScript"
- 数据标准化: 简单原始类型直接输出,引用与复杂对象用封装结构
  - 引用: {type:"ref", raw:"{fileID:..., guid:..., type:...}"}
  - 复杂对象: {type:"object", raw:"{x:..., y:..., z:...}"}

工具依赖

- 优先使用 rg,不可用时回退到 grep

测试素材

- Unity 预制体 AI 友好查询/Dev_example 内含 prefab,meta,脚本及其 meta 供测试
- 示例脚本 meta guid: 3383921b82e57b7439e7d76d6d21d9de



requirements/prefab_viewer.md 需求文档 -->
整体功能已完成,优化中
工具检测重复调用
脚本 GUID 缓存机制
路径检查优化 
可配置的脚本目录名