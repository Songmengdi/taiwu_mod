using TaiwuModdingLib.Core.Plugin;
using TaiwuUiDependencyPrototype.Provider;

namespace TaiwuUiDependencyPrototype.Consumer;

public static class DependencyProbeState
{
    public static string Snapshot { get; internal set; } = "consumer not initialized";
}

[PluginConfig("TaiwuUi.DependencyPrototype.Consumer", "SMD", "0.0.1")]
public sealed class ConsumerPlugin : TaiwuRemakePlugin
{
    public override void Initialize()
    {
        try
        {
            ProviderReply reply = TaiwuUiProviderApi.Ping("TaiwuUi.DependencyPrototype.Consumer");
            DependencyProbeState.Snapshot =
                "ConsumerInitialized=True\n" +
                "ProviderCallSucceeded=True\n" +
                $"ProviderInitializedFirst={reply.PluginInitialized}\n" +
                $"ProviderApiMajor={TaiwuUiProviderApi.ApiMajor}\n" +
                $"ProviderAssembly={reply.AssemblyFullName}\n" +
                $"ProviderLocation={(string.IsNullOrEmpty(reply.AssemblyLocation) ? "<empty: loaded from bytes>" : reply.AssemblyLocation)}\n" +
                $"ProviderMVID={reply.ModuleVersionId}\n" +
                $"ProviderLoadedCopies={reply.LoadedCopyCount}";
        }
        catch (Exception exception)
        {
            DependencyProbeState.Snapshot =
                "ConsumerInitialized=True\n" +
                "ProviderCallSucceeded=False\n" +
                $"Error={exception.GetType().FullName}: {exception.Message}";
            throw;
        }
    }

    public override void Dispose() => DependencyProbeState.Snapshot = "consumer disposed";
}

