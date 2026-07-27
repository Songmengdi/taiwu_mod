using GameData.Domains.CombatSkill;
using GameData.Domains.Item;

namespace MapSkillFinder.Frontend;

/// <summary>
/// Pure frontend projection over one backend holdings read. It deliberately keeps
/// page-target changes local: no area scan is repeated while the player compares
/// possible books and holder sets.
/// </summary>
internal sealed record BookPageAvailability(
    PageTargetChoice Target,
    int HolderCount);

internal sealed record BookHolderSet(
    IReadOnlyList<BookHolderView> Holders,
    string Key);

internal static class BookHoldingWorkspace
{
    internal const int CombatPageCount = 6;
    internal const int LifePageCount = 5;
    internal const int MaxRenderedSets = 120;

    internal static int PageCount(bool combat) => combat ? CombatPageCount : LifePageCount;

    internal static IReadOnlyList<BookPageAvailability> GetPageAvailability(
        IReadOnlyList<BookHolderView> holders,
        int page,
        bool combat)
    {
        return holders
            .SelectMany(holder => holder.Books
                .Select(book => ReadTarget(book, page, combat))
                .Distinct()
                .Select(target => (holder.CharacterId, target)))
            .GroupBy(item => item.target)
            .Select(group => new BookPageAvailability(group.Key,
                group.Select(item => item.CharacterId).Distinct().Count()))
            .OrderBy(item => PageDisplayOrder(page, item.Target, combat))
            .ThenByDescending(item => item.HolderCount)
            .ToArray();
    }

    // State < 0 is the wildcard ("不限"): the page is covered by any copy of the
    // book, so only the pages with a concrete target constrain the holder sets.
    internal static bool Matches(BookCopyView book, int page, PageTargetChoice target, bool combat) =>
        target.State < 0 || ReadTarget(book, page, combat) == target;

    internal static IReadOnlyList<BookHolderSet> FindHolderSets(
        IReadOnlyList<BookHolderView> source,
        IReadOnlyList<PageTargetChoice> targets,
        bool combat) =>
        FindHolderSets(source, targets.Select(target =>
            target.State < 0
                ? (IReadOnlyCollection<PageTargetChoice>)Array.Empty<PageTargetChoice>()
                : new[] { target }).ToArray(), combat);

    internal static IReadOnlyList<BookHolderSet> FindHolderSets(
        IReadOnlyList<BookHolderView> source,
        IReadOnlyList<IReadOnlyCollection<PageTargetChoice>> targets,
        bool combat)
    {
        if (targets.Count != PageCount(combat))
            throw new ArgumentException(
                combat ? "功法书必须选择总纲与五页正文。" : "技艺书必须选择五页正文。",
                nameof(targets));

        ulong required = (1UL << targets.Count) - 1UL;
        var candidates = source
            .Select(holder => (Holder: holder, Coverage: Coverage(holder, targets, combat)))
            .Where(item => item.Coverage != 0)
            .OrderBy(item => item.Holder.CharacterId)
            .ToArray();
        var output = new List<BookHolderSet>();
        var selected = new List<(BookHolderView Holder, ulong Coverage)>();

        for (int size = 1; size <= candidates.Length && output.Count < MaxRenderedSets; size++)
            Enumerate(candidates, required, size, 0, 0, selected, output);

        return output;
    }

    private static void Enumerate(
        IReadOnlyList<(BookHolderView Holder, ulong Coverage)> candidates,
        ulong required,
        int targetSize,
        int start,
        ulong coverage,
        List<(BookHolderView Holder, ulong Coverage)> selected,
        List<BookHolderSet> output)
    {
        if (output.Count >= MaxRenderedSets) return;
        if (selected.Count == targetSize)
        {
            if ((coverage & required) != required) return;
            BookHolderView[] holders = selected.Select(item => item.Holder).ToArray();
            output.Add(new BookHolderSet(holders,
                string.Join("-", holders.Select(item => item.CharacterId))));
            return;
        }

        int remaining = targetSize - selected.Count;
        for (int index = start; index <= candidates.Count - remaining; index++)
        {
            var candidate = candidates[index];
            selected.Add(candidate);
            Enumerate(candidates, required, targetSize, index + 1, coverage | candidate.Coverage,
                selected, output);
            selected.RemoveAt(selected.Count - 1);
            if (output.Count >= MaxRenderedSets) return;
        }
    }

