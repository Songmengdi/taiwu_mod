# 《太吾绘卷：天幕心帷》Mod 开发方法论

> 文档基线：游戏 2026-06-17 正式版发布线；本机 2026-07-10 热更新文件，游戏配置版本 `1.0.55.0`。
>
> 验证环境：`D:\SteamLibrary\steamapps\common\The Scroll Of Taiwu`
>
> 最后核对：2026-07-11。

这不是一份“复制代码即可用”的 API 清单，而是一套在游戏持续更新时仍能工作的开发方法。最重要的原则只有一句：

> 网络资料负责提供搜索方向；本机当前 DLL、实际调用链和运行日志负责决定代码。

## 1. 事实的可信度顺序

开发中遇到互相矛盾的资料时，按以下顺序判断：

1. 当前游戏运行日志和可重复实验；
2. 当前安装目录 DLL 的反编译代码和程序集元数据；
3. 当前安装的、确实能运行的 Workshop Mod；
4. 2026 当前分支的开源项目；
5. Wiki、论坛、视频和教程；
6. 2026-06-17 以前的旧 Mod 源码。

后四项可以帮助找到关键词和设计模式，但不能证明类型、方法签名、目标框架或调用时机仍然正确。

文档或代码中的结论最好附一种证据标签：

- **本机实证**：从当前 DLL、配置文件或日志直接确认；
- **样本验证**：从当前可运行 Mod 反编译确认；
- **推断**：根据调用链得出的设计判断，仍需运行验证；
- **历史资料**：只解释概念，不可直接复制 API。

## 2. 当前架构：前端和后端是两个运行侧

当前游戏不是“一个 Unity 进程加一堆 DLL”，而是两个运行侧：

```text
Unity 前端进程
  The Scroll of Taiwu.exe
  The Scroll of Taiwu_Data/Managed/Assembly-CSharp.dll
  The Scroll of Taiwu_Data/Managed/GameData.Shared.dll
  The Scroll of Taiwu_Data/Managed/TaiwuModdingLib.dll
  UnityEngine.*.dll
            │
            │ 游戏的 Domain 调用/序列化/IPC
            ▼
GameData 后端进程
  Backend/GameData.exe
  Backend/GameData.dll
  Backend/GameData.Shared.dll
  Backend/GameData.Common.dll
  Backend/GameData.ArchiveData.dll
  Backend/GameData.Utilities.dll
  Backend/GameData.*.dll
  Backend/TaiwuModdingLib.dll
```

`Backend/GameData.runtimeconfig.json` 在本机明确声明：

```json
{
  "tfm": "net8.0",
  "includedFrameworks": [
    { "name": "Microsoft.NETCore.App", "version": "8.0.22" }
  ]
}
```

这只证明后端使用 .NET 8，不能推出前端也应使用 `net8.0`。

| 需求 | 运行侧 | 首要观察对象 |
| --- | --- | --- |
| UI、按钮、Tooltip、Unity 对象、输入和渲染 | 前端 | `Assembly-CSharp.dll` |
| 角色、物品、战斗、过月、存档权威状态 | 后端 | `Backend/GameData.dll` 和所需拆分程序集 |
| UI 查询或修改后端状态 | 前端 + 后端 | 前端调用点、后端 DomainMethod、跨端契约 |
| 文本、事件和配置内容 | 内容系统优先 | Lua、事件包、配置表及其加载逻辑 |

不要在同一个入口工程里同时引用前端 `Assembly-CSharp.dll` 和后端 `GameData.dll`，也不要把后端业务规则复制到前端当作权威判断。

## 3. 到底应该反编译哪些 DLL

### 3.1 固定先看

