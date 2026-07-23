using Config;
using FrameWork;
using Game.Views.Bottom;
using GameData.Domains.Character;
using GameData.Domains.Item;
using GameData.Domains.Map;
using TaiwuUi;
using UnityEngine;

namespace MapSkillFinder.Frontend;

internal sealed class SkillFinderWindow : MonoBehaviour
{
    private const string OwnerId = "MapSkillFinder";
    private const string WindowId = "TaiwuFinder";

    private sealed class AbilityFilterState
    {
        internal TaiwuValue<bool> Enabled { get; } = new(false);
        internal sbyte LifeSkillType;
        internal sbyte Metric;
        internal TaiwuValue<float> Minimum { get; } = new(80f);
    }

    private sealed record CombatBookOption(short TemplateId, string Name, int Order);
    private sealed class BookCatalog
    {
        internal IReadOnlyList<TaiwuChoiceOption<sbyte>> CombatSects { get; init; } =
            Array.Empty<TaiwuChoiceOption<sbyte>>();
        internal IReadOnlyDictionary<sbyte, IReadOnlyList<TaiwuChoiceOption<sbyte>>> CombatTypes { get; init; } =
            new Dictionary<sbyte, IReadOnlyList<TaiwuChoiceOption<sbyte>>>();
        internal IReadOnlyDictionary<(sbyte Sect, sbyte Type), IReadOnlyList<CombatBookOption>> CombatBooks { get; init; } =
            new Dictionary<(sbyte Sect, sbyte Type), IReadOnlyList<CombatBookOption>>();
        internal IReadOnlyList<TaiwuChoiceOption<sbyte>> LifeTypes { get; init; } =
            Array.Empty<TaiwuChoiceOption<sbyte>>();
        internal IReadOnlyDictionary<sbyte, IReadOnlyList<Config.LifeSkillItem>> LifeBooks { get; init; } =
            new Dictionary<sbyte, IReadOnlyList<Config.LifeSkillItem>>();
        internal IReadOnlyList<(int Value, string Label)> OutlineTypes { get; init; } =
            Array.Empty<(int Value, string Label)>();
    }

    private ITaiwuWindow? _window;
    private int _requestVersion;
    // Holdings queries for the two book tabs run in parallel; a shared version
    // would let the later request invalidate the earlier one's response.
    private int _combatHoldingsVersion;
    private int _lifeHoldingsVersion;
    private bool _initialized;
    private string _status = "正在读取地域目录……";
    private readonly TaiwuValue<UiElement> _statusContent = new(Ui.Muted("正在读取地域目录……"));
    private readonly TaiwuValue<UiElement>[] _tabContent =
    {
        new(Ui.Spacer()), new(Ui.Spacer()), new(Ui.Spacer()), new(Ui.Spacer()),
    };
    private sbyte _activeTab;
    private BookCatalog? _bookCatalog;
    private FinderCatalogView? _catalog;
    private short _selectedAreaId = -1;
    private sbyte _areaCategory;
    // Last game date seen in a catalog response; 0 = unknown (older backend or
    // no catalog yet). A change means a month passed and all cached queries are stale.
    private int _catalogDateTick;
    private float _nextWorldCheckAt = -1f;
    private bool _worldCheckInFlight;

    private readonly TaiwuSelection<sbyte> _mainTab = new(TaiwuSelectionMode.Single, new sbyte[] { 0 });
    private readonly TaiwuSelection<byte> _bookSources =
        new(TaiwuSelectionMode.Multiple, new byte[] { 1 });

    private sbyte _combatSect = -1;
    private sbyte _combatType = -1;
    private short _combatSkill = -1;
    private readonly sbyte[] _combatStates = { 0, 0, 0, 0, 0, 0 };
    private readonly sbyte[] _combatTypes = { -1, -1, -1, -1, -1, -1 };

    private sbyte _lifeType = -1;
    private short _lifeSkill = -1;
    private readonly sbyte[] _lifeStates = { 0, 0, 0, 0, 0 };

    private BookHoldingsResponse? _combatHoldings;
    private BookHoldingsResponse? _lifeHoldings;
    private TaiwuBookKnowledge _combatTaiwuKnowledge = TaiwuBookKnowledge.Empty;
    private TaiwuBookKnowledge _lifeTaiwuKnowledge = TaiwuBookKnowledge.Empty;
    // Holder sets mount in pages of this size to bound the live UI object count.
    private const int HolderSetRenderPageSize = 30;
    private int _holderSetRenderLimit = HolderSetRenderPageSize;

    private readonly TaiwuValue<string> _personName = new(string.Empty);
    private readonly TaiwuSelection<sbyte> _personGrades = new(TaiwuSelectionMode.Multiple);
    private readonly TaiwuSelection<sbyte> _personGenders = new(TaiwuSelectionMode.Single);
    private readonly AbilityFilterState[] _abilityFilters =
        { new(), new(), new() };
    private PersonSearchResponse? _personResponse;
    private readonly TaiwuTableModel<PersonRowView, int> _personTable = new(row => row.CharacterId,
        sort: new TaiwuValue<TaiwuSortState>(new TaiwuSortState("name", TaiwuSortDirection.Ascending)));
    // Person results live in their own dynamic host so a finished search only
    // rebuilds that region: the name input keeps focus and the page keeps scroll.
    private readonly TaiwuValue<UiElement> _personResultsContent =
        new(Ui.Muted("设置至少一个筛选条件后自动查找。"));
    // Debounce timer for auto-search; -1 means no search is pending.
    private float _personSearchAt = -1f;
    private const float PersonSearchDebounceSeconds = 0.6f;
    // While the window is open the catalog is re-polled at this interval so a
    // month advance (which changes NPC holdings/abilities) drops cached results.
    private const float WorldCheckIntervalSeconds = 3f;

    private readonly TaiwuSelection<sbyte> _merchantTargets =
        new(TaiwuSelectionMode.Multiple, new sbyte[] { 0 });
    private readonly TaiwuSelection<sbyte> _merchantGuilds =
        new(TaiwuSelectionMode.Multiple);
    private readonly TaiwuSelection<sbyte> _merchantLevels =
        new(TaiwuSelectionMode.Multiple);
    private readonly TaiwuSelection<sbyte> _caravanState =
        new(TaiwuSelectionMode.Single, new sbyte[] { 0 });
    private MerchantSearchResponse? _merchantResponse;
    private readonly TaiwuTableModel<MerchantRowView, string> _merchantTable =
        new(row => $"{row.TargetType}:{row.EntityId}");
    // Keep merchant results in their own dynamic host so filter controls stay
    // mounted while an asynchronous query completes.
    private readonly TaiwuValue<UiElement> _merchantResultsContent =
        new(Ui.Muted("变更筛选条件后自动寻找。"));
    private float _merchantSearchAt = -1f;

    internal void Initialize(ViewBottom owner)
    {
        if (_initialized)
            return;
        _initialized = true;
        _personTable.Selection.SelectionChanged += _ => RefreshActivePage();
        _merchantTable.InlineRowAction = row => CanMarkSelectedArea && row.BlockId >= 0
            ? new TaiwuMenuAction("定位", () => MarkMerchantLocation(row))
            : null;
        _mainTab.SelectionChanged += selected =>
        {
            if (selected.Count == 0) return;
            sbyte next = selected.First();
            if (next == _activeTab) return;
            _tabContent[_activeTab].SetValue(Ui.Spacer());
            _activeTab = next;
            ClearStatus();
            RefreshActivePage();
            if (next == 2 && _personResponse == null) SchedulePersonSearch();
            if (next == 3 && _merchantResponse == null) ScheduleMerchantSearch();
        };
        _bookSources.SelectionChanged += _ =>
        {
            MarkBookStale(0);
            MarkBookStale(1);
            RefreshActivePage();
        };
        foreach (AbilityFilterState filter in _abilityFilters)
        {
            // No page rebuild here: the popup card refreshes its own trigger and
            // fields, and rebuilding would destroy the open card.
            filter.Enabled.ValueChanged += _ => SchedulePersonSearch();
            filter.Minimum.ValueChanged += _ => SchedulePersonSearch();
        }
        _personName.ValueChanged += _ => SchedulePersonSearch();
        _personGrades.SelectionChanged += _ => SchedulePersonSearch();
        _personGenders.SelectionChanged += _ => SchedulePersonSearch();
        _merchantTargets.SelectionChanged += _ =>
        {
            // 商队状态 row visibility depends on the target selection.
            ScheduleMerchantSearch();
            RefreshActivePage();
        };
        _merchantGuilds.SelectionChanged += _ => ScheduleMerchantSearch();
        _merchantLevels.SelectionChanged += _ => ScheduleMerchantSearch();
        _caravanState.SelectionChanged += _ => ScheduleMerchantSearch();
    }

