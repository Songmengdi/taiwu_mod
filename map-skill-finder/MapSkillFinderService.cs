using System.Diagnostics;
using System.Reflection;
using Config;
using GameData.Common;
using GameData.Domains;
using GameData.Domains.Building;
using GameData.Domains.Character;
using GameData.Domains.Character.Filters;
using GameData.Domains.CombatSkill;
using GameData.Domains.Item;
using GameData.Domains.Map;
using GameData.Domains.Merchant;
using GameData.Domains.Organization;
using MapSkillFinder.Domain;
using TaiwuCharacter = GameData.Domains.Character.Character;
using TaiwuSkillBook = GameData.Domains.Item.SkillBook;
using TaiwuShortList = GameData.Utilities.ShortList;

namespace MapSkillFinder.Backend;

internal enum SearchBookKind : sbyte
{
    Combat = 0,
    Life = 1,
}

internal enum PageMatchState : sbyte
{
    Any = -1,
    Complete = 0,
    Incomplete = 1,
    Lost = 2,
}

internal enum PageMatchType : sbyte
{
    Any = -1,
}

internal sealed record AreaOption(short AreaId, string Name, sbyte Category, sbyte StateId);

internal sealed record FinderCatalog(short CurrentAreaId, IReadOnlyList<AreaOption> Areas, int DateTick);

internal sealed record PageRequirement(sbyte State, sbyte Type);

internal sealed record BookSearchRequest(
    short AreaId,
    SearchBookKind Kind,
    short SkillTemplateId,
    BookSource Sources,
    IReadOnlyList<PageRequirement> Pages,
    int Page,
    int PageSize);

/// <summary>One read of every matching copy in an area; the frontend then changes page targets locally.</summary>
internal sealed record BookHoldingsRequest(
    short AreaId,
    SearchBookKind Kind,
    short SkillTemplateId,
    BookSource Sources);

internal sealed record BookHoldingsResult(
    string BookName,
    long ElapsedMilliseconds,
    IReadOnlyList<BookHolderCandidate> Holders);

internal sealed record BookSearchResult(
    string BookName,
    int HolderCount,
    int TotalCount,
    int Page,
    int PageSize,
    ulong MissingMask,
    long ElapsedMilliseconds,
    IReadOnlyList<BookCombination> Combinations,
    IReadOnlyDictionary<int, BookHolderCandidate> Holders);

internal enum AbilityMetric : sbyte
{
    Aptitude = 0,
    Attainment = 1,
}

internal sealed record AbilityCondition(sbyte LifeSkillType, AbilityMetric Metric, short Minimum);

internal sealed record PersonSearchRequest(
    short AreaId,
    string Name,
    int GradeMask,
    short MinAge,
    short MaxAge,
    sbyte Gender,
    IReadOnlyList<sbyte> Organizations,
    IReadOnlyList<AbilityCondition> Abilities,
    int Page,
    int PageSize);

internal sealed record AbilityValue(
    sbyte LifeSkillType,
    AbilityMetric Metric,
    short Total,
    short Base,
    sbyte GrowthAdjust,
    sbyte GrowthType);

internal sealed record PersonSearchRow(
    int CharacterId,
    string Name,
    short AreaId,
    short BlockId,
    string Organization,
    sbyte OrganizationId,
    sbyte Grade,
    short Age,
    sbyte Gender,
    IReadOnlyList<AbilityValue> Abilities);

internal sealed record PersonSearchResult(
    int TotalCount,
    int Page,
    int PageSize,
    long ElapsedMilliseconds,
    IReadOnlyList<PersonSearchRow> People);

internal enum MerchantTargetType : sbyte
{
    Merchant = 0,
    Caravan = 1,
    Guild = 2,
}

internal sealed record MerchantSearchRequest(
    short AreaId,
    int TargetTypeMask,
    int GuildTypeMask,
    int LevelMask,
    sbyte CaravanState,
    int Page,
    int PageSize);

internal sealed record MerchantSearchRow(
    MerchantTargetType TargetType,
    int EntityId,
    string Name,
    short AreaId,
    short BlockId,
    sbyte GuildType,
    string GuildName,
    sbyte Level,
    bool Robbed);

