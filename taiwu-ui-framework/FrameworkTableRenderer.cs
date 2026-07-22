using FrameWork.UISystem.UIElements;
using Game.Components.Common;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TaiwuUi;

internal static class FrameworkTableRenderer
{
    internal static void Render(
        Transform parent,
        RectTransform overlayRoot,
        TableNode node,
        TaiwuTheme theme)
    {
        RectTransform root = UiFactory.Rect("Table", parent);
        UiFactory.Layout(root, -1f, node.Options.Height, flexibleWidth: 1f);
        VirtualTableView view = root.gameObject.AddComponent<VirtualTableView>();
        view.Initialize(node.Projection, node.Options, theme, overlayRoot);
    }
}

internal sealed class VirtualTableView : MonoBehaviour
{
    internal const float ActionColumnWidth = 150f;

    private ElementStateProjection? _projection;
    private TaiwuTableOptions? _options;
    private TaiwuTheme? _theme;
    private RectTransform? _overlayRoot;
    private RectTransform? _viewport;
    private RectTransform? _content;
    private ScrollRect? _scroll;
    private TextMeshProUGUI? _emptyText;
    private RectTransform? _actionHeaderCell;
    private readonly List<TableRowView> _rows = new();
    private readonly List<TextMeshProUGUI> _headers = new();
    private readonly List<CImage> _headerArrows = new();
    private float _lastViewportHeight = -1f;
    private int _lastStart = -1;
    private AnchoredMenuView? _menu;

    internal void Initialize(
        ElementStateProjection projection,
        TaiwuTableOptions options,
        TaiwuTheme theme,
        RectTransform overlayRoot)
    {
        _projection = projection;
        _options = options;
        _theme = theme;
        _overlayRoot = overlayRoot;

        RectTransform root = (RectTransform)transform;
        float headerHeight = options.ShowHeader ? 36f : 0f;
        if (options.ShowHeader)
            BuildHeader(root, headerHeight);

        _viewport = UiFactory.Rect("Viewport", root);
        _viewport.anchorMin = Vector2.zero;
        _viewport.anchorMax = Vector2.one;
        _viewport.offsetMin = new Vector2(0f, 0f);
        _viewport.offsetMax = new Vector2(0f, -headerHeight);
        CImage viewportImage = _viewport.gameObject.AddComponent<CImage>();
        viewportImage.color = Color.clear;
        _viewport.gameObject.AddComponent<RectMask2D>();

        _content = UiFactory.Rect("Content", _viewport);
        _content.anchorMin = new Vector2(0f, 1f);
        _content.anchorMax = new Vector2(1f, 1f);
        _content.pivot = new Vector2(0.5f, 1f);
        _content.anchoredPosition = Vector2.zero;

        _scroll = _viewport.gameObject.AddComponent<ScrollRect>();
        _scroll.viewport = _viewport;
        _scroll.content = _content;
        _scroll.horizontal = false;
        _scroll.vertical = true;
        _scroll.movementType = ScrollRect.MovementType.Clamped;
        _scroll.inertia = true;
        _scroll.scrollSensitivity = options.RowHeight * 0.65f;
        _scroll.onValueChanged.AddListener(_ =>
        {
            CloseMenu();
            RefreshVisible(force: false);
        });

        RectTransform scrollbarRoot = UiFactory.Rect("VerticalScrollbar", root);
        scrollbarRoot.anchorMin = new Vector2(1f, 0f);
        scrollbarRoot.anchorMax = new Vector2(1f, 1f);
        scrollbarRoot.pivot = new Vector2(1f, 0.5f);
        scrollbarRoot.sizeDelta = new Vector2(20f, -headerHeight);
        scrollbarRoot.anchoredPosition = new Vector2(12f, -headerHeight * 0.5f);
        CImage track = scrollbarRoot.gameObject.AddComponent<CImage>();
        RectTransform handleRoot = UiFactory.Rect("Handle", scrollbarRoot);
        UiFactory.Stretch(handleRoot, Vector2.zero, Vector2.zero);
        CImage handle = handleRoot.gameObject.AddComponent<CImage>();
        Theme.ApplyVerticalScrollbar(track, handle);
        // See FrameworkView.BuildScroll: CScrollbar assumes a native prefab
        // hierarchy during Awake and cannot safely be assembled at runtime.
        Scrollbar scrollbar = scrollbarRoot.gameObject.AddComponent<Scrollbar>();
        scrollbar.targetGraphic = handle;
        scrollbar.handleRect = handleRoot;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        _scroll.verticalScrollbar = scrollbar;
        _scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        _emptyText = UiFactory.Text(
            "Empty", _viewport, options.EmptyText, 20f, theme,
            TaiwuTextStyle.Muted, TextAlignmentOptions.Center);
        UiFactory.Stretch(_emptyText.rectTransform, new Vector2(24f, 24f), new Vector2(-24f, -24f));

        projection.Changed += OnSourceChanged;
        RefreshAll();
    }

