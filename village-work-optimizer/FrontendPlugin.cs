using HarmonyLib;
using TaiwuModdingLib.Core.Plugin;
using UnityEngine;

namespace VillageWorkOptimizer.Frontend;

[PluginConfig("VillageWorkOptimizer.Frontend", "SMD", "0.1.2")]
public sealed class FrontendPlugin : TaiwuRemakePlugin
{
    private Harmony? _harmony;
    private GameObject? _hotkeyHost;

    internal static string ModId { get; private set; } = string.Empty;

    public override void Initialize()
    {
        ModId = ModIdStr;
        _harmony = new Harmony(GetGuid());
        _harmony.PatchAll(typeof(FrontendPlugin).Assembly);
        _hotkeyHost = new GameObject("VillageWorkOptimizer.Hotkey");
        UnityEngine.Object.DontDestroyOnLoad(_hotkeyHost);
        _hotkeyHost.AddComponent<NativeUiHotkey>();
    }

    public override void Dispose()
    {
        _harmony?.UnpatchSelf();
        _harmony = null;
        if (_hotkeyHost != null)
            UnityEngine.Object.Destroy(_hotkeyHost);
        _hotkeyHost = null;
    }
}
