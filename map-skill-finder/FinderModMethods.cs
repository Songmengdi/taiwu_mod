using GameData.Common;
using GameData.Domains;
using GameData.Domains.Mod;
using GameData.Serializer;
using MapSkillFinder.Domain;

namespace MapSkillFinder.Backend;

internal static class FinderModMethods
{
    internal const string CatalogMethod = "TaiwuFinder.GetCatalog.v1";
    internal const string BookMethod = "TaiwuFinder.SearchBooks.v1";
    internal const string BookHoldingsMethod = "TaiwuFinder.GetBookHoldings.v1";
    internal const string PersonMethod = "TaiwuFinder.SearchPeople.v1";
    internal const string MerchantMethod = "TaiwuFinder.SearchMerchants.v1";
    internal const string RenxiaMethod = "TaiwuFinder.SearchRenxia.v1";

    internal static void Register(string modId)
    {
        Register(modId, CatalogMethod, CreateCallback(nameof(GetCatalog)));
        Register(modId, BookMethod, CreateCallback(nameof(SearchBooks)));
        Register(modId, BookHoldingsMethod, CreateCallback(nameof(GetBookHoldings)));
        Register(modId, PersonMethod, CreateCallback(nameof(SearchPeople)));
        Register(modId, MerchantMethod, CreateCallback(nameof(SearchMerchants)));
        Register(modId, RenxiaMethod, CreateCallback(nameof(SearchRenxia)));
    }

    // Delegate.CreateDelegate avoids compiler-generated method-group/lambda
    // cache classes. Those nested cache types cannot reliably resolve the
    // running backend's forwarded DataContext type in a uniquely hot-loaded DLL.
    private static Func<DataContext, SerializableModData, SerializableModData> CreateCallback(string methodName)
    {
        var method = typeof(FinderModMethods).GetMethod(methodName,
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(FinderModMethods).FullName, methodName);
        return (Func<DataContext, SerializableModData, SerializableModData>)Delegate.CreateDelegate(
            typeof(Func<DataContext, SerializableModData, SerializableModData>), method);
    }

    private static void Register(
        string modId,
        string method,
        Func<DataContext, SerializableModData, SerializableModData> callback) =>
        DomainManager.Mod.AddModMethod(modId, method, callback);

    private static SerializableModData GetCatalog(DataContext context, SerializableModData parameter) =>
        Safe("读取地域目录", response =>
        {
            FinderCatalog catalog = MapSkillFinderService.GetCatalog();
            // Frontend features are introduced independently from the long-lived backend process.
            // A missing value in an older backend is read as 0 by the client, so it can ask for a restart
            // before attempting an unregistered method.
            response.Set("ApiVersion", 3);
            response.Set("CurrentAreaId", (int)catalog.CurrentAreaId);
            response.Set("DateTick", catalog.DateTick);
            response.Set("AreaCount", catalog.Areas.Count);
            for (int i = 0; i < catalog.Areas.Count; i++)
            {
                AreaOption area = catalog.Areas[i];
                response.Set($"AreaId{i}", (int)area.AreaId);
                response.Set($"AreaName{i}", area.Name);
                response.Set($"AreaCategory{i}", (int)area.Category);
                response.Set($"AreaState{i}", (int)area.StateId);
            }
        });

    private static SerializableModData SearchBooks(DataContext context, SerializableModData data) =>
        Safe("查询书籍", response =>
        {
            int pageCount = Required(data, "PageCount");
            var pages = new List<PageRequirement>(pageCount);
            for (int i = 0; i < pageCount; i++)
                pages.Add(new PageRequirement(
                    checked((sbyte)Get(data, $"PageState{i}", -1)),
                    checked((sbyte)Get(data, $"PageType{i}", -1))));
            var request = new BookSearchRequest(
                checked((short)Required(data, "AreaId")),
                checked((SearchBookKind)(sbyte)Required(data, "Kind")),
                checked((short)Required(data, "SkillTemplateId")),
                checked((BookSource)(byte)Get(data, "Sources", (int)BookSource.All)),
                pages,
                Get(data, "Page", 0),
                Get(data, "PageSize", 50));
            BookSearchResult result = MapSkillFinderService.SearchBooks(context, request);
            response.Set("BookName", result.BookName);
            response.Set("HolderCount", result.HolderCount);
            response.Set("TotalCount", result.TotalCount);
            response.Set("Page", result.Page);
            response.Set("PageSize", result.PageSize);
            response.Set("MissingMask", checked((int)result.MissingMask));
            response.Set("ElapsedMs", checked((int)result.ElapsedMilliseconds));
            response.Set("CombinationCount", result.Combinations.Count);
            for (int i = 0; i < result.Combinations.Count; i++)
                WriteCombination(response, i, result.Combinations[i], result.Holders);
        });