    private void BuildHeader(RectTransform root, float height)
    {
        RectTransform header = UiFactory.Rect("Header", root);
        header.anchorMin = new Vector2(0f, 1f);
        header.anchorMax = new Vector2(1f, 1f);
        header.pivot = new Vector2(0.5f, 1f);
        header.sizeDelta = new Vector2(0f, height);
        header.anchoredPosition = Vector2.zero;
        var layout = header.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 0f;
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        TableSnapshot snapshot = Snapshot;
        for (int column = 0; column < snapshot.Columns.Count; column++)
        {
            TableColumnSnapshot columnState = snapshot.Columns[column];
            int captured = column;
            RectTransform cell = UiFactory.Rect("Header_" + column, header);
            UiFactory.Layout(cell, columnState.Width, height,
                flexibleWidth: column == snapshot.Columns.Count - 1 ? 1f : 0f);
            CImage image = cell.gameObject.AddComponent<CImage>();
            CButton button = cell.gameObject.AddComponent<CButton>();
            button.targetGraphic = image;
            button.interactable = columnState.Sortable;
            button.onClick.AddListener(() => Projection.Dispatch(new CycleTableSortIntent(captured)));
            Theme.ApplyTableHeader(image, button);
            TextMeshProUGUI label = UiFactory.Text(
                "Label", cell, columnState.Header, 24f, Theme,
                TaiwuTextStyle.Muted, TextAlignmentOptions.Center);
            Theme.ApplyTableHeaderText(label);
            UiFactory.Stretch(label.rectTransform, new Vector2(22f, 0f), new Vector2(-22f, 0f));
            RectTransform arrowRoot = UiFactory.Rect("Arrow", cell);
            arrowRoot.anchorMin = new Vector2(0f, 0.5f);
            arrowRoot.anchorMax = new Vector2(0f, 0.5f);
            arrowRoot.pivot = new Vector2(0.5f, 0.5f);
            arrowRoot.sizeDelta = new Vector2(20f, 12f);
            arrowRoot.anchoredPosition = new Vector2(18f, 0f);
            CImage arrow = arrowRoot.gameObject.AddComponent<CImage>();
            Theme.ApplyTableSortArrow(arrow);
            _headerArrows.Add(arrow);
            if (column < snapshot.Columns.Count - 1)
            {
                RectTransform lineRoot = UiFactory.Rect("RightLine", cell);
                lineRoot.anchorMin = new Vector2(1f, 0.5f);
                lineRoot.anchorMax = new Vector2(1f, 0.5f);
                lineRoot.pivot = new Vector2(1f, 0.5f);
                lineRoot.sizeDelta = new Vector2(2f, height);
                CImage line = lineRoot.gameObject.AddComponent<CImage>();
                Theme.ApplyTableVerticalLine(line);
            }
            _headers.Add(label);
        }

        // Trailing inline-action column header; visibility follows each snapshot.
        RectTransform actionCell = UiFactory.Rect("Header_Action", header);
        UiFactory.Layout(actionCell, ActionColumnWidth, height, flexibleWidth: 0f);
        TextMeshProUGUI actionLabel = UiFactory.Text(
            "Label", actionCell, "操作", 24f, Theme,
            TaiwuTextStyle.Muted, TextAlignmentOptions.Center);
        Theme.ApplyTableHeaderText(actionLabel);
        UiFactory.Stretch(actionLabel.rectTransform, new Vector2(22f, 0f), new Vector2(-22f, 0f));
        _actionHeaderCell = actionCell;

        RefreshHeaders();
    }