internal sealed record MerchantSearchResult(
    int TotalCount,
    int Page,
    int PageSize,
    long ElapsedMilliseconds,
    IReadOnlyList<MerchantSearchRow> Rows);

internal sealed record RenxiaSearchRequest(short AreaId, int GradeMask);

internal sealed record RenxiaSearchRow(short TemplateId, string Name, short AreaId, short BlockId, sbyte Grade);

internal sealed record RenxiaSearchResult(
    int TotalCount,
    long ElapsedMilliseconds,
    IReadOnlyList<RenxiaSearchRow> Rows);

internal static class MapSkillFinderService
{
    private static readonly FieldInfo SoldLibraryBooksField =
        typeof(CharacterDomain).GetField("_soldLibrarySkillBooks", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(typeof(CharacterDomain).FullName, "_soldLibrarySkillBooks");

    internal static FinderCatalog GetCatalog()
    {
        Location current = DomainManager.Taiwu.GetTaiwu().GetLocation();
        var areas = new List<AreaOption>(MapAreaData.RegularAreasCount);
        for (short areaId = 0; areaId < MapAreaData.RegularAreasCount; areaId++)
        {
            MapAreaData data = DomainManager.Map.GetElement_Areas(areaId);
            MapAreaItem config = data.GetConfig();
            sbyte category = config.AreaType == 1 ? (sbyte)0 : config.AreaType == 0 ? (sbyte)1 : (sbyte)2;
            areas.Add(new AreaOption(areaId, config.Name, category, config.StateID));
        }
        // Packed game date lets the frontend drop cached results after a month change.
        int dateTick = (DomainManager.World.GetCurrYear() << 8) | DomainManager.World.GetCurrMonthInYear();
        return new FinderCatalog(current.AreaId, areas, dateTick);
    }

    internal static BookSearchResult SearchBooks(DataContext context, BookSearchRequest request)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        ValidateArea(request.AreaId);
        ValidatePaging(request.Page, request.PageSize);
        if (request.Sources == BookSource.None || (request.Sources & ~BookSource.All) != 0)
            throw new ArgumentOutOfRangeException(nameof(request.Sources));

        short bookId;
        string bookName;
        if (request.Kind == SearchBookKind.Combat)
        {
            CombatSkillItem skill = Config.CombatSkill.Instance.GetItem(request.SkillTemplateId)
                ?? throw new ArgumentOutOfRangeException(nameof(request.SkillTemplateId), "功法不存在。");
            bookId = skill.BookId;
            bookName = skill.Name;
            if (request.Pages.Count != 6)
                throw new ArgumentException("功法秘籍需要总纲与五页正文条件。");
        }
        else if (request.Kind == SearchBookKind.Life)
        {
            Config.LifeSkillItem skill = Config.LifeSkill.Instance.GetItem(request.SkillTemplateId)
                ?? throw new ArgumentOutOfRangeException(nameof(request.SkillTemplateId), "技艺不存在。");
            bookId = skill.SkillBookId;
            bookName = skill.Name;
            if (request.Pages.Count != 5)
                throw new ArgumentException("技艺书需要五页条件。");
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(request.Kind));
        }

        if (bookId < 0)
            throw new InvalidOperationException("所选功法或技艺没有对应书籍。");

        List<TaiwuCharacter> characters = FindCharacters(request.AreaId);
        var holders = new List<BookHolderCandidate>();
        foreach (TaiwuCharacter character in characters)
        {
            if (!IsEligibleCharacter(character))
                continue;
            var books = new List<BookCopyCandidate>();
            if ((request.Sources & BookSource.PrivateLibrary) != 0)
                books.AddRange(GetPrivateLibraryBooks(context, character, request.Kind,
                    request.SkillTemplateId, bookId, request.Pages));
            if ((request.Sources & BookSource.Inventory) != 0)
                books.AddRange(GetInventoryBooks(character, bookId, request.Pages));
            if (books.Count == 0)
                continue;

            OrganizationInfo organization = character.GetOrganizationInfo();
            OrganizationItem? organizationConfig = organization.GetOrganizationConfig();
            Location location = character.GetLocation();
            holders.Add(new BookHolderCandidate(
                character.GetId(),
                ResolveName(character),
                location.AreaId,
                location.BlockId,
                organizationConfig?.Name ?? "无所属",
                organization.Grade,
                books));
        }

