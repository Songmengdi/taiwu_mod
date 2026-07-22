using FrameWork.UISystem.UIElements;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;

namespace TaiwuUi;

internal static class FrameworkComponentRenderer
{
    internal static bool TryRender(
        Transform parent,
        RectTransform overlayRoot,
        UiNode node,
        TaiwuTheme theme)
    {
        switch (node)
        {
            case TabsNode tabs:
                FrameworkNavigationRenderer.RenderTabs(parent, tabs, theme);
                return true;
            case NavigationNode navigation:
                FrameworkNavigationRenderer.RenderNavigation(parent, navigation, theme);
                return true;
            case SearchInputNode search:
                SearchInputFamilyModule.Render(parent, search, theme);
                return true;
            case ToggleNode toggle:
                CheckboxFamilyModule.Render(parent, toggle, theme);
                return true;
            case NativeActionIconNode actionIcon:
                ActionIconFamilyModule.Render(parent, actionIcon, theme);
                return true;
            case ChoiceGroupNode choices:
                FilterFamilyModule.RenderChoices(parent, choices, theme);
                return true;
            case PopupSelectNode popupSelect:
                PopupSelectFamilyModule.Render(parent, overlayRoot, popupSelect, theme);
                return true;
            case PopupCardNode popupCard:
                PopupCardFamilyModule.Render(parent, overlayRoot, popupCard, theme);
                return true;
            case SliderNode slider:
                FilterFamilyModule.RenderSlider(parent, slider, theme);
                return true;
            case RangeSliderNode range:
                FilterFamilyModule.RenderRangeSlider(parent, range, theme);
                return true;
            case TableNode table:
                FrameworkTableRenderer.Render(parent, overlayRoot, table, theme);
                return true;
            default:
                return false;
        }
    }
}

internal static class PopupSelectFamilyModule
{
    internal static void Render(
        Transform parent,
        RectTransform overlayRoot,
        PopupSelectNode node,
        TaiwuTheme theme)
    {
        RectTransform root = UiFactory.Rect("PopupSelect", parent);
        UiFactory.Layout(root, node.Options.Width, node.Options.Height, flexibleWidth: 0f);
        CImage triggerImage = root.gameObject.AddComponent<CImage>();
        CButton trigger = root.gameObject.AddComponent<CButton>();
        trigger.targetGraphic = triggerImage;
        theme.ApplyButton(triggerImage, trigger, TaiwuButtonStyle.Secondary);
        TextMeshProUGUI triggerText = UiFactory.Text(
            "Label", root, string.Empty, 22f, theme, TaiwuTextStyle.Body,
            TextAlignmentOptions.Center);
        UiFactory.Stretch(triggerText.rectTransform, new Vector2(12f, 0f), new Vector2(-12f, 0f));

        RectTransform? blocker = null;
        RectTransform? popup = null;

        void ClosePopup()
        {
            if (popup != null)
                UnityEngine.Object.Destroy(popup.gameObject);
            if (blocker != null)
                UnityEngine.Object.Destroy(blocker.gameObject);
            popup = null;
            blocker = null;
        }

        void RefreshTrigger()
        {
            ChoiceSnapshot snapshot = node.Projection.Snapshot<ChoiceSnapshot>();
            ChoiceItemSnapshot? selected = snapshot.Items.FirstOrDefault(item => item.Selected);
            string value = selected?.Label ?? "请选择";
            triggerText.text = string.IsNullOrEmpty(node.Label)
                ? value + "  >"
                : node.Label + " · " + value + "  >";
            trigger.interactable = snapshot.Items.Any(item => item.Interactable);
        }

        void OpenPopup()
        {
            if (popup != null)
            {
                ClosePopup();
                return;
            }

            ChoiceSnapshot snapshot = node.Projection.Snapshot<ChoiceSnapshot>();
            float popupHeight = Math.Max(
                node.Options.PopupHeight,
                RequiredPopupHeight(snapshot, node.Options.PopupWidth));

            blocker = UiFactory.Rect("PopupSelectBlocker", overlayRoot);
            UiFactory.Stretch(blocker, Vector2.zero, Vector2.zero);
            CEmptyGraphic blockerGraphic = blocker.gameObject.AddComponent<CEmptyGraphic>();
            CButton blockerButton = blocker.gameObject.AddComponent<CButton>();
            blockerButton.targetGraphic = blockerGraphic;
            blockerButton.transition = Selectable.Transition.None;
            blockerButton.onClick.AddListener(ClosePopup);

            popup = UiFactory.Rect("PopupSelectPanel", overlayRoot);
            popup.anchorMin = popup.anchorMax = new Vector2(0.5f, 0.5f);
            popup.pivot = new Vector2(0f, 1f);
            popup.sizeDelta = new Vector2(node.Options.PopupWidth, popupHeight);
            CImage panel = popup.gameObject.AddComponent<CImage>();
            theme.ApplyPanel(panel);

            RectTransform title = UiFactory.Rect("Title", popup);
            title.anchorMin = new Vector2(0f, 1f);
            title.anchorMax = new Vector2(1f, 1f);
            title.pivot = new Vector2(0.5f, 1f);
            title.sizeDelta = new Vector2(-32f, 42f);
            title.anchoredPosition = new Vector2(0f, -14f);
            TextMeshProUGUI titleText = UiFactory.Text(
                "Text", title, node.Label, 24f, theme, TaiwuTextStyle.Heading,
                TextAlignmentOptions.MidlineLeft);
            UiFactory.Stretch(titleText.rectTransform, Vector2.zero, Vector2.zero);

            RectTransform flowRoot = UiFactory.Rect("Options", popup);
            UiFactory.Stretch(flowRoot, new Vector2(18f, 18f), new Vector2(-18f, -62f));
            var flow = flowRoot.gameObject.AddComponent<TaiwuFlowLayout>();
            flow.Spacing = new Vector2(6f, 7f);
            flow.ItemHeight = 46f;

            for (int index = 0; index < snapshot.Items.Count; index++)
            {
                int captured = index;
                ChoiceItemSnapshot item = snapshot.Items[index];
                RectTransform option = UiFactory.Rect("Option_" + index, flowRoot);
                float width = Math.Max(104f, item.Label.Length * 24f + 34f);
                UiFactory.Layout(option, width, flow.ItemHeight, flexibleWidth: 0f);
                CImage image = option.gameObject.AddComponent<CImage>();
                CButton button = option.gameObject.AddComponent<CButton>();
                button.targetGraphic = image;
                theme.ApplyFilterChoice(image, button, item.Selected);
                button.interactable = item.Interactable;
                TextMeshProUGUI label = UiFactory.Text(
                    "Label", option, item.Label, 22f, theme, TaiwuTextStyle.Body,
                    TextAlignmentOptions.Center);
                UiFactory.Stretch(label.rectTransform, new Vector2(8f, 0f), new Vector2(-8f, 0f));
                button.onClick.AddListener(() =>
                {
                    ClosePopup();
                    node.Projection.Dispatch(new SelectChoiceIntent(captured));
                });
            }

            popup.SetAsLastSibling();
            Canvas.ForceUpdateCanvases();
            PositionPopup(root, popup, overlayRoot);
        }

        trigger.onClick.AddListener(OpenPopup);
        node.Projection.Changed += RefreshTrigger;
        UiFactory.Lifetime(root).Add(() => node.Projection.Changed -= RefreshTrigger);
        UiFactory.Lifetime(root).Add(node.Projection.Dispose);
        UiFactory.Lifetime(root).Add(ClosePopup);
        RefreshTrigger();
    }

