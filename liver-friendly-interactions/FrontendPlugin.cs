using HarmonyLib;
using System.Reflection;
using TaiwuModdingLib.Core.Plugin;

namespace LiverFriendlyInteractions.Frontend;

[PluginConfig("LiverFriendlyInteractions.Frontend", "SMD", "0.9.0")]
public sealed class FrontendPlugin : TaiwuRemakePlugin
{
    internal static string ModId { get; private set; } = string.Empty;

    public override void Initialize()
    {
        ModId = ModIdStr;
        FrontendRuntime.Install(GetGuid());
        InteractionHubRuntime.Install();
    }

    public override void Dispose()
    {
        InteractionHubRuntime.Uninstall();
        FrontendRuntime.Uninstall();
    }

    internal static void InstallInteractionHub(string modId)
    {
        ModId = modId;
        InteractionHubRuntime.Install();
    }
}

public static class InteractionHubHotloadEntrypoint
{
    public static string Install(string modId)
    {
        UnityEngine.GameObject? existing = UnityEngine.GameObject.Find("LiverFriendlyInteractions_InteractionHub");
        if (existing != null) UnityEngine.Object.Destroy(existing);
        FrontendPlugin.InstallInteractionHub(modId);
        return "Installed interaction hub frontend for " + modId + ".";
    }

    public static string Open()
    {
        InteractionHubRuntime.Open();
        return "Opened interaction hub.";
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
