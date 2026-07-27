using MapSkillFinder.Domain;
using MapSkillFinder.Frontend;

static BookCopyCandidate Book(string id, BookSource source, params int[] pages)
{
    ulong mask = 0;
    foreach (int page in pages) mask |= 1UL << page;
    return new BookCopyCandidate(id, source, 0, 0, mask);
}

static BookHolderCandidate Holder(int id, params BookCopyCandidate[] books) =>
    new(id, $"人物{id}", 1, (short)id, "门派", 4, books);

static void Equal<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"{message}: expected={expected}, actual={actual}");
}

static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual, string message)
{
    if (!expected.SequenceEqual(actual))
        throw new Exception($"{message}: expected=[{string.Join(',', expected)}], actual=[{string.Join(',', actual)}]");
}

const ulong all = 0b11_1111;

var one = BookCombinationSolver.Solve(new[]
{
    Holder(1, Book("one", BookSource.Inventory, 0, 1, 2, 3, 4, 5)),
    Holder(2, Book("half-a", BookSource.PrivateLibrary, 0, 1, 2)),
    Holder(3, Book("half-b", BookSource.PrivateLibrary, 3, 4, 5)),
}, all);
Equal(1, one.HolderCount, "one holder suppresses larger combinations");
Equal(1, one.Combinations.Count, "only the one-holder combination remains");

var pairs = BookCombinationSolver.Solve(new[]
{
    Holder(1, Book("a", BookSource.Inventory, 0, 1, 2)),
    Holder(2, Book("b", BookSource.Inventory, 3, 4, 5)),
    Holder(3, Book("c", BookSource.PrivateLibrary, 0, 1, 2)),
}, all);
Equal(2, pairs.HolderCount, "pair layer selected");
Equal(2, pairs.Combinations.Count, "all distinct pairs are retained");
Equal("2-3", pairs.Combinations[0].StableKey, "private-library-heavy pair sorts first");

var triples = BookCombinationSolver.Solve(new[]
{
    Holder(1, Book("a", BookSource.Inventory, 0, 1)),
    Holder(2, Book("b", BookSource.Inventory, 2, 3)),
    Holder(3, Book("c", BookSource.Inventory, 4, 5)),
    Holder(4, Book("d", BookSource.PrivateLibrary, 4, 5)),
}, all);
Equal(3, triples.HolderCount, "triple layer selected");
Equal(2, triples.Combinations.Count, "all distinct triples are retained");

var impossible = BookCombinationSolver.Solve(new[]
{
    Holder(1, Book("a", BookSource.Inventory, 0)),
    Holder(2, Book("b", BookSource.Inventory, 1)),
    Holder(3, Book("c", BookSource.Inventory, 2)),
    Holder(4, Book("d", BookSource.Inventory, 3)),
}, all);
Equal(false, impossible.Success, "more-than-three holder solution is rejected");
Equal(0b11_0000UL, impossible.MissingMask, "missing pages are reported");

var multipleCopies = BookCombinationSolver.Solve(new[]
{
    Holder(1,
        Book("a", BookSource.PrivateLibrary, 0, 1, 2),
        Book("b", BookSource.Inventory, 3, 4, 5)),
}, all);
Equal(1, multipleCopies.HolderCount, "multiple copies owned by one holder remain one-holder solution");
Equal(2, multipleCopies.Combinations[0].BookCount, "both required copies are selected");

Console.WriteLine("MapSkillFinder domain contracts passed.");

// ---- Area search: local first, fallback results ordered by count ----

Equal(true, AreaSearchPlan.ShouldLoadCatalog(hasCatalog: false),
    "first open loads the area catalog");
Equal(false, AreaSearchPlan.ShouldLoadCatalog(hasCatalog: true),
    "reopen reuses the area catalog");
Equal(false, AreaSearchPlan.HasDateChanged(100, 100),
    "same date preserves query caches");
Equal(true, AreaSearchPlan.HasDateChanged(100, 101),
    "month advance invalidates query caches");
Equal(false, AreaSearchPlan.HasDateChanged(0, 101),
    "unknown old date does not cause a false invalidation");

SequenceEqual(new short[] { 5, 1, 3 }, AreaSearchPlan.BuildSearchOrder(new short[] { 1, 3, 5 }, 5),
    "current area is searched before every other area");
SequenceEqual(new short[] { 1, 3 }, AreaSearchPlan.BuildSearchOrder(new short[] { 1, 3, 1 }, 5),
    "missing current area does not invent an invalid query");
var orderedAreas = AreaSearchPlan.OrderByResultCount(
    new[] { (AreaId: (short)3, Count: 2), (AreaId: (short)1, Count: 5), (AreaId: (short)2, Count: 5) },
    item => item.Count, item => item.AreaId);
SequenceEqual(new short[] { 1, 2, 3 }, orderedAreas.Select(item => item.AreaId),
    "region sheets sort by result count then stable area id");
Equal(false, AreaSearchPlan.ShouldStartAfterCatalog(
    hasSearchCriteria: true, cacheValid: true, searchInFlight: false),
    "same-month result cache suppresses reopen search");
Equal(true, AreaSearchPlan.ShouldStartAfterCatalog(
    hasSearchCriteria: true, cacheValid: false, searchInFlight: false),
    "invalidated result cache searches after reopen");
