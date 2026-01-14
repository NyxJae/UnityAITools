#!/usr/bin/env node

/**
 * Unity Log Server - Node.js 日志服务端
 *
 * 功能:
 * - 固定端口 6800 接收 Unity 客户端日志
 * - 支持多个 Unity 客户端同时接入,自动分配客户端ID
 * - 接收查询客户端的查询请求,支持按客户端筛选
 * - 缓存最近 200 条日志
 * - 实时打印收到的日志(带客户端前缀)
 * - 支持清空日志功能
 *
 * 通信协议 (JSON, 每条消息以 \n 结尾):
 * - 注册消息: {"type":"register","data":{clientName?}} -> 响应 {"clientId":"Unity-1"}
 * - 日志消息: {"type":"log","data":{timestamp,message,stackTrace,logType}}
 * - 清空消息: {"type":"clear"}
 * - 查询消息: {"type":"query","data":{count?,minutes?,keyword?,fuzzy?,regex?,client?}}
 * 注意!此服务端让用户自己手动启动!!!,用户没启动就友好提醒用户启动
 */

const net = require("net");

// 配置常量
const PORT = 6800;
const MAX_LOG_CAPACITY = 200;

// 日志缓存
let logCache = [];

// Unity客户端管理
let unityClientCount = 0;
const unityClients = new Map(); // clientName(路径) -> {socket, displayName}

// 日志类型颜色
const LOG_COLORS = {
  Log: "\x1b[37m", // 白色
  Warning: "\x1b[33m", // 黄色
  Error: "\x1b[31m", // 红色
  Exception: "\x1b[35m", // 紫色
  Assert: "\x1b[36m", // 青色
};
const RESET_COLOR = "\x1b[0m";

// 服务端信息颜色
const INFO_COLOR = "\x1b[32m"; // 绿色
const SERVER_COLOR = "\x1b[34m"; // 蓝色

/**
 * 获取当前时间戳
 */
function getTimestamp() {
  return new Date().toISOString();
}

/**
 * 打印服务端信息
 */
function serverLog(message) {
  console.log(
    `${SERVER_COLOR}[Server ${getTimestamp()}]${RESET_COLOR} ${message}`
  );
}

/**
 * 打印Unity日志 (带客户端前缀)
 */
function printUnityLog(logEntry, clientId) {
  const color = LOG_COLORS[logEntry.logType] || LOG_COLORS.Log;
  const clientPrefix = clientId ? `[${clientId}]` : "[Unknown]";
  const typeStr = `[${logEntry.logType}]`.padEnd(12);
  const timeStr = logEntry.timestamp ? `[${logEntry.timestamp}]` : "";

  console.log(
    `${INFO_COLOR}${clientPrefix}${RESET_COLOR} ${color}${typeStr}${RESET_COLOR} ${timeStr} ${logEntry.message}`
  );

  // 如果有堆栈信息且是Error/Exception, 打印堆栈
  if (
    logEntry.stackTrace &&
    (logEntry.logType === "Error" || logEntry.logType === "Exception")
  ) {
    const stackLines = logEntry.stackTrace.split("\n").slice(0, 5);
    stackLines.forEach((line) => {
      if (line.trim()) {
        console.log(`${color}    ${line}${RESET_COLOR}`);
      }
    });
  }
}

/**
 * 添加日志到缓存
 */
function addLogToCache(logEntry) {
  logCache.push(logEntry);

  // 超出容量时移除最早的日志
  while (logCache.length > MAX_LOG_CAPACITY) {
    logCache.shift();
  }
}

/**
 * 清空日志缓存
 */
function clearLogCache() {
  logCache = [];
  serverLog("Log cache cleared");
}

/**
 * 验证查询参数
 */
