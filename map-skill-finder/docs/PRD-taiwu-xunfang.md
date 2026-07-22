---
title: 太吾寻访
status: ready-for-agent
created: 2026-07-18
---

## Problem Statement

现有“地图功法寻人”只能在太吾当前地域、当前门派中查询单一功法秘籍。它不能查询其他未被侵袭地域，不能查询技艺书，不能根据逐页正逆与完整状态寻找可由多人共同覆盖的书页组合，也不能按姓名、身份品级、技艺资质或造诣寻人，或按原生商会规则查找商人、商队和商会。现有定位操作还超出了用户期望的信息检索边界。

用户需要一个原生太吾风格、查询边界明确且不会因全域扫描或路径计算拖慢游戏的统一寻访工具。工具应当一次查询一个用户选择的未被侵袭地域，包含未显示地块，返回稳定、可解释、可滚动查看的书籍组合、人物或商会结果，但不提供地图定位、导航、路径、距离、精力或旅行时间计算。

## Solution

将插件重构为“太吾寻访”。全屏“寻访中心”顶部首先提供功法书、技艺书、人物、商会四个主页签，页签内容共享一个紧随其后的两级地域筛选器。一级为门派地域、大城市、其他地域，二级为该类 15 个未被侵袭常规地域；用户也可一键恢复当前地域。筛选变化不自动查询，统一由主查找按钮提交一次地域快照查询，旁边只保留一个重置图标。

功法书和技艺书一次选择一本具体书，并按逐页目标寻找私人藏书和人物背包中的候选副本。组合求解最多使用三名持有人，只展示能够覆盖全部目标页的最少人数层级，并分页展示该层所有去重组合。人物页支持姓名、身份品级、最多三条技艺资质／造诣阈值及少量高级条件。商会页吸收原生商会类型、商会等级、商队状态等筛选，但返回具体实体列表。

## User Stories

