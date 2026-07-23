using FrameWork.UISystem.UIElements;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TaiwuUi;

internal sealed class FrameworkView : UIBase
{
    private TaiwuTheme? _theme;
    private int _buttonSequence;

    internal void Build(WindowDefinition definition, Action close)
    {
        _theme = TaiwuTheme.Resolve();
        _buttonSequence = 0;

        // Cover mask (blocker)
        CImage blocker = GetComponent<CImage>();
        blocker.color = definition.Presentation == TaiwuWindowPresentation.Encyclopedia
            ? Color.clear
            : definition.Cover switch
            {
                TaiwuWindowCover.None => Color.clear,
                TaiwuWindowCover.Dimmed => new Color(0f, 0f, 0f, 0.42f),
                _ => new Color(0f, 0f, 0f, 0.58f),
            };
        blocker.raycastTarget = true;

        // Main panel background (matches ViewFindMapBlock's Bg)
        RectTransform panel = CreateRect(
            "Panel", transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), definition.Size);
        if (definition.Presentation == TaiwuWindowPresentation.Encyclopedia)
        {
            SetRect(panel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            CRawImage background = panel.gameObject.AddComponent<CRawImage>();
            _theme.ApplyEncyclopediaBackground(background);
        }
        else
        {
            CImage panelImage = panel.gameObject.AddComponent<CImage>();
            _theme.ApplyPanel(panelImage);
        }

        // Title bar background (matches ViewFindMapBlock's TitleHolder)
        RectTransform titleBar = CreateRect(
            "TitleBar", panel, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(-TaiwuUiMetrics.WindowChromeInset * 2f, TaiwuUiMetrics.TitleHeight));
        titleBar.anchoredPosition = new Vector2(
            0f,
            -TaiwuUiMetrics.TitleHeight * 0.5f);
        CImage titleBarImage = titleBar.gameObject.AddComponent<CImage>();
        if (definition.Presentation == TaiwuWindowPresentation.Encyclopedia)
            titleBarImage.color = new Color(0.015f, 0.045f, 0.05f, 0.28f);
        else
            _theme.ApplyTitleBar(titleBarImage);

        // Title text
        TextMeshProUGUI title = CreateText(
            "Title", titleBar, definition.Title,
            definition.Presentation == TaiwuWindowPresentation.Encyclopedia
                ? TaiwuUiMetrics.EncyclopediaTitleFontSize
                : TaiwuUiMetrics.WindowTitleFontSize,
            TextAlignmentOptions.MidlineLeft, TaiwuTextStyle.Heading);
        // The title background is window chrome and nearly fills the panel. Its
        // label still aligns with the narrower body content column.
        SetRect(title.rectTransform, Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero);
        title.margin = new Vector4(
            definition.Presentation == TaiwuWindowPresentation.Encyclopedia
                ? TaiwuUiMetrics.EncyclopediaHorizontalInset
                : TaiwuUiMetrics.ContentHorizontalInset - TaiwuUiMetrics.WindowChromeInset,
            0f,
            TaiwuUiMetrics.CloseButtonSize + 16f,
            0f);
        title.enableWordWrapping = false;
        title.overflowMode = TextOverflowModes.Ellipsis;

        // Close button (matches ViewFindMapBlock's ButtonCloseView)
        float closeSize = definition.Presentation == TaiwuWindowPresentation.Encyclopedia
            ? TaiwuUiMetrics.EncyclopediaCloseButtonSize
            : TaiwuUiMetrics.CloseButtonSize;
        RectTransform closeRect = CreateRect(
            "Close", titleBar, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(closeSize, closeSize));
        closeRect.anchoredPosition = new Vector2(-closeSize * 0.5f - 18f, 0f);
        CImage closeImage = closeRect.gameObject.AddComponent<CImage>();
        CButton closeButton = closeRect.gameObject.AddComponent<CButton>();
        closeButton.targetGraphic = closeImage;
        _theme.ApplyCloseButton(closeImage, closeButton);
        closeButton.onClick.AddListener(() => close());

        // Content area
        RectTransform content = CreateRect("Content", panel, Vector2.zero, Vector2.one, Vector2.zero);
        bool encyclopedia = definition.Presentation == TaiwuWindowPresentation.Encyclopedia;
        float horizontalInset = encyclopedia
            ? TaiwuUiMetrics.EncyclopediaHorizontalInset
            : TaiwuUiMetrics.ContentHorizontalInset;
        content.offsetMin = new Vector2(horizontalInset,
            encyclopedia ? TaiwuUiMetrics.EncyclopediaBottomInset : TaiwuUiMetrics.ContentBottomInset);
        content.offsetMax = new Vector2(-horizontalInset,
            -(TaiwuUiMetrics.TitleHeight + (encyclopedia
                ? TaiwuUiMetrics.EncyclopediaContentTopGap
                : TaiwuUiMetrics.ContentTopGap)));
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = TaiwuUiMetrics.ContentSpacing;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        BuildNodes(content, definition.Nodes);
    }

