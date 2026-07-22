return {
    Title = "太吾村本月最优排班",
    Author = "SMD",
    Version = "0.1.2.0",
    GameVersion = "1.0.56.0",
    Description = "按照可调整的目标优先级，计算本月太吾村建筑人员安排。支持资产、人才培养、资源收获、威望与招人。\n\n在太吾村建筑总览底部工具栏点击“村务排班”，或按 F8 打开原生风格预览。当前只提供预览，不会自动修改岗位。",
    Visibility = 0,
    BackendPlugins = {
        [1] = "VillageWorkOptimizer.Backend.dll",
    },
    FrontendPlugins = {
        [1] = "VillageWorkOptimizer.Frontend.dll",
    },
    Source = 0,
    HasArchive = false,
    NeedRestartWhenSettingChanged = false,
    ChangeConfig = false,
    DefaultSettings = { },
    SettingGroups = { },
    TagList = {
        [1] = "Modifications",
        [2] = "Compatible Mods",
    },
}
