using GameData.Common;
using GameData.Domains;
using GameData.Domains.Character;
using GameData.Domains.Taiwu;
using HarmonyLib;

namespace CombatSkillPresetBinding.Backend;

[HarmonyPatch(typeof(TaiwuDomain), nameof(TaiwuDomain.UpdateCombatSkillPlan))]
internal static class UpdateCombatSkillPlanPatch
{
    [HarmonyPrefix]
    private static void Prefix(DataContext context)
    {
        try
        {
            BindingStore.CaptureCurrentPlan(context);
        }
        catch (Exception exception)
        {
            ModLog.Error("切换运功预设前记录绑定失败，已继续执行原游戏逻辑：" + exception);
        }
    }

    [HarmonyPostfix]
    private static void Postfix(DataContext context)
    {
        try
        {
            BindingStore.ApplyCurrentPlan(context);
        }
        catch (Exception exception)
        {
            ModLog.Error("切换运功预设后应用绑定失败，已保留原游戏切换结果：" + exception);
        }
    }
}

[HarmonyPatch(typeof(TaiwuDomain), nameof(TaiwuDomain.ChangeCombatSkillBreakPlate))]
internal static class ChangeCombatSkillBreakPlatePatch
{
    [HarmonyPrefix]
    private static void Prefix(short skillId, out sbyte __state)
    {
        __state = -1;
        try
        {
            __state = BindingStore.GetCurrentBreakPresetIndex(skillId);
        }
        catch (Exception exception)
        {
            ModLog.Error("读取功法修改前的突破预设失败，已继续执行原游戏逻辑：" + exception);
        }
    }

    [HarmonyPostfix]
    private static void Postfix(DataContext context, short skillId, sbyte index, sbyte __state)
    {
        try
        {
            BindingStore.RecordBreakPresetChange(context, skillId, index, __state);
        }
        catch (Exception exception)
        {
            ModLog.Error("记录功法突破预设绑定失败，已保留原游戏修改结果：" + exception);
        }
    }
}

[HarmonyPatch(typeof(CharacterDomain), nameof(CharacterDomain.AddEquippedCombatSkill))]
internal static class AddEquippedCombatSkillPatch
{
    [HarmonyPostfix]
    private static void Postfix(DataContext context, int charId, short skillTemplateId)
    {
        try
        {
            BindingStore.ApplyEquippedSkill(context, charId, skillTemplateId);
        }
        catch (Exception exception)
        {
            ModLog.Error("装入功法后应用突破预设绑定失败，已保留原游戏装备结果：" + exception);
        }
    }
}

[HarmonyPatch(typeof(CharacterDomain), nameof(CharacterDomain.AutoEquipCombatSkills))]
internal static class AutoEquipCombatSkillsPatch
{
    [HarmonyPostfix]
    private static void Postfix(DataContext context, int charId)
    {
        if (charId != DomainManager.Taiwu.GetTaiwuCharId())
        {
            return;
        }

        try
        {
            BindingStore.ApplyCurrentPlan(context);
        }
        catch (Exception exception)
        {
            ModLog.Error("自动运功后应用突破预设绑定失败，已保留原游戏自动装备结果：" + exception);
        }
    }
}

[HarmonyPatch(typeof(TaiwuDomain), nameof(TaiwuDomain.ClearCombatSkillPlan))]
internal static class ClearCombatSkillPlanPatch
{
    [HarmonyPostfix]
    private static void Postfix(DataContext context)
    {
        try
        {
            BindingStore.ClearCurrentPlanBindings(context);
        }
        catch (Exception exception)
        {
            ModLog.Error("清空运功预设后移除绑定失败，已保留原游戏清空结果：" + exception);
        }
    }
}