    public override void OnInit(FrameWork.ArgumentBox argsBox) { }

    private void BuildNodes(Transform parent, IEnumerable<UiNode> nodes)
    {
        foreach (UiNode node in nodes)
            BuildNode(parent, node);
    }

    private void BuildScroll(Transform parent, ScrollNode node)
    {
        RectTransform root = UiFactory.Rect("Scroll", parent);
        UiFactory.Layout(root, -1f, node.Options.Height, flexibleWidth: 1f);
        CImage background = root.gameObject.AddComponent<CImage>();
        if (node.Options.ShowBackground)
            Theme.ApplyScrollBackground(background);
        else
            background.color = Color.clear;

        float scrollbarWidth = node.Options.ShowScrollbar ? 20f : 0f;
        RectTransform viewport = UiFactory.Rect("Viewport", root);
        UiFactory.Stretch(viewport,
            new Vector2(node.Options.Padding, node.Options.Padding),
            new Vector2(-(node.Options.Padding + scrollbarWidth), -node.Options.Padding));
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform content = UiFactory.Rect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = node.Options.Spacing;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = root.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 52f;

        if (node.Options.ShowScrollbar)
        {
            RectTransform trackRoot = UiFactory.Rect("VerticalScrollbar", root);
            trackRoot.anchorMin = new Vector2(1f, 0f);
            trackRoot.anchorMax = new Vector2(1f, 1f);
            trackRoot.pivot = new Vector2(1f, 0.5f);
            trackRoot.sizeDelta = new Vector2(scrollbarWidth, -node.Options.Padding * 2f);
            trackRoot.anchoredPosition = new Vector2(-node.Options.Padding, 0f);
            CImage track = trackRoot.gameObject.AddComponent<CImage>();

            RectTransform handleRoot = UiFactory.Rect("HandleRect", trackRoot);
            UiFactory.Stretch(handleRoot, Vector2.zero, Vector2.zero);
            CImage handle = handleRoot.gameObject.AddComponent<CImage>();
            Theme.ApplyVerticalScrollbar(track, handle);

            // CScrollbar is designed for the game's prefab hierarchy. Its Awake
            // immediately looks up prefab-only hover nodes, so constructing it on
            // a declarative framework object throws before these fields can be
            // assigned. The base Scrollbar has the behaviour ScrollRect needs
            // without that hidden prefab contract.
            Scrollbar scrollbar = trackRoot.gameObject.AddComponent<Scrollbar>();
            scrollbar.targetGraphic = handle;
            scrollbar.handleRect = handleRoot;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        BuildNodes(content, node.Children);
    }

    private void BuildNode(Transform parent, UiNode node)
    {
        int childCount = parent.childCount;
        if (FrameworkComponentRenderer.TryRender(parent, (RectTransform)transform, node, Theme))
        {
            TagCreatedRoot(parent, childCount, node);
            return;
        }

        switch (node)
        {
            case TextNode textNode:
            {
                TextAlignmentOptions alignment =
                    parent.GetComponent<HorizontalLayoutGroup>() != null
                        ? TextAlignmentOptions.MidlineLeft
                        : TextAlignmentOptions.TopLeft;
                TextMeshProUGUI text = CreateText(
                    "Text", parent, textNode.Text, textNode.Options.FontSize,
                    alignment, textNode.Options.Style);
                LayoutElement element = text.gameObject.AddComponent<LayoutElement>();
                float textHeight = Math.Max(
                    textNode.Options.MinimumHeight, textNode.Options.FontSize * 1.5f);
                element.minHeight = textHeight;
                element.preferredHeight = textHeight;
                element.flexibleWidth = 1f;
                element.flexibleHeight = 0f;
                break;
            }
            case ButtonNode buttonNode:
            {
                RectTransform buttonRoot = CreateRect(
                    "Button_" + _buttonSequence++, parent,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(buttonNode.Options.Width, buttonNode.Options.Height));
                LayoutElement element = buttonRoot.gameObject.AddComponent<LayoutElement>();
                element.preferredWidth = buttonNode.Options.Width;
                element.minHeight = buttonNode.Options.Height;
                element.preferredHeight = buttonNode.Options.Height;
                element.flexibleWidth = 0f;
                element.flexibleHeight = 0f;
                AddButton(buttonRoot, buttonNode.Label, buttonNode.OnClick, buttonNode.Options);
                break;
            }
            case RowNode rowNode:
            {
                float rowHeight = Math.Max(
                    TaiwuUiMetrics.ButtonHeight,
                    rowNode.Children.Select(PreferredHeight).DefaultIfEmpty(0f).Max());
                RectTransform row = CreateRect(
                    "Row", parent, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                    new Vector2(0f, rowHeight));
                LayoutElement element = row.gameObject.AddComponent<LayoutElement>();
                element.minHeight = rowHeight;
                element.preferredHeight = rowHeight;
                element.flexibleWidth = 1f;
                element.flexibleHeight = 0f;
                var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = rowNode.Spacing;
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
                BuildNodes(row, rowNode.Children);
                break;
            }

            case ColumnNode columnNode:
            {
                RectTransform column = CreateRect(
                    "Column", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero);
                LayoutElement columnElement = column.gameObject.AddComponent<LayoutElement>();
                columnElement.flexibleWidth = 1f;
                columnElement.preferredHeight = columnNode.Children.Sum(PreferredHeight) +
                    Math.Max(0, columnNode.Children.Count - 1) * columnNode.Spacing;
                var columnLayout = column.gameObject.AddComponent<VerticalLayoutGroup>();
                columnLayout.spacing = columnNode.Spacing;
                columnLayout.childControlWidth = true;
                columnLayout.childForceExpandWidth = true;
                columnLayout.childControlHeight = true;
                columnLayout.childForceExpandHeight = false;
                BuildNodes(column, columnNode.Children);
                break;
            }

            case FlexNode flexNode:
            {
                RectTransform flex = CreateRect(
                    "Flex", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero);
                LayoutElement flexElement = flex.gameObject.AddComponent<LayoutElement>();
                flexElement.layoutPriority = 10;
                flexElement.minWidth = 0f;
                flexElement.preferredWidth = 0f;
                flexElement.flexibleWidth = flexNode.Grow;
                flexElement.preferredHeight = flexNode.Children.Sum(PreferredHeight);
                var flexLayout = flex.gameObject.AddComponent<VerticalLayoutGroup>();
                flexLayout.childControlWidth = true;
                flexLayout.childForceExpandWidth = true;
                flexLayout.childControlHeight = true;
                flexLayout.childForceExpandHeight = false;
                BuildNodes(flex, flexNode.Children);
                break;
            }

            case DynamicNode dynamicNode:
            {
                RectTransform host = CreateRect(
                    "Dynamic", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero);
                LayoutElement hostElement = host.gameObject.AddComponent<LayoutElement>();
                hostElement.minHeight = dynamicNode.Height;
                hostElement.preferredHeight = dynamicNode.Height;
                hostElement.flexibleWidth = 1f;
                var hostLayout = host.gameObject.AddComponent<VerticalLayoutGroup>();
                hostLayout.childControlWidth = true;
                hostLayout.childForceExpandWidth = true;
                hostLayout.childControlHeight = true;
                hostLayout.childForceExpandHeight = false;

                // Always compile from the current value. The plan's stored children
                // freeze the content at mount time; a native rebuild (reopen after the
                // UIElement sleep state destroyed the GameObject) would otherwise show
                // stale or empty content that never catches up with the live value.
                void RebuildHost(UiElement content)
                {
                    List<UiNode> nextNodes;
                    try
                    {
                        nextNodes = new List<UiNode> { UiElementCompiler.CompileElement(content) };
                        UiRenderPlanCompiler.AssignIdentities(nextNodes, dynamicNode.Identity + "/fragment");
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                        return;
                    }
                    try
                    {
                        for (int index = host.childCount - 1; index >= 0; index--)
                            DestroyImmediate(host.GetChild(index).gameObject);
                        BuildNodes(host, nextNodes);
                        Canvas.ForceUpdateCanvases();
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                    }
                }

                RebuildHost(dynamicNode.Content.Value);
                Action<UiElement> refresh = DynamicContentSubscription.Subscribe(
                    dynamicNode.Content,
                    () => host != null,
                    RebuildHost);
                UiFactory.Lifetime(host).Add(() => dynamicNode.Content.AdapterValueChanged -= refresh);
                break;
            }

            case NativeImageNode nativeImage:
            {
                RectTransform nativeImageRect = CreateRect(
                    "NativeImage", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(nativeImage.Width, nativeImage.Height));
                LayoutElement nativeImageLayout = nativeImageRect.gameObject.AddComponent<LayoutElement>();
                nativeImageLayout.preferredWidth = nativeImage.Width;
                nativeImageLayout.preferredHeight = nativeImage.Height;
                CImage nativeImageView = nativeImageRect.gameObject.AddComponent<CImage>();
                Theme.ApplyNativeAsset(nativeImageView, nativeImage.Asset);
                nativeImageView.preserveAspect = true;
                nativeImageView.raycastTarget = false;
                break;
            }
            case DividerNode:
            {
                RectTransform divider = CreateRect(
                    "Divider", parent, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 4f));
                CImage image = divider.gameObject.AddComponent<CImage>();
                Theme.ApplyDivider(image);
                image.raycastTarget = false;
                LayoutElement element = divider.gameObject.AddComponent<LayoutElement>();
                element.minHeight = 4f;
                element.preferredHeight = 4f;
                element.flexibleWidth = 1f;
                element.flexibleHeight = 0f;
                break;
            }
            case SpacerNode spacerNode:
            {
                RectTransform spacer = CreateRect(
                    "Spacer", parent, Vector2.zero, Vector2.zero, new Vector2(0f, spacerNode.Height));
                LayoutElement element = spacer.gameObject.AddComponent<LayoutElement>();
                element.preferredHeight = spacerNode.Height;
                element.flexibleHeight = 0f;
                break;
            }
            case ScrollNode scrollNode:
            {
                BuildScroll(parent, scrollNode);
                break;
            }
            case TabViewNode tabViewNode:
            {
                BuildTabView(parent, tabViewNode);
                break;
            }
            case BottomTabsNode bottomTabsNode:
            {
                BuildBottomTabs(parent, bottomTabsNode);
                break;
            }
        }
        TagCreatedRoot(parent, childCount, node);
    }

