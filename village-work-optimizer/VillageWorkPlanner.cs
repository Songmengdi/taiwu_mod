using System.Reflection;
using Config;
using GameData.Common;
using GameData.Domains;
using GameData.Domains.Building;
using GameData.Domains.Character;
using GameData.Domains.Character.Display;

namespace VillageWorkOptimizer.Backend;

internal enum PlanObjective
{
    Money = 0,
    Training = 1,
    Authority = 2,
    Recruit = 3,
    Resource = 4,
}

internal sealed record PlanRow(string BuildingName, string LeaderName, string MemberNames, string Action, string Purpose = "");

internal sealed record WorkPlan(string ObjectiveName, string Note, List<PlanRow> Rows);

internal sealed class PersonInfo
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required short Age { get; init; }
    public required short RoleTemplateId { get; init; }
    public required sbyte LeftPotential { get; init; }
    public required LifeSkillShorts LifeAttainments { get; init; }
    public required CombatSkillShorts CombatAttainments { get; init; }
    public required LifeSkillShorts LifeQualifications { get; init; }
    public required CombatSkillShorts CombatQualifications { get; init; }
    public bool IsChild => Age < 16;

    public int Attainment(BuildingBlockItem config)
    {
        if (config.RequireLifeSkillType >= 0 && config.RequireLifeSkillType < 16)
            return LifeAttainments.Get(config.RequireLifeSkillType);
        if (config.RequireCombatSkillType >= 0 && config.RequireCombatSkillType < 14)
            return CombatAttainments[config.RequireCombatSkillType];
        return 0;
    }

    public int Qualification(BuildingBlockItem config)
    {
        if (config.RequireLifeSkillType >= 0 && config.RequireLifeSkillType < 16)
            return LifeQualifications.Get(config.RequireLifeSkillType);
        if (config.RequireCombatSkillType >= 0 && config.RequireCombatSkillType < 14)
            return CombatQualifications[config.RequireCombatSkillType];
        return 0;
    }

    public bool RoleMatches(BuildingBlockItem config) =>
        config.VillagerRoleTemplateIds != null && config.VillagerRoleTemplateIds.Contains(RoleTemplateId);
}

internal sealed class BuildingInfo
{
    public required BuildingBlockKey Key { get; init; }
    public required BuildingBlockData Data { get; init; }
    public required BuildingBlockItem Config { get; init; }
    public required List<int> CurrentManagers { get; init; }
    public int MaxProgress => Math.Max(0, (int)Config.MaxProduceValue);
}

internal static class VillageWorkPlanner
{
    private static readonly MethodInfo? VillageBuildingsMethod = typeof(BuildingDomain).GetMethod(
        "GetTaiwuVillageNotEmptyBuildingBlockData",
        BindingFlags.Instance | BindingFlags.NonPublic);

    internal static WorkPlan Calculate(DataContext context, PlanObjective objective)
    {
        List<BuildingInfo> allBuildings = ReadBuildings();
        Dictionary<int, PersonInfo> people = ReadPeople(context, allBuildings);
        List<BuildingInfo> targets = allBuildings.Where(x => MatchesObjective(x.Config, objective)).ToList();

        if (targets.Count == 0)
            return new WorkPlan(ObjectiveName(objective), "当前太吾村没有适用于该目标的建筑。", new List<PlanRow>());

        var used = new HashSet<int>();
        var assignments = targets.ToDictionary(x => x.Key, _ => new List<int>());

        // 先保证每栋目标建筑拥有一名合法成人主事。临近结算的建筑先选人。
        foreach (BuildingInfo building in targets
                     .OrderByDescending(BuildingPriority)
                     .ThenBy(x => x.Config.TemplateId))
        {
            PersonInfo? leader = people.Values
                .Where(x => !x.IsChild && !used.Contains(x.Id))
                .OrderByDescending(x => LeaderScore(x, building, objective))
                .ThenBy(x => x.Id)
                .FirstOrDefault();
            if (leader == null)
                continue;
            assignments[building.Key].Add(leader.Id);
            used.Add(leader.Id);
        }

        if (objective == PlanObjective.Training)
            AssignStudents(targets, people, assignments, used);
        else
            AssignWorkers(targets, people, assignments, used);

        var rows = new List<PlanRow>();
        foreach (BuildingInfo building in targets.OrderBy(x => x.Config.Name))
        {
            List<int> assigned = assignments[building.Key];
            string leaderName = assigned.Count > 0 ? people[assigned[0]].Name : "空置";
            string memberNames = assigned.Count > 1
                ? string.Join("、", assigned.Skip(1).Select(id => people[id].Name))
                : "无";
            string action = DescribeChange(building.CurrentManagers, assigned, people);
            rows.Add(new PlanRow(building.Config.Name, leaderName, memberNames, action, ObjectiveName(objective)));
        }

        string note = "V1 启发式方案：已保证人物不重复，并按本月进度阈值或培养潜力分配；当前仅预览，不会修改岗位。";
        return new WorkPlan(ObjectiveName(objective), note, rows);
    }

