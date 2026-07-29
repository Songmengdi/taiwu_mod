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

    internal static bool ShouldHandleWorldMapAdventure(
        bool shortcutPressed,
        bool worldMapHasFocus,
        bool textInputHasFocus,
        bool inAdventure,
        bool currentBlockHasAdventureIcon) =>
        shortcutPressed &&
        worldMapHasFocus &&
        !textInputHasFocus &&
        !inAdventure &&
        currentBlockHasAdventureIcon;
}
