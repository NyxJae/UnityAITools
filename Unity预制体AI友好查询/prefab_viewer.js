#!/usr/bin/env node
// -*- coding: utf-8 -*-

const fs = require("fs");
const { execSync } = require("child_process");
const path = require("path");

/**
 * Unity Prefab AI 友好查询工具
 *
 * 功能: 读取 Unity Prefab(YAML 格式)并以 JSON 输出
 * 用途: 方便 AI 或非专业人员快速查看 Prefab 结构与组件信息
 *
 * 使用方式:
 *   node prefab_viewer.js -h
 *   node prefab_viewer.js <prefab路径> --tree
 *   node prefab_viewer.js <prefab路径> --root-meta
 *   node prefab_viewer.js <prefab路径> --components-of <gameobjectFileID>[,<gameobjectFileID>...]
 *   node prefab_viewer.js <prefab路径> --component <componentFileID>[,<componentFileID>...]
 */

// ============================================================================
// 1. 脚本顶部配置区
// ============================================================================

// Assets 路径配置(使用前用户必须配置)
// 支持多种路径格式:
//   - Windows 双反斜杠: F:\\UnityProject\\YourProject\\Assets
//   - 正斜杠: F:/UnityProject/YourProject/Assets
const ASSETS_PATH = "F:\\UnityProject\\YourProject\\Assets";

// 脚本目录名称(用户可根据项目结构修改)
const SCRIPT_DIR_NAME = "Scripts";

// ============================================================================
// 脚本搜索超时配置（单位：毫秒）
// ============================================================================
// 如果项目过大或搜索较慢，用户可适当调高超时阈值
// 超时时间说明：
//   - 第一阶段（Scripts 目录）：预期大部分脚本在此，设置较短超时
//   - 第二阶段（Assets 全局）：搜索范围更大，设置较长超时
// 推荐值：小型项目 5-10s，中型项目 10-20s，大型项目 20-60s
const SEARCH_TIMEOUT = {
  // ripgrep 在 Scripts 目录的超时时间（默认 5000ms = 5秒）
  SCRIPTS_RG: 5000,
  // grep 在 Scripts 目录的超时时间（默认 10000ms = 10秒）
  SCRIPTS_GREP: 10000,
  // ripgrep 在 Assets 全局的超时时间（默认 10000ms = 10秒）
  ASSETS_RG: 10000,
  // grep 在 Assets 全局的超时时间（默认 30000ms = 30秒）
  ASSETS_GREP: 30000,
};

// 规范化路径，支持各种路径格式（正斜杠/双反斜杠）
const NORMALIZED_ASSETS_PATH = path.normalize(ASSETS_PATH);
// 验证路径是否存在
if (!fs.existsSync(NORMALIZED_ASSETS_PATH)) {
  console.warn(`警告: ASSETS_PATH 不存在: ${NORMALIZED_ASSETS_PATH}`);
  console.warn("请检查本脚本顶部的 ASSETS_PATH 的路径配置是否正确,不正确则帮用户修改下");
}

// Unity 类型 ID 映射表
const UNITY_TYPE_MAP = {
  1: "GameObject",
  4: "Transform",
  224: "RectTransform",
  114: "MonoBehaviour",
  222: "CanvasRenderer",
  100: "Camera",
  108: "Light",
  115: "MonoScript",
  128: "ParticleRenderer",
  198: "ParticleSystem",
  199: "ParticleSystemRenderer",
  205: "LODGroup",
  215: "Canvas",
  216: "CanvasGroup",
  225: "CanvasScaler",
  226: "GraphicRaycaster",
  227: "Physics2DRaycaster",
  228: "PhysicsRaycaster",
  231: "Grid",
  232: "Tilemap",
  233: "TilemapRenderer",
  258: "LineRenderer",
  326: "VideoPlayer",
  327: "VideoClip",
  329: "AudioSource",
  330: "AudioListener",
  331: "AudioReverbZone",
  332: "AudioEchoFilter",
  333: "AudioLowPassFilter",
  334: "AudioHighPassFilter",
  335: "AudioDistortionFilter",
  336: "AudioChorusFilter",
  337: "AudioReverbFilter",
};

