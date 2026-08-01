namespace LiverFriendlyInteractions.Backend;

internal static class InteractionHubBeginPolicy
{
    internal static bool CanStart(bool returnSessionActive) => !returnSessionActive;
}