    private static void TagCreatedRoot(Transform parent, int previousChildCount, UiNode node)
    {
        if (parent.childCount <= previousChildCount)
            return;
        GameObject root = parent.GetChild(previousChildCount).gameObject;
        UiElementIdentity identity = root.AddComponent<UiElementIdentity>();
        identity.Path = node.Identity;
        identity.Kind = node.GetType().Name;
    }

    internal UiRuntimeState CaptureRuntimeState()
    {
        var state = new UiRuntimeState();
        foreach (ScrollRect scroll in GetComponentsInChildren<ScrollRect>(true))
        {
            UiElementIdentity? identity = scroll.GetComponentInParent<UiElementIdentity>();
            if (identity != null)
                state.ScrollPositions[identity.StateKey(scroll.transform.GetSiblingIndex())] =
                    scroll.normalizedPosition;
        }
        foreach (TMP_InputField input in GetComponentsInChildren<TMP_InputField>(true))
        {
            if (!input.isFocused)
                continue;
            UiElementIdentity? identity = input.GetComponentInParent<UiElementIdentity>();
            if (identity != null)
                state.Focused = identity.StateKey(0);
        }
        return state;
    }

    internal void RestoreRuntimeState(UiRuntimeState state)
    {
        DeferredUiAction.Run(gameObject, () =>
        {
            foreach (ScrollRect scroll in GetComponentsInChildren<ScrollRect>(true))
            {
                UiElementIdentity? identity = scroll.GetComponentInParent<UiElementIdentity>();
                if (identity != null && state.ScrollPositions.TryGetValue(
                    identity.StateKey(scroll.transform.GetSiblingIndex()), out Vector2 position))
                    scroll.normalizedPosition = position;
            }
            foreach (TMP_InputField input in GetComponentsInChildren<TMP_InputField>(true))
            {
                UiElementIdentity? identity = input.GetComponentInParent<UiElementIdentity>();
                if (identity != null && identity.StateKey(0) == state.Focused)
                {
                    input.Select();
                    input.ActivateInputField();
                    break;
                }
            }
        });
    }

