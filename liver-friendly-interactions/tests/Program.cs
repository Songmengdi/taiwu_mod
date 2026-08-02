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

Assert("有主动入口的抵达事件不强制触发", expected: true,
    AdventureForcedInteractionPolicy.ShouldSuppressArrival(
        isTaiwuArrivedElement: true,
        hasManualInteractEvent: true));
Assert("主动交互事件保留", expected: false,
    AdventureForcedInteractionPolicy.ShouldSuppressArrival(
        isTaiwuArrivedElement: false,
        hasManualInteractEvent: true));
Assert("纯抵达剧情事件保留", expected: false,
    AdventureForcedInteractionPolicy.ShouldSuppressArrival(
        isTaiwuArrivedElement: true,
        hasManualInteractEvent: false));

Console.WriteLine("护肝交互奇遇强制交互策略测试通过：3 项");

var hubOptions = new[]
{
    new InteractionOptionView(57, "interaction:57", "浏览货物", true, 57, 0, 0),
    new InteractionOptionView(21, "interaction:21", "交换私人藏书", false, 21, 0, 0),
    new InteractionOptionView(6, "interaction:6", "赠送礼物", true, 6, 30, 0),
    new InteractionOptionView(8, "interaction:8", "较艺比试", true, 8, 30, 0),
};
string[] favorites = { "interaction:57", "interaction:21", "interaction:6" };
Assert("常用仅含可用项", "浏览货物,赠送礼物",
    string.Join(',', InteractionHubPolicy.Select(hubOptions, favorites, InteractionTab.Favorite)
        .Select(item => item.Name)));
Assert("常用遵循用户顺序", "赠送礼物,浏览货物",
    string.Join(',', InteractionHubPolicy.Select(hubOptions,
        new[] { "interaction:6", "interaction:57" }, InteractionTab.Favorite).Select(item => item.Name)));
Assert("其他排除常用", "较艺比试",
    string.Join(',', InteractionHubPolicy.Select(hubOptions, favorites, InteractionTab.Other)
        .Select(item => item.Name)));
Assert("不可用收纳失效常用", "交换私人藏书",
    string.Join(',', InteractionHubPolicy.Select(hubOptions, favorites, InteractionTab.Unavailable)
        .Select(item => item.Name)));
Assert("私人藏书显示名", "交换私人藏书", InteractionHubPolicy.DisplayName(21, "交换藏书"));
Assert("默认首项显示人物", InteractionHubPolicy.ShowCharacterKey,
    InteractionHubPolicy.DefaultFavorites[0]);

var meetOnlyOptions = new[]
{
    new InteractionOptionView(-1, InteractionHubPolicy.ShowCharacterKey,
        "显示人物", true, -2, 0, 0),
    new InteractionOptionView(-1, InteractionHubPolicy.MeetCharacterKey,
        "结识", true, -1, 0, 0),
};
Assert("常用仅有显示人物时自动切到结识", (byte)InteractionTab.Other,
    (byte)InteractionHubPolicy.ResolveVisibleTab(
        meetOnlyOptions,
        new[] { InteractionHubPolicy.ShowCharacterKey },
        InteractionTab.Favorite));
Assert("已经位于结识所在页时保持当前页", (byte)InteractionTab.Other,
    (byte)InteractionHubPolicy.ResolveVisibleTab(
        meetOnlyOptions,
        new[] { InteractionHubPolicy.ShowCharacterKey },
        InteractionTab.Other));
Assert("常用存在真正交互时保持常用", (byte)InteractionTab.Favorite,
    (byte)InteractionHubPolicy.ResolveVisibleTab(
        hubOptions,
        favorites,
        InteractionTab.Favorite));
var favoriteOnlyOptions = new[]
{
    new InteractionOptionView(-1, InteractionHubPolicy.ShowCharacterKey,
        "显示人物", true, -2, 0, 0),
    new InteractionOptionView(57, "interaction:57", "浏览货物", true, 57, 0, 0),
};
Assert("切换人物后其他页为空则回到有动作的常用页", (byte)InteractionTab.Favorite,
    (byte)InteractionHubPolicy.ResolveVisibleTab(
        favoriteOnlyOptions,
        new[] { InteractionHubPolicy.ShowCharacterKey, "interaction:57" },
        InteractionTab.Other));

