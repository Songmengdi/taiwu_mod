namespace LiverFriendlyInteractions.Backend;

internal static class LargeMarketMerchantArrivalPolicy
{
    // Runtime CoreId observed from the game's current large Gongshufang adventure.
    internal const int GongshufangLargeAdventureCoreId = 136028250;

    internal static bool ShouldSuppressForcedInteraction(
        int adventureCoreId,
        int characterId,
        bool isTaiwuArrivedElement) =>
        adventureCoreId == GongshufangLargeAdventureCoreId &&
        characterId >= 0 &&
        isTaiwuArrivedElement;
}
