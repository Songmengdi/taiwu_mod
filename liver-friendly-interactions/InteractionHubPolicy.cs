namespace LiverFriendlyInteractions.Frontend;

internal static class InteractionHubPolicy
{
    internal const string ShowCharacterKey = "builtin:show-character";
    internal const string BrowseGoodsKey = "interaction:57";
    internal const string InteractCaravanKey = "builtin:interact-caravan";
    internal const string ExchangeItemsKey = "builtin:exchange-items";
    internal const string MeetCharacterKey = "builtin:meet-character";
    internal const string SpecialInteractionKey = "builtin:special-interaction";

    internal static readonly string[] DefaultFavorites =
    {
        ShowCharacterKey,
        InteractCaravanKey,
        "interaction:57", // 浏览货物
        "interaction:21", // 交换私人藏书
        "interaction:6",  // 赠送礼物
        "interaction:30", // 知心而交
        "interaction:86", // 慧眼识珠
        ExchangeItemsKey,
    };

    internal static IReadOnlyList<InteractionOptionView> Select(
        IReadOnlyList<InteractionOptionView> options,
        IReadOnlyList<string> favorites,
        InteractionTab tab)
    {
        var ranks = favorites.Select((key, index) => (key, index))
            .ToDictionary(item => item.key, item => item.index, StringComparer.Ordinal);

        IEnumerable<InteractionOptionView> selected = tab switch
        {
            InteractionTab.Favorite => options.Where(item => item.Available && ranks.ContainsKey(item.PreferenceKey))
                .OrderBy(item => ranks[item.PreferenceKey]),
            InteractionTab.Other => options.Where(item => item.Available && !ranks.ContainsKey(item.PreferenceKey))
                .OrderBy(item => item.NativeOrder),
            InteractionTab.Unavailable => options.Where(item => !item.Available)
                .OrderBy(item => ranks.TryGetValue(item.PreferenceKey, out int rank) ? rank : int.MaxValue)
                .ThenBy(item => item.NativeOrder),
            _ => Array.Empty<InteractionOptionView>(),
        };
        return selected.ToArray();
    }

    internal static string DisplayName(short templateId, string nativeName) => templateId switch
    {
        21 => "交换私人藏书",
        _ => nativeName,
    };
}