| DLL | 什么时候看 | 主要用途 |
| --- | --- | --- |
| 两侧 `TaiwuModdingLib.dll` | 建项目之前 | 插件基类、生命周期、设置、Mod 数据和通信原语 |
| 前端 `Assembly-CSharp.dll` | 需求涉及画面或用户操作 | UI 类、点击事件、异步 Domain 调用、数据刷新 |
| 后端 `GameData.dll` | 需求涉及规则或存档 | Domain 实现、权威状态、DomainMethod 分发、业务调用链 |
| 两侧 `GameData.Shared.dll` | 跨端参数或显示数据 | DTO、枚举、配置和共享数据结构 |

### 3.2 按编译错误和调用链再看

| DLL | 典型触发条件 |
| --- | --- |
| `GameData.Common.dll` | `DataContext` 等后端公共类型 |
| `GameData.ArchiveData.dll` | 存档读写、序列化上下文 |
| `GameData.Utilities.dll` | `AdaptableLog`、集合和工具类型 |
| `GameData.Serializer.dll` | 自定义跨端数据或序列化问题 |
| `GameData.ActionPlanning*.dll` | 行动规划、AI 行为相关调用链 |
| `GameData.Adventure.dll` | 奇遇领域逻辑 |
| `UnityEngine.*.dll` | 前端实际使用对应 Unity 模块时 |
| `0Harmony.dll` | 一般只读官方文档；只有排查 Harmony 内部行为才反编译 |

不要一开始反编译整个 `Managed` 和 `Backend`。更有效的路线是：

```text
界面文字/用户动作
  → 前端事件处理方法
  → AsyncCall 或 Domain 操作 ID
  → 后端 DomainMethod
  → 权威状态的读写方法
  → 上下游调用者
```

### 3.3 反编译命令

```powershell
dotnet tool install --global ilspycmd

$game = 'D:\SteamLibrary\steamapps\common\The Scroll Of Taiwu'

ilspycmd -p -o decompiled\frontend `
  "$game\The Scroll of Taiwu_Data\Managed\Assembly-CSharp.dll"

ilspycmd -p -o decompiled\backend `
  "$game\Backend\GameData.dll"

ilspycmd -p -o decompiled\shared `
  "$game\Backend\GameData.Shared.dll"
```

反编译输出应作为本机临时观察资料，不应把整份游戏源码提交到仓库。

## 4. 从需求到真实调用链

### 4.1 先写行为句

不要从“我要 Patch 哪个方法”开始，而应先写：

```text
当 <事件> 发生时，在哪个运行侧读取 <权威状态>；
满足 <条件> 后，通过哪个游戏原生入口产生 <结果>；
数据在何时保存，禁用或卸载后如何处理。
```

例如：

```text
切换太吾的运功方案后，在后端读取目标方案装备的功法；
对每门有绑定的功法调用游戏原生突破预设切换入口；
绑定随当前存档保存，禁用时停止记录和应用但保留数据。
```

### 4.2 用可见文本找前端，用领域名找后端

前端常用搜索锚点：

- 按钮或 Tooltip 的中文/本地化键；
- UI 类名、View、Controller、OnClick、Refresh；
- `AsyncCall`、操作 ID 和回调；
- 显示数据 DTO 的类型名。

后端常用搜索锚点：

- `DomainManager.<领域>`；
- `[DomainMethod]`；
- 状态字段的 Getter/Setter；
- `SetElement_*`、`AddElement_*`、`RemoveElement_*`；
- 前端传入的操作 ID 在分发表中的反序列化位置。

可见 UI 只是入口，不一定是正确 Patch 点。按钮可能只打开面板，真正修改发生在后端 Domain 方法；反过来，如果需求只是增加 Tooltip，Patch 后端会徒增跨端复杂度。

### 4.3 不只看目标方法，还要看调用者

方法名正确不代表 Patch 时机正确。至少检查：

1. 谁调用它；
2. 调用前状态是什么；
3. 原方法何时改变关键字段；
4. 原方法内部是否递归调用另一个已 Patch 方法；
5. 失败或异常时 Postfix/Finalizer 如何表现；
6. 自动操作、复制、删除等旁路是否也走这里。

## 5. 项目结构和目标框架

当前本机已验证的官方插件入口基类是：

```csharp
TaiwuModdingLib.Core.Plugin.TaiwuRemakePlugin
```

当前 DLL 中不要假定存在旧教程常用的 `TaiwuRemakeHarmonyPlugin`。插件自行创建 Harmony：

```csharp
using HarmonyLib;
using TaiwuModdingLib.Core.Plugin;