    internal static WorkPlan CalculatePrioritized(DataContext context, IReadOnlyList<PlanObjective> priorities)
    {
        List<BuildingInfo> allBuildings = ReadBuildings();
        Dictionary<int, PersonInfo> people = ReadPeople(context, allBuildings);
        var used = new HashSet<int>();
        var assignments = allBuildings.ToDictionary(x => x.Key, _ => new List<int>());
        var purposes = new Dictionary<BuildingBlockKey, string>();

        foreach (PlanObjective objective in priorities)
        {
            List<BuildingInfo> targets = allBuildings
                .Where(x => MatchesObjective(x.Config, objective))
                .Where(x => assignments[x.Key].Count == 0)
                .OrderByDescending(BuildingPriority)
                .ThenBy(x => x.Config.TemplateId)
                .ToList();

            foreach (BuildingInfo building in targets)
            {
                PersonInfo? leader = people.Values
                    .Where(x => !x.IsChild && !used.Contains(x.Id))
                    .OrderByDescending(x => LeaderScore(x, building, objective))
                    .ThenBy(x => x.Id)
                    .FirstOrDefault();
                if (leader == null)
                    continue;
                assignments[building.Key].Add(leader.Id);
                used.Add(leader.Id);
                purposes[building.Key] = ObjectiveName(objective);
            }

            if (objective == PlanObjective.Training)
                AssignStudents(targets, people, assignments, used);
            else
                AssignWorkers(targets, people, assignments, used, stopAtCompletion: true);
        }

        // 完全没有被更高优先级使用的人，尽量保持原岗位，减少无意义调动。
        foreach (BuildingInfo building in allBuildings)
        {
            List<int> planned = assignments[building.Key];
            foreach (int id in building.CurrentManagers)
            {
                if (planned.Count >= 7 || used.Contains(id) || !people.ContainsKey(id))
                    continue;
                if (planned.Count == 0 && people[id].IsChild)
                    continue;
                planned.Add(id);
                used.Add(id);
            }
            if (planned.Count > 0 && !purposes.ContainsKey(building.Key))
                purposes[building.Key] = "保持原岗位";
        }

        var rows = new List<PlanRow>();
        foreach (BuildingInfo building in allBuildings.OrderBy(x => x.Config.Name))
        {
            List<int> planned = assignments[building.Key];
            bool changed = !building.CurrentManagers.SequenceEqual(planned);
            if (!changed && planned.Count == 0)
                continue;
            string leader = planned.Count > 0 ? people[planned[0]].Name : "空置";
            string members = planned.Count > 1
                ? string.Join("、", planned.Skip(1).Select(id => people[id].Name))
                : "无";
            rows.Add(new PlanRow(
                building.Config.Name,
                leader,
                members,
                DescribeChange(building.CurrentManagers, planned, people),
                purposes.TryGetValue(building.Key, out string? purpose) ? purpose : "未安排"));
        }

        string order = string.Join(" → ", priorities.Select(ObjectiveName));
        return new WorkPlan(
            "太吾村综合排班",
            $"优先级：{order}。V1.1 高效分配会优先跨越本月结算线，跨线后把剩余人员交给下一目标。",
            rows);
    }

