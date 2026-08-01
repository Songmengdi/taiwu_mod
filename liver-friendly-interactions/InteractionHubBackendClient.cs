using GameData.Domains.Mod;
using GameData.Serializer;
using GameData.Utilities;

namespace LiverFriendlyInteractions.Frontend;

internal static class InteractionHubBackendClient
{
    private const string SnapshotMethod = "LiverFriendlyInteractions.InteractionHub.Snapshot.v2";
    private const string BeginMethod = "LiverFriendlyInteractions.InteractionHub.Begin.v2";

    internal static void GetSnapshot(Action<InteractionHubSnapshot> callback) =>
        Call(SnapshotMethod, new SerializableModData(), data => callback(ParseSnapshot(data)));

    internal static void Begin(int characterId, bool isCaravan, string action, short templateId,
        Action<bool, string> callback)
    {
        var parameter = new SerializableModData();
        parameter.Set("CharacterId", characterId);
        parameter.Set("IsCaravan", isCaravan);
        parameter.Set("Action", action);
        parameter.Set("TemplateId", (int)templateId);
        Call(BeginMethod, parameter, data => callback(
            Get(data, "Success", false) && Get(data, "Started", false),
            Get(data, "Message", string.Empty)));
    }

    private static InteractionHubSnapshot ParseSnapshot(SerializableModData data)
    {
        bool success = Get(data, "Success", false);
        string message = Get(data, "Message", string.Empty);
        var rawCatalog = new List<(short Id, string Name, int Order, int ActionCost, int DebtCost)>();
        int catalogCount = Get(data, "CatalogCount", 0);
        for (int i = 0; i < catalogCount; i++)
        {
            string prefix = "K" + i;
            short id = checked((short)Get(data, prefix + "Id", -1));
            rawCatalog.Add((id, InteractionHubPolicy.DisplayName(id, Get(data, prefix + "Name", "交互")),
                i, Get(data, prefix + "ActionCost", 0), Get(data, prefix + "DebtCost", 0)));
        }

        var familyById = new Dictionary<short, (string Key, string Name, int Order, int ActionCost, int DebtCost)>();
        foreach (IGrouping<string, (short Id, string Name, int Order, int ActionCost, int DebtCost)> family in
                 rawCatalog.GroupBy(item => item.Name, StringComparer.Ordinal))
        {
            var first = family.OrderBy(item => item.Id).First();
            string key = "interaction:" + first.Id;
            foreach (var item in family)
                familyById[item.Id] = (key, first.Name, family.Min(value => value.Order),
                    item.ActionCost, item.DebtCost);
        }

        var catalog = familyById.Values.GroupBy(item => item.Key, StringComparer.Ordinal).Select(group => group.First())
            .Select(item => new InteractionCatalogItem(item.Key, item.Name, item.Order,
                item.ActionCost, item.DebtCost))
            .OrderBy(item => item.NativeOrder)
            .ToList();
        catalog.Insert(0, new InteractionCatalogItem(InteractionHubPolicy.ShowCharacterKey,
            "显示人物", -2, 0, 0));
        catalog.Add(new InteractionCatalogItem(InteractionHubPolicy.ExchangeItemsKey,
            "交换物品", int.MaxValue - 1, 0, 0));

        IReadOnlyList<InteractionPersonView> map = ParsePeople(data, "M", "MapCount",
            InteractionPersonGroup.CurrentBlock, familyById);
        IReadOnlyList<InteractionPersonView> teammates = ParsePeople(data, "T", "TeammateCount",
            InteractionPersonGroup.Teammate, familyById);
        IReadOnlyList<InteractionPersonView> merchants = ParsePeople(data, "R", "MerchantCount",
            InteractionPersonGroup.Merchant, familyById);
        return new InteractionHubSnapshot(success, message, map, teammates, merchants, catalog);
    }

    private static IReadOnlyList<InteractionPersonView> ParsePeople(
        SerializableModData data,
        string prefixBase,
        string countKey,
        InteractionPersonGroup group,
        IReadOnlyDictionary<short, (string Key, string Name, int Order, int ActionCost, int DebtCost)> catalog)
    {
        var people = new List<InteractionPersonView>();
        int count = Get(data, countKey, 0);
        for (int i = 0; i < count; i++)
        {
            string prefix = prefixBase + i;
            int noInteractionReason = Get(data, prefix + "Reason", 0);
            bool isCaravan = Get(data, prefix + "Caravan", false);
            var raw = new List<(short Id, bool Available)>();
            int optionCount = Get(data, prefix + "OptionCount", 0);
            for (int j = 0; j < optionCount; j++)
                raw.Add((checked((short)Get(data, prefix + "O" + j + "Id", -1)),
                    Get(data, prefix + "O" + j + "Available", false)));

            var options = raw.Where(item => catalog.ContainsKey(item.Id))
                .GroupBy(item => catalog[item.Id].Key, StringComparer.Ordinal)
                .Select(family =>
                {
                    var selected = family.OrderByDescending(item => item.Available).ThenBy(item => item.Id).First();
                    var meta = catalog[selected.Id];
                    return new InteractionOptionView(selected.Id, meta.Key, meta.Name,
                        family.Any(item => item.Available), meta.Order, meta.ActionCost, meta.DebtCost,
                        "当前条件不满足");
                })
                .ToList();

            options.Insert(0, new InteractionOptionView(-1, InteractionHubPolicy.ShowCharacterKey,
                "显示人物", true, -2, 0, 0));
            if (isCaravan)
            {
                options.Clear();
                options.Add(new InteractionOptionView(-1, InteractionHubPolicy.InteractCaravanKey,
                    "交互商队", true, -2, 0, 0));
            }
            else if (group == InteractionPersonGroup.Teammate)
                options.Add(new InteractionOptionView(-1, InteractionHubPolicy.ExchangeItemsKey,
                    "交换物品", true, int.MaxValue - 1, 0, 0));
            if (noInteractionReason == 1)
                options.Add(new InteractionOptionView(-1, InteractionHubPolicy.MeetCharacterKey,
                    "结识", true, -1, 0, 0));
            else if (noInteractionReason == 2)
                options.Add(new InteractionOptionView(-1, InteractionHubPolicy.SpecialInteractionKey,
                    "特殊互动", true, -1, 0, 0));

            people.Add(new InteractionPersonView(Get(data, prefix + "Id", -1), group,
                isCaravan ? InteractionPersonKind.Caravan : InteractionPersonKind.Character,
                Get(data, prefix + "Display", string.Empty), options, noInteractionReason));
        }
        return people;
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
