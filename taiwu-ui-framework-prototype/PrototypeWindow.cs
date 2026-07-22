using FrameWork.UISystem.Components;
using FrameWork.UISystem.UIElements;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TaiwuUiFrameworkPrototype;

internal sealed class PrototypeWindow : IDisposable
{
    private static readonly AccessTools.FieldRef<UIElement, string> ElementPath =
        AccessTools.FieldRefAccess<UIElement, string>("_path");

    private readonly PrototypeHost _host;
    private readonly PrototypeTrace _trace;
    private UIElement? _element;
    private PrototypeView? _view;

    internal PrototypeWindow(PrototypeHost host, int generation)
    {
        _host = host;
        _trace = new PrototypeTrace(generation);
        Create();
    }

    private void Create()
    {
        var root = new GameObject(
            "TaiwuUiFrameworkPrototype",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CImage));

        PrototypeView view = root.AddComponent<PrototypeView>();
        var element = new UIElement { UiBase = view };
        ElementPath(element) = "Mod/TaiwuUiFrameworkPrototype";

        view.Element = element;
        view.UiType = UILayer.LayerPopUp;
        view.UiFlags = UIFlag.FullCover;
        view.OpenCloseAudio = UIBase.UIOpenCloseAudioType.None;

        _element = element;
        _view = view;

        UIManager.Instance.PlaceUI(view);
        ConfigureCanvasAndRect(root);
        view.Build(
            _trace,
            Close,
            ToggleFullCover,
            _host.DestroyAndRecreate);
        SetLayerRecursively(root, root.transform.parent.gameObject.layer);
    }

    private static void ConfigureCanvasAndRect(GameObject root)
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
            canvas.sortingOrder = layerCanvas.sortingOrder;
        }

        ConchShipGraphicRaycaster raycaster = root.GetComponent<ConchShipGraphicRaycaster>();
        raycaster.TargetCamera = UIManager.Instance.UiCamera;
    }

    internal void Toggle()
    {
        if (_element == null)
            return;
        if (_element.IsShowing)
            Close();
        else
            Show();
    }

    internal void Show()
    {
        if (_element != null)
            UIManager.Instance.ShowUI(_element);
    }

    private void Close()
    {
        if (_element != null)
            UIManager.Instance.HideUI(_element);
    }

    private void ToggleFullCover()
    {
        if (_view == null || _element == null)
            return;

        // UIVisableHandler does not support mutating UiFlags in-place. Remove the
        // element through the normal hide event, change the flags, then show it again.
        bool reopen = _element.IsShowing;
        if (reopen)
            UIManager.Instance.HideUI(_element);
        _view.UiFlags = _view.UiFlags.HasFlag(UIFlag.FullCover)
            ? UIFlag.IncludeCoverCheck
            : UIFlag.FullCover;
        _trace.Apply(PrototypeEvent.CoverModeChanged);
        if (reopen)
            UIManager.Instance.ShowUI(_element);
        else
            _view.Refresh();
    }

    internal void Tick() => _view?.Refresh();

    public void Dispose()
    {
        if (_view != null)
        {
            if (UIManager.Instance != null)
                UIManager.DestroyUiBase(_view);
            else
                UnityEngine.Object.Destroy(_view.gameObject);
        }
        _view = null;
        _element = null;
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        root.layer = layer;
        for (int i = 0; i < root.transform.childCount; i++)
            SetLayerRecursively(root.transform.GetChild(i).gameObject, layer);
    }
}

internal sealed class PrototypeView : UIBase
{
    private PrototypeTrace? _trace;
    private TextMeshProUGUI? _status;
    private TMP_FontAsset? _font;

    internal void Build(
        PrototypeTrace trace,
        Action close,
        Action toggleFullCover,
        Action destroyAndRecreate)
    {
        _trace = trace;
        _font = UnityEngine.Object.FindObjectsOfType<TextMeshProUGUI>()
            .FirstOrDefault(text => text.font != null)?.font;

        CImage blocker = GetComponent<CImage>();
        blocker.color = new Color(0f, 0f, 0f, 0.58f);
        blocker.raycastTarget = true;

        RectTransform panel = CreateRect(
            "Panel",
            transform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(1040f, 660f));
        CImage panelImage = panel.gameObject.AddComponent<CImage>();
        panelImage.color = new Color(0.055f, 0.105f, 0.11f, 0.99f);

        TextMeshProUGUI title = CreateText(
            "Title",
            panel,
            "太吾 UI 框架 · 原生生命周期原型",
            32f,
            TextAlignmentOptions.MidlineLeft);
        SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(34f, -82f), new Vector2(-150f, -18f));

