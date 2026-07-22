using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using GameData.Utilities;

namespace TaiwuProbeFrontend
{
    /// <summary>
    /// MCP-over-HTTP 服务端，参照 dnSpy.Extension.MCP 的架构彻底重写。
    ///
    /// 核心改进：
    ///   - 每个请求通过线程池处理（ThreadPool.QueueUserWorkItem），不阻塞主循环
    ///   - 路径路由：/health (GET)、/mcp 或 / (POST/GET/DELETE)、其余 → 404
    ///   - Streamable HTTP (2025-03-26) 完整支持：
    ///       POST 带 Accept: text/event-stream → 创建/使用 session，返回 Mcp-Session-Id
    ///       GET 带 Accept: text/event-stream → SSE 长连接
    ///       POST 不带 event-stream → Legacy plain HTTP JSON-RPC
    ///       DELETE → 关闭 session
    ///   - 所有响应加 CORS 头
    ///   - SSE 连接不阻塞其它请求
    ///   - 未知 session ID 自动重建（服务端重启后客户端 session 变 stale 时不会 404）
    /// </summary>
    internal sealed class McpHttpServer : IDisposable
    {
        #region 字段

        /// <summary>HttpListener 监听前缀，形如 http://localhost:13131/mcp/。</summary>
        private readonly string _prefix;

        /// <summary>底层 HTTP 监听器，Start 时创建，Stop 时关闭。</summary>
        private HttpListener? _listener;

        /// <summary>主循环的取消令牌，Stop 时触发以退出接受循环。</summary>
        private CancellationTokenSource? _cts;

        /// <summary>服务是否在运行中，volatile 确保多线程可见性。</summary>
        private volatile bool _running;

        /// <summary>
        /// Streamable HTTP session 追踪表。
        /// key 为 session ID（Guid 字符串），value 为 session 对象。
        /// 服务端重启后清空，客户端发旧 ID 时会自动重建。
        /// </summary>
        private readonly ConcurrentDictionary<string, StreamableHttpSession> _sessions = new();

        /// <summary>
        /// Legacy SSE (2024-11-05) session 追踪表。
        /// key 为 session ID（Guid 字符串），value 持有 SSE 输出流。
        /// </summary>
        private readonly ConcurrentDictionary<string, SseSession> _sseSessions = new();

        #endregion

        #region 构造与生命周期

        /// <param name="prefix">HttpListener 前缀，形如 "http://localhost:13131/mcp/"。</param>
        public McpHttpServer(string prefix) => _prefix = prefix;

        /// <summary>启动 HTTP 监听。创建后台线程运行接受循环。</summary>
        public void Start()
        {
            _listener = new HttpListener();
            string prefixWithSlash = _prefix.TrimEnd('/') + "/";
            _listener.Prefixes.Add(prefixWithSlash);
            _listener.Start();
            _running = true;
            _cts = new CancellationTokenSource();

            // 独立后台线程：只负责 Accept，每个请求扔线程池处理
            Thread loopThread = new Thread(RunLoop)
            {
                IsBackground = true,
                Name = "TaiwuProbe-MCP-Accept"
            };
            loopThread.Start();

            AdaptableLog.Info(
                $"[TaiwuProbe] MCP 服务已启动，监听 {prefixWithSlash}");
        }

        /// <summary>停止 HTTP 监听。关闭所有活跃 SSE session，清理资源。</summary>
        public void Stop()
        {
            _running = false;
            _cts?.Cancel();
            try { _listener?.Stop(); } catch { }
            try { _listener?.Close(); } catch { }

            // 关闭所有活跃 Streamable HTTP session
            foreach (var kv in _sessions)
            {
                kv.Value.Close();
            }
            _sessions.Clear();

            // 关闭所有活跃 Legacy SSE session
            foreach (var kv in _sseSessions)
            {
                kv.Value.Close();
            }
            _sseSessions.Clear();

            _listener = null;
            _cts?.Dispose();
            _cts = null;
        }

        public void Dispose() => Stop();

        #endregion

        #region 主循环

        /// <summary>
        /// 主接受循环。每个请求通过线程池处理，确保 SSE 长连接不阻塞其它请求。
        /// </summary>
        private void RunLoop()
        {
            while (_running && _listener != null)
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = _listener.GetContext();
                }
                catch
                {
                    // _listener.Stop() 会导致 GetContext() 抛异常，正常退出
                    break;
                }