// Unity 官方内置 MonoBehaviour 组件 GUID 映射表
// 这些是 Unity 引擎内置的组件，不存放在项目 Scripts 目录中
// 注: Unity 2022.3.52f1c1 (LTS) 验证
const UNITY_BUILTIN_SCRIPTS = {
  // UI 基础组件
  "5f7201a12d95ffc409449d95f23cf332": "UnityEngine.UI.Text",
  fe87c0e1cc204ed48ad3b37840f39efc: "UnityEngine.UI.Image",
  "1344c3c82d62a2a41a3576d8abb8e3ea": "UnityEngine.UI.RawImage",
  "4e29b1a8efbd4b44bb3f3716e73f07ff": "UnityEngine.UI.Button",
  "1aa08ab6e0800fa44ae55d278d1423e3": "UnityEngine.UI.ScrollRect",
  "9085046f02f69544eb97fd06b6048fe2": "UnityEngine.UI.Toggle",
  "517523331f2b484496d1c1b2a6d9819c": "UnityEngine.UI.LayoutElement",
  "59f8146938fff824cb5fd77236b75775b": "UnityEngine.UI.GridLayoutGroup",
  "3245ec927659c4140ac4f8d17403cc18": "UnityEngine.UI.HorizontalLayoutGroup",
  "3312d7739989d2b4e91e6319e9a96d76": "UnityEngine.UI.ContentSizeFitter",
  "31a19414c41e5ae4aae2af33fee712f6": "UnityEngine.UI.Mask",
  "263d95d21cafbc94fbef85db44cd680e": "UnityEngine.UI.Outline",
  "14a8ccb29cd8f8042953183e58f8417e": "UnityEngine.UI.Shadow",
  "91096d0a8d1d4fb4bbe140e83c8a3f41": "UnityEngine.UI.CanvasRenderer",
  "22434a6c8a24107499a80d12afede315": "UnityEngine.UI.RectMask2D",

  // 布局组件
  "306cc8c2b49d7114eaa3623786fc2126": "UnityEngine.UI.LayoutElement",

  // 交互组件
  "2a4db7a114972834c8e4117be1d82ba3": "UnityEngine.UI.Scrollbar",
  "7b743370ac3e4ec2a1668f5455a8ef8a": "UnityEngine.UI.Dropdown",

  // Canvas相关组件
  "0cd44c1031e13a943bb63640046fad76": "UnityEngine.UI.CanvasScaler",
  e41b4bbe00d928244b4f8f8a8a75e8f6: "UnityEngine.CanvasGroup",
  dc42784cf147c0c48a680349fa168899: "UnityEngine.UI.GraphicRaycaster",

  // 事件系统组件
  "76c392e42b5098c458856cdf6ecaaaa1": "UnityEngine.EventSystems.EventSystem",
  "4f231c4fb786f3946a6b90b886c48677":
    "UnityEngine.EventSystems.StandaloneInputModule",

  // TextMeshPro组件
  f4688fdb7df04437aeb418b961361dc5: "TMPro.TextMeshProUGUI",
  "2da0c512f12947e489f739169773d7ca": "TMPro.TMP_InputField",

  // 其他常见内置组件
  "3245ec927659c4140ac4f8d17403cc18": "UnityEngine.EventSystems.EventTrigger",
};

// 退出码定义
const EXIT_CODES = {
  SUCCESS: 0,
  GENERAL_ERROR: 1,
  PARAM_ERROR: 2,
  SYSTEM_ERROR: 3,
};

// ============================================================================
// 2. 工具函数区
// ============================================================================

/**
 * 统一错误输出函数
 * @param {string} message - 错误消息
 * @param {number} code - 退出码，默认为 GENERAL_ERROR
 */
function errorExit(message, code = EXIT_CODES.GENERAL_ERROR) {
  console.error(message);
  process.exit(code);
}

/**
 * 检测可用工具（rg 或 grep）
 * @returns {string} 可用的工具名称 ('rg' 或 'grep')
 */
function detectTool() {
  // 优先使用 ripgrep (rg)，因为它更快更高效
  try {
    execSync("rg --version", { stdio: "ignore" });
    return "rg";
  } catch (e) {
    // rg 不可用，回退到 grep
    return "grep";
  }
}

// 可用搜索工具(在脚本启动时检测一次)
const AVAILABLE_TOOL = detectTool();

// 脚本GUID缓存(避免重复搜索.meta文件)
const SCRIPT_GUID_CACHE = new Map();

// 脚本目录路径(避免重复构建)
const SCRIPTS_PATH = path.join(NORMALIZED_ASSETS_PATH, SCRIPT_DIR_NAME);

// 脚本目录存在性检查(避免重复文件系统操作)
const SCRIPTS_EXISTS = fs.existsSync(SCRIPTS_PATH);
/**
 * 数据标准化函数
 * @param {any} value - 原始值
 * @returns {any} 标准化后的值
 */
function normalizeValue(value) {
  // 1. 如果是 null 或 undefined，返回 null
  if (value === null || value === undefined) {
    return null;
  }

  // 2. 如果是数字，直接返回
  if (typeof value === "number") {
    return value;
  }

  // 3. 如果是布尔值，直接返回
  if (typeof value === "boolean") {
    return value;
  }

  // 4. 如果是字符串，封装为 {type: 'string', raw: value}
  if (typeof value === "string") {
    return { type: "string", raw: value };
  }

  // 5. 如果是对象
  if (typeof value === "object") {
    // 如果对象有 type 属性（来自 parseValue），直接返回
    if (value.type !== undefined) {
      return value;
    }

    // 检查是否是引用（包含 fileID）
    if (value.fileID !== undefined) {
      // 如果 fileID 为 0，返回 null
      if (value.fileID === 0) {
        return null;
      }
      // 否则，封装为 {type: 'ref', raw: JSON.stringify(value)}
      return { type: "ref", raw: JSON.stringify(value) };
    }

    // 否则，封装为 {type: 'object', raw: JSON.stringify(value)}
    return { type: "object", raw: JSON.stringify(value) };
  }

  // 其他情况直接返回
  return value;
}

