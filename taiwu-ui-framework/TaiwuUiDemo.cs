using UnityEngine;

namespace TaiwuUi;

/// <summary>Runtime visual fixtures used by MCP validation.</summary>
public static class TaiwuUiDemo
{
    private static ITaiwuWindow? _window;
    private static readonly TaiwuChoiceOption<string>[] Filters =
    {
        new("plain", "普通"), new("good", "优秀"), new("rare", "卓越"), new("legend", "绝世"),
    };

    public static void CloseCurrent()
    {
        _window?.Dispose();
        _window = null;
    }

    public static void ShowBasicText() => Open("basic", "声明式基础元素", Ui.Column(
        Ui.Heading("声明式 element tree") with { Key = "heading" },
        Ui.Text("props、events 与 children 组合为不可变界面声明。") with { Key = "body" },
        Ui.Muted($"API {TaiwuUiApi.ApiVersion} / Major {TaiwuUiApi.ApiMajor}") with { Key = "version" },
        Ui.Divider() with { Key = "divider" },
        Ui.Row(
            Ui.Button("主要操作", () => Debug.Log("TaiwuUi primary")) with { Key = "primary" },
            Ui.Button("次要操作", () => Debug.Log("TaiwuUi secondary"),
                new TaiwuButtonOptions { Style = TaiwuButtonStyle.Secondary }) with { Key = "secondary" })
            with { Key = "actions" }) with { Key = "root" });

    public static void ShowToggle()
    {
        var value = new TaiwuValue<bool>(true);
        Open("toggle", "原生 Checkbox", Ui.Column(
            Ui.Checkbox(value, "显示隐藏地格") with { Key = "visible" },
            Ui.Checkbox(new TaiwuValue<bool>(false), "仅显示重要地格") with { Key = "important" },
            Ui.ResetIcon(value.Reset) with { Key = "reset" }) with { Key = "root" });
    }

    public static void ShowSearchInput()
    {
        var query = new TaiwuValue<string>(string.Empty);
        Open("search", "村民名册搜索", Ui.Column(
            Ui.SearchInput(query, "输入姓名") with { Key = "search" },
            Ui.Muted("复用村民名册顶部的原生搜索框与清除按钮。") with { Key = "hint" })
            with { Key = "root" });
    }

    public static void ShowSlider()
    {
        var value = new TaiwuValue<float>(18f);
        Open("slider", "原生年龄滑块", Ui.Column(
            Ui.Slider("年龄", value, 0f, 100f) with { Key = "age" },
            Ui.ResetIcon(value.Reset) with { Key = "reset" }) with { Key = "root" });
    }

    public static void ShowRangeSlider()
    {
        var value = new TaiwuValue<TaiwuRange>(new TaiwuRange(12f, 68f));
        Open("range", "原生区域滑块", Ui.Column(
            Ui.RangeSlider("区域", value, 0f, 100f) with { Key = "region" },
            Ui.ResetIcon(value.Reset) with { Key = "reset" }) with { Key = "root" });
    }

    public static void ShowFilterButtons()
    {
        var selected = new TaiwuSelection<string>(TaiwuSelectionMode.Multiple, new[] { "plain" });
        Open("filters", "原生筛选按钮", Ui.Column(
            Ui.FilterButtons("资质", selected, Filters) with { Key = "filters" },
            Ui.ResetIcon(selected.Reset) with { Key = "reset" }) with { Key = "root" });
    }

    public static void ShowSelectButtons()
    {
        var selected = new TaiwuSelection<string>(TaiwuSelectionMode.Single, new[] { "here" });
        Open("select-buttons", "紧凑单选按钮", Ui.SelectButtons(selected, new[]
        {
            new TaiwuChoiceOption<string>("here", "嵩山 76"),
            new TaiwuChoiceOption<string>("other", "然山 12"),
        }, compact: true) with { Key = "select-buttons" }, 960f, 360f);
    }

    public static void ShowTable()
    {
        var data = CreateTable();
        Open("table", "村民名册 Table",
            Ui.Table(data.Model, data.Columns, new TaiwuTableOptions { Height = 620f }) with { Key = "table" },
            1420f, 820f);
    }

