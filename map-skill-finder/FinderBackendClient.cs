using GameData.Domains.Mod;
using GameData.Serializer;
using GameData.Utilities;

namespace MapSkillFinder.Frontend;

internal sealed record AreaOptionView(short AreaId, string Name, sbyte Category, sbyte StateId);

internal sealed record FinderCatalogView(bool Success, string Message, int ApiVersion, short CurrentAreaId,
    int DateTick, IReadOnlyList<AreaOptionView> Areas);

internal sealed record PageRequirementView(sbyte State, sbyte Type);

internal sealed record BookSearchRequestView(
    short AreaId, sbyte Kind, short SkillTemplateId, byte Sources,
    IReadOnlyList<PageRequirementView> Pages, int Page = 0, int PageSize = 50);

internal sealed record BookHoldingsRequestView(
    short AreaId, sbyte Kind, short SkillTemplateId, byte Sources);

internal sealed record BookCopyView(
    string CopyId, byte Source, byte PageTypes, ushort PageStates, int Coverage);

internal sealed record BookContributionView(
    int CharacterId, string Name, short AreaId, short BlockId,
    string Organization, sbyte Grade, int Coverage, IReadOnlyList<BookCopyView> Books);

internal sealed record BookCombinationView(
    string Key, int BookCount, int PrivateCount, IReadOnlyList<BookContributionView> Contributions);

internal sealed record BookSearchResponse(
    bool Success, string Message, string BookName, int HolderCount,
    int TotalCount, int Page, int PageSize, int MissingMask, int ElapsedMs,
    IReadOnlyList<BookCombinationView> Combinations);

internal sealed record BookHolderView(
    int CharacterId, string Name, short AreaId, short BlockId,
    string Organization, sbyte Grade, IReadOnlyList<BookCopyView> Books);

internal sealed record BookHoldingsResponse(
    bool Success, string Message, string BookName, int ElapsedMs,
    IReadOnlyList<BookHolderView> Holders);

internal sealed record AbilityConditionView(sbyte LifeSkillType, sbyte Metric, short Minimum);

internal sealed record PersonSearchRequestView(
    short AreaId, string Name, int GradeMask, short MinAge, short MaxAge, sbyte Gender,
    IReadOnlyList<sbyte> Organizations, IReadOnlyList<AbilityConditionView> Abilities,
    int Page = 0, int PageSize = 100);

internal sealed record AbilityValueView(
    sbyte LifeSkillType, sbyte Metric, short Total, short Base, sbyte GrowthAdjust, sbyte GrowthType);

internal sealed record PersonRowView(
    int CharacterId, string Name, short AreaId, short BlockId, string Organization,
    sbyte OrganizationId, sbyte Grade, short Age, sbyte Gender,
    IReadOnlyList<AbilityValueView> Abilities);

internal sealed record PersonSearchResponse(
    bool Success, string Message, int TotalCount, int Page, int PageSize,
    int ElapsedMs, IReadOnlyList<PersonRowView> People);

internal sealed record MerchantSearchRequestView(
    short AreaId, int TargetTypeMask, int GuildTypeMask, int LevelMask,
    sbyte CaravanState, int Page = 0, int PageSize = 100);

internal sealed record MerchantRowView(
    sbyte TargetType, int EntityId, string Name, short AreaId, short BlockId,
    sbyte GuildType, string GuildName, sbyte Level, bool Robbed);

internal sealed record MerchantSearchResponse(
    bool Success, string Message, int TotalCount, int Page, int PageSize,
    int ElapsedMs, IReadOnlyList<MerchantRowView> Rows);

internal static class FinderBackendClient
{
    private const string CatalogMethod = "TaiwuFinder.GetCatalog.v1";
    private const string BookMethod = "TaiwuFinder.SearchBooks.v1";
    private const string BookHoldingsMethod = "TaiwuFinder.GetBookHoldings.v1";
    private const string PersonMethod = "TaiwuFinder.SearchPeople.v1";
    private const string MerchantMethod = "TaiwuFinder.SearchMerchants.v1";