function validateQueryParams(params) {
  if (!params) {
    return "No query parameters";
  }

  const hasCount = params.count !== undefined && params.count !== null;
  const hasMinutes = params.minutes !== undefined && params.minutes !== null;
  const hasKeyword = params.keyword !== undefined && params.keyword !== null;
  const hasFuzzy = params.fuzzy !== undefined && params.fuzzy !== null;
  const hasRegex = params.regex !== undefined && params.regex !== null;

  // 至少需要一个查询参数
  if (!hasCount && !hasMinutes && !hasKeyword && !hasFuzzy && !hasRegex) {
    return "No query conditions specified";
  }

  // count 和 minutes 不能同时使用
  if (hasCount && hasMinutes) {
    return "count and minutes cannot be used together";
  }

  // count 范围检查
  if (hasCount && (params.count < 1 || params.count > MAX_LOG_CAPACITY)) {
    return `count must be between 1 and ${MAX_LOG_CAPACITY}`;
  }

  // minutes 范围检查
  if (hasMinutes && (params.minutes < 1 || params.minutes > 60)) {
    return "minutes must be between 1 and 60";
  }

  // keyword/fuzzy/regex 不能同时使用
  const filterCount = [hasKeyword, hasFuzzy, hasRegex].filter(Boolean).length;
  if (filterCount > 1) {
    return "keyword, fuzzy, and regex cannot be used together";
  }

  // 正则表达式语法检查
  if (hasRegex) {
    try {
      new RegExp(params.regex);
    } catch (e) {
      return "Invalid regex pattern";
    }
  }

  return null;
}

/**
 * 处理查询请求
 */
function processQuery(params) {
  const response = {
    success: false,
    count: 0,
    logs: [],
    error: "",
  };

  // 验证参数
  const error = validateQueryParams(params);
  if (error) {
    response.error = error;
    return response;
  }

  // 复制日志列表
  let logs = [...logCache];

  // 按时间范围筛选
  if (params.minutes !== undefined && params.minutes !== null) {
    const cutoffTime = new Date(Date.now() - params.minutes * 60 * 1000);
    logs = logs.filter((log) => {
      const logTime = new Date(log.timestamp);
      return logTime >= cutoffTime;
    });
  }

  // 按客户端筛选
  if (
    params.client !== undefined &&
    params.client !== null &&
    params.client !== ""
  ) {
    logs = logs.filter(
      (log) => log.clientId && log.clientId.includes(params.client)
    );
  }

  // 按数量筛选(最近n条)
  if (params.count !== undefined && params.count !== null) {
    if (logs.length > params.count) {
      logs = logs.slice(-params.count);
    }
  }

  // 按严格关键词筛选
  if (params.keyword !== undefined && params.keyword !== null) {
    logs = logs.filter((log) => log.message === params.keyword);
  }

  // 按模糊关键词筛选
  if (params.fuzzy !== undefined && params.fuzzy !== null) {
    logs = logs.filter((log) => log.message.includes(params.fuzzy));
  }

  // 按正则表达式筛选
  if (params.regex !== undefined && params.regex !== null) {
    try {
      const regex = new RegExp(params.regex);
      logs = logs.filter((log) => regex.test(log.message));
    } catch (e) {
      // 验证已通过, 这里不应出现异常
    }
  }

  response.success = true;
  response.count = logs.length;
  response.logs = logs;
  return response;
}

/**
 * 处理客户端消息
 */
function handleMessage(messageStr, socket, clientInfo) {
  try {
    const message = JSON.parse(messageStr);

    switch (message.type) {
      case "register":
        // 处理注册消息 - Unity客户端注册
        // clientName使用项目绝对路径作为唯一标识
        const clientName = message.data?.clientName || "Unknown";

        // 检查是否已有同路径的客户端连接,有则踢掉旧连接
        if (unityClients.has(clientName)) {
          const oldClient = unityClients.get(clientName);
          if (oldClient && oldClient.socket && oldClient.socket !== socket) {
            serverLog(`Replacing old connection for: ${clientName}`);
            try {
              oldClient.socket.destroy();
            } catch (e) {}
            // 不减计数,因为马上就会加回来
            unityClientCount--;
          }
        }

        // 用路径最后一段作为显示名
        const pathParts = clientName.replace(/\\/g, "/").split("/");
        const shortName = pathParts[pathParts.length - 1] || clientName;
        const displayName = shortName;

        // 保存客户端信息
        clientInfo.clientName = clientName;
        clientInfo.displayName = displayName;
        clientInfo.isUnityClient = true;
        unityClients.set(clientName, { socket, displayName, clientInfo });
        unityClientCount++;

        // 发送注册响应
        const regResponse = JSON.stringify({ clientId: displayName }) + "\n";
        socket.write(regResponse);

        serverLog(
          `Unity client registered: ${displayName} (total: ${unityClientCount})`
        );
        break;

      case "log":
        // 处理日志消息
        if (message.data) {
          const logEntry = {
            timestamp: message.data.timestamp || getTimestamp(),
            message: message.data.message || "",
            stackTrace: message.data.stackTrace || "",
            logType: message.data.logType || "Log",
            clientId: clientInfo.displayName || "Unknown",
          };
          addLogToCache(logEntry);
          printUnityLog(logEntry, clientInfo.displayName);
        }
        break;

      case "clear":
        // 处理清空消息 - 只清空该客户端的日志,或全部清空
        if (clientInfo.displayName) {
          // 清空该客户端的日志
          const beforeCount = logCache.length;
          logCache = logCache.filter(
            (log) => log.clientId !== clientInfo.displayName
          );
          const cleared = beforeCount - logCache.length;
          serverLog(`Cleared ${cleared} logs from ${clientInfo.displayName}`);
        } else {
          // 非Unity客户端发送clear则清空全部
          clearLogCache();
        }
        break;

      case "query":
        // 处理查询消息 - 查询是一次性请求,发送响应后关闭连接
        const response = processQuery(message.data);
        const responseStr = JSON.stringify(response) + "\n";
        socket.write(responseStr, () => {
          socket.end(); // 发送完成后关闭连接
        });
        break;

      default:
        serverLog(`Unknown message type: ${message.type}`);
    }
  } catch (e) {
    serverLog(`Parse message error: ${e.message}`);
  }
}

