using FrameWork.UISystem.UIElements;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TaiwuUi;

internal sealed class TaiwuTheme
{
    private static TaiwuTheme? _cached;
    private readonly Dictionary<string, Sprite> _sprites;
    private readonly Dictionary<string, Texture> _textures;

    internal TMP_FontAsset? Font { get; }
    internal Color BodyText { get; } = new(0.88f, 0.84f, 0.72f, 1f);
    internal Color HeadingText { get; } = new(0.96f, 0.85f, 0.59f, 1f);
    internal Color MutedText { get; } = new(0.62f, 0.64f, 0.58f, 1f);
    internal Color DividerColor { get; } = new(0.55f, 0.47f, 0.31f, 0.55f);

    private TaiwuTheme(
        TMP_FontAsset? font,
        Dictionary<string, Sprite> sprites,
        Dictionary<string, Texture> textures)
    {
        Font = font;
        _sprites = sprites;
        _textures = textures;
    }

    internal static TaiwuTheme Resolve() => _cached ??= Create();

    private static TaiwuTheme Create()
    {
        TMP_FontAsset[] fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        TMP_FontAsset? font = fonts.FirstOrDefault(item => item.name == "Font SDF GB2312")
            ?? Resources.FindObjectsOfTypeAll<TextMeshProUGUI>()
                .FirstOrDefault(text => text.gameObject.activeInHierarchy && text.font != null)?.font
            ?? fonts.FirstOrDefault();
        Dictionary<string, Sprite> sprites = Resources.FindObjectsOfTypeAll<Sprite>()
            .Where(sprite => sprite != null && !sprite.name.EndsWith("(Clone)", StringComparison.Ordinal))
            .GroupBy(sprite => sprite.name)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        Dictionary<string, Texture> textures = Resources.FindObjectsOfTypeAll<Texture>()
            .Where(texture => texture != null && !string.IsNullOrEmpty(texture.name))
            .GroupBy(texture => texture.name)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        return new TaiwuTheme(font, sprites, textures);
    }

    internal void ApplyEncyclopediaBackground(CRawImage image)
    {
        if (_textures.TryGetValue("ui9_back_background_0", out Texture texture))
        {
            image.texture = texture;
            image.color = Color.white;
        }
        else
        {
            image.color = new Color(0.025f, 0.065f, 0.07f, 1f);
        }
        image.raycastTarget = false;
    }