    private static float RequiredPopupHeight(ChoiceSnapshot snapshot, float popupWidth)
    {
        const float horizontalPadding = 36f;
        const float optionHeight = 46f;
        const float rowSpacing = 7f;
        const float chromeHeight = 80f;
        float available = Math.Max(1f, popupWidth - horizontalPadding);
        float x = 0f;
        int rows = 1;
        foreach (ChoiceItemSnapshot item in snapshot.Items)
        {
            float width = Math.Max(104f, item.Label.Length * 24f + 34f);
            if (x > 0f && x + width > available)
            {
                rows++;
                x = 0f;
            }
            x += width + 6f;
        }
        return chromeHeight + rows * optionHeight + Math.Max(0, rows - 1) * rowSpacing;
    }

    internal static void PositionPopup(
        RectTransform trigger,
        RectTransform popup,
        RectTransform overlayRoot)
    {
        var corners = new Vector3[4];
        trigger.GetWorldCorners(corners);
        Vector2 bottomLeft = overlayRoot.InverseTransformPoint(corners[0]);
        Vector2 topLeft = overlayRoot.InverseTransformPoint(corners[1]);
        Rect bounds = overlayRoot.rect;
        float x = Math.Clamp(bottomLeft.x, bounds.xMin + 12f, bounds.xMax - popup.rect.width - 12f);
        float belowTop = bottomLeft.y - 8f;
        float y = belowTop - popup.rect.height >= bounds.yMin + 12f
            ? belowTop
            : Math.Min(topLeft.y + popup.rect.height + 8f, bounds.yMax - 12f);
        popup.anchoredPosition = new Vector2(x, y);
    }
}

