using TaiwuUi;

Expect(TaiwuUiApi.ApiMajor, 2, "v2 API major");

var search = new TaiwuValue<string>("seed");
var checkbox = new TaiwuValue<bool>(true);
var slider = new TaiwuValue<float>(18f);
var range = new TaiwuValue<TaiwuRange>(new TaiwuRange(10f, 40f));
var filterSelection = new TaiwuSelection<string>(
    TaiwuSelectionMode.Multiple, new[] { "plain" });
var selectButtons = new TaiwuSelection<string>(
    TaiwuSelectionMode.Single, new[] { "here" });
var sheetTabs = new TaiwuSelection<string>(
    TaiwuSelectionMode.Single, new[] { "here" });
var popupSelection = new TaiwuSelection<string>(
    TaiwuSelectionMode.Single, new[] { "sect" });
var popupCard = new TaiwuPopupCardModel(
    () => "门派 / 内功 / 九品",
    () => new TaiwuPopupCardField[]
    {
        new("门派", "门派", new[] { new TaiwuPopupCardOption("门派", true) }, _ => { }),
        new("分类", "内功", new[] { new TaiwuPopupCardOption("内功", true) }, _ => { }),
        new("功法", "九品", new[] { new TaiwuPopupCardOption("九品", true) }, _ => { }, CloseCardAfterSelect: true),
    });
var pageSelection = new TaiwuSelection<string>(
    TaiwuSelectionMode.Single, new[] { "people" });
var bottomSelection = new TaiwuSelection<string>(TaiwuSelectionMode.Single);
var dynamicContent = new TaiwuValue<UiElement>(Ui.Text("初始详情"));
var staleDynamicContent = new TaiwuValue<UiElement>(Ui.Text("stale"));
bool dynamicHostAlive = true;
int dynamicRenderCount = 0;
DynamicContentSubscription.Subscribe(
    staleDynamicContent,
    () => dynamicHostAlive,
    _ => dynamicRenderCount++);
dynamicHostAlive = false;
staleDynamicContent.SetValue(Ui.Text("discarded"));
Expect(dynamicRenderCount, 0, "destroyed dynamic host does not render");
dynamicHostAlive = true;
staleDynamicContent.SetValue(Ui.Text("detached"));
Expect(dynamicRenderCount, 0, "destroyed dynamic host unsubscribes itself");

var tabSelection = new TaiwuSelection<string>(
    TaiwuSelectionMode.Single, new[] { "world" });
var tabs = new TaiwuTabsModel<string>(tabSelection, new[]
{
    new TaiwuTabItem<string>("home", "主页", TaiwuIcons.Home),
    new TaiwuTabItem<string>("world", "世界", TaiwuIcons.World),
});

var navigation = new TaiwuNavigationModel<string>(
    new TaiwuSelection<string>(TaiwuSelectionMode.Single, new[] { "origin" }),
    new TaiwuSelection<string>(TaiwuSelectionMode.Multiple, new[] { "creation" }),
    new[]
    {
        new TaiwuNavigationGroup<string>("creation", "创建人物", new[]
        {
            new TaiwuNavigationItem<string>("basics", "基本信息"),
            new TaiwuNavigationItem<string>("origin", "出生地区"),
        }),
    });

var sort = new TaiwuValue<TaiwuSortState>(
    new TaiwuSortState("age", TaiwuSortDirection.Ascending));
var table = new TaiwuTableModel<Row, string>(row => row.Name, sort: sort);
table.SetItems(new[]
{
    new Row("older", 78),
    new Row("younger", 18),
});
var columns = new[]
{
    new TaiwuTableColumn<Row>("name", "姓名", row => row.Name),
    new TaiwuTableColumn<Row>("age", "年龄", row => row.Age.ToString(),
        sortable: true, sortValue: row => row.Age),
};