Equal(false, AreaSearchPlan.ShouldStartAfterCatalog(
    hasSearchCriteria: true, cacheValid: false, searchInFlight: true),
    "in-flight map search is never duplicated");
Equal(false, AreaSearchPlan.ShouldStartAfterCatalog(
    hasSearchCriteria: false, cacheValid: false, searchInFlight: false),
    "missing search criteria does not search");
Equal(false, AreaSearchPlan.ShouldSearchBeyondCurrentArea(
    currentAreaHasResults: true, forceFullMap: false),
    "a local hit keeps the default search local");
Equal(true, AreaSearchPlan.ShouldSearchBeyondCurrentArea(
    currentAreaHasResults: true, forceFullMap: true),
    "manual full-map search continues after a local hit");
Equal(true, AreaSearchPlan.ShouldSearchBeyondCurrentArea(
    currentAreaHasResults: false, forceFullMap: false),
    "a local miss falls back to the full map");
Console.WriteLine("Area search contracts passed.");

// ---- TaiwuPageMarking: per-page 已拥有/已读 coverage marks ----

// Combat knowledge: page 0 read outline type 2; page 1 owns 完整正页(bit0) and
// read 逆页(bit1) → both directions covered; page 2 owns 完整逆页 only.
var combat = new TaiwuBookKnowledge(Combat: true,
    OwnedTypeMasks: new[] { 0, 0b01, 0b10, 0, 0, 0 },
    ReadTypeMasks: new[] { 0b100, 0b10, 0, 0, 0, 0 });
Equal(true, TaiwuPageMarking.IsVariantCoveredByTaiwu(combat, 0, new PageTargetChoice(2, 0)),
    "read outline page is covered");
Equal(false, TaiwuPageMarking.IsVariantCoveredByTaiwu(combat, 0, new PageTargetChoice(3, 0)),
    "other outline types are not covered");
Equal(true, TaiwuPageMarking.IsVariantCoveredByTaiwu(combat, 1, new PageTargetChoice(0, 0)),
    "owned complete 正 page is covered");
Equal(true, TaiwuPageMarking.IsVariantCoveredByTaiwu(combat, 1, new PageTargetChoice(1, 0)),
    "read 逆 page is covered");
Equal(true, TaiwuPageMarking.IsVariantCoveredByTaiwu(combat, 1, new PageTargetChoice(0, 1)),
    "coverage ignores the option's page state as long as it is concrete");
Equal(false, TaiwuPageMarking.IsVariantCoveredByTaiwu(combat, 1, new PageTargetChoice(-1, -1)),
    "wildcard target is never covered");
Equal(false, TaiwuPageMarking.IsVariantCoveredByTaiwu(combat, 3, new PageTargetChoice(0, 0)),
    "unknown page is not covered");
Equal(false, TaiwuPageMarking.IsVariantCoveredByTaiwu(combat, 9, new PageTargetChoice(0, 0)),
    "out-of-range page is tolerated");
Equal(true, TaiwuPageMarking.IsPageFullyCoveredByTaiwu(combat, 1),
    "combat page with both directions covered is fully covered");
Equal(false, TaiwuPageMarking.IsPageFullyCoveredByTaiwu(combat, 2),
    "combat page with only one direction covered is not fully covered");
Equal(true, TaiwuPageMarking.IsPageFullyCoveredByTaiwu(combat, 0),
    "outline page counts as fully covered once any outline type is covered");
Equal(false, TaiwuPageMarking.IsPageFullyCoveredByTaiwu(combat, 4),
    "untouched page is not fully covered");
Equal(true, TaiwuPageMarking.HasAnyMark(combat), "knowledge with marks reports any");

// Life knowledge: page index 1 owned, page index 3 read; type is ignored.
var life = new TaiwuBookKnowledge(Combat: false,
    OwnedTypeMasks: new[] { 0, 1, 0, 0, 0 },
    ReadTypeMasks: new[] { 0, 0, 0, 1, 0 });
Equal(true, TaiwuPageMarking.IsVariantCoveredByTaiwu(life, 1, new PageTargetChoice(-1, 0)),
    "life page coverage ignores type");
Equal(true, TaiwuPageMarking.IsVariantCoveredByTaiwu(life, 3, new PageTargetChoice(-1, 1)),
    "life read page is covered");
Equal(true, TaiwuPageMarking.IsPageFullyCoveredByTaiwu(life, 1),
    "life page is fully covered once owned or read");
Equal(false, TaiwuPageMarking.IsPageFullyCoveredByTaiwu(life, 0),
    "life untouched page is not fully covered");

var empty = TaiwuBookKnowledge.Empty;
Equal(false, TaiwuPageMarking.IsVariantCoveredByTaiwu(empty, 0, new PageTargetChoice(0, 0)),
    "empty knowledge covers nothing");
Equal(false, TaiwuPageMarking.IsPageFullyCoveredByTaiwu(empty, 0), "empty knowledge fully covers nothing");
Equal(false, TaiwuPageMarking.HasAnyMark(empty), "empty knowledge has no marks");

Console.WriteLine("TaiwuPageMarking contracts passed.");
