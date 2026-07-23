namespace TaiwuUi;

/// <summary>Immutable declaration consumed by the framework and composed by consumer MODs.</summary>
public abstract record UiElement
{
    /// <summary>Stable identity among siblings. Required for dynamic collections.</summary>
    public string? Key { get; init; }

    internal virtual IEnumerable<UiElement> ChildElements => Array.Empty<UiElement>();
    internal abstract UiNode Compile();
}

public sealed record UiColumnElement(IReadOnlyList<UiElement> Children, float Spacing = 2f) : UiElement
{
    internal override IEnumerable<UiElement> ChildElements => Children;
    internal override UiNode Compile() =>
        new ColumnNode(UiElementCompiler.CompileChildren(Children), Math.Max(0f, Spacing));
}

public sealed record UiRowElement(IReadOnlyList<UiElement> Children, float Spacing = 12f) : UiElement
{
    internal override IEnumerable<UiElement> ChildElements => Children;
    internal override UiNode Compile() =>
        new RowNode(UiElementCompiler.CompileChildren(Children), Math.Max(0f, Spacing));
}

/// <summary>Responsive layout slot with an explicit grow ratio and zero content basis.</summary>
public sealed record UiFlexElement(UiElement Content, float Grow = 1f) : UiElement
{
    internal override IEnumerable<UiElement> ChildElements => new[] { Content };
    internal override UiNode Compile() =>
        new FlexNode(new List<UiNode> { UiElementCompiler.CompileElement(Content) }, Math.Max(0f, Grow));
}

/// <summary>Locally replaceable fragment that does not remount its surrounding window.</summary>
public sealed record UiDynamicElement(TaiwuValue<UiElement> Content, float Height) : UiElement
{
    internal override IEnumerable<UiElement> ChildElements => new[] { Content.Value };
    internal override UiNode Compile() => new DynamicNode(
        Content,
        new List<UiNode> { UiElementCompiler.CompileElement(Content.Value) },
        Math.Max(0f, Height));
}

public sealed record UiTextElement(string Text, TaiwuTextOptions Options) : UiElement
{
    internal override UiNode Compile() => new TextNode(Text ?? string.Empty, Options with { });
}

public sealed record UiButtonElement(string Label, Action OnClick, TaiwuButtonOptions Options) : UiElement
{
    internal override UiNode Compile() => new ButtonNode(Label ?? string.Empty, OnClick, Options with { });
}

public sealed record UiDividerElement : UiElement
{
    internal override UiNode Compile() => new DividerNode();
}

public sealed record UiSpacerElement(float Height) : UiElement
{
    internal override UiNode Compile() => new SpacerNode(Math.Max(0f, Height));
}

public sealed record UiScrollElement(UiElement Content, TaiwuScrollOptions Options) : UiElement
{
    internal override IEnumerable<UiElement> ChildElements => new[] { Content };
    internal override UiNode Compile() =>
        new ScrollNode(UiElementCompiler.CompileContent(Content), Options with { });
}

public sealed record UiSearchInputElement(
    TaiwuValue<string> Value,
    string Placeholder = "",
    float Width = 459f) : UiElement
{
    internal override UiNode Compile() => new SearchInputNode(Value, Placeholder ?? string.Empty, Width);
}

public sealed record UiCheckboxElement(TaiwuValue<bool> Value, string Label = "") : UiElement
{
    internal override UiNode Compile() => new ToggleNode(Label ?? string.Empty, Value);
}

public sealed record UiSliderElement(
    string Label,
    TaiwuValue<float> Value,
    float Minimum,
    float Maximum,
    float Step = 1f) : UiElement
{
    internal override UiNode Compile() => new SliderNode(Label ?? string.Empty, Value, Minimum, Maximum, Step);
}

public sealed record UiRangeSliderElement(
    string Label,
    TaiwuValue<TaiwuRange> Value,
    float Minimum,
    float Maximum,
    float Step = 1f) : UiElement
{
    internal override UiNode Compile() => new RangeSliderNode(Label ?? string.Empty, Value, Minimum, Maximum, Step);
}

public enum UiActionIcon
{
    Reset,
    Refresh,
}

public sealed record UiActionIconElement(UiActionIcon Icon, Action OnClick, float Size = 52f) : UiElement
{
    internal override UiNode Compile() => new NativeActionIconNode(
        Icon == UiActionIcon.Reset ? NativeActionIcon.Reset : NativeActionIcon.Refresh,
        OnClick,
        Size);
}

public sealed record UiFilterButtonsElement<T>(
    string Label,
    TaiwuSelection<T> Selection,
    IReadOnlyList<TaiwuChoiceOption<T>> Items,
    bool Compact = false) : UiElement
{
    internal override UiNode Compile() => new ChoiceGroupNode(
        Label ?? string.Empty,
        ElementStateProjection.Choices(Selection, Items),
        Compact);
}

