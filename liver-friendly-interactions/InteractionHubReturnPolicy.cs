namespace LiverFriendlyInteractions.Backend;

internal enum InteractionHubReturnDecision : byte
{
    None,
    ObserveInteraction,
    CloseReturnedMenu,
    CompleteWithoutMenu,
}

internal static class InteractionHubReturnPolicy
{
    private const string GiftCancelledEvent = "79705282-b752-4194-a11a-c627d2cbede5";
    private const string GiftSelectEvent = "5699d2a7-30c6-456e-9fe2-695b674e9e46";

    private static readonly HashSet<string> MenuEvents = new(StringComparer.Ordinal)
    {
        "567d1caf-8b28-4dbf-8cbe-e746e8ac8cfd",
        "05e87c45-f14e-49ef-8769-cbaced4753ae",
        "9dce4f27-347c-4588-9be4-08c1c7f1f4a3",
        "a9d0bcd8-e378-4ee9-96a6-1e5b9db17371",
        "bad63f08-115a-45aa-970c-fa203dd85e2b",
        "7c70ce0c-577a-4049-bcad-e593c63d62d4",
        "fb38f657-6ed0-41e4-a0c2-c82afb49762f",
        "e78e92d1-7712-4d0f-82d2-780b65f4a49b",
    };

    internal static bool IsMenuEvent(string eventGuid) => MenuEvents.Contains(eventGuid);

    internal static bool ShouldPreserveAcrossHiddenDisplay(short templateId) => templateId == 6;

    internal static bool IsAutoReturnBridgeEvent(string eventGuid) =>
        eventGuid == GiftCancelledEvent;

    internal static bool DidStartDirectInteraction(
        short templateId,
        bool nativeStarted,
        string? showingEventGuid) =>
        nativeStarted || templateId == 6 && showingEventGuid == GiftSelectEvent;

    internal static bool ShouldArmDirectReturn(
        bool started,
        bool eventStillShowing,
        bool externalListenerActive) =>
        started && (eventStillShowing || externalListenerActive);

    internal static InteractionHubReturnDecision Decide(
        bool active,
        bool interactionObserved,
        bool hasDisplay,
        bool isMenuEvent,
        bool externalListenerActive = false,
        bool preserveAcrossHiddenDisplay = false,
        bool starting = false)
    {
        if (!active) return InteractionHubReturnDecision.None;
        if (starting) return InteractionHubReturnDecision.None;
        if (!hasDisplay && preserveAcrossHiddenDisplay)
            return InteractionHubReturnDecision.None;
        if (!hasDisplay) return externalListenerActive
            ? interactionObserved
                ? InteractionHubReturnDecision.None
                : InteractionHubReturnDecision.ObserveInteraction
            : InteractionHubReturnDecision.CompleteWithoutMenu;
        if (isMenuEvent) return interactionObserved
            ? InteractionHubReturnDecision.CloseReturnedMenu
            : InteractionHubReturnDecision.None;
        return InteractionHubReturnDecision.ObserveInteraction;
    }
}