    internal static void GetCatalog(Action<FinderCatalogView> callback) =>
        Call(CatalogMethod, new SerializableModData(), data =>
        {
            bool success = Get(data, "Success", false);
            int count = Get(data, "AreaCount", 0);
            var areas = new List<AreaOptionView>(count);
            for (int i = 0; i < count; i++)
                areas.Add(new AreaOptionView(
                    checked((short)Get(data, $"AreaId{i}", -1)),
                    Get(data, $"AreaName{i}", string.Empty),
                    checked((sbyte)Get(data, $"AreaCategory{i}", -1)),
                    checked((sbyte)Get(data, $"AreaState{i}", -1))));
            callback(new FinderCatalogView(success, Get(data, "Message", string.Empty),
                Get(data, "ApiVersion", 0),
                checked((short)Get(data, "CurrentAreaId", -1)),
                // 0 = older backend without date support: month-invalidation is skipped.
                Get(data, "DateTick", 0), areas));
        });

    internal static void SearchBooks(BookSearchRequestView request, Action<BookSearchResponse> callback)
    {
        var parameter = new SerializableModData();
        parameter.Set("AreaId", (int)request.AreaId);
        parameter.Set("Kind", (int)request.Kind);
        parameter.Set("SkillTemplateId", (int)request.SkillTemplateId);
        parameter.Set("Sources", (int)request.Sources);
        parameter.Set("PageCount", request.Pages.Count);
        parameter.Set("Page", request.Page);
        parameter.Set("PageSize", request.PageSize);
        for (int i = 0; i < request.Pages.Count; i++)
        {
            parameter.Set($"PageState{i}", (int)request.Pages[i].State);
            parameter.Set($"PageType{i}", (int)request.Pages[i].Type);
        }
        Call(BookMethod, parameter, data => callback(ParseBookResponse(data)));
    }

    internal static void GetBookHoldings(BookHoldingsRequestView request, Action<BookHoldingsResponse> callback)
    {
        var parameter = new SerializableModData();
        parameter.Set("AreaId", (int)request.AreaId);
        parameter.Set("Kind", (int)request.Kind);
        parameter.Set("SkillTemplateId", (int)request.SkillTemplateId);
        parameter.Set("Sources", (int)request.Sources);
        Call(BookHoldingsMethod, parameter, data => callback(ParseBookHoldingsResponse(data)));
    }

    internal static void SearchPeople(PersonSearchRequestView request, Action<PersonSearchResponse> callback)
    {
        var parameter = new SerializableModData();
        parameter.Set("AreaId", (int)request.AreaId);
        parameter.Set("Name", request.Name);
        parameter.Set("GradeMask", request.GradeMask);
        parameter.Set("MinAge", (int)request.MinAge);
        parameter.Set("MaxAge", (int)request.MaxAge);
        parameter.Set("Gender", (int)request.Gender);
        parameter.Set("OrganizationCount", request.Organizations.Count);
        for (int i = 0; i < request.Organizations.Count; i++)
            parameter.Set($"Organization{i}", (int)request.Organizations[i]);
        parameter.Set("AbilityCount", request.Abilities.Count);
        for (int i = 0; i < request.Abilities.Count; i++)
        {
            parameter.Set($"AbilityType{i}", (int)request.Abilities[i].LifeSkillType);
            parameter.Set($"AbilityMetric{i}", (int)request.Abilities[i].Metric);
            parameter.Set($"AbilityMinimum{i}", (int)request.Abilities[i].Minimum);
        }
        parameter.Set("Page", request.Page);
        parameter.Set("PageSize", request.PageSize);
        Call(PersonMethod, parameter, data => callback(ParsePersonResponse(data)));
    }