    private void LateUpdate()
    {
        if (_viewport == null)
            return;
        float height = _viewport.rect.height;
        if (!Mathf.Approximately(height, _lastViewportHeight))
        {
            _lastViewportHeight = height;
            RefreshVisible(force: true);
        }
    }

    private void OnSourceChanged() => RefreshAll();

    private void RefreshAll()
    {
        if (_content == null || _emptyText == null)
            return;
        TableSnapshot snapshot = Snapshot;
        float contentHeight = snapshot.Rows.Count * Options.RowHeight;
        _content.sizeDelta = new Vector2(0f, contentHeight);
        _emptyText.gameObject.SetActive(snapshot.Rows.Count == 0);
        _lastStart = -1;
        RefreshHeaders();
        RefreshVisible(force: true);
    }

    private void RefreshHeaders()
    {
        TableSnapshot snapshot = Snapshot;
        TaiwuSortState state = snapshot.Sort;
        for (int column = 0; column < _headers.Count; column++)
        {
            TableColumnSnapshot columnState = snapshot.Columns[column];
            bool active = columnState.Sortable &&
                string.Equals(state.ColumnId, columnState.Id, StringComparison.Ordinal) &&
                state.Direction != TaiwuSortDirection.None;
            _headers[column].text = columnState.Header;
            _headerArrows[column].gameObject.SetActive(active);
            _headerArrows[column].rectTransform.localEulerAngles = new Vector3(
                0f, 0f, state.Direction == TaiwuSortDirection.Descending ? 180f : 0f);
        }
        if (_actionHeaderCell != null)
            _actionHeaderCell.gameObject.SetActive(snapshot.HasInlineActions);
    }

    private void RefreshVisible(bool force)
    {
        TableSnapshot snapshot = Snapshot;
        if (_viewport == null || _content == null || snapshot.Rows.Count == 0)
        {
            foreach (TableRowView row in _rows)
                row.gameObject.SetActive(false);
            return;
        }

        float offset = Math.Max(0f, _content.anchoredPosition.y);
        int start = Math.Clamp((int)MathF.Floor(offset / Options.RowHeight), 0, snapshot.Rows.Count - 1);
        int visible = Math.Max(1, (int)MathF.Ceiling(_viewport.rect.height / Options.RowHeight) + 2);
        EnsureRowPool(visible);
        if (!force && start == _lastStart)
            return;
        _lastStart = start;

        for (int poolIndex = 0; poolIndex < _rows.Count; poolIndex++)
        {
            int rowIndex = start + poolIndex;
            TableRowView row = _rows[poolIndex];
            if (rowIndex >= snapshot.Rows.Count)
            {
                row.gameObject.SetActive(false);
                continue;
            }
            row.gameObject.SetActive(true);
            row.Rect.anchoredPosition = new Vector2(0f, -rowIndex * Options.RowHeight);
            row.Render(
                rowIndex,
                snapshot.Rows[rowIndex],
                Options.ShowAlternatingRows,
                Theme,
                OnRowClicked,
                snapshot.HasInlineActions);
        }
    }

    private void EnsureRowPool(int count)
    {
        if (_content == null)
            return;
        while (_rows.Count < count)
        {
            TableRowView row = TableRowView.Create(
                "Row_" + _rows.Count,
                _content,
                Options.RowHeight,
                Snapshot,
                Theme);
            _rows.Add(row);
        }
    }

    private void OnRowClicked(int rowIndex, RectTransform anchor)
    {
        Projection.Dispatch(new ClickTableRowIntent(rowIndex));
        IReadOnlyList<TaiwuMenuAction> actions = Snapshot.Rows[rowIndex].Actions;
        if (actions.Count == 0 || _overlayRoot == null)
            return;
        CloseMenu();
        _menu = AnchoredMenuView.Show(_overlayRoot, anchor, actions, Theme, () => _menu = null);
    }

    private void CloseMenu()
    {
        if (_menu != null)
            _menu.Close();
        _menu = null;
    }

    private void OnDestroy()
    {
        CloseMenu();
        if (_projection != null)
        {
            _projection.Changed -= OnSourceChanged;
            _projection.Dispose();
        }
    }