UiElement content = Ui.Column(
    Ui.SearchInput(search, "输入姓名") with { Key = "search" },
    Ui.Row(
        Ui.Checkbox(checkbox, "显示隐藏地格") with { Key = "hidden" },
        Ui.ResetIcon(checkbox.Reset) with { Key = "reset" },
        Ui.RefreshIcon(() => { }) with { Key = "refresh" }) with { Key = "actions" },
    Ui.FilterButtons("资质", filterSelection, new[]
    {
        new TaiwuChoiceOption<string>("plain", "普通"),
        new TaiwuChoiceOption<string>("good", "优秀"),
    }) with { Key = "filters" },
    Ui.SelectButtons(selectButtons, new[]
    {
        new TaiwuChoiceOption<string>("here", "本地"),
        new TaiwuChoiceOption<string>("other", "其他"),
    }, compact: true) with { Key = "select-buttons" },
    Ui.SheetTabs(sheetTabs, new[]
    {
        new TaiwuChoiceOption<string>("here", "嵩山 76"),
        new TaiwuChoiceOption<string>("other", "然山 12"),
    }) with { Key = "sheet-tabs" },
    Ui.PopupSelect("地域", popupSelection, new[]
    {
        new TaiwuChoiceOption<string>("sect", "门派地域"),
        new TaiwuChoiceOption<string>("city", "大城市"),
    }) with { Key = "popup-select" },
    Ui.PopupCard("功法", popupCard) with { Key = "popup-card" },
    Ui.Slider("年龄", slider, 0f, 100f) with { Key = "age" },
    Ui.RangeSlider("区域", range, 0f, 100f) with { Key = "region" },
    Ui.IconTabs(tabs) with { Key = "icons" },
    Ui.Navigation(navigation) with { Key = "navigation" },
    Ui.Tabs(pageSelection, new[]
    {
        new UiTabPage<string>("people", "村民", Ui.Table(table, columns) with { Key = "table" }),
        new UiTabPage<string>("status", "状态", Ui.Text("状态页") with { Key = "status-text" }),
    }) with { Key = "pages" },
    Ui.BottomTabs(bottomSelection, new[]
    {
        new TaiwuChoiceOption<string>("all", "全部"),
        new TaiwuChoiceOption<string>("main", "主线"),
    }) with { Key = "bottom" }) with { Key = "root" };

var window = new UiWindow(
    "contract", "declarative", content, "声明式 UI",
    width: 1280f, height: 900f,
    presentation: TaiwuWindowPresentation.Encyclopedia);

UiValidationResult validation = TaiwuUiApi.Validate(window);
Expect(validation.IsValid, true, "declarative window validates");
Expect(validation.Errors.Count, 0, "declarative window errors");
Expect(window.Key, "contract:declarative", "stable window key");
Expect(((UiColumnElement)window.Content).Children.Count, 13, "public element tree");
Expect(bottomSelection.IsSelected("all"), false, "validation has no state side effects");

var responsive = new UiWindow("contract", "responsive", Ui.Row(
    Ui.Flex(Ui.Text("主列表")) with { Key = "master" },
    Ui.Flex(Ui.Dynamic(dynamicContent, 480f)) with { Key = "detail" }));
Expect(TaiwuUiApi.Validate(responsive).IsValid, true, "flex dynamic layout validates");
dynamicContent.SetValue(Ui.Text("新详情"));
Expect(dynamicContent.Value, Ui.Text("新详情"), "dynamic content is controlled state");

var duplicateKeys = new UiWindow("contract", "duplicate", Ui.Column(
    Ui.Text("A") with { Key = "same" },
    Ui.Text("B") with { Key = "same" }));
UiValidationResult invalid = TaiwuUiApi.Validate(duplicateKeys);
Expect(invalid.IsValid, false, "duplicate sibling keys rejected");
Expect(invalid.Errors.Single().Path, "content/same", "duplicate key path");

var unkeyed = new UiWindow("contract", "warning", Ui.Column(
    Ui.Text("A"), Ui.Text("B")));
UiValidationResult warning = TaiwuUiApi.Validate(unkeyed);
Expect(warning.IsValid, true, "unkeyed static children remain valid");
Expect(warning.Warnings.Count, 1, "unkeyed dynamic warning");

var updatedWindow = window with
{
    Content = Ui.Column(
        Ui.SearchInput(search, "输入姓名或身份") with { Key = "search" },
        Ui.Text("替换 actions 类型") with { Key = "actions" },
        Ui.Text("新增") with { Key = "added" }) with { Key = "root" },
};
UiUpdatePreview update = TaiwuUiApi.PreviewUpdate(window, updatedWindow);
Expect(update.Reused.Contains("content/search"), true, "same key and type reused");
Expect(update.Replaced.Contains("content/actions"), true, "same key different type replaced");
Expect(update.Added.Contains("content/added"), true, "new key added");
Expect(update.Removed.Contains("content/filters"), true, "missing key removed");

search.SetValue("new value");
Expect(search.Value, "new value", "controlled value updates");
search.Reset();
Expect(search.Value, "seed", "controlled value resets");
filterSelection.Toggle("good");
Expect(filterSelection.IsSelected("good"), true, "controlled multiple selection");
tabSelection.Select("home");
Expect(tabSelection.IsSelected("home"), true, "controlled tab selection");
sheetTabs.Select("other");
Expect(sheetTabs.IsSelected("other"), true, "controlled sheet-tab selection");

ExpectThrows<ArgumentException>(() => new TaiwuTabsModel<string>(
    new TaiwuSelection<string>(TaiwuSelectionMode.Multiple)),
    "tabs reject multiple selection");
Console.WriteLine("TaiwuUi v2 declarative element contracts passed.");

static void ExpectThrows<TException>(Action action, string label)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }
    throw new InvalidOperationException($"{label}: expected {typeof(TException).Name}");
}

static void Expect<T>(T actual, T expected, string label)
{
    if (!EqualityComparer<T>.Default.Equals(actual, expected))
        throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
}

internal sealed record Row(string Name, int Age);
