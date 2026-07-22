using FrameWork.UISystem.UIElements;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TaiwuUi;

internal static class FrameworkNavigationRenderer
{
    internal static void RenderTabs(Transform parent, TabsNode node, TaiwuTheme theme)
    {
        RectTransform root = UiFactory.Rect(
            node.Style == TabsNodeStyle.Icon ? "IconTabs" : "ClosableTabs", parent);
        UiFactory.Layout(root, -1f, node.Height, flexibleWidth: 1f);
        RectTransform itemsRoot = node.Style == TabsNodeStyle.Icon
            ? BuildIconTabsContainer(root)
            : BuildClosableTabsViewport(root, node, theme);

        void Refresh()
        {
            TabsSnapshot snapshot = node.Projection.Snapshot<TabsSnapshot>();
            ClearChildren(itemsRoot);
            for (int index = 0; index < snapshot.Items.Count; index++)
            {
                if (node.Style == TabsNodeStyle.Icon)
                    BuildIconTab(itemsRoot, node, snapshot, index, theme);
                else
                    BuildClosableTab(itemsRoot, node, snapshot, index, theme);
            }
        }

        node.Projection.Changed += Refresh;
        UiFactory.Lifetime(root).Add(() => node.Projection.Changed -= Refresh);
        UiFactory.Lifetime(root).Add(node.Projection.Dispose);
        Refresh();
    }

    private static RectTransform BuildIconTabsContainer(RectTransform root)
    {
        var layout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        return root;
    }

    private static RectTransform BuildClosableTabsViewport(
        RectTransform root,
        TabsNode node,
        TaiwuTheme theme)
    {
        float clearWidth = node.ShowClearButton ? 68f : 0f;
        RectTransform viewport = UiFactory.Rect("Viewport", root);
        UiFactory.Stretch(viewport, Vector2.zero, new Vector2(-clearWidth, 0f));
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform content = UiFactory.Rect("Content", viewport);
        content.anchorMin = new Vector2(0f, 0f);
        content.anchorMax = new Vector2(0f, 1f);
        content.pivot = new Vector2(0f, 0.5f);
        content.sizeDelta = Vector2.zero;
        var layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 2f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = true;
        scroll.vertical = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 56f;

        if (node.ShowClearButton)
        {
            RectTransform clearRoot = UiFactory.Rect("ClearAll", root);
            clearRoot.anchorMin = clearRoot.anchorMax = new Vector2(1f, 0.5f);
            clearRoot.sizeDelta = new Vector2(68f, node.Height);
            clearRoot.anchoredPosition = new Vector2(-34f, 0f);
            CImage clearImage = clearRoot.gameObject.AddComponent<CImage>();
            theme.ApplyClosableTabsClear(clearImage);
            CButton clear = clearRoot.gameObject.AddComponent<CButton>();
            ConfigureFlatButton(clear, clearImage, true);
            clear.onClick.AddListener(() => node.Projection.Dispatch(new ClearTabsIntent()));
        }
        return content;
    }

    internal static void RenderNavigation(
        Transform parent,
        NavigationNode node,
        TaiwuTheme theme)
    {
        RectTransform root = UiFactory.Rect("Navigation", parent);
        UiFactory.Layout(root, node.Options.Width, node.Options.Height, flexibleWidth: 0f);
        CImage background = root.gameObject.AddComponent<CImage>();
        background.color = new Color(0.025f, 0.055f, 0.058f, 0.98f);

        RectTransform viewport = UiFactory.Rect("Viewport", root);
        UiFactory.Stretch(viewport, new Vector2(6f, 6f), new Vector2(-6f, -6f));
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform content = UiFactory.Rect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        var vertical = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vertical.spacing = 2f;
        vertical.childAlignment = TextAnchor.UpperLeft;
        vertical.childControlWidth = true;
        vertical.childControlHeight = true;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = node.Options.ItemHeight * 0.7f;

        void Refresh()
        {
            NavigationSnapshot snapshot = node.Projection.Snapshot<NavigationSnapshot>();
            ClearChildren(content);
            for (int group = 0; group < snapshot.Groups.Count; group++)
            {
                BuildNavigationGroup(content, node, snapshot, group, theme);
                if (!snapshot.Groups[group].Expanded)
                    continue;
                for (int item = 0; item < snapshot.Groups[group].Items.Count; item++)
                    BuildNavigationItem(content, node, snapshot, group, item, theme);
            }
        }

        node.Projection.Changed += Refresh;
        UiFactory.Lifetime(root).Add(() => node.Projection.Changed -= Refresh);
        UiFactory.Lifetime(root).Add(node.Projection.Dispose);
        Refresh();
    }