/// <summary>
/// Renders a compact trigger and a dependent-field card. This keeps related filters
/// out of the page flow while deliberately keeping every state transition local to
/// the open card (rather than remounting the hosting window after each step).
/// </summary>
internal static class PopupCardFamilyModule
{
    internal static void Render(
        Transform parent,
        RectTransform overlayRoot,
        PopupCardNode node,
        TaiwuTheme theme)
    {
        RectTransform root = UiFactory.Rect("PopupCard", parent);
        UiFactory.Layout(root, node.Options.Width, node.Options.Height, flexibleWidth: 0f);
        CImage triggerImage = root.gameObject.AddComponent<CImage>();
        CButton trigger = root.gameObject.AddComponent<CButton>();
        trigger.targetGraphic = triggerImage;
        bool useUnderlineTrigger = node.Options.TriggerStyle == TaiwuPopupCardTriggerStyle.Underline;
        bool useFilterOptionTrigger = node.Options.TriggerStyle == TaiwuPopupCardTriggerStyle.FilterOption;
        bool showDisclosure = !useUnderlineTrigger && !useFilterOptionTrigger;
        if (useUnderlineTrigger)
        {
            triggerImage.color = Color.clear;
            trigger.transition = Selectable.Transition.None;

            RectTransform underline = UiFactory.Rect("Underline", root);
            underline.anchorMin = new Vector2(0f, 0f);
            underline.anchorMax = new Vector2(1f, 0f);
            underline.pivot = new Vector2(0.5f, 0f);
            underline.sizeDelta = new Vector2(-4f, 2f);
            underline.anchoredPosition = new Vector2(0f, 2f);
            theme.ApplyTableHorizontalLine(underline.gameObject.AddComponent<CImage>());
        }
        else if (useFilterOptionTrigger)
        {
            theme.ApplyInlineFilterOption(triggerImage, trigger);
        }
        else
        {
            theme.ApplyButton(triggerImage, trigger, TaiwuButtonStyle.Secondary);
        }
        TextMeshProUGUI triggerText = UiFactory.Text(
            "Label", root, string.Empty, 22f, theme, TaiwuTextStyle.Body,
            useUnderlineTrigger ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center);
        UiFactory.Stretch(triggerText.rectTransform, new Vector2(12f, 0f), new Vector2(-12f, 0f));

        RectTransform? blocker = null;
        RectTransform? card = null;
        RectTransform? cardContent = null;

        void CloseCard()
        {
            if (card != null)
                UnityEngine.Object.Destroy(card.gameObject);
            if (blocker != null)
                UnityEngine.Object.Destroy(blocker.gameObject);
            card = null;
            blocker = null;
        }

        void RefreshTrigger()
        {
            string summary = node.Model.Summary;
            if (!showDisclosure)
            {
                triggerText.text = string.IsNullOrEmpty(node.Label)
                    ? summary
                    : node.Label + " · " + summary;
            }
            else
            {
                triggerText.text = string.IsNullOrEmpty(node.Label)
                    ? summary + "  >"
                    : node.Label + " · " + summary + "  >";
            }
            trigger.interactable = node.Model.Fields.Count > 0;
        }

        void BuildCard()
        {
            if (card == null || cardContent == null)
                return;
            for (int index = cardContent.childCount - 1; index >= 0; index--)
                UnityEngine.Object.Destroy(cardContent.GetChild(index).gameObject);

            IReadOnlyList<TaiwuPopupCardField> fields = node.Model.Fields;
            float desiredHeight = RequiredCardHeight(fields, node.Options.PopupWidth);
            float maximumHeight = Math.Max(node.Options.PopupHeight, node.Options.MaximumPopupHeight);
            float cardHeight = Math.Min(Math.Max(node.Options.PopupHeight, desiredHeight), maximumHeight);
            card.sizeDelta = new Vector2(node.Options.PopupWidth, cardHeight);

            for (int index = 0; index < fields.Count; index++)
            {
                TaiwuPopupCardField field = fields[index];
                RectTransform fieldRoot = UiFactory.Rect("Field_" + index, cardContent);
                var vertical = fieldRoot.gameObject.AddComponent<VerticalLayoutGroup>();
                vertical.spacing = 4f;
                vertical.childAlignment = TextAnchor.UpperLeft;
                vertical.childControlWidth = true;
                vertical.childControlHeight = true;
                vertical.childForceExpandWidth = true;
                vertical.childForceExpandHeight = false;
                float fieldHeight = RequiredFieldHeight(field, node.Options.PopupWidth - 36f);
                UiFactory.Layout(fieldRoot, -1f, fieldHeight, flexibleWidth: 1f);

                TextMeshProUGUI fieldLabel = UiFactory.Text(
                    "Label", fieldRoot, field.Label, 22f, theme, TaiwuTextStyle.Body,
                    TextAlignmentOptions.MidlineLeft);
                LayoutElement labelLayout = UiFactory.Layout(
                    fieldLabel.rectTransform, -1f, 32f, flexibleWidth: 1f);
                labelLayout.minHeight = 32f;

                if (field.Options.Count == 0)
                {
                    TextMeshProUGUI hint = UiFactory.Text(
                        "Hint", fieldRoot, field.Value, 20f, theme, TaiwuTextStyle.Muted,
                        TextAlignmentOptions.MidlineLeft);
                    LayoutElement hintLayout = UiFactory.Layout(
                        hint.rectTransform, -1f, 38f, flexibleWidth: 1f);
                    hintLayout.minHeight = 38f;
                    continue;
                }

                RectTransform flowRoot = UiFactory.Rect("Options", fieldRoot);
                var flow = flowRoot.gameObject.AddComponent<TaiwuFlowLayout>();
                flow.Spacing = new Vector2(6f, 6f);
                flow.ItemHeight = 44f;
                float optionsHeight = RequiredOptionsHeight(
                    field.Options, node.Options.PopupWidth - 36f);
                LayoutElement flowLayout = UiFactory.Layout(
                    flowRoot, -1f, optionsHeight, flexibleWidth: 1f);
                flowLayout.minHeight = optionsHeight;

                for (int optionIndex = 0; optionIndex < field.Options.Count; optionIndex++)
                {
                    int captured = optionIndex;
                    TaiwuPopupCardOption option = field.Options[optionIndex];
                    RectTransform choice = UiFactory.Rect("Option_" + optionIndex, flowRoot);
                    float width = OptionWidth(option.Label);
                    UiFactory.Layout(choice, width, flow.ItemHeight, flexibleWidth: 0f);
                    CImage image = choice.gameObject.AddComponent<CImage>();
                    CButton button = choice.gameObject.AddComponent<CButton>();
                    button.targetGraphic = image;
                    theme.ApplyFilterChoice(image, button, option.Selected);
                    button.interactable = field.Interactable && option.Interactable;
                    TextMeshProUGUI label = UiFactory.Text(
                        "Label", choice, option.Label, 21f, theme, TaiwuTextStyle.Body,
                        TextAlignmentOptions.Center);
                    UiFactory.Stretch(label.rectTransform, new Vector2(8f, 0f), new Vector2(-8f, 0f));
                    button.onClick.AddListener(() =>
                    {
                        bool closeAfterSelection = field.CloseCardAfterSelect;
                        if (closeAfterSelection)
                            CloseCard();
                        field.OnSelect(captured);
                        if (!closeAfterSelection)
                            node.Model.Refresh();
                    });
                }
            }
            Canvas.ForceUpdateCanvases();
            PopupSelectFamilyModule.PositionPopup(root, card, overlayRoot);
        }

        void OpenCard()
        {
            if (card != null)
            {
                CloseCard();
                return;
            }
            blocker = UiFactory.Rect("PopupCardBlocker", overlayRoot);
            UiFactory.Stretch(blocker, Vector2.zero, Vector2.zero);
            CEmptyGraphic blockerGraphic = blocker.gameObject.AddComponent<CEmptyGraphic>();
            CButton blockerButton = blocker.gameObject.AddComponent<CButton>();
            blockerButton.targetGraphic = blockerGraphic;
            blockerButton.transition = Selectable.Transition.None;
            blockerButton.onClick.AddListener(CloseCard);

            card = UiFactory.Rect("PopupCardPanel", overlayRoot);
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0f, 1f);
            CImage panel = card.gameObject.AddComponent<CImage>();
            theme.ApplyPanel(panel);

            string title = node.Options.Title ?? node.Label;
            float titleHeight = string.IsNullOrEmpty(title) ? 0f : 50f;
            if (titleHeight > 0f)
            {
                RectTransform titleRoot = UiFactory.Rect("Title", card);
                titleRoot.anchorMin = new Vector2(0f, 1f);
                titleRoot.anchorMax = new Vector2(1f, 1f);
                titleRoot.pivot = new Vector2(0.5f, 1f);
                titleRoot.sizeDelta = new Vector2(-36f, titleHeight);
                titleRoot.anchoredPosition = new Vector2(0f, -12f);
                TextMeshProUGUI titleText = UiFactory.Text(
                    "Text", titleRoot, title, 26f, theme, TaiwuTextStyle.Heading,
                    TextAlignmentOptions.MidlineLeft);
                UiFactory.Stretch(titleText.rectTransform, Vector2.zero, Vector2.zero);
            }

            RectTransform viewport = UiFactory.Rect("Viewport", card);
            UiFactory.Stretch(viewport, new Vector2(18f, 16f), new Vector2(-18f, -(titleHeight + 12f)));
            viewport.gameObject.AddComponent<RectMask2D>();
            cardContent = UiFactory.Rect("Content", viewport);
            cardContent.anchorMin = new Vector2(0f, 1f);
            cardContent.anchorMax = new Vector2(1f, 1f);
            cardContent.pivot = new Vector2(0.5f, 1f);
            cardContent.sizeDelta = Vector2.zero;
            var contentLayout = cardContent.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 12f;
            contentLayout.childAlignment = TextAnchor.UpperLeft;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            cardContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;
            ScrollRect scroll = card.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = cardContent;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 52f;
            card.SetAsLastSibling();
            BuildCard();
        }

        void Refresh()
        {
            RefreshTrigger();
            BuildCard();
        }

