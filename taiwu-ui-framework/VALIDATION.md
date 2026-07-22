# Runtime validation

Validated in The Scroll of Taiwu `1.0.58.0` on 2026-07-14 without reading or
writing save data.

- Built provider, sample consumer, and development harness with zero warnings.
- Loaded provider then consumer through the game's real `PluginHelper.LoadPlugin`.
- Observed exactly one loaded `TaiwuUi.Core` assembly; its `Location` is empty,
  matching the game's `Assembly.Load(byte[])` behavior.
- Confirmed the sample consumer directory does not contain `TaiwuUi.Core.dll`.
- Created `TaiwuUi_TaiwuUi_Sample_Hello` under `LayerPopUp` at sort order `600`.
- Confirmed title, Chinese text, close button, content button, and full-cover mask.
- Triggered the content callback and close button through the Taiwu UI probe.
- Confirmed `IsShowing` changes `true -> false -> true` on close and cached reopen.

Visual evidence: `runtime-validation-sample.png`.

## Version 2.0.0 declarative architecture

Validated in the running game on 2026-07-17 through the public declarative API.

- Built the provider, sample consumer, and public contract tests with zero
  warnings and zero errors.
- Mounted the component gallery without restarting the game and exercised the
  checkbox, native filter buttons, slider, sortable table, table tabs,
  navigation, bottom tabs, and encyclopedia icon tabs.
- Confirmed the native sprite states and table sort order change correctly.
- Mounted a second render with the same window key and confirmed controlled
  input state survives the replacement.
- Submitted an invalid replacement containing duplicate sibling keys. Validation
  rejected it before commit, the old window remained usable, and a later valid
  replacement succeeded.
- Read the game error log after the interaction pass and observed zero new
  `Error`, `Assert`, or `Exception` entries.
- Installed the production provider and sample into the local game MOD folders;
  the deployed provider hash matches the release build.

Visual evidence: `runtime-v2-all-elements-interacted.png`,
`runtime-v2-table-tabs-switched.png`,
`runtime-v2-navigation-expanded-selected.png`,
`runtime-v2-bottom-tabs-selected.png`,
`runtime-v2-encyclopedia-icon-tabs.png`, and
`runtime-v2-hot-replacement-rollback.png`.

## Version 0.2.0

- Probed the live resource catalog and confirmed active Chinese UI uses
  `Font SDF GB2312`.
- Resolved native window and button sprites by exact resource name with safe
  color/font fallbacks.
- Verified heading/body/muted text, divider, nested row, primary button, and
  secondary button in the native `UIElement` tree.
- Found and fixed a layout regression where an unspecified flexible height made
  a 54px row consume the remaining 266px.
- Assigned deterministic button node names (`Button_0`, `Button_1`) and invoked
  both callbacks through the UI probe.
- Confirmed the production provider and sample still build with zero warnings.
- Used uniquely named development assemblies because Mono binds repeated
  same-name hot loads to the first version in the process. A normal restart has
  only the production `TaiwuUi.Core` assembly.

Visual evidence: `runtime-validation-0.2-final-stable.png`.

## Version 0.3.0 build validation

- Built `TaiwuUi.Core` with zero warnings and zero errors.
- Rebuilt the existing 0.2 sample consumer without source changes, with zero
  warnings and zero errors.
- Kept `ApiMajor` at 1 and retained AssemblyVersion `0.2.0.0` so the additive
  component package does not force existing consumers across a binary seam.
- Added controlled input, choice, slider, table, selection, sorting, and anchored
  menu implementations without adding a new sample MOD.

The new 0.3 components have compile-time validation only at this point. In-game
layout and interaction validation remains pending.

## Version 0.4.0 native metrics and component contracts

- Measured active native UI text in the game process: 24px was the dominant
  general-text size (220 active instances); 28px and 22px were common heading
  and supporting sizes.
- Confirmed common native action buttons at 320x62 with 24px labels and 184x60
  with 28px labels. The framework standard is 62px high with a 24px label.
- Confirmed `ViewFindMapBlock` uses a 60px title row and 60x60 close sprite, then
  intentionally rendered the framework close control at 44x44 per visual review.
- Added an executable contract test for standard and customized text/button
  options. It verifies defaults and caller overrides after they enter the node
  tree through the public builder interface.
- Hot-loaded `NativeDemoV35` and measured a 920x600 panel with a 900x72
  near-full-width title bar, an independently inset 816x458 content column,
  a 44x44 close control, and 62px standard action buttons.
- Kept the title label aligned with the body column while separating the title
  background from body padding. This matches the native window hierarchy and
  avoids the narrow floating-header appearance.
- Kept `ApiMajor` at 1 and AssemblyVersion `0.2.0.0`; version 0.4 is additive and
  remains binary-compatible with existing consumers.

Visual evidence: `runtime-validation-0.4-layout-final-desktop.png` and
`runtime-validation-0.4-layout-final.png`.

## Version 0.5.0 Encyclopedia navigation

- Captured the live `ViewEncyclopedia` tree and direct game-window image while
  `百晓册` was open.