    private static void BuildIconTab(
        RectTransform parent,
        TabsNode node,
        TabsSnapshot snapshot,
        int index,
        TaiwuTheme theme)
    {
        TabItemSnapshot state = snapshot.Items[index];
        bool selected = state.Selected;
        bool interactable = state.Interactable;
        RectTransform item = UiFactory.Rect("Tab_" + index, parent);
        LayoutElement itemLayout = UiFactory.Layout(item, node.MinimumItemWidth, node.Height, flexibleWidth: 1f);
        itemLayout.minWidth = node.MinimumItemWidth;
        CImage background = item.gameObject.AddComponent<CImage>();
        background.color = selected
            ? new Color(0.36f, 0.12f, 0.13f, 0.96f)
            : new Color(0.08f, 0.12f, 0.12f, 0.82f);
        CButton button = item.gameObject.AddComponent<CButton>();
        ConfigureFlatButton(button, background, interactable);
        button.onClick.AddListener(() => node.Projection.Dispatch(new SelectTabIntent(index)));

        // The encyclopedia resources are complete tab artworks rather than
        // tightly cropped glyphs. Native UI renders each one across the full
        // 196x112 tab target; putting it in a small square shrinks both its
        // transparent margins and the visible symbol.
        RectTransform artworkRoot = UiFactory.Rect("Artwork", item);
        UiFactory.Stretch(artworkRoot, Vector2.zero, Vector2.zero);
        CImage artwork = artworkRoot.gameObject.AddComponent<CImage>();
        artwork.sprite = state.Icon is { } token ? theme.ResolveIcon(token) : null;
        artwork.preserveAspect = true;
        artwork.raycastTarget = false;
        artwork.gameObject.SetActive(artwork.sprite != null);

        TextMeshProUGUI label = UiFactory.Text(
            "Label", item, state.Label, 22f, theme,
            selected ? TaiwuTextStyle.Heading : TaiwuTextStyle.Muted,
            TextAlignmentOptions.Center);
        label.rectTransform.anchorMin = new Vector2(0f, 0f);
        label.rectTransform.anchorMax = new Vector2(1f, 0f);
        label.rectTransform.pivot = new Vector2(0.5f, 0f);
        label.rectTransform.sizeDelta = new Vector2(-12f, 36f);
        label.rectTransform.anchoredPosition = new Vector2(0f, 4f);

        if (selected)
        {
            RectTransform line = UiFactory.Rect("Selected", item);
            line.anchorMin = new Vector2(0f, 0f);
            line.anchorMax = new Vector2(1f, 0f);
            line.pivot = new Vector2(0.5f, 0f);
            line.sizeDelta = new Vector2(-10f, 4f);
            line.anchoredPosition = Vector2.zero;
            line.gameObject.AddComponent<CImage>().color = new Color(0.76f, 0.24f, 0.22f, 1f);
        }
    }