    private static List<BuildingInfo> ReadBuildings()
    {
        if (VillageBuildingsMethod == null)
            throw new MissingMethodException("未找到太吾村建筑枚举方法。");

        object? value = VillageBuildingsMethod.Invoke(DomainManager.Building, null);
        if (value is not IEnumerable<(BuildingBlockKey, BuildingBlockData)> entries)
            throw new InvalidOperationException("无法读取太吾村建筑列表。");

        IReadOnlyDictionary<BuildingBlockKey, CharacterList> managers = DomainManager.Building.GetShopManagerDict();
        var result = new List<BuildingInfo>();
        foreach ((BuildingBlockKey key, BuildingBlockData data) in entries)
        {
            BuildingBlockItem config = data.ConfigData;
            if (!config.IsShop)
                continue;
            List<int> current = managers.TryGetValue(key, out CharacterList list)
                ? list.GetCollection().Where(x => x >= 0).ToList()
                : new List<int>();
            result.Add(new BuildingInfo { Key = key, Data = data, Config = config, CurrentManagers = current });
        }
        return result;
    }

    private static Dictionary<int, PersonInfo> ReadPeople(DataContext context, List<BuildingInfo> buildings)
    {
        var ids = new HashSet<int>(DomainManager.Taiwu.GetAllVillagersAvailableForWork());
        ids.UnionWith(DomainManager.Taiwu.GetAllChildAvailableForWork());
        foreach (BuildingInfo building in buildings)
            ids.UnionWith(building.CurrentManagers);

        List<VillagerSelectCharacterDisplayData> display =
            DomainManager.Taiwu.GetVillagersForWorkDisplayData(context, ids.OrderBy(x => x).ToList());
        var result = new Dictionary<int, PersonInfo>();
        foreach (VillagerSelectCharacterDisplayData item in display)
        {
            if (item.MainData == null || item.MainData.IsCompanion)
                continue;
            var (surname, givenName) = item.MainData.NameData.GetDisplayName(false);
            string name = (surname ?? string.Empty) + (givenName ?? string.Empty);
            if (string.IsNullOrWhiteSpace(name))
                name = "人物" + item.MainData.CharacterId;
            result[item.MainData.CharacterId] = new PersonInfo
            {
                Id = item.MainData.CharacterId,
                Name = name,
                Age = item.MainData.CurrAge,
                RoleTemplateId = item.RoleTemplateId,
                LeftPotential = item.LeftPotentialCount,
                LifeAttainments = item.LifeSkillAttainments,
                CombatAttainments = item.CombatSkillAttainments,
                LifeQualifications = item.MainData.LifeSkillQualifications,
                CombatQualifications = item.MainData.CombatSkillQualifications,
            };
        }
        return result;
    }

    private static bool MatchesObjective(BuildingBlockItem config, PlanObjective objective) => objective switch
    {
        PlanObjective.Money => config.FuncType == EBuildingBlockFuncType.Money,
        PlanObjective.Authority => config.FuncType == EBuildingBlockFuncType.Authority,
        PlanObjective.Recruit => config.FuncType == EBuildingBlockFuncType.People,
        PlanObjective.Resource => config.FuncType == EBuildingBlockFuncType.Material || config.IsCollectResourceBuilding,
        PlanObjective.Training => config.IsShop && config.NeedLeader &&
                                  (config.VillagerRoleTemplateIds?.Length ?? 0) > 0,
        _ => false,
    };

    private static double BuildingPriority(BuildingInfo building)
    {
        if (!building.Config.NeedShopProgress || building.MaxProgress <= 0)
            return 0;
        return (double)building.Data.ShopProgress / building.MaxProgress;
    }

    private static int LeaderScore(PersonInfo person, BuildingInfo building, PlanObjective objective)
    {
        int attainment = person.Attainment(building.Config);
        if (objective != PlanObjective.Training)
            return attainment;
        int qualification = person.Qualification(building.Config);
        return (person.RoleMatches(building.Config) ? 100000 : 0) + qualification * 100 + attainment;
    }

