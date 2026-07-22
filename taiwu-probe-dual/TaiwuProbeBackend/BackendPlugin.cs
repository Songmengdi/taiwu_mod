using HarmonyLib;
using TaiwuModdingLib.Core.Plugin;

namespace TaiwuProbeBackend;

[PluginConfig("TaiwuProbe.Backend", "Magian.SMD", "0.3.0")]
public sealed class BackendPlugin : TaiwuRemakePlugin
{
    private Harmony? _harmony;
    private BackendHttpBridge? _bridge;

    internal static string ModId { get; private set; } = string.Empty;
    internal static bool UnsafeCSharpEnabled { get; private set; } = true;

    public override void Initialize()
    {
        ModId = ModIdStr;
        BackendModMethods.Register(ModIdStr);
        _harmony = new Harmony(GetGuid());
        _harmony.PatchAll(typeof(BackendPlugin).Assembly);
        _bridge = new BackendHttpBridge("http://localhost:13132/");
        _bridge.Start();
        GameData.Utilities.AdaptableLog.Info("[TaiwuProbeBackend] 后端调试工具已注册。");
    }

    public override void Dispose()
    {
        _bridge?.Dispose();
        _bridge = null;
        _harmony?.UnpatchSelf();
        _harmony = null;
        BackendMainThreadRunner.CancelPending("后端插件正在卸载");
        ModId = string.Empty;
    }

    public override void OnModSettingUpdate()
    {
        bool enabled = true;
        _ = GameData.Domains.DomainManager.Mod.GetSetting(ModIdStr, "EnableUnsafeCSharp", ref enabled);
        UnsafeCSharpEnabled = enabled;
    }
}