        trigger.onClick.AddListener(OpenCard);
        node.Model.AdapterChanged += Refresh;
        UiFactory.Lifetime(root).Add(() => node.Model.AdapterChanged -= Refresh);
        UiFactory.Lifetime(root).Add(CloseCard);
        RefreshTrigger();
    }

    private static float RequiredCardHeight(
        IReadOnlyList<TaiwuPopupCardField> fields,
        float popupWidth)
    {
        const float fieldSpacing = 12f;
        const float chromeHeight = 82f;
        float available = Math.Max(1f, popupWidth - 36f);
        float total = chromeHeight;
        foreach (TaiwuPopupCardField field in fields)
            total += RequiredFieldHeight(field, available) + fieldSpacing;
        return total;
    }

    private static float RequiredFieldHeight(TaiwuPopupCardField field, float availableWidth) =>
        32f + 4f + (field.Options.Count == 0
            ? 38f
            : RequiredOptionsHeight(field.Options, availableWidth));

    private static float RequiredOptionsHeight(
        IReadOnlyList<TaiwuPopupCardOption> options,
        float availableWidth)
    {
        float available = Math.Max(1f, availableWidth);
        float x = 0f;
        int rows = 1;
        foreach (TaiwuPopupCardOption option in options)
        {
            float width = OptionWidth(option.Label);
            if (x > 0f && x + width > available)
            {
                rows++;
                x = 0f;
            }
            x += width + 6f;
        }
        return rows * 44f + Math.Max(0, rows - 1) * 6f;
    }

    private static float OptionWidth(string label) => Math.Max(96f, label.Length * 23f + 30f);
}

internal static class SearchInputFamilyModule
{
    internal static void Render(Transform parent, SearchInputNode node, TaiwuTheme theme)
    {
        RectTransform root = UiFactory.Rect("SearchInput", parent);
        UiFactory.Layout(root, node.Width, 56f, flexibleWidth: 0f);

        CEmptyGraphic hitArea = root.gameObject.AddComponent<CEmptyGraphic>();

        TMP_InputField input = root.gameObject.AddComponent<TMP_InputField>();
        input.targetGraphic = hitArea;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.interactable = node.Value.Interactable;

        RectTransform backgroundRect = UiFactory.Rect("Image", root);
        UiFactory.Stretch(backgroundRect, Vector2.zero, Vector2.zero);
        CImage background = backgroundRect.gameObject.AddComponent<CImage>();
        background.raycastTarget = false;

        RectTransform hoverRect = UiFactory.Rect("Hover", root);
        UiFactory.Stretch(hoverRect, Vector2.zero, Vector2.zero);
        CImage hoverImage = hoverRect.gameObject.AddComponent<CImage>();
        hoverImage.raycastTarget = false;
        hoverRect.gameObject.SetActive(false);

        RectTransform iconRect = UiFactory.Rect("Icon", root);
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.sizeDelta = new Vector2(50f, 50f);
        iconRect.anchoredPosition = new Vector2(4.4f, 0f);
        CImage icon = iconRect.gameObject.AddComponent<CImage>();
        icon.raycastTarget = false;

        RectTransform lineRect = UiFactory.Rect("Line", root);
        lineRect.anchorMin = lineRect.anchorMax = new Vector2(0f, 0.5f);
        lineRect.pivot = new Vector2(0f, 0.5f);
        lineRect.sizeDelta = new Vector2(2f, 42f);
        lineRect.anchoredPosition = new Vector2(55.6f, 0f);
        CImage line = lineRect.gameObject.AddComponent<CImage>();
        line.raycastTarget = false;

        theme.ApplySearchFrame(background, hoverImage, icon, line);

        RectTransform viewport = UiFactory.Rect("Text Area", root);
        UiFactory.Stretch(viewport, new Vector2(60f, 0f), new Vector2(-68f, 0f));
        viewport.gameObject.AddComponent<RectMask2D>();

        TextMeshProUGUI text = UiFactory.Text(
            "Text", viewport, node.Value.Value, 28f, theme, TaiwuTextStyle.Body,
            TextAlignmentOptions.MidlineLeft);
        UiFactory.Stretch(text.rectTransform, new Vector2(5f, 5f), new Vector2(-5f, -5f));
        theme.ApplySearchText(text);

        TextMeshProUGUI placeholder = UiFactory.Text(
            "Placeholder", viewport, node.Placeholder, 28f, theme, TaiwuTextStyle.Muted,
            TextAlignmentOptions.MidlineLeft);
        UiFactory.Stretch(placeholder.rectTransform, new Vector2(10f, 5f), new Vector2(-5f, -5f));
        theme.ApplySearchText(placeholder);

        input.textViewport = viewport;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.SetTextWithoutNotify(node.Value.Value ?? string.Empty);

        RectTransform clearRect = UiFactory.Rect("ButtonClearSearch", root);
        clearRect.anchorMin = clearRect.anchorMax = new Vector2(1f, 0.5f);
        clearRect.pivot = new Vector2(1f, 0.5f);
        clearRect.sizeDelta = new Vector2(64f, 48f);
        clearRect.anchoredPosition = new Vector2(-4f, 0f);
        CImage clearImage = clearRect.gameObject.AddComponent<CImage>();
        CButton clear = clearRect.gameObject.AddComponent<CButton>();
        clear.targetGraphic = clearImage;
        theme.ApplySearchClear(clearImage, clear);
        clear.interactable = node.Value.Interactable;
        clear.onClick.AddListener(() =>
        {
            input.SetTextWithoutNotify(string.Empty);
            node.Value.SetValue(string.Empty);
        });

        var hover = root.gameObject.AddComponent<TaiwuMenuHover>();
        hover.Enter = () => hoverRect.gameObject.SetActive(input.interactable);
        hover.Exit = () => hoverRect.gameObject.SetActive(false);

        bool syncing = false;
        input.onValueChanged.AddListener(value =>
        {
            if (!syncing)
                node.Value.SetValue(value);
        });
        Action<string> syncValue = value =>
        {
            syncing = true;
            input.SetTextWithoutNotify(value ?? string.Empty);
            syncing = false;
        };
        Action<bool> syncInteractable = value =>
        {
            input.interactable = value;
            clear.interactable = value;
            if (!value)
                hoverRect.gameObject.SetActive(false);
        };
        node.Value.AdapterValueChanged += syncValue;
        node.Value.AdapterInteractableChanged += syncInteractable;
        UiFactory.Lifetime(root).Add(() => node.Value.AdapterValueChanged -= syncValue);
        UiFactory.Lifetime(root).Add(() => node.Value.AdapterInteractableChanged -= syncInteractable);
    }

}

