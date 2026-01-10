# Unity Prefab AI 查看器 - 实现计划书

## 项目概述

**目标**: 创建一个 JS 脚本用于 AI 查看 Unity Prefab,专注于查看功能,支持命令行参数模式,输出 JSON 格式的 Prefab 结构和组件信息。

**目标脚本位置**: `E:\Project\UnityAITools\Unity预制体AI友好查询\prefab_viewer.js`

**技术栈**: Node.js + 手写轻量级 YAML 解析器(无外部依赖)

---

## 一、文件结构设计

### 1.1 单脚本架构

采用单脚本 `prefab_viewer.js`,通过模块化函数组织代码:

```
prefab_viewer.js
├── 配置区 (脚本顶部)
│   ├── ASSETS_PATH: Unity Assets 绝对路径
│   └── UNITY_TYPE_MAP: Unity 类型 ID 映射表
├── 工具函数区
│   ├── errorExit(message, code): 统一错误输出与退出
│   ├── detectTool(): 检测 rg/grep 工具
│   └── normalizeValue(value): 数据标准化
├── 核心解析区
│   ├── parseArgs(argv): 命令行参数解析
│   ├── parsePrefab(content): Prefab YAML 解析
│   ├── parseDocumentBlocks(content): 文档块分割
│   ├── parseKeyValuePairs(block): 键值对解析
│   └── buildMappings(blocks): 构建映射表
├── 查询功能区
│   ├── findScriptName(guid): MonoBehaviour 脚本名反查
│   ├── getUnityTypeName(typeId): Unity 类型识别
│   ├── buildTree(rootId): 构建树状结构
│   ├── getRootMeta(): 获取根节点元数据
│   ├── getComponentsOf(gameobjectIds): 获取组件列表
│   └── getComponentDetails(componentIds): 获取组件详情
└── 主程序入口
    └── main(): 主流程控制
```

### 1.2 设计原则

- **模块化**: 每个函数职责单一,高内聚低耦合
- **可扩展**: 预留扩展点,便于添加新功能
- **可维护**: 代码清晰,注释完整
- **无依赖**: 不依赖第三方库,纯 Node.js 实现

---

## 二、核心模块详细设计

### 2.1 配置区 (脚本顶部)

```javascript
// 用户必须配置此路径
const ASSETS_PATH = "F:\\UnityProject\\RXJH\\RXJH_307_mini\\Code\\Assets";

// Unity 类型 ID 映射表
const UNITY_TYPE_MAP = {
  1: "GameObject",
  4: "Transform",
  224: "RectTransform",
  114: "MonoBehaviour",
  222: "CanvasRenderer",
  // 常见 UI 类型
  114: "Image",
  114: "Text",
  114: "Button",
  223: "Canvas",
  114: "CanvasGroup",
  // 可根据需要扩展
};

// 退出码定义
const EXIT_CODES = {
  SUCCESS: 0,
  GENERAL_ERROR: 1,
  ARGUMENT_ERROR: 2,
  SYSTEM_ERROR: 3
};
```

### 2.2 工具函数区

#### 2.2.1 errorExit(message, code)

```javascript
function errorExit(message, code) {
  console.error(`Error: ${message}`);
  process.exit(code);
}
```

**用途**: 统一错误输出到 stderr,返回指定退出码

#### 2.2.2 detectTool()

```javascript
function detectTool() {
  const { execSync } = require('child_process');
  try {
    execSync('rg --version', { stdio: 'ignore' });
    return 'rg';
  } catch (e) {
    return 'grep';
  }
}
```

**用途**: 检测 rg 是否可用,不可用时回退到 grep

#### 2.2.3 normalizeValue(value)

```javascript
function normalizeValue(value) {
  // null 值: {fileID: 0}
  if (value === '{fileID: 0}' || value === 'null') {
    return null;
  }
  
  // 数字类型: 0, 1, 3.14
  if (/^-?\d+(\.\d+)?$/.test(value)) {
    return Number(value);
  }
  
  // 布尔类型: 0, 1 (Unity 中使用 0/1 表示 false/true)
  if (value === '0' || value === '1') {
    return Number(value);
  }
  
  // 引用类型: {fileID: xxx, guid: xxx, type: xxx}
  if (value.includes('fileID') && (value.includes('guid') || value.includes('type'))) {
    return { type: 'ref', raw: value };
  }
  
  // 对象类型: {x: 0, y: 0, z: 0}
  if (value.includes(':') && value.includes(',') && !value.includes('fileID')) {
    return { type: 'object', raw: value };
  }
  
  // 字符串类型: 其他所有情况
  return { type: 'string', raw: value };
}
```

