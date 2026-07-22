# Architecture

## Public interface

Consumer MODs describe windows as immutable C# declarative element trees. Each
`UiElement` carries props, events, children, and an optional stable `Key`. Pure
functions compose framework elements into consumer-owned compound elements.
Unity objects and native resource names never cross this seam.

`TaiwuUiApi.Validate` and `TaiwuUiApi.Mount` consume the same `UiWindow`
interface. Tests exercise the caller's test surface rather than reflecting
private render nodes.

## Declarative render plan module

`UiRenderPlanCompiler` validates identity, geometry, element constraints, and
sibling keys before producing an immutable render plan. Invalid plans never
reach Unity. Dynamic unkeyed children remain valid but produce a diagnostic.

## Reconciliation seam

`PreviewUpdate` compares two documents with the same window key. Matching path
and element type means reuse; a type change at the same key means replacement.
Added and removed paths are explicit.

The window host validates before changing a mounted window, creates the next
view first, restores focus and scroll only for matching keyed element types, and
then destroys the previous Unity tree. Construction failure restores the old
definition, source document, UI element, and view.

## State projection module

`ElementStateProjection` is the single seam between controlled state and native
rendering. Its small interface is snapshot, intent dispatch, change notification,
and disposal. Its implementation owns generic type erasure, stable table sort,
selection projection, interactable state, subscriptions, and cleanup.

The former five one-adapter `*NodeSource` interfaces were removed. Renderers
consume typed snapshots and emit intents; they do not reimplement state rules.

## Native UI family modules

- `SearchInputFamilyModule`
- `CheckboxFamilyModule`
- `ActionIconFamilyModule`
- `FilterFamilyModule` (filter buttons and single/range sliders)
- `PopupSelectFamilyModule` (compact single selection in a floating native panel)
- `PopupCardFamilyModule` (single-layer cascading choice card with wrapped, scrollable choices)
- `FrameworkNavigationRenderer` (tabs and navigation)
- `FrameworkTableRenderer` (virtual rows, sorting, selection, menus)
- `FrameworkView` (window chrome, basic layout, scroll, page hosts)

`FrameworkComponentRenderer` is only a router. Each native family owns its
definition projection, Unity implementation, visual behavior, and cleanup.

## Native visual catalog

`TaiwuTheme` is the native visual catalog. Renderers request semantic visuals;
sprite names, SpriteSwap state, fonts, fallback colors, and resource lookup stay
inside it. Standard elements never accept raw resource strings.

`NativeAssetRef` is the explicit advanced escape hatch for a game asset the
framework has not yet absorbed. Table icons and native images use this typed
reference instead of leaking string resource knowledge.

## Window host

`TaiwuUiRuntime` owns mounted windows by stable `ownerId:windowId`. Mounting an
existing key performs a validated update. `FrameworkWindow` owns placement in
Taiwu's `UIManager`, visibility, atomic replacement, rollback, and destruction.

The public lifecycle is `Mount`, `Render`, `Show`, `Hide`, `Toggle`, and
`Dispose`. Unity reflection and `UIElement._path` remain internal.

## Distribution

`TaiwuUi.Core` is a provider MOD. Consumers declare FileId `990058100`, reference
the DLL with `Private=false`, and do not ship a private copy. Version 2 removes
the v0.x builder interface; no real consumer MOD required compatibility.
