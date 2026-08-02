namespace LiverFriendlyInteractions.Frontend;

internal static class InteractionHubTargetPolicy
{
    internal static bool ShouldAutoMeet(InteractionPersonKind kind,
        InteractionPersonGroup sourceGroup) =>
        kind == InteractionPersonKind.Character &&
        sourceGroup == InteractionPersonGroup.CurrentBlock;

    internal static InteractionPersonGroup? ResolveGroup(
        int targetId,
        InteractionPersonKind kind,
        InteractionPersonGroup preferredGroup,
        IReadOnlyList<InteractionPersonView> currentBlock,
        IReadOnlyList<InteractionPersonView> teammates,
        IReadOnlyList<InteractionPersonView> merchants)
    {
        foreach (InteractionPersonGroup group in PreferredOrder(preferredGroup))
        {
            IReadOnlyList<InteractionPersonView> people = group switch
            {
                InteractionPersonGroup.CurrentBlock => currentBlock,
                InteractionPersonGroup.Teammate => teammates,
                InteractionPersonGroup.Merchant => merchants,
                _ => Array.Empty<InteractionPersonView>(),
            };
            if (people.Any(person => person.TargetId == targetId && person.Kind == kind))
                return group;
        }
        return null;
    }

    private static IEnumerable<InteractionPersonGroup> PreferredOrder(
        InteractionPersonGroup preferredGroup)
    {
        yield return preferredGroup;
        foreach (InteractionPersonGroup group in new[]
                 {
                     InteractionPersonGroup.CurrentBlock,
                     InteractionPersonGroup.Teammate,
                     InteractionPersonGroup.Merchant,
                 })
        {
            if (group != preferredGroup) yield return group;
        }
    }
}