Console.WriteLine("护肝交互统一人物交互排序策略测试通过：10 项");

Assert("江湖商会四品成年成员归入商人", expected: true,
    InteractionHubGroupingPolicy.IsBlockMerchant(28, 4, 24, 16));
Assert("商会婴儿不归入商人", expected: false,
    InteractionHubGroupingPolicy.IsBlockMerchant(28, 4, 3, 16));
Assert("普通组织四品不归入商人", expected: false,
    InteractionHubGroupingPolicy.IsBlockMerchant(20, 4, 24, 16));
Assert("商会非四品成员不归入商人", expected: false,
    InteractionHubGroupingPolicy.IsBlockMerchant(28, 3, 24, 16));
Assert("队伍列表排除太吾本人", expected: false,
    InteractionHubGroupingPolicy.ShouldIncludeTeammate(100, 100, 0));
Assert("队伍列表排除非存活人物", expected: false,
    InteractionHubGroupingPolicy.ShouldIncludeTeammate(101, 100, 1));
Assert("队伍列表保留存活同道", expected: true,
    InteractionHubGroupingPolicy.ShouldIncludeTeammate(101, 100, 0));

Console.WriteLine("护肝交互人物分组策略测试通过：7 项");

Assert("大地图人物栏仍属于大地图上下文", expected: true,
    InteractionHubWorldMapFocusPolicy.IsSupportedContext(
        worldMapHasFocus: false,
        stateMainWorldHasFocus: false,
        mapBlockCharacterListHasFocus: true));
Assert("非大地图界面不能打开交互中心", expected: false,
    InteractionHubWorldMapFocusPolicy.IsSupportedContext(
        worldMapHasFocus: false,
        stateMainWorldHasFocus: false,
        mapBlockCharacterListHasFocus: false));
Assert("原版人物页关闭后返回交互中心", expected: true,
    InteractionHubWorldMapFocusPolicy.ShouldReturnFromExternalUi(
        wasObservedActive: true, isActive: false));
Assert("原版人物页尚未显示时不能提前返回", expected: false,
    InteractionHubWorldMapFocusPolicy.ShouldReturnFromExternalUi(
        wasObservedActive: false, isActive: false));
Assert("原生事件窗口显示过且已关闭时返回交互中心", expected: true,
    InteractionHubWorldMapFocusPolicy.ShouldReturnFromExternalUi(
        wasObservedActive: true, isActive: false));
Assert("原生事件仍显示时不执行昂贵的返回环境探测", expected: false,
    InteractionHubWorldMapFocusPolicy.ShouldInspectNativeFlowCompletion(
        wasObservedActive: true,
        nativeEventActive: true,
        transitionSettled: false));
Assert("原生事件关闭且过渡完成后才探测返回环境", expected: true,
    InteractionHubWorldMapFocusPolicy.ShouldInspectNativeFlowCompletion(
        wasObservedActive: true,
        nativeEventActive: false,
        transitionSettled: true));
Assert("商店仍显示时不执行昂贵的大地图探测", expected: false,
    InteractionHubWorldMapFocusPolicy.ShouldInspectWorldMapAfterNativeFlow(
        shouldInspectCompletion: true,
        externalPopupActive: true));
Assert("原生外部窗口全部关闭后才探测大地图", expected: true,
    InteractionHubWorldMapFocusPolicy.ShouldInspectWorldMapAfterNativeFlow(
        shouldInspectCompletion: true,
        externalPopupActive: false));
Assert("原生事件转入商店时不能提前返回交互中心", expected: false,
    InteractionHubWorldMapFocusPolicy.ShouldReturnFromNativeFlow(
        wasObservedActive: true,
        nativeEventActive: false,
        transitionSettled: true,
        externalPopupActive: true,
        worldMapActive: true));
Assert("原生事件与后续窗口的切换间隙不能提前返回交互中心", expected: false,
    InteractionHubWorldMapFocusPolicy.ShouldReturnFromNativeFlow(
        wasObservedActive: true,
        nativeEventActive: false,
        transitionSettled: false,
        externalPopupActive: false,
        worldMapActive: true));
Assert("原生事件结束并回到大地图后返回交互中心", expected: true,
    InteractionHubWorldMapFocusPolicy.ShouldReturnFromNativeFlow(
        wasObservedActive: true,
        nativeEventActive: false,
        transitionSettled: true,
        externalPopupActive: false,
        worldMapActive: true));
