using FrameWork;
using FrameWork.UISystem.UIElements;
using Game.Views.CharacterMenu;
using Game.Views.Map;
using TaiwuUi;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LiverFriendlyInteractions.Frontend;

internal sealed class InteractionHubWindow : MonoBehaviour
{
    private const string OwnerId = "LiverFriendlyInteractions";
    private const string WindowId = "InteractionHub";
    private const float NativeEventProbeIntervalSeconds = 0.05f;
    private const float WorldMapFallbackGraceSeconds = 2f;

    private InteractionHubPreferences _preferences = null!;
    private readonly TaiwuSelection<InteractionPersonGroup> _personGroup = new(
        TaiwuSelectionMode.Single, new[] { InteractionPersonGroup.CurrentBlock });
    private TaiwuTabsModel<InteractionPersonGroup> _personTabs = null!;
    private readonly TaiwuSelection<InteractionTab> _interactionTab = new(
        TaiwuSelectionMode.Single, new[] { InteractionTab.Favorite });
    private readonly TaiwuValue<UiElement> _hoverContent = new(Ui.Spacer());

    private InteractionHubSettingsWindow? _settings;
    private ITaiwuWindow? _window;
    private InteractionHubSnapshot? _snapshot;
    private int _selectedTargetId = -1;
    private InteractionPersonKind _selectedKind;
    private bool _requesting;
    private bool _interactionPending;
    private bool _waitingForWorldMap;
    private bool _nativeEventObserved;
    private GameObject? _nativeEventWindow;
    private float _nativeEventWaitStartedAt;
    private float _nextNativeEventProbeAt;
    private UIElement? _returnAfterElement;
    private bool _returnElementObserved;
    private bool _closedByUser;
    private bool _shopPrewarmRequested;

    // 人物列表与交互网格由 MOD 自管（框架的整窗 Render 会全量重建原生树，导致每次
    // 点击人物/页签都让人物卡片整列重刷）。点击时只重建真正变化的区域。
    private Transform? _personContent;
    private Transform? _actionContent;
    private readonly Dictionary<long, CImage> _cardSelectionVisuals = new();
    private static TMP_FontAsset? _cachedFont;

    internal bool IsShowing => _window?.IsShowing == true;
    internal bool SettingsShowing => _settings?.IsShowing == true;

    private void Awake()
    {
        _preferences = new InteractionHubPreferences();
        _personTabs = new TaiwuTabsModel<InteractionPersonGroup>(_personGroup, new[]
        {
            new TaiwuTabItem<InteractionPersonGroup>(
                InteractionPersonGroup.CurrentBlock, "当前地格", TaiwuIcons.MapCharacters),
            new TaiwuTabItem<InteractionPersonGroup>(
                InteractionPersonGroup.Teammate, "同道", TaiwuIcons.MapEnemies),
            new TaiwuTabItem<InteractionPersonGroup>(
                InteractionPersonGroup.Merchant, "商人/商队", TaiwuIcons.MapCaravans),
        });
        _settings = new InteractionHubSettingsWindow(_preferences, PopulateActions);
        _personGroup.SelectionChanged += _ =>
        {
            SelectDefaultPerson();
            PopulatePeople();
            PopulateActions();
        };
        _interactionTab.SelectionChanged += _ => PopulateActions();
    }

    private void Update()
    {
        bool closePressed = Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1);
        bool togglePressed = Input.GetKeyDown(KeyCode.BackQuote);
        if ((togglePressed || closePressed) && SettingsShowing && UIManager.Instance != null &&
            UIManager.Instance.IsFocusElement(UIElement.Dialog))
        {
            if (togglePressed) UIManager.Instance.HideUI(UIElement.Dialog);
            return;
        }
        if ((togglePressed || closePressed) && SettingsShowing)
        {
            _settings!.Hide();
            return;
        }
        if ((togglePressed || closePressed) && IsShowing)
        {
            _closedByUser = true;
            _window!.Hide();
            return;
        }
        if (togglePressed && InteractionHubWorldMapFocusPolicy.ShouldOpenFromShortcut(
                CanOpenFromWorldMap(), _closedByUser, HasActiveWorldMap()))
        {
            Open();
            return;
        }