        CreateButton("Close", panel, "关闭", close,
            new Vector2(1f, 1f), new Vector2(-82f, -49f), new Vector2(112f, 50f));

        _status = CreateText(
            "State",
            panel,
            string.Empty,
            21f,
            TextAlignmentOptions.TopLeft);
        _status.enableWordWrapping = false;
        SetRect(_status.rectTransform, Vector2.zero, Vector2.one,
            new Vector2(42f, 108f), new Vector2(-42f, -104f));

        CreateButton("ToggleCover", panel, "切换 FullCover", toggleFullCover,
            new Vector2(0.5f, 0f), new Vector2(-210f, 50f), new Vector2(250f, 56f));
        CreateButton("Recreate", panel, "销毁并重建", destroyAndRecreate,
            new Vector2(0.5f, 0f), new Vector2(90f, 50f), new Vector2(250f, 56f));

        Refresh();
    }

    public override void OnInit(FrameWork.ArgumentBox argsBox)
    {
        _trace?.Apply(PrototypeEvent.Init);
        Refresh();
    }

    public override void OnReset()
    {
        base.OnReset();
        _trace?.Apply(PrototypeEvent.Reset);
        Refresh();
    }

    public override void NotifyUIShow()
    {
        _trace?.Apply(PrototypeEvent.Show);
        base.NotifyUIShow();
        Refresh();
    }

    public override void NotifyUIShowFinish()
    {
        _trace?.Apply(PrototypeEvent.ShowFinished);
        base.NotifyUIShowFinish();
        Refresh();
    }

    public override void NotifyUIHideStart()
    {
        _trace?.Apply(PrototypeEvent.HideStarted);
        base.NotifyUIHideStart();
    }

    public override void NotifyUIHide()
    {
        _trace?.Apply(PrototypeEvent.Hide);
        base.NotifyUIHide();
    }

    internal void Refresh()
    {
        if (_status == null || _trace == null || Element == null)
            return;

        string state = Enum.GetValues(typeof(EUiElementState))
            .Cast<EUiElementState>()
            .FirstOrDefault(Element.IsInState)
            .ToString();
        bool fullCover = UiFlags.HasFlag(UIFlag.FullCover);
        bool? bottomCovered = UIElement.Bottom.UiBase?
            .GetComponent<UIViewCoveredBehaviour>()?.IsCovered;

        _status.text =
            $"<b>Generation</b>         {_trace.Generation}\n" +
            $"<b>State</b>              {state}\n" +
            $"<b>Ready / Showing</b>    {Element.Ready} / {Element.IsShowing}\n" +
            $"<b>Root active</b>        {gameObject.activeInHierarchy}\n" +
            $"<b>Layer / sortOrder</b>  {UiType} / {GetComponent<Canvas>().sortingOrder}\n" +
            $"<b>Cover mode</b>         {(fullCover ? "FullCover" : "IncludeCoverCheck")}\n" +
            $"<b>ViewBottom covered</b> {bottomCovered?.ToString() ?? "n/a"}\n" +
            $"<b>Resolution</b>         {Screen.width} x {Screen.height}\n\n" +
            $"<b>Lifecycle counters</b>\n" +
            $"Init={_trace.InitCount}  Reset={_trace.ResetCount}  " +
            $"Show={_trace.ShowCount}  ShowFinished={_trace.ShowFinishedCount}\n" +
            $"HideStarted={_trace.HideStartedCount}  Hide={_trace.HideCount}  " +
            $"CoverChanges={_trace.CoverModeChangeCount}\n" +
            $"Last event={_trace.LastEvent}\n\n" +
            "F9 或 ESC：关闭；再次 F9：从缓存重开。";
    }

    private TextMeshProUGUI CreateText(
        string name,
        Transform parent,
        string value,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.font = _font;
        text.text = value;
        text.fontSize = fontSize;
        text.color = new Color(0.88f, 0.84f, 0.72f, 1f);
        text.alignment = alignment;
        text.raycastTarget = false;
        text.richText = true;
        return text;
    }

    private void CreateButton(
        string name,
        Transform parent,
        string label,
        Action action,
        Vector2 anchor,
        Vector2 position,
        Vector2 size)
    {
        RectTransform rect = CreateRect(name, parent, anchor, anchor, size);
        rect.anchoredPosition = position;
        CImage image = rect.gameObject.AddComponent<CImage>();
        image.color = new Color(0.16f, 0.29f, 0.29f, 1f);
        CButton button = rect.gameObject.AddComponent<CButton>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => action());

        TextMeshProUGUI text = CreateText("Label", rect, label, 21f, TextAlignmentOptions.Center);
        SetRect(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    private static RectTransform CreateRect(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;
        return rect;
    }

    private static void SetRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