Assert("交互中心主动关闭后快捷键可以重新打开", expected: true,
    InteractionHubWorldMapFocusPolicy.ShouldOpenFromShortcut(
        hasSupportedMapFocus: false,
        wasClosedByUser: true,
        hasActiveWorldMap: true));
Assert("离开大地图后不能凭关闭记录重新打开", expected: false,
    InteractionHubWorldMapFocusPolicy.ShouldOpenFromShortcut(
        hasSupportedMapFocus: false,
        wasClosedByUser: true,
        hasActiveWorldMap: false));

Assert("native event window search starts before a window is found", expected: true,
    InteractionHubWorldMapFocusPolicy.ShouldSearchForNativeEventWindow(
        hasCachedWindow: false,
        wasObservedActive: false));
Assert("cached native event window prevents another global search", expected: false,
    InteractionHubWorldMapFocusPolicy.ShouldSearchForNativeEventWindow(
        hasCachedWindow: true,
        wasObservedActive: false));
Assert("observed native event window prevents searching after destruction", expected: false,
    InteractionHubWorldMapFocusPolicy.ShouldSearchForNativeEventWindow(
        hasCachedWindow: false,
        wasObservedActive: true));
Assert("native event search waits until its probe interval", expected: false,
    InteractionHubWorldMapFocusPolicy.ShouldProbeNativeEventWindow(
        hasCachedWindow: false,
        wasObservedActive: false,
        now: 1.0f,
        nextProbeAt: 1.1f));
Assert("native event search runs when its probe interval elapses", expected: true,
    InteractionHubWorldMapFocusPolicy.ShouldProbeNativeEventWindow(
        hasCachedWindow: false,
        wasObservedActive: false,
        now: 1.1f,
        nextProbeAt: 1.1f));
Assert("native event flow uses map focus only before its window is observed", expected: true,
    InteractionHubWorldMapFocusPolicy.ShouldCheckWorldMapFallback(
        wasObservedActive: false));
Assert("observed native event window avoids per-frame world map searches", expected: false,
    InteractionHubWorldMapFocusPolicy.ShouldCheckWorldMapFallback(
        wasObservedActive: true));
Assert("world map fallback waits for native event startup grace period", expected: false,
    InteractionHubWorldMapFocusPolicy.ShouldCheckWorldMapFallback(
        wasObservedActive: false,
        secondsWaiting: 1.9f,
        graceSeconds: 2.0f));
Assert("world map fallback starts after native event startup grace period", expected: true,
    InteractionHubWorldMapFocusPolicy.ShouldCheckWorldMapFallback(
        wasObservedActive: false,
        secondsWaiting: 2.0f,
        graceSeconds: 2.0f));
Assert("hub hides as soon as native interaction starts", expected: true,
    InteractionHubWorldMapFocusPolicy.ShouldHideHubForNativeFlow(
        interactionPending: true,
        nativeEventActive: false));
Assert("hub hides while native event is active", expected: true,
    InteractionHubWorldMapFocusPolicy.ShouldHideHubForNativeFlow(
        interactionPending: true,
        nativeEventActive: true));
Assert("hub remains visible while no native interaction is pending", expected: false,
    InteractionHubWorldMapFocusPolicy.ShouldHideHubForNativeFlow(
        interactionPending: false,
        nativeEventActive: false));

Console.WriteLine("护肝交互大地图焦点与返回策略测试通过：26 项");

Assert("直达交互经过初始菜单时不能自动退出",
    (byte)InteractionHubReturnDecision.None,
    (byte)InteractionHubReturnPolicy.Decide(
        active: true,
        interactionObserved: false,
        hasDisplay: true,
        isMenuEvent: true));
Assert("进入实际交互后记录进度",
    (byte)InteractionHubReturnDecision.ObserveInteraction,
    (byte)InteractionHubReturnPolicy.Decide(
        active: true,
        interactionObserved: false,
        hasDisplay: true,
        isMenuEvent: false));
Assert("实际交互完成并返回菜单时关闭事件",
    (byte)InteractionHubReturnDecision.CloseReturnedMenu,
    (byte)InteractionHubReturnPolicy.Decide(
        active: true,
        interactionObserved: true,
        hasDisplay: true,
        isMenuEvent: true));
