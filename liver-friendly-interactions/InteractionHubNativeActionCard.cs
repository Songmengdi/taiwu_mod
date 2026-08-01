using TMPro;
using FrameWork.UISystem.UIElements;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LiverFriendlyInteractions.Frontend;

internal static class InteractionHubNativeActionCard
{
    private static readonly Dictionary<string, Sprite?> SpriteCache = new(StringComparer.Ordinal);

    internal static GameObject Create(InteractionOptionView option, Action onClick, Action<string> hover)
    {
        var root = new GameObject("InteractionAction_" + Sanitize(option.PreferenceKey),
            typeof(RectTransform), typeof(CImage), typeof(CButton), typeof(HorizontalLayoutGroup));
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(300f, 112f);

        CImage background = root.GetComponent<CImage>();
        background.color = option.Available
            ? new Color(0.10f, 0.17f, 0.17f, 0.96f)
            : new Color(0.08f, 0.10f, 0.10f, 0.78f);
        CButton button = root.GetComponent<CButton>();
        button.targetGraphic = background;
        button.interactable = option.Available;
        if (option.Available) button.onClick.AddListener(() => onClick());

        var layout = root.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 10, 10);
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;

        var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CImage), typeof(LayoutElement));
        iconObject.transform.SetParent(root.transform, false);
        iconObject.GetComponent<RectTransform>().sizeDelta = new Vector2(76f, 76f);
        var iconLayout = iconObject.GetComponent<LayoutElement>();
        iconLayout.preferredWidth = 76f;
        iconLayout.preferredHeight = 76f;
        CImage frame = iconObject.GetComponent<CImage>();
        frame.color = option.Available
            ? new Color(0.62f, 0.45f, 0.22f, 1f)
            : new Color(0.32f, 0.33f, 0.30f, 1f);
        frame.raycastTarget = false;

        var innerObject = new GameObject("Inner", typeof(RectTransform), typeof(CImage));
        innerObject.transform.SetParent(iconObject.transform, false);
        var innerRect = (RectTransform)innerObject.transform;
        innerRect.anchorMin = Vector2.zero;
        innerRect.anchorMax = Vector2.one;
        innerRect.offsetMin = new Vector2(2f, 2f);
        innerRect.offsetMax = new Vector2(-2f, -2f);
        CImage inner = innerObject.GetComponent<CImage>();
        inner.color = new Color(0.035f, 0.11f, 0.12f, 1f);
        inner.raycastTarget = false;

        var artworkObject = new GameObject("Artwork", typeof(RectTransform), typeof(CImage));
        artworkObject.transform.SetParent(iconObject.transform, false);
        var artworkRect = (RectTransform)artworkObject.transform;
        artworkRect.anchorMin = Vector2.zero;
        artworkRect.anchorMax = Vector2.one;
        artworkRect.offsetMin = new Vector2(6f, 6f);
        artworkRect.offsetMax = new Vector2(-6f, -6f);
        CImage artwork = artworkObject.GetComponent<CImage>();
        artwork.sprite = LoadSprite(option.PreferenceKey);
        artwork.color = option.Available ? Color.white : new Color(0.45f, 0.48f, 0.48f, 1f);
        artwork.preserveAspect = true;
        artwork.raycastTarget = false;

        var textRoot = new GameObject("Text", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        textRoot.transform.SetParent(root.transform, false);
        var textLayout = textRoot.GetComponent<LayoutElement>();
        textLayout.preferredWidth = 188f;
        textLayout.preferredHeight = 84f;
        var vertical = textRoot.GetComponent<VerticalLayoutGroup>();
        vertical.childAlignment = TextAnchor.MiddleLeft;
        vertical.childControlWidth = true;
        vertical.childControlHeight = false;
        vertical.spacing = 2f;
        AddText(textRoot.transform, "Name", option.Name, 25f,
            option.Available ? new Color(0.91f, 0.90f, 0.80f) : new Color(0.48f, 0.49f, 0.47f), 42f);
        AddText(textRoot.transform, "Cost", CostText(option), 18f,
            option.Available ? new Color(0.55f, 0.72f, 0.72f) : new Color(0.38f, 0.40f, 0.40f), 32f);

        var hoverState = root.AddComponent<InteractionActionHover>();
        hoverState.Enter = () => hover(TooltipText(option));
        hoverState.Exit = () => hover(string.Empty);
        return root;
    }

    internal static void Release(GameObject root) => UnityEngine.Object.Destroy(root);

    private static void AddText(Transform parent, string name, string value, float size, Color color, float height)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        root.transform.SetParent(parent, false);
        var text = root.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        // 双成本（行动力 + 恩义）或长名称放不下时自动缩字号，避免被省略号截断
        text.enableAutoSizing = true;
        text.fontSizeMin = size * 0.6f;
        text.fontSizeMax = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        root.GetComponent<LayoutElement>().preferredHeight = height;
    }

    private static string CostText(InteractionOptionView option)
    {
        var parts = new List<string>();
        if (option.ActionPointCost > 0) parts.Add("行动力 " + option.ActionPointCost);
        if (option.SpiritualDebtCost > 0) parts.Add("恩义 " + option.SpiritualDebtCost);
        return parts.Count == 0 ? "直接交互" : string.Join("·", parts);
    }

    private static string TooltipText(InteractionOptionView option) => option.Available
        ? string.IsNullOrWhiteSpace(CostText(option)) ? option.Name : option.Name + "\n" + CostText(option)
        : option.Name + "\n" + (string.IsNullOrWhiteSpace(option.UnavailableReason)
            ? "当前条件不满足" : option.UnavailableReason);

    private static Sprite? LoadSprite(string key)
    {
        if (SpriteCache.TryGetValue(key, out Sprite? cached)) return cached;
        try
        {
            string iconDirectory = ResolveIconDirectory();
            string file = Path.Combine(iconDirectory, FileName(key) + ".png");
            if (!File.Exists(file))
                file = Path.Combine(iconDirectory, "builtin_special-interaction.png");
            if (!File.Exists(file)) return SpriteCache[key] = null;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(file), markNonReadable: true))
                return SpriteCache[key] = null;
            texture.name = "LFI_" + FileName(key);
            const float inset = 22f;
            return SpriteCache[key] = Sprite.Create(texture,
                new Rect(inset, inset, texture.width - inset * 2f, texture.height - inset * 2f),
                new Vector2(0.5f, 0.5f), 100f);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[护肝交互] 加载交互图标失败：" + exception.Message);
            return SpriteCache[key] = null;
        }
    }

    private static string ResolveIconDirectory()
    {
        string gameRoot = Path.GetDirectoryName(Application.dataPath) ?? string.Empty;
        string modDirectoryName = Path.GetFileName(FrontendPlugin.ModId);
        string installed = Path.Combine(gameRoot, "Mod", modDirectoryName,
            "Assets", "InteractionIcons");
        if (Directory.Exists(installed)) return installed;

        string titled = Path.Combine(gameRoot, "Mod", "护肝交互", "Assets", "InteractionIcons");
        if (Directory.Exists(titled)) return titled;

        string assemblyLocation = typeof(FrontendPlugin).Assembly.Location;
        if (!string.IsNullOrWhiteSpace(assemblyLocation))
            return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(assemblyLocation)!,
                "..", "Assets", "InteractionIcons"));
        return installed;
    }

    internal static string FileName(string key) => key.Replace(':', '_').Replace('/', '_');
    private static string Sanitize(string key) => new(key.Select(character =>
        char.IsLetterOrDigit(character) ? character : '_').ToArray());
}

internal sealed class InteractionActionHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    internal Action? Enter;
    internal Action? Exit;
    public void OnPointerEnter(PointerEventData eventData) => Enter?.Invoke();
    public void OnPointerExit(PointerEventData eventData) => Exit?.Invoke();
}
