# AGENTS.md

本仓库是《太吾绘卷》MOD 工作区，单 git 仓库管理 4 个独立 MOD 项目。每个子目录是一个完整、可独立构建发布的 MOD，根目录没有统一解决方案。

## 项目一览

| 目录 | MOD | 说明 |
| --- | --- | --- |
| `taiwu-probe-dual/` | TaiwuProbeDual | 调试探针（基础设施）。游戏内启动 MCP 服务 `localhost:13131/mcp`，供 AI Agent 远程做 UI 快照/点击、反射求值、热加载 DLL、读日志。前端 + 后端双 DLL。 |
| `taiwu-ui-framework/` | TaiwuUiFramework | 声明式原生 UI 框架 2.0（TaiwuUi.Core.dll）。消费 MOD 用 C# element tree 描述界面，框架负责验证、布局、差异更新。详见其 `README.md` / `ARCHITECTURE.md`。 |
| `map-skill-finder/` | 太吾寻访 | 全屏寻访工具：功法书/技艺书/拼书组合/人物/商会查询与地图标记。**依赖 taiwu-ui-framework**（前端 csproj 引用 `TaiwuUi.Core.dll`）。 |
| `combat-skill-preset-binding/` | 运功预设绑定 | 后端单 DLL，无 UI。切换运功预设时自动恢复每门功法的突破预设（突破盘/玄机格/玄机之物）。 |

## 关键路径

- 游戏目录（构建脚本内硬编码）：`D:\SteamLibrary\steamapps\common\The Scroll Of Taiwu`
- 游戏 MOD 安装目录：`<游戏目录>\Mod\<ModName>\`
- 后端日志：`<游戏目录>\Logs\GameData_*.log`
- 前端日志：Unity Player.log

## 构建与部署

```powershell
# combat-skill-preset-binding：build + deploy 一步到位
cd combat-skill-preset-binding
dotnet build .\CombatSkillPresetBinding.Backend.csproj -c Release
.\deploy.cmd

# taiwu-probe-dual：构建即自动部署（TaiwuProbe.props 里的 TaiwuDeployToGame target）
cd taiwu-probe-dual
dotnet build TaiwuProbeFrontend\TaiwuProbeFrontend.csproj -c Release
dotnet build TaiwuProbeBackend\TaiwuProbeBackend.csproj -c Release

# taiwu-ui-framework：框架 + sample 一起构建并复制到游戏目录
cd taiwu-ui-framework
.\install-local.ps1

# map-skill-finder：先装框架，再分别构建前后端（csproj 内 GameDir 指游戏目录）
cd map-skill-finder
dotnet build MapSkillFinder.Backend.csproj -c Release
dotnet build MapSkillFinder.Frontend.csproj -c Release
```

## 测试

各项目 `tests/` 下是控制台 Exe 形式的测试工程（非 xUnit），用 `dotnet run` 执行：

- `taiwu-ui-framework/tests/` — TaiwuUi.Core 契约测试（net10.0）
- `map-skill-finder/tests/` — 领域逻辑测试（拼书求解等，net8.0）
- `taiwu-probe-dual/tests/` — 协议测试与前后端桥测试（net10.0）

## 运行期验证（重要）

游戏运行时通过 TaiwuProbe 的 MCP 工具验证改动，配置在 `.kimi-code/mcp.json`（不入库）：

- `taiwu_ping` / `taiwu_backend_ping` — 探针连通性
- `taiwu_ui_snapshot` → `taiwu_ui_click`/`fill`/`toggle`/`hover`/`scroll` — snapshot→ref→action 的 UI 自动化范式
- `taiwu_ui_screenshot` — 截图回传，视觉回归
- `taiwu_hotload_invoke` — 热加载 DLL 免重启验证
- `taiwu_backend_eval` / `taiwu_backend_csharp` / `taiwu_backend_log_tail` — 后端状态查询与日志

## 架构常识

- MOD 分**前端**（Unity 主线程，UI/Harmony Patch）与**后端**（GameData 主循环，游戏权威状态）两端，分别编译为独立 DLL；两端不能直接互相调用，需走桥接（参考 taiwu-probe-dual 的 HTTP 桥、map-skill-finder 的 FinderBackendClient）。
- 后端 Domain 状态只能在 GameData 主线程访问；taiwu-probe-dual 的后端 HTTP 线程只入队，由主循环消费。

## 仓库约定

- `.gitignore` 排除 `obj/`、`publish/`、`bin/`、`mod/Plugins/*.dll` 等构建产物及 `.artifacts/`、`.kimi-code/` 等本地目录。
- `.git-backup/`（不入库）保存 map-skill-finder / taiwu-probe-dual / taiwu-ui-framework 合并前的独立 git 历史；需要时把对应目录移回 `<项目>/.git` 即可恢复。
- taiwu-probe-dual 上游仓库：`https://github.com/magian1127/TaiwuProbe.git`
- 提交信息使用中文，格式如 `feat: ...` / `fix: ...` / `chore: ...`。

## Agent skills

### Issue tracker

Issue 与 PRD 跟踪在本仓库的 GitHub Issues（使用 `gh` CLI）。见 `docs/agents/issue-tracker.md`。

### Triage labels

使用五个默认 triage 标签：`needs-triage` / `needs-info` / `ready-for-agent` / `ready-for-human` / `wontfix`。见 `docs/agents/triage-labels.md`。

### Domain docs

Single-context 布局：根目录 `CONTEXT.md` + `docs/adr/`（不存在时静默跳过，由 `/domain-modeling` 惰性创建）。见 `docs/agents/domain.md`。
