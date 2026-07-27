namespace MapSkillFinder.Frontend;

/// <summary>Pure ordering and cache rules for local-first map searches.</summary>
internal static class AreaSearchPlan
{
    internal static bool ShouldLoadCatalog(bool hasCatalog) => !hasCatalog;

    internal static bool HasDateChanged(int previousDateTick, int currentDateTick) =>
        previousDateTick > 0 && currentDateTick > 0 && previousDateTick != currentDateTick;

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
        bool hasSearchCriteria,
        bool cacheValid,
        bool searchInFlight) => hasSearchCriteria && !cacheValid && !searchInFlight;

    internal static bool ShouldSearchBeyondCurrentArea(
        bool currentAreaHasResults,
        bool forceFullMap) => forceFullMap || !currentAreaHasResults;
}
