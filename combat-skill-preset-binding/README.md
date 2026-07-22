# 运功预设绑定｜功法突破预设自动切换

每套运功预设分别记住其中每门功法所使用的突破预设。切换运功预设时，自动恢复对应突破盘、玄机格与玄机之物效果。

## 使用

1. 在游戏 Mod 管理器启用“运功预设绑定”。
2. 进入一套运功预设。
3. 在功法突破界面为相关功法选择所需的突破预设。
4. 切换到另一套运功预设，并设置另一组功法突破预设。
5. 此后切换运功预设时会自动恢复。

首次修改某门功法时，Mod 会先用修改前的突破预设补齐其他尚未绑定的运功方案，再只更新当前运功方案。这样即使其他方案尚未访问，也不会跟随当前方案一起变化。

从运功方案卸下功法不会删除它的绑定；以后重新装入时会立即恢复该方案保存的突破预设。使用游戏的“清空运功方案”操作则会同时清空该方案的全部绑定。

## 实现边界

- 后端单 DLL，不修改 UI。
- Patch `TaiwuDomain.UpdateCombatSkillPlan`，在切换前记录旧方案、切换后应用新方案。
- Patch `TaiwuDomain.ChangeCombatSkillBreakPlate`，修改前捕获旧值、用旧值初始化缺失绑定，修改后只记录当前运功方案的新值。
- Patch 单门功法装备、自动运功与清空方案操作，分别负责立即恢复、批量恢复与重置绑定。
- 从新版 `CombatSkillEquipment` 枚举当前方案的五类功法，不使用已经失效的旧装备数组接口。
- 使用游戏原生 `ChangeCombatSkillBreakPlate` 完成切换，不直接修改突破盘或玄机物品。
- 使用 `ModDomain` 的 archive data 保存绑定。
- 新增、复制、删除运功预设时同步维护绑定索引。

## 构建与部署

```powershell
dotnet build .\CombatSkillPresetBinding.Backend.csproj -c Release
.\deploy.cmd
```

后端日志：

```text
<游戏目录>\Logs\GameData_*.log
```

搜索日志标签：`运功预设绑定`。
