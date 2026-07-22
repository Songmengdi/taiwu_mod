using System.Text.Json;
using GameData.Common;
using GameData.Domains;
using GameData.Domains.CombatSkill;

namespace CombatSkillPresetBinding.Backend;

internal static class BindingStore
{
    private const string ArchiveDataKey = "CombatSkillPresetBindings.v1";
    private const int MaxCombatSkillPlanCount = 9;
    private const int MaxBreakPresetCount = 3;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static Dictionary<int, Dictionary<short, sbyte>> _plans = new();
    private static string _modId = string.Empty;
    private static bool _applying;
    private static int _planMutationDepth;

    internal static bool IsPlanMutationInProgress => _planMutationDepth > 0;

    internal static void SetModId(string modId)
    {
        _modId = modId;
    }

    internal static void Load()
    {
        _plans = new Dictionary<int, Dictionary<short, sbyte>>();

        try
        {
            if (string.IsNullOrEmpty(_modId)
                || !DomainManager.Mod.TryGet(_modId, ArchiveDataKey, isArchive: true, out string json)
                || string.IsNullOrWhiteSpace(json))
            {
                ModLog.Detail("当前存档尚无绑定数据，将在首次切换时自动建立。");
                return;
            }

            BindingDocument? document = JsonSerializer.Deserialize<BindingDocument>(json, JsonOptions);
            if (document?.Plans == null)
            {
                return;
            }

            foreach (PlanBindingDto plan in document.Plans)
            {
                if (plan.PlanId < 0 || plan.PlanId >= MaxCombatSkillPlanCount)
                {
                    continue;
                }

                Dictionary<short, sbyte> skills = new();
                if (plan.Skills != null)
                {
                    foreach ((short skillId, sbyte presetIndex) in plan.Skills)
                    {
                        if (skillId >= 0 && IsPresetIndexValid(presetIndex))
                        {
                            skills[skillId] = presetIndex;
                        }
                    }
                }

                _plans[plan.PlanId] = skills;
            }

            ModLog.Info($"已从存档载入 {_plans.Count} 套运功预设绑定。");
        }
        catch (Exception exception)
        {
            _plans = new Dictionary<int, Dictionary<short, sbyte>>();
            ModLog.Error("读取绑定数据失败，将使用空绑定：" + exception);
        }
    }

    internal static void ResetForNewWorld()
    {
        _plans = new Dictionary<int, Dictionary<short, sbyte>>();
        _applying = false;
        _planMutationDepth = 0;
    }

    internal static void ResetRuntimeState()
    {
        _plans = new Dictionary<int, Dictionary<short, sbyte>>();
        _applying = false;
        _planMutationDepth = 0;
        _modId = string.Empty;
    }

    internal static void CaptureCurrentPlan(DataContext context)
    {
        if (!BackendPlugin.Enabled || _applying || IsPlanMutationInProgress)
        {
            return;
        }

        int planId = DomainManager.Taiwu.GetCurrCombatSkillPlanId();
        CapturePlan(context, planId, persist: true);
    }

    internal static void ApplyCurrentPlan(DataContext context)
    {
        if (!BackendPlugin.Enabled || _applying || IsPlanMutationInProgress)
        {
            return;
        }

        int planId = DomainManager.Taiwu.GetCurrCombatSkillPlanId();
        if (!_plans.ContainsKey(planId))
        {
            CapturePlan(context, planId, persist: true);
            ModLog.Detail($"运功预设 {planId + 1} 首次使用，已采用当前功法突破预设作为初始绑定。");
            return;
        }

        Dictionary<short, sbyte> bindings = _plans[planId];
        bool changedDocument = false;
        int appliedCount = 0;
        _applying = true;

        try
        {
            foreach (short skillId in GetEquippedSkillIds())
            {
                try
                {
                    if (!TryGetCurrentPresetIndex(skillId, out sbyte currentIndex))
                    {
                        continue;
                    }

                    if (!bindings.TryGetValue(skillId, out sbyte targetIndex))
                    {
                        bindings[skillId] = currentIndex;
                        changedDocument = true;
                        continue;
                    }

                    if (targetIndex == currentIndex)
                    {
                        continue;
                    }

                    if (!CanSwitchToPreset(skillId, targetIndex))
                    {
                        ModLog.Detail($"跳过不可用的绑定：运功预设 {planId + 1}，功法 {skillId}，突破预设 {targetIndex + 1}。");
                        continue;
                    }

                    DomainManager.Taiwu.ChangeCombatSkillBreakPlate(context, skillId, targetIndex);
                    appliedCount++;
                }
                catch (Exception exception)
                {
                    ModLog.Warning($"应用运功预设 {planId + 1} 的功法 {skillId} 绑定失败，已跳过该功法：{exception.Message}");
                }
            }
        }
        catch (Exception exception)
        {
            ModLog.Error($"应用运功预设 {planId + 1} 的功法绑定失败：{exception}");
        }
        finally
        {
            _applying = false;
        }

        if (changedDocument)
        {
            Save(context);
        }

        ModLog.Detail($"已应用运功预设 {planId + 1}，切换 {appliedCount} 门功法的突破预设。");
    }