// ============================================================================
// 3. 核心解析函数区
// ============================================================================

/**
 * 命令行参数解析
 * @param {string[]} args - 命令行参数数组
 * @returns {Object} 解析后的参数对象
 */
function parseArgs(args) {
  const result = {
    outputType: null,
    prefabPath: null,
    targetIDs: [],
  };

  // 解析参数
  for (let i = 0; i < args.length; i++) {
    const arg = args[i];

    // 优先检测 --help 参数，无论在哪个位置都显示帮助信息并退出
    if (arg === "--help" || arg === "-h") {
      console.log(`
Unity Prefab AI 查看器 - 用于 AI 查看 Unity Prefab 文件

使用方法:
  node prefab_viewer.js <prefab路径> <输出类型> [参数]

输出类型 (必选其一):
  --tree              输出树状层级结构
  --root-meta         输出根节点元数据
  --components-of <ID> 输出 GameObject 组件列表
  --component <ID>    输出组件详情

参数:
  <prefab路径>        Prefab 文件路径(相对或绝对)
  --components-of     后跟 GameObject fileID，多个 ID 用逗号分隔
  --component         后跟组件 fileID，多个 ID 用逗号分隔

示例:
  node prefab_viewer.js example.prefab --tree
  node prefab_viewer.js example.prefab --root-meta
  node prefab_viewer.js example.prefab --components-of 160547937799403005
  node prefab_viewer.js example.prefab --component 1744541728560894454
`);
      process.exit(0);
    } else if (arg === "--tree") {
      if (result.outputType) {
        errorExit(
          "Error: only one output type is allowed",
          EXIT_CODES.PARAM_ERROR
        );
      }
      result.outputType = "tree";
    } else if (arg === "--root-meta") {
      if (result.outputType) {
        errorExit(
          "Error: only one output type is allowed",
          EXIT_CODES.PARAM_ERROR
        );
      }
      result.outputType = "root-meta";
    } else if (arg === "--components-of") {
      if (result.outputType) {
        errorExit(
          "Error: only one output type is allowed",
          EXIT_CODES.PARAM_ERROR
        );
      }
      result.outputType = "components-of";
      // 下一个参数是 ID 列表
      i++;
      // 检查是否为 --help 参数
      if (i < args.length && (args[i] === "--help" || args[i] === "-h")) {
        console.log(`
Unity Prefab AI 查看器 - 用于 AI 查看 Unity Prefab 文件

使用方法:
  node prefab_viewer.js <prefab路径> <输出类型> [参数]

输出类型 (必选其一):
  --tree              输出树状层级结构
  --root-meta         输出根节点元数据
  --components-of <ID> 输出 GameObject 组件列表
  --component <ID>    输出组件详情

参数:
  <prefab路径>        Prefab 文件路径(相对或绝对)
  --components-of     后跟 GameObject fileID，多个 ID 用逗号分隔
  --component         后跟组件 fileID，多个 ID 用逗号分隔

示例:
  node prefab_viewer.js example.prefab --tree
  node prefab_viewer.js example.prefab --root-meta
  node prefab_viewer.js example.prefab --components-of 160547937799403005
  node prefab_viewer.js example.prefab --component 1744541728560894454
`);
        process.exit(0);
      }
      if (i >= args.length || args[i].startsWith("--")) {
        errorExit(
          "Error: --components-of or --component requires IDs",
          EXIT_CODES.PARAM_ERROR
        );
      }
      // 提取 ID 列表并过滤空值
      const idList = args[i]
        .split(",")
        .map((id) => id.trim())
        .filter((id) => id.length > 0);

      // 验证是否包含有效 ID
      if (idList.length === 0) {
        errorExit("Error: IDs cannot be empty", EXIT_CODES.PARAM_ERROR);
      }

      result.targetIDs = idList;
    } else if (arg === "--component") {
      if (result.outputType) {
        errorExit(
          "Error: only one output type is allowed",
          EXIT_CODES.PARAM_ERROR
        );
      }
      result.outputType = "component";
      // 下一个参数是 ID 列表
      i++;
      // 检查是否为 --help 参数
      if (i < args.length && (args[i] === "--help" || args[i] === "-h")) {
        console.log(`Unity Prefab AI 查看器 - 用于 AI 查看 Unity Prefab 文件

使用方法:
  node prefab_viewer.js <prefab路径> <输出类型> [参数]

输出类型 (必选其一):
  --tree              输出树状层级结构
  --root-meta         输出根节点元数据
  --components-of <ID> 输出 GameObject 组件列表
  --component <ID>    输出组件详情

参数:
  <prefab路径>        Prefab 文件路径(相对或绝对)
  --components-of     后跟 GameObject fileID，多个 ID 用逗号分隔
  --component         后跟组件 fileID，多个 ID 用逗号分隔

示例:
  node prefab_viewer.js example.prefab --tree
  node prefab_viewer.js example.prefab --root-meta
  node prefab_viewer.js example.prefab --components-of 160547937799403005
  node prefab_viewer.js example.prefab --component 1744541728560894454
  `);
        process.exit(0);
      }
      if (i >= args.length || args[i].startsWith("--")) {
        errorExit(
          "Error: --components-of or --component requires IDs",
          EXIT_CODES.PARAM_ERROR
        );
      }
      // 提取 ID 列表并过滤空值
      const idList = args[i]
        .split(",")
        .map((id) => id.trim())
        .filter((id) => id.length > 0);

      // 验证是否包含有效 ID
      if (idList.length === 0) {
        errorExit("Error: IDs cannot be empty", EXIT_CODES.PARAM_ERROR);
      }

      result.targetIDs = idList;
    } else if (!arg.startsWith("--")) {
      // prefab 文件路径
      result.prefabPath = path.resolve(arg);
    }
  }

  // 验证参数
  if (!result.outputType) {
    errorExit(
      "Error: must specify one output type (--tree, --root-meta, --components-of, or --component)",
      EXIT_CODES.PARAM_ERROR
    );
  }

  if (!result.prefabPath) {
    errorExit("Error: prefab file path is required", EXIT_CODES.PARAM_ERROR);
  }

  return result;
}

