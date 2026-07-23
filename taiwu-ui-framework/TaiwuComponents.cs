namespace TaiwuUi;

public enum TaiwuSelectionMode
{
    Single,
    Multiple,
}

public enum TaiwuSortDirection
{
    None,
    Ascending,
    Descending,
}

public readonly record struct TaiwuRange(float Lower, float Upper)
{
    public TaiwuRange Normalize(float minimum, float maximum)
    {
        float lower = Math.Clamp(Lower, minimum, maximum);
        float upper = Math.Clamp(Upper, minimum, maximum);
        return lower <= upper ? new TaiwuRange(lower, upper) : new TaiwuRange(upper, lower);
    }
}

public readonly record struct TaiwuSortState(string? ColumnId, TaiwuSortDirection Direction)
{
    public static TaiwuSortState None => new(null, TaiwuSortDirection.None);
}

/// <summary>
/// Framework-owned controlled value. Silent writes still refresh attached UI adapters,
/// but do not notify the consumer through <see cref="ValueChanged"/>.
/// </summary>
public sealed class TaiwuValue<T>
{
    private readonly IEqualityComparer<T> _comparer;
    private T _value;
    private bool _interactable = true;

    public T Value => _value;
    public T DefaultValue { get; }
    public bool Interactable => _interactable;

    public event Action<T>? ValueChanged;
    public event Action<bool>? InteractableChanged;

    internal event Action<T>? AdapterValueChanged;
    internal event Action<bool>? AdapterInteractableChanged;

    public TaiwuValue(T initialValue, IEqualityComparer<T>? comparer = null)
    {
        _value = initialValue;
        DefaultValue = initialValue;
        _comparer = comparer ?? EqualityComparer<T>.Default;
    }

    public void SetValue(T value)
    {
        if (_comparer.Equals(_value, value))
            return;
        _value = value;
        AdapterValueChanged?.Invoke(value);
        ValueChanged?.Invoke(value);
    }

    public void SetValueWithoutNotify(T value)
    {
        if (_comparer.Equals(_value, value))
            return;
        _value = value;
        AdapterValueChanged?.Invoke(value);
    }

    public void Reset() => SetValue(DefaultValue);

    public void SetInteractable(bool interactable)
    {
        if (_interactable == interactable)
            return;
        _interactable = interactable;
        AdapterInteractableChanged?.Invoke(interactable);
        InteractableChanged?.Invoke(interactable);
    }
}

/// <summary>
/// Attaches a dynamic UI renderer to controlled content while tolerating the
/// native UI manager destroying its Unity host before its teardown callback
/// has run.
/// </summary>
internal static class DynamicContentSubscription
{
    internal static Action<UiElement> Subscribe(
        TaiwuValue<UiElement> content,
        Func<bool> hostIsAlive,
        Action<UiElement> render)
    {
        Action<UiElement> refresh = null!;
        refresh = value =>
        {
            if (!hostIsAlive())
            {
                content.AdapterValueChanged -= refresh;
                return;
            }

            render(value);
        };
        content.AdapterValueChanged += refresh;
        return refresh;
    }
}

public sealed class TaiwuSelection<T>
{
    private readonly HashSet<T> _selected;
    private readonly T[] _defaults;
    private bool _interactable = true;

    public TaiwuSelectionMode Mode { get; }
    public IReadOnlyCollection<T> Selected => _selected;
    public bool Interactable => _interactable;

    public event Action<IReadOnlyCollection<T>>? SelectionChanged;
    public event Action<bool>? InteractableChanged;

    internal event Action? AdapterChanged;

    public TaiwuSelection(
        TaiwuSelectionMode mode = TaiwuSelectionMode.Single,
        IEnumerable<T>? initialSelection = null,
        IEqualityComparer<T>? comparer = null)
    {
        Mode = mode;
        _selected = new HashSet<T>(comparer);
        if (initialSelection != null)
        {
            foreach (T value in initialSelection)
            {
                if (Mode == TaiwuSelectionMode.Single && _selected.Count > 0)
                    break;
                _selected.Add(value);
            }
        }
        _defaults = _selected.ToArray();
    }

