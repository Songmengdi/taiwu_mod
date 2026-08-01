namespace TaiwuUi;

/// <summary>Stable semantic icon token resolved by the framework's native-resource adapter.</summary>
public readonly record struct TaiwuIcon
{
    public string Key { get; }

    public TaiwuIcon(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Icon key is required.", nameof(key));
        Key = key.Trim();
    }
}

public static class TaiwuIcons
{
    public static TaiwuIcon Home { get; } = new("home");
    public static TaiwuIcon Journey { get; } = new("journey");
    public static TaiwuIcon World { get; } = new("world");
    public static TaiwuIcon Sect { get; } = new("sect");
    public static TaiwuIcon People { get; } = new("people");
    public static TaiwuIcon Interaction { get; } = new("interaction");
    public static TaiwuIcon Study { get; } = new("study");
    public static TaiwuIcon Combat { get; } = new("combat");
    public static TaiwuIcon Industry { get; } = new("industry");
    public static TaiwuIcon Items { get; } = new("items");
    public static TaiwuIcon Travel { get; } = new("travel");
    public static TaiwuIcon Extensions { get; } = new("extensions");
    public static TaiwuIcon MapCharacters { get; } = new("map-characters");
    public static TaiwuIcon MapEnemies { get; } = new("map-enemies");
    public static TaiwuIcon MapCaravans { get; } = new("map-caravans");
    public static TaiwuIcon MapTombs { get; } = new("map-tombs");
}

public sealed record TaiwuTabItem<T>(
    T Value,
    string Label,
    TaiwuIcon? Icon = null,
    bool Interactable = true,
    bool Closable = false);

/// <summary>Controlled single-selection tab collection shared by icon and closable tabs.</summary>
public sealed class TaiwuTabsModel<T>
{
    private IReadOnlyList<TaiwuTabItem<T>> _items = Array.Empty<TaiwuTabItem<T>>();

    public IReadOnlyList<TaiwuTabItem<T>> Items => _items;
    public TaiwuSelection<T> Selection { get; }
    public event Action<T>? CloseRequested;
    public event Action? ClearRequested;

    internal event Action? AdapterChanged;

    public TaiwuTabsModel(
        TaiwuSelection<T>? selection = null,
        IEnumerable<TaiwuTabItem<T>>? items = null)
    {
        Selection = selection ?? new TaiwuSelection<T>();
        if (Selection.Mode != TaiwuSelectionMode.Single)
            throw new ArgumentException("Tabs require single selection.", nameof(selection));
        Selection.AdapterChanged += OnSelectionChanged;
        if (items != null)
            SetItems(items);
    }

    public void SetItems(IEnumerable<TaiwuTabItem<T>> items)
    {
        TaiwuTabItem<T>[] snapshot = items?.ToArray()
            ?? throw new ArgumentNullException(nameof(items));
        if (snapshot.Any(item => item == null))
            throw new ArgumentException("Tab items cannot contain null.", nameof(items));
        if (snapshot.Select(item => item.Value).Distinct().Count() != snapshot.Length)
            throw new ArgumentException("Tab values must be unique.", nameof(items));
        _items = snapshot;
        AdapterChanged?.Invoke();
    }

    public void Refresh() => AdapterChanged?.Invoke();

    internal void RequestClose(int index)
    {
        TaiwuTabItem<T> item = _items[index];
        if (Selection.Interactable && item.Interactable && item.Closable)
            CloseRequested?.Invoke(item.Value);
    }

    internal void RequestClear()
    {
        if (Selection.Interactable && _items.Any(item => item.Interactable && item.Closable))
            ClearRequested?.Invoke();
    }

    private void OnSelectionChanged() => AdapterChanged?.Invoke();
}

public sealed record TaiwuNavigationItem<T>(
    T Value,
    string Label,
    bool Interactable = true);

public sealed class TaiwuNavigationGroup<T>
{
    public string Id { get; }
    public string Label { get; }
    public IReadOnlyList<TaiwuNavigationItem<T>> Items { get; }

    public TaiwuNavigationGroup(
        string id,
        string label,
        IEnumerable<TaiwuNavigationItem<T>> items)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Navigation group ID is required.", nameof(id));
        Id = id.Trim();
        Label = label ?? string.Empty;
        Items = items?.ToArray() ?? throw new ArgumentNullException(nameof(items));
        if (Items.Count == 0)
            throw new ArgumentException("Navigation groups require at least one item.", nameof(items));
        if (Items.Any(item => item == null))
            throw new ArgumentException("Navigation items cannot contain null.", nameof(items));
    }
}