/**
 * YAML 文档分割和块解析
 * @param {string} content - Prefab 文件内容
 * @returns {Array} 文档块数组
 */
function parseDocumentBlocks(content) {
  // 使用正则表达式匹配所有块头: --- !u!xxx &fileID
  // 注意：移除 ^ 锚点，因为 m 标志会使 ^ 匹配每行开头而非字符串开头
  const blockRegex = /--- !u!(\d+)\s+&(\d+)\s*\n([\s\S]*?)(?=\n--- !u!|$)/g;
  const blocks = [];
  let match;

  while ((match = blockRegex.exec(content)) !== null) {
    const typeId = parseInt(match[1], 10);
    const fileID = match[2];
    const blockContent = match[3].trim();

    // 跳过空块
    if (!blockContent) continue;

    blocks.push({
      typeId: typeId,
      fileID: fileID,
      content: blockContent,
    });
  }

  return blocks;
}

/**
 * 键值对解析
 * @param {string} block - 文档块内容
 * @returns {Object} 键值对对象
 */
function parseKeyValuePairs(block) {
  const result = {};
  const lines = block.split("\n");

  // 首先找到类型行（如 "GameObject:", "RectTransform:" 等）
  let firstNonEmptyLine = 0;
  for (let i = 0; i < lines.length; i++) {
    if (lines[i].trim() && !lines[i].trim().startsWith("m_")) {
      firstNonEmptyLine = i;
      break;
    }
  }

  // 追踪当前数组键（用于处理缩进的数组项）
  let currentArrayKey = null;
  let currentArrayIndent = 0;

  // 处理每一行
  for (let i = firstNonEmptyLine; i < lines.length; i++) {
    const line = lines[i];
    const trimmed = line.trim();

    // 跳过空行
    if (!trimmed) continue;

    // 提取缩进
    const indentMatch = line.match(/^(\s*)/);
    const indent = indentMatch ? indentMatch[1].length : 0;

    // 处理数组项（以 "- " 开头）
    if (trimmed.startsWith("- ")) {
      // 如果有当前数组键，且缩进匹配（数组项缩进 >= 父键缩进）
      if (currentArrayKey && indent >= currentArrayIndent) {
        const arrayItem = trimmed.substring(2).trim();

        // 先检查是否是对象引用（优先判断，因为有花括号）
        if (arrayItem.startsWith("{") && arrayItem.endsWith("}")) {
          // 直接对象引用格式: {fileID: xxx, guid: xxx, type: xxx}
          const value = parseValue(arrayItem);

          // 添加到数组
          if (!result[currentArrayKey]) {
            result[currentArrayKey] = [];
          }
          result[currentArrayKey].push(value);
        } else {
          // 键值对格式: fileID: xxx
          const colonIndex = arrayItem.indexOf(":");
          if (colonIndex !== -1) {
            const key = arrayItem.substring(0, colonIndex).trim();
            const valueStr = arrayItem.substring(colonIndex + 1).trim();

            // 解析值
            const value = parseValue(valueStr);

            // 构建数组项对象
            const item = {};
            item[key] = value;

            // 添加到数组
            if (!result[currentArrayKey]) {
              result[currentArrayKey] = [];
            }
            result[currentArrayKey].push(item);
          }
        }
      }
      continue;
    }

    // 检查是否是新的键（以 m_ 开头）
    if (trimmed.startsWith("m_")) {
      const colonIndex = trimmed.indexOf(":");
      if (colonIndex !== -1) {
        const key = trimmed.substring(0, colonIndex).trim();
        const valueStr = trimmed.substring(colonIndex + 1).trim();

        if (valueStr === "") {
          // 空值，可能是数组声明
          currentArrayKey = key;
          currentArrayIndent = indent;
          if (!result[key]) {
            result[key] = [];
          }
        } else if (valueStr === "[]") {
          // 空数组
          result[key] = [];
          currentArrayKey = null;
        } else {
          // 有值，直接赋值
          currentArrayKey = null; // 重置数组键
          const value = parseValue(valueStr);
          result[key] = value;
        }
      }
      continue;
    }

    // 其他行（类型行等）跳过
    continue;
  }

  return result;
}