**用途**: 根据值类型进行标准化处理

### 2.3 命令行参数解析器

#### 2.3.1 parseArgs(argv)

```javascript
function parseArgs(argv) {
  const args = argv.slice(2);
  const result = {
    prefabPath: null,
    outputType: null,
    targetIds: []
  };
  
  for (let i = 0; i < args.length; i++) {
    const arg = args[i];
    
    if (arg === '--tree') {
      if (result.outputType) errorExit('only one output type is allowed', EXIT_CODES.ARGUMENT_ERROR);
      result.outputType = 'tree';
    } else if (arg === '--root-meta') {
      if (result.outputType) errorExit('only one output type is allowed', EXIT_CODES.ARGUMENT_ERROR);
      result.outputType = 'root-meta';
    } else if (arg === '--components-of') {
      if (result.outputType) errorExit('only one output type is allowed', EXIT_CODES.ARGUMENT_ERROR);
      result.outputType = 'components-of';
      if (i + 1 >= args.length) errorExit('--components-of requires argument', EXIT_CODES.ARGUMENT_ERROR);
      result.targetIds = args[++i].split(',').map(id => id.trim());
    } else if (arg === '--component') {
      if (result.outputType) errorExit('only one output type is allowed', EXIT_CODES.ARGUMENT_ERROR);
      result.outputType = 'component';
      if (i + 1 >= args.length) errorExit('--component requires argument', EXIT_CODES.ARGUMENT_ERROR);
      result.targetIds = args[++i].split(',').map(id => id.trim());
    } else if (!arg.startsWith('--')) {
      result.prefabPath = arg;
    }
  }
  
  // 验证参数
  if (!result.outputType) {
    errorExit('must specify one output type (--tree, --root-meta, --components-of, or --component)', EXIT_CODES.ARGUMENT_ERROR);
  }
  
  if (!result.prefabPath) {
    errorExit('prefab path is required', EXIT_CODES.ARGUMENT_ERROR);
  }
  
  return result;
}
```

### 2.4 YAML 轻量级解析器

#### 2.4.1 parseDocumentBlocks(content)

```javascript
function parseDocumentBlocks(content) {
  // 移除 YAML 头部
  const withoutHeader = content.replace(/^%YAML\s+[\d.]+\n/, '');
  
  // 按 --- !u! 分割文档块
  const blockRegex = /---\s+!u!(\d+)\s+&(\d+)\n([\s\S]+?)(?=\n---\s+!u!|$)/g;
  const blocks = [];
  let match;
  
  while ((match = blockRegex.exec(withoutHeader)) !== null) {
    blocks.push({
      typeId: match[1],
      fileID: match[2],
      content: match[3]
    });
  }
  
  return blocks;
}
```

**用途**: 将 Prefab 文档分割为多个块,每个块包含类型ID、fileID 和内容

#### 2.4.2 parseKeyValuePairs(blockContent)

```javascript
function parseKeyValuePairs(blockContent) {
  const lines = blockContent.split('\n');
  const result = {};
  const stack = [{ obj: result, indent: -1 }];
  
  for (const line of lines) {
    if (!line.trim()) continue;
    
    const indent = line.search(/\S/);
    const trimmed = line.trim();
    
    // 跳过类型声明行 (GameObject:, RectTransform:, 等)
    if (trimmed.endsWith(':') && !trimmed.includes(' ')) continue;
    
    // 解析键值对
    const colonIndex = trimmed.indexOf(':');
    if (colonIndex === -1) continue;
    
    const key = trimmed.substring(0, colonIndex);
    const valueStr = trimmed.substring(colonIndex + 1).trim();
    
    // 处理数组项 (- {fileID: xxx})
    if (key === '-') {
      const currentLevel = stack[stack.length - 1];
      if (!Array.isArray(currentLevel.obj)) {
        // 创建数组
        const parentKey = Object.keys(currentLevel.obj).pop();
        currentLevel.obj[parentKey] = [];
        currentLevel.obj = currentLevel.obj[parentKey];
      }
      const arrayItem = {};
      currentLevel.obj.push(arrayItem);
    } else {
      // 处理普通键值对
      const currentLevel = stack[stack.length - 1];
      currentLevel.obj[key] = valueStr;
    }
  }
  
  return result;
}
```

**用途**: 解析块内容为键值对对象

#### 2.4.3 buildMappings(blocks)