/// <summary>
/// A compact exclusive button group. Unlike <see cref="UiFilterButtonsElement{T}"/>,
/// clicking the active option keeps it selected, making it suitable for content sheets.
/// </summary>
public sealed record UiSelectButtonsElement<T>(
    TaiwuSelection<T> Selection,
    IReadOnlyList<TaiwuChoiceOption<T>> Items,
    bool Compact = false) : UiElement
{
    internal override UiNode Compile()
    {
        if (Selection.Mode != TaiwuSelectionMode.Single)
            throw new ArgumentException("Select buttons require single selection.", nameof(Selection));
        return new ChoiceGroupNode(string.Empty,
            ElementStateProjection.Choices(Selection, Items), Compact, selectOnly: true);
    }
}

/// <summary>
/// A compact, exclusive sheet selector with individually framed buttons.
/// Use it for a local content scope such as regions; it is intentionally less
/// prominent than primary navigation tabs.
/// </summary>
public sealed record UiSheetTabsElement<T>(
    TaiwuSelection<T> Selection,
    IReadOnlyList<TaiwuChoiceOption<T>> Items) : UiElement
{
    internal override UiNode Compile()
    {
        if (Selection.Mode != TaiwuSelectionMode.Single)
            throw new ArgumentException("Sheet tabs require single selection.", nameof(Selection));
        return new ChoiceGroupNode(string.Empty,
            ElementStateProjection.Choices(Selection, Items), compact: true, selectOnly: true,
            appearance: ChoiceGroupAppearance.SheetTab);
    }
}

/// <summary>A single-selection button that opens a native-styled floating choice panel.</summary>
public sealed record UiPopupSelectElement<T>(
    string Label,
    TaiwuSelection<T> Selection,
    IReadOnlyList<TaiwuChoiceOption<T>> Items,
    TaiwuPopupSelectOptions Options) : UiElement
{
    internal override UiNode Compile()
    {
        if (Selection.Mode != TaiwuSelectionMode.Single)
            throw new ArgumentException("Popup selectors require single selection.", nameof(Selection));
        return new PopupSelectNode(
            Label ?? string.Empty,
            ElementStateProjection.Choices(Selection, Items),
            Options with { });
    }
}

/// <summary>A compact trigger that opens a card containing dependent selection fields.</summary>
public sealed record UiPopupCardElement(
    string Label,
    TaiwuPopupCardModel Model,
    TaiwuPopupCardOptions Options) : UiElement
{
    internal override UiNode Compile() => new PopupCardNode(
        Label ?? string.Empty,
        Model ?? throw new ArgumentNullException(nameof(Model)),
        Options with { });
}

public sealed record UiIconTabsElement<T>(
    TaiwuTabsModel<T> Model,
    TaiwuIconTabsOptions Options) : UiElement
{
    internal override UiNode Compile() => new TabsNode(
        ElementStateProjection.Tabs(Model), TabsNodeStyle.Icon,
        Options.Height, Options.MinimumItemWidth);
}

public sealed record UiClosableTabsElement<T>(
    TaiwuTabsModel<T> Model,
    TaiwuClosableTabsOptions Options) : UiElement
{
    internal override UiNode Compile() => new TabsNode(
        ElementStateProjection.Tabs(Model), TabsNodeStyle.Closable,
        Options.Height, Options.MinimumItemWidth, Options.ShowClearButton);
}

public sealed record UiNavigationElement<T>(
    TaiwuNavigationModel<T> Model,
    TaiwuNavigationOptions Options) : UiElement
{
    internal override UiNode Compile() =>
        new NavigationNode(ElementStateProjection.Navigation(Model), Options);
}

public sealed record UiBottomTabsElement<T>(
    TaiwuSelection<T> Selection,
    IReadOnlyList<TaiwuChoiceOption<T>> Items,
    TaiwuBottomTabsOptions Options) : UiElement
{
    internal override UiNode Compile()
    {
        if (Selection.Mode != TaiwuSelectionMode.Single)
            throw new ArgumentException("Bottom tabs require single selection.", nameof(Selection));
        if (Items.Count > 0 && Selection.Selected.Count == 0)
            Selection.Select(Items[0].Value);
        return new BottomTabsNode(ElementStateProjection.Choices(Selection, Items), Options);
    }
}

public sealed record UiTableElement<TRow, TKey>(
    TaiwuTableModel<TRow, TKey> Model,
    IReadOnlyList<TaiwuTableColumn<TRow>> Columns,
    TaiwuTableOptions Options) : UiElement where TKey : notnull
{
    internal override UiNode Compile() =>
        new TableNode(ElementStateProjection.Table(Model, Columns), Options);
}