        if (_returnAfterElement != null && UIManager.Instance != null)
        {
            bool isActive = UIManager.Instance.IsElementActive(_returnAfterElement);
            _returnElementObserved |= isActive;
            if (InteractionHubWorldMapFocusPolicy.ShouldReturnFromExternalUi(
                    _returnElementObserved, isActive))
            {
                _returnAfterElement = null;
                _returnElementObserved = false;
                _interactionPending = false;
                _closedByUser = false;
                RefreshAndShow();
                return;
            }
        }

        if (_waitingForWorldMap)
        {
            bool nativeEventActive = IsNativeEventWindowActive();
            if (InteractionHubWorldMapFocusPolicy.ShouldHideHubForNativeEvent(nativeEventActive))
                _window?.Hide();
            _nativeEventObserved |= nativeEventActive;
            bool nativeEventClosed = InteractionHubWorldMapFocusPolicy.ShouldReturnFromExternalUi(
                _nativeEventObserved, nativeEventActive);
            bool worldMapReturned = InteractionHubWorldMapFocusPolicy.ShouldCheckWorldMapFallback(
                _nativeEventObserved,
                Time.unscaledTime - _nativeEventWaitStartedAt,
                WorldMapFallbackGraceSeconds) && TryGetFocusedWorldMap(out _);
            if (nativeEventClosed || worldMapReturned)
            {
                _waitingForWorldMap = false;
                _nativeEventObserved = false;
                _nativeEventWindow = null;
                _interactionPending = false;
                RefreshAndShow();
            }
        }
    }

    internal void Open()
    {
        if (_requesting || _interactionPending) return;
        PrewarmShopUi();
        _closedByUser = false;
        _interactionTab.Replace(new[] { InteractionTab.Favorite }, notify: false);
        _personGroup.Replace(new[] { InteractionPersonGroup.CurrentBlock }, notify: false);
        // KeepAlive retains the complete native tree. Reopening should expose
        // that cached tree immediately while the fresh snapshot is requested,
        // instead of replacing it with a loading document and rebuilding the
        // expensive native character cards twice.
        if (_window != null && _personContent != null && _actionContent != null)
            _window.Show();
        RefreshAndShow();
    }

    private void RefreshAndShow()
    {
        if (_requesting) return;
        _requesting = true;
        InteractionHubBackendClient.GetSnapshot(snapshot =>
        {
            _requesting = false;
            _snapshot = snapshot;
            EnsureSelection();
            if (snapshot.Success && _window != null &&
                _personContent != null && _actionContent != null)
            {
                PopulatePeople();
                PopulateActions();
            }
            else
            {
                Render();
            }
            _window?.Show();
        });
    }

    private void Render()
    {
        if (_snapshot == null) return;
        UiWindow document = BuildDocument(_snapshot.Success
            ? BuildHubContent()
            : Ui.Muted("读取交互数据失败：" + _snapshot.Message));
        if (_window == null) _window = TaiwuUiApi.Mount(document);
        else _window.Render(document);
    }

    private UiWindow BuildDocument(UiElement content) => new(
        OwnerId, WindowId, content, title: "人物交互",
        width: 1920f, height: 1080f,
        layer: TaiwuWindowLayer.Popup, cover: TaiwuWindowCover.Full,
        presentation: TaiwuWindowPresentation.Encyclopedia,
        lifetime: TaiwuWindowLifetime.KeepAlive);

    private UiElement BuildHubContent()
    {
        UiElement left = Ui.Column(
            Ui.NativeHost(1f, 1f, CreateLeftWidthAnchor, UnityEngine.Object.Destroy)
                with { Key = "left-width-anchor" },
            Ui.MapIconTabs(_personTabs) with { Key = "person-tabs" },
            Ui.NativeHost(390f, 760f, CreatePersonRegion, ReleasePersonRegion)
                with { Key = "person-host" }) with { Key = "left" };

        UiElement right = Ui.Column(
            Ui.Row(
                Ui.SecondaryTabs(_interactionTab, new[]
                {
                    new TaiwuChoiceOption<InteractionTab>(InteractionTab.Favorite, "常用"),
                    new TaiwuChoiceOption<InteractionTab>(InteractionTab.Other, "其他"),
                    new TaiwuChoiceOption<InteractionTab>(InteractionTab.Unavailable, "不可用"),
                }) with { Key = "interaction-tabs" },
                Ui.Button("设置", OpenSettings, new TaiwuButtonOptions
                {
                    Width = 150f, Height = 52f, Style = TaiwuButtonStyle.Outlined,
                }) with { Key = "settings" }) with { Key = "right-head" },
            Ui.Divider(),
            Ui.NativeHost(1236f, 760f, CreateActionRegion, ReleaseActionRegion)
                with { Key = "action-host" },
            Ui.Dynamic(_hoverContent, 62f) with { Key = "hover-tip" }) with { Key = "right" };

        return Ui.Row(left, Ui.Spacer(18f), Ui.Flex(right)) with { Key = "hub-layout" };
    }

    private static GameObject CreateLeftWidthAnchor()
    {
        var root = new GameObject("InteractionHubLeftWidthAnchor", typeof(RectTransform));
        InteractionHubFixedWidthAnchor anchor = root.AddComponent<InteractionHubFixedWidthAnchor>();
        anchor.Width = 390f;
        anchor.TabVisualOffsetY = 32f;
        return root;
    }

    // ---------- MOD 自管区域：人物列表 ----------

    private GameObject CreatePersonRegion()
    {
        (GameObject root, Transform content) = CreateScrollRegion(
            "InteractionHubPersonRegion", 2f, showScrollbar: true);
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 4f;
        layout.padding = new RectOffset(12, 0, 0, 0);
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = false;
        layout.childForceExpandWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        _personContent = content;
        PopulatePeople();
        return root;
    }

    private void ReleasePersonRegion(GameObject root)
    {
        Transform? content = root.transform.Find("Viewport/Content");
        if (content != null)
            ReleasePersonCards(content);
        // Release 经队列延迟执行，可能晚于新区域创建，只有仍指向旧区域时才清空引用。
        if (_personContent != null && _personContent.IsChildOf(root.transform))
        {
            _personContent = null;
            _cardSelectionVisuals.Clear();
        }
        UnityEngine.Object.Destroy(root);
    }

    private void PopulatePeople()
    {
        if (_personContent == null) return;
        ReleasePersonCards(_personContent);
        _cardSelectionVisuals.Clear();
        IReadOnlyList<InteractionPersonView> people = CurrentPeople();
        if (people.Count == 0)
        {
            AddMutedText(_personContent, "这里没有可交互的人物");
            return;
        }
        foreach (InteractionPersonView person in people)
        {
            long key = PersonKey(person);
            GameObject card = InteractionHubNativeCharacterCard.Create(
                person, IsSelected(person), () => SelectPerson(person));
            card.transform.SetParent(_personContent, false);
            var layout = card.GetComponent<LayoutElement>() ?? card.AddComponent<LayoutElement>();
            layout.preferredWidth = 340f;
            layout.preferredHeight = 102f;
            if (InteractionHubNativeCharacterCard.FindSelectionVisual(card) is { } selection)
                _cardSelectionVisuals[key] = selection;
        }
    }

    // ---------- MOD 自管区域：交互网格 ----------

    private GameObject CreateActionRegion()
    {
        (GameObject root, Transform content) = CreateScrollRegion("InteractionHubActionRegion", 10f);
        var grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(300f, 112f);
        grid.spacing = new Vector2(12f, 12f);
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;
        _actionContent = content;
        PopulateActions();
        return root;
    }

    private void ReleaseActionRegion(GameObject root)
    {
        if (_actionContent != null && _actionContent.IsChildOf(root.transform))
            _actionContent = null;
        UnityEngine.Object.Destroy(root);
    }

    private void PopulateActions()
    {
        if (_actionContent == null) return;
        ClearChildren(_actionContent);
        InteractionPersonView? person = SelectedPerson();
        if (person == null)
        {
            AddMutedText(_actionContent, "请先选择人物");
            return;
        }
        InteractionTab tab = _interactionTab.Selected.FirstOrDefault();
        IReadOnlyList<InteractionOptionView> options = InteractionHubPolicy.Select(
            person.Options, _preferences.Favorites, tab);
        if (options.Count == 0)
        {
            AddMutedText(_actionContent, tab switch
            {
                InteractionTab.Favorite => "没有当前可用的常用交互，可在右上角设置中调整。",
                InteractionTab.Other => "没有其他可用交互。",
                _ => "没有不可用的交互。",
            });
            return;
        }
        foreach (InteractionOptionView option in options)
        {
            GameObject card = InteractionHubNativeActionCard.Create(
                option, () => Execute(person, option), SetHover);
            card.transform.SetParent(_actionContent, false);
        }
    }

    // ---------- 区域基础设施 ----------

    private static (GameObject Root, Transform Content) CreateScrollRegion(
        string name, float padding, bool showScrollbar = false)
    {
        if (showScrollbar)
        {
            TaiwuNativeScrollView native = TaiwuNativeScroll.CreateVertical(name);
            return (native.Root, native.Content);
        }

        var root = new GameObject(name, typeof(RectTransform), typeof(ScrollRect));
        var rootRect = (RectTransform)root.transform;
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.sizeDelta = Vector2.zero;

        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(root.transform, false);
        var viewportRect = (RectTransform)viewport.transform;
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(padding, padding);
        viewportRect.offsetMax = new Vector2(-(padding + (showScrollbar ? 14f : 0f)), -padding);

        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(viewport.transform, false);
        var contentRect = (RectTransform)content.transform;
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = Vector2.zero;
        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = root.GetComponent<ScrollRect>();
        scroll.viewport = viewportRect;
        scroll.content = contentRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 52f;
        if (showScrollbar)
        {
            var scrollbarRoot = new GameObject(
                "Scrollbar", typeof(RectTransform), typeof(CImage), typeof(Scrollbar));
            scrollbarRoot.transform.SetParent(root.transform, false);
            var scrollbarRect = (RectTransform)scrollbarRoot.transform;
            scrollbarRect.anchorMin = new Vector2(1f, 0f);
            scrollbarRect.anchorMax = new Vector2(1f, 1f);
            scrollbarRect.pivot = new Vector2(1f, 0.5f);
            scrollbarRect.sizeDelta = new Vector2(8f, -8f);
            scrollbarRect.anchoredPosition = new Vector2(-2f, 0f);
            CImage track = scrollbarRoot.GetComponent<CImage>();
            track.color = new Color(0.06f, 0.10f, 0.10f, 0.85f);

            var handleObject = new GameObject("Handle", typeof(RectTransform), typeof(CImage));
            handleObject.transform.SetParent(scrollbarRoot.transform, false);
            var handleRect = (RectTransform)handleObject.transform;
            handleRect.anchorMin = Vector2.zero;
            handleRect.anchorMax = Vector2.one;
            handleRect.offsetMin = Vector2.zero;
            handleRect.offsetMax = Vector2.zero;
            CImage handle = handleObject.GetComponent<CImage>();
            handle.color = new Color(0.65f, 0.53f, 0.31f, 0.95f);

            Scrollbar scrollbar = scrollbarRoot.GetComponent<Scrollbar>();
            scrollbar.handleRect = handleRect;
            scrollbar.targetGraphic = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            scroll.verticalScrollbarSpacing = 4f;
        }
        return (root, contentRect);
    }

    private static void ClearChildren(Transform parent)
    {
        for (int index = parent.childCount - 1; index >= 0; index--)
            UnityEngine.Object.DestroyImmediate(parent.GetChild(index).gameObject);
    }

    private static void ReleasePersonCards(Transform parent)
    {
        for (int index = parent.childCount - 1; index >= 0; index--)
        {
            GameObject child = parent.GetChild(index).gameObject;
            if (child.GetComponent<Game.Views.MapBlockCharList.MapBlockChar>() != null)
                InteractionHubNativeCharacterCard.Release(child);
            else
                UnityEngine.Object.DestroyImmediate(child);
        }
    }

    private static void AddMutedText(Transform parent, string message)
    {
        var root = new GameObject("Muted", typeof(RectTransform), typeof(TextMeshProUGUI),
            typeof(LayoutElement));
        root.transform.SetParent(parent, false);
        var text = root.GetComponent<TextMeshProUGUI>();
        text.text = message;
        text.fontSize = 22f;
        text.color = new Color(0.55f, 0.60f, 0.60f);
        text.alignment = TextAlignmentOptions.TopLeft;
        if (ResolveFont() is { } font)
            text.font = font;
        root.GetComponent<LayoutElement>().preferredHeight = 48f;
    }

    private static TMP_FontAsset? ResolveFont()
    {
        if (_cachedFont != null) return _cachedFont;
        _cachedFont = InteractionHubNativeCharacterCard.ResolveTemplate()
            ?.GetComponentInChildren<TextMeshProUGUI>(true)?.font;
        return _cachedFont;
    }

    // ---------- 交互行为 ----------

    private void Execute(InteractionPersonView person, InteractionOptionView option)
    {
        if (!option.Available || _interactionPending) return;
        _interactionPending = true;
        _nativeEventObserved = false;
        _nativeEventWindow = null;
        if (option.PreferenceKey == InteractionHubPolicy.ShowCharacterKey)
        {
            _window?.Hide();
            UIElement.CharacterMenu.SetOnInitArgs(EasyPool.Get<ArgumentBox>()
                .Set("CharacterId", person.TargetId)
                .Set("PreviousView", (short)9)
                .SetObject("ViewCharacterMenuTaretPage",
                    new SubPageIndex(ECharacterSubToggleBase.CharacterBase, ECharacterSubPage.Character)));
            WaitForExternalUi(UIElement.CharacterMenu);
            UIManager.Instance.ShowUI(UIElement.CharacterMenu);
            return;
        }
        if (option.PreferenceKey == InteractionHubPolicy.ExchangeItemsKey)
        {
            _window?.Hide();
            UIElement.Exchange.SetOnInitArgs(EasyPool.Get<ArgumentBox>()
                .Set("CharacterId", person.TargetId)
                .Set("ShouldTriggerEvent", false));
            WaitForExternalUi(UIElement.Exchange);
            UIManager.Instance.ShowUI(UIElement.Exchange);
            return;
        }

        InteractionHubBackendClient.Begin(person.TargetId, person.Kind == InteractionPersonKind.Caravan,
            option.PreferenceKey, option.TemplateId,
            (started, message) =>
        {
            if (started)
            {
                if (option.PreferenceKey == InteractionHubPolicy.BrowseGoodsKey)
                {
                    _window?.Hide();
                    WaitForExternalUi(UIElement.NewShop);
                }
                else
                {
                    _waitingForWorldMap = true;
                    _nativeEventWaitStartedAt = Time.unscaledTime;
                    _nextNativeEventProbeAt = 0f;
                    _nativeEventObserved = IsNativeEventWindowActive();
                }
                return;
            }
            _interactionPending = false;
            Debug.LogWarning("[护肝交互] 启动人物交互失败：" + message);
            RefreshAndShow();
        });
    }

    private void OpenSettings()
    {
        if (_snapshot != null) _settings?.Show(_snapshot.Catalog);
    }

    private void SetHover(string text) => _hoverContent.SetValue(string.IsNullOrWhiteSpace(text)
        ? Ui.Spacer() with { Key = "hover-empty" }
        : Ui.Muted(text) with { Key = "hover-text" });

    private void SelectPerson(InteractionPersonView person)
    {
        if (IsSelected(person)) return;
        long previous = SelectedKey();
        _selectedTargetId = person.TargetId;
        _selectedKind = person.Kind;
        UpdateCardTint(previous);
        UpdateCardTint(PersonKey(person));
        PopulateActions();
    }

    private void UpdateCardTint(long key)
    {
        if (key < 0) return;
        if (_cardSelectionVisuals.TryGetValue(key, out CImage? visual) && visual != null)
            InteractionHubNativeCharacterCard.SetSelected(visual, key == SelectedKey());
    }

    private void EnsureSelection()
    {
        IReadOnlyList<InteractionPersonView> current = CurrentPeople();
        if (current.Any(IsSelected)) return;
        SelectDefaultPerson();
    }

    private void SelectDefaultPerson()
    {
        IReadOnlyList<InteractionPersonView> current = CurrentPeople();
        if (current.Count == 0 && _snapshot != null)
        {
            foreach (InteractionPersonGroup alternative in new[]
                     {
                         InteractionPersonGroup.CurrentBlock,
                         InteractionPersonGroup.Teammate,
                         InteractionPersonGroup.Merchant,
                     })
            {
                IReadOnlyList<InteractionPersonView> other = People(alternative);
                if (other.Count == 0) continue;
                _personGroup.Replace(new[] { alternative }, notify: false);
                current = other;
                break;
            }
        }
        InteractionPersonView? first = current.FirstOrDefault();
        _selectedTargetId = first?.TargetId ?? -1;
        _selectedKind = first?.Kind ?? InteractionPersonKind.Character;
    }

    private IReadOnlyList<InteractionPersonView> CurrentPeople() =>
        People(_personGroup.Selected.FirstOrDefault());
    private IReadOnlyList<InteractionPersonView> People(InteractionPersonGroup group) => _snapshot == null
        ? Array.Empty<InteractionPersonView>()
        : group switch
        {
            InteractionPersonGroup.CurrentBlock => _snapshot.CurrentBlock,
            InteractionPersonGroup.Teammate => _snapshot.Teammates,
            InteractionPersonGroup.Merchant => _snapshot.Merchants,
            _ => Array.Empty<InteractionPersonView>(),
        };
    private InteractionPersonView? SelectedPerson() =>
        CurrentPeople().FirstOrDefault(IsSelected);

    private bool IsSelected(InteractionPersonView person) =>
        person.TargetId == _selectedTargetId && person.Kind == _selectedKind;

    private long SelectedKey() => _selectedTargetId < 0
        ? -1
        : ((long)_selectedKind << 32) | (uint)_selectedTargetId;

    private static long PersonKey(InteractionPersonView person) =>
        ((long)person.Kind << 32) | (uint)person.TargetId;

    private void PrewarmShopUi()
    {
        if (_shopPrewarmRequested || UIElement.NewShop.UiBase != null) return;
        _shopPrewarmRequested = true;
        UIElement.NewShop.PrepareRes(autoShow: false, onPrefabLoaded: _ => _shopPrewarmRequested = false,
            isLoadAsyncInBackground: true);
    }

    private void WaitForExternalUi(UIElement element)
    {
        _returnAfterElement = element;
        _returnElementObserved = false;
        _waitingForWorldMap = false;
        _nativeEventObserved = false;
        _nativeEventWindow = null;
        _closedByUser = false;
    }

    private static bool CanOpenFromWorldMap()
    {
        if (!TryGetFocusedWorldMap(out _)) return false;
        GameObject? selected = EventSystem.current?.currentSelectedGameObject;
        return selected == null || selected.GetComponentInParent<TMP_InputField>() == null;
    }

    private static bool TryGetFocusedWorldMap(out ViewWorldMap? worldMap)
    {
        worldMap = Resources.FindObjectsOfTypeAll<ViewWorldMap>()
            .FirstOrDefault(view => view.gameObject.activeInHierarchy);
        if (worldMap == null || UIManager.Instance == null || ViewWorldMap.InAdventureRemake)
            return false;

        return InteractionHubWorldMapFocusPolicy.IsSupportedContext(
            UIManager.Instance.IsFocusElement(worldMap.Element),
            UIManager.Instance.IsFocusElement(UIElement.StateMainWorld),
            UIManager.Instance.IsFocusElement(UIElement.MapBlockCharList));
    }

    private static bool HasActiveWorldMap() =>
        Resources.FindObjectsOfTypeAll<ViewWorldMap>()
            .Any(view => view.gameObject.activeInHierarchy) &&
        !ViewWorldMap.InAdventureRemake;

    private bool IsNativeEventWindowActive()
    {
        float now = Time.unscaledTime;
        if (InteractionHubWorldMapFocusPolicy.ShouldProbeNativeEventWindow(
                _nativeEventWindow != null, _nativeEventObserved, now, _nextNativeEventProbeAt))
        {
            _nextNativeEventProbeAt = now + NativeEventProbeIntervalSeconds;
            _nativeEventWindow = GameObject.Find("ViewEventWindow");
        }

        return _nativeEventWindow != null && _nativeEventWindow.activeInHierarchy;
    }

    private void OnDestroy()
    {
        _settings?.Dispose();
        _window?.Dispose();
    }
}

