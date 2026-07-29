using System;

namespace LiverFriendlyInteractions.Backend;

internal static class MeditationRestPolicy
{
    public const string RestChoiceEventGuid = "9c82725d-764e-4103-b3b3-d529c7570ee4";
    public const string RestResultEventGuid = "1f1b5f7b-cc6a-4fdb-a802-22f9334455c0";
    public const string RestResultOptionKey = "Option_-1089598311";

    public static bool ShouldSkipResultDisplay(string? eventGuid, bool hasDisplayData)
    {
        return hasDisplayData &&
               string.Equals(eventGuid, RestResultEventGuid, StringComparison.Ordinal);
    }
}
