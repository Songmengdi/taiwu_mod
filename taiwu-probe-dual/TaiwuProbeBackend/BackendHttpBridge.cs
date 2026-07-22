using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TaiwuProbeBackend;

internal sealed class BackendHttpBridge : IDisposable
{
    private const int MaxRequestBytes = 1024 * 1024;
    private const int TakeoverAttempts = 20;
    private static readonly TimeSpan TakeoverRetryDelay = TimeSpan.FromMilliseconds(100);
    private readonly string _prefix;
    private readonly Action<string> _log;
    private readonly IPortConflictResolver _portConflictResolver;
    private readonly object _lifecycleLock = new();
    private HttpListener? _listener;
    private Thread? _acceptThread;
    private volatile bool _running;

    internal BackendHttpBridge(string prefix)
        : this(prefix, message => GameData.Utilities.AdaptableLog.Info(message), new StaleTaiwuProcessTerminator())
    {
    }

    internal BackendHttpBridge(string prefix, Action<string> log, IPortConflictResolver portConflictResolver)
    {
        _prefix = prefix;
        _log = log;
        _portConflictResolver = portConflictResolver;
    }

    internal void Start()
    {
        lock (_lifecycleLock)
        {
            if (_running)
                return;

            HttpListener? listener = TryStartListener();
            if (listener == null)
                return;

            _listener = listener;
            _running = true;
            _acceptThread = new Thread(() => AcceptLoop(listener))
            {
                IsBackground = true,
                Name = "TaiwuProbe-Backend-HTTP"
            };
            _acceptThread.Start();
            _log("[TaiwuProbeBackend] 内部桥已监听 " + _prefix);
        }
    }

    private HttpListener? TryStartListener()
    {
        try
        {
            return CreateStartedListener();
        }
        catch (HttpListenerException ex)
        {
            int port = new Uri(_prefix).Port;
            if (!_portConflictResolver.TryTerminateOwner(port, out string detail))
            {
                _log($"[TaiwuProbeBackend] 无法接管内部桥端口 {port}：{detail}；后端 MCP 已降级停用，不影响 GameData 加载。原始错误：{ex.Message}");
                return null;
            }

            _log($"[TaiwuProbeBackend] {detail}；正在接管端口 {port}。");
            for (int attempt = 1; attempt <= TakeoverAttempts; attempt++)
            {
                try
                {
                    return CreateStartedListener();
                }
                catch (HttpListenerException retryException)
                {
                    if (attempt == TakeoverAttempts)
                    {
                        _log($"[TaiwuProbeBackend] 已终止残留进程，但端口 {port} 仍无法绑定；后端 MCP 已降级停用，不影响 GameData 加载。最后错误：{retryException.Message}");
                        return null;
                    }

                    Thread.Sleep(TakeoverRetryDelay);
                }
            }
        }
        catch (Exception ex)
        {
            _log($"[TaiwuProbeBackend] 内部桥启动失败，后端 MCP 已降级停用，不影响 GameData 加载：{ex.GetType().Name}: {ex.Message}");
            return null;
        }

        return null;
    }

    private HttpListener CreateStartedListener()
    {
        var listener = new HttpListener();
        listener.Prefixes.Add(_prefix);
        try
        {
            listener.Start();
            return listener;
        }
        catch
        {
            listener.Close();
            throw;
        }
    }

    private void AcceptLoop(HttpListener listener)
    {
        while (_running)
        {
            try
            {
                HttpListenerContext context = listener.GetContext();
                ThreadPool.QueueUserWorkItem(_ => Handle(context));
            }
            catch when (!_running)
            {
                return;
            }
            catch (Exception ex)
            {
                _log("[TaiwuProbeBackend] 内部桥接收异常，监听已停止: " + ex.Message);
                return;
            }
        }
    }

    private static void Handle(HttpListenerContext context)
    {
        try
        {
            if (context.Request.HttpMethod != "POST" || context.Request.Url?.AbsolutePath != "/probe")
            {
                Write(context.Response, 404, "not found", "text/plain");
                return;
            }
            if (context.Request.ContentLength64 > MaxRequestBytes)
            {
                Write(context.Response, 413, "request too large", "text/plain");
                return;
            }

            string body;
            using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8, true, 4096, leaveOpen: false))
                body = reader.ReadToEnd();
            JObject request = JObject.Parse(body);
            string tool = request["tool"]?.Value<string>() ?? string.Empty;
            string argumentsJson = (request["arguments"] as JObject ?? new JObject()).ToString(Formatting.None);
            string text = BackendMainThreadRunner.Execute(tool, argumentsJson);
            var response = new JObject { ["text"] = text };
            Write(context.Response, 200, response.ToString(Formatting.None), "application/json; charset=utf-8");
        }
        catch (Exception ex)
        {
            var error = new JObject { ["text"] = $"<后端内部桥异常: {ex.GetType().Name}: {ex.Message}>" };
            Write(context.Response, 500, error.ToString(Formatting.None), "application/json; charset=utf-8");
        }
    }

    private static void Write(HttpListenerResponse response, int status, string body, string contentType)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(body);
        response.StatusCode = status;
        response.ContentType = contentType;
        response.ContentLength64 = bytes.Length;
        response.OutputStream.Write(bytes, 0, bytes.Length);
        response.Close();
    }

    public void Dispose()
    {
        Thread? acceptThread;
        lock (_lifecycleLock)
        {
            _running = false;
            try { _listener?.Stop(); } catch { }
            try { _listener?.Close(); } catch { }
            _listener = null;
            acceptThread = _acceptThread;
            _acceptThread = null;
        }

        if (acceptThread != null && acceptThread != Thread.CurrentThread)
            acceptThread.Join(TimeSpan.FromSeconds(1));
    }
}