/**
 * 解析单个值
 * @param {string} valueStr - 值字符串
 * @returns {any} 解析后的值,可能是:
 *   - 原始类型(number/boolean/null): 直接返回
 *   - 字符串: {type: "string", raw: "value"}
 *   - 对象: {type: "object", raw: "{x: 0, y: 0, z: 0}"}
 *   - 引用: {type: "ref", raw: "{fileID: 0, guid: xxx, type: 3}"}
 */
function parseValue(valueStr) {
  // 处理空字符串,返回空字符串对象而非null
  if (!valueStr) {
    return { type: "string", raw: "" };
  }

  // 检查是否是对象或引用（以 { 开头）
  if (valueStr.startsWith("{") && valueStr.endsWith("}")) {
    // 判断是引用还是普通对象
    if (valueStr.includes("fileID:") || valueStr.includes("guid:")) {
      // 引用类型
      return { type: "ref", raw: valueStr };
    } else {
      // 对象类型
      return { type: "object", raw: valueStr };
    }
  }

  // 检查是否是数字(整数或浮点数)
  // 正则说明: -?可选负号, \d+一个或多个数字, (?:\.\d+)?可选的小数部分
  if (/^-?\d+(?:\.\d+)?$/.test(valueStr)) {
    return parseFloat(valueStr);
  }

  // 其他情况作为字符串
  return { type: "string", raw: valueStr };
}

/**
 * 构建映射表
 * @param {Array} blocks - 文档块数组
 * @returns {Object} 映射表
 */
function buildMappings(blocks) {
  const mappings = {
    fileIDs: {},
    gameObjects: [],
    rootGameObject: null,
  };

  // 遍历所有块
  for (const block of blocks) {
    // 解析块内容获取类型名称和键值对
    const typeName = getUnityTypeName(block.typeId);
    const data = parseKeyValuePairs(block.content);

    // 添加到 fileIDs 映射
    mappings.fileIDs[block.fileID] = {
      typeId: block.typeId,
      typeName: typeName,
      data: data,
    };

    // 识别 GameObject（typeId === 1）
    if (block.typeId === 1) {
      mappings.gameObjects.push(block.fileID);
    }
  }

  // 找到根节点（第一个 GameObject）
  if (mappings.gameObjects.length > 0) {
    // 根节点通常是第一个 GameObject，或者是一个没有父节点的 GameObject
    // 简单起见，我们使用第一个 GameObject
    mappings.rootGameObject = mappings.gameObjects[0];

    // 更精确的根节点识别：查找 Transform/RectTransform 的 m_Father 为 {fileID: 0} 的 GameObject
    for (const gameObjectFileID of mappings.gameObjects) {
      const gameObject = mappings.fileIDs[gameObjectFileID];

      // 获取 GameObject 的 Transform/RectTransform 组件
      const components = gameObject.data.m_Component || [];
      if (components.length > 0) {
        const transformRef = components[0].component;
        if (transformRef && transformRef.type === "ref") {
          const transformFileIDMatch =
            transformRef.raw.match(/fileID:\s*(\d+)/);
          if (transformFileIDMatch) {
            const transformFileID = transformFileIDMatch[1];
            const transform = mappings.fileIDs[transformFileID];

            if (transform && transform.data.m_Father) {
              const fatherRef = transform.data.m_Father;

              if (fatherRef && fatherRef.type === "ref") {
                const fileIDMatch = fatherRef.raw.match(/fileID:\s*(\d+)/);
                if (fileIDMatch && parseInt(fileIDMatch[1], 10) === 0) {
                  mappings.rootGameObject = gameObjectFileID;
                  break;
                }
              }
            }
          }
        }
      }
    }
  }

  return mappings;
}

/**
 * MonoBehaviour 脚本名反查
 * @param {string} guid - MonoBehaviour 的 m_Script.guid
 * @returns {string|Object} 脚本名（如 'K3Panel.cs'）或 MissingScript 对象
 */
