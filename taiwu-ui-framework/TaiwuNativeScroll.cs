using FrameWork.UISystem.UIElements;
using Game.Views.MapBlockCharList;
using UnityEngine;
using UnityEngine.UI;

namespace TaiwuUi;

/// <summary>Native world-map vertical scrolling behavior and measurements.</summary>
public sealed record TaiwuNativeScrollOptions
{
    public float ScrollSpeed { get; init; } = 2000f;
    public float ScrollbarWidth { get; init; } = 20f;
    public float ViewportRightInset { get; init; } = 8f;
    public float ScrollbarRightInset { get; init; } = 21f;
    public float ScrollbarVerticalInset { get; init; } = 2f;
}

/// <summary>
/// A native CScrollRect with the world-map scrollbar. Add children below
/// <see cref="Content"/> and let their layout determine the content height.
/// </summary>
public sealed class TaiwuNativeScrollView
{
    public GameObject Root { get; }
    public RectTransform Content { get; }

    internal TaiwuNativeScrollView(GameObject root, RectTransform content)
    {
        Root = root;
        Content = content;
    }
}

/// <summary>Creates native scroll regions without exposing prefab wiring to consumers.</summary>
public static class TaiwuNativeScroll
{
    public static TaiwuNativeScrollView CreateVertical(
        string name,
        TaiwuNativeScrollOptions? options = null)
    {
        options ??= new TaiwuNativeScrollOptions();
        var root = new GameObject(string.IsNullOrWhiteSpace(name) ? "TaiwuNativeScroll" : name,
            typeof(RectTransform), typeof(CanvasRenderer));
        root.SetActive(false);

        var viewportObject = new GameObject("Viewport", typeof(RectTransform),
            typeof(RectMask2D), typeof(CanvasRenderer), typeof(CEmptyGraphic));
        viewportObject.transform.SetParent(root.transform, false);
        var viewport = (RectTransform)viewportObject.transform;
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = Vector2.zero;
        viewport.offsetMax = new Vector2(-options.ViewportRightInset, 0f);
        viewportObject.GetComponent<CEmptyGraphic>().raycastTarget = true;

        var contentObject = new GameObject("Content", typeof(RectTransform));
        contentObject.transform.SetParent(viewport, false);
        var content = (RectTransform)contentObject.transform;
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        var fitter = contentObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Scrollbar scrollbar = CreateScrollbar(root.transform, options);
        var scroll = root.AddComponent<CScrollRect>();
        scroll.Viewport = viewport;
        scroll.Content = content;
        scroll.Direction = CScrollRect.ScrollDirection.Vertical;
        scroll.ScrollBar = scrollbar;
        scroll.ScrollSpeed = options.ScrollSpeed;
        scroll.DampedCoefficient = 3f;
        scroll.AdjustSpeed = 4000f;
        scroll.Movement = CScrollRect.MovementType.Elastic;
        scroll.CanScroll = true;

        root.SetActive(true);
        return new TaiwuNativeScrollView(root, content);
    }

    private static Scrollbar CreateScrollbar(Transform parent, TaiwuNativeScrollOptions options)
    {
        GameObject? source = Resources.FindObjectsOfTypeAll<MapBlockCharScroll>()
            .Select(item => item.transform.Find("VerticalScrollbar")?.gameObject)
            .FirstOrDefault(item => item != null);

        GameObject root;
        Scrollbar scrollbar;
        if (source != null)
        {
            root = UnityEngine.Object.Instantiate(source, parent, false);
            root.name = "VerticalScrollbar";
            scrollbar = root.GetComponent<Scrollbar>();
        }
        else
        {
            root = new GameObject("VerticalScrollbar", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(CImage), typeof(Scrollbar));
            root.transform.SetParent(parent, false);
            var handleObject = new GameObject("HandleRect", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(CImage));
            handleObject.transform.SetParent(root.transform, false);
            var handle = (RectTransform)handleObject.transform;
            handle.anchorMin = Vector2.zero;
            handle.anchorMax = Vector2.one;
            handle.offsetMin = new Vector2(0f, 3f);
            handle.offsetMax = new Vector2(0f, -3f);
            CImage trackImage = root.GetComponent<CImage>();
            CImage handleImage = handleObject.GetComponent<CImage>();
            ApplyFallbackSprites(trackImage, handleImage);
            scrollbar = root.GetComponent<Scrollbar>();
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
        }

        scrollbar.onValueChanged.RemoveAllListeners();
        scrollbar.direction = Scrollbar.Direction.TopToBottom;
        var rect = (RectTransform)root.transform;
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(
            options.ScrollbarWidth, -options.ScrollbarVerticalInset * 2f);
        rect.anchoredPosition = new Vector2(-options.ScrollbarRightInset, 0f);
        root.SetActive(true);
        return scrollbar;
    }

    private static void ApplyFallbackSprites(CImage track, CImage handle)
    {
        Sprite[] sprites = Resources.FindObjectsOfTypeAll<Sprite>();
        track.sprite = sprites.FirstOrDefault(item => item.name == "ui9_btn_scroll_base_1");
        handle.sprite = sprites.FirstOrDefault(item => item.name == "ui9_btn_scroll_base_0");
        track.type = Image.Type.Sliced;
        handle.type = Image.Type.Sliced;
        handle.raycastTarget = false;
    }
}
