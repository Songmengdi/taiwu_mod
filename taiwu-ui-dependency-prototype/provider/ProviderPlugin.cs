using System.Reflection;
using TaiwuModdingLib.Core.Plugin;

namespace TaiwuUiDependencyPrototype.Provider;

public sealed class ProviderReply
{
    public string Consumer { get; }
    public string AssemblyFullName { get; }
    public string AssemblyLocation { get; }
    public string ModuleVersionId { get; }
    public bool PluginInitialized { get; }
    public int LoadedCopyCount { get; }

    public ProviderReply(
        string consumer,
        string assemblyFullName,
        string assemblyLocation,
        string moduleVersionId,
        bool pluginInitialized,
        int loadedCopyCount)
    {
        Consumer = consumer;
        AssemblyFullName = assemblyFullName;
        AssemblyLocation = assemblyLocation;
        ModuleVersionId = moduleVersionId;
        PluginInitialized = pluginInitialized;
        LoadedCopyCount = loadedCopyCount;
    }
}

public static class TaiwuUiProviderApi
{
    public const int ApiMajor = 1;

    public static ProviderReply Ping(string consumer)
    {
        Assembly assembly = typeof(TaiwuUiProviderApi).Assembly;
        return new ProviderReply(
            consumer,
            assembly.FullName ?? string.Empty,
            SafeLocation(assembly),
            assembly.ManifestModule.ModuleVersionId.ToString(),
            ProviderPlugin.Initialized,
            AppDomain.CurrentDomain.GetAssemblies().Count(candidate =>
                candidate.GetName().Name == assembly.GetName().Name));
    }

    private static string SafeLocation(Assembly assembly)
    {
        try { return assembly.Location; }
        catch { return "<unavailable>"; }
    }
}

[PluginConfig("TaiwuUi.DependencyPrototype.Provider", "SMD", "0.0.1")]
public sealed class ProviderPlugin : TaiwuRemakePlugin
{
    public static bool Initialized { get; private set; }
    public static string Snapshot { get; private set; } = "provider not initialized";

    public override void Initialize()
    {
        Initialized = true;
        ProviderReply reply = TaiwuUiProviderApi.Ping("provider-self-check");
        Snapshot =
            $"Initialized={Initialized}\n" +
            $"ApiMajor={TaiwuUiProviderApi.ApiMajor}\n" +
            $"Assembly={reply.AssemblyFullName}\n" +
            $"Location={(string.IsNullOrEmpty(reply.AssemblyLocation) ? "<empty: loaded from bytes>" : reply.AssemblyLocation)}\n" +
            $"MVID={reply.ModuleVersionId}\n" +
            $"LoadedCopies={reply.LoadedCopyCount}";
    }

    public override void Dispose()
    {
        Initialized = false;
        Snapshot = "provider disposed";
    }
}