                // 每个请求在线程池上独立处理 —— 关键设计：
                // 如果直接在循环内同步处理，一个慢请求（如 SSE 长连接）会阻塞所有后续请求
                var ctxCapture = ctx;
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        HandleRequest(ctxCapture);
                    }
                    catch (Exception ex)
                    {
                        AdaptableLog.Info($"[TaiwuProbe] 请求处理异常: {ex.Message}");
                        try { ctxCapture.Response.StatusCode = 500; ctxCapture.Response.Close(); }
                        catch { /* ignore */ }
                    }
                });
            }
        }

        #endregion

        #region 请求路由

        /// <summary>
        /// 入口路由：CORS → 路径 → HTTP 方法。
        /// 参照 dnSpy 的 HandleHttpRequest 模式。
        /// </summary>
        private void HandleRequest(HttpListenerContext ctx)
        {
            // 任意 C# 工具使浏览器跨源访问不可接受。原生 MCP 客户端不发送 Origin；
            // 网页请求会被直接拒绝，避免恶意站点借 localhost 执行游戏进程内代码。
            string origin = ctx.Request.Headers["Origin"] ?? string.Empty;
            if (origin.Length > 0)
            {
                ctx.Response.StatusCode = 403;
                ctx.Response.Close();
                return;
            }

            ctx.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, DELETE, OPTIONS";
            ctx.Response.Headers["Access-Control-Allow-Headers"] =
                "Content-Type, Accept, Mcp-Session-Id, MCP-Protocol-Version";
            ctx.Response.Headers["Access-Control-Expose-Headers"] = "Mcp-Session-Id";

            var path = ctx.Request.Url?.AbsolutePath ?? string.Empty;
            var httpMethod = ctx.Request.HttpMethod.ToUpperInvariant();

            // OPTIONS → CORS 预检
            if (httpMethod == "OPTIONS")
            {
                ctx.Response.StatusCode = 200;
                ctx.Response.Close();
                return;
            }

            // /health → 存活检查（用于负载均衡或容器化环境的健康检查）
            if (path == "/health" && httpMethod == "GET")
            {
                byte[] health = Encoding.UTF8.GetBytes(
                    "{\"status\":\"ok\",\"service\":\"taiwu-probe\"}");
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "application/json";
                ctx.Response.ContentLength64 = health.Length;
                ctx.Response.OutputStream.Write(health, 0, health.Length);
                ctx.Response.Close();
                return;
            }

            // /mcp 或 / → MCP 协议端点
            bool isMcpPath = path == "/mcp" || path == "/mcp/" || path == "/";

            if (isMcpPath)
            {
                string accept = ctx.Request.Headers["Accept"] ?? string.Empty;
                bool acceptsEventStream = accept.IndexOf(
                    "text/event-stream", StringComparison.OrdinalIgnoreCase) >= 0;

                switch (httpMethod)
                {
                    case "POST":
                        if (acceptsEventStream)
                            HandleStreamableHttpPost(ctx);
                        else
                            HandleLegacyPlainPost(ctx);
                        return;

                    case "GET":
                        if (acceptsEventStream)
                            HandleStreamableHttpGet(ctx);
                        else
                            HandleStatusPage(ctx); // 浏览器直接访问 → HTML 状态页
                        return;

                    case "DELETE":
                        HandleStreamableHttpDelete(ctx);
                        return;
                }
            }

            // Legacy MCP SSE transport (2024-11-05)
            if (path == "/sse" && httpMethod == "GET")
            {
                HandleLegacySseGet(ctx);
                return;
            }
            if (path == "/message" && httpMethod == "POST")
            {
                HandleLegacySsePost(ctx);
                return;
            }

            // 不匹配任何路由 → 404
            ctx.Response.StatusCode = 404;
            ctx.Response.Close();
        }

        #endregion

        #region MCP Streamable HTTP (2025-03-26) — POST

        /// <summary>
        /// Streamable HTTP POST。读取 JSON-RPC 请求体，在 initialize 时创建 session 并
        /// 通过 Mcp-Session-Id 头返回。后续请求通过该头识别 session。
        /// 响应始终是 application/json（协议允许内联响应）。
        /// 通知类消息返回 202 Accepted。
        /// </summary>
        private void HandleStreamableHttpPost(HttpListenerContext ctx)
        {
            string body;
            using (var sr = new StreamReader(
                ctx.Request.InputStream, ctx.Request.ContentEncoding))
                body = sr.ReadToEnd();

            string method = JsonRpc.ExtractMethod(body);
            bool isInitialize = string.Equals(method, "initialize", StringComparison.Ordinal);

            if (isInitialize)
            {
                var session = new StreamableHttpSession(Guid.NewGuid().ToString("N"));
                _sessions[session.Id] = session;
                ctx.Response.Headers["Mcp-Session-Id"] = session.Id;
                AdaptableLog.Info(
                    $"[TaiwuProbe] Streamable HTTP session opened: {session.Id}");
            }
            else
            {
                // 非 initialize 请求，检查 Mcp-Session-Id
                string headerSessionId = ctx.Request.Headers["Mcp-Session-Id"] ?? string.Empty;
                if (!string.IsNullOrEmpty(headerSessionId) &&
                    !_sessions.ContainsKey(headerSessionId))
                {
                    // 未知 session ID：自动重建（服务器重启后会清空 session，客户端 session 变 stale）
                    _sessions[headerSessionId] = new StreamableHttpSession(headerSessionId);
                    AdaptableLog.Info(
                        $"[TaiwuProbe] Streamable HTTP session auto-recreated: {headerSessionId}");
                }
            }

            bool isNotification = method.StartsWith("notifications/",
                StringComparison.Ordinal);

            if (isNotification)
            {
                // 通知不需要响应体（JSON-RPC 通知语义）
                JsonRpc.Dispatch(body);
                ctx.Response.StatusCode = 202;
                ctx.Response.ContentLength64 = 0;
                ctx.Response.Close();
                return;
            }

            string responseJson = JsonRpc.Dispatch(body);
            byte[] bytes = Encoding.UTF8.GetBytes(responseJson);
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "application/json";
            ctx.Response.ContentLength64 = bytes.Length;
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            ctx.Response.Close();
        }

        #endregion

        #region MCP Streamable HTTP — GET（SSE 长连接）

        /// <summary>
        /// Streamable HTTP GET：打开 SSE 长连接。客户端通过此连接接收服务端推送。
        /// 当前无主动推送内容，仅发送 keepalive ping 维持连接。
        /// 客户端断开或服务器停止时自动退出。
        /// </summary>
        private void HandleStreamableHttpGet(HttpListenerContext ctx)
        {
            string sessionId = ctx.Request.Headers["Mcp-Session-Id"] ?? string.Empty;
            if (string.IsNullOrEmpty(sessionId) || !_sessions.ContainsKey(sessionId))
            {
                ctx.Response.StatusCode = 404;
                byte[] err = Encoding.UTF8.GetBytes("Unknown Mcp-Session-Id");
                ctx.Response.OutputStream.Write(err, 0, err.Length);
                ctx.Response.Close();
                return;
            }

            ctx.Response.ContentType = "text/event-stream";
            ctx.Response.Headers["Cache-Control"] = "no-cache";
            ctx.Response.SendChunked = true;
            ctx.Response.KeepAlive = true;

            AdaptableLog.Info(
                $"[TaiwuProbe] Streamable HTTP GET stream opened: {sessionId}");

            var token = _cts?.Token ?? CancellationToken.None;
            try
            {
                // 保持连接打开，等待客户端断开或服务器停止
                while (!token.IsCancellationRequested && _running)
                {
                    Thread.Sleep(30000);
                }
            }
            finally
            {
                AdaptableLog.Info(
                    $"[TaiwuProbe] Streamable HTTP GET stream closed: {sessionId}");
                try { ctx.Response.OutputStream.Close(); } catch { }
                try { ctx.Response.Close(); } catch { }
            }
        }

        #endregion

        #region MCP Streamable HTTP — DELETE

        /// <summary>
        /// 关闭 Streamable HTTP session。即使 session 不存在也返回 200（幂等删除）。
        /// </summary>
        private void HandleStreamableHttpDelete(HttpListenerContext ctx)
        {
            string sessionId = ctx.Request.Headers["Mcp-Session-Id"] ?? string.Empty;
            if (!string.IsNullOrEmpty(sessionId) && _sessions.TryRemove(sessionId, out var session))
            {
                session.Close();
                AdaptableLog.Info(
                    $"[TaiwuProbe] Streamable HTTP session closed by DELETE: {sessionId}");
            }
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentLength64 = 0;
            ctx.Response.Close();
        }

        #endregion

        #region Legacy SSE transport (2024-11-05)

        /// <summary>
        /// Legacy MCP SSE GET：打开 SSE 长连接，发送 endpoint 事件告知客户端
        /// POST 路径（/message?sessionId=xxx）。客户端后续 JSON-RPC 请求通过
        /// POST 发送到该路径，响应通过 SSE 流返回。
        ///
        /// 这是 MCP 最初的 HTTP 传输方案（2024-11-05 规范），部分客户端（如
        /// 早期 ZCode MCP 实现）可能使用此传输。
        /// </summary>
        private void HandleLegacySseGet(HttpListenerContext ctx)
        {
            ctx.Response.ContentType = "text/event-stream";
            ctx.Response.Headers["Cache-Control"] = "no-cache";
            ctx.Response.SendChunked = true;
            ctx.Response.KeepAlive = true;

            var sessionId = Guid.NewGuid().ToString("N");
            var session = new SseSession(sessionId, ctx.Response.OutputStream);
            _sseSessions[sessionId] = session;

            AdaptableLog.Info(
                $"[TaiwuProbe] Legacy SSE session opened: {sessionId}");

            // 告诉客户端 POST 端点路径
            session.WriteEvent("endpoint", $"/message?sessionId={sessionId}");

            var token = _cts?.Token ?? CancellationToken.None;
            try
            {
                // 保持连接打开，等待客户端断开或服务器停止
                while (!token.IsCancellationRequested && _running)
                {
                    Thread.Sleep(30000);
                }
            }
            finally
            {
                _sseSessions.TryRemove(sessionId, out _);
                AdaptableLog.Info(
                    $"[TaiwuProbe] Legacy SSE session closed: {sessionId}");
                try { ctx.Response.OutputStream.Close(); } catch { }
                try { ctx.Response.Close(); } catch { }
            }
        }

        /// <summary>
        /// Legacy MCP SSE POST：接收客户端通过 /message?sessionId=xxx 发送的
        /// JSON-RPC 请求，处理后通过对应 SSE 连接的 message 事件返回响应。
        /// </summary>
        private void HandleLegacySsePost(HttpListenerContext ctx)
        {
            string sessionId = ctx.Request.QueryString["sessionId"] ?? string.Empty;
            if (string.IsNullOrEmpty(sessionId) || !_sseSessions.TryGetValue(sessionId, out var session))
            {
                ctx.Response.StatusCode = 404;
                byte[] err = Encoding.UTF8.GetBytes("Unknown sessionId");
                ctx.Response.OutputStream.Write(err, 0, err.Length);
                ctx.Response.Close();
                return;
            }

            string body;
            using (var sr = new StreamReader(
                ctx.Request.InputStream, ctx.Request.ContentEncoding))
                body = sr.ReadToEnd();

            // 先 ACK POST（202 Accepted）
            ctx.Response.StatusCode = 202;
            byte[] ack = Encoding.UTF8.GetBytes("Accepted");
            ctx.Response.OutputStream.Write(ack, 0, ack.Length);
            ctx.Response.Close();

            // 处理请求，将响应通过 SSE 流发回
            string method = JsonRpc.ExtractMethod(body);
            bool isNotification = method.StartsWith("notifications/",
                StringComparison.Ordinal) || JsonRpc.ExtractIdRaw(body) == null;

            if (!isNotification)
            {
                string responseJson = JsonRpc.Dispatch(body);
                session.WriteEvent("message", responseJson);
            }
            else
            {
                JsonRpc.Dispatch(body);
            }
        }

        #endregion

        #region Legacy plain HTTP POST

        /// <summary>
        /// 传统纯 HTTP 的 JSON-RPC（不带 SSE/Streamable 传输层）。
        /// 直接读取请求体 → 分发 → 返回 JSON 响应。
        /// 不检查 session ID，与 Streamable HTTP 路径共存。
        /// </summary>
        private void HandleLegacyPlainPost(HttpListenerContext ctx)
        {
            string body;
            using (var sr = new StreamReader(
                ctx.Request.InputStream, ctx.Request.ContentEncoding))
                body = sr.ReadToEnd();

            string method = JsonRpc.ExtractMethod(body);
            string responseJson = JsonRpc.Dispatch(body);

            if (method == "initialize")
            {
                ctx.Response.Headers["Mcp-Session-Id"] =
                    Guid.NewGuid().ToString("N");
            }

            byte[] bytes = Encoding.UTF8.GetBytes(responseJson);
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "application/json";
            ctx.Response.ContentLength64 = bytes.Length;
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            ctx.Response.Close();
        }

        #endregion

        #region 状态页

        /// <summary>
        /// 浏览器直接 GET /mcp 时的 HTML 状态页，参照 dnSpy 的做法提供可读信息。
        /// 纯展示用途，不影响 MCP 协议功能。
        /// </summary>
        private void HandleStatusPage(HttpListenerContext ctx)
        {
            // 从前缀中提取端口号
            int port = 13131;
            try { port = new Uri(_prefix).Port; } catch { }

            var html =
                "<!doctype html><html><head><meta charset=\"utf-8\">" +
                "<title>TaiwuProbe MCP Server</title></head>" +
                "<body style=\"font-family:system-ui,sans-serif;" +
                "max-width:42rem;margin:3rem auto;line-height:1.5\">" +
                "<h1>TaiwuProbe MCP Server</h1>" +
                $"<p><b>状态:</b> 运行中，端口 {port}</p>" +
                "<p>这是太吾绘卷调试探针的 MCP 端点，不是网站。" +
                "请用 MCP 客户端连接。</p>" +
                "<ul>" +
                $"<li><code>GET /health</code> — 存活检查 (<a href=\"/health\">/health</a>)</li>" +
                "<li><code>POST /</code> — JSON-RPC（纯 HTTP 或 MCP Streamable HTTP）</li>" +
                "<li><code>GET /sse</code> — 传统 MCP SSE 传输</li>" +
                "</ul></body></html>";
            byte[] buffer = Encoding.UTF8.GetBytes(html);
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/html; charset=utf-8";
            ctx.Response.ContentLength64 = buffer.Length;
            ctx.Response.OutputStream.Write(buffer, 0, buffer.Length);
            ctx.Response.Close();
        }

        #endregion
    }

    /// <summary>
    /// Streamable HTTP session。追踪 session 身份，不做其它事情（当前无服务端推送）。
    /// session 在 initialize 时创建，DELETE 或服务重启时销毁。
    /// </summary>
    internal sealed class StreamableHttpSession
    {
        /// <summary>session 唯一标识（Guid 字符串）。</summary>
        public string Id { get; }

        /// <summary>session 创建时间（UTC），可用于判断 session 年龄。</summary>
        public DateTime CreatedAtUtc { get; } = DateTime.UtcNow;

        public StreamableHttpSession(string id) => Id = id;

        /// <summary>预留：关闭 session 时清理资源。</summary>
        public void Close() { /* 预留：清理资源 */ }
    }

    /// <summary>
    /// Legacy SSE (2024-11-05) session。持有 SSE 输出流，提供 WriteEvent/WriteComment
    /// 辅助方法，带写锁保证多线程安全。
    /// </summary>
    internal sealed class SseSession
    {
        private readonly System.IO.Stream _stream;
        private readonly object _writeLock = new();

        public string Id { get; }

        public SseSession(string id, System.IO.Stream stream)
        {
            Id = id;
            _stream = stream;
        }

        /// <summary>写 SSE 命名事件。多行 data 自动拆分成多行。</summary>
        public void WriteEvent(string eventName, string data)
        {
            var sb = new StringBuilder();
            sb.Append("event: ").Append(eventName).Append('\n');
            foreach (var line in data.Split('\n'))
                sb.Append("data: ").Append(line.TrimEnd('\r')).Append('\n');
            sb.Append('\n');
            WriteRaw(sb.ToString());
        }

        /// <summary>写 SSE 注释行（用于 keepalive ping）。</summary>
        public void WriteComment(string text) =>
            WriteRaw(": " + text + "\n\n");

        /// <summary>关闭 session（当前无资源需清理，保留供 Stop 时调用）。</summary>
        public void Close() { }

        private void WriteRaw(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            lock (_writeLock)
            {
                _stream.Write(bytes, 0, bytes.Length);
                _stream.Flush();
            }
        }
    }
}