    public bool IsSelected(T value) => _selected.Contains(value);

    public void Toggle(T value)
    {
        if (!_interactable)
            return;

        if (_selected.Contains(value))
            _selected.Remove(value);
        else
        {
            if (Mode == TaiwuSelectionMode.Single)
                _selected.Clear();
            _selected.Add(value);
        }
        Notify();
    }

    public void Select(T value)
    {
        if (!_interactable)
            return;
        bool changed = false;
        if (Mode == TaiwuSelectionMode.Single && (_selected.Count != 1 || !_selected.Contains(value)))
        {
            _selected.Clear();
            changed = true;
        }
        changed |= _selected.Add(value);
        if (changed)
            Notify();
    }

    public void Deselect(T value)
    {
        if (_interactable && _selected.Remove(value))
            Notify();
    }

    public void Replace(IEnumerable<T> values, bool notify = true)
    {
        _selected.Clear();
        foreach (T value in values)
        {
            _selected.Add(value);
            if (Mode == TaiwuSelectionMode.Single)
                break;
        }
        if (notify)
            Notify();
        else
            AdapterChanged?.Invoke();
    }

    public void Clear()
    {
        if (_selected.Count == 0)
            return;
        _selected.Clear();
        Notify();
    }

    public void Reset() => Replace(_defaults);

    public void SetInteractable(bool interactable)
    {
        if (_interactable == interactable)
            return;
        _interactable = interactable;
        AdapterChanged?.Invoke();
        InteractableChanged?.Invoke(interactable);
    }

    private void Notify()
    {
        AdapterChanged?.Invoke();
        SelectionChanged?.Invoke(_selected);
    }
}

/// <param name="Highlighted">Tints the option background green to mark it as
/// already available (for example, owned or learned), without changing its size.</param>
public sealed record TaiwuChoiceOption<T>(
    T Value,
    string Label,
    bool Interactable = true,
    TaiwuChoiceTone Tone = TaiwuChoiceTone.Neutral,
    bool Highlighted = false);

/// <summary>A choice displayed by a field inside a <see cref="TaiwuPopupCardModel"/>.</summary>
public sealed record TaiwuPopupCardOption(
    string Label,
    bool Selected = false,
    bool Interactable = true);

/// <summary>
/// One cascading field in a popup card. The field factory is evaluated every time the
/// card refreshes, so a selection can safely change the options of its later fields.
/// </summary>
public sealed record TaiwuPopupCardField(
    string Label,
    string Value,
    IReadOnlyList<TaiwuPopupCardOption> Options,
    Action<int> OnSelect,
    bool Interactable = true,
    bool CloseCardAfterSelect = false);

/// <summary>
/// Controlled state for a compact trigger that opens a multi-step selection card.
/// It is intentionally type-erased at the card boundary: each field can represent a
/// different domain type while still participating in one cascading interaction.
/// </summary>
public sealed class TaiwuPopupCardModel
{
    private readonly Func<string> _summary;
    private readonly Func<IReadOnlyList<TaiwuPopupCardField>> _fields;

    internal event Action? AdapterChanged;

    public TaiwuPopupCardModel(
        Func<string> summary,
        Func<IReadOnlyList<TaiwuPopupCardField>> fields)
    {
        _summary = summary ?? throw new ArgumentNullException(nameof(summary));
        _fields = fields ?? throw new ArgumentNullException(nameof(fields));
    }

    /// <summary>Notifies the open card and its trigger that externally owned state changed.</summary>
    public void Refresh() => AdapterChanged?.Invoke();

    internal string Summary => _summary() ?? string.Empty;
    internal IReadOnlyList<TaiwuPopupCardField> Fields => _fields() ?? Array.Empty<TaiwuPopupCardField>();
}

