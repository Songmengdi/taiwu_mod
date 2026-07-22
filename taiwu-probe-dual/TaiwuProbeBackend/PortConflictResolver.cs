using System;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace TaiwuProbeBackend;

internal interface IPortConflictResolver
{
    bool TryTerminateOwner(int port, out string detail);
}

internal sealed class StaleTaiwuProcessTerminator : IPortConflictResolver
{
    private const int AddressFamilyInterNetwork = 2;
    private const uint ErrorInsufficientBuffer = 122;

    public bool TryTerminateOwner(int port, out string detail)
    {
        int? ownerPid = FindTcpListenerOwner(port);
        if (ownerPid == 4)
            ownerPid = FindHttpSysListenerOwner(port, out _);
        if (ownerPid == null)
        {
            detail = $"未找到端口 {port} 的监听进程";
            return false;
        }

        using Process current = Process.GetCurrentProcess();
        if (ownerPid.Value == current.Id)
        {
            detail = $"端口 {port} 由当前游戏进程持有，拒绝终止自身";
            return false;
        }

        try
        {
            using Process owner = Process.GetProcessById(ownerPid.Value);
            if (!string.Equals(owner.ProcessName, current.ProcessName, StringComparison.OrdinalIgnoreCase))
            {
                detail = $"端口 {port} 由非太吾进程 {owner.ProcessName}（PID {owner.Id}）持有，拒绝误杀";
                return false;
            }

            int pid = owner.Id;
            owner.Kill(entireProcessTree: true);
            if (!owner.WaitForExit(3000))
            {
                detail = $"残留太吾进程 PID {pid} 在终止请求后仍未退出";
                return false;
            }

            detail = $"已终止占用端口的残留太吾进程 PID {pid}";
            return true;
        }
        catch (Exception ex)
        {
            detail = $"终止端口 {port} 的占用进程失败：{ex.GetType().Name}: {ex.Message}";
            return false;
        }
    }

    internal static int? FindHttpSysListenerOwner(int port, out string diagnostic)
    {
        try
        {
            using var netsh = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "netsh.exe",
                    Arguments = "http show servicestate view=requestq verbose=yes",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            netsh.Start();
            string output = netsh.StandardOutput.ReadToEnd();
            if (!netsh.WaitForExit(3000) || netsh.ExitCode != 0)
            {
                diagnostic = $"netsh exit={netsh.ExitCode}, outputLength={output.Length}";
                return null;
            }

            string urlPattern = @"https?://[^\s/]+:" + port + @"(?:/|\s)";
            Match url = Regex.Match(output, urlPattern, RegexOptions.IgnoreCase);
            if (url.Success)
            {
                MatchCollection processes = Regex.Matches(
                    output.Substring(0, url.Index),
                    @"(?m)^\s*ID:\s*(\d+)\s*,",
                    RegexOptions.IgnoreCase);
                if (processes.Count > 0 &&
                    int.TryParse(processes[processes.Count - 1].Groups[1].Value, out int processId))
                {
                    diagnostic = $"matched PID {processId}";
                    return processId;
                }
            }

            diagnostic = $"urlMatches={Regex.Matches(output, urlPattern, RegexOptions.IgnoreCase).Count}, outputLength={output.Length}";
        }
        catch (Exception ex)
        {
            diagnostic = ex.GetType().Name + ": " + ex.Message;
            return null;
        }

        return null;
    }

    internal static int? FindTcpListenerOwner(int port)
    {
        int bufferSize = 0;
        uint result = GetExtendedTcpTable(IntPtr.Zero, ref bufferSize, true,
            AddressFamilyInterNetwork, TcpTableClass.OwnerPidListener, 0);
        if (result != ErrorInsufficientBuffer || bufferSize <= 0)
            return null;

        IntPtr buffer = Marshal.AllocHGlobal(bufferSize);
        try
        {
            result = GetExtendedTcpTable(buffer, ref bufferSize, true,
                AddressFamilyInterNetwork, TcpTableClass.OwnerPidListener, 0);
            if (result != 0)
                return null;

            int rowCount = Marshal.ReadInt32(buffer);
            IntPtr rowPointer = IntPtr.Add(buffer, sizeof(int));
            int rowSize = Marshal.SizeOf<TcpRowOwnerPid>();
            for (int index = 0; index < rowCount; index++)
            {
                TcpRowOwnerPid row = Marshal.PtrToStructure<TcpRowOwnerPid>(rowPointer);
                int localPort = unchecked((ushort)IPAddress.NetworkToHostOrder((short)row.LocalPort));
                if (localPort == port)
                    return unchecked((int)row.OwningPid);

                rowPointer = IntPtr.Add(rowPointer, rowSize);
            }

            return null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(
        IntPtr tcpTable,
        ref int size,
        bool order,
        int ipVersion,
        TcpTableClass tableClass,
        uint reserved);

    private enum TcpTableClass
    {
        OwnerPidListener = 3
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TcpRowOwnerPid
    {
        public uint State;
        public uint LocalAddress;
        public uint LocalPort;
        public uint RemoteAddress;
        public uint RemotePort;
        public uint OwningPid;
    }
}