    private ElementStateProjection Projection => _projection ??
        throw new InvalidOperationException("Table projection is not initialized.");
    private TableSnapshot Snapshot => Projection.Snapshot<TableSnapshot>();
    private TaiwuTableOptions Options => _options ?? throw new InvalidOperationException("Table options are not initialized.");
    private TaiwuTheme Theme => _theme ?? throw new InvalidOperationException("Table theme is not initialized.");
}

internal sealed class TableRowView : MonoBehaviour
{
    private readonly List<TableCellView> _cells = new();
    private CImage? _background;
    private CImage? _selected;
    private CImage? _hover;
    private CButton? _button;
    private RectTransform? _actionCell;
    private CButton? _actionButton;
    private TextMeshProUGUI? _actionText;
    private int _rowIndex;
    private bool _disabled;

    internal RectTransform Rect => (RectTransform)transform;

    internal static TableRowView Create(
        string name,
        RectTransform parent,
        float height,
        TableSnapshot snapshot,
        TaiwuTheme theme)
    {
        RectTransform root = UiFactory.Rect(name, parent);
        root.anchorMin = new Vector2(0f, 1f);
        root.anchorMax = new Vector2(1f, 1f);
        root.pivot = new Vector2(0.5f, 1f);
        root.sizeDelta = new Vector2(0f, height);
        TableRowView view = root.gameObject.AddComponent<TableRowView>();
        view.Build(snapshot, theme);
        return view;
    }

    private void Build(TableSnapshot snapshot, TaiwuTheme theme)
    {
        _background = gameObject.AddComponent<CImage>();
        theme.ApplyTableRow(_background);
        _button = gameObject.AddComponent<CButton>();
        _button.targetGraphic = _background;
        _button.transition = Selectable.Transition.None;
        var layout = gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 0f;
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        for (int column = 0; column < snapshot.Columns.Count; column++)
        {
            TableColumnSnapshot columnState = snapshot.Columns[column];
            RectTransform cellRoot = UiFactory.Rect("Cell_" + column, transform);
            UiFactory.Layout(cellRoot, columnState.Width, -1f,
                flexibleWidth: column == snapshot.Columns.Count - 1 ? 1f : 0f);
            _cells.Add(TableCellView.Create(cellRoot, theme, column < snapshot.Columns.Count - 1));
        }

        // Trailing inline-action cell; visibility follows each render pass.
        RectTransform actionCell = UiFactory.Rect("Cell_Action", transform);
        UiFactory.Layout(actionCell, VirtualTableView.ActionColumnWidth, -1f, flexibleWidth: 0f);
        RectTransform actionButtonRoot = UiFactory.Rect("ActionButton", actionCell);
        actionButtonRoot.anchorMin = actionButtonRoot.anchorMax = new Vector2(0.5f, 0.5f);
        actionButtonRoot.pivot = new Vector2(0.5f, 0.5f);
        actionButtonRoot.sizeDelta = new Vector2(VirtualTableView.ActionColumnWidth - 28f, 52f);
        _actionButton = UiFactory.Button(actionButtonRoot, string.Empty, theme, TaiwuButtonStyle.Secondary);
        _actionText = actionButtonRoot.GetComponentInChildren<TextMeshProUGUI>();
        _actionCell = actionCell;

        RectTransform bottomLine = UiFactory.Rect("BottomLine", transform);
        bottomLine.anchorMin = new Vector2(0f, 0f);
        bottomLine.anchorMax = new Vector2(1f, 0f);
        bottomLine.pivot = new Vector2(0.5f, 0f);
        bottomLine.sizeDelta = new Vector2(0f, 2f);
        CImage bottomLineImage = bottomLine.gameObject.AddComponent<CImage>();
        theme.ApplyTableHorizontalLine(bottomLineImage);
        IgnoreLayout(bottomLine);

        RectTransform selectedRoot = UiFactory.Rect("Selected", transform);
        UiFactory.Stretch(selectedRoot, Vector2.zero, Vector2.zero);
        _selected = selectedRoot.gameObject.AddComponent<CImage>();
        theme.ApplyTableSelected(_selected);
        selectedRoot.gameObject.SetActive(false);
        IgnoreLayout(selectedRoot);

        RectTransform hoverRoot = UiFactory.Rect("Hover", transform);
        UiFactory.Stretch(hoverRoot, Vector2.zero, Vector2.zero);
        _hover = hoverRoot.gameObject.AddComponent<CImage>();
        theme.ApplyTableHover(_hover);
        hoverRoot.gameObject.SetActive(false);
        IgnoreLayout(hoverRoot);

        TaiwuMenuHover hover = gameObject.AddComponent<TaiwuMenuHover>();
        hover.Enter = () =>
        {
            if (!_disabled && _hover != null)
                _hover.gameObject.SetActive(true);
        };
        hover.Exit = () =>
        {
            if (_hover != null)
                _hover.gameObject.SetActive(false);
        };
    }