```javascript
function buildMappings(blocks) {
  const fileIDToBlock = new Map();
  const gameObjects = new Map();
  const components = new Map();
  const componentToGameObject = new Map();
  
  for (const block of blocks) {
    const blockData = {
      typeId: block.typeId,
      fileID: block.fileID,
      data: parseKeyValuePairs(block.content)
    };
    
    fileIDToBlock.set(block.fileID, blockData);
    
    // GameObject 类型 (typeId = 1)
    if (block.typeId === '1') {
      gameObjects.set(block.fileID, blockData);
      
      // 提取组件列表
      if (blockData.data.m_Component) {
        for (const comp of blockData.data.m_Component) {
          const compFileID = comp.component.replace('{fileID: ', '').replace('}', '');
          componentToGameObject.set(compFileID, block.fileID);
        }
      }
    } else {
      // 组件类型
      components.set(block.fileID, blockData);
    }
  }
  
  return {
    fileIDToBlock,
    gameObjects,
    components,
    componentToGameObject
  };
}
```

**用途**: 构建映射表,便于快速查找

### 2.5 查询功能区

#### 2.5.1 findScriptName(guid)

```javascript
function findScriptName(guid) {
  const { execSync } = require('child_process');
  const tool = detectTool();
  
  try {
    let command;
    if (tool === 'rg') {
      command = `rg -l "^guid: ${guid}" "${ASSETS_PATH}" --type-add 'meta:*.meta' -t meta`;
    } else {
      command = `grep -r "^guid: ${guid}" "${ASSETS_PATH}" --include="*.meta" -l`;
    }
    
    const result = execSync(command, { encoding: 'utf-8' });
    const metaPath = result.trim().split('\n')[0];
    
    if (!metaPath) {
      return { $status: 'MissingScript', guid };
    }
    
    // 从路径中提取脚本名: xxx.cs.meta -> xxx.cs
    const scriptName = metaPath.replace(/\.meta$/, '').split(/[/\\]/).pop();
    
    return scriptName;
  } catch (e) {
    return { $status: 'MissingScript', guid };
  }
}
```

**用途**: 通过 GUID 反查 MonoBehaviour 脚本名

#### 2.5.2 getUnityTypeName(typeId)

```javascript
function getUnityTypeName(typeId) {
  return UNITY_TYPE_MAP[typeId] || 'GameObject';
}
```

**用途**: 根据 Unity 类型 ID 获取类型名

#### 2.5.3 buildTree(rootId, mappings)

```javascript
function buildTree(rootId, mappings) {
  const rootBlock = mappings.gameObjects.get(rootId);
  if (!rootBlock) return null;
  
  function buildNode(blockId) {
    const block = mappings.gameObjects.get(blockId);
    if (!block) return null;
    
    const node = {
      name: block.data.m_Name || 'Unknown',
      id: blockId,
      children: []
    };
    
    // 获取子节点
    if (block.data.m_Children) {
      for (const childRef of block.data.m_Children) {
        const childFileID = childRef.replace('{fileID: ', '').replace('}', '');
        const childNode = buildNode(childFileID);
        if (childNode) {
          node.children.push(childNode);
        }
      }
    }
    
    return node;
  }
  
  return buildNode(rootId);
}
```

**用途**: 递归构建 GameObject 树状结构

#### 2.5.4 getRootMeta(mappings)

```javascript
function getRootMeta(mappings) {
  // 找到第一个 GameObject 作为根节点
  const rootId = Array.from(mappings.gameObjects.keys())[0];
  const rootBlock = mappings.gameObjects.get(rootId);
  
  if (!rootBlock) {
    errorExit('root GameObject not found', EXIT_CODES.GENERAL_ERROR);
  }
  
  const metaFields = ['m_Layer', 'm_TagString', 'm_Name', 'm_IsActive', 'm_NavMeshLayer', 'm_StaticEditorFlags'];
  const result = {};
  
  for (const field of metaFields) {
    if (rootBlock.data[field] !== undefined) {
      result[field] = normalizeValue(rootBlock.data[field]);
    }
  }
  
  return result;
}
```

**用途**: 获取根节点元数据

#### 2.5.5 getComponentsOf(gameobjectIds, mappings)

