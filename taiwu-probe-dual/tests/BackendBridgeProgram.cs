using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using TaiwuProbeBackend;

if (args.Length == 2 && args[0] == "--occupy")
{
    using var childListener = new HttpListener();
    childListener.Prefixes.Add($"http://localhost:{int.Parse(args[1])}/");
    childListener.Start();
    Console.WriteLine("ready");
    Console.Out.Flush();
    Thread.Sleep(Timeout.Infinite);
    return;
}

int port;
using (var socket = new TcpListener(IPAddress.Loopback, 0))
{
    socket.Start();
    port = ((IPEndPoint)socket.LocalEndpoint).Port;
}

string prefix = $"http://localhost:{port}/";
using var blocker = new HttpListener();
blocker.Prefixes.Add(prefix);
blocker.Start();

var selfTerminator = new StaleTaiwuProcessTerminator();
if (selfTerminator.TryTerminateOwner(port, out string selfDetail) ||
    !selfDetail.Contains("拒绝终止自身", StringComparison.Ordinal))
    throw new Exception("端口接管器没有拒绝终止当前进程：" + selfDetail);

var resolver = new ReleasingConflictResolver(() =>
{
    blocker.Stop();
    blocker.Close();
});
using var bridge = new BackendHttpBridge(prefix, _ => { }, resolver);
bridge.Start();

if (resolver.CallCount != 1)
    throw new Exception("后端桥没有尝试接管被占用端口。");

using var client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(250) };
string? response = null;
DateTime deadline = DateTime.UtcNow.AddSeconds(5);
while (DateTime.UtcNow < deadline)
{
    try
    {
        using var content = new StringContent("{\"tool\":\"ping\",\"arguments\":{}}", Encoding.UTF8, "application/json");
        response = await (await client.PostAsync(prefix + "probe", content)).Content.ReadAsStringAsync();
        break;
    }
    catch (HttpRequestException)
    {
        await Task.Delay(50);
    }
    catch (TaskCanceledException)
    {
        await Task.Delay(50);
    }
}

if (response == null || !response.Contains("stub:ping", StringComparison.Ordinal))
    throw new Exception("后端桥没有在端口释放后自动恢复监听。");

bridge.Dispose();
using var rebound = new HttpListener();
rebound.Prefixes.Add(prefix);
rebound.Start();
rebound.Stop();

int occupiedPort;
using (var socket = new TcpListener(IPAddress.Loopback, 0))
{
    socket.Start();
    occupiedPort = ((IPEndPoint)socket.LocalEndpoint).Port;
}

string executable = Environment.ProcessPath
    ?? throw new Exception("无法确定测试进程路径。");
using var staleProcess = Process.Start(new ProcessStartInfo
{
    FileName = executable,
    Arguments = $"--occupy {occupiedPort}",
    UseShellExecute = false,
    CreateNoWindow = true
}) ?? throw new Exception("无法启动模拟残留进程。");

try
{
    DateTime listenDeadline = DateTime.UtcNow.AddSeconds(5);
    while (DateTime.UtcNow < listenDeadline &&
           !IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(x => x.Port == occupiedPort))
        await Task.Delay(25);

    if (!IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(x => x.Port == occupiedPort))
        throw new Exception("模拟残留进程没有成功占用端口。");

    int? tcpOwner = StaleTaiwuProcessTerminator.FindTcpListenerOwner(occupiedPort);
    if (tcpOwner != 4)
        throw new Exception($"HTTP.sys TCP 拥有者预期为 PID 4，实际为 {tcpOwner?.ToString() ?? "null"}。");
    int? httpOwner = StaleTaiwuProcessTerminator.FindHttpSysListenerOwner(occupiedPort, out string httpDiagnostic);
    if (httpOwner != staleProcess.Id)
        throw new Exception($"HTTP.sys 请求队列拥有者预期为 PID {staleProcess.Id}，实际为 {httpOwner?.ToString() ?? "null"}；{httpDiagnostic}。");

    var terminator = new StaleTaiwuProcessTerminator();
    if (!terminator.TryTerminateOwner(occupiedPort, out string takeoverDetail))
        throw new Exception("无法终止占用端口的同名残留进程：" + takeoverDetail);
    if (!staleProcess.HasExited)
        throw new Exception("端口占用进程仍在运行。");
}
finally
{
    if (!staleProcess.HasExited)
        staleProcess.Kill(entireProcessTree: true);
}

using var finalBind = new HttpListener();
finalBind.Prefixes.Add($"http://localhost:{occupiedPort}/");
finalBind.Start();

Console.WriteLine("TaiwuProbe backend bridge resilience passed.");

namespace TaiwuProbeBackend
{
    internal sealed class ReleasingConflictResolver : IPortConflictResolver
    {
        private readonly Action _release;

        internal ReleasingConflictResolver(Action release) => _release = release;

        internal int CallCount { get; private set; }

        public bool TryTerminateOwner(int port, out string detail)
        {
            CallCount++;
            _release();
            detail = "released by test resolver";
            return true;
        }
    }

    internal static class BackendMainThreadRunner
    {
        internal static string Execute(string tool, string argumentsJson) => "stub:" + tool;
    }
}

namespace GameData.Utilities
{
    internal static class AdaptableLog
    {
        internal static void Info(string message) { }
    }
}
