namespace TaiwuUi;

internal abstract record ElementSnapshot;
internal abstract record ElementIntent;

internal sealed record ChoiceItemSnapshot(
    string Label,
    bool Selected,
    bool Interactable,
    TaiwuChoiceTone Tone = TaiwuChoiceTone.Neutral,
    bool Highlighted = false);
internal sealed record ChoiceSnapshot(IReadOnlyList<ChoiceItemSnapshot> Items) : ElementSnapshot;
internal sealed record ToggleChoiceIntent(int Index) : ElementIntent;
internal sealed record SelectChoiceIntent(int Index) : ElementIntent;

internal sealed record TabItemSnapshot(
    string Label, TaiwuIcon? Icon, bool Selected, bool Interactable, bool Closable);
internal sealed record TabsSnapshot(IReadOnlyList<TabItemSnapshot> Items) : ElementSnapshot;
internal sealed record SelectTabIntent(int Index) : ElementIntent;
internal sealed record CloseTabIntent(int Index) : ElementIntent;
internal sealed record ClearTabsIntent : ElementIntent;

internal sealed record NavigationItemSnapshot(string Label, bool Selected, bool Interactable);
internal sealed record NavigationGroupSnapshot(
    string Id, string Label, bool Expanded, bool Interactable,
    IReadOnlyList<NavigationItemSnapshot> Items);
internal sealed record NavigationSnapshot(IReadOnlyList<NavigationGroupSnapshot> Groups)
    : ElementSnapshot;
internal sealed record ToggleNavigationGroupIntent(int Group) : ElementIntent;
internal sealed record SelectNavigationItemIntent(int Group, int Item) : ElementIntent;

internal sealed record TableColumnSnapshot(
    string Id, string Header, float Width, bool Sortable);
internal sealed record TableRowSnapshot(
    IReadOnlyList<TaiwuTableCell> Cells,
    bool Selected,
    bool Disabled,
    IReadOnlyList<TaiwuMenuAction> Actions,
    TaiwuMenuAction? InlineAction);
internal sealed record TableSnapshot(
    IReadOnlyList<TableColumnSnapshot> Columns,
    IReadOnlyList<TableRowSnapshot> Rows,
    TaiwuSortState Sort,
    bool HasInlineActions) : ElementSnapshot;
internal sealed record ClickTableRowIntent(int Row) : ElementIntent;
internal sealed record CycleTableSortIntent(int Column) : ElementIntent;

/// <summary>Owns type erasure, snapshots, intents, subscriptions, and disposal for stateful elements.</summary>
internal sealed class ElementStateProjection : IDisposable
{
    private readonly Func<ElementSnapshot> _read;
    private readonly Action<ElementIntent> _dispatch;
    private readonly Action<Action> _subscribe;
    private readonly Action<Action> _unsubscribe;
    private readonly Action _onChanged;
    private Action? _changed;
    private bool _attached;

    /// <summary>
    /// Model attachment is lazy and re-attachable. Compiled nodes (and their
    /// projections) outlive the native view: closing a window sleeps the
    /// element and destroys the GameObject, and the next show rebuilds the
    /// view from the same nodes. View teardown calls <see cref="Dispose"/>,
    /// so a renderer subscribing <see cref="Changed"/> on rebuild must
    /// re-attach the projection to the model — otherwise intents still reach
    /// the model through <see cref="Dispatch"/> while snapshot refreshes never
    /// fire (frozen tab pages, stale toggles).
    /// </summary>
    internal event Action? Changed
    {
        add
        {
            Attach();
            _changed += value;
        }
        remove => _changed -= value;
    }

    private ElementStateProjection(
        Func<ElementSnapshot> read,
        Action<ElementIntent> dispatch,
        Action<Action> subscribe,
        Action<Action> unsubscribe)
    {
        _read = read;
        _dispatch = dispatch;
        _subscribe = subscribe;
        _unsubscribe = unsubscribe;
        _onChanged = OnChanged;
    }

    internal T Snapshot<T>() where T : ElementSnapshot => (T)_read();
    internal void Dispatch(ElementIntent intent) => _dispatch(intent);

    public void Dispose()
    {
        if (!_attached)
            return;
        _attached = false;
        _unsubscribe(_onChanged);
        _changed = null;
    }

    private void Attach()
    {
        if (_attached)
            return;
        _attached = true;
        _subscribe(_onChanged);
    }

    private void OnChanged() => _changed?.Invoke();

