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
    private sealed class CombatAreaHoldings
    {
        internal CombatAreaHoldings(short areaId, BookHoldingsResponse holdings)
        {
            AreaId = areaId;
            Holdings = holdings;
        }

        internal short AreaId { get; }
        internal BookHoldingsResponse Holdings { get; }
        internal sbyte[] States { get; } = new sbyte[BookHoldingWorkspace.CombatPageCount];
        internal sbyte[] Types { get; } = Enumerable.Repeat((sbyte)-1, BookHoldingWorkspace.CombatPageCount).ToArray();
    }

    private sealed class LifeAreaHoldings
    {
        internal LifeAreaHoldings(short areaId, BookHoldingsResponse holdings)
        {
            AreaId = areaId;
            Holdings = holdings;
        }

        internal short AreaId { get; }
        internal BookHoldingsResponse Holdings { get; }
        internal sbyte[] States { get; } = new sbyte[BookHoldingWorkspace.LifePageCount];
    }

    private sealed class PersonAreaResults
    {
        internal PersonAreaResults(short areaId, PersonSearchResponse response)
        {
            AreaId = areaId;
            Response = response;
        }

        internal short AreaId { get; }
        internal PersonSearchResponse Response { get; set; }
    }

    private sealed class MerchantAreaResults
    {
        internal MerchantAreaResults(short areaId, MerchantSearchResponse response)
        {
            AreaId = areaId;
            Response = response;
        }

        internal short AreaId { get; }
        internal MerchantSearchResponse Response { get; set; }
    }

    private sealed class RenxiaAreaResults
    {
        internal RenxiaAreaResults(short areaId, RenxiaSearchResponse response)
        {
            AreaId = areaId;
            Response = response;
        }

        internal short AreaId { get; }
        internal RenxiaSearchResponse Response { get; }
    }

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
        new(Ui.Spacer()), new(Ui.Spacer()), new(Ui.Spacer()), new(Ui.Spacer()), new(Ui.Spacer()),
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
    private IReadOnlyList<CombatAreaHoldings> _combatAreaHoldings = Array.Empty<CombatAreaHoldings>();
    private readonly TaiwuSelection<short> _combatAreaTabs = new(TaiwuSelectionMode.Single);
    // A completed search (including an empty result) remains reusable until
    // the book/source changes, the player changes area, or the game advances a month.
    private bool _combatSearchCacheValid;
    private bool _combatSearchInFlight;
    private int _combatSearchCompleted;
    private int _combatSearchTotal;

    private sbyte _lifeType = -1;
    private short _lifeSkill = -1;
    private IReadOnlyList<LifeAreaHoldings> _lifeAreaHoldings = Array.Empty<LifeAreaHoldings>();
    private readonly TaiwuSelection<short> _lifeAreaTabs = new(TaiwuSelectionMode.Single);
    private bool _lifeSearchCacheValid;
    private bool _lifeSearchInFlight;
    private int _lifeSearchCompleted;
    private int _lifeSearchTotal;
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
    private IReadOnlyList<PersonAreaResults> _personAreaResults = Array.Empty<PersonAreaResults>();
    private readonly TaiwuSelection<short> _personAreaTabs = new(TaiwuSelectionMode.Single);
    private bool _personSearchCacheValid;
    private bool _personSearchInFlight;
    private int _personSearchCompleted;
    private int _personSearchTotal;
    private int _personSearchVersion;
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
    private IReadOnlyList<MerchantAreaResults> _merchantAreaResults = Array.Empty<MerchantAreaResults>();
    private readonly TaiwuSelection<short> _merchantAreaTabs = new(TaiwuSelectionMode.Single);
    private bool _merchantSearchCacheValid;
    private bool _merchantSearchInFlight;
    private int _merchantSearchCompleted;
    private int _merchantSearchTotal;
    private int _merchantSearchVersion;
    private readonly TaiwuTableModel<MerchantRowView, string> _merchantTable =
        new(row => $"{row.TargetType}:{row.EntityId}");
    // Keep merchant results in their own dynamic host so filter controls stay
    // mounted while an asynchronous query completes.
    private readonly TaiwuValue<UiElement> _merchantResultsContent =
        new(Ui.Muted("变更筛选条件后自动寻找。"));
    private float _merchantSearchAt = -1f;

    private readonly TaiwuSelection<sbyte> _renxiaGrades = new(TaiwuSelectionMode.Multiple);
    private IReadOnlyList<RenxiaAreaResults> _renxiaAreaResults = Array.Empty<RenxiaAreaResults>();
    private readonly TaiwuSelection<short> _renxiaAreaTabs = new(TaiwuSelectionMode.Single);
    private bool _renxiaSearchCacheValid;
    private bool _renxiaSearchInFlight;
    private int _renxiaSearchCompleted;
    private int _renxiaSearchTotal;
    private int _renxiaSearchVersion;
    private readonly TaiwuTableModel<RenxiaRowView, int> _renxiaTable = new(row => row.Key);
    // Renxia results live in their own dynamic host so the grade filter row
    // stays mounted while an asynchronous query completes.
    private readonly TaiwuValue<UiElement> _renxiaResultsContent =
        new(Ui.Muted("选择品级后自动查找。"));
    private float _renxiaSearchAt = -1f;

    internal void Initialize(ViewBottom owner)
    {
        if (_initialized)
            return;
        _initialized = true;
        _personTable.Selection.SelectionChanged += _ => RefreshActivePage();
        _merchantTable.InlineRowAction = row => CanMarkArea(row.AreaId) && row.BlockId >= 0
            ? new TaiwuMenuAction(
                MapMarkTracker.MarkedKey == MerchantMarkKey(row) ? "已标记" : "定位",
                () => MarkMerchantLocation(row))
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
            if (next == 0 && AreaSearchPlan.ShouldStartAfterCatalog(
                _combatSkill >= 0, _combatSearchCacheValid, _combatSearchInFlight))
                StartCombatSearch();
            if (next == 1 && AreaSearchPlan.ShouldStartAfterCatalog(
                _lifeSkill >= 0, _lifeSearchCacheValid, _lifeSearchInFlight))
                StartLifeSearch();
            if (next == 2 && AreaSearchPlan.ShouldStartAfterCatalog(
                HasPersonFilter, _personSearchCacheValid, _personSearchInFlight))
                SchedulePersonSearch();
            if (next == 3 && AreaSearchPlan.ShouldStartAfterCatalog(
                true, _merchantSearchCacheValid, _merchantSearchInFlight))
                ScheduleMerchantSearch();
            if (next == 4 && AreaSearchPlan.ShouldStartAfterCatalog(
                HasRenxiaFilter, _renxiaSearchCacheValid, _renxiaSearchInFlight))
                ScheduleRenxiaSearch();
        };
        _bookSources.SelectionChanged += _ =>
        {
            MarkBookStale(0);
            MarkBookStale(1);
            if (_activeTab == 0 && _combatSkill >= 0)
                StartCombatSearch();
            if (_activeTab == 1 && _lifeSkill >= 0)
                StartLifeSearch();
            RefreshActivePage();
        };
        _combatAreaTabs.SelectionChanged += _ =>
        {
            _holderSetRenderLimit = HolderSetRenderPageSize;
            RefreshActivePage();
        };
        _lifeAreaTabs.SelectionChanged += _ =>
        {
            _holderSetRenderLimit = HolderSetRenderPageSize;
            RefreshActivePage();
        };
        _personAreaTabs.SelectionChanged += _ => RefreshActivePage();
        _merchantAreaTabs.SelectionChanged += _ => RefreshActivePage();
        _renxiaAreaTabs.SelectionChanged += _ => RefreshActivePage();
        foreach (AbilityFilterState filter in _abilityFilters)
        {
            // No page rebuild here: the popup card refreshes its own trigger and
            // fields, and rebuilding would destroy the open card.
            filter.Enabled.ValueChanged += _ => { ClearPersonResults(); SchedulePersonSearch(); };
            filter.Minimum.ValueChanged += _ => { ClearPersonResults(); SchedulePersonSearch(); };
        }
        _personName.ValueChanged += _ => { ClearPersonResults(); SchedulePersonSearch(); };
        _personGrades.SelectionChanged += _ => { ClearPersonResults(); SchedulePersonSearch(); };
        _personGenders.SelectionChanged += _ => { ClearPersonResults(); SchedulePersonSearch(); };
        _merchantTargets.SelectionChanged += _ =>
        {
            // 商队状态 row visibility depends on the target selection.
            ClearMerchantResults();
            ScheduleMerchantSearch();
            RefreshActivePage();
        };
        _merchantGuilds.SelectionChanged += _ => { ClearMerchantResults(); ScheduleMerchantSearch(); };
        _merchantLevels.SelectionChanged += _ => { ClearMerchantResults(); ScheduleMerchantSearch(); };
        _caravanState.SelectionChanged += _ => { ClearMerchantResults(); ScheduleMerchantSearch(); };
        _renxiaTable.Selection.SelectionChanged += _ => RefreshActivePage();
        _renxiaGrades.SelectionChanged += _ => { ClearRenxiaResults(); ScheduleRenxiaSearch(); };
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
            if (_activeTab == 0 && AreaSearchPlan.ShouldStartAfterCatalog(
                _combatSkill >= 0, _combatSearchCacheValid, _combatSearchInFlight))
                StartCombatSearch();
            if (_activeTab == 1 && AreaSearchPlan.ShouldStartAfterCatalog(
                _lifeSkill >= 0, _lifeSearchCacheValid, _lifeSearchInFlight))
                StartLifeSearch();
            if (_activeTab == 2 && AreaSearchPlan.ShouldStartAfterCatalog(
                HasPersonFilter, _personSearchCacheValid, _personSearchInFlight))
                SchedulePersonSearch();
            if (_activeTab == 3 && AreaSearchPlan.ShouldStartAfterCatalog(
                true, _merchantSearchCacheValid, _merchantSearchInFlight))
                ScheduleMerchantSearch();
            if (_activeTab == 4 && AreaSearchPlan.ShouldStartAfterCatalog(
                HasRenxiaFilter, _renxiaSearchCacheValid, _renxiaSearchInFlight))
                ScheduleRenxiaSearch();
        });
    }

    internal void Close()
    {
        _requestVersion++;
        CancelInFlightSearches();
        _window?.Hide();
    }

    private void OnDisable()
    {
        _requestVersion++;
        CancelInFlightSearches();
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
            new UiTabPage<sbyte>(4, "任侠", Ui.Dynamic(_tabContent[4], 1200f) with { Key = "renxia-dynamic" }),
        }, new TaiwuTabViewOptions { Height = 1300f, TabHeight = 64f, ContentPadding = 18f }) with { Key = "main-tabs" };

        return new UiWindow(OwnerId, WindowId,
            tabs,
            title: "寻访中心", width: 1920f, height: 1080f,
            layer: TaiwuWindowLayer.Popup, cover: TaiwuWindowCover.Full,
            presentation: TaiwuWindowPresentation.Encyclopedia);
    }

    private UiElement BuildTabPage(UiElement content, string key, bool showRegionSelector = true)
    {
        var children = new List<UiElement>();
        if (showRegionSelector)
            children.Add(BuildRegionSelector());
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
                Ui.Flex(BuildBookSource()),
                Ui.ResetIcon(ResetCombat)) with { Key = "combat-top-filters" },
        };
        if (_combatSkill >= 0)
        {
            if (!SupportsBookHoldingWorkspace)
            {
                children.Add(Ui.Muted("书页组合功能已更新：请重启游戏，使后端载入新版接口。"));
            }
            else if (_combatSearchInFlight)
            {
                children.Add(Ui.Muted(_combatSearchCompleted == 0
                    ? "正在搜索功法书……"
                    : $"正在搜索全地图（{_combatSearchCompleted}/{_combatSearchTotal}）……"));
            }
            else if (_combatAreaHoldings.Count == 0)
            {
                children.Add(Ui.Muted("未找到此功法书的持有人。"));
            }
            else children.Add(BuildCombatAreaTabs());
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
            else if (_lifeSearchInFlight)
            {
                children.Add(Ui.Muted(_lifeSearchCompleted == 0
                    ? "正在搜索技艺书……"
                    : $"正在搜索全地图（{_lifeSearchCompleted}/{_lifeSearchTotal}）……"));
            }
            else if (_lifeAreaHoldings.Count == 0)
            {
                children.Add(Ui.Muted("未找到此技艺书的持有人。"));
            }
            else children.Add(BuildLifeAreaTabs());
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
                    StartCombatSearch();
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
            _lifeSkill = value;
            MarkBookStale(1);
            if (_activeTab == 1) StartLifeSearch();
            RefreshActivePage();
        }), skills.Select(skill => new TaiwuChoiceOption<short>(skill.TemplateId,
            $"{skill.Name}·{GradeName(skill.Grade)}")).ToArray(), compact: true)
            with { Key = "life-skill" });
        return Ui.Column(elements.ToArray()) with { Key = "life-selectors" };
    }

    private UiElement BuildCombatAreaTabs()
    {
        CombatAreaHoldings? selected = SelectedCombatAreaHoldings;
        if (selected == null)
            return Ui.Spacer();

        // Region sheets are compact exclusive choices, not another level of
        // primary tabs: natural widths keep a one- or two-region result compact.
        TaiwuChoiceOption<short>[] sheets = _combatAreaHoldings.Select(area =>
            new TaiwuChoiceOption<short>(area.AreaId,
                $"{AreaName(area.AreaId)} {area.Holdings.Holders.Count}")).ToArray();
        return Ui.Column(
            Ui.SheetTabs(_combatAreaTabs, sheets)
                with { Key = "combat-area-sheets" },
            Ui.Spacer(10f),
            BuildCombatHoldingWorkspace(selected)) with { Key = "combat-area-tabs" };
    }

    private UiElement BuildCombatHoldingWorkspace(CombatAreaHoldings area)
    {
        IReadOnlyList<BookHolderView> holders = area.Holdings.Holders;
        IReadOnlyList<PageTargetChoice> targets = area.Types
            .Select((type, page) => new PageTargetChoice(type, area.States[page]))
            .ToArray();
        IReadOnlyList<BookHolderSet> sets = BookHoldingWorkspace.FindHolderSets(holders, targets, combat: true);

        var left = new List<UiElement>
        {
            Ui.Muted($"{holders.Count} 人持有此书。点击实际存在的书页状态；默认优先选择持有人最多的完整状态。"),
        };
        if (TaiwuPageMarking.HasAnyMark(_combatTaiwuKnowledge))
            left.Add(Ui.Muted("绿色背景 = 太吾已有或已读的书页，无需再寻。"));
        for (int page = 0; page < BookHoldingWorkspace.CombatPageCount; page++)
            left.Add(BuildCombatHoldingPagePicker(area, page));

        return BuildHoldingWorkspace(
            Ui.Column(left.ToArray()) with { Key = $"combat-{area.AreaId}-page-picker-list" },
            sets, () => StartCombatSearch(forceFullMap: false), () => StartCombatSearch(forceFullMap: true),
            ResetCombat, "combat-" + area.AreaId, area.AreaId);
    }

    private UiElement BuildLifeAreaTabs()
    {
        LifeAreaHoldings? selected = SelectedLifeAreaHoldings;
        if (selected == null)
            return Ui.Spacer();

        TaiwuChoiceOption<short>[] sheets = _lifeAreaHoldings.Select(area =>
            new TaiwuChoiceOption<short>(area.AreaId,
                $"{AreaName(area.AreaId)} {area.Holdings.Holders.Count}")).ToArray();
        return Ui.Column(
            Ui.SheetTabs(_lifeAreaTabs, sheets)
                with { Key = "life-area-sheets" },
            Ui.Spacer(10f),
            BuildLifeHoldingWorkspace(selected)) with { Key = "life-area-tabs" };
    }

    private UiElement BuildLifeHoldingWorkspace(LifeAreaHoldings area)
    {
        IReadOnlyList<BookHolderView> holders = area.Holdings.Holders;
        IReadOnlyList<PageTargetChoice> targets = area.States
            .Select(state => new PageTargetChoice(-1, state))
            .ToArray();
        IReadOnlyList<BookHolderSet> sets = BookHoldingWorkspace.FindHolderSets(holders, targets, combat: false);

        var left = new List<UiElement>
        {
            Ui.Muted($"{holders.Count} 人持有此书。点击实际存在的书页状态；默认优先选择持有人最多的完整状态。"),
        };
        for (int page = 0; page < BookHoldingWorkspace.LifePageCount; page++)
            left.Add(BuildLifeHoldingPagePicker(area, page));
        if (TaiwuPageMarking.HasAnyMark(_lifeTaiwuKnowledge))
            left.Add(Ui.Muted("绿色背景 = 太吾已有或已读的书页，无需再寻。"));

        return BuildHoldingWorkspace(
            Ui.Column(left.ToArray()) with { Key = $"life-{area.AreaId}-page-picker-list" },
            sets, () => StartLifeSearch(forceFullMap: false), () => StartLifeSearch(forceFullMap: true),
            ResetLife, "life-" + area.AreaId, area.AreaId);
    }

    private UiElement BuildHoldingWorkspace(
        UiElement pagePane,
        IReadOnlyList<BookHolderSet> sets,
        Action reload,
        Action searchFullMap,
        Action reset,
        string key,
        short areaId)
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
            right.Add(BuildHolderSetRow(set, key, areaId));
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
                Ui.Button("搜索全图", searchFullMap,
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

    private UiElement BuildCombatHoldingPagePicker(CombatAreaHoldings area, int page)
    {
        IReadOnlyList<BookHolderView> holders = area.Holdings.Holders;
        IReadOnlyList<BookPageAvailability> availability =
            BookHoldingWorkspace.GetPageAvailability(holders, page, combat: true);
        PageTargetChoice selected = new(area.Types[page], area.States[page]);
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
            area.Types[page] = value.Type;
            area.States[page] = value.State;
            RefreshActivePage();
        }), options.ToArray(), compact: true) with { Key = $"combat-{area.AreaId}-holding-page-{page}" };
    }

    private UiElement BuildLifeHoldingPagePicker(LifeAreaHoldings area, int page)
    {
        IReadOnlyList<BookHolderView> holders = area.Holdings.Holders;
        IReadOnlyList<BookPageAvailability> availability =
            BookHoldingWorkspace.GetPageAvailability(holders, page, combat: false);
        PageTargetChoice selected = new(-1, area.States[page]);
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
            area.States[page] = value.State;
            RefreshActivePage();
        }), options.ToArray(), compact: true) with { Key = $"life-{area.AreaId}-holding-page-{page}" };
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

    private UiElement BuildHolderSetRow(BookHolderSet set, string key, short areaId)
    {
        UiElement summary = Ui.Column(
            Ui.Text(string.Join("、", set.Holders.Select(holder => holder.Name))),
            Ui.Muted(string.Join(" · ", set.Holders.Select(holder =>
                $"{holder.Organization}／地格 {holder.BlockId}"))),
            Ui.Spacer(6f),
            Ui.Divider());

        if (!CanMarkArea(areaId))
            return summary with { Key = key + "-holder-set-" + set.Key };

        string markKey = "book:" + set.Key;
        return Ui.Row(
            Ui.Flex(summary),
            Ui.Button(MarkButtonLabel(markKey), () => MarkHolderSet(set, markKey, areaId),
                new TaiwuButtonOptions { Width = 156f, Style = TaiwuButtonStyle.Secondary })) with
        {
            Key = key + "-holder-set-" + set.Key,
        };
    }

    private void MarkHolderSet(BookHolderSet set, string markKey, short areaId)
    {
        if (!CanMarkArea(areaId))
        {
            SetStatus("仅能标记太吾当前所在地域的地格。");
            return;
        }

        List<Location> locations = set.Holders
            .Where(holder => holder.AreaId == areaId && holder.BlockId >= 0)
            .Select(holder => new Location(holder.AreaId, holder.BlockId))
            .Distinct()
            .ToList();
        if (locations.Count == 0)
        {
            SetStatus("这套组合没有可标记的地格。");
            return;
        }
        TryMarkLocations(locations, markKey, areaId);
    }

    private static string MarkButtonLabel(string markKey) =>
        MapMarkTracker.MarkedKey == markKey ? "已标记" : "标记地格";

    private void TryMarkLocations(List<Location> locations, string markKey, short? areaId = null)
    {
        try
        {
            WorldMapModel map = SingletonObject.getInstance<WorldMapModel>();
            short targetAreaId = areaId ?? _selectedAreaId;
            if (map.CurrentAreaId != targetAreaId)
            {
                SetStatus("太吾已不在当前查询地域，未标记地格。");
                return;
            }
            MapMarkTracker.ReplaceMarks(map, locations, markKey);
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
        if (_personSearchInFlight)
            return Ui.Muted(_personSearchCompleted == 0
                ? "正在搜索人物……"
                : $"正在搜索全地图（{_personSearchCompleted}/{_personSearchTotal}）……") with { Key = "person-loading" };
        if (!_personSearchCacheValid)
            return Ui.Muted("设置至少一个筛选条件后自动查找。") with { Key = "person-empty" };
        if (_personAreaResults.Count == 0)
            return Ui.Muted("未找到符合条件的人物。") with { Key = "person-no-results" };

        PersonAreaResults selected = SelectedPersonAreaResults!;
        TaiwuChoiceOption<short>[] sheets = _personAreaResults.Select(area =>
            new TaiwuChoiceOption<short>(area.AreaId,
                $"{AreaName(area.AreaId)} {area.Response.TotalCount}")).ToArray();
        return Ui.Column(
            Ui.SheetTabs(_personAreaTabs, sheets)
                with { Key = "person-area-sheets" },
            Ui.Spacer(10f),
            BuildPersonResults(selected.Response)) with { Key = "person-area-tabs" };
    }

    private UiElement BuildPersonResults(PersonSearchResponse response)
    {
        if (!response.Success)
            return Ui.Muted(response.Message) with { Key = "person-error" };
        _personTable.SetItems(response.People);
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
        string title = response.People.Count < response.TotalCount
            ? $"人物结果 · 已加载 {response.People.Count}/{response.TotalCount} 人"
            : $"人物结果 · {response.TotalCount} 人 · {response.ElapsedMs} ms";
        int remaining = response.TotalCount - response.People.Count;
        return Ui.Column(
            Ui.Row(
                Ui.Heading(title),
                Ui.Flex(Ui.Spacer(0)),
                Ui.Button("重新读取", SearchPeople,
                    new TaiwuButtonOptions { Width = 180f, Style = TaiwuButtonStyle.Secondary }),
                Ui.Button("搜索全图", () => SearchPeople(forceFullMap: true),
                    new TaiwuButtonOptions { Width = 180f, Style = TaiwuButtonStyle.Secondary }),
                remaining > 0
                    ? Ui.Button($"继续加载 {remaining} 人",
                        LoadMorePeople,
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
        if (CanMarkArea(person.AreaId) && person.BlockId >= 0)
        {
            string markKey = "person:" + person.CharacterId;
            rows.Add(Ui.Spacer(8f));
            rows.Add(Ui.Row(
                Ui.Button(MarkButtonLabel(markKey), () => MarkPersonLocation(person, markKey),
                    new TaiwuButtonOptions { Width = 200f, Style = TaiwuButtonStyle.Secondary }),
                Ui.Flex(Ui.Spacer(0))));
        }
        return Ui.Column(rows.ToArray());
    }

    private void MarkPersonLocation(PersonRowView person, string markKey)
    {
        if (!CanMarkArea(person.AreaId))
        {
            SetStatus("仅能标记太吾当前所在地域的地格。");
            return;
        }
        TryMarkLocations(new List<Location> { new(person.AreaId, person.BlockId) }, markKey, person.AreaId);
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
        if (_merchantSearchInFlight)
            return Ui.Muted(_merchantSearchCompleted == 0
                ? "正在搜索商会目标……"
                : $"正在搜索全地图（{_merchantSearchCompleted}/{_merchantSearchTotal}）……") with { Key = "merchant-loading" };
        if (!_merchantSearchCacheValid)
            return Ui.Muted("变更筛选条件后自动查找。") with { Key = "merchant-empty" };
        if (_merchantAreaResults.Count == 0)
            return Ui.Muted("未找到符合条件的商会目标。") with { Key = "merchant-no-results" };

        MerchantAreaResults selected = SelectedMerchantAreaResults!;
        TaiwuChoiceOption<short>[] sheets = _merchantAreaResults.Select(area =>
            new TaiwuChoiceOption<short>(area.AreaId,
                $"{AreaName(area.AreaId)} {area.Response.TotalCount}")).ToArray();
        return Ui.Column(
            Ui.SheetTabs(_merchantAreaTabs, sheets)
                with { Key = "merchant-area-sheets" },
            Ui.Spacer(10f),
            BuildMerchantResults(selected.Response)) with { Key = "merchant-area-tabs" };
    }

    private UiElement BuildMerchantResults(MerchantSearchResponse response)
    {
        if (!response.Success)
            return Ui.Muted(response.Message) with { Key = "merchant-error" };
        _merchantTable.SetItems(response.Rows);
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
        int remaining = response.TotalCount - response.Rows.Count;
        return Ui.Column(
            Ui.Row(
                Ui.Heading($"商会结果 · {response.TotalCount} 项 · {response.ElapsedMs} ms"),
                Ui.Flex(Ui.Spacer(0)),
                Ui.Button("重新读取", SearchMerchants,
                    new TaiwuButtonOptions { Width = 180f, Style = TaiwuButtonStyle.Secondary }),
                Ui.Button("搜索全图", () => SearchMerchants(forceFullMap: true),
                    new TaiwuButtonOptions { Width = 180f, Style = TaiwuButtonStyle.Secondary }),
                remaining > 0
                    ? Ui.Button($"继续加载 {remaining} 项",
                        LoadMoreMerchants,
                        new TaiwuButtonOptions { Width = 260f, Style = TaiwuButtonStyle.Secondary })
                    : Ui.Spacer(0)) with { Key = "merchant-results-header" },
            Ui.Table(_merchantTable, columns,
                new TaiwuTableOptions { Height = 700f, RowHeight = 70f }))
            with { Key = "merchant-results" };
    }

    private static string MerchantMarkKey(MerchantRowView row) =>
        $"merchant:{row.TargetType}:{row.EntityId}";

    private void MarkMerchantLocation(MerchantRowView row)
    {
        if (!CanMarkArea(row.AreaId))
        {
            SetStatus("仅能标记太吾当前所在地域的地格。");
            return;
        }
        TryMarkLocations(new List<Location> { new(row.AreaId, row.BlockId) }, MerchantMarkKey(row), row.AreaId);
    }

    private UiElement BuildRenxiaPage()
    {
        // The backend method only exists after a game restart registers it.
        if (_catalog != null && !SupportsRenxiaSearch)
            return Ui.Muted("任侠查找功能已更新：请重启游戏，使后端载入新版接口。") with { Key = "renxia-unsupported" };
        _renxiaResultsContent.SetValueWithoutNotify(BuildRenxiaResults());
        var rows = new List<UiElement>
        {
            Ui.Row(
                Ui.FilterButtons("品级", _renxiaGrades,
                    Enumerable.Range(0, 9).Select(value => new TaiwuChoiceOption<sbyte>((sbyte)value,
                        GradeName((sbyte)value))).ToArray(), true),
                Ui.Flex(Ui.Spacer(0)),
                Ui.ResetIcon(ResetRenxia)) with { Key = "renxia-grades" },
            Ui.Dynamic(_renxiaResultsContent, 800f) with { Key = "renxia-results-dynamic" },
        };
        return Ui.Column(rows.ToArray()) with { Key = "renxia-content" };
    }

    private UiElement BuildRenxiaResults()
    {
        if (_renxiaSearchInFlight)
            return Ui.Muted(_renxiaSearchCompleted == 0
                ? "正在搜索任侠……"
                : $"正在搜索全地图（{_renxiaSearchCompleted}/{_renxiaSearchTotal}）……") with { Key = "renxia-loading" };
        if (!_renxiaSearchCacheValid)
            return Ui.Muted("选择品级后自动查找。") with { Key = "renxia-empty" };
        if (_renxiaAreaResults.Count == 0)
            return Ui.Muted("未找到符合条件的任侠。") with { Key = "renxia-no-results" };

        RenxiaAreaResults selectedArea = SelectedRenxiaAreaResults!;
        TaiwuChoiceOption<short>[] sheets = _renxiaAreaResults.Select(area =>
            new TaiwuChoiceOption<short>(area.AreaId,
                $"{AreaName(area.AreaId)} {area.Response.TotalCount}")).ToArray();
        return Ui.Column(
            Ui.SheetTabs(_renxiaAreaTabs, sheets)
                with { Key = "renxia-area-sheets" },
            Ui.Spacer(10f),
            BuildRenxiaResults(selectedArea.Response)) with { Key = "renxia-area-tabs" };
    }

    private UiElement BuildRenxiaResults(RenxiaSearchResponse response)
    {
        if (!response.Success)
            return Ui.Muted(response.Message) with { Key = "renxia-error" };
        _renxiaTable.SetItems(response.Rows);
        RenxiaRowView? selected = Selected(_renxiaTable);
        var columns = new[]
        {
            new TaiwuTableColumn<RenxiaRowView>("name", "名字", row => row.Name, 260f, true, row => row.Name),
            new TaiwuTableColumn<RenxiaRowView>("grade", "品级", row => GradeName(row.Grade),
                150f, true, row => row.Grade),
            new TaiwuTableColumn<RenxiaRowView>("location", "地格", row => $"地格 {row.BlockId}",
                180f, true, row => row.BlockId),
        };
        return Ui.Column(
            Ui.Row(
                Ui.Heading($"任侠结果 · {response.TotalCount} 个 · {response.ElapsedMs} ms"),
                Ui.Flex(Ui.Spacer(0)),
                Ui.Button("重新读取", SearchRenxia,
                    new TaiwuButtonOptions { Width = 180f, Style = TaiwuButtonStyle.Secondary }),
                Ui.Button("搜索全图", () => SearchRenxia(forceFullMap: true),
                    new TaiwuButtonOptions { Width = 180f, Style = TaiwuButtonStyle.Secondary })) with { Key = "renxia-results-header" },
            Ui.Row(
                Ui.Flex(Ui.Table(_renxiaTable, columns,
                    new TaiwuTableOptions { Height = 700f, RowHeight = 70f }), 2f),
                Ui.Flex(BuildRenxiaDetail(selected), 1f))) with { Key = "renxia-results" };
    }

    private UiElement BuildRenxiaDetail(RenxiaRowView? row)
    {
        if (row == null) return Ui.Muted("选择任侠查看详情。");
        var rows = new List<UiElement>
        {
            Ui.Heading(row.Name),
            Ui.Text($"任侠 · {GradeName(row.Grade)} · 地格 {row.BlockId}"),
        };
        if (CanMarkArea(row.AreaId) && row.BlockId >= 0)
        {
            string markKey = $"renxia:{row.TemplateId}@{row.AreaId}:{row.BlockId}";
            rows.Add(Ui.Spacer(8f));
            rows.Add(Ui.Row(
                Ui.Button(MarkButtonLabel(markKey), () => MarkRenxiaLocation(row, markKey),
                    new TaiwuButtonOptions { Width = 200f, Style = TaiwuButtonStyle.Secondary }),
                Ui.Flex(Ui.Spacer(0))));
        }
        return Ui.Column(rows.ToArray());
    }

    private void MarkRenxiaLocation(RenxiaRowView row, string markKey)
    {
        if (!CanMarkArea(row.AreaId))
        {
            SetStatus("仅能标记太吾当前所在地域的地格。");
            return;
        }
        TryMarkLocations(new List<Location> { new(row.AreaId, row.BlockId) }, markKey, row.AreaId);
    }

    private UiElement ActionRow(Action search, Action reset, string label) => Ui.Row(
        Ui.Button(label, search, new TaiwuButtonOptions { Width = 300f }),
        Ui.ResetIcon(reset)) with { Key = "actions-" + label };

    private CombatAreaHoldings? SelectedCombatAreaHoldings
    {
        get
        {
            if (_combatAreaHoldings.Count == 0)
                return null;
            short selectedAreaId = _combatAreaTabs.Selected.Count == 0
                ? _combatAreaHoldings[0].AreaId
                : _combatAreaTabs.Selected.First();
            return _combatAreaHoldings.FirstOrDefault(area => area.AreaId == selectedAreaId)
                ?? _combatAreaHoldings[0];
        }
    }

    private LifeAreaHoldings? SelectedLifeAreaHoldings
    {
        get
        {
            if (_lifeAreaHoldings.Count == 0)
                return null;
            short selectedAreaId = _lifeAreaTabs.Selected.Count == 0
                ? _lifeAreaHoldings[0].AreaId
                : _lifeAreaTabs.Selected.First();
            return _lifeAreaHoldings.FirstOrDefault(area => area.AreaId == selectedAreaId)
                ?? _lifeAreaHoldings[0];
        }
    }

    private PersonAreaResults? SelectedPersonAreaResults =>
        SelectedAreaResult(_personAreaResults, _personAreaTabs, result => result.AreaId);

    private MerchantAreaResults? SelectedMerchantAreaResults =>
        SelectedAreaResult(_merchantAreaResults, _merchantAreaTabs, result => result.AreaId);

    private RenxiaAreaResults? SelectedRenxiaAreaResults =>
        SelectedAreaResult(_renxiaAreaResults, _renxiaAreaTabs, result => result.AreaId);

    private static T? SelectedAreaResult<T>(
        IReadOnlyList<T> results,
        TaiwuSelection<short> selection,
        Func<T, short> areaId)
        where T : class
    {
        if (results.Count == 0)
            return null;
        short selectedAreaId = selection.Selected.Count == 0
            ? areaId(results[0])
            : selection.Selected.First();
        return results.FirstOrDefault(result => areaId(result) == selectedAreaId) ?? results[0];
    }

    private void StartCombatSearch(bool forceFullMap = false)
    {
        if (_combatSkill < 0 || _catalog == null) return;
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
            RefreshActivePage();
            return;
        }

        IReadOnlyList<short> areas = AreaSearchPlan.BuildSearchOrder(
            _catalog.Areas.Select(area => area.AreaId), _catalog.CurrentAreaId);
        if (areas.Count == 0)
        {
            SetStatus("没有可查询的地域。");
            RefreshActivePage();
            return;
        }

        int version = ++_combatHoldingsVersion;
        _combatSearchCacheValid = false;
        _combatSearchInFlight = true;
        _combatSearchCompleted = 0;
        _combatSearchTotal = areas.Count;
        _combatAreaHoldings = Array.Empty<CombatAreaHoldings>();
        _combatAreaTabs.Replace(Array.Empty<short>(), notify: false);
        _combatTaiwuKnowledge = TaiwuBookKnowledge.Empty;
        SetStatus("正在读取当前地域的持有情况……");
        RefreshActivePage();
        SearchCombatArea(version, areas, 0, sourceMask, forceFullMap, new List<CombatAreaHoldings>());
    }

    private void SearchCombatArea(
        int version,
        IReadOnlyList<short> areas,
        int index,
        byte sourceMask,
        bool forceFullMap,
        List<CombatAreaHoldings> results)
    {
        FinderBackendClient.GetBookHoldings(new BookHoldingsRequestView(
            areas[index], 0, _combatSkill, sourceMask), response =>
        {
            if (version != _combatHoldingsVersion || _window?.IsShowing != true) return;
            _combatSearchCompleted++;
            if (!response.Success)
            {
                if (index == 0)
                {
                    _combatSearchInFlight = false;
                    SetStatus(response.Message);
                    RefreshActivePage();
                    return;
                }
                SearchNextCombatArea(version, areas, index, sourceMask, forceFullMap, results);
                return;
            }

            if (index == 0)
            {
                _combatTaiwuKnowledge = BookHoldingWorkspace.BuildTaiwuKnowledge(
                    response.TaiwuBooks, response.TaiwuReadingState, combat: true);
            }
            if (response.Holders.Count > 0)
                results.Add(CreateCombatAreaHoldings(areas[index], response));

            if (index == 0 && !AreaSearchPlan.ShouldSearchBeyondCurrentArea(
                currentAreaHasResults: results.Count > 0, forceFullMap))
            {
                CompleteCombatSearch(results);
                return;
            }
            SearchNextCombatArea(version, areas, index, sourceMask, forceFullMap, results);
        });
    }

    private void SearchNextCombatArea(
        int version,
        IReadOnlyList<short> areas,
        int index,
        byte sourceMask,
        bool forceFullMap,
        List<CombatAreaHoldings> results)
    {
        int next = index + 1;
        if (next >= areas.Count)
        {
            CompleteCombatSearch(results);
            return;
        }
        SetStatus($"正在搜索全地图（{_combatSearchCompleted}/{_combatSearchTotal}）……");
        SearchCombatArea(version, areas, next, sourceMask, forceFullMap, results);
    }

    private void CompleteCombatSearch(IReadOnlyList<CombatAreaHoldings> results)
    {
        _combatSearchInFlight = false;
        _combatSearchCacheValid = true;
        _combatAreaHoldings = AreaSearchPlan.OrderByResultCount(
            results, area => area.Holdings.Holders.Count, area => area.AreaId);
        _combatAreaTabs.Replace(_combatAreaHoldings.Take(1).Select(area => area.AreaId), notify: false);
        _holderSetRenderLimit = HolderSetRenderPageSize;
        ClearStatus();
        RefreshActivePage();
    }

    private CombatAreaHoldings CreateCombatAreaHoldings(short areaId, BookHoldingsResponse holdings)
    {
        var area = new CombatAreaHoldings(areaId, holdings);
        ApplyCombatHoldingDefaults(area);
        return area;
    }

    private void ApplyCombatHoldingDefaults(CombatAreaHoldings area)
    {
        IReadOnlyList<BookHolderView> holders = area.Holdings.Holders;
        for (int page = 0; page < BookHoldingWorkspace.CombatPageCount; page++)
        {
            // 正/逆（总纲为任意总纲类型）都已读或已有完整页的书页无需寻访，默认"不限"。
            if (TaiwuPageMarking.IsPageFullyCoveredByTaiwu(_combatTaiwuKnowledge, page))
            {
                area.Types[page] = AnyPageTarget.Type;
                area.States[page] = AnyPageTarget.State;
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
                area.Types[page] = preferred.Target.Type;
                area.States[page] = preferred.Target.State;
            }
        }
    }

    private void StartLifeSearch(bool forceFullMap = false)
    {
        if (_lifeSkill < 0 || _catalog == null) return;
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

        IReadOnlyList<short> areas = AreaSearchPlan.BuildSearchOrder(
            _catalog.Areas.Select(area => area.AreaId), _catalog.CurrentAreaId);
        if (areas.Count == 0)
        {
            SetStatus("没有可查询的地域。");
            RefreshActivePage();
            return;
        }

        int version = ++_lifeHoldingsVersion;
        _lifeSearchCacheValid = false;
        _lifeSearchInFlight = true;
        _lifeSearchCompleted = 0;
        _lifeSearchTotal = areas.Count;
        _lifeAreaHoldings = Array.Empty<LifeAreaHoldings>();
        _lifeAreaTabs.Replace(Array.Empty<short>(), notify: false);
        _lifeTaiwuKnowledge = TaiwuBookKnowledge.Empty;
        SetStatus("正在读取当前地域的持有情况……");
        RefreshActivePage();
        SearchLifeArea(version, areas, 0, sourceMask, forceFullMap, new List<LifeAreaHoldings>());
    }

    private void SearchLifeArea(
        int version,
        IReadOnlyList<short> areas,
        int index,
        byte sourceMask,
        bool forceFullMap,
        List<LifeAreaHoldings> results)
    {
        FinderBackendClient.GetBookHoldings(new BookHoldingsRequestView(
            areas[index], 1, _lifeSkill, sourceMask), response =>
        {
            if (version != _lifeHoldingsVersion || _window?.IsShowing != true) return;
            _lifeSearchCompleted++;
            if (!response.Success)
            {
                if (index == 0)
                {
                    _lifeSearchInFlight = false;
                    SetStatus(response.Message);
                    RefreshActivePage();
                    return;
                }
                SearchNextLifeArea(version, areas, index, sourceMask, forceFullMap, results);
                return;
            }

            if (index == 0)
            {
                _lifeTaiwuKnowledge = BookHoldingWorkspace.BuildTaiwuKnowledge(
                    response.TaiwuBooks, response.TaiwuReadingState, combat: false);
            }
            if (response.Holders.Count > 0)
                results.Add(CreateLifeAreaHoldings(areas[index], response));

            if (index == 0 && !AreaSearchPlan.ShouldSearchBeyondCurrentArea(
                currentAreaHasResults: results.Count > 0, forceFullMap))
            {
                CompleteLifeSearch(results);
                return;
            }
            SearchNextLifeArea(version, areas, index, sourceMask, forceFullMap, results);
        });
    }

    private void SearchNextLifeArea(
        int version,
        IReadOnlyList<short> areas,
        int index,
        byte sourceMask,
        bool forceFullMap,
        List<LifeAreaHoldings> results)
    {
        int next = index + 1;
        if (next >= areas.Count)
        {
            CompleteLifeSearch(results);
            return;
        }
        SetStatus($"正在搜索全地图（{_lifeSearchCompleted}/{_lifeSearchTotal}）……");
        SearchLifeArea(version, areas, next, sourceMask, forceFullMap, results);
    }

    private void CompleteLifeSearch(IReadOnlyList<LifeAreaHoldings> results)
    {
        _lifeSearchInFlight = false;
        _lifeSearchCacheValid = true;
        _lifeAreaHoldings = AreaSearchPlan.OrderByResultCount(
            results, area => area.Holdings.Holders.Count, area => area.AreaId);
        _lifeAreaTabs.Replace(_lifeAreaHoldings.Take(1).Select(area => area.AreaId), notify: false);
        _holderSetRenderLimit = HolderSetRenderPageSize;
        ClearStatus();
        RefreshActivePage();
    }

    private LifeAreaHoldings CreateLifeAreaHoldings(short areaId, BookHoldingsResponse holdings)
    {
        var area = new LifeAreaHoldings(areaId, holdings);
        ApplyLifeHoldingDefaults(area);
        return area;
    }

    private void ApplyLifeHoldingDefaults(LifeAreaHoldings area)
    {
        IReadOnlyList<BookHolderView> holders = area.Holdings.Holders;
        for (int page = 0; page < BookHoldingWorkspace.LifePageCount; page++)
        {
            // 太吾已拥有或已读的书页无需寻访，默认"不限"。
            if (TaiwuPageMarking.IsPageFullyCoveredByTaiwu(_lifeTaiwuKnowledge, page))
            {
                area.States[page] = AnyPageTarget.State;
                continue;
            }
            BookPageAvailability? preferred = BookHoldingWorkspace.GetPageAvailability(holders, page, combat: false)
                .Where(item => item.Target.State != 2)
                .OrderByDescending(item => item.HolderCount)
                .ThenBy(item => item.Target.State)
                .FirstOrDefault();
            preferred ??= BookHoldingWorkspace.GetPageAvailability(holders, page, combat: false).FirstOrDefault();
            if (preferred != null)
                area.States[page] = preferred.Target.State;
        }
    }

    private void Update()
    {
        if (_window?.IsShowing != true)
        {
            _personSearchAt = -1f;
            _merchantSearchAt = -1f;
            _renxiaSearchAt = -1f;
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
                    // No filter set: stay on the hint instead of listing the whole map.
                    ClearPersonResults();
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
        if (_renxiaSearchAt >= 0f)
        {
            if (_activeTab != 4) _renxiaSearchAt = -1f;
            else if (Time.unscaledTime >= _renxiaSearchAt)
            {
                _renxiaSearchAt = -1f;
                if (HasRenxiaFilter) SearchRenxia();
                else
                {
                    // No grade selected: stay on the hint instead of listing the whole map.
                    ClearRenxiaResults();
                    _renxiaResultsContent.SetValue(BuildRenxiaResults());
                }
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
            if (_activeTab == 0 && _combatSkill >= 0) StartCombatSearch();
            if (_activeTab == 1 && _lifeSkill >= 0) StartLifeSearch();
            if (_activeTab == 2 && HasPersonFilter) SchedulePersonSearch();
            if (_activeTab == 3) ScheduleMerchantSearch();
            if (_activeTab == 4 && HasRenxiaFilter) ScheduleRenxiaSearch();
        });
    }

    private void ScheduleMerchantSearch() =>
        _merchantSearchAt = Time.unscaledTime + PersonSearchDebounceSeconds;

    private void ScheduleRenxiaSearch() =>
        _renxiaSearchAt = Time.unscaledTime + PersonSearchDebounceSeconds;

    // Renxia search requires at least one grade; without one the tab only shows
    // a hint. Any selected grade auto-searches after a short debounce.
    private bool HasRenxiaFilter => _renxiaGrades.Selected.Count > 0;

    // Person search requires at least one filter; without one the tab only
    // shows a hint. Any active filter auto-searches after a short debounce.
    private bool HasPersonFilter =>
        !string.IsNullOrWhiteSpace(_personName.Value) ||
        _personGrades.Selected.Count > 0 ||
        _personGenders.Selected.Count > 0 ||
        _abilityFilters.Any(filter => filter.Enabled.Value);

    private PersonSearchRequestView CreatePersonRequest(short areaId, int page = 0)
    {
        int gradeMask = _personGrades.Selected.Aggregate(0, (mask, value) => mask | (1 << value));
        var abilities = _abilityFilters.Where(filter => filter.Enabled.Value)
            .Select(filter => new AbilityConditionView(filter.LifeSkillType, filter.Metric,
                checked((short)Math.Round(filter.Minimum.Value)))).ToArray();
        sbyte gender = _personGenders.Selected.Count == 0 ? (sbyte)-1 : _personGenders.Selected.First();
        return new PersonSearchRequestView(areaId, _personName.Value, gradeMask,
            0, 200, gender, Array.Empty<sbyte>(), abilities, page, 200);
    }

    private void SearchPeople() => SearchPeople(forceFullMap: false);

    private void SearchPeople(bool forceFullMap)
    {
        if (_catalog == null || !HasPersonFilter) return;
        IReadOnlyList<short> areas = AreaSearchPlan.BuildSearchOrder(
            _catalog.Areas.Select(area => area.AreaId), _catalog.CurrentAreaId);
        if (areas.Count == 0) return;

        int version = ++_personSearchVersion;
        _personSearchCacheValid = false;
        _personSearchInFlight = true;
        _personSearchCompleted = 0;
        _personSearchTotal = areas.Count;
        _personAreaResults = Array.Empty<PersonAreaResults>();
        _personAreaTabs.Replace(Array.Empty<short>(), notify: false);
        SetStatus("正在查询人物……");
        _personResultsContent.SetValue(BuildPersonResults());
        SearchPersonArea(version, areas, 0, forceFullMap, new List<PersonAreaResults>());
    }

    private void SearchPersonArea(
        int version,
        IReadOnlyList<short> areas,
        int index,
        bool forceFullMap,
        List<PersonAreaResults> results)
    {
        FinderBackendClient.SearchPeople(CreatePersonRequest(areas[index]), response =>
        {
            if (version != _personSearchVersion || _window?.IsShowing != true) return;
            _personSearchCompleted++;
            if (!response.Success)
            {
                if (index == 0)
                {
                    _personSearchInFlight = false;
                    SetStatus(response.Message);
                    _personResultsContent.SetValue(BuildPersonResults());
                    return;
                }
                SearchNextPersonArea(version, areas, index, forceFullMap, results);
                return;
            }

            if (response.TotalCount > 0)
                results.Add(new PersonAreaResults(areas[index], response));
            if (index == 0 && !AreaSearchPlan.ShouldSearchBeyondCurrentArea(
                currentAreaHasResults: results.Count > 0, forceFullMap))
            {
                CompletePersonSearch(results);
                return;
            }
            SearchNextPersonArea(version, areas, index, forceFullMap, results);
        });
    }

    private void SearchNextPersonArea(
        int version,
        IReadOnlyList<short> areas,
        int index,
        bool forceFullMap,
        List<PersonAreaResults> results)
    {
        int next = index + 1;
        if (next >= areas.Count)
        {
            CompletePersonSearch(results);
            return;
        }
        SetStatus($"正在搜索全地图（{_personSearchCompleted}/{_personSearchTotal}）……");
        SearchPersonArea(version, areas, next, forceFullMap, results);
    }

    private void CompletePersonSearch(IReadOnlyList<PersonAreaResults> results)
    {
        _personSearchInFlight = false;
        _personSearchCacheValid = true;
        _personAreaResults = AreaSearchPlan.OrderByResultCount(
            results, area => area.Response.TotalCount, area => area.AreaId);
        _personAreaTabs.Replace(_personAreaResults.Take(1).Select(area => area.AreaId), notify: false);
        ClearStatus();
        _personResultsContent.SetValue(BuildPersonResults());
    }

    private void LoadMorePeople()
    {
        PersonAreaResults? area = SelectedPersonAreaResults;
        if (area == null || !area.Response.Success || area.Response.People.Count >= area.Response.TotalCount)
            return;
        int version = ++_personSearchVersion;
        SetStatus("正在加载更多人物……");
        FinderBackendClient.SearchPeople(CreatePersonRequest(area.AreaId, area.Response.Page + 1), response =>
        {
            if (version != _personSearchVersion || _window?.IsShowing != true || !response.Success) return;
            area.Response = response with { People = area.Response.People.Concat(response.People).ToArray() };
            ClearStatus();
            _personResultsContent.SetValue(BuildPersonResults());
        });
    }

    private MerchantSearchRequestView CreateMerchantRequest(short areaId, int page = 0)
    {
        int targetMask = _merchantTargets.Selected.Aggregate(0, (mask, value) => mask | (1 << value));
        int guildMask = _merchantGuilds.Selected.Aggregate(0, (mask, value) => mask | (1 << value));
        int levelMask = _merchantLevels.Selected.Aggregate(0, (mask, value) => mask | (1 << value));
        sbyte caravanState = _caravanState.Selected.Count == 0 ? (sbyte)0 : _caravanState.Selected.First();
        return new MerchantSearchRequestView(areaId, targetMask, guildMask, levelMask, caravanState, page, 100);
    }

    private void SearchMerchants() => SearchMerchants(forceFullMap: false);

    private void SearchMerchants(bool forceFullMap)
    {
        if (_catalog == null) return;
        IReadOnlyList<short> areas = AreaSearchPlan.BuildSearchOrder(
            _catalog.Areas.Select(area => area.AreaId), _catalog.CurrentAreaId);
        if (areas.Count == 0) return;

        int version = ++_merchantSearchVersion;
        _merchantSearchCacheValid = false;
        _merchantSearchInFlight = true;
        _merchantSearchCompleted = 0;
        _merchantSearchTotal = areas.Count;
        _merchantAreaResults = Array.Empty<MerchantAreaResults>();
        _merchantAreaTabs.Replace(Array.Empty<short>(), notify: false);
        SetStatus("正在查询商会目标……");
        _merchantResultsContent.SetValue(BuildMerchantResults());
        SearchMerchantArea(version, areas, 0, forceFullMap, new List<MerchantAreaResults>());
    }

    private void SearchMerchantArea(
        int version,
        IReadOnlyList<short> areas,
        int index,
        bool forceFullMap,
        List<MerchantAreaResults> results)
    {
        FinderBackendClient.SearchMerchants(CreateMerchantRequest(areas[index]), response =>
        {
            if (version != _merchantSearchVersion || _window?.IsShowing != true) return;
            _merchantSearchCompleted++;
            if (!response.Success)
            {
                if (index == 0)
                {
                    _merchantSearchInFlight = false;
                    SetStatus(response.Message);
                    _merchantResultsContent.SetValue(BuildMerchantResults());
                    return;
                }
                SearchNextMerchantArea(version, areas, index, forceFullMap, results);
                return;
            }

            if (response.TotalCount > 0)
                results.Add(new MerchantAreaResults(areas[index], response));
            if (index == 0 && !AreaSearchPlan.ShouldSearchBeyondCurrentArea(
                currentAreaHasResults: results.Count > 0, forceFullMap))
            {
                CompleteMerchantSearch(results);
                return;
            }
            SearchNextMerchantArea(version, areas, index, forceFullMap, results);
        });
    }

    private void SearchNextMerchantArea(
        int version,
        IReadOnlyList<short> areas,
        int index,
        bool forceFullMap,
        List<MerchantAreaResults> results)
    {
        int next = index + 1;
        if (next >= areas.Count)
        {
            CompleteMerchantSearch(results);
            return;
        }
        SetStatus($"正在搜索全地图（{_merchantSearchCompleted}/{_merchantSearchTotal}）……");
        SearchMerchantArea(version, areas, next, forceFullMap, results);
    }

    private void CompleteMerchantSearch(IReadOnlyList<MerchantAreaResults> results)
    {
        _merchantSearchInFlight = false;
        _merchantSearchCacheValid = true;
        _merchantAreaResults = AreaSearchPlan.OrderByResultCount(
            results, area => area.Response.TotalCount, area => area.AreaId);
        _merchantAreaTabs.Replace(_merchantAreaResults.Take(1).Select(area => area.AreaId), notify: false);
        ClearStatus();
        _merchantResultsContent.SetValue(BuildMerchantResults());
    }

    private void LoadMoreMerchants()
    {
        MerchantAreaResults? area = SelectedMerchantAreaResults;
        if (area == null || !area.Response.Success || area.Response.Rows.Count >= area.Response.TotalCount)
            return;
        int version = ++_merchantSearchVersion;
        SetStatus("正在加载更多商会目标……");
        FinderBackendClient.SearchMerchants(CreateMerchantRequest(area.AreaId, area.Response.Page + 1), response =>
        {
            if (version != _merchantSearchVersion || _window?.IsShowing != true || !response.Success) return;
            area.Response = response with { Rows = area.Response.Rows.Concat(response.Rows).ToArray() };
            ClearStatus();
            _merchantResultsContent.SetValue(BuildMerchantResults());
        });
    }

    private void SearchRenxia() => SearchRenxia(forceFullMap: false);

    private void SearchRenxia(bool forceFullMap)
    {
        if (_catalog == null || !HasRenxiaFilter) return;
        if (!SupportsRenxiaSearch)
        {
            SetStatus("任侠查找需要重启游戏后才能使用。新版后端尚未载入。");
            RefreshActivePage();
            return;
        }
        IReadOnlyList<short> areas = AreaSearchPlan.BuildSearchOrder(
            _catalog.Areas.Select(area => area.AreaId), _catalog.CurrentAreaId);
        if (areas.Count == 0) return;

        int version = ++_renxiaSearchVersion;
        _renxiaSearchCacheValid = false;
        _renxiaSearchInFlight = true;
        _renxiaSearchCompleted = 0;
        _renxiaSearchTotal = areas.Count;
        _renxiaAreaResults = Array.Empty<RenxiaAreaResults>();
        _renxiaAreaTabs.Replace(Array.Empty<short>(), notify: false);
        SetStatus("正在查询任侠……");
        _renxiaResultsContent.SetValue(BuildRenxiaResults());
        int gradeMask = _renxiaGrades.Selected.Aggregate(0, (mask, value) => mask | (1 << value));
        SearchRenxiaArea(version, areas, 0, gradeMask, forceFullMap, new List<RenxiaAreaResults>());
    }

    private void SearchRenxiaArea(
        int version,
        IReadOnlyList<short> areas,
        int index,
        int gradeMask,
        bool forceFullMap,
        List<RenxiaAreaResults> results)
    {
        FinderBackendClient.SearchRenxia(new RenxiaSearchRequestView(areas[index], gradeMask), response =>
        {
            if (version != _renxiaSearchVersion || _window?.IsShowing != true) return;
            _renxiaSearchCompleted++;
            if (!response.Success)
            {
                if (index == 0)
                {
                    _renxiaSearchInFlight = false;
                    SetStatus(response.Message);
                    _renxiaResultsContent.SetValue(BuildRenxiaResults());
                    return;
                }
                SearchNextRenxiaArea(version, areas, index, gradeMask, forceFullMap, results);
                return;
            }

            if (response.TotalCount > 0)
                results.Add(new RenxiaAreaResults(areas[index], response));
            if (index == 0 && !AreaSearchPlan.ShouldSearchBeyondCurrentArea(
                currentAreaHasResults: results.Count > 0, forceFullMap))
            {
                CompleteRenxiaSearch(results);
                return;
            }
            SearchNextRenxiaArea(version, areas, index, gradeMask, forceFullMap, results);
        });
    }

    private void SearchNextRenxiaArea(
        int version,
        IReadOnlyList<short> areas,
        int index,
        int gradeMask,
        bool forceFullMap,
        List<RenxiaAreaResults> results)
    {
        int next = index + 1;
        if (next >= areas.Count)
        {
            CompleteRenxiaSearch(results);
            return;
        }
        SetStatus($"正在搜索全地图（{_renxiaSearchCompleted}/{_renxiaSearchTotal}）……");
        SearchRenxiaArea(version, areas, next, gradeMask, forceFullMap, results);
    }

    private void CompleteRenxiaSearch(IReadOnlyList<RenxiaAreaResults> results)
    {
        _renxiaSearchInFlight = false;
        _renxiaSearchCacheValid = true;
        _renxiaAreaResults = AreaSearchPlan.OrderByResultCount(
            results, area => area.Response.TotalCount, area => area.AreaId);
        _renxiaAreaTabs.Replace(_renxiaAreaResults.Take(1).Select(area => area.AreaId), notify: false);
        ClearStatus();
        _renxiaResultsContent.SetValue(BuildRenxiaResults());
    }

    private void ResetCombat()
    {
        _combatSect = _combatType = -1; _combatSkill = -1;
        ClearCombatResults();
        RefreshActivePage();
    }

    private void ResetLife()
    {
        _lifeType = -1; _lifeSkill = -1;
        ClearLifeResults();
        RefreshActivePage();
    }

    private void ResetPeople()
    {
        _personName.SetValueWithoutNotify(string.Empty); _personGrades.Clear(); _personGenders.Clear();
        foreach (AbilityFilterState filter in _abilityFilters)
        {
            filter.Enabled.SetValueWithoutNotify(false); filter.LifeSkillType = 0; filter.Metric = 0;
            filter.Minimum.SetValueWithoutNotify(80);
        }
        ClearPersonResults();
        RefreshActivePage();
    }

    private void ResetRenxia()
    {
        _renxiaGrades.Clear();
        ClearRenxiaResults();
        RefreshActivePage();
    }

    private void SelectArea(short areaId, bool markStale = true, bool refresh = true)
    {
        if (_catalog == null) return;
        AreaOptionView? area = _catalog.Areas.FirstOrDefault(item => item.AreaId == areaId);
        if (area == null) return;
        _selectedAreaId = area.AreaId; _areaCategory = area.Category;
        if (markStale) MarkAllStale(invalidateCombat: false);
        if (markStale && _activeTab == 2) SchedulePersonSearch();
        if (markStale && _activeTab == 3) ScheduleMerchantSearch();
        if (markStale && _activeTab == 4) ScheduleRenxiaSearch();
        if (markStale && refresh)
        {
            RefreshActivePage();
            return;
        }
        if (refresh) RefreshActivePage();
    }

    private void MarkAllStale(bool invalidateCombat = true)
    {
        _requestVersion++;
        if (invalidateCombat) ClearCombatResults();
        ClearLifeResults();
        ClearPersonResults();
        ClearMerchantResults();
        ClearRenxiaResults();
        ClearStatus();
    }

    private void MarkBookStale(sbyte kind)
    {
        if (kind == 0)
        {
            ClearCombatResults();
        }
        else
        {
            ClearLifeResults();
        }
    }

    private void ClearLifeResults()
    {
        _lifeHoldingsVersion++;
        _lifeSearchCacheValid = false;
        _lifeSearchInFlight = false;
        _lifeSearchCompleted = 0;
        _lifeSearchTotal = 0;
        _lifeAreaHoldings = Array.Empty<LifeAreaHoldings>();
        _lifeAreaTabs.Replace(Array.Empty<short>(), notify: false);
        _lifeTaiwuKnowledge = TaiwuBookKnowledge.Empty;
    }

    private void ClearPersonResults()
    {
        _personSearchVersion++;
        _personSearchCacheValid = false;
        _personSearchInFlight = false;
        _personSearchCompleted = 0;
        _personSearchTotal = 0;
        _personAreaResults = Array.Empty<PersonAreaResults>();
        _personAreaTabs.Replace(Array.Empty<short>(), notify: false);
        _personTable.SetItems(Array.Empty<PersonRowView>());
    }

    private void ClearMerchantResults()
    {
        _merchantSearchVersion++;
        _merchantSearchCacheValid = false;
        _merchantSearchInFlight = false;
        _merchantSearchCompleted = 0;
        _merchantSearchTotal = 0;
        _merchantAreaResults = Array.Empty<MerchantAreaResults>();
        _merchantAreaTabs.Replace(Array.Empty<short>(), notify: false);
        _merchantTable.SetItems(Array.Empty<MerchantRowView>());
    }

    private void ClearRenxiaResults()
    {
        _renxiaSearchVersion++;
        _renxiaSearchCacheValid = false;
        _renxiaSearchInFlight = false;
        _renxiaSearchCompleted = 0;
        _renxiaSearchTotal = 0;
        _renxiaAreaResults = Array.Empty<RenxiaAreaResults>();
        _renxiaAreaTabs.Replace(Array.Empty<short>(), notify: false);
        _renxiaTable.SetItems(Array.Empty<RenxiaRowView>());
    }

    private void CancelInFlightSearches()
    {
        if (_combatSearchInFlight) ClearCombatResults();
        if (_lifeSearchInFlight) ClearLifeResults();
        if (_personSearchInFlight) ClearPersonResults();
        if (_merchantSearchInFlight) ClearMerchantResults();
        if (_renxiaSearchInFlight) ClearRenxiaResults();
    }

    private void ClearCombatResults()
    {
        _combatHoldingsVersion++;
        _combatSearchCacheValid = false;
        _combatSearchInFlight = false;
        _combatSearchCompleted = 0;
        _combatSearchTotal = 0;
        _combatAreaHoldings = Array.Empty<CombatAreaHoldings>();
        _combatAreaTabs.Replace(Array.Empty<short>(), notify: false);
        _combatTaiwuKnowledge = TaiwuBookKnowledge.Empty;
    }

    private void Render()
    {
        UiWindow document = BuildDocument();
        if (_window == null) _window = TaiwuUiApi.Mount(document);
        else _window.Render(document);
    }

    private UiElement BuildActiveTabPage() => _activeTab switch
    {
        0 => BuildTabPage(BuildCombatPage(), "combat", showRegionSelector: false),
        1 => BuildTabPage(BuildLifePage(), "life", showRegionSelector: false),
        2 => BuildTabPage(BuildPersonPage(), "person", showRegionSelector: false),
        3 => BuildTabPage(BuildMerchantPage(), "merchant", showRegionSelector: false),
        4 => BuildTabPage(BuildRenxiaPage(), "renxia", showRegionSelector: false),
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

    // ApiVersion 3 added the renxia search method; older backends only get it
    // after a game restart, so the tab shows a restart hint instead.
    private bool SupportsRenxiaSearch => _catalog?.ApiVersion >= 3;

    private bool CanMarkArea(short areaId) => _catalog?.CurrentAreaId == areaId;

    private bool CanMarkSelectedArea => CanMarkArea(_selectedAreaId);

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
