using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using GameData.Common;
using GameData.Utilities.Coroutine;
using HarmonyLib;

namespace TaiwuProbeBackend;

internal static class BackendMainThreadRunner
{
    private const int TimeoutMilliseconds = 10000;
    private const int MaxRequestsPerFrame = 8;
    private static readonly ConcurrentQueue<Request> Pending = new();

    internal static string Execute(string tool, string argumentsJson)
    {
        var request = new Request(tool, argumentsJson);
        Pending.Enqueue(request);
        if (!request.Completion.Task.Wait(TimeoutMilliseconds))
            return "<后端主线程执行超时（10秒）>";
        return request.Completion.Task.Result;
    }

    internal static void Drain()
    {
        DataContext context = DataContextManager.GetCurrentThreadDataContext();
        for (int i = 0; i < MaxRequestsPerFrame && Pending.TryDequeue(out Request? request); i++)
        {
            try
            {
                request.Completion.TrySetResult(BackendTools.Execute(context, request.Tool, request.ArgumentsJson));
            }
            catch (Exception ex)
            {
                request.Completion.TrySetResult($"<后端工具异常: {ex.GetType().Name}: {ex.Message}>");
            }
        }
    }

    internal static void CancelPending(string reason)
    {
        while (Pending.TryDequeue(out Request? request))
            request.Completion.TrySetResult("<" + reason + ">");
    }

    private sealed class Request
    {
        internal readonly string Tool;
        internal readonly string ArgumentsJson;
        internal readonly TaskCompletionSource<string> Completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal Request(string tool, string argumentsJson)
        {
            Tool = tool;
            ArgumentsJson = argumentsJson;
        }
    }
}

[HarmonyPatch(typeof(CoroutineManager), nameof(CoroutineManager.OnUpdate))]
internal static class CoroutineManagerOnUpdatePatch
{
    private static void Prefix()
    {
        BackendMainThreadRunner.Drain();
    }
}