[PluginConfig("MyMod.Backend", "author-id", "0.1.0")]
public sealed class Plugin : TaiwuRemakePlugin
{
    private Harmony? _harmony;

    public override void Initialize()
    {
        _harmony = new Harmony(GetGuid());
        _harmony.PatchAll(typeof(Plugin).Assembly);
    }

    public override void Dispose()
    {
        _harmony?.UnpatchSelf();
        _harmony = null;
    }
}
```

本机入口生命周期已确认包含：

```text
Initialize
Dispose
OnModSettingUpdate
OnEnterNewWorld
OnLoadedArchiveData
OnCrossArchive
```

工程建议：

| 工程 | 目标框架 | 引用来源 |
| --- | --- | --- |
| Frontend | `netstandard2.1` | `The Scroll of Taiwu_Data/Managed` |
| Backend | `net8.0` | `Backend` |
| Contracts | 无 Unity/后端实现依赖的共同目标 | 纯 DTO 和协议 |

后端最小 `.csproj` 形态：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <PropertyGroup>
    <GameDir Condition="'$(GameDir)' == ''">D:\SteamLibrary\steamapps\common\The Scroll Of Taiwu</GameDir>
    <BackendDir>$(GameDir)\Backend</BackendDir>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="0Harmony" HintPath="$(BackendDir)\0Harmony.dll" Private="false" />
    <Reference Include="TaiwuModdingLib" HintPath="$(BackendDir)\TaiwuModdingLib.dll" Private="false" />
    <Reference Include="GameData" HintPath="$(BackendDir)\GameData.dll" Private="false" />
    <Reference Include="GameData.Shared" HintPath="$(BackendDir)\GameData.Shared.dll" Private="false" />
    <Reference Include="GameData.Common" HintPath="$(BackendDir)\GameData.Common.dll" Private="false" />
  </ItemGroup>
</Project>
```

所有游戏引用都应 `Private=false`，避免把游戏 DLL 打进 Mod。第三方私有依赖则必须明确部署和冲突策略。公开仓库最好用未提交的本机属性文件或环境变量覆盖 `GameDir`。

## 6. Harmony Patch 的选择原则

按侵入程度排序：

1. 游戏已有配置、事件或公开 API；
2. Prefix/Postfix；
3. Finalizer 处理补丁自己的状态清理；
4. Transpiler；
5. 反射/Publicizer；
6. 外部加载器。

### 6.1 Prefix、Postfix 和 `__state`

需要比较修改前后状态时，用 Prefix 捕获旧值，经 `__state` 传给 Postfix：

```csharp
[HarmonyPrefix]
private static void Prefix(short skillId, out sbyte __state)
{
    __state = ReadCurrentPreset(skillId);
}

[HarmonyPostfix]
private static void Postfix(short skillId, sbyte index, sbyte __state)
{
    RecordTransition(skillId, previous: __state, current: index);
}
```

这比在 Postfix 中猜“修改前是什么”可靠，因为原方法执行后旧状态可能已经不可恢复。

### 6.2 防止递归和旁路污染

如果 Mod 在 Postfix 中再次调用被 Patch 的原生方法，必须设置重入保护：

```csharp
if (_applying)
    return;

_applying = true;
try
{
    CallOriginalGameApi();
}
finally
{
    _applying = false;
}
```

复制、新增、删除方案可能在内部再次调用普通切换方法。可用 mutation depth 抑制嵌套 Patch，并用 Finalizer 在原方法抛异常时释放深度。否则一次异常可能让 Mod 永久停留在“正在修改”状态。

