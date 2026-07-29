using System;
using System.Collections.Generic;

namespace LiverFriendlyInteractions.Backend;

internal static class MarketIntroPolicy
{
    // All current one-time arrival introductions for the seven small
    // assemblies and the seven large Spring Market merchant areas.
    public static IReadOnlyList<string> KnownIntroEventGuids { get; } = new[]
    {
        // Small assemblies.
        "672f3006-d15f-4dba-83ba-3158f3a83420", // 文山书海阁
        "1982c173-b358-49c9-96e4-6b30af2bba85", // 公输坊
        "1025ce91-26cc-4a29-85c5-868d17e2805f", // 回春堂
        "c8342a33-ef2f-4e28-9533-72e9893b80b4", // 奇货斋
        "e90093b3-097f-4f7a-86d1-440d20659030", // 五湖商会
        "e98688eb-cebd-40c4-abad-1815bb58eae8", // 服牛帮
        "fe791cd0-fff9-425f-a7d2-f9b6a27bbdbf", // 大武魁

        // Large Spring Market merchant areas.
        "ad9fb49e-5d70-40c0-af96-f50fa5900ba9", // 文山书海阁
        "90f875ea-4381-415a-993e-1da833dd0e91", // 公输坊
        "62f6dc2a-5397-4fa8-92a5-3425421fea4d", // 回春堂
        "838b7122-58c6-4dae-b5bf-cac3353a3cba", // 奇货斋
        "63b40a7c-265e-4cc3-b599-a2c8b3c52ccc", // 五湖商会
        "5086a82c-4e4e-451e-b84a-0caaee02f005", // 服牛帮
        "dc74e456-1738-4870-970d-02fbb67ca951", // 大武魁
    };

    private static readonly HashSet<string> KnownIntroEventGuidSet =
        new(KnownIntroEventGuids, StringComparer.Ordinal);

    public static bool ShouldStartFastForward(string? eventGuid, bool onlyOnce)
    {
        return onlyOnce &&
               eventGuid is not null &&
               KnownIntroEventGuidSet.Contains(eventGuid);
    }

    public static bool ShouldFastForwardDisplay(
        bool active,
        string? eventGuid,
        int visibleOptionCount)
    {
        return active &&
               eventGuid is not null &&
               visibleOptionCount == 1;
    }
}