    private static SerializableModData GetBookHoldings(DataContext context, SerializableModData data) =>
        Safe("读取秘籍持有情况", response =>
        {
            var request = new BookHoldingsRequest(
                checked((short)Required(data, "AreaId")),
                checked((SearchBookKind)(sbyte)Required(data, "Kind")),
                checked((short)Required(data, "SkillTemplateId")),
                checked((BookSource)(byte)Get(data, "Sources", (int)BookSource.All)));
            BookHoldingsResult result = MapSkillFinderService.GetBookHoldings(context, request);
            response.Set("BookName", result.BookName);
            response.Set("ElapsedMs", checked((int)result.ElapsedMilliseconds));
            response.Set("HolderCount", result.Holders.Count);
            for (int i = 0; i < result.Holders.Count; i++)
                WriteHolder(response, $"H{i}_", result.Holders[i]);
        });

    private static void WriteCombination(
        SerializableModData response,
        int index,
        BookCombination combination,
        IReadOnlyDictionary<int, BookHolderCandidate> holders)
    {
        response.Set($"CombinationKey{index}", combination.StableKey);
        response.Set($"CombinationBookCount{index}", combination.BookCount);
        response.Set($"CombinationPrivateCount{index}", combination.PrivateBookCount);
        response.Set($"ContributionCount{index}", combination.Contributions.Count);
        for (int j = 0; j < combination.Contributions.Count; j++)
        {
            BookContribution contribution = combination.Contributions[j];
            BookHolderCandidate holder = holders[contribution.CharacterId];
            string prefix = $"C{index}_{j}";
            response.Set(prefix + "CharacterId", contribution.CharacterId);
            response.Set(prefix + "Name", contribution.HolderName);
            response.Set(prefix + "AreaId", (int)holder.AreaId);
            response.Set(prefix + "BlockId", (int)holder.BlockId);
            response.Set(prefix + "Organization", holder.Organization);
            response.Set(prefix + "Grade", (int)holder.Grade);
            response.Set(prefix + "Coverage", checked((int)contribution.CoverageMask));
            response.Set(prefix + "BookCount", contribution.Books.Count);
            for (int k = 0; k < contribution.Books.Count; k++)
            {
                BookCopyCandidate book = contribution.Books[k];
                string bookPrefix = prefix + $"B{k}";
                response.Set(bookPrefix + "Id", book.CopyId);
                response.Set(bookPrefix + "Source", (int)book.Source);
                response.Set(bookPrefix + "PageTypes", (int)book.PageTypes);
                response.Set(bookPrefix + "PageStates", (int)book.PageStates);
                response.Set(bookPrefix + "Coverage", checked((int)book.CoverageMask));
            }
        }
    }

    private static void WriteHolder(SerializableModData response, string prefix, BookHolderCandidate holder)
    {
        response.Set(prefix + "CharacterId", holder.CharacterId);
        response.Set(prefix + "Name", holder.Name);
        response.Set(prefix + "AreaId", (int)holder.AreaId);
        response.Set(prefix + "BlockId", (int)holder.BlockId);
        response.Set(prefix + "Organization", holder.Organization);
        response.Set(prefix + "Grade", (int)holder.Grade);
        response.Set(prefix + "BookCount", holder.Books.Count);
        for (int k = 0; k < holder.Books.Count; k++)
        {
            BookCopyCandidate book = holder.Books[k];
            string bookPrefix = prefix + $"B{k}";
            response.Set(bookPrefix + "Id", book.CopyId);
            response.Set(bookPrefix + "Source", (int)book.Source);
            response.Set(bookPrefix + "PageTypes", (int)book.PageTypes);
            response.Set(bookPrefix + "PageStates", (int)book.PageStates);
            response.Set(bookPrefix + "Coverage", checked((int)book.CoverageMask));
        }
    }

