using System.Reflection;
using FrameWork;
using FrameWork.UISystem.UIElements;
using Game.Components.Avatar;
using Game.Components.Character;
using Game.Views.MapBlockCharList;
using GameData.DLC.FiveLoong;
using GameData.Domains.Character.Display;
using GameData.Domains.Map;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MapSkillFinder.Frontend;

/// <summary>Builds a result card by cloning the game's world-map character entry.</summary>
internal static class NativeCharacterCard
{
    private const int MaximumCachedCards = 24;
    private static readonly Dictionary<int, Stack<GameObject>> Cache = new();
    private static Transform? _cacheRoot;
    private static int _cachedCount;

    private static readonly FieldInfo CharIdField = typeof(MapBlockChar).GetField(
        "CharId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(typeof(MapBlockChar).FullName, "CharId");

    internal static GameObject CreatePlaceholder()
    {
        var root = new GameObject("FinderCharacterPlaceholder", typeof(RectTransform));
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(320f, 102f);
        return root;
    }

    internal static void ReleasePlaceholder(GameObject root) => UnityEngine.Object.Destroy(root);

    internal static GameObject Create(
        int characterId,
        string name,
        string position,
        sbyte grade,
        string displayData,
        short areaId,
        short blockId,
        Action<int> onInteract)
    {
        bool canInteract = IsAtTaiwuLocation(areaId, blockId);
        if (TryTakeCached(characterId, out GameObject cached))
        {
            Configure(cached, characterId, name, position, grade, displayData, canInteract, onInteract);
            return cached;
        }

        MapBlockChar? template = Resources.FindObjectsOfTypeAll<MapBlockChar>()
            .FirstOrDefault(item => item.gameObject.scene.IsValid() &&
                item.GetComponentInParent<Canvas>() != null);
        if (template == null)
            return CreateFallback(name, position);

        GameObject root = UnityEngine.Object.Instantiate(template.gameObject);
        root.name = "FinderCharacter_" + characterId;
        root.SetActive(true);

        Configure(root, characterId, name, position, grade, displayData, canInteract, onInteract);
        return root;
    }

    internal static void Release(GameObject root)
    {
        if (root == null) return;
        NativeCharacterCardState? state = root.GetComponent<NativeCharacterCardState>();
        if (state == null || _cachedCount >= MaximumCachedCards)
        {
            root.transform.SetParent(null, false);
            UnityEngine.Object.Destroy(root);
            return;
        }

        root.SetActive(false);
        root.transform.SetParent(GetCacheRoot(), false);
        if (!Cache.TryGetValue(state.CharacterId, out Stack<GameObject> cards))
            Cache[state.CharacterId] = cards = new Stack<GameObject>();
        cards.Push(root);
        _cachedCount++;
    }

    private static void Configure(
        GameObject root, int characterId, string name, string position, sbyte grade,
        string displayData, bool canInteract, Action<int> onInteract)
    {
        root.name = "FinderCharacter_" + characterId;
        root.SetActive(true);
        MapBlockChar card = root.GetComponent<MapBlockChar>();
        NativeCharacterCardState state = root.GetComponent<NativeCharacterCardState>()
            ?? root.AddComponent<NativeCharacterCardState>();
        if (root.GetComponent<ClearSelectionOnCharacterHover>() == null)
            root.AddComponent<ClearSelectionOnCharacterHover>();
        state.CharacterId = characterId;
        state.Holder ??= new FinderCharacterHolder();
        state.Holder.Configure(canInteract, onInteract);
        bool hasNativeDisplay = state.NativeDisplayApplied;
        if (!string.Equals(state.DisplayData, displayData, StringComparison.Ordinal))
        {
            hasNativeDisplay = TryApplyCharacterData(card, state.Holder, displayData);
            if (!hasNativeDisplay)
                CharIdField.SetValue(card, characterId);
            state.DisplayData = displayData;
            state.NativeDisplayApplied = hasNativeDisplay;
        }

        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        if (texts.Length > 0) texts[0].text = name;
        // MapBlockChar.Set obtains the game's rich-text organization title via
        // its native GetTextData path (for example: white sect + red leader).
        // Replacing it with our plain backend string discards that grade color.
        if (!hasNativeDisplay)
        {
            GradeComponent? gradeComponent = root.GetComponentInChildren<GradeComponent>(true);
            if (gradeComponent != null)
                gradeComponent.Set(position, grade);
            else if (texts.Length > 1)
                texts[1].text = position;
        }

        SetOptionalChild(root.transform, "MapBlockCharStat", false);
        SetOptionalChild(root.transform, "GuardIcon", false);

        CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>() ?? root.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;
        CImage? background = root.GetComponent<CImage>();
        if (background != null)
            background.color = canInteract ? Color.white : new Color(0.58f, 0.58f, 0.58f, 1f);
    }

    private static bool TryTakeCached(int characterId, out GameObject root)
    {
        if (Cache.TryGetValue(characterId, out Stack<GameObject> cards))
        {
            while (cards.Count > 0)
            {
                root = cards.Pop();
                _cachedCount--;
                if (root != null) return true;
            }
            Cache.Remove(characterId);
        }
        root = null!;
        return false;
    }

    private static Transform GetCacheRoot()
    {
        if (_cacheRoot != null) return _cacheRoot;
        var root = new GameObject("MapSkillFinder_CharacterCardCache");
        root.SetActive(false);
        UnityEngine.Object.DontDestroyOnLoad(root);
        _cacheRoot = root.transform;
        return _cacheRoot;
    }

    internal static bool IsAtTaiwuLocation(short areaId, short blockId)
    {
        try
        {
            Location current = SingletonObject.getInstance<WorldMapModel>().CurrentLocation;
            return current.AreaId == areaId && current.BlockId == blockId;
        }
        catch
        {
            return false;
        }
    }

    private static unsafe bool TryApplyCharacterData(
        MapBlockChar card, IMapBlockCharHolder holder, string encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded)) return false;
        try
        {
            byte[] bytes = Convert.FromBase64String(encoded);
            var data = new CharacterDisplayData();
            fixed (byte* pointer = bytes)
                data.Deserialize(pointer);
            card.Set(holder, data, isSpecialNpc: false, isActive: true);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[MapSkillFinder] 原生人物数据读取失败：" + exception.Message);
            return false;
        }
    }

    private static void SetOptionalChild(Transform root, string name, bool active)
    {
        Transform? child = root.Find(name);
        if (child != null) child.gameObject.SetActive(active);
    }

    private static GameObject CreateFallback(string name, string position)
    {
        var root = new GameObject("FinderCharacterFallback", typeof(RectTransform));
        Debug.LogWarning($"[MapSkillFinder] 未找到原生人物条目模板：{name}（{position}）");
        return root;
    }
}

internal sealed class FinderCharacterHolder : IMapBlockCharHolder
{
    private bool _canInteract;
    private Action<int>? _onInteract;

    internal void Configure(bool canInteract, Action<int> onInteract)
    {
        _canInteract = canInteract;
        _onInteract = onInteract;
    }

    public List<LoongInfo> LoongInfos { get; } = new();
    public bool CanClick(DisplayType type, int id) => _canInteract;
    public void OnClick(DisplayType type, int id)
    {
        if (_canInteract) _onInteract?.Invoke(id);
    }
}

internal sealed class NativeCharacterCardState : MonoBehaviour
{
    internal int CharacterId;
    internal string DisplayData = string.Empty;
    internal bool NativeDisplayApplied;
    internal FinderCharacterHolder? Holder;
}

/// <summary>
/// Native mouse-tip hotkeys refuse to run while any UI object remains selected.
/// Search fields and the event window can leave a stale selection behind, so
/// match the map character list by releasing keyboard focus on actual hover.
/// </summary>
internal sealed class ClearSelectionOnCharacterHover : MonoBehaviour, IPointerEnterHandler
{
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (EventSystem.current?.currentSelectedGameObject != null)
            EventSystem.current.SetSelectedGameObject(null, eventData);
    }
}
