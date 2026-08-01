using System.Reflection;
using FrameWork;
using FrameWork.UISystem.UIElements;
using Game.Views.MapBlockCharList;
using GameData.DLC.FiveLoong;
using GameData.Domains.Character.Display;
using GameData.Domains.Merchant;
using GameData.Serializer;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TaiwuUi;

public enum TaiwuNativeCharacterKind
{
    Character,
    Caravan,
}

/// <summary>Serialized display data accepted by the native world-map character card.</summary>
public sealed record TaiwuNativeCharacterCardData(
    int CharacterId,
    string DisplayData,
    TaiwuNativeCharacterKind Kind = TaiwuNativeCharacterKind.Character);

/// <summary>Visual and interaction options for a native world-map character card.</summary>
public sealed record TaiwuNativeCharacterCardOptions
{
    public float Width { get; init; } = 320f;
    public float Height { get; init; } = 102f;
    public bool Selected { get; init; }
    public bool Interactable { get; init; } = true;
    public bool ShowStatus { get; init; } = true;
    public bool ShowGuardIcon { get; init; }
    public bool EnableHotkeyTooltip { get; init; } = true;
}

/// <summary>
/// Native world-map character cards, including CharacterOnMapBlock tooltips.
/// The tooltip's built-in Alt and Shift states work even after an input field had focus.
/// </summary>
public static class TaiwuNativeCharacterCard
{
    private const int MaximumCachedCards = 32;
    private static readonly Stack<GameObject> Cache = new();
    private static readonly FieldInfo JieqingSignField = typeof(MapBlockChar).GetField(
        "jieqingSign", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(typeof(MapBlockChar).FullName, "jieqingSign");
    private static Transform? _cacheRoot;
    private static MapBlockChar? _template;

    public static GameObject Create(
        TaiwuNativeCharacterCardData data,
        Action onClick,
        TaiwuNativeCharacterCardOptions? options = null)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (onClick == null) throw new ArgumentNullException(nameof(onClick));
        options ??= new TaiwuNativeCharacterCardOptions();

        GameObject root = TakeCached() ?? CreateClone();
        root.name = "TaiwuNativeCharacter_" + data.CharacterId;
        root.SetActive(true);
        Configure(root, data, onClick, options);
        return root;
    }

    public static void Release(GameObject root)
    {
        if (root == null) return;
        foreach (TooltipInvoker tooltip in root.GetComponentsInChildren<TooltipInvoker>(true))
            if (tooltip.Showing) tooltip.HideTips();
        if (root.GetComponent<TaiwuNativeCharacterCardState>() == null ||
            Cache.Count >= MaximumCachedCards)
        {
            UnityEngine.Object.Destroy(root);
            return;
        }
        root.SetActive(false);
        root.transform.SetParent(GetCacheRoot(), false);
        Cache.Push(root);
    }

    public static void SetSelected(GameObject root, bool selected)
    {
        Transform? selection = root?.transform.Find("TaiwuUiSelection");
        if (selection != null) selection.gameObject.SetActive(selected);
    }

    private static GameObject CreateClone()
    {
        MapBlockChar? template = ResolveTemplate();
        if (template == null)
            return new GameObject("TaiwuNativeCharacterFallback", typeof(RectTransform));
        GameObject root = UnityEngine.Object.Instantiate(template.gameObject);
        root.AddComponent<TaiwuNativeCharacterCardState>();
        root.AddComponent<TaiwuCharacterHotkeyFocusRelease>();
        root.AddComponent<TaiwuCharacterScrollForwarder>();
        return root;
    }

    private static void Configure(
        GameObject root,
        TaiwuNativeCharacterCardData data,
        Action onClick,
        TaiwuNativeCharacterCardOptions options)
    {
        MapBlockChar? card = root.GetComponent<MapBlockChar>();
        if (card == null)
        {
            RectTransform fallbackRect = root.GetComponent<RectTransform>();
            fallbackRect.sizeDelta = new Vector2(options.Width, options.Height);
            return;
        }

        TaiwuNativeCharacterCardState state = root.GetComponent<TaiwuNativeCharacterCardState>()
            ?? root.AddComponent<TaiwuNativeCharacterCardState>();
        state.Holder ??= new TaiwuNativeCharacterHolder();
        state.Holder.Configure(data.CharacterId, options.Interactable, onClick);

        // These cloned cards never show the legacy-point marker. Removing the
        // serialized reference before Set also prevents its asynchronous query.
        if (JieqingSignField.GetValue(card) is CImage sign)
            sign.gameObject.SetActive(false);
        JieqingSignField.SetValue(card, null);

        ApplyDisplayData(card, state.Holder, data);
        // Keep the selectable enabled so hover tooltips (including Alt/Shift)
        // remain available for remote/non-clickable search results. The holder
        // alone decides whether a click is accepted.
        card.Interactable = true;
        SetOptionalChild(root.transform, "MapBlockCharStat", options.ShowStatus);
        SetOptionalChild(root.transform, "GuardIcon", options.ShowGuardIcon);

        TooltipInvoker? characterTooltip = root.GetComponents<TooltipInvoker>()
            .FirstOrDefault(item => item.Type == TipType.CharacterOnMapBlock);
        if (characterTooltip != null)
            characterTooltip.enabled = options.EnableHotkeyTooltip &&
                                       data.Kind == TaiwuNativeCharacterKind.Character;
        TaiwuCharacterHotkeyFocusRelease? focusRelease =
            root.GetComponent<TaiwuCharacterHotkeyFocusRelease>();
        if (focusRelease != null)
            focusRelease.enabled = characterTooltip?.enabled == true;

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(options.Width, options.Height);
        LayoutElement layout = root.GetComponent<LayoutElement>() ?? root.AddComponent<LayoutElement>();
        layout.minWidth = options.Width;
        layout.preferredWidth = options.Width;
        layout.minHeight = options.Height;
        layout.preferredHeight = options.Height;
        layout.flexibleWidth = 0f;
        layout.flexibleHeight = 0f;

        CanvasGroup canvas = root.GetComponent<CanvasGroup>() ?? root.AddComponent<CanvasGroup>();
        canvas.alpha = 1f;
        canvas.blocksRaycasts = true;
        canvas.interactable = true;
        if (root.GetComponent<CImage>() is { } background)
            background.color = options.Interactable
                ? Color.white
                : new Color(0.58f, 0.58f, 0.58f, 1f);
        EnsureSelectionVisual(root, options.Selected);
    }

