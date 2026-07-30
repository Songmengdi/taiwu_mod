using GameData.Adventure;
using GameData.Domains.Adventure;
using GameData.Domains.TaiwuEvent;
using HarmonyLib;

namespace LiverFriendlyInteractions.Backend;

[HarmonyPatch(
    typeof(AdventureDomain),
    "TriggerElementEvents",
    new[]
    {
        typeof(AdventureRuntime),
        typeof(AdventureElement),
        typeof(EAdventureElementEventTriggerType),
        typeof(EventArgBox),
    })]
internal static class LargeMarketMerchantArrivalPatch
{
    private static bool Prefix(
        AdventureRuntime adventure,
        AdventureElement element,
        EAdventureElementEventTriggerType triggerType,
        ref EAdventureChanged __result)
    {
        if (!LargeMarketMerchantArrivalPolicy.ShouldSuppressForcedInteraction(
                adventure.CoreId,
                element.CharacterId,
                triggerType == EAdventureElementEventTriggerType.TaiwuArrivedElement))
        {
            return true;
        }

        __result = EAdventureChanged.None;
        return false;
    }
}
