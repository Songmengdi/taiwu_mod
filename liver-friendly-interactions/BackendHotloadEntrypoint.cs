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

    public static string InstallInteractionHub(string modId)
    {
        InteractionHubModMethods.Register(modId);
        var harmony = new Harmony(HarmonyId + ".InteractionHub");
        harmony.UnpatchSelf();
        harmony.CreateClassProcessor(typeof(InteractionHubReturnPatch)).Patch();
        return "Installed interaction hub backend for " + modId + ".";
    }

    public static string InstallAutoMeet(string modId)
    {
        InteractionHubModMethods.RegisterMeet(modId);
        return "Installed interaction hub auto-meet backend for " + modId + ".";
    }
}