function findScriptName(guid) {
  // 0. 首先检查是否是 Unity 内置组件
  if (UNITY_BUILTIN_SCRIPTS[guid]) {
    return UNITY_BUILTIN_SCRIPTS[guid];
  }

  // 1. 检查缓存
  if (SCRIPT_GUID_CACHE.has(guid)) {
    return SCRIPT_GUID_CACHE.get(guid);
  }

  // 2. 使用已缓存的工具检测结果
  const tool = AVAILABLE_TOOL;

  try {
    // ========================================
    // 第一阶段：在 Assets/Scripts 中搜索
    // ========================================
    if (SCRIPTS_EXISTS) {
      if (tool === "rg") {
        try {
          // 在 Scripts 目录下搜索
          const output = execSync(
            `rg "guid: ${guid}" -g "*.cs.meta" -l --max-depth 8`,
            {
              cwd: SCRIPTS_PATH,
              encoding: "utf-8",
              timeout: SEARCH_TIMEOUT.SCRIPTS_RG,
            }
          ).trim();

          if (output) {
            const matchedPath = output.split("\n")[0];
            const scriptName = path.basename(matchedPath, ".meta");
            SCRIPT_GUID_CACHE.set(guid, scriptName);
            return scriptName;
          }
        } catch (rgError) {
          // rg 在 Scripts 目录下没找到或失败，继续下一阶段
        }
      } else {
        // 使用 grep 在 Scripts 目录下搜索
        try {
          const output = execSync(
            `grep -r "guid: ${guid}" --include="*.cs.meta" -l`,
            {
              cwd: SCRIPTS_PATH,
              encoding: "utf-8",
              timeout: SEARCH_TIMEOUT.SCRIPTS_GREP,
            }
          ).trim();

          if (output) {
            const matchedPath = output.split("\n")[0];
            const scriptName = path.basename(matchedPath, ".meta");
            SCRIPT_GUID_CACHE.set(guid, scriptName);
            return scriptName;
          }
        } catch (grepError) {
          // grep 在 Scripts 目录下没找到或失败，继续下一阶段
        }
      }
    }

    // ========================================
    // 第二阶段：在 Assets 全局搜索（排除 Scripts）
    // ========================================
    if (tool === "rg") {
      try {
        // 在 Assets 目录下搜索，排除 Scripts 目录
        const output = execSync(
          `rg "guid: ${guid}" -g "*.cs.meta" -g "!${SCRIPT_DIR_NAME}/" -l --max-depth 8`,
          {
            cwd: ASSETS_PATH,
            encoding: "utf-8",
            timeout: SEARCH_TIMEOUT.ASSETS_RG,
          }
        ).trim();

        if (output) {
          const matchedPath = output.split("\n")[0];
          const scriptName = path.basename(matchedPath, ".meta");
          SCRIPT_GUID_CACHE.set(guid, scriptName);
          return scriptName;
        }
      } catch (rgError) {
        // rg 也失败，返回 MissingScript
      }
    } else {
      // 使用 grep 在 Assets 目录下搜索（排除 Scripts）
      // grep 使用 --exclude-dir 排除 Scripts 目录
      try {
        const output = execSync(
          `grep -r "guid: ${guid}" --include="*.cs.meta" --exclude-dir="${SCRIPT_DIR_NAME}" -l`,
          {
            cwd: ASSETS_PATH,
            encoding: "utf-8",
            timeout: SEARCH_TIMEOUT.ASSETS_GREP,
          }
        ).trim();

        if (output) {
          const matchedPath = output.split("\n")[0];
          const scriptName = path.basename(matchedPath, ".meta");
          SCRIPT_GUID_CACHE.set(guid, scriptName);
          return scriptName;
        }
      } catch (grepError) {
        // grep 也失败，返回 MissingScript
      }
    }

    // 搜索失败，返回 MissingScript 或 BuiltInScript
    return { $status: "MissingScript or BuiltInScript", guid: guid };
  } catch (error) {
    // 整体异常，返回 MissingScript 或 BuiltInScript
    return { $status: "MissingScript or BuiltInScript", guid: guid };
  }
}

/**
 * Unity 类型名称查找
 * @param {number} typeId - Unity 类型 ID
 * @returns {string} Unity 类型名称
 */
function getUnityTypeName(typeId) {
  // Unity 类型名称查找
  return UNITY_TYPE_MAP[typeId] || "GameObject";
}

/**
 * 构建树状结构
 * @param {Array} blocks - 文档块数组
 * @returns {Object} 树状结构
 */