### 6.3 Patch 失败不能破坏原游戏操作

辅助型 Mod 应遵守 fail-open：补丁异常时记录完整错误，但保留游戏原本的切换、装备或删除结果。只有在明确保护存档一致性且能证明安全时，才考虑让 Prefix 返回 `false` 跳过原方法。

## 7. 状态建模：先找出游戏“没有保存什么”

许多 Mod 不是修改一个值，而是在补充原游戏缺少的关系。必须先分别写出：

- 游戏原生状态；
- Mod 新增状态；
- 两者同步的事件；
- 初次使用时如何初始化；
- 保存、复制、删除和卸载时如何迁移。

不要把 UI 上同时出现的两个概念误认为游戏已经保存了它们的对应关系。

### 7.1 运功预设绑定案例

本机反编译确认：

- `CombatSkillPlan` 保存当前运功方案的功法组合；
- `CombatSkillBreakPreset.CurrentIndex` 是某门功法当前使用的突破预设；
- 原游戏没有保存“运功方案 → 功法 → 突破预设”的关系。

Mod 因此新增：

```text
Dictionary<运功方案 ID, Dictionary<功法 ID, 突破预设索引>>
```

同步事件：

```text
切换运功方案 Prefix   → 只补齐离开方案中尚未记录的功法
切换运功方案 Postfix  → 应用目标方案已有绑定
修改突破预设 Prefix   → 捕获修改前旧值
修改突破预设 Postfix  → 用旧值补齐其他缺失方案，只更新当前方案为新值
复制/新增/删除方案    → 同步复制、建立或重排绑定
新增/自动装备功法     → 应用当前方案对该功法已有的绑定
```

这里有两个重要教训。

#### 教训一：名字像对，不代表数据源对

第一版使用：

```csharp
GetEquippedCombatSkills()
```

代码能编译，名字也合理，但当前运功界面运行日志显示：

```text
equipped=[]
```

反编译 `CharacterDomain.ApplyCombatSkillPlan` 后发现，当前版本真正使用的是 `CombatSkillEquipment`。正确枚举方式是读取五类装备列表，而不是沿用旧数组接口。

结论：**编译成功只证明签名存在，不证明它仍是当前功能的权威数据源。**

#### 教训二：首次初始化必须发生在信息丢失之前

原游戏只有全局突破预设。若用户先把运功三中的小纵跃功从“贰”改成“壹”，再首次访问运功二，Postfix 时全局状态只剩“壹”，已经无法推断运功二原来应为“贰”。

因此必须在修改发生前捕获旧值：

```text
修改前：旧值 = 贰
  未绑定的运功一 → 贰
  未绑定的运功二 → 贰
  未绑定的运功三 → 贰

修改后：只更新当前运功三 → 壹
```

已有绑定绝不能被这个初始化过程覆盖。

这条经验可推广到所有“给原游戏补一层上下文绑定”的 Mod：在原状态被覆盖之前保存信息，不要在变化后凭当前值补历史。

## 8. 存档、设置和生命周期

`Config.lua` 中需要区分：

- 设置：用户偏好，例如启用开关、详细日志；
- Archive Data：跟随某个存档的业务状态；
- 运行时静态状态：重入标志、缓存和 mutation depth。

如果 Mod 有跟随存档的数据，应设置：

```lua
HasArchive = true
```

后端可通过 `DomainManager.Mod` 的 archive data 保存。数据应包含版本号，并对未知方案 ID、功法 ID和非法索引进行过滤。读档失败应回退为空状态并记录错误，而不是阻止存档载入。

生命周期建议：

| 时机 | 责任 |
| --- | --- |
| `Initialize` | 读取设置、安装 Patch、初始化非存档资源 |
| `OnLoadedArchiveData` | 清空旧缓存并读取当前存档数据 |
| `OnEnterNewWorld` | 建立新世界的空业务状态 |
| `OnModSettingUpdate` | 重新读取设置，不重复安装 Patch |
| `Dispose` | Unpatch，清空静态引用、缓存和重入状态 |

