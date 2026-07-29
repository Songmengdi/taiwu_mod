return {
    Title = "护肝交互",
    Author = "SMD",
    Version = "0.7.0.0",
    GameVersion = "1.0.72.0",
    Description = "从后端略过全部小型集会与大型春日集市的首次到达强制单选说明，并省略休息点打坐后的结果确认。\n\n保留游戏原生的 onlyOnce 触发记录、事件效果、休息结算和任务推进；遇到两个及以上真实选项时立即恢复正常显示。",
    Visibility = 0,
    BackendPlugins = {
        [1] = "LiverFriendlyInteractions.Backend.dll",
    },
    FrontendPlugins = { },
    Source = 0,
    HasArchive = false,
    NeedRestartWhenSettingChanged = false,
    ChangeConfig = false,
    DefaultSettings = { },
    TagList = {
        [1] = "Modifications",
        [2] = "Compatible Mods",
    },
}