function buildTree(mappings) {
  // 递归构建树节点
  function buildNode(gameObjectFileID) {
    // 检查传入的是 Transform/RectTransform 还是 GameObject
    let gameObject = mappings.fileIDs[gameObjectFileID];
    let actualGameObjectID = gameObjectFileID;

    // 如果是 Transform/RectTransform（typeId 4 或 224），需要找到对应的 GameObject
    if (gameObject && (gameObject.typeId === 4 || gameObject.typeId === 224)) {
      const gameObjectRef = gameObject.data.m_GameObject;
      if (gameObjectRef && gameObjectRef.type === "ref") {
        const fileIDMatch = gameObjectRef.raw.match(/fileID:\s*(\d+)/);
        if (fileIDMatch) {
          actualGameObjectID = fileIDMatch[1];
          gameObject = mappings.fileIDs[actualGameObjectID];
        }
      }
    }

    if (!gameObject) {
      return null;
    }

    // 获取 GameObject 名称
    const m_Name = gameObject.data.m_Name;
    const name =
      typeof m_Name === "object" && m_Name !== null && m_Name.raw
        ? m_Name.raw
        : m_Name || "Unknown";

    // 获取 Transform/RectTransform 的子节点
    let children = [];

    // 从 GameObject 的 m_Component 数组中找到 Transform/RectTransform
    const components = gameObject.data.m_Component || [];

    if (components.length > 0) {
      // 第一个组件通常是 Transform/RectTransform
      const transformRef = components[0].component;

      if (transformRef && transformRef.type === "ref") {
        // 提取 Transform/RectTransform 的 fileID
        const fileIDMatch = transformRef.raw.match(/fileID:\s*(\d+)/);
        if (fileIDMatch) {
          const transformFileID = fileIDMatch[1];
          const transform = mappings.fileIDs[transformFileID];

          if (transform && transform.data.m_Children) {
            // 遍历子节点并递归构建
            for (const childRef of transform.data.m_Children) {
              if (childRef && childRef.type === "ref") {
                const childFileIDMatch = childRef.raw.match(/fileID:\s*(\d+)/);
                if (childFileIDMatch) {
                  const childFileID = childFileIDMatch[1];
                  const childNode = buildNode(childFileID);
                  if (childNode) {
                    children.push(childNode);
                  }
                }
              }
            }
          }
        }
      }
    }

    return {
      name: name,
      id: actualGameObjectID,
      children: children,
    };
  }

  // 从根节点开始构建
  if (!mappings.rootGameObject) {
    return {};
  }

  return buildNode(mappings.rootGameObject);
}

/**
 * 获取根节点元数据
 * @param {Object} mappings - 映射对象，包含 rootGameObject 和 fileIDs
 * @returns {Object} 根节点元数据
 */
function getRootMeta(mappings) {
  // 1. 获取根 GameObject 的 fileID
  const rootGameObjectFileID = mappings.rootGameObject;

  // 2. 如果根节点不存在，返回空对象
  if (!rootGameObjectFileID) {
    return {};
  }

  // 3. 获取根 GameObject 的数据
  const gameObject = mappings.fileIDs[rootGameObjectFileID];
  if (!gameObject || !gameObject.data) {
    return {};
  }

  const data = gameObject.data;

  // 4. 提取并标准化元数据字段
  const meta = {};

  // 需要提取的字段列表
  const fields = [
    "m_Layer",
    "m_TagString",
    "m_Name",
    "m_IsActive",
    "m_NavMeshLayer",
    "m_StaticEditorFlags",
  ];

  // 遍历字段，如果存在则提取并标准化
  for (const field of fields) {
    if (data[field] !== undefined) {
      meta[field] = normalizeValue(data[field]);
    }
  }

  return meta;
}

/**
 * 获取 GameObject 组件列表
 * @param {string[]} gameobjectFileIDs - GameObject fileID 数组
 * @param {Object} mappings - 映射表对象
 * @returns {Object} 组件列表，格式: {gameobjectFileID: [{id, type, script}, ...]}
 */
function getComponentsOf(gameobjectFileIDs, mappings) {
  const result = {};

  for (const gameObjectFileID of gameobjectFileIDs) {
    const gameObject = mappings.fileIDs[gameObjectFileID];

    // 如果 GameObject 不存在，返回空数组
    if (!gameObject) {
      result[gameObjectFileID] = [];
      continue;
    }

    const components = [];

    // 获取 m_Component 数组
    const mComponent = gameObject.data.m_Component;

    if (!mComponent || !Array.isArray(mComponent)) {
      result[gameObjectFileID] = [];
      continue;
    }

    // 遍历每个组件引用
    for (const componentRef of mComponent) {
      // componentRef 格式: {component: {type: "ref", raw: "{fileID: xxx}"}}
      if (!componentRef.component) {
        continue;
      }

      // 提取 fileID
      let componentFileID = null;
      const componentValue = componentRef.component;

      if (
        typeof componentValue === "object" &&
        componentValue.type === "ref" &&
        componentValue.raw
      ) {
        const match = componentValue.raw.match(/fileID:\s*(\d+)/);
        componentFileID = match ? match[1] : null;
      } else if (componentValue.fileID) {
        componentFileID = String(componentValue.fileID);
      }

      if (!componentFileID) {
        continue;
      }
      const component = mappings.fileIDs[componentFileID];

      if (!component) {
        continue;
      }

      // 识别组件类型
      const componentType =
        UNITY_TYPE_MAP[component.typeId] || `Unknown(${component.typeId})`;

      const componentInfo = {
        id: componentFileID,
        type: componentType,
        script: null,
      };

      // 如果是 MonoBehaviour，反查脚本名
      if (component.typeId === 114) {
        const scriptRef = component.data.m_Script;
        let guid = null;

        // scriptRef 可能是 {type: 'ref', raw: '{fileID: 11500000, guid: xxx, type: 3}'}
        if (scriptRef && scriptRef.raw) {
          const guidMatch = scriptRef.raw.match(/guid:\s*([a-f0-9]+)/i);
          if (guidMatch) {
            guid = guidMatch[1];
          }
        }

        if (guid) {
          const scriptName = findScriptName(guid);
          componentInfo.script = scriptName;
        }
      }

      components.push(componentInfo);
    }

    result[gameObjectFileID] = components;
  }

  return result;
}

