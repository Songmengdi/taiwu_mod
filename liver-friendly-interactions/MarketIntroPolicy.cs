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

    // The large Spring Market introduction is a tutorial menu rather than a
    // meaningful choice. Most merchants put the dismiss option first, but
    // Funiu Bang puts it last and several merchants have two mutually
    // exclusive dismiss options. Match by event and option key instead of by
    // position or localized text.
    public static IReadOnlyDictionary<string, IReadOnlyList<string>>
        LargeMarketTutorialDismissOptionKeys { get; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["35fe50f2-b98c-4b8f-8763-8523492593a2"] =
                new[] { "Option_373688144" }, // 文山书海阁
            ["38bc8cf4-e653-44b2-b793-a42e1b05d597"] =
                new[] { "Option_1451118799", "Option_-1229110468" }, // 大武魁
            ["28f41b60-4b35-4b21-a743-8e4bf0733128"] =
                new[] { "Option_817779741", "Option_-1398056254" }, // 奇货斋
            ["6d762bd5-ad84-4d9d-a606-e8746840603c"] =
                new[] { "Option_1900367150", "Option_752079466" }, // 公输坊
            ["eca473b1-4b53-4401-96a5-9bfe3cf26b77"] =
                new[] { "Option_241669961" }, // 五湖商会
            ["99f17c6d-12f5-4d42-8254-bc5e4cfcfb78"] =
                new[] { "Option_-1334330028" }, // 服牛帮
            ["23df3e52-6d7b-404c-b5a9-1d39c16bdb34"] =
                new[] { "Option_1733734559", "Option_1388688129" }, // 回春堂
        };

    public static bool ShouldStartFastForward(string? eventGuid, bool onlyOnce)
    {
        return onlyOnce &&
               eventGuid is not null &&
               KnownIntroEventGuidSet.Contains(eventGuid);
    }

    public static int FindFastForwardOptionIndex(
        bool active,
        string? eventGuid,
        IReadOnlyList<string> visibleOptionKeys)
    {
        if (eventGuid is null || visibleOptionKeys.Count == 0)
        {
            return -1;
        }

        if (LargeMarketTutorialDismissOptionKeys.TryGetValue(
                eventGuid,
                out IReadOnlyList<string>? dismissOptionKeys))
        {
            for (int index = 0; index < visibleOptionKeys.Count; index++)
            {
                if (dismissOptionKeys.Contains(
                        visibleOptionKeys[index],
                        StringComparer.Ordinal))
                {
                    return index;
                }
            }
        }

        // Ordinary one-option layers are safe to skip only while handling a
        // confirmed onlyOnce market entrance. Large-market tutorial menus are
        // queued and published after that call scope has ended, so their exact
        // event/option whitelist above intentionally does not require active.
        if (!active)
        {
            return -1;
        }

        return visibleOptionKeys.Count == 1 ? 0 : -1;
    }
}
