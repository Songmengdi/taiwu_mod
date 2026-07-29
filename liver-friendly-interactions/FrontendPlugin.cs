using HarmonyLib;
using TaiwuModdingLib.Core.Plugin;

namespace LiverFriendlyInteractions.Frontend;

[PluginConfig("LiverFriendlyInteractions.Frontend", "SMD", "0.8.1")]
public sealed class FrontendPlugin : TaiwuRemakePlugin
{
    public override void Initialize()
    {
        FrontendRuntime.Install(GetGuid());
    }

    public override void Dispose()
    {
        FrontendRuntime.Uninstall();
    }
}

public static class FrontendRuntime
{
    private const string DefaultHarmonyId = "SMD.LiverFriendlyInteractions.Frontend";
    private static Harmony? _harmony;

    public static string Install() => Install(DefaultHarmonyId);

    internal static string Install(string harmonyId)
    {
        if (_harmony != null)
        {
            return "护肝交互前端补丁已经安装。";
        }

        _harmony = new Harmony(harmonyId);
        _harmony.PatchAll(typeof(FrontendPlugin).Assembly);
        return "护肝交互前端补丁安装成功。";
    }

    public static string Uninstall()
    {
        _harmony?.UnpatchSelf();
        _harmony = null;
        return "护肝交互前端补丁已卸载。";
    }
}