- Measured native reference geometry at the 2560x1440 UI coordinate scale:
  196x112 top-level icon tabs, 500x1052 side navigation, 400x48 navigation
  items, 52px pinned labels, and an 80px detail title row.
- Hot-loaded measurement assembly `NativeDemoV36`, then iterated disposable
  visual prototypes through V41 without restarting the game.
- Validated three interactive arrangements: semantic icon tabs, two-level
  collapsible navigation, and closable context tabs with detail actions.
- Triggered actual `CButton.onClick` paths through the probe: tab selection,
  switching expanded groups, tag close, favorite, and pin actions.
- Diagnosed and removed a V38 per-frame `NullReferenceException` caused by
  Unity fake-null behavior in prototype keyboard focus detection. V39+ produced
  no new exceptions during the stability window.
- Added production `IconTabs`, `Navigation`, and `ClosableTabs` builder entries,
  controlled models, semantic icon tokens, node sources, and an internal Unity
  navigation renderer.
- The 0.5 contract test covers node creation, default measurements, controlled
  selection/expansion, close requests, and invalid selection/duplicate-key
  errors.
- Production and contract-test builds pass. The two previously recorded nullable
  warnings in `FrameworkView.cs` and `TaiwuTheme.cs` remain unchanged.
- The live screenshots validate the disposable V41 adapter. The newly built
  production provider cannot replace its already loaded assembly until the game
  restarts; run `TaiwuUiDemo.ShowNavigation()` after restart to verify the final
  provider adapter in memory.

Visual evidence: `runtime-validation-0.5-icon-tabs.png`,
`runtime-validation-0.5-navigation.png`, and
`runtime-validation-0.5-tags.png`.

### Icon-tab artwork correction

- Compared the V41 icon-tab prototype directly with the still-open native
  Encyclopedia and confirmed that `ui9_tab_encyclopedia_level_*_0` resources
  are complete tab artworks, not tightly cropped glyphs.
- Replaced the 50x50 glyph container with a full-tab, aspect-preserving,
  non-raycast artwork layer while retaining the existing label, selection
  background, bottom marker, and button hit target.
- Hot-loaded `NativeDemoV42` without restarting the game and confirmed the
  visible symbols now match the native tabs' scale.
- Triggered `PrototypeTab_3` through its actual `CButton.onClick` listener and
  verified the artwork layer has `raycastTarget = false`.
- Production and contract-test builds pass; the two pre-existing nullable
  warnings remain unchanged.

Visual evidence: `runtime-validation-0.5-icon-tabs-v42.png`.

## Version 0.6.0 full-screen presentation and scrolling

- Inspected the live Encyclopedia at 2560x1440 UI scale and captured its
  `ui9_back_background_0` full-screen texture, horizontally scrolling pinned
  labels, `HorizontalLayoutGroup + ContentSizeFitter`, masked vertical content,
  and 20px native scrollbar resources.
- Added `TaiwuWindowPresentation.Encyclopedia`; callers get full-screen native
  background, title/close chrome, cover behavior, and content insets without
  referencing Unity or resource names.
- Changed `ClosableTabs` to a masked horizontal `ScrollRect`, adopted
  `ui9_btn_three_0_0` plus `ui9_sp_btn_second_tap_1`, and added optional
  clear-all intent through `TaiwuTabsModel.ClearRequested`.
- Added the `Scroll` builder entry. Its adapter owns viewport masking,
  self-sizing vertical content, clamped scrolling, and native track/handle
  artwork.
- Extended the contract test with presentation, nested scroll-node options,
  clear requests, and clear-button projection. Production build and tests pass;
  only the two previously recorded nullable warnings remain.
- Hot-loaded unique validation assemblies through `taiwu_ui_scenario` without
  restarting. Root assertion, full-client capture, and new-exception check all
  passed with zero new exceptions.
- Invoked a real close button on the fifth open tab and confirmed the content
  count changed from 12 to 11. Invoked the vertical scrollbar through a pointer
  action and confirmed the article moved from sections 1-7 to sections 2-8.

Visual evidence: `runtime-validation-0.6-encyclopedia-final.png` and
`runtime-validation-0.6-scroll-pointer.png`.

## Version 0.7.0 native table and content tabs

- Studied the live `ViewTaiwuVillagers` roster. Its active table uses a 36px
  native header, explicit column widths, `ui9_btn_table_head_0`,
  `ui9_icon_arrow`, 116px rich rows, a clipped virtual scroll body, and a 20px
  native scrollbar.
- Confirmed the roster's nine 48px secondary tabs use
  `ui9_back_second_toggle_1` and `ui9_btn_second_toggle_2`; different tabs own
  different active column groups.
- Added stable in-framework ordering driven by each sortable column's
  `sortValue`. Contract tests verify ascending rows `2,5,9`, a real sort-cycle,
  and descending rows `9,5,2`.
- Added `TabView`, which binds controlled single selection to complete builder
  content pages. Contract tests verify page construction and selection changes.
