using HarmonyLib;
using System.Reflection;
using TaiwuModdingLib.Core.Plugin;

namespace LiverFriendlyInteractions.Frontend;

[PluginConfig("LiverFriendlyInteractions.Frontend", "SMD", "0.8.4")]
public sealed class FrontendPlugin : TaiwuRemakePlugin
{
    public override void Initialize()
    {
        FrontendRuntime.Install(GetGuid());
    }

    public override void Dispose()
    {
        FrontendRuntime.Uninstall();
    }
}

public static class FrontendRuntime
{
    private const string DefaultHarmonyId = "SMD.LiverFriendlyInteractions.Frontend";
    private static Harmony? _harmony;

    public static string Install() => Install(DefaultHarmonyId);

    internal static string Install(string harmonyId)
    {
        if (_harmony != null)
        {
            return "护肝交互前端补丁已经安装。";
        }

        RemoveInstalledPatches();
        _harmony = new Harmony(harmonyId);
        _harmony.PatchAll(typeof(FrontendPlugin).Assembly);
        return "护肝交互前端补丁安装成功。";
    }

    public static string Uninstall()
    {
        RemoveInstalledPatches();
        _harmony = null;
        return "护肝交互前端补丁已卸载。";
    }

    private static void RemoveInstalledPatches()
    {
        string? pluginNamespace = typeof(FrontendPlugin).Namespace;
        foreach (MethodBase original in Harmony.GetAllPatchedMethods().ToArray())
        {
            Patches? patchInfo = Harmony.GetPatchInfo(original);
            if (patchInfo == null)
            {
                continue;
            }

            MethodInfo[] patchMethods = patchInfo.Prefixes
                .Concat(patchInfo.Postfixes)
                .Concat(patchInfo.Transpilers)
                .Concat(patchInfo.Finalizers)
                .Select(patch => patch.PatchMethod)
                .Where(method => method?.DeclaringType?.Namespace == pluginNamespace)
                .Distinct()
                .ToArray();

            foreach (MethodInfo patchMethod in patchMethods)
            {
                new Harmony(DefaultHarmonyId).Unpatch(original, patchMethod);
            }
        }
    }
}
