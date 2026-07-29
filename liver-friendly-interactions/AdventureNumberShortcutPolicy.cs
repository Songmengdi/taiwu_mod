namespace LiverFriendlyInteractions.Frontend;

internal static class AdventureNumberShortcutPolicy
{
    internal static bool ShouldHandleFirstOption(
        bool shortcutPressed,
        bool adventureHasFocus,
        bool textInputHasFocus,
        int displayItemCount) =>
        shortcutPressed &&
        adventureHasFocus &&
        !textInputHasFocus &&
        displayItemCount > 0;
}
