using MapSkillFinder.Domain;

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
