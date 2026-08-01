namespace LiverFriendlyInteractions.Frontend;

internal enum InteractionPersonGroup : byte
{
    CurrentBlock,
    Teammate,
    Merchant,
}

internal enum InteractionPersonKind : byte
{
    Character,
    Caravan,
}

internal enum InteractionTab : byte
{
    Favorite,
    Other,
    Unavailable,
}

internal sealed record InteractionOptionView(
    short TemplateId,
    string PreferenceKey,
    string Name,
    bool Available,
    int NativeOrder,
    int ActionPointCost,
    int SpiritualDebtCost,
    string UnavailableReason = "");

internal sealed record InteractionPersonView(
    int TargetId,
    InteractionPersonGroup Group,
    InteractionPersonKind Kind,
    string DisplayData,
    IReadOnlyList<InteractionOptionView> Options,
    int NoInteractionReason);

internal sealed record InteractionCatalogItem(
    string PreferenceKey,
    string Name,
    int NativeOrder,
    int ActionPointCost,
    int SpiritualDebtCost);

internal sealed record InteractionHubSnapshot(
    bool Success,
    string Message,
    IReadOnlyList<InteractionPersonView> CurrentBlock,
    IReadOnlyList<InteractionPersonView> Teammates,
    IReadOnlyList<InteractionPersonView> Merchants,
    IReadOnlyList<InteractionCatalogItem> Catalog);