/**
 * 处理客户端连接
 */
function handleConnection(socket) {
  const clientAddr = `${socket.remoteAddress}:${socket.remotePort}`;
  let buffer = "";

  // 客户端信息对象
  const clientInfo = {
    clientId: null,
    clientName: null,
    displayName: null,
    isUnityClient: false,
    addr: clientAddr,
  };

  socket.on("data", (data) => {
    buffer += data.toString();

    // 按换行符分割消息
    let lines = buffer.split("\n");
    buffer = lines.pop(); // 保留未完成的部分

    for (const line of lines) {
      if (line.trim()) {
        handleMessage(line, socket, clientInfo);
      }
    }
  });

  socket.on("end", () => {
    if (clientInfo.isUnityClient) {
      unityClientCount--;
      unityClients.delete(socket);
      serverLog(
        `Unity client disconnected: ${clientInfo.displayName} (total: ${unityClientCount})`
      );
    }
  });

  socket.on("error", (err) => {
    if (clientInfo.isUnityClient) {
      unityClientCount--;
      unityClients.delete(socket);
      serverLog(
        `Unity client error: ${clientInfo.displayName} - ${err.message} (total: ${unityClientCount})`
      );
    }
  });

  socket.on("close", () => {
    // 处理缓冲区中剩余的数据
    if (buffer.trim()) {
      handleMessage(buffer, socket, clientInfo);
    }
  });
}

/**
 * 启动服务器
 */
function startServer() {
  const server = net.createServer(handleConnection);

  server.on("error", (err) => {
    if (err.code === "EADDRINUSE") {
      console.error(
        `${SERVER_COLOR}[Error]${RESET_COLOR} Port ${PORT} is already in use`
      );
      console.error(
        "Please stop other services using this port and try again."
      );
      process.exit(1);
    } else {
      console.error(
        `${SERVER_COLOR}[Error]${RESET_COLOR} Server error: ${err.message}`
      );
    }
  });

  server.listen(PORT, () => {
    console.log("");
    console.log(
      `${INFO_COLOR}========================================${RESET_COLOR}`
    );
    console.log(`${INFO_COLOR}  Unity Log Server Started${RESET_COLOR}`);
    console.log(`${INFO_COLOR}  Port: ${PORT}${RESET_COLOR}`);
    console.log(
      `${INFO_COLOR}  Max Log Cache: ${MAX_LOG_CAPACITY}${RESET_COLOR}`
    );
    console.log(
      `${INFO_COLOR}========================================${RESET_COLOR}`
    );
    console.log("");
    serverLog("Waiting for connections...");
  });

  // 处理进程信号
  let isShuttingDown = false;

  function gracefulShutdown() {
    if (isShuttingDown) return;
    isShuttingDown = true;

    serverLog("Shutting down...");

    // 关闭所有Unity客户端连接
    for (const [name, client] of unityClients) {
      try {
        client.socket.destroy();
      } catch (e) {}
    }
    unityClients.clear();

    // 关闭服务器
    server.close(() => {
      serverLog("Server stopped");
      process.exit(0);
    });

    // 2秒后强制退出
    setTimeout(() => {
      serverLog("Force exit");
      process.exit(0);
    }, 2000);
  }

  process.on("SIGINT", gracefulShutdown);
  process.on("SIGTERM", gracefulShutdown);
}

// 启动服务器
startServer();
