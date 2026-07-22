# taiwu_mod

《太吾绘卷》MOD 工作区，包含 4 个独立 MOD 项目：

| 目录 | MOD | 简介 |
| --- | --- | --- |
| `map-skill-finder/` | 太吾寻访 | 原生风格全屏寻访工具：按功法书/技艺书/人物/商会等条件查询持有人，支持拼书组合求解与地图标记。 |
| `combat-skill-preset-binding/` | 运功预设绑定 | 每套运功预设自动记住各功法的突破预设，切换运功方案时自动恢复突破盘与玄机配置。 |
| `taiwu-ui-framework/` | TaiwuUiFramework | 声明式原生 UI 框架，让消费 MOD 用 C# element tree 描述界面（太吾寻访基于它构建）。 |
| `taiwu-probe-dual/` | TaiwuProbeDual | 调试探针：游戏内启动 MCP 服务，供 AI Agent 做 UI 自动化、反射求值、热加载与日志读取。 |

各子目录是独立 MOD，可单独构建、部署与发布；具体构建方式见各项目 README 及根目录 [AGENTS.md](AGENTS.md)。
