using FrameWork.UISystem.UIElements;
using HarmonyLib;
using UnityEngine;
using UnityEngine.U2D;

namespace TaiwuUi;

internal sealed class FrameworkWindow : ITaiwuWindow
{
    static FrameworkWindow() => FrameworkLifetimePatches.EnsurePatched();

    private static readonly AccessTools.FieldRef<UIElement, string> ElementPath =
        AccessTools.FieldRefAccess<UIElement, string>("_path");

    private WindowDefinition _definition;
    private readonly Action _onDisposed;
    private UIElement? _element;
    private FrameworkView? _view;
    private UiWindow? _source;
    private bool _disposed;

    public string Key => _definition.Key;
    public bool IsShowing => !_disposed && _element?.IsShowing == true;

    internal FrameworkWindow(UiRenderPlan plan, Action onDisposed)
    {
        _definition = plan.Definition;
        _source = plan.Source;
        _onDisposed = onDisposed;
        CreateNativeElement();
    }

    private void CreateNativeElement()
    {
        string objectName = "TaiwuUi_" + Sanitize(_definition.OwnerId) + "_" + Sanitize(_definition.WindowId);
        var root = new GameObject(objectName, typeof(RectTransform), typeof(Canvas), typeof(CImage));
        FrameworkView view = root.AddComponent<FrameworkView>();
        view.KeepAlive = _definition.Lifetime == TaiwuWindowLifetime.KeepAlive;
        // Native UIBase instances receive this array during the game's resource
        // preparation pipeline. Framework windows are mounted directly, so keep
        // the same lifecycle invariant ourselves. AtlasInfo.DoUnloadPackers
        // enumerates every UIBase.RelativeAtlases when returning to the main menu
        // and does not accept null.
        view.RelativeAtlases = Array.Empty<SpriteAtlas>();
        var element = new UIElement { UiBase = view };
        ElementPath(element) = "Mod/TaiwuUi/" + objectName;

        view.Element = element;
        view.UiType = MapLayer(_definition.Layer);
        view.UiFlags = _definition.Cover == TaiwuWindowCover.Full ||
            _definition.Presentation == TaiwuWindowPresentation.Encyclopedia
            ? UIFlag.FullCover
            : UIFlag.IncludeCoverCheck;
        view.OpenCloseAudio = UIBase.UIOpenCloseAudioType.None;

        _view = view;
        _element = element;
        UIManager.Instance.PlaceUI(view);
        ConfigureRoot(root);
        view.Build(_definition, Hide);
        SetLayerRecursively(root, root.transform.parent.gameObject.layer);
    }

    private static UILayer MapLayer(TaiwuWindowLayer layer) => layer switch
    {
        TaiwuWindowLayer.Main => UILayer.LayerMain,
        TaiwuWindowLayer.Part => UILayer.LayerPart,
        TaiwuWindowLayer.Popup => UILayer.LayerPopUp,
        TaiwuWindowLayer.Tips => UILayer.LayerTips,
        TaiwuWindowLayer.VeryTop => UILayer.LayerVeryTop,
        _ => UILayer.LayerPopUp,
    };

    private static void ConfigureRoot(GameObject root)
    {
        RectTransform rect = (RectTransform)root.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;

        Canvas canvas = root.GetComponent<Canvas>();
        Canvas? layerCanvas = root.transform.parent.GetComponent<Canvas>();
        canvas.overrideSorting = true;
        if (layerCanvas != null)
        {
            canvas.sortingLayerID = layerCanvas.sortingLayerID;
            // An override-sorting canvas tied with the layer canvas can be drawn
            // behind native child canvases that use the same order (for example
            // the character menu).  Keep framework windows one order above their
            // owning layer so a newly opened popup is actually visible.
            canvas.sortingOrder = layerCanvas.sortingOrder + 1;
        }
        root.GetComponent<ConchShipGraphicRaycaster>().TargetCamera = UIManager.Instance.UiCamera;
    }

