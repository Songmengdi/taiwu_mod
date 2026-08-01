namespace LiverFriendlyInteractions.Frontend;

internal static class InteractionHubWorldMapFocusPolicy
{
    internal static bool IsSupportedContext(
        bool worldMapHasFocus,
        bool stateMainWorldHasFocus,
        bool mapBlockCharacterListHasFocus) =>
        worldMapHasFocus || stateMainWorldHasFocus || mapBlockCharacterListHasFocus;

    internal static bool ShouldReturnFromExternalUi(bool wasObservedActive, bool isActive) =>
        wasObservedActive && !isActive;

    internal static bool ShouldSearchForNativeEventWindow(
        bool hasCachedWindow,
        bool wasObservedActive) =>
        !hasCachedWindow && !wasObservedActive;

    internal static bool ShouldProbeNativeEventWindow(
        bool hasCachedWindow,
        bool wasObservedActive,
        float now,
        float nextProbeAt) =>
        ShouldSearchForNativeEventWindow(hasCachedWindow, wasObservedActive) && now >= nextProbeAt;

    internal static bool ShouldCheckWorldMapFallback(bool wasObservedActive) =>
        !wasObservedActive;

    internal static bool ShouldCheckWorldMapFallback(
        bool wasObservedActive,
        float secondsWaiting,
        float graceSeconds) =>
        ShouldCheckWorldMapFallback(wasObservedActive) && secondsWaiting >= graceSeconds;

    internal static bool ShouldHideHubForNativeEvent(bool nativeEventActive) =>
        nativeEventActive;

    internal static bool ShouldOpenFromShortcut(
        bool hasSupportedMapFocus,
        bool wasClosedByUser,
        bool hasActiveWorldMap) =>
        hasSupportedMapFocus || (wasClosedByUser && hasActiveWorldMap);
}