    private static SerializableModData SearchPeople(DataContext context, SerializableModData data) =>
        Safe("查询人物", response =>
        {
            int organizationCount = Get(data, "OrganizationCount", 0);
            var organizations = new List<sbyte>(organizationCount);
            for (int i = 0; i < organizationCount; i++)
                organizations.Add(checked((sbyte)Required(data, $"Organization{i}")));
            int abilityCount = Get(data, "AbilityCount", 0);
            var abilities = new List<AbilityCondition>(abilityCount);
            for (int i = 0; i < abilityCount; i++)
                abilities.Add(new AbilityCondition(
                    checked((sbyte)Required(data, $"AbilityType{i}")),
                    checked((AbilityMetric)(sbyte)Required(data, $"AbilityMetric{i}")),
                    checked((short)Required(data, $"AbilityMinimum{i}"))));
            var request = new PersonSearchRequest(
                checked((short)Required(data, "AreaId")),
                Get(data, "Name", string.Empty),
                Get(data, "GradeMask", 0),
                checked((short)Get(data, "MinAge", -1)),
                checked((short)Get(data, "MaxAge", -1)),
                checked((sbyte)Get(data, "Gender", -1)),
                organizations,
                abilities,
                Get(data, "Page", 0),
                Get(data, "PageSize", 100));
            PersonSearchResult result = MapSkillFinderService.SearchPeople(request);
            response.Set("TotalCount", result.TotalCount);
            response.Set("Page", result.Page);
            response.Set("PageSize", result.PageSize);
            response.Set("ElapsedMs", checked((int)result.ElapsedMilliseconds));
            response.Set("Count", result.People.Count);
            for (int i = 0; i < result.People.Count; i++)
            {
                PersonSearchRow person = result.People[i];
                response.Set($"CharacterId{i}", person.CharacterId);
                response.Set($"Name{i}", person.Name);
                response.Set($"AreaId{i}", (int)person.AreaId);
                response.Set($"BlockId{i}", (int)person.BlockId);
                response.Set($"Organization{i}", person.Organization);
                response.Set($"OrganizationId{i}", (int)person.OrganizationId);
                response.Set($"Grade{i}", (int)person.Grade);
                response.Set($"Age{i}", (int)person.Age);
                response.Set($"Gender{i}", (int)person.Gender);
                response.Set($"AbilityCount{i}", person.Abilities.Count);
                for (int j = 0; j < person.Abilities.Count; j++)
                {
                    AbilityValue ability = person.Abilities[j];
                    string prefix = $"A{i}_{j}";
                    response.Set(prefix + "Type", (int)ability.LifeSkillType);
                    response.Set(prefix + "Metric", (int)ability.Metric);
                    response.Set(prefix + "Total", (int)ability.Total);
                    response.Set(prefix + "Base", (int)ability.Base);
                    response.Set(prefix + "GrowthAdjust", (int)ability.GrowthAdjust);
                    response.Set(prefix + "GrowthType", (int)ability.GrowthType);
                }
            }
        });

    private static SerializableModData SearchMerchants(DataContext context, SerializableModData data) =>
        Safe("查询商会", response =>
        {
            var request = new MerchantSearchRequest(
                checked((short)Required(data, "AreaId")),
                Get(data, "TargetTypeMask", 0b111),
                Get(data, "GuildTypeMask", 0),
                Get(data, "LevelMask", 0),
                checked((sbyte)Get(data, "CaravanState", 0)),
                Get(data, "Page", 0),
                Get(data, "PageSize", 100));
            MerchantSearchResult result = MapSkillFinderService.SearchMerchants(context, request);
            response.Set("TotalCount", result.TotalCount);
            response.Set("Page", result.Page);
            response.Set("PageSize", result.PageSize);
            response.Set("ElapsedMs", checked((int)result.ElapsedMilliseconds));
            response.Set("Count", result.Rows.Count);
            for (int i = 0; i < result.Rows.Count; i++)
            {
                MerchantSearchRow row = result.Rows[i];
                response.Set($"TargetType{i}", (int)row.TargetType);
                response.Set($"EntityId{i}", row.EntityId);
                response.Set($"Name{i}", row.Name);
                response.Set($"AreaId{i}", (int)row.AreaId);
                response.Set($"BlockId{i}", (int)row.BlockId);
                response.Set($"GuildType{i}", (int)row.GuildType);
                response.Set($"GuildName{i}", row.GuildName);
                response.Set($"Level{i}", (int)row.Level);
                response.Set($"Robbed{i}", row.Robbed);
            }
        });

    private static SerializableModData SearchRenxia(DataContext context, SerializableModData data) =>
        Safe("查询任侠", response =>
        {
            var request = new RenxiaSearchRequest(
                checked((short)Required(data, "AreaId")),
                Get(data, "GradeMask", 0));
            RenxiaSearchResult result = MapSkillFinderService.SearchRenxia(request);
            response.Set("TotalCount", result.TotalCount);
            response.Set("ElapsedMs", checked((int)result.ElapsedMilliseconds));
            response.Set("Count", result.Rows.Count);
            for (int i = 0; i < result.Rows.Count; i++)
            {
                RenxiaSearchRow row = result.Rows[i];
                response.Set($"TemplateId{i}", (int)row.TemplateId);
                response.Set($"Name{i}", row.Name);
                response.Set($"AreaId{i}", (int)row.AreaId);
                response.Set($"BlockId{i}", (int)row.BlockId);
                response.Set($"Grade{i}", (int)row.Grade);
            }
        });

    private static SerializableModData Safe(string operation, Action<SerializableModData> write)
    {
        var response = new SerializableModData();
        try
        {
            write(response);
            response.Set("Success", true);
            response.Set("Message", string.Empty);
        }
        catch (Exception ex)
        {
            response.Set("Success", false);
            response.Set("Message", $"{operation}失败：{ex.Message}");
            response.Set("Count", 0);
            response.Set("TotalCount", 0);
        }
        return response;
    }

    private static int Required(SerializableModData data, string key) =>
        data.Get(key, out int value) ? value : throw new ArgumentException($"缺少参数 {key}。");

    private static int Get(SerializableModData data, string key, int fallback) =>
        data.Get(key, out int value) ? value : fallback;

    private static string Get(SerializableModData data, string key, string fallback) =>
        data.Get(key, out string value) ? value : fallback;
}