    private static void BuildClosableTab(
        RectTransform parent,
        TabsNode node,
        TabsSnapshot snapshot,
        int index,
        TaiwuTheme theme)
    {
        TabItemSnapshot state = snapshot.Items[index];
        bool selected = state.Selected;
        bool interactable = state.Interactable;
        bool closable = state.Closable;
        float width = Math.Max(
            node.MinimumItemWidth,
            state.Label.Length * 23f + (closable ? 58f : 28f));
        RectTransform item = UiFactory.Rect("Tab_" + index, parent);
        UiFactory.Layout(item, width, node.Height - 4f, flexibleWidth: 0f);
        CImage background = item.gameObject.AddComponent<CImage>();
        theme.ApplyClosableTabBackground(background);
        background.color = background.sprite == null
            ? selected
                ? new Color(0.16f, 0.20f, 0.19f, 1f)
                : new Color(0.10f, 0.14f, 0.14f, 1f)
            : Color.white;
        CButton button = item.gameObject.AddComponent<CButton>();
        ConfigureFlatButton(button, background, interactable);
        button.onClick.AddListener(() => node.Projection.Dispatch(new SelectTabIntent(index)));

        if (selected)
        {
            RectTransform selectedRoot = UiFactory.Rect("Selected", item);
            UiFactory.Stretch(selectedRoot, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            CImage selectedImage = selectedRoot.gameObject.AddComponent<CImage>();
            bool hasSelectedArtwork = theme.ApplyClosableTabSelected(selectedImage);
            selectedImage.raycastTarget = false;
            selectedRoot.gameObject.SetActive(hasSelectedArtwork);
        }

        TextMeshProUGUI label = UiFactory.Text(
            "Label", item, state.Label, 21f, theme,
            selected ? TaiwuTextStyle.Heading : TaiwuTextStyle.Body,
            TextAlignmentOptions.MidlineLeft);
        UiFactory.Stretch(label.rectTransform, new Vector2(14f, 0f), new Vector2(closable ? -46f : -14f, 0f));

        if (!closable)
            return;
        RectTransform closeRoot = UiFactory.Rect("Close", item);
        closeRoot.anchorMin = closeRoot.anchorMax = new Vector2(1f, 0.5f);
        closeRoot.sizeDelta = new Vector2(36f, 36f);
        closeRoot.anchoredPosition = new Vector2(-20f, 0f);
        CImage closeImage = closeRoot.gameObject.AddComponent<CImage>();
        theme.ApplyClosableTabClose(closeImage);
        CButton close = closeRoot.gameObject.AddComponent<CButton>();
        ConfigureFlatButton(close, closeImage, interactable);
        TextMeshProUGUI fallback = UiFactory.Text(
            "Fallback", closeRoot, "关", 15f, theme, TaiwuTextStyle.Heading,
            TextAlignmentOptions.Center);
        UiFactory.Stretch(fallback.rectTransform, Vector2.zero, Vector2.zero);
        fallback.gameObject.SetActive(closeImage.sprite == null);
        close.onClick.AddListener(() => node.Projection.Dispatch(new CloseTabIntent(index)));
    }

    private static void BuildNavigationGroup(
        RectTransform parent,
        NavigationNode node,
        NavigationSnapshot snapshot,
        int group,
        TaiwuTheme theme)
    {
        NavigationGroupSnapshot groupState = snapshot.Groups[group];
        bool expanded = groupState.Expanded;
        bool interactable = groupState.Interactable;
        RectTransform header = UiFactory.Rect("Group_" + group, parent);
        UiFactory.Layout(header, -1f, node.Options.GroupHeight, flexibleWidth: 1f);
        CImage background = header.gameObject.AddComponent<CImage>();
        background.color = new Color(0.06f, 0.105f, 0.105f, 1f);
        CButton button = header.gameObject.AddComponent<CButton>();
        ConfigureFlatButton(button, background, interactable);
        button.onClick.AddListener(() => node.Projection.Dispatch(new ToggleNavigationGroupIntent(group)));

        RectTransform stateRoot = UiFactory.Rect("State", header);
        stateRoot.anchorMin = stateRoot.anchorMax = new Vector2(0f, 0.5f);
        stateRoot.sizeDelta = new Vector2(34f, 34f);
        stateRoot.anchoredPosition = new Vector2(22f, 0f);
        CImage stateImage = stateRoot.gameObject.AddComponent<CImage>();
        theme.ApplyNavigationGroupState(stateImage);
        TextMeshProUGUI state = UiFactory.Text(
            "Label", stateRoot, expanded ? "收" : "展", 14f, theme,
            TaiwuTextStyle.Heading, TextAlignmentOptions.Center);
        UiFactory.Stretch(state.rectTransform, Vector2.zero, Vector2.zero);

        TextMeshProUGUI label = UiFactory.Text(
            "Label", header, groupState.Label, 24f, theme,
            expanded ? TaiwuTextStyle.Heading : TaiwuTextStyle.Body,
            TextAlignmentOptions.MidlineLeft);
        UiFactory.Stretch(label.rectTransform, new Vector2(48f, 0f), new Vector2(-12f, 0f));
    }

    private static void BuildNavigationItem(
        RectTransform parent,
        NavigationNode node,
        NavigationSnapshot snapshot,
        int group,
        int itemIndex,
        TaiwuTheme theme)
    {
        NavigationItemSnapshot itemState = snapshot.Groups[group].Items[itemIndex];
        bool selected = itemState.Selected;
        bool interactable = itemState.Interactable;
        RectTransform item = UiFactory.Rect("Item_" + group + "_" + itemIndex, parent);
        UiFactory.Layout(item, -1f, node.Options.ItemHeight, flexibleWidth: 1f);
        CImage background = item.gameObject.AddComponent<CImage>();
        background.color = selected
            ? new Color(0.12f, 0.16f, 0.13f, 1f)
            : new Color(0.035f, 0.07f, 0.072f, 0.96f);
        CButton button = item.gameObject.AddComponent<CButton>();
        ConfigureFlatButton(button, background, interactable);
        button.onClick.AddListener(() => node.Projection.Dispatch(
            new SelectNavigationItemIntent(group, itemIndex)));
        TextMeshProUGUI label = UiFactory.Text(
            "Label", item, itemState.Label, 21f, theme,
            selected ? TaiwuTextStyle.Heading : TaiwuTextStyle.Muted,
            TextAlignmentOptions.MidlineLeft);
        UiFactory.Stretch(label.rectTransform, new Vector2(42f, 0f), new Vector2(-12f, 0f));
    }

    private static void ConfigureFlatButton(CButton button, CImage target, bool interactable)
    {
        button.targetGraphic = target;
        button.interactable = interactable;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = new ColorBlock
        {
            normalColor = Color.white,
            highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f),
            pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f),
            selectedColor = Color.white,
            disabledColor = new Color(0.48f, 0.48f, 0.48f, 0.72f),
            colorMultiplier = 1f,
            fadeDuration = 0.08f,
        };
    }

    private static void ClearChildren(RectTransform root)
    {
        for (int index = root.childCount - 1; index >= 0; index--)
        {
            GameObject child = root.GetChild(index).gameObject;
            child.SetActive(false);
            UnityEngine.Object.Destroy(child);
        }
    }
}