internal sealed class InteractionHubFixedWidthAnchor : MonoBehaviour
{
    internal float Width;
    internal float TabVisualOffsetY;
    private float _lastContentHeight = -1f;

    private void Start()
    {
        ApplyLayout();
    }

    private void LateUpdate()
    {
        Transform? column = transform.parent?.parent;
        if (column?.parent?.parent is not RectTransform content) return;
        if (!Mathf.Approximately(_lastContentHeight, content.rect.height))
            ApplyLayout();
    }

    private void ApplyLayout()
    {
        Transform? column = transform.parent?.parent;
        if (column == null || column.parent is not RectTransform row ||
            row.parent is not RectTransform content) return;
        LayoutElement? layout = column.GetComponent<LayoutElement>();
        if (layout == null) return;
        layout.minWidth = Width;
        layout.preferredWidth = Width;
        layout.flexibleWidth = 0f;

        float contentHeight = content.rect.height;
        if (contentHeight > 0f)
        {
            _lastContentHeight = contentHeight;
            LayoutElement? rowLayout = row.GetComponent<LayoutElement>();
            HorizontalLayoutGroup? rowGroup = row.GetComponent<HorizontalLayoutGroup>();
            if (rowGroup != null)
                rowGroup.childAlignment = TextAnchor.UpperLeft;
            if (rowLayout != null)
            {
                rowLayout.minHeight = contentHeight;
                rowLayout.preferredHeight = contentHeight;
            }
            layout.minHeight = contentHeight;
            layout.preferredHeight = contentHeight;

            Transform? personHost = column.Cast<Transform>()
                .FirstOrDefault(child => child.Find("InteractionHubPersonRegion") != null);
            RectTransform? tabsRoot = column.Find("MapIconTabs") as RectTransform;
            LayoutElement? anchorLayout = transform.parent?.GetComponent<LayoutElement>();
            LayoutElement? personLayout = personHost?.GetComponent<LayoutElement>();
            VerticalLayoutGroup? columnLayout = column.GetComponent<VerticalLayoutGroup>();
            if (personHost is RectTransform personRect && personLayout != null &&
                tabsRoot != null && columnLayout != null)
            {
                float anchorHeight = anchorLayout?.preferredHeight ?? 0f;
                float tabsHeight = tabsRoot.GetComponent<LayoutElement>()?.preferredHeight
                    ?? tabsRoot.rect.height;
                float personHeight = Mathf.Max(0f, contentHeight - anchorHeight - tabsHeight -
                    columnLayout.spacing * (column.childCount - 1));
                personLayout.minHeight = personHeight;
                personLayout.preferredHeight = personHeight;
                personRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, personHeight);
                if (personHost.Find("InteractionHubPersonRegion") is RectTransform region)
                    region.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, personHeight);
            }
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        LayoutRebuilder.ForceRebuildLayoutImmediate(row);
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)column);

        Transform? tabs = column.Find("MapIconTabs");
        if (tabs == null) return;
        OffsetTabVisual(tabs.Find("Back") as RectTransform);
        OffsetTabVisual(tabs.Find("Tweak") as RectTransform);
    }

    private void OffsetTabVisual(RectTransform? visual)
    {
        if (visual != null)
            visual.anchoredPosition += new Vector2(0f, TabVisualOffsetY);
    }
}

internal static class InteractionHubRuntime
{
    private static InteractionHubWindow? _window;

    internal static void Install()
    {
        if (_window != null) return;
        var root = new GameObject("LiverFriendlyInteractions_InteractionHub");
        UnityEngine.Object.DontDestroyOnLoad(root);
        _window = root.AddComponent<InteractionHubWindow>();
    }

    internal static void Uninstall()
    {
        if (_window != null) UnityEngine.Object.Destroy(_window.gameObject);
        _window = null;
    }

    internal static void Open() => _window?.Open();
}
