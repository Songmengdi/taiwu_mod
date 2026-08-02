using System.Reflection;
using FrameWork;
using Game.Views.Bottom;
using Game.Views.MapBlockCharList;
using GameData.Domains.Character;
using GameData.Utilities;
using HarmonyLib;

namespace LiverFriendlyInteractions.Frontend;

[HarmonyPatch(typeof(ViewMapBlockCharList), nameof(ViewMapBlockCharList.OnClick),
    new[] { typeof(DisplayType), typeof(int) })]
internal static class WorldMapCharacterClickPatch
{
    [HarmonyPrefix]
    private static bool Prefix(DisplayType type, int id)
    {
        if (!FrontendPlugin.OverrideWorldMapCharacterClicks || id < 0)
            return true;

        InteractionPersonKind kind;
        if ((type & DisplayType.Normal) != 0)
            kind = InteractionPersonKind.Character;
        else if ((type & DisplayType.Caravan) != 0)
            kind = InteractionPersonKind.Caravan;
        else
            return true;

        return !InteractionHubRuntime.TryOpenForCharacter(
            id, kind, InteractionPersonGroup.CurrentBlock);
    }
}

[HarmonyPatch]
internal static class WorldMapTeammateClickPatch
{
    private static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(ViewBottom), "ClickTeammates", new[] { typeof(int) });

    [HarmonyPrefix]
    private static bool Prefix(int index)
    {
        if (!FrontendPlugin.OverrideWorldMapCharacterClicks)
            return true;

        List<int> ids = SingletonObject.getInstance<CharacterMonitorModel>()
            .GetTaiwuCombatTeamCharIds();
        if (index < 0 || index >= ids.Count || ids[index] < 0)
            return true;

        return !InteractionHubRuntime.TryOpenForCharacter(
            ids[index], InteractionPersonKind.Character, InteractionPersonGroup.Teammate);
    }
}

internal static class WorldMapCharacterOverrideHotloadRuntime
{
    private const string HarmonyId = "SMD.LiverFriendlyInteractions.WorldMapOverride.Hotload";

    internal static void Install()
    {
        var harmony = new Harmony(HarmonyId);
        Harmony.UnpatchID(HarmonyId);
        RemoveOlderOverridePatches(harmony,
            AccessTools.Method(typeof(ViewMapBlockCharList), nameof(ViewMapBlockCharList.OnClick),
                new[] { typeof(DisplayType), typeof(int) }),
            typeof(WorldMapCharacterClickPatch).FullName!);
        RemoveOlderOverridePatches(harmony,
            AccessTools.Method(typeof(ViewBottom), "ClickTeammates", new[] { typeof(int) }),
            typeof(WorldMapTeammateClickPatch).FullName!);
        harmony.CreateClassProcessor(typeof(WorldMapCharacterClickPatch)).Patch();
        harmony.CreateClassProcessor(typeof(WorldMapTeammateClickPatch)).Patch();
    }

    private static void RemoveOlderOverridePatches(Harmony harmony, MethodBase original,
        string patchTypeName)
    {
        Patches? info = Harmony.GetPatchInfo(original);
        if (info == null) return;
        foreach (MethodInfo patchMethod in info.Prefixes
                     .Select(patch => patch.PatchMethod)
                     .Where(method => method.DeclaringType?.FullName == patchTypeName)
                     .ToArray())
        {
            harmony.Unpatch(original, patchMethod);
        }
    }
}
