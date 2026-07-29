using GameData.Domains.TaiwuEvent;
using GameData.Domains.TaiwuEvent.DisplayEvent;
using GameData.Utilities;

namespace LiverFriendlyInteractions.Backend;

internal static class MeditationRestPatch
{
    // Called from the SetDisplayingEventData boundary. At this point the
    // original UpdateEventDisplayData method has already run TryExecuteScript,
    // including the native energy-restoration logic, but the display data has
    // not yet been published to the frontend.
    public static bool TrySkipResultDisplay(
        TaiwuEventDomain domain,
        TaiwuEventDisplayData? displayData)
    {
        if (!MeditationRestPolicy.ShouldSkipResultDisplay(
                domain.ShowingEvent?.EventGuid,
                displayData is not null))
        {
            return false;
        }

        domain.EventSelect(
            MeditationRestPolicy.RestResultEventGuid,
            MeditationRestPolicy.RestResultOptionKey);
        AdaptableLog.Info("[护肝交互] 已在原生休息结算后、发送显示数据前略过结果确认。");
        return true;
    }
}
