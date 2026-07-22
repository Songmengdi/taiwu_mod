# Taiwu UI Framework

为《太吾绘卷》MOD 提供可复用、贴合游戏原生视觉与交互的 UI 能力。

## Language

**UI 框架 MOD**:
提供窗口、布局、控件、原生视觉和运行时生命周期能力的基础 MOD；当前 interface 可以直接演进，不承担历史消费者兼容。
_Avoid_: UI 库、公共组件包

**消费 MOD**:
使用 UI 框架 MOD 构建自身游戏界面的业务 MOD；当前尚无需要兼容的真实消费 MOD。
_Avoid_: 客户端、调用方插件

**声明式 element tree**:
消费 MOD 通过 props、events 与 children 描述界面，并可将多个 element 封装为可复用的复合 element。
_Avoid_: HTML 模板、Unity 对象树、控件 builder 链

**复合 element**:
消费 MOD 组合多个 element 形成的可复用界面片段，与框架提供的原生 element 使用相同的组合方式。
_Avoid_: 自定义 Unity Prefab、页面 helper