关闭功能开关时是否保留存档数据要明确说明。通常“停止记录和应用，但保留已有数据”比静默删除更安全。

## 9. Config.lua 和部署结构

官方 Mod 目录的常见结构：

```text
The Scroll Of Taiwu/Mod/MyMod/
  Config.lua
  Settings.Lua                 # 游戏生成或维护
  Cover.jpg                    # 可选
  Plugins/
    MyMod.Frontend.dll         # 可选
    MyMod.Backend.dll          # 可选
```

前后端入口分别写入 `FrontendPlugins` 和 `BackendPlugins`。不要根据旧教程假定文件夹名必须等于工坊标题；以当前 `Config.lua` 解析、上传工具写回的 `FileId` 和实际发布流程为准。

部署脚本至少应做到：

1. Release 构建失败立即退出；
2. 只复制本 Mod 的 `Config.lua`、封面、入口 DLL、私有依赖和可选 PDB；
3. 不删除整个游戏 `Mod` 目录；
4. 输出最终路径；
5. 校验构建产物和部署 DLL 的哈希；
6. 提醒 DLL 变更后重启对应进程。

游戏加载 DLL 后，即使磁盘文件能覆盖，当前进程仍运行已加载的旧代码。部署成功不等于热更新成功。

## 10. 日志和诊断闭环

当前常用日志位置：

```text
前端：%USERPROFILE%\AppData\LocalLow\Conchship\The Scroll of Taiwu\Player.log
后端：<游戏目录>\Logs\GameData_*.log
```

后端使用 `GameData.Utilities.AdaptableLog`，并统一标签。详细日志应能回答：

- Patch 是否命中；
- 事件发生前后关键 ID 是什么；
- 枚举到了哪些对象；
- 当前值、目标值和保存值是什么；
- 为什么跳过；
- 是否真正调用了游戏原生修改入口。

推荐诊断流程：

1. 写出 3–5 个可证伪假设；
2. 设计最短复现步骤；
3. 每个假设只加能区分它的日志；
4. 复现一次；
5. 用日志排除，而不是继续猜；
6. 反编译确认真实数据源和时序；
7. 修复后重复完全相同的复现；
8. 删除临时噪声日志，保留可配置的长期诊断信息。

本项目的实际诊断证据链是：

```text
症状：运功二、三仍共享突破预设
  → 日志：Change Patch 命中，绑定保存成功
  → 日志：切换时 switched=0
  → 定向日志：equipped=[]
  → 反编译：当前 ApplyCombatSkillPlan 使用 CombatSkillEquipment
  → 修复枚举源
  → 新日志：能枚举功法，但目标方案 table=<missing>
  → 定位第二个问题：首次初始化晚于全局状态变化
  → Prefix 捕获旧值并补齐缺失绑定
```

一个症状可以同时包含多个缺陷。修掉第一个原因后仍失败，不代表第一处修复无效，应继续比较新旧证据。

## 11. 最低回归矩阵

| 场景 | 必测内容 |
| --- | --- |
| 首次安装、旧存档 | 没有 Mod 数据时的初始化语义 |
| 新游戏 | 空数据、首次触发、默认设置 |
| 修改前后 | Prefix 旧值和 Postfix 新值是否正确 |
| 往返切换 | A→B→A，不只测试单向 |
| 保存并读档 | Archive Data 是否真正持久化 |
| 新增、复制、删除 | 自定义索引是否同步维护 |
| 自动操作 | 自动装备、批量操作是否走旁路 |
| 禁用再启用 | 是否停止应用、旧数据是否保留 |
| 异常路径 | mutation depth、重入标志是否释放 |
| 与同类 Mod 共存 | Patch 顺序、重复调用、同一状态写入 |
| 游戏重启 | 确认加载的是新 DLL，不是内存旧版本 |