[HarmonyPatch(typeof(TaiwuDomain), nameof(TaiwuDomain.CopyCombatSkillPlan))]
internal static class CopyCombatSkillPlanPatch
{
    [HarmonyPrefix]
    private static void Prefix(DataContext context, out int __state)
    {
        __state = -1;
        try
        {
            __state = BindingStore.BeginPlanMutation(context);
        }
        catch (Exception exception)
        {
            ModLog.Error("复制运功预设前记录绑定失败，已继续执行原游戏逻辑：" + exception);
        }
    }

    [HarmonyPostfix]
    private static void Postfix(DataContext context, int __state)
    {
        if (__state < 0)
        {
            return;
        }

        try
        {
            BindingStore.FinishCopyPlan(context, __state);
        }
        catch (Exception exception)
        {
            BindingStore.AbortPlanMutation();
            ModLog.Error("复制运功预设后同步绑定失败，已保留原游戏复制结果：" + exception);
        }
    }

    [HarmonyFinalizer]
    private static Exception? Finalizer(Exception? __exception, int __state)
    {
        if (__exception != null && __state >= 0)
        {
            BindingStore.AbortPlanMutation();
        }

        return __exception;
    }
}

[HarmonyPatch(typeof(TaiwuDomain), nameof(TaiwuDomain.AppendCombatSkillPlan))]
internal static class AppendCombatSkillPlanPatch
{
    [HarmonyPrefix]
    private static void Prefix(DataContext context, out int __state)
    {
        __state = -1;
        try
        {
            int previousPlanCount = DomainManager.Extra.GetUnlockedCombatSkillPlanCount();
            _ = BindingStore.BeginPlanMutation(context);
            __state = previousPlanCount;
        }
        catch (Exception exception)
        {
            ModLog.Error("新增运功预设前记录绑定失败，已继续执行原游戏逻辑：" + exception);
        }
    }

    [HarmonyPostfix]
    private static void Postfix(DataContext context, int __state)
    {
        if (__state < 0)
        {
            return;
        }

        try
        {
            BindingStore.FinishAppendPlan(context, __state);
        }
        catch (Exception exception)
        {
            BindingStore.AbortPlanMutation();
            ModLog.Error("新增运功预设后同步绑定失败，已保留原游戏新增结果：" + exception);
        }
    }

    [HarmonyFinalizer]
    private static Exception? Finalizer(Exception? __exception, int __state)
    {
        if (__exception != null && __state >= 0)
        {
            BindingStore.AbortPlanMutation();
        }

        return __exception;
    }
}

[HarmonyPatch(typeof(TaiwuDomain), nameof(TaiwuDomain.DeleteCombatSkillPlan))]
internal static class DeleteCombatSkillPlanPatch
{
    [HarmonyPrefix]
    private static void Prefix(DataContext context, out DeleteState __state)
    {
        __state = new DeleteState(-1, -1);
        try
        {
            int previousPlanCount = DomainManager.Extra.GetUnlockedCombatSkillPlanCount();
            int deletedPlanId = BindingStore.BeginPlanMutation(context);
            __state = new DeleteState(deletedPlanId, previousPlanCount);
        }
        catch (Exception exception)
        {
            ModLog.Error("删除运功预设前记录绑定失败，已继续执行原游戏逻辑：" + exception);
        }
    }

    [HarmonyPostfix]
    private static void Postfix(DataContext context, DeleteState __state)
    {
        if (__state.DeletedPlanId < 0)
        {
            return;
        }

        try
        {
            BindingStore.FinishDeletePlan(context, __state.DeletedPlanId, __state.PreviousPlanCount);
        }
        catch (Exception exception)
        {
            BindingStore.AbortPlanMutation();
            ModLog.Error("删除运功预设后同步绑定失败，已保留原游戏删除结果：" + exception);
        }
    }

    [HarmonyFinalizer]
    private static Exception? Finalizer(Exception? __exception, DeleteState __state)
    {
        if (__exception != null && __state.DeletedPlanId >= 0)
        {
            BindingStore.AbortPlanMutation();
        }

        return __exception;
    }

    internal readonly record struct DeleteState(int DeletedPlanId, int PreviousPlanCount);
}
