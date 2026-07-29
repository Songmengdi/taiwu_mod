using GameData.Common;
using GameData.Domains.TaiwuEvent;
using GameData.Domains.TaiwuEvent.DisplayEvent;
using HarmonyLib;

namespace LiverFriendlyInteractions.Backend;

[HarmonyPatch(typeof(TaiwuEventDomain), nameof(TaiwuEventDomain.SetDisplayingEventData))]
internal static class EventDisplayInterceptionPatch
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(
        TaiwuEventDomain __instance,
        TaiwuEventDisplayData? value,
        DataContext context)
    {
        if (MarketIntroDisplayInterceptor.TryFastForward(__instance, value))
        {
            return false;
        }

        if (MeditationRestPatch.TrySkipResultDisplay(__instance, value))
        {
            return false;
        }

        return true;
    }
}