涉及存档的功能至少完成“保存 → 退出游戏 → 重新进入 → 读档 → 再验证”。只在同一局内切换不能证明持久化正确。

## 12. 如何评估网络项目和已安装 Mod

检查顺序：

1. 目标游戏版本和最近实际代码提交；
2. 前后端是否分开；
3. 项目目标框架；
4. 引用来自 `Managed` 还是 `Backend`；
5. 是否仍使用当前不存在的基类；
6. Patch 目标在本机 DLL 是否存在且时机一致；
7. Config 的入口、存档和设置声明；
8. 是否有构建、日志或 Workshop 运行证据；
9. 卸载、异常和更新兼容策略。

参考价值分三级：

- **当前实现参考**：当前版本构建并有运行证据；
- **设计模式参考**：工程化或架构值得学习，API 必须重新定位；
- **历史资料**：只用于理解概念和搜索关键词。

当前值得优先查看的公开项目：

- [Wanxiang-Sanctum/taiwu-mods](https://github.com/Wanxiang-Sanctum/taiwu-mods)：多 Mod、共享项目、组包和发布模板；
- [Wanxiang-Sanctum/community-taiwu-mods](https://github.com/Wanxiang-Sanctum/community-taiwu-mods)：复杂 Mod 与配套工具；
- [gruiyuan/taiwu-mod-dev-skill](https://github.com/gruiyuan/taiwu-mod-dev-skill)：当前开发流程和反编译路线；
- [jinqia0/AutoMonthlyGroupMerchantBuy](https://github.com/jinqia0/AutoMonthlyGroupMerchantBuy)：后端 Domain Patch 的小型样本。

这些链接只证明项目存在和提供某类结构，不替代本机兼容验证。

已安装 Workshop Mod 适合回答“当前加载器实际接受什么目录和 Config”“复杂功能如何分前后端”。反编译时优先挑：

- 最近更新；
- 已在当前游戏版本成功加载；
- 与需求同端侧、同领域；
- 体量小、调用链清楚；
- 不依赖一长串私有框架。

## 13. 游戏更新后的兼容审计

每个 Patch 维护一条清单：

```text
端侧 | 目标程序集 | 类型 | 完整方法签名 | Patch 类型 | 依赖状态 | 回归用例
```

游戏更新后：

1. 记录 `Assembly-CSharp.dll`、两侧 `TaiwuModdingLib.dll`、后端 `GameData.dll` 和实际引用 DLL 的 SHA-256；
2. 重新反编译每个 Patch 目标；
3. 检查签名、重载、调用者和状态改变时机；
4. 检查数据源是否仍被当前调用链使用；
5. Transpiler 验证 IL 锚点数量；
6. 编译并检查 Harmony Patch 日志；
7. 跑最低回归矩阵；
8. 最后才更新 `GameVersion` 和发布说明。

“还能编译”不是兼容验证；本项目的旧装备数组接口就是典型反例。

## 14. Definition of Done

一个 Mod 功能只有同时满足以下条件才算完成：

1. 端侧选择正确；
2. Patch 目标和调用时序有当前 DLL 证据；
3. 状态模型包含初始化、更新、复制、删除和持久化；
4. 自动调用不会递归污染；
5. 异常不会破坏原游戏主要操作；
6. 日志能解释“为什么没有生效”；
7. Release 构建和部署产物一致；
8. 完成首次安装、往返切换、保存读档和重启回归；
9. 文档说明兼容范围、冲突点和卸载影响；
10. 游戏更新后有可逐项执行的审计清单。

## 15. 外部资料

- [Harmony 官方文档：Patching](https://harmony.pardeike.net/articles/patching.html)
- [灰机 Wiki：MOD 制作](https://taiwu.huijiwiki.com/wiki/MOD%E5%88%B6%E4%BD%9C)
- [Steam 创意工坊](https://steamcommunity.com/workshop/browse/?appid=838350&l=schinese)

外部资料可能跨越多个游戏架构年代，阅读时始终回到本文第一节的证据顺序。
