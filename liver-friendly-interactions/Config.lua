return {
    Title = "护肝交互",
    Author = "SMD",
    Version = "0.9.0.0",
    GameVersion = "1.0.72.0",
    Description = "略过全部小型集会与大型春日集市的首次到达强制说明，并省略休息点打坐后的结果确认。\n\n大地图按 ~ 打开统一人物交互页，可在当前地格、同道与商人/商队之间连续切换，并按全局偏好排列常用交互；商店界面会提前后台加载。原版人物入口保持不变。\n\n奇遇内可主动交互的商人、设施等不再于抵达时强制弹出，仍可点击或按 1 主动触发。大地图按 1 可直接进入太吾当前地格的奇遇。",
    Visibility = 0,
    Dependencies = {
        [1] = 3767831883,
    },
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