- Hot-loaded `TaiwuUi.Core.Dev071` without restarting, invoked the age header,
  and observed visible row values change from ascending to descending. The sort
  arrow became active and no new frontend exceptions were recorded.
- Invoked the status tab and structurally confirmed `Page_0=false`,
  `Page_1=true`, `Page_2=false`; the visible columns changed to mood, favor, and
  injury.

Visual evidence: `runtime-validation-0.7-table-final.png` and
`runtime-validation-0.7-tab-final.png`.

## Version 0.10.0 native sliders and filter buttons

- Studied the live `ViewFindMapBlock` controls and reproduced its 300x56 slider
  track, 56x56 circular handle, 50x50 inner icon, blue progress artwork, and
  44x44 reset glyph.
- Replaced the former two-row range approximation with the game's real
  `RangeSlider`, including two independent thumbs and a draggable middle range.
- Replaced the custom filter-button renderer with the game's normal, hover,
  pressed, disabled, and selected sprite states at the native 114x52 size.
- Hot-loaded `TaiwuUi.Core.Dev103` without restarting. The final inspection
  confirmed the single handle is 56x56, range thumbs and middle fill align, and
  selected filter state uses `ui9_btn_tap_1_4`, with no new Error, Assert, or
  Exception logs.
- `SearchInput` was not changed in this version.

Visual evidence: `runtime-native-slider-filter-dev103.png`.

## Version 0.11.0 native search input

- Studied the live `ViewTaiwuVillagers` search field: 459x56 layout,
  `ui9_btn_three_1_0`/`_1` frame states, 50x50 search icon, 2x42 divider,
  28px TMP input area, and a 64x48 clear-search button.
- Replaced the framework-original search renderer behind the existing controlled
  `SearchInput` interface; callers retain the same value, placeholder, width,
  clear action, and interactable-state contract.
- Hot-loaded `TaiwuUi.Core.Dev111` without restarting and inspected both enabled
  and disabled instances. All native sprites and measurements matched; invoking
  clear after injecting input restored an empty value, and no new Error, Assert,
  or Exception logs were recorded.

Visual evidence: `runtime-native-search-input-dev111.png`.

## Version 0.12.0 native villager table presentation

- Re-inspected the live `ViewTaiwuVillagers` table. Its rows are 116px high and
  use `ui9_back_item_list`, 2px horizontal/vertical separators,
  `ui9_sp_btn_second_tap_1` hover artwork, and `ui9_bg_common_selected` selection
  artwork; body text is centered at about 25px.
- Replaced the remaining custom black/color-tint row implementation while
  retaining the existing table interface, virtual scrolling, controlled sorting,
  selection, disabled rows, and anchored action menus.
- Hot-loaded `TaiwuUi.Core.Dev121` without restarting. The live table exposed the
  exact native row, line, selection, header, and scrollbar sprites; clicking the
  age header changed the first visible value from 18 to 78, and clicking the row
  activated the native selected overlay. No new Error, Assert, or Exception logs
  were recorded.

Visual evidence: `runtime-native-villager-table-dev121-selected-sorted.png`.

## Version 0.9.0 native checkbox and reset/refresh glyph

- Studied `ViewFindMapBlock`: checkbox uses a 78x78 hit area, 50x50 artwork,
  `ui9_icon_switch_type_1_0`, `ui9_icon_switch_type_0_0`, and a separate 48x48
  hover layer. Reset uses a 52x52 `CButton` with refresh sprites `_0` through `_3`.
- Hot-loaded `TaiwuUi.Core.Dev092` without restarting and invoked
  `TaiwuUiDemo.ShowCheckboxAndResetIcons`.
- Pointer/invoke actions toggled both checkbox forms; reset restored their distinct
  defaults and refresh completed without a frontend exception.
- MCP inspection confirmed Row text uses `MidlineLeft` and shares the icon centre
  line after correcting the initial `TopLeft` alignment defect.
- Final screenshot: `runtime-validation-0.9-checkbox-reset-final.png`.

## Version 0.8.0 journal bottom tabs

- Inspected the live `ViewTaskPopUpPanel` while `手记` was open and measured its
  `Bottom` strip: 129px high, `CToggleGroup + HorizontalLayoutGroup`, 224x66px
  items with 24px spacing, and 2x41.32px separators.
- Reused native `ui9_back_lowerpopup_base_2`, `ui9_btn_first_tap`,
  `ui9_sp_btn_second_tap_1`, and `ui9_line_progressbar` artwork behind the public
  selection-only `BottomTabs` interface.
- Contract tests verify default selection and controlled switching through the
  builder-node interface.
- Hot-loaded final assembly `TaiwuUi.Core.Dev081` without restarting, invoked
  `TaiwuUiDemo.ShowBottomTabs`, and performed a real pointer click from `全部` to
  `主线`. Inspection confirmed the old sprite cleared and the new selected sprite
  activated; no new Error, Exception, or Assert logs were recorded.

Visual evidence: `runtime-validation-0.8-bottom-tabs-all.png` and
`runtime-validation-0.8-bottom-tabs-final.png`.
