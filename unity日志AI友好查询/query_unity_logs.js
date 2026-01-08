#!/usr/bin/env node

const fs = require("fs");
const net = require("net");
const path = require("path");
const os = require("os");

// 配置常量
const DEFAULT_PORT = 6800;
const HOST = "127.0.0.1";
const MAX_COUNT = 200;
const MAX_MINUTES = 60;

// 显示帮助信息
function showHelp() {
  console.log(`
UnityLogServer 查询工具
========================

用法:
    node query_unity_logs.js [选项]

查询选项（必须至少提供一个）:
    --count <n>            查询最近n条日志 (1-200)
    --minutes <n>          查询最近n分钟的日志 (1-60)
    --keyword "<text>"     严格关键词匹配
    --fuzzy "<text>"       模糊关键词匹配
    --regex "<pattern>"    正则表达式匹配

参数组合规则:
    - count 和 minutes 不能同时使用
    - keyword / fuzzy / regex 不能同时使用
    - 至少需要一个查询参数
    - 可组合: count/minutes + keyword/fuzzy/regex

示例:
    node query_unity_logs.js --count 20
    node query_unity_logs.js --minutes 5 --fuzzy "error"
    node query_unity_logs.js --keyword "Error"
    node query_unity_logs.js --count 50 --regex "Error.*player"

其他选项:
    --help                 显示此帮助信息

输出格式:
    结果包含每条日志的时间戳、类型、消息和堆栈信息（如果有）

注意!!!:
    使用命令获取日志前MUST提示用户手动触发需要的日志!!!用户告知后再运行命令获取日志
`);
}

// 解析命令行参数
function parseArgs() {
  const args = process.argv.slice(2);
  const params = {};

  if (args.includes("--help") || args.includes("-h")) {
    showHelp();
    process.exit(0);
  }

  for (let i = 0; i < args.length; i++) {
    const arg = args[i];
    switch (arg) {
      case "--count":
        params.count = parseInt(args[++i]);
        break;
      case "--minutes":
        params.minutes = parseInt(args[++i]);
        break;
      case "--keyword":
        params.keyword = args[++i];
        break;
      case "--fuzzy":
        params.fuzzy = args[++i];
        break;
      case "--regex":
        params.regex = args[++i];
        break;
      default:
        if (arg.startsWith("--")) {
          console.error(`❌ 未知参数: ${arg}`);
          console.log("使用 --help 查看帮助信息");
          process.exit(1);
        }
    }
  }

  return params;
}

// 验证参数
function validateParams(params) {
  // 检查是否至少有一个查询参数
  const hasCount = params.count !== undefined;
  const hasMinutes = params.minutes !== undefined;
  const hasKeyword = params.keyword !== undefined;
  const hasFuzzy = params.fuzzy !== undefined;
  const hasRegex = params.regex !== undefined;

  if (!hasCount && !hasMinutes && !hasKeyword && !hasFuzzy && !hasRegex) {
    console.error("❌ 错误: 至少需要一个查询参数");
    console.log("使用 --help 查看帮助信息");
    process.exit(1);
  }

  // 检查 count 和 minutes 不能同时使用
  if (hasCount && hasMinutes) {
    console.error("❌ 错误: --count 和 --minutes 不能同时使用");
    console.log("使用 --help 查看帮助信息");
    process.exit(1);
  }

  // 检查 keyword/fuzzy/regex 不能同时使用
  const filterCount = [hasKeyword, hasFuzzy, hasRegex].filter(Boolean).length;
  if (filterCount > 1) {
    console.error("❌ 错误: --keyword, --fuzzy, --regex 不能同时使用");
    console.log("使用 --help 查看帮助信息");
    process.exit(1);
  }

  // 验证 count 范围
  if (hasCount && (params.count < 1 || params.count > MAX_COUNT)) {
    console.error(`❌ 错误: --count 必须在 1-${MAX_COUNT} 之间`);
    process.exit(1);
  }

  // 验证 minutes 范围
  if (hasMinutes && (params.minutes < 1 || params.minutes > MAX_MINUTES)) {
    console.error(`❌ 错误: --minutes 必须在 1-${MAX_MINUTES} 之间`);
    process.exit(1);
  }

  return params;
}

// 读取端口号
function readPort() {
  let port = DEFAULT_PORT;
  let portFile;

  // 尝试读取端口文件
  try {
    const homeDir = os.homedir();
    portFile = path.join(homeDir, ".unitylog_port.txt");

    if (fs.existsSync(portFile)) {
      const portContent = fs.readFileSync(portFile, "utf-8").trim();
      const parsedPort = parseInt(portContent);
      if (!isNaN(parsedPort) && parsedPort > 0 && parsedPort <= 65535) {
        port = parsedPort;
      }
    }
  } catch (error) {
    // 忽略读取错误，使用默认端口
  }

  return port;
}

// 构建JSON请求
function buildRequest(params) {
  const request = {};

  if (params.count !== undefined) {
    request.count = params.count;
  } else if (params.minutes !== undefined) {
    request.minutes = params.minutes;
  }

  if (params.keyword !== undefined) {
    request.keyword = params.keyword;
  } else if (params.fuzzy !== undefined) {
    request.fuzzy = params.fuzzy;
  } else if (params.regex !== undefined) {
    request.regex = params.regex;
  }

  return JSON.stringify(request);
}

// 发送查询请求
function queryLogs(requestJson, port) {
  return new Promise((resolve, reject) => {
    const client = new net.Socket();
    let responseData = "";

    client.setTimeout(5000);

    client.connect(port, HOST, () => {
      // 发送请求
      client.write(requestJson);
    });

    client.on("data", (data) => {
      responseData += data.toString();
    });

    client.on("end", () => {
      try {
        const response = JSON.parse(responseData);
        client.end(); // 显式关闭连接
        resolve(response);
      } catch (error) {
        client.destroy(); // 解析错误时强制关闭
        reject(new Error(`解析响应失败: ${error.message}`));
      }
    });

    client.on("error", (error) => {
      client.destroy(); // 确保连接已关闭
      reject(new Error(`连接错误: ${error.message}`));
    });

    client.on("timeout", () => {
      client.destroy(); // 确保连接已关闭
      reject(new Error("连接超时"));
    });
  });
}

// 格式化输出日志
function formatLogs(response) {
  if (!response.success) {
    console.error(`❌ 查询失败: ${response.error || "未知错误"}`);
    return;
  }

  const count = response.count || 0;
  const logs = response.logs || [];

  console.log(`✅ Found ${count} log(s)`);
  console.log("=".repeat(80));

  logs.forEach((log, index) => {
    const logType = log.type || "Log";
    const timestamp = log.timestamp || "";
    const message = log.message || "";
    const stack = log.stack || "";

    console.log(`\n[${index + 1}] ${logType} - ${timestamp}`);
    console.log(`    Message: ${message}`);

    if (stack) {
      console.log(`    Stack: ${stack}`);
    }
  });

  if (count === 0) {
    console.log("\n没有找到匹配的日志");
  }
}

// 主函数
async function main() {
  try {
    // 解析和验证参数
    const rawParams = parseArgs();
    const params = validateParams(rawParams);

    // 读取端口
    const port = readPort();
    console.log(`📡 连接到 UnityLogServer (${HOST}:${port})`);

    // 构建请求
    const requestJson = buildRequest(params);

    // 发送查询
    const response = await queryLogs(requestJson, port);

    // 格式化输出
    formatLogs(response);
  } catch (error) {
    console.error(`❌ 错误: ${error.message}`);
    process.exit(1);
  }
}

// 运行主函数
main();