Assert("外部窗口监听期间隐藏事件页不会提前结束",
    (byte)InteractionHubReturnDecision.None,
    (byte)InteractionHubReturnPolicy.Decide(
        active: true,
        interactionObserved: true,
        hasDisplay: false,
        isMenuEvent: false,
        externalListenerActive: true));
Assert("赠礼选物窗口隐藏事件页时保持返回会话",
    (byte)InteractionHubReturnDecision.None,
    (byte)InteractionHubReturnPolicy.Decide(
        active: true,
        interactionObserved: true,
        hasDisplay: false,
        isMenuEvent: false,
        externalListenerActive: false,
        preserveAcrossHiddenDisplay: true));
Assert("原生直达尚在内部导航时不判定返回",
    (byte)InteractionHubReturnDecision.None,
    (byte)InteractionHubReturnPolicy.Decide(
        active: true,
        interactionObserved: false,
        hasDisplay: true,
        isMenuEvent: true,
        starting: true));
Assert("从原版菜单进入外部窗口时记录交互进度",
    (byte)InteractionHubReturnDecision.ObserveInteraction,
    (byte)InteractionHubReturnPolicy.Decide(
        active: true,
        interactionObserved: false,
        hasDisplay: false,
        isMenuEvent: false,
        externalListenerActive: true));
Assert("不经菜单直接结束时清理返回会话",
    (byte)InteractionHubReturnDecision.CompleteWithoutMenu,
    (byte)InteractionHubReturnPolicy.Decide(
        active: true,
        interactionObserved: true,
        hasDisplay: false,
        isMenuEvent: false,
        externalListenerActive: false));
Assert("开悟交互入口属于返回菜单", expected: true,
    InteractionHubReturnPolicy.IsMenuEvent("e78e92d1-7712-4d0f-82d2-780b65f4a49b"));
Assert("少林主线事件不是人物交互返回菜单", expected: false,
    InteractionHubReturnPolicy.IsMenuEvent("3d79705b-1245-4a8a-a45c-b2a8d5b2f02d"));
Assert("玄石火灰事件不是返回菜单", expected: false,
    InteractionHubReturnPolicy.IsMenuEvent("45b767f3-3d09-4502-bc94-6492c69c2e30"));
Assert("普通事件显示期间启用返回监听", expected: true,
    InteractionHubReturnPolicy.ShouldArmDirectReturn(
        started: true, eventStillShowing: true, externalListenerActive: false));
Assert("商店藏书等外部窗口期间启用返回监听", expected: true,
    InteractionHubReturnPolicy.ShouldArmDirectReturn(
        started: true, eventStillShowing: false, externalListenerActive: true));
Assert("同步结束且无窗口的交互不残留返回监听", expected: false,
    InteractionHubReturnPolicy.ShouldArmDirectReturn(
        started: true, eventStillShowing: false, externalListenerActive: false));
Assert("赠礼会话活动时拒绝第二次启动", expected: false,
    InteractionHubBeginPolicy.CanStart(returnSessionActive: true));
Assert("无活动赠礼会话时允许启动", expected: true,
    InteractionHubBeginPolicy.CanStart(returnSessionActive: false));
Assert("赠礼交互需跨越选物窗口的隐藏阶段", expected: true,
    InteractionHubReturnPolicy.ShouldPreserveAcrossHiddenDisplay(templateId: 6));
Assert("其他交互不默认保留隐藏会话", expected: false,
    InteractionHubReturnPolicy.ShouldPreserveAcrossHiddenDisplay(templateId: 57));
Assert("取消赠礼的不选页是自动返回中转页", expected: true,
    InteractionHubReturnPolicy.IsAutoReturnBridgeEvent("79705282-b752-4194-a11a-c627d2cbede5"));
Assert("赠礼结算页必须保留给玩家查看", expected: false,
    InteractionHubReturnPolicy.IsAutoReturnBridgeEvent("a431b14a-a2ec-4799-baaf-c9eee30cfc30"));
Assert("浏览货物结算页自动经过其它话题返回", expected: true,
    InteractionHubReturnPolicy.IsAutoReturnBridgeEvent("966d20e2-a5c8-40e4-a2cd-a5e3991ae53a"));