    private void BuildBottomTabs(Transform parent, BottomTabsNode node)
    {
        RectTransform root = CreateRect(
            "BottomTabs", parent, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(0f, node.Options.Height));
        UiFactory.Layout(root, -1f, node.Options.Height, flexibleWidth: 1f);
        CImage background = root.gameObject.AddComponent<CImage>();
        Theme.ApplyBottomTabsBackground(background);
        background.raycastTarget = false;

        ChoiceSnapshot initial = node.Projection.Snapshot<ChoiceSnapshot>();
        float stripWidth = initial.Items.Count * node.Options.ItemWidth +
            Math.Max(0, initial.Items.Count - 1) * node.Options.Spacing;
        RectTransform strip = CreateRect(
            "Strip", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(stripWidth, node.Options.TabHeight));
        var layout = strip.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = node.Options.Spacing;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var selectedImages = new List<CImage>();
        var buttons = new List<CButton>();
        for (int index = 0; index < initial.Items.Count; index++)
        {
            int captured = index;
            RectTransform tab = CreateRect(
                "Tab_" + index, strip, Vector2.zero, Vector2.zero,
                new Vector2(node.Options.ItemWidth, node.Options.TabHeight));
            UiFactory.Layout(tab, node.Options.ItemWidth, node.Options.TabHeight, flexibleWidth: 0f);
            CImage image = tab.gameObject.AddComponent<CImage>();
            image.color = Color.clear;
            CButton button = tab.gameObject.AddComponent<CButton>();
            button.targetGraphic = image;
            button.onClick.AddListener(() =>
            {
                ChoiceSnapshot current = node.Projection.Snapshot<ChoiceSnapshot>();
                if (!current.Items[captured].Selected)
                    node.Projection.Dispatch(new SelectChoiceIntent(captured));
            });
            RectTransform hoverRect = CreateRect(
                "Hover", tab, Vector2.zero, Vector2.one, Vector2.zero);
            CImage hoverImage = hoverRect.gameObject.AddComponent<CImage>();
            Theme.ApplyBottomTabHover(hoverImage);
            hoverImage.raycastTarget = false;
            hoverRect.gameObject.SetActive(false);
            TaiwuMenuHover hover = tab.gameObject.AddComponent<TaiwuMenuHover>();
            hover.Enter = () => hoverRect.gameObject.SetActive(true);
            hover.Exit = () => hoverRect.gameObject.SetActive(false);
            RectTransform selectedRect = CreateRect(
                "Selected", tab, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(node.Options.ItemWidth + 46.99f, node.Options.TabHeight + 11.1f));
            selectedRect.anchoredPosition = new Vector2(10.97f, 1.45f);
            CImage selectedImage = selectedRect.gameObject.AddComponent<CImage>();
            selectedImage.raycastTarget = false;
            TextMeshProUGUI label = CreateText(
                "Label", tab, initial.Items[index].Label, 36f,
                TextAlignmentOptions.Center, TaiwuTextStyle.Body);
            SetRect(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            if (index < initial.Items.Count - 1)
            {
                RectTransform divider = CreateRect(
                    "RightLine", tab, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                    new Vector2(2f, node.Options.TabHeight * 0.626f));
                divider.anchoredPosition = new Vector2(node.Options.Spacing * 0.5f + 1f, 0f);
                CImage dividerImage = divider.gameObject.AddComponent<CImage>();
                Theme.ApplyBottomTabDivider(dividerImage);
                dividerImage.raycastTarget = false;
            }
            selectedImages.Add(selectedImage);
            buttons.Add(button);
        }

        void Refresh()
        {
            ChoiceSnapshot snapshot = node.Projection.Snapshot<ChoiceSnapshot>();
            for (int index = 0; index < selectedImages.Count; index++)
            {
                buttons[index].interactable = snapshot.Items[index].Interactable;
                Theme.ApplyBottomTab(selectedImages[index], buttons[index], snapshot.Items[index].Selected);
            }
        }

        node.Projection.Changed += Refresh;
        UiFactory.Lifetime(root).Add(() => node.Projection.Changed -= Refresh);
        UiFactory.Lifetime(root).Add(node.Projection.Dispose);
        Refresh();
    }

    private void BuildTabView(Transform parent, TabViewNode node)
    {
        RectTransform root = CreateRect(
            "TabView", parent, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(0f, node.Options.Height));
        LayoutElement rootLayout = root.gameObject.AddComponent<LayoutElement>();
        rootLayout.minHeight = node.Options.Height;
        rootLayout.preferredHeight = node.Options.Height;
        rootLayout.flexibleWidth = 1f;
        rootLayout.flexibleHeight = 0f;

        RectTransform tabs = CreateRect(
            "Tabs", root, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, node.Options.TabHeight));
        tabs.pivot = new Vector2(0.5f, 1f);
        tabs.anchoredPosition = Vector2.zero;
        CImage tabsBackground = tabs.gameObject.AddComponent<CImage>();
        Theme.ApplySecondaryTabsBackground(tabsBackground);
        var tabsLayout = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
        tabsLayout.spacing = 0f;
        tabsLayout.padding = new RectOffset(0, 0, 0, 0);
        tabsLayout.childControlWidth = true;
        tabsLayout.childControlHeight = true;
        tabsLayout.childForceExpandWidth = true;
        tabsLayout.childForceExpandHeight = true;

        var tabImages = new List<CImage>();
        var tabButtons = new List<CButton>();
        ChoiceSnapshot initial = node.Projection.Snapshot<ChoiceSnapshot>();
        for (int index = 0; index < initial.Items.Count; index++)
        {
            int captured = index;
            RectTransform tab = CreateRect(
                "Tab_" + index, tabs, Vector2.zero, Vector2.one, Vector2.zero);
            LayoutElement layout = tab.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = 72f;
            layout.flexibleWidth = 1f;
            CImage image = tab.gameObject.AddComponent<CImage>();
            CButton button = tab.gameObject.AddComponent<CButton>();
            button.targetGraphic = image;
            // Unselected tabs keep a transparent base color so the window
            // background shows through; SpriteSwap multiplies the hover artwork
            // by that color and the hover would be invisible.
            tab.gameObject.AddComponent<TaiwuHoverTint>();
            button.onClick.AddListener(() => node.Projection.Dispatch(new SelectChoiceIntent(captured)));
            RectTransform hoverRect = CreateRect(
                "Hover", tab, Vector2.zero, Vector2.one, Vector2.zero);
            CImage hoverImage = hoverRect.gameObject.AddComponent<CImage>();
            Theme.ApplySecondaryTabHover(hoverImage);
            hoverRect.gameObject.SetActive(false);
            TaiwuMenuHover hover = tab.gameObject.AddComponent<TaiwuMenuHover>();
            hover.Enter = () => hoverRect.gameObject.SetActive(true);
            hover.Exit = () => hoverRect.gameObject.SetActive(false);
            TextMeshProUGUI label = CreateText(
                "Label", tab, initial.Items[index].Label, 24f,
                TextAlignmentOptions.Center, TaiwuTextStyle.Body);
            SetRect(label.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(6f, 0f), new Vector2(-6f, 0f));
            if (index < initial.Items.Count - 1)
            {
                RectTransform line = CreateRect(
                    "RightLine", tab, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                    new Vector2(2f, node.Options.TabHeight * 0.5f));
                line.pivot = new Vector2(1f, 0.5f);
                CImage lineImage = line.gameObject.AddComponent<CImage>();
                Theme.ApplySecondaryTabDivider(lineImage);
                lineImage.raycastTarget = false;
            }
            tabImages.Add(image);
            tabButtons.Add(button);
        }

        RectTransform pageHost = CreateRect(
            "Pages", root, Vector2.zero, Vector2.one, Vector2.zero);
        pageHost.offsetMin = Vector2.zero;
        pageHost.offsetMax = new Vector2(0f, -node.Options.TabHeight);
        var pages = new List<RectTransform>();
        for (int index = 0; index < node.Pages.Count; index++)
        {
            RectTransform page = CreateRect(
                "Page_" + index, pageHost, Vector2.zero, Vector2.one, Vector2.zero);
            page.offsetMin = Vector2.zero;
            page.offsetMax = Vector2.zero;
            var layout = page.gameObject.AddComponent<VerticalLayoutGroup>();
            int padding = Mathf.RoundToInt(node.Options.ContentPadding);
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.spacing = node.Options.ContentSpacing;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            BuildNodes(page, node.Pages[index].Children);
            pages.Add(page);
        }

        void Refresh()
        {
            ChoiceSnapshot snapshot = node.Projection.Snapshot<ChoiceSnapshot>();
            for (int index = 0; index < tabImages.Count; index++)
            {
                bool selected = snapshot.Items[index].Selected;
                tabButtons[index].interactable = snapshot.Items[index].Interactable;
                Theme.ApplySecondaryTab(tabImages[index], tabButtons[index], selected);
                pages[index].gameObject.SetActive(selected);
            }
        }

        node.Projection.Changed += Refresh;
        UiFactory.Lifetime(root).Add(() => node.Projection.Changed -= Refresh);
        UiFactory.Lifetime(root).Add(node.Projection.Dispose);
        Refresh();
    }

