using HarmonyLib;

namespace LiverFriendlyInteractions.Backend;

public static class BackendHotloadEntrypoint
{
    private const string HarmonyId =
        "LiverFriendlyInteractions.Backend.Hotload.GongshufangMerchantArrival";

    public static string Install()
    {
        var harmony = new Harmony(HarmonyId);
        harmony.UnpatchSelf();
        harmony.CreateClassProcessor(typeof(LargeMarketMerchantArrivalPatch)).Patch();
        return "Installed Gongshufang merchant-arrival suppression.";
    }
}