1. As a player, I want one “寻访”入口, so that I can search books, people, merchants, caravans, and guilds without learning several tools.
2. As a player, I want a full-screen native-style dialog, so that the feature feels consistent with Taiwu rather than an external debug panel.
3. As a player, I want four clear tabs for 功法书、技艺书、人物、商会, so that each task has focused controls and results.
4. As a player, I want the selected region shared across tabs, so that I do not repeatedly choose the same search boundary.
5. As a player, I want to choose 门派地域、大城市或其他地域 first, so that the region list is understandable rather than a wall of 45 buttons.
6. As a player, I want to choose one concrete region second, so that each search has a bounded and predictable cost.
7. As a player, I want only the 45 uninvaded regular regions offered, so that broken areas are not mixed into ordinary people and commerce searches.
8. As a player, I want undiscovered blocks included, so that search completeness does not depend on map reveal state.
9. As a player, I want a “使用当前地域” shortcut, so that local searches require one click.
10. As a player, I want changing filters not to query automatically, so that several edits produce one backend request.
11. As a player, I want one explicit 查找 action, so that I control when a potentially expensive snapshot is taken without a duplicate refresh icon.
12. As a player, I want old results marked stale after changing region, so that I do not mistake them for current-region results.
13. As a player, I want no map jump, marker, or navigation action, so that the plugin remains an information finder.
14. As a player, I want no path, distance, action-point, energy, or travel-time calculation, so that queries remain fast and do not pretend to be route planning.
15. As a book collector, I want combat-skill selection independent of the selected region's sect, so that I can search any combat book in any region.
16. As a book collector, I want to narrow combat books by sect, category, and concrete book without a separate grade step, so that exact selection remains fast and manageable.
17. As a book collector, I want to narrow life-skill books by category, grade, and concrete book, so that I can find the exact technical book I need.
18. As a book collector, I want one concrete book per query, so that page targets and combination results stay understandable.
19. As a combat-book collector, I want to select one of five outline types or any type, so that the generated book matches my desired outline effect.
20. As a combat-book collector, I want outline state choices of complete, lost, or any, so that filters match states the game can actually generate.
21. As a combat-book collector, I want each normal page to choose direct, reverse, or any direction, so that mixed direct/reverse targets are possible.
22. As a combat-book collector, I want each normal page to choose complete, incomplete, lost, or any state, so that page matching is exact.
23. As a combat-book collector, I want a default target of complete outline and five complete pages with any direction, so that the common “complete book” task is immediate.
24. As a life-skill book collector, I want all five pages to choose complete, incomplete, lost, or any, so that technical-book page conditions are represented faithfully.
25. As a life-skill book collector, I want all five pages to default to complete, so that finding a complete technical book is immediate.
26. As a collector, I want to search private libraries, inventories, or both, so that I can distinguish likely tradeable books from merely held books.
27. As a collector, I want mixed-source combinations, so that a valid solution can use both private-library and inventory copies.
28. As a collector, I want every result to label its source, so that I understand acquisition confidence.
29. As a collector, I want private-library-heavy combinations ranked first within the same holder count, so that practical solutions are easier to act on.
30. As a collector, I want a book copy to contribute all matching target pages, so that the solver does not inflate the number of books or people.
31. As a collector, I want combinations confined to the selected region, so that results never silently mix travel between regions.
32. As a collector, I want a combination to contain at most three holders, so that suggested plans remain practical.
33. As a collector, I want one-holder solutions searched first, so that a single-person complete solution suppresses all larger combinations.
34. As a collector, I want two-holder solutions searched only when no one-holder solution exists, so that results represent the global minimum holder count.
35. As a collector, I want three-holder solutions searched only when no one- or two-holder solution exists, so that expensive combinations are a last resort.
36. As a collector, I want every distinct combination in the minimum holder-count layer available through scrolling, so that I can choose among equivalent people.
37. As a collector, I want duplicate combinations with identical holders and contribution collapsed, so that pagination is not polluted by equivalent assignments.
38. As a collector, I want missing target pages explained when no three-person solution exists, so that failure is actionable.
39. As a collector, I want each combination card to show holders, book sources, copies, and contributed pages, so that the coverage is auditable.
40. As a people finder, I want all living normal characters in the selected region searchable, so that the feature is not limited to people already met.
41. As a people finder, I want Taiwu, dead characters, infected characters, temporary characters, and invalid locations excluded, so that results represent visitable normal people.
42. As a people finder, I want fuzzy name search, so that partial names work.
43. As a people finder, I want identity-grade filters, so that I can seek people at useful ranks.
44. As a people finder, I want up to three life-skill aptitude or attainment conditions combined with AND, so that I can seek specialized candidates.
45. As a people finder, I want each threshold to use greater-than-or-equal comparison, so that boundary values behave like “at least”.
46. As a people finder, I want aptitude thresholds applied to the current real total, so that filtering matches the displayed effective value.
47. As a people finder, I want aptitude displayed as total plus growth adjustment, so that a value such as 92 (+12) explains its composition.
48. As a people finder, I want base aptitude and precocious/late-blooming contribution visible in details, so that I can judge long-term value.
49. As a people finder, I want age, gender, and organization/sect under advanced filters, so that common filters are available without dominating the screen.
50. As a people finder, I want unfiltered listing within one region, so that the page can act as a complete regional roster.
51. As a people finder, I want default ordering by name, so that results are predictable and easy to scan.
52. As a people finder, I want sortable table headers, so that I can inspect grade or selected skill values on demand.
53. As a people finder, I want row selection to update only the detail panel, so that the table does not remount or lose scroll position.
54. As a merchant finder, I want merchant, caravan, and guild target types, so that all three native commerce entities are represented.
55. As a merchant finder, I want the seven native merchant guild types, so that filters use the game's own trade taxonomy.
56. As a merchant finder, I want guild levels one through seven, so that I can seek the appropriate commercial tier.
57. As a merchant finder, I want normal or robbed caravan state, so that endangered caravans are distinguishable.
58. As a merchant finder, I want caravan state controls enabled only when caravans are included, so that irrelevant filters do not mislead me.
59. As a merchant finder, I want caravan state to constrain caravans only when mixed types are selected, so that merchants and guilds remain visible.
60. As a merchant finder, I want normal caravans selected by default, so that routine commerce appears first.
61. As a merchant finder, I want results sorted by level descending, then merchant, caravan, guild, so that the requested priority is stable.
62. As a merchant finder, I want robbed caravans after normal caravans, so that routine options precede exceptions.
63. As a merchant finder, I want concrete entities rather than only matched blocks, so that results explain what was found.
64. As a player, I want result locations shown as text without a locate button, so that information is useful without becoming navigation.
65. As a player, I want paged backend data and virtual scrolling, so that large regional result sets do not freeze or silently truncate.
66. As a player, I want each tab to retain its own filters and results during the dialog session, so that tab switching does not discard work.
67. As a player, I want closing and reopening the dialog to reset the region to current while retaining safe session behavior, so that stale remote assumptions do not persist indefinitely.
68. As a player, I want ordinary searches to target 500 ms and combination searches to have bounded budgets, so that GameData remains responsive.
69. As a player, I want visible loading state after 200 ms and a closeable UI, so that long work is communicated.
70. As a developer, I want malformed requests rejected without crashing GameData, so that the mod fails safely.
71. As a developer, I want stable request versions, so that late asynchronous responses cannot overwrite newer filters or a closed dialog.
72. As a developer, I want declarative keyed UI nodes, so that tables, selection, focus, and scroll remain stable across render updates.
73. As a developer, I want no private Unity template leakage into feature state, so that Taiwu UI Framework remains the visual and lifecycle boundary.
74. As a maintainer, I want automated solver tests and live read-only game verification, so that feature expansion does not regress existing behavior.
75. As a player, I want the four primary tabs at the top, generous spacing, and only page-level plus necessary result-list scrolling, so that the full-screen UI remains calm and readable without nested scroll traps.

