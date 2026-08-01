namespace LiverFriendlyInteractions.Backend;

internal static class InteractionHubGroupingPolicy
{
    internal static bool IsBlockMerchant(int organizationTemplateId, int grade,
        int physiologicalAge, int identityActiveAge) =>
        IsMerchantOrganization(organizationTemplateId) &&
        grade == 4 &&
        physiologicalAge >= identityActiveAge;

    internal static bool ShouldIncludeTeammate(int characterId, int taiwuCharacterId, int aliveState) =>
        characterId != taiwuCharacterId && aliveState == 0;

    private static bool IsMerchantOrganization(int organizationTemplateId) =>
        organizationTemplateId == 16 ||
        organizationTemplateId is >= 21 and <= 35 ||
        organizationTemplateId is >= 36 and <= 38;
}