    internal void ApplyScrollBackground(CImage image)
    {
        Sprite? sprite = Find("ui9_back_base_9") ?? Find("ui9_back_list_2");
        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
        }
        else
        {
            image.color = new Color(0.035f, 0.075f, 0.078f, 0.92f);
        }
    }

    internal void ApplyVerticalScrollbar(CImage track, CImage handle)
    {
        track.sprite = Find("ui9_btn_scroll_base_1");
        handle.sprite = Find("ui9_btn_scroll_base_0");
        track.type = Image.Type.Sliced;
        handle.type = Image.Type.Sliced;
        track.color = track.sprite == null ? new Color(0.06f, 0.10f, 0.10f, 0.9f) : Color.white;
        handle.color = handle.sprite == null ? new Color(0.58f, 0.52f, 0.37f, 1f) : Color.white;
    }

    internal void ApplyTableHeader(CImage image, CButton button)
    {
        Sprite? sprite = Find("ui9_btn_table_head_0");
        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState
            {
                highlightedSprite = Find("ui9_btn_table_head_1"),
                pressedSprite = sprite,
                selectedSprite = sprite,
                disabledSprite = sprite,
            };
        }
        else
        {
            image.color = new Color(0.08f, 0.12f, 0.12f, 0.94f);
        }
    }

    internal void ApplyTableHeaderText(TextMeshProUGUI text)
    {
        text.color = new Color32(185, 182, 177, 255);
        text.fontStyle = FontStyles.Normal;
    }

    internal void ApplyTableRow(CImage image)
    {
        image.sprite = Find("ui9_back_item_list");
        image.type = Image.Type.Simple;
        image.color = image.sprite == null ? new Color(0.11f, 0.145f, 0.15f, 0.96f) : Color.white;
    }

    internal void ApplyTableHorizontalLine(CImage image)
    {
        image.sprite = Find("ui9_line_horizontal_1");
        image.type = Image.Type.Sliced;
        image.color = image.sprite == null ? DividerColor : Color.white;
        image.raycastTarget = false;
    }

    internal void ApplyTableVerticalLine(CImage image)
    {
        image.sprite = Find("ui9_line_vertical_1");
        image.type = Image.Type.Sliced;
        image.color = image.sprite == null ? DividerColor : Color.white;
        image.raycastTarget = false;
    }

    internal void ApplyTableSelected(CImage image)
    {
        image.sprite = Find("ui9_bg_common_selected");
        image.type = Image.Type.Sliced;
        image.color = image.sprite == null ? new Color(0.24f, 0.31f, 0.27f, 0.75f) : Color.white;
        image.raycastTarget = false;
    }

    internal void ApplyTableHover(CImage image)
    {
        image.sprite = Find("ui9_sp_btn_second_tap_1");
        image.type = Image.Type.Sliced;
        image.color = image.sprite == null ? new Color(1f, 1f, 1f, 0.08f) : Color.white;
        image.raycastTarget = false;
    }

    internal Color TableTextColor(TaiwuTextStyle style) => style switch
    {
        TaiwuTextStyle.Heading => HeadingText,
        TaiwuTextStyle.Muted => MutedText,
        _ => Color.white,
    };

    internal void ApplyTableSortArrow(CImage image)
    {
        image.sprite = Find("ui9_icon_arrow");
        image.type = Image.Type.Simple;
        image.color = image.sprite == null ? HeadingText : Color.white;
        image.raycastTarget = false;
    }

    internal void ApplySecondaryTabsBackground(CImage image)
    {
        image.sprite = Find("ui9_back_second_toggle_1");
        image.type = Image.Type.Sliced;
        image.color = image.sprite == null ? new Color(0.06f, 0.10f, 0.10f, 0.94f) : Color.white;
    }

    internal void ApplySecondaryTab(CImage image, CButton button, bool selected)
    {
        Sprite? normal = selected ? Find("ui9_btn_second_toggle_2") : null;
        image.sprite = normal;
        image.type = Image.Type.Sliced;
        image.color = normal == null ? Color.clear : Color.white;
        button.transition = Selectable.Transition.None;
    }

    internal void ApplySecondaryTabDivider(CImage image)
    {
        image.sprite = Find("ui9_btn_second_tap_2") ?? Find("ui9_line_vertical_1");
        image.type = Image.Type.Sliced;
        image.color = image.sprite == null ? DividerColor : Color.white;
    }

    internal void ApplyBottomTabsBackground(CImage image)
    {
        image.sprite = Find("ui9_back_lowerpopup_base_2");
        image.type = Image.Type.Simple;
        image.color = image.sprite == null ? new Color(0.08f, 0.14f, 0.17f, 0.96f) : Color.white;
    }

    internal void ApplyBottomTab(CImage image, CButton button, bool selected)
    {
        Sprite? selectedSprite = Find("ui9_btn_first_tap");
        image.sprite = selected ? selectedSprite : null;
        image.type = Image.Type.Simple;
        image.color = selected && selectedSprite != null ? Color.white : Color.clear;
        button.transition = Selectable.Transition.None;
    }

    internal void ApplyBottomTabHover(CImage image)
    {
        image.sprite = Find("ui9_sp_btn_second_tap_1");
        image.type = Image.Type.Sliced;
        image.color = image.sprite == null ? new Color(0.45f, 0.25f, 0.12f, 0.28f) : Color.white;
    }

    internal void ApplyBottomTabDivider(CImage image)
    {
        image.sprite = Find("ui9_line_progressbar") ?? Find("ui9_line_vertical_1");
        image.type = Image.Type.Simple;
        image.color = image.sprite == null ? DividerColor : Color.white;
    }

    // ── Panel background (matches ViewFindMapBlock style) ─────────

    internal void ApplyPanel(CImage image)
    {
        // Current game version uses ui9_back_popup_3 for popup backgrounds
        Sprite? sprite = Find("ui9_back_popup_3")
            ?? Find("GUI_Window_Big_Black_NoColor")
            ?? Find("GUI_Window_Big");
        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
        }
        else
        {
            image.color = new Color(0.055f, 0.105f, 0.11f, 0.99f);
        }
    }

    internal void ApplyTitleBar(CImage image)
    {
        Sprite? sprite = Find("ui9_back_popup_title_3");
        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
        }
        else
        {
            image.color = new Color(0.08f, 0.14f, 0.15f, 0.98f);
        }
    }

    internal void ApplyListBackground(CImage image)
    {
        Sprite? sprite = Find("ui9_back_list_2");
        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
        }
        else
        {
            image.color = new Color(0.05f, 0.09f, 0.10f, 1f);
        }
    }

    // ── Buttons (matches native CButton + ButtonStyle look) ───────

    internal void ApplyButton(CImage image, CButton button, TaiwuButtonStyle style)
    {
        // Primary: ui9_btn_1_* set (warm-toned, consistent normal/hover/pressed)
        // Secondary: ui9_btn_2_* set (neutral-toned)
        if (style == TaiwuButtonStyle.Primary)
        {
            Sprite? normal = Find("ui9_btn_1_0") ?? Find("ui_sp_btn_1_0");
            Sprite? hover = Find("ui9_btn_1_1") ?? Find("ui_sp_btn_1_1");
            Sprite? pressed = Find("ui9_btn_1_3") ?? Find("ui_sp_btn_1_2");
            ApplyButtonSet(image, button, normal, hover, pressed);
        }
        else
        {
            Sprite? normal = Find("ui9_btn_2_0") ?? Find("ui_sp_btn_3_0");
            Sprite? hover = Find("ui9_btn_2_1") ?? Find("ui_sp_btn_3_1");
            Sprite? pressed = Find("ui9_btn_2_3") ?? Find("ui_sp_btn_3_2");
            ApplyButtonSet(image, button, normal, hover, pressed);
        }
    }

    private void ApplyButtonSet(CImage image, CButton button, Sprite? normal, Sprite? hover, Sprite? pressed)
    {
        if (normal != null)
        {
            image.sprite = normal;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState
            {
                highlightedSprite = hover,
                selectedSprite = hover,
                pressedSprite = pressed ?? hover,
                disabledSprite = pressed ?? normal,
            };
        }
        else
        {
            image.color = new Color(0.12f, 0.18f, 0.18f, 1f);
        }
    }

    internal void ApplyCloseButton(CImage image, CButton button)
    {
        Sprite? sprite = Find("ui9_btn_close_0");
        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.color = Color.white;
            button.transition = Selectable.Transition.None;
        }
    }

    internal void ApplyResetButton(CImage image, CButton button)
    {
        Sprite? normal = Find("ui9_btn_mapelement_refresh_0");
        if (normal != null)
        {
            image.sprite = normal;
            image.type = Image.Type.Simple;
            image.color = Color.white;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState
            {
                highlightedSprite = Find("ui9_btn_mapelement_refresh_1"),
                pressedSprite = Find("ui9_btn_mapelement_refresh_2"),
                selectedSprite = Find("ui9_btn_mapelement_refresh_1"),
                disabledSprite = Find("ui9_btn_mapelement_refresh_3"),
            };
        }
        else
        {
            ApplyButton(image, button, TaiwuButtonStyle.Secondary);
        }
    }

    private void SetButtonSprite(CImage image, CButton button, Sprite? normal, string fallback)
    {
        if (normal != null)
        {
            image.sprite = normal;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
            button.transition = Selectable.Transition.SpriteSwap;
            Sprite? highlighted = Find(fallback.Replace("0_4", "1_4"))
                ?? Find(fallback.Replace("0_0", "0_1"));
            Sprite? pressed = Find(fallback.Replace("0_4", "0_3"))
                ?? Find(fallback.Replace("0_0", "0_3"));
            if (highlighted != null || pressed != null)
            {
                button.spriteState = new SpriteState
                {
                    highlightedSprite = highlighted,
                    selectedSprite = highlighted ?? normal,
                    pressedSprite = pressed ?? normal,
                    disabledSprite = pressed ?? normal,
                };
            }
        }
        else
        {
            image.color = new Color(0.12f, 0.18f, 0.18f, 1f);
        }
    }

    // ── Toggle / Checkbox (matches CToggle + ToggleStyle) ─────────

    internal void ApplyCheckbox(
        CImage background,
        CImage checkmark,
        CImage hover,
        bool isOn)
    {
        Sprite? hoverSprite = Find("ui9_icon_switch_type_hover");
        if (hoverSprite != null)
        {
            hover.sprite = hoverSprite;
            hover.type = Image.Type.Simple;
            hover.color = Color.white;
        }
        ApplyCheckboxState(background, checkmark, isOn);
    }

    internal void ApplyCheckboxState(CImage background, CImage checkmark, bool isOn)
    {
        Sprite? backgroundSprite = Find("ui9_icon_switch_type_1_0");
        Sprite? checkSprite = Find("ui9_icon_switch_type_0_0");
        if (backgroundSprite != null)
        {
            background.sprite = backgroundSprite;
            background.type = Image.Type.Simple;
            background.color = Color.white;
        }
        if (checkSprite != null)
        {
            checkmark.sprite = checkSprite;
            checkmark.type = Image.Type.Simple;
            checkmark.color = isOn ? Color.white : new Color(1f, 1f, 1f, 0f);
        }
    }

    // ── Slider ────────────────────────────────────────────────────

    internal void ApplyFilterChoice(CImage image, CButton button, bool selected)
    {
        Sprite? normal = Find(selected ? "ui9_btn_tap_1_4" : "ui9_btn_three_0_0");
        image.sprite = normal;
        image.type = Image.Type.Sliced;
        image.color = normal == null ? new Color(0.08f, 0.13f, 0.13f, 1f) : Color.white;
        button.transition = Selectable.Transition.SpriteSwap;
        button.spriteState = new SpriteState
        {
            highlightedSprite = selected ? normal : Find("ui9_btn_three_0_1"),
            pressedSprite = selected ? normal : Find("ui9_btn_three_0_1"),
            selectedSprite = normal,
            disabledSprite = Find("ui9_btn_three_0_3"),
        };
    }

    internal void ApplySheetTabFrame(CImage image)
    {
        Sprite? frame = Find("ui9_btn_2_0") ?? Find("ui9_btn_three_0_0");
        image.sprite = frame;
        image.type = Image.Type.Sliced;
        image.color = frame == null ? new Color(0.48f, 0.40f, 0.25f, 0.95f) : Color.white;
        image.raycastTarget = false;
    }

    internal void ApplySheetTabChoice(CImage image, CButton button, bool selected)
    {
        Sprite? normal = Find(selected ? "ui9_btn_tap_1_4" : "ui9_btn_three_0_0");
        image.sprite = normal;
        image.type = Image.Type.Sliced;
        image.color = normal == null ? new Color(0.08f, 0.13f, 0.13f, 1f) : Color.white;
        button.transition = Selectable.Transition.SpriteSwap;
        button.spriteState = new SpriteState
        {
            highlightedSprite = selected ? normal : Find("ui9_btn_three_0_1"),
            pressedSprite = selected ? normal : Find("ui9_btn_three_0_1"),
            selectedSprite = normal,
            disabledSprite = Find("ui9_btn_three_0_3"),
        };
    }

    internal void ApplyChoiceTone(CImage image, TaiwuChoiceTone tone)
    {
        image.sprite = null;
        image.type = Image.Type.Simple;
        image.color = tone switch
        {
            TaiwuChoiceTone.Complete => new Color(0.88f, 0.69f, 0.32f, 1f),
            TaiwuChoiceTone.Incomplete => new Color(0.66f, 0.39f, 0.20f, 1f),
            TaiwuChoiceTone.Lost => new Color(0.43f, 0.48f, 0.47f, 1f),
            _ => Color.clear,
        };
        image.raycastTarget = false;
    }

    /// <summary>Translucent green plate marking a choice as already available.</summary>
    internal void ApplyChoiceHighlight(CImage image)
    {
        image.sprite = null;
        image.type = Image.Type.Simple;
        image.color = new Color(0.30f, 0.72f, 0.42f, 0.45f);
        image.raycastTarget = false;
    }

    /// <summary>
    /// Applies the native inline-filter chrome used by the game's compact options
    /// (for example, the “门派” button in the map-block filter). Unlike a toggle,
    /// this is a command button and intentionally has no checkmark layer.
    /// </summary>
    internal void ApplyInlineFilterOption(CImage image, CButton button)
    {
        Sprite? normal = Find("ui9_btn_filter_option_normal_0");
        Sprite? hover = Find("ui9_btn_filter_option_normal_1") ?? normal;
        image.sprite = normal;
        image.type = Image.Type.Sliced;
        image.color = normal == null ? new Color(0.08f, 0.13f, 0.13f, 1f) : Color.white;
        button.transition = Selectable.Transition.SpriteSwap;
        button.spriteState = new SpriteState
        {
            highlightedSprite = hover,
            pressedSprite = hover,
            selectedSprite = normal,
            disabledSprite = Find("ui9_btn_filter_option_normal_3") ?? normal,
        };
    }

    internal void ApplyFilterResetButton(CImage image, CButton button)
    {
        Sprite? normal = Find("ui9_btn_mapelement_lit_refresh_0");
        image.sprite = normal;
        image.type = Image.Type.Simple;
        image.color = normal == null ? Color.white : Color.white;
        button.transition = Selectable.Transition.SpriteSwap;
        button.spriteState = new SpriteState
        {
            highlightedSprite = Find("ui9_btn_mapelement_lit_refresh_1"),
            pressedSprite = Find("ui9_btn_mapelement_lit_refresh_2"),
            selectedSprite = Find("ui9_btn_mapelement_lit_refresh_1"),
            disabledSprite = Find("ui9_btn_mapelement_lit_refresh_3"),
        };
    }

    internal void ApplySliderValueBackground(CImage image)
    {
        image.sprite = Find("ui9_btn_base_0");
        image.type = Image.Type.Sliced;
        image.color = image.sprite == null ? new Color(0.06f, 0.10f, 0.10f, 1f) : Color.white;
    }

    internal void ApplySliderTrack(CImage image)
    {
        Sprite? sprite = Find("ui9_icon_slider_0");
        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
        }
        else
        {
            image.color = new Color(0.08f, 0.13f, 0.13f, 1f);
        }
    }

    internal void ApplySliderFill(CImage image)
    {
        image.sprite = Find("ui9_back_mapelement_blue_progress_0");
        image.type = Image.Type.Sliced;
        image.color = image.sprite == null ? new Color(0.27f, 0.62f, 0.78f, 1f) : Color.white;
    }

    internal void ApplySliderHandle(CImage image)
    {
        image.sprite = Find("ui9_btn_slider_0");
        image.type = Image.Type.Simple;
        image.color = image.sprite == null ? new Color(0.88f, 0.76f, 0.49f, 1f) : Color.white;
    }

    internal void ApplySliderHandleIcon(CImage image)
    {
        image.sprite = Find("ui9_btn_slider_icon");
        image.type = Image.Type.Simple;
        image.color = image.sprite == null ? Color.white : Color.white;
    }

    // ── SearchInput ───────────────────────────────────────────────

    internal void ApplySearchFrame(CImage background, CImage hover, CImage icon, CImage line)
    {
        background.sprite = Find("ui9_btn_three_1_0");
        background.type = Image.Type.Sliced;
        background.color = background.sprite == null
            ? new Color(0.055f, 0.095f, 0.095f, 0.98f)
            : Color.white;

        hover.sprite = Find("ui9_btn_three_1_1");
        hover.type = Image.Type.Sliced;
        hover.color = hover.sprite == null ? new Color(1f, 1f, 1f, 0.08f) : Color.white;

        icon.sprite = Find("ui9_btn_search_0");
        icon.type = Image.Type.Simple;
        icon.color = icon.sprite == null ? MutedText : Color.white;

        line.sprite = Find("ui9_btn_second_tap_line");
        line.type = Image.Type.Simple;
        line.color = line.sprite == null ? DividerColor : Color.white;
    }

    internal void ApplySearchText(TextMeshProUGUI text)
    {
        text.color = new Color32(185, 182, 177, 255);
        text.fontStyle = FontStyles.Normal;
        text.raycastTarget = true;
    }

    internal void ApplySearchClear(CImage image, CButton button)
    {
        image.sprite = Find("ui9_btn_encyclopedia_clear_search_0");
        image.type = Image.Type.Simple;
        image.color = image.sprite == null ? MutedText : Color.white;
        button.transition = Selectable.Transition.SpriteSwap;
        button.spriteState = new SpriteState
        {
            highlightedSprite = Find("ui9_btn_encyclopedia_clear_search_1"),
            disabledSprite = Find("ui9_btn_encyclopedia_clear_search_2"),
        };
    }

    // ── Divider ───────────────────────────────────────────────────

    internal void ApplyDivider(CImage image)
    {
        Sprite? sprite = Find("ui9_line_list_7");
        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
        }
        else
        {
            image.color = DividerColor;
        }
    }

    // ── Text ──────────────────────────────────────────────────────

    internal Color TextColor(TaiwuTextStyle style) => style switch
    {
        TaiwuTextStyle.Heading => HeadingText,
        TaiwuTextStyle.Muted => MutedText,
        _ => BodyText,
    };

    internal Sprite? ResolveSprite(string name) => Find(name);

    internal void ApplyNativeAsset(CImage image, NativeAssetRef asset)
    {
        image.sprite = Find(asset.Name);
        image.type = Image.Type.Simple;
        image.color = image.sprite == null ? Color.clear : Color.white;
    }

    internal void ApplyClosableTabsClear(CImage image)
    {
        image.sprite = Find("ui9_btn_encyclopedia_close_0");
        image.preserveAspect = true;
        image.color = image.sprite == null
            ? new Color(0.10f, 0.14f, 0.14f, 1f)
            : Color.white;
    }

    internal void ApplyClosableTabBackground(CImage image)
    {
        image.sprite = Find("ui9_btn_three_0_0");
        image.type = Image.Type.Sliced;
        image.color = image.sprite == null ? new Color(0.10f, 0.14f, 0.14f, 1f) : Color.white;
    }

    internal bool ApplyClosableTabSelected(CImage image)
    {
        image.sprite = Find("ui9_sp_btn_second_tap_1");
        image.type = Image.Type.Sliced;
        image.color = image.sprite == null ? Color.clear : Color.white;
        return image.sprite != null;
    }

    internal void ApplyClosableTabClose(CImage image)
    {
        image.sprite = Find("ui9_btn_encyclopedia_close_small_0");
        image.preserveAspect = true;
        image.color = image.sprite == null ? new Color(0.12f, 0.16f, 0.16f, 1f) : Color.white;
    }

    internal void ApplyNavigationGroupState(CImage image)
    {
        image.sprite = Find("ui9_btn_encyclopedia_expand_small_0");
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    internal Sprite? ResolveIcon(TaiwuIcon icon)
    {
        string? resource = icon.Key switch
        {
            "home" => "ui9_tab_encyclopedia_level_0_0",
            "journey" => "ui9_tab_encyclopedia_level_1_0",
            "world" => "ui9_tab_encyclopedia_level_2_0",
            "sect" => "ui9_tab_encyclopedia_level_3_0",
            "people" => "ui9_tab_encyclopedia_level_4_0",
            "interaction" => "ui9_tab_encyclopedia_level_5_0",
            "study" => "ui9_tab_encyclopedia_level_6_0",
            "combat" => "ui9_tab_encyclopedia_level_7_0",
            "industry" => "ui9_tab_encyclopedia_level_8_0",
            "items" => "ui9_tab_encyclopedia_level_9_0",
            "travel" => "ui9_tab_encyclopedia_level_10_0",
            "extensions" => "ui9_tab_encyclopedia_level_11_0",
            _ => null,
        };
        return resource == null ? null : Find(resource);
    }

    private Sprite? Find(string name) => _sprites.TryGetValue(name, out Sprite sprite) ? sprite : null;

    /// <summary>
    /// Atlas packs reuse some sprite names for unrelated artworks (for example
    /// ui9_btn_second_tap_2 is both a 2x8 divider line and the 88x52 highlighted
    /// tab background), and the name-only dictionary picks one arbitrarily.
    /// This overload scans every loaded sprite with the name and returns the
    /// widest one meeting the minimum width.
    /// </summary>
    private Sprite? Find(string name, float minimumWidth) =>
        Resources.FindObjectsOfTypeAll<Sprite>()
            .Where(sprite => sprite != null && sprite.name == name &&
                sprite.rect.width >= minimumWidth)
            .OrderByDescending(sprite => sprite.rect.width)
            .FirstOrDefault();
}