    internal void Open()
    {
        EnsureBookCatalog();
        _activeTab = _mainTab.Selected.Count == 0 ? (sbyte)0 : _mainTab.Selected.First();
        for (int index = 0; index < _tabContent.Length; index++)
            _tabContent[index].SetValueWithoutNotify(Ui.Spacer());
        SetStatus("正在读取地域目录……");
        _tabContent[_activeTab].SetValueWithoutNotify(BuildActiveTabPage());
        if (_window == null)
            Render();
        _window!.Show();
        _nextWorldCheckAt = Time.unscaledTime + WorldCheckIntervalSeconds;
        int version = ++_requestVersion;
        FinderBackendClient.GetCatalog(catalog =>
        {
            if (!Accept(version)) return;
            if (!catalog.Success)
            {
                SetStatus(catalog.Message);
                return;
            }
            // Switching to the current area or a month advance since the cached
            // queries ran makes them stale; drop them so the page re-queries.
            bool areaChanged = _selectedAreaId >= 0 && _selectedAreaId != catalog.CurrentAreaId;
            bool dateChanged = _catalogDateTick > 0 && catalog.DateTick > 0
                && catalog.DateTick != _catalogDateTick;
            _catalog = catalog;
            if (catalog.DateTick > 0) _catalogDateTick = catalog.DateTick;
            if (areaChanged || dateChanged) MarkAllStale();
            SelectArea(catalog.CurrentAreaId, markStale: false, refresh: false);
            ClearStatus();
            RefreshActivePage();
            if (_activeTab == 2) SchedulePersonSearch();
            if (_activeTab == 3) ScheduleMerchantSearch();
        });
    }

    internal void Close()
    {
        _requestVersion++;
        _window?.Hide();
    }

    private void OnDisable()
    {
        _requestVersion++;
        _window?.Dispose();
        _window = null;
    }

    private bool Accept(int version) => version == _requestVersion && _window?.IsShowing == true;

    private UiWindow BuildDocument()
    {
        UiElement tabs = Ui.Tabs(_mainTab, new[]
        {
            new UiTabPage<sbyte>(0, "功法书", Ui.Dynamic(_tabContent[0], 1200f) with { Key = "combat-dynamic" }),
            new UiTabPage<sbyte>(1, "技艺书", Ui.Dynamic(_tabContent[1], 1200f) with { Key = "life-dynamic" }),
            new UiTabPage<sbyte>(2, "人物", Ui.Dynamic(_tabContent[2], 1200f) with { Key = "person-dynamic" }),
            new UiTabPage<sbyte>(3, "商会", Ui.Dynamic(_tabContent[3], 1200f) with { Key = "merchant-dynamic" }),
        }, new TaiwuTabViewOptions { Height = 1300f, TabHeight = 64f, ContentPadding = 18f }) with { Key = "main-tabs" };

        return new UiWindow(OwnerId, WindowId,
            tabs,
            title: "寻访中心", width: 1920f, height: 1080f,
            layer: TaiwuWindowLayer.Popup, cover: TaiwuWindowCover.Full,
            presentation: TaiwuWindowPresentation.Encyclopedia);
    }

    private UiElement BuildTabPage(UiElement content, string key)
    {
        var children = new List<UiElement> { BuildRegionSelector() };
        if (!string.IsNullOrWhiteSpace(_status))
            children.Add(Ui.Dynamic(_statusContent, 36f) with { Key = key + "-status" });
        children.Add(Ui.Divider());
        children.Add(Ui.Spacer(10f));
        children.Add(content);
        children.Add(Ui.Spacer(8f));
        return Ui.Scroll(
            Ui.Column(children.ToArray()) with { Key = key + "-page-content" },
            new TaiwuScrollOptions { Height = 1200f, ShowBackground = false })
            with { Key = key + "-page-scroll" };
    }

    private UiElement BuildRegionSelector()
    {
        if (_catalog == null)
            return Ui.Row(Ui.Heading("查询地域"), Ui.Muted("正在读取……")) with { Key = "region-loading" };

        return Ui.Row(
            Ui.PopupCard(string.Empty,
                new TaiwuPopupCardModel(
                    () => AreaName(_selectedAreaId),
                BuildRegionCardFields),
                new TaiwuPopupCardOptions
                {
                    Title = "查询地域",
                    TriggerStyle = TaiwuPopupCardTriggerStyle.FilterOption,
                    Width = 130f,
                    PopupWidth = 620f,
                    PopupHeight = 240f,
                    MaximumPopupHeight = 560f,
                }) with { Key = "region-card" }) with { Key = "region-selector" };
    }

    private IReadOnlyList<TaiwuPopupCardField> BuildRegionCardFields()
    {
        if (_catalog == null)
            return Array.Empty<TaiwuPopupCardField>();

        TaiwuChoiceOption<sbyte>[] categories =
        {
            new(0, "门派地域"),
            new(1, "大城市"),
            new(2, "其他地域"),
        };
        AreaOptionView[] areas = _catalog.Areas.Where(item => item.Category == _areaCategory).ToArray();
        return new TaiwuPopupCardField[]
        {
            new(
                "地域类型",
                categories.First(item => item.Value == _areaCategory).Label,
                categories.Select(item => new TaiwuPopupCardOption(
                    item.Label, item.Value == _areaCategory)).ToArray(),
                index =>
                {
                    _areaCategory = categories[index].Value;
                    AreaOptionView? first = _catalog.Areas.FirstOrDefault(item => item.Category == _areaCategory);
                    if (first != null) SelectArea(first.AreaId, markStale: true, refresh: false);
                }),
            new(
                "具体地域",
                AreaName(_selectedAreaId),
                areas.Select(item => new TaiwuPopupCardOption(
                    item.Name, item.AreaId == _selectedAreaId)).ToArray(),
                index => SelectArea(areas[index].AreaId),
                Interactable: areas.Length > 0,
                CloseCardAfterSelect: true),
        };
    }

    private UiElement BuildCombatPage()
    {
        var children = new List<UiElement>
        {
            Ui.Row(
                BuildCombatSelectors(),
                Ui.Flex(BuildBookSource())) with { Key = "combat-top-filters" },
        };
        if (_combatSkill >= 0)
        {
            if (!SupportsBookHoldingWorkspace)
            {
                children.Add(Ui.Muted("书页组合功能已更新：请重启游戏，使后端载入新版接口。"));
            }
            else if (_combatHoldings == null)
            {
                children.Add(Ui.Muted("先读取该地域实际持有的书页，再从可用状态中点选目标。"));
                children.Add(ActionRow(LoadCombatHoldings, ResetCombat, "读取功法书持有情况"));
            }
            else children.Add(BuildCombatHoldingWorkspace());
        }
        return Ui.Column(children.ToArray()) with { Key = "combat-content" };
    }

    private UiElement BuildLifePage()
    {
        var children = new List<UiElement>
        {
            BuildLifeSelectors(),
        };
        if (_lifeSkill >= 0)
        {
            if (!SupportsBookHoldingWorkspace)
            {
                children.Add(Ui.Muted("书页组合功能已更新：请重启游戏，使后端载入新版接口。"));
            }
            else if (_lifeHoldings == null)
            {
                children.Add(Ui.Muted("先读取该地域实际持有的书页，再从可用状态中点选目标。"));
                children.Add(ActionRow(LoadLifeHoldings, ResetLife, "读取技艺书持有情况"));
            }
            else children.Add(BuildLifeHoldingWorkspace());
        }
        return Ui.Column(children.ToArray()) with { Key = "life-content" };
    }

    private UiElement BuildBookSource() => Ui.FilterButtons(string.Empty, _bookSources, new[]
    {
        new TaiwuChoiceOption<byte>(1, "私人藏书"),
        new TaiwuChoiceOption<byte>(2, "背包持有"),
    }, compact: true) with { Key = "book-source" };

