using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace AssetsTools
{
    /// <summary>
    /// Unity日志客户端
    /// 连接到外部Node.js日志服务端,发送日志
    /// 支持自动重连、清空日志同步
    /// </summary>
    [InitializeOnLoad]
    public class UnityLogClient
    {
        private const string SERVER_HOST = "127.0.0.1";
        private const int SERVER_PORT = 6800;
        private const int RECONNECT_INTERVAL = 3000; // 重连间隔(毫秒)
        private const int SEND_TIMEOUT = 1000;

        private static TcpClient s_Client;
        private static NetworkStream s_Stream;
        private static Thread s_ConnectThread;
        private static volatile bool s_IsRunning;
        private static volatile bool s_IsConnected;
        private static string s_ClientId; // 服务端分配的客户端ID
        
        private static readonly object s_LockObj = new object();
        
        // 主线程日志队列
        private static readonly Queue<string> s_MainThreadLogQueue = new Queue<string>();
        private static readonly object s_LogQueueLock = new object();

        // Console Clear 检测相关
        private static MethodInfo s_LogEntriesGetCountMethod;
        private static int s_LastConsoleLogCount;
        private static float s_LastClearCheckTime;
        
        // 防止重复初始化
        private static bool s_Initialized = false;

        static UnityLogClient()
        {
            // 注册域重载事件,在重载前清理
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            
            Initialize();
        }

        /// <summary>
        /// 域重载前清理
        /// </summary>
        private static void OnBeforeAssemblyReload()
        {
            Shutdown(silent: true);
        }

        /// <summary>
        /// 初始化客户端
        /// </summary>
        private static void Initialize()
        {
            // 防止重复初始化
            if (s_Initialized)
            {
                return;
            }
            s_Initialized = true;
            
            // 先清理可能存在的旧连接
            CloseConnection();
            
            s_IsRunning = true;
            s_IsConnected = false;
            s_ClientId = null;

            // 注册日志回调
            Application.logMessageReceived -= HandleLog; // 先移除防止重复
            Application.logMessageReceived += HandleLog;

            // 注册退出回调
            EditorApplication.quitting -= OnQuit;
            EditorApplication.quitting += OnQuit;
            
            // 注册主线程更新
            EditorApplication.update -= ProcessMainThreadLogs;
            EditorApplication.update += ProcessMainThreadLogs;

            // 初始化 Console Clear 检测
            InitializeConsoleClearCheck();

            // 启动连接线程
            StartConnectThread();
        }
        
        /// <summary>
        /// 处理主线程日志队列
        /// </summary>
        private static void ProcessMainThreadLogs()
        {
            lock (s_LogQueueLock)
            {
                while (s_MainThreadLogQueue.Count > 0)
                {
                    string log = s_MainThreadLogQueue.Dequeue();
                    Debug.Log(log);
                }
            }
        }
        
        /// <summary>
        /// 安全的日志输出(从任意线程调用,在主线程输出)
        /// </summary>
        private static void SafeLog(string message)
        {
            lock (s_LogQueueLock)
            {
                s_MainThreadLogQueue.Enqueue(message);
            }
        }

        /// <summary>
        /// 启动连接线程
        /// </summary>
        private static void StartConnectThread()
        {
            s_ConnectThread = new Thread(ConnectThreadProc);
            s_ConnectThread.IsBackground = true;
            s_ConnectThread.Start();
        }

        /// <summary>
        /// 连接线程处理
        /// </summary>
        private static void ConnectThreadProc()
        {
            while (s_IsRunning)
            {
                if (!s_IsConnected)
                {
                    TryConnect();
                }
                else
                {
                    // 检查连接状态
                    try
                    {
                        if (s_Client != null && !s_Client.Connected)
                        {
                            s_IsConnected = false;
                            Debug.Log("[UnityLogClient] Connection lost, will reconnect...");
                        }
                    }
                    catch
                    {
                        s_IsConnected = false;
                    }
                }

                Thread.Sleep(RECONNECT_INTERVAL);
            }
        }

        /// <summary>
        /// 尝试连接服务端
        /// </summary>
        private static void TryConnect()
        {
            try
            {
                // 关闭旧连接
                CloseConnection();

                // 创建新连接
                s_Client = new TcpClient();
                s_Client.SendTimeout = SEND_TIMEOUT;
                s_Client.ReceiveTimeout = SEND_TIMEOUT;
                s_Client.Connect(SERVER_HOST, SERVER_PORT);
                s_Stream = s_Client.GetStream();

                // 发送注册消息
                string clientName = GetClientName();
                string registerMsg = $"{{\"type\":\"register\",\"data\":{{\"clientName\":\"{clientName}\"}}}}\n";
                byte[] data = Encoding.UTF8.GetBytes(registerMsg);
                s_Stream.Write(data, 0, data.Length);

                // 读取注册响应
                byte[] buffer = new byte[1024];
                int bytesRead = s_Stream.Read(buffer, 0, buffer.Length);
                if (bytesRead > 0)
                {
                    string response = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                    // 解析 {"clientId":"Unity-1(ProjectName)"}
                    int start = response.IndexOf("\"clientId\":\"");
                    if (start >= 0)
                    {
                        start += 12;
                        int end = response.IndexOf("\"", start);
                        if (end > start)
                        {
                            s_ClientId = response.Substring(start, end - start);
                        }
                    }
                }

                s_IsConnected = true;
                SafeLog($"[UnityLogClient] Connected to server as {s_ClientId ?? "Unknown"}");
            }
            catch (Exception)
            {
                // 连接失败,静默处理,等待下次重连
                s_IsConnected = false;
                CloseConnection();
            }
        }

        /// <summary>
        /// 获取客户端名称(使用项目名)
        /// </summary>
        private static string GetClientName()
        {
            try
            {
                // 使用项目文件夹名作为客户端名
                string projectPath = Application.dataPath;
                if (projectPath.EndsWith("/Assets"))
                {
                    projectPath = projectPath.Substring(0, projectPath.Length - 7);
                }
                return Path.GetFileName(projectPath);
            }
            catch
            {
                return "UnityProject";
            }
        }

        /// <summary>
        /// 关闭连接
        /// </summary>
        private static void CloseConnection()
        {
            try
            {
                s_Stream?.Close();
                s_Client?.Close();
            }
            catch { }
            finally
            {
                s_Stream = null;
                s_Client = null;
            }
        }

        /// <summary>
        /// 初始化 Console Clear 检测功能
        /// </summary>
        private static void InitializeConsoleClearCheck()
        {
            try
            {
                // 通过反射获取 LogEntries.GetCount 方法
                var logEntriesType = typeof(EditorApplication).Assembly.GetType("UnityEditor.LogEntries");
                if (logEntriesType != null)
                {
                    s_LogEntriesGetCountMethod = logEntriesType.GetMethod("GetCount", BindingFlags.Static | BindingFlags.Public);
                }

                // 初始化上次的日志数量
                if (s_LogEntriesGetCountMethod != null)
                {
                    try
                    {
                        s_LastConsoleLogCount = (int)s_LogEntriesGetCountMethod.Invoke(null, null);
                    }
                    catch
                    {
                        s_LastConsoleLogCount = 0;
                    }
                }

                s_LastClearCheckTime = Time.realtimeSinceStartup;

                // 注册 EditorApplication.update 回调
                EditorApplication.update += CheckConsoleClear;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnityLogClient] InitializeConsoleClearCheck error: {ex.Message}");
            }
        }

        /// <summary>
        /// 检测 Console Clear 事件
        /// </summary>
        private static void CheckConsoleClear()
        {
            // 每 0.5 秒检查一次
            float currentTime = Time.realtimeSinceStartup;
            if (currentTime - s_LastClearCheckTime < 0.5f)
            {
                return;
            }
            s_LastClearCheckTime = currentTime;

            try
            {
                if (s_LogEntriesGetCountMethod == null)
                {
                    return;
                }

                int currentCount = (int)s_LogEntriesGetCountMethod.Invoke(null, null);

                // 检测日志数量骤降(清空操作)
                if (s_LastConsoleLogCount > 10 && currentCount < s_LastConsoleLogCount / 2 && currentCount < 5)
                {
                    // 发送清空消息到服务端
                    SendClearMessage();
                    Debug.Log("[UnityLogClient] Console cleared, notified server");
                }

                s_LastConsoleLogCount = currentCount;
            }
            catch { }
        }

        /// <summary>
        /// 发送清空消息
        /// </summary>
        private static void SendClearMessage()
        {
            if (!s_IsConnected || s_Stream == null)
            {
                return;
            }

            try
            {
                string message = "{\"type\":\"clear\"}\n";
                byte[] data = Encoding.UTF8.GetBytes(message);
                s_Stream.Write(data, 0, data.Length);
            }
            catch
            {
                s_IsConnected = false;
            }
        }

        /// <summary>
        /// 处理Unity日志消息
        /// </summary>
        private static void HandleLog(string logString, string stackTrace, LogType type)
        {
            if (!s_IsConnected || s_Stream == null)
            {
                return;
            }

            try
            {
                // 构建JSON消息
                string timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                string escapedMessage = EscapeJson(logString ?? "");
                string escapedStack = EscapeJson(stackTrace ?? "");
                string logType = type.ToString();

                string message = $"{{\"type\":\"log\",\"data\":{{\"timestamp\":\"{timestamp}\",\"message\":\"{escapedMessage}\",\"stackTrace\":\"{escapedStack}\",\"logType\":\"{logType}\"}}}}\n";
                
                byte[] data = Encoding.UTF8.GetBytes(message);
                s_Stream.Write(data, 0, data.Length);
            }
            catch
            {
                s_IsConnected = false;
            }
        }

        /// <summary>
        /// JSON字符串转义
        /// </summary>
        private static string EscapeJson(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return "";
            }

            StringBuilder sb = new StringBuilder();
            foreach (char c in str)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 32)
                        {
                            sb.AppendFormat("\\u{0:x4}", (int)c);
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Unity编辑器退出时的清理
        /// </summary>
        private static void OnQuit()
        {
            Shutdown();
        }

        /// <summary>
        /// 关闭客户端
        /// </summary>
        /// <param name="silent">是否静默模式(不输出日志)</param>
        private static void Shutdown(bool silent = false)
        {
            s_IsRunning = false;
            s_Initialized = false; // 重置初始化标志,允许下次重新初始化

            try
            {
                // 取消注册回调
                Application.logMessageReceived -= HandleLog;
                EditorApplication.update -= CheckConsoleClear;
                EditorApplication.update -= ProcessMainThreadLogs;

                // 等待连接线程结束
                if (s_ConnectThread != null && s_ConnectThread.IsAlive)
                {
                    s_ConnectThread.Join(500); // 缩短等待时间
                    s_ConnectThread = null;
                }

                // 关闭连接
                CloseConnection();

                if (!silent)
                {
                    Debug.Log("[UnityLogClient] Client stopped");
                }
            }
            catch (Exception ex)
            {
                if (!silent)
                {
                    Debug.LogError($"[UnityLogClient] Shutdown error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 手动重连
        /// </summary>
        [MenuItem("Tools/UnityLogClient/Reconnect")]
        public static void Reconnect()
        {
            s_IsConnected = false;
            Debug.Log("[UnityLogClient] Manual reconnect triggered");
        }

        /// <summary>
        /// 显示连接状态
        /// </summary>
        [MenuItem("Tools/UnityLogClient/Status")]
        public static void ShowStatus()
        {
            string status = s_IsConnected ? "Connected" : "Disconnected";
            string clientId = s_ClientId ?? "N/A";
            Debug.Log($"[UnityLogClient] Status: {status}, ClientId: {clientId}, Server: {SERVER_HOST}:{SERVER_PORT}");
        }
    }
}