internal static class CheckboxFamilyModule
{
    internal static void Render(Transform parent, ToggleNode node, TaiwuTheme theme)
    {
        bool standalone = string.IsNullOrEmpty(node.Label);
        RectTransform root = UiFactory.Rect(
            standalone ? "CheckboxStandalone" : "CheckboxLabeled", parent);
        var rootLayout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
        rootLayout.spacing = 10f;
        rootLayout.padding = new RectOffset(14, standalone ? 14 : 0, 14, 14);
        rootLayout.childAlignment = TextAnchor.MiddleLeft;
        rootLayout.childControlWidth = false;
        rootLayout.childControlHeight = false;
        rootLayout.childForceExpandWidth = false;
        rootLayout.childForceExpandHeight = false;
        UiFactory.Layout(root, standalone ? 78f : -1f, 78f, flexibleWidth: standalone ? 0f : 1f);

        RectTransform bgRect = UiFactory.Rect("Background", root);
        bgRect.sizeDelta = new Vector2(50f, 50f);
        UiFactory.Layout(bgRect, 50f, 50f, flexibleWidth: 0f);
        CImage bgImage = bgRect.gameObject.AddComponent<CImage>();

        RectTransform hoverRect = UiFactory.Rect("Hover", bgRect);
        hoverRect.sizeDelta = new Vector2(48f, 48f);
        CImage hoverImage = hoverRect.gameObject.AddComponent<CImage>();
        hoverImage.raycastTarget = false;

        RectTransform checkRect = UiFactory.Rect("Checkmark", bgRect);
        UiFactory.Stretch(checkRect, Vector2.zero, Vector2.zero);
        CImage checkImage = checkRect.gameObject.AddComponent<CImage>();
        checkImage.raycastTarget = false;

        CToggle toggle = root.gameObject.AddComponent<CToggle>();
        toggle.targetGraphic = bgImage;
        toggle.transition = Selectable.Transition.SpriteSwap;
        toggle.isOn = node.Value.Value;

        if (!standalone)
        {
            TextMeshProUGUI label = UiFactory.Text(
                "Label", root, node.Label, 20f, theme, TaiwuTextStyle.Body,
                TextAlignmentOptions.MidlineLeft);
            UiFactory.Layout(label.rectTransform, 200f, 50f, flexibleWidth: 1f);
        }

        theme.ApplyCheckbox(bgImage, checkImage, hoverImage, toggle.isOn);
        hoverImage.gameObject.SetActive(false);
        var hover = root.gameObject.AddComponent<TaiwuMenuHover>();
        hover.Enter = () =>
        {
            if (toggle.interactable)
                hoverImage.gameObject.SetActive(true);
        };
        hover.Exit = () => hoverImage.gameObject.SetActive(false);

        toggle.onValueChanged.AddListener(value =>
        {
            theme.ApplyCheckboxState(bgImage, checkImage, value);
            node.Value.SetValue(value);
        });
        Action<bool> syncValue = v =>
        {
            if (toggle.isOn != v) toggle.isOn = v;
            theme.ApplyCheckboxState(bgImage, checkImage, v);
        };
        Action<bool> syncInteractable = v =>
        {
            toggle.interactable = v;
            if (!v) hoverImage.gameObject.SetActive(false);
        };
        node.Value.AdapterValueChanged += syncValue;
        node.Value.AdapterInteractableChanged += syncInteractable;
        UiFactory.Lifetime(root).Add(() => node.Value.AdapterValueChanged -= syncValue);
        UiFactory.Lifetime(root).Add(() => node.Value.AdapterInteractableChanged -= syncInteractable);
    }

}

internal static class ActionIconFamilyModule
{
    internal static void Render(
        Transform parent,
        NativeActionIconNode node,
        TaiwuTheme theme)
    {
        string name = node.Icon == NativeActionIcon.Reset
            ? "ResetIconButton"
            : "RefreshIconButton";
        RectTransform root = UiFactory.Rect(name, parent);
        root.sizeDelta = new Vector2(node.Size, node.Size);
        UiFactory.Layout(root, node.Size, node.Size, flexibleWidth: 0f);
        CImage image = root.gameObject.AddComponent<CImage>();
        CButton button = root.gameObject.AddComponent<CButton>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => node.OnClick());
        theme.ApplyResetButton(image, button);
    }

}