```javascript
function getComponentsOf(gameobjectIds, mappings) {
  const result = {};
  
  for (const goId of gameobjectIds) {
    const goBlock = mappings.gameObjects.get(goId);
    if (!goBlock) {
      errorExit(`GameObject not found: ${goId}`, EXIT_CODES.GENERAL_ERROR);
    }
    
    const components = [];
    
    if (goBlock.data.m_Component) {
      for (const compRef of goBlock.data.m_Component) {
        const compFileID = compRef.component.replace('{fileID: ', '').replace('}', '');
        const compBlock = mappings.fileIDToBlock.get(compFileID);
        
        if (!compBlock) continue;
        
        const compInfo = {
          id: compFileID,
          type: getUnityTypeName(compBlock.typeId)
        };
        
        // 如果是 MonoBehaviour,查找脚本名
        if (compBlock.typeId === '114' && compBlock.data.m_Script) {
          const guidMatch = compBlock.data.m_Script.match(/guid:\s*([a-f0-9]+)/i);
          if (guidMatch) {
            const scriptName = findScriptName(guidMatch[1]);
            compInfo.script = scriptName;
          }
        }
        
        components.push(compInfo);
      }
    }
    
    result[goId] = components;
  }
  
  return result;
}
```

**用途**: 获取指定 GameObject 的组件列表

#### 2.5.6 getComponentDetails(componentIds, mappings)

```javascript
function getComponentDetails(componentIds, mappings) {
  const result = {};
  
  for (const compId of componentIds) {
    const compBlock = mappings.fileIDToBlock.get(compId);
    if (!compBlock) {
      errorExit(`component not found: ${compId}`, EXIT_CODES.GENERAL_ERROR);
    }
    
    const details = {};
    
    for (const [key, value] of Object.entries(compBlock.data)) {
      details[key] = normalizeValue(value);
    }
    
    result[compId] = details;
  }
  
  return result;
}
```

**用途**: 获取指定组件的详细信息

### 2.6 主程序入口

```javascript
function main() {
  try {
    // 解析参数
    const args = parseArgs(process.argv);
    
    // 读取 Prefab 文件
    const fs = require('fs');
    let prefabPath = args.prefabPath;
    
    // 转换为绝对路径
    if (!fs.existsSync(prefabPath)) {
      // 尝试相对路径
      const resolvedPath = require('path').resolve(prefabPath);
      if (fs.existsSync(resolvedPath)) {
        prefabPath = resolvedPath;
      } else {
        errorExit(`file not found: ${args.prefabPath}`, EXIT_CODES.GENERAL_ERROR);
      }
    }
    
    const content = fs.readFileSync(prefabPath, 'utf-8');
    
    // 解析 Prefab
    const blocks = parseDocumentBlocks(content);
    const mappings = buildMappings(blocks);
    
    // 根据输出类型执行相应操作
    let output;
    
    switch (args.outputType) {
      case 'tree':
        const rootId = Array.from(mappings.gameObjects.keys())[0];
        output = buildTree(rootId, mappings);
        break;
        
      case 'root-meta':
        output = getRootMeta(mappings);
        break;
        
      case 'components-of':
        output = getComponentsOf(args.targetIds, mappings);
        break;
        
      case 'component':
        output = getComponentDetails(args.targetIds, mappings);
        break;
    }
    
    // 输出 JSON 到 stdout
    console.log(JSON.stringify(output, null, 2));
    
    // 成功退出
    process.exit(EXIT_CODES.SUCCESS);
    
  } catch (error) {
    errorExit(error.message, EXIT_CODES.GENERAL_ERROR);
  }
}

// 启动主程序
main();
```

---

## 三、实现步骤和顺序

### 阶段 1: 基础框架搭建 (TODO 1.1-1.4)

1. 创建 `prefab_viewer.js` 文件
2. 定义脚本顶部配置 (ASSETS_PATH, UNITY_TYPE_MAP, EXIT_CODES)
3. 实现工具函数 (errorExit, detectTool, normalizeValue)
4. 设计模块接口

### 阶段 2: 命令行参数解析 (TODO 2.1-2.4)

1. 实现 parseArgs 函数
2. 实现参数验证逻辑
3. 实现路径处理逻辑
4. 测试参数解析功能

### 阶段 3: YAML 解析器 (TODO 3.1-3.5)

1. 实现 parseDocumentBlocks 函数
2. 实现 parseKeyValuePairs 函数
3. 实现 buildMappings 函数
4. 测试解析器功能

### 阶段 4: 数据标准化 (TODO 4.1-4.5)

1. 完善 normalizeValue 函数
2. 测试各种值类型的标准化
3. 处理边缘情况

### 阶段 5: MonoBehaviour 脚本反查 (TODO 5.1-5.5)

1. 实现 detectTool 函数
2. 实现 findScriptName 函数
3. 实现 Missing Script 处理
4. 测试脚本反查功能

### 阶段 6: Unity 类型识别 (TODO 6.1-6.4)

1. 完善 UNITY_TYPE_MAP
2. 实现 getUnityTypeName 函数
3. 测试类型识别功能

### 阶段 7: 功能实现 - --tree (TODO 7.1-7.4)