    private BookCatalog EnsureBookCatalog()
    {
        if (_bookCatalog != null)
            return _bookCatalog;

        CombatSkillItem[] combat = ((IEnumerable<CombatSkillItem>)Config.CombatSkill.Instance)
            // IsNonPublic describes teaching availability, not whether its book exists.
            // Filtering it hid the public half of every sect's catalogue.
            .Where(skill => skill.BookId >= 0)
            .ToArray();
        sbyte[] sectIds = combat.Select(skill => skill.SectId).Distinct().OrderBy(value => value).ToArray();
        var combatTypes = sectIds.ToDictionary(
            sectId => sectId,
            sectId => (IReadOnlyList<TaiwuChoiceOption<sbyte>>)combat
                .Where(skill => skill.SectId == sectId)
                .Select(skill => skill.Type)
                .Distinct()
                .OrderBy(value => value)
                .Select(type => new TaiwuChoiceOption<sbyte>(type,
                    Config.CombatSkillType.Instance.GetItem(type)?.Name ?? $"分类{type}"))
                .ToArray());
        var combatBooks = combat
            .GroupBy(skill => (skill.SectId, skill.Type))
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<CombatBookOption>)group
                    .OrderBy(skill => skill.OrderIdInSect)
                    .ThenBy(skill => skill.TemplateId)
                    .Select(skill => new CombatBookOption(skill.TemplateId, skill.Name, skill.OrderIdInSect))
                    .ToArray());

        Config.LifeSkillItem[] life = ((IEnumerable<Config.LifeSkillItem>)Config.LifeSkill.Instance)
            .Where(skill => skill.SkillBookId >= 0)
            .ToArray();
        sbyte[] lifeTypes = life.Select(skill => skill.Type).Distinct().OrderBy(value => value).ToArray();
        var lifeBooks = life
            .GroupBy(skill => skill.Type)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<Config.LifeSkillItem>)group
                    .OrderBy(skill => skill.TemplateId)
                    .ToArray());

        var outlineTypes = new List<(int Value, string Label)> { (-1, "不限") };
        string[] outlineShortNames = { "承", "和", "解", "异", "独" };
        int outlineIndex = 0;
        foreach (sbyte id in Config.SkillBreakOutlineEffect.Instance.GetAllKeys().OrderBy(value => value))
        {
            Config.SkillBreakOutlineEffectItem? item = Config.SkillBreakOutlineEffect.Instance.GetItem(id);
            if (item != null)
                outlineTypes.Add((id, outlineIndex < outlineShortNames.Length
                    ? outlineShortNames[outlineIndex++]
                    : $"总纲{id}"));
        }

        _bookCatalog = new BookCatalog
        {
            CombatSects = sectIds.Select(id => new TaiwuChoiceOption<sbyte>(id,
                Config.Organization.Instance.GetItem(id)?.Name ?? $"门派{id}")).ToArray(),
            CombatTypes = combatTypes,
            CombatBooks = combatBooks,
            LifeTypes = lifeTypes.Select(id => new TaiwuChoiceOption<sbyte>(id,
                Config.LifeSkillType.Instance.GetItem(id)?.Name ?? $"技艺{id}")).ToArray(),
            LifeBooks = lifeBooks,
            OutlineTypes = outlineTypes,
        };
        return _bookCatalog;
    }

    private UiElement BuildCombatSelectors()
    {
        return Ui.PopupCard("功法",
            new TaiwuPopupCardModel(CombatSelectionSummary, BuildCombatCardFields),
            new TaiwuPopupCardOptions
            {
                TriggerStyle = TaiwuPopupCardTriggerStyle.FilterOption,
                Width = CompactTriggerWidth("功法 · " + CombatSelectionSummary(), 220f, 460f),
                PopupWidth = 680f,
                PopupHeight = 300f,
                MaximumPopupHeight = 620f,
            }) with { Key = "combat-selectors" };
    }

    private IReadOnlyList<TaiwuPopupCardField> BuildCombatCardFields()
    {
        BookCatalog catalog = EnsureBookCatalog();
        IReadOnlyList<TaiwuChoiceOption<sbyte>> types = _combatSect >= 0 &&
            catalog.CombatTypes.TryGetValue(_combatSect, out var cachedTypes)
            ? cachedTypes : Array.Empty<TaiwuChoiceOption<sbyte>>();
        IReadOnlyList<CombatBookOption> skills = _combatSect >= 0 && _combatType >= 0 &&
            catalog.CombatBooks.TryGetValue((_combatSect, _combatType), out var cachedBooks)
            ? cachedBooks : Array.Empty<CombatBookOption>();

        return new TaiwuPopupCardField[]
        {
            new(
                "门派",
                ChoiceLabel(catalog.CombatSects, _combatSect, "请选择门派"),
                catalog.CombatSects.Select(item => new TaiwuPopupCardOption(
                    item.Label, item.Value == _combatSect)).ToArray(),
                index =>
                {
                    _combatSect = catalog.CombatSects[index].Value;
                    _combatType = -1;
                    _combatSkill = -1;
                    MarkBookStale(0);
                }),
            new(
                "功法分类",
                _combatSect < 0 ? "请先选择门派" : ChoiceLabel(types, _combatType, "请选择分类"),
                types.Select(item => new TaiwuPopupCardOption(
                    item.Label, item.Value == _combatType)).ToArray(),
                index =>
                {
                    _combatType = types[index].Value;
                    _combatSkill = -1;
                    MarkBookStale(0);
                },
                Interactable: _combatSect >= 0 && types.Count > 0),
            new(
                "具体功法",
                _combatType < 0 ? "请先选择分类" : ChoiceLabel(skills, _combatSkill, "请选择功法"),
                skills.Select(item => new TaiwuPopupCardOption(
                    item.Name, item.TemplateId == _combatSkill)).ToArray(),
                index =>
                {
                    _combatSkill = skills[index].TemplateId;
                    MarkBookStale(0);
                    RefreshActivePage();
                },
                Interactable: _combatType >= 0 && skills.Count > 0,
                CloseCardAfterSelect: true),
        };
    }

    private string CombatSelectionSummary()
    {
        BookCatalog catalog = EnsureBookCatalog();
        string sect = ChoiceLabel(catalog.CombatSects, _combatSect, "请选择");
        if (_combatSect < 0) return sect;
        IReadOnlyList<TaiwuChoiceOption<sbyte>> types = catalog.CombatTypes.TryGetValue(_combatSect, out var cachedTypes)
            ? cachedTypes : Array.Empty<TaiwuChoiceOption<sbyte>>();
        string type = ChoiceLabel(types, _combatType, "请选择");
        if (_combatType < 0) return sect + " / " + type;
        IReadOnlyList<CombatBookOption> skills = catalog.CombatBooks.TryGetValue((_combatSect, _combatType), out var cachedBooks)
            ? cachedBooks : Array.Empty<CombatBookOption>();
        return sect + " / " + type + " / " + ChoiceLabel(skills, _combatSkill, "请选择");
    }

    private static float CompactTriggerWidth(string text, float minimum, float maximum) =>
        Math.Clamp(text.Length * 26f + 32f, minimum, maximum);

    private UiElement BuildLifeSelectors()
    {
        BookCatalog catalog = EnsureBookCatalog();
        var elements = new List<UiElement>
        {
            Ui.Row(
                Ui.Flex(Ui.FilterButtons("技艺分类", Selection(_lifeType, value =>
                {
                    _lifeType = value; _lifeSkill = -1; MarkBookStale(1); RefreshActivePage();
                }), catalog.LifeTypes, compact: true)),
                BuildBookSource()) with { Key = "life-type-row" },
        };
        if (_lifeType < 0) return Ui.Column(elements.ToArray()) with { Key = "life-selectors" };
        IReadOnlyList<Config.LifeSkillItem> typed = catalog.LifeBooks.TryGetValue(_lifeType, out var cachedLifeBooks)
            ? cachedLifeBooks
            : Array.Empty<Config.LifeSkillItem>();
        Config.LifeSkillItem[] skills = typed
            .OrderBy(skill => skill.Grade)
            .ThenBy(skill => skill.TemplateId)
            .ToArray();
        elements.Add(Ui.FilterButtons("具体技艺书", Selection(_lifeSkill, value =>
        {
            _lifeSkill = value; MarkBookStale(1); RefreshActivePage();
        }), skills.Select(skill => new TaiwuChoiceOption<short>(skill.TemplateId,
            $"{skill.Name}·{GradeName(skill.Grade)}")).ToArray(), compact: true)
            with { Key = "life-skill" });
        return Ui.Column(elements.ToArray()) with { Key = "life-selectors" };
    }

    private UiElement BuildCombatHoldingWorkspace()
    {
        if (_combatHoldings == null)
            return Ui.Spacer();
        if (!_combatHoldings.Success)
            return Ui.Column(
                Ui.Muted(_combatHoldings.Message),
                ActionRow(LoadCombatHoldings, ResetCombat, "重新读取持有情况"));

        IReadOnlyList<BookHolderView> holders = _combatHoldings.Holders;
        IReadOnlyList<PageTargetChoice> targets = _combatTypes
            .Select((type, page) => new PageTargetChoice(type, _combatStates[page]))
            .ToArray();
        IReadOnlyList<BookHolderSet> sets = BookHoldingWorkspace.FindHolderSets(holders, targets, combat: true);

        var left = new List<UiElement>
        {
            Ui.Muted($"{holders.Count} 人持有此书。点击实际存在的书页状态；默认优先选择持有人最多的完整状态。"),
        };
        if (TaiwuPageMarking.HasAnyMark(_combatTaiwuKnowledge))
            left.Add(Ui.Muted("绿色背景 = 太吾已有或已读的书页，无需再寻。"));
        for (int page = 0; page < BookHoldingWorkspace.CombatPageCount; page++)
            left.Add(BuildCombatHoldingPagePicker(holders, page));

        return BuildHoldingWorkspace(
            Ui.Column(left.ToArray()) with { Key = "combat-page-picker-list" },
            sets, LoadCombatHoldings, ResetCombat, "combat");
    }

    private UiElement BuildLifeHoldingWorkspace()
    {
        if (_lifeHoldings == null)
            return Ui.Spacer();
        if (!_lifeHoldings.Success)
            return Ui.Column(
                Ui.Muted(_lifeHoldings.Message),
                ActionRow(LoadLifeHoldings, ResetLife, "重新读取持有情况"));

        IReadOnlyList<BookHolderView> holders = _lifeHoldings.Holders;
        IReadOnlyList<PageTargetChoice> targets = _lifeStates
            .Select(state => new PageTargetChoice(-1, state))
            .ToArray();
        IReadOnlyList<BookHolderSet> sets = BookHoldingWorkspace.FindHolderSets(holders, targets, combat: false);

        var left = new List<UiElement>
        {
            Ui.Muted($"{holders.Count} 人持有此书。点击实际存在的书页状态；默认优先选择持有人最多的完整状态。"),
        };
        if (TaiwuPageMarking.HasAnyMark(_lifeTaiwuKnowledge))
            left.Add(Ui.Muted("绿色背景 = 太吾已有或已读的书页，无需再寻。"));
        // Life pickers carry only 2-3 options each: pair them to keep the pane compact.
        for (int page = 0; page < BookHoldingWorkspace.LifePageCount; page += 2)
        {
            UiElement first = BuildLifeHoldingPagePicker(holders, page);
            left.Add(page + 1 < BookHoldingWorkspace.LifePageCount
                ? Ui.Row(Ui.Flex(first), Ui.Flex(BuildLifeHoldingPagePicker(holders, page + 1)))
                    with { Key = $"life-holding-page-row-{page}" }
                : first);
        }

        return BuildHoldingWorkspace(
            Ui.Column(left.ToArray()) with { Key = "life-page-picker-list" },
            sets, LoadLifeHoldings, ResetLife, "life");
    }

    private UiElement BuildHoldingWorkspace(
        UiElement pagePane,
        IReadOnlyList<BookHolderSet> sets,
        Action reload,
        Action reset,
        string key)
    {
        var right = new List<UiElement>();
        // Render in pages: mounting all capped sets at once creates well over a
        // thousand UI objects and noticeably drags the frame rate down.
        IReadOnlyList<BookHolderSet> visible = sets.Take(_holderSetRenderLimit).ToArray();
        int previousCount = -1;
        foreach (BookHolderSet set in visible)
        {
            int count = set.Holders.Count;
            if (count != previousCount)
            {
                int countAtThisSize = sets.Count(item => item.Holders.Count == count);
                right.Add(Ui.Heading($"{count} 人组合 · {countAtThisSize} 套"));
                right.Add(Ui.Divider());
                previousCount = count;
            }
            right.Add(BuildHolderSetRow(set, key));
        }
        if (sets.Count > visible.Count)
        {
            int more = Math.Min(HolderSetRenderPageSize, sets.Count - visible.Count);
            right.Add(Ui.Button($"再显示 {more} 套（已显示 {visible.Count}/{sets.Count}）",
                () =>
                {
                    _holderSetRenderLimit += HolderSetRenderPageSize;
                    RefreshActivePage();
                },
                new TaiwuButtonOptions { Width = 360f, Style = TaiwuButtonStyle.Secondary }));
        }
        if (sets.Count == BookHoldingWorkspace.MaxRenderedSets)
            right.Add(Ui.Muted($"组合较多，当前仅计算前 {BookHoldingWorkspace.MaxRenderedSets} 套。"));

        // Header stays outside the scroll so the reload/reset actions and the set
        // count remain visible while scrolling through combinations.
        UiElement holderPane = Ui.Column(
            Ui.Row(
                Ui.Heading("需要寻访的人"),
                Ui.Flex(Ui.Spacer(0)),
                Ui.Button("重新读取", reload,
                    new TaiwuButtonOptions { Width = 200f, Style = TaiwuButtonStyle.Secondary }),
                Ui.ResetIcon(reset)) with { Key = key + "-holding-header" },
            Ui.Muted(sets.Count == 0
                ? "当前目标没有可覆盖的持有人组合。"
                : $"共 {sets.Count} 套组合，按人数由少到多排序。"),
            Ui.Scroll(Ui.Column(right.ToArray()) with { Key = key + "-holder-set-list" },
                new TaiwuScrollOptions { Height = 760f, ShowBackground = true })
                with { Key = key + "-holder-set-scroll" })
            with { Key = key + "-holder-pane" };
        UiElement columns = Ui.Row(Ui.Flex(pagePane, 0.92f), Ui.Flex(holderPane, 1.08f)) with
        {
            Key = key + "-holding-columns",
        };

        return Ui.Column(columns) with { Key = key + "-holding-workspace" };
    }

    // Wildcard page target ("不限"): the page is covered by any holder.
    private static readonly PageTargetChoice AnyPageTarget = new(-1, -1);

    private UiElement BuildCombatHoldingPagePicker(IReadOnlyList<BookHolderView> holders, int page)
    {
        IReadOnlyList<BookPageAvailability> availability =
            BookHoldingWorkspace.GetPageAvailability(holders, page, combat: true);
        PageTargetChoice selected = new(_combatTypes[page], _combatStates[page]);
        string title = page == 0 ? "总纲" : $"第{page}页";
        if (availability.Count == 0)
            return Ui.Column(Ui.Heading(title), Ui.Muted("无可用书页"));

        var options = new List<TaiwuChoiceOption<PageTargetChoice>>
        {
            new(AnyPageTarget, $"不限 {holders.Count}"),
        };
        options.AddRange(availability.Select(item => new TaiwuChoiceOption<PageTargetChoice>(
            item.Target, CompactPageTargetLabel(page, item.Target) + $" {item.HolderCount}",
            Tone: PageTone(item.Target.State),
            Highlighted: TaiwuPageMarking.IsVariantCoveredByTaiwu(_combatTaiwuKnowledge, page, item.Target))));
        return Ui.FilterButtons(title, Selection(selected, value =>
        {
            _combatTypes[page] = value.Type;
            _combatStates[page] = value.State;
            RefreshActivePage();
        }), options.ToArray(), compact: true) with { Key = $"combat-holding-page-{page}" };
    }

    private UiElement BuildLifeHoldingPagePicker(IReadOnlyList<BookHolderView> holders, int page)
    {
        IReadOnlyList<BookPageAvailability> availability =
            BookHoldingWorkspace.GetPageAvailability(holders, page, combat: false);
        PageTargetChoice selected = new(-1, _lifeStates[page]);
        string title = $"第{page + 1}页";
        if (availability.Count == 0)
            return Ui.Column(Ui.Heading(title), Ui.Muted("无可用书页"));

        var options = new List<TaiwuChoiceOption<PageTargetChoice>>
        {
            new(AnyPageTarget, $"不限 {holders.Count}"),
        };
        options.AddRange(availability.Select(item => new TaiwuChoiceOption<PageTargetChoice>(
            item.Target, LifePageTargetLabel(item.Target.State) + $" {item.HolderCount}",
            Tone: PageTone(item.Target.State),
            Highlighted: TaiwuPageMarking.IsVariantCoveredByTaiwu(_lifeTaiwuKnowledge, page, item.Target))));
        return Ui.FilterButtons(title, Selection(selected, value =>
        {
            _lifeStates[page] = value.State;
            RefreshActivePage();
        }), options.ToArray(), compact: true) with { Key = $"life-holding-page-{page}" };
    }

    private static string LifePageTargetLabel(sbyte state) =>
        state == 0 ? "完" : state == 1 ? "残" : "佚";

    private string CompactPageTargetLabel(int page, PageTargetChoice target)
    {
        string type = page == 0
            ? EnsureBookCatalog().OutlineTypes.FirstOrDefault(item => item.Value == target.Type).Label ?? $"纲{target.Type}"
            : target.Type == 0 ? "正" : target.Type == 1 ? "逆" : "?";
        string state = target.State == 0 ? "完" : target.State == 1 ? "残" : "佚";
        return $"{type}·{state}";
    }

    private UiElement BuildHolderSetRow(BookHolderSet set, string key)
    {
        UiElement summary = Ui.Column(
            Ui.Text(string.Join("、", set.Holders.Select(holder => holder.Name))),
            Ui.Muted(string.Join(" · ", set.Holders.Select(holder =>
                $"{holder.Organization}／地格 {holder.BlockId}"))),
            Ui.Spacer(6f),
            Ui.Divider());

        if (!CanMarkSelectedArea)
            return summary with { Key = key + "-holder-set-" + set.Key };

        return Ui.Row(
            Ui.Flex(summary),
            Ui.Button("标记地格", () => MarkHolderSet(set),
                new TaiwuButtonOptions { Width = 156f, Style = TaiwuButtonStyle.Secondary })) with
        {
            Key = key + "-holder-set-" + set.Key,
        };
    }

    private void MarkHolderSet(BookHolderSet set)
    {
        if (!CanMarkSelectedArea)
        {
            SetStatus("仅能标记太吾当前所在地域的地格。");
            return;
        }

        List<Location> locations = set.Holders
            .Where(holder => holder.AreaId == _selectedAreaId && holder.BlockId >= 0)
            .Select(holder => new Location(holder.AreaId, holder.BlockId))
            .Distinct()
            .ToList();
        if (locations.Count == 0)
        {
            SetStatus("这套组合没有可标记的地格。");
            return;
        }
        TryMarkLocations(locations);
    }

    private void TryMarkLocations(List<Location> locations)
    {
        try
        {
            WorldMapModel map = SingletonObject.getInstance<WorldMapModel>();
            if (map.CurrentAreaId != _selectedAreaId)
            {
                SetStatus("太吾已不在当前查询地域，未标记地格。");
                return;
            }
            MapMarkTracker.ReplaceMarks(map, locations);
            Close();
        }
        catch (Exception exception)
        {
            SetStatus("标记地格失败：" + exception.Message);
        }
    }

    private static TaiwuChoiceTone PageTone(sbyte state) => state == 0
        ? TaiwuChoiceTone.Complete
        : state == 1 ? TaiwuChoiceTone.Incomplete : TaiwuChoiceTone.Lost;

    private UiElement BuildPersonPage()
    {
        // Keep the results fragment consistent with the response whenever the
        // whole page is rebuilt; live search updates use SetValue instead.
        _personResultsContent.SetValueWithoutNotify(BuildPersonResults());
        var filterRows = new List<UiElement>
        {
            Ui.Row(Ui.SearchInput(_personName, "输入人物姓名", 420f),
                Ui.FilterButtons(string.Empty, _personGenders, new[]
                {
                    new TaiwuChoiceOption<sbyte>(1, "男"), new TaiwuChoiceOption<sbyte>(0, "女"),
                }, true),
                Ui.Flex(Ui.Spacer(0)),
                Ui.ResetIcon(ResetPeople)) with { Key = "person-top" },
            Ui.FilterButtons("身份品级", _personGrades,
                Enumerable.Range(0, 9).Select(value => new TaiwuChoiceOption<sbyte>((sbyte)value,
                    GradeName((sbyte)value))).ToArray(), true) with { Key = "person-grades" },
            Ui.Row(BuildAbilityCard(0), BuildAbilityCard(1), BuildAbilityCard(2))
                with { Key = "person-abilities" },
        };
        filterRows.Add(Ui.Dynamic(_personResultsContent, 800f) with { Key = "person-results-dynamic" });
        return Ui.Column(filterRows.ToArray()) with { Key = "person-content" };
    }

    private UiElement BuildAbilityCard(int index) => Ui.PopupCard(string.Empty,
        new TaiwuPopupCardModel(() => AbilitySummary(index), () => BuildAbilityCardFields(index)),
        new TaiwuPopupCardOptions
        {
            Title = $"条件 {index + 1}（同时满足）",
            TriggerStyle = TaiwuPopupCardTriggerStyle.FilterOption,
            Width = 360f,
            PopupWidth = 660f,
            PopupHeight = 470f,
            MaximumPopupHeight = 640f,
        }) with { Key = $"ability-{index}" };

    private string AbilitySummary(int index)
    {
        AbilityFilterState filter = _abilityFilters[index];
        if (!filter.Enabled.Value) return $"条件 {index + 1} · 未启用";
        string skill = Config.LifeSkillType.Instance.GetItem(filter.LifeSkillType)?.Name
            ?? $"技艺{filter.LifeSkillType}";
        return $"条件 {index + 1} · {skill}{(filter.Metric == 0 ? "资质" : "造诣")}≥{filter.Minimum.Value:0}";
    }

    private IReadOnlyList<TaiwuPopupCardField> BuildAbilityCardFields(int index)
    {
        AbilityFilterState filter = _abilityFilters[index];
        bool enabled = filter.Enabled.Value;
        Config.LifeSkillTypeItem[] types = ((IEnumerable<Config.LifeSkillTypeItem>)Config.LifeSkillType.Instance)
            .OrderBy(item => item.TemplateId).ToArray();
        int[] thresholds = { 40, 60, 70, 80, 90, 100, 120, 150, 200 };
        int current = (int)Math.Round(filter.Minimum.Value);
        return new TaiwuPopupCardField[]
        {
            new("开关", enabled ? "启用" : "停用",
                new[]
                {
                    new TaiwuPopupCardOption("启用", enabled),
                    new TaiwuPopupCardOption("停用", !enabled),
                },
                option => filter.Enabled.SetValue(option == 0)),
            new("技艺", types.FirstOrDefault(item => item.TemplateId == filter.LifeSkillType)?.Name ?? string.Empty,
                types.Select(item => new TaiwuPopupCardOption(
                    item.Name, item.TemplateId == filter.LifeSkillType)).ToArray(),
                option =>
                {
                    filter.LifeSkillType = types[option].TemplateId;
                    SchedulePersonSearch();
                },
                Interactable: enabled),
            new("指标", filter.Metric == 0 ? "资质" : "造诣",
                new[]
                {
                    new TaiwuPopupCardOption("资质", filter.Metric == 0),
                    new TaiwuPopupCardOption("造诣", filter.Metric == 1),
                },
                option =>
                {
                    filter.Metric = (sbyte)option;
                    SchedulePersonSearch();
                },
                Interactable: enabled),
            new("至少", current.ToString(),
                thresholds.Select(value => new TaiwuPopupCardOption(
                    value.ToString(), value == current)).ToArray(),
                option => filter.Minimum.SetValue(thresholds[option]),
                Interactable: enabled),
        };
    }

    private UiElement BuildPersonResults()
    {
        if (_personResponse == null)
            return Ui.Muted("设置至少一个筛选条件后自动查找。") with { Key = "person-empty" };
        if (!_personResponse.Success)
            return Ui.Muted(_personResponse.Message) with { Key = "person-error" };
        _personTable.SetItems(_personResponse.People);
        PersonRowView? selected = Selected(_personTable);
        var columns = new List<TaiwuTableColumn<PersonRowView>>
        {
            new("name", "姓名", row => row.Name, 260f, true, row => row.Name),
            new("grade", "品级", row => GradeName(row.Grade), 150f, true, row => row.Grade),
            new("organization", "组织", row => row.Organization, 250f, true, row => row.Organization),
            new("age", "年龄", row => row.Age.ToString(), 120f, true, row => row.Age),
        };
        foreach (AbilityFilterState filter in _abilityFilters.Where(item => item.Enabled.Value))
        {
            sbyte type = filter.LifeSkillType;
            sbyte metric = filter.Metric;
            string header = (Config.LifeSkillType.Instance.GetItem(type)?.Name ?? $"技艺{type}") +
                (metric == 0 ? "资质" : "造诣");
            columns.Add(new TaiwuTableColumn<PersonRowView>($"ability-{type}-{metric}", header,
                row => AbilityDisplay(row.Abilities.FirstOrDefault(value =>
                    value.LifeSkillType == type && value.Metric == metric)),
                190f, true, row => row.Abilities.FirstOrDefault(value =>
                    value.LifeSkillType == type && value.Metric == metric)?.Total ?? -1));
        }
        string title = _personResponse.People.Count < _personResponse.TotalCount
            ? $"人物结果 · 已加载 {_personResponse.People.Count}/{_personResponse.TotalCount} 人"
            : $"人物结果 · {_personResponse.TotalCount} 人 · {_personResponse.ElapsedMs} ms";
        int remaining = _personResponse.TotalCount - _personResponse.People.Count;
        return Ui.Column(
            Ui.Row(
                Ui.Heading(title),
                Ui.Flex(Ui.Spacer(0)),
                remaining > 0
                    ? Ui.Button($"继续加载 {remaining} 人",
                        () => SearchPeople(_personResponse.Page + 1, append: true),
                        new TaiwuButtonOptions { Width = 260f, Style = TaiwuButtonStyle.Secondary })
                    : Ui.Spacer(0)) with { Key = "person-results-header" },
            Ui.Row(
                Ui.Flex(Ui.Table(_personTable, columns,
                    new TaiwuTableOptions { Height = 590f, RowHeight = 70f }), 2f),
                Ui.Flex(BuildPersonDetail(selected), 1f))) with { Key = "person-results" };
    }

    private UiElement BuildPersonDetail(PersonRowView? person)
    {
        if (person == null) return Ui.Muted("选择人物查看详情。");
        var rows = new List<UiElement>
        {
            Ui.Heading(person.Name),
            Ui.Text($"{person.Organization} · {GradeName(person.Grade)} · {person.Age} 岁 · 地格 {person.BlockId}"),
        };
        foreach (AbilityValueView ability in person.Abilities)
        {
            string skill = Config.LifeSkillType.Instance.GetItem(ability.LifeSkillType)?.Name ?? $"技艺{ability.LifeSkillType}";
            if (ability.Metric == 0)
            {
                string growth = ability.GrowthType == SkillQualificationGrowthType.Precocious ? "早熟" :
                    ability.GrowthType == SkillQualificationGrowthType.LateBlooming ? "晚成" : "均衡";
                rows.Add(Ui.Text($"{skill}资质 {ability.Total}（{ability.GrowthAdjust:+#;-#;0}）"));
                rows.Add(Ui.Muted($"基础 {ability.Base} · {growth}"));
            }
            else rows.Add(Ui.Text($"{skill}造诣 {ability.Total}"));
        }
        if (CanMarkSelectedArea && person.BlockId >= 0)
        {
            rows.Add(Ui.Spacer(8f));
            rows.Add(Ui.Row(
                Ui.Button("标记地格", () => MarkPersonLocation(person),
                    new TaiwuButtonOptions { Width = 200f, Style = TaiwuButtonStyle.Secondary }),
                Ui.Flex(Ui.Spacer(0))));
        }
        return Ui.Column(rows.ToArray());
    }

    private void MarkPersonLocation(PersonRowView person)
    {
        if (!CanMarkSelectedArea)
        {
            SetStatus("仅能标记太吾当前所在地域的地格。");
            return;
        }
        TryMarkLocations(new List<Location> { new(person.AreaId, person.BlockId) });
    }

    private UiElement BuildMerchantPage()
    {
        _merchantResultsContent.SetValueWithoutNotify(BuildMerchantResults());
        string[] guildNames = { "服牛帮", "文山书海阁", "五湖商会", "大武魁商号", "回春堂", "公输坊", "奇货斋" };
        var rows = new List<UiElement>
        {
            Ui.FilterButtons("类型", _merchantTargets, new[]
            {
                new TaiwuChoiceOption<sbyte>(0, "商人"),
                new TaiwuChoiceOption<sbyte>(1, "商队"),
                new TaiwuChoiceOption<sbyte>(2, "商会"),
            }, true),
            Ui.FilterButtons("商会类型", _merchantGuilds,
                guildNames.Select((name, index) => new TaiwuChoiceOption<sbyte>((sbyte)index, name)).ToArray(), true),
            Ui.FilterButtons("商会等级", _merchantLevels,
                Enumerable.Range(0, 7).Select(value => new TaiwuChoiceOption<sbyte>((sbyte)value,
                    $"{ChineseLevel(value + 1)}级")).ToArray(), true),
        };
        if (_merchantTargets.IsSelected(1))
            rows.Add(Ui.FilterButtons("商队状态", _caravanState, new[]
            {
                new TaiwuChoiceOption<sbyte>(0, "正常"), new TaiwuChoiceOption<sbyte>(1, "被劫"),
            }, true));
        rows.Add(Ui.Dynamic(_merchantResultsContent, 800f) with { Key = "merchant-results-dynamic" });
        return Ui.Column(rows.ToArray()) with { Key = "merchant-content" };
    }

    private UiElement BuildMerchantResults()
    {
        if (_merchantResponse == null)
            return Ui.Muted("变更筛选条件后自动查找。") with { Key = "merchant-empty" };
        if (!_merchantResponse.Success)
            return Ui.Muted(_merchantResponse.Message) with { Key = "merchant-error" };
        _merchantTable.SetItems(_merchantResponse.Rows);
        var columns = new[]
        {
            new TaiwuTableColumn<MerchantRowView>("name", "名称", row => row.Name, 400f, true, row => row.Name),
            new TaiwuTableColumn<MerchantRowView>("type", "类型", row => MerchantTypeName(row.TargetType),
                150f, true, row => row.TargetType),
            new TaiwuTableColumn<MerchantRowView>("guild", "商会", row => row.GuildName,
                280f, true, row => row.GuildType),
            new TaiwuTableColumn<MerchantRowView>("level", "等级", row => $"{ChineseLevel(row.Level + 1)}级",
                150f, true, row => row.Level),
            new TaiwuTableColumn<MerchantRowView>("state", "状态", row => row.TargetType == 1 ?
                (row.Robbed ? "被劫" : "正常") : "—", 140f, true, row => row.Robbed ? 1 : 0),
            new TaiwuTableColumn<MerchantRowView>("location", "所在地", row => $"地格 {row.BlockId}",
                180f, true, row => row.BlockId),
        };
        int remaining = _merchantResponse.TotalCount - _merchantResponse.Rows.Count;
        return Ui.Column(
            Ui.Row(
                Ui.Heading($"商会结果 · {_merchantResponse.TotalCount} 项 · {_merchantResponse.ElapsedMs} ms"),
                Ui.Flex(Ui.Spacer(0)),
                remaining > 0
                    ? Ui.Button($"继续加载 {remaining} 项",
                        () => SearchMerchants(_merchantResponse.Page + 1, append: true),
                        new TaiwuButtonOptions { Width = 260f, Style = TaiwuButtonStyle.Secondary })
                    : Ui.Spacer(0)) with { Key = "merchant-results-header" },
            Ui.Table(_merchantTable, columns,
                new TaiwuTableOptions { Height = 700f, RowHeight = 70f }))
            with { Key = "merchant-results" };
    }

    private void MarkMerchantLocation(MerchantRowView row)
    {
        if (!CanMarkSelectedArea)
        {
            SetStatus("仅能标记太吾当前所在地域的地格。");
            return;
        }
        TryMarkLocations(new List<Location> { new(row.AreaId, row.BlockId) });
    }

    private UiElement ActionRow(Action search, Action reset, string label) => Ui.Row(
        Ui.Button(label, search, new TaiwuButtonOptions { Width = 300f }),
        Ui.ResetIcon(reset)) with { Key = "actions-" + label };

    private void LoadCombatHoldings()
    {
        if (_selectedAreaId < 0 || _combatSkill < 0) return;
        if (!SupportsBookHoldingWorkspace)
        {
            SetStatus("书页组合功能需要重启游戏后才能使用。新版后端尚未载入。");
            RefreshActivePage();
            return;
        }
        byte sourceMask = 0;
        foreach (byte source in _bookSources.Selected) sourceMask |= source;
        if (sourceMask == 0)
        {
            SetStatus("请至少选择一种秘籍来源。");
            return;
        }

        SetStatus("正在读取该功法的持有人与书页状态……");
        int version = ++_combatHoldingsVersion;
        FinderBackendClient.GetBookHoldings(new BookHoldingsRequestView(
            _selectedAreaId, 0, _combatSkill, sourceMask), response =>
        {
            if (version != _combatHoldingsVersion || _window?.IsShowing != true) return;
            _combatHoldings = response;
            _combatTaiwuKnowledge = response.Success
                ? BookHoldingWorkspace.BuildTaiwuKnowledge(
                    response.TaiwuBooks, response.TaiwuReadingState, combat: true)
                : TaiwuBookKnowledge.Empty;
            _holderSetRenderLimit = HolderSetRenderPageSize;
            if (response.Success)
            {
                ApplyCombatHoldingDefaults(response.Holders);
                SetStatus($"已读取 {response.Holders.Count} 位持有人 · {response.ElapsedMs} ms");
            }
            else SetStatus(response.Message);
            RefreshActivePage();
        });
    }

    private void ApplyCombatHoldingDefaults(IReadOnlyList<BookHolderView> holders)
    {
        for (int page = 0; page < BookHoldingWorkspace.CombatPageCount; page++)
        {
            // 正/逆（总纲为任意总纲类型）都已读或已有完整页的书页无需寻访，默认"不限"。
            if (TaiwuPageMarking.IsPageFullyCoveredByTaiwu(_combatTaiwuKnowledge, page))
            {
                _combatTypes[page] = AnyPageTarget.Type;
                _combatStates[page] = AnyPageTarget.State;
                continue;
            }
            BookPageAvailability? preferred = BookHoldingWorkspace.GetPageAvailability(holders, page, combat: true)
                .Where(item => item.Target.State != 2)
                .OrderByDescending(item => item.HolderCount)
                .ThenBy(item => item.Target.State)
                .ThenBy(item => item.Target.Type)
                .FirstOrDefault();
            preferred ??= BookHoldingWorkspace.GetPageAvailability(holders, page, combat: true).FirstOrDefault();
            if (preferred != null)
            {
                _combatTypes[page] = preferred.Target.Type;
                _combatStates[page] = preferred.Target.State;
            }
        }
    }

    private void LoadLifeHoldings()
    {
        if (_selectedAreaId < 0 || _lifeSkill < 0) return;
        if (!SupportsBookHoldingWorkspace)
        {
            SetStatus("书页组合功能需要重启游戏后才能使用。新版后端尚未载入。");
            RefreshActivePage();
            return;
        }
        byte sourceMask = 0;
        foreach (byte source in _bookSources.Selected) sourceMask |= source;
        if (sourceMask == 0)
        {
            SetStatus("请至少选择一种秘籍来源。");
            return;
        }

        SetStatus("正在读取该技艺的持有人与书页状态……");
        int version = ++_lifeHoldingsVersion;
        FinderBackendClient.GetBookHoldings(new BookHoldingsRequestView(
            _selectedAreaId, 1, _lifeSkill, sourceMask), response =>
        {
            if (version != _lifeHoldingsVersion || _window?.IsShowing != true) return;
            _lifeHoldings = response;
            _lifeTaiwuKnowledge = response.Success
                ? BookHoldingWorkspace.BuildTaiwuKnowledge(
                    response.TaiwuBooks, response.TaiwuReadingState, combat: false)
                : TaiwuBookKnowledge.Empty;
            _holderSetRenderLimit = HolderSetRenderPageSize;
            if (response.Success)
            {
                ApplyLifeHoldingDefaults(response.Holders);
                SetStatus($"已读取 {response.Holders.Count} 位持有人 · {response.ElapsedMs} ms");
            }
            else SetStatus(response.Message);
            RefreshActivePage();
        });
    }

    private void ApplyLifeHoldingDefaults(IReadOnlyList<BookHolderView> holders)
    {
        for (int page = 0; page < BookHoldingWorkspace.LifePageCount; page++)
        {
            // 太吾已拥有或已读的书页无需寻访，默认"不限"。
            if (TaiwuPageMarking.IsPageFullyCoveredByTaiwu(_lifeTaiwuKnowledge, page))
            {
                _lifeStates[page] = AnyPageTarget.State;
                continue;
            }
            BookPageAvailability? preferred = BookHoldingWorkspace.GetPageAvailability(holders, page, combat: false)
                .Where(item => item.Target.State != 2)
                .OrderByDescending(item => item.HolderCount)
                .ThenBy(item => item.Target.State)
                .FirstOrDefault();
            preferred ??= BookHoldingWorkspace.GetPageAvailability(holders, page, combat: false).FirstOrDefault();
            if (preferred != null)
                _lifeStates[page] = preferred.Target.State;
        }
    }

    private void Update()
    {
        if (_window?.IsShowing != true)
        {
            _personSearchAt = -1f;
            _merchantSearchAt = -1f;
            return;
        }
        CheckWorldChanges();
        if (_personSearchAt >= 0f)
        {
            if (_activeTab != 2) _personSearchAt = -1f;
            else if (Time.unscaledTime >= _personSearchAt)
            {
                _personSearchAt = -1f;
                if (HasPersonFilter) SearchPeople();
                else
                {
                    // No filter set: stay on the hint instead of listing the whole area.
                    _personResponse = null;
                    _personResultsContent.SetValue(BuildPersonResults());
                }
            }
        }
        if (_merchantSearchAt >= 0f)
        {
            if (_activeTab != 3) _merchantSearchAt = -1f;
            else if (Time.unscaledTime >= _merchantSearchAt)
            {
                _merchantSearchAt = -1f;
                SearchMerchants();
            }
        }
    }

    // Filters auto-search: every change only re-arms the debounce timer, so
    // typing or dragging a slider coalesces into one backend query.
    private void SchedulePersonSearch() =>
        _personSearchAt = Time.unscaledTime + PersonSearchDebounceSeconds;

    // Re-polls the catalog while the window is open. A month advance changes NPC
    // holdings, abilities and caravan states, so all cached queries are dropped;
    // the selected area is kept. Taiwu moving areas only refreshes the catalog
    // (location marking depends on it) — per-area query results stay valid.
    private void CheckWorldChanges()
    {
        if (_catalog == null || _worldCheckInFlight) return;
        if (Time.unscaledTime < _nextWorldCheckAt) return;
        _nextWorldCheckAt = Time.unscaledTime + WorldCheckIntervalSeconds;
        _worldCheckInFlight = true;
        int version = _requestVersion;
        FinderBackendClient.GetCatalog(catalog =>
        {
            _worldCheckInFlight = false;
            if (!Accept(version) || !catalog.Success) return;
            bool dateChanged = _catalogDateTick > 0 && catalog.DateTick > 0
                && catalog.DateTick != _catalogDateTick;
            _catalog = catalog;
            if (catalog.DateTick > 0) _catalogDateTick = catalog.DateTick;
            if (!dateChanged) return;
            MarkAllStale();
            RefreshActivePage();
            if (_activeTab == 2) SchedulePersonSearch();
            if (_activeTab == 3) ScheduleMerchantSearch();
        });
    }

    private void ScheduleMerchantSearch() =>
        _merchantSearchAt = Time.unscaledTime + PersonSearchDebounceSeconds;

    // Person search requires at least one filter; without one the tab only
    // shows a hint. Any active filter auto-searches after a short debounce.
    private bool HasPersonFilter =>
        !string.IsNullOrWhiteSpace(_personName.Value) ||
        _personGrades.Selected.Count > 0 ||
        _personGenders.Selected.Count > 0 ||
        _abilityFilters.Any(filter => filter.Enabled.Value);

    private void SearchPeople() => SearchPeople(0, append: false);

    private void SearchPeople(int page, bool append)
    {
        if (_selectedAreaId < 0) return;
        int gradeMask = _personGrades.Selected.Aggregate(0, (mask, value) => mask | (1 << value));
        var abilities = _abilityFilters.Where(filter => filter.Enabled.Value)
            .Select(filter => new AbilityConditionView(filter.LifeSkillType, filter.Metric,
                checked((short)Math.Round(filter.Minimum.Value)))).ToArray();
        sbyte gender = _personGenders.Selected.Count == 0 ? (sbyte)-1 : _personGenders.Selected.First();
        var request = new PersonSearchRequestView(_selectedAreaId, _personName.Value, gradeMask,
            0, 200, gender,
            Array.Empty<sbyte>(), abilities, page, 200);
        SetStatus("正在查询人物……");
        int version = ++_requestVersion;
        FinderBackendClient.SearchPeople(request, response =>
        {
            if (!Accept(version)) return;
            if (append && _personResponse != null && response.Success)
                response = response with { People = _personResponse.People.Concat(response.People).ToArray() };
            _personResponse = response;
            _personTable.SetItems(response.People);
            SetStatus(response.Success ? $"找到 {response.TotalCount} 人 · {response.ElapsedMs} ms" : response.Message);
            _personResultsContent.SetValue(BuildPersonResults());
        });
    }

    private void SearchMerchants() => SearchMerchants(0, append: false);

    private void SearchMerchants(int page, bool append)
    {
        if (_selectedAreaId < 0) return;
        int targetMask = _merchantTargets.Selected.Aggregate(0, (mask, value) => mask | (1 << value));
        int guildMask = _merchantGuilds.Selected.Aggregate(0, (mask, value) => mask | (1 << value));
        int levelMask = _merchantLevels.Selected.Aggregate(0, (mask, value) => mask | (1 << value));
        sbyte caravanState = _caravanState.Selected.Count == 0 ? (sbyte)0 : _caravanState.Selected.First();
        SetStatus("正在查询商会目标……");
        int version = ++_requestVersion;
        FinderBackendClient.SearchMerchants(new MerchantSearchRequestView(_selectedAreaId,
            targetMask, guildMask, levelMask, caravanState, page, 100), response =>
        {
            if (!Accept(version)) return;
            if (append && _merchantResponse != null && response.Success)
                response = response with { Rows = _merchantResponse.Rows.Concat(response.Rows).ToArray() };
            _merchantResponse = response;
            _merchantTable.SetItems(response.Rows);
            SetStatus(response.Success ? $"找到 {response.TotalCount} 项 · {response.ElapsedMs} ms" : response.Message);
            _merchantResultsContent.SetValue(BuildMerchantResults());
        });
    }

    private void ResetCombat()
    {
        _combatSect = _combatType = -1; _combatSkill = -1;
        for (int i = 0; i < 6; i++) { _combatStates[i] = 0; _combatTypes[i] = -1; }
        _combatHoldings = null; _combatTaiwuKnowledge = TaiwuBookKnowledge.Empty; RefreshActivePage();
    }

    private void ResetLife()
    {
        _lifeType = -1; _lifeSkill = -1;
        for (int i = 0; i < 5; i++) _lifeStates[i] = 0;
        _lifeHoldings = null; _lifeTaiwuKnowledge = TaiwuBookKnowledge.Empty; RefreshActivePage();
    }

    private void ResetPeople()
    {
        _personName.SetValueWithoutNotify(string.Empty); _personGrades.Clear(); _personGenders.Clear();
        foreach (AbilityFilterState filter in _abilityFilters)
        {
            filter.Enabled.SetValueWithoutNotify(false); filter.LifeSkillType = 0; filter.Metric = 0;
            filter.Minimum.SetValueWithoutNotify(80);
        }
        _personResponse = null; _personTable.SetItems(Array.Empty<PersonRowView>()); RefreshActivePage();
    }

    private void SelectArea(short areaId, bool markStale = true, bool refresh = true)
    {
        if (_catalog == null) return;
        AreaOptionView? area = _catalog.Areas.FirstOrDefault(item => item.AreaId == areaId);
        if (area == null) return;
        _selectedAreaId = area.AreaId; _areaCategory = area.Category;
        if (markStale) MarkAllStale();
        if (markStale && _activeTab == 2) SchedulePersonSearch();
        if (markStale && _activeTab == 3) ScheduleMerchantSearch();
        if (markStale && refresh)
        {
            RefreshActivePage();
            return;
        }
        if (refresh) RefreshActivePage();
    }

    private void MarkAllStale()
    {
        // Invalidate in-flight searches so a response for the previous area
        // cannot land after the area changed.
        _requestVersion++;
        _combatHoldings = null; _lifeHoldings = null; _personResponse = null; _merchantResponse = null;
        _combatTaiwuKnowledge = TaiwuBookKnowledge.Empty; _lifeTaiwuKnowledge = TaiwuBookKnowledge.Empty;
        _personTable.SetItems(Array.Empty<PersonRowView>());
        _merchantTable.SetItems(Array.Empty<MerchantRowView>());
        ClearStatus();
    }

    private void MarkBookStale(sbyte kind)
    {
        if (kind == 0)
        {
            _combatHoldings = null;
            _combatTaiwuKnowledge = TaiwuBookKnowledge.Empty;
        }
        else
        {
            _lifeHoldings = null;
            _lifeTaiwuKnowledge = TaiwuBookKnowledge.Empty;
        }
    }

    private void Render()
    {
        UiWindow document = BuildDocument();
        if (_window == null) _window = TaiwuUiApi.Mount(document);
        else _window.Render(document);
    }

    private UiElement BuildActiveTabPage() => _activeTab switch
    {
        0 => BuildTabPage(BuildCombatPage(), "combat"),
        1 => BuildTabPage(BuildLifePage(), "life"),
        2 => BuildTabPage(BuildPersonPage(), "person"),
        3 => BuildTabPage(BuildMerchantPage(), "merchant"),
        _ => Ui.Spacer(),
    };

    private void RefreshActivePage()
    {
        UiElement content = BuildActiveTabPage();
        if (_window == null)
            _tabContent[_activeTab].SetValueWithoutNotify(content);
        else
            _tabContent[_activeTab].SetValue(content);
    }

    private void SetStatus(string status)
    {
        _status = status;
        _statusContent.SetValue(Ui.Muted(status) with { Key = "status-text" });
    }

    private void ClearStatus()
    {
        _status = string.Empty;
        _statusContent.SetValue(Ui.Spacer() with { Key = "status-empty" });
    }

    private string AreaName(short areaId) =>
        _catalog?.Areas.FirstOrDefault(item => item.AreaId == areaId)?.Name ?? $"地域{areaId}";

    private bool SupportsBookHoldingWorkspace => _catalog?.ApiVersion >= 2;

    private bool CanMarkSelectedArea => _catalog?.CurrentAreaId == _selectedAreaId;

    private static TaiwuSelection<T> Selection<T>(T selected, Action<T> changed)
    {
        var selection = new TaiwuSelection<T>(TaiwuSelectionMode.Single, new[] { selected });
        selection.SelectionChanged += values =>
        {
            if (values.Count > 0) changed(values.First());
        };
        return selection;
    }

    private static string ChoiceLabel<T>(
        IReadOnlyList<TaiwuChoiceOption<T>> options,
        T selected,
        string fallback)
    {
        TaiwuChoiceOption<T>? choice = options.FirstOrDefault(item =>
            EqualityComparer<T>.Default.Equals(item.Value, selected));
        return choice?.Label ?? fallback;
    }

    private static string ChoiceLabel(
        IReadOnlyList<CombatBookOption> options,
        short selected,
        string fallback) =>
        options.FirstOrDefault(item => item.TemplateId == selected)?.Name ?? fallback;

    private static TRow? Selected<TRow, TKey>(TaiwuTableModel<TRow, TKey> model) where TKey : notnull
    {
        if (model.Selection.Selected.Count == 0) return default;
        TKey key = model.Selection.Selected.First();
        return model.Items.FirstOrDefault(row => EqualityComparer<TKey>.Default.Equals(model.RowKey(row), key));
    }

    private static string GradeName(sbyte grade) =>
        grade is >= 0 and <= 8 ? LocalStringManager.Get($"LK_ShortGrade_{grade}") : $"{grade}品";

    private static string AbilityDisplay(AbilityValueView? ability) => ability == null ? "—" :
        ability.Metric == 0 ? $"{ability.Total}（{ability.GrowthAdjust:+#;-#;0}）" : ability.Total.ToString();

    private static string MerchantTypeName(sbyte type) => type == 0 ? "商人" : type == 1 ? "商队" : "商会";

    private static string ChineseLevel(int level) => level switch
    {
        1 => "一", 2 => "二", 3 => "三", 4 => "四", 5 => "五", 6 => "六", 7 => "七", _ => level.ToString(),
    };
}