    internal static ElementStateProjection Choices<T>(
        TaiwuSelection<T> selection,
        IReadOnlyList<TaiwuChoiceOption<T>> options) =>
        new(
            () => new ChoiceSnapshot(options.Select(option => new ChoiceItemSnapshot(
                option.Label,
                selection.IsSelected(option.Value),
                selection.Interactable && option.Interactable,
                option.Tone,
                option.Highlighted)).ToArray()),
            intent =>
            {
                int index = intent switch
                {
                    ToggleChoiceIntent toggle => toggle.Index,
                    SelectChoiceIntent select => select.Index,
                    _ => -1,
                };
                if (index < 0 || index >= options.Count ||
                    !selection.Interactable || !options[index].Interactable)
                    return;
                if (intent is SelectChoiceIntent)
                    selection.Select(options[index].Value);
                else
                    selection.Toggle(options[index].Value);
            },
            handler => selection.AdapterChanged += handler,
            handler => selection.AdapterChanged -= handler);

    internal static ElementStateProjection Tabs<T>(TaiwuTabsModel<T> model) =>
        new(
            () => new TabsSnapshot(model.Items.Select(item => new TabItemSnapshot(
                item.Label ?? string.Empty,
                item.Icon,
                model.Selection.IsSelected(item.Value),
                model.Selection.Interactable && item.Interactable,
                item.Closable)).ToArray()),
            intent =>
            {
                switch (intent)
                {
                    case SelectTabIntent select when select.Index >= 0 && select.Index < model.Items.Count:
                        TaiwuTabItem<T> item = model.Items[select.Index];
                        if (model.Selection.Interactable && item.Interactable)
                            model.Selection.Select(item.Value);
                        break;
                    case CloseTabIntent close:
                        model.RequestClose(close.Index);
                        break;
                    case ClearTabsIntent:
                        model.RequestClear();
                        break;
                }
            },
            handler => model.AdapterChanged += handler,
            handler => model.AdapterChanged -= handler);

    internal static ElementStateProjection TabView<T>(
        TaiwuSelection<T> selection,
        IReadOnlyList<UiTabPage<T>> pages) =>
        new(
            () => new ChoiceSnapshot(pages.Select(page => new ChoiceItemSnapshot(
                page.Label,
                selection.IsSelected(page.Value),
                selection.Interactable && page.Interactable)).ToArray()),
            intent =>
            {
                if (intent is not SelectChoiceIntent select ||
                    select.Index < 0 || select.Index >= pages.Count)
                    return;
                UiTabPage<T> page = pages[select.Index];
                if (selection.Interactable && page.Interactable)
                    selection.Select(page.Value);
            },
            handler => selection.AdapterChanged += handler,
            handler => selection.AdapterChanged -= handler);

    internal static ElementStateProjection Navigation<T>(TaiwuNavigationModel<T> model) =>
        new(
            () => new NavigationSnapshot(model.Groups.Select(group => new NavigationGroupSnapshot(
                group.Id,
                group.Label,
                model.ExpandedGroups.IsSelected(group.Id),
                model.ExpandedGroups.Interactable,
                group.Items.Select(item => new NavigationItemSnapshot(
                    item.Label ?? string.Empty,
                    model.Selection.IsSelected(item.Value),
                    model.Selection.Interactable && item.Interactable)).ToArray())).ToArray()),
            intent =>
            {
                switch (intent)
                {
                    case ToggleNavigationGroupIntent toggle
                        when toggle.Group >= 0 && toggle.Group < model.Groups.Count &&
                             model.ExpandedGroups.Interactable:
                        model.ExpandedGroups.Toggle(model.Groups[toggle.Group].Id);
                        break;
                    case SelectNavigationItemIntent select
                        when select.Group >= 0 && select.Group < model.Groups.Count &&
                             select.Item >= 0 && select.Item < model.Groups[select.Group].Items.Count:
                        TaiwuNavigationItem<T> item = model.Groups[select.Group].Items[select.Item];
                        if (model.Selection.Interactable && item.Interactable)
                            model.Selection.Select(item.Value);
                        break;
                }
            },
            handler => model.AdapterChanged += handler,
            handler => model.AdapterChanged -= handler);

    internal static ElementStateProjection Table<TRow, TKey>(
        TaiwuTableModel<TRow, TKey> model,
        IReadOnlyList<TaiwuTableColumn<TRow>> columns) where TKey : notnull =>
        new(
            () => BuildTableSnapshot(model, columns),
            intent => DispatchTableIntent(model, columns, intent),
            handler => model.AdapterChanged += handler,
            handler => model.AdapterChanged -= handler);

