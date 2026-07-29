using System;
using GameData.Adventure;
using GameData.Domains.Adventure;
using GameData.Domains.TaiwuEvent;
using GameData.Domains.TaiwuEvent.DisplayEvent;
using GameData.Utilities;
using HarmonyLib;

namespace LiverFriendlyInteractions.Backend;

internal static class MarketIntroFastForward
{
    private static int _depth;
    private static int _skippedDisplays;

    public static bool Active => _depth > 0;

    public static void Begin()
    {
        if (_depth++ == 0)
        {
            _skippedDisplays = 0;
        }
    }

    public static void RecordSkippedDisplay() => _skippedDisplays++;

    public static int End()
    {
        if (_depth <= 0)
        {
            return 0;
        }

        _depth--;
        if (_depth > 0)
        {
            return 0;
        }

        int result = _skippedDisplays;
        _skippedDisplays = 0;
        return result;
    }
}

[HarmonyPatch]
internal static class MarketIntroTriggerPatch
{
    [HarmonyPatch(
        typeof(AdventureDomain),
        "TriggerEvent",
        new[] { typeof(int), typeof(AdventureEventData), typeof(EventArgBox) })]
    [HarmonyPrefix]
    private static void BeforeTrigger(AdventureEventData eventData)
    {
        if (MarketIntroPolicy.ShouldStartFastForward(eventData.Guid, eventData.OnlyOnce))
        {
            MarketIntroFastForward.Begin();
        }
    }

    [HarmonyPatch(
        typeof(AdventureDomain),
        "TriggerEvent",
        new[] { typeof(int), typeof(AdventureEventData), typeof(EventArgBox) })]
    [HarmonyFinalizer]
    private static Exception? FinishTrigger(Exception? __exception, AdventureEventData eventData)
    {
        if (!MarketIntroPolicy.ShouldStartFastForward(eventData.Guid, eventData.OnlyOnce))
        {
            return __exception;
        }

        int skippedDisplays = MarketIntroFastForward.End();
        if (__exception is null && skippedDisplays > 0)
        {
            AdaptableLog.Info($"[护肝交互] 已略过集市首次到达的 {skippedDisplays} 层强制单选说明。");
        }

        return __exception;
    }
}

internal static class MarketIntroDisplayInterceptor
{
    public static bool TryFastForward(
        TaiwuEventDomain domain,
        TaiwuEventDisplayData? displayData)
    {
        if (displayData?.EventOptionInfos is not { } optionInfos)
        {
            return false;
        }

        int optionIndex = MarketIntroPolicy.FindFastForwardOptionIndex(
            MarketIntroFastForward.Active,
            displayData.EventGuid,
            optionInfos.Select(option => option.OptionKey).ToArray());
        if (optionIndex < 0)
        {
            return false;
        }

        string optionKey = optionInfos[optionIndex].OptionKey;
        MarketIntroFastForward.RecordSkippedDisplay();
        domain.EventSelect(displayData.EventGuid, optionKey);
        return true;
    }
}
