using GameData.Domains.TaiwuEvent;
using GameData.Domains.TaiwuEvent.DisplayEvent;

namespace LiverFriendlyInteractions.Backend;

internal static class InteractionHubReturnSession
{
    internal static bool Active { get; private set; }
    internal static int CharacterId { get; private set; } = -1;
    private static bool InteractionObserved { get; set; }
    private static bool PreserveAcrossHiddenDisplay { get; set; }
    private static bool Starting { get; set; }

    internal static void BeginFromMenu(int characterId)
    {
        Active = true;
        CharacterId = characterId;
        InteractionObserved = false;
        PreserveAcrossHiddenDisplay = false;
        Starting = false;
    }

    internal static void BeginDirectStart(int characterId, bool preserveAcrossHiddenDisplay)
    {
        Active = true;
        CharacterId = characterId;
        InteractionObserved = false;
        PreserveAcrossHiddenDisplay = preserveAcrossHiddenDisplay;
        Starting = true;
    }

    internal static void CommitDirectStart()
    {
        Starting = false;
        InteractionObserved = true;
    }

    internal static void BeginDirect(int characterId, bool preserveAcrossHiddenDisplay)
    {
        Active = true;
        CharacterId = characterId;
        InteractionObserved = true;
        PreserveAcrossHiddenDisplay = preserveAcrossHiddenDisplay;
        Starting = false;
    }

    internal static void Cancel()
    {
        Active = false;
        CharacterId = -1;
        InteractionObserved = false;
        PreserveAcrossHiddenDisplay = false;
        Starting = false;
    }

    internal static bool TryCloseReturnedMenu(TaiwuEventDomain domain, TaiwuEventDisplayData? display)
    {
        if (Active && display?.EventOptionInfos is { Count: 1 } optionInfos &&
            InteractionHubReturnPolicy.IsAutoReturnBridgeEvent(display.EventGuid))
        {
            domain.EventSelect(display.EventGuid, optionInfos[0].OptionKey);
            return true;
        }

        bool hasDisplay = display != null;
        bool isMenuEvent = hasDisplay && InteractionHubReturnPolicy.IsMenuEvent(display!.EventGuid);
        InteractionHubReturnDecision decision = InteractionHubReturnPolicy.Decide(
            Active, InteractionObserved, hasDisplay, isMenuEvent, domain.GetHasListeningEvent(),
            PreserveAcrossHiddenDisplay, Starting);
        if (decision == InteractionHubReturnDecision.ObserveInteraction)
        {
            InteractionObserved = true;
            return false;
        }
        if (decision == InteractionHubReturnDecision.CompleteWithoutMenu)
        {
            Cancel();
            return false;
        }
        if (decision != InteractionHubReturnDecision.CloseReturnedMenu)
            return false;

        Cancel();
        domain.ToEvent(string.Empty);
        return true;
    }
}
