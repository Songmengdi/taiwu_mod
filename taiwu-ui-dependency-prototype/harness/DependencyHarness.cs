using System.Reflection;
using TaiwuModdingLib.Core.Plugin;

namespace TaiwuUiDependencyPrototype.Harness;

public sealed class DependencyHarness
{
    public static string Snapshot { get; private set; } = "not run";

    public DependencyHarness() => Run();

    private static void Run()
    {
        string root = @"E:\02_workspace\taiwu_mod\taiwu-ui-dependency-prototype";
        string providerDir = Path.Combine(root, "provider", "mod", "Plugins");
        string consumerDir = Path.Combine(root, "consumer", "mod", "Plugins");

        Type helperType = typeof(TaiwuRemakePlugin).Assembly.GetType(
            "TaiwuModdingLib.Core.Plugin.PluginHelper", throwOnError: true)!;
        MethodInfo loadPlugin = helperType.GetMethod(
            "LoadPlugin", BindingFlags.Public | BindingFlags.Static)!;

        string consumerFirstResult;
        try
        {
            loadPlugin.Invoke(null, new object[]
            {
                consumerDir,
                "TaiwuUi.DependencyPrototype.Consumer.dll",
                "990058002"
            });
            consumerFirstResult = "UNEXPECTED_SUCCESS";
        }
        catch (Exception exception)
        {
            consumerFirstResult = RootCause(exception);
        }

        TaiwuRemakePlugin provider = (TaiwuRemakePlugin)loadPlugin.Invoke(null, new object[]
        {
            providerDir,
            "TaiwuUi.DependencyPrototype.Provider.dll",
            "990058001"
        })!;
        TaiwuRemakePlugin consumer = (TaiwuRemakePlugin)loadPlugin.Invoke(null, new object[]
        {
            consumerDir,
            "TaiwuUi.DependencyPrototype.Consumer.dll",
            "990058002"
        })!;

        Type stateType = consumer.GetType().Assembly.GetType(
            "TaiwuUiDependencyPrototype.Consumer.DependencyProbeState", throwOnError: true)!;
        string consumerSnapshot = (string)stateType.GetProperty(
            "Snapshot", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;

        Snapshot =
            "ConsumerBeforeProvider=" + consumerFirstResult + "\n\n" +
            "ProviderPlugin=" + provider.GetType().AssemblyQualifiedName + "\n" +
            "ConsumerPlugin=" + consumer.GetType().AssemblyQualifiedName + "\n\n" +
            consumerSnapshot;
    }

    private static string RootCause(Exception exception)
    {
        Exception current = exception;
        while (current.InnerException != null)
            current = current.InnerException;
        return current.GetType().FullName + ": " + current.Message;
    }

    public override string ToString() => Snapshot;
}