    internal void Render(
        int rowIndex,
        TableRowSnapshot row,
        bool alternating,
        TaiwuTheme theme,
        Action<int, RectTransform> onClick,
        bool showActionColumn)
    {
        _rowIndex = rowIndex;
        bool selected = row.Selected;
        bool disabled = row.Disabled;
        _disabled = disabled;
        if (_background != null)
            _background.color = disabled
                ? new Color(0.55f, 0.55f, 0.55f, 1f)
                : alternating && rowIndex % 2 == 1
                    ? new Color(0.94f, 0.96f, 0.96f, 1f)
                    : Color.white;
        if (_selected != null)
            _selected.gameObject.SetActive(selected);
        if (_hover != null)
            _hover.gameObject.SetActive(false);
        if (_button != null)
        {
            _button.interactable = !disabled;
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(() => onClick(_rowIndex, Rect));
        }
        if (_actionCell != null && _actionButton != null)
        {
            TaiwuMenuAction? action = showActionColumn && !disabled ? row.InlineAction : null;
            _actionCell.gameObject.SetActive(showActionColumn);
            _actionButton.gameObject.SetActive(action != null);
            if (action != null)
            {
                if (_actionText != null)
                    _actionText.text = action.Label;
                _actionButton.interactable = action.Interactable;
                _actionButton.onClick.RemoveAllListeners();
                _actionButton.onClick.AddListener(() => action.OnClick());
            }
        }
        for (int column = 0; column < _cells.Count; column++)
            _cells[column].Render(row.Cells[column], disabled, theme);
    }

    private static void IgnoreLayout(RectTransform rect)
    {
        LayoutElement layout = rect.gameObject.AddComponent<LayoutElement>();
        layout.ignoreLayout = true;
    }
}

internal sealed class TableCellView
{
    private readonly CImage _icon;
    private readonly LayoutElement _iconLayout;
    private readonly TextMeshProUGUI _text;

    private TableCellView(CImage icon, LayoutElement iconLayout, TextMeshProUGUI text)
    {
        _icon = icon;
        _iconLayout = iconLayout;
        _text = text;
    }

    internal static TableCellView Create(RectTransform root, TaiwuTheme theme, bool showRightLine)
    {
        var layout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 6f;
        layout.padding = new RectOffset(8, 8, 0, 0);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        RectTransform iconRoot = UiFactory.Rect("Icon", root);
        CImage icon = iconRoot.gameObject.AddComponent<CImage>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        LayoutElement iconLayout = UiFactory.Layout(iconRoot, 26f, 26f, flexibleWidth: 0f);

        TextMeshProUGUI text = UiFactory.Text(
            "Text", root, string.Empty, 25f, theme,
            TaiwuTextStyle.Body, TextAlignmentOptions.Center);
        UiFactory.Layout(text.rectTransform, 20f, 50f, flexibleWidth: 1f);

        if (showRightLine)
        {
            RectTransform lineRoot = UiFactory.Rect("RightLine", root);
            lineRoot.anchorMin = new Vector2(1f, 0f);
            lineRoot.anchorMax = new Vector2(1f, 1f);
            lineRoot.pivot = new Vector2(1f, 0.5f);
            lineRoot.sizeDelta = new Vector2(2f, -32f);
            CImage line = lineRoot.gameObject.AddComponent<CImage>();
            theme.ApplyTableVerticalLine(line);
            LayoutElement lineLayout = lineRoot.gameObject.AddComponent<LayoutElement>();
            lineLayout.ignoreLayout = true;
        }
        return new TableCellView(icon, iconLayout, text);
    }