/**
 * 获取组件详情
 *
 * 根据组件 fileID 查找组件块，提取所有参数 key/value 并标准化数据
 *
 * @param {string[]} componentFileIDs - 组件 fileID 数组
 * @param {Object} mappings - 映射表对象，包含 fileIDs 索引
 * @returns {Object} 组件详情
 *   - 单个组件时: 直接返回详情对象，如 {"m_Script": {...}, "m_Enabled": 1}
 *   - 多个组件时: 返回嵌套对象，如 {"fileID1": {...}, "fileID2": {...}}
 * @throws {Error} 当组件不存在时抛出错误
 */
function getComponentDetails(componentFileIDs, mappings) {
  // 参数验证
  if (!Array.isArray(componentFileIDs) || componentFileIDs.length === 0) {
    errorExit(
      "Error: componentFileIDs must be a non-empty array",
      EXIT_CODES.PARAM_ERROR
    );
  }

  if (!mappings || !mappings.fileIDs) {
    errorExit("Error: mappings must contain fileIDs", EXIT_CODES.PARAM_ERROR);
  }

  const result = {};
  const missingIDs = [];

  // 遍历所有组件 ID
  for (const componentFileID of componentFileIDs) {
    // 从 mappings.fileIDs 中查找组件信息
    const component = mappings.fileIDs[componentFileID];

    // 如果组件不存在，记录缺失的 ID
    if (!component) {
      missingIDs.push(componentFileID);
      continue;
    }

    // 提取组件详情对象
    const details = {};

    // 从 component.data 中提取所有参数 key/value
    if (component.data && typeof component.data === "object") {
      for (const [key, value] of Object.entries(component.data)) {
        // 使用 normalizeValue 标准化所有数据
        details[key] = normalizeValue(value);
      }
    }

    result[componentFileID] = details;
  }

  // 如果有组件不存在，报错并返回退出码 1
  if (missingIDs.length > 0) {
    errorExit(
      `Error: component(s) not found: ${missingIDs.join(", ")}`,
      EXIT_CODES.GENERAL_ERROR
    );
  }

  // 单个组件时直接返回详情对象（不嵌套）
  if (componentFileIDs.length === 1) {
    return result[componentFileIDs[0]];
  }

  // 多个组件时返回嵌套结构 {fileID: 详情}
  return result;
}

// ============================================================================
// 4. 主程序入口
// ============================================================================

/**
 * 主程序入口函数
 */
function main() {
  try {
    // 1. 解析命令行参数
    const args = parseArgs(process.argv.slice(2));

    // 2. 验证 Prefab 文件是否存在
    if (!fs.existsSync(args.prefabPath)) {
      errorExit(
        `Error: file not found: ${args.prefabPath}`,
        EXIT_CODES.GENERAL_ERROR
      );
    }

    // 3. 读取 Prefab 文件内容
    const content = fs.readFileSync(args.prefabPath, "utf-8");

    // 4. 解析 YAML 内容
    const blocks = parseDocumentBlocks(content);
    const mappings = buildMappings(blocks);

    // 5. 根据参数调用相应的查询功能
    let result;
    switch (args.outputType) {
      case "tree":
        result = buildTree(mappings);
        break;
      case "root-meta":
        result = getRootMeta(mappings);
        break;
      case "components-of":
        result = getComponentsOf(args.targetIDs, mappings);
        break;
      case "component":
        result = getComponentDetails(args.targetIDs, mappings);
        break;
      default:
        errorExit("Error: invalid output type", EXIT_CODES.PARAM_ERROR);
    }

    // 6. 输出 JSON 到 stdout
    console.log(JSON.stringify(result, null, 2));
  } catch (error) {
    errorExit(`Error: ${error.message}`, EXIT_CODES.GENERAL_ERROR);
  }
}

// 执行主程序
main();