internal static class FilterFamilyModule
{
    internal static void RenderChoices(Transform parent, ChoiceGroupNode node, TaiwuTheme theme)
    {
        RectTransform root = UiFactory.Rect("ChoiceGroup", parent);
        var vertical = root.gameObject.AddComponent<VerticalLayoutGroup>();
        vertical.spacing = node.Compact ? 4f : 6f;
        vertical.childAlignment = TextAnchor.UpperLeft;
        vertical.childControlWidth = true;
        vertical.childControlHeight = true;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;
        var fitter = root.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        // Let the vertical group derive its height from the flow's actual wrapped rows.
        // A fixed preferred height here made rows beyond the first overlap the next choice group.
        UiFactory.Layout(root, -1f, -1f, flexibleWidth: 1f);

        if (!string.IsNullOrEmpty(node.Label))
        {
            TextMeshProUGUI title = UiFactory.Text(
                "Title", root, node.Label, 24f, theme, TaiwuTextStyle.Body,
                TextAlignmentOptions.MidlineLeft);
            UiFactory.Layout(title.rectTransform, -1f, 28f, flexibleWidth: 1f);
        }

        RectTransform flowRoot = UiFactory.Rect("Options", root);
        var flow = flowRoot.gameObject.AddComponent<TaiwuFlowLayout>();
        flow.Spacing = new Vector2(node.Compact ? 3f : 0f, 4f);
        flow.ItemHeight = node.Compact ? 40f : 52f;
        flow.padding = new RectOffset(0, 0, 0, 0);
        // TaiwuFlowLayout reports the correct height after measuring all wrapped options.
        UiFactory.Layout(flowRoot, -1f, -1f, flexibleWidth: 1f);

        var buttons = new List<(CImage Image, CButton Button)>();
        ChoiceSnapshot initial = node.Projection.Snapshot<ChoiceSnapshot>();
        for (int index = 0; index < initial.Items.Count; index++)
        {
            int captured = index;
            RectTransform option = UiFactory.Rect("Option_" + index, flowRoot);
            float width = Math.Max(
                node.Compact ? 68f : 114f,
                initial.Items[index].Label.Length * 24f + (node.Compact ? 20f : 42f));
            UiFactory.Layout(option, width, flow.ItemHeight, flexibleWidth: 0f);
            CImage image = option.gameObject.AddComponent<CImage>();
            CButton button = option.gameObject.AddComponent<CButton>();
            button.targetGraphic = image;
            if (initial.Items[index].Tone != TaiwuChoiceTone.Neutral)
            {
                RectTransform tone = UiFactory.Rect("Tone", option);
                tone.anchorMin = new Vector2(0f, 0f);
                tone.anchorMax = new Vector2(0f, 1f);
                tone.pivot = new Vector2(0f, 0.5f);
                tone.sizeDelta = new Vector2(5f, -8f);
                tone.anchoredPosition = new Vector2(4f, 0f);
                theme.ApplyChoiceTone(tone.gameObject.AddComponent<CImage>(), initial.Items[index].Tone);
            }
            TextMeshProUGUI text = UiFactory.Text(
                "Label", option, initial.Items[index].Label, 24f, theme,
                TaiwuTextStyle.Body, TextAlignmentOptions.Center);
            UiFactory.Stretch(text.rectTransform, new Vector2(8f, 0f), new Vector2(-8f, 0f));
            button.onClick.AddListener(() => node.Projection.Dispatch(new ToggleChoiceIntent(captured)));
            buttons.Add((image, button));
        }

        void Refresh()
        {
            ChoiceSnapshot snapshot = node.Projection.Snapshot<ChoiceSnapshot>();
            for (int index = 0; index < buttons.Count; index++)
                ApplyChoiceVisual(
                    buttons[index].Image,
                    buttons[index].Button,
                    snapshot.Items[index].Selected,
                    snapshot.Items[index].Interactable,
                    theme);
        }

        node.Projection.Changed += Refresh;
        UiFactory.Lifetime(root).Add(() => node.Projection.Changed -= Refresh);
        UiFactory.Lifetime(root).Add(node.Projection.Dispose);
        Refresh();
    }

    internal static void RenderSlider(Transform parent, SliderNode node, TaiwuTheme theme)
    {
        RectTransform root = CreateSliderRow(
            parent, node.Label, node.Value.Reset, theme,
            out Slider slider, out TextMeshProUGUI valueLabel);
        ConfigureSlider(slider, node.Minimum, node.Maximum, node.Value.Value);
        bool syncing = false;

        void Refresh(float value)
        {
            float normalized = Quantize(value, node.Minimum, node.Maximum, node.Step);
            syncing = true;
            slider.SetValueWithoutNotify(normalized);
            valueLabel.text = FormatNumber(normalized, node.Step);
            syncing = false;
        }

        slider.onValueChanged.AddListener(value =>
        {
            if (!syncing)
                node.Value.SetValue(Quantize(value, node.Minimum, node.Maximum, node.Step));
        });
        Action<float> syncValue = Refresh;
        Action<bool> syncInteractable = value => slider.interactable = value;
        node.Value.AdapterValueChanged += syncValue;
        node.Value.AdapterInteractableChanged += syncInteractable;
        UiFactory.Lifetime(root).Add(() => node.Value.AdapterValueChanged -= syncValue);
        UiFactory.Lifetime(root).Add(() => node.Value.AdapterInteractableChanged -= syncInteractable);
        slider.interactable = node.Value.Interactable;
        Refresh(node.Value.Value);
    }

    internal static void RenderRangeSlider(Transform parent, RangeSliderNode node, TaiwuTheme theme)
    {
        RectTransform root = UiFactory.Rect("RangeSlider", parent);
        UiFactory.Layout(root, -1f, 56f, flexibleWidth: 1f);
        var layout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        CreateFilterReset(root, node.Value.Reset, theme);
        TextMeshProUGUI title = UiFactory.Text(
            "Label", root, node.Label, 24f, theme, TaiwuTextStyle.Body,
            TextAlignmentOptions.MidlineLeft);
        UiFactory.Layout(title.rectTransform, 90f, 40f, flexibleWidth: 0f);

        RectTransform values = UiFactory.Rect("InputHolder", root);
        UiFactory.Layout(values, 136f, 30f, flexibleWidth: 0f);
        CImage valueBackground = values.gameObject.AddComponent<CImage>();
        theme.ApplySliderValueBackground(valueBackground);
        TextMeshProUGUI lowerLabel = UiFactory.Text(
            "LowerValue", values, string.Empty, 22f, theme, TaiwuTextStyle.Body,
            TextAlignmentOptions.Center);
        lowerLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
        lowerLabel.rectTransform.anchorMax = new Vector2(0.42f, 1f);
        lowerLabel.rectTransform.offsetMin = Vector2.zero;
        lowerLabel.rectTransform.offsetMax = Vector2.zero;
        TextMeshProUGUI dash = UiFactory.Text(
            "Separator", values, "-", 22f, theme, TaiwuTextStyle.Body,
            TextAlignmentOptions.Center);
        dash.rectTransform.anchorMin = new Vector2(0.42f, 0f);
        dash.rectTransform.anchorMax = new Vector2(0.58f, 1f);
        dash.rectTransform.offsetMin = Vector2.zero;
        dash.rectTransform.offsetMax = Vector2.zero;
        TextMeshProUGUI upperLabel = UiFactory.Text(
            "UpperValue", values, string.Empty, 22f, theme, TaiwuTextStyle.Body,
            TextAlignmentOptions.Center);
        upperLabel.rectTransform.anchorMin = new Vector2(0.58f, 0f);
        upperLabel.rectTransform.anchorMax = Vector2.one;
        upperLabel.rectTransform.offsetMin = Vector2.zero;
        upperLabel.rectTransform.offsetMax = Vector2.zero;

        RectTransform sliderRoot = UiFactory.Rect("SliderWithStyle", root);
        UiFactory.Layout(sliderRoot, 300f, 56f, flexibleWidth: 1f);
        RangeSlider slider = CreateRangeSlider(sliderRoot, theme);
        bool syncing = false;

        void Refresh(TaiwuRange range)
        {
            TaiwuRange value = range.Normalize(node.Minimum, node.Maximum);
            float low = Quantize(value.Lower, node.Minimum, node.Maximum, node.Step);
            float high = Quantize(value.Upper, node.Minimum, node.Maximum, node.Step);
            syncing = true;
            slider.SetValue(low, high, false);
            lowerLabel.text = FormatNumber(low, node.Step);
            upperLabel.text = FormatNumber(high, node.Step);
            syncing = false;
        }

        slider.SetRange(node.Minimum, node.Maximum, (low, high) =>
        {
            if (!syncing)
                node.Value.SetValue(new TaiwuRange(
                    Quantize(low, node.Minimum, node.Maximum, node.Step),
                    Quantize(high, node.Minimum, node.Maximum, node.Step)));
        });
        Action<TaiwuRange> syncValue = Refresh;
        Action<bool> syncInteractable = value => slider.enabled = value;
        node.Value.AdapterValueChanged += syncValue;
        node.Value.AdapterInteractableChanged += syncInteractable;
        UiFactory.Lifetime(root).Add(() => node.Value.AdapterValueChanged -= syncValue);
        UiFactory.Lifetime(root).Add(() => node.Value.AdapterInteractableChanged -= syncInteractable);
        syncInteractable(node.Value.Interactable);
        Refresh(node.Value.Value);
        root.gameObject.AddComponent<DeferredUiAction>()
            .Configure(() => Refresh(node.Value.Value));
    }

