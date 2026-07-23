---
name: taiwu-steam-publish
description: 准备、校验并指导发布本仓库《太吾绘卷》MOD 到 Steam 创意工坊，覆盖 Taiwu UI Framework 与太吾寻访的版本同步、上传包、游戏内更新及订阅者更新。Use when the user asks to publish, upload, release, update a Steam Workshop MOD, or prepare release notes.
---

# 太吾 Steam 创意工坊发布

适用对象是 Taiwu UI Framework 与太吾寻访的一次联合发布。先阅读 [REFERENCE.md](REFERENCE.md)；其中包含完整路径、包内容和面向玩家的说明。

## 发布前

1. 查看 `git status`，确认本次改动范围；不要将未确认的他人改动混入发布。
2. 确定两个新版本号。更新对应 `Config.lua` 的 `Version`，并同步各 `.csproj` 的 `Version` 与 `FileVersion`。
3. 仅在破坏二进制兼容性时修改 `AssemblyVersion`。普通增量组件应保留它，避免依赖旧框架的 MOD 加载失败。
4. 在 `Config.lua` 的 `UpdateLogList` 追加当前 Unix 时间戳；保留既有 `FileId`、`Dependencies` 与 `Source`。
5. 新建本次 Steam 更新说明，提供一段可粘贴的“更新简述”和完整条目。不要把内部调试过程写入玩家说明。

## 构建、同步与校验

从仓库根目录运行：

```powershell
.\.agents\skills\taiwu-steam-publish\scripts\Prepare-SteamRelease.ps1
```

脚本按依赖顺序构建框架、太吾寻访前端和后端，再同步到 `publish/` 上传包及游戏 `Mod/` 目录。上传包和 DLL 以 SHA-256 校验；游戏会重写本地清单的格式与更新日志，因此安装清单校验其 `Version` 字段。

完整命令会写入游戏 `Mod/` 目录，必须先完全退出游戏；否则运行中的游戏可能保留旧清单或随后写回其内存状态。脚本完成后再启动游戏。

只检查当前状态、不写入文件时运行：

```powershell
.\.agents\skills\taiwu-steam-publish\scripts\Prepare-SteamRelease.ps1 -VerifyOnly
```

构建前仍须运行相关契约/领域测试。若后端出现既有警告，记录它；新警告或错误必须先处理。

## 游戏内上传

1. 退出并重启游戏，使它重新读取 `Mod/<名称>/Config.lua`。
2. 打开“模组管理 → 上传模组”，确认界面显示的新版本号。
3. 先选中并更新 `Taiwu UI Framework`，再更新“太吾寻访”。使用既有项目的“更新”，不要新建项目。
4. 粘贴对应更新说明中的“更新简述”，确认程序文件和依赖关系正确后上传。
5. 上传完成后在 Steam 页面确认版本、更新时间和依赖关系。

若上传界面显示旧版本，绝不要点击“保存”或“还原”；先退出游戏并运行 `-VerifyOnly` 定位是缓存还是目录不同步。

## 交付

报告两个版本号、测试/构建结果、两个上传目录和更新说明文件。除非用户明确要求，不执行 git 提交或 Steam 上传后的公开性变更。
