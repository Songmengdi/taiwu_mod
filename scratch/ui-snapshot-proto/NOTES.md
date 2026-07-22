# UI Snapshot 原型 — 实测结论（2026-07-21）

热加载原型（Proto1~Proto6）在运行中的游戏上验证了 snapshot→ref→action 范式。以下结论已固化进正式插件实现。

## 验证通过的机制

- **窗口检测**：`Camera_UIRoot/Canvas` 的直接子节点是层（LayerBack/Main/Part/PopUp/VeryTop/Tips/…），按 Canvas.sortingOrder 排序；顶层窗口 = 排序最高的、含有意义窗口（≥1 交互或文本后代）的层。
- **地图装饰**：世界地图上的商户/蛐蛐图标是 LayerBack 子树内的嵌套 Canvas（本次实测 56 个子 Canvas / 3612 节点），必须折叠为一行摘要，绝不进树。
- **角色推断**（按组件，无需硬编码游戏类）：TMP_InputField→input（带当前值）、Toggle 基类→toggle（CToggle/SwitchToggleSmall 都继承 Unity Toggle，isOn 可读可写）、Slider→slider（value/max）、类型名 "RangeSlider"→rangeslider（反射 LowerValue/UpperValue/MinValue/MaxValue）、Button 基类→button、ScrollRect→scroll、UIInteractionBehaviour/PointerTrigger→clickable。
- **ref 失效检测**：GameObject 销毁（Unity 假 null）+ FullPath 比对 + activeInHierarchy，三种都能给出"请重新 snapshot"的可执行错误。
- **diff**：对两次 snapshot 的规范化行（role|text|state）做多重集 diff，准确反映游戏响应（实测：Fill 年龄 18 → 滑条联动 + 结果数 2→37）。
- **SetToggle**：`Toggle.isOn = x` 直接赋值即可触发 CToggle 状态流转，幂等。
- **Fill**：`TMP_InputField.text = x` + `onEndEdit.Invoke(x)`，游戏真实响应（联动滑条与搜索）。

## 关键坑（新发现，此前无记录）

1. **tooltip 不能用 pointerEnter 模拟触发**：游戏每帧用真实鼠标位置轮询，模拟的 pointerEnter 会被立刻刷掉。必须找目标自身/子级/父级的 `TooltipInvoker` 组件，反射调 `ShowTips()`（返回 bool）。tooltip 文本从 `Camera_UIRoot/Canvas/LayerTips` 下的 TMP_Text 收集。
2. **hotload 的 returnValue 与所有 structuredContent 在 kimi MCP 客户端不可见**：原型因此把所有结果写文件再 Read。正式插件必须把核心信息放进文本 content，structuredContent 只做冗余。
3. **diff 必须绑定 scope**：跨 scope 的两次 snapshot 做 diff 会产生全量 +/- 噪音。scope 变化时跳过 diff。
4. **热迭代换 AssemblyName 会丢 ref 缓存**（静态字段随程序集）：原型限制，正式插件无此问题；但意味着 ref 缓存失效场景（游戏重启/场景切换）要能优雅报 stale。
5. **标题提取**：优先找名字含 "Title" 的子节点下的 TMP_Text，富文本标签（`<color=...>`）必须 strip 后再判空，否则得到空标题；兜底用 GameObject 名。
6. **label 消费**：交互节点的文本来自其子级 TMP_Text 时，要把该 TMP 实例记入"已消费"，否则 label 会以 text 行重复出现；input 要消费全部子级 TMP（placeholder 是噪音）。
7. **slider/rangeslider/input 的子树不展开**（handle、placeholder 等内部结构纯噪音）。

## 待正式版验证

- ui_wait（协程轮询，原型无法等帧）
- ui_scroll（verticalNormalizedPosition 直接赋值，逻辑简单未实测）
- ui_screenshot 的 annotate（v1 只返回 ref 屏幕坐标清单，不做图上叠字）
- 遮挡检查（PointerClick 已实现"最上层命中者"报错，未实测遮挡场景）

## 正式版验收发现（2026-07-22，游戏重启后实测）及修复

1. **ComputeDiff 层解析 bug（已修）**：`BuildSnapshot` 按 sortingOrder 排序选顶层，而 `ComputeDiff` 按
   transform 原始顺序取第一个有意义层——两者不一致导致动作后 diff 把别的层内容当成"变化"
   （误判"窗口关闭"）。修复：抽出 `CollectInteractiveLayers`/`ResolveScope` 共用。
2. **跨层窗口变化在 scope 内不可见（已修）**：在 LayerPart scope 点击打开 LayerPopUp 窗口，
   diff 显示"无可见变化"。修复：层/窗口行（`layer|层名|窗口标题`）进入签名，任何 scope 的
   diff 都能反映窗口开关。
3. **hover 的 TooltipInvoker 查找退化（已修）**：固化时把原型的递归 `GetComponentInChildren`
   改成了只查直接子节点，导致 hover 找不到 invoker 回退 pointerEnter（tooltip 为空）。
   已恢复递归查找（按类型名跨程序集找 Type，注意 `asm.GetTypes()` 要 try/catch）。
4. **CScrollRect 不继承 Unity ScrollRect（已修）**：角色推断漏掉 scroll 角色；`ui_scroll` 也
   找不到目标。修复：类型名以 "ScrollRect" 结尾即视为 scroll 角色，滚动用反射读写
   `verticalNormalizedPosition`。
5. **遮挡检查按设计生效（实测）**：ViewObtain 的全屏 Bg 按钮中心被内容卡片覆盖时，报
   `FAIL: 目标被遮挡（最上层命中: …/Content/0）`。此类"点背景关闭"的 UI 应改用
   `mode:invoke`（直接 onClick，绕过 raycast），实测可关。
6. **验收通过项**：snapshot 对未见过的窗口类型（太吾月报 ViewMonthNotify、ViewObtain）开箱
   可用（标题/disabled/toggle 状态正确）；fill 联动游戏真实数据；toggle on/off 幂等；
   describe 输出组件全限定名+程序集+父链（反编译链完整）；ui_wait 文字出现/消失正常。
7. **CScrollRect 滚动 API（已查明并实现）**：无可写 verticalNormalizedPosition；
   `ScrollBar.value` 写入会被 `UpdateScrollBarValue()` 每帧回刷。正确姿势是
   `ScrollTo(Vector2 targetAnchorPosition, float duration)`：down = content.anchoredPosition.y 增加，
   内部自动 clamp（实测 contentH=458/viewportH=354 时 y:0→104 到底）；duration=0 也是阻尼滚动，
   立即读值看不到变化，动作后 wait_frames 再 diff 即可。另有 `ScrollTo(RectTransform, float)`
   可实现 scrollintoview（未做）。
8. **hover 读到"别人的"tooltip（已知现象）**：TooltipInvoker 链上找到的 invoker 其 RuntimeParam
   绑定的内容不一定等于 ref 节点语义（实测资源栏 hover 出地格提示）。机制本身可用，
   但 tooltip 内容归属需要按场景验证，必要时用 describe 看 RuntimeParam。
