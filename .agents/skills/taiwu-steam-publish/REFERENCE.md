# Steam 创意工坊发布参考

## 受管 MOD 与目录

| MOD | 工程配置 | 上传包 | 游戏安装目录 |
| --- | --- | --- | --- |
| 太吾寻访 | `map-skill-finder/mod/Config.lua` | `map-skill-finder/publish/地图功法找人` | `<GameDir>/Mod/地图功法找人` |
| Taiwu UI Framework | `taiwu-ui-framework/Config.lua` | `taiwu-ui-framework/publish/TaiwuUiFramework` | `<GameDir>/Mod/TaiwuUiFramework` |

默认 `<GameDir>` 为 `D:\SteamLibrary\steamapps\common\The Scroll Of Taiwu`。需要不同库目录时，为脚本传入 `-GameDir`。

## 版本规则

每次发布都要让下列位置一致：

- `Config.lua` 的 `Version`：游戏模组管理与创意工坊展示的版本。
- `.csproj` 的 `Version`、`FileVersion`：构建 DLL 的产品与文件版本。
- `UpdateLogList`：追加这次发布的 Unix 时间戳。

不要修改已有 `FileId`。太吾寻访须保留对 UI Framework 的依赖；先发布框架，后发布消费者。框架的 `AssemblyVersion` 是兼容性承诺，只有破坏 ABI 时才提升它。

## 上传包内容

| MOD | 必须保留 |
| --- | --- |
| 太吾寻访 | `Config.lua`、`Cover.jpg`、`Plugins/MapSkillFinder.Backend.dll`、`Plugins/MapSkillFinder.Backend.deps.json`、`Plugins/MapSkillFinder.Frontend.dll`、`Plugins/MapSkillFinder.Frontend.deps.json` |
| UI Framework | `Config.lua`、`Cover.jpg`、`Plugins/TaiwuUi.Core.dll`、`Plugins/TaiwuUi.Core.deps.json` |

不要上传 `.pdb`、`Settings.lua`、本地调试产物或其它个人配置。封面是既有工坊资源；准备脚本会保留并检查它，而不从构建目录伪造空文件。

## 发布者手工流程

1. 写更新说明：一句话“更新简述”先给玩家价值，再列完整变更、修复和兼容性。
2. 修改版本字段与更新日志，运行相关测试。
3. 完全退出游戏，再运行 `Prepare-SteamRelease.ps1`，确认输出的两个版本和所有哈希校验成功。
4. 启动游戏，进入“模组管理 → 上传模组”。上传界面从游戏 `Mod/` 目录读取清单，不读取 `publish/` 目录。
5. 依序更新框架和太吾寻访。上传完成后打开创意工坊条目确认。

若界面仍是旧版本，先关闭游戏。不要在旧界面“保存”或“还原”，否则可能把旧的内存清单覆盖到磁盘。关闭后执行 `-VerifyOnly`：若安装目录版本不一致，重新执行完整准备命令；若一致，重启后再进入上传页。

游戏可能自行重新排版安装目录的 `Config.lua` 并追加 `UpdateLogList` 时间戳，这是正常行为。校验时以安装目录的 `Version` 为准；上传包中的 `Config.lua` 与 DLL 才要求逐字节匹配源产物。

## 给订阅用户的更新指引

可复制给普通玩家：

> Steam 会自动下载已订阅 MOD 的更新。请完全退出《太吾绘卷》，等待 Steam 下载完成后重新启动游戏；确认“Taiwu UI Framework”和“太吾寻访”均已订阅并启用。不要把开发版 DLL 手工覆盖到创意工坊目录。若更新后异常，可先停用两个 MOD、重启游戏，再依次启用前置框架和太吾寻访。

## 更新说明模板

```md
一句话更新简述：<玩家立刻能感知的改进>。

完整说明：
- 新增：…
- 优化：…
- 修复：…
- 兼容性：适配游戏版本 …；需要的前置为 …。
```
