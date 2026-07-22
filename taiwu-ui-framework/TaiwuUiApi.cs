namespace TaiwuUi;

public enum TaiwuWindowLayer
{
    Main,
    Part,
    Popup,
    Tips,
    VeryTop,
}

public enum TaiwuWindowCover
{
    None,
    Dimmed,
    Full,
}

/// <summary>High-level native window presentation; Unity chrome and sizing stay internal.</summary>
public enum TaiwuWindowPresentation
{
    Dialog,
    Encyclopedia,
}

public enum TaiwuButtonStyle
{
    Primary,
    Secondary,
}

/// <summary>Visual treatment for a trigger that opens a popup card.</summary>
public enum TaiwuPopupCardTriggerStyle
{
    Button,
    Underline,
    /// <summary>Compact native filter-option button, matching controls such as “门派”.</summary>
    FilterOption,
}

public enum TaiwuTextStyle
{
    Body,
    Heading,
    Muted,
}

/// <summary>Semantic accent for an inline choice; renderers keep the native chrome.</summary>
public enum TaiwuChoiceTone
{
    Neutral,
    Complete,
    Incomplete,
    Lost,
}

public interface ITaiwuWindow : IDisposable
{
    string Key { get; }
    bool IsShowing { get; }
    void Show();
    void Hide();
    void Toggle();
    void Render(UiWindow window);
}

public static class TaiwuUiApi
{
    public const int ApiMajor = 2;
    public const string ApiVersion = "2.0.0";

    public static bool IsReady => UIManager.Instance != null;

    public static UiValidationResult Validate(UiWindow window) => UiRenderPlanCompiler.Validate(window);

    public static UiUpdatePreview PreviewUpdate(UiWindow current, UiWindow next) =>
        UiReconciler.Preview(current, next);

    public static ITaiwuWindow Mount(UiWindow window) => TaiwuUiRuntime.Mount(window);

}