    internal static void ApplyEquippedSkill(DataContext context, int charId, short skillId)
    {
        if (!BackendPlugin.Enabled
            || _applying
            || IsPlanMutationInProgress
            || charId != DomainManager.Taiwu.GetTaiwuCharId()
            || skillId < 0
            || !TryGetCurrentPresetIndex(skillId, out sbyte currentIndex))
        {
            return;
        }

        int planId = DomainManager.Taiwu.GetCurrCombatSkillPlanId();
        Dictionary<short, sbyte> bindings = GetOrCreatePlan(planId);
        if (!bindings.TryGetValue(skillId, out sbyte targetIndex))
        {
            bindings[skillId] = currentIndex;
            Save(context);
            ModLog.Detail($"新增绑定：运功预设 {planId + 1}，功法 {skillId} → 突破预设 {currentIndex + 1}。");
            return;
        }

        if (targetIndex == currentIndex)
        {
            return;
        }

        if (!CanSwitchToPreset(skillId, targetIndex))
        {
            ModLog.Detail($"装入功法时跳过不可用的绑定：运功预设 {planId + 1}，功法 {skillId}，突破预设 {targetIndex + 1}。");
            return;
        }

        _applying = true;
        try
        {
            DomainManager.Taiwu.ChangeCombatSkillBreakPlate(context, skillId, targetIndex);
            ModLog.Detail($"装入功法时已恢复绑定：运功预设 {planId + 1}，功法 {skillId} → 突破预设 {targetIndex + 1}。");
        }
        catch (Exception exception)
        {
            ModLog.Warning($"装入功法时恢复绑定失败，已保留原游戏装备结果：运功预设 {planId + 1}，功法 {skillId}，{exception.Message}");
        }
        finally
        {
            _applying = false;
        }
    }

    internal static void ClearCurrentPlanBindings(DataContext context)
    {
        if (!BackendPlugin.Enabled || _applying || IsPlanMutationInProgress)
        {
            return;
        }

        int planId = DomainManager.Taiwu.GetCurrCombatSkillPlanId();
        if (_plans.Remove(planId))
        {
            Save(context);
            ModLog.Detail($"已随清空操作移除运功预设 {planId + 1} 的全部功法绑定。");
        }
    }

    internal static sbyte GetCurrentBreakPresetIndex(short skillId)
    {
        return TryGetCurrentPresetIndex(skillId, out sbyte presetIndex) ? presetIndex : (sbyte)-1;
    }

    internal static void RecordBreakPresetChange(DataContext context, short skillId, sbyte presetIndex, sbyte previousIndex)
    {
        if (!BackendPlugin.Enabled || _applying || IsPlanMutationInProgress || skillId < 0 || !IsPresetIndexValid(presetIndex))
        {
            return;
        }

        int planId = DomainManager.Taiwu.GetCurrCombatSkillPlanId();
        Dictionary<short, sbyte> bindings = GetOrCreatePlan(planId);

        bool changedDocument = false;
        if (IsPresetIndexValid(previousIndex))
        {
            int planCount = DomainManager.Extra.GetUnlockedCombatSkillPlanCount();
            for (int otherPlanId = 0; otherPlanId < planCount; otherPlanId++)
            {
                Dictionary<short, sbyte> otherBindings = GetOrCreatePlan(otherPlanId);
                if (!otherBindings.ContainsKey(skillId))
                {
                    otherBindings[skillId] = previousIndex;
                    changedDocument = true;
                }
            }
        }

        if (!bindings.TryGetValue(skillId, out sbyte oldIndex) || oldIndex != presetIndex)
        {
            bindings[skillId] = presetIndex;
            changedDocument = true;
        }

        if (!changedDocument)
        {
            return;
        }

        Save(context);
        ModLog.Detail($"记录：运功预设 {planId + 1}，功法 {skillId} → 突破预设 {presetIndex + 1}。");
    }

    internal static int BeginPlanMutation(DataContext context)
    {
        int sourcePlanId = DomainManager.Taiwu.GetCurrCombatSkillPlanId();
        if (BackendPlugin.Enabled && _planMutationDepth == 0)
        {
            CapturePlan(context, sourcePlanId, persist: false);
        }

        _planMutationDepth++;
        return sourcePlanId;
    }

    internal static void FinishCopyPlan(DataContext context, int sourcePlanId)
    {
        EndMutation();
        if (!BackendPlugin.Enabled)
        {
            return;
        }

        int targetPlanId = DomainManager.Taiwu.GetCurrCombatSkillPlanId();
        Dictionary<short, sbyte> source = GetOrCreatePlan(sourcePlanId);
        _plans[targetPlanId] = new Dictionary<short, sbyte>(source);
        Save(context);
        ApplyCurrentPlan(context);
        ModLog.Detail($"复制绑定：运功预设 {sourcePlanId + 1} → {targetPlanId + 1}。");
    }