    private void CreateButton(
        string name,
        Transform parent,
        string label,
        Action action,
        TaiwuButtonStyle style,
        Vector2 anchor,
        Vector2 position,
        Vector2 size)
    {
        RectTransform rect = CreateRect(name, parent, anchor, anchor, size);
        rect.anchoredPosition = position;
        AddButton(rect, label, action, new TaiwuButtonOptions
        {
            Width = size.x,
            Height = size.y,
            Style = style,
        });
    }

    private void AddButton(
        RectTransform rect,
        string label,
        Action action,
        TaiwuButtonOptions options)
    {
        CImage image = rect.gameObject.AddComponent<CImage>();
        CButton button = rect.gameObject.AddComponent<CButton>();
        button.targetGraphic = image;
        Theme.ApplyButton(image, button, options.Style);
        button.onClick.AddListener(() => action());
        TextMeshProUGUI text = CreateText(
            "Label", rect, label, options.FontSize,
            TextAlignmentOptions.Center, TaiwuTextStyle.Body);
        SetRect(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    private TextMeshProUGUI CreateText(
        string name,
        Transform parent,
        string value,
        float size,
        TextAlignmentOptions alignment,
        TaiwuTextStyle style)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.font = Theme.Font;
        text.text = value;
        text.fontSize = size;
        text.color = Theme.TextColor(style);
        text.alignment = alignment;
        text.raycastTarget = false;
        text.richText = true;
        return text;
    }

    private TaiwuTheme Theme => _theme ?? throw new InvalidOperationException("Theme is not initialized.");

    private static float PreferredHeight(UiNode node) => node switch
    {
        TextNode text => Math.Max(text.Options.MinimumHeight, text.Options.FontSize * 1.5f),
        ButtonNode button => button.Options.Height,
        SearchInputNode => 56f,
        ToggleNode => 78f,
        SliderNode => 56f,
        RangeSliderNode => 56f,
        ChoiceGroupNode choices => string.IsNullOrEmpty(choices.Label)
            ? choices.Compact ? 40f : 52f
            : choices.Compact ? 72f : 86f,
        PopupSelectNode popupSelect => popupSelect.Options.Height,
        NativeActionIconNode icon => icon.Size,
        DividerNode => 4f,
        SpacerNode spacer => spacer.Height,
        RowNode row => row.Children.Select(PreferredHeight).DefaultIfEmpty(0f).Max(),
        ColumnNode column => column.Children.Sum(PreferredHeight) +
            Math.Max(0, column.Children.Count - 1) * column.Spacing,
        FlexNode flex => flex.Children.Sum(PreferredHeight),
        DynamicNode dynamic => dynamic.Height,
        NativeImageNode image => image.Height,
        TabsNode tabs => tabs.Height,
        NavigationNode navigation => navigation.Options.Height,
        TableNode table => table.Options.Height,
        ScrollNode scroll => scroll.Options.Height,
        TabViewNode tabView => tabView.Options.Height,
        BottomTabsNode bottomTabs => bottomTabs.Options.Height,
        _ => TaiwuUiMetrics.ButtonHeight,
    };

    private static RectTransform CreateRect(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;
        return rect;
    }

    private static void SetRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}

/// <summary>
/// Keeps a tab's target graphic at full opacity while hovered. Secondary tabs
/// use a transparent base color when unselected (the window background shows
/// through), and Unity's SpriteSwap multiplies the hover artwork by that
/// color, making the hover invisible. On exit the base color is recomputed
/// from the current base sprite, mirroring TaiwuTheme.ApplySecondaryTab, so a
/// selection change during hover cannot strand a stale color.
/// </summary>
internal sealed class TaiwuHoverTint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Graphic? _graphic;

    private void Awake() => _graphic = GetComponent<Graphic>();

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_graphic != null)
            _graphic.color = Color.white;
    }

    public void OnPointerExit(PointerEventData eventData) => ApplyBaseColor();

    private void OnDisable() => ApplyBaseColor();

    private void ApplyBaseColor()
    {
        if (_graphic is Image image)
            image.color = image.sprite == null ? Color.clear : Color.white;
    }
}
