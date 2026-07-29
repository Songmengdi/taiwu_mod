using LiverFriendlyInteractions.Backend;
using LiverFriendlyInteractions.Frontend;

Assert("集市入口数量", expected: 14, MarketIntroPolicy.KnownIntroEventGuids.Count);
Assert("集市入口 GUID 无重复", expected: 14,
    MarketIntroPolicy.KnownIntroEventGuids.Distinct(StringComparer.Ordinal).Count());

foreach (string eventGuid in MarketIntroPolicy.KnownIntroEventGuids)
{
    Assert($"识别集市入口 {eventGuid}", expected: true,
        MarketIntroPolicy.ShouldStartFastForward(eventGuid, onlyOnce: true));
}

Assert("同一入口非首次触发", expected: false,
    MarketIntroPolicy.ShouldStartFastForward(
        MarketIntroPolicy.KnownIntroEventGuids[0],
        onlyOnce: false));
Assert("其他首次事件", expected: false,
    MarketIntroPolicy.ShouldStartFastForward(
        "00000000-0000-0000-0000-000000000000",
        onlyOnce: true));
Assert("活动中的单选说明", expected: true,
    MarketIntroPolicy.ShouldFastForwardDisplay(
        active: true,
        eventGuid: MarketIntroPolicy.KnownIntroEventGuids[0],
        visibleOptionCount: 1));
Assert("活动中的真实多选", expected: false,
    MarketIntroPolicy.ShouldFastForwardDisplay(
        active: true,
        eventGuid: "next-event",
        visibleOptionCount: 2));
Assert("非集市快进期间的单选事件", expected: false,
    MarketIntroPolicy.ShouldFastForwardDisplay(
        active: false,
        eventGuid: "other-event",
        visibleOptionCount: 1));

Console.WriteLine("护肝交互集市引导策略测试通过：21 项");

Assert("休息结果显示数据", expected: true,
    MeditationRestPolicy.ShouldSkipResultDisplay(MeditationRestPolicy.RestResultEventGuid, hasDisplayData: true));
Assert("休息结果清空通知", expected: false,
    MeditationRestPolicy.ShouldSkipResultDisplay(MeditationRestPolicy.RestResultEventGuid, hasDisplayData: false));
Assert("休息选择事件", expected: false,
    MeditationRestPolicy.ShouldSkipResultDisplay(MeditationRestPolicy.RestChoiceEventGuid, hasDisplayData: true));
Assert("其他事件", expected: false,
    MeditationRestPolicy.ShouldSkipResultDisplay("00000000-0000-0000-0000-000000000000", hasDisplayData: true));

Console.WriteLine("护肝交互打坐休息策略测试通过：4 项");

Assert("奇遇第一项快捷键", expected: true,
    AdventureNumberShortcutPolicy.ShouldHandleFirstOption(
        shortcutPressed: true,
        adventureHasFocus: true,
        textInputHasFocus: false,
        displayItemCount: 1));
Assert("未按快捷键", expected: false,
    AdventureNumberShortcutPolicy.ShouldHandleFirstOption(false, true, false, 1));
Assert("弹窗已取得焦点", expected: false,
    AdventureNumberShortcutPolicy.ShouldHandleFirstOption(true, false, false, 1));
Assert("正在编辑搜索框", expected: false,
    AdventureNumberShortcutPolicy.ShouldHandleFirstOption(true, true, true, 1));
Assert("交互列表为空", expected: false,
    AdventureNumberShortcutPolicy.ShouldHandleFirstOption(true, true, false, 0));

Console.WriteLine("护肝交互奇遇数字快捷键策略测试通过：5 项");

static void Assert<T>(string name, T expected, T actual)
    where T : IEquatable<T>
{
    if (!expected.Equals(actual))
    {
        throw new InvalidOperationException($"{name}: expected {expected}, actual {actual}");
    }
}