    private static TableSnapshot BuildTableSnapshot<TRow, TKey>(
        TaiwuTableModel<TRow, TKey> model,
        IReadOnlyList<TaiwuTableColumn<TRow>> columns) where TKey : notnull
    {
        IReadOnlyList<TRow> rows = SortRows(model, columns);
        TableRowSnapshot[] rowSnapshots = rows.Select(row => new TableRowSnapshot(
            columns.Select(column => column.Cell(row)).ToArray(),
            model.Selection.IsSelected(model.RowKey(row)),
            model.RowDisabled?.Invoke(row) == true,
            model.RowActions?.Invoke(row) ?? Array.Empty<TaiwuMenuAction>(),
            model.InlineRowAction?.Invoke(row))).ToArray();
        return new TableSnapshot(
            columns.Select(column => new TableColumnSnapshot(
                column.Id, column.Header, column.Width, column.Sortable)).ToArray(),
            rowSnapshots,
            model.Sort.Value,
            rowSnapshots.Any(row => row.InlineAction != null));
    }

    private static void DispatchTableIntent<TRow, TKey>(
        TaiwuTableModel<TRow, TKey> model,
        IReadOnlyList<TaiwuTableColumn<TRow>> columns,
        ElementIntent intent) where TKey : notnull
    {
        IReadOnlyList<TRow> view = SortRows(model, columns);
        if (intent is ClickTableRowIntent click && click.Row >= 0 && click.Row < view.Count)
        {
            TRow row = view[click.Row];
            if (model.RowDisabled?.Invoke(row) != true)
                model.Selection.Toggle(model.RowKey(row));
            return;
        }
        if (intent is not CycleTableSortIntent cycle ||
            cycle.Column < 0 || cycle.Column >= columns.Count ||
            !model.Sort.Interactable || !columns[cycle.Column].Sortable)
            return;

        string columnId = columns[cycle.Column].Id;
        TaiwuSortState current = model.Sort.Value;
        TaiwuSortDirection next = !string.Equals(current.ColumnId, columnId, StringComparison.Ordinal)
            ? TaiwuSortDirection.Ascending
            : current.Direction switch
            {
                TaiwuSortDirection.Ascending => TaiwuSortDirection.Descending,
                TaiwuSortDirection.Descending => TaiwuSortDirection.None,
                _ => TaiwuSortDirection.Ascending,
            };
        model.Sort.SetValue(next == TaiwuSortDirection.None
            ? TaiwuSortState.None
            : new TaiwuSortState(columnId, next));
    }

    private static IReadOnlyList<TRow> SortRows<TRow, TKey>(
        TaiwuTableModel<TRow, TKey> model,
        IReadOnlyList<TaiwuTableColumn<TRow>> columns) where TKey : notnull
    {
        TaiwuSortState state = model.Sort.Value;
        TaiwuTableColumn<TRow>? column = columns.FirstOrDefault(candidate =>
            candidate.Sortable && string.Equals(candidate.Id, state.ColumnId, StringComparison.Ordinal));
        if (column == null || state.Direction == TaiwuSortDirection.None)
            return model.Items.ToArray();

        Func<TRow, IComparable?> value = column.SortValue
            ?? (row => column.Cell(row).Text ?? string.Empty);
        IComparer<IComparable?> comparer = state.Direction == TaiwuSortDirection.Descending
            ? DescendingComparableComparer.Instance
            : ComparableComparer.Instance;
        return model.Items
            .Select((row, index) => (row, index))
            .OrderBy(item => value(item.row), comparer)
            .ThenBy(item => item.index)
            .Select(item => item.row)
            .ToArray();
    }

    private sealed class ComparableComparer : IComparer<IComparable?>
    {
        internal static ComparableComparer Instance { get; } = new();
        public int Compare(IComparable? left, IComparable? right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return -1;
            if (right == null) return 1;
            try { return left.CompareTo(right); }
            catch (ArgumentException)
            {
                return StringComparer.CurrentCulture.Compare(left.ToString(), right.ToString());
            }
        }
    }

    private sealed class DescendingComparableComparer : IComparer<IComparable?>
    {
        internal static DescendingComparableComparer Instance { get; } = new();
        public int Compare(IComparable? left, IComparable? right) =>
            ComparableComparer.Instance.Compare(right, left);
    }
}

internal enum TabsNodeStyle { Icon, Closable, MapIcon }

