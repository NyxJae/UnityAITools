using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace AssetsTools
{
    /// <summary>
    /// 日志条目类
    /// </summary>
    [Serializable]
    public class LogEntry
    {
        public string timestamp;
        public string message;
        public string stackTrace;
        public string type;

        public LogEntry(string logString, string stackTrace, LogType logType)
        {
            timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            message = logString ?? "";
            this.stackTrace = stackTrace ?? "";
            type = logType.ToString();
        }
    }

    /// <summary>
    /// 查询请求类
    /// </summary>
    [Serializable]
    public class QueryRequest
    {
        public int? count;
        public int? minutes;
        public string keyword;
        public string fuzzy;
        public string regex;
    }

    /// <summary>
    /// 查询响应类
    /// </summary>
    [Serializable]
    public class QueryResponse
    {
        public bool success;
        public int count;
        public List<LogEntry> logs;
        public string error;

        public QueryResponse()
        {
            success = false;
            count = 0;
            logs = new List<LogEntry>();
            error = "";
        }
    }

    /// <summary>
    /// Unity日志TCP服务器
    /// 随Unity编辑器启动自动启动，提供日志查询功能
    /// </summary>
    [InitializeOnLoad]
    public class UnityLogServer
    {
        private const int DEFAULT_PORT = 6800;
        private const int MIN_PORT = 6801;
        private const int MAX_PORT = 6999;
        private const int MAX_LOG_CAPACITY = 200;
        private const int BUFFER_SIZE = 8192;

        private static TcpListener s_Listener;
        private static Thread s_ServerThread;
        private static bool s_IsRunning;
        private static int s_CurrentPort;
        
        private static Queue<LogEntry> s_LogQueue;
        private static readonly object s_LockObj = new object();

        // Console Clear 检测相关
        private static MethodInfo s_LogEntriesGetCountMethod;
        private static int s_LastConsoleLogCount;
        private static float s_LastClearCheckTime;

        static UnityLogServer()
        {
            Initialize();
        }

        /// <summary>
        /// 初始化日志服务器
        /// </summary>
        private static void Initialize()
        {
            s_LogQueue = new Queue<LogEntry>(MAX_LOG_CAPACITY);
            s_IsRunning = false;
            s_CurrentPort = DEFAULT_PORT;

            // 注册日志回调
            Application.logMessageReceived += HandleLog;

            // 注册退出回调
            EditorApplication.quitting += OnQuit;

            // 初始化 Console Clear 检测
            InitializeConsoleClearCheck();

            // 启动TCP服务器
            StartServer();
        }

        /// <summary>
        /// 启动TCP服务器
        /// </summary>
        private static void StartServer()
        {
            try
            {
                // 尝试绑定默认端口
                if (TryStartServer(DEFAULT_PORT))
                {
                    s_CurrentPort = DEFAULT_PORT;
                }
                else
                {
                    // 端口被占用，随机选择可用端口
                    System.Random random = new System.Random();
                    int attempts = 0;
                    const int maxAttempts = 100;

                    while (attempts < maxAttempts)
                    {
                        int port = random.Next(MIN_PORT, MAX_PORT + 1);
                        if (TryStartServer(port))
                        {
                            s_CurrentPort = port;
                            break;
                        }
                        attempts++;
                    }

                    if (!s_IsRunning)
                    {
                        return;
                    }
                }

                // 写入端口文件
                WritePortFile(s_CurrentPort);

                // 打印启动日志
                Debug.Log($"[UnityLogServer] Server started on port {s_CurrentPort}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnityLogServer] StartServer error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 尝试在指定端口启动服务器
        /// </summary>
        private static bool TryStartServer(int port)
        {
            try
            {
                s_Listener = new TcpListener(IPAddress.Any, port);
                s_Listener.Start();
                s_IsRunning = true;

                s_ServerThread = new Thread(ServerThreadProc);
                s_ServerThread.IsBackground = true;
                s_ServerThread.Start();

                return true;
            }
            catch
            {
                s_IsRunning = false;
                if (s_Listener != null)
                {
                    s_Listener.Stop();
                    s_Listener = null;
                }
                return false;
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
                else
                {
                    s_LastConsoleLogCount = 0;
                }

                s_LastClearCheckTime = Time.realtimeSinceStartup;

                // 注册 EditorApplication.update 回调
                EditorApplication.update += CheckConsoleClear;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnityLogServer] InitializeConsoleClearCheck error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 检测 Console Clear 事件
        /// </summary>
        private static void CheckConsoleClear()
        {
            // 每 0.5 秒检查一次，避免过于频繁
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

                // 获取当前 Console 中的日志数量
                int currentCount = (int)s_LogEntriesGetCountMethod.Invoke(null, null);

                // 检测日志数量是否骤降（从较多日志变为很少或0）
                // 判断条件：上次数量 > 10 且 当前数量 < 上次数量的一半 且 当前数量 < 5
                if (s_LastConsoleLogCount > 10 && currentCount < s_LastConsoleLogCount / 2 && currentCount < 5)
                {
                    // 检测到 Console Clear，清空我们的日志缓存
                    ClearLogCache();
                    Debug.Log("[UnityLogServer] Console cleared, log cache synchronized");
                }

                s_LastConsoleLogCount = currentCount;
            }
            catch (Exception ex)
            {
                // 静默处理异常，避免频繁报错
            }
        }

        /// <summary>
        /// 清空日志缓存
        /// </summary>
        private static void ClearLogCache()
        {
            lock (s_LockObj)
            {
                s_LogQueue.Clear();
            }
        }

        /// <summary>
        /// 写入端口号到文件
        /// </summary>
        private static void WritePortFile(int port)
        {
            try
            {
                string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string portFile = Path.Combine(homeDir, ".unitylog_port.txt");
                File.WriteAllText(portFile, port.ToString());
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnityLogServer] WritePortFile error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 服务器线程处理
        /// </summary>
        private static void ServerThreadProc()
        {
            while (s_IsRunning)
            {
                try
                {
                    if (s_Listener != null && s_Listener.Pending())
                    {
                        TcpClient client = s_Listener.AcceptTcpClient();
                        ThreadPool.QueueUserWorkItem(HandleClient, client);
                    }
                    Thread.Sleep(10);
                }
                catch (System.Threading.ThreadAbortException)
                {
                    // 线程被强行中止（通常是Unity关闭时），这是正常行为，不要报错，静默处理即可。
                }
                catch (Exception ex)
                {
                    if (s_IsRunning)
                    {
                        Debug.LogError($"[UnityLogServer] ServerThreadProc error: {ex.Message}\n{ex.StackTrace}");
                    }
                }
            }
        }

        /// <summary>
        /// 处理客户端连接
        /// </summary>
        private static void HandleClient(object state)
        {
            TcpClient client = null;
            NetworkStream stream = null;
            try
            {
                client = (TcpClient)state;
                client.ReceiveTimeout = 5000;
                client.SendTimeout = 5000;
                stream = client.GetStream();

                // 使用MemoryStream收集完整数据
                using (var ms = new MemoryStream())
                {
                    byte[] buffer = new byte[BUFFER_SIZE];
                    int bytesRead;
                    while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        ms.Write(buffer, 0, bytesRead);
                        // 如果读取的字节数小于缓冲区大小，说明数据已经读完
                        if (bytesRead < buffer.Length)
                            break;
                    }
                    string requestJson = Encoding.UTF8.GetString(ms.ToArray());

                    if (ms.Length > 0)
                    {
                        string responseJson = ProcessQuery(requestJson);

                        byte[] responseBytes = Encoding.UTF8.GetBytes(responseJson);
                        stream.Write(responseBytes, 0, responseBytes.Length);
                    }
                }
            }
            catch (Exception ex)
            {
            }
            finally
            {
                stream?.Close();
                client?.Close();
            }
        }

        /// <summary>
        /// 处理查询请求
        /// </summary>
        private static string ProcessQuery(string requestJson)
        {
            QueryResponse response = new QueryResponse();

            try
            {
                // 解析JSON请求
                QueryRequest request;
                try
                {
                    request = JsonUtility.FromJson<QueryRequest>(requestJson);
                    
                    // 检查request是否为null
                    if (request == null)
                    {
                        response.success = false;
                        response.count = 0;
                        response.error = "Invalid JSON: empty request";
                        return JsonUtility.ToJson(response);
                    }
                    
                    // 检查所有字段是否为null（JsonUtility的已知问题）
                    if (request.count == null && request.minutes == null && 
                        string.IsNullOrEmpty(request.keyword) && 
                        string.IsNullOrEmpty(request.fuzzy) && 
                        string.IsNullOrEmpty(request.regex))
                    {
                        request = ManualParseJson(requestJson);
                    }
                }
                catch (Exception)
                {
                    response.success = false;
                    response.count = 0;
                    response.error = "Invalid JSON format";
                    return JsonUtility.ToJson(response);
                }

                // 验证参数
                string error = ValidateRequest(request);
                if (!string.IsNullOrEmpty(error))
                {
                    response.success = false;
                    response.error = error;
                    return JsonUtility.ToJson(response);
                }

                // 获取日志列表
                List<LogEntry> logs = GetLogs();

                // 应用筛选条件
                logs = FilterLogs(logs, request);

                // 构造响应
                response.success = true;
                response.count = logs.Count;
                response.logs = logs;

                return JsonUtility.ToJson(response);
            }
            catch (Exception ex)
            {
                response.success = false;
                response.error = $"Internal error: {ex.Message}";
                return JsonUtility.ToJson(response);
            }
        }

        /// <summary>
        /// 验证请求参数
        /// </summary>
        private static string ValidateRequest(QueryRequest request)
        {
            // 检查是否有查询条件
            if (request.count == null && request.minutes == null && 
                string.IsNullOrEmpty(request.keyword) && 
                string.IsNullOrEmpty(request.fuzzy) && 
                string.IsNullOrEmpty(request.regex))
            {
                return "No query conditions specified";
            }

            // 检查count和minutes不能同时使用
            if (request.count != null && request.minutes != null)
            {
                return "count and minutes cannot be used together";
            }

            // 检查count范围
            if (request.count.HasValue)
            {
                if (request.count.Value < 1 || request.count.Value > MAX_LOG_CAPACITY)
                {
                    return $"count must be between 1 and {MAX_LOG_CAPACITY}";
                }
            }

            // 检查minutes范围
            if (request.minutes.HasValue)
            {
                if (request.minutes.Value < 1 || request.minutes.Value > 60)
                {
                    return "minutes must be between 1 and 60";
                }
            }

            // 检查keyword、fuzzy、regex不能同时使用
            int filterCount = 0;
            if (!string.IsNullOrEmpty(request.keyword)) filterCount++;
            if (!string.IsNullOrEmpty(request.fuzzy)) filterCount++;
            if (!string.IsNullOrEmpty(request.regex)) filterCount++;

            if (filterCount > 1)
            {
                return "keyword, fuzzy, and regex cannot be used together";
            }

            // 检查正则表达式语法
            if (!string.IsNullOrEmpty(request.regex))
            {
                try
                {
                    Regex.IsMatch("", request.regex);
                }
                catch (ArgumentException)
                {
                    return "Invalid regex pattern";
                }
            }

            return null;
        }

        /// <summary>
        /// 获取所有日志
        /// </summary>
        private static List<LogEntry> GetLogs()
        {
            lock (s_LockObj)
            {
                return new List<LogEntry>(s_LogQueue);
            }
        }

        /// <summary>
        /// 筛选日志
        /// </summary>
        private static List<LogEntry> FilterLogs(List<LogEntry> logs, QueryRequest request)
        {
            // 按时间范围筛选
            if (request.minutes.HasValue)
            {
                DateTime cutoffTime = DateTime.UtcNow.AddMinutes(-request.minutes.Value);
                logs = logs.FindAll(log => DateTime.Parse(log.timestamp) >= cutoffTime);
            }

            // 按数量筛选（最近n条）
            if (request.count.HasValue)
            {
                int count = request.count.Value;
                if (logs.Count > count)
                {
                    logs = logs.GetRange(logs.Count - count, count);
                }
            }

            // 按严格关键词筛选
            if (!string.IsNullOrEmpty(request.keyword))
            {
                logs = logs.FindAll(log => log.message == request.keyword);
            }

            // 按模糊关键词筛选
            if (!string.IsNullOrEmpty(request.fuzzy))
            {
                logs = logs.FindAll(log => log.message.Contains(request.fuzzy));
            }

            // 按正则表达式筛选
            if (!string.IsNullOrEmpty(request.regex))
            {
                try
                {
                    Regex regex = new Regex(request.regex);
                    logs = logs.FindAll(log => regex.IsMatch(log.message));
                }
                catch (Exception)
                {
                    // 正则表达式验证已通过，这里不应出现异常
                }
            }

            return logs;
        }

        /// <summary>
        /// 处理Unity日志消息
        /// </summary>
        private static void HandleLog(string logString, string stackTrace, LogType type)
        {
            lock (s_LockObj)
            {
                // 创建日志条目
                LogEntry entry = new LogEntry(logString, stackTrace, type);

                // 添加到队列
                s_LogQueue.Enqueue(entry);

                // 如果超过容量限制，移除最早的日志
                while (s_LogQueue.Count > MAX_LOG_CAPACITY)
                {
                    s_LogQueue.Dequeue();
                }
            }
        }

        /// <summary>
        /// Unity编辑器退出时的清理
        /// </summary>
        private static void OnQuit()
        {
            StopServer();
        }

        /// <summary>
        /// 停止服务器
        /// </summary>
        private static void StopServer()
        {
            s_IsRunning = false;

            try
            {
                // 取消注册日志回调
                Application.logMessageReceived -= HandleLog;

                // 取消注册 Console Clear 检测回调
                EditorApplication.update -= CheckConsoleClear;

                if (s_ServerThread != null && s_ServerThread.IsAlive)
                {
                    // 增加超时时间到3000ms，并检查线程状态
                    if (!s_ServerThread.Join(3000))
                    {
                    }
                    s_ServerThread = null;
                }

                if (s_Listener != null)
                {
                    s_Listener.Stop();
                    s_Listener = null;
                }

                lock (s_LockObj)
                {
                    s_LogQueue?.Clear();
                }

                // 打印停止日志
                Debug.Log($"[UnityLogServer] Server stopped");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnityLogServer] StopServer error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 停止服务器
        /// </summary>
        [MenuItem("Tools/UnityLogServer/Stop", true)]
        public static bool StopServerValidate()
        {
            return s_IsRunning;
        }

        [MenuItem("Tools/UnityLogServer/Stop", false)]
        public static void StopServerMenu()
        {
            if (!s_IsRunning)
            {
                return;
            }

            try
            {
                StopServer();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnityLogServer] StopServerMenu error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 重启服务器
        /// </summary>
        [MenuItem("Tools/UnityLogServer/Restart", true)]
        public static bool RestartServerValidate()
        {
            return s_IsRunning;
        }

        [MenuItem("Tools/UnityLogServer/Restart", false)]
        public static void RestartServer()
        {
            // 检查服务器是否运行
            if (!s_IsRunning)
            {
                StartServer();
                return;
            }

            try
            {
                StopServer();

                // 重新注册回调（StopServer已取消注册）
                Application.logMessageReceived += HandleLog;

                StartServer();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnityLogServer] RestartServer error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 手动解析JSON（作为JsonUtility的备用方案）
        /// </summary>
        private static QueryRequest ManualParseJson(string json)
        {
            QueryRequest request = new QueryRequest();
            
            try
            {
                // 简单的JSON解析器：查找字段名和值
                // 格式: {"field": value}
                json = json.Trim();
                if (!json.StartsWith("{") || !json.EndsWith("}"))
                {
                    return request;
                }

                // 移除外层大括号
                json = json.Substring(1, json.Length - 2).Trim();

                // 分割字段
                string[] parts = json.Split(new[]{','}, StringSplitOptions.RemoveEmptyEntries);
                
                foreach (string part in parts)
                {
                    string field = part.Trim();
                    int colonIndex = field.IndexOf(':');
                    if (colonIndex <= 0) continue;

                    string fieldName = field.Substring(0, colonIndex).Trim().Trim('"');
                    string fieldValue = field.Substring(colonIndex + 1).Trim();

                    // 解析字段值
                    switch (fieldName)
                    {
                        case "count":
                            if (int.TryParse(fieldValue, out int countVal))
                            {
                                request.count = countVal;
                            }
                            break;
                        case "minutes":
                            if (int.TryParse(fieldValue, out int minutesVal))
                            {
                                request.minutes = minutesVal;
                            }
                            break;
                        case "keyword":
                            request.keyword = fieldValue.Trim('"');
                            break;
                        case "fuzzy":
                            request.fuzzy = fieldValue.Trim('"');
                            break;
                        case "regex":
                            request.regex = fieldValue.Trim('"');
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnityLogServer] ManualParseJson error: {ex.Message}\n{ex.StackTrace}");
            }

            return request;
        }
    }
}