# 太吾 Mod 资料审计记录

> 最后核对：2026-07-11。
>
> 本文只记录资料的参考等级和已排除的旧结论。当前开发方法以 [taiwu-modding-methodology-2026.md](./taiwu-modding-methodology-2026.md) 为准。

## 1. 为什么重写旧调研

早期调研混合了 EA、2022 正式版、2025 拆分版和 2026-06-17 正式版资料，其中若干结论已被本机 DLL 和运行日志直接证伪。继续保留原文会让开发者在正确文档和错误文档之间反复横跳，因此本文件不再充当教程。

## 2. 已证伪或不应继续传播的结论

| 旧结论 | 当前判断 | 本机证据 |
| --- | --- | --- |
| 整个 Mod 都应使用 .NET 8 | 错误 | 后端 runtime 是 .NET 8；前端和后端应分工程、分目标框架 |
| 官方入口应继承 `TaiwuRemakeHarmonyPlugin` | 错误 | 当前两侧 `TaiwuModdingLib.dll` 可用入口是 `TaiwuRemakePlugin`，Harmony 自行安装 |
| 一个工程同时引用 `Assembly-CSharp.dll` 和 `GameData.dll` | 错误架构 | 两者属于不同运行侧，权威后端通过 Domain/IPC 交互 |
| `GameData.dll` 位于前端 Managed，可直接作为统一后端引用 | 错误 | 当前后端实现位于 `Backend/`，前端主要持有共享类型和调用端 |
| 日志只看 `output_log.txt` | 过时 | 当前常用前端 `Player.log`、后端 `Logs/GameData_*.log` |
| Mod 文件夹名必须与工坊标题完全一致 | 未证实且不应作为规则 | 当前加载依赖 Config 和上传语义；内部目录名与展示标题是不同概念 |
| `source=1` 能概括全部 Workshop 发布语义 | 过度简化 | 还涉及 `FileId`、上传工具写回、可见性和当前 Config 解析 |
| 卸载后应删除整个某类 Mod 存档目录 | 危险 | 可能同时删除其他 Mod 数据；卸载策略必须按本 Mod 的 archive key 设计 |
| 能编译就说明 API 可用 | 错误 | `GetEquippedCombatSkills()` 在当前运功界面存在但返回空，真实调用链使用 `CombatSkillEquipment` |

## 3. 当前公开参考项目

### 当前工程化参考

- [Wanxiang-Sanctum/taiwu-mods](https://github.com/Wanxiang-Sanctum/taiwu-mods)：monorepo、前后端/共享项目、组包和发布流程。
- [Wanxiang-Sanctum/community-taiwu-mods](https://github.com/Wanxiang-Sanctum/community-taiwu-mods)：复杂 Mod 和工具的实际组织方式。
- [gruiyuan/taiwu-mod-dev-skill](https://github.com/gruiyuan/taiwu-mod-dev-skill)：面向当前版本的开发与反编译流程。
- [jinqia0/AutoMonthlyGroupMerchantBuy](https://github.com/jinqia0/AutoMonthlyGroupMerchantBuy)：较小的后端 Domain Patch 样本。

### 历史模式参考

- `phorcys/Taiwu_mods`：旧生态的重要代码库，只借鉴 Harmony 和功能拆分思路。
- `hanabi1224/ScrollOfTaiwuMods`：可参考 CI/工程自动化，不作为当前 API 基线。
- 2022–2023 教程、B 站视频和论坛帖子：只用于认识 Config、Harmony、前后端等概念。

## 4. 参考项目审计表

每次准备复制某段代码前填写：

| 项目 | 提交/更新日期 | 目标游戏版本 | 端侧 | 目标框架 | Patch 目标本机存在 | 调用时机已核对 | 参考等级 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 示例 | 2026-xx-xx | 1.0.xx | Backend | net8.0 | 是/否 | 是/否 | 当前实现/模式/历史 |

没有完成“本机存在”和“调用时机已核对”的代码，不能直接进入正式 Mod。

## 5. 本机样本的使用方式

Workshop Mod 的价值不是证明它“写得最好”，而是提供当前加载器和游戏版本的运行样本。选择样本时优先：

1. 当前版本实际加载成功；
2. 最近更新；
3. 与需求属于同一端侧和领域；
4. 入口和依赖较少；
5. 能从日志确认 Patch 已命中。

反编译第三方 Mod 时只学习调用方式和项目结构，不复制受版权约束的大段实现，也不假定其内部私有框架是必要依赖。

## 6. 资料维护规则

- 新结论必须注明本机版本和证据来源；
- 游戏更新后优先更新主方法论中的程序集、框架和生命周期事实；
- 旧资料不再以“当前可用”措辞呈现；
- 未复核的 Workshop ID、QQ群、论坛状态和发布日期不写成长期事实；
- 如果本文件和主方法论冲突，以主方法论及当前本机证据为准。