    internal void Render(TaiwuTableCell cell, bool disabled, TaiwuTheme theme)
    {
        _text.text = cell.Text ?? string.Empty;
        _text.color = disabled ? theme.MutedText : theme.TableTextColor(cell.Style);
        if (cell.Icon == null)
        {
            _icon.sprite = null;
            _icon.gameObject.SetActive(false);
            _iconLayout.ignoreLayout = true;
        }
        else
        {
            theme.ApplyNativeAsset(_icon, cell.Icon);
            _icon.gameObject.SetActive(_icon.sprite != null);
            _iconLayout.ignoreLayout = _icon.sprite == null;
        }
    }
}

internal sealed class AnchoredMenuView : MonoBehaviour
{
    private Action? _onClosed;
    private bool _closed;

    internal static AnchoredMenuView Show(
        RectTransform overlayRoot,
        RectTransform anchor,
        IReadOnlyList<TaiwuMenuAction> actions,
        TaiwuTheme theme,
        Action onClosed)
    {
        RectTransform overlay = UiFactory.Rect("AnchoredMenuOverlay", overlayRoot);
        UiFactory.Stretch(overlay, Vector2.zero, Vector2.zero);
        overlay.SetAsLastSibling();
        CImage blocker = overlay.gameObject.AddComponent<CImage>();
        blocker.color = Color.clear;
        CButton closeButton = overlay.gameObject.AddComponent<CButton>();
        closeButton.targetGraphic = blocker;

        RectTransform menu = UiFactory.Rect("Menu", overlay);
        float width = Math.Max(176f, actions.Max(action => action.Label.Length * 24f + 54f));
        float height = actions.Count * 46f + 12f;
        menu.sizeDelta = new Vector2(width, height);
        CImage menuImage = menu.gameObject.AddComponent<CImage>();
        theme.ApplyPanel(menuImage);
        var layout = menu.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 2f;
        layout.padding = new RectOffset(8, 8, 6, 6);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        AnchoredMenuView view = overlay.gameObject.AddComponent<AnchoredMenuView>();
        view._onClosed = onClosed;
        closeButton.onClick.AddListener(view.Close);
        foreach (TaiwuMenuAction action in actions)
        {
            RectTransform buttonRoot = UiFactory.Rect("Action", menu);
            UiFactory.Layout(buttonRoot, -1f, 44f, flexibleWidth: 1f);
            CButton button = UiFactory.Button(buttonRoot, action.Label, theme, TaiwuButtonStyle.Secondary);
            button.interactable = action.Interactable;
            TooltipInvoker tooltip = buttonRoot.gameObject.AddComponent<TooltipInvoker>();
            if (string.IsNullOrWhiteSpace(action.Tooltip))
                tooltip.enabled = false;
            else
            {
                tooltip.Type = TipType.SingleDesc;
                tooltip.PresetParam = new[] { action.Tooltip };
                tooltip.enabled = true;
            }
            TaiwuMenuHover hover = buttonRoot.gameObject.AddComponent<TaiwuMenuHover>();
            hover.Enter = action.OnPointerEnter;
            hover.Exit = action.OnPointerExit;
            if (action.Interactable)
            {
                button.onClick.AddListener(() =>
                {
                    view.Close();
                    action.OnClick();
                });
            }
        }

        Vector3[] corners = new Vector3[4];
        anchor.GetWorldCorners(corners);
        Vector3 local = overlayRoot.InverseTransformPoint(corners[2]);
        Rect bounds = overlayRoot.rect;
        float x = local.x + width * 0.5f + 8f;
        float y = local.y - height * 0.5f;
        if (x + width * 0.5f > bounds.xMax)
            x = local.x - width * 0.5f - anchor.rect.width - 8f;
        x = Mathf.Clamp(x, bounds.xMin + width * 0.5f, bounds.xMax - width * 0.5f);
        y = Mathf.Clamp(y, bounds.yMin + height * 0.5f, bounds.yMax - height * 0.5f);
        menu.anchoredPosition = new Vector2(x, y);
        return view;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            Close();
    }

    internal void Close()
    {
        if (_closed)
            return;
        _closed = true;
        _onClosed?.Invoke();
        _onClosed = null;
        Destroy(gameObject);
    }
}

internal sealed class TaiwuMenuHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    internal Action? Enter { get; set; }
    internal Action? Exit { get; set; }
    public void OnPointerEnter(PointerEventData eventData) => Enter?.Invoke();
    public void OnPointerExit(PointerEventData eventData) => Exit?.Invoke();
}
