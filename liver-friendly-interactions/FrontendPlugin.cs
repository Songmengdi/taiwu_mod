using HarmonyLib;
using System.Reflection;
using GameData.Domains.Mod;
using TaiwuModdingLib.Core.Plugin;

namespace LiverFriendlyInteractions.Frontend;

[PluginConfig("LiverFriendlyInteractions.Frontend", "SMD", "0.9.0")]
public sealed class FrontendPlugin : TaiwuRemakePlugin
{
    internal static string ModId { get; private set; } = string.Empty;
    internal static bool OverrideWorldMapCharacterClicks { get; private set; } = true;

    public override void Initialize()
    {
        ModId = ModIdStr;
        ReloadSettings();
        FrontendRuntime.Install(GetGuid());
        InteractionHubRuntime.Install();
    }

    public override void OnModSettingUpdate()
    {
        ReloadSettings();
    }

    public override void Dispose()
    {
        InteractionHubRuntime.Uninstall();
        FrontendRuntime.Uninstall();
    }

    internal static void InstallInteractionHub(string modId)
    {
        ModId = modId;
        ReloadSettings();
        InteractionHubRuntime.Install();
    }

    private static void ReloadSettings()
    {
        bool enabled = true;
        try
        {
            if (!ModManager.GetSetting(ModId, "OverrideWorldMapCharacterClicks", ref enabled))
                enabled = true;
        }
        catch
        {
            enabled = true;
        }
        OverrideWorldMapCharacterClicks = enabled;
    }
}

public static class InteractionHubHotloadEntrypoint
{
    public static string Install(string modId)
    {
        UnityEngine.GameObject? existing = UnityEngine.GameObject.Find("LiverFriendlyInteractions_InteractionHub");
        if (existing != null) UnityEngine.Object.Destroy(existing);
        FrontendPlugin.InstallInteractionHub(modId);
        WorldMapCharacterOverrideHotloadRuntime.Install();
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