internal sealed class TabsNode(
    ElementStateProjection projection,
    TabsNodeStyle style,
    float height,
    float minimumItemWidth,
    bool showClearButton = false,
    float spacing = 0f) : UiNode
{
    internal ElementStateProjection Projection { get; } = projection;
    internal TabsNodeStyle Style { get; } = style;
    internal float Height { get; } = height;
    internal float MinimumItemWidth { get; } = minimumItemWidth;
    internal bool ShowClearButton { get; } = showClearButton;
    internal float Spacing { get; } = spacing;
}

internal sealed class TabPageNode(List<UiNode> children)
{
    internal List<UiNode> Children { get; } = children;
}

internal sealed class TabViewNode(
    ElementStateProjection projection,
    IReadOnlyList<TabPageNode> pages,
    TaiwuTabViewOptions options) : UiNode
{
    internal ElementStateProjection Projection { get; } = projection;
    internal IReadOnlyList<TabPageNode> Pages { get; } = pages;
    internal TaiwuTabViewOptions Options { get; } = options;
}

internal sealed class NavigationNode(
    ElementStateProjection projection,
    TaiwuNavigationOptions options) : UiNode
{
    internal ElementStateProjection Projection { get; } = projection;
    internal TaiwuNavigationOptions Options { get; } = options;
}

internal sealed class SearchInputNode(
    TaiwuValue<string> value, string placeholder, float width) : UiNode
{
    internal TaiwuValue<string> Value { get; } = value;
    internal string Placeholder { get; } = placeholder;
    internal float Width { get; } = width;
}

internal sealed class ToggleNode(string label, TaiwuValue<bool> value) : UiNode
{
    internal string Label { get; } = label;
    internal TaiwuValue<bool> Value { get; } = value;
}

internal enum NativeActionIcon { Reset, Refresh }

internal sealed class NativeActionIconNode(
    NativeActionIcon icon, Action onClick, float size) : UiNode
{
    internal NativeActionIcon Icon { get; } = icon;
    internal Action OnClick { get; } = onClick;
    internal float Size { get; } = size;
}

internal sealed class SliderNode(
    string label, TaiwuValue<float> value, float minimum, float maximum, float step) : UiNode
{
    internal string Label { get; } = label;
    internal TaiwuValue<float> Value { get; } = value;
    internal float Minimum { get; } = minimum;
    internal float Maximum { get; } = maximum;
    internal float Step { get; } = step;
}

internal sealed class RangeSliderNode(
    string label, TaiwuValue<TaiwuRange> value, float minimum, float maximum, float step) : UiNode
{
    internal string Label { get; } = label;
    internal TaiwuValue<TaiwuRange> Value { get; } = value;
    internal float Minimum { get; } = minimum;
    internal float Maximum { get; } = maximum;
    internal float Step { get; } = step;
}

internal enum ChoiceGroupAppearance
{
    Filter,
    SheetTab,
    SecondaryTab,
}

internal sealed class ChoiceGroupNode(
    string label,
    ElementStateProjection projection,
    bool compact,
    bool selectOnly = false,
    ChoiceGroupAppearance appearance = ChoiceGroupAppearance.Filter,
    TaiwuChoiceAction? leadingAction = null) : UiNode
{
    internal string Label { get; } = label;
    internal ElementStateProjection Projection { get; } = projection;
    internal bool Compact { get; } = compact;
    internal bool SelectOnly { get; } = selectOnly;
    internal ChoiceGroupAppearance Appearance { get; } = appearance;
    internal TaiwuChoiceAction? LeadingAction { get; } = leadingAction;
}

internal sealed class PopupSelectNode(
    string label,
    ElementStateProjection projection,
    TaiwuPopupSelectOptions options) : UiNode
{
    internal string Label { get; } = label;
    internal ElementStateProjection Projection { get; } = projection;
    internal TaiwuPopupSelectOptions Options { get; } = options;
}

internal sealed class PopupCardNode(
    string label,
    TaiwuPopupCardModel model,
    TaiwuPopupCardOptions options) : UiNode
{
    internal string Label { get; } = label;
    internal TaiwuPopupCardModel Model { get; } = model;
    internal TaiwuPopupCardOptions Options { get; } = options;
}

internal sealed class BottomTabsNode(
    ElementStateProjection projection, TaiwuBottomTabsOptions options) : UiNode
{
    internal ElementStateProjection Projection { get; } = projection;
    internal TaiwuBottomTabsOptions Options { get; } = options;
}

internal sealed class TableNode(
    ElementStateProjection projection, TaiwuTableOptions options) : UiNode
{
    internal ElementStateProjection Projection { get; } = projection;
    internal TaiwuTableOptions Options { get; } = options;
}
