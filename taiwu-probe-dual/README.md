# TaiwuProbe

太吾绘卷调试探针。在游戏内启动端口服务，通过上下文协议工具(MCP)给 AI 代理调用，实现远程操控和反射求值。

- Steam 创意工坊：<https://steamcommunity.com/sharedfiles/filedetails/?id=3753415740>

## 本地双端扩展

本分支在原有 Unity 前端探针之外增加 `TaiwuProbeBackend.dll`。AI 客户端仍只连接
`localhost:13131/mcp`。前端通过仅监听本机的内部 HTTP 桥 `localhost:13132/probe`
转发后台请求；后端 HTTP 线程只负责入队，由 GameData 主循环在主线程消费请求。
若 13132 被上一轮残留的同名太吾进程占用，后端会定位 HTTP.sys 请求队列、终止残留进程并立即接管端口；
当前进程或非太吾进程不会被终止。接管失败时仅停用后端 MCP，不再阻断 GameData 加载。
因此工具在主菜单和进入存档后都可用，并且不会从 HTTP 工作线程直接访问 Domain 状态。

新增工具：

- `taiwu_backend_ping`：检查后端插件、进程、主线程及 Domain 初始化状态。
- `taiwu_backend_eval`：只读反射求值，支持读取 `DomainManager` 字段、属性及安全查询方法。
- `taiwu_backend_type_search`：搜索后端已加载类型。
- `taiwu_backend_members`：查看字段、属性和完整方法签名。
- `taiwu_backend_log_tail`：读取最新的 `GameData_*.log`。
- `taiwu_backend_csharp`：在 GameData 主线程编译执行任意 C# 方法体，并返回结果。

## 0.5 Agent 向 UI 工具（snapshot → ref → action）

0.5 重做 UI 工具面，核心消费者从人改为 Agent，借鉴 agent-browser 的
snapshot/ref 范式。旧的 `ui_list` / `ui_tree` / `ui_click`(坐标/名称版) / `ui_trigger` /
`ui_inspect` / `ui_action` 全部退役（`ui_scenario` 内部的 action/assert 引擎保留）。
所有新工具把核心信息放进文本 content，structuredContent 仅作冗余。

- `taiwu_ui_snapshot`：主入口。输出场景摘要（场景/顶层窗口/其他层/地图装饰计数）+
  顶层窗口语义树：每个可交互节点带 `@eN` ref、角色（button/toggle/slider/rangeslider/
  input/scroll/clickable）、文字、状态（on/off、slider 值、input 当前值）。
  同 scope 再次调用在头部附带与上次的变化 diff。`scope` 可选 `all` 或层名/窗口名。
- `taiwu_ui_click`：点击 `@eN` 或 selector（逃生舱）。pointer 模式做遮挡检查并报出
  遮挡者；invoke 直接调 Button/Toggle。返回结果 + 动作后 diff。ref 失效报 `stale_ref`。
- `taiwu_ui_fill`：向 input 填文本并提交（onEndEdit），游戏真实响应。
- `taiwu_ui_toggle`：显式设 on/off，幂等。
- `taiwu_ui_hover`：优先走游戏原生 `TooltipInvoker.ShowTips()`（pointerEnter 模拟会被
  真实鼠标轮询刷掉），等待若干帧后返回 tooltip 全文。
- `taiwu_ui_scroll`：滚动 ScrollRect，返回位置变化 + 内容 diff。
- `taiwu_ui_wait`：等文字/selector 出现或消失，或纯毫秒；带超时。
- `taiwu_ui_describe`：单点深挖——组件全限定类名+所在程序集（反编译定位）、rect/屏幕
  边界、父链组件摘要、可选子树。配合 `taiwu_eval` 形成 snapshot → describe → eval/反编译
  的调试链。

设计依据与实测结论见 `scratch/ui-snapshot-proto/NOTES.md`。

## 0.4 保留的验证工具

- `taiwu_ui_screenshot`：在 `WaitForEndOfFrame` 后捕获最终游戏帧，以 MCP image content
  直接返回 PNG；支持完整游戏画面和按元素裁剪。
- `taiwu_hotload_invoke`：从 DLL 字节加载程序集、检测 Mono 同名冲突、调用静态入口并
  返回 SHA-256 与调用结果。
- `taiwu_frontend_log`：建立日志 cursor，只读取本轮操作之后新增的前端日志。
- `taiwu_ui_scenario`：一次编排热加载、动作、结构化断言、异常检查和截图
  （内部 action/assert 仍用 selector 引擎）。

单次热验证示例：

```json
{
  "assembly": {
    "assembly_path": "E:/work/NativeDemoV42.dll",
    "type": "HotDemoV42.NativeDemo",
    "method": "EncyclopediaPrototype",
    "wait_frames": 2
  },
  "steps": [
    {
      "action": {
        "selector": { "path": "PrototypeStage/PrototypeTab_3" },
        "mode": "invoke",
        "wait_frames": 1
      }
    },
    {
      "assert": {
        "selector": { "path": "PrototypeStage/PrototypeTab_3/Artwork" },
        "property": "image.raycastTarget",
        "equals": false
      }
    }
  ],
  "capture": {
    "target": "game_client",
    "wait_frames": 2,
    "save_path": "E:/work/runtime-validation.png"
  },
  "fail_on_new_exceptions": true
}
```

0.4 验证工具使用标准 MCP `structuredContent`；失败时同时设置 `isError`。截图还会返回标准
`image/png` content，不需要再借助 PowerShell、前台窗口或 `PrintWindow`。
注意：部分 MCP 客户端不向模型展示 structuredContent，0.5 的 Agent 向工具因此一律把
核心信息放在文本 content。

后端 eval 不支持 `value:` 写入，仅允许调用以 `Get`、`Is`、`Can`、`Has`、
`Contains` 或 `ToString` 开头的查询方法。方法参数支持 string、数值、bool、enum、
null 和可选参数；`DataContext` 参数由执行器自动注入。暂不支持 ref/out、泛型及复杂对象参数。

### 任意 C# 查询

`taiwu_backend_csharp` 使用 Roslyn 将传入内容包装为
`object? Execute(DataContext context)`，因此代码必须通过 `return` 返回结果。默认可直接使用
`DomainManager`、LINQ 和变量，也可通过 `usings` 参数加入额外命名空间。

```csharp
var taiwuId = DomainManager.Taiwu.GetTaiwuCharId();
var items = DomainManager.Taiwu.GetTaiwuAllItems(context);
return new
{
    taiwuId,
    itemCount = items.Count,
    firstItems = items.Take(5).Select(x => x.ToString()).ToArray()
};
```

这是任意进程内代码执行，不是真正的只读沙箱。代码能够修改 Domain、访问文件、启动进程，
同步死循环还会永久阻塞 GameData 主线程。只运行可信查询代码；日常探索优先使用只读
`taiwu_backend_eval`。网页带 `Origin` 的请求会被 MCP 服务拒绝，降低浏览器跨源调用风险。

本地安装时应禁用创意工坊原版探针，避免两个前端插件同时监听 13131 端口。