1. 实现 buildTree 函数
2. 实现 --tree 输出逻辑
3. 测试树状结构输出

### 阶段 8: 功能实现 - --root-meta (TODO 8.1-8.4)

1. 实现 getRootMeta 函数
2. 实现 --root-meta 输出逻辑
3. 测试元数据输出

### 阶段 9: 功能实现 - --components-of (TODO 9.1-9.6)

1. 实现 getComponentsOf 函数
2. 实现 --components-of 输出逻辑
3. 测试组件列表输出

### 阶段 10: 功能实现 - --component (TODO 10.1-10.5)

1. 实现 getComponentDetails 函数
2. 实现 --component 输出逻辑
3. 测试组件详情输出

### 阶段 11: 错误处理 (TODO 11.1-11.5)

1. 完善错误输出函数
2. 实现各种错误场景处理
3. 测试错误处理逻辑

### 阶段 12: 测试与文档 (TODO 12.1-12.7)

1. 编写单元测试
2. 编写使用文档
3. 进行完整测试
4. 修复发现的问题

---

## 四、重要注意事项

### 4.1 性能优化

- **脚本反查缓存**: 可以缓存 GUID 到脚本名的映射,避免重复搜索
- **映射表预构建**: parsePrefab 时一次性构建所有映射表
- **工具检测缓存**: detectTool 结果缓存,避免重复检测

### 4.2 边缘情况处理

- **空值处理**: 处理 null、空字符串、空数组等情况
- **文件路径处理**: 处理 Windows/Unix 路径分隔符
- **编码问题**: 确保文件读取使用 UTF-8 编码
- **大文件处理**: Prefab 文件可能很大,注意内存使用

### 4.3 错误处理

- **文件不存在**: 返回退出码 1
- **参数错误**: 返回退出码 2
- **系统错误**: 返回退出码 3
- **所有错误信息输出到 stderr,stdout 保持为空**

### 4.4 可扩展性

- **预留扩展点**: 便于添加新的输出类型
- **模块化设计**: 每个功能独立,便于维护
- **配置化**: ASSETS_PATH 和 UNITY_TYPE_MAP 可配置

### 4.5 代码质量

- **注释完整**: 每个函数都有清晰的注释
- **命名规范**: 使用驼峰命名,语义清晰
- **错误处理**: 每个关键步骤都有错误处理
- **日志输出**: 调试时可输出详细日志(可选)

---

## 五、测试计划

### 5.1 单元测试

1. 测试 --tree 输出
2. 测试 --root-meta 输出
3. 测试 --components-of 输出
4. 测试 --component 输出
5. 测试错误处理

### 5.2 集成测试

使用 `Dev_example/example1.prefab` 进行完整测试:

```bash
# 测试 --tree
node prefab_viewer.js Dev_example/example1.prefab --tree

# 测试 --root-meta
node prefab_viewer.js Dev_example/example1.prefab --root-meta

# 测试 --components-of
node prefab_viewer.js Dev_example/example1.prefab --components-of 160547937799403005

# 测试 --component
node prefab_viewer.js Dev_example/example1.prefab --component 1744541728560894454

# 测试错误处理
node prefab_viewer.js notexist.prefab --tree
```

### 5.3 性能测试

测试大文件处理性能,确保在合理时间内完成解析。

---

## 六、文档计划

### 6.1 README.md

包含以下内容:

1. 项目简介
2. 安装说明
3. 使用示例
4. 参数说明
5. 输出格式说明
6. 常见问题

### 6.2 使用示例

```bash
# 查看树状结构
node prefab_viewer.js path/to/prefab.prefab --tree

# 查看根节点元数据
node prefab_viewer.js path/to/prefab.prefab --root-meta

# 查看指定 GameObject 的组件列表
node prefab_viewer.js path/to/prefab.prefab --components-of 160547937799403005,183714419140684073

# 查看指定组件的详细信息
node prefab_viewer.js path/to/prefab.prefab --component 1744541728560894454
```

---

## 七、总结

本计划书详细描述了 Unity Prefab AI 查看器的实现方案,包括:

1. **文件结构**: 单脚本架构,模块化设计
2. **核心模块**: 命令行解析、YAML 解析、数据标准化、脚本反查、类型识别
3. **实现步骤**: 分 12 个阶段,逐步实现功能
4. **注意事项**: 性能优化、边缘情况处理、错误处理、可扩展性、代码质量
5. **测试计划**: 单元测试、集成测试、性能测试
6. **文档计划**: README.md 和使用示例

通过本计划,团队成员可以清晰地了解整个项目的实现方案,按照步骤有序地完成开发任务。