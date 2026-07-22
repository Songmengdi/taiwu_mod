namespace MapSkillFinder.Domain;

[Flags]
internal enum BookSource : byte
{
    None = 0,
    PrivateLibrary = 1,
    Inventory = 2,
    All = PrivateLibrary | Inventory,
}

internal sealed record BookCopyCandidate(
    string CopyId,
    BookSource Source,
    byte PageTypes,
    ushort PageStates,
    ulong CoverageMask);

internal sealed record BookHolderCandidate(
    int CharacterId,
    string Name,
    short AreaId,
    short BlockId,
    string Organization,
    sbyte Grade,
    IReadOnlyList<BookCopyCandidate> Books)
{
    internal ulong CoverageMask => Books.Aggregate(0UL, static (mask, book) => mask | book.CoverageMask);
}

internal sealed record BookContribution(
    int CharacterId,
    string HolderName,
    IReadOnlyList<BookCopyCandidate> Books,
    ulong CoverageMask);

internal sealed record BookCombination(
    IReadOnlyList<BookContribution> Contributions,
    int PrivateBookCount,
    int BookCount,
    string StableKey);

internal sealed record BookCombinationResult(
    int HolderCount,
    IReadOnlyList<BookCombination> Combinations,
    ulong MissingMask)
{
    internal bool Success => Combinations.Count > 0;
}

internal static class BookCombinationSolver
{
    internal static BookCombinationResult Solve(
        IReadOnlyList<BookHolderCandidate> candidates,
        ulong requiredMask,
        int maxHolders = 3)
    {
        if (requiredMask == 0)
            throw new ArgumentOutOfRangeException(nameof(requiredMask), "至少需要一个目标书页。");
        if (maxHolders is < 1 or > 3)
            throw new ArgumentOutOfRangeException(nameof(maxHolders));

        BookHolderCandidate[] holders = candidates
            .Where(holder => (holder.CoverageMask & requiredMask) != 0)
            .OrderBy(holder => holder.CharacterId)
            .ToArray();

        ulong availableMask = holders.Aggregate(0UL, static (mask, holder) => mask | holder.CoverageMask);
        for (int count = 1; count <= maxHolders; count++)
        {
            var combinations = new List<BookCombination>();
            EnumerateHolderSets(holders, requiredMask, count, 0, new List<BookHolderCandidate>(count), combinations);
            if (combinations.Count == 0)
                continue;

            BookCombination[] ordered = combinations
                .GroupBy(combo => combo.StableKey, StringComparer.Ordinal)
                .Select(group => group
                    .OrderByDescending(combo => combo.PrivateBookCount)
                    .ThenBy(combo => combo.BookCount)
                    .First())
                .OrderByDescending(combo => combo.PrivateBookCount)
                .ThenBy(combo => combo.BookCount)
                .ThenBy(combo => combo.StableKey, StringComparer.Ordinal)
                .ToArray();
            return new BookCombinationResult(count, ordered, 0);
        }

        return new BookCombinationResult(0, Array.Empty<BookCombination>(), requiredMask & ~availableMask);
    }

    private static void EnumerateHolderSets(
        IReadOnlyList<BookHolderCandidate> holders,
        ulong requiredMask,
        int targetCount,
        int start,
        List<BookHolderCandidate> selected,
        List<BookCombination> output)
    {
        if (selected.Count == targetCount)
        {
            ulong union = selected.Aggregate(0UL, static (mask, holder) => mask | holder.CoverageMask);
            if ((union & requiredMask) != requiredMask)
                return;
            BookCombination? assignment = BuildBestAssignment(selected, requiredMask);
            if (assignment != null)
                output.Add(assignment);
            return;
        }

        int remaining = targetCount - selected.Count;
        for (int i = start; i <= holders.Count - remaining; i++)
        {
            selected.Add(holders[i]);
            EnumerateHolderSets(holders, requiredMask, targetCount, i + 1, selected, output);
            selected.RemoveAt(selected.Count - 1);
        }
    }

    private sealed record AssignmentState(
        ulong Coverage,
        IReadOnlyList<(BookHolderCandidate Holder, BookCopyCandidate Book)> SelectedBooks,
        int PrivateCount);

    private static BookCombination? BuildBestAssignment(
        IReadOnlyList<BookHolderCandidate> holders,
        ulong requiredMask)
    {
        var states = new Dictionary<ulong, AssignmentState>
        {
            [0] = new AssignmentState(0, Array.Empty<(BookHolderCandidate, BookCopyCandidate)>(), 0),
        };

        foreach (BookHolderCandidate holder in holders)
        {
            foreach (BookCopyCandidate book in holder.Books)
            {
                ulong useful = book.CoverageMask & requiredMask;
                if (useful == 0)
                    continue;

                AssignmentState[] snapshot = states.Values.ToArray();
                foreach (AssignmentState state in snapshot)
                {
                    ulong nextCoverage = state.Coverage | useful;
                    if (nextCoverage == state.Coverage)
                        continue;
                    var selected = state.SelectedBooks
                        .Append((holder, book))
                        .ToArray();
                    int privateCount = state.PrivateCount +
                        (book.Source == BookSource.PrivateLibrary ? 1 : 0);
                    var next = new AssignmentState(nextCoverage, selected, privateCount);
                    if (!states.TryGetValue(nextCoverage, out AssignmentState? old) || Better(next, old))
                        states[nextCoverage] = next;
                }
            }
        }

        if (!states.TryGetValue(requiredMask, out AssignmentState? best))
            return null;

        BookContribution[] contributions = best.SelectedBooks
            .GroupBy(item => item.Holder.CharacterId)
            .OrderBy(group => group.Key)
            .Select(group => new BookContribution(
                group.Key,
                group.First().Holder.Name,
                group.Select(item => item.Book).ToArray(),
                group.Aggregate(0UL, static (mask, item) => mask | item.Book.CoverageMask)))
            .ToArray();
        if (contributions.Length != holders.Count)
            return null;

        string stableKey = string.Join("-", contributions.Select(item => item.CharacterId));
        return new BookCombination(contributions, best.PrivateCount, best.SelectedBooks.Count, stableKey);
    }

    private static bool Better(AssignmentState candidate, AssignmentState current)
    {
        if (candidate.SelectedBooks.Count != current.SelectedBooks.Count)
            return candidate.SelectedBooks.Count < current.SelectedBooks.Count;
        if (candidate.PrivateCount != current.PrivateCount)
            return candidate.PrivateCount > current.PrivateCount;
        string candidateKey = string.Join("|", candidate.SelectedBooks.Select(item => item.Book.CopyId));
        string currentKey = string.Join("|", current.SelectedBooks.Select(item => item.Book.CopyId));
        return string.CompareOrdinal(candidateKey, currentKey) < 0;
    }
}
