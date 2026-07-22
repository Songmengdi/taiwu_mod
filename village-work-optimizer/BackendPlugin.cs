using TaiwuModdingLib.Core.Plugin;

namespace VillageWorkOptimizer.Backend;

[PluginConfig("VillageWorkOptimizer.Backend", "SMD", "0.1.2")]
public sealed class BackendPlugin : TaiwuRemakePlugin
{
    public override void Initialize()
    {
        OptimizerModMethods.Register(ModIdStr);
    }

    public override void Dispose()
    {
    }
}
