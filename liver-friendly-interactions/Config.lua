return {
    Title = "护肝交互",
    Author = "SMD",
    Version = "0.8.2.0",
    GameVersion = "1.0.72.0",
    Description = "略过全部小型集会与大型春日集市的首次到达强制单选说明，并省略休息点打坐后的结果确认。\n\n大地图按 1 可直接进入太吾当前地格的奇遇，不触发人物交互；奇遇内部按 1 可触发当前交互列表第 1 项，再按 1 使用游戏原生快捷键确认。",
    Visibility = 0,
    BackendPlugins = {
        [1] = "LiverFriendlyInteractions.Backend.dll",
    },
    FrontendPlugins = {
        [1] = "LiverFriendlyInteractions.Frontend.dll",
    },
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
