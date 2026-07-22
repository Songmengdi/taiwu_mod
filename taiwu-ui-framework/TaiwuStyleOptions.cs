namespace TaiwuUi;

/// <summary>Overrides for a text node. Omit this object to use the native standard preset.</summary>
public sealed record TaiwuTextOptions
{
    public float FontSize { get; init; } = TaiwuUiMetrics.BodyFontSize;
    public float MinimumHeight { get; init; } = TaiwuUiMetrics.BodyTextHeight;
    public TaiwuTextStyle Style { get; init; } = TaiwuTextStyle.Body;
}

/// <summary>Overrides for a button node. Omit this object to use the native standard preset.</summary>
public sealed record TaiwuButtonOptions
{
    public float Width { get; init; } = TaiwuUiMetrics.ButtonWidth;
    public float Height { get; init; } = TaiwuUiMetrics.ButtonHeight;
    public float FontSize { get; init; } = TaiwuUiMetrics.ButtonFontSize;
    public TaiwuButtonStyle Style { get; init; } = TaiwuButtonStyle.Primary;
}

/// <summary>Overrides for a vertically scrolling content region.</summary>
public sealed record TaiwuScrollOptions
{
    public float Height { get; init; } = 440f;
    public float Spacing { get; init; } = 4f;
    public float Padding { get; init; } = 10f;
    public bool ShowScrollbar { get; init; } = true;
    public bool ShowBackground { get; init; } = true;
}

/// <summary>Overrides for a compact selector whose choices open above the window content.</summary>
public sealed record TaiwuPopupSelectOptions
{
    public float Width { get; init; } = 300f;
    public float Height { get; init; } = 52f;
    public float PopupWidth { get; init; } = 720f;
    public float PopupHeight { get; init; } = 250f;
}

/// <summary>Measurements for a trigger that opens a cascading selection card.</summary>
public sealed record TaiwuPopupCardOptions
{
    /// <summary>Optional card heading; the trigger can remain a concise value-only label.</summary>
    public string? Title { get; init; }
    /// <summary>How the compact trigger is drawn in the surrounding filter row.</summary>
    public TaiwuPopupCardTriggerStyle TriggerStyle { get; init; } = TaiwuPopupCardTriggerStyle.Button;
    public float Width { get; init; } = 440f;
    public float Height { get; init; } = 52f;
    /// <summary>Card width; choices wrap inside this width instead of opening a second popup.</summary>
    public float PopupWidth { get; init; } = 680f;
    /// <summary>Minimum card height.</summary>
    public float PopupHeight { get; init; } = 270f;
    /// <summary>Card content scrolls after reaching this height.</summary>
    public float MaximumPopupHeight { get; init; } = 640f;
    // Kept for source compatibility with the first popup-card preview. Choices now
    // render directly in the card, so consumers no longer need to set these values.
    public float OptionPopupWidth { get; init; } = 760f;
    public float OptionPopupHeight { get; init; } = 280f;
}

/// <summary>
/// Native measurements live behind the public builder interface so every renderer
/// and preset uses one source of truth without exposing Unity layout details.
/// </summary>
internal static class TaiwuUiMetrics
{
    internal const float BodyFontSize = 24f;
    internal const float HeadingFontSize = 28f;
    internal const float MutedFontSize = 22f;
    internal const float BodyTextHeight = 40f;
    internal const float HeadingTextHeight = 44f;
    internal const float MutedTextHeight = 36f;

    internal const float ButtonWidth = 280f;
    internal const float ButtonHeight = 62f;
    internal const float ButtonFontSize = 24f;

    internal const float TitleHeight = 72f;
    internal const float WindowTitleFontSize = 28f;
    internal const float EncyclopediaTitleFontSize = 54f;
    internal const float CloseButtonSize = 44f;
    internal const float EncyclopediaCloseButtonSize = 60f;
    internal const float WindowChromeInset = 10f;
    internal const float ContentHorizontalInset = 52f;
    internal const float ContentTopGap = 20f;
    internal const float ContentBottomInset = 50f;
    internal const float ContentSpacing = 2f;
    internal const float EncyclopediaHorizontalInset = 24f;
    internal const float EncyclopediaContentTopGap = 32f;
    internal const float EncyclopediaBottomInset = 28f;
}