    private static RectTransform CreateSliderRow(
        Transform parent,
        string label,
        Action onReset,
        TaiwuTheme theme,
        out Slider slider,
        out TextMeshProUGUI valueLabel)
    {
        RectTransform root = UiFactory.Rect("Slider", parent);
        UiFactory.Layout(root, -1f, 56f, flexibleWidth: 1f);
        var layout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        CreateFilterReset(root, onReset, theme);

        TextMeshProUGUI title = UiFactory.Text(
            "Label", root, label, 24f, theme, TaiwuTextStyle.Body,
            TextAlignmentOptions.MidlineLeft);
        UiFactory.Layout(title.rectTransform, 90f, 40f, flexibleWidth: 0f);

        RectTransform valueRoot = UiFactory.Rect("InputField", root);
        UiFactory.Layout(valueRoot, 64f, 30f, flexibleWidth: 0f);
        CImage valueBackground = valueRoot.gameObject.AddComponent<CImage>();
        theme.ApplySliderValueBackground(valueBackground);

        valueLabel = UiFactory.Text(
            "Value", valueRoot, string.Empty, 22f, theme, TaiwuTextStyle.Body,
            TextAlignmentOptions.Center);
        UiFactory.Stretch(valueLabel.rectTransform, Vector2.zero, Vector2.zero);

        RectTransform sliderRoot = UiFactory.Rect("SliderWithStyle", root);
        UiFactory.Layout(sliderRoot, 300f, 56f, flexibleWidth: 1f);
        slider = CreateSlider(sliderRoot, theme);
        return root;
    }

    private static Slider CreateSlider(RectTransform root, TaiwuTheme theme)
    {
        CSlider slider = root.gameObject.AddComponent<CSlider>();

        RectTransform backgroundRoot = UiFactory.Rect("Background", root);
        backgroundRoot.anchorMin = new Vector2(0f, 0.5f);
        backgroundRoot.anchorMax = new Vector2(1f, 0.5f);
        backgroundRoot.sizeDelta = new Vector2(0f, 16f);
        CImage background = backgroundRoot.gameObject.AddComponent<CImage>();
        theme.ApplySliderTrack(background);

        RectTransform fillArea = UiFactory.Rect("FillArea", root);
        UiFactory.Stretch(fillArea, new Vector2(0f, 21f), new Vector2(0f, -21f));
        RectTransform fill = UiFactory.Rect("Fill", fillArea);
        UiFactory.Stretch(fill, Vector2.zero, Vector2.zero);
        CImage fillImage = fill.gameObject.AddComponent<CImage>();
        theme.ApplySliderFill(fillImage);

        RectTransform handleArea = UiFactory.Rect("HandleArea", root);
        UiFactory.Stretch(handleArea, Vector2.zero, Vector2.zero);
        RectTransform handle = UiFactory.Rect("Handle", handleArea);
        // Unity Slider stretches handleRect on the cross axis. Keep only the
        // horizontal size delta so the 56px container produces a true 56x56 handle.
        handle.sizeDelta = new Vector2(56f, 0f);
        CImage handleImage = handle.gameObject.AddComponent<CImage>();
        theme.ApplySliderHandle(handleImage);
        RectTransform iconRoot = UiFactory.Rect("Image", handle);
        iconRoot.sizeDelta = new Vector2(50f, 50f);
        CImage icon = iconRoot.gameObject.AddComponent<CImage>();
        icon.raycastTarget = false;
        theme.ApplySliderHandleIcon(icon);

        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        return slider;
    }

    private static RangeSlider CreateRangeSlider(RectTransform root, TaiwuTheme theme)
    {
        RangeSlider slider = root.gameObject.AddComponent<RangeSlider>();
        RectTransform background = UiFactory.Rect("Background", root);
        background.anchorMin = new Vector2(0f, 0.5f);
        background.anchorMax = new Vector2(1f, 0.5f);
        background.sizeDelta = new Vector2(0f, 16f);
        theme.ApplySliderTrack(background.gameObject.AddComponent<CImage>());

        RectTransform area = UiFactory.Rect("Handle Slide Area", root);
        UiFactory.Stretch(area, Vector2.zero, Vector2.zero);
        RectTransform middle = UiFactory.Rect("Image", area);
        middle.anchorMin = new Vector2(0f, 0.5f);
        middle.anchorMax = new Vector2(0f, 0.5f);
        middle.pivot = new Vector2(0f, 0.5f);
        middle.sizeDelta = new Vector2(0f, 14f);
        theme.ApplySliderFill(middle.gameObject.AddComponent<CImage>());
        RectTransform left = CreateRangeHandle(area, "Handle", theme);
        RectTransform right = CreateRangeHandle(area, "Handle_1", theme);

        SetPrivate(slider, "background", background);
        SetPrivate(slider, "leftThumb", left);
        SetPrivate(slider, "rightThumb", right);
        SetPrivate(slider, "middleHandle", middle);
        return slider;
    }

    private static RectTransform CreateRangeHandle(Transform parent, string name, TaiwuTheme theme)
    {
        RectTransform handle = UiFactory.Rect(name, parent);
        handle.sizeDelta = new Vector2(56f, 56f);
        theme.ApplySliderHandle(handle.gameObject.AddComponent<CImage>());
        RectTransform iconRoot = UiFactory.Rect("Image", handle);
        iconRoot.sizeDelta = new Vector2(50f, 50f);
        CImage icon = iconRoot.gameObject.AddComponent<CImage>();
        icon.raycastTarget = false;
        theme.ApplySliderHandleIcon(icon);
        return handle;
    }