public sealed record TaiwuMenuAction(
    string Label,
    Action OnClick,
    bool Interactable = true,
    string? Tooltip = null,
    Action? OnPointerEnter = null,
    Action? OnPointerExit = null);

public sealed record TaiwuTableCell(
    string Text,
    TaiwuTextStyle Style = TaiwuTextStyle.Body,
    NativeAssetRef? Icon = null,
    string? Tooltip = null);

public sealed class TaiwuTableColumn<TRow>
{
    public string Id { get; }
    public string Header { get; }
    public float Width { get; }
    public bool Sortable { get; }
    public Func<TRow, TaiwuTableCell> Cell { get; }
    public Func<TRow, IComparable?>? SortValue { get; }

    public TaiwuTableColumn(
        string id,
        string header,
        Func<TRow, string> text,
        float width = 160f,
        bool sortable = false,
        Func<TRow, IComparable?>? sortValue = null)
        : this(id, header, row => new TaiwuTableCell(text(row)), width, sortable, sortValue)
    {
    }

    public TaiwuTableColumn(
        string id,
        string header,
        Func<TRow, TaiwuTableCell> cell,
        float width = 160f,
        bool sortable = false,
        Func<TRow, IComparable?>? sortValue = null)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Column ID is required.", nameof(id));
        if (width <= 0f)
            throw new ArgumentOutOfRangeException(nameof(width));
        Id = id.Trim();
        Header = header ?? string.Empty;
        Cell = cell ?? throw new ArgumentNullException(nameof(cell));
        Width = width;
        Sortable = sortable;
        SortValue = sortValue;
    }
}

public sealed class TaiwuTableOptions
{
    public float Height { get; init; } = 420f;
    public float RowHeight { get; init; } = 116f;
    public bool ShowHeader { get; init; } = true;
    public bool ShowAlternatingRows { get; init; } = false;
    public string EmptyText { get; init; } = "没有符合条件的内容";
}

public sealed class TaiwuTableModel<TRow, TKey>
    where TKey : notnull
{
    private IReadOnlyList<TRow> _items = Array.Empty<TRow>();

    public IReadOnlyList<TRow> Items => _items;
    public Func<TRow, TKey> RowKey { get; }
    public TaiwuSelection<TKey> Selection { get; }
    public TaiwuValue<TaiwuSortState> Sort { get; }
    public Func<TRow, IReadOnlyList<TaiwuMenuAction>>? RowActions { get; set; }
    public Func<TRow, bool>? RowDisabled { get; set; }
    // Renders a trailing button column; return null for rows without a button.
    public Func<TRow, TaiwuMenuAction?>? InlineRowAction { get; set; }

    internal event Action? AdapterChanged;

    public TaiwuTableModel(
        Func<TRow, TKey> rowKey,
        TaiwuSelection<TKey>? selection = null,
        TaiwuValue<TaiwuSortState>? sort = null)
    {
        RowKey = rowKey ?? throw new ArgumentNullException(nameof(rowKey));
        Selection = selection ?? new TaiwuSelection<TKey>();
        Sort = sort ?? new TaiwuValue<TaiwuSortState>(TaiwuSortState.None);
        Selection.AdapterChanged += OnChildStateChanged;
        Sort.AdapterValueChanged += OnSortChanged;
        Sort.AdapterInteractableChanged += OnSortInteractableChanged;
    }

    public void SetItems(IEnumerable<TRow> items)
    {
        _items = items?.ToArray() ?? throw new ArgumentNullException(nameof(items));
        AdapterChanged?.Invoke();
    }

    public void Refresh() => AdapterChanged?.Invoke();

    private void OnChildStateChanged() => AdapterChanged?.Invoke();
    private void OnSortChanged(TaiwuSortState _) => AdapterChanged?.Invoke();
    private void OnSortInteractableChanged(bool _) => AdapterChanged?.Invoke();
}