## Implementation Decisions

- The visible product is renamed to “太吾寻访”; the button is “寻访” and the window title is “寻访中心”. Existing assembly names and Mod identity remain stable.
- The frontend remains a Taiwu UI Framework 2.0 consumer. It owns immutable screen state and produces keyed declarative documents; the framework owns Unity objects, native visual families, table reconciliation, scroll, focus, and window lifecycle.
- The top-level screen is a four-tab state machine. Region selection is global; tab filters, request versions, loading state, paging cursors, selection, and results are tab-local.
- The primary tab strip is the first control below the window title. Each tab has one page scroll; tables keep their own necessary result scrolling, while adjacent detail panes do not introduce another scrollbar.
- Region metadata is exposed as a small backend catalog. Only regular, uninvaded areas are selectable. Area types map to three product categories: sect, major city, and other. Discovery state is deliberately ignored.
- Queries always accept exactly one AreaId. Backend validation rejects special, broken, or out-of-range areas.
- No query calls map pathfinding, travel-route calculation, or distance/cost APIs. Location text is derived from authoritative character, caravan, settlement, or building data only.
- Filter edits update controlled frontend state and mark results stale. Only explicit submit dispatches a backend request.
- The backend contract is versioned by operation name and returns structured flat SerializableModData envelopes with success, message, elapsed time, page number, page size, total count, and operation-specific rows.
- Book selection metadata is built once per frontend session from combat-skill, life-skill, skill-book, organization, and grade configuration, then cached independently of region queries. Combat selection uses sect, category, and concrete book; life-skill selection retains category, grade, and concrete book. Both queries share one book-candidate model after selection.
- Combat outline labels come from `SkillBreakOutlineEffect.DescShort` (尊本重源、心存正念、守正和中、心向偏门、破例出魔), not the configuration ref names that mirror character behavior types.
- Only the active primary tab mounts its full page tree. Filter and result changes replace that tab's dynamic fragment rather than remounting the complete four-tab window.
- A book target is a small page predicate vector. Combat targets contain an outline predicate and five normal-page predicates; life-skill targets contain five state predicates.
- Outline type uses the game's five behavior types. Outline state offers complete, lost, or any; invalid and normally impossible incomplete outline states are never user options.
- Normal page state uses complete, incomplete, lost, or any. Combat normal pages additionally use direct, reverse, or any.
- Book candidates preserve holder, location, source, copy identity, page types, and page states. Private-library previews use the game's deterministic generation rules and sold-library ledger without leaving temporary items in game state.
- Inventory and private-library sources may be queried independently or together. Each candidate copy computes a compact target-coverage mask.
- Combination solving is a pure module over immutable candidate holders. It checks one holder, then holder pairs, then triples; it stops at the first non-empty holder-count layer. Each holder can contribute multiple copies and pages. Dominated duplicate assignments are canonicalized.
- Minimum-layer combinations are ordered by private-library contribution descending, required copy count ascending, holder name sequence, and stable character IDs. Results are paged without a fixed product cap.
- Person queries scan normal characters in one area, including invisible blocks. Eligibility excludes Taiwu, infected, dead, temporary, and invalid-location records.
- Person ability conditions support one to three life-skill metrics joined by AND. Each condition selects life-skill type, aptitude or attainment, and a 0–999 inclusive minimum.
- Aptitude result projection exposes current total, base value, growth adjustment, and growth type. Filtering uses the current total. Attainment exposes the authoritative current value.
- Person default sort is normalized display name, identity grade, then character ID. The frontend table supports user-selected column sorting.
- Merchant queries mirror native semantics: merchant people, moving caravans, and guild buildings; seven merchant types; merchant/guild level; and caravan robbed state. They scan every block in the selected area regardless of visibility.
- Merchant ordering is level descending, target type merchant/caravan/guild, native guild type, display name, then stable ID. Normal caravans precede robbed caravans.
- There is no locate, clear-marker, jump, route, or navigation command in the new interface or backend contract.
- Ordinary queries have a 500 ms target. Candidate snapshot work remains on the GameData thread; pure combination solving uses compact masks and bounded one-area input. The UI communicates loading and rejects stale responses.
- Existing source compatibility is not required. The old current-sect-only backend methods and legacy locate behavior may be removed.