/// <summary>Controlled two-level navigation with caller-owned item and group keys.</summary>
public sealed class TaiwuNavigationModel<T>
{
    private IReadOnlyList<TaiwuNavigationGroup<T>> _groups = Array.Empty<TaiwuNavigationGroup<T>>();

    public IReadOnlyList<TaiwuNavigationGroup<T>> Groups => _groups;
    public TaiwuSelection<T> Selection { get; }
    public TaiwuSelection<string> ExpandedGroups { get; }

    internal event Action? AdapterChanged;

    public TaiwuNavigationModel(
        TaiwuSelection<T>? selection = null,
        TaiwuSelection<string>? expandedGroups = null,
        IEnumerable<TaiwuNavigationGroup<T>>? groups = null)
    {
        Selection = selection ?? new TaiwuSelection<T>();
        ExpandedGroups = expandedGroups
            ?? new TaiwuSelection<string>(TaiwuSelectionMode.Multiple);
        if (Selection.Mode != TaiwuSelectionMode.Single)
            throw new ArgumentException("Navigation requires single item selection.", nameof(selection));
        if (ExpandedGroups.Mode != TaiwuSelectionMode.Multiple)
            throw new ArgumentException("Navigation expansion requires multiple selection.", nameof(expandedGroups));
        Selection.AdapterChanged += OnChildStateChanged;
        ExpandedGroups.AdapterChanged += OnChildStateChanged;
        if (groups != null)
            SetGroups(groups);
    }

    public void SetGroups(IEnumerable<TaiwuNavigationGroup<T>> groups)
    {
        TaiwuNavigationGroup<T>[] snapshot = groups?.ToArray()
            ?? throw new ArgumentNullException(nameof(groups));
        if (snapshot.Any(group => group == null))
            throw new ArgumentException("Navigation groups cannot contain null.", nameof(groups));
        if (snapshot.Select(group => group.Id).Distinct(StringComparer.Ordinal).Count() != snapshot.Length)
            throw new ArgumentException("Navigation group IDs must be unique.", nameof(groups));
        TaiwuNavigationItem<T>[] items = snapshot.SelectMany(group => group.Items).ToArray();
        if (items.Select(item => item.Value).Distinct().Count() != items.Length)
            throw new ArgumentException("Navigation item values must be globally unique.", nameof(groups));
        _groups = snapshot;
        AdapterChanged?.Invoke();
    }

    public void Refresh() => AdapterChanged?.Invoke();
    private void OnChildStateChanged() => AdapterChanged?.Invoke();
}

public sealed record TaiwuIconTabsOptions
{
    public float Height { get; init; } = 104f;
    public float MinimumItemWidth { get; init; } = 118f;
}

/// <summary>Measurements for the compact icon strip used by the world-map character list.</summary>
public sealed record TaiwuMapIconTabsOptions
{
    public float Height { get; init; } = 62f;
    public float ItemWidth { get; init; } = 68f;
    public float Spacing { get; init; } = 10f;
}

public sealed record TaiwuClosableTabsOptions
{
    public float Height { get; init; } = 52f;
    public float MinimumItemWidth { get; init; } = 120f;
    public bool ShowClearButton { get; init; }
}

public sealed record TaiwuNavigationOptions
{
    public float Width { get; init; } = 304f;
    public float Height { get; init; } = 440f;
    public float GroupHeight { get; init; } = 50f;
    public float ItemHeight { get; init; } = 44f;
}

public sealed record TaiwuTabViewOptions
{
    public float Height { get; init; } = 520f;
    public float TabHeight { get; init; } = 48f;
    public float ContentPadding { get; init; } = 8f;
    public float ContentSpacing { get; init; } = 4f;
}

/// <summary>Measurements for the centered lower-popup tab strip used by journal-style dialogs.</summary>
public sealed record TaiwuBottomTabsOptions
{
    public float Height { get; init; } = 129f;
    public float TabHeight { get; init; } = 66f;
    public float ItemWidth { get; init; } = 224f;
    public float Spacing { get; init; } = 24f;
}