        ulong requiredMask = (1UL << request.Pages.Count) - 1UL;
        BookCombinationResult solved = BookCombinationSolver.Solve(holders, requiredMask, 3);
        BookCombination[] page = solved.Combinations
            .Skip(request.Page * request.PageSize)
            .Take(request.PageSize)
            .ToArray();
        IReadOnlyDictionary<int, BookHolderCandidate> holderMap = holders.ToDictionary(holder => holder.CharacterId);
        stopwatch.Stop();
        return new BookSearchResult(bookName, solved.HolderCount, solved.Combinations.Count,
            request.Page, request.PageSize, solved.MissingMask, stopwatch.ElapsedMilliseconds, page, holderMap);
    }

    internal static BookHoldingsResult GetBookHoldings(DataContext context, BookHoldingsRequest request)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        ValidateArea(request.AreaId);
        if (request.Sources == BookSource.None || (request.Sources & ~BookSource.All) != 0)
            throw new ArgumentOutOfRangeException(nameof(request.Sources));

        short bookId;
        string bookName;
        int pageCount;
        if (request.Kind == SearchBookKind.Combat)
        {
            CombatSkillItem skill = Config.CombatSkill.Instance.GetItem(request.SkillTemplateId)
                ?? throw new ArgumentOutOfRangeException(nameof(request.SkillTemplateId), "功法不存在。");
            bookId = skill.BookId;
            bookName = skill.Name;
            pageCount = 6;
        }
        else if (request.Kind == SearchBookKind.Life)
        {
            Config.LifeSkillItem skill = Config.LifeSkill.Instance.GetItem(request.SkillTemplateId)
                ?? throw new ArgumentOutOfRangeException(nameof(request.SkillTemplateId), "技艺不存在。");
            bookId = skill.SkillBookId;
            bookName = skill.Name;
            pageCount = 5;
        }
        else throw new ArgumentOutOfRangeException(nameof(request.Kind));

        if (bookId < 0)
            throw new InvalidOperationException("所选功法或技艺没有对应书籍。");

        PageRequirement[] anyPages = Enumerable.Range(0, pageCount)
            .Select(_ => new PageRequirement((sbyte)PageMatchState.Any, (sbyte)PageMatchType.Any)).ToArray();
        var holders = new List<BookHolderCandidate>();
        foreach (TaiwuCharacter character in FindCharacters(request.AreaId))
        {
            if (!IsEligibleCharacter(character)) continue;
            var books = new List<BookCopyCandidate>();
            if ((request.Sources & BookSource.PrivateLibrary) != 0)
                books.AddRange(GetPrivateLibraryBooks(context, character, request.Kind,
                    request.SkillTemplateId, bookId, anyPages));
            if ((request.Sources & BookSource.Inventory) != 0)
                books.AddRange(GetInventoryBooks(character, bookId, anyPages));
            if (books.Count == 0) continue;

            OrganizationInfo organization = character.GetOrganizationInfo();
            OrganizationItem? organizationConfig = organization.GetOrganizationConfig();
            Location location = character.GetLocation();
            holders.Add(new BookHolderCandidate(
                character.GetId(), ResolveName(character), location.AreaId, location.BlockId,
                organizationConfig?.Name ?? "无所属", organization.Grade, books));
        }
        stopwatch.Stop();
        return new BookHoldingsResult(bookName, stopwatch.ElapsedMilliseconds, holders);
    }

    internal static PersonSearchResult SearchPeople(PersonSearchRequest request)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        ValidateArea(request.AreaId);
        ValidatePaging(request.Page, request.PageSize);
        if (request.Abilities.Count > 3)
            throw new ArgumentException("最多允许三条技艺条件。");

        HashSet<sbyte>? organizations = request.Organizations.Count == 0
            ? null
            : request.Organizations.ToHashSet();
        var rows = new List<PersonSearchRow>();
        foreach (TaiwuCharacter character in FindCharacters(request.AreaId))
        {
            if (!IsEligibleCharacter(character))
                continue;
            string name = ResolveName(character);
            if (!string.IsNullOrWhiteSpace(request.Name) &&
                name.IndexOf(request.Name.Trim(), StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            OrganizationInfo organization = character.GetOrganizationInfo();
            if (request.GradeMask != 0 && (request.GradeMask & (1 << organization.Grade)) == 0)
                continue;
            if (organizations != null && !organizations.Contains(organization.OrgTemplateId))
                continue;

            short age = character.GetActualAge();
            if (request.MinAge >= 0 && age < request.MinAge)
                continue;
            if (request.MaxAge >= 0 && age > request.MaxAge)
                continue;
            sbyte gender = character.GetDisplayingGender();
            if (request.Gender >= 0 && gender != request.Gender)
                continue;

            var abilities = new List<AbilityValue>(request.Abilities.Count);
            bool matches = true;
            foreach (AbilityCondition condition in request.Abilities)
            {
                if (condition.LifeSkillType is < 0 or >= 16 || condition.Minimum is < 0 or > 999)
                    throw new ArgumentOutOfRangeException(nameof(request.Abilities));
                short total;
                short @base;
                sbyte adjust = 0;
                sbyte growth = character.GetLifeSkillQualificationGrowthType();
                if (condition.Metric == AbilityMetric.Aptitude)
                {
                    total = character.GetLifeSkillQualification(condition.LifeSkillType);
                    @base = character.GetBaseLifeSkillQualifications().Get(condition.LifeSkillType);
                    adjust = character.GetLifeSkillQualificationAgeAdjust();
                }
                else if (condition.Metric == AbilityMetric.Attainment)
                {
                    total = character.GetLifeSkillAttainment(condition.LifeSkillType);
                    var baseValues = new LifeSkillShorts();
                    baseValues.Initialize();
                    character.GetLifeSkillBaseAttainment(ref baseValues);
                    @base = baseValues.Get(condition.LifeSkillType);
                }
                else
                {
                    throw new ArgumentOutOfRangeException(nameof(condition.Metric));
                }
                if (total < condition.Minimum)
                {
                    matches = false;
                    break;
                }
                abilities.Add(new AbilityValue(condition.LifeSkillType, condition.Metric,
                    total, @base, adjust, growth));
            }
            if (!matches)
                continue;

            OrganizationItem? organizationConfig = organization.GetOrganizationConfig();
            Location location = character.GetLocation();
            rows.Add(new PersonSearchRow(
                character.GetId(), name, location.AreaId, location.BlockId,
                organizationConfig?.Name ?? "无所属", organization.OrgTemplateId,
                organization.Grade, age, gender, abilities));
        }

        PersonSearchRow[] ordered = rows
            .OrderBy(row => row.Name, StringComparer.CurrentCulture)
            .ThenBy(row => row.Grade)
            .ThenBy(row => row.CharacterId)
            .ToArray();
        PersonSearchRow[] page = ordered.Skip(request.Page * request.PageSize).Take(request.PageSize).ToArray();
        stopwatch.Stop();
        return new PersonSearchResult(ordered.Length, request.Page, request.PageSize,
            stopwatch.ElapsedMilliseconds, page);
    }

    internal static MerchantSearchResult SearchMerchants(DataContext context, MerchantSearchRequest request)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        ValidateArea(request.AreaId);
        ValidatePaging(request.Page, request.PageSize);
        int targetMask = request.TargetTypeMask == 0 ? 0b111 : request.TargetTypeMask;
        var rows = new List<MerchantSearchRow>();
        var seenCaravans = new HashSet<int>();
        var seenGuilds = new HashSet<string>(StringComparer.Ordinal);
        Span<MapBlockData> blocks = DomainManager.Map.GetAreaBlocks(request.AreaId);
        for (short blockId = 0; blockId < blocks.Length; blockId++)
        {
            MapBlockData block = blocks[blockId];
            Location location = new(request.AreaId, blockId);
            if ((targetMask & (1 << (int)MerchantTargetType.Merchant)) != 0 && block.CharacterSet != null)
            {
                foreach (int characterId in block.CharacterSet)
                {
                    TaiwuCharacter character = DomainManager.Character.GetElement_Objects(characterId);
                    if (character.GetAgeGroup() != 2 ||
                        !DomainManager.Extra.TryGetMerchantCharToType(characterId, out sbyte guildType) ||
                        character.GetOrganizationInfo().Grade != 4)
                        continue;
                    sbyte level = DomainManager.Merchant.GetMerchantLevel(characterId);
                    if (!MerchantMatches(request, guildType, level, robbed: false, isCaravan: false))
                        continue;
                    rows.Add(new MerchantSearchRow(MerchantTargetType.Merchant, characterId,
                        ResolveName(character), request.AreaId, blockId, guildType,
                        MerchantGuildName(guildType), level, false));
                }
            }

            if ((targetMask & (1 << (int)MerchantTargetType.Caravan)) != 0)
            {
                foreach (CaravanDisplayData caravan in DomainManager.Merchant.GetCaravanAtBlock(context, location))
                {
                    if (!seenCaravans.Add(caravan.CaravanId))
                        continue;
                    MerchantItem merchant = Config.Merchant.Instance[caravan.MerchantTemplateId];
                    bool robbed = caravan.ExtraData != null && caravan.ExtraData.StateEnum == CaravanState.Robbed;
                    if (!MerchantMatches(request, merchant.MerchantType, merchant.Level, robbed, isCaravan: true))
                        continue;
                    rows.Add(new MerchantSearchRow(MerchantTargetType.Caravan, caravan.CaravanId,
                        merchant.UiName, request.AreaId, blockId, merchant.MerchantType,
                        MerchantGuildName(merchant.MerchantType), merchant.Level, robbed));
                }
            }

            if ((targetMask & (1 << (int)MerchantTargetType.Guild)) == 0)
                continue;
            Settlement? settlement = DomainManager.Organization.GetSettlementByLocation(location);
            if (settlement == null)
                continue;
            Location settlementLocation = settlement.GetLocation();
            foreach (BuildingBlockData building in DomainManager.Building.GetBuildingBlocksAtLocation(settlementLocation, null))
            {
                if (building.TemplateId is < 276 or > 282)
                    continue;
                BuildingBlockItem buildingConfig = Config.BuildingBlock.Instance[building.TemplateId];
                sbyte guildType = buildingConfig.MerchantId;
                MerchantTypeItem guild = Config.MerchantType.Instance[guildType];
                short areaTemplateId = DomainManager.Map.GetElement_Areas(request.AreaId).GetTemplateId();
                sbyte level = guild.HeadArea == areaTemplateId ? guild.HeadLevel : guild.BranchLevel;
                string key = $"{settlementLocation.AreaId}:{settlementLocation.BlockId}:{guildType}";
                if (!seenGuilds.Add(key) || !MerchantMatches(request, guildType, level, false, isCaravan: false))
                    continue;
                rows.Add(new MerchantSearchRow(MerchantTargetType.Guild,
                    HashCode.Combine(settlementLocation.AreaId, settlementLocation.BlockId, guildType),
                    guild.Name, settlementLocation.AreaId, settlementLocation.BlockId,
                    guildType, guild.Name, level, false));
            }
        }

        MerchantSearchRow[] ordered = rows
            .OrderByDescending(row => row.Level)
            .ThenBy(row => row.TargetType)
            .ThenBy(row => row.GuildType)
            .ThenBy(row => row.Robbed)
            .ThenBy(row => row.Name, StringComparer.CurrentCulture)
            .ThenBy(row => row.EntityId)
            .ToArray();
        MerchantSearchRow[] page = ordered.Skip(request.Page * request.PageSize).Take(request.PageSize).ToArray();
        stopwatch.Stop();
        return new MerchantSearchResult(ordered.Length, request.Page, request.PageSize,
            stopwatch.ElapsedMilliseconds, page);
    }

    private static bool MerchantMatches(
        MerchantSearchRequest request,
        sbyte guildType,
        sbyte level,
        bool robbed,
        bool isCaravan)
    {
        if (request.GuildTypeMask != 0 && (request.GuildTypeMask & (1 << guildType)) == 0)
            return false;
        if (request.LevelMask != 0 && (request.LevelMask & (1 << level)) == 0)
            return false;
        if (isCaravan && request.CaravanState >= 0 && (request.CaravanState == 1) != robbed)
            return false;
        return true;
    }

    private static string MerchantGuildName(sbyte guildType) =>
        Config.MerchantType.Instance.GetItem(guildType)?.Name ?? $"商会{guildType}";

    // Renxia are template enemies living on map blocks (Organization template 18),
    // not real NPCs, so the scan walks block data instead of the character domain.
    internal static RenxiaSearchResult SearchRenxia(RenxiaSearchRequest request)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        ValidateArea(request.AreaId);
        var rows = new List<RenxiaSearchRow>();
        Span<MapBlockData> blocks = DomainManager.Map.GetAreaBlocks(request.AreaId);
        for (short blockId = 0; blockId < blocks.Length; blockId++)
        {
            List<MapTemplateEnemyInfo>? enemies = blocks[blockId].TemplateEnemyList;
            if (enemies == null)
                continue;
            foreach (MapTemplateEnemyInfo enemy in enemies)
            {
                CharacterItem? config = Config.Character.Instance.GetItem(enemy.TemplateId);
                if (config == null || config.OrganizationInfo.OrgTemplateId != 18)
                    continue;
                sbyte grade = config.OrganizationInfo.Grade;
                if (request.GradeMask != 0 && (request.GradeMask & (1 << grade)) == 0)
                    continue;
                string name = config.Surname + config.GivenName;
                rows.Add(new RenxiaSearchRow(enemy.TemplateId,
                    string.IsNullOrWhiteSpace(name) ? $"任侠{enemy.TemplateId}" : name,
                    request.AreaId, blockId, grade));
            }
        }

        RenxiaSearchRow[] ordered = rows
            .OrderByDescending(row => row.Grade)
            .ThenBy(row => row.BlockId)
            .ThenBy(row => row.TemplateId)
            .ToArray();
        stopwatch.Stop();
        return new RenxiaSearchResult(ordered.Length, stopwatch.ElapsedMilliseconds, ordered);
    }

    private static IEnumerable<BookCopyCandidate> GetPrivateLibraryBooks(
        DataContext context,
        TaiwuCharacter character,
        SearchBookKind kind,
        short skillTemplateId,
        short bookId,
        IReadOnlyList<PageRequirement> requirements)
    {
        if (IsLibraryBookSold(character.GetId(), bookId))
            yield break;

        ushort seedState = 0;
        bool available;
        if (kind == SearchBookKind.Combat)
        {
            available = DomainManager.CombatSkill.GetCharCombatSkills(character.GetId())
                .TryGetValue(skillTemplateId, out GameData.Domains.CombatSkill.CombatSkill? skill);
            if (!available || skill == null)
                yield break;
            seedState = skill.GetActivationState();
            if (!CombatSkillStateHelper.IsBrokenOut(seedState) ||
                !CombatSkillStateHelper.CanGenerateBookFromActivationState(seedState))
                yield break;
        }
        else
        {
            Config.LifeSkillItem? learnedConfig = Config.LifeSkill.Instance.GetItem(skillTemplateId);
            available = learnedConfig != null && character.GetLearnedLifeSkills()
                .Any(skill => skill.SkillTemplateId == skillTemplateId && skill.GetReadPagesCount() >= 3);
            if (!available)
                yield break;
        }

        context.SwitchRandomSource((ulong)(uint)character.GetId());
        try
        {
            ulong seed = kind == SearchBookKind.Combat
                ? (ulong)(uint)(character.GetId() ^ ((seedState << 16) + bookId))
                : (ulong)(uint)(character.GetId() ^ bookId);
            context.Random.Reinitialise(seed);
            TaiwuSkillBook preview = kind == SearchBookKind.Combat
                ? new TaiwuSkillBook(context.Random, bookId, -1, seedState)
                : new TaiwuSkillBook(context.Random, bookId, -1, -1, -1, -1, 50, true);
            ulong coverage = Coverage(preview.GetPageTypes(), preview.GetPageIncompleteState(), requirements);
            if (coverage != 0)
                yield return new BookCopyCandidate($"L:{character.GetId()}:{bookId}",
                    BookSource.PrivateLibrary, preview.GetPageTypes(), preview.GetPageIncompleteState(), coverage);
        }
        finally
        {
            context.RestoreRandomSource();
        }
    }

    private static IEnumerable<BookCopyCandidate> GetInventoryBooks(
        TaiwuCharacter character,
        short bookId,
        IReadOnlyList<PageRequirement> requirements)
    {
        foreach (ItemKey key in character.GetInventory().Items.Keys)
        {
            if (key.ItemType != 10 || key.TemplateId != bookId || key.GetData() is not TaiwuSkillBook book)
                continue;
            ulong coverage = Coverage(book.GetPageTypes(), book.GetPageIncompleteState(), requirements);
            if (coverage != 0)
                yield return new BookCopyCandidate($"I:{character.GetId()}:{key.Id}",
                    BookSource.Inventory, book.GetPageTypes(), book.GetPageIncompleteState(), coverage);
        }
    }

    private static ulong Coverage(
        byte pageTypes,
        ushort pageStates,
        IReadOnlyList<PageRequirement> requirements)
    {
        ulong result = 0;
        for (byte page = 0; page < requirements.Count; page++)
        {
            PageRequirement requirement = requirements[page];
            sbyte state = SkillBookStateHelper.GetPageIncompleteState(pageStates, page);
            if (requirement.State >= 0 && state != requirement.State)
                continue;
            sbyte type = page == 0
                ? SkillBookStateHelper.GetOutlinePageType(pageTypes)
                : SkillBookStateHelper.GetNormalPageType(pageTypes, page);
            if (requirement.Type >= 0 && type != requirement.Type)
                continue;
            result |= 1UL << page;
        }
        return result;
    }

    private static bool IsLibraryBookSold(int characterId, short bookId)
    {
        var sold = (Dictionary<int, TaiwuShortList>)SoldLibraryBooksField.GetValue(DomainManager.Character)!;
        return sold.TryGetValue(characterId, out TaiwuShortList list) &&
               list.Items != null && list.Items.Contains(bookId);
    }

    private static List<TaiwuCharacter> FindCharacters(short areaId)
    {
        var characters = new List<TaiwuCharacter>();
        MapCharacterFilter.Find(_ => true, characters, areaId, includeInfected: false);
        return characters;
    }

    private static bool IsEligibleCharacter(TaiwuCharacter character)
    {
        int id = character.GetId();
        Location location = character.GetLocation();
        return id != DomainManager.Taiwu.GetTaiwuCharId() &&
               location.IsValid() &&
               !DomainManager.Character.IsTemporaryIntelligentCharacter(id) &&
               !DomainManager.Adventure.IsAdventureTemporaryCharacter(id);
    }

    private static void ValidateArea(short areaId)
    {
        if (areaId < 0 || areaId >= MapAreaData.RegularAreasCount ||
            !MapAreaData.IsRegularArea(areaId) || MapAreaData.IsBrokenArea(areaId))
            throw new ArgumentOutOfRangeException(nameof(areaId), "只能查询未被侵袭的常规地域。");
    }

    private static void ValidatePaging(int page, int pageSize)
    {
        if (page < 0 || pageSize is < 1 or > 200)
            throw new ArgumentOutOfRangeException(nameof(pageSize));
    }

    internal static string ResolveName(TaiwuCharacter character)
    {
        // Prefer the game's own naming: sect members may be known by a monastic
        // title (道号/法号) instead of their real name. Show both when they differ
        // so the list can be matched against what the game displays.
        int id = character.GetId();
        string real = DomainManager.Character.GetName(id, true);
        if (string.IsNullOrWhiteSpace(real))
            real = ResolveFallbackName(character);
        string display = DomainManager.Character.GetName(id, false);
        return string.IsNullOrWhiteSpace(display) || display == real
            ? real
            : $"{real}（{display}）";
    }

    private static string ResolveFallbackName(TaiwuCharacter character)
    {
        string simple = character.GetSurname() + character.GetGivenName();
        if (!string.IsNullOrWhiteSpace(simple))
            return simple;
        try
        {
            var fullName = character.GetFullName().GetName(character.GetGender(),
                new Dictionary<int, string>());
            string generated = fullName.Item1 + fullName.Item2;
            if (!string.IsNullOrWhiteSpace(generated))
                return generated;
        }
        catch
        {
            // Custom names may require save-specific text entries. Preserve the
            // stable entity fallback when those entries are unavailable.
        }
        return $"人物{character.GetId()}";
    }
}
