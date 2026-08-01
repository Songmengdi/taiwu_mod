using Config;
using GameData.Common;
using GameData.Domains;
using GameData.Domains.Character.Display;
using GameData.Domains.Map;
using GameData.Domains.Merchant;
using GameData.Domains.Taiwu;

namespace LiverFriendlyInteractions.Backend;

internal sealed record HubOption(short TemplateId, bool Available);
internal sealed record HubPerson(int TargetId, bool IsCaravan, string DisplayData,
    int NoInteractionReason, IReadOnlyList<HubOption> Options);
internal sealed record HubCatalog(short TemplateId, string Name, int InteractionType,
    int ActionPointCost, int SpiritualDebtCost);
internal sealed record HubSnapshot(IReadOnlyList<HubPerson> CurrentBlock,
    IReadOnlyList<HubPerson> Teammates, IReadOnlyList<HubPerson> Merchants,
    IReadOnlyList<HubCatalog> Catalog);

internal static class InteractionHubService
{
    internal static HubSnapshot GetSnapshot(DataContext context)
    {
        Location location = DomainManager.Taiwu.GetTaiwu().GetLocation();
        MapBlockData block = DomainManager.Map.GetBlockData(location.AreaId, location.BlockId);
        MapBlockCharacterList blockCharacters = DomainManager.Map.RequestMapBlockCharacterList(context, block);

        var current = new List<HubPerson>();
        var merchants = new List<HubPerson>();
        AddMapPeople(current, merchants, blockCharacters.SpecialCharacters);
        AddMapPeople(current, merchants, blockCharacters.NormalCharacters);
        foreach (CaravanDisplayData caravan in blockCharacters.Caravans ?? Enumerable.Empty<CaravanDisplayData>())
            merchants.Add(ToCaravan(caravan));

        int taiwuCharacterId = DomainManager.Taiwu.GetTaiwuCharId();
        var teammates = new List<HubPerson>();
        foreach (int characterId in DomainManager.Taiwu.GetGroupCharIds().GetCollection())
        {
            CharacterDisplayData display = DomainManager.Character.GetCharacterDisplayData(characterId);
            if (!InteractionHubGroupingPolicy.ShouldIncludeTeammate(
                    characterId, taiwuCharacterId, display.AliveState))
                continue;
            var visible = DomainManager.TaiwuEvent.GetVisibleCharacterInteractionEventOptions(characterId);
            teammates.Add(ToPerson(display, visible.dict, visible.NoInteractionReason));
        }

        var catalog = InteractionEventOption.Instance
            .Where(item => item.InteractionType != EInteractionEventOptionInteractionType.Invalid)
            .Select(item => new HubCatalog(item.TemplateId, item.Name, (int)item.InteractionType,
                item.ActionPointCost, item.SpiritualDebtCost))
            .ToArray();
        return new HubSnapshot(current, teammates, merchants, catalog);
    }

    private static void AddMapPeople(List<HubPerson> people, List<HubPerson> merchants,
        IEnumerable<CharacterDisplayData>? displays)
    {
        if (displays == null) return;
        foreach (CharacterDisplayData display in displays)
        {
            HubPerson person = ToPerson(display,
                display.VisibleCharacterInteractionEventOptionDict, display.NoInteractionReason);
            (IsBlockMerchant(display) ? merchants : people).Add(person);
        }
    }

    private static bool IsBlockMerchant(CharacterDisplayData display)
    {
        int organizationTemplateId = display.OrgInfo.OrgTemplateId;
        var organization = Organization.Instance[organizationTemplateId];
        if (organization?.Members == null)
            return false;
        var identity = OrganizationMember.Instance[organization.Members[4]];
        return identity != null && InteractionHubGroupingPolicy.IsBlockMerchant(
            organizationTemplateId, display.OrgInfo.Grade,
            display.PhysiologicalAge, identity.IdentityActiveAge);
    }

    private static HubPerson ToPerson(CharacterDisplayData display,
        Dictionary<short, bool>? visible, int noInteractionReason)
    {
        var options = visible?.Select(pair => new HubOption(pair.Key, pair.Value)).ToArray()
            ?? Array.Empty<HubOption>();
        return new HubPerson(display.CharacterId, false, Serialize(display), noInteractionReason, options);
    }

    private static HubPerson ToCaravan(CaravanDisplayData display) =>
        new(display.CaravanId, true, Serialize(display), 0, Array.Empty<HubOption>());

    private static unsafe string Serialize(CharacterDisplayData display)
    {
        byte[] bytes = new byte[display.GetSerializedSize()];
        fixed (byte* pointer = bytes)
            display.Serialize(pointer);
        return Convert.ToBase64String(bytes);
    }

    private static unsafe string Serialize(CaravanDisplayData display)
    {
        byte[] bytes = new byte[display.GetSerializedSize()];
        fixed (byte* pointer = bytes)
            display.Serialize(pointer);
        return Convert.ToBase64String(bytes);
    }
}
