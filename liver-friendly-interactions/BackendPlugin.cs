using HarmonyLib;
using TaiwuModdingLib.Core.Plugin;

namespace LiverFriendlyInteractions.Backend;

[PluginConfig("LiverFriendlyInteractions.Backend", "SMD", "0.9.0")]
public sealed class BackendPlugin : TaiwuRemakePlugin
{
    private Harmony? _harmony;

    public override void Initialize()
    {
        InteractionHubModMethods.Register(ModIdStr);
        _harmony = new Harmony(GetGuid());
        _harmony.PatchAll(typeof(BackendPlugin).Assembly);
    }

    public override void Dispose()
    {
        _harmony?.UnpatchSelf();
        _harmony = null;
    }
}
