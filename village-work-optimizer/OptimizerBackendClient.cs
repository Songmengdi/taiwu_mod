using GameData.Domains.Mod;
using GameData.Serializer;
using GameData.Utilities;

namespace VillageWorkOptimizer.Frontend;

internal sealed class PlanRowView
{
    internal string Building = string.Empty;
    internal string Leader = string.Empty;
    internal string Members = string.Empty;
    internal string Purpose = string.Empty;
    internal string Action = string.Empty;
}

internal static class OptimizerBackendClient
{
    internal static void Request(IReadOnlyList<int> priorities, Action<bool, string, List<PlanRowView>> callback)
    {
        var parameter = new SerializableModData();
        for (int i = 0; i < priorities.Count; i++)
            parameter.Set($"Priority{i}", priorities[i]);

        ModDomainMethod.AsyncCall.CallModMethodWithParamAndRet(
            null,
            FrontendPlugin.ModId,
            "CalculatePrioritizedVillageWorkPlan",
            parameter,
            (offset, pool) => Receive(offset, pool, callback));
    }

    private static void Receive(
        int offset,
        RawDataPool pool,
        Action<bool, string, List<PlanRowView>> callback)
    {
        var result = new SerializableModData();
        SerializerHolder<SerializableModData>.Deserialize(pool, offset, ref result);
        bool success = result.Get("Success", out bool successValue) && successValue;
        string reason = result.Get("Reason", out string reasonValue) ? reasonValue : string.Empty;
        int count = result.Get("Count", out int countValue) ? countValue : 0;
        var rows = new List<PlanRowView>();
        for (int i = 0; i < count; i++)
        {
            rows.Add(new PlanRowView
            {
                Building = Get(result, $"Building{i}"),
                Leader = Get(result, $"Leader{i}"),
                Members = Get(result, $"Members{i}"),
                Purpose = Get(result, $"Purpose{i}"),
                Action = Get(result, $"Action{i}"),
            });
        }
        callback(success, reason, rows);
    }

    private static string Get(SerializableModData data, string key) =>
        data.Get(key, out string value) ? value : string.Empty;
}