    public void Show()
    {
        ThrowIfDisposed();
        EnsureNativeElementAlive();
        if (_element != null && !_element.IsShowing)
        {
            UIManager.Instance.ShowUI(_element);
            // UIManager can normalize the canvas order while showing it. Apply
            // the framework window's override again on the following frame so
            // native windows already occupying the layer cannot cover it.
            if (_view != null)
                DeferredUiAction.Run(_view.gameObject, () => ConfigureRoot(_view.gameObject));
        }
    }

    public void Hide()
    {
        if (!_disposed && _element?.IsShowing == true)
            UIManager.Instance.HideUI(_element);
    }

    /// <summary>
    /// Closing a window drives the element's state machine into Sleep, where
    /// UIElementStateSleep.OnEnter destroys the UiBase GameObject and nulls
    /// UIElement.UiBase.  Native elements silently reload their prefab on the
    /// next Show, but framework windows have no prefab: the load fails (logged
    /// as a ResLoader error) and the element stays stuck in ResourcePrepare
    /// with IsShowing permanently true, so the window can never reopen.
    /// Detect the dead native pair and rebuild it before showing again.
    /// </summary>
    private void EnsureNativeElementAlive()
    {
        if (_element != null && _view != null && _element.UiBase != null)
            return;
        if (_element != null && UIManager.Instance != null)
            // Drops the stale element from UIManager tracking; a no-op when it
            // is not tracked, and safe while UiBase is null.
            UIManager.Instance.HideUI(_element);
        _view = null;
        _element = null;
        CreateNativeElement();
    }

    public void Toggle()
    {
        if (IsShowing) Hide(); else Show();
    }

    public void Render(UiWindow window) => RenderPlan(UiRenderPlanCompiler.Compile(window));

    internal void RenderPlan(UiRenderPlan plan)
    {
        ThrowIfDisposed();
        if (!string.Equals(plan.Definition.Key, Key, StringComparison.Ordinal))
            throw new ArgumentException("A mounted window cannot change its key.", nameof(plan));

        UiUpdatePreview? update = _source == null ? null : UiReconciler.Preview(_source, plan.Source);
        bool wasShowing = IsShowing;
        WindowDefinition oldDefinition = _definition;
        UiWindow? oldSource = _source;
        FrameworkView? oldView = _view;
        UIElement? oldElement = _element;
        UiRuntimeState? runtimeState = oldView?.CaptureRuntimeState();
        _definition = plan.Definition;
        _source = plan.Source;
        _view = null;
        _element = null;
        try
        {
            CreateNativeElement();
            if (wasShowing)
                Show();
            if (runtimeState != null && update != null)
                _view?.RestoreRuntimeState(runtimeState);
        }
        catch
        {
            if (_view != null)
                UIManager.DestroyUiBase(_view);
            _view = oldView;
            _element = oldElement;
            _definition = oldDefinition;
            _source = oldSource;
            throw;
        }

        if (oldView != null)
            UIManager.DestroyUiBase(oldView);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        if (_view != null)
        {
            if (UIManager.Instance != null)
                UIManager.DestroyUiBase(_view);
            else
                UnityEngine.Object.Destroy(_view.gameObject);
        }
        _view = null;
        _element = null;
        _onDisposed();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(Key);
    }

    private static string Sanitize(string value) =>
        new(value.Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray());

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        root.layer = layer;
        for (int i = 0; i < root.transform.childCount; i++)
            SetLayerRecursively(root.transform.GetChild(i).gameObject, layer);
    }
}

internal static class FrameworkLifetimePatches
{
    private static bool _patched;

    internal static void EnsurePatched()
    {
        if (_patched) return;
        _patched = true;
        new Harmony("magian.taiwu-ui-framework.lifecycle").Patch(
            AccessTools.Method(typeof(UIElement), nameof(UIElement.DestroyUIBase)),
            prefix: new HarmonyMethod(typeof(FrameworkLifetimePatches), nameof(BeforeDestroyUiBase)));
    }

    private static bool BeforeDestroyUiBase(UIElement __instance) =>
        __instance.UiBase is not FrameworkView { KeepAlive: true };
}
