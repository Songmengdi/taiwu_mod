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
Assert("活动中的单选说明", expected: 0,
    MarketIntroPolicy.FindFastForwardOptionIndex(
        active: true,
        eventGuid: MarketIntroPolicy.KnownIntroEventGuids[0],
        visibleOptionKeys: new[] { "Option_Only" }));
Assert("大型集市教程数量", expected: 7,
    MarketIntroPolicy.LargeMarketTutorialDismissOptionKeys.Count);
foreach ((string eventGuid, IReadOnlyList<string> dismissKeys) in
         MarketIntroPolicy.LargeMarketTutorialDismissOptionKeys)
{
    Assert($"识别大型集市教程跳过项 {eventGuid}", expected: 1,
        MarketIntroPolicy.FindFastForwardOptionIndex(
            active: true,
            eventGuid: eventGuid,
            visibleOptionKeys: new[] { "Option_Help", dismissKeys[0], "Option_MoreHelp" }));
}
Assert("大型集市教程延迟发布后仍略过", expected: 1,
    MarketIntroPolicy.FindFastForwardOptionIndex(
        active: false,
        eventGuid: "23df3e52-6d7b-404c-b5a9-1d39c16bdb34",
        visibleOptionKeys: new[]
        {
            "Option_Help",
            "Option_1733734559",
            "Option_MoreHelp",
        }));
Assert("服牛帮跳过项无需位于第一项", expected: 4,
    MarketIntroPolicy.FindFastForwardOptionIndex(
        active: true,
        eventGuid: "99f17c6d-12f5-4d42-8254-bc5e4cfcfb78",
        visibleOptionKeys: new[]
        {
            "Option_1322292162",
            "Option_-1540531955",
            "Option_883977983",
            "Option_-1935442782",
            "Option_-1334330028",
        }));
Assert("大型集市真实多选", expected: -1,
    MarketIntroPolicy.FindFastForwardOptionIndex(
        active: true,
        eventGuid: "next-event",
        visibleOptionKeys: new[] { "Option_A", "Option_B" }));
Assert("教程页缺少预期跳过项", expected: -1,
    MarketIntroPolicy.FindFastForwardOptionIndex(
        active: true,
        eventGuid: "23df3e52-6d7b-404c-b5a9-1d39c16bdb34",
        visibleOptionKeys: new[] { "Option_HelpA", "Option_HelpB" }));
Assert("非集市快进期间的单选事件", expected: -1,
    MarketIntroPolicy.FindFastForwardOptionIndex(
        active: false,
        eventGuid: "other-event",
        visibleOptionKeys: new[] { "Option_Only" }));

Console.WriteLine("护肝交互集市引导策略测试通过：32 项");

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

Assert("大地图当前地格奇遇快捷键", expected: true,
    AdventureNumberShortcutPolicy.ShouldHandleWorldMapAdventure(
        shortcutPressed: true,
        worldMapHasFocus: true,
        textInputHasFocus: false,
        inAdventure: false,
        currentBlockHasAdventureIcon: true));
Assert("大地图人物不视为奇遇图标", expected: false,
    AdventureNumberShortcutPolicy.ShouldHandleWorldMapAdventure(true, true, false, false, false));
Assert("奇遇内部不触发大地图入口", expected: false,
    AdventureNumberShortcutPolicy.ShouldHandleWorldMapAdventure(true, true, false, true, true));
Assert("大地图不是顶层", expected: false,
    AdventureNumberShortcutPolicy.ShouldHandleWorldMapAdventure(true, false, false, false, true));
Assert("大地图搜索框正在输入", expected: false,
    AdventureNumberShortcutPolicy.ShouldHandleWorldMapAdventure(true, true, true, false, true));

Console.WriteLine("护肝交互大地图奇遇快捷键策略测试通过：5 项");

Assert("公输坊大型奇遇抵达商人格不强制交互", expected: true,
    LargeMarketMerchantArrivalPolicy.ShouldSuppressForcedInteraction(
        adventureCoreId: LargeMarketMerchantArrivalPolicy.GongshufangLargeAdventureCoreId,
        characterId: 12212,
        isTaiwuArrivedElement: true));
Assert("公输坊大型奇遇手动交互保留", expected: false,
    LargeMarketMerchantArrivalPolicy.ShouldSuppressForcedInteraction(
        adventureCoreId: LargeMarketMerchantArrivalPolicy.GongshufangLargeAdventureCoreId,
        characterId: 12212,
        isTaiwuArrivedElement: false));
Assert("公输坊大型奇遇非人物元素不拦截", expected: false,
    LargeMarketMerchantArrivalPolicy.ShouldSuppressForcedInteraction(
        adventureCoreId: LargeMarketMerchantArrivalPolicy.GongshufangLargeAdventureCoreId,
        characterId: -1,
        isTaiwuArrivedElement: true));
Assert("其他奇遇人物抵达事件不拦截", expected: false,
    LargeMarketMerchantArrivalPolicy.ShouldSuppressForcedInteraction(
        adventureCoreId: 1,
        characterId: 12212,
        isTaiwuArrivedElement: true));

Console.WriteLine("护肝交互公输坊商人抵达策略测试通过：4 项");

static void Assert<T>(string name, T expected, T actual)
    where T : IEquatable<T>
{
    if (!expected.Equals(actual))
    {
        throw new InvalidOperationException($"{name}: expected {expected}, actual {actual}");
    }
}
