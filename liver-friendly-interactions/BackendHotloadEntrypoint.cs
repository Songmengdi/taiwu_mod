using HarmonyLib;

namespace LiverFriendlyInteractions.Backend;

public static class BackendHotloadEntrypoint
{
    private const string HarmonyId =
        "LiverFriendlyInteractions.Backend.Hotload.ForcedAdventureInteraction";

    public static string Install()
    {
        var harmony = new Harmony(HarmonyId);
        harmony.UnpatchSelf();
        harmony.CreateClassProcessor(typeof(AdventureForcedInteractionPatch)).Patch();
        return "Installed forced adventure-interaction suppression.";
    }
}