    private static void SetPrivate(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(target, value);

    private static void CreateFilterReset(
        Transform parent,
        Action onReset,
        TaiwuTheme theme) =>
        CreateFilterReset(parent, onReset, theme, out _);

    private static void CreateFilterReset(
        Transform parent,
        Action onReset,
        TaiwuTheme theme,
        out CButton button)
    {
        RectTransform reset = UiFactory.Rect("ResetBtn", parent);
        UiFactory.Layout(reset, 44f, 44f, flexibleWidth: 0f);
        CImage image = reset.gameObject.AddComponent<CImage>();
        button = reset.gameObject.AddComponent<CButton>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => onReset());
        theme.ApplyFilterResetButton(image, button);
    }

    private static void ConfigureSlider(Slider slider, float minimum, float maximum, float value)
    {
        slider.minValue = minimum;
        slider.maxValue = maximum;
        slider.SetValueWithoutNotify(Math.Clamp(value, minimum, maximum));
    }

    private static float Quantize(float value, float minimum, float maximum, float step)
    {
        float clamped = Math.Clamp(value, minimum, maximum);
        return Math.Clamp(minimum + MathF.Round((clamped - minimum) / step) * step, minimum, maximum);
    }

    private static string FormatNumber(float value, float step) =>
        step >= 1f ? MathF.Round(value).ToString("0") : value.ToString("0.##");

    private static void ApplyChoiceVisual(
        CImage image,
        CButton button,
        bool selected,
        bool interactable,
        TaiwuTheme theme)
    {
        theme.ApplyFilterChoice(image, button, selected);
        button.interactable = interactable;
        image.color = interactable ? Color.white : new Color(0.55f, 0.55f, 0.55f, 0.8f);
    }
}

internal sealed class ComponentLifetime : MonoBehaviour
{
    private readonly List<Action> _dispose = new();
    internal void Add(Action action) => _dispose.Add(action);

    private void OnDestroy()
    {
        foreach (Action action in _dispose)
            action();
        _dispose.Clear();
    }
}

internal sealed class DeferredUiAction : MonoBehaviour
{
    private Action? _action;

    internal static void Run(GameObject target, Action action)
    {
        DeferredUiAction deferred = target.AddComponent<DeferredUiAction>();
        deferred._action = action;
    }

    internal void Configure(Action action) => _action = action;

    private System.Collections.IEnumerator Start()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        _action?.Invoke();
        _action = null;
        Destroy(this);
    }
}

internal static class UiFactory
{
    internal static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        return rect;
    }

    internal static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    internal static LayoutElement Layout(
        RectTransform rect,
        float width,
        float height,
        float flexibleWidth)
    {
        LayoutElement element = rect.gameObject.GetComponent<LayoutElement>()
            ?? rect.gameObject.AddComponent<LayoutElement>();
        if (width >= 0f)
            element.preferredWidth = width;
        if (height >= 0f)
            element.preferredHeight = height;
        element.flexibleWidth = flexibleWidth;
        element.flexibleHeight = 0f;
        return element;
    }

    internal static TextMeshProUGUI Text(
        string name,
        Transform parent,
        string value,
        float size,
        TaiwuTheme theme,
        TaiwuTextStyle style,
        TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.font = theme.Font;
        text.text = value;
        text.fontSize = size;
        text.color = theme.TextColor(style);
        text.alignment = alignment;
        text.raycastTarget = false;
        text.richText = true;
        return text;
    }

    internal static CButton Button(
        RectTransform rect,
        string label,
        TaiwuTheme theme,
        TaiwuButtonStyle style,
        float fontSize = 19f)
    {
        CImage image = rect.gameObject.GetComponent<CImage>() ?? rect.gameObject.AddComponent<CImage>();
        CButton button = rect.gameObject.GetComponent<CButton>() ?? rect.gameObject.AddComponent<CButton>();
        button.targetGraphic = image;
        theme.ApplyButton(image, button, style);
        TextMeshProUGUI text = Text(
            "Label", rect, label, fontSize, theme, TaiwuTextStyle.Body,
            TextAlignmentOptions.Center);
        Stretch(text.rectTransform, new Vector2(6f, 0f), new Vector2(-6f, 0f));
        return button;
    }

    internal static ComponentLifetime Lifetime(RectTransform root) =>
        root.gameObject.GetComponent<ComponentLifetime>()
        ?? root.gameObject.AddComponent<ComponentLifetime>();
}

internal sealed class TaiwuFlowLayout : LayoutGroup
{
    internal Vector2 Spacing { get; set; } = new(6f, 6f);
    internal float ItemHeight { get; set; } = 42f;
    private readonly List<(RectTransform Rect, float X, float Y, float Width)> _layout = new();
    private float _preferredHeight;

    public override void CalculateLayoutInputHorizontal()
    {
        base.CalculateLayoutInputHorizontal();
        BuildLayout();
        SetLayoutInputForAxis(padding.horizontal, -1f, -1f, 0);
    }

    public override void CalculateLayoutInputVertical()
    {
        BuildLayout();
        SetLayoutInputForAxis(_preferredHeight, _preferredHeight, _preferredHeight, 1);
    }

    public override void SetLayoutHorizontal()
    {
        BuildLayout();
        foreach (var item in _layout)
        {
            SetChildAlongAxis(item.Rect, 0, item.X, item.Width);
            SetChildAlongAxis(item.Rect, 1, item.Y, ItemHeight);
        }
    }

    public override void SetLayoutVertical() => SetLayoutHorizontal();

    private void BuildLayout()
    {
        _layout.Clear();
        float available = Math.Max(1f, rectTransform.rect.width - padding.horizontal);
        float x = padding.left;
        float y = padding.top;
        int rows = 1;
        foreach (RectTransform child in rectChildren)
        {
            float width = Math.Max(1f, LayoutUtility.GetPreferredWidth(child));
            if (x > padding.left && x + width > padding.left + available)
            {
                x = padding.left;
                y += ItemHeight + Spacing.y;
                rows++;
            }
            _layout.Add((child, x, y, width));
            x += width + Spacing.x;
        }
        _preferredHeight = padding.vertical + rows * ItemHeight + Math.Max(0, rows - 1) * Spacing.y;
    }
}
