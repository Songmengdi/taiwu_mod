# Taiwu UI Framework 2.0

《太吾绘卷》MOD 的声明式原生 UI 框架。消费 MOD 用 C# element tree 描述
界面，框架负责验证、受控状态、原生 Sprite、布局、差异更新和 Unity 生命周期。

## Quick start

```csharp
var query = new TaiwuValue<string>(string.Empty);
var enabled = new TaiwuValue<bool>(true);

UiWindow document = new(
    ownerId: "My.Mod",
    windowId: "villagers",
    content: Ui.Column(
        Ui.SearchInput(query, "输入姓名") with { Key = "search" },
        Ui.Checkbox(enabled, "显示隐藏地格") with { Key = "hidden" },
        Ui.Button("刷新", Refresh) with { Key = "refresh" })
        with { Key = "root" },
    title: "村民名册",
    width: 1280f,
    height: 820f);

UiValidationResult validation = TaiwuUiApi.Validate(document);
if (!validation.IsValid)
    throw new UiValidationException(validation.Errors);

ITaiwuWindow window = TaiwuUiApi.Mount(document);
window.Show();
```

## Composition

复合 element 是返回 `UiElement` 的普通纯函数：

```csharp
static UiElement VillagerFilters(FilterState state) => Ui.Column(
    Ui.SearchInput(state.Query, "输入姓名") with { Key = "search" },
    Ui.RangeSlider("区域", state.Region, 0f, 100f) with { Key = "region" },
    Ui.FilterButtons("资质", state.Qualifications, state.Options)
        with { Key = "qualifications" });
```

消费 MOD 持有业务状态；框架只保留焦点、滚动位置等瞬时 UI 状态。

## Elements

- Layout: `Column`, `Row`, `Flex`, `Dynamic`, `Scroll`, `Spacer`, `Divider`
- Text/actions: `Text`, `Heading`, `Muted`, `Button`
- Forms: `SearchInput`, `Checkbox`, `Slider`, `RangeSlider`
- Filters/actions: `FilterButtons`, `PopupSelect`, `PopupCard`, `ResetIcon`, `RefreshIcon`
- Navigation: `IconTabs`, `ClosableTabs`, `Tabs`, `BottomTabs`, `Navigation`
- Data: virtual sortable `Table`
- Advanced: typed `NativeImage(NativeAssetRef, width, height)`

All elements support `with { Key = "stable-key" }`. Stable keys are required for
dynamic collections and recommended for stateful elements.

## Controlled state

`TaiwuValue<T>` provides value changes, reset, and interactable state.
`TaiwuSelection<T>` supports single or multiple selection. Native elements
project these values and emit user intent back into the same controlled state.

## Keyed updates

```csharp
UiUpdatePreview preview = TaiwuUiApi.PreviewUpdate(current, next);
window.Render(next);
```

`PreviewUpdate` reports `Reused`, `Replaced`, `Added`, and `Removed` paths.
`Render` validates first. Invalid input leaves the mounted window intact. Valid
updates retain focus and scroll state for matching key/type paths.

## Native appearance

Built-in elements use semantic native visuals. Sprite names and fallback rules
do not cross the public interface. `NativeAssetRef` is the explicit escape hatch
for advanced game resources not yet represented by a standard element.

## Consumer project

```xml
<Reference Include="TaiwuUi.Core"
           HintPath="path/to/TaiwuUi.Core.dll"
           Private="false" />
```

The consumer `Config.lua` declares FileId `990058100` in `Dependencies`. It must
not ship a private copy of `TaiwuUi.Core.dll`.

## Local commands

```powershell
dotnet build .\TaiwuUi.Core.csproj
dotnet build .\sample\TaiwuUi.Sample.csproj
dotnet run --project .\tests\TaiwuUi.Core.ContractTests.csproj
.\install-local.ps1
```

The sample toggles its v2 declarative window with `F10`.

## Versioning

- `ApiMajor = 2`
- `ApiVersion = 2.0.0`
- v0.x builder compatibility is intentionally removed.

See [ARCHITECTURE.md](ARCHITECTURE.md) and [VALIDATION.md](VALIDATION.md).
