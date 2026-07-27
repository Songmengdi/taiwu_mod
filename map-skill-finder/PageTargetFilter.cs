namespace MapSkillFinder.Frontend;

/// <summary>
/// A page can accept more than one concrete variant. An empty selection keeps
/// the existing unrestricted behaviour for that page.
/// </summary>
internal static class PageTargetFilter
{
    internal static bool Matches(
        PageTargetChoice actual,
        IReadOnlyCollection<PageTargetChoice> selectedTargets) =>
        selectedTargets.Count == 0 || selectedTargets.Contains(actual);
}