    private static ulong Coverage(
        BookHolderView holder,
        IReadOnlyList<IReadOnlyCollection<PageTargetChoice>> targets,
        bool combat)
    {
        ulong coverage = 0;
        for (int page = 0; page < targets.Count; page++)
        {
            if (holder.Books.Any(book => PageTargetFilter.Matches(
                    ReadTarget(book, page, combat), targets[page])))
                coverage |= 1UL << page;
        }
        return coverage;
    }

    // 已读书页：功法 readingState 是 CombatSkill.GetReadingState() 原值，按游戏内部页序解码
    // （总纲 behaviorType 0..4，正文 pageId 1..5 + 正/逆方向，均经运行核实）；
    // 技艺 readingState 是后端按页 0..4 拼好的位掩码。<0 表示太吾尚未习得。
    internal static TaiwuBookKnowledge BuildTaiwuKnowledge(
        IReadOnlyList<BookCopyView> taiwuBooks,
        int readingState,
        bool combat)
    {
        int pageCount = PageCount(combat);
        var owned = new int[pageCount];
        var read = new int[pageCount];
        foreach (BookCopyView book in taiwuBooks)
        {
            for (int page = 0; page < pageCount; page++)
            {
                PageTargetChoice target = ReadTarget(book, page, combat);
                if (target.State != 0) continue;
                owned[page] |= TaiwuPageMarking.VariantBit(combat, target.Type);
            }
        }
        if (readingState >= 0)
        {
            if (combat)
            {
                ushort state = (ushort)readingState;
                for (sbyte outline = 0; outline < 5; outline++)
                {
                    if (CombatSkillStateHelper.IsPageRead(state,
                            CombatSkillStateHelper.GetOutlinePageInternalIndex(outline)))
                        read[0] |= 1 << outline;
                }
                for (byte page = 1; page < pageCount; page++)
                {
                    for (sbyte direction = 0; direction < 2; direction++)
                    {
                        if (CombatSkillStateHelper.IsPageRead(state,
                                CombatSkillStateHelper.GetNormalPageInternalIndex(direction, page)))
                            read[page] |= 1 << direction;
                    }
                }
            }
            else
            {
                for (int page = 0; page < pageCount; page++)
                {
                    if ((readingState & (1 << page)) != 0)
                        read[page] = 1;
                }
            }
        }
        return new TaiwuBookKnowledge(combat, owned, read);
    }

    private static PageTargetChoice ReadTarget(BookCopyView book, int page, bool combat)
    {
        // 技艺书没有总纲与正逆方向，方向固定为 -1。
        sbyte type = combat
            ? page == 0
                ? SkillBookStateHelper.GetOutlinePageType(book.PageTypes)
                : SkillBookStateHelper.GetNormalPageType(book.PageTypes, (byte)page)
            : (sbyte)-1;
        sbyte state = SkillBookStateHelper.GetPageIncompleteState(book.PageStates, (byte)page);
        return new PageTargetChoice(type, state);
    }

    private static int PageDisplayOrder(int page, PageTargetChoice target, bool combat)
    {
        if (!combat)
            return target.State;

        if (page == 0)
            return target.Type * 10 + target.State;

        // Always preserve the player's reading order: 正完、逆完、正残、逆残、正佚、逆佚.
        int typeOrder = target.Type == 0 ? 0 : target.Type == 1 ? 1 : 9;
        int stateOrder = target.State == 0 ? 0 : target.State == 1 ? 1 : 2;
        return stateOrder * 2 + typeOrder;
    }
}
