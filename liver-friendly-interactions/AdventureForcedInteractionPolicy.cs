namespace LiverFriendlyInteractions.Backend;

internal static class AdventureForcedInteractionPolicy
{
    internal static bool ShouldSuppressArrival(
        bool isTaiwuArrivedElement,
        bool hasManualInteractEvent) =>
        isTaiwuArrivedElement && hasManualInteractEvent;
}