Assert("赠礼原生返回假但已进入选物事件时视为启动成功", expected: true,
    InteractionHubReturnPolicy.DidStartDirectInteraction(
        templateId: 6,
        nativeStarted: false,
        showingEventGuid: "5699d2a7-30c6-456e-9fe2-695b674e9e46"));
Assert("赠礼未到达选物事件时保留原生失败", expected: false,
    InteractionHubReturnPolicy.DidStartDirectInteraction(
        templateId: 6,
        nativeStarted: false,
        showingEventGuid: "05e87c45-f14e-49ef-8769-cbaced4753ae"));

Console.WriteLine("护肝交互返回会话策略测试通过：12 项");

InteractionPersonView mapPerson = Person(
    101, InteractionPersonGroup.CurrentBlock, InteractionPersonKind.Character);
InteractionPersonView teammateCopy = Person(
    101, InteractionPersonGroup.Teammate, InteractionPersonKind.Character);
InteractionPersonView merchant = Person(
    202, InteractionPersonGroup.Merchant, InteractionPersonKind.Character);
InteractionPersonView caravan = Person(
    303, InteractionPersonGroup.Merchant, InteractionPersonKind.Caravan);

Assert("地图普通人物点击需要自动结识", true,
    InteractionHubTargetPolicy.ShouldAutoMeet(
        InteractionPersonKind.Character, InteractionPersonGroup.CurrentBlock));
Assert("底部同道点击不重复结识", false,
    InteractionHubTargetPolicy.ShouldAutoMeet(
        InteractionPersonKind.Character, InteractionPersonGroup.Teammate));
Assert("地图商队点击不执行人物结识", false,
    InteractionHubTargetPolicy.ShouldAutoMeet(
        InteractionPersonKind.Caravan, InteractionPersonGroup.CurrentBlock));

Assert("地图点击优先选择当前地格中的同一人物",
    (byte)InteractionPersonGroup.CurrentBlock,
    (byte)InteractionHubTargetPolicy.ResolveGroup(
        101, InteractionPersonKind.Character, InteractionPersonGroup.CurrentBlock,
        new[] { mapPerson }, new[] { teammateCopy }, Array.Empty<InteractionPersonView>())!.Value);
Assert("底部同道点击优先选择同道分组",
    (byte)InteractionPersonGroup.Teammate,
    (byte)InteractionHubTargetPolicy.ResolveGroup(
        101, InteractionPersonKind.Character, InteractionPersonGroup.Teammate,
        new[] { mapPerson }, new[] { teammateCopy }, Array.Empty<InteractionPersonView>())!.Value);
Assert("地图商人不在当前地格分组时回退到商人分组",
    (byte)InteractionPersonGroup.Merchant,
    (byte)InteractionHubTargetPolicy.ResolveGroup(
        202, InteractionPersonKind.Character, InteractionPersonGroup.CurrentBlock,
        Array.Empty<InteractionPersonView>(), Array.Empty<InteractionPersonView>(),
        new[] { merchant })!.Value);
Assert("商队 ID 不会误选中同 ID 的人物",
    (byte)InteractionPersonGroup.Merchant,
    (byte)InteractionHubTargetPolicy.ResolveGroup(
        303, InteractionPersonKind.Caravan, InteractionPersonGroup.CurrentBlock,
        new[] { Person(303, InteractionPersonGroup.CurrentBlock,
            InteractionPersonKind.Character) }, Array.Empty<InteractionPersonView>(),
        new[] { caravan })!.Value);
Assert("快照中不存在的目标不产生分组", true,
    InteractionHubTargetPolicy.ResolveGroup(
        404, InteractionPersonKind.Character, InteractionPersonGroup.CurrentBlock,
        new[] { mapPerson }, new[] { teammateCopy }, new[] { merchant, caravan }) == null);

Console.WriteLine("护肝交互地图人物目标定位与自动结识策略测试通过：8 项");

static InteractionPersonView Person(int id, InteractionPersonGroup group,
    InteractionPersonKind kind) =>
    new(id, group, kind, "", Array.Empty<InteractionOptionView>(), 0);

static void Assert<T>(string name, T expected, T actual)
    where T : IEquatable<T>
{
    if (!expected.Equals(actual))
    {
        throw new InvalidOperationException($"{name}: expected {expected}, actual {actual}");
    }
}
