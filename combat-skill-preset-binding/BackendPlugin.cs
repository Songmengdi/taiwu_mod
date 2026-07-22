using HarmonyLib;
using TaiwuModdingLib.Core.Plugin;

namespace CombatSkillPresetBinding.Backend;

[PluginConfig("CombatSkillPresetBinding.Backend", "local", "0.2.2")]
public sealed class BackendPlugin : TaiwuRemakePlugin
{
    private Harmony? _harmony;

    internal static bool Enabled { get; private set; } = true;

    internal static bool DetailedLogging { get; private set; }

    public override void Initialize()
    {
        BindingStore.SetModId(ModIdStr);
        ReloadSettings();

        _harmony = new Harmony(GetGuid());
        _harmony.PatchAll(typeof(BackendPlugin).Assembly);
        ModLog.Info("后端插件已加载。切换运功预设时将同步绑定的功法突破预设。");
    }

    public override void OnLoadedArchiveData()
    {
        BindingStore.Load();
    }

    public override void OnEnterNewWorld()
    {
        BindingStore.ResetForNewWorld();
    }

    public override void OnModSettingUpdate()
    {
        ReloadSettings();
    }

    public override void Dispose()
    {
        _harmony?.UnpatchSelf();
        _harmony = null;
        BindingStore.ResetRuntimeState();
        ModLog.Info("后端插件已卸载。");
    }

    private void ReloadSettings()
    {
        bool enabled = true;
        bool detailedLogging = false;
        _ = GameData.Domains.DomainManager.Mod.GetSetting(ModIdStr, "EnableBinding", ref enabled);
        _ = GameData.Domains.DomainManager.Mod.GetSetting(ModIdStr, "DetailedLogging", ref detailedLogging);
        Enabled = enabled;
        DetailedLogging = detailedLogging;
    }
}
