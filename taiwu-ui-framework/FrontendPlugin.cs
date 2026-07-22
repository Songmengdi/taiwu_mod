using TaiwuModdingLib.Core.Plugin;

namespace TaiwuUi;

[PluginConfig("TaiwuUi.Core", "SMD", "2.1.2")]
public sealed class FrontendPlugin : TaiwuRemakePlugin
{
    public override void Initialize() => TaiwuUiRuntime.Initialize();

    public override void Dispose() => TaiwuUiRuntime.DisposeAll();
}
