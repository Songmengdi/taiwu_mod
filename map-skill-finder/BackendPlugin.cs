using TaiwuModdingLib.Core.Plugin;

namespace MapSkillFinder.Backend;

[PluginConfig("MapSkillFinder.Backend", "SMD", "1.0.2")]
public sealed class BackendPlugin : TaiwuRemakePlugin
{
    public override void Initialize()
    {
        FinderModMethods.Register(ModIdStr);
    }

    public override void Dispose()
    {
    }
}
