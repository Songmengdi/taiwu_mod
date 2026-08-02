using GameData.Common;
using GameData.Domains;
using GameData.Domains.Mod;
using GameData.Serializer;

namespace LiverFriendlyInteractions.Backend;

internal static class InteractionHubModMethods
{
    internal const string SnapshotMethod = "LiverFriendlyInteractions.InteractionHub.Snapshot.v2";
    internal const string BeginMethod = "LiverFriendlyInteractions.InteractionHub.Begin.v2";
    internal const string MeetMethod = "LiverFriendlyInteractions.InteractionHub.Meet.v1";

    internal static void Register(string modId)
    {
        DomainManager.Mod.AddModMethod(modId, SnapshotMethod, Snapshot);
        DomainManager.Mod.AddModMethod(modId, BeginMethod, Begin);
        DomainManager.Mod.AddModMethod(modId, MeetMethod, Meet);
    }

    internal static void RegisterMeet(string modId) =>
        DomainManager.Mod.AddModMethod(modId, MeetMethod, Meet);

    private static SerializableModData Snapshot(DataContext context, SerializableModData parameter) =>
        Safe(response =>
        {
            HubSnapshot snapshot = InteractionHubService.GetSnapshot(context);
            response.Set("MapCount", snapshot.CurrentBlock.Count);
            for (int i = 0; i < snapshot.CurrentBlock.Count; i++)
                WritePerson(response, "M" + i, snapshot.CurrentBlock[i]);
            response.Set("TeammateCount", snapshot.Teammates.Count);
            for (int i = 0; i < snapshot.Teammates.Count; i++)
                WritePerson(response, "T" + i, snapshot.Teammates[i]);
            response.Set("MerchantCount", snapshot.Merchants.Count);
            for (int i = 0; i < snapshot.Merchants.Count; i++)
                WritePerson(response, "R" + i, snapshot.Merchants[i]);
            response.Set("CatalogCount", snapshot.Catalog.Count);
            for (int i = 0; i < snapshot.Catalog.Count; i++)
            {
                HubCatalog item = snapshot.Catalog[i];
                string prefix = "K" + i;
                response.Set(prefix + "Id", (int)item.TemplateId);
                response.Set(prefix + "Name", item.Name);
                response.Set(prefix + "Type", item.InteractionType);
                response.Set(prefix + "ActionCost", item.ActionPointCost);
                response.Set(prefix + "DebtCost", item.SpiritualDebtCost);
            }
        });

    private static SerializableModData Begin(DataContext context, SerializableModData parameter) =>
        Safe(response =>
        {
            try
            {
                int characterId = Required(parameter, "CharacterId");
                string action = Get(parameter, "Action", string.Empty);
                bool isCaravan = Get(parameter, "IsCaravan", false);
                if (!InteractionHubBeginPolicy.CanStart(InteractionHubReturnSession.Active))
                {
                    response.Set("Started", false);
                    return;
                }
                bool started;
                if (isCaravan && action == "builtin:interact-caravan")
                {
                    InteractionHubReturnSession.Cancel();
                    InteractionHubReturnSession.BeginFromMenu(characterId);
                    DomainManager.TaiwuEvent.OnInteractCaravan(characterId);
                    started = true;
                }
                else if (action == "builtin:meet-character" || action == "builtin:special-interaction")
                {
                    InteractionHubReturnSession.Cancel();
                    InteractionHubReturnSession.BeginFromMenu(characterId);
                    DomainManager.TaiwuEvent.OnCharacterClicked(context, characterId);
                    started = true;
                }
                else if (action.StartsWith("interaction:", StringComparison.Ordinal) &&
                         parameter.Get("TemplateId", out int rawTemplateId) &&
                         rawTemplateId >= short.MinValue && rawTemplateId <= short.MaxValue)
                {
                    short templateId = (short)rawTemplateId;
                    InteractionHubReturnSession.Cancel();
                    bool preserveAcrossHiddenDisplay =
                        InteractionHubReturnPolicy.ShouldPreserveAcrossHiddenDisplay(templateId);
                    if (preserveAcrossHiddenDisplay)
                        InteractionHubReturnSession.BeginDirectStart(characterId, preserveAcrossHiddenDisplay: true);
                    bool nativeStarted = DomainManager.TaiwuEvent
                        .JumpToInteractionEventOptionByInteractionId(characterId, templateId);
                    started = InteractionHubReturnPolicy.DidStartDirectInteraction(
                        templateId, nativeStarted, DomainManager.TaiwuEvent.ShowingEvent?.EventGuid);
                    if (started && preserveAcrossHiddenDisplay)
                        InteractionHubReturnSession.CommitDirectStart();
                    if (!preserveAcrossHiddenDisplay && InteractionHubReturnPolicy.ShouldArmDirectReturn(started,
                            DomainManager.TaiwuEvent.IsShowingEvent,
                            DomainManager.TaiwuEvent.GetHasListeningEvent()))
                        InteractionHubReturnSession.BeginDirect(characterId,
                            preserveAcrossHiddenDisplay: false);
                }
                else
                {
                    started = false;
                }
                if (!started)
                {
                    InteractionHubReturnSession.Cancel();
                }
                response.Set("Started", started);
            }
            catch
            {
                InteractionHubReturnSession.Cancel();
                throw;
            }
        });

    private static SerializableModData Meet(DataContext context, SerializableModData parameter) =>
        Safe(response =>
        {
            int characterId = Required(parameter, "CharacterId");
            int taiwuCharacterId = DomainManager.Taiwu.GetTaiwuCharId();
            bool alreadyMet = DomainManager.Character.TryGetRelation(
                characterId, taiwuCharacterId, out _);
            if (!alreadyMet)
                DomainManager.TaiwuEvent.MeetTaiwu(context, characterId);
            bool met = DomainManager.Character.TryGetRelation(
                characterId, taiwuCharacterId, out _);
            response.Set("AlreadyMet", alreadyMet);
            response.Set("Met", met);
        });

    private static void WritePerson(SerializableModData response, string prefix, HubPerson person)
    {
        response.Set(prefix + "Id", person.TargetId);
        response.Set(prefix + "Caravan", person.IsCaravan);
        response.Set(prefix + "Display", person.DisplayData);
        response.Set(prefix + "Reason", person.NoInteractionReason);
        response.Set(prefix + "OptionCount", person.Options.Count);
        for (int j = 0; j < person.Options.Count; j++)
        {
            response.Set(prefix + "O" + j + "Id", (int)person.Options[j].TemplateId);
            response.Set(prefix + "O" + j + "Available", person.Options[j].Available);
        }
    }

    private static SerializableModData Safe(Action<SerializableModData> write)
    {
        var response = new SerializableModData();
        try
        {
            write(response);
            response.Set("Success", true);
            response.Set("Message", string.Empty);
        }
        catch (Exception exception)
        {
            response.Set("Success", false);
            response.Set("Message", exception.Message);
        }
        return response;
    }

    private static int Required(SerializableModData data, string key) =>
        data.Get(key, out int value) ? value : throw new ArgumentException("缺少参数 " + key);
    private static bool Get(SerializableModData data, string key, bool fallback) =>
        data.Get(key, out bool value) ? value : fallback;
    private static string Get(SerializableModData data, string key, string fallback) =>
        data.Get(key, out string value) ? value : fallback;
}
