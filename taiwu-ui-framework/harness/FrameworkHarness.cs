using System.Reflection;
using TaiwuModdingLib.Core.Plugin;

namespace TaiwuUi.FrameworkHarness;

public sealed class FrameworkHarness
{
    public static string Snapshot { get; private set; } = "not run";

    public FrameworkHarness()
    {
        try
        {
            Run();
        }
        catch (Exception exception)
        {
            Snapshot = RootCause(exception).ToString();
        }
    }

    private static void Run()
    {
        string root = @"E:\02_workspace\taiwu_mod\taiwu-ui-framework\dev\hot-0.2c";
        Type helperType = typeof(TaiwuRemakePlugin).Assembly.GetType(
            "TaiwuModdingLib.Core.Plugin.PluginHelper", throwOnError: true)!;
        MethodInfo loadPlugin = helperType.GetMethod(
            "LoadPlugin", BindingFlags.Public | BindingFlags.Static)!;

        TaiwuRemakePlugin provider = Load(
            loadPlugin, Path.Combine(root, "provider"), "TaiwuUi.Core.Dev02c.dll", "990058106");
        TaiwuRemakePlugin sample = Load(
            loadPlugin, Path.Combine(root, "consumer"), "TaiwuUi.Sample.Dev02c.dll", "990058107");

        Type control = sample.GetType().Assembly.GetType("TaiwuUi.Sample.SampleControl", throwOnError: true)!;
        control.GetMethod("Toggle", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null);
        string sampleSnapshot = (string)control.GetProperty(
            "Snapshot", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;

        Assembly[] coreCopies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => assembly.GetName().Name?.StartsWith("TaiwuUi.Core", StringComparison.Ordinal) == true)
            .ToArray();
        Snapshot =
            $"Provider={provider.GetType().AssemblyQualifiedName}\n" +
            $"Sample={sample.GetType().AssemblyQualifiedName}\n" +
            $"CoreCopiesInHotReloadProcess={coreCopies.Length}; " +
            $"CoreVersions={string.Join(",", coreCopies.Select(item => item.GetName().Version))}\n" +
            sampleSnapshot;
    }

    private static TaiwuRemakePlugin Load(
        MethodInfo loadPlugin, string directory, string fileName, string modId) =>
        (TaiwuRemakePlugin)loadPlugin.Invoke(null, new object[] { directory, fileName, modId })!;

    private static Exception RootCause(Exception exception)
    {
        Exception current = exception;
        while (current.InnerException != null)
            current = current.InnerException;
        return current;
    }

    public override string ToString() => Snapshot;
}