## Testing Decisions

- Tests assert externally visible contracts and domain outcomes rather than private implementation structure.
- The highest automated seam for combination behavior is a pure solver contract: candidate holders plus page target produce the minimum non-empty holder layer, canonical combinations, stable order, and missing-page mask.
- Solver tests cover one-holder suppression, multiple two-holder results, two-holder suppression of triples, multiple three-holder results, over-three failure, multiple copies per holder, mixed sources, source preference, duplicate collapse, combat directions, outline types, all page states, and life-skill pages.
- Backend contract tests validate area rejection, selectable-area projection, paging envelopes, filter defaults, and safe error responses without needing frontend Unity objects.
- Frontend document tests validate stable keys, four tabs, shared region state, stale-result transitions, no locate action, controlled filters, and table/detail independence through Taiwu UI Framework's validation and update-preview seams.
- Existing framework contract tests and the current map-finder hot-load entry are prior art. They remain the preferred seams for declarative validation and no-restart game verification.
- Live MCP verification uses the current save read-only: open the window, switch all four tabs, change both region filter levels, submit representative queries, select and sort rows, scroll results, close/reopen, capture screenshots, and assert no new frontend exceptions.
- Live backend verification checks response timing and confirms no path or travel APIs are invoked by observing code contracts and representative timings; it does not mutate inventory, characters, caravans, map visibility, or save data.
- If the save lacks a one-, two-, or three-holder real combination, synthetic solver tests establish correctness and the live UI verifies the explicit missing-page state.

## Out of Scope

- Full-world or multi-region scans.
- Cross-region book combinations.
- More than three holders in one book combination.
- Map markers, map jumps, navigation, automatic movement, route planning, distance sorting, action-point estimates, energy estimates, travel days, money costs, or authority costs.
- Searching several concrete books in one request.
- Searching merchant inventories or guaranteeing the goods currently sold.
- Person relationship, marriage, reincarnation, mood, health, injury, poison, pregnancy, trait, favorability, or combat-skill aptitude filters.
- Continuous tracking of moving characters or caravans.
- Automatic queries on every filter edit.
- Mutating game data to manufacture test fixtures.
- Backward compatibility with the pre-“太吾寻访” source interface.

## Further Notes

- Game version inspected during design: 1.0.58.
- Skill-book page states are Complete = 0, Incomplete = 1, Lost = 2, Invalid = -1. Combat outlines have five behavior types. Normal generation forces outlines complete; exceptional generation can make them lost but not incomplete.
- The native map-block finder scans only visible blocks in the current area and returns locations. This product intentionally uses its own one-area entity queries to include invisible blocks and return concrete results.
- The repository currently has no configured remote issue tracker. This document is the authoritative local publication and carries the required `ready-for-agent` status.
