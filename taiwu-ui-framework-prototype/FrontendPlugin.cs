using TaiwuModdingLib.Core.Plugin;

namespace TaiwuUiFrameworkPrototype;

[PluginConfig("TaiwuUiFrameworkPrototype.Frontend", "SMD", "0.0.1")]
public sealed class FrontendPlugin : TaiwuRemakePlugin
{
    public override void Initialize() => PrototypeBootstrap.Install();

    public override void Dispose() => PrototypeBootstrap.Uninstall();
}

// Public constructor allows the already-running game to load this prototype through
// Assembly.LoadFrom(...).CreateInstance(...) without installing or restarting first.
public sealed class PrototypeBootstrap
{
    internal const string HostName = "TaiwuUiFrameworkPrototype.Host";
    private static UnityEngine.GameObject? _host;

    public PrototypeBootstrap() => Install();

    public static void Install()
    {
        if (_host != null)
            return;

        UnityEngine.GameObject? existing = UnityEngine.GameObject.Find(HostName);
        if (existing != null)
        {
            _host = existing;
            return;
        }

        _host = new UnityEngine.GameObject(HostName);
        UnityEngine.Object.DontDestroyOnLoad(_host);
        _host.AddComponent<PrototypeHost>();
    }

    public static void Uninstall()
    {
        if (_host != null)
            UnityEngine.Object.Destroy(_host);
        _host = null;
    }

    public override string ToString() => _host != null ? "installed" : "not installed";
}

// Hot-load recovery hook for this throwaway prototype. It removes an older loaded
// generation and resets visibility state after an intentionally unsupported
// ForceUpdateElements experiment. Normal MOD loading never instantiates this type.
public sealed class PrototypeRecovery
{
    public PrototypeRecovery()
    {
        UnityEngine.GameObject? existing = UnityEngine.GameObject.Find(PrototypeBootstrap.HostName);
        if (existing != null)
            UnityEngine.Object.DestroyImmediate(existing);

        object handler = UIManager.Instance.UIVisableHandler;
        foreach (string fieldName in new[]
                 {
                     "_showingElements",
                     "_elementNodeLookup",
                     "_tempStack",
                     "_tempElementList",
                 })
        {
            object? collection = HarmonyLib.AccessTools.Field(handler.GetType(), fieldName)?.GetValue(handler);
            collection?.GetType().GetMethod("Clear")?.Invoke(collection, null);
        }

        System.Reflection.MethodInfo? addElement = HarmonyLib.AccessTools.Method(
            handler.GetType(), "AddElement");
        foreach (UIBase uiBase in UnityEngine.Resources.FindObjectsOfTypeAll<UIBase>())
        {
            if (uiBase.gameObject.activeInHierarchy &&
                uiBase.Element != null &&
                uiBase.UiFlags.HasFlag(UIFlag.IncludeCoverCheck))
            {
                addElement?.Invoke(handler, new object[] { uiBase.Element });
            }
        }

        System.Reflection.MethodInfo? onDisable = HarmonyLib.AccessTools.Method(
            typeof(FrameWork.UISystem.Components.UIViewCoveredBehaviour), "OnDisable");
        System.Reflection.MethodInfo? onEnable = HarmonyLib.AccessTools.Method(
            typeof(FrameWork.UISystem.Components.UIViewCoveredBehaviour), "OnEnable");
        foreach (FrameWork.UISystem.Components.UIViewCoveredBehaviour behaviour in
                 UnityEngine.Resources.FindObjectsOfTypeAll<FrameWork.UISystem.Components.UIViewCoveredBehaviour>())
        {
            onDisable?.Invoke(behaviour, null);
            if (behaviour.isActiveAndEnabled)
                onEnable?.Invoke(behaviour, null);
        }

        PrototypeBootstrap.Install();
    }

    public override string ToString() => "recovered and installed";
}

public sealed class PrototypeOpen
{
    public PrototypeOpen()
    {
        UnityEngine.GameObject.Find(PrototypeBootstrap.HostName)?
            .GetComponent<PrototypeHost>()?.Toggle();
    }

    public override string ToString() => "toggle requested";
}

public sealed class PrototypeRecheckCoverage
{
    public PrototypeRecheckCoverage()
    {
        System.Reflection.MethodInfo? recheck = HarmonyLib.AccessTools.Method(
            typeof(FrameWork.UISystem.Components.UIViewCoveredBehaviour), "OnCoverStateChanged");
        foreach (FrameWork.UISystem.Components.UIViewCoveredBehaviour behaviour in
                 UnityEngine.Resources.FindObjectsOfTypeAll<FrameWork.UISystem.Components.UIViewCoveredBehaviour>())
        {
            if (behaviour.isActiveAndEnabled)
                recheck?.Invoke(behaviour, null);
        }
    }

    public override string ToString() => "coverage rechecked";
}