    internal static void FinishAppendPlan(DataContext context, int previousPlanCount)
    {
        EndMutation();
        if (!BackendPlugin.Enabled)
        {
            return;
        }

        int currentPlanCount = DomainManager.Extra.GetUnlockedCombatSkillPlanCount();
        if (currentPlanCount <= previousPlanCount)
        {
            return;
        }

        int targetPlanId = DomainManager.Taiwu.GetCurrCombatSkillPlanId();
        _plans[targetPlanId] = new Dictionary<short, sbyte>();
        CapturePlan(context, targetPlanId, persist: true);
    }

    internal static void FinishDeletePlan(DataContext context, int deletedPlanId, int previousPlanCount)
    {
        EndMutation();
        if (!BackendPlugin.Enabled)
        {
            return;
        }

        if (previousPlanCount <= 1)
        {
            return;
        }

        for (int planId = deletedPlanId; planId < previousPlanCount - 1; planId++)
        {
            if (_plans.TryGetValue(planId + 1, out Dictionary<short, sbyte>? next))
            {
                _plans[planId] = next;
            }
            else
            {
                _plans.Remove(planId);
            }
        }

        _plans.Remove(previousPlanCount - 1);
        Save(context);
        ApplyCurrentPlan(context);
        ModLog.Detail($"已删除运功预设 {deletedPlanId + 1} 的绑定并重排后续绑定。");
    }

    private static void EndMutation()
    {
        if (_planMutationDepth > 0)
        {
            _planMutationDepth--;
        }
    }

    internal static void AbortPlanMutation()
    {
        EndMutation();
    }

    private static void CapturePlan(DataContext context, int planId, bool persist)
    {
        Dictionary<short, sbyte> bindings = GetOrCreatePlan(planId);
        bool changed = false;

        foreach (short skillId in GetEquippedSkillIds())
        {
            if (TryGetCurrentPresetIndex(skillId, out sbyte currentIndex)
                && !bindings.ContainsKey(skillId))
            {
                bindings[skillId] = currentIndex;
                changed = true;
            }
        }

        if (persist && changed)
        {
            Save(context);
        }
    }

    private static IEnumerable<short> GetEquippedSkillIds()
    {
        var equipment = DomainManager.Taiwu.GetTaiwu().GetCombatSkillEquipment();
        HashSet<short> uniqueIds = new();
        for (sbyte equipType = 0; equipType < 5; equipType++)
        {
            foreach (short skillId in equipment[equipType])
            {
                if (skillId >= 0 && uniqueIds.Add(skillId))
                {
                    yield return skillId;
                }
            }
        }
    }

    private static bool TryGetCurrentPresetIndex(short skillId, out sbyte presetIndex)
    {
        if (DomainManager.Taiwu.TryGetElement_CombatSkillBreakPresets(skillId, out CombatSkillBreakPreset? preset)
            && preset != null
            && IsPresetIndexValid(preset.CurrentIndex))
        {
            presetIndex = (sbyte)preset.CurrentIndex;
            return true;
        }

        presetIndex = 0;
        return true;
    }

    private static bool CanSwitchToPreset(short skillId, sbyte presetIndex)
    {
        if (!IsPresetIndexValid(presetIndex))
        {
            return false;
        }

        if (!DomainManager.Taiwu.TryGetElement_CombatSkillBreakPresets(skillId, out CombatSkillBreakPreset? preset)
            || preset == null)
        {
            return presetIndex == 0;
        }

        return presetIndex == preset.CurrentIndex
            || (preset.Presets != null && presetIndex < preset.Presets.Count);
    }

    private static bool IsPresetIndexValid(int presetIndex)
    {
        return presetIndex >= 0 && presetIndex < MaxBreakPresetCount;
    }

    private static Dictionary<short, sbyte> GetOrCreatePlan(int planId)
    {
        if (!_plans.TryGetValue(planId, out Dictionary<short, sbyte>? bindings))
        {
            bindings = new Dictionary<short, sbyte>();
            _plans[planId] = bindings;
        }

        return bindings;
    }

    private static void Save(DataContext context)
    {
        try
        {
            BindingDocument document = new()
            {
                Version = 1,
                Plans = _plans
                    .OrderBy(pair => pair.Key)
                    .Select(pair => new PlanBindingDto
                    {
                        PlanId = pair.Key,
                        Skills = new Dictionary<short, sbyte>(pair.Value),
                    })
                    .ToList(),
            };

            string json = JsonSerializer.Serialize(document, JsonOptions);
            _ = DomainManager.Mod.SetString(context, _modId, ArchiveDataKey, isArchive: true, json);
        }
        catch (Exception exception)
        {
            ModLog.Error("保存绑定数据失败：" + exception);
        }
    }

    private sealed class BindingDocument
    {
        public int Version { get; set; }

        public List<PlanBindingDto>? Plans { get; set; }
    }

    private sealed class PlanBindingDto
    {
        public int PlanId { get; set; }

        public Dictionary<short, sbyte>? Skills { get; set; }
    }
}