public sealed record UiTabPage<T>(T Value, string Label, UiElement Content, bool Interactable = true);

public sealed record UiTabViewElement<T>(
    TaiwuSelection<T> Selection,
    IReadOnlyList<UiTabPage<T>> Pages,
    TaiwuTabViewOptions Options) : UiElement
{
    internal override IEnumerable<UiElement> ChildElements => Pages.Select(page => page.Content);
    internal override UiNode Compile()
    {
        if (Selection.Mode != TaiwuSelectionMode.Single)
            throw new ArgumentException("Tab views require single selection.", nameof(Selection));
        if (Pages.Count > 0 && Selection.Selected.Count == 0)
            Selection.Select(Pages[0].Value);
        var pageNodes = Pages
            .Select(page => new TabPageNode(UiElementCompiler.CompileContent(page.Content)))
            .ToArray();
        return new TabViewNode(ElementStateProjection.TabView(Selection, Pages), pageNodes, Options);
    }
}

public sealed record NativeAssetRef
{
    public string Name { get; }

    public NativeAssetRef(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Native asset name is required.", nameof(name));
        Name = name.Trim();
    }
}

public sealed record UiNativeImageElement(NativeAssetRef Asset, float Width, float Height) : UiElement
{
    internal override UiNode Compile() => new NativeImageNode(Asset, Width, Height);
}

public sealed record UiWindow
{
    public string OwnerId { get; init; }
    public string WindowId { get; init; }
    public string Title { get; init; }
    public float Width { get; init; }
    public float Height { get; init; }
    public TaiwuWindowLayer Layer { get; init; }
    public TaiwuWindowCover Cover { get; init; }
    public TaiwuWindowPresentation Presentation { get; init; }
    public UiElement Content { get; init; }
    public string Key => OwnerId + ":" + WindowId;

    public UiWindow(
        string ownerId,
        string windowId,
        UiElement content,
        string title = "",
        float width = 960f,
        float height = 640f,
        TaiwuWindowLayer layer = TaiwuWindowLayer.Popup,
        TaiwuWindowCover cover = TaiwuWindowCover.Full,
        TaiwuWindowPresentation presentation = TaiwuWindowPresentation.Dialog)
    {
        OwnerId = ownerId?.Trim() ?? string.Empty;
        WindowId = windowId?.Trim() ?? string.Empty;
        Content = content ?? throw new ArgumentNullException(nameof(content));
        Title = title ?? string.Empty;
        Width = width;
        Height = height;
        Layer = layer;
        Cover = cover;
        Presentation = presentation;
    }
}

public sealed record UiValidationIssue(string Path, string Message);

public sealed class UiValidationResult
{
    public IReadOnlyList<UiValidationIssue> Errors { get; }
    public IReadOnlyList<UiValidationIssue> Warnings { get; }
    public bool IsValid => Errors.Count == 0;

    internal UiValidationResult(
        IReadOnlyList<UiValidationIssue> errors,
        IReadOnlyList<UiValidationIssue> warnings)
    {
        Errors = errors;
        Warnings = warnings;
    }
}

