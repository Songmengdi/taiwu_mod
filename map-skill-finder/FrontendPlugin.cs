using HarmonyLib;
using TaiwuModdingLib.Core.Plugin;

namespace MapSkillFinder.Frontend;

[PluginConfig("MapSkillFinder.Frontend", "SMD", "1.0.2")]
public sealed class FrontendPlugin : TaiwuRemakePlugin
{
    private Harmony? _harmony;

    internal static string ModId { get; private set; } = string.Empty;

    internal static void UseRuntimeModId(string modId) => ModId = modId;

    public override void Initialize()
    {
        ModId = ModIdStr;
        _harmony = new Harmony(GetGuid());
        _harmony.PatchAll(typeof(FrontendPlugin).Assembly);
        FinderCharacterJieqingSignPatch.Install(_harmony);
        TextStyleHelperRefreshGuardPatch.Install(_harmony);
    }

    public override void Dispose()
    {
        _harmony?.UnpatchSelf();
        _harmony = null;
    }
}