    private static unsafe void ApplyDisplayData(
        MapBlockChar card,
        IMapBlockCharHolder holder,
        TaiwuNativeCharacterCardData data)
    {
        byte[] bytes = Convert.FromBase64String(data.DisplayData);
        fixed (byte* pointer = bytes)
        {
            if (data.Kind == TaiwuNativeCharacterKind.Caravan)
            {
                var caravan = new CaravanDisplayData();
                caravan.Deserialize(pointer);
                card.Set(holder, caravan);
            }
            else
            {
                var character = new CharacterDisplayData();
                character.Deserialize(pointer);
                card.Set(holder, character, isSpecialNpc: false, isActive: true);
            }
        }
    }

    private static void EnsureSelectionVisual(GameObject root, bool selected)
    {
        Transform? existing = root.transform.Find("TaiwuUiSelection");
        if (existing != null)
        {
            existing.gameObject.SetActive(selected);
            return;
        }

        var selection = new GameObject("TaiwuUiSelection", typeof(RectTransform), typeof(CImage));
        selection.transform.SetParent(root.transform, false);
        selection.transform.SetAsFirstSibling();
        var rect = (RectTransform)selection.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        CImage image = selection.GetComponent<CImage>();
        CButton? button = root.GetComponent<CButton>();
        image.sprite = button?.spriteState.highlightedSprite;
        image.type = root.GetComponent<CImage>()?.type ?? Image.Type.Simple;
        image.color = image.sprite == null
            ? new Color(0.20f, 0.70f, 0.68f, 0.72f)
            : Color.white;
        image.raycastTarget = false;
        selection.SetActive(selected);
    }

    private static MapBlockChar? ResolveTemplate()
    {
        if (_template != null) return _template;
        _template = Resources.FindObjectsOfTypeAll<MapBlockChar>()
            .FirstOrDefault(item => item.gameObject.scene.IsValid() &&
                                    item.GetComponentInParent<Canvas>() != null);
        return _template;
    }

    private static GameObject? TakeCached()
    {
        while (Cache.Count > 0)
        {
            GameObject root = Cache.Pop();
            if (root != null) return root;
        }
        return null;
    }

    private static Transform GetCacheRoot()
    {
        if (_cacheRoot != null) return _cacheRoot;
        var root = new GameObject("TaiwuUi_NativeCharacterCardCache");
        root.SetActive(false);
        UnityEngine.Object.DontDestroyOnLoad(root);
        _cacheRoot = root.transform;
        return _cacheRoot;
    }

    private static void SetOptionalChild(Transform root, string name, bool active)
    {
        Transform? child = root.Find(name);
        if (child != null) child.gameObject.SetActive(active);
    }
}

internal sealed class TaiwuNativeCharacterHolder : IMapBlockCharHolder
{
    private int _characterId;
    private bool _interactable;
    private Action? _onClick;

    internal void Configure(int characterId, bool interactable, Action onClick)
    {
        _characterId = characterId;
        _interactable = interactable;
        _onClick = onClick;
    }

    public List<LoongInfo> LoongInfos { get; } = new();
    public bool CanClick(DisplayType type, int id) => _interactable && id == _characterId;
    public void OnClick(DisplayType type, int id)
    {
        if (CanClick(type, id)) _onClick?.Invoke();
    }
}

internal sealed class TaiwuNativeCharacterCardState : MonoBehaviour
{
    internal TaiwuNativeCharacterHolder? Holder;
}

internal sealed class TaiwuCharacterHotkeyFocusRelease : MonoBehaviour, IPointerEnterHandler
{
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (EventSystem.current?.currentSelectedGameObject != null)
            EventSystem.current.SetSelectedGameObject(null, eventData);
    }
}

/// <summary>
/// MapBlockChar carries an EventTrigger, so Unity's normal ExecuteHierarchy path
/// stops on the card. Forwarding restores wheel scrolling in ordinary ScrollRects;
/// CScrollRect still uses its native polling path.
/// </summary>
internal sealed class TaiwuCharacterScrollForwarder : MonoBehaviour, IScrollHandler
{
    public void OnScroll(PointerEventData eventData)
    {
        if (transform.parent != null)
            ExecuteEvents.ExecuteHierarchy(
                transform.parent.gameObject, eventData, ExecuteEvents.scrollHandler);
    }
}