    internal static void SearchMerchants(MerchantSearchRequestView request, Action<MerchantSearchResponse> callback)
    {
        var parameter = new SerializableModData();
        parameter.Set("AreaId", (int)request.AreaId);
        parameter.Set("TargetTypeMask", request.TargetTypeMask);
        parameter.Set("GuildTypeMask", request.GuildTypeMask);
        parameter.Set("LevelMask", request.LevelMask);
        parameter.Set("CaravanState", (int)request.CaravanState);
        parameter.Set("Page", request.Page);
        parameter.Set("PageSize", request.PageSize);
        Call(MerchantMethod, parameter, data => callback(ParseMerchantResponse(data)));
    }

    private static BookSearchResponse ParseBookResponse(SerializableModData data)
    {
        int count = Get(data, "CombinationCount", 0);
        var combinations = new List<BookCombinationView>(count);
        for (int i = 0; i < count; i++)
        {
            int contributionCount = Get(data, $"ContributionCount{i}", 0);
            var contributions = new List<BookContributionView>(contributionCount);
            for (int j = 0; j < contributionCount; j++)
            {
                string prefix = $"C{i}_{j}";
                int bookCount = Get(data, prefix + "BookCount", 0);
                var books = new List<BookCopyView>(bookCount);
                for (int k = 0; k < bookCount; k++)
                {
                    string bookPrefix = prefix + $"B{k}";
                    books.Add(new BookCopyView(
                        Get(data, bookPrefix + "Id", string.Empty),
                        checked((byte)Get(data, bookPrefix + "Source", 0)),
                        checked((byte)Get(data, bookPrefix + "PageTypes", 0)),
                        checked((ushort)Get(data, bookPrefix + "PageStates", 0)),
                        Get(data, bookPrefix + "Coverage", 0)));
                }
                contributions.Add(new BookContributionView(
                    Get(data, prefix + "CharacterId", -1),
                    Get(data, prefix + "Name", string.Empty),
                    checked((short)Get(data, prefix + "AreaId", -1)),
                    checked((short)Get(data, prefix + "BlockId", -1)),
                    Get(data, prefix + "Organization", string.Empty),
                    checked((sbyte)Get(data, prefix + "Grade", 0)),
                    Get(data, prefix + "Coverage", 0), books));
            }
            combinations.Add(new BookCombinationView(
                Get(data, $"CombinationKey{i}", i.ToString()),
                Get(data, $"CombinationBookCount{i}", 0),
                Get(data, $"CombinationPrivateCount{i}", 0), contributions));
        }
        return new BookSearchResponse(
            Get(data, "Success", false), Get(data, "Message", string.Empty),
            Get(data, "BookName", string.Empty), Get(data, "HolderCount", 0),
            Get(data, "TotalCount", 0), Get(data, "Page", 0), Get(data, "PageSize", 50),
            Get(data, "MissingMask", 0), Get(data, "ElapsedMs", 0), combinations);
    }

    private static BookHoldingsResponse ParseBookHoldingsResponse(SerializableModData data)
    {
        int count = Get(data, "HolderCount", 0);
        var holders = new List<BookHolderView>(count);
        for (int i = 0; i < count; i++)
        {
            string prefix = $"H{i}_";
            int bookCount = Get(data, prefix + "BookCount", 0);
            var books = new List<BookCopyView>(bookCount);
            for (int j = 0; j < bookCount; j++)
            {
                string bookPrefix = prefix + $"B{j}";
                books.Add(new BookCopyView(
                    Get(data, bookPrefix + "Id", string.Empty),
                    checked((byte)Get(data, bookPrefix + "Source", 0)),
                    checked((byte)Get(data, bookPrefix + "PageTypes", 0)),
                    checked((ushort)Get(data, bookPrefix + "PageStates", 0)),
                    Get(data, bookPrefix + "Coverage", 0)));
            }
            holders.Add(new BookHolderView(
                Get(data, prefix + "CharacterId", -1), Get(data, prefix + "Name", string.Empty),
                checked((short)Get(data, prefix + "AreaId", -1)), checked((short)Get(data, prefix + "BlockId", -1)),
                Get(data, prefix + "Organization", string.Empty), checked((sbyte)Get(data, prefix + "Grade", 0)), books));
        }
        return new BookHoldingsResponse(
            Get(data, "Success", false), Get(data, "Message", string.Empty),
            Get(data, "BookName", string.Empty), Get(data, "ElapsedMs", 0), holders);
    }