    private static void AssignWorkers(
        List<BuildingInfo> buildings,
        Dictionary<int, PersonInfo> people,
        Dictionary<BuildingBlockKey, List<int>> assignments,
        HashSet<int> used,
        bool stopAtCompletion = false)
    {
        while (true)
        {
            (BuildingInfo? building, PersonInfo? person, double gain) best = (null, null, 0);
            foreach (BuildingInfo building in buildings)
            {
                List<int> assigned = assignments[building.Key];
                if (assigned.Count == 0 || assigned.Count >= 7)
                    continue;
                if (stopAtCompletion && ReachesCompletion(building, assigned, people))
                    continue;
                double before = OutputProxy(building, assigned, people);
                foreach (PersonInfo person in people.Values)
                {
                    if (person.IsChild || used.Contains(person.Id))
                        continue;
                    assigned.Add(person.Id);
                    double gain = OutputProxy(building, assigned, people) - before;
                    assigned.RemoveAt(assigned.Count - 1);
                    if (gain > best.gain)
                        best = (building, person, gain);
                }
            }
            if (best.building == null || best.person == null || best.gain <= 0)
                break;
            assignments[best.building.Key].Add(best.person.Id);
            used.Add(best.person.Id);
        }
    }

    private static bool ReachesCompletion(
        BuildingInfo building,
        List<int> assigned,
        Dictionary<int, PersonInfo> people)
    {
        if (!building.Config.NeedShopProgress || building.MaxProgress <= 0 || assigned.Count == 0)
            return false;
        int attainment = 150 + people[assigned[0]].Attainment(building.Config);
        attainment += assigned.Skip(1).Sum(id => 50 + people[id].Attainment(building.Config)) / 3;
        return building.Data.ShopProgress + 650 + attainment >= building.MaxProgress;
    }

    private static double OutputProxy(BuildingInfo building, List<int> assigned, Dictionary<int, PersonInfo> people)
    {
        if (assigned.Count == 0)
            return 0;
        int attainment = 150 + people[assigned[0]].Attainment(building.Config);
        attainment += assigned.Skip(1).Sum(id => 50 + people[id].Attainment(building.Config)) / 3;
        if (!building.Config.NeedShopProgress || building.MaxProgress <= 0)
            return attainment;
        int next = building.Data.ShopProgress + 650 + attainment;
        if (next >= building.MaxProgress)
            return 1_000_000 + attainment * 10;
        return (double)next / building.MaxProgress * 100_000;
    }

    private static void AssignStudents(
        List<BuildingInfo> buildings,
        Dictionary<int, PersonInfo> people,
        Dictionary<BuildingBlockKey, List<int>> assignments,
        HashSet<int> used)
    {
        while (true)
        {
            (BuildingInfo? building, PersonInfo? person, int score) best = (null, null, int.MinValue);
            foreach (BuildingInfo building in buildings)
            {
                List<int> assigned = assignments[building.Key];
                if (assigned.Count == 0 || assigned.Count >= 7)
                    continue;
                PersonInfo leader = people[assigned[0]];
                if (!leader.RoleMatches(building.Config))
                    continue;
                foreach (PersonInfo student in people.Values)
                {
                    if (used.Contains(student.Id) || student.LeftPotential <= 0)
                        continue;
                    int gap = leader.Qualification(building.Config) - student.Qualification(building.Config);
                    int score = Math.Max(0, gap) * 100 + student.LeftPotential * 5 + (student.IsChild ? 10 : 0);
                    if (score > best.score)
                        best = (building, student, score);
                }
            }
            if (best.building == null || best.person == null || best.score <= 0)
                break;
            assignments[best.building.Key].Add(best.person.Id);
            used.Add(best.person.Id);
        }
    }

    private static string DescribeChange(List<int> current, List<int> planned, Dictionary<int, PersonInfo> people)
    {
        var actions = new List<string>();
        foreach (int id in planned)
        {
            string name = people.TryGetValue(id, out PersonInfo? p) ? p.Name : id.ToString();
            actions.Add(current.Contains(id) ? name + "保留" : name + "调入");
        }
        foreach (int id in current.Where(id => !planned.Contains(id)))
        {
            string name = people.TryGetValue(id, out PersonInfo? p) ? p.Name : id.ToString();
            actions.Add(name + "调出");
        }
        return actions.Count == 0 ? "保持空置" : string.Join("；", actions);
    }

    private static string ObjectiveName(PlanObjective objective) => objective switch
    {
        PlanObjective.Money => "最高资产收益",
        PlanObjective.Training => "最高人才培养",
        PlanObjective.Authority => "最高威望收益",
        PlanObjective.Recruit => "最高招人收益",
        PlanObjective.Resource => "最高资源收获",
        _ => "本月排班",
    };
}