/// <summary>Typed factories for the declarative element tree.</summary>
public static class Ui
{
    public static UiColumnElement Column(params UiElement[] children) => new(children);
    public static UiRowElement Row(params UiElement[] children) => new(children);
    public static UiFlexElement Flex(UiElement content, float grow = 1f) =>
        new(content ?? throw new ArgumentNullException(nameof(content)), grow);
    public static UiDynamicElement Dynamic(TaiwuValue<UiElement> content, float height) =>
        new(content ?? throw new ArgumentNullException(nameof(content)), height);
    public static UiTextElement Text(string text, TaiwuTextOptions? options = null) =>
        new(text ?? string.Empty, options ?? new TaiwuTextOptions());
    public static UiTextElement Heading(string text) => new(text ?? string.Empty, new TaiwuTextOptions
    {
        FontSize = TaiwuUiMetrics.HeadingFontSize,
        MinimumHeight = TaiwuUiMetrics.HeadingTextHeight,
        Style = TaiwuTextStyle.Heading,
    });
    public static UiTextElement Muted(string text) => new(text ?? string.Empty, new TaiwuTextOptions
    {
        FontSize = TaiwuUiMetrics.MutedFontSize,
        MinimumHeight = TaiwuUiMetrics.MutedTextHeight,
        Style = TaiwuTextStyle.Muted,
    });
    public static UiButtonElement Button(string label, Action onClick, TaiwuButtonOptions? options = null) =>
        new(label ?? string.Empty, onClick ?? throw new ArgumentNullException(nameof(onClick)),
            options ?? new TaiwuButtonOptions());
    public static UiDividerElement Divider() => new();
    public static UiSpacerElement Spacer(float height = 16f) => new(height);
    public static UiScrollElement Scroll(UiElement content, TaiwuScrollOptions? options = null) =>
        new(content, options ?? new TaiwuScrollOptions());
    public static UiSearchInputElement SearchInput(
        TaiwuValue<string> value, string placeholder = "", float width = 459f) =>
        new(value, placeholder, width);
    public static UiCheckboxElement Checkbox(TaiwuValue<bool> value, string label = "") =>
        new(value, label);
    public static UiSliderElement Slider(
        string label, TaiwuValue<float> value, float minimum, float maximum, float step = 1f) =>
        new(label, value, minimum, maximum, step);
    public static UiRangeSliderElement RangeSlider(
        string label, TaiwuValue<TaiwuRange> value, float minimum, float maximum, float step = 1f) =>
        new(label, value, minimum, maximum, step);
    public static UiActionIconElement ResetIcon(Action onReset, float size = 52f) =>
        new(UiActionIcon.Reset, onReset, size);
    public static UiActionIconElement RefreshIcon(Action onRefresh, float size = 52f) =>
        new(UiActionIcon.Refresh, onRefresh, size);
    public static UiFilterButtonsElement<T> FilterButtons<T>(
        string label, TaiwuSelection<T> selection, IReadOnlyList<TaiwuChoiceOption<T>> items,
        bool compact = false) => new(label, selection, items, compact);
    public static UiSelectButtonsElement<T> SelectButtons<T>(
        TaiwuSelection<T> selection, IReadOnlyList<TaiwuChoiceOption<T>> items,
        bool compact = false) => new(selection, items, compact);
    public static UiSheetTabsElement<T> SheetTabs<T>(
        TaiwuSelection<T> selection, IReadOnlyList<TaiwuChoiceOption<T>> items) => new(selection, items);
    public static UiPopupSelectElement<T> PopupSelect<T>(
        string label, TaiwuSelection<T> selection, IReadOnlyList<TaiwuChoiceOption<T>> items,
        TaiwuPopupSelectOptions? options = null) =>
        new(label, selection, items, options ?? new TaiwuPopupSelectOptions());
    public static UiPopupCardElement PopupCard(
        string label, TaiwuPopupCardModel model, TaiwuPopupCardOptions? options = null) =>
        new(label ?? string.Empty, model ?? throw new ArgumentNullException(nameof(model)),
            options ?? new TaiwuPopupCardOptions());
    public static UiIconTabsElement<T> IconTabs<T>(
        TaiwuTabsModel<T> model, TaiwuIconTabsOptions? options = null) =>
        new(model, options ?? new TaiwuIconTabsOptions());
    public static UiClosableTabsElement<T> ClosableTabs<T>(
        TaiwuTabsModel<T> model, TaiwuClosableTabsOptions? options = null) =>
        new(model, options ?? new TaiwuClosableTabsOptions());
    public static UiNavigationElement<T> Navigation<T>(
        TaiwuNavigationModel<T> model, TaiwuNavigationOptions? options = null) =>
        new(model, options ?? new TaiwuNavigationOptions());
    public static UiBottomTabsElement<T> BottomTabs<T>(
        TaiwuSelection<T> selection, IReadOnlyList<TaiwuChoiceOption<T>> items,
        TaiwuBottomTabsOptions? options = null) =>
        new(selection, items, options ?? new TaiwuBottomTabsOptions());
    public static UiTableElement<TRow, TKey> Table<TRow, TKey>(
        TaiwuTableModel<TRow, TKey> model,
        IReadOnlyList<TaiwuTableColumn<TRow>> columns,
        TaiwuTableOptions? options = null) where TKey : notnull =>
        new(model, columns, options ?? new TaiwuTableOptions());
    public static UiTabViewElement<T> Tabs<T>(
        TaiwuSelection<T> selection, IReadOnlyList<UiTabPage<T>> pages,
        TaiwuTabViewOptions? options = null) =>
        new(selection, pages, options ?? new TaiwuTabViewOptions());
    public static UiNativeImageElement NativeImage(NativeAssetRef asset, float width, float height) =>
        new(asset, width, height);
}

internal static class UiElementCompiler
{
    internal static UiNode CompileElement(UiElement element)
    {
        if (element == null)
            throw new ArgumentException("Element cannot be null.", nameof(element));
        UiNode node = element.Compile();
        node.Key = element.Key;
        return node;
    }

    internal static List<UiNode> CompileContent(UiElement content) => content switch
    {
        UiColumnElement column => CompileChildren(column.Children),
        _ => new List<UiNode> { content.Compile() },
    };

    internal static List<UiNode> CompileChildren(IEnumerable<UiElement> children) =>
        children.Select(CompileElement).ToList();
}