    private static PersonSearchResponse ParsePersonResponse(SerializableModData data)
    {
        int count = Get(data, "Count", 0);
        var people = new List<PersonRowView>(count);
        for (int i = 0; i < count; i++)
        {
            int abilityCount = Get(data, $"AbilityCount{i}", 0);
            var abilities = new List<AbilityValueView>(abilityCount);
            for (int j = 0; j < abilityCount; j++)
            {
                string prefix = $"A{i}_{j}";
                abilities.Add(new AbilityValueView(
                    checked((sbyte)Get(data, prefix + "Type", -1)),
                    checked((sbyte)Get(data, prefix + "Metric", -1)),
                    checked((short)Get(data, prefix + "Total", 0)),
                    checked((short)Get(data, prefix + "Base", 0)),
                    checked((sbyte)Get(data, prefix + "GrowthAdjust", 0)),
                    checked((sbyte)Get(data, prefix + "GrowthType", 0))));
            }
            people.Add(new PersonRowView(
                Get(data, $"CharacterId{i}", -1), Get(data, $"Name{i}", string.Empty),
                checked((short)Get(data, $"AreaId{i}", -1)), checked((short)Get(data, $"BlockId{i}", -1)),
                Get(data, $"Organization{i}", string.Empty), checked((sbyte)Get(data, $"OrganizationId{i}", -1)),
                checked((sbyte)Get(data, $"Grade{i}", 0)), checked((short)Get(data, $"Age{i}", 0)),
                checked((sbyte)Get(data, $"Gender{i}", 0)), abilities));
        }
        return new PersonSearchResponse(
            Get(data, "Success", false), Get(data, "Message", string.Empty),
            Get(data, "TotalCount", 0), Get(data, "Page", 0), Get(data, "PageSize", 100),
            Get(data, "ElapsedMs", 0), people);
    }

    private static MerchantSearchResponse ParseMerchantResponse(SerializableModData data)
    {
        int count = Get(data, "Count", 0);
        var rows = new List<MerchantRowView>(count);
        for (int i = 0; i < count; i++)
            rows.Add(new MerchantRowView(
                checked((sbyte)Get(data, $"TargetType{i}", -1)), Get(data, $"EntityId{i}", -1),
                Get(data, $"Name{i}", string.Empty), checked((short)Get(data, $"AreaId{i}", -1)),
                checked((short)Get(data, $"BlockId{i}", -1)), checked((sbyte)Get(data, $"GuildType{i}", -1)),
                Get(data, $"GuildName{i}", string.Empty), checked((sbyte)Get(data, $"Level{i}", 0)),
                Get(data, $"Robbed{i}", false)));
        return new MerchantSearchResponse(
            Get(data, "Success", false), Get(data, "Message", string.Empty),
            Get(data, "TotalCount", 0), Get(data, "Page", 0), Get(data, "PageSize", 100),
            Get(data, "ElapsedMs", 0), rows);
    }

    private static void Call(string method, SerializableModData parameter, Action<SerializableModData> callback) =>
        ModDomainMethod.AsyncCall.CallModMethodWithParamAndRet(null, FrontendPlugin.ModId, method, parameter,
            (offset, pool) => callback(Deserialize(offset, pool)));

    private static SerializableModData Deserialize(int offset, RawDataPool pool)
    {
        var data = new SerializableModData();
        SerializerHolder<SerializableModData>.Deserialize(pool, offset, ref data);
        return data;
    }

    private static int Get(SerializableModData data, string key, int fallback) =>
        data.Get(key, out int value) ? value : fallback;
    private static bool Get(SerializableModData data, string key, bool fallback) =>
        data.Get(key, out bool value) ? value : fallback;
    private static string Get(SerializableModData data, string key, string fallback) =>
        data.Get(key, out string value) ? value : fallback;
}
