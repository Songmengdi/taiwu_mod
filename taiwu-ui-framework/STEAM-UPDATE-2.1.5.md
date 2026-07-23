# Taiwu UI Framework 2.1.5.0 更新说明

可直接粘贴到 Steam 创意工坊的“更新简述”：

> 新增紧凑地域页签与低干扰描边操作按钮，供依赖 MOD 在保留太吾原生风格的同时组织多地域搜索结果。

完整说明：

- 新增 `SheetTabs`：适合地域等少量、短标签的结果分组，不会采用顶部主导航的等宽铺满样式。
- 新增 `Outlined` 按钮样式：用于上下文工具和行内操作，降低与主要操作的视觉竞争。
- 保持程序集兼容版本不变；依赖旧版框架的 MOD 可继续加载。

上传目录：`publish/TaiwuUiFramework`。

上传包只保留 `Config.lua`、`Cover.jpg` 与 `Plugins/TaiwuUi.Core.dll`、`Plugins/TaiwuUi.Core.deps.json`；不要上传 PDB。
