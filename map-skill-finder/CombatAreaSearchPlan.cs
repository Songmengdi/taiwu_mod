namespace MapSkillFinder.Frontend;

/// <summary>Pure ordering rules for the combat-book local-first search.</summary>
internal static class CombatAreaSearchPlan
{
    internal static IReadOnlyList<short> BuildSearchOrder(
        IEnumerable<short> areaIds,
        short currentAreaId)
    {
        short[] distinct = areaIds.Distinct().ToArray();
        return distinct.Contains(currentAreaId)
            ? new[] { currentAreaId }.Concat(distinct.Where(areaId => areaId != currentAreaId)).ToArray()
            : distinct;
    }

    internal static IReadOnlyList<T> OrderByResultCount<T>(
        IEnumerable<T> results,
        Func<T, int> resultCount,
        Func<T, short> areaId) =>
        results.OrderByDescending(resultCount).ThenBy(areaId).ToArray();

    internal static bool ShouldStartAfterCatalog(
        bool hasSelectedBook,
        bool cacheValid,
        bool searchInFlight) => hasSelectedBook && !cacheValid && !searchInFlight;
}