    public static void ShowTableTabs()
    {
        var data = CreateTable();
        var selected = new TaiwuSelection<string>(TaiwuSelectionMode.Single, new[] { "roster" });
        Open("table-tabs", "Table 与 Tabs", Ui.Tabs(selected, new[]
        {
            new UiTabPage<string>("roster", "村民名册", Ui.Table(data.Model, data.Columns,
                new TaiwuTableOptions { Height = 560f }) with { Key = "table" }),
            new UiTabPage<string>("notes", "说明", Ui.Text("排序与 tabs 共用受控状态。") with { Key = "notes" }),
        }, new TaiwuTabViewOptions { Height = 650f }) with { Key = "tabs" }, 1420f, 850f);
    }

    public static void ShowBottomTabs()
    {
        var selected = new TaiwuSelection<string>(TaiwuSelectionMode.Single);
        Open("bottom-tabs", "手记底部 Tabs", Ui.BottomTabs(selected, new[]
        {
            new TaiwuChoiceOption<string>("all", "全部"),
            new TaiwuChoiceOption<string>("main", "主线"),
            new TaiwuChoiceOption<string>("done", "已完成"),
        }) with { Key = "bottom" }, 1280f, 460f);
    }

    public static void ShowNavigation()
    {
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
                new TaiwuNavigationGroup<string>("growth", "人物成长", new[]
                {
                    new TaiwuNavigationItem<string>("traits", "出生特质"),
                    new TaiwuNavigationItem<string>("skills", "技艺与功法"),
                }),
            });
        Open("navigation", "百晓册导航", Ui.Navigation(navigation) with { Key = "navigation" }, 720f, 720f);
    }

    public static void ShowEncyclopediaLayout()
    {
        var selected = new TaiwuSelection<string>(TaiwuSelectionMode.Single, new[] { "home" });
        var tabs = new TaiwuTabsModel<string>(selected, new[]
        {
            new TaiwuTabItem<string>("home", "主页", TaiwuIcons.Home),
            new TaiwuTabItem<string>("world", "世界", TaiwuIcons.World),
            new TaiwuTabItem<string>("people", "人物", TaiwuIcons.People),
            new TaiwuTabItem<string>("study", "研读", TaiwuIcons.Study),
        });
        Open(new UiWindow("TaiwuUi.Core", "encyclopedia", Ui.Column(
            Ui.IconTabs(tabs) with { Key = "tabs" },
            Ui.Text("全屏背景、标题、关闭按钮与内容区域由窗口宿主统一处理。") with { Key = "body" })
            with { Key = "root" }, "百晓册", 1920f, 1080f,
            presentation: TaiwuWindowPresentation.Encyclopedia));
    }

    public static void ShowNativeSliderAndFilters()
    {
        var age = new TaiwuValue<float>(18f);
        var region = new TaiwuValue<TaiwuRange>(new TaiwuRange(20f, 80f));
        var selected = new TaiwuSelection<string>(TaiwuSelectionMode.Multiple, new[] { "plain" });
        Open("native-slider-filters", "查找地格原生控件", Ui.Column(
            Ui.Slider("年龄", age, 0f, 100f) with { Key = "age" },
            Ui.RangeSlider("区域", region, 0f, 100f) with { Key = "region" },
            Ui.FilterButtons("资质", selected, Filters) with { Key = "filters" }) with { Key = "root" },
            1240f, 760f);
    }

    public static void ShowCheckboxAndResetIcons()
    {
        var value = new TaiwuValue<bool>(true);
        Open("checkbox-reset", "Checkbox 与刷新", Ui.Row(
            Ui.Checkbox(value, "显示隐藏地格") with { Key = "checkbox" },
            Ui.ResetIcon(value.Reset) with { Key = "reset" },
            Ui.RefreshIcon(() => Debug.Log("TaiwuUi refresh")) with { Key = "refresh" })
            with { Key = "actions" });
    }

    public static void ShowAll()
    {
        var query = new TaiwuValue<string>(string.Empty);
        var enabled = new TaiwuValue<bool>(true);
        var age = new TaiwuValue<float>(18f);
        var range = new TaiwuValue<TaiwuRange>(new TaiwuRange(20f, 80f));
        var selected = new TaiwuSelection<string>(TaiwuSelectionMode.Multiple, new[] { "plain" });
        var data = CreateTable();
        Open("all-v2", "Taiwu UI v2 全组件", Ui.Scroll(Ui.Column(
            Ui.SearchInput(query, "输入姓名") with { Key = "search" },
            Ui.Row(Ui.Checkbox(enabled, "显示隐藏地格") with { Key = "checkbox" },
                Ui.ResetIcon(enabled.Reset) with { Key = "reset" },
                Ui.RefreshIcon(() => { }) with { Key = "refresh" }) with { Key = "actions" },
            Ui.Slider("年龄", age, 0f, 100f) with { Key = "age" },
            Ui.RangeSlider("区域", range, 0f, 100f) with { Key = "region" },
            Ui.FilterButtons("资质", selected, Filters) with { Key = "filters" },
            Ui.Table(data.Model, data.Columns, new TaiwuTableOptions { Height = 420f }) with { Key = "table" })
            with { Key = "content" }, new TaiwuScrollOptions { Height = 820f }) with { Key = "scroll" },
            1500f, 980f);
    }

    public static void ShowHotReplacement()
    {
        CloseCurrent();
        var query = new TaiwuValue<string>("保留的受控状态");
        var initial = new UiWindow("TaiwuUi.Core", "hot-replace", Ui.Column(
            Ui.SearchInput(query, "输入内容") with { Key = "search" },
            Ui.Text("替换前") with { Key = "status" }) with { Key = "root" },
            "Keyed Reconciliation", 1080f, 620f);
        _window = TaiwuUiApi.Mount(initial);
        _window.Show();

        var invalid = initial with
        {
            Content = Ui.Column(
                Ui.Text("重复 A") with { Key = "duplicate" },
                Ui.Text("重复 B") with { Key = "duplicate" }),
        };
        try
        {
            _window.Render(invalid);
            throw new InvalidOperationException("Invalid replacement unexpectedly succeeded.");
        }
        catch (UiValidationException)
        {
            // Expected: the mounted window remains intact.
        }

        var updated = initial with
        {
            Content = Ui.Column(
                Ui.SearchInput(query, "输入内容") with { Key = "search" },
                Ui.Text("原子替换完成；无效 render 已回滚。") with { Key = "status" },
                Ui.Muted("相同 key + 相同类型保留运行时状态。") with { Key = "detail" })
                with { Key = "root" },
        };
        _window.Render(updated);
    }

    private static void Open(string id, string title, UiElement content, float width = 1080f, float height = 700f) =>
        Open(new UiWindow("TaiwuUi.Core", id, content, title, width, height));

    private static void Open(UiWindow document)
    {
        CloseCurrent();
        UiValidationResult validation = TaiwuUiApi.Validate(document);
        if (!validation.IsValid)
            throw new UiValidationException(validation.Errors);
        _window = TaiwuUiApi.Mount(document);
        _window.Show();
    }

    private static TableFixture CreateTable()
    {
        var model = new TaiwuTableModel<DemoRow, string>(row => row.Name);
        model.SetItems(new[]
        {
            new DemoRow("阿青", "太吾村", 18, "村民"), new DemoRow("宁越", "伏龙坛", 78, "门人"),
            new DemoRow("商妤", "璇女派", 34, "掌门"), new DemoRow("闻舟", "五仙教", 46, "长老"),
            new DemoRow("陆沉", "百花谷", 25, "弟子"), new DemoRow("江眠", "然山派", 63, "执事"),
        });
        return new TableFixture(model, new[]
        {
            new TaiwuTableColumn<DemoRow>("name", "姓名", row => row.Name, 260f, true, row => row.Name),
            new TaiwuTableColumn<DemoRow>("place", "所在", row => row.Place, 320f, true, row => row.Place),
            new TaiwuTableColumn<DemoRow>("age", "年龄", row => row.Age.ToString(), 180f, true, row => row.Age),
            new TaiwuTableColumn<DemoRow>("identity", "身份", row => row.Identity, 260f, true, row => row.Identity),
        });
    }

    private sealed record DemoRow(string Name, string Place, int Age, string Identity);
    private sealed record TableFixture(
        TaiwuTableModel<DemoRow, string> Model,
        TaiwuTableColumn<DemoRow>[] Columns);
}
