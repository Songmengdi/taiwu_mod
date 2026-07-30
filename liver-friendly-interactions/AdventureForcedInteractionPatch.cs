using System.Linq;
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
internal static class AdventureForcedInteractionPatch
{
    private static bool Prefix(
        AdventureElement element,
        EAdventureElementEventTriggerType triggerType,
        ref EAdventureChanged __result)
    {
        bool hasManualInteractEvent = element.Core.Events.Any(
            eventData => eventData.TriggerType ==
                         EAdventureElementEventTriggerType.ManualInteract);

        if (!AdventureForcedInteractionPolicy.ShouldSuppressArrival(
                triggerType == EAdventureElementEventTriggerType.TaiwuArrivedElement,
                hasManualInteractEvent))
        {
            return true;
        }

        __result = EAdventureChanged.None;
        return false;
    }
}
