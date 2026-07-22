using GameData.Common;
using GameData.Domains;
using GameData.Domains.Mod;
using GameData.Serializer;

namespace VillageWorkOptimizer.Backend;

internal static class OptimizerModMethods
{
    internal const string CalculateMethod = "CalculateVillageWorkPlan";
    internal const string CalculatePrioritizedMethod = "CalculatePrioritizedVillageWorkPlan";

    internal static void Register(string modId)
    {
        DomainManager.Mod.AddModMethod(
            modId,
            CalculateMethod,
            (Func<DataContext, SerializableModData, SerializableModData>)Calculate);
        DomainManager.Mod.AddModMethod(
            modId,
            CalculatePrioritizedMethod,
            (Func<DataContext, SerializableModData, SerializableModData>)CalculatePrioritized);
    }

    private static SerializableModData Calculate(DataContext context, SerializableModData parameter)
    {
        var result = new SerializableModData();
        try
        {
            int objective = parameter.Get("Objective", out int requestedObjective) ? requestedObjective : 0;
            var plan = VillageWorkPlanner.Calculate(context, (PlanObjective)objective);

            result.Set("Success", true);
            result.Set("Reason", plan.Note);
            result.Set("ObjectiveName", plan.ObjectiveName);
            result.Set("Count", plan.Rows.Count);
            for (int i = 0; i < plan.Rows.Count; i++)
            {
                PlanRow row = plan.Rows[i];
                result.Set($"Building{i}", row.BuildingName);
                result.Set($"Leader{i}", row.LeaderName);
                result.Set($"Members{i}", row.MemberNames);
                result.Set($"Action{i}", row.Action);
                result.Set($"Purpose{i}", row.Purpose);
            }
        }
        catch (Exception ex)
        {
            result.Set("Success", false);
            result.Set("Reason", "计算失败：" + ex.Message);
            result.Set("Count", 0);
        }

        return result;
    }

    private static SerializableModData CalculatePrioritized(DataContext context, SerializableModData parameter)
    {
        var result = new SerializableModData();
        try
        {
            var priorities = new List<PlanObjective>();
            for (int i = 0; i < 5; i++)
            {
                if (parameter.Get($"Priority{i}", out int value) &&
                    Enum.IsDefined(typeof(PlanObjective), value) &&
                    !priorities.Contains((PlanObjective)value))
                    priorities.Add((PlanObjective)value);
            }
            foreach (PlanObjective objective in Enum.GetValues<PlanObjective>())
                if (!priorities.Contains(objective))
                    priorities.Add(objective);

            WorkPlan plan = VillageWorkPlanner.CalculatePrioritized(context, priorities);
            result.Set("Success", true);
            result.Set("Reason", plan.Note);
            result.Set("ObjectiveName", plan.ObjectiveName);
            result.Set("Count", plan.Rows.Count);
            for (int i = 0; i < plan.Rows.Count; i++)
            {
                PlanRow row = plan.Rows[i];
                result.Set($"Building{i}", row.BuildingName);
                result.Set($"Leader{i}", row.LeaderName);
                result.Set($"Members{i}", row.MemberNames);
                result.Set($"Action{i}", row.Action);
                result.Set($"Purpose{i}", row.Purpose);
            }
        }
        catch (Exception ex)
        {
            result.Set("Success", false);
            result.Set("Reason", "综合排班失败：" + ex.Message);
            result.Set("Count", 0);
        }
        return result;
    }
}
